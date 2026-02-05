namespace News_Back_end.DTOs
{
    #region Dashboard Overview DTOs
    
    /// <summary>
    /// Main dashboard overview with key metrics
    /// </summary>
    public class BroadcastAnalyticsDashboardDTO
{
public OverviewMetricsDTO Overview { get; set; } = new();
     public EngagementTrendsDTO Trends { get; set; } = new();
        public TopPerformingContentDTO TopContent { get; set; } = new();
    public AudienceInsightsDTO AudienceInsights { get; set; } = new();
        public List<RecentBroadcastPerformanceDTO> RecentBroadcasts { get; set; } = new();
    }

    public class OverviewMetricsDTO
    {
        // Time period for these metrics
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        
        // Volume metrics
     public int TotalBroadcastsSent { get; set; }
  public int TotalEmailsSent { get; set; }
     public int TotalEmailsDelivered { get; set; }
    
        // Engagement metrics
  public int TotalUniqueOpens { get; set; }
        public int TotalClicks { get; set; }
      public double AverageOpenRate { get; set; }
        public double AverageClickRate { get; set; }
     public double AverageClickToOpenRate { get; set; }
        
    // Comparison to previous period
        public double OpenRateChange { get; set; } // Percentage change
        public double ClickRateChange { get; set; }
   public double VolumeChange { get; set; }
    
        // Health metrics
        public double DeliveryRate { get; set; }
   public double BounceRate { get; set; }
      public double UnsubscribeRate { get; set; }
}

    #endregion

    #region Engagement Trends DTOs

    public class EngagementTrendsDTO
    {
        public List<DailyEngagementDTO> DailyMetrics { get; set; } = new();
        public BestTimeToSendDTO BestTimeToSend { get; set; } = new();
        public List<WeekdayPerformanceDTO> WeekdayPerformance { get; set; } = new();
    }

    public class DailyEngagementDTO
 {
        public DateTime Date { get; set; }
        public int EmailsSent { get; set; }
 public int Opens { get; set; }
        public int Clicks { get; set; }
        public double OpenRate { get; set; }
        public double ClickRate { get; set; }
    }

    public class BestTimeToSendDTO
    {
        public int BestHourOfDay { get; set; } // 0-23
        public int BestDayOfWeek { get; set; } // 0=Sunday, 6=Saturday
   public string BestDayName { get; set; } = string.Empty;
        public double OpenRateAtBestTime { get; set; }
        public List<HourlyEngagementDTO> HourlyBreakdown { get; set; } = new();
    }

    public class HourlyEngagementDTO
    {
        public int Hour { get; set; }
        public double AverageOpenRate { get; set; }
        public double AverageClickRate { get; set; }
        public int TotalOpens { get; set; }
    }

    public class WeekdayPerformanceDTO
    {
        public int DayOfWeek { get; set; }
        public string DayName { get; set; } = string.Empty;
      public int BroadcastsSent { get; set; }
        public double AverageOpenRate { get; set; }
        public double AverageClickRate { get; set; }
    }

    #endregion

  #region Topic Performance DTOs

    public class TopPerformingContentDTO
    {
        public List<TopicEngagementDTO> TopInterestTopics { get; set; } = new();
        public List<TopicEngagementDTO> TopIndustryTopics { get; set; } = new();
        public List<ArticlePerformanceDTO> TopArticles { get; set; } = new();
        public List<TopicTrendDTO> TrendingTopics { get; set; } = new();
    }

    public class TopicEngagementDTO
    {
 public int TagId { get; set; }
     public string TagNameEN { get; set; } = string.Empty;
     public string TagNameZH { get; set; } = string.Empty;
    public int TotalBroadcasts { get; set; }
        public int TotalRecipients { get; set; }
        public int TotalOpens { get; set; }
        public int TotalClicks { get; set; }
        public double OpenRate { get; set; }
        public double ClickRate { get; set; }
        public double EngagementScore { get; set; } // 0-100 composite score
        public string PerformanceLevel { get; set; } = string.Empty; // Excellent, Good, Average, Poor
    }

    public class ArticlePerformanceDTO
    {
        public int PublicationDraftId { get; set; }
     public string TitleEN { get; set; } = string.Empty;
        public string TitleZH { get; set; } = string.Empty;
        public int TimesIncludedInBroadcast { get; set; }
   public int TotalClicks { get; set; }
        public double ClickRate { get; set; }
   public List<string> Topics { get; set; } = new();
  }

    public class TopicTrendDTO
    {
        public int TagId { get; set; }
  public string TagName { get; set; } = string.Empty;
     public string TrendDirection { get; set; } = string.Empty; // Rising, Stable, Declining
        public double CurrentEngagementScore { get; set; }
        public double PreviousEngagementScore { get; set; }
     public double ChangePercentage { get; set; }
    }

    #endregion

    #region Audience Insights DTOs

 public class AudienceInsightsDTO
    {
        public int TotalSubscribers { get; set; }
        public int ActiveSubscribers { get; set; } // Engaged in last 30 days
        public int AtRiskSubscribers { get; set; } // No engagement in 60+ days
    public int InactiveSubscribers { get; set; } // No engagement in 90+ days

        public List<EngagementSegmentDTO> EngagementSegments { get; set; } = new();
     public List<DemographicEngagementDTO> ByCountry { get; set; } = new();
  public List<DemographicEngagementDTO> ByLanguage { get; set; } = new();
        public List<DemographicEngagementDTO> ByIndustry { get; set; } = new();
        public DeviceBreakdownDTO DeviceBreakdown { get; set; } = new();
    }

    public class EngagementSegmentDTO
    {
   public string SegmentName { get; set; } = string.Empty; // HighlyEngaged, Engaged, Occasional, Inactive
   public int MemberCount { get; set; }
        public double PercentageOfTotal { get; set; }
      public double AverageOpenRate { get; set; }
     public string Description { get; set; } = string.Empty;
    }

    public class DemographicEngagementDTO
    {
        public string Group { get; set; } = string.Empty;
public int MemberCount { get; set; }
   public int EmailsReceived { get; set; }
        public int EmailsOpened { get; set; }
    public double OpenRate { get; set; }
        public double ClickRate { get; set; }
    }

    public class DeviceBreakdownDTO
    {
        public int DesktopOpens { get; set; }
        public int MobileOpens { get; set; }
        public int TabletOpens { get; set; }
        public int UnknownOpens { get; set; }
        public double DesktopPercentage { get; set; }
        public double MobilePercentage { get; set; }
      public double TabletPercentage { get; set; }
    }

    #endregion

    #region Individual Broadcast Analytics DTOs

    public class RecentBroadcastPerformanceDTO
    {
     public int BroadcastId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public int TotalSent { get; set; }
        public int Delivered { get; set; }
        public int UniqueOpens { get; set; }
        public int Clicks { get; set; }
        public double OpenRate { get; set; }
        public double ClickRate { get; set; }
     public string PerformanceRating { get; set; } = string.Empty; // AboveAverage, Average, BelowAverage
    }

    public class DetailedBroadcastAnalyticsDTO
    {
   public int BroadcastId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string Status { get; set; } = string.Empty;
        
        // Delivery metrics
        public DeliveryMetricsDTO Delivery { get; set; } = new();
        
        // Engagement metrics
        public EngagementMetricsDTO Engagement { get; set; } = new();
        
    // Content performance
        public List<ArticleClickDTO> ArticleClicks { get; set; } = new();
      
        // Audience breakdown
        public BroadcastAudienceBreakdownDTO AudienceBreakdown { get; set; } = new();
        
        // Timeline
        public List<EngagementTimelineDTO> EngagementTimeline { get; set; } = new();
    }

    public class DeliveryMetricsDTO
    {
        public int TotalSent { get; set; }
        public int Delivered { get; set; }
        public int Bounced { get; set; }
   public int HardBounces { get; set; }
        public int SoftBounces { get; set; }
public double DeliveryRate { get; set; }
     public double BounceRate { get; set; }
    }

    public class EngagementMetricsDTO
    {
        public int UniqueOpens { get; set; }
        public int TotalOpens { get; set; }
        public int UniqueClicks { get; set; }
        public int TotalClicks { get; set; }
        public double OpenRate { get; set; }
        public double ClickRate { get; set; }
   public double ClickToOpenRate { get; set; }
        public int Unsubscribes { get; set; }
        public double UnsubscribeRate { get; set; }
        public TimeSpan? AverageTimeToOpen { get; set; }
    }

    public class ArticleClickDTO
    {
     public int PublicationDraftId { get; set; }
public string Title { get; set; } = string.Empty;
        public int ClickCount { get; set; }
        public int UniqueClicks { get; set; }
        public double ClickPercentage { get; set; } // % of total clicks
    }

    public class BroadcastAudienceBreakdownDTO
  {
     public List<DemographicEngagementDTO> ByCountry { get; set; } = new();
        public List<DemographicEngagementDTO> ByLanguage { get; set; } = new();
     public DeviceBreakdownDTO DeviceBreakdown { get; set; } = new();
 }

    public class EngagementTimelineDTO
  {
  public DateTime Timestamp { get; set; }
        public int CumulativeOpens { get; set; }
 public int CumulativeClicks { get; set; }
    }

    #endregion

    #region Member Engagement DTOs

    public class MemberEngagementSummaryDTO
    {
        public int MemberId { get; set; }
    public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
      
  public string EngagementLevel { get; set; } = string.Empty;
        public double EngagementScore { get; set; }
        
        public int TotalEmailsReceived { get; set; }
      public int TotalEmailsOpened { get; set; }
   public int TotalLinksClicked { get; set; }
        public double OpenRate { get; set; }
  public double ClickRate { get; set; }
        
        public DateTime? LastEmailOpenedAt { get; set; }
        public List<string> TopEngagedTopics { get; set; } = new();
        
        public int? PreferredHourOfDay { get; set; }
  public string? PreferredDayOfWeek { get; set; }
    }

public class MemberEngagementListDTO
    {
 public int TotalMembers { get; set; }
      public int HighlyEngaged { get; set; }
        public int Engaged { get; set; }
        public int Occasional { get; set; }
        public int Inactive { get; set; }
        public List<MemberEngagementSummaryDTO> Members { get; set; } = new();
    }

    #endregion

    #region Link Click Tracking DTOs

 public class LinkClickTrackingDTO
    {
        public int BroadcastId { get; set; }
        public int MemberId { get; set; }
     public string Url { get; set; } = string.Empty;
        public string? LinkIdentifier { get; set; }
        public int? ArticleId { get; set; }
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
    }

    public class LinkPerformanceDTO
    {
   public string Url { get; set; } = string.Empty;
   public string? LinkIdentifier { get; set; }
 public int TotalClicks { get; set; }
        public int UniqueClicks { get; set; }
 public double ClickPercentage { get; set; }
    }

 #endregion

    #region Report DTOs

    public class BroadcastReportRequestDTO
    {
        public DateTime FromDate { get; set; }
   public DateTime ToDate { get; set; }
        public bool IncludeTopicAnalysis { get; set; } = true;
        public bool IncludeAudienceInsights { get; set; } = true;
        public bool IncludeBroadcastDetails { get; set; } = true;
    }

    public class TopicRecommendationDTO
    {
        public string RecommendationType { get; set; } = string.Empty; // IncreaseFrequency, ReduceFrequency, Test, Avoid
    public int TagId { get; set; }
      public string TagName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
     public double CurrentEngagement { get; set; }
     public double ExpectedImpact { get; set; }
    }

    public class ContentRecommendationsDTO
    {
        public List<TopicRecommendationDTO> TopicsToFocus { get; set; } = new();
        public List<TopicRecommendationDTO> TopicsToAvoid { get; set; } = new();
        public BestTimeToSendDTO OptimalSendTime { get; set; } = new();
  public List<string> GeneralRecommendations { get; set; } = new();
    }

  #endregion
}
