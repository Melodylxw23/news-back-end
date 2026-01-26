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

namespace News_Back_end.Controllers
{
 [ApiController]
 [Route("api/[controller]")]
 public class PublishController : ControllerBase
 {
 private readonly MyDBContext _db;
 private readonly IImageGenerationService? _imageService;
 private readonly IPublicationService _pubService;

 public PublishController(MyDBContext db, IPublicationService pubService, IImageGenerationService? imageService = null)
 {
 _db = db;
 _imageService = imageService;
 _pubService = pubService;
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

 // POST /api/publish/{id}/generate-hero
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
 // fallback to placeholder URL when service not configured
 var placeholder = $"/assets/generated/hero_{id}.jpg";
 draft.HeroImageUrl = placeholder;
 draft.HeroImageSource = "generated";
 draft.UpdatedAt = DateTime.Now;
 await _db.SaveChangesAsync();
 return Ok(new { url = placeholder });
 }

 // build prompt using article content and optional override
 var article = await _db.NewsArticles.FindAsync(id);
 var baseText = article?.TitleEN ?? article?.TitleZH ?? article?.OriginalContent ?? "";
 var prompt = (dto.PromptOverride?.Trim().Length >0) ? dto.PromptOverride!.Trim() : $"Professional hero image for article: {baseText}";

 try
 {
 var url = await _imageService.GenerateImageAsync(id, prompt, dto.Style);
 if (url == null)
 {
 return StatusCode(502, "image generation failed");
 }

 draft.HeroImageUrl = url;
 draft.HeroImageSource = "generated";
 draft.UpdatedAt = DateTime.Now;
 await _db.SaveChangesAsync();

 return Ok(new { url });
 }
 catch (Exception ex)
 {
 Console.WriteLine("GenerateHero error: " + ex);
 return StatusCode(500, "error generating image");
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
 }
}
