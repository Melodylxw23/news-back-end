using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using News_Back_end.DTOs;

namespace News_Back_end.Services
{
    /// <summary>
    /// Uses the OpenAIBroadcastAnalytics HttpClient to generate
    /// human-readable recommendations from analytics snapshots.
    /// </summary>
    public sealed class OpenAIBroadcastAnalyticsService : IBroadcastAnalyticsAiService
    {
        private readonly HttpClient _http;
        private readonly ILogger<OpenAIBroadcastAnalyticsService> _logger;

        public OpenAIBroadcastAnalyticsService(HttpClient http, ILogger<OpenAIBroadcastAnalyticsService> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<string> GenerateRecommendationsAsync(object analyticsSnapshot, CancellationToken cancellationToken)
        {
            var result = await GenerateRecommendationsInternalAsync(analyticsSnapshot, cancellationToken);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        }

        internal async Task<BroadcastAiResultDTO> GenerateRecommendationsTypedAsync(object analyticsSnapshot, CancellationToken cancellationToken)
        {
            return await GenerateRecommendationsInternalAsync(analyticsSnapshot, cancellationToken);
        }

        private async Task<BroadcastAiResultDTO> GenerateRecommendationsInternalAsync(object analyticsSnapshot, CancellationToken cancellationToken)
        {
            var snapshotJson = JsonSerializer.Serialize(analyticsSnapshot, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var prompt = BuildEnhancedPrompt(snapshotJson);

            var body = new
            {
                model = "gpt-4o-mini",
                temperature = 0.3,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = "You are an expert email marketing analyst helping users create high-performing broadcast emails. Provide specific, actionable feedback that helps them improve their content before sending." },
                    new { role = "user", content = prompt }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await _http.SendAsync(req, cancellationToken);
            var content = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[OpenAIBroadcastAnalytics] Non-success status {Status}: {Body}", (int)resp.StatusCode, content);
                throw new InvalidOperationException($"OpenAI analytics request failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
            }

            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var choices = root.GetProperty("choices");
                if (choices.GetArrayLength() == 0)
                    throw new InvalidOperationException("OpenAI returned no choices");

                var msg = choices[0].GetProperty("message");
                JsonElement contentEl;
                if (msg.TryGetProperty("content", out contentEl))
                {
                    if (contentEl.ValueKind == JsonValueKind.Object)
                    {
                        return DeserializeAiResult(contentEl);
                    }

                    if (contentEl.ValueKind == JsonValueKind.String)
                    {
                        var text = contentEl.GetString() ?? string.Empty;
                        var jsonCandidate = ExtractJsonObject(text);
                        var toParse = !string.IsNullOrWhiteSpace(jsonCandidate) ? jsonCandidate : text;

                        try
                        {
                            var parsed = JsonDocument.Parse(toParse).RootElement;
                            if (parsed.ValueKind == JsonValueKind.Object)
                            {
                                return DeserializeAiResult(parsed);
                            }
                        }
                        catch (JsonException jsonEx)
                        {
                            _logger.LogDebug(jsonEx, "Parsing AI text content failed, falling back to raw text");
                            return new BroadcastAiResultDTO { Raw = text, GeneratedAt = DateTime.UtcNow };
                        }

                        return new BroadcastAiResultDTO { Raw = text, GeneratedAt = DateTime.UtcNow };
                    }
                }

                _logger.LogError("[OpenAIBroadcastAnalytics] Unexpected response structure: {Body}", content);
                throw new InvalidOperationException("Unexpected OpenAI response format");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OpenAIBroadcastAnalytics] Failed parsing OpenAI response: {Body}", content);
                throw;
            }
        }

        private static string BuildEnhancedPrompt(string snapshotJson)
        {
            return $@"Analyze this broadcast email draft and provide actionable feedback to help the user create a high-performing email.

BROADCAST DATA:
{snapshotJson}

Provide a comprehensive analysis. Return ONLY valid JSON with this exact schema:
{{
 ""summary"": ""2-3 sentence overall assessment of the broadcast draft"",
 ""overallScore"": ""A|B|C|D|F"",
 ""readinessStatus"": ""Ready|NeedsWork|NotRecommended"",
 ""insights"": {{
 ""bestSendTime"": ""ISO datetime or HH:mm"",
 ""bestSendTimeLocal"": ""human readable e.g. Tuesday 9:00 AM"",
 ""timingRationale"": ""why this time is recommended based on data"",
 ""predictedOpenRate"": 0-100 number,
 ""predictedClickRate"": 0-100 number,
 ""engagementScore"": ""Low|Medium|High"",
 ""engagementRationale"": ""why this engagement level is predicted"",
 ""optimalSubjectLength"": ""e.g. 40-50 chars"",
 ""subjectLineFeedback"": ""specific feedback on the current subject line"",
 ""subjectLineSuggestions"": [""alternative subject line 1"", ""alternative subject line 2""],
 ""bodyLengthFeedback"": ""feedback on email body length"",
 ""toneFeedback"": ""feedback on writing tone and style"",
 ""contentImprovements"": [""specific improvement 1"", ""specific improvement 2""],
 ""estimatedRecipientsCount"": number or null,
 ""audienceMatchQuality"": ""Poor|Fair|Good|Excellent"",
 ""audienceInsight"": ""insight about the target audience preferences""
 }},
 ""recommendations"": [
 {{
 ""priority"": ""High|Medium|Low"",
 ""category"": ""Content|Timing|Audience|Subject|Articles"",
 ""title"": ""short action title"",
 ""why"": ""data-driven justification"",
 ""actions"": [""specific step 1"", ""specific step 2""],
 ""metricsReferenced"": [""metric1"", ""metric2""],
 ""expectedImpact"": 0-100 number representing % improvement
 }}
 ],
 ""suggestedArticles"": [
 {{
 ""publicationDraftId"": number,
 ""title"": ""article title"",
 ""relevanceScore"": 0-100,
 ""whySuggested"": ""why this article would perform well""
 }}
 ],
 ""quickWins"": [""simple change 1 that would help"", ""simple change 2""],
 ""warnings"": [""issue that should be fixed before sending""]
 }}

Guidelines:
- Be specific and actionable - tell them exactly what to change
- Reference actual data from the snapshot when making recommendations
- Subject line suggestions should be compelling and match the content
- Quick wins should be easy 5-minute fixes
- Warnings should be critical issues only
- If body/subject is empty, note that content is needed
- Consider the audience segments and their preferences
- Base timing on historical engagement data if available";
        }

        private BroadcastAiResultDTO DeserializeAiResult(JsonElement root)
        {
            var result = new BroadcastAiResultDTO
            {
                GeneratedAt = DateTime.UtcNow,
                ModelVersion = "gpt-4o-mini"
            };

            // Parse top-level fields
            if (root.TryGetProperty("summary", out var summaryEl) && summaryEl.ValueKind == JsonValueKind.String)
                result.Summary = summaryEl.GetString();

            if (root.TryGetProperty("overallScore", out var scoreEl) && scoreEl.ValueKind == JsonValueKind.String)
                result.OverallScore = scoreEl.GetString();

            if (root.TryGetProperty("readinessStatus", out var readyEl) && readyEl.ValueKind == JsonValueKind.String)
                result.ReadinessStatus = readyEl.GetString();

            // Parse recommendations
            if (root.TryGetProperty("recommendations", out var recsEl) && recsEl.ValueKind == JsonValueKind.Array)
            {
                var recs = new List<BroadcastAiRecommendationDTO>();
                foreach (var r in recsEl.EnumerateArray())
                {
                    try
                    {
                        var rec = new BroadcastAiRecommendationDTO
                        {
                            Priority = GetStringProp(r, "priority"),
                            Category = GetStringProp(r, "category"),
                            Title = GetStringProp(r, "title"),
                            Why = GetStringProp(r, "why"),
                            Actions = GetStringArrayProp(r, "actions"),
                            MetricsReferenced = GetStringArrayProp(r, "metricsReferenced"),
                            ExpectedImpact = GetDoubleProp(r, "expectedImpact")
                        };
                        recs.Add(rec);
                    }
                    catch { /* skip invalid entries */ }
                }
                result.Recommendations = recs;
            }

            // Parse insights
            if (root.TryGetProperty("insights", out var insightsEl) && insightsEl.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    var insights = new BroadcastAiInsightsDTO
                    {
                        BestSendTime = GetStringProp(insightsEl, "bestSendTime"),
                        BestSendTimeLocal = GetStringProp(insightsEl, "bestSendTimeLocal"),
                        TimingRationale = GetStringProp(insightsEl, "timingRationale"),
                        PredictedOpenRate = ClampPercent(GetDoubleProp(insightsEl, "predictedOpenRate")),
                        PredictedClickRate = ClampPercent(GetDoubleProp(insightsEl, "predictedClickRate")),
                        EngagementScore = GetStringProp(insightsEl, "engagementScore"),
                        EngagementRationale = GetStringProp(insightsEl, "engagementRationale"),
                        OptimalSubjectLength = GetStringProp(insightsEl, "optimalSubjectLength"),
                        SubjectLineFeedback = GetStringProp(insightsEl, "subjectLineFeedback"),
                        SubjectLineSuggestions = GetStringArrayProp(insightsEl, "subjectLineSuggestions"),
                        BodyLengthFeedback = GetStringProp(insightsEl, "bodyLengthFeedback"),
                        ToneFeedback = GetStringProp(insightsEl, "toneFeedback"),
                        ContentImprovements = GetStringArrayProp(insightsEl, "contentImprovements"),
                        EstimatedRecipientsCount = GetIntProp(insightsEl, "estimatedRecipientsCount"),
                        AudienceMatchQuality = GetStringProp(insightsEl, "audienceMatchQuality"),
                        AudienceInsight = GetStringProp(insightsEl, "audienceInsight")
                    };
                    result.Insights = insights;
                }
                catch { /* ignore insights parsing errors */ }
            }

            // Parse suggested articles
            if (root.TryGetProperty("suggestedArticles", out var articlesEl) && articlesEl.ValueKind == JsonValueKind.Array)
            {
                var articles = new List<SuggestedArticleDTO>();
                foreach (var a in articlesEl.EnumerateArray())
                {
                    try
                    {
                        var article = new SuggestedArticleDTO
                        {
                            PublicationDraftId = GetIntProp(a, "publicationDraftId") ?? 0,
                            Title = GetStringProp(a, "title"),
                            RelevanceScore = GetDoubleProp(a, "relevanceScore"),
                            WhySuggested = GetStringProp(a, "whySuggested"),
                            MatchingTopics = GetStringArrayProp(a, "matchingTopics")
                        };
                        if (article.PublicationDraftId > 0)
                            articles.Add(article);
                    }
                    catch { /* skip invalid entries */ }
                }
                result.SuggestedArticles = articles;
            }

            // Parse quick wins and warnings
            result.QuickWins = GetStringArrayProp(root, "quickWins");
            result.Warnings = GetStringArrayProp(root, "warnings");

            return result;
        }

        // Helper methods for safe JSON parsing
        private static string? GetStringProp(JsonElement el, string propName)
        {
            if (el.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
            return null;
        }

        private static double? GetDoubleProp(JsonElement el, string propName)
        {
            if (el.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var d)) return d;
                if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out var d2)) return d2;
            }
            return null;
        }

        private static int? GetIntProp(JsonElement el, string propName)
        {
            if (el.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i)) return i;
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var i2)) return i2;
            }
            return null;
        }

        private static List<string>? GetStringArrayProp(JsonElement el, string propName)
        {
            if (el.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                return prop.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }
            return null;
        }

        private static double? ClampPercent(double? val)
        {
            if (!val.HasValue) return null;
            if (val < 0) return 0;
            if (val > 100) return 100;
            return val;
        }

        private static string? ExtractJsonObject(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var first = s.IndexOf('{');
            var last = s.LastIndexOf('}');
            if (first >= 0 && last > first)
            {
                var candidate = s.Substring(first, last - first + 1);
                try
                {
                    JsonDocument.Parse(candidate);
                    return candidate;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }
}
