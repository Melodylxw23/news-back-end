using HtmlAgilityPack;
using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;
using System;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Net;

namespace News_Back_end.Services
{
    public interface Crawler
    {
        Task<List<CrawlerDTO>> CrawlAsync(Source source);
    }

    // ===== RSS Crawler (typed HttpClient) =====
    public class RSSCrawlerService : Crawler
    {
        private readonly HttpClient _http;
        private readonly HTMLCrawlerService? _htmlCrawler;

        public class FeedDiagnostics
        {
            public string FeedUrl { get; set; } = string.Empty;
            public int StatusCode { get; set; }
            public string ContentType { get; set; } = string.Empty;
            public int SyndicationItems { get; set; }
            public int FallbackItems { get; set; }
            public string Snippet { get; set; } = string.Empty;
        }

        public RSSCrawlerService(HttpClient http, HTMLCrawlerService? htmlCrawler = null)
        {
            _http = http;
            _htmlCrawler = htmlCrawler;
        }

        // Diagnostics helper to inspect feed fetch and parsing counts
        public async Task<FeedDiagnostics> GetDiagnosticsAsync(Source source)
        {
            var diag = new FeedDiagnostics { FeedUrl = source.BaseUrl };
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, source.BaseUrl);
                var resp = await _http.SendAsync(req);
                diag.StatusCode = (int)resp.StatusCode;
                diag.ContentType = resp.Content.Headers.ContentType?.ToString() ?? string.Empty;
                var body = await resp.Content.ReadAsStringAsync();
                diag.Snippet = body == null ? string.Empty : (body.Length <= 800 ? body : body.Substring(0, 800));

                // syndication parse
                try
                {
                    using var sr = new StringReader(body ?? string.Empty);
                    using var xr = XmlReader.Create(sr);
                    var feed = SyndicationFeed.Load(xr);
                    diag.SyndicationItems = feed?.Items?.Count() ?? 0;
                }
                catch { diag.SyndicationItems = 0; }

                // fallback xml node count
                try
                {
                    var doc = XDocument.Parse(body ?? string.Empty);
                    diag.FallbackItems = doc.Descendants().Count(x => x.Name.LocalName == "item" || x.Name.LocalName == "entry");
                }
                catch { diag.FallbackItems = 0; }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetDiagnosticsAsync error for {source.Name}: {ex.Message}");
            }
            return diag;
        }

        public async Task<List<CrawlerDTO>> CrawlAsync(Source source)
        {
            var articles = new List<CrawlerDTO>();
            if (string.IsNullOrWhiteSpace(source.BaseUrl)) return articles;

            try
            {
                // Attempt to fetch the configured URL
                var feedUrl = source.BaseUrl;
                string feedBody = await FetchStringWithLogging(feedUrl);

                // If we got HTML (homepage), try to detect <link rel="alternate" type="application/rss+xml|atom+xml" href="...">
                if (IsHtml(feedBody))
                {
                    var discovered = FindFeedLinkFromHtml(feedBody, feedUrl);
                    if (!string.IsNullOrEmpty(discovered) && !string.Equals(discovered, feedUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        feedUrl = discovered;
                        feedBody = await FetchStringWithLogging(feedUrl);
                    }
                    else
                    {
                        // If no feed link found, try to detect a simple <iframe src="..."> that may point to a real content page
                        try
                        {
                            var doc = new HtmlDocument();
                            doc.LoadHtml(feedBody);
                            var iframe = doc.DocumentNode.SelectSingleNode("//iframe[@src]");
                            if (iframe != null)
                            {
                                var href = iframe.GetAttributeValue("src", null);
                                if (!string.IsNullOrWhiteSpace(href))
                                {
                                    try
                                    {
                                        var resolved = new Uri(new Uri(feedUrl), href).ToString();
                                        // attempt to fetch iframe target and treat it as feed/body fallback
                                        var iframeBody = await FetchStringWithLogging(resolved);
                                        if (!string.IsNullOrWhiteSpace(iframeBody))
                                        {
                                            // replace feedBody so subsequent discovery/parse will try the iframe content
                                            feedUrl = resolved;
                                            feedBody = iframeBody;
                                        }
                                    }
                                    catch { /* ignore iframe resolution errors */ }
                                }
                            }
                        }
                        catch { /* ignore iframe parsing errors */ }
                    }
                }

                // If still looks like HTML and no feed found, we can optionally try to parse the homepage with HTML crawler
                if (IsHtml(feedBody) && _htmlCrawler != null)
                {
                    // Use HTML crawler as fallback: treat the homepage as an article list page
                    var fakeSource = new Source { BaseUrl = source.BaseUrl, Language = source.Language, Name = source.Name };
                    var htmlArticles = await _htmlCrawler.CrawlAsync(fakeSource);
                    foreach (var a in htmlArticles)
                    {
                        articles.Add(new CrawlerDTO
                        {
                            Title = a.Title,
                            Content = a.Content,
                            SourceURL = a.SourceURL,
                            PublishedDate = a.PublishedDate,
                            OriginalLanguage = a.OriginalLanguage ?? source.Language.ToString()
                        });
                    }
                    return articles;
                }

                // Parse feed XML
                using var sr = new StringReader(feedBody ?? string.Empty);
                using var xr = XmlReader.Create(sr);
                var feed = SyndicationFeed.Load(xr);
                if (feed?.Items == null) return articles;

                foreach (var item in feed.Items)
                {
                    var link = item.Links.FirstOrDefault()?.Uri?.ToString();

                    // Prefer item.Content (TextSyndicationContent) which often contains the full article
                    string? content = null;
                    try
                    {
                        if (item.Content is TextSyndicationContent tsc)
                        {
                            content = tsc.Text?.Trim();
                        }
                        else
                        {
                            // Some feeds include <content:encoded> as an extension — try to find it
                            var encoded = item.ElementExtensions.ReadElementExtensions<string>("encoded", "http://purl.org/rss/1.0/modules/content/").FirstOrDefault();
                            if (!string.IsNullOrWhiteSpace(encoded)) content = encoded.Trim();
                        }
                    }
                    catch { /* ignore extension parsing errors */ }

                    // fallback to summary
                    if (string.IsNullOrWhiteSpace(content))
                        content = item.Summary?.Text?.Trim();

                    // If still no content, attempt to fetch the article page via HTML crawler when available
                    if (string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(link) && _htmlCrawler != null)
                    {
                        try
                        {
                            var htmlDto = await _htmlCrawler.FetchArticleAsync(link);
                            if (htmlDto != null && !string.IsNullOrWhiteSpace(htmlDto.Content))
                                content = htmlDto.Content;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"RSS: failed to fetch article page {link}: {ex.Message}");
                        }
                    }

                    articles.Add(new CrawlerDTO
                    {
                        Title = item.Title?.Text,
                        Content = content ?? string.Empty,
                        SourceURL = link ?? string.Empty,
                        PublishedDate = item.PublishDate.DateTime == DateTime.MinValue ? DateTime.Now : item.PublishDate.DateTime,
                        OriginalLanguage = source.Language.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RSS crawler error for {source.Name} ({source.BaseUrl}): {ex.Message}");
            }

            return articles;
        }

        private async Task<string> FetchStringWithLogging(string url)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                // Do not force a custom User-Agent here; allow the typed HttpClient defaults to apply
                var resp = await _http.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"[RSS] Fetched {url} status={resp.StatusCode} len={body?.Length ?? 0}");
                Console.WriteLine("[RSS] body-snippet: " + (body?.Length > 800 ? body.Substring(0, 800) : body));
                return body ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RSS] Fetch error for {url}: {ex.Message}");
                return string.Empty;
            }
        }

        private static bool IsHtml(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;
            var trimmed = body.TrimStart();
            if (trimmed.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
                return true;
            // heuristic: presence of <html> tag or <head>
            return trimmed.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0 || trimmed.IndexOf("<head", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FindFeedLinkFromHtml(string html, string baseUrl)
        {
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Look for <link rel="alternate" type="application/rss+xml|atom+xml" href="...">
                var nodes = doc.DocumentNode.SelectNodes("//link[@rel='alternate' or contains(@rel,'alternate')]");
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        var type = node.GetAttributeValue("type", null)?.ToLowerInvariant() ?? "";
                        if (type.Contains("rss") || type.Contains("atom") || type.Contains("xml"))
                        {
                            var href = node.GetAttributeValue("href", null);
                            if (!string.IsNullOrWhiteSpace(href))
                            {
                                // resolve relative URL
                                try
                                {
                                    var resolved = new Uri(new Uri(baseUrl), href).ToString();
                                    return resolved;
                                }
                                catch { return href; }
                            }
                        }
                    }
                }

                // Fallback: try <a> tags that look like feed links (common patterns)
                var aNodes = doc.DocumentNode.SelectNodes("//a[contains(translate(@href, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'rss') or contains(translate(@href, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'feed')]");
                if (aNodes != null)
                {
                    foreach (var a in aNodes)
                    {
                        var href = a.GetAttributeValue("href", null);
                        if (!string.IsNullOrWhiteSpace(href))
                        {
                            try
                            {
                                var resolved = new Uri(new Uri(baseUrl), href).ToString();
                                return resolved;
                            }
                            catch { return href; }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RSS] feed discovery error for {baseUrl}: {ex.Message}");
            }

            return string.Empty;
        }
    }
    // ===== API crawler (JSON) =====
    public class ApiResponse
    {
        public List<ApiArticle> Articles { get; set; } = new();
    }

    public class ApiArticle
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Url { get; set; }
        public DateTime PublishedAt { get; set; }
    }

    public class APICrawlerservice : Crawler
    {
        private readonly HttpClient _httpClient;

        public APICrawlerservice(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task<List<CrawlerDTO>> CrawlAsync(Source source)
        {
            var articles = new List<CrawlerDTO>();
            if (string.IsNullOrWhiteSpace(source.BaseUrl)) return articles;

            try
            {
                var httpResp = await _httpClient.GetAsync(source.BaseUrl);
                var body = await httpResp.Content.ReadAsStringAsync();

                if (!httpResp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"API crawler: non-success status {httpResp.StatusCode} for {source.Name} ({source.BaseUrl}) - body starts with: {GetSnippet(body)}");
                    return articles;
                }

                var contentType = httpResp.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"API crawler: unexpected content-type '{contentType}' for {source.Name} ({source.BaseUrl}) - body starts with: {GetSnippet(body)}");
                    return articles;
                }

                ApiResponse? response = null;
                try
                {
                    response = JsonSerializer.Deserialize<ApiResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException jex)
                {
                    Console.WriteLine($"API crawler: failed to deserialize JSON for {source.Name} ({source.BaseUrl}): {jex.Message} - body starts with: {GetSnippet(body)}");
                    return articles;
                }

                if (response?.Articles == null) return articles;

                foreach (var item in response.Articles)
                {
                    articles.Add(new CrawlerDTO
                    {
                        Title = item.Title,
                        Content = item.Content,
                        SourceURL = item.Url,
                        PublishedDate = item.PublishedAt,
                        OriginalLanguage = source.Language.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"API crawler error for {source.Name} ({source.BaseUrl}): {ex.Message}");
            }

            return articles;
        }

        private static string GetSnippet(string? s, int max = 200)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }

    // ===== HTML crawler (HtmlAgilityPack) =====
    public class HTMLCrawlerService : Crawler
    {
        private readonly HttpClient _httpClient;
        public HTMLCrawlerService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<CrawlerDTO>> CrawlAsync(Source source)
        {
            var articles = new List<CrawlerDTO>();
            if (string.IsNullOrWhiteSpace(source.BaseUrl)) return articles;

            try
            {
                var html = await _httpClient.GetStringAsync(source.BaseUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                // Example XPath — differs per site; try several heuristics to discover article links
                var nodes = doc.DocumentNode.SelectNodes("//div[@class='news-item']");

                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        var titleNode = node.SelectSingleNode(".//a");
                        var link = titleNode?.GetAttributeValue("href", string.Empty);
                        var title = titleNode?.InnerText.Trim();

                        articles.Add(new CrawlerDTO
                        {
                            Title = title ?? "No title",
                            Content = "Content fetched separately if needed",
                            SourceURL = link ?? string.Empty,
                        PublishedDate = DateTime.Now,
                            OriginalLanguage = source.Language.ToString()
                        });
                    }
                    return articles;
                }

                // Fallback heuristics: find <article> anchors, heading links, or obvious article anchors
                var articleLinks = new HashSet<string>();

                // 1) <article> tags with links
                var articleAnchors = doc.DocumentNode.SelectNodes("//article//a[@href]");
                if (articleAnchors != null)
                {
                    foreach (var a in articleAnchors)
                    {
                        var href = a.GetAttributeValue("href", null);
                        if (!string.IsNullOrWhiteSpace(href)) articleLinks.Add(href);
                    }
                }

                // 2) headings with links (h1/h2/h3)
                var headingAnchors = doc.DocumentNode.SelectNodes("//h1//a[@href] | //h2//a[@href] | //h3//a[@href]");
                if (headingAnchors != null)
                {
                    foreach (var a in headingAnchors)
                    {
                        var href = a.GetAttributeValue("href", null);
                        if (!string.IsNullOrWhiteSpace(href)) articleLinks.Add(href);
                    }
                }

                // 3) generic anchors that likely point to articles
                var genericAnchors = doc.DocumentNode.SelectNodes("//a[@href]");
                if (genericAnchors != null)
                {
                    foreach (var a in genericAnchors)
                    {
                        var href = a.GetAttributeValue("href", null);
                        if (string.IsNullOrWhiteSpace(href)) continue;
                        var low = href.ToLowerInvariant();
                        if (low.Contains("/news") || low.Contains("/article") || low.Contains("/story") || low.Contains("/202") || low.Contains("/2025") || low.Contains("rss") || low.Contains("/artikel"))
                        {
                            articleLinks.Add(href);
                        }
                    }
                }

                // Resolve relative URLs and limit to first 20
                var resolved = new List<string>();
                foreach (var href in articleLinks)
                {
                    try
                    {
                        var resolvedUrl = new Uri(new Uri(source.BaseUrl), href).ToString();
                        if (!resolved.Contains(resolvedUrl)) resolved.Add(resolvedUrl);
                    }
                    catch { /* ignore */ }
                    // increase per-source discovery limit to fetch more articles from HTML pages
                    if (resolved.Count >=20) break;
                }

                // Fetch each article page and extract content
                foreach (var url in resolved)
                {
                    try
                    {
                        var art = await FetchArticleAsync(url);
                        if (art != null)
                        {
                            art.OriginalLanguage ??= source.Language.ToString();
                            articles.Add(art);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"HTML crawler: failed to fetch article {url}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTML crawler error for {source.Name} ({source.BaseUrl}): {ex.Message}");
            }

            return articles;
        }

        // Best-effort single-URL article fetch used by RSS crawler when feed item has no summary
        public async Task<CrawlerDTO?> FetchArticleAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var html = await _httpClient.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                // remove scripts/styles and common chrome elements to reduce noise
                try
                {
                    var cleanup = doc.DocumentNode.SelectNodes("//script|//style|//noscript|//iframe|//header|//footer|//nav|//aside");
                    if (cleanup != null)
                    {
                        foreach (var n in cleanup) n.Remove();
                    }

                    // remove common noisy classes/ids
                    var noisyPatterns = new[] { "nav", "breadcrumb", "footer", "header", "subscribe", "share", "related", "comments", "advert", "ads", "cookie" };
                    foreach (var pat in noisyPatterns)
                    {
                        var nodes = doc.DocumentNode.SelectNodes($"//*[contains(translate(@class, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{pat}') or contains(translate(@id, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{pat}')]");
                        if (nodes == null) continue;
                        foreach (var n in nodes) n.Remove();
                    }
                }
                catch { }

                // Candidate XPath selectors to find main content
                var xpaths = new[] {
                    "//article",
                    "//main",
                    "//*[@id='content']",
                    "//*[@id='main']",
                    "//*[contains(@class,'article-body')]",
                    "//*[contains(@class,'article-content')]",
                    "//*[contains(@class,'post-content')]",
                    "//*[contains(@class,'entry-content')]",
                    "//*[contains(@class,'content') and (name() = 'div' or name() = 'section') ]"
                };

                HtmlNode bestNode = null;
                int bestLen = 0;
                foreach (var xp in xpaths)
                {
                    try
                    {
                        var nodes = doc.DocumentNode.SelectNodes(xp);
                        if (nodes == null) continue;
                        foreach (var n in nodes)
                        {
                            var txt = (n.InnerText ?? string.Empty).Trim();
                            var len = txt.Length;
                            if (len > bestLen)
                            {
                                bestLen = len;
                                bestNode = n;
                            }
                        }
                        if (bestLen > 200) break; // likely found main content
                    }
                    catch { }
                }

                // Fallback: choose largest child under body
                if (bestNode == null)
                {
                    try
                    {
                        var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
                        foreach (var child in body.ChildNodes)
                        {
                            var txt = (child.InnerText ?? string.Empty).Trim();
                            var len = txt.Length;
                            if (len > bestLen)
                            {
                                bestLen = len;
                                bestNode = child;
                            }
                        }
                    }
                    catch { }
                }

                var contentNode = bestNode ?? doc.DocumentNode;

                // extract and normalize text
                var text = contentNode.InnerText ?? string.Empty;
                // collapse whitespace
                text = Regex.Replace(text, "\\s+", " ").Trim();

                // Heuristic: discard boilerplate/legal/privacy pages that are not real articles
                try
                {
                    var low = text.Length > 1000 ? text.Substring(0, 1000).ToLowerInvariant() : text.ToLowerInvariant();
                    var boilerplateIndicators = new[] { "关于我们", "联系我们", "版权", "隐私", "免责声明", "联系我们", "联系我们" , "all rights reserved", "privacy policy", "terms of use", "联系我们" };
                    var matches = boilerplateIndicators.Count(p => low.Contains(p));
                    if (matches >= 2 || text.Length < 50)
                    {
                        // treat as non-article
                        return null;
                    }
                }
                catch { }

                // extract title
                var title = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.GetAttributeValue("content", null)
                            ?? doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();

                // remove leading title occurrences from content
                if (!string.IsNullOrWhiteSpace(title) && text.StartsWith(title))
                {
                    text = text.Substring(title.Length).Trim();
                }

                // final trim
                if (text.Length > 0 && text.Length < 20)
                {
                    // very short extraction, try fallback to full document text
                    text = Regex.Replace(doc.DocumentNode.InnerText ?? string.Empty, "\\s+", " ").Trim();
                }

                return new CrawlerDTO
                {
                    Title = title,
                    Content = text,
                    SourceURL = url,
                    PublishedDate = DateTime.Now,
                    OriginalLanguage = null
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FetchArticleAsync error for {url}: {ex.Message}");
                return null;
            }
        }
    }

    // ===== Factory =====
    public interface CrawlerFactory
    {
        Crawler CreateCrawler(string sourceType);
    }

    public class CrawlersFactory : CrawlerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public CrawlersFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Crawler CreateCrawler(string sourceType)
        {
            if (string.IsNullOrWhiteSpace(sourceType))
                throw new ArgumentException("sourceType is required", nameof(sourceType));

            // Do not return UnifiedCrawlerService here because it does not implement `Crawler`.
            // Unified crawling is used directly where needed (see UnifiedCrawlerService.CrawlAsync).

            switch (sourceType.Trim().ToLowerInvariant())
            {
                case "rss":
                    return _serviceProvider.GetRequiredService<RSSCrawlerService>();
                case "api":
                    return _serviceProvider.GetRequiredService<APICrawlerservice>();
                case "html":
                    return _serviceProvider.GetRequiredService<HTMLCrawlerService>();
                default:
                    throw new NotSupportedException($"Source type {sourceType} not supported");
            }
        }
    }

    // ===== Background hosted crawler (optional) =====
    public class NewsCrawlerBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public NewsCrawlerBackgroundService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();

                var factory = scope.ServiceProvider.GetRequiredService<CrawlerFactory>();
                var db = scope.ServiceProvider.GetRequiredService<MyDBContext>();

                // Per-user toggle: only run if at least one user enabled auto-fetch.
                // Use the minimum interval among enabled users as the global loop delay.
                var enabledSettings = await db.AutoFetchSettings
                .AsNoTracking()
                .Where(x => x.Enabled)
                .ToListAsync(stoppingToken);

                var intervalSeconds = enabledSettings.Count ==0
                ?300
                : enabledSettings.Min(x => x.IntervalSeconds);
                if (intervalSeconds <10) intervalSeconds =10;

                if (enabledSettings.Count ==0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                    continue;
                }

                var sources = await db.Sources
                .Where(s => s.IsActive)
                .ToListAsync(stoppingToken);

                foreach (var source in sources)
                {
                    try
                    {
                        var crawler = factory.CreateCrawler(source.Type.ToString());
                        var articles = await crawler.CrawlAsync(source);

                        // Persist articles to database, avoiding duplicates by SourceURL
                        foreach (var a in articles)
                        {
                            if (string.IsNullOrWhiteSpace(a.SourceURL))
                                continue;

                            bool exists = await db.Set<NewsArticle>().AnyAsync(x => x.SourceURL == a.SourceURL, stoppingToken);
                            if (exists)
                                continue;

                        // Try to process the raw article using ArticleProcessor (will translate+summarize when available)
                        var processor = scope.ServiceProvider.GetService<ArticleProcessor>();
                        // load per-source settings if available
                        var perSourceSettings = await db.SourceDescriptionSettings.FirstOrDefaultAsync(x => x.SourceId == source.SourceId, stoppingToken);
                        if (perSourceSettings == null)
                        {
                            perSourceSettings = new SourceDescriptionSetting
                            {
                                MinArticleLength = 0,
                                MaxArticlesPerFetch = 5,
                                TranslateOnFetch = true,
                                IncludeEnglishSummary = true,
                                IncludeChineseSummary = true,
                                SummaryWordCount = 150,
                                SummaryTone = "neutral",
                                SummaryFormat = "paragraph"
                            };
                        }

                        if (processor != null)
                        {
                            try
                            {
                                var processed = await processor.ProcessArticle(a, perSourceSettings);
                                if (processed != null)
                                {
                                    var entity = new NewsArticle
                                    {
                                        TitleZH = processed.TitleZH ?? string.Empty,
                                        TitleEN = processed.TitleEN,
                                        OriginalContent = processed.OriginalContent ?? string.Empty,
                                        TranslatedContent = processed.TranslatedContent,
                                        FullContentEN = processed.FullContentEN,
                                        FullContentZH = processed.FullContentZH,
                                        SummaryEN = processed.SummaryEN,
                                        SummaryZH = processed.SummaryZH,
                                        SourceURL = processed.SourceURL ?? string.Empty,
                                        PublishedAt = processed.PublishedAt,
                                        OriginalLanguage = processed.OriginalLanguage ?? source.Language.ToString(),
                                        SourceId = source.SourceId,
                                        CreatedAt = DateTime.Now,
                                        TranslationSavedBy = "crawler",
                                        TranslationSavedAt = DateTime.Now,
                                        TranslationStatus = Models.SQLServer.TranslationStatus.Translated
                                    };
                                    // best-effort audit
                                    try
                                    {
                                        db.TranslationAudits.Add(new TranslationAudit
                                        {
                                            NewsArticleId = entity.NewsArticleId,
                                            Action = "Saved",
                                            PerformedBy = entity.TranslationSavedBy ?? "crawler",
                                            PerformedAt = DateTime.Now,
                                            Details = entity.TranslationLanguage ?? string.Empty
                                        });
                                    }
                                    catch { }

                                    db.NewsArticles.Add(entity);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Background processor failed for article {a.SourceURL}: {ex.Message}");
                                // fallback: add minimal entity so article is persisted
                                db.NewsArticles.Add(new NewsArticle
                                {
                                    TitleZH = a.Title ?? string.Empty,
                                    TitleEN = null,
                                    OriginalContent = a.Content ?? string.Empty,
                                    SourceURL = a.SourceURL ?? string.Empty,
                                    PublishedAt = a.PublishedDate,
                                    OriginalLanguage = a.OriginalLanguage ?? source.Language.ToString(),
                                    SourceId = source.SourceId,
                                    CreatedAt = DateTime.Now
                                });
                            }
                        }
                        else
                        {
                            // No ArticleProcessor available — fallback to lightweight translation behavior
                            var entity = new NewsArticle
                            {
                                TitleZH = a.Title ?? string.Empty,
                                TitleEN = null,
                                OriginalContent = a.Content ?? string.Empty,
                                SourceURL = a.SourceURL ?? string.Empty,
                                PublishedAt = a.PublishedDate,
                                OriginalLanguage = a.OriginalLanguage ?? source.Language.ToString(),
                                SourceId = source.SourceId,
                                CreatedAt = DateTime.Now
                            };

                            var translationService = scope.ServiceProvider.GetService<ITranslationService>();
                            if (translationService != null)
                            {
                                var detected = await translationService.DetectLanguageAsync(entity.OriginalContent);
                                entity.OriginalLanguage = detected;
                                if (detected == "zh")
                                {
                                    entity.TranslationLanguage = "en";
                                    entity.TranslatedContent = await translationService.TranslateAsync(entity.OriginalContent, "en");
                                    entity.TranslationStatus = Models.SQLServer.TranslationStatus.InProgress;
                                }
                                else
                                {
                                    entity.TranslationLanguage = "zh";
                                    entity.TranslatedContent = await translationService.TranslateAsync(entity.OriginalContent, "zh");
                                    entity.TranslationStatus = Models.SQLServer.TranslationStatus.InProgress;
                                }

                                try
                                {
                                    entity.TranslationSavedBy = "crawler";
                                    entity.TranslationSavedAt = DateTime.Now;
                                    db.TranslationAudits.Add(new TranslationAudit
                                    {
                                        NewsArticleId = entity.NewsArticleId,
                                        Action = "Saved",
                                        PerformedBy = entity.TranslationSavedBy ?? "crawler",
                                        PerformedAt = DateTime.Now,
                                        Details = entity.TranslationLanguage
                                    });
                                }
                                catch { }
                            }

                            db.NewsArticles.Add(entity);
                        }
                        }
                        source.LastCrawledAt = DateTime.Now;
                        await db.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error crawling source {source.Name}: {ex.Message}");
                    }
                }

                // sleep between cycles to avoid a tight loop
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
        }
    }
    public class CrawlResult
 {
 public List<ArticleDtos> Processed { get; set; } = new List<ArticleDtos>();
 public int RawCount { get; set; }
 public int DuplicateSkipped { get; set; }
 }

    public class UnifiedCrawlerService
    {
        private readonly RSSCrawlerService _rss;
        private readonly HTMLCrawlerService _html;
        private readonly APICrawlerservice _api;
        private readonly ArticleProcessor _processor;
        private readonly MyDBContext _db;

        public UnifiedCrawlerService(RSSCrawlerService rss, HTMLCrawlerService html, APICrawlerservice api, ArticleProcessor processor, MyDBContext db)
        {
            _rss = rss;
            _html = html;
            _api = api;
            _processor = processor;
            _db = db;
        }

        // Return CrawlResult including processed articles and counts.
        // userId is used to check the FetchedArticleUrls history so we don't
        // re-fetch articles the user has already seen (even if they were deleted).
        public async Task<CrawlResult> CrawlAsync(Source source, SourceDescriptionSetting settings, bool force = false, string? userId = null)
        {
     List<CrawlerDTO> rawArticles = source.Type switch
       {
   SourceType.rss => await _rss.CrawlAsync(source),
       SourceType.html => await _html.CrawlAsync(source),
           SourceType.api => await _api.CrawlAsync(source),
     _ => new List<CrawlerDTO>()
     };

    var result = new CrawlResult { RawCount = rawArticles.Count };

 // Determine how many NEW (non-duplicate) articles we want
      int cap = settings.MaxArticlesPerFetch ?? rawArticles.Count;

     Console.WriteLine($"[UnifiedCrawler] Source={source.SourceId} rawCount={rawArticles.Count} cap={cap} force={force}");

           // Iterate through ALL raw articles, skip duplicates, stop once we
    // have collected enough new articles to satisfy the cap.
 foreach (var raw in rawArticles)
  {
     // Already collected enough new articles
     if (result.Processed.Count >= cap) break;

     if (!force && !string.IsNullOrWhiteSpace(raw.SourceURL))
     {
        // Check 1: does it already exist in NewsArticles?
  var existsInArticles = await _db.NewsArticles.AnyAsync(x => x.SourceURL == raw.SourceURL);
      if (existsInArticles)
     {
        result.DuplicateSkipped++;
    Console.WriteLine($"[UnifiedCrawler] Skipped duplicate (in NewsArticles): {raw.SourceURL}");
       continue;
          }

        // Check 2: was it previously fetched by this user (even if the article was deleted)?
      if (!string.IsNullOrWhiteSpace(userId))
   {
   var previouslyFetched = await _db.FetchedArticleUrls.AnyAsync(
      x => x.ApplicationUserId == userId && x.SourceURL == raw.SourceURL);
       if (previouslyFetched)
            {
           result.DuplicateSkipped++;
     Console.WriteLine($"[UnifiedCrawler] Skipped duplicate (in fetch history): {raw.SourceURL}");
      continue;
       }
          }
        }

       var article = await _processor.ProcessArticle(raw, settings);
       if (article != null) result.Processed.Add(article);
      }

      Console.WriteLine($"[UnifiedCrawler] Source={source.SourceId} processed={result.Processed.Count} duplicatesSkipped={result.DuplicateSkipped}");

            return result;
        }
    }

}