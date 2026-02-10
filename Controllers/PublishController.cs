using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;
using News_Back_end.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace News_Back_end.Controllers
{
 [ApiController]
 [Route("api/[controller]")]
 public class PublishController : ControllerBase
 {
 private readonly MyDBContext _db;
 private readonly IImageGenerationService? _imageService;
 private readonly IPublicationService _pubService;
 private readonly IConfiguration _config;
 private readonly IHttpClientFactory _httpFactory;
 private readonly ILogger<PublishController> _logger;
 private readonly IWebHostEnvironment _env;

 public PublishController(MyDBContext db, IPublicationService pubService, IConfiguration config, IHttpClientFactory httpFactory, ILogger<PublishController> logger, IImageGenerationService? imageService = null)
 {
 _db = db;
 _imageService = imageService;
 _pubService = pubService;
 _config = config;
 _httpFactory = httpFactory;
 _logger = logger;
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
        // New: POST /api/publish/suggest
        // Body: { "articleIds": [1,2,3] }
        // Returns per-article suggested IndustryTagId and InterestTagIds (must map to existing tags)
        [HttpPost("suggest")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> SuggestClassification([FromBody] SuggestRequestDto req)
        {
            if (req?.ArticleIds == null || req.ArticleIds.Count ==0) return BadRequest("articleIds required");

            // Load tags list to restrict suggestions
            var industries = await _db.IndustryTags.AsNoTracking().Select(i => new { i.IndustryTagId, i.NameEN }).ToListAsync();
            var interests = await _db.InterestTags.AsNoTracking().Select(i => new { i.InterestTagId, i.NameEN }).ToListAsync();

            // OpenAI config
            var apiKey = _config["OpenAIAISuggestedClassification:ApiKey"];
            var baseUrl = _config["OpenAIAISuggestedClassification:BaseUrl"] ?? "https://api.openai.com/";
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // AI not configured, return empty suggestions so frontend can fall back
                var fallback = new List<SuggestResultDto>();
                foreach (var id in req.ArticleIds)
                {
                    fallback.Add(new SuggestResultDto { NewsArticleId = id, IndustryTagId = null, InterestTagIds = new List<int>(), Error = "AI not configured" });
                }
                return Ok(fallback);
            }

            var results = new List<SuggestResultDto>();
            var client = _httpFactory.CreateClient();
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            foreach (var aid in req.ArticleIds)
            {
                try
                {
                    var article = await _db.NewsArticles.AsNoTracking().FirstOrDefaultAsync(a => a.NewsArticleId == aid);
                    if (article == null)
                    {
                        results.Add(new SuggestResultDto { NewsArticleId = aid, Error = "article not found" });
                        continue;
                    }

                    var textEN = article.FullContentEN ?? article.OriginalContent ?? string.Empty;
                    var textZH = article.FullContentZH ?? string.Empty;

                    // Build prompt with available tags and IDs to force model to choose existing tags
                    var sb = new StringBuilder();
                    sb.AppendLine("You are an assistant that classifies news articles into one Industry and multiple Interest topics.");
                    sb.AppendLine("Choose exactly one industry id and1-3 interest ids from the provided lists. Respond with a JSON object with keys: industryId (number or null) and interestIds (array of numbers). Only return valid ids from the lists.");
                    sb.AppendLine();
                    sb.AppendLine("Industries:");
                    foreach (var i in industries) sb.AppendLine($"[{i.IndustryTagId}] {i.NameEN}");
                    sb.AppendLine();
                    sb.AppendLine("Interests:");
                    foreach (var t in interests) sb.AppendLine($"[{t.InterestTagId}] {t.NameEN}");
                    sb.AppendLine();
                    sb.AppendLine("Article content (English):");
                    sb.AppendLine(textEN.Length >2000 ? textEN.Substring(0,2000) + "..." : textEN);
                    if (!string.IsNullOrWhiteSpace(textZH))
                    {
                        sb.AppendLine();
                        sb.AppendLine("Article content (Chinese):");
                        sb.AppendLine(textZH.Length >2000 ? textZH.Substring(0,2000) + "..." : textZH);
                    }
                    sb.AppendLine();
                    sb.AppendLine("Return only JSON. Example: {\"industryId\":2, \"interestIds\": [5,7] }");

                    var payload = new
                    {
                        model = "gpt-3.5-turbo",
                        messages = new[] {
                            new { role = "system", content = "You are a helpful classification assistant."},
                            new { role = "user", content = sb.ToString() }
                        },
                        temperature =0.0,
                        max_tokens =200
                    };

                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    using var resp = await client.PostAsync("v1/chat/completions", content);
                    var body = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("OpenAI classify failed: {0} {1}", resp.StatusCode, body);
                        results.Add(new SuggestResultDto { NewsArticleId = aid, Error = $"AI error: {resp.StatusCode} {body}" });
                        continue;
                    }

                    // parse response and extract content
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() >0)
                        {
                            var message = choices[0].GetProperty("message").GetProperty("content").GetString();
                            var parsed = TryParseJsonLike(message);
                            if (parsed != null)
                            {
                                var rootEl = parsed.Value;
                                int? industryId = null;
                                List<int> interestIds = new List<int>();
                                if (rootEl.TryGetProperty("industryId", out var indEl) && indEl.ValueKind == JsonValueKind.Number)
                                {
                                    if (indEl.TryGetInt32(out var indVal))
                                    {
                                        industryId = indVal;
                                        // validate exists
                                        if (!industries.Any(x => x.IndustryTagId == industryId.Value)) industryId = null;
                                    }
                                }
                                if (rootEl.TryGetProperty("interestIds", out var ints) && ints.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var el in ints.EnumerateArray())
                                    {
                                        if (el.ValueKind == JsonValueKind.Number)
                                        {
                                            if (el.TryGetInt32(out var iid))
                                            {
                                                if (interests.Any(x => x.InterestTagId == iid)) interestIds.Add(iid);
                                            }
                                        }
                                    }
                                }

                                results.Add(new SuggestResultDto { NewsArticleId = aid, IndustryTagId = industryId, InterestTagIds = interestIds, RawSuggestion = message });
                            }
                            else
                            {
                                var snippet = message?.Length >300 ? message.Substring(0,300) + "..." : message;
                                results.Add(new SuggestResultDto { NewsArticleId = aid, Error = $"failed to parse AI response: {snippet}", RawSuggestion = message });
                            }
                        }
                        else
                        {
                            results.Add(new SuggestResultDto { NewsArticleId = aid, Error = $"no choices: {body}" });
                        }
                    }
                    catch (JsonException jex)
                    {
                        _logger.LogWarning(jex, "Failed to parse OpenAI response: {0}", body);
                        var snippet = body?.Length >500 ? body.Substring(0,500) + "..." : body;
                        results.Add(new SuggestResultDto { NewsArticleId = aid, Error = $"parse error: {jex.Message} {snippet}" });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SuggestClassification error for article {id}", aid);
                    results.Add(new SuggestResultDto { NewsArticleId = aid, Error = ex.Message });
                }
            }

            return Ok(results);
        }

        // Helper: try parse string content as JSON object; trims code fences and tries to find first JSON object in text
        private static JsonElement? TryParseJsonLike(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            // remove markdown fences
            text = text.Trim();
            if (text.StartsWith("```"))
            {
                var idx = text.IndexOf("\n");
                if (idx >=0) text = text.Substring(idx +1).Trim();
                if (text.EndsWith("```")) text = text.Substring(0, text.Length -3).Trim();
            }

            // find first { and last }
            var first = text.IndexOf('{');
            var last = text.LastIndexOf('}');
            if (first >=0 && last > first)
            {
                var json = text.Substring(first, last - first +1);
                try
                {
                    var doc = JsonDocument.Parse(json);
                    return doc.RootElement;
                }
                catch { return null; }
            }

            return null;
        }

        // DTOs used by the new endpoint
        public class SuggestRequestDto { public List<int>? ArticleIds { get; set; } }
        public class SuggestResultDto { public int NewsArticleId { get; set; } public int? IndustryTagId { get; set; } public List<int> InterestTagIds { get; set; } = new(); public string? RawSuggestion { get; set; } public string? Error { get; set; } }
    }
}
