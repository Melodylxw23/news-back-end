using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using News_Back_end.Services;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace News_Back_end.Controllers
{
    [ApiController]
    [Route("api/genai")]
    [Route("api/UserControllers/genai")]
    [Authorize(Roles = "Admin")]
    public class GenAiInsightsController : ControllerBase
    {
        private readonly MyDBContext _db;
        private readonly OpenAIChatClient _chat;
        private readonly ILogger<GenAiInsightsController> _logger;

        public GenAiInsightsController(MyDBContext db, OpenAIChatClient chat, ILogger<GenAiInsightsController> logger)
        {
            _db = db;
            _chat = chat;
            _logger = logger;
        }

        // POST api/genai/insights
        [HttpPost("insights")]
        public async Task<IActionResult> GenerateInsights()
        {
            try
            {
                //1) Build aggregates for prompt
                var totalMembers = await _db.Members.CountAsync();
                var interestCounts = await _db.Members
                    .SelectMany(m => m.Interests)
                    .GroupBy(i => i.InterestTagId)
                    .Select(g => new { InterestTagId = g.Key, Count = g.Count() })
                    .ToListAsync();

                var interestMeta = await _db.InterestTags
                    .Where(t => interestCounts.Select(ic => ic.InterestTagId).Contains(t.InterestTagId))
                    .ToDictionaryAsync(t => t.InterestTagId, t => new { t.NameEN, t.NameZH });

                var industryCounts = await _db.Members
                    .SelectMany(m => m.IndustryTags)
                    .GroupBy(i => i.IndustryTagId)
                    .Select(g => new { IndustryTagId = g.Key, Count = g.Count() })
                    .ToListAsync();

                var industryMeta = await _db.IndustryTags
                    .Where(i => industryCounts.Select(ic => ic.IndustryTagId).Contains(i.IndustryTagId))
                    .ToDictionaryAsync(i => i.IndustryTagId, i => new { i.NameEN, i.NameZH });

                // simple interaction/trend: use PublicationDraft.CreatedAt as proxy since ArticleInteractions are not present
                var now = DateTime.UtcNow;
                var last30 = now.AddDays(-30);
                var prev30Start = now.AddDays(-60);

                var drafts = await _db.PublicationDrafts
                    .Where(d => d.NewsArticleId != null)
                    .Include(d => d.InterestTags)
                    .ToListAsync();

                var interestTrend = new Dictionary<int, (int prevCount, int recentCount)>();

                foreach (var d in drafts)
                {
                    var ts = d.CreatedAt;
                    foreach (var tag in d.InterestTags)
                    {
                        if (!interestTrend.ContainsKey(tag.InterestTagId)) interestTrend[tag.InterestTagId] = (0, 0);
                        var cur = interestTrend[tag.InterestTagId];
                        if (ts >= last30) cur.recentCount++;
                        else if (ts >= prev30Start) cur.prevCount++;
                        interestTrend[tag.InterestTagId] = cur;
                    }
                }

                // Compose a compact JSON summary for prompt
                var summary = new
                {
                    TotalMembers = totalMembers,
                    Interests = interestCounts.Select(ic => new
                    {
                        ic.InterestTagId,
                        Name = interestMeta.TryGetValue(ic.InterestTagId, out var im) ? (im.NameEN ?? im.NameZH ?? "") : "",
                        Count = ic.Count,
                        PrevCount = interestTrend.TryGetValue(ic.InterestTagId, out var t) ? t.prevCount : 0,
                        RecentCount = interestTrend.TryGetValue(ic.InterestTagId, out var t2) ? t2.recentCount : 0
                    }),
                    Industries = industryCounts.Select(i => new
                    {
                        i.IndustryTagId,
                        Name = industryMeta.TryGetValue(i.IndustryTagId, out var im) ? (im.NameEN ?? im.NameZH ?? "") : "",
                        Count = i.Count
                    })
                };

                var summaryJson = JsonSerializer.Serialize(summary);

                //2) Build prompt asking for structured JSON response using StringBuilder to avoid verbatim string issues
                var systemPrompt = "You are an analytics assistant that produces concise actionable reports. Output JSON only, without markdown code fences.";
                var sb = new StringBuilder();
                sb.AppendLine("We have the following numeric data about members, their interests and industries (JSON):");
                sb.AppendLine(summaryJson);
                sb.AppendLine();
                sb.AppendLine("Produce a JSON object with keys (exact):");
                sb.AppendLine("- segments: array of {id:int, size:int, percent:float, interests:[string]} (segment membership by interest patterns)");
                sb.AppendLine("- industryGrowthForecast: array of {industry:string, expectedGrowthPercent:int, currentMembers:int, rationale:string}");
                sb.AppendLine("- topicPopularityForecast: array of {topic:string, expectedGrowthPercent:int, currentMembers:int, rationale:string}");
                sb.AppendLine("- suggestedActions: array[string]");
                sb.AppendLine();
                sb.AppendLine("Instructions for suggestions (very important):");
                sb.AppendLine("- Return 4-6 highly specific, short action strings for suggestedActions.");
                sb.AppendLine("- Each action MUST follow this concise template: Action: <what>; Target: <segment/industry/topic>; When: <timeframe>; KPI: <numeric goal>; Rationale: <one-line>.");
                sb.AppendLine("- Use numbers from the provided JSON (TotalMembers, Counts) to set realistic KPIs (e.g., convert counts to percentages or absolute targets).");
                sb.AppendLine("- Keep each suggested action <=200 characters. No extra explanation outside the JSON.");
                sb.AppendLine();
                sb.AppendLine("Use only the numeric inputs provided. Keep outputs concise and factual. Return ONLY valid JSON without markdown code fences (no ```json or ```).");

                var userPrompt = sb.ToString();

                //3) Call OpenAI
                string? aiText;
                try
                {
                    aiText = await _chat.CreateChatCompletionAsync(systemPrompt, userPrompt, maxTokens: 700);
                    _logger.LogInformation("OpenAI Response (raw): {Response}", aiText);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OpenAI API call failed");
                    return StatusCode(502, new { error = "OpenAI call failed", detail = ex.Message });
                }

                if (string.IsNullOrWhiteSpace(aiText))
                {
                    _logger.LogError("OpenAI returned empty response");
                    return StatusCode(502, new { error = "OpenAI returned empty response" });
                }

                //4) Extract JSON from markdown code fences if present
                var cleanedText = aiText.Trim();

                // Check if response has markdown code fences
                if (cleanedText.StartsWith("```"))
                {
                    _logger.LogInformation("Detected markdown code fences, extracting JSON");

                    // Use regex to extract JSON between ```json and ``` or ``` and ```
                    var match = Regex.Match(cleanedText, @"```(?:json)?\s*\n?([\s\S]*?)\n?```", RegexOptions.Singleline);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        cleanedText = match.Groups[1].Value.Trim();
                        _logger.LogInformation("Extracted JSON: {Json}", cleanedText);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to extract JSON from markdown, attempting line-by-line removal");
                        // Fallback: remove first and last lines
                        var lines = cleanedText.Split('\n');
                        if (lines.Length > 2)
                        {
                            cleanedText = string.Join('\n', lines.Skip(1).Take(lines.Length - 2)).Trim();
                        }
                    }
                }

                //5) Parse JSON and return structured response
                try
                {
                    using var doc = JsonDocument.Parse(cleanedText);

                    // Convert JsonElement to a serializable object
                    var parsed = JsonSerializer.Deserialize<object>(cleanedText);

                    _logger.LogInformation("Successfully parsed AI response");
                    return Ok(new { raw = aiText, parsed = parsed });
                }
                catch (JsonException jex)
                {
                    _logger.LogError(jex, "Failed to parse AI response as JSON. Cleaned text: {Text}", cleanedText);
                    return Ok(new { raw = aiText, parsed = (object?)null, error = "JSON parsing failed", cleanedAttempt = cleanedText });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GenerateInsights");
                return StatusCode(500, new { error = "Internal server error", detail = ex.Message });
            }
        }
    }
}
