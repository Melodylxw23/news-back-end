using System.ComponentModel.DataAnnotations;

namespace News_Back_end.DTOs
{
 public sealed class ConsultantInsightsPreviewDTO
 {
 public string Subject { get; set; } = string.Empty;
 public string HtmlBody { get; set; } = string.Empty;
 public DateTimeOffset GeneratedAtUtc { get; set; }
 }

 public sealed class ConsultantInsightsSendNowResultDTO
 {
 public bool Success { get; set; }
 public string Message { get; set; } = string.Empty;
 public string Email { get; set; } = string.Empty;
 public DateTimeOffset AttemptedAtUtc { get; set; }
 }

 public sealed class ConsultantInsightsSendNowRequestDTO
 {
 /// <summary>
 /// If true, bypass the once-per-period idempotency guard.
 /// Useful for testing/demo, but should generally be false.
 /// </summary>
 public bool Force { get; set; } = false;
 }
}
