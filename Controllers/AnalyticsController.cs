using Microsoft.AspNetCore.Mvc;
using News_Back_end.Models.SQLServer;
using News_Back_end.DTOs;
using News_Back_end.Services;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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

   public AnalyticsController(
     MyDBContext db, 
         IBroadcastAnalyticsService analyticsService,
            IPracticalAnalyticsService practicalAnalyticsService,
       IConfiguration configuration,
          IBroadcastAnalyticsAiService? aiService = null)
        {
  _db = db;
  _analyticsService = analyticsService;
   _practicalAnalyticsService = practicalAnalyticsService;
  _configuration = configuration;
            _aiService = aiService;
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
        /// </summary>
[HttpGet("broadcast/ai-recommendations")]
        public async Task<IActionResult> GetBroadcastAiRecommendations(
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
       var dashboard = await _analyticsService.GetDashboardAsync(from, to);
          var contentRecs = await _analyticsService.GetContentRecommendationsAsync();
                var bestTime = await _analyticsService.GetBestTimeToSendAsync();

         var snapshot = new
       {
  period = new { fromDate = from, toDate = to },
             overview = dashboard.Overview,
     trends = dashboard.Trends,
               topContent = dashboard.TopContent,
  audienceInsights = dashboard.AudienceInsights,
              recentBroadcasts = dashboard.RecentBroadcasts,
 currentRecommendations = contentRecs,
  bestTimeToSend = bestTime
   };

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
    }
}
