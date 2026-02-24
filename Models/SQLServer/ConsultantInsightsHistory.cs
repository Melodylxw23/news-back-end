using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
    /// <summary>
    /// Stores historical consultant insights to ensure fresh, unique content daily.
    /// Prevents repetition of key developments, opportunities, watchouts, and actions across periods.
    /// </summary>
    public class ConsultantInsightsHistory
    {
        public int ConsultantInsightsHistoryId { get; set; }

        [Required]
        public string ConsultantUserId { get; set; } = string.Empty;

        public ApplicationUser ConsultantUser { get; set; } = null!;

        /// <summary>
        /// Period this insight was generated for (Daily or Weekly)
        /// </summary>
        public ConsultantInsightsPeriod Period { get; set; }

        /// <summary>
        /// Date key in UTC (same as PeriodDateUtc in ConsultantInsightsSendLog)
        /// </summary>
        public DateTime PeriodDateUtc { get; set; }

        /// <summary>
      /// Serialized JSON array of key developments sent in this period
   /// </summary>
    [MaxLength(5000)]
        public string KeyDevelopmentsJson { get; set; } = "[]";

        /// <summary>
     /// Serialized JSON array of opportunities sent in this period
   /// </summary>
        [MaxLength(5000)]
      public string OpportunitiesJson { get; set; } = "[]";

        /// <summary>
        /// Serialized JSON array of watchouts sent in this period
     /// </summary>
        [MaxLength(5000)]
     public string WatchoutsJson { get; set; } = "[]";

 /// <summary>
  /// Serialized JSON array of recommended actions sent in this period
        /// </summary>
  [MaxLength(5000)]
   public string RecommendedActionsJson { get; set; } = "[]";

        /// <summary>
  /// Executive summary sent in this period
  /// </summary>
        [MaxLength(1000)]
        public string ExecutiveSummary { get; set; } = string.Empty;

        /// <summary>
/// When this insight was generated/sent
        /// </summary>
        public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;

      /// <summary>
        /// Unique constraint: one record per consultant + period + periodDate
        /// </summary>
   public void SetPeriodKey(ConsultantInsightsFrequency frequency, DateTimeOffset nowUtc)
      {
  var date = nowUtc.UtcDateTime.Date;
      if (frequency == ConsultantInsightsFrequency.Weekly)
     {
         int diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
var monday = date.AddDays(-diff);
 Period = ConsultantInsightsPeriod.Weekly;
   PeriodDateUtc = monday;
          }
       else
            {
         Period = ConsultantInsightsPeriod.Daily;
    PeriodDateUtc = date;
        }
        }
    }
}
