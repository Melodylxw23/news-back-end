using Microsoft.EntityFrameworkCore;
using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;

namespace News_Back_end.Services
{
    /// <summary>
    /// Helper class to hold member interest counts
 /// </summary>
    public class MemberInterestCount
    {
        public int TagId { get; set; }
 public int MemberCount { get; set; }
    }

    public interface IPracticalAnalyticsService
    {
        Task<PracticalDashboardDTO> GetDashboardAsync(DateTime? fromDate = null, DateTime? toDate = null);
      Task<DeliveryHealthDTO> GetDeliveryHealthAsync(DateTime fromDate, DateTime toDate);
      Task<AudienceReachDTO> GetAudienceReachAsync(DateTime fromDate, DateTime toDate);
        Task<ContentDistributionDTO> GetContentDistributionAsync(DateTime fromDate, DateTime toDate);
        Task<MemberPreferencesDTO> GetMemberPreferencesAsync();
        Task<EngagementSignalsDTO> GetEngagementSignalsAsync(DateTime fromDate, DateTime toDate);
  Task<PracticalRecommendationsDTO> GetRecommendationsAsync();
        Task<List<DeliveryTrendDTO>> GetDeliveryTrendsAsync(DateTime fromDate, DateTime toDate);
    }

    public class PracticalAnalyticsService : IPracticalAnalyticsService
    {
        private readonly MyDBContext _context;
        private readonly ILogger<PracticalAnalyticsService> _logger;

        public PracticalAnalyticsService(MyDBContext context, ILogger<PracticalAnalyticsService> logger)
  {
    _context = context;
    _logger = logger;
}

        #region Dashboard

        public async Task<PracticalDashboardDTO> GetDashboardAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
 var to = toDate ?? DateTime.UtcNow;
       var from = fromDate ?? to.AddDays(-30);

         return new PracticalDashboardDTO
         {
 DeliveryHealth = await GetDeliveryHealthAsync(from, to),
       AudienceReach = await GetAudienceReachAsync(from, to),
                ContentDistribution = await GetContentDistributionAsync(from, to),
              MemberPreferences = await GetMemberPreferencesAsync(),
    EngagementSignals = await GetEngagementSignalsAsync(from, to),
    RecentBroadcasts = await GetRecentBroadcastsAsync(10)
 };
     }

        #endregion

        #region Delivery Health - RELIABLE METRICS

        public async Task<DeliveryHealthDTO> GetDeliveryHealthAsync(DateTime fromDate, DateTime toDate)
 {
            var deliveries = await _context.BroadcastDeliveries
       .Where(d => d.SentAt >= fromDate && d.SentAt <= toDate)
      .ToListAsync();

            var totalAttempted = deliveries.Count;
            var successful = deliveries.Count(d => d.DeliverySuccess);
            var failed = deliveries.Count(d => !d.DeliverySuccess);
          var hardBounces = deliveries.Count(d => d.BounceType == BounceType.Hard);
 var softBounces = deliveries.Count(d => d.BounceType == BounceType.Soft);

            // Get previous period for comparison
    var periodLength = (toDate - fromDate).TotalDays;
   var previousFrom = fromDate.AddDays(-periodLength);
            var previousTo = fromDate;

 var previousDeliveries = await _context.BroadcastDeliveries
       .Where(d => d.SentAt >= previousFrom && d.SentAt < previousTo)
            .ToListAsync();

            var prevSuccessful = previousDeliveries.Count(d => d.DeliverySuccess);
            var prevTotal = previousDeliveries.Count;

     var currentDeliveryRate = totalAttempted > 0 ? (double)successful / totalAttempted * 100 : 0;
          var prevDeliveryRate = prevTotal > 0 ? (double)prevSuccessful / prevTotal * 100 : 0;

   var broadcastCount = await _context.BroadcastMessages
              .CountAsync(b => b.Status == BroadcastStatus.Sent &&
        b.UpdatedAt >= fromDate && b.UpdatedAt <= toDate);

            var healthScore = CalculateDeliveryHealthScore(currentDeliveryRate, hardBounces, totalAttempted);
      var recommendations = GenerateDeliveryRecommendations(currentDeliveryRate, hardBounces, softBounces, totalAttempted);

            return new DeliveryHealthDTO
     {
  FromDate = fromDate,
       ToDate = toDate,
      TotalBroadcastsSent = broadcastCount,
          TotalEmailsAttempted = totalAttempted,
          SuccessfulDeliveries = successful,
    FailedDeliveries = failed,
          HardBounces = hardBounces,
        SoftBounces = softBounces,
         DeliveryRate = Math.Round(currentDeliveryRate, 2),
  BounceRate = totalAttempted > 0 ? Math.Round((double)(hardBounces + softBounces) / totalAttempted * 100, 2) : 0,
      HardBounceRate = totalAttempted > 0 ? Math.Round((double)hardBounces / totalAttempted * 100, 2) : 0,
   HealthScore = healthScore,
                HealthStatus = GetHealthStatus(healthScore),
    DeliveryRateChange = prevDeliveryRate > 0 ? Math.Round((currentDeliveryRate - prevDeliveryRate), 2) : 0,
    VolumeChange = prevTotal > 0 ? Math.Round((double)(totalAttempted - prevTotal) / prevTotal * 100, 2) : 0,
         Recommendations = recommendations
     };
        }

        public async Task<List<DeliveryTrendDTO>> GetDeliveryTrendsAsync(DateTime fromDate, DateTime toDate)
        {
     var deliveries = await _context.BroadcastDeliveries
                .Where(d => d.SentAt >= fromDate && d.SentAt <= toDate)
                .ToListAsync();

       return deliveries
     .GroupBy(d => d.SentAt.Date)
         .Select(g => new DeliveryTrendDTO
         {
        Date = g.Key,
           EmailsAttempted = g.Count(),
            EmailsDelivered = g.Count(d => d.DeliverySuccess),
         Bounces = g.Count(d => d.BounceType != BounceType.None),
      DeliveryRate = g.Count() > 0 ? Math.Round((double)g.Count(d => d.DeliverySuccess) / g.Count() * 100, 2) : 0
        })
      .OrderBy(t => t.Date)
     .ToList();
        }

        #endregion

    #region Audience Reach

        public async Task<AudienceReachDTO> GetAudienceReachAsync(DateTime fromDate, DateTime toDate)
        {
            var activeMembers = await _context.Members
           .Where(m => m.PreferredChannel == Channels.Email || m.PreferredChannel == Channels.Both)
                .Where(m => !string.IsNullOrEmpty(m.Email))
    .Include(m => m.IndustryTags)
         .Include(m => m.Interests)
            .ToListAsync();

     var deliveries = await _context.BroadcastDeliveries
        .Where(d => d.SentAt >= fromDate && d.SentAt <= toDate && d.DeliverySuccess)
     .Include(d => d.Member)
       .ToListAsync();

            var membersReached = deliveries.Select(d => d.MemberId).Distinct().Count();

// By Country
            var byCountry = activeMembers
       .GroupBy(m => m.Country.ToString())
           .Select(g =>
    {
      var memberIds = g.Select(m => m.MemberId).ToHashSet();
      var reached = deliveries.Where(d => memberIds.Contains(d.MemberId)).Select(d => d.MemberId).Distinct().Count();
            var emailsDelivered = deliveries.Count(d => memberIds.Contains(d.MemberId));
        return new SegmentReachDTO
         {
     SegmentName = g.Key,
        TotalMembers = g.Count(),
     MembersReached = reached,
                EmailsDelivered = emailsDelivered,
                   ReachPercentage = g.Count() > 0 ? Math.Round((double)reached / g.Count() * 100, 2) : 0,
AverageEmailsPerMember = reached > 0 ? Math.Round((double)emailsDelivered / reached, 2) : 0
      };
              })
              .ToList();

       // By Language
     var byLanguage = activeMembers
   .GroupBy(m => m.PreferredLanguage.ToString())
                .Select(g =>
{
              var memberIds = g.Select(m => m.MemberId).ToHashSet();
      var reached = deliveries.Where(d => memberIds.Contains(d.MemberId)).Select(d => d.MemberId).Distinct().Count();
      var emailsDelivered = deliveries.Count(d => memberIds.Contains(d.MemberId));
   return new SegmentReachDTO
  {
          SegmentName = g.Key,
            TotalMembers = g.Count(),
 MembersReached = reached,
             EmailsDelivered = emailsDelivered,
 ReachPercentage = g.Count() > 0 ? Math.Round((double)reached / g.Count() * 100, 2) : 0,
                 AverageEmailsPerMember = reached > 0 ? Math.Round((double)emailsDelivered / reached, 2) : 0
       };
 })
    .ToList();

         // By Membership Type
            var byType = activeMembers
       .GroupBy(m => m.MembershipType.ToString())
       .Select(g =>
   {
        var memberIds = g.Select(m => m.MemberId).ToHashSet();
           var reached = deliveries.Where(d => memberIds.Contains(d.MemberId)).Select(d => d.MemberId).Distinct().Count();
      var emailsDelivered = deliveries.Count(d => memberIds.Contains(d.MemberId));
       return new SegmentReachDTO
  {
      SegmentName = g.Key,
      TotalMembers = g.Count(),
   MembersReached = reached,
        EmailsDelivered = emailsDelivered,
   ReachPercentage = g.Count() > 0 ? Math.Round((double)reached / g.Count() * 100, 2) : 0,
        AverageEmailsPerMember = reached > 0 ? Math.Round((double)emailsDelivered / reached, 2) : 0
        };
          })
      .ToList();

          var coverageGaps = FindCoverageGaps(activeMembers, deliveries);

 return new AudienceReachDTO
        {
    TotalActiveMembers = activeMembers.Count,
     MembersReachedThisPeriod = membersReached,
       ReachPercentage = activeMembers.Count > 0 ? Math.Round((double)membersReached / activeMembers.Count * 100, 2) : 0,
    ByCountry = byCountry,
          ByLanguage = byLanguage,
     ByMembershipType = byType,
    CoverageGaps = coverageGaps
    };
   }

        #endregion

   #region Content Distribution

        public async Task<ContentDistributionDTO> GetContentDistributionAsync(DateTime fromDate, DateTime toDate)
        {
            var broadcasts = await _context.BroadcastMessages
                .Where(b => b.Status == BroadcastStatus.Sent && b.UpdatedAt >= fromDate && b.UpdatedAt <= toDate)
    .Include(b => b.SelectedArticles)
             .ThenInclude(a => a.InterestTags)
       .Include(b => b.SelectedArticles)
    .ThenInclude(a => a.IndustryTag)
    .ToListAsync();

       var memberInterests = await _context.Members
      .Where(m => m.PreferredChannel == Channels.Email || m.PreferredChannel == Channels.Both)
    .SelectMany(m => m.Interests)
                .GroupBy(i => i.InterestTagId)
         .Select(g => new MemberInterestCount { TagId = g.Key, MemberCount = g.Count() })
     .ToListAsync();

        var memberIndustries = await _context.Members
          .Where(m => m.PreferredChannel == Channels.Email || m.PreferredChannel == Channels.Both)
                .SelectMany(m => m.IndustryTags)
     .GroupBy(i => i.IndustryTagId)
       .Select(g => new MemberInterestCount { TagId = g.Key, MemberCount = g.Count() })
                .ToListAsync();

  var totalActiveMembers = await _context.Members
       .CountAsync(m => m.PreferredChannel == Channels.Email || m.PreferredChannel == Channels.Both);

            var interestTopicDist = new Dictionary<int, (InterestTag Tag, int TimesSent, int Recipients)>();
   var industryTopicDist = new Dictionary<int, (IndustryTag Tag, int TimesSent, int Recipients)>();

     foreach (var broadcast in broadcasts)
  {
          var recipientCount = await _context.BroadcastDeliveries
          .CountAsync(d => d.BroadcastMessageId == broadcast.Id && d.DeliverySuccess);

        foreach (var article in broadcast.SelectedArticles)
    {
              foreach (var tag in article.InterestTags)
          {
                  if (!interestTopicDist.ContainsKey(tag.InterestTagId))
           interestTopicDist[tag.InterestTagId] = (tag, 0, 0);

      var current = interestTopicDist[tag.InterestTagId];
        interestTopicDist[tag.InterestTagId] = (current.Tag, current.TimesSent + 1, current.Recipients + recipientCount);
  }

               if (article.IndustryTag != null)
        {
          if (!industryTopicDist.ContainsKey(article.IndustryTag.IndustryTagId))
  industryTopicDist[article.IndustryTag.IndustryTagId] = (article.IndustryTag, 0, 0);

     var current = industryTopicDist[article.IndustryTag.IndustryTagId];
                industryTopicDist[article.IndustryTag.IndustryTagId] = (current.Tag, current.TimesSent + 1, current.Recipients + recipientCount);
       }
   }
            }

       var totalBroadcasts = broadcasts.Count;

   var topInterestTopics = interestTopicDist.Values
        .Select(t =>
    {
        var memberDemand = memberInterests.FirstOrDefault(m => m.TagId == t.Tag.InterestTagId)?.MemberCount ?? 0;
 return new TopicDistributionDTO
      {
     TagId = t.Tag.InterestTagId,
        TagNameEN = t.Tag.NameEN,
TagNameZH = t.Tag.NameZH,
          TimesSent = t.TimesSent,
         TotalRecipients = t.Recipients,
   PercentageOfBroadcasts = totalBroadcasts > 0 ? Math.Round((double)t.TimesSent / totalBroadcasts * 100, 2) : 0,
    MembersInterestedIn = memberDemand,
           DemandScore = totalActiveMembers > 0 ? Math.Round((double)memberDemand / totalActiveMembers * 100, 2) : 0,
         SupplyDemandRatio = memberDemand > 0 ? Math.Round((double)t.TimesSent / memberDemand, 2) : 0
            };
        })
        .OrderByDescending(t => t.TimesSent)
   .Take(10)
 .ToList();

        var topIndustryTopics = industryTopicDist.Values
  .Select(t =>
     {
      var memberDemand = memberIndustries.FirstOrDefault(m => m.TagId == t.Tag.IndustryTagId)?.MemberCount ?? 0;
           return new TopicDistributionDTO
   {
      TagId = t.Tag.IndustryTagId,
          TagNameEN = t.Tag.NameEN,
  TagNameZH = t.Tag.NameZH,
       TimesSent = t.TimesSent,
      TotalRecipients = t.Recipients,
         PercentageOfBroadcasts = totalBroadcasts > 0 ? Math.Round((double)t.TimesSent / totalBroadcasts * 100, 2) : 0,
MembersInterestedIn = memberDemand,
         DemandScore = totalActiveMembers > 0 ? Math.Round((double)memberDemand / totalActiveMembers * 100, 2) : 0,
  SupplyDemandRatio = memberDemand > 0 ? Math.Round((double)t.TimesSent / memberDemand, 2) : 0
            };
                })
    .OrderByDescending(t => t.TimesSent)
     .Take(10)
           .ToList();

     var contentGaps = FindContentGaps(memberInterests, interestTopicDist, totalActiveMembers);
   var matchScore = CalculatePreferenceMatchScore(memberInterests, interestTopicDist, totalActiveMembers);

        return new ContentDistributionDTO
          {
                TotalArticlesSent = broadcasts.Sum(b => b.SelectedArticles.Count),
            UniqueArticlesSent = broadcasts.SelectMany(b => b.SelectedArticles).Select(a => a.PublicationDraftId).Distinct().Count(),
     TopInterestTopics = topInterestTopics,
      TopIndustryTopics = topIndustryTopics,
            PreferenceMatchScore = matchScore,
 ContentGaps = contentGaps.Where(g => g.GapType == "Underserved").ToList(),
    OverservedTopics = contentGaps.Where(g => g.GapType == "Overserved").ToList()
   };
        }

        #endregion

        #region Member Preferences

        public async Task<MemberPreferencesDTO> GetMemberPreferencesAsync()
  {
    var members = await _context.Members
    .Include(m => m.IndustryTags)
             .Include(m => m.Interests)
 .ToListAsync();

            var totalMembers = members.Count;
     var membersWithPreferences = members.Count(m => m.Interests.Any() || m.IndustryTags.Any());

         var topInterests = members
     .SelectMany(m => m.Interests)
           .GroupBy(i => new { i.InterestTagId, i.NameEN, i.NameZH })
    .Select(g => new PreferenceRankingDTO
      {
             TagId = g.Key.InterestTagId,
          TagNameEN = g.Key.NameEN,
         TagNameZH = g.Key.NameZH,
   MemberCount = g.Count(),
                PercentageOfMembers = totalMembers > 0 ? Math.Round((double)g.Count() / totalMembers * 100, 2) : 0
    })
      .OrderByDescending(p => p.MemberCount)
      .Take(10)
       .ToList();

            var topIndustries = members
           .SelectMany(m => m.IndustryTags)
  .GroupBy(i => new { i.IndustryTagId, i.NameEN, i.NameZH })
       .Select(g => new PreferenceRankingDTO
      {
  TagId = g.Key.IndustryTagId,
 TagNameEN = g.Key.NameEN,
       TagNameZH = g.Key.NameZH,
                MemberCount = g.Count(),
     PercentageOfMembers = totalMembers > 0 ? Math.Round((double)g.Count() / totalMembers * 100, 2) : 0
   })
          .OrderByDescending(p => p.MemberCount)
           .Take(10)
          .ToList();

            var langGroups = members.GroupBy(m => m.PreferredLanguage).ToList();
            var enOnly = langGroups.FirstOrDefault(g => g.Key == Language.EN)?.Count() ?? 0;
            var zhOnly = langGroups.FirstOrDefault(g => g.Key == Language.ZH)?.Count() ?? 0;
      var both = langGroups.FirstOrDefault(g => g.Key == Language.Both)?.Count() ?? 0;

   var emailOnly = members.Count(m => m.PreferredChannel == Channels.Email);
    var bothChannels = members.Count(m => m.PreferredChannel == Channels.Both);

            return new MemberPreferencesDTO
         {
TotalMembers = totalMembers,
        MembersWithPreferences = membersWithPreferences,
           TopInterests = topInterests,
       TopIndustries = topIndustries,
      LanguageBreakdown = new LanguagePreferenceDTO
           {
       EnglishOnly = enOnly,
      ChineseOnly = zhOnly,
        Both = both,
                EnglishPercentage = totalMembers > 0 ? Math.Round((double)enOnly / totalMembers * 100, 2) : 0,
      ChinesePercentage = totalMembers > 0 ? Math.Round((double)zhOnly / totalMembers * 100, 2) : 0,
 BothPercentage = totalMembers > 0 ? Math.Round((double)both / totalMembers * 100, 2) : 0
            },
     EmailOnlyMembers = emailOnly,
       BothChannelsMembers = bothChannels
      };
 }

        #endregion

        #region Engagement Signals

        public async Task<EngagementSignalsDTO> GetEngagementSignalsAsync(DateTime fromDate, DateTime toDate)
        {
            var unsubscribes = await _context.BroadcastDeliveries
       .Where(d => d.Unsubscribed && d.UnsubscribedAt >= fromDate && d.UnsubscribedAt <= toDate)
.Include(d => d.BroadcastMessage)
         .ThenInclude(b => b.SelectedArticles)
  .ThenInclude(a => a.InterestTags)
     .ToListAsync();

            var totalDelivered = await _context.BroadcastDeliveries
      .CountAsync(d => d.SentAt >= fromDate && d.SentAt <= toDate && d.DeliverySuccess);

          var newMembers = await _context.Members
         .CountAsync(m => m.CreatedAt >= fromDate && m.CreatedAt <= toDate);

            var activeListSize = await _context.Members
       .CountAsync(m => m.PreferredChannel == Channels.Email || m.PreferredChannel == Channels.Both);

  var periodLength = (toDate - fromDate).TotalDays;
    var previousFrom = fromDate.AddDays(-periodLength);
     var previousNewMembers = await _context.Members
    .CountAsync(m => m.CreatedAt >= previousFrom && m.CreatedAt < fromDate);

   var unsubsByBroadcast = unsubscribes
    .GroupBy(d => d.BroadcastMessageId)
        .Select(g => new UnsubscribeInsightDTO
     {
    BroadcastId = g.Key,
BroadcastTitle = g.First().BroadcastMessage?.Title ?? "Unknown",
    UnsubscribeCount = g.Count(),
            TopicsIncluded = g.First().BroadcastMessage?.SelectedArticles
 .SelectMany(a => a.InterestTags)
       .Select(t => t.NameEN)
     .Distinct()
          .Take(5)
    .ToList() ?? new List<string>()
       })
    .OrderByDescending(u => u.UnsubscribeCount)
   .Take(5)
      .ToList();

  var unsubscribeRate = totalDelivered > 0 ? Math.Round((double)unsubscribes.Count / totalDelivered * 100, 4) : 0;
    var churnRate = activeListSize > 0 ? Math.Round((double)unsubscribes.Count / activeListSize * 100, 4) : 0;
 var growthRate = previousNewMembers > 0
  ? Math.Round((double)(newMembers - previousNewMembers) / previousNewMembers * 100, 2)
    : (newMembers > 0 ? 100 : 0);

          var recommendations = GenerateEngagementRecommendations(unsubscribeRate, churnRate, newMembers, activeListSize);

    return new EngagementSignalsDTO
            {
    FromDate = fromDate,
       ToDate = toDate,
     Unsubscribes = unsubscribes.Count,
    UnsubscribeRate = unsubscribeRate,
         UnsubscribesByTopic = unsubsByBroadcast,
     NewMembers = newMembers,
  ActiveListSize = activeListSize,
     ListGrowthRate = growthRate,
 ChurnRate = churnRate,
          ListHealthStatus = GetListHealthStatus(churnRate, growthRate),
            Recommendations = recommendations
            };
        }

        #endregion

        #region Recommendations

        public async Task<PracticalRecommendationsDTO> GetRecommendationsAsync()
     {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
       var now = DateTime.UtcNow;

   var contentDist = await GetContentDistributionAsync(thirtyDaysAgo, now);
        var memberPrefs = await GetMemberPreferencesAsync();
        var engagement = await GetEngagementSignalsAsync(thirtyDaysAgo, now);
    var delivery = await GetDeliveryHealthAsync(thirtyDaysAgo, now);

         var recommendations = new PracticalRecommendationsDTO();

            // Recommend topics based on member preferences vs. what we're sending
            recommendations.RecommendedTopics = memberPrefs.TopInterests
 .Where(p => !contentDist.TopInterestTopics.Any(c => c.TagId == p.TagId) ||
           contentDist.TopInterestTopics.First(c => c.TagId == p.TagId).SupplyDemandRatio < 0.5)
        .Take(5)
          .Select(p => new TopicRecommendationDTO
       {
     RecommendationType = "IncreaseFrequency",
     TagId = p.TagId,
  TagName = p.TagNameEN,
      Reason = $"{p.MemberCount} members are interested in this topic but it's underrepresented in broadcasts",
   CurrentEngagement = 0,
          ExpectedImpact = p.PercentageOfMembers
             })
          .ToList();

       // Topics that correlate with unsubscribes
if (engagement.UnsubscribesByTopic.Any())
    {
       var topicsWithUnsubscribes = engagement.UnsubscribesByTopic
 .SelectMany(u => u.TopicsIncluded)
  .GroupBy(t => t)
       .OrderByDescending(g => g.Count())
        .Take(3)
               .Select(g => new TopicRecommendationDTO
     {
      RecommendationType = "Review",
   TagName = g.Key,
           Reason = $"This topic appears in broadcasts that generated unsubscribes",
  CurrentEngagement = 0,
   ExpectedImpact = -10
 })
    .ToList();

           recommendations.TopicsToReconsider = topicsWithUnsubscribes;
   }

// Audience recommendations
            var audienceRecs = new List<AudienceRecommendationDTO>();

       if (contentDist.ContentGaps.Any())
 {
                foreach (var gap in contentDist.ContentGaps.Take(3))
      {
    audienceRecs.Add(new AudienceRecommendationDTO
            {
      SegmentName = gap.TagName,
              RecommendationType = "Create Targeted Content",
        Reason = $"{gap.MembersDemanding} members interested but only sent {gap.TimesSent} times",
   AffectedMembers = gap.MembersDemanding
          });
        }
            }
      recommendations.AudienceRecommendations = audienceRecs;

            // List health recommendations
      recommendations.ListHealthRecommendations = delivery.Recommendations;

            // General recommendations
        var general = new List<string>();

     if (memberPrefs.MembersWithPreferences < memberPrefs.TotalMembers * 0.5)
   {
          general.Add($"Only {Math.Round((double)memberPrefs.MembersWithPreferences / memberPrefs.TotalMembers * 100)}% of members have set preferences. Consider encouraging members to update their interests.");
       }

       if (delivery.HardBounceRate > 2)
    {
     general.Add($"Hard bounce rate is {delivery.HardBounceRate}%. Consider cleaning your email list to remove invalid addresses.");
  }

    if (engagement.ChurnRate > 1)
   {
                general.Add("Churn rate is elevated. Review recent broadcast content and frequency.");
  }

      recommendations.GeneralRecommendations = general;

          return recommendations;
        }

        #endregion

        #region Helper Methods

        private async Task<List<RecentBroadcastSummaryDTO>> GetRecentBroadcastsAsync(int count)
    {
      var broadcasts = await _context.BroadcastMessages
    .Where(b => b.Status == BroadcastStatus.Sent)
 .OrderByDescending(b => b.UpdatedAt)
      .Take(count)
  .Include(b => b.SelectedArticles)
              .ThenInclude(a => a.InterestTags)
                .ToListAsync();

        var result = new List<RecentBroadcastSummaryDTO>();

       foreach (var broadcast in broadcasts)
      {
      var deliveries = await _context.BroadcastDeliveries
           .Where(d => d.BroadcastMessageId == broadcast.Id)
            .ToListAsync();

       var totalSent = deliveries.Count;
  var delivered = deliveries.Count(d => d.DeliverySuccess);
    var bounced = deliveries.Count(d => d.BounceType != BounceType.None);
           var unsubscribes = deliveries.Count(d => d.Unsubscribed);

        var topics = broadcast.SelectedArticles
.SelectMany(a => a.InterestTags)
            .Select(t => t.NameEN)
  .Distinct()
         .Take(5)
        .ToList();

     result.Add(new RecentBroadcastSummaryDTO
        {
           BroadcastId = broadcast.Id,
     Title = broadcast.Title,
      SentAt = deliveries.Any() ? deliveries.Min(d => d.SentAt) : broadcast.UpdatedAt.DateTime,
     TotalSent = totalSent,
         Delivered = delivered,
       Bounced = bounced,
              DeliveryRate = totalSent > 0 ? Math.Round((double)delivered / totalSent * 100, 2) : 0,
    ArticleCount = broadcast.SelectedArticles.Count,
     TopicsIncluded = topics,
             Unsubscribes = unsubscribes,
         DeliveryStatus = GetDeliveryStatus(totalSent > 0 ? (double)delivered / totalSent * 100 : 0)
 });
          }

         return result;
        }

        private double CalculateDeliveryHealthScore(double deliveryRate, int hardBounces, int totalAttempted)
   {
      var score = deliveryRate;

if (totalAttempted > 0)
            {
          var hardBounceRate = (double)hardBounces / totalAttempted * 100;
     score -= hardBounceRate * 2;
  }

        return Math.Max(0, Math.Min(100, Math.Round(score, 2)));
        }

    private string GetHealthStatus(double healthScore)
        {
        return healthScore switch
            {
        >= 95 => "Excellent",
             >= 85 => "Good",
             >= 70 => "Needs Attention",
      _ => "Critical"
        };
        }

        private string GetDeliveryStatus(double deliveryRate)
        {
          return deliveryRate switch
            {
     >= 95 => "Excellent",
         >= 85 => "Good",
                _ => "Issues"
            };
        }

        private string GetListHealthStatus(double churnRate, double growthRate)
        {
     if (growthRate > churnRate * 2) return "Growing";
     if (growthRate > churnRate) return "Stable";
         if (churnRate > 2) return "Declining";
          return "Stable";
  }

      private List<string> GenerateDeliveryRecommendations(double deliveryRate, int hardBounces, int softBounces, int total)
        {
            var recs = new List<string>();

   if (total == 0)
      {
        recs.Add("No emails sent in this period. Consider scheduling regular broadcasts.");
         return recs;
  }

         if (deliveryRate < 95)
    {
    recs.Add($"Delivery rate is {deliveryRate}%. Industry standard is 95%+. Review email content and sender reputation.");
            }

   if (hardBounces > 0 && (double)hardBounces / total > 0.02)
            {
                recs.Add($"{hardBounces} hard bounces detected. Remove these invalid email addresses from your list.");
    }

   if (softBounces > 0 && (double)softBounces / total > 0.05)
  {
            recs.Add($"{softBounces} soft bounces detected. Consider retry logic or check for temporary issues.");
}

  if (deliveryRate >= 98)
            {
  recs.Add("Excellent delivery rate! Your email list health is very good.");
            }

            return recs;
        }

        private List<string> GenerateEngagementRecommendations(double unsubRate, double churnRate, int newMembers, int activeSize)
   {
        var recs = new List<string>();

  if (unsubRate > 0.5)
       {
       recs.Add($"Unsubscribe rate ({unsubRate}%) is above average. Review content relevance and send frequency.");
            }

     if (churnRate > 2)
      {
   recs.Add("Significant list churn detected. Consider a re-engagement campaign for inactive members.");
            }

      if (newMembers == 0 && activeSize > 0)
     {
 recs.Add("No new members in this period. Consider list growth strategies.");
            }

    if (unsubRate < 0.1 && newMembers > 0)
     {
     recs.Add("Low unsubscribe rate and new member growth - your list is healthy!");
       }

return recs;
        }

        private List<CoverageGapDTO> FindCoverageGaps(List<Member> activeMembers, List<BroadcastDelivery> deliveries)
   {
            var gaps = new List<CoverageGapDTO>();
            var memberIdsReached = deliveries.Select(d => d.MemberId).ToHashSet();

            var industryGroups = activeMembers
             .SelectMany(m => m.IndustryTags.Select(t => new { Member = m, Tag = t }))
       .GroupBy(x => x.Tag.NameEN)
       .Where(g => g.Count() >= 5);

       foreach (var group in industryGroups)
            {
  var membersInGroup = group.Select(x => x.Member.MemberId).Distinct().ToList();
     var reached = membersInGroup.Count(id => memberIdsReached.Contains(id));
    var reachRate = (double)reached / membersInGroup.Count * 100;

    if (reachRate < 50)
       {
             gaps.Add(new CoverageGapDTO
                  {
    SegmentType = "Industry",
      SegmentName = group.Key,
    MemberCount = membersInGroup.Count,
        EmailsReceived = deliveries.Count(d => membersInGroup.Contains(d.MemberId)),
        Recommendation = $"Only {Math.Round(reachRate)}% of {group.Key} members received emails. Create targeted content for this industry."
   });
                }
}

   return gaps.OrderByDescending(g => g.MemberCount).Take(5).ToList();
        }

        private List<ContentGapDTO> FindContentGaps(
     List<MemberInterestCount> memberInterests,
     Dictionary<int, (InterestTag Tag, int TimesSent, int Recipients)> contentDist,
            int totalMembers)
{
            var gaps = new List<ContentGapDTO>();

            foreach (var interest in memberInterests)
      {
       var contentSent = contentDist.GetValueOrDefault(interest.TagId);
         var timesSent = contentSent.TimesSent;
       var tag = contentSent.Tag;

     if (interest.MemberCount >= totalMembers * 0.1 && timesSent < 2)
   {
     gaps.Add(new ContentGapDTO
     {
 TagId = interest.TagId,
      TagName = tag?.NameEN ?? $"Topic {interest.TagId}",
 GapType = "Underserved",
             MembersDemanding = interest.MemberCount,
          TimesSent = timesSent,
           Recommendation = $"{interest.MemberCount} members interested but only sent {timesSent} times"
               });
    }
     }

     foreach (var content in contentDist)
            {
                var demand = memberInterests.FirstOrDefault(m => m.TagId == content.Key)?.MemberCount ?? 0;
       if (content.Value.TimesSent > 5 && demand < totalMembers * 0.05)
                {
        gaps.Add(new ContentGapDTO
                    {
             TagId = content.Key,
    TagName = content.Value.Tag.NameEN,
              GapType = "Overserved",
 MembersDemanding = demand,
         TimesSent = content.Value.TimesSent,
          Recommendation = $"Sent {content.Value.TimesSent} times but only {demand} members interested"
             });
         }
          }

          return gaps.OrderByDescending(g => g.MembersDemanding).Take(10).ToList();
 }

        private double CalculatePreferenceMatchScore(
    List<MemberInterestCount> memberInterests,
  Dictionary<int, (InterestTag Tag, int TimesSent, int Recipients)> contentDist,
        int totalMembers)
        {
   if (totalMembers == 0 || !contentDist.Any()) return 0;

     var topicsSent = contentDist.Keys.ToHashSet();
     var matchedInterests = memberInterests.Where(m => topicsSent.Contains(m.TagId)).Sum(m => m.MemberCount);
   var totalInterests = memberInterests.Sum(m => m.MemberCount);

            if (totalInterests == 0) return 50.0;

          var score = (double)matchedInterests / totalInterests * 100;
 return Math.Round(Math.Min(100, score), 2);
        }

  #endregion
    }
}
