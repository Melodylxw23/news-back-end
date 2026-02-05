using Microsoft.EntityFrameworkCore;
using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;
using System.Text.Json;

namespace News_Back_end.Services
{
 public interface IBroadcastAnalyticsService
    {
  // Dashboard & Overview
  Task<BroadcastAnalyticsDashboardDTO> GetDashboardAsync(DateTime? fromDate = null, DateTime? toDate = null);
 Task<OverviewMetricsDTO> GetOverviewMetricsAsync(DateTime fromDate, DateTime toDate);
        
        // Topic & Content Performance
        Task<TopPerformingContentDTO> GetTopPerformingContentAsync(DateTime fromDate, DateTime toDate, int topCount = 10);
    Task<List<TopicEngagementDTO>> GetTopicPerformanceAsync(DateTime fromDate, DateTime toDate);
   Task<List<TopicTrendDTO>> GetTrendingTopicsAsync(int days = 30);
        
        // Audience Insights
        Task<AudienceInsightsDTO> GetAudienceInsightsAsync();
      Task<MemberEngagementListDTO> GetMemberEngagementListAsync(string? engagementLevel = null, int page = 1, int pageSize = 50);
        Task<MemberEngagementSummaryDTO> GetMemberEngagementAsync(int memberId);
        
// Individual Broadcast Analytics
        Task<DetailedBroadcastAnalyticsDTO> GetBroadcastAnalyticsAsync(int broadcastId);
 Task<List<RecentBroadcastPerformanceDTO>> GetRecentBroadcastsAsync(int count = 10);
   
        // Engagement Trends
      Task<EngagementTrendsDTO> GetEngagementTrendsAsync(DateTime fromDate, DateTime toDate);
        Task<BestTimeToSendDTO> GetBestTimeToSendAsync();
        
     // Tracking
    Task RecordEmailOpenAsync(int broadcastId, int memberId, string? userAgent = null, string? ipAddress = null);
   Task RecordLinkClickAsync(LinkClickTrackingDTO clickData);
        
 // Recommendations
        Task<ContentRecommendationsDTO> GetContentRecommendationsAsync();
        
   // Background processing
        Task ComputeAnalyticsSummariesAsync();
      Task UpdateMemberEngagementProfilesAsync();
    }

    public class BroadcastAnalyticsService : IBroadcastAnalyticsService
    {
     private readonly MyDBContext _context;

        public BroadcastAnalyticsService(MyDBContext context)
        {
     _context = context;
    }

    #region Dashboard & Overview

        public async Task<BroadcastAnalyticsDashboardDTO> GetDashboardAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
         var to = toDate ?? DateTime.UtcNow;
        var from = fromDate ?? to.AddDays(-30);

        return new BroadcastAnalyticsDashboardDTO
            {
     Overview = await GetOverviewMetricsAsync(from, to),
     Trends = await GetEngagementTrendsAsync(from, to),
   TopContent = await GetTopPerformingContentAsync(from, to),
     AudienceInsights = await GetAudienceInsightsAsync(),
       RecentBroadcasts = await GetRecentBroadcastsAsync(10)
    };
        }

      public async Task<OverviewMetricsDTO> GetOverviewMetricsAsync(DateTime fromDate, DateTime toDate)
        {
var deliveries = await _context.BroadcastDeliveries
                .Where(d => d.SentAt >= fromDate && d.SentAt <= toDate)
            .ToListAsync();

        var totalSent = deliveries.Count;
  var delivered = deliveries.Count(d => d.DeliverySuccess);
            var bounced = deliveries.Count(d => d.BounceType != BounceType.None);
            var uniqueOpens = deliveries.Count(d => d.EmailOpened);
      var totalClicks = deliveries.Sum(d => d.ClickCount);
   var unsubscribes = deliveries.Count(d => d.Unsubscribed);

            // Get previous period for comparison
        var periodLength = (toDate - fromDate).TotalDays;
         var previousFrom = fromDate.AddDays(-periodLength);
            var previousTo = fromDate;

       var previousDeliveries = await _context.BroadcastDeliveries
  .Where(d => d.SentAt >= previousFrom && d.SentAt < previousTo)
 .ToListAsync();

   var prevDelivered = previousDeliveries.Count(d => d.DeliverySuccess);
   var prevOpens = previousDeliveries.Count(d => d.EmailOpened);
        var prevClicks = previousDeliveries.Sum(d => d.ClickCount);

         var currentOpenRate = delivered > 0 ? (double)uniqueOpens / delivered * 100 : 0;
  var currentClickRate = delivered > 0 ? (double)totalClicks / delivered * 100 : 0;
        var prevOpenRate = prevDelivered > 0 ? (double)prevOpens / prevDelivered * 100 : 0;
      var prevClickRate = prevDelivered > 0 ? (double)prevClicks / prevDelivered * 100 : 0;

            var broadcastCount = await _context.BroadcastMessages
  .CountAsync(b => b.Status == BroadcastStatus.Sent &&
          b.UpdatedAt >= fromDate && b.UpdatedAt <= toDate);

            return new OverviewMetricsDTO
   {
     FromDate = fromDate,
    ToDate = toDate,
          TotalBroadcastsSent = broadcastCount,
      TotalEmailsSent = totalSent,
       TotalEmailsDelivered = delivered,
       TotalUniqueOpens = uniqueOpens,
        TotalClicks = totalClicks,
     AverageOpenRate = Math.Round(currentOpenRate, 2),
          AverageClickRate = Math.Round(currentClickRate, 2),
      AverageClickToOpenRate = uniqueOpens > 0 ? Math.Round((double)totalClicks / uniqueOpens * 100, 2) : 0,
         OpenRateChange = prevOpenRate > 0 ? Math.Round((currentOpenRate - prevOpenRate) / prevOpenRate * 100, 2) : 0,
         ClickRateChange = prevClickRate > 0 ? Math.Round((currentClickRate - prevClickRate) / prevClickRate * 100, 2) : 0,
    VolumeChange = previousDeliveries.Count > 0 ? Math.Round((double)(totalSent - previousDeliveries.Count) / previousDeliveries.Count * 100, 2) : 0,
                DeliveryRate = totalSent > 0 ? Math.Round((double)delivered / totalSent * 100, 2) : 0,
     BounceRate = totalSent > 0 ? Math.Round((double)bounced / totalSent * 100, 2) : 0,
        UnsubscribeRate = delivered > 0 ? Math.Round((double)unsubscribes / delivered * 100, 2) : 0
            };
  }

    #endregion

      #region Topic & Content Performance

   public async Task<TopPerformingContentDTO> GetTopPerformingContentAsync(DateTime fromDate, DateTime toDate, int topCount = 10)
        {
       return new TopPerformingContentDTO
            {
       TopInterestTopics = await GetTopInterestTopicsAsync(fromDate, toDate, topCount),
       TopIndustryTopics = await GetTopIndustryTopicsAsync(fromDate, toDate, topCount),
        TopArticles = await GetTopArticlesAsync(fromDate, toDate, topCount),
    TrendingTopics = await GetTrendingTopicsAsync(30)
            };
        }

        private async Task<List<TopicEngagementDTO>> GetTopInterestTopicsAsync(DateTime fromDate, DateTime toDate, int topCount)
        {
            // Get all broadcasts in the period with their articles and tags
            var broadcastData = await _context.BroadcastDeliveries
        .Where(d => d.SentAt >= fromDate && d.SentAt <= toDate)
 .Include(d => d.BroadcastMessage)
   .ThenInclude(b => b.SelectedArticles)
             .ThenInclude(a => a.InterestTags)
    .ToListAsync();

          // Group by interest tag and calculate metrics
     var tagMetrics = new Dictionary<int, (InterestTag Tag, int Sent, int Opens, int Clicks)>();

      foreach (var delivery in broadcastData)
        {
                foreach (var article in delivery.BroadcastMessage.SelectedArticles)
                {
          foreach (var tag in article.InterestTags)
          {
      if (!tagMetrics.ContainsKey(tag.InterestTagId))
            {
    tagMetrics[tag.InterestTagId] = (tag, 0, 0, 0);
            }

       var current = tagMetrics[tag.InterestTagId];
    tagMetrics[tag.InterestTagId] = (
    current.Tag,
                current.Sent + 1,
   current.Opens + (delivery.EmailOpened ? 1 : 0),
       current.Clicks + delivery.ClickCount
         );
          }
           }
  }

            return tagMetrics.Values
                .Select(t => new TopicEngagementDTO
                {
          TagId = t.Tag.InterestTagId,
         TagNameEN = t.Tag.NameEN,
 TagNameZH = t.Tag.NameZH,
   TotalRecipients = t.Sent,
   TotalOpens = t.Opens,
  TotalClicks = t.Clicks,
       OpenRate = t.Sent > 0 ? Math.Round((double)t.Opens / t.Sent * 100, 2) : 0,
        ClickRate = t.Sent > 0 ? Math.Round((double)t.Clicks / t.Sent * 100, 2) : 0,
      EngagementScore = CalculateEngagementScore(t.Sent, t.Opens, t.Clicks),
       PerformanceLevel = GetPerformanceLevel(t.Sent > 0 ? (double)t.Opens / t.Sent * 100 : 0)
                })
      .OrderByDescending(t => t.EngagementScore)
          .Take(topCount)
 .ToList();
      }

        private async Task<List<TopicEngagementDTO>> GetTopIndustryTopicsAsync(DateTime fromDate, DateTime toDate, int topCount)
        {
        var broadcastData = await _context.BroadcastDeliveries
      .Where(d => d.SentAt >= fromDate && d.SentAt <= toDate)
                .Include(d => d.BroadcastMessage)
          .ThenInclude(b => b.SelectedArticles)
   .ThenInclude(a => a.IndustryTag)
        .ToListAsync();

    var tagMetrics = new Dictionary<int, (IndustryTag Tag, int Sent, int Opens, int Clicks)>();

  foreach (var delivery in broadcastData)
         {
foreach (var article in delivery.BroadcastMessage.SelectedArticles)
       {
          if (article.IndustryTag == null) continue;

              var tag = article.IndustryTag;
           if (!tagMetrics.ContainsKey(tag.IndustryTagId))
{
      tagMetrics[tag.IndustryTagId] = (tag, 0, 0, 0);
    }

    var current = tagMetrics[tag.IndustryTagId];
           tagMetrics[tag.IndustryTagId] = (
           current.Tag,
       current.Sent + 1,
             current.Opens + (delivery.EmailOpened ? 1 : 0),
     current.Clicks + delivery.ClickCount
         );
        }
    }

    return tagMetrics.Values
   .Select(t => new TopicEngagementDTO
           {
            TagId = t.Tag.IndustryTagId,
   TagNameEN = t.Tag.NameEN,
                 TagNameZH = t.Tag.NameZH,
  TotalRecipients = t.Sent,
          TotalOpens = t.Opens,
                    TotalClicks = t.Clicks,
             OpenRate = t.Sent > 0 ? Math.Round((double)t.Opens / t.Sent * 100, 2) : 0,
        ClickRate = t.Sent > 0 ? Math.Round((double)t.Clicks / t.Sent * 100, 2) : 0,
       EngagementScore = CalculateEngagementScore(t.Sent, t.Opens, t.Clicks),
        PerformanceLevel = GetPerformanceLevel(t.Sent > 0 ? (double)t.Opens / t.Sent * 100 : 0)
          })
     .OrderByDescending(t => t.EngagementScore)
    .Take(topCount)
      .ToList();
        }

        private async Task<List<ArticlePerformanceDTO>> GetTopArticlesAsync(DateTime fromDate, DateTime toDate, int topCount = 10)
        {
       // First check if we have any click data
    var hasClicks = await _context.BroadcastLinkClicks
      .AnyAsync(c => c.ClickedAt >= fromDate && c.ClickedAt <= toDate && c.PublicationDraftId != null);
   
     if (!hasClicks)
      {
   // No click data - return empty list or articles included in broadcasts without click metrics
      return new List<ArticlePerformanceDTO>();
     }
    
      var articleClicks = await _context.BroadcastLinkClicks
         .Where(c => c.ClickedAt >= fromDate && c.ClickedAt <= toDate && c.PublicationDraftId != null)
            .Include(c => c.PublicationDraft)
           .ThenInclude(p => p!.NewsArticle)
    .Include(c => c.PublicationDraft)
  .ThenInclude(p => p!.InterestTags)
   .GroupBy(c => c.PublicationDraftId)
     .Select(g => new
 {
            PublicationDraftId = g.Key!.Value,
      TotalClicks = g.Count(),
           Article = g.First().PublicationDraft
  })
 .OrderByDescending(x => x.TotalClicks)
     .Take(topCount)
    .ToListAsync();

       return articleClicks.Select(a => new ArticlePerformanceDTO
    {
        PublicationDraftId = a.PublicationDraftId,
   TitleEN = a.Article?.NewsArticle?.TitleEN ?? "Untitled",
TitleZH = a.Article?.NewsArticle?.TitleZH ?? "ÎÞ±êÌâ",
    TotalClicks = a.TotalClicks,
     Topics = a.Article?.InterestTags.Select(t => t.NameEN).ToList() ?? new List<string>()
            }).ToList();
        }

        public async Task<List<TopicEngagementDTO>> GetTopicPerformanceAsync(DateTime fromDate, DateTime toDate)
        {
            var interestTopics = await GetTopInterestTopicsAsync(fromDate, toDate, 50);
  var industryTopics = await GetTopIndustryTopicsAsync(fromDate, toDate, 50);

            return interestTopics.Concat(industryTopics)
        .OrderByDescending(t => t.EngagementScore)
       .ToList();
   }

        public async Task<List<TopicTrendDTO>> GetTrendingTopicsAsync(int days = 30)
        {
    var currentEnd = DateTime.UtcNow;
      var currentStart = currentEnd.AddDays(-days);
        var previousEnd = currentStart;
    var previousStart = previousEnd.AddDays(-days);

            var currentMetrics = await GetTopInterestTopicsAsync(currentStart, currentEnd, 100);
            var previousMetrics = await GetTopInterestTopicsAsync(previousStart, previousEnd, 100);

    var previousDict = previousMetrics.ToDictionary(t => t.TagId);

            return currentMetrics.Select(current =>
    {
       var previous = previousDict.GetValueOrDefault(current.TagId);
                var prevScore = previous?.EngagementScore ?? 0;
       var change = prevScore > 0 ? (current.EngagementScore - prevScore) / prevScore * 100 : 100;

    return new TopicTrendDTO
     {
       TagId = current.TagId,
        TagName = current.TagNameEN,
   TrendDirection = change > 10 ? "Rising" : change < -10 ? "Declining" : "Stable",
         CurrentEngagementScore = current.EngagementScore,
         PreviousEngagementScore = prevScore,
        ChangePercentage = Math.Round(change, 2)
         };
            })
            .OrderByDescending(t => t.ChangePercentage)
            .Take(10)
            .ToList();
        }

        #endregion

   #region Audience Insights

   public async Task<AudienceInsightsDTO> GetAudienceInsightsAsync()
  {
    var now = DateTime.UtcNow;
   var thirtyDaysAgo = now.AddDays(-30);
          var sixtyDaysAgo = now.AddDays(-60);
     var ninetyDaysAgo = now.AddDays(-90);

    var members = await _context.Members
    .Include(m => m.IndustryTags)
          .ToListAsync();

 var profiles = await _context.MemberEngagementProfiles.ToListAsync();
 var profileDict = profiles.ToDictionary(p => p.MemberId);

 // Calculate engagement segments
var totalSubscribers = members.Count;
  var activeSubscribers = 0;
      var atRiskSubscribers = 0;
     var inactiveSubscribers = 0;

  var engagementCounts = new Dictionary<string, int>
            {
  { "HighlyEngaged", 0 },
      { "Engaged", 0 },
  { "Occasional", 0 },
    { "Inactive", 0 }
     };

       foreach (var member in members)
      {
  if (profileDict.TryGetValue(member.MemberId, out var profile))
   {
       if (profile.LastEmailOpenedAt >= thirtyDaysAgo)
 activeSubscribers++;
   else if (profile.LastEmailOpenedAt >= sixtyDaysAgo)
   atRiskSubscribers++;
else
            inactiveSubscribers++;

         if (engagementCounts.ContainsKey(profile.EngagementLevel))
 engagementCounts[profile.EngagementLevel]++;
     }
  else
   {
    inactiveSubscribers++;
         engagementCounts["Inactive"]++;
     }
        }

   // Country breakdown - load data first, then group on client side
   var deliveriesWithMembers = await _context.BroadcastDeliveries
        .Include(d => d.Member)
   .Where(d => d.Member != null)
      .ToListAsync();

    // Handle case where there are no deliveries
    var byCountry = new List<DemographicEngagementDTO>();
    var byLanguage = new List<DemographicEngagementDTO>();

    if (deliveriesWithMembers.Any())
    {
      byCountry = deliveriesWithMembers
     .GroupBy(d => d.Member!.Country.ToString())
  .Select(g => new DemographicEngagementDTO
        {
 Group = g.Key,
MemberCount = g.Select(d => d.MemberId).Distinct().Count(),
EmailsReceived = g.Count(),
    EmailsOpened = g.Count(d => d.EmailOpened),
     OpenRate = g.Count() > 0 ? Math.Round((double)g.Count(d => d.EmailOpened) / g.Count() * 100, 2) : 0
        })
      .ToList();

 // Language breakdown - client-side grouping
          byLanguage = deliveriesWithMembers
       .GroupBy(d => d.Member!.PreferredLanguage.ToString())
   .Select(g => new DemographicEngagementDTO
    {
 Group = g.Key,
    MemberCount = g.Select(d => d.MemberId).Distinct().Count(),
 EmailsReceived = g.Count(),
            EmailsOpened = g.Count(d => d.EmailOpened),
 OpenRate = g.Count() > 0 ? Math.Round((double)g.Count(d => d.EmailOpened) / g.Count() * 100, 2) : 0
  })
.ToList();
    }

   // Device breakdown
var deviceStats = await _context.BroadcastDeliveries
    .Where(d => d.EmailOpened)
            .GroupBy(d => d.DeviceType ?? "Unknown")
      .Select(g => new { DeviceType = g.Key, Count = g.Count() })
    .ToListAsync();

          var totalOpens = deviceStats.Sum(d => d.Count);
   var deviceBreakdown = new DeviceBreakdownDTO
    {
   DesktopOpens = deviceStats.FirstOrDefault(d => d.DeviceType == "Desktop")?.Count ?? 0,
    MobileOpens = deviceStats.FirstOrDefault(d => d.DeviceType == "Mobile")?.Count ?? 0,
          TabletOpens = deviceStats.FirstOrDefault(d => d.DeviceType == "Tablet")?.Count ?? 0,
  UnknownOpens = deviceStats.FirstOrDefault(d => d.DeviceType == "Unknown")?.Count ?? 0
       };

       if (totalOpens > 0)
  {
       deviceBreakdown.DesktopPercentage = Math.Round((double)deviceBreakdown.DesktopOpens / totalOpens * 100, 2);
    deviceBreakdown.MobilePercentage = Math.Round((double)deviceBreakdown.MobileOpens / totalOpens * 100, 2);
     deviceBreakdown.TabletPercentage = Math.Round((double)deviceBreakdown.TabletOpens / totalOpens * 100, 2);
    }

     return new AudienceInsightsDTO
    {
 TotalSubscribers = totalSubscribers,
 ActiveSubscribers = activeSubscribers,
   AtRiskSubscribers = atRiskSubscribers,
     InactiveSubscribers = inactiveSubscribers,
  EngagementSegments = engagementCounts.Select(kv => new EngagementSegmentDTO
 {
 SegmentName = kv.Key,
    MemberCount = kv.Value,
        PercentageOfTotal = totalSubscribers > 0 ? Math.Round((double)kv.Value / totalSubscribers * 100, 2) : 0,
       Description = GetSegmentDescription(kv.Key)
    }).ToList(),
         ByCountry = byCountry,
     ByLanguage = byLanguage,
   DeviceBreakdown = deviceBreakdown
 };
        }

        public async Task<MemberEngagementListDTO> GetMemberEngagementListAsync(string? engagementLevel = null, int page = 1, int pageSize = 50)
        {
 var query = _context.MemberEngagementProfiles
              .Include(p => p.Member)
            .AsQueryable();

       if (!string.IsNullOrEmpty(engagementLevel))
 {
                query = query.Where(p => p.EngagementLevel == engagementLevel);
   }

            var totalCount = await query.CountAsync();
            var profiles = await query
 .OrderByDescending(p => p.OverallEngagementScore)
      .Skip((page - 1) * pageSize)
      .Take(pageSize)
    .ToListAsync();

   var allProfiles = await _context.MemberEngagementProfiles.ToListAsync();

return new MemberEngagementListDTO
            {
         TotalMembers = totalCount,
    HighlyEngaged = allProfiles.Count(p => p.EngagementLevel == "HighlyEngaged"),
           Engaged = allProfiles.Count(p => p.EngagementLevel == "Engaged"),
      Occasional = allProfiles.Count(p => p.EngagementLevel == "Occasional"),
Inactive = allProfiles.Count(p => p.EngagementLevel == "Inactive"),
  Members = profiles.Select(p => MapToMemberEngagementSummary(p)).ToList()
      };
        }

    public async Task<MemberEngagementSummaryDTO> GetMemberEngagementAsync(int memberId)
        {
    var profile = await _context.MemberEngagementProfiles
         .Include(p => p.Member)
  .FirstOrDefaultAsync(p => p.MemberId == memberId);

          if (profile == null)
 {
     var member = await _context.Members.FindAsync(memberId);
     if (member == null)
    throw new ArgumentException("Member not found", nameof(memberId));

           return new MemberEngagementSummaryDTO
    {
           MemberId = memberId,
         ContactPerson = member.ContactPerson,
      Email = member.Email,
            CompanyName = member.CompanyName,
EngagementLevel = "Unknown",
 EngagementScore = 0
        };
       }

            return MapToMemberEngagementSummary(profile);
        }

      #endregion

    #region Individual Broadcast Analytics

        public async Task<DetailedBroadcastAnalyticsDTO> GetBroadcastAnalyticsAsync(int broadcastId)
        {
    var broadcast = await _context.BroadcastMessages
           .Include(b => b.SelectedArticles)
 .ThenInclude(a => a.NewsArticle)
        .FirstOrDefaultAsync(b => b.Id == broadcastId);

 if (broadcast == null)
           throw new ArgumentException("Broadcast not found", nameof(broadcastId));

 var deliveries = await _context.BroadcastDeliveries
        .Include(d => d.Member)
     .Include(d => d.LinkClicks)
      .Where(d => d.BroadcastMessageId == broadcastId)
      .ToListAsync();

 var totalSent = deliveries.Count;
var delivered = deliveries.Count(d => d.DeliverySuccess);
       var bounced = deliveries.Count(d => d.BounceType != BounceType.None);
     var hardBounces = deliveries.Count(d => d.BounceType == BounceType.Hard);
   var softBounces = deliveries.Count(d => d.BounceType == BounceType.Soft);

 var uniqueOpens = deliveries.Count(d => d.EmailOpened);
 var totalOpens = deliveries.Sum(d => d.OpenCount);
     var uniqueClicks = deliveries.Count(d => d.HasClicked);
   var totalClicks = deliveries.Sum(d => d.ClickCount);
     var unsubscribes = deliveries.Count(d => d.Unsubscribed);

  // Article click breakdown
        var articleClicks = deliveries
   .SelectMany(d => d.LinkClicks)
      .Where(c => c.PublicationDraftId != null)
  .GroupBy(c => c.PublicationDraftId)
   .Select(g => new ArticleClickDTO
     {
 PublicationDraftId = g.Key!.Value,
 Title = broadcast.SelectedArticles
     .FirstOrDefault(a => a.PublicationDraftId == g.Key)?.NewsArticle?.TitleEN ?? "Unknown",
         ClickCount = g.Count(),
        UniqueClicks = g.Select(c => c.BroadcastDelivery.MemberId).Distinct().Count(),
          ClickPercentage = totalClicks > 0 ? Math.Round((double)g.Count() / totalClicks * 100, 2) : 0
        })
   .OrderByDescending(a => a.ClickCount)
          .ToList();

     // Engagement timeline (hourly for first 48 hours)
   // Handle case when there are no deliveries
   var sentAt = deliveries.Any() ? deliveries.Min(d => d.SentAt) : broadcast.UpdatedAt.DateTime;
   var timeline = new List<EngagementTimelineDTO>();
      for (int i = 0; i < 48; i++)
        {
         var hourStart = sentAt.AddHours(i);
   var hourEnd = hourStart.AddHours(1);
          var opens = deliveries.Count(d => d.FirstOpenedAt >= hourStart && d.FirstOpenedAt < hourEnd);
     var clicks = deliveries.SelectMany(d => d.LinkClicks).Count(c => c.ClickedAt >= hourStart && c.ClickedAt < hourEnd);

 timeline.Add(new EngagementTimelineDTO
 {
      Timestamp = hourStart,
       CumulativeOpens = deliveries.Count(d => d.FirstOpenedAt < hourEnd && d.EmailOpened),
  CumulativeClicks = deliveries.SelectMany(d => d.LinkClicks).Count(c => c.ClickedAt < hourEnd)
    });
         }

      return new DetailedBroadcastAnalyticsDTO
  {
     BroadcastId = broadcastId,
      Title = broadcast.Title,
         Subject = broadcast.Subject,
         SentAt = sentAt,
      Status = broadcast.Status.ToString(),
  Delivery = new DeliveryMetricsDTO
    {
          TotalSent = totalSent,
 Delivered = delivered,
    Bounced = bounced,
     HardBounces = hardBounces,
       SoftBounces = softBounces,
     DeliveryRate = totalSent > 0 ? Math.Round((double)delivered / totalSent * 100, 2) : 0,
          BounceRate = totalSent > 0 ? Math.Round((double)bounced / totalSent * 100, 2) : 0
    },
   Engagement = new EngagementMetricsDTO
       {
   UniqueOpens = uniqueOpens,
        TotalOpens = totalOpens,
     UniqueClicks = uniqueClicks,
    TotalClicks = totalClicks,
      OpenRate = delivered > 0 ? Math.Round((double)uniqueOpens / delivered * 100, 2) : 0,
     ClickRate = delivered > 0 ? Math.Round((double)uniqueClicks / delivered * 100, 2) : 0,
  ClickToOpenRate = uniqueOpens > 0 ? Math.Round((double)uniqueClicks / uniqueOpens * 100, 2) : 0,
        Unsubscribes = unsubscribes,
          UnsubscribeRate = delivered > 0 ? Math.Round((double)unsubscribes / delivered * 100, 2) : 0,
      AverageTimeToOpen = CalculateAverageTimeToOpen(deliveries, sentAt)
      },
 ArticleClicks = articleClicks,
         EngagementTimeline = timeline
            };
 }

        public async Task<List<RecentBroadcastPerformanceDTO>> GetRecentBroadcastsAsync(int count = 10)
        {
        var broadcasts = await _context.BroadcastMessages
          .Where(b => b.Status == BroadcastStatus.Sent)
     .OrderByDescending(b => b.UpdatedAt)
       .Take(count)
        .ToListAsync();

            var result = new List<RecentBroadcastPerformanceDTO>();

            foreach (var broadcast in broadcasts)
  {
 var deliveries = await _context.BroadcastDeliveries
        .Where(d => d.BroadcastMessageId == broadcast.Id)
       .ToListAsync();

           var totalSent = deliveries.Count;
           var delivered = deliveries.Count(d => d.DeliverySuccess);
   var uniqueOpens = deliveries.Count(d => d.EmailOpened);
 var clicks = deliveries.Sum(d => d.ClickCount);

         var openRate = delivered > 0 ? (double)uniqueOpens / delivered * 100 : 0;

  result.Add(new RecentBroadcastPerformanceDTO
                {
         BroadcastId = broadcast.Id,
 Title = broadcast.Title,
          SentAt = deliveries.Any() ? deliveries.Min(d => d.SentAt) : broadcast.UpdatedAt.DateTime,
    TotalSent = totalSent,
         Delivered = delivered,
            UniqueOpens = uniqueOpens,
     Clicks = clicks,
 OpenRate = Math.Round(openRate, 2),
     ClickRate = delivered > 0 ? Math.Round((double)clicks / delivered * 100, 2) : 0,
      PerformanceRating = GetPerformanceRating(openRate)
     });
  }

      return result;
}

        #endregion

        #region Engagement Trends

  public async Task<EngagementTrendsDTO> GetEngagementTrendsAsync(DateTime fromDate, DateTime toDate)
        {
            var deliveries = await _context.BroadcastDeliveries
    .Where(d => d.SentAt >= fromDate && d.SentAt <= toDate)
                .ToListAsync();

      // Daily metrics
var dailyMetrics = deliveries
         .GroupBy(d => d.SentAt.Date)
      .Select(g => new DailyEngagementDTO
                {
          Date = g.Key,
            EmailsSent = g.Count(),
     Opens = g.Count(d => d.EmailOpened),
           Clicks = g.Sum(d => d.ClickCount),
            OpenRate = g.Count() > 0 ? Math.Round((double)g.Count(d => d.EmailOpened) / g.Count() * 100, 2) : 0,
         ClickRate = g.Count() > 0 ? Math.Round((double)g.Sum(d => d.ClickCount) / g.Count() * 100, 2) : 0
           })
    .OrderBy(d => d.Date)
    .ToList();

// Weekday performance
 var weekdayPerformance = deliveries
        .GroupBy(d => d.SentAt.DayOfWeek)
   .Select(g => new WeekdayPerformanceDTO
  {
DayOfWeek = (int)g.Key,
             DayName = g.Key.ToString(),
               BroadcastsSent = g.Select(d => d.BroadcastMessageId).Distinct().Count(),
  AverageOpenRate = g.Count() > 0 ? Math.Round((double)g.Count(d => d.EmailOpened) / g.Count() * 100, 2) : 0,
  AverageClickRate = g.Count() > 0 ? Math.Round((double)g.Sum(d => d.ClickCount) / g.Count() * 100, 2) : 0
      })
          .OrderBy(w => w.DayOfWeek)
    .ToList();

  return new EngagementTrendsDTO
  {
                DailyMetrics = dailyMetrics,
  BestTimeToSend = await GetBestTimeToSendAsync(),
           WeekdayPerformance = weekdayPerformance
  };
        }

        public async Task<BestTimeToSendDTO> GetBestTimeToSendAsync()
        {
      var deliveries = await _context.BroadcastDeliveries
           .Where(d => d.EmailOpened && d.FirstOpenedAt != null)
          .ToListAsync();

          if (!deliveries.Any())
            {
          return new BestTimeToSendDTO
             {
     BestHourOfDay = 9,
    BestDayOfWeek = 2, // Tuesday
          BestDayName = "Tuesday"
    };
      }

   // Group by hour of day when email was opened
     var hourlyEngagement = deliveries
      .GroupBy(d => d.FirstOpenedAt!.Value.Hour)
   .Select(g => new HourlyEngagementDTO
                {
 Hour = g.Key,
       TotalOpens = g.Count(),
     AverageOpenRate = 0 // Would need total sent at that hour to calculate
        })
      .OrderByDescending(h => h.TotalOpens)
   .ToList();

            var bestHour = hourlyEngagement.FirstOrDefault()?.Hour ?? 9;

  // Group by day of week
  var dayPerformance = deliveries
         .GroupBy(d => d.SentAt.DayOfWeek)
    .Select(g => new { Day = g.Key, Opens = g.Count() })
    .OrderByDescending(d => d.Opens)
          .FirstOrDefault();

          var bestDay = dayPerformance?.Day ?? DayOfWeek.Tuesday;

          return new BestTimeToSendDTO
    {
         BestHourOfDay = bestHour,
    BestDayOfWeek = (int)bestDay,
             BestDayName = bestDay.ToString(),
                HourlyBreakdown = hourlyEngagement.OrderBy(h => h.Hour).ToList()
            };
        }

        #endregion

    #region Tracking

      public async Task RecordEmailOpenAsync(int broadcastId, int memberId, string? userAgent = null, string? ipAddress = null)
    {
            var delivery = await _context.BroadcastDeliveries
                .FirstOrDefaultAsync(d => d.BroadcastMessageId == broadcastId && d.MemberId == memberId);

         if (delivery != null)
   {
        var now = DateTime.UtcNow;

                if (!delivery.EmailOpened)
     {
      delivery.EmailOpened = true;
           delivery.FirstOpenedAt = now;
             delivery.UserAgent = userAgent;
        delivery.IpAddress = ipAddress;
      delivery.DeviceType = ParseDeviceType(userAgent);
           delivery.EmailClient = ParseEmailClient(userAgent);
        }

         delivery.LastOpenedAt = now;
          delivery.OpenCount++;

           await _context.SaveChangesAsync();

                // Update member engagement profile asynchronously
        _ = UpdateSingleMemberProfileAsync(memberId);
    }
        }

        public async Task RecordLinkClickAsync(LinkClickTrackingDTO clickData)
        {
 var delivery = await _context.BroadcastDeliveries
         .FirstOrDefaultAsync(d => d.BroadcastMessageId == clickData.BroadcastId && d.MemberId == clickData.MemberId);

if (delivery != null)
 {
           var now = DateTime.UtcNow;

    // Record click details
   var linkClick = new BroadcastLinkClick
                {
     BroadcastDeliveryId = delivery.BroadcastDeliveryId,
      PublicationDraftId = clickData.ArticleId,
OriginalUrl = clickData.Url,
  LinkIdentifier = clickData.LinkIdentifier,
         ClickedAt = now,
  UserAgent = clickData.UserAgent,
              IpAddress = clickData.IpAddress,
   DeviceType = ParseDeviceType(clickData.UserAgent),
          EmailClient = ParseEmailClient(clickData.UserAgent)
       };

     _context.BroadcastLinkClicks.Add(linkClick);

   // Update delivery summary
           if (!delivery.HasClicked)
           {
      delivery.HasClicked = true;
    delivery.FirstClickedAt = now;
          }
       delivery.LastClickedAt = now;
      delivery.ClickCount++;

        await _context.SaveChangesAsync();
            }
        }

        #endregion

     #region Recommendations

        public async Task<ContentRecommendationsDTO> GetContentRecommendationsAsync()
   {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
   var topicPerformance = await GetTopicPerformanceAsync(thirtyDaysAgo, DateTime.UtcNow);
            var bestTime = await GetBestTimeToSendAsync();

     var topicsToFocus = topicPerformance
.Where(t => t.EngagementScore >= 70)
 .OrderByDescending(t => t.EngagementScore)
       .Take(5)
          .Select(t => new TopicRecommendationDTO
     {
          RecommendationType = "IncreaseFrequency",
            TagId = t.TagId,
         TagName = t.TagNameEN,
               Reason = $"High engagement with {t.OpenRate}% open rate",
           CurrentEngagement = t.EngagementScore,
 ExpectedImpact = 15
                })
            .ToList();

    var topicsToAvoid = topicPerformance
      .Where(t => t.EngagementScore < 30 && t.TotalRecipients > 100)
 .OrderBy(t => t.EngagementScore)
      .Take(5)
          .Select(t => new TopicRecommendationDTO
                {
   RecommendationType = "ReduceFrequency",
            TagId = t.TagId,
        TagName = t.TagNameEN,
     Reason = $"Low engagement with only {t.OpenRate}% open rate",
          CurrentEngagement = t.EngagementScore,
        ExpectedImpact = -10
       })
          .ToList();

        var recommendations = new List<string>();

            var audienceInsights = await GetAudienceInsightsAsync();
 if (audienceInsights.AtRiskSubscribers > audienceInsights.TotalSubscribers * 0.2)
      {
     recommendations.Add("Consider a re-engagement campaign - over 20% of subscribers are at risk of churning.");
          }

            if (audienceInsights.DeviceBreakdown.MobilePercentage > 50)
            {
            recommendations.Add("Optimize email templates for mobile - majority of opens are on mobile devices.");
   }

            return new ContentRecommendationsDTO
   {
   TopicsToFocus = topicsToFocus,
     TopicsToAvoid = topicsToAvoid,
   OptimalSendTime = bestTime,
       GeneralRecommendations = recommendations
         };
        }

        #endregion

        #region Background Processing

        public async Task ComputeAnalyticsSummariesAsync()
        {
            var broadcasts = await _context.BroadcastMessages
   .Where(b => b.Status == BroadcastStatus.Sent)
                .ToListAsync();

    foreach (var broadcast in broadcasts)
    {
  var deliveries = await _context.BroadcastDeliveries
      .Include(d => d.Member)
          .Include(d => d.LinkClicks)
  .Where(d => d.BroadcastMessageId == broadcast.Id)
     .ToListAsync();

        var summary = await _context.BroadcastAnalyticsSummaries
      .FirstOrDefaultAsync(s => s.BroadcastMessageId == broadcast.Id);

   if (summary == null)
      {
     summary = new BroadcastAnalyticsSummary { BroadcastMessageId = broadcast.Id };
       _context.BroadcastAnalyticsSummaries.Add(summary);
     }

      // Update metrics
    summary.TotalSent = deliveries.Count;
       summary.TotalDelivered = deliveries.Count(d => d.DeliverySuccess);
    summary.TotalBounced = deliveries.Count(d => d.BounceType != BounceType.None);
  summary.DeliveryRate = summary.TotalSent > 0 ? (double)summary.TotalDelivered / summary.TotalSent * 100 : 0;

      summary.UniqueOpens = deliveries.Count(d => d.EmailOpened);
    summary.TotalOpens = deliveries.Sum(d => d.OpenCount);
   summary.OpenRate = summary.TotalDelivered > 0 ? (double)summary.UniqueOpens / summary.TotalDelivered * 100 : 0;

    summary.UniqueClicks = deliveries.Count(d => d.HasClicked);
      summary.TotalClicks = deliveries.Sum(d => d.ClickCount);
     summary.ClickRate = summary.TotalDelivered > 0 ? (double)summary.UniqueClicks / summary.TotalDelivered * 100 : 0;
  summary.ClickToOpenRate = summary.UniqueOpens > 0 ? (double)summary.UniqueClicks / summary.UniqueOpens * 100 : 0;

       // Fix: handle empty collection for Min/Max
         var openedDeliveries = deliveries.Where(d => d.EmailOpened).ToList();
       summary.FirstOpenAt = openedDeliveries.Any() ? openedDeliveries.Min(d => d.FirstOpenedAt) : null;
   summary.LastOpenAt = openedDeliveries.Any() ? openedDeliveries.Max(d => d.LastOpenedAt) : null;

// Device breakdown
 summary.DesktopOpens = deliveries.Count(d => d.DeviceType == "Desktop");
        summary.MobileOpens = deliveries.Count(d => d.DeviceType == "Mobile");
   summary.TabletOpens = deliveries.Count(d => d.DeviceType == "Tablet");

   summary.ComputedAt = DateTime.UtcNow;
 }

      await _context.SaveChangesAsync();
      }

        public async Task UpdateMemberEngagementProfilesAsync()
        {
  var members = await _context.Members.ToListAsync();

            foreach (var member in members)
   {
                await UpdateSingleMemberProfileAsync(member.MemberId);
            }
     }

     private async Task UpdateSingleMemberProfileAsync(int memberId)
   {
   var deliveries = await _context.BroadcastDeliveries
   .Include(d => d.LinkClicks)
     .Where(d => d.MemberId == memberId)
    .ToListAsync();

        var profile = await _context.MemberEngagementProfiles
  .FirstOrDefaultAsync(p => p.MemberId == memberId);

        if (profile == null)
      {
     profile = new MemberEngagementProfile { MemberId = memberId };
    _context.MemberEngagementProfiles.Add(profile);
      }

   profile.TotalEmailsReceived = deliveries.Count;
   profile.TotalEmailsOpened = deliveries.Count(d => d.EmailOpened);
    profile.TotalLinksClicked = deliveries.Sum(d => d.ClickCount);
  profile.LifetimeOpenRate = profile.TotalEmailsReceived > 0
   ? (double)profile.TotalEmailsOpened / profile.TotalEmailsReceived * 100
  : 0;
   profile.LifetimeClickRate = profile.TotalEmailsReceived > 0
          ? (double)profile.TotalLinksClicked / profile.TotalEmailsReceived * 100
    : 0;

  // Fix: handle empty collections for Max operations
  profile.LastEmailReceivedAt = deliveries.Any() ? deliveries.Max(d => (DateTime?)d.SentAt) : null;

  var openedDeliveries = deliveries.Where(d => d.EmailOpened).ToList();
  profile.LastEmailOpenedAt = openedDeliveries.Any() ? openedDeliveries.Max(d => d.FirstOpenedAt) : null;
  
  var clickedDeliveries = deliveries.Where(d => d.HasClicked).ToList();
  profile.LastLinkClickedAt = clickedDeliveries.Any() ? clickedDeliveries.Max(d => d.FirstClickedAt) : null;

  // Calculate engagement level
   profile.EngagementLevel = CalculateEngagementLevel(profile);
      profile.OverallEngagementScore = CalculateMemberEngagementScore(profile);

   // Best time to reach
   var openedWithTime = deliveries.Where(d => d.EmailOpened && d.FirstOpenedAt != null).ToList();
    if (openedWithTime.Any())
       {
    var hourCounts = openedWithTime.GroupBy(d => d.FirstOpenedAt!.Value.Hour)
  .OrderByDescending(g => g.Count())
      .FirstOrDefault();
         profile.PreferredHourOfDay = hourCounts?.Key;

   var dayCounts = openedWithTime.GroupBy(d => (int)d.FirstOpenedAt!.Value.DayOfWeek)
  .OrderByDescending(g => g.Count())
   .FirstOrDefault();
     profile.PreferredDayOfWeek = dayCounts?.Key;
       }

     profile.UpdatedAt = DateTime.UtcNow;

         await _context.SaveChangesAsync();
    }

        #endregion

        #region Helper Methods

        private double CalculateEngagementScore(int sent, int opens, int clicks)
        {
      if (sent == 0) return 0;

            var openRate = (double)opens / sent;
          var clickRate = (double)clicks / sent;

   // Weighted score: Opens = 60%, Clicks = 40%
   var score = (openRate * 0.6 + clickRate * 0.4) * 100;
       return Math.Min(100, Math.Round(score * 2, 2)); // Scale up but cap at 100
        }

   private string GetPerformanceLevel(double openRate)
        {
   return openRate switch
  {
    >= 40 => "Excellent",
 >= 25 => "Good",
           >= 15 => "Average",
                _ => "Poor"
     };
        }

   private string GetPerformanceRating(double openRate)
        {
     return openRate switch
            {
            >= 30 => "AboveAverage",
      >= 15 => "Average",
    _ => "BelowAverage"
    };
        }

        private string GetSegmentDescription(string segmentName)
        {
            return segmentName switch
 {
                "HighlyEngaged" => "Opens most emails and frequently clicks links",
     "Engaged" => "Regularly opens emails with occasional clicks",
       "Occasional" => "Occasionally opens emails",
    "Inactive" => "Has not engaged with emails recently",
 _ => "Unknown engagement pattern"
   };
        }

        private string CalculateEngagementLevel(MemberEngagementProfile profile)
        {
   if (profile.LifetimeOpenRate >= 60 && profile.LifetimeClickRate >= 10)
         return "HighlyEngaged";
            if (profile.LifetimeOpenRate >= 30 || profile.LifetimeClickRate >= 5)
             return "Engaged";
            if (profile.LifetimeOpenRate >= 10)
         return "Occasional";
 return "Inactive";
        }

 private double CalculateMemberEngagementScore(MemberEngagementProfile profile)
        {
   var recencyScore = 0.0;
  if (profile.LastEmailOpenedAt != null)
         {
     var daysSinceLastOpen = (DateTime.UtcNow - profile.LastEmailOpenedAt.Value).TotalDays;
        recencyScore = Math.Max(0, 100 - daysSinceLastOpen * 2);
       }

            var frequencyScore = Math.Min(100, profile.LifetimeOpenRate * 2);
            var clickScore = Math.Min(100, profile.LifetimeClickRate * 10);

            // Weighted average
            return Math.Round((recencyScore * 0.4 + frequencyScore * 0.4 + clickScore * 0.2), 2);
        }

        private TimeSpan? CalculateAverageTimeToOpen(List<BroadcastDelivery> deliveries, DateTime sentAt)
        {
            var openedDeliveries = deliveries.Where(d => d.EmailOpened && d.FirstOpenedAt != null).ToList();
            if (!openedDeliveries.Any()) return null;

            var avgTicks = openedDeliveries.Average(d => (d.FirstOpenedAt!.Value - sentAt).Ticks);
  return TimeSpan.FromTicks((long)avgTicks);
        }

    private string? ParseDeviceType(string? userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return null;

         userAgent = userAgent.ToLower();
      if (userAgent.Contains("mobile") || userAgent.Contains("android") || userAgent.Contains("iphone"))
   return "Mobile";
            if (userAgent.Contains("tablet") || userAgent.Contains("ipad"))
  return "Tablet";
        return "Desktop";
      }

        private string? ParseEmailClient(string? userAgent)
        {
       if (string.IsNullOrEmpty(userAgent)) return null;

 userAgent = userAgent.ToLower();
  if (userAgent.Contains("outlook")) return "Outlook";
       if (userAgent.Contains("gmail")) return "Gmail";
   if (userAgent.Contains("apple mail") || userAgent.Contains("webkit")) return "Apple Mail";
    if (userAgent.Contains("thunderbird")) return "Thunderbird";
            return "Other";
        }

        private MemberEngagementSummaryDTO MapToMemberEngagementSummary(MemberEngagementProfile profile)
{
      var topTopics = new List<string>();
            if (!string.IsNullOrEmpty(profile.TopEngagedTopicsJson))
            {
    try
     {
    topTopics = JsonSerializer.Deserialize<List<string>>(profile.TopEngagedTopicsJson) ?? new List<string>();
          }
    catch { }
 }

            return new MemberEngagementSummaryDTO
            {
      MemberId = profile.MemberId,
       ContactPerson = profile.Member?.ContactPerson ?? "",
Email = profile.Member?.Email ?? "",
  CompanyName = profile.Member?.CompanyName ?? "",
  EngagementLevel = profile.EngagementLevel,
          EngagementScore = profile.OverallEngagementScore,
  TotalEmailsReceived = profile.TotalEmailsReceived,
      TotalEmailsOpened = profile.TotalEmailsOpened,
           TotalLinksClicked = profile.TotalLinksClicked,
      OpenRate = Math.Round(profile.LifetimeOpenRate, 2),
       ClickRate = Math.Round(profile.LifetimeClickRate, 2),
       LastEmailOpenedAt = profile.LastEmailOpenedAt,
    TopEngagedTopics = topTopics,
         PreferredHourOfDay = profile.PreferredHourOfDay,
     PreferredDayOfWeek = profile.PreferredDayOfWeek != null
        ? ((DayOfWeek)profile.PreferredDayOfWeek).ToString()
          : null
          };
        }

     #endregion
    }
}
