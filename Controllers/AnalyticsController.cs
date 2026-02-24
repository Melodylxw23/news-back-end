using Microsoft.AspNetCore.Mvc;
using News_Back_end.Models.SQLServer;
using News_Back_end.DTOs;
using News_Back_end.Services;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace News_Back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
  public class AnalyticsController : ControllerBase
  {
   private readonly MyDBContext _db;
        private readonly IBroadcastAnalyticsService _analyticsService;
     private readonly IPracticalAnalyticsService _practicalAnalyticsService;
        private readonly IConfiguration _configuration;
        private readonly IBroadcastAnalyticsAiService? _aiService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AnalyticsController> _logger;

   public AnalyticsController(
     MyDBContext db, 
         IBroadcastAnalyticsService analyticsService,
            IPracticalAnalyticsService practicalAnalyticsService,
       IConfiguration configuration,
          IMemoryCache cache,
 ILogger<AnalyticsController> logger,
          IBroadcastAnalyticsAiService? aiService = null)
        {
  _db = db;
  _analyticsService = analyticsService;
   _practicalAnalyticsService = practicalAnalyticsService;
  _configuration = configuration;
            _aiService = aiService;
    _cache = cache;
    _logger = logger;
        }

    #region Diagnostics

        /// <summary>
        /// Diagnostic endpoint to check analytics tracking configuration and status
        /// </summary>
    [HttpGet("diagnostics")]
      public async Task<IActionResult> GetDiagnostics()
     {
      var baseUrl = _configuration.GetValue<string>("BaseUrl") ?? "NOT CONFIGURED";
    var frontendArticleUrl = _configuration.GetValue<string>("Frontend:ArticleBaseUrl") ?? "NOT CONFIGURED";
     
   // Get counts for tracking verification
            var totalDeliveries = await _db.BroadcastDeliveries.CountAsync();
          var openedDeliveries = await _db.BroadcastDeliveries.CountAsync(d => d.EmailOpened);
   var clickedDeliveries = await _db.BroadcastDeliveries.CountAsync(d => d.HasClicked);
   var totalLinkClicks = await _db.BroadcastLinkClicks.CountAsync();
         
  // Get last 5 tracked opens and clicks for verification
 var recentOpens = await _db.BroadcastDeliveries
      .Where(d => d.EmailOpened)
   .OrderByDescending(d => d.FirstOpenedAt)
     .Take(5)
   .Select(d => new { d.BroadcastMessageId, d.MemberId, d.FirstOpenedAt, d.UserAgent, d.DeviceType })
                .ToListAsync();
        
   var recentClicks = await _db.BroadcastLinkClicks
  .OrderByDescending(c => c.ClickedAt)
        .Take(5)
     .Select(c => new { c.BroadcastDeliveryId, c.PublicationDraftId, c.OriginalUrl, c.ClickedAt, c.DeviceType })
                .ToListAsync();
       
    // Check for articles without SourceURL (will cause tracking issues)
            var articlesWithoutUrls = await _db.PublicationDrafts
       .Include(p => p.NewsArticle)
        .Where(p => p.NewsArticle == null || string.IsNullOrEmpty(p.NewsArticle.SourceURL))
   .CountAsync();
            
            var issues = new List<string>();
            
       if (baseUrl.Contains("localhost"))
      issues.Add("WARNING: BaseUrl contains 'localhost'. Email tracking will NOT work for external recipients.");
          
            if (baseUrl == "NOT CONFIGURED")
    issues.Add("CRITICAL: BaseUrl is not configured in appsettings.json");
   
            if (totalDeliveries > 0 && openedDeliveries == 0)
        issues.Add("WARNING: Emails have been sent but no opens recorded. Check if tracking pixel is being blocked or BaseUrl is unreachable.");
         
         if (openedDeliveries > 0 && clickedDeliveries == 0)
        issues.Add("INFO: Opens recorded but no clicks. This may be normal if recipients aren't clicking links.");
             
            if (articlesWithoutUrls > 0)
   issues.Add($"WARNING: {articlesWithoutUrls} articles without SourceURL. Click tracking will use frontend fallback URL.");
            
  return Ok(new
          {
     configuration = new
     {
         baseUrl,
        frontendArticleUrl,
    isBaseUrlPublic = !baseUrl.Contains("localhost"),
         trackingEndpoints = new
   {
   openTracking = $"{baseUrl}/api/analytics/track/open/{{broadcastId}}/{{memberId}}",
      clickTracking = $"{baseUrl}/api/analytics/track/click/{{broadcastId}}/{{memberId}}?url={{encodedUrl}}&linkId={{linkId}}&articleId={{articleId}}"
             }
                },
      statistics = new
 {
  totalDeliveries,
    openedDeliveries,
      openRate = totalDeliveries > 0 ? Math.Round((double)openedDeliveries / totalDeliveries * 100, 2) : 0,
         clickedDeliveries,
           clickRate = totalDeliveries > 0 ? Math.Round((double)clickedDeliveries / totalDeliveries * 100, 2) : 0,
     totalLinkClicks,
             articlesWithoutSourceUrl = articlesWithoutUrls
             },
        recentActivity = new
    {
      recentOpens,
         recentClicks
        },
       issues,
      recommendations = new[]
   {
     "For development: Use ngrok or similar to expose your local server and set that URL as BaseUrl",
          "For production: Set BaseUrl to your public API URL (e.g., https://api.yourdomain.com)",
           "Ensure CORS allows requests from email client domains for tracking pixel",
          "Note: Gmail and other email clients may proxy tracking pixels, causing inaccurate open rates"
            }
    });
        }

    /// <summary>
        /// Test tracking by manually simulating an open event
        /// </summary>
        [HttpPost("diagnostics/simulate-open/{broadcastId}/{memberId}")]
        public async Task<IActionResult> SimulateOpen(int broadcastId, int memberId)
        {
            try
  {
    await _analyticsService.RecordEmailOpenAsync(broadcastId, memberId, "DiagnosticTest/1.0", "127.0.0.1");
        return Ok(new { message = "Open event simulated successfully", broadcastId, memberId });
            }
       catch (Exception ex)
{
             return BadRequest(new { error = ex.Message });
      }
 }

        /// <summary>
        /// Test tracking by manually simulating a click event
        /// </summary>
        [HttpPost("diagnostics/simulate-click/{broadcastId}/{memberId}")]
  public async Task<IActionResult> SimulateClick(int broadcastId, int memberId, [FromQuery] string url = "https://test.com/article", [FromQuery] int? articleId = null)
        {
            try
     {
          await _analyticsService.RecordLinkClickAsync(new LinkClickTrackingDTO
{
    BroadcastId = broadcastId,
       MemberId = memberId,
           Url = url,
        LinkIdentifier = "diagnostic-test",
 ArticleId = articleId,
  UserAgent = "DiagnosticTest/1.0",
            IpAddress = "127.0.0.1"
    });
  return Ok(new { message = "Click event simulated successfully", broadcastId, memberId, url });
            }
       catch (Exception ex)
            {
      return BadRequest(new { error = ex.Message });
            }
  }

        #endregion

        #region Practical Analytics (Reliable Metrics)

        /// <summary>
      /// Get the practical analytics dashboard with reliable, measurable metrics.
        /// This focuses on delivery health, audience reach, and content distribution
        /// rather than unreliable open/click tracking.
        /// </summary>
  [HttpGet("practical/dashboard")]
        public async Task<IActionResult> GetPracticalDashboard(
           [FromQuery] DateTime? fromDate = null,
   [FromQuery] DateTime? toDate = null)
  {
      try
   {
 var dashboard = await _practicalAnalyticsService.GetDashboardAsync(fromDate, toDate);
      return Ok(dashboard);
        }
  catch (Exception ex)
        {
   return StatusCode(500, new { error = ex.Message });
 }
  }

        /// <summary>
    /// Get delivery health metrics - 100% reliable from email server responses
        /// </summary>
     [HttpGet("practical/delivery-health")]
        public async Task<IActionResult> GetDeliveryHealth(
      [FromQuery] DateTime? fromDate = null,
  [FromQuery] DateTime? toDate = null)
        {
       var to = toDate ?? DateTime.UtcNow;
      var from = fromDate ?? to.AddDays(-30);

       try
 {
             var health = await _practicalAnalyticsService.GetDeliveryHealthAsync(from, to);
        return Ok(health);
     }
catch (Exception ex)
  {
   return StatusCode(500, new { error = ex.Message });
  }
        }

        /// <summary>
    /// Get delivery trends over time
      /// </summary>
    [HttpGet("practical/delivery-trends")]
        public async Task<IActionResult> GetDeliveryTrends(
     [FromQuery] DateTime? fromDate = null,
         [FromQuery] DateTime? toDate = null)
        {
     var to = toDate ?? DateTime.UtcNow;
  var from = fromDate ?? to.AddDays(-30);

         try
         {
     var trends = await _practicalAnalyticsService.GetDeliveryTrendsAsync(from, to);
       return Ok(trends);
          }
      catch (Exception ex)
     {
               return StatusCode(500, new { error = ex.Message });
            }
  }

        /// <summary>
        /// Get audience reach analysis - which segments are we reaching?
        /// </summary>
        [HttpGet("practical/audience-reach")]
        public async Task<IActionResult> GetAudienceReach(
          [FromQuery] DateTime? fromDate = null,
     [FromQuery] DateTime? toDate = null)
 {
            var to = toDate ?? DateTime.UtcNow;
     var from = fromDate ?? to.AddDays(-30);

     try
            {
      var reach = await _practicalAnalyticsService.GetAudienceReachAsync(from, to);
          return Ok(reach);
  }
    catch (Exception ex)
    {
      return StatusCode(500, new { error = ex.Message });
            }
     }

        /// <summary>
        /// Get content distribution analysis - what topics are we sending?
    /// </summary>
     [HttpGet("practical/content-distribution")]
        public async Task<IActionResult> GetContentDistribution(
      [FromQuery] DateTime? fromDate = null,
    [FromQuery] DateTime? toDate = null)
        {
            var to = toDate ?? DateTime.UtcNow;
  var from = fromDate ?? to.AddDays(-30);

            try
  {
          var distribution = await _practicalAnalyticsService.GetContentDistributionAsync(from, to);
       return Ok(distribution);
       }
     catch (Exception ex)
{
      return StatusCode(500, new { error = ex.Message });
            }
 }

 /// <summary>
    /// Get member preferences analysis - what do members want?
        /// </summary>
    [HttpGet("practical/member-preferences")]
    public async Task<IActionResult> GetMemberPreferences()
        {
            try
            {
     var preferences = await _practicalAnalyticsService.GetMemberPreferencesAsync();
   return Ok(preferences);
    }
     catch (Exception ex)
      {
    return StatusCode(500, new { error = ex.Message });
   }
 }

      /// <summary>
        /// Get engagement signals - unsubscribes, list growth, etc.
    /// </summary>
      [HttpGet("practical/engagement-signals")]
 public async Task<IActionResult> GetEngagementSignals(
          [FromQuery] DateTime? fromDate = null,
     [FromQuery] DateTime? toDate = null)
        {
            var to = toDate ?? DateTime.UtcNow;
  var from = fromDate ?? to.AddDays(-30);

    try
  {
       var signals = await _practicalAnalyticsService.GetEngagementSignalsAsync(from, to);
     return Ok(signals);
       }
       catch (Exception ex)
      {
     return StatusCode(500, new { error = ex.Message });
   }
    }

    /// <summary>
        /// Get practical recommendations based on measurable data
        /// </summary>
    [HttpGet("practical/recommendations")]
        public async Task<IActionResult> GetPracticalRecommendations()
        {
            try
        {
       var recommendations = await _practicalAnalyticsService.GetRecommendationsAsync();
     return Ok(recommendations);
    }
   catch (Exception ex)
  {
    return StatusCode(500, new { error = ex.Message });
   }
        }

        #endregion

        #region Fetch Metrics (Existing)

        // GET api/analytics/fetch-overview?hours=24
   [HttpGet("fetch-overview")]
        public async Task<IActionResult> FetchOverview([FromQuery] int hours = 24)
        {
 var since = System.DateTime.Now.AddHours(-hours);
            var q = _db.FetchMetrics.Where(f => f.Timestamp >= since);
     var total = await q.CountAsync();
            var success = await q.CountAsync(f => f.Success);
        var failed = total - success;

     // per-source summary
            var perSource = await q.GroupBy(f => f.SourceId)
                .Select(g => new {
        SourceId = g.Key,
      Attempts = g.Count(),
       Success = g.Count(x => x.Success),
    Failures = g.Count(x => !x.Success),
          AvgDurationMs = (int?)g.Average(x => x.DurationMs) ?? 0,
          AvgItems = (int?)g.Average(x => x.ItemsFetched) ?? 0
                }).ToListAsync();

            return Ok(new {
    hours,
          total,
      success,
     failed,
        perSource
     });
        }

     #endregion

        #region Broadcast Analytics Dashboard

        /// <summary>
        /// Get comprehensive broadcast analytics dashboard
        /// </summary>
[HttpGet("broadcast/dashboard")]
     public async Task<IActionResult> GetBroadcastDashboard(
   [FromQuery] DateTime? fromDate = null,
 [FromQuery] DateTime? toDate = null)
        {
    try
      {
            var dashboard = await _analyticsService.GetDashboardAsync(fromDate, toDate);
           return Ok(dashboard);
            }
          catch (Exception ex)
  {
                return StatusCode(500, new { error = ex.Message });
         }
   }

        /// <summary>
        /// Get overview metrics for a specific period
        /// </summary>
        [HttpGet("broadcast/overview")]
 public async Task<IActionResult> GetOverviewMetrics(
     [FromQuery] DateTime? fromDate = null,
    [FromQuery] DateTime? toDate = null)
 {
      var to = toDate ?? DateTime.UtcNow;
     var from = fromDate ?? to.AddDays(-30);

            try
            {
                var metrics = await _analyticsService.GetOverviewMetricsAsync(from, to);
            return Ok(metrics);
            }
            catch (Exception ex)
          {
       return StatusCode(500, new { error = ex.Message });
  }
        }

        #endregion

   #region Topic & Content Performance

        /// <summary>
        /// Get top performing content and topics
      /// </summary>
  [HttpGet("broadcast/top-content")]
        public async Task<IActionResult> GetTopPerformingContent(
          [FromQuery] DateTime? fromDate = null,
     [FromQuery] DateTime? toDate = null,
     [FromQuery] int topCount = 10)
        {
            var to = toDate ?? DateTime.UtcNow;
   var from = fromDate ?? to.AddDays(-30);

            try
{
             var content = await _analyticsService.GetTopPerformingContentAsync(from, to, topCount);
       return Ok(content);
   }
            catch (Exception ex)
            {
         return StatusCode(500, new { error = ex.Message });
     }
        }

        /// <summary>
        /// Get performance metrics for all topics
    /// </summary>
        [HttpGet("broadcast/topic-performance")]
        public async Task<IActionResult> GetTopicPerformance(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
        {
   var to = toDate ?? DateTime.UtcNow;
   var from = fromDate ?? to.AddDays(-30);

        try
            {
                var topics = await _analyticsService.GetTopicPerformanceAsync(from, to);
  return Ok(topics);
     }
   catch (Exception ex)
      {
    return StatusCode(500, new { error = ex.Message });
    }
}

/// <summary>
        /// Get trending topics (rising vs declining engagement)
     /// </summary>
        [HttpGet("broadcast/trending-topics")]
        public async Task<IActionResult> GetTrendingTopics([FromQuery] int days = 30)
        {
          try
     {
      var trending = await _analyticsService.GetTrendingTopicsAsync(days);
           return Ok(trending);
        }
     catch (Exception ex)
            {
      return StatusCode(500, new { error = ex.Message });
            }
  }

        #endregion

        #region Audience Insights

        /// <summary>
      /// Get comprehensive audience insights
     /// </summary>
        [HttpGet("broadcast/audience-insights")]
        public async Task<IActionResult> GetAudienceInsights()
        {
  try
         {
     var insights = await _analyticsService.GetAudienceInsightsAsync();
    return Ok(insights);
            }
    catch (Exception ex)
     {
         return StatusCode(500, new { error = ex.Message });
   }
        }

/// <summary>
        /// Get member engagement list with filtering
        /// </summary>
        [HttpGet("broadcast/member-engagement")]
        public async Task<IActionResult> GetMemberEngagement(
            [FromQuery] string? engagementLevel = null,
            [FromQuery] int page = 1,
         [FromQuery] int pageSize = 50)
 {
            try
       {
        var members = await _analyticsService.GetMemberEngagementListAsync(engagementLevel, page, pageSize);
      return Ok(members);
            }
    catch (Exception ex)
     {
      return StatusCode(500, new { error = ex.Message });
            }
        }

  /// <summary>
        /// Get engagement details for a specific member
 /// </summary>
[HttpGet("broadcast/member-engagement/{memberId}")]
      public async Task<IActionResult> GetMemberEngagementDetails(int memberId)
        {
            try
      {
     var member = await _analyticsService.GetMemberEngagementAsync(memberId);
         return Ok(member);
       }
catch (ArgumentException ex)
            {
  return NotFound(new { error = ex.Message });
            }
          catch (Exception ex)
            {
           return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

 #region Individual Broadcast Analytics

        /// <summary>
        /// Get detailed analytics for a specific broadcast
        /// </summary>
     [HttpGet("broadcast/{broadcastId}")]
        public async Task<IActionResult> GetBroadcastAnalytics(int broadcastId)
        {
  try
       {
   var analytics = await _analyticsService.GetBroadcastAnalyticsAsync(broadcastId);
   return Ok(analytics);
    }
            catch (ArgumentException ex)
    {
              return NotFound(new { error = ex.Message });
    }
      catch (Exception ex)
            {
         return StatusCode(500, new { error = ex.Message });
            }
 }

        /// <summary>
        /// Get list of recent broadcasts with performance metrics
      /// </summary>
      [HttpGet("broadcast/recent")]
   public async Task<IActionResult> GetRecentBroadcasts([FromQuery] int count = 10)
        {
            try
     {
       var broadcasts = await _analyticsService.GetRecentBroadcastsAsync(count);
        return Ok(broadcasts);
         }
       catch (Exception ex)
    {
     return StatusCode(500, new { error = ex.Message });
  }
        }

        #endregion

        #region Engagement Trends

        /// <summary>
        /// Get engagement trends over time
        /// </summary>
        [HttpGet("broadcast/trends")]
        public async Task<IActionResult> GetEngagementTrends(
        [FromQuery] DateTime? fromDate = null,
     [FromQuery] DateTime? toDate = null)
        {
     var to = toDate ?? DateTime.UtcNow;
            var from = fromDate ?? to.AddDays(-30);

   try
    {
       var trends = await _analyticsService.GetEngagementTrendsAsync(from, to);
        return Ok(trends);
         }
        catch (Exception ex)
          {
       return StatusCode(500, new { error = ex.Message });
     }
    }

     /// <summary>
        /// Get best time to send broadcasts based on historical engagement
  /// </summary>
        [HttpGet("broadcast/best-send-time")]
 public async Task<IActionResult> GetBestTimeToSend()
    {
    try
  {
       var bestTime = await _analyticsService.GetBestTimeToSendAsync();
                return Ok(bestTime);
            }
 catch (Exception ex)
          {
       return StatusCode(500, new { error = ex.Message });
     }
        }

        #endregion

        #region Recommendations

     /// <summary>
        /// Get content recommendations based on analytics
        /// </summary>
        [HttpGet("broadcast/recommendations")]
 public async Task<IActionResult> GetContentRecommendations()
    {
   try
    {
 var recommendations = await _analyticsService.GetContentRecommendationsAsync();
       return Ok(recommendations);
  }
   catch (Exception ex)
          {
     return StatusCode(500, new { error = ex.Message });
    }
     }

        /// <summary>
      /// Get AI-powered recommendations using OpenAI based on analytics snapshot.
      /// Requires OpenAIBroadcastAnalytics:ApiKey to be configured.
        /// </summary>
        [HttpGet("ai/recommendations")]
  public async Task<IActionResult> GetAiRecommendations(
            [FromQuery] DateTime? fromDate = null,
      [FromQuery] DateTime? toDate = null)
    {
    if (_aiService == null)
      {
    return StatusCode(503, new { message = "AI analytics is not configured. Set OpenAIBroadcastAnalytics:ApiKey in configuration." });
  }

  var to = toDate ?? DateTime.UtcNow;
 var from = fromDate ?? to.AddDays(-30);

            try
  {
   // Gather analytics data from both services
 var practicalDashboard = await _practicalAnalyticsService.GetDashboardAsync(from, to);
 var broadcastDashboard = await _analyticsService.GetDashboardAsync(from, to);

 // Create a combined snapshot for AI analysis
 var snapshot = new
 {
 period = new { fromDate = from, toDate = to },
 practical = practicalDashboard,
 broadcast = broadcastDashboard
 };

 // If concrete OpenAIBroadcastAnalyticsService is registered, call the typed method to get structured DTO
 if (_aiService is OpenAIBroadcastAnalyticsService concrete)
 {
 var typed = await concrete.GenerateRecommendationsTypedAsync(snapshot, HttpContext.RequestAborted);
 return Ok(typed);
 }

 // Fallback: call generic string-returning method
 var aiResponse = await _aiService.GenerateRecommendationsAsync(snapshot, HttpContext.RequestAborted);

 // Try to parse as JSON and return structured response
 try
 {
 var parsed = System.Text.Json.JsonSerializer.Deserialize<object>(aiResponse);
 return Ok(parsed);
 }
 catch
 {
 // If parsing fails, return raw response wrapped
 return Ok(new { raw = aiResponse });
 }
 }
 catch (Exception ex)
 {
 return StatusCode(502, new { message = "AI recommendation generation failed", error = ex.Message });
 }
        }

 /// <summary>
     /// Get AI-powered recommendations for practical analytics specifically.
        /// </summary>
 [HttpGet("practical/ai-recommendations")]
        public async Task<IActionResult> GetPracticalAiRecommendations(
            [FromQuery] DateTime? fromDate = null,
   [FromQuery] DateTime? toDate = null)
      {
     if (_aiService == null)
        {
          return StatusCode(503, new { message = "AI analytics is not configured. Set OpenAIBroadcastAnalytics:ApiKey in configuration." });
            }

       var to = toDate ?? DateTime.UtcNow;
        var from = fromDate ?? to.AddDays(-30);

     try
      {
           var dashboard = await _practicalAnalyticsService.GetDashboardAsync(from, to);
              var memberPrefs = await _practicalAnalyticsService.GetMemberPreferencesAsync();

       var snapshot = new
          {
       period = new { fromDate = from, toDate = to },
      deliveryHealth = dashboard.DeliveryHealth,
    audienceReach = dashboard.AudienceReach,
 contentDistribution = dashboard.ContentDistribution,
      memberPreferences = memberPrefs,
           engagementSignals = dashboard.EngagementSignals,
           recentBroadcasts = dashboard.RecentBroadcasts
    };

         if (_aiService is OpenAIBroadcastAnalyticsService concrete)
 {
 var typed = await concrete.GenerateRecommendationsTypedAsync(snapshot, HttpContext.RequestAborted);
 return Ok(typed);
 }

 var aiResponse = await _aiService.GenerateRecommendationsAsync(snapshot, HttpContext.RequestAborted);

 try
 {
 var parsed = System.Text.Json.JsonSerializer.Deserialize<object>(aiResponse);
 return Ok(parsed);
 }
 catch
 {
 return Ok(new { raw = aiResponse });
 }
 }
 catch (Exception ex)
 {
 return StatusCode(502, new { message = "AI recommendation generation failed", error = ex.Message });
 }
 }

 /// <summary>
 /// Get AI-powered recommendations for broadcast analytics specifically.
 /// Returns typed BroadcastAiResultDTO when supported; falls back to raw string parsing otherwise.
 /// Supports caching by broadcastId + period.
 /// </summary>
[HttpGet("broadcast/ai-recommendations")]
 public async Task<IActionResult> GetBroadcastAiRecommendations([
 FromQuery] int? broadcastId = null,
 [FromQuery] DateTime? fromDate = null,
 [FromQuery] DateTime? toDate = null)
 {
 if (_aiService == null)
 {
 return StatusCode(503, new { message = "AI analytics is not configured. Set OpenAIBroadcastAnalytics:ApiKey in configuration." });
 }

 var to = toDate ?? DateTime.UtcNow;
 var from = fromDate ?? to.AddDays(-30);

 // cache key uses broadcast id and period
 var cacheKey = broadcastId.HasValue
 ? $"ai:broadcast:{broadcastId}:{from:o}:{to:o}"
 : $"ai:broadcast:global:{from:o}:{to:o}";

 if (_cache.TryGetValue(cacheKey, out BroadcastAiResultDTO cached))
 {
 return Ok(cached);
 }

 try
 {
 object snapshot = null!;
 if (broadcastId.HasValue)
 {
 snapshot = await BuildBroadcastSnapshotAsync(broadcastId.Value, from, to);
 }
 else
 {
 // global snapshot - include dashboards
 var practicalDashboard = await _practicalAnalyticsService.GetDashboardAsync(from, to);
 var broadcastDashboard = await _analyticsService.GetDashboardAsync(from, to);
 snapshot = new { period = new { fromDate = from, toDate = to }, practical = practicalDashboard, broadcast = broadcastDashboard };
 }

 BroadcastAiResultDTO typedResult = null!;

 // If concrete OpenAIBroadcastAnalyticsService is available, call its typed API
 if (_aiService is OpenAIBroadcastAnalyticsService concrete)
 {
 typedResult = await concrete.GenerateRecommendationsTypedAsync(snapshot, HttpContext.RequestAborted);
 }
 else
 {
 var raw = await _aiService.GenerateRecommendationsAsync(snapshot, HttpContext.RequestAborted);
 try
 {
 typedResult = System.Text.Json.JsonSerializer.Deserialize<BroadcastAiResultDTO>(raw, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new BroadcastAiResultDTO { Raw = raw };
 }
 catch (System.Text.Json.JsonException)
 {
 typedResult = new BroadcastAiResultDTO { Raw = raw };
 }
 }

 // Cache result for short TTL
 _cache.Set(cacheKey, typedResult, TimeSpan.FromMinutes(30));
 return Ok(typedResult);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to generate broadcast AI recommendations");
 return StatusCode(502, new { message = "AI recommendation generation failed", error = ex.Message });
 }
 }

 // Build a snapshot object for the AI model containing broadcast details, audience snapshot and recent performance
 private async Task<object> BuildBroadcastSnapshotAsync(int broadcastId, DateTime from, DateTime to)
 {
 var broadcast = await _db.BroadcastMessages
 .Include(b => b.SelectedArticles)
 .ThenInclude(a => a.NewsArticle)
 .FirstOrDefaultAsync(b => b.Id == broadcastId);

 if (broadcast == null)
 {
 return new { error = "broadcast_not_found", broadcastId };
 }

 var selectedArticleIds = broadcast.SelectedArticles.Select(a => a.PublicationDraftId).ToList();

 // audience snapshot - overall and segmented
 var totalMembers = await _db.Members.CountAsync();

 // Engagement segments from MemberEngagementProfiles (if present)
 var profileCounts = await _db.MemberEngagementProfiles
 .GroupBy(p => p.EngagementLevel)
 .Select(g => new { Level = g.Key, Count = g.Count() })
 .ToListAsync();

 var segments = profileCounts.ToDictionary(x => x.Level ?? "Unknown", x => x.Count);

 // If broadcast targets specific interest/industry tags, compute member counts per tag
 var interestTagCounts = new Dictionary<int, int>();
 var industryTagCounts = new Dictionary<int, int>();

 if (broadcast.SelectedInterestTagIds?.Any() == true)
 {
 foreach (var tagId in broadcast.SelectedInterestTagIds)
 {
 var count = await _db.Members.CountAsync(m => m.Interests.Any(t => t.InterestTagId == tagId));
 interestTagCounts[tagId] = count;
 }
 }

 if (broadcast.SelectedIndustryTagIds?.Any() == true)
 {
 foreach (var tagId in broadcast.SelectedIndustryTagIds)
 {
 var count = await _db.Members.CountAsync(m => m.IndustryTags.Any(t => t.IndustryTagId == tagId));
 industryTagCounts[tagId] = count;
 }
 }

 // Top countries among members that match targeting (if tags selected use those members, otherwise global)
 IQueryable<Models.SQLServer.Member> memberQuery = _db.Members;
 if (broadcast.SelectedInterestTagIds?.Any() == true)
 {
 var tags = broadcast.SelectedInterestTagIds;
 memberQuery = memberQuery.Where(m => m.Interests.Any(t => tags.Contains(t.InterestTagId)));
 }
 else if (broadcast.SelectedIndustryTagIds?.Any() == true)
 {
 var tags = broadcast.SelectedIndustryTagIds;
 memberQuery = memberQuery.Where(m => m.IndustryTags.Any(t => tags.Contains(t.IndustryTagId)));
 }

 var topCountries = await memberQuery
 .GroupBy(m => m.Country)
 .Select(g => new { Country = g.Key, Count = g.Count() })
 .OrderByDescending(x => x.Count)
 .Take(10)
 .ToListAsync();

 // Device breakdown for previous sends of this broadcast (if any)
 var deviceStats = await _db.BroadcastDeliveries
 .Where(d => d.BroadcastMessageId == broadcastId && d.SentAt >= from && d.SentAt <= to)
 .GroupBy(d => d.DeviceType ?? "Unknown")
 .Select(g => new { Device = g.Key, Count = g.Count() })
 .ToListAsync();

 var deviceBreakdown = deviceStats.ToDictionary(x => x.Device, x => x.Count);

 // Feature flags & content metadata
 var hasHeroImages = broadcast.SelectedArticles.Any(a => !string.IsNullOrEmpty(a.HeroImageUrl));
 var bodyLength = string.IsNullOrEmpty(broadcast.Body) ?0 : broadcast.Body.Length;
 var subjectLength = string.IsNullOrEmpty(broadcast.Subject) ?0 : broadcast.Subject.Length;
 var hasScheduledSend = broadcast.ScheduledSendAt.HasValue && broadcast.ScheduledSendAt.Value > DateTimeOffset.UtcNow;
 var channel = broadcast.Channel.ToString();
 var selectedArticleCount = selectedArticleIds.Count;

 // Top topics from selected articles (interest tags)
 var topicCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
 var selectedArticleIdsForTopics = selectedArticleIds;
 if (selectedArticleIdsForTopics.Any())
 {
 var articlesWithTags = await _db.PublicationDrafts
 .Where(p => selectedArticleIdsForTopics.Contains(p.PublicationDraftId))
 .Include(p => p.InterestTags)
 .ToListAsync();

 foreach (var a in articlesWithTags)
 {
 foreach (var t in a.InterestTags ?? Enumerable.Empty<Models.SQLServer.InterestTag>())
 {
 if (string.IsNullOrEmpty(t.NameEN)) continue;
 if (!topicCounts.ContainsKey(t.NameEN)) topicCounts[t.NameEN] =0;
 topicCounts[t.NameEN]++;
 }
 }
 }

 var topTopics = topicCounts.OrderByDescending(kv => kv.Value).Take(10).Select(kv => new { name = kv.Key, count = kv.Value }).ToList();

 // recent similar sends: pick recent broadcasts with overlapping tags or same target audience
 var recentBroadcasts = await _db.BroadcastMessages
 .Where(b => b.Status == BroadcastStatus.Sent && b.Id != broadcastId)
 .OrderByDescending(b => b.UpdatedAt)
 .Take(10)
 .ToListAsync();

 // Map limited performance metrics using existing analytics service where available
 var recentPerformance = new List<object>();
 foreach (var recent in recentBroadcasts)
 {
 try
 {
 var det = await _analyticsService.GetBroadcastAnalyticsAsync(recent.Id);
 recentPerformance.Add(new
 {
 broadcastId = recent.Id,
 title = recent.Title,
 sentAt = det.SentAt,
 openRate = det.Engagement.OpenRate,
 clickRate = det.Engagement.ClickRate,
 uniqueOpens = det.Engagement.UniqueOpens,
 totalClicks = det.Engagement.TotalClicks
 });
 }
 catch
 {
 // ignore failures per item
 }
 }

 // top performing articles for the period
 var topArticles = await _analyticsService.GetTopPerformingContentAsync(from, to,5);

 var snapshot = new
 {
 broadcastId = broadcast.Id,
 title = broadcast.Title,
 subject = broadcast.Subject,
 body = broadcast.Body,
 channel,
 targetAudience = broadcast.TargetAudience,
 scheduledSendAt = broadcast.ScheduledSendAt,
 selectedArticleIds,
 selectedInterestTagIds = broadcast.SelectedInterestTagIds,
 selectedIndustryTagIds = broadcast.SelectedIndustryTagIds,
 audience = new
 {
 totalMembers,
 segments,
 interestTagCounts,
 industryTagCounts,
 topCountries
 },
 deviceBreakdown,
 features = new
 {
 hasHeroImages,
 bodyLength,
 subjectLength,
 hasScheduledSend,
 selectedArticleCount
 },
 topTopics,
 recentPerformance,
 topArticles,
 period = new { fromDate = from, toDate = to }
 };

 return snapshot;
 }

        #endregion

        #region Tracking Endpoints

        /// <summary>
        /// Track email open (returns a 1x1 transparent pixel)
    /// </summary>
        [HttpGet("track/open/{broadcastId}/{memberId}")]
 public async Task<IActionResult> TrackEmailOpen(int broadcastId, int memberId)
        {
            try
          {
             var userAgent = Request.Headers["User-Agent"].FirstOrDefault();
       var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

              await _analyticsService.RecordEmailOpenAsync(broadcastId, memberId, userAgent, ipAddress);

     // Return a 1x1 transparent GIF
 var transparentGif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
     return File(transparentGif, "image/gif");
            }
       catch
   {
                // Silently fail - don't break email rendering
      var transparentGif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
       return File(transparentGif, "image/gif");
     }
}

        /// <summary>
        /// Track link click and redirect to original URL
        /// </summary>
        [HttpGet("track/click/{broadcastId}/{memberId}")]
        public async Task<IActionResult> TrackLinkClick(
        int broadcastId,
            int memberId,
  [FromQuery] string url,
            [FromQuery] string? linkId = null,
   [FromQuery] int? articleId = null)
{
            try
         {
         var userAgent = Request.Headers["User-Agent"].FirstOrDefault();
 var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _analyticsService.RecordLinkClickAsync(new LinkClickTrackingDTO
         {
        BroadcastId = broadcastId,
          MemberId = memberId,
  Url = url,
      LinkIdentifier = linkId,
           ArticleId = articleId,
           UserAgent = userAgent,
 IpAddress = ipAddress
        });

     return Redirect(url);
            }
            catch
            {
   // Redirect even if tracking fails
    return Redirect(url);
 }
   }

        #endregion

     #region Admin/Background Tasks

        /// <summary>
        /// Trigger recomputation of analytics summaries (admin only)
        /// </summary>
    [HttpPost("broadcast/compute-summaries")]
        public async Task<IActionResult> ComputeAnalyticsSummaries()
        {
      try
    {
      await _analyticsService.ComputeAnalyticsSummariesAsync();
      return Ok(new { message = "Analytics summaries computed successfully" });
            }
   catch (Exception ex)
            {
    return StatusCode(500, new { error = ex.Message });
            }
      }

        /// <summary>
        /// Update member engagement profiles (admin only)
        /// </summary>
        [HttpPost("broadcast/update-member-profiles")]
      public async Task<IActionResult> UpdateMemberProfiles()
        {
       try
            {
        await _analyticsService.UpdateMemberEngagementProfilesAsync();
            return Ok(new { message = "Member engagement profiles updated successfully" });
   }
      catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
            }
      }

        #endregion

        #region Quick Insights (Real-time, No AI)

 /// <summary>
 /// Get quick insights for a broadcast draft during creation/editing.
 /// This is a lightweight, fast endpoint that doesn't call AI.
 /// Use this for real-time feedback as the user types.
 /// </summary>
 [HttpPost("broadcast/quick-insights")]
 public async Task<IActionResult> GetQuickInsights([FromBody] BroadcastAiInsightsRequestDTO request)
 {
 try
 {
 var result = new BroadcastQuickInsightsDTO();

 // Subject line analysis
 var subjectLen = request.DraftSubject?.Length ?? 0;
 result.SubjectCharCount = subjectLen;
 result.SubjectLengthStatus = subjectLen == 0 ? "Missing" : subjectLen < 30 ? "TooShort" : subjectLen > 60 ? "TooLong" : "Good";
 result.SubjectTip = result.SubjectLengthStatus switch
 {
 "Missing" => "Add a compelling subject line to improve open rates",
 "TooShort" => "Consider adding more context - aim for 40-50 characters",
 "TooLong" => "Shorter subjects (40-50 chars) typically perform better",
 _ => "Good length! Make sure it's compelling and clear"
 };

 // Body analysis
 var bodyWords = string.IsNullOrWhiteSpace(request.DraftBody) ? 0 : request.DraftBody.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
 result.BodyWordCount = bodyWords;
 result.BodyLengthStatus = bodyWords == 0 ? "Missing" : bodyWords < 50 ? "TooShort" : bodyWords > 500 ? "TooLong" : "Good";

 // Estimate recipients based on targeting
 int estimatedRecipients = 0;
 string audienceDesc = "All members";

 if (request.SelectedInterestTagIds?.Any() == true || request.SelectedIndustryTagIds?.Any() == true)
 {
 IQueryable<Member> query = _db.Members;
 
 if (request.SelectedInterestTagIds?.Any() == true)
 {
 var tags = request.SelectedInterestTagIds;
 query = query.Where(m => m.Interests.Any(t => tags.Contains(t.InterestTagId)));
 }
 
 if (request.SelectedIndustryTagIds?.Any() == true)
 {
 var tags = request.SelectedIndustryTagIds;
 query = query.Where(m => m.IndustryTags.Any(t => tags.Contains(t.IndustryTagId)));
 }

 estimatedRecipients = await query.CountAsync();

 // Build audience description
 var tagNames = new List<string>();
 if (request.SelectedInterestTagIds?.Any() == true)
 {
 var names = await _db.InterestTags
 .Where(t => request.SelectedInterestTagIds.Contains(t.InterestTagId))
 .Select(t => t.NameEN)
 .Take(3)
 .ToListAsync();
 tagNames.AddRange(names);
 }
 if (request.SelectedIndustryTagIds?.Any() == true)
 {
 var names = await _db.IndustryTags
 .Where(t => request.SelectedIndustryTagIds.Contains(t.IndustryTagId))
 .Select(t => t.NameEN)
 .Take(2)
 .ToListAsync();
 tagNames.AddRange(names);
 }
 audienceDesc = tagNames.Any() ? $"Members interested in: {string.Join(", ", tagNames)}" : "Targeted members";
 }
 else
 {
 estimatedRecipients = await _db.Members.CountAsync();
 }

 result.EstimatedRecipients = estimatedRecipients;
 result.AudienceDescription = audienceDesc;

 // Timing analysis
 var bestTime = await _analyticsService.GetBestTimeToSendAsync();
 result.SuggestedSendTime = bestTime.BestDayName + " " + (bestTime.BestHourOfDay < 12 ? $"{bestTime.BestHourOfDay}:00 AM" : $"{bestTime.BestHourOfDay - 12}:00 PM");
 
 if (request.PlannedSendTime.HasValue)
 {
 var plannedHour = request.PlannedSendTime.Value.Hour;
 var plannedDay = (int)request.PlannedSendTime.Value.DayOfWeek;
 result.IsPlannedTimeOptimal = Math.Abs(plannedHour - bestTime.BestHourOfDay) <= 2 && plannedDay == bestTime.BestDayOfWeek;
 }
 else
 {
 result.IsPlannedTimeOptimal = false;
 }

 // Article selection analysis
 result.SelectedArticleCount = request.SelectedArticleIds?.Count ?? 0;
 result.ArticleSelectionTip = result.SelectedArticleCount switch
 {
 0 => "Consider adding 1-3 articles to increase engagement",
 1 => "Good start! Adding 1-2 more articles often improves click rates",
 2 or 3 => "Great selection - 2-3 articles is optimal for engagement",
 > 5 => "Consider reducing to 3-4 articles - too many can overwhelm readers",
 _ => "Good number of articles"
 };

 // Calculate readiness score
 var missingItems = new List<string>();
 int readinessScore = 100;

 if (string.IsNullOrWhiteSpace(request.DraftSubject))
 {
 missingItems.Add("Subject line");
 readinessScore -= 30;
 }
 else if (result.SubjectLengthStatus != "Good")
 {
 readinessScore -= 10;
 }

 if (string.IsNullOrWhiteSpace(request.DraftBody))
 {
 missingItems.Add("Email body content");
 readinessScore -= 30;
 }
 else if (result.BodyLengthStatus != "Good")
 {
 readinessScore -= 10;
 }

 if (result.SelectedArticleCount == 0)
 {
 missingItems.Add("At least one article");
 readinessScore -= 15;
 }

 if (estimatedRecipients == 0)
 {
 missingItems.Add("Target audience (no matching recipients)");
 readinessScore -= 25;
 }

 result.ReadinessPercent = Math.Max(0, readinessScore);
 result.MissingItems = missingItems.Any() ? missingItems : null;

 return Ok(result);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to generate quick insights");
 return StatusCode(500, new { error = ex.Message });
 }
 }

 /// <summary>
 /// Get AI insights for a broadcast draft during creation.
 /// Accepts draft content via POST body for real-time feedback.
 /// </summary>
 [HttpPost("broadcast/ai-insights")]
 public async Task<IActionResult> GetBroadcastAiInsights([FromBody] BroadcastAiInsightsRequestDTO request)
 {
 if (_aiService == null)
 {
 return StatusCode(503, new { message = "AI analytics is not configured. Set OpenAIBroadcastAnalytics:ApiKey in configuration." });
 }

 var from = DateTime.UtcNow.AddDays(-30);
 var to = DateTime.UtcNow;

 try
 {
 // Build snapshot from request (draft content)
 object snapshot;
 
 if (request.BroadcastId.HasValue)
 {
 // Editing existing broadcast - use stored data plus any overrides from request
 snapshot = await BuildBroadcastSnapshotAsync(request.BroadcastId.Value, from, to);
 }
 else
 {
 // Creating new broadcast - use request data
 snapshot = await BuildDraftSnapshotAsync(request, from, to);
 }

 BroadcastAiResultDTO typedResult;

 if (_aiService is OpenAIBroadcastAnalyticsService concrete)
 {
 typedResult = await concrete.GenerateRecommendationsTypedAsync(snapshot, HttpContext.RequestAborted);
 }
 else
 {
 var raw = await _aiService.GenerateRecommendationsAsync(snapshot, HttpContext.RequestAborted);
 try
 {
 typedResult = System.Text.Json.JsonSerializer.Deserialize<BroadcastAiResultDTO>(raw, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new BroadcastAiResultDTO { Raw = raw };
 }
 catch
 {
 typedResult = new BroadcastAiResultDTO { Raw = raw };
 }
 }

 return Ok(typedResult);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to generate AI insights for broadcast draft");
 return StatusCode(502, new { message = "AI insights generation failed", error = ex.Message });
 }
 }

 // Build snapshot from draft request (for new broadcasts being created)
 private async Task<object> BuildDraftSnapshotAsync(BroadcastAiInsightsRequestDTO request, DateTime from, DateTime to)
 {
 // Audience estimation
 var totalMembers = await _db.Members.CountAsync();
 var interestTagCounts = new Dictionary<int, int>();
 var industryTagCounts = new Dictionary<int, int>();

 if (request.SelectedInterestTagIds?.Any() == true)
 {
 foreach (var tagId in request.SelectedInterestTagIds)
 {
 var count = await _db.Members.CountAsync(m => m.Interests.Any(t => t.InterestTagId == tagId));
 interestTagCounts[tagId] = count;
 }
 }

 if (request.SelectedIndustryTagIds?.Any() == true)
 {
 foreach (var tagId in request.SelectedIndustryTagIds)
 {
 var count = await _db.Members.CountAsync(m => m.IndustryTags.Any(t => t.IndustryTagId == tagId));
 industryTagCounts[tagId] = count;
 }
 }

 // Get selected article details
 var selectedArticles = new List<object>();
 if (request.SelectedArticleIds?.Any() == true)
 {
 var articles = await _db.PublicationDrafts
 .Where(p => request.SelectedArticleIds.Contains(p.PublicationDraftId))
 .Include(p => p.NewsArticle)
 .Include(p => p.InterestTags)
 .ToListAsync();

 selectedArticles = articles.Select(a => new
 {
 id = a.PublicationDraftId,
 title = a.NewsArticle?.TitleEN ?? a.NewsArticle?.TitleZH ?? "Untitled",
 topics = a.InterestTags?.Select(t => t.NameEN).ToList() ?? new List<string>()
 }).Cast<object>().ToList();
 }

 // Recent performance for context
 var recentBroadcasts = await _db.BroadcastMessages
 .Where(b => b.Status == BroadcastStatus.Sent)
 .OrderByDescending(b => b.UpdatedAt)
 .Take(5)
 .ToListAsync();

 var recentPerformance = new List<object>();
 foreach (var recent in recentBroadcasts)
 {
 try
 {
 var det = await _analyticsService.GetBroadcastAnalyticsAsync(recent.Id);
 recentPerformance.Add(new
 {
 title = recent.Title,
 subjectLength = recent.Subject?.Length ?? 0,
 openRate = det.Engagement.OpenRate,
 clickRate = det.Engagement.ClickRate
 });
 }
 catch { }
 }

 // Top performing articles to suggest
 var topArticles = await _analyticsService.GetTopPerformingContentAsync(from, to, 10);

 // Available articles not yet selected
 var availableArticleIds = request.SelectedArticleIds ?? new List<int>();
 var suggestableArticles = await _db.PublicationDrafts
 .Where(p => p.IsPublished && !availableArticleIds.Contains(p.PublicationDraftId))
 .Include(p => p.NewsArticle)
 .Include(p => p.InterestTags)
 .OrderByDescending(p => p.PublishedAt)
 .Take(20)
 .Select(p => new
 {
 id = p.PublicationDraftId,
 title = p.NewsArticle != null ? (p.NewsArticle.TitleEN ?? p.NewsArticle.TitleZH) : "Untitled",
 topics = p.InterestTags.Select(t => t.NameEN).ToList()
 })
 .ToListAsync();

 var snapshot = new
 {
 isDraft = true,
 title = request.DraftTitle,
 subject = request.DraftSubject,
 body = request.DraftBody,
 plannedSendTime = request.PlannedSendTime,
 selectedArticleIds = request.SelectedArticleIds,
 selectedArticles,
 selectedInterestTagIds = request.SelectedInterestTagIds,
 selectedIndustryTagIds = request.SelectedIndustryTagIds,
 audience = new
 {
 totalMembers,
 interestTagCounts,
 industryTagCounts
 },
 features = new
 {
 subjectLength = request.DraftSubject?.Length ?? 0,
 bodyLength = request.DraftBody?.Length ?? 0,
 selectedArticleCount = request.SelectedArticleIds?.Count ?? 0,
 hasPlannedSendTime = request.PlannedSendTime.HasValue
 },
 recentPerformance,
 topPerformingArticles = topArticles,
 suggestableArticles,
 requestedFeedback = new
 {
 includeSubjectSuggestions = request.IncludeSubjectSuggestions,
 includeArticleSuggestions = request.IncludeArticleSuggestions,
 includeTimingAnalysis = request.IncludeTimingAnalysis,
 includeContentFeedback = request.IncludeContentFeedback
 },
 period = new { fromDate = from, toDate = to }
 };

 return snapshot;
 }

 #endregion
    }
}
