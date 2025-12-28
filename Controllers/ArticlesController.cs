using Microsoft.AspNetCore.Mvc;
using News_Back_end.Models.SQLServer;
using News_Back_end.DTOs;
using Microsoft.EntityFrameworkCore;
using News_Back_end.Services;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace News_Back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArticlesController : ControllerBase
    {
        private readonly MyDBContext _db;
        private readonly CrawlerFactory _factory;
        private readonly ITranslationService? _translationService;

        public ArticlesController(MyDBContext db, CrawlerFactory factory, ITranslationService? translationService = null)
        {
            _db = db;
            _factory = factory;
            _translationService = translationService;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 20;

            var q = _db.NewsArticles.OrderByDescending(a => a.PublishedAt ?? a.CreatedAt);
            var total = await q.LongCountAsync();
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(a => new ArticleDto(
                a.NewsArticleId,
                a.Title,
                a.OriginalContent,
                a.OriginalLanguage,
                a.TranslationLanguage,
                a.TranslationStatus,
                a.SourceURL,
                a.PublishedAt,
                a.CrawledAt,
                a.SourceId)).ToList();

            return Ok(new PagedResult<ArticleDto> { Page = page, PageSize = pageSize, Total = total, Items = dtos });
        }

        // GET: api/articles/search?q=bitcoin&page=1&pageSize=20
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 200) pageSize = 20;
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("query required");

            var normalized = q.Trim();
            var baseQ = _db.NewsArticles
                .AsNoTracking()
                .Where(a => a.Title.Contains(normalized) || a.OriginalContent.Contains(normalized));

            var total = await baseQ.LongCountAsync();
            var items = await baseQ.OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(a => new ArticleDto(
                a.NewsArticleId,
                a.Title,
                a.OriginalContent,
                a.OriginalLanguage,
                a.TranslationLanguage,
                a.TranslationStatus,
                a.SourceURL,
                a.PublishedAt,
                a.CrawledAt,
                a.SourceId)).ToList();

            return Ok(new PagedResult<ArticleDto> { Page = page, PageSize = pageSize, Total = total, Items = dtos });
        }

        //// GET: api/articles/source/{sourceId}?limit=0  (limit 0 = all)
        //[HttpGet("source/{sourceId:int}")]
        //public async Task<IActionResult> GetBySource(int sourceId, [FromQuery] int limit = 20)
        //{
        //    IQueryable<NewsArticle> q = _db.NewsArticles.Where(a => a.SourceId == sourceId).OrderByDescending(a => a.PublishedAt ?? a.CreatedAt);
        //    if (limit > 0) q = q.Take(limit);
        //    var items = await q.AsNoTracking().ToListAsync();
        //    var dtos = items.Select(a => new ArticleDto(
        //        a.NewsArticleId,
        //        a.Title,
        //        a.OriginalContent,
        //        a.OriginalLanguage,
        //        a.TranslationLanguage,
        //        a.TranslationStatus,
        //        a.SourceURL,
        //        a.PublishedAt,
        //        a.CrawledAt,
        //        a.SourceId)).ToList();
        //    return Ok(dtos);
        //}

        // GET: api/articles/{id}?lang=zh
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, [FromQuery] string? lang)
        {
            var a = await _db.NewsArticles.FindAsync(id);
            if (a == null) return NotFound();

            var dto = new ArticleDto(
                a.NewsArticleId,
                a.Title,
                SelectContentForLanguage(a, lang),
                a.OriginalLanguage,
                a.TranslationLanguage,
                a.TranslationStatus,
                a.SourceURL,
                a.PublishedAt,
                a.CrawledAt,
                a.SourceId);

            return Ok(new { Article = dto, OriginalContent = a.OriginalContent, TranslatedContent = a.TranslatedContent, a.NLPKeywords, a.NamedEntities, a.SentimentScore });
        }

        // POST: api/articles
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateArticleDto dto)
        {
            if (dto == null) return BadRequest();
            if (string.IsNullOrWhiteSpace(dto.SourceURL)) return BadRequest("SourceURL is required");

            bool exists = await _db.NewsArticles.AnyAsync(x => x.SourceURL == dto.SourceURL);
            if (exists) return Conflict("Article with same SourceURL already exists");

            var entity = new NewsArticle
            {
                Title = dto.Title ?? string.Empty,
                OriginalContent = dto.OriginalContent ?? string.Empty,
                SourceURL = dto.SourceURL,
                PublishedAt = dto.PublishedAt,
                OriginalLanguage = dto.OriginalLanguage ?? "",
                SourceId = dto.SourceId,
                CreatedAt = DateTime.Now
            };

            _db.NewsArticles.Add(entity);
            await _db.SaveChangesAsync();

            var resultDto = new ArticleDto(entity.NewsArticleId, entity.Title, entity.OriginalContent, entity.OriginalLanguage, entity.TranslationLanguage, entity.TranslationStatus, entity.SourceURL, entity.PublishedAt, entity.CrawledAt, entity.SourceId);

            return CreatedAtAction(nameof(GetById), new { id = entity.NewsArticleId }, resultDto);
        }

        // PUT: api/articles/{id}
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateArticleDto dto)
        {
            var a = await _db.NewsArticles.FindAsync(id);
            if (a == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.Title)) a.Title = dto.Title;
            if (dto.OriginalContent != null) a.OriginalContent = dto.OriginalContent;
            if (!string.IsNullOrWhiteSpace(dto.SourceURL)) a.SourceURL = dto.SourceURL;
            if (dto.PublishedAt.HasValue) a.PublishedAt = dto.PublishedAt;
            if (dto.SourceId.HasValue) a.SourceId = dto.SourceId;

            a.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/articles/{id}
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var a = await _db.NewsArticles.FindAsync(id);
            if (a == null) return NotFound();

            _db.NewsArticles.Remove(a);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Helper DTOs for create/update
        public class CreateArticleDto
        {
            public string? Title { get; set; }
            public string? OriginalContent { get; set; }
            public string SourceURL { get; set; } = null!;
            public DateTime? PublishedAt { get; set; }
            public string? OriginalLanguage { get; set; }
            public int? SourceId { get; set; }
        }

        public class UpdateArticleDto
        {
            public string? Title { get; set; }
            public string? OriginalContent { get; set; }
            public string? SourceURL { get; set; }
            public DateTime? PublishedAt { get; set; }
            public int? SourceId { get; set; }
        }

        public class TranslateDto
        {
            public string TargetLanguage { get; set; } = null!; // e.g. "en" or "zh"
        }

        private string SelectContentForLanguage(NewsArticle a, string? lang)
        {
            // prefer explicit lang parameter, otherwise use Accept-Language header
            if (string.IsNullOrWhiteSpace(lang))
            {
                var accept = Request.Headers["Accept-Language"].ToString();
                if (!string.IsNullOrWhiteSpace(accept))
                    lang = accept.Split(',').FirstOrDefault()?.Split('-').FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(lang)) lang = null;

            // normalize
            lang = lang?.Trim().ToLowerInvariant();

            if (lang != null && a.TranslatedContent != null && a.TranslationLanguage != null && lang.StartsWith(a.TranslationLanguage))
                return a.TranslatedContent;

            // fallback to original
            return a.OriginalContent;
        }

        // POST: api/articles/{id}/translate
        [HttpPost("{id:int}/translate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Translate(int id, [FromBody] TranslateDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.TargetLanguage)) return BadRequest("target language required");
            var a = await _db.NewsArticles.FindAsync(id);
            if (a == null) return NotFound();
            if (_translationService == null) return BadRequest("translation service not available");

            // perform translation
            var translated = await _translationService.TranslateAsync(a.OriginalContent ?? string.Empty, dto.TargetLanguage);
            a.TranslatedContent = translated;
            a.TranslationLanguage = dto.TargetLanguage;
            a.TranslationStatus = Models.SQLServer.TranslationStatus.Reviewed;
            a.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            var dtoOut = new ArticleDto(a.NewsArticleId, a.Title, SelectContentForLanguage(a, dto.TargetLanguage), a.OriginalLanguage, a.TranslationLanguage, a.TranslationStatus, a.SourceURL, a.PublishedAt, a.CrawledAt, a.SourceId);
            return Ok(dtoOut);
        }

        // GET: api/articles/stats
        [HttpGet("stats")]
        public async Task<IActionResult> Stats()
        {
            var total = await _db.NewsArticles.LongCountAsync();
            var bySource = await _db.NewsArticles.GroupBy(a => a.SourceId).Select(g => new { SourceId = g.Key, Count = g.LongCount() }).ToListAsync();
            return Ok(new { total, bySource });
        }
    }
}
