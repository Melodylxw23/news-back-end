using Microsoft.AspNetCore.Mvc;
using News_Back_end.Models.SQLServer;
using News_Back_end.Services;
using News_Back_end.DTOs;
using Microsoft.EntityFrameworkCore;
using HtmlAgilityPack;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace News_Back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SourcesController : ControllerBase
    {
        private readonly MyDBContext _db;
        private readonly CrawlerFactory _factory;
        private readonly IHttpClientFactory _httpFactory;
        private readonly RSSCrawlerService _rssService;

        public SourcesController(MyDBContext db, CrawlerFactory factory, IHttpClientFactory httpFactory, RSSCrawlerService rssService)
        {
            _db = db;
            _factory = factory;
            _httpFactory = httpFactory;
            _rssService = rssService;
        }

        // GET: api/sources
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var items = await _db.Sources
                .OrderBy(s => s.Name)
                .Select(s => new {
                    s.SourceId,
                    s.Name,
                    s.BaseUrl,
                    Type = s.Type.ToString(),
                    Language = s.Language.ToString(),
                    s.CrawlFrequency,
                    s.LastCrawledAt,
                    Ownership = s.Ownership.ToString(),
                    RegionLevel = s.RegionLevel.ToString(),
                    s.IsActive,
                    s.Description,
                    s.Notes
                })
                .ToListAsync();

            return Ok(items);
        }

        // GET: api/sources/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var s = await _db.Sources.FindAsync(id);
            if (s == null) return NotFound();

            return Ok(new {
                s.SourceId,
                s.Name,
                s.BaseUrl,
                Type = s.Type.ToString(),
                Language = s.Language.ToString(),
                s.CrawlFrequency,
                s.LastCrawledAt,
                Ownership = s.Ownership.ToString(),
                RegionLevel = s.RegionLevel.ToString(),
                s.IsActive,
                s.Description,
                s.Notes
            });
        }

        public class SourceCreateDto
        {
            public string Name { get; set; } = null!;
            public string BaseUrl { get; set; } = null!;
            public string Type { get; set; } = "rss";
            public string Language { get; set; } = "EN";
            public int CrawlFrequency { get; set; } = 60;
            public string? Notes { get; set; }
            public string? Ownership { get; set; }
            public string? RegionLevel { get; set; }
            public string? Description { get; set; }
            public bool IsActive { get; set; } = true;
        }

        // POST: api/sources
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] SourceCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.BaseUrl))
                return BadRequest("Name and BaseUrl are required.");

            if (!System.Enum.TryParse<SourceType>(dto.Type, true, out var st))
                return BadRequest("Invalid Type");

            if (!System.Enum.TryParse<Languages>(dto.Language, true, out var lang))
                return BadRequest("Invalid Language");

            var entity = new Source
            {
                Name = dto.Name.Trim(),
                BaseUrl = dto.BaseUrl.Trim(),
                Type = st,
                Language = lang,
                CrawlFrequency = dto.CrawlFrequency,
                Notes = dto.Notes,
                Description = dto.Description,
                IsActive = dto.IsActive
            };

            if (!string.IsNullOrWhiteSpace(dto.Ownership) && System.Enum.TryParse<MediaOwnership>(dto.Ownership, true, out var mo))
                entity.Ownership = mo;

            if (!string.IsNullOrWhiteSpace(dto.RegionLevel) && System.Enum.TryParse<RegionLevel>(dto.RegionLevel, true, out var rl))
                entity.RegionLevel = rl;

            _db.Sources.Add(entity);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = entity.SourceId }, entity);
        }

        // PUT: api/sources/{id}
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] SourceCreateDto dto)
        {
            var s = await _db.Sources.FindAsync(id);
            if (s == null) return NotFound();

            if (!System.Enum.TryParse<SourceType>(dto.Type, true, out var st))
                return BadRequest("Invalid Type");

            if (!System.Enum.TryParse<Languages>(dto.Language, true, out var lang))
                return BadRequest("Invalid Language");

            s.Name = dto.Name.Trim();
            s.BaseUrl = dto.BaseUrl.Trim();
            s.Type = st;
            s.Language = lang;
            s.CrawlFrequency = dto.CrawlFrequency;
            s.Notes = dto.Notes;
            s.Description = dto.Description;
            s.IsActive = dto.IsActive;

            if (!string.IsNullOrWhiteSpace(dto.Ownership) && System.Enum.TryParse<MediaOwnership>(dto.Ownership, true, out var mo))
                s.Ownership = mo;

            if (!string.IsNullOrWhiteSpace(dto.RegionLevel) && System.Enum.TryParse<RegionLevel>(dto.RegionLevel, true, out var rl))
                s.RegionLevel = rl;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE (soft): api/sources/{id}
        // Now toggles active state: deactivate -> activate, activate -> deactivate
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var s = await _db.Sources.FindAsync(id);
            if (s == null) return NotFound();

            s.IsActive = !s.IsActive;
            await _db.SaveChangesAsync();

            return Ok(new { s.SourceId, s.IsActive });
        }

        // DELETE (hard): api/sources/{id}/permanent
        // Permanently remove a Source from the database (Admin only).
        [HttpDelete("{id:int}/permanent")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeletePermanently(int id)
        {
            var s = await _db.Sources.FindAsync(id);
            if (s == null) return NotFound();

            _db.Sources.Remove(s);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    

        public class FetchDto { public List<int> SourceIds { get; set; } = new(); public bool Persist { get; set; } = false; }

        // POST: api/sources/fetch
        // Fetch articles from specified sources and optionally persist to DB
        // POST: api/sources/fetch
        [HttpPost("fetch")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Fetch([FromBody] FetchDto dto)
        {
            var sourcesQuery = _db.Sources.AsQueryable();
            if (dto.SourceIds != null && dto.SourceIds.Count > 0)
            {
                sourcesQuery = sourcesQuery.Where(s => dto.SourceIds.Contains(s.SourceId));
            }
            var sources = await sourcesQuery.Where(s => s.IsActive).ToListAsync();

            var results = new List<object>();

            // Perform crawling for all sources concurrently (no DB writes during parallel work)
            var fetchTasks = sources.Select(async src =>
            {
                // diagnostics (best-effort)
                RSSCrawlerService.FeedDiagnostics? diag = null;
                if (_rssService != null)
                {
                    try
                    {
                        diag = await _rssService.GetDiagnosticsAsync(src);
                        Console.WriteLine($"[DISCOVER DIAG] source={src.SourceId} status={diag.StatusCode} syndication={diag.SyndicationItems} fallback={diag.FallbackItems}");
                    }
                    catch (System.Exception ex)
                    {
                        Console.WriteLine($"[DISCOVER DIAG] failed for source={src.SourceId}: {ex.Message}");
                    }
                }

                var crawler = _factory.CreateCrawler(src.Type.ToString());
                if (crawler == null)
                {
                    Console.WriteLine($"No crawler available for source type={src.Type}");
                    return (Source: src, Articles: new List<CrawlerDTO>(), ArticleDtos: new List<object>());
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                List<CrawlerDTO> articles;
                string? error = null;
                try
                {
                    articles = await crawler.CrawlAsync(src) ?? new List<CrawlerDTO>();
                }
                catch (System.Exception ex)
                {
                    articles = new List<CrawlerDTO>();
                    error = ex.Message;
                }
                sw.Stop();
                var articleDtosLocal = new List<object>();
                foreach (var a in articles)
                {
                    var content = a.Content ?? string.Empty;
                    var snippet = content.Length > 200 ? content.Substring(0, 200) : content;

                    articleDtosLocal.Add(new
                    {
                        Title = a.Title,
                        Snippet = snippet,
                        Url = a.SourceURL,
                        PublishedAt = a.PublishedDate,
                        OriginalLanguage = a.OriginalLanguage,
                        Content = a.Content
                    });
                }

                // record metric (best-effort, don't throw)
                try
                {
                    var metric = new FetchMetric
                    {
                        SourceId = src.SourceId,
                        Timestamp = System.DateTime.Now,
                        Success = string.IsNullOrWhiteSpace(error),
                        ItemsFetched = articles.Count,
                        DurationMs = (int)sw.ElapsedMilliseconds,
                        ErrorMessage = error
                    };
                    // Use separate context operations to avoid concurrency with the main loop
                    _db.FetchMetrics.Add(metric);
                    await _db.SaveChangesAsync();
                }
                catch { }

                return (Source: src, Articles: articles, ArticleDtos: articleDtosLocal);
            }).ToArray();

            var fetched = await Task.WhenAll(fetchTasks);

            // Persist results sequentially to avoid DbContext concurrency issues
            var addedPerSource = new Dictionary<int, int>();
            if (dto.Persist)
            {
                foreach (var f in fetched)
                {
                    var src = f.Source;
                    var added = 0;
                    foreach (var a in f.Articles)
                    {
                        if (string.IsNullOrWhiteSpace(a.SourceURL)) continue;
                        bool exists = await _db.NewsArticles.AnyAsync(x => x.SourceURL == a.SourceURL);
                        if (exists) continue;

                        var entity = new NewsArticle
                        {
                            TitleZH = a.Title ?? string.Empty,
                            TitleEN = null,
                            OriginalContent = a.Content ?? string.Empty,
                            SourceURL = a.SourceURL ?? string.Empty,
                            PublishedAt = a.PublishedDate,
                            OriginalLanguage = a.OriginalLanguage ?? src.Language.ToString(),
                            SourceId = src.SourceId,
                            CreatedAt = System.DateTime.Now
                        };
                        _db.NewsArticles.Add(entity);
                        added++;
                    }
                    if (added > 0)
                    {
                        src.LastCrawledAt = System.DateTime.Now;
                        await _db.SaveChangesAsync();
                    }
                    addedPerSource[src.SourceId] = added;
                }
            }

            // Build response
            foreach (var f in fetched)
            {
                addedPerSource.TryGetValue(f.Source.SourceId, out var addedCount);
                results.Add(new
                {
                    f.Source.SourceId,
                    f.Source.Name,
                    fetched = f.Articles.Count,
                    added = addedCount,
                    articles = f.ArticleDtos
                });
            }

            return Ok(results);
        }

        // GET: api/sources/discover?sourceId=2 OR api/sources/discover?url=https://www.news.ch/
        [HttpGet("discover")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Discover([FromQuery] int? sourceId, [FromQuery] string? url, [FromQuery] bool confirmUpdate = false)
        {
            string target = url ?? string.Empty;
            Source? sourceEntity = null;
            if (string.IsNullOrWhiteSpace(target))
            {
                if (!sourceId.HasValue) return BadRequest("Provide sourceId or url");
                sourceEntity = await _db.Sources.FindAsync(sourceId.Value);
                if (sourceEntity == null) return NotFound();
                target = sourceEntity.BaseUrl;
            }

            try
            {
                var client = _httpFactory.CreateClient();
                // Use browser-like headers to avoid simple blocks
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

                var resp = await client.GetAsync(target);
                var body = await resp.Content.ReadAsStringAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(body);

                var feedLinks = new List<object>();
                var discoveredHrefs = new List<string>();
                var nodes = doc.DocumentNode.SelectNodes("//link[@rel='alternate' or contains(@rel,'alternate')]");
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        var href = node.GetAttributeValue("href", null);
                        var type = node.GetAttributeValue("type", null);
                        if (!string.IsNullOrWhiteSpace(href))
                        {
                            try { href = new Uri(new Uri(target), href).ToString(); } catch { }
                            feedLinks.Add(new { href, type });
                            discoveredHrefs.Add(href);
                        }
                    }
                }

                if (!feedLinks.Any())
                {
                    // fallback: look for anchors that contain rss/feed keywords
                    var aNodes = doc.DocumentNode.SelectNodes("//a[@href]");
                    if (aNodes != null)
                    {
                        foreach (var a in aNodes)
                        {
                            var href = a.GetAttributeValue("href", null);
                            if (string.IsNullOrWhiteSpace(href)) continue;
                            var low = href.ToLowerInvariant();
                            if (low.Contains("rss") || low.Contains("feed") || low.EndsWith(".xml"))
                            {
                                try { href = new Uri(new Uri(target), href).ToString(); } catch { }
                                feedLinks.Add(new { href, type = "detected" });
                            }
                        }
                    }
                }

                var snippet = body == null ? string.Empty : (body.Length <= 800 ? body : body.Substring(0, 800));

                return Ok(new
                {
                    requested = target,
                    status = (int)resp.StatusCode,
                    contentType = resp.Content.Headers.ContentType?.ToString(),
                    feedLinks,
                    snippet
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
