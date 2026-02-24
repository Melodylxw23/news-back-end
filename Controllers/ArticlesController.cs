using HtmlAgilityPack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;
using News_Back_end.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using static News_Back_end.Controllers.SourcesController;

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
        private readonly ArticleProcessor _processor;

        public ArticlesController(MyDBContext db, CrawlerFactory factory, IServiceProvider services, ArticleProcessor processor, ITranslationService? translationService = null)
        {
            _db = db;
            _factory = factory;
            _services = services;
            _translationService = translationService;
            _processor = processor;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

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
                    a.TitleZH,
                    a.TitleEN,
                    a.OriginalContent ?? string.Empty,
                    a.OriginalLanguage ?? string.Empty,
                    a.TranslationLanguage,
                    a.TranslationStatus,
                    a.SourceURL,
                    a.PublishedAt,
                    a.CrawledAt,
                    a.SourceId,
                    a.TranslationSavedBy,
                    a.TranslationSavedAt,
                    a.FullContentEN,
                    a.FullContentZH,
                    a.SummaryEN,
                    a.SummaryZH));

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

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, [FromQuery] string? articleStatus = null)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 10000) pageSize = 20;

            var q = _db.NewsArticles.AsQueryable();

            // Filter by TranslationStatus if ?status= is provided
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Models.SQLServer.TranslationStatus>(status, true, out var parsedStatus))
            {
                q = q.Where(a => a.TranslationStatus == parsedStatus);
            }

            // NEW: also support filtering by ArticleStatus via ?articleStatus=
            // Used by member articles page to show only consultant-published articles.
            // Example: ?articleStatus=Published  =>  ArticleStatus.Published
            if (!string.IsNullOrWhiteSpace(articleStatus) && Enum.TryParse<ArticleStatus>(articleStatus, true, out var parsedArticleStatus))
            {
                q = q.Where(a => a.Status == parsedArticleStatus);
            }

            q = q.OrderByDescending(a => a.PublishedAt ?? a.CreatedAt);

            var total = await q.LongCountAsync();
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(a => new ArticleDto(
                a.NewsArticleId,
                a.TitleZH,
                a.TitleEN,
                a.OriginalContent,
                a.OriginalLanguage,
                a.TranslationLanguage,
                a.TranslationStatus,
                a.SourceURL,
                a.PublishedAt,
                a.CrawledAt,
                a.SourceId,
                a.TranslationSavedBy,
                a.TranslationSavedAt,
                a.FullContentEN,
                a.FullContentZH,
                a.SummaryEN,
                a.SummaryZH)).ToList();

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
                .Where(a =>
                    (a.TitleZH != null && a.TitleZH.Contains(normalized)) ||
                    (a.TitleEN != null && a.TitleEN.Contains(normalized)) ||
                    (a.OriginalContent != null && a.OriginalContent.Contains(normalized)) ||
                    (a.FullContentEN != null && a.FullContentEN.Contains(normalized)) ||
                    (a.FullContentZH != null && a.FullContentZH.Contains(normalized)) ||
                    (a.SummaryEN != null && a.SummaryEN.Contains(normalized)) ||
                    (a.SummaryZH != null && a.SummaryZH.Contains(normalized))
                );

            var total = await baseQ.LongCountAsync();
            var items = await baseQ.OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(a => new ArticleDto(
                a.NewsArticleId,
                a.TitleZH,
                a.TitleEN,
                a.OriginalContent,
                a.OriginalLanguage,
                a.TranslationLanguage,
                a.TranslationStatus,
                a.SourceURL,
                a.PublishedAt,
                a.CrawledAt,
                a.SourceId,
                a.TranslationSavedBy,
                a.TranslationSavedAt,
                a.FullContentEN,
                a.FullContentZH,
                a.SummaryEN,
                a.SummaryZH)).ToList();

            return Ok(new PagedResult<ArticleDto> { Page = page, PageSize = pageSize, Total = total, Items = dtos });
        }

        // GET: /api/articles/published?page=1&pageSize=1000
        [HttpGet("published")]
        public async Task<IActionResult> Published([FromQuery] int page = 1, [FromQuery] int pageSize = 1000)
        {
            if (page <= 0) page = 1;
            const int MaxPageSize = 10000;
            if (pageSize <= 0 || pageSize > MaxPageSize) pageSize = Math.Min(pageSize <= 0 ? 1000 : pageSize, MaxPageSize);

            var q = _db.NewsArticles
                .AsNoTracking()
                .Where(a => a.PublishedAt != null && a.PublishedAt <= DateTime.UtcNow)
                .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt);

            var total = await q.LongCountAsync();
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(a => new ArticleDto(
                a.NewsArticleId,
                a.TitleZH,
                a.TitleEN,
                a.OriginalContent ?? string.Empty,
                a.OriginalLanguage ?? string.Empty,
                a.TranslationLanguage,
                a.TranslationStatus,
                a.SourceURL,
                a.PublishedAt,
                a.CrawledAt,
                a.SourceId,
                a.TranslationSavedBy,
                a.TranslationSavedAt,
                a.FullContentEN,
                a.FullContentZH,
                a.SummaryEN,
                a.SummaryZH)).ToList();

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
                a.TitleZH,
                a.TitleEN,
                SelectContentForLanguage(a, lang),
                a.OriginalLanguage,
                a.TranslationLanguage,
                a.TranslationStatus,
                a.SourceURL,
                a.PublishedAt,
                a.CrawledAt,
                a.SourceId,
                a.TranslationSavedBy,
                a.TranslationSavedAt,
                a.FullContentEN,
                a.FullContentZH,
                a.SummaryEN,
                a.SummaryZH);

            return Ok(new
            {
                Article = dto,
                OriginalContent = a.OriginalContent,
                TranslatedContent = a.TranslatedContent,
                FullContentEN = a.FullContentEN,
                FullContentZH = a.FullContentZH,
                SummaryEN = a.SummaryEN,
                SummaryZH = a.SummaryZH,
                TitleEN = a.TitleEN,
                TitleZH = a.TitleZH,
                a.NLPKeywords,
                a.NamedEntities,
                a.SentimentScore
            });
        }

        // DELETE: api/articles/{id}
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> Delete(int id)
        {
            var a = await _db.NewsArticles.FindAsync(id);
            if (a == null) return NotFound();

            var joinRows = await _db.FetchAttemptArticles.Where(x => x.NewsArticleId == id).ToListAsync();
            if (joinRows.Count > 0)
            {
                _db.FetchAttemptArticles.RemoveRange(joinRows);
            }

            _db.NewsArticles.Remove(a);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private string SelectContentForLanguage(NewsArticle a, string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                var accept = Request.Headers["Accept-Language"].ToString();
                if (!string.IsNullOrWhiteSpace(accept))
                    lang = accept.Split(',').FirstOrDefault()?.Split('-').FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(lang)) lang = null;
            lang = lang?.Trim().ToLowerInvariant();

            if (lang != null && a.TranslatedContent != null && a.TranslationLanguage != null && lang.StartsWith(a.TranslationLanguage))
                return a.TranslatedContent;

            return a.OriginalContent;
        }

        // GET: api/articles/stats
        [HttpGet("stats")]
        public async Task<IActionResult> Stats()
        {
            var total = await _db.NewsArticles.LongCountAsync();

            var translated = await _db.NewsArticles
                .AsNoTracking()
                .LongCountAsync(a => a.TranslationStatus == Models.SQLServer.TranslationStatus.Translated
                    && a.TranslationReviewedAt != null);

            var inProgress = await _db.NewsArticles
                .AsNoTracking()
                .LongCountAsync(a => a.TranslationStatus == Models.SQLServer.TranslationStatus.InProgress);

            var pending = await _db.NewsArticles
                .AsNoTracking()
                .LongCountAsync(a => a.TranslationStatus == Models.SQLServer.TranslationStatus.Pending);

            var bySource = await _db.NewsArticles
                .GroupBy(a => a.SourceId)
                .Select(g => new { SourceId = g.Key, Count = g.LongCount() })
                .ToListAsync();

            return Ok(new { total, pending, inProgress, translated, bySource });
        }

        // GET: /api/articles/fetchAttempts?limit=50
        [HttpGet("fetchAttempts")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> FetchAttempts([FromQuery] int limit = 50)
        {
            if (limit < 1) limit = 1;
            if (limit > 200) limit = 200;

            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var attempts = await _db.FetchAttempts
                .AsNoTracking()
                .Where(a => a.ApplicationUserId == userId)
                .OrderBy(a => a.FetchedAt)
                .Take(limit)
                .Select(a => new
                {
                    a.FetchAttemptId,
                    a.AttemptNumber,
                    a.FetchedAt,
                    a.MaxArticlesPerFetch,
                    a.SourceIdsSnapshot,
                    a.SummaryFormat,
                    a.SummaryLength,
                    a.SummaryWordCount,
                    Articles = a.Articles
                        .OrderBy(x => x.SortOrder)
                        .Select(x => new
                        {
                            x.FetchAttemptArticleId,
                            x.NewsArticleId,
                            TitleZH = x.NewsArticle!.TitleZH,
                            TitleEN = x.NewsArticle!.TitleEN,
                            x.NewsArticle!.SourceId,
                            x.NewsArticle!.SourceURL,
                            x.NewsArticle!.PublishedAt,
                            x.NewsArticle!.OriginalLanguage,
                            x.NewsArticle!.TranslationLanguage,
                            x.NewsArticle!.SummaryEN,
                            x.NewsArticle!.SummaryZH,
                            x.NewsArticle!.FullContentEN,
                            x.NewsArticle!.FullContentZH,
                            x.NewsArticle!.FetchedAt,
                            Status = x.NewsArticle!.Status.ToString()
                        })
                        .ToList()
                })
                .ToListAsync();

            return Ok(attempts);
        }

        // POST: /api/articles/fetchArticles
        [HttpPost("fetchArticles")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> Fetch([FromBody] JsonElement body, [FromQuery] bool debug = false)
        {
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            FetchRequestDto dto;
            try
            {
                var dtoElem = body;
                if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("dto", out var wrapper))
                    dtoElem = wrapper;

                dto = JsonSerializer.Deserialize<FetchRequestDto>(dtoElem.GetRawText(), jsonOptions) ?? new FetchRequestDto();
            }
            catch (JsonException je)
            {
                return BadRequest(new { error = "Invalid request body", detail = je.Message });
            }

            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            int totalArticlesToFetch = dto.MaxArticles
                ?? dto.MaxArticlesPerSource
                ?? dto.SourceSettingOverride?.MaxArticlesPerFetch
                ?? 5;
            totalArticlesToFetch = Math.Clamp(totalArticlesToFetch, 1, 10);

            string effectiveSummaryFormat = dto.SummaryFormat
                ?? dto.SourceSettingOverride?.SummaryFormat
                ?? "paragraph";

            string? effectiveSummaryLength = dto.SummaryLength
                ?? dto.SourceSettingOverride?.SummaryLength;

            int effectiveSummaryWordCount;
            if (dto.SummaryWordCount.HasValue)
            {
                effectiveSummaryWordCount = dto.SummaryWordCount.Value;
            }
            else if (!string.IsNullOrWhiteSpace(effectiveSummaryLength))
            {
                effectiveSummaryWordCount = effectiveSummaryLength.Trim().ToLowerInvariant() switch
                {
                    "short" => 75,
                    "medium" => 150,
                    "long" => 250,
                    _ => dto.SourceSettingOverride?.SummaryWordCount ?? 150
                };
            }
            else
            {
                effectiveSummaryWordCount = dto.SourceSettingOverride?.SummaryWordCount ?? 150;
            }

            int nextAttemptNumber = 1;
            var maxExisting = await _db.FetchAttempts
                .Where(a => a.ApplicationUserId == userId)
                .MaxAsync(a => (int?)a.AttemptNumber);
            if (maxExisting.HasValue)
                nextAttemptNumber = maxExisting.Value + 1;

            var attempt = new FetchAttempt
            {
                ApplicationUserId = userId,
                FetchedAt = DateTime.UtcNow,
                AttemptNumber = nextAttemptNumber,
                MaxArticlesPerFetch = totalArticlesToFetch,
                SourceIdsSnapshot = dto.SourceIds != null && dto.SourceIds.Count > 0
                    ? string.Join(",", dto.SourceIds)
                    : null,
                SummaryFormat = effectiveSummaryFormat,
                SummaryLength = effectiveSummaryLength,
                SummaryWordCount = effectiveSummaryWordCount
            };
            _db.FetchAttempts.Add(attempt);
            await _db.SaveChangesAsync();

            var sourcesQuery = _db.Sources.AsQueryable();
            if (dto.SourceIds != null && dto.SourceIds.Count > 0)
                sourcesQuery = sourcesQuery.Where(s => dto.SourceIds.Contains(s.SourceId));

            var sources = await sourcesQuery.Where(s => s.IsActive).ToListAsync();

            var settings = new SourceDescriptionSetting
            {
                MinArticleLength = 0,
                MaxArticlesPerFetch = totalArticlesToFetch,
                TranslateOnFetch = true,
                IncludeEnglishSummary = true,
                IncludeChineseSummary = true,
                SummaryWordCount = effectiveSummaryWordCount,
                SummaryTone = dto.SummaryTone ?? dto.SourceSettingOverride?.SummaryTone ?? "neutral",
                SummaryFormat = effectiveSummaryFormat,
                SummaryLanguage = dto.SummaryLanguage ?? dto.SourceSettingOverride?.SummaryLanguage ?? "EN",
                CustomKeyPoints = dto.CustomKeyPoints ?? dto.SourceSettingOverride?.CustomKeyPoints,
                SummaryFocus = dto.SourceSettingOverride?.SummaryFocus
            };

            var results = new List<object>();
            int attemptSortOrder = 0;
            int remainingGlobal = totalArticlesToFetch;

            foreach (var src in sources)
            {
                if (remainingGlobal <= 0) break;

                settings.MaxArticlesPerFetch = remainingGlobal;

                Console.WriteLine($"[FetchDebug] SourceId={src.SourceId} Name={src.Name} remainingGlobal={remainingGlobal} SummaryFormat={settings.SummaryFormat} SummaryWordCount={settings.SummaryWordCount}");

                CrawlResult? crawlResult = null;
                List<CrawlerDTO> rawArticles = new List<CrawlerDTO>();
                var sw = Stopwatch.StartNew();
                string? crawlError = null;

                var unified = _services.GetService(typeof(UnifiedCrawlerService)) as UnifiedCrawlerService;
                if (unified != null)
                {
                    try
                    {
                        crawlResult = await unified.CrawlAsync(src, settings, dto.Force, userId);
                    }
                    catch (Exception ex)
                    {
                        crawlError = ex.Message;
                        crawlResult = new CrawlResult { RawCount = 0, DuplicateSkipped = 0 };
                    }
                    finally { sw.Stop(); }
                }
                else
                {
                    var crawler = _factory.CreateCrawler(src.Type.ToString());
                    try
                    {
                        rawArticles = await crawler.CrawlAsync(src) ?? new List<CrawlerDTO>();
                    }
                    catch (Exception ex)
                    {
                        crawlError = ex.Message;
                        rawArticles = new List<CrawlerDTO>();
                    }
                    finally { sw.Stop(); }
                }

                var processedArticles = new List<ArticleDtos>();
                var entitiesToSave = new List<NewsArticle>();
                int duplicatesSkipped = 0;

                if (crawlResult != null)
                {
                    processedArticles = crawlResult.Processed;
                    duplicatesSkipped = crawlResult.DuplicateSkipped;

                    foreach (var article in processedArticles)
                    {
                        if (remainingGlobal <= 0) break;

                        if (!dto.Force && !string.IsNullOrWhiteSpace(article.SourceURL))
                        {
                            var alreadyExists = await _db.NewsArticles.AnyAsync(x => x.SourceURL == article.SourceURL);
                            if (alreadyExists)
                            {
                                duplicatesSkipped++;
                                continue;
                            }
                        }

                        var entity = new NewsArticle
                        {
                            TitleZH = article.TitleZH ?? string.Empty,
                            TitleEN = article.TitleEN,
                            OriginalContent = article.OriginalContent ?? string.Empty,
                            TranslatedContent = article.TranslatedContent,
                            FullContentEN = article.FullContentEN,
                            FullContentZH = article.FullContentZH,
                            SummaryEN = article.SummaryEN,
                            SummaryZH = article.SummaryZH,
                            SourceURL = article.SourceURL ?? string.Empty,
                            PublishedAt = article.PublishedAt == default ? DateTime.Now : article.PublishedAt,
                            OriginalLanguage = string.IsNullOrWhiteSpace(article.OriginalLanguage) ? src.Language.ToString() : article.OriginalLanguage,
                            SourceId = src.SourceId,
                            CreatedAt = DateTime.Now,
                            FetchedAt = attempt.FetchedAt,
                            TranslationSavedBy = userId,
                            TranslationSavedAt = DateTime.UtcNow
                        };

                        entitiesToSave.Add(entity);
                        remainingGlobal--;
                    }
                }
                else
                {
                    foreach (var raw in rawArticles)
                    {
                        if (remainingGlobal <= 0) break;

                        if (!string.IsNullOrWhiteSpace(raw.SourceURL) && !dto.Force)
                        {
                            var alreadyExists = await _db.NewsArticles.AnyAsync(x => x.SourceURL == raw.SourceURL);
                            if (alreadyExists)
                            {
                                duplicatesSkipped++;
                                continue;
                            }
                        }

                        var previouslyFetched = await _db.FetchedArticleUrls.AnyAsync(
                            x => x.ApplicationUserId == userId && x.SourceURL == raw.SourceURL);
                        if (previouslyFetched)
                        {
                            duplicatesSkipped++;
                            continue;
                        }

                        var article = await _processor.ProcessArticle(raw, settings);
                        if (article == null) continue;

                        var entity = new NewsArticle
                        {
                            TitleZH = article.TitleZH ?? string.Empty,
                            TitleEN = article.TitleEN,
                            OriginalContent = article.OriginalContent ?? string.Empty,
                            TranslatedContent = article.TranslatedContent,
                            FullContentEN = article.FullContentEN,
                            FullContentZH = article.FullContentZH,
                            SummaryEN = article.SummaryEN,
                            SummaryZH = article.SummaryZH,
                            SourceURL = article.SourceURL ?? string.Empty,
                            PublishedAt = article.PublishedAt == default ? DateTime.Now : article.PublishedAt,
                            OriginalLanguage = string.IsNullOrWhiteSpace(article.OriginalLanguage) ? src.Language.ToString() : article.OriginalLanguage,
                            SourceId = src.SourceId,
                            CreatedAt = DateTime.Now,
                            FetchedAt = attempt.FetchedAt,
                            TranslationSavedBy = userId,
                            TranslationSavedAt = DateTime.UtcNow
                        };

                        entitiesToSave.Add(entity);
                        processedArticles.Add(article);
                        remainingGlobal--;
                    }
                }

                Console.WriteLine($"[FetchDebug] Source={src.SourceId} entitiesToSave={entitiesToSave.Count} duplicatesSkipped={duplicatesSkipped}");

                if (entitiesToSave.Count > 0)
                {
                    _db.NewsArticles.AddRange(entitiesToSave);
                    src.LastCrawledAt = DateTime.Now;
                    await _db.SaveChangesAsync();

                    foreach (var saved in entitiesToSave)
                    {
                        _db.FetchAttemptArticles.Add(new FetchAttemptArticle
                        {
                            FetchAttemptId = attempt.FetchAttemptId,
                            NewsArticleId = saved.NewsArticleId,
                            SortOrder = attemptSortOrder++
                        });

                        if (!string.IsNullOrWhiteSpace(saved.SourceURL))
                        {
                            var alreadyTracked = await _db.FetchedArticleUrls.AnyAsync(
                                x => x.ApplicationUserId == userId && x.SourceURL == saved.SourceURL);
                            if (!alreadyTracked)
                            {
                                _db.FetchedArticleUrls.Add(new FetchedArticleUrl
                                {
                                    ApplicationUserId = userId,
                                    SourceURL = saved.SourceURL,
                                    FetchedAt = attempt.FetchedAt
                                });
                            }
                        }
                    }
                    await _db.SaveChangesAsync();
                }

                results.Add(new
                {
                    src.SourceId,
                    src.Name,
                    fetched = entitiesToSave.Count,
                    duplicatesSkipped,
                    rawCount = crawlResult?.RawCount ?? rawArticles.Count,
                    error = crawlError
                });
            }

            var totalAdded = results.Sum(r => (int)((dynamic)r).fetched);

            return Ok(new
            {
                attempt.FetchAttemptId,
                attempt.AttemptNumber,
                attempt.FetchedAt,
                Configuration = new
                {
                    maxArticles = totalArticlesToFetch,
                    summaryFormat = effectiveSummaryFormat,
                    summaryLength = effectiveSummaryLength,
                    summaryWordCount = effectiveSummaryWordCount,
                    sourceIds = dto.SourceIds
                },
                totalRequested = totalArticlesToFetch,
                totalAdded,
                results
            });
        }

        // DELETE: /api/articles/fetchAttempts/{id}
        [HttpDelete("fetchAttempts/{id:int}")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> DeleteFetchAttempt(int id)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var attempt = await _db.FetchAttempts
                .Include(a => a.Articles)
                .FirstOrDefaultAsync(a => a.FetchAttemptId == id && a.ApplicationUserId == userId);
            if (attempt == null) return NotFound();

            var articleIds = attempt.Articles.Select(a => a.NewsArticleId).ToList();
            _db.FetchAttemptArticles.RemoveRange(attempt.Articles);

            if (articleIds.Count > 0)
            {
                var articles = await _db.NewsArticles
                    .Where(a => articleIds.Contains(a.NewsArticleId))
                    .ToListAsync();
                _db.NewsArticles.RemoveRange(articles);
            }

            _db.FetchAttempts.Remove(attempt);
            await _db.SaveChangesAsync();
            await RenumberFetchAttempts(userId);

            return NoContent();
        }

        // DELETE: /api/articles/fetchAttempts/{attemptId}/articles/{articleId}
        [HttpDelete("fetchAttempts/{attemptId:int}/articles/{articleId:int}")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> DeleteArticleFromFetchAttempt(int attemptId, int articleId)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var attempt = await _db.FetchAttempts
                .FirstOrDefaultAsync(a => a.FetchAttemptId == attemptId && a.ApplicationUserId == userId);
            if (attempt == null) return NotFound("Fetch attempt not found.");

            var joinRow = await _db.FetchAttemptArticles
                .FirstOrDefaultAsync(x => x.FetchAttemptId == attemptId && x.NewsArticleId == articleId);
            if (joinRow == null) return NotFound("Article not found in this fetch attempt.");

            _db.FetchAttemptArticles.Remove(joinRow);

            var article = await _db.NewsArticles.FindAsync(articleId);
            if (article != null)
            {
                _db.NewsArticles.Remove(article);
            }

            await _db.SaveChangesAsync();

            var remainingJoins = await _db.FetchAttemptArticles
                .Where(x => x.FetchAttemptId == attemptId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
            for (int i = 0; i < remainingJoins.Count; i++)
            {
                remainingJoins[i].SortOrder = i;
            }
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // POST: /api/articles/markReadyForPublish
        [HttpPost("markReadyForPublish")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> MarkReadyForPublish([FromBody] BatchIdsDto dto)
        {
            if (dto.ArticleIds == null || dto.ArticleIds.Count == 0)
                return BadRequest("articleIds required");

            var articles = await _db.NewsArticles
                .Where(a => dto.ArticleIds.Contains(a.NewsArticleId))
                .ToListAsync();

            foreach (var article in articles)
            {
                article.Status = ArticleStatus.ReadyForPublish;
                article.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { updated = articles.Count });
        }

        // DELETE: /api/articles/fetchAttempts
        [HttpDelete("fetchAttempts")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> DeleteAllFetchAttempts()
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var attempts = await _db.FetchAttempts
                .Include(a => a.Articles)
                .Where(a => a.ApplicationUserId == userId)
                .ToListAsync();

            if (attempts.Count == 0) return NoContent();

            var allArticleIds = attempts
                .SelectMany(a => a.Articles.Select(x => x.NewsArticleId))
                .Distinct()
                .ToList();

            var allJoinRows = attempts.SelectMany(a => a.Articles).ToList();
            _db.FetchAttemptArticles.RemoveRange(allJoinRows);

            if (allArticleIds.Count > 0)
            {
                var articles = await _db.NewsArticles
                    .Where(a => allArticleIds.Contains(a.NewsArticleId))
                    .ToListAsync();
                _db.NewsArticles.RemoveRange(articles);
            }

            _db.FetchAttempts.RemoveRange(attempts);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        private async Task RenumberFetchAttempts(string userId)
        {
            var remaining = await _db.FetchAttempts
                .Where(a => a.ApplicationUserId == userId)
                .OrderBy(a => a.FetchedAt)
                .ToListAsync();

            for (int i = 0; i < remaining.Count; i++)
            {
                remaining[i].AttemptNumber = i + 1;
            }

            if (remaining.Count > 0)
                await _db.SaveChangesAsync();
        }
    }
}