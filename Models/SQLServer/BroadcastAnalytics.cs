using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
  /// <summary>
    /// Tracks individual link clicks within broadcast emails
    /// </summary>
public class BroadcastLinkClick
    {
        public int BroadcastLinkClickId { get; set; }

     // Foreign keys
        public int BroadcastDeliveryId { get; set; }
        public BroadcastDelivery BroadcastDelivery { get; set; } = null!;

        // Optional: Which article was clicked (if applicable)
        public int? PublicationDraftId { get; set; }
        public PublicationDraft? PublicationDraft { get; set; }

   // The original URL that was clicked
        [MaxLength(2000)]
        public string OriginalUrl { get; set; } = string.Empty;

        // Link identifier for tracking (e.g., "article-1", "cta-button", "unsubscribe")
  [MaxLength(100)]
        public string? LinkIdentifier { get; set; }

        // Click metadata
        public DateTime ClickedAt { get; set; } = DateTime.UtcNow;
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }

        // Device/client detection (parsed from UserAgent)
        [MaxLength(50)]
        public string? DeviceType { get; set; } // Desktop, Mobile, Tablet
        [MaxLength(100)]
        public string? EmailClient { get; set; } // Gmail, Outlook, Apple Mail, etc.
    }

    /// <summary>
    /// Pre-computed analytics summary per broadcast
    /// </summary>
    public class BroadcastAnalyticsSummary
    {
        public int BroadcastAnalyticsSummaryId { get; set; }

      public int BroadcastMessageId { get; set; }
        public BroadcastMessage BroadcastMessage { get; set; } = null!;

        // Delivery metrics
        public int TotalSent { get; set; }
        public int TotalDelivered { get; set; }
        public int TotalBounced { get; set; }
        public double DeliveryRate { get; set; }

        // Engagement metrics
        public int UniqueOpens { get; set; }
        public int TotalOpens { get; set; }
   public double OpenRate { get; set; }

  public int UniqueClicks { get; set; }
        public int TotalClicks { get; set; }
        public double ClickRate { get; set; } // Clicks / Delivered
  public double ClickToOpenRate { get; set; } // Clicks / Opens

        // Timing metrics
     public DateTime? FirstOpenAt { get; set; }
        public DateTime? LastOpenAt { get; set; }
    public DateTime? PeakEngagementHour { get; set; }

   // Audience breakdown (JSON serialized for flexibility)
        public string? EngagementByCountryJson { get; set; }
        public string? EngagementByLanguageJson { get; set; }
      public string? EngagementByIndustryJson { get; set; }
        public string? EngagementByInterestJson { get; set; }

        // Device breakdown
  public int DesktopOpens { get; set; }
  public int MobileOpens { get; set; }
  public int TabletOpens { get; set; }

     // Last computed timestamp
        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Aggregated topic/interest performance over time
    /// </summary>
    public class TopicPerformanceMetric
    {
        public int TopicPerformanceMetricId { get; set; }

        // Which topic this metric is for
   public int? InterestTagId { get; set; }
        public InterestTag? InterestTag { get; set; }

        public int? IndustryTagId { get; set; }
     public IndustryTag? IndustryTag { get; set; }

        // Time period for this metric (daily aggregation)
        public DateTime MetricDate { get; set; }

    // How many broadcasts included this topic
        public int BroadcastCount { get; set; }

        // Total emails sent with this topic
   public int TotalSent { get; set; }

        // Engagement metrics
      public int TotalOpens { get; set; }
        public int TotalClicks { get; set; }
        public double AverageOpenRate { get; set; }
      public double AverageClickRate { get; set; }

        // Computed engagement score (0-100)
        public double EngagementScore { get; set; }

        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Member engagement profile - tracks individual member behavior patterns
    /// </summary>
    public class MemberEngagementProfile
    {
        public int MemberEngagementProfileId { get; set; }

        public int MemberId { get; set; }
        public Member Member { get; set; } = null!;

        // Lifetime metrics
        public int TotalEmailsReceived { get; set; }
      public int TotalEmailsOpened { get; set; }
        public int TotalLinksClicked { get; set; }
  public double LifetimeOpenRate { get; set; }
        public double LifetimeClickRate { get; set; }

        // Engagement level classification
   [MaxLength(20)]
      public string EngagementLevel { get; set; } = "Unknown"; // HighlyEngaged, Engaged, Occasional, Inactive, AtRisk

     // Best time to reach this member (based on historical opens)
   public int? PreferredDayOfWeek { get; set; } // 0-6
        public int? PreferredHourOfDay { get; set; } // 0-23

        // Most engaged topics (comma-separated interest tag IDs)
    public string? TopEngagedTopicsJson { get; set; }

        // Recent activity
        public DateTime? LastEmailReceivedAt { get; set; }
        public DateTime? LastEmailOpenedAt { get; set; }
        public DateTime? LastLinkClickedAt { get; set; }

        // Computed scores
        public double RecencyScore { get; set; } // How recently they engaged
    public double FrequencyScore { get; set; } // How often they engage
        public double OverallEngagementScore { get; set; } // Combined score 0-100

  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

 /// <summary>
    /// Daily aggregated analytics for dashboard
    /// </summary>
  public class DailyBroadcastMetric
    {
        public int DailyBroadcastMetricId { get; set; }

      public DateTime MetricDate { get; set; }

     // Volume metrics
        public int BroadcastsSent { get; set; }
        public int TotalEmailsSent { get; set; }
        public int TotalEmailsDelivered { get; set; }

        // Engagement metrics
        public int TotalOpens { get; set; }
 public int TotalClicks { get; set; }
     public double AverageOpenRate { get; set; }
        public double AverageClickRate { get; set; }

        // Audience metrics
   public int UniqueRecipientsReached { get; set; }
        public int NewSubscribersEngaged { get; set; }

     public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    }
}
