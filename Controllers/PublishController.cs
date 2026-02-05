using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;
using News_Back_end.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace News_Back_end.Controllers
{
 [ApiController]
 [Route("api/[controller]")]
 public class PublishController : ControllerBase
 {
 private readonly MyDBContext _db;
 private readonly IImageGenerationService? _imageService;
 private readonly IPublicationService _pubService;
 private readonly IWebHostEnvironment _env;
 private readonly OpenAIChatClient _chat;

 public PublishController(MyDBContext db, IPublicationService pubService, IWebHostEnvironment env, IImageGenerationService? imageService = null, OpenAIChatClient? chat = null)
 {
 _db = db;
 _imageService = imageService;
 _pubService = pubService;
 _env = env;
 _chat = chat!; // may be null in some registrations, but controller expects chat when AI used
 }

 // GET /api/publish/{id}
 [HttpGet("{id:int}")]
 [Authorize(Roles = "Consultant")]
 public async Task<IActionResult> GetDraft(int id)
 {
 var article = await _db.NewsArticles.FindAsync(id);
 if (article == null) return NotFound();

 var draft = await _db.PublicationDrafts
 .Include(d => d.IndustryTag)
 .Include(d => d.InterestTags)
 .FirstOrDefaultAsync(d => d.NewsArticleId == id);

 var industries = await _db.IndustryTags.Select(i => new { i.IndustryTagId, i.NameEN, i.NameZH }).ToListAsync();
 var interests = await _db.InterestTags.Select(i => new { i.InterestTagId, i.NameEN, i.NameZH }).ToListAsync();

 return Ok(new { article, draft, industries, interests });
 }

 // PATCH /api/publish/{id}
 [HttpPatch("{id:int}")]
 [Authorize(Roles = "Consultant")]
 public async Task<IActionResult> SaveDraft(int id, [FromBody] PublishArticleDto dto)
 {
 if (id != dto.NewsArticleId) return BadRequest("id mismatch");

 var article = await _db.NewsArticles.FindAsync(id);
 if (article == null) return NotFound();

 var draft = await _db.PublicationDrafts
 .Include(d => d.IndustryTag)
 .Include(d => d.InterestTags)
 .FirstOrDefaultAsync(d => d.NewsArticleId == id);

 if (draft == null)
 {
 draft = new PublicationDraft
 {
 NewsArticleId = id,
 CreatedBy = User?.Identity?.Name ?? "consultant",
 CreatedAt = DateTime.Now
 };
 _db.PublicationDrafts.Add(draft);
 }

 draft.HeroImageUrl = dto.HeroImageUrl;
 draft.HeroImageAlt = dto.HeroImageAlt;
 draft.HeroImageSource = dto.HeroImageSource;
 draft.FullContentEN = dto.FullContentEN;
 draft.FullContentZH = dto.FullContentZH;
 draft.UpdatedAt = DateTime.Now;

 // set single industry tag
 if (dto.IndustryTagId.HasValue)
 {
 var industry = await _db.IndustryTags.FindAsync(dto.IndustryTagId.Value);
 if (industry == null) return BadRequest("invalid industry tag");
 draft.IndustryTag = industry;
 draft.IndustryTagId = industry.IndustryTagId;
 }
 else
 {
 draft.IndustryTag = null;
 draft.IndustryTagId = null;
 }

 // update interest tags many-to-many
 draft.InterestTags.Clear();
 if (dto.InterestTagIds != null && dto.InterestTagIds.Count >0)
 {
 var tags = await _db.InterestTags.Where(t => dto.InterestTagIds.Contains(t.InterestTagId)).ToListAsync();
 foreach (var t in tags) draft.InterestTags.Add(t);
 }

 // update schedule
 draft.ScheduledAt = dto.ScheduledAt;

 await _db.SaveChangesAsync();
 return Ok(new { message = "Draft saved." });
 }

 // POST /api/publish/{id}/publish
 [HttpPost("{id:int}/publish")]
 [Authorize(Roles = "Consultant")]
 public async Task<IActionResult> Publish(int id, [FromBody] PublishActionDto action)
 {
 if (id != action.NewsArticleId) return BadRequest("id mismatch");

 var draft = await _db.PublicationDrafts
 .Include(d => d.IndustryTag)
 .Include(d => d.InterestTags)
 .FirstOrDefaultAsync(d => d.NewsArticleId == id);

 if (draft == null) return BadRequest("No draft to publish.");

 var article = await _db.NewsArticles.FindAsync(id);
 if (article == null) return NotFound();

 if (action.Action == "publish")
 {
 // ensure industry tag exists
 if (draft.IndustryTagId == null) return BadRequest("Industry tag is required.");
 // ensure at least one interest tag
 if (draft.InterestTags == null || !draft.InterestTags.Any()) return BadRequest("At least one interest tag is required.");

 var (ok, err) = await _pubService.PublishDraftAsync(draft, action.ScheduledAt, User?.Identity?.Name ?? "consultant");
 if (!ok) return BadRequest(err);

 return Ok(new { message = "Published." });
 }
 else if (action.Action == "unpublish")
 {
 var (ok, err) = await _pubService.UnpublishDraftAsync(draft, User?.Identity?.Name ?? "consultant");
 if (!ok) return BadRequest(err);
 return Ok(new { message = "Unpublished." });
 }

 return BadRequest("unknown action");
 }
        [HttpPost("{id:int}/generate-hero")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GenerateHero(int id, [FromBody] GenerateHeroImageDto dto)
        {
            var draft = await _db.PublicationDrafts.FirstOrDefaultAsync(d => d.NewsArticleId == id);
            if (draft == null)
            {
                draft = new PublicationDraft { NewsArticleId = id, CreatedAt = DateTime.Now };
                _db.PublicationDrafts.Add(draft);
            }

            if (_imageService == null)
            {
                var placeholder = "/assets/generated/hero_placeholder.svg";
                draft.HeroImageUrl = placeholder;
                draft.HeroImageSource = "generated-placeholder";
                draft.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                return Ok(new { url = placeholder, fallback = true });
            }

            var article = await _db.NewsArticles.FindAsync(id);
            var baseText = article?.TitleEN ?? article?.TitleZH ?? article?.OriginalContent ?? "";
            var prompt = (!string.IsNullOrWhiteSpace(dto?.PromptOverride)) ? dto.PromptOverride!.Trim()
                        : $"Professional hero image for article: {baseText}";

            string? url = null;
            string? lastError = null;
            const int maxAttempts = 3;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    url = await _imageService.GenerateImageAsync(id, prompt, dto?.Style);
                    if (!string.IsNullOrWhiteSpace(url)) break;
                    lastError = $"service returned null/empty (attempt {attempt})";
                    Console.WriteLine(lastError);
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    Console.WriteLine($"GenerateHero attempt {attempt} failed: {ex}");
                }
                if (attempt < maxAttempts) await Task.Delay(1000 * attempt);
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                const string placeholder = "/assets/generated/hero_placeholder.svg";
                draft.HeroImageUrl = placeholder;
                draft.HeroImageSource = "generated-fallback";
                draft.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();

                Console.WriteLine($"GenerateHero: failed after retries for id={id}, reason={lastError}");
                return Ok(new { url = placeholder, fallback = true, reason = lastError });
            }

            try
            {
                if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var comma = url.IndexOf(',');
                    if (comma <= 0) throw new InvalidOperationException("invalid data URI");
                    var meta = url.Substring(5, comma - 5);
                    var base64 = url.Substring(comma + 1);
                    var bytes = Convert.FromBase64String(base64);

                    string ext = "jpg";
                    if (meta.Contains("png")) ext = "png";
                    else if (meta.Contains("gif")) ext = "gif";
                    else if (meta.Contains("jpeg")) ext = "jpg";

                    var fileName = $"hero_{id}.{ext}";
                    var dir = Path.Combine(_env.WebRootPath ?? "wwwroot", "assets", "generated");
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, fileName);
                    await System.IO.File.WriteAllBytesAsync(path, bytes);

                    var savedUrl = $"{Request.Scheme}://{Request.Host}/assets/generated/{fileName}";
                    draft.HeroImageUrl = savedUrl;
                }
                else if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    draft.HeroImageUrl = url;
                }
                else
                {
                    var normalized = url.StartsWith("/") ? url : "/assets/generated/" + url;
                    draft.HeroImageUrl = normalized;
                }

                draft.HeroImageSource = "generated";
                draft.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();

                return Ok(new { url = draft.HeroImageUrl });
            }
            catch (Exception ex)
            {
                Console.WriteLine("GenerateHero save error: " + ex);

                const string placeholder = "/assets/generated/hero_placeholder.svg";
                draft.HeroImageUrl = placeholder;
                draft.HeroImageSource = "generated-error";
                draft.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();

                return StatusCode(502, new { error = "image_processing_failed", reason = ex.Message });
            }
        }

        // GET /api/publish/{id}/preview
        [HttpGet("{id:int}/preview")]
 [Authorize(Roles = "Consultant")]
 public async Task<IActionResult> Preview(int id, [FromQuery] string? lang = null)
 {
 var article = await _db.NewsArticles.FindAsync(id);
 if (article == null) return NotFound();

 var draft = await _db.PublicationDrafts
 .Include(d => d.IndustryTag)
 .Include(d => d.InterestTags)
 .FirstOrDefaultAsync(d => d.NewsArticleId == id);

 // assemble preview DTO
 var content = draft?.FullContentEN ?? article.FullContentEN ?? article.OriginalContent;
 if (!string.IsNullOrWhiteSpace(lang) && lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
 {
 content = draft?.FullContentZH ?? article.FullContentZH ?? article.OriginalContent;
 }

 var preview = new
 {
 TitleZH = article.TitleZH,
 TitleEN = article.TitleEN,
 Content = content,
 HeroImageUrl = draft?.HeroImageUrl,
 IndustryTag = draft?.IndustryTag == null ? null : new { draft.IndustryTag.IndustryTagId, draft.IndustryTag.NameEN, draft.IndustryTag.NameZH },
 InterestTags = draft?.InterestTags.Select(i => new { i.InterestTagId, i.NameEN, i.NameZH })
 };

 return Ok(preview);
 }

 // POST /api/publish/batch/publish
 [HttpPost("batch/publish")]
 [Authorize(Roles = "Consultant")]
 public async Task<IActionResult> BatchPublish([FromBody] BatchPublishDto dto)
 {
 if (dto.ArticleIds == null || dto.ArticleIds.Count ==0) return BadRequest("articleIds required");

 var results = new List<object>();
 var drafts = await _db.PublicationDrafts
 .Include(d => d.InterestTags)
 .Where(d => dto.ArticleIds.Contains(d.NewsArticleId))
 .ToListAsync();

 foreach (var id in dto.ArticleIds)
 {
 var draft = drafts.FirstOrDefault(d => d.NewsArticleId == id);
 if (draft == null)
 {
 results.Add(new { id, success = false, error = "no draft" });
 continue;
 }

 var (ok, err) = await _pubService.PublishDraftAsync(draft, dto.ScheduledAt, User?.Identity?.Name ?? "consultant");
 results.Add(new { id, success = ok, error = err });
 }

 await _db.SaveChangesAsync();
 return Ok(results);
 }

 // POST /api/publish/batch/unpublish
 [HttpPost("batch/unpublish")]
 [Authorize(Roles = "Consultant")]
 public async Task<IActionResult> BatchUnpublish([FromBody] BatchIdsDto dto)
 {
 if (dto.ArticleIds == null || dto.ArticleIds.Count ==0) return BadRequest("articleIds required");
 var drafts = await _db.PublicationDrafts.Where(d => dto.ArticleIds.Contains(d.NewsArticleId)).ToListAsync();
 var results = new List<object>();
 foreach (var id in dto.ArticleIds)
 {
 var draft = drafts.FirstOrDefault(d => d.NewsArticleId == id);
 if (draft == null) { results.Add(new { id, success = false, error = "no draft" }); continue; }
 var (ok, err) = await _pubService.UnpublishDraftAsync(draft, User?.Identity?.Name ?? "consultant");
 results.Add(new { id, success = ok, error = err });
 }
 await _db.SaveChangesAsync();
 return Ok(results);
 }

 // POST /api/publish/batch/save
 [HttpPost("batch/save")]
 [Authorize(Roles = "Consultant")]
 public async Task<IActionResult> BatchSave([FromBody] List<PublishArticleDto> dtos)
 {
 if (dtos == null || dtos.Count ==0) return BadRequest("body required");
 var results = new List<object>();
 foreach (var dto in dtos)
 {
 var id = dto.NewsArticleId;
 var article = await _db.NewsArticles.FindAsync(id);
 if (article == null) { results.Add(new { id, success = false, error = "article not found" }); continue; }
 var draft = await _db.PublicationDrafts.Include(d => d.InterestTags).FirstOrDefaultAsync(d => d.NewsArticleId == id);
 if (draft == null)
 {
 draft = new PublicationDraft { NewsArticleId = id, CreatedAt = DateTime.Now, CreatedBy = User?.Identity?.Name ?? "consultant" };
 _db.PublicationDrafts.Add(draft);
 }
 draft.HeroImageUrl = dto.HeroImageUrl;
 draft.HeroImageAlt = dto.HeroImageAlt;
 draft.HeroImageSource = dto.HeroImageSource;
 draft.FullContentEN = dto.FullContentEN;
 draft.FullContentZH = dto.FullContentZH;
 draft.UpdatedAt = DateTime.Now;
 if (dto.IndustryTagId.HasValue)
 {
 var industry = await _db.IndustryTags.FindAsync(dto.IndustryTagId.Value);
 if (industry == null) { results.Add(new { id, success = false, error = "invalid industry" }); continue; }
 draft.IndustryTag = industry;
 draft.IndustryTagId = industry.IndustryTagId;
 }
 draft.InterestTags.Clear();
 if (dto.InterestTagIds != null && dto.InterestTagIds.Count >0)
 {
 var tags = await _db.InterestTags.Where(t => dto.InterestTagIds.Contains(t.InterestTagId)).ToListAsync();
 foreach (var t in tags) draft.InterestTags.Add(t);
 }
 results.Add(new { id, success = true });
 }
 await _db.SaveChangesAsync();
 return Ok(results);
 }
        // POST /api/publish/suggest
        // Uses OpenAI to suggest industry and interest tags for a list of articles
        [HttpPost("suggest")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> SuggestPublish([FromBody] SuggestPublishDto dto)
        {
            if (dto == null || dto.ArticleIds == null || dto.ArticleIds.Count == 0)
                return BadRequest("ArticleIds are required.");

            // Load articles
            var articles = await _db.NewsArticles
                .Where(a => dto.ArticleIds.Contains(a.NewsArticleId))
                .ToDictionaryAsync(a => a.NewsArticleId);

            // Load tag metadata
            var industryTags = await _db.IndustryTags.ToListAsync();
            var interestTags = await _db.InterestTags.ToListAsync();

            // Prepare choice lists for the prompt
            var industryNames = industryTags.Select(i => i.NameEN).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
            var interestNames = interestTags.Select(i => i.NameEN).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();

            // Build a combined prompt feeding the list of choices so AI selects from them
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("You are an assistant that maps articles to one industry and up to3 interest topics. Respond with JSON only.");
            sb.AppendLine("Available industries:");
            sb.AppendLine(JsonSerializer.Serialize(industryNames));
            sb.AppendLine("Available interests:");
            sb.AppendLine(JsonSerializer.Serialize(interestNames));
            sb.AppendLine();
            sb.AppendLine("For each article provided, return an object with keys: articleId, industry (choose one exact name from Available industries), interests (array of up to3 exact names from Available interests), rationale (one short sentence). Return a JSON array named suggestions.");

            // Add articles content to user prompt
            sb.AppendLine();
            sb.AppendLine("Articles:");
            foreach (var id in dto.ArticleIds)
            {
                if (articles.TryGetValue(id, out var a))
                {
                    var title = a.TitleEN ?? a.TitleZH ?? string.Empty;
                    var summary = a.SummaryEN ?? a.SummaryZH ?? (a.OriginalContent?.Substring(0, Math.Min(400, (a.OriginalContent ?? string.Empty).Length)) ?? string.Empty);
                    sb.AppendLine(JsonSerializer.Serialize(new { articleId = id, title, summary }));
                }
            }

            var userPrompt = sb.ToString();

            string? aiText;
            try
            {
                aiText = await _chat.CreateChatCompletionAsync("You are a precise classifier. Output JSON only.", userPrompt, maxTokens: 800, temperature: 0.2);
            }
            catch (Exception ex)
            {
                return StatusCode(502, new { error = "OpenAI call failed", detail = ex.Message });
            }

            if (string.IsNullOrWhiteSpace(aiText)) return StatusCode(502, new { error = "Empty response from OpenAI" });

            var cleaned = aiText.Trim();
            // try extract JSON from code fences
            if (cleaned.StartsWith("```"))
            {
                var match = Regex.Match(cleaned, "```(?:json)?\\s*\\n?([\\s\\S]*?)\\n?```", RegexOptions.Singleline);
                if (match.Success && match.Groups.Count > 1) cleaned = match.Groups[1].Value.Trim();
            }

            // Try parse
            try
            {
                using var doc = JsonDocument.Parse(cleaned);
                var root = doc.RootElement;
                // Expecting { "suggestions": [ ... ] } or an array
                JsonElement suggestionsElem;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("suggestions", out suggestionsElem))
                {
                    // ok
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    // wrap into suggestions
                    var arr = root;
                    var result = new List<object>();
                    foreach (var item in arr.EnumerateArray())
                    {
                        result.Add(item);
                    }
                    return Ok(new { raw = aiText, suggestions = result });
                }

                // Map suggestion names to tag ids where possible and return
                var suggestions = new List<object>();
                var arrElem = root.ValueKind == JsonValueKind.Object ? root.GetProperty("suggestions") : root;
                foreach (var item in arrElem.EnumerateArray())
                {
                    var aid = item.GetProperty("articleId").GetInt32();
                    var industryName = item.TryGetProperty("industry", out var iname) ? iname.GetString() ?? string.Empty : string.Empty;
                    var interestsList = new List<string>();
                    if (item.TryGetProperty("interests", out var iarr) && iarr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var it in iarr.EnumerateArray()) if (it.ValueKind == JsonValueKind.String) interestsList.Add(it.GetString() ?? string.Empty);
                    }
                    var rationale = item.TryGetProperty("rationale", out var r) ? r.GetString() ?? string.Empty : string.Empty;

                    var matchedIndustry = industryTags.FirstOrDefault(t => string.Equals(t.NameEN, industryName, StringComparison.OrdinalIgnoreCase) || string.Equals(t.NameZH, industryName, StringComparison.OrdinalIgnoreCase));
                    var matchedInterestIds = interestTags.Where(t => interestsList.Any(n => string.Equals(n, t.NameEN, StringComparison.OrdinalIgnoreCase) || string.Equals(n, t.NameZH, StringComparison.OrdinalIgnoreCase))).Select(t => t.InterestTagId).ToList();

                    suggestions.Add(new
                    {
                        articleId = aid,
                        industry = new { name = industryName, industryTagId = matchedIndustry?.IndustryTagId },
                        interests = interestsList.Select((n, idx) => new { name = n, interestTagId = matchedInterestIds.ElementAtOrDefault(idx) }).ToList(),
                        rationale
                    });
                }

                return Ok(new { raw = aiText, suggestions });
            }
            catch (JsonException)
            {
                // parsing failed - return raw
                return Ok(new { raw = aiText, parsed = (object?)null });
            }
        }
 }
 public class SuggestPublishDto
 {
 public List<int> ArticleIds { get; set; } = new List<int>();
 }
}
