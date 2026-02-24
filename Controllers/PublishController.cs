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
using System.Text.RegularExpressions;
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

        public PublishController(
 MyDBContext db,
            IPublicationService pubService,
        IConfiguration config,
  IHttpClientFactory httpFactory,
 ILogger<PublishController> logger,
      IWebHostEnvironment env,
            IImageGenerationService? imageService = null)
  {
            _db = db;
            _imageService = imageService;
  _pubService = pubService;
      _config = config;
     _httpFactory = httpFactory;
            _logger = logger;
         _env = env;
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
     if (dto.InterestTagIds != null && dto.InterestTagIds.Count > 0)
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
     string prompt;
            if (!string.IsNullOrWhiteSpace(dto?.PromptOverride))
{
                prompt = dto.PromptOverride!.Trim();
            }
      else
   {
     prompt = BuildHeroImagePrompt(article);
            }

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
    _logger.LogWarning("GenerateHero attempt {attempt} for id={id}: {error}", attempt, id, lastError);
   }
           catch (Exception ex)
        {
         lastError = ex.Message;
       _logger.LogWarning(ex, "GenerateHero attempt {attempt} failed for id={id}", attempt, id);
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

      _logger.LogWarning("GenerateHero: failed after retries for id={id}, reason={reason}", id, lastError);
    return Ok(new { url = placeholder, fallback = true, reason = lastError });
      }

  try
   {
        var savedUrl = await SaveImageLocally(id, url);
         draft.HeroImageUrl = savedUrl;
    draft.HeroImageSource = "generated";
       draft.UpdatedAt = DateTime.Now;
    await _db.SaveChangesAsync();
 return Ok(new { url = draft.HeroImageUrl });
 }
   catch (Exception ex)
  {
         _logger.LogError(ex, "GenerateHero save error for id={id}", id);
    const string placeholder = "/assets/generated/hero_placeholder.svg";
                draft.HeroImageUrl = placeholder;
       draft.HeroImageSource = "generated-error";
                draft.UpdatedAt = DateTime.Now;
   await _db.SaveChangesAsync();
      return StatusCode(502, new { error = "image_processing_failed", reason = ex.Message });
}
        }

     // POST /api/publish/{id}/upload-hero
        [HttpPost("{id:int}/upload-hero")]
        [Authorize(Roles = "Consultant")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB max
        public async Task<IActionResult> UploadHeroImage(int id, IFormFile file)
        {
        if (file == null || file.Length == 0)
      return BadRequest("No file uploaded.");

 var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
   if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
          return BadRequest("Only JPG and PNG files are allowed.");

    var contentType = file.ContentType?.ToLowerInvariant() ?? "";
    if (contentType != "image/jpeg" && contentType != "image/png" && contentType != "image/jpg")
           return BadRequest("Only JPG and PNG files are allowed.");

         var draft = await _db.PublicationDrafts.FirstOrDefaultAsync(d => d.NewsArticleId == id);
   if (draft == null)
    {
         var article = await _db.NewsArticles.FindAsync(id);
       if (article == null) return NotFound();
        draft = new PublicationDraft { NewsArticleId = id, CreatedAt = DateTime.Now, CreatedBy = User?.Identity?.Name ?? "consultant" };
      _db.PublicationDrafts.Add(draft);
 }

    try
         {
            var dir = Path.Combine(_env.WebRootPath ?? "wwwroot", "assets", "generated");
      Directory.CreateDirectory(dir);
          var fileName = $"hero_{id}{ext}";
    var filePath = Path.Combine(dir, fileName);

    // Delete any previously uploaded file with different extension
     foreach (var old in new[] { ".jpg", ".jpeg", ".png" })
    {
         var oldPath = Path.Combine(dir, $"hero_{id}{old}");
   if (System.IO.File.Exists(oldPath) && oldPath != filePath)
              System.IO.File.Delete(oldPath);
         }

    using (var stream = new FileStream(filePath, FileMode.Create))
  {
            await file.CopyToAsync(stream);
       }

      var savedUrl = $"/assets/generated/{fileName}";
          draft.HeroImageUrl = savedUrl;
         draft.HeroImageSource = "uploaded";
         draft.HeroImageAlt = Path.GetFileNameWithoutExtension(file.FileName);
    draft.UpdatedAt = DateTime.Now;
         await _db.SaveChangesAsync();

        return Ok(new { url = savedUrl, source = "uploaded" });
        }
    catch (Exception ex)
       {
     _logger.LogError(ex, "UploadHeroImage failed for id={id}", id);
  return StatusCode(500, new { error = "upload_failed", reason = ex.Message });
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
        if (dto.ArticleIds == null || dto.ArticleIds.Count == 0) return BadRequest("articleIds required");

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
          if (dto.ArticleIds == null || dto.ArticleIds.Count == 0) return BadRequest("articleIds required");
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
         if (dtos == null || dtos.Count == 0) return BadRequest("body required");
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
if (dto.InterestTagIds != null && dto.InterestTagIds.Count > 0)
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
          if (req?.ArticleIds == null || req.ArticleIds.Count == 0) return BadRequest("articleIds required");

    var industries = await _db.IndustryTags.AsNoTracking().Select(i => new { i.IndustryTagId, i.NameEN }).ToListAsync();
   var interests = await _db.InterestTags.AsNoTracking().Select(i => new { i.InterestTagId, i.NameEN }).ToListAsync();

      var apiKey = _config["OpenAIAISuggestedClassification:ApiKey"];
        var baseUrl = _config["OpenAIAISuggestedClassification:BaseUrl"] ?? "https://api.openai.com/";
            if (string.IsNullOrWhiteSpace(apiKey))
    {
          var fallback = new List<SuggestResultDto>();
    foreach (var id in req.ArticleIds)
     fallback.Add(new SuggestResultDto { NewsArticleId = id, IndustryTagId = null, InterestTagIds = new List<int>(), Error = "AI not configured" });
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

      var textZH = article.FullContentZH ?? article.OriginalContent ?? string.Empty;
              var textEN = article.FullContentEN ?? "";

             var sb = new StringBuilder();
      sb.AppendLine("You are an assistant that classifies news articles into one Industry and multiple Interest topics.");
    sb.AppendLine("Choose exactly one industry id and 1-3 interest ids from the provided lists. Respond with a JSON object with keys: industryId (number or null) and interestIds (array of numbers). Only return valid ids from the lists.");
          sb.AppendLine();
     sb.AppendLine("Industries:");
    foreach (var i in industries) sb.AppendLine($"[{i.IndustryTagId}] {i.NameEN}");
     sb.AppendLine();
  sb.AppendLine("Interests:");
        foreach (var t in interests) sb.AppendLine($"[{t.InterestTagId}] {t.NameEN}");
           sb.AppendLine();
        sb.AppendLine("Article content (Chinese - primary for analysis):");
   sb.AppendLine(textZH.Length > 2000 ? textZH.Substring(0, 2000) + "..." : textZH);
              if (!string.IsNullOrWhiteSpace(textEN))
     {
      sb.AppendLine();
      sb.AppendLine("Article content (English - supplementary):");
   sb.AppendLine(textEN.Length > 2000 ? textEN.Substring(0, 2000) + "..." : textEN);
          }
   sb.AppendLine();
             sb.AppendLine("Return only JSON. Example: {\"industryId\":2, \"interestIds\": [5,7] }");

        var result = await CallOpenAIForClassification(client, sb.ToString(), aid, industries.Select(i => i.IndustryTagId).ToList(), interests.Select(i => i.InterestTagId).ToList());
   results.Add(result);
     }
  catch (Exception ex)
    {
     _logger.LogError(ex, "SuggestClassification error for article {id}", aid);
         results.Add(new SuggestResultDto { NewsArticleId = aid, Error = ex.Message });
      }
   }

  return Ok(results);
  }

  // POST /api/publish/quick-publish
     // Batch: AI auto-classifies tags, generates hero images, cleans titles, then publishes
        [HttpPost("quick-publish")]
     [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> QuickPublish([FromBody] QuickPublishDto dto)
      {
    if (dto?.ArticleIds == null || dto.ArticleIds.Count == 0)
            return BadRequest("articleIds required");

   var actor = User?.Identity?.Name ?? "consultant";
      var results = new List<QuickPublishResultDto>();

            // Load existing tags
   var industries = await _db.IndustryTags.AsNoTracking().ToListAsync();
     var interests = await _db.InterestTags.AsNoTracking().ToListAsync();

          // Prepare OpenAI client for classification
            var classifyApiKey = _config["OpenAIAISuggestedClassification:ApiKey"];
        var classifyBaseUrl = _config["OpenAIAISuggestedClassification:BaseUrl"] ?? "https://api.openai.com/";
  HttpClient? classifyClient = null;
    if (!string.IsNullOrWhiteSpace(classifyApiKey))
    {
    classifyClient = _httpFactory.CreateClient();
  classifyClient.BaseAddress = new Uri(classifyBaseUrl);
     classifyClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", classifyApiKey);
            }

       foreach (var articleId in dto.ArticleIds)
{
          var result = new QuickPublishResultDto { NewsArticleId = articleId };
   try
       {
      var article = await _db.NewsArticles.FindAsync(articleId);
         if (article == null)
        {
       result.Success = false;
     result.Error = "Article not found";
     results.Add(result);
  continue;
        }

     // 1. Clean titles
    article.TitleZH = CleanTitle(article.TitleZH);
  if (!string.IsNullOrWhiteSpace(article.TitleEN))
        article.TitleEN = CleanTitle(article.TitleEN);
  result.TitlesCleaned = true;

                    // 2. Get or create draft
    var draft = await _db.PublicationDrafts
       .Include(d => d.InterestTags)
  .FirstOrDefaultAsync(d => d.NewsArticleId == articleId);
         if (draft == null)
        {
               draft = new PublicationDraft
        {
          NewsArticleId = articleId,
               CreatedAt = DateTime.Now,
             CreatedBy = actor
      };
     _db.PublicationDrafts.Add(draft);
              await _db.SaveChangesAsync();
     }

         // 3. AI classify tags
       if (classifyClient != null && industries.Count > 0 && interests.Count > 0)
         {
            try
           {
var textZH = article.FullContentZH ?? article.OriginalContent ?? "";
        var textEN = article.FullContentEN ?? "";
    var classifyPrompt = BuildClassificationPrompt(textZH, textEN, industries, interests);
           var classification = await CallOpenAIForClassification(
     classifyClient, classifyPrompt, articleId,
      industries.Select(i => i.IndustryTagId).ToList(),
              interests.Select(i => i.InterestTagId).ToList());

                if (classification.IndustryTagId.HasValue)
         {
   draft.IndustryTagId = classification.IndustryTagId.Value;
              draft.IndustryTag = industries.First(i => i.IndustryTagId == classification.IndustryTagId.Value);
          }
       if (classification.InterestTagIds.Count > 0)
       {
          draft.InterestTags.Clear();
             var matchedTags = await _db.InterestTags.Where(t => classification.InterestTagIds.Contains(t.InterestTagId)).ToListAsync();
        foreach (var t in matchedTags) draft.InterestTags.Add(t);
             }
        result.TagsAssigned = true;
     result.AssignedIndustryTagId = draft.IndustryTagId;
             result.AssignedInterestTagIds = draft.InterestTags.Select(t => t.InterestTagId).ToList();
       }
       catch (Exception ex)
       {
     _logger.LogWarning(ex, "Quick publish: AI classification failed for article {id}", articleId);
     result.TagsAssigned = false;
             result.ClassificationError = ex.Message;
             }
          }

     // 4. Generate hero image
      if (_imageService != null)
       {
            try
{
     var prompt = BuildHeroImagePrompt(article);
         var imgUrl = await _imageService.GenerateImageAsync(articleId, prompt);
           if (!string.IsNullOrWhiteSpace(imgUrl))
           {
         var savedUrl = await SaveImageLocally(articleId, imgUrl);
                  draft.HeroImageUrl = savedUrl;
            draft.HeroImageSource = "generated";
        result.HeroImageGenerated = true;
   result.HeroImageUrl = savedUrl;
            }
 }
         catch (Exception ex)
              {
     _logger.LogWarning(ex, "Quick publish: Hero image generation failed for article {id}", articleId);
           result.HeroImageGenerated = false;
      result.ImageError = ex.Message;
   }
  }

      draft.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

         // 5. Publish (skip validation failures)
  if (draft.IndustryTagId != null && draft.InterestTags.Any())
   {
           var (ok, err) = await _pubService.PublishDraftAsync(draft, null, actor);
        result.Success = ok;
            if (!ok) result.Error = err;
          }
           else
       {
    result.Success = false;
              result.Error = "Missing industry tag or interest tags after AI classification.";
      }
   }
          catch (Exception ex)
       {
        _logger.LogError(ex, "QuickPublish error for article {id}", articleId);
        result.Success = false;
      result.Error = ex.Message;
      }
     results.Add(result);
 }

     return Ok(results);
        }

        // POST /api/publish/quick-schedule
        // Batch: AI auto-classifies tags, generates hero images, cleans titles, then schedules
        [HttpPost("quick-schedule")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> QuickSchedule([FromBody] QuickScheduleDto dto)
    {
     if (dto?.ArticleIds == null || dto.ArticleIds.Count == 0)
   return BadRequest("articleIds required");
      if (dto.ScheduledAt == null)
       return BadRequest("scheduledAt is required for scheduling");

     var actor = User?.Identity?.Name ?? "consultant";
      var results = new List<QuickPublishResultDto>();

     var industries = await _db.IndustryTags.AsNoTracking().ToListAsync();
            var interests = await _db.InterestTags.AsNoTracking().ToListAsync();

  var classifyApiKey = _config["OpenAIAISuggestedClassification:ApiKey"];
         var classifyBaseUrl = _config["OpenAIAISuggestedClassification:BaseUrl"] ?? "https://api.openai.com/";
     HttpClient? classifyClient = null;
     if (!string.IsNullOrWhiteSpace(classifyApiKey))
       {
          classifyClient = _httpFactory.CreateClient();
classifyClient.BaseAddress = new Uri(classifyBaseUrl);
      classifyClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", classifyApiKey);
       }

            foreach (var articleId in dto.ArticleIds)
     {
    var result = new QuickPublishResultDto { NewsArticleId = articleId };
           try
  {
         var article = await _db.NewsArticles.FindAsync(articleId);
      if (article == null)
           {
         result.Success = false;
      result.Error = "Article not found";
                    results.Add(result);
         continue;
  }

       // 1. Clean titles
 article.TitleZH = CleanTitle(article.TitleZH);
        if (!string.IsNullOrWhiteSpace(article.TitleEN))
      article.TitleEN = CleanTitle(article.TitleEN);
              result.TitlesCleaned = true;

  // 2. Get or create draft
        var draft = await _db.PublicationDrafts
            .Include(d => d.InterestTags)
              .FirstOrDefaultAsync(d => d.NewsArticleId == articleId);
            if (draft == null)
   {
      draft = new PublicationDraft
          {
     NewsArticleId = articleId,
     CreatedAt = DateTime.Now,
  CreatedBy = actor
    };
     _db.PublicationDrafts.Add(draft);
   await _db.SaveChangesAsync();
    }

     // 3. AI classify tags
       if (classifyClient != null && industries.Count > 0 && interests.Count > 0)
          {
                   try
   {
         var textZH = article.FullContentZH ?? article.OriginalContent ?? "";
         var textEN = article.FullContentEN ?? "";
      var classifyPrompt = BuildClassificationPrompt(textZH, textEN, industries, interests);
                  var classification = await CallOpenAIForClassification(
classifyClient, classifyPrompt, articleId,
   industries.Select(i => i.IndustryTagId).ToList(),
                interests.Select(i => i.InterestTagId).ToList());

        if (classification.IndustryTagId.HasValue)
            {
             draft.IndustryTagId = classification.IndustryTagId.Value;
  draft.IndustryTag = industries.First(i => i.IndustryTagId == classification.IndustryTagId.Value);
      }
    if (classification.InterestTagIds.Count > 0)
       {
         draft.InterestTags.Clear();
        var matchedTags = await _db.InterestTags.Where(t => classification.InterestTagIds.Contains(t.InterestTagId)).ToListAsync();
     foreach (var t in matchedTags) draft.InterestTags.Add(t);
      }
          result.TagsAssigned = true;
          result.AssignedIndustryTagId = draft.IndustryTagId;
       result.AssignedInterestTagIds = draft.InterestTags.Select(t => t.InterestTagId).ToList();
      }
          catch (Exception ex)
                 {
    _logger.LogWarning(ex, "Quick schedule: AI classification failed for article {id}", articleId);
 result.TagsAssigned = false;
         result.ClassificationError = ex.Message;
            }
         }

            // 4. Generate hero image
    if (_imageService != null)
          {
     try
   {
     var prompt = BuildHeroImagePrompt(article);
   var imgUrl = await _imageService.GenerateImageAsync(articleId, prompt);
   if (!string.IsNullOrWhiteSpace(imgUrl))
         {
      var savedUrl = await SaveImageLocally(articleId, imgUrl);
   draft.HeroImageUrl = savedUrl;
             draft.HeroImageSource = "generated";
           result.HeroImageGenerated = true;
          result.HeroImageUrl = savedUrl;
   }
      }
              catch (Exception ex)
           {
         _logger.LogWarning(ex, "Quick schedule: Hero image generation failed for article {id}", articleId);
          result.HeroImageGenerated = false;
        result.ImageError = ex.Message;
    }
      }

    // 5. Set schedule
        draft.ScheduledAt = dto.ScheduledAt;
   draft.UpdatedAt = DateTime.Now;
     await _db.SaveChangesAsync();

     // Set article status to Scheduled
        if (draft.IndustryTagId != null && draft.InterestTags.Any())
    {
    article.Status = ArticleStatus.Scheduled;
       draft.IsPublished = false;
            await _db.SaveChangesAsync();
          result.Success = true;
      result.ScheduledAt = dto.ScheduledAt;
        }
    else
              {
  result.Success = false;
        result.Error = "Missing industry tag or interest tags after AI classification.";
   }
           }
         catch (Exception ex)
                {
  _logger.LogError(ex, "QuickSchedule error for article {id}", articleId);
result.Success = false;
      result.Error = ex.Message;
        }
    results.Add(result);
}

         return Ok(results);
        }

   // POST /api/publish/analytics/tags
        // Returns AI-generated analytics about tag usage patterns across all articles in publish queue
        [HttpPost("analytics/tags")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> TagAnalytics([FromBody] TagAnalyticsRequestDto? req)
  {
     // Gather tag usage data
    var drafts = await _db.PublicationDrafts
  .Include(d => d.IndustryTag)
       .Include(d => d.InterestTags)
     .Include(d => d.NewsArticle)
           .Where(d => d.IndustryTagId != null || d.InterestTags.Any())
                .AsNoTracking()
        .ToListAsync();

    // Industry tag distribution
       var industryDistribution = drafts
                .Where(d => d.IndustryTag != null)
             .GroupBy(d => new { d.IndustryTag!.IndustryTagId, d.IndustryTag.NameEN, d.IndustryTag.NameZH })
    .Select(g => new TagUsageDto
 {
    TagId = g.Key.IndustryTagId,
            NameEN = g.Key.NameEN,
      NameZH = g.Key.NameZH,
  Count = g.Count(),
        PublishedCount = g.Count(d => d.IsPublished),
 ScheduledCount = g.Count(d => d.ScheduledAt != null && !d.IsPublished),
  DraftCount = g.Count(d => !d.IsPublished && d.ScheduledAt == null)
         })
                .OrderByDescending(x => x.Count)
       .ToList();

    // Interest tag distribution
            var interestDistribution = drafts
                .SelectMany(d => d.InterestTags.Select(t => new { Draft = d, Tag = t }))
            .GroupBy(x => new { x.Tag.InterestTagId, x.Tag.NameEN, x.Tag.NameZH })
      .Select(g => new TagUsageDto
         {
         TagId = g.Key.InterestTagId,
         NameEN = g.Key.NameEN,
            NameZH = g.Key.NameZH,
        Count = g.Count(),
        PublishedCount = g.Count(x => x.Draft.IsPublished),
          ScheduledCount = g.Count(x => x.Draft.ScheduledAt != null && !x.Draft.IsPublished),
    DraftCount = g.Count(x => !x.Draft.IsPublished && x.Draft.ScheduledAt == null)
                })
  .OrderByDescending(x => x.Count)
   .ToList();

            // Co-occurrence: which interest tags appear together most often
       var coOccurrences = drafts
       .Where(d => d.InterestTags.Count >= 2)
        .SelectMany(d =>
                {
      var tags = d.InterestTags.OrderBy(t => t.InterestTagId).ToList();
  var pairs = new List<(int A, int B, string AName, string BName)>();
          for (int i = 0; i < tags.Count; i++)
              for (int j = i + 1; j < tags.Count; j++)
         pairs.Add((tags[i].InterestTagId, tags[j].InterestTagId, tags[i].NameEN, tags[j].NameEN));
       return pairs;
         })
       .GroupBy(p => new { p.A, p.B, p.AName, p.BName })
        .Select(g => new { TagA = g.Key.AName, TagB = g.Key.BName, Count = g.Count() })
                .OrderByDescending(x => x.Count)
           .Take(10)
          .ToList();

            // Unused tags
        var allIndustries = await _db.IndustryTags.AsNoTracking().ToListAsync();
  var allInterests = await _db.InterestTags.AsNoTracking().ToListAsync();
            var usedIndustryIds = industryDistribution.Select(d => d.TagId).ToHashSet();
    var usedInterestIds = interestDistribution.Select(d => d.TagId).ToHashSet();
     var unusedIndustries = allIndustries.Where(i => !usedIndustryIds.Contains(i.IndustryTagId)).Select(i => new { i.IndustryTagId, i.NameEN, i.NameZH }).ToList();
    var unusedInterests = allInterests.Where(i => !usedInterestIds.Contains(i.InterestTagId)).Select(i => new { i.InterestTagId, i.NameEN, i.NameZH }).ToList();

      // Recent trend: articles tagged in last 7 days vs previous 7 days
 var now = DateTime.Now;
        var recentDrafts = drafts.Where(d => d.CreatedAt >= now.AddDays(-7)).Count();
       var previousDrafts = drafts.Where(d => d.CreatedAt >= now.AddDays(-14) && d.CreatedAt < now.AddDays(-7)).Count();

     // Build stats summary for AI
            var statsSummary = new StringBuilder();
            statsSummary.AppendLine($"Total articles with tags: {drafts.Count}");
        statsSummary.AppendLine($"Articles tagged in last 7 days: {recentDrafts}, previous 7 days: {previousDrafts}");
            statsSummary.AppendLine();
     statsSummary.AppendLine("Industry tag usage:");
            foreach (var ind in industryDistribution)
      statsSummary.AppendLine($"  {ind.NameEN} ({ind.NameZH}): {ind.Count} total, {ind.PublishedCount} published, {ind.ScheduledCount} scheduled, {ind.DraftCount} draft");
            statsSummary.AppendLine();
            statsSummary.AppendLine("Interest tag usage:");
 foreach (var intr in interestDistribution)
    statsSummary.AppendLine($"  {intr.NameEN} ({intr.NameZH}): {intr.Count} total, {intr.PublishedCount} published, {intr.ScheduledCount} scheduled, {intr.DraftCount} draft");
            statsSummary.AppendLine();
      statsSummary.AppendLine("Top co-occurring interest tags:");
            foreach (var co in coOccurrences)
    statsSummary.AppendLine($"  {co.TagA} + {co.TagB}: {co.Count} times");
       statsSummary.AppendLine();
            statsSummary.AppendLine($"Unused industry tags: {unusedIndustries.Count}");
            foreach (var u in unusedIndustries) statsSummary.AppendLine($"  {u.NameEN} ({u.NameZH})");
       statsSummary.AppendLine($"Unused interest tags: {unusedInterests.Count}");
   foreach (var u in unusedInterests) statsSummary.AppendLine($"  {u.NameEN} ({u.NameZH})");

   // Call AI for analysis
            string? aiAnalysis = null;
      var apiKey = _config["OpenAIAISuggestedClassification:ApiKey"];
      var baseUrl = _config["OpenAIAISuggestedClassification:BaseUrl"] ?? "https://api.openai.com/";
            if (!string.IsNullOrWhiteSpace(apiKey) && drafts.Count > 0)
            {
        try
                {
          var client = _httpFactory.CreateClient();
     client.BaseAddress = new Uri(baseUrl);
         client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

   var aiPrompt = new StringBuilder();
 aiPrompt.AppendLine("You are a publishing analytics assistant for a news platform. Analyze the following tag usage data and provide actionable insights for a consultant.");
 aiPrompt.AppendLine("Please produce a clear, professional analysis in well-formed English. Use full sentences with correct grammar and punctuation. For each section below, write2-4 complete sentences; avoid fragmented lists or colon-separated phrase entries.");
 aiPrompt.AppendLine();
 aiPrompt.AppendLine("Include the following sections in your analysis. For each section, write in complete sentences:");
 aiPrompt.AppendLine("1) Content Coverage — Describe which industries and topics are well covered and which are underrepresented.");
 aiPrompt.AppendLine("2) Trending Topics — Explain which topics show recent increases or decreases in tagging activity and why they may be trending.");
 aiPrompt.AppendLine("3) Content Gaps — Identify unused or underused tags that represent possible missed opportunities and describe the opportunity clearly.");
 aiPrompt.AppendLine("4) Audience Targeting — Based on tag distribution, describe the audience segments that are well served and those that are neglected.");
 aiPrompt.AppendLine("5) Publishing Recommendations — Provide two to three specific, actionable content strategy recommendations (each recommendation should be a short paragraph).");
 aiPrompt.AppendLine("6) Tag Correlation Insights — Describe interesting patterns from tags that co-occur and what they imply for topic bundling or cross-promotion.");
 aiPrompt.AppendLine();
 aiPrompt.AppendLine("Present each section with a short heading followed by2-4 complete sentences. Do not produce a bare list of phrases or colon-only lines; prefer short paragraphs and full sentences.");
     aiPrompt.AppendLine("DATA:");
     aiPrompt.AppendLine(statsSummary.ToString());

      var payload = new
   {
     model = "gpt-3.5-turbo",
        messages = new[]
   {
          new { role = "system", content = "You are a publishing analytics assistant. Provide data-driven insights in clear, structured format." },
           new { role = "user", content = aiPrompt.ToString() }
            },
               temperature = 0.4,
        max_tokens = 1500
     };

      var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
     using var resp = await client.PostAsync("v1/chat/completions", content);
     if (resp.IsSuccessStatusCode)
          {
            var body = await resp.Content.ReadAsStringAsync();
       using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
           aiAnalysis = choices[0].GetProperty("message").GetProperty("content").GetString();
           }
         }
    else
           {
          _logger.LogWarning("TagAnalytics AI call failed: {status}", resp.StatusCode);
     }
            }
            catch (Exception ex)
        {
        _logger.LogError(ex, "TagAnalytics AI analysis error");
              }
            }

            return Ok(new TagAnalyticsResponseDto
   {
            GeneratedAt = DateTime.Now,
     TotalTaggedArticles = drafts.Count,
      RecentTaggedArticles = recentDrafts,
    PreviousPeriodTaggedArticles = previousDrafts,
    IndustryDistribution = industryDistribution,
     InterestDistribution = interestDistribution,
   TopCoOccurrences = coOccurrences.Select(c => new CoOccurrenceDto { TagA = c.TagA, TagB = c.TagB, Count = c.Count }).ToList(),
    UnusedIndustryTags = unusedIndustries.Select(u => new UnusedTagDto { TagId = u.IndustryTagId, NameEN = u.NameEN, NameZH = u.NameZH }).ToList(),
                UnusedInterestTags = unusedInterests.Select(u => new UnusedTagDto { TagId = u.InterestTagId, NameEN = u.NameEN, NameZH = u.NameZH }).ToList(),
     AiAnalysis = aiAnalysis
    });
        }

        // ?????????????????????? Helper Methods ??????????????????????

  /// <summary>
 /// Builds an image generation prompt from the article's Chinese content,
 /// emphasising nouns and adjectives, avoiding text instructions.
 /// </summary>
 private string BuildHeroImagePrompt(NewsArticle? article)
 {
 if (article == null)
 return "A professional, visually striking editorial photograph with soft lighting, modern composition, and no text or writing.";

 // Use Chinese full content as primary source for analysis
 var content = article.FullContentZH ?? article.OriginalContent ?? article.TitleZH ?? "";
 // Truncate for prompt length safety
 if (content.Length >1500) content = content.Substring(0,1500);

 var prompt = new StringBuilder();
 prompt.AppendLine("Create a visually compelling, professional editorial hero image inspired by the following article content.");
 prompt.AppendLine("Focus on the key subjects, objects, settings, and emotional atmosphere described in the article.");
 prompt.AppendLine("Emphasize concrete nouns (places, objects, scenes) and descriptive adjectives (colors, textures, moods, scale).");
 prompt.AppendLine("IMPORTANT: Do not include any English and Chinese text, words, letters, numbers, watermarks, logos, or written characters in the image.");
 prompt.AppendLine("IMPORTANT: Do not include any human depictions, Avoid humanoid figures, human faces and human silhouettes.");
 prompt.AppendLine("Avoid signages, banners, screens, book covers, newspapers, product labels, or any elements that display text and characters. Favor visual elements that convey meaning without written language.");
 prompt.AppendLine();
 prompt.AppendLine("Article content for visual inspiration:");
 prompt.AppendLine(content);

 return prompt.ToString();
 }

 /// <summary>
 /// Try parse string content as JSON object; trims code fences
 /// </summary>
 private static JsonElement? TryParseJsonLike(string? text)
 {
 if (string.IsNullOrWhiteSpace(text)) return null;
 text = text.Trim();
 if (text.StartsWith("```"))
 {
 var idx = text.IndexOf("\n");
 if (idx >=0) text = text.Substring(idx +1).Trim();
 if (text.EndsWith("```")) text = text.Substring(0, text.Length -3).Trim();
 }

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

 /// <summary>
 /// Removes disallowed characters from article titles: | ~ - < > *
 /// </summary>
 private static string CleanTitle(string title)
 {
 if (string.IsNullOrWhiteSpace(title)) return title;
 var cleaned = Regex.Replace(title, "[|~\\-<>*]", " ");
 cleaned = Regex.Replace(cleaned, "\\s{2,}", " ").Trim();
 return cleaned;
 }

 /// <summary>
 /// Saves image locally from URL or data URI, returns relative URL
 /// </summary>
 private async Task<string> SaveImageLocally(int articleId, string url)
 {
 if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
 {
 var comma = url.IndexOf(',');
 if (comma <=0) throw new InvalidOperationException("invalid data URI");
 var meta = url.Substring(5, comma -5);
 var base64 = url.Substring(comma +1);
 var bytes = Convert.FromBase64String(base64);

 string ext = "jpg";
 if (meta.Contains("png")) ext = "png";
 else if (meta.Contains("gif")) ext = "gif";
 else if (meta.Contains("jpeg")) ext = "jpg";

 var fileName = $"hero_{articleId}.{ext}";
 var dir = Path.Combine(_env.WebRootPath ?? "wwwroot", "assets", "generated");
 Directory.CreateDirectory(dir);
 var path = Path.Combine(dir, fileName);
 await System.IO.File.WriteAllBytesAsync(path, bytes);
 return $"/assets/generated/{fileName}";
 }
 else if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
 {
 // If URL is absolute, just return it (LocalImageAdapter should normally download)
 return url;
 }
 else
 {
 return url.StartsWith("/") ? url : "/assets/generated/" + url;
 }
 }

 /// <summary>
 /// Builds the classification prompt for AI tag suggestion
 /// </summary>
 private string BuildClassificationPrompt(string textZH, string textEN, List<IndustryTag> industries, List<InterestTag> interests)
 {
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
 sb.AppendLine("Article content (Chinese - primary for analysis):");
 sb.AppendLine(textZH.Length >2000 ? textZH.Substring(0,2000) + "..." : textZH);
 if (!string.IsNullOrWhiteSpace(textEN))
 {
 sb.AppendLine();
 sb.AppendLine("Article content (English - supplementary):");
 sb.AppendLine(textEN.Length >2000 ? textEN.Substring(0,2000) + "..." : textEN);
 }
 sb.AppendLine();
 sb.AppendLine("Return only JSON. Example: {\"industryId\":2, \"interestIds\": [5,7] }");
 return sb.ToString();
 }

 /// <summary>
 /// Calls OpenAI for classification and parses the response
 /// </summary>
 private async Task<SuggestResultDto> CallOpenAIForClassification(HttpClient client, string prompt, int articleId, List<int> validIndustryIds, List<int> validInterestIds)
 {
 var payload = new
 {
 model = "gpt-3.5-turbo",
 messages = new[]
 {
 new { role = "system", content = "You are a helpful classification assistant." },
 new { role = "user", content = prompt }
 },
 temperature =0.0,
 max_tokens =200
 };

 var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
 using var resp = await client.PostAsync("v1/chat/completions", content);
 var body = await resp.Content.ReadAsStringAsync();

 if (!resp.IsSuccessStatusCode)
 {
 _logger.LogWarning("OpenAI classify failed: {status} {body}", resp.StatusCode, body);
 return new SuggestResultDto { NewsArticleId = articleId, Error = $"AI error: {resp.StatusCode}" };
 }

 using var doc = JsonDocument.Parse(body);
 var root = doc.RootElement;
 if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() ==0)
 return new SuggestResultDto { NewsArticleId = articleId, Error = "no choices in response" };

 var message = choices[0].GetProperty("message").GetProperty("content").GetString();
 var parsed = TryParseJsonLike(message);
 if (parsed == null)
 return new SuggestResultDto { NewsArticleId = articleId, Error = "failed to parse AI response", RawSuggestion = message };

 var rootEl = parsed.Value;
 int? industryId = null;
 var interestIds = new List<int>();

 if (rootEl.TryGetProperty("industryId", out var indEl) && indEl.ValueKind == JsonValueKind.Number && indEl.TryGetInt32(out var indVal))
 {
 if (validIndustryIds.Contains(indVal)) industryId = indVal;
 }
 if (rootEl.TryGetProperty("interestIds", out var ints) && ints.ValueKind == JsonValueKind.Array)
 {
 foreach (var el in ints.EnumerateArray())
 {
 if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var iid))
 {
 if (validInterestIds.Contains(iid)) interestIds.Add(iid);
 }
 }
 }

 return new SuggestResultDto
 {
 NewsArticleId = articleId,
 IndustryTagId = industryId,
 InterestTagIds = interestIds,
 RawSuggestion = message
 };
 }

 // ?????????????????????? Inner DTOs ??????????????????????
 public class SuggestRequestDto { public List<int>? ArticleIds { get; set; } }
 public class SuggestResultDto { public int NewsArticleId { get; set; } public int? IndustryTagId { get; set; } public List<int> InterestTagIds { get; set; } = new(); public string? RawSuggestion { get; set; } public string? Error { get; set; } }

 }
}
