using HtmlAgilityPack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;
using News_Back_end.Services;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Linq;
using System.Net;
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
        public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 20;

            var q = _db.NewsArticles.AsQueryable();

            // Filter by translation status if provided
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Models.SQLServer.TranslationStatus>(status, true, out var parsedStatus))
            {
                q = q.Where(a => a.TranslationStatus == parsedStatus);
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

            // Count translated: ONLY manually approved/reviewed articles
            var translated = await _db.NewsArticles
                .AsNoTracking()
                .LongCountAsync(a => a.TranslationStatus == Models.SQLServer.TranslationStatus.Translated
                    && a.TranslationReviewedAt != null);

            // Count in-progress articles
            var inProgress = await _db.NewsArticles
    .AsNoTracking()
  .LongCountAsync(a => a.TranslationStatus == Models.SQLServer.TranslationStatus.InProgress);

      // Count pending articles (explicitly)
       var pending = await _db.NewsArticles
  .AsNoTracking()
      .LongCountAsync(a => a.TranslationStatus == Models.SQLServer.TranslationStatus.Pending);

            var bySource = await _db.NewsArticles
  .GroupBy(a => a.SourceId)
.Select(g => new { SourceId = g.Key, Count = g.LongCount() })
  .ToListAsync();

            return Ok(new { total, pending, inProgress, translated, bySource });
        }

        

        
        [HttpPost("fetchArticles")]
        [Authorize(Roles = "Consultant")]

        public async Task<IActionResult> Fetch([FromBody] JsonElement body, [FromQuery] bool debug = false)
        {
            // support flexible client payloads: either raw FetchRequestDto or wrapped { "dto": { ... } }
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            News_Back_end.DTOs.FetchRequestDto dto;
            try
            {
                var dtoElem = body;
                if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("dto", out var wrapper))
                    dtoElem = wrapper;

                dto = JsonSerializer.Deserialize<News_Back_end.DTOs.FetchRequestDto>(dtoElem.GetRawText(), jsonOptions)
                      ?? new News_Back_end.DTOs.FetchRequestDto();
            }
            catch (JsonException je)
            {
                return BadRequest(new { error = "Invalid request body", detail = je.Message });
            }
            var sourcesQuery = _db.Sources.AsQueryable();
            if (dto.SourceIds != null && dto.SourceIds.Count > 0)
            {
                sourcesQuery = sourcesQuery.Where(s => dto.SourceIds.Contains(s.SourceId));
            }
            var sources = await sourcesQuery.Where(s => s.IsActive).ToListAsync();

            var results = new List<object>();

            foreach (var src in sources)
            {
                // Load description settings for this source (allow per-request override)
                SourceDescriptionSetting? settings = null;
                if (dto.SourceSettingOverride != null)
                {
                    var o = dto.SourceSettingOverride;
                    settings = new SourceDescriptionSetting
                    {
                        TranslateOnFetch = o.TranslateOnFetch,
                        SummaryWordCount = o.SummaryWordCount,
                        SummaryTone = o.SummaryTone ?? "neutral",
                        SummaryFormat = o.SummaryFormat ?? "paragraph",
                        CustomKeyPoints = o.CustomKeyPoints,
                        MaxArticlesPerFetch = o.MaxArticlesPerFetch,
                        IncludeOriginalChinese = o.IncludeOriginalChinese,
                        IncludeEnglishSummary = o.IncludeEnglishSummary,
                        IncludeChineseSummary = o.IncludeChineseSummary,
                        MinArticleLength = o.MinArticleLength,
                        SummaryFocus = o.SummaryFocus,
                        SentimentAnalysisEnabled = o.SentimentAnalysisEnabled,
                        HighlightEntities = o.HighlightEntities,
                        SummaryLanguage = o.SummaryLanguage ?? "EN"
                    };
                }
                else
                {
                    settings = await _db.SourceDescriptionSettings
                        .FirstOrDefaultAsync(x => x.SourceId == src.SourceId);
                }

                // If no settings are configured for this source, use permissive defaults so fetch returns articles
                if (settings == null)
                {
                    settings = new SourceDescriptionSetting
                    {
                        MinArticleLength = 0, // accept short items
                        MaxArticlesPerFetch = 100,
                        TranslateOnFetch = true,
                        IncludeEnglishSummary = true,
                        IncludeChineseSummary = true,
                        SummaryWordCount = 150,
                        SummaryTone = "neutral",
                        SummaryFormat = "paragraph"
                    };
                }

                // Try unified crawler first (combines RSS/API/HTML) then fallback to type-specific crawler
                List<CrawlerDTO> rawArticles = new List<CrawlerDTO>();
                List<ArticleDtos>? unifiedProcessed = null;
                var sw = Stopwatch.StartNew();
                string? crawlError = null;

                var unified = _services.GetService(typeof(UnifiedCrawlerService)) as UnifiedCrawlerService;
                if (unified != null)
                {
                    try
                    {
                        unifiedProcessed = await unified.CrawlAsync(src, settings ?? new SourceDescriptionSetting());
                        // convert for diagnostics
                        rawArticles = (unifiedProcessed ?? new List<ArticleDtos>())
                            .Select(a => new CrawlerDTO { Title = a.TitleZH ?? a.TitleEN, Content = a.OriginalContent, SourceURL = a.SourceURL, PublishedDate = a.PublishedAt, OriginalLanguage = a.OriginalLanguage })
                            .ToList();
                        Console.WriteLine($"[Articles.Fetch][Unified] source={src.SourceId} name={src.Name} items={rawArticles.Count}");
                        for (int i = 0; i < Math.Min(5, rawArticles.Count); i++)
                        {
                            var r = rawArticles[i];
                            Console.WriteLine($"[Articles.Fetch][Unified] sample[{i}] title={r.Title ?? "(no title)"} url={r.SourceURL ?? "(null)"} len={ (r.Content?.Length ?? 0) }");
                        }
                    }
                    catch (Exception ex)
                    {
                        crawlError = ex.Message;
                        rawArticles = new List<CrawlerDTO>();
                        Console.WriteLine($"[Articles.Fetch][Unified] error for {src.Name}: {ex.Message}");
                    }
                    finally
                    {
                        if (sw.IsRunning) sw.Stop();
                    }
                }
                else
                {
                    // fallback to factory-created crawler
                    var crawler = _factory.CreateCrawler(src.Type.ToString());
                    try
                    {
                        rawArticles = await crawler.CrawlAsync(src) ?? new List<CrawlerDTO>();
                        Console.WriteLine($"[Articles.Fetch] source={src.SourceId} name={src.Name} rawArticles={rawArticles.Count}");
                        for (int i = 0; i < Math.Min(5, rawArticles.Count); i++)
                        {
                            var r = rawArticles[i];
                            Console.WriteLine($"[Articles.Fetch] sample[{i}] title={r.Title ?? "(no title)"} url={r.SourceURL ?? "(null)"} len={ (r.Content?.Length ?? 0) }");
                        }
                    }
                    catch (Exception ex)
                    {
                        crawlError = ex.Message;
                        rawArticles = new List<CrawlerDTO>();
                        try
                        {
                            sw.Stop();
                            var metric = new FetchMetric
                            {
                                SourceId = src.SourceId,
                                Timestamp = DateTime.Now,
                                Success = false,
                                ItemsFetched = 0,
                                DurationMs = (int)sw.ElapsedMilliseconds,
                                ErrorMessage = crawlError
                            };
                            _db.FetchMetrics.Add(metric);
                            await _db.SaveChangesAsync();
                        }
                        catch { }

                        results.Add(new { src.SourceId, src.Name, error = ex.Message });
                        continue;
                    }
                    finally
                    {
                        if (sw.IsRunning) sw.Stop();
                    }
                }

                // record successful crawl metric (best-effort)
                try
                {
                    var metric = new FetchMetric
                    {
                        SourceId = src.SourceId,
                        Timestamp = DateTime.Now,
                        Success = string.IsNullOrWhiteSpace(crawlError),
                        ItemsFetched = rawArticles?.Count ?? 0,
                        DurationMs = (int)sw.ElapsedMilliseconds,
                        ErrorMessage = crawlError
                    };
                    _db.FetchMetrics.Add(metric);
                    await _db.SaveChangesAsync();
                }
                catch { }

                var processedArticles = new List<ArticleDtos>();
                var entitiesToSave = new List<NewsArticle>();
                int updatedCount = 0;

                foreach (var raw in rawArticles.Take(settings?.MaxArticlesPerFetch ?? 10))
                {
                    try
                    {
                        var article = await _processor.ProcessArticle(raw, settings);
                        if (article == null) continue;

                        // persist to DB as NewsArticle (defer SaveChanges until after loop)
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
                            DescriptionSettingId = settings?.SettingId
                        };

                        // mark as auto-translated/summarized by crawler
                        try
                        {
                            var origLang = (entity.OriginalLanguage ?? "").Trim().ToLowerInvariant();
                            entity.TranslationLanguage = origLang.StartsWith("zh") ? "en" : "zh";
                            entity.TranslationStatus = Models.SQLServer.TranslationStatus.Translated;
                            entity.TranslationSavedBy = "crawler";
                            entity.TranslationSavedAt = DateTime.Now;
                            entity.TranslationReviewedBy = "crawler";
                            entity.TranslationReviewedAt = DateTime.Now;
                        }
                        catch { }

                        // handle duplicates: update existing article when re-fetching, otherwise insert
                        if (!string.IsNullOrWhiteSpace(entity.SourceURL))
                        {
                            var existing = await _db.NewsArticles.FirstOrDefaultAsync(x => x.SourceURL == entity.SourceURL);
                            if (existing != null && !dto.Force)
                            {
                                // decide whether to update: prefer newer PublishedAt or different content
                                var existingPub = existing.PublishedAt ?? DateTime.MinValue;
                                var incomingPub = entity.PublishedAt ?? DateTime.MinValue;
                                var contentChanged = !string.Equals(existing.OriginalContent ?? string.Empty, entity.OriginalContent ?? string.Empty, StringComparison.Ordinal);
                                var newer = incomingPub > existingPub;

                                if (newer || contentChanged)
                                {
                                    existing.TitleZH = entity.TitleZH;
                                    existing.TitleEN = entity.TitleEN;
                                    existing.OriginalContent = entity.OriginalContent;
                                    existing.TranslatedContent = entity.TranslatedContent;
                                    existing.FullContentEN = entity.FullContentEN;
                                    existing.FullContentZH = entity.FullContentZH;
                                    existing.SummaryEN = entity.SummaryEN;
                                    existing.SummaryZH = entity.SummaryZH;
                                    existing.PublishedAt = entity.PublishedAt ?? existing.PublishedAt;
                                    existing.OriginalLanguage = entity.OriginalLanguage;
                                    existing.UpdatedAt = DateTime.Now;
                                    try
                                    {
                                        await _db.SaveChangesAsync();
                                        updatedCount++;
                                    }
                                    catch { }
                                    processedArticles.Add(article);
                                }
                                else
                                {
                                    // no update needed; skip
                                    continue;
                                }

                                continue;
                            }
                        }

                        entitiesToSave.Add(entity);
                        processedArticles.Add(article);
                    }
                    catch (Exception ex)
                    {
                        // log and continue with next article
                        Console.WriteLine($"Error processing article from {src.Name}: {ex.Message}");
                    }
                }

                if (entitiesToSave.Count > 0)
                {
                    Console.WriteLine($"[Articles.Fetch] source={src.SourceId} adding={entitiesToSave.Count} articles to DB");
                    _db.NewsArticles.AddRange(entitiesToSave);
                    src.LastCrawledAt = DateTime.Now;
                    await _db.SaveChangesAsync();
                }

                var entry = new
                {
                    src.SourceId,
                    src.Name,
                    fetched = processedArticles.Count,
                    added = entitiesToSave.Count,
                    articles = processedArticles
                };

                if (debug)
                {
                    var samples = rawArticles.Take(5).Select(r => new { r.Title, r.SourceURL, Length = r.Content?.Length ?? 0 }).ToList();
                    results.Add(new
                    {
                        SourceId = src.SourceId,
                        Name = src.Name,
                        fetched = processedArticles.Count,
                        added = entitiesToSave.Count,
                        articles = processedArticles,
                        rawCount = rawArticles.Count,
                        samples
                    });
                }
                else
                {
                    results.Add(entry);
                }
            }

            return Ok(results);
        }
    }
}
