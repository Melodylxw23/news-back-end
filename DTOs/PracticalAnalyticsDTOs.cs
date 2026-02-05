namespace News_Back_end.DTOs
{
    /// <summary>
    /// Practical analytics DTOs that focus on measurable data
    /// without relying on unreliable email open/click tracking
    /// </summary>
    
    #region Dashboard Overview
 
    /// <summary>
    /// Main dashboard focusing on delivery health and audience reach
    /// </summary>
    public class PracticalDashboardDTO
    {
        public DeliveryHealthDTO DeliveryHealth { get; set; } = new();
        public AudienceReachDTO AudienceReach { get; set; } = new();
        public ContentDistributionDTO ContentDistribution { get; set; } = new();
        public MemberPreferencesDTO MemberPreferences { get; set; } = new();
      public EngagementSignalsDTO EngagementSignals { get; set; } = new();
     public List<RecentBroadcastSummaryDTO> RecentBroadcasts { get; set; } = new();
    }
    
    #endregion

    #region Delivery Health - What We CAN Reliably Measure
    
    /// <summary>
    /// Email delivery metrics - 100% reliable from email server responses
    /// </summary>
    public class DeliveryHealthDTO
 {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
   
        // Volume
        public int TotalBroadcastsSent { get; set; }
        public int TotalEmailsAttempted { get; set; }
  
        // Delivery outcomes - RELIABLE metrics
        public int SuccessfulDeliveries { get; set; }
        public int FailedDeliveries { get; set; }
        public int HardBounces { get; set; } // Invalid emails - remove from list
        public int SoftBounces { get; set; } // Temporary failures - retry
        
     // Rates
        public double DeliveryRate { get; set; } // Success / Attempted
public double BounceRate { get; set; }
        public double HardBounceRate { get; set; }
        
        // Health score (0-100) based on delivery success
        public double HealthScore { get; set; }
        public string HealthStatus { get; set; } = string.Empty; // Excellent, Good, Needs Attention, Critical
        
        // Comparison to previous period
   public double DeliveryRateChange { get; set; }
        public double VolumeChange { get; set; }
        
// Actionable insights
  public List<string> Recommendations { get; set; } = new();
  }
    
    /// <summary>
    /// Trend data for delivery health over time
    /// </summary>
    public class DeliveryTrendDTO
    {
      public DateTime Date { get; set; }
        public int EmailsAttempted { get; set; }
        public int EmailsDelivered { get; set; }
    public int Bounces { get; set; }
        public double DeliveryRate { get; set; }
    }
    
    #endregion

    #region Audience Reach - Who Are We Actually Reaching?
    
    /// <summary>
    /// Audience reach metrics based on successful deliveries
    /// </summary>
    public class AudienceReachDTO
    {
        public int TotalActiveMembers { get; set; } // Members who receive emails
        public int MembersReachedThisPeriod { get; set; } // Unique members who received at least one email
        public double ReachPercentage { get; set; } // % of active members reached
        
     // Breakdown by segment
     public List<SegmentReachDTO> ByCountry { get; set; } = new();
        public List<SegmentReachDTO> ByLanguage { get; set; } = new();
        public List<SegmentReachDTO> ByMembershipType { get; set; } = new();
        public List<SegmentReachDTO> ByIndustry { get; set; } = new();
    
        // Coverage gaps - segments not being reached
        public List<CoverageGapDTO> CoverageGaps { get; set; } = new();
    }
    
    public class SegmentReachDTO
    {
        public string SegmentName { get; set; } = string.Empty;
   public int TotalMembers { get; set; }
        public int MembersReached { get; set; }
      public int EmailsDelivered { get; set; }
        public double ReachPercentage { get; set; }
        public double AverageEmailsPerMember { get; set; }
    }
    
    public class CoverageGapDTO
    {
        public string SegmentType { get; set; } = string.Empty; // Industry, Interest, Country
        public string SegmentName { get; set; } = string.Empty;
      public int MemberCount { get; set; }
     public int EmailsReceived { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }
    
  #endregion

    #region Content Distribution - What Are We Sending?
    
    /// <summary>
    /// Analysis of what content is being distributed
    /// </summary>
    public class ContentDistributionDTO
    {
   public int TotalArticlesSent { get; set; }
        public int UniqueArticlesSent { get; set; }
        
      // Which topics are we sending most?
        public List<TopicDistributionDTO> TopInterestTopics { get; set; } = new();
 public List<TopicDistributionDTO> TopIndustryTopics { get; set; } = new();
        
   // Are we matching member preferences?
        public double PreferenceMatchScore { get; set; } // 0-100: How well content matches audience interests
        public List<ContentGapDTO> ContentGaps { get; set; } = new(); // Topics members want but we're not sending
        public List<ContentGapDTO> OverservedTopics { get; set; } = new(); // Topics we send a lot but few members want
    }
    
    public class TopicDistributionDTO
    {
        public int TagId { get; set; }
        public string TagNameEN { get; set; } = string.Empty;
        public string TagNameZH { get; set; } = string.Empty;
     
        // Send metrics
        public int TimesSent { get; set; } // How many broadcasts included this topic
        public int TotalRecipients { get; set; } // Total members who received this topic
        public double PercentageOfBroadcasts { get; set; }
        
        // Demand metrics (from member preferences)
        public int MembersInterestedIn { get; set; }
        public double DemandScore { get; set; } // Members interested / Total members
        
   // Match score
        public double SupplyDemandRatio { get; set; } // How well we're serving this topic vs demand
  }
    
    public class ContentGapDTO
    {
        public int TagId { get; set; }
        public string TagName { get; set; } = string.Empty;
     public string GapType { get; set; } = string.Empty; // "Underserved" or "Overserved"
        public int MembersDemanding { get; set; }
        public int TimesSent { get; set; }
     public string Recommendation { get; set; } = string.Empty;
    }
    
    #endregion

#region Member Preferences - What Do Members Want?
    
    /// <summary>
    /// Analysis of member preferences for content recommendations
    /// </summary>
    public class MemberPreferencesDTO
    {
     public int TotalMembers { get; set; }
public int MembersWithPreferences { get; set; } // Members who have set interests
        
 // Most popular interests
        public List<PreferenceRankingDTO> TopInterests { get; set; } = new();
        public List<PreferenceRankingDTO> TopIndustries { get; set; } = new();
        
        // Language preferences
     public LanguagePreferenceDTO LanguageBreakdown { get; set; } = new();
        
 // Channel preferences
     public int EmailOnlyMembers { get; set; }
        public int BothChannelsMembers { get; set; }
    }
    
    public class PreferenceRankingDTO
    {
        public int TagId { get; set; }
        public string TagNameEN { get; set; } = string.Empty;
        public string TagNameZH { get; set; } = string.Empty;
        public int MemberCount { get; set; }
 public double PercentageOfMembers { get; set; }
    }
    
  public class LanguagePreferenceDTO
    {
  public int EnglishOnly { get; set; }
     public int ChineseOnly { get; set; }
      public int Both { get; set; }
        public double EnglishPercentage { get; set; }
 public double ChinesePercentage { get; set; }
    public double BothPercentage { get; set; }
    }
 
    #endregion

    #region Engagement Signals - Indirect Engagement Indicators
    
    /// <summary>
    /// Indirect signals of engagement that don't require tracking pixels
    /// </summary>
    public class EngagementSignalsDTO
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        
        // Negative signals
        public int Unsubscribes { get; set; }
    public double UnsubscribeRate { get; set; }
        public List<UnsubscribeInsightDTO> UnsubscribesByTopic { get; set; } = new(); // Which topics cause unsubscribes?
        
  // Positive signals
        public int NewMembers { get; set; }
        public int ProfileUpdates { get; set; } // Members updating their preferences
public int PreferenceExpansions { get; set; } // Members adding more interests
        
 // List health
        public int ActiveListSize { get; set; }
        public double ListGrowthRate { get; set; }
        public double ChurnRate { get; set; } // Unsubscribes / Active members
        
        // Health assessment
 public string ListHealthStatus { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
    }
    
    public class UnsubscribeInsightDTO
    {
        public int BroadcastId { get; set; }
        public string BroadcastTitle { get; set; } = string.Empty;
    public int UnsubscribeCount { get; set; }
        public List<string> TopicsIncluded { get; set; } = new();
    }
    
    #endregion

    #region Recommendations Based on Measurable Data
    
/// <summary>
    /// Content recommendations based on member preferences and delivery data
    /// </summary>
    public class PracticalRecommendationsDTO
    {
        // What to send next based on member preferences
    public List<TopicRecommendationDTO> RecommendedTopics { get; set; } = new();
        
 // Topics to avoid (high unsubscribe correlation)
        public List<TopicRecommendationDTO> TopicsToReconsider { get; set; } = new();
   
     // Audience segments to focus on
        public List<AudienceRecommendationDTO> AudienceRecommendations { get; set; } = new();
        
        // List health recommendations
      public List<string> ListHealthRecommendations { get; set; } = new();
        
        // General recommendations
        public List<string> GeneralRecommendations { get; set; } = new();
    }
    
    public class AudienceRecommendationDTO
    {
        public string SegmentName { get; set; } = string.Empty;
        public string RecommendationType { get; set; } = string.Empty; // "Increase Frequency", "Create Targeted Content"
    public string Reason { get; set; } = string.Empty;
        public int AffectedMembers { get; set; }
 }
    
    #endregion

    #region Recent Broadcast Summary
    
    public class RecentBroadcastSummaryDTO
  {
        public int BroadcastId { get; set; }
      public string Title { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        
        // Reliable metrics
  public int TotalSent { get; set; }
        public int Delivered { get; set; }
 public int Bounced { get; set; }
     public double DeliveryRate { get; set; }
        
        // Content info
        public int ArticleCount { get; set; }
 public List<string> TopicsIncluded { get; set; } = new();
   
     // Engagement signals
        public int Unsubscribes { get; set; }
        
      // Status assessment
  public string DeliveryStatus { get; set; } = string.Empty; // "Excellent", "Good", "Issues"
    }
    
    #endregion
}
