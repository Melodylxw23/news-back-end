using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
 public enum ConsultantInsightsPeriod
 {
 Daily,
 Weekly
 }

 /// <summary>
 /// Idempotency log to ensure consultant insights are sent at most once per period.
 /// - Daily: one row per consultant per date (UTC)
 /// - Weekly: one row per consultant per ISO week start date (UTC Monday)
 /// </summary>
 public class ConsultantInsightsSendLog
 {
 public int ConsultantInsightsSendLogId { get; set; }

 [Required]
 public string ConsultantUserId { get; set; } = string.Empty;
 public ApplicationUser ConsultantUser { get; set; } = null!;

 public ConsultantInsightsPeriod Period { get; set; }

 /// <summary>
 /// Key date in UTC used for uniqueness.
 /// Daily: date portion of nowUtc.
 /// Weekly: Monday date of the week in UTC.
 /// </summary>
 public DateTime PeriodDateUtc { get; set; }

 public DateTimeOffset SentAtUtc { get; set; } = DateTimeOffset.UtcNow;

 [MaxLength(320)]
 public string Email { get; set; } = string.Empty;

 public bool Success { get; set; } = true;

 [MaxLength(2000)]
 public string? Error { get; set; }
 }
}
