using News_Back_end.Models.SQLServer;
using System;

namespace News_Back_end.DTOs
{
 public class BroadcastCreateDTO
 {
 public string Title { get; set; } = string.Empty;
 public string Subject { get; set; } = string.Empty;
 public string Body { get; set; } = string.Empty;
 public BroadcastChannel Channel { get; set; } = BroadcastChannel.Email;
 public BroadcastAudience TargetAudience { get; set; } = BroadcastAudience.All;
 public DateTimeOffset? ScheduledSendAt { get; set; }
 }

 public class BroadcastUpdateDTO : BroadcastCreateDTO
 {
 public BroadcastStatus? Status { get; set; }
 }
}
