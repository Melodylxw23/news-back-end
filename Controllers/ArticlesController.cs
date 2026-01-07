using Microsoft.AspNetCore.Mvc;
using News_Back_end.Models.SQLServer;
using News_Back_end.DTOs;
using Microsoft.EntityFrameworkCore;
using News_Back_end.Services;
using HtmlAgilityPack;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceProvider _services;

        public ArticlesController(MyDBContext db, CrawlerFactory factory, IServiceProvider services, ITranslationService? translationService = null)
        {
            _db = db;
            _factory = factory;
            _services = services;
            _translationService = translationService;
        }

        // Clean HTML and decode entities to plain text
        private static string CleanHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var text = doc.DocumentNode.InnerText ?? string.Empty;
                return WebUtility.HtmlDecode(text).Trim();
            }
            catch
            {
                return WebUtility.HtmlDecode(html).Replace("<", " ").Replace(">", " ").Trim();
            }
        }

        // DTOs reused from NewsArticlesController
        public class TranslatePreviewDto { public string TargetLanguage { get; set; } = "en"; }
        public class TranslateAndSaveDto { public string TargetLanguage { get; set; } = "en"; public string? EditedTranslation { get; set; } = null; public bool AutoTranslateIfNoEdit { get; set; } = true; }

        // GET: /api/articles/recent?limit=20
        [HttpGet("recent")]
        public async Task<IActionResult> Recent([FromQuery] int? limit = 20)
        {
            const int AbsoluteMax = 10000;

            IQueryable<ArticleDto> query = _db.NewsArticles
                .AsNoTracking()
                .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
                .Select(a => new ArticleDto(
                    a.NewsArticleId,
                    a.Title,
                    a.OriginalContent ?? string.Empty,
                    a.OriginalLanguage ?? string.Empty,
                    a.TranslationLanguage,
                    a.TranslationStatus,
                    a.SourceURL,
                    a.PublishedAt,
                    a.CrawledAt,
                    a.SourceId,
                    a.TranslationSavedBy,
                    a.TranslationSavedAt));

            if (limit.HasValue)
            {
                if (limit.Value == 0)
                {
                    // no limit, return all
                }
                else
                {
                    var l = limit.Value;
                    if (l < 1) l = 1;
                    if (l > AbsoluteMax) l = AbsoluteMax;
                    query = query.Take(l);
                }
            }

            var dtos = await query.ToListAsync();
            return Ok(dtos);
        }

        // POST: /api/articles/{id}/translate-preview
        [HttpPost("{id:int}/translate-preview")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> TranslatePreview(int id, [FromBody] TranslatePreviewDto dto)
        {
            var article = await _db.NewsArticles.FindAsync(id);
            if (article == null) return NotFound();

            var translator = _translationService ?? _services.GetService<ITranslationService>();
            if (translator == null) return BadRequest("Translation service not configured.");

            var target = string.IsNullOrWhiteSpace(dto.TargetLanguage) ? "en" : dto.TargetLanguage;
            var sourceText = CleanHtml(article.OriginalContent ?? string.Empty);
            var suggested = await translator.TranslateAsync(sourceText, target);

            return Ok(new { Original = sourceText, Suggested = suggested, TargetLanguage = target });
        }

        // POST: /api/articles/{id}/translate-and-save
        [HttpPost("{id:int}/translate-and-save")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> TranslateAndSave(int id, [FromBody] TranslateAndSaveDto dto)
        {
            var article = await _db.NewsArticles.FindAsync(id);
            if (article == null) return NotFound();

            var sourceText = CleanHtml(article.OriginalContent ?? string.Empty);

            string finalText;
            if (!string.IsNullOrWhiteSpace(dto.EditedTranslation))
            {
                // edited translation provided by user — save as Pending for review
                finalText = dto.EditedTranslation!;

                if (string.Equals(finalText?.Trim(), sourceText?.Trim(), StringComparison.Ordinal))
                {
                    try
                    {
                        _db.TranslationAudits.Add(new Models.SQLServer.TranslationAudit
                        {
                            NewsArticleId = article.NewsArticleId,
                            Action = "Skipped",
                            PerformedBy = User?.Identity?.Name ?? "unknown",
                            PerformedAt = DateTime.Now,
                            Details = dto.TargetLanguage
                        });
                        await _db.SaveChangesAsync();
                    }
                    catch { }

                    return Conflict("Translation matches original content; not saved.");
                }

                article.TranslatedContent = finalText;
                article.TranslationLanguage = string.IsNullOrWhiteSpace(dto.TargetLanguage) ? "en" : dto.TargetLanguage;
                article.TranslationStatus = Models.SQLServer.TranslationStatus.Pending;
                article.TranslationReviewedBy = null;
                article.TranslationReviewedAt = null;
                article.TranslationSavedBy = User?.Identity?.Name ?? "unknown";
                article.TranslationSavedAt = DateTime.Now;
                article.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
            else if (dto.AutoTranslateIfNoEdit)
            {
                var translator = _translationService ?? _services.GetService<ITranslationService>();
                if (translator == null) return BadRequest("Translation service not configured.");
                var target = string.IsNullOrWhiteSpace(dto.TargetLanguage) ? "en" : dto.TargetLanguage;

                // mark InProgress and persist so stats/clients can see translation is running
                article.TranslationLanguage = target;
                article.TranslationStatus = Models.SQLServer.TranslationStatus.InProgress;
                article.TranslationSavedBy = User?.Identity?.Name ?? "system";
                article.TranslationSavedAt = DateTime.Now;
                article.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();

                // perform translation
                finalText = await translator.TranslateAsync(sourceText, target);

                // guard: don't persist if translated text equals cleaned original
                if (string.Equals(finalText?.Trim(), sourceText?.Trim(), StringComparison.Ordinal))
                {
                    try
                    {
                        _db.TranslationAudits.Add(new Models.SQLServer.TranslationAudit
                        {
                            NewsArticleId = article.NewsArticleId,
                            Action = "Skipped",
                            PerformedBy = User?.Identity?.Name ?? "system",
                            PerformedAt = DateTime.Now,
                            Details = target
                        });
                        await _db.SaveChangesAsync();
                    }
                    catch { }

                    // revert InProgress to Pending with no content
                    article.TranslatedContent = null;
                    article.TranslationStatus = Models.SQLServer.TranslationStatus.Pending;
                    article.TranslationReviewedBy = null;
                    article.TranslationReviewedAt = null;
                    article.UpdatedAt = DateTime.Now;
                    await _db.SaveChangesAsync();

                    return Conflict("Translation matches original content; not saved.");
                }

                // persist translated content and mark Pending for review
                article.TranslatedContent = finalText;
                article.TranslationStatus = Models.SQLServer.TranslationStatus.Pending;
                article.TranslationReviewedBy = null;
                article.TranslationReviewedAt = null;
                article.TranslationSavedBy = User?.Identity?.Name ?? "system";
                article.TranslationSavedAt = DateTime.Now;
                article.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
            else
            {
                return BadRequest("No edited translation and auto-translate disabled.");
            }

            // record audit
            try
            {
                _db.TranslationAudits.Add(new Models.SQLServer.TranslationAudit
                {
                    NewsArticleId = article.NewsArticleId,
                    Action = "Saved",
                    PerformedBy = article.TranslationSavedBy ?? "unknown",
                    PerformedAt = DateTime.Now,
                    Details = article.TranslationLanguage
                });
                await _db.SaveChangesAsync();
            }
            catch { /* non-fatal */ }

            return Ok(new {
                article.NewsArticleId,
                article.TranslationStatus,
                article.TranslationLanguage,
                TranslatedContent = article.TranslatedContent,
                SavedBy = article.TranslationSavedBy,
                SavedAt = article.TranslationSavedAt
            });
        }


        // Approve a saved translation and mark as Reviewed
        // POST: api/articles/{id}/translation/approve
        [HttpPost("{id:int}/translation/approve")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> ApproveTranslation(int id)
        {
            var a = await _db.NewsArticles.FindAsync(id);
            if (a == null) return NotFound();
            if (string.IsNullOrWhiteSpace(a.TranslatedContent)) return BadRequest("No translation available to approve.");

            a.TranslationStatus = Models.SQLServer.TranslationStatus.Translated;
            a.TranslationReviewedBy = User?.Identity?.Name ?? "unknown";
            a.TranslationReviewedAt = DateTime.Now;
            a.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            // audit approve
            try
            {
                _db.TranslationAudits.Add(new Models.SQLServer.TranslationAudit
                {
                    NewsArticleId = a.NewsArticleId,
                    Action = "Approved",
                    PerformedBy = a.TranslationReviewedBy ?? "unknown",
                    PerformedAt = DateTime.Now,
                    Details = a.TranslationLanguage
                });
                await _db.SaveChangesAsync();
            }
            catch { }

            return Ok(new
            {
                a.NewsArticleId,
                a.TranslationStatus,
                a.TranslationLanguage,
                TranslatedContent = a.TranslatedContent,
                ReviewedBy = a.TranslationReviewedBy,
                ReviewedAt = a.TranslationReviewedAt
            });
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
                a.SourceId,
                a.TranslationSavedBy,
                a.TranslationSavedAt)).ToList();

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

        

        // DELETE: api/articles/{id}
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> Delete(int id)
        {
            var a = await _db.NewsArticles.FindAsync(id);
            if (a == null) return NotFound();

            _db.NewsArticles.Remove(a);
            await _db.SaveChangesAsync();
            return NoContent();
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


        // GET: api/articles/stats
        [HttpGet("stats")]
        public async Task<IActionResult> Stats()
        {
            var total = await _db.NewsArticles.LongCountAsync();

            // Count translated: explicit Translated status OR saved translation timestamp
            var translated = await _db.NewsArticles
                .AsNoTracking()
                .LongCountAsync(a => a.TranslationSavedAt != null
                    || a.TranslationStatus == Models.SQLServer.TranslationStatus.Translated);

            // Count in-progress: statuses that indicate progress/review (adjust to your enum values)
            var inProgress = await _db.NewsArticles
                .AsNoTracking()
                .LongCountAsync(a => a.TranslationStatus == Models.SQLServer.TranslationStatus.InProgress
                    || a.TranslationStatus == Models.SQLServer.TranslationStatus.InProgress);

            // pending = remaining items
            var pending = Math.Max(0L, total - translated - inProgress);

            var bySource = await _db.NewsArticles
                .GroupBy(a => a.SourceId)
                .Select(g => new { SourceId = g.Key, Count = g.LongCount() })
                .ToListAsync();
                
            return Ok(new { total, pending, inProgress, translated, bySource });
        }

        // GET: api/articles/review?status=inprogress|pending|translated&auto=true
        // Returns articles that need review. Set auto=true to restrict to crawler auto-translated items.
        [HttpGet("review")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> Review([FromQuery] string? status = null, [FromQuery] bool? auto = null)
        {
            var q = _db.NewsArticles.AsNoTracking().AsQueryable();

            // filter by status if provided, otherwise default to items needing review
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Models.SQLServer.TranslationStatus>(status, true, out var parsed))
            {
                q = q.Where(a => a.TranslationStatus == parsed);
            }
            else
            {
                // default: include InProgress and Pending
                q = q.Where(a => a.TranslationStatus == Models.SQLServer.TranslationStatus.InProgress
                    || a.TranslationStatus == Models.SQLServer.TranslationStatus.Pending);
            }

            if (auto.HasValue)
            {
                if (auto.Value)
                    q = q.Where(a => a.TranslationSavedBy == "crawler");
                else
                    q = q.Where(a => a.TranslationSavedBy == null || a.TranslationSavedBy != "crawler");
            }

            var items = await q.OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
                .Take(1000)
                .Select(a => new ArticleDto(
                    a.NewsArticleId,
                    a.Title,
                    a.OriginalContent,
                    a.OriginalLanguage,
                    a.TranslationLanguage,
                    a.TranslationStatus,
                    a.SourceURL,
                    a.PublishedAt,
                    a.CrawledAt,
                    a.SourceId,
                    a.TranslationSavedBy,
                    a.TranslationSavedAt))
                .ToListAsync();

            return Ok(items);
        }
    }
}
