using News_Back_end.Models.SQLServer;
using System;
using System.Collections.Generic;

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
 
 // List of PublicationDraft IDs to include in this broadcast
 public List<int> SelectedArticleIds { get; set; } = new List<int>();
 }

 public class BroadcastUpdateDTO : BroadcastCreateDTO
 {
 public BroadcastStatus? Status { get; set; }
 }

 // Lightweight DTO for list view - only essential fields for display
 public class BroadcastListItemDTO
 {
 public int Id { get; set; }
 public string Title { get; set; } = string.Empty;
 public string Subject { get; set; } = string.Empty;
 public BroadcastChannel Channel { get; set; }
 public BroadcastAudience TargetAudience { get; set; }
 public BroadcastStatus Status { get; set; }
 public DateTimeOffset CreatedAt { get; set; }
 public DateTimeOffset UpdatedAt { get; set; }
 public DateTimeOffset? ScheduledSendAt { get; set; }
 public string? CreatedById { get; set; }
 
 // Just the count and IDs, not full objects
 public int SelectedArticlesCount { get; set; }
 public List<int> SelectedArticleIds { get; set; } = new List<int>();
 }

 // Full DTO for detail view - includes all related data
 public class BroadcastDetailDTO
 {
 public int Id { get; set; }
 public string Title { get; set; } = string.Empty;
 public string Subject { get; set; } = string.Empty;
 public string Body { get; set; } = string.Empty;
 public BroadcastChannel Channel { get; set; }
 public BroadcastAudience TargetAudience { get; set; }
 public BroadcastStatus Status { get; set; }
 public DateTimeOffset CreatedAt { get; set; }
 public DateTimeOffset UpdatedAt { get; set; }
 public DateTimeOffset? ScheduledSendAt { get; set; }
 public string? CreatedById { get; set; }
 
 // Full article details for editing
 public List<PublishedArticleListDTO> SelectedArticles { get; set; } = new List<PublishedArticleListDTO>();
 }

 public class PublishedArticleListDTO
 {
 public int PublicationDraftId { get; set; }
 public string Title { get; set; } = string.Empty;
 public string? HeroImageUrl { get; set; }
 public DateTime? PublishedAt { get; set; }
 public string? IndustryTagName { get; set; }
 public List<string> InterestTagNames { get; set; } = new List<string>();
 }

 #region Broadcast Scheduling DTOs

 /// <summary>
 /// Request to schedule a broadcast for future sending
 /// </summary>
 public class ScheduleBroadcastRequestDTO
 {
     public int BroadcastId { get; set; }
     public DateTimeOffset ScheduledSendAt { get; set; }
 }

 /// <summary>
 /// Request to reschedule a broadcast to a different time
 /// </summary>
 public class RescheduleBroadcastRequestDTO
 {
     public DateTimeOffset NewScheduledSendAt { get; set; }
 }

 /// <summary>
 /// Information about a scheduled broadcast
 /// </summary>
 public class ScheduledBroadcastInfoDTO
 {
     public int Id { get; set; }
     public string Title { get; set; } = string.Empty;
     public string Subject { get; set; } = string.Empty;
     public DateTimeOffset ScheduledSendAt { get; set; }
     public DateTimeOffset CreatedAt { get; set; }
     public string? CreatedById { get; set; }
     public int SelectedArticlesCount { get; set; }
     public double MinutesUntilSend { get; set; }
     public bool IsOverdue { get; set; }
 }

 /// <summary>
 /// Status information about the broadcast scheduler service
 /// </summary>
 public class BroadcastSchedulerStatusDTO
 {
     public bool IsRunning { get; set; }
     public int CheckIntervalMinutes { get; set; }
     public DateTimeOffset LastCheckedAt { get; set; }
     public string Description { get; set; } = string.Empty;
     
     public int TotalScheduledBroadcasts { get; set; }
     public int UpcomingInNextHour { get; set; }
     public int UpcomingInNext24Hours { get; set; }
     public int OverdueBroadcasts { get; set; }
  
     public DateTimeOffset CurrentServerTime { get; set; }
 }

 #endregion
}
