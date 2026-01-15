using System;

namespace News_Back_end.Models.SQLServer
{
 [Flags]
 public enum BroadcastAudience
 {
 All =1,
 Technology =2,
 Business =4,
 Sports =8,
 Entertainment =16,
 Politics =32
 }

 public enum BroadcastChannel
 {
 Email,
 SMS,
 Push,
 All
 }

 public enum BroadcastStatus
 {
 Draft,
 Scheduled,
 Sent,
 Cancelled
 }

 public class BroadcastMessage
 {
 public int Id { get; set; }

 // Human-friendly title for the template/draft
 public string Title { get; set; } = string.Empty;

 // Optional subject (useful for email or notifications)
 public string Subject { get; set; } = string.Empty;

 // The main body/content (HTML or plain text)
 public string Body { get; set; } = string.Empty;

 // Which channel this broadcast is for
 public BroadcastChannel Channel { get; set; } = BroadcastChannel.Email;

 // Which audience this broadcast targets (flags allow multiple selections)
 public BroadcastAudience TargetAudience { get; set; } = BroadcastAudience.All;

 // Draft/scheduled/sent state
 public BroadcastStatus Status { get; set; } = BroadcastStatus.Draft;

 // Audit fields
 public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
 public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

 // Optional scheduling
 public DateTimeOffset? ScheduledSendAt { get; set; }

 // Optional reference to the creating user (ApplicationUser.Id)
 public string? CreatedById { get; set; }
 }
}