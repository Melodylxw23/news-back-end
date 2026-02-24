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
    public BroadcastLanguage Language { get; set; } = BroadcastLanguage.English;
 public DateTimeOffset? ScheduledSendAt { get; set; }
 
 // List of PublicationDraft IDs to include in this broadcast
 public List<int> SelectedArticleIds { get; set; } = new List<int>();

 // Tag-based targeting
 public List<int> SelectedInterestTagIds { get; set; } = new List<int>();
 public List<int> SelectedIndustryTagIds { get; set; } = new List<int>();
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
        public BroadcastLanguage Language { get; set; }
 public BroadcastStatus Status { get; set; }
 public DateTimeOffset CreatedAt { get; set; }
 public DateTimeOffset UpdatedAt { get; set; }
 public DateTimeOffset? ScheduledSendAt { get; set; }
 public string? CreatedById { get; set; }
 
 // Just the count and IDs, not full objects
 public int SelectedArticlesCount { get; set; }
 public List<int> SelectedArticleIds { get; set; } = new List<int>();

 // Tag-based targeting
 public List<int> SelectedInterestTagIds { get; set; } = new List<int>();
 public List<int> SelectedIndustryTagIds { get; set; } = new List<int>();
        
   // Translation status
        public bool HasChineseTranslation { get; set; }
 }

 // Full DTO for detail view - includes all related data
 public class BroadcastDetailDTO
 {
 public int Id { get; set; }
 public string Title { get; set; } = string.Empty;
 public string Subject { get; set; } = string.Empty;
 public string Body { get; set; } = string.Empty;
        
        // Chinese translations
        public string? TitleZH { get; set; }
        public string? SubjectZH { get; set; }
    public string? BodyZH { get; set; }
   
 public BroadcastChannel Channel { get; set; }
 public BroadcastAudience TargetAudience { get; set; }
    public BroadcastLanguage Language { get; set; }
 public BroadcastStatus Status { get; set; }
 public DateTimeOffset CreatedAt { get; set; }
 public DateTimeOffset UpdatedAt { get; set; }
 public DateTimeOffset? ScheduledSendAt { get; set; }
 public string? CreatedById { get; set; }
 
 // Full article details for editing
 public List<PublishedArticleListDTO> SelectedArticles { get; set; } = new List<PublishedArticleListDTO>();

 // Tag-based targeting
 public List<int> SelectedInterestTagIds { get; set; } = new List<int>();
 public List<int> SelectedIndustryTagIds { get; set; } = new List<int>();
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

    #region Broadcast Translation DTOs

 /// <summary>
    /// Request to translate a broadcast to Chinese
    /// </summary>
    public class BroadcastTranslateRequestDTO
    {
        public int BroadcastId { get; set; }
        public string TargetLanguage { get; set; } = "zh";  // "zh" for Chinese, "en" for English
  }

    /// <summary>
    /// Result of broadcast translation
    /// </summary>
    public class BroadcastTranslateResultDTO
    {
        public int BroadcastId { get; set; }
   public string SourceLanguage { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
     
        // Translated content
        public string TranslatedTitle { get; set; } = string.Empty;
      public string TranslatedSubject { get; set; } = string.Empty;
        public string TranslatedBody { get; set; } = string.Empty;
        
     public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    #endregion

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

 #region Tag-Based Broadcast Targeting

 /// <summary>
 /// DTO for selecting broadcast targets by actual interest/industry tags
 /// This replaces/supplements the hardcoded BroadcastAudience enum
 /// </summary>
 public class BroadcastTargetingDTO
 {
     /// <summary>
     /// Target by specific InterestTag IDs
     /// </summary>
     public List<int> SelectedInterestTagIds { get; set; } = new List<int>();

     /// <summary>
     /// Target by specific IndustryTag IDs
     /// </summary>
     public List<int> SelectedIndustryTagIds { get; set; } = new List<int>();

  /// <summary>
     /// If true, target members who have ANY of the selected tags
     /// If false, target members who have ALL of the selected tags
   /// </summary>
     public bool UseOrLogic { get; set; } = true;

     /// <summary>
     /// Optional: filter by member country
     /// </summary>
     public List<string> TargetCountries { get; set; } = new List<string>();

  /// <summary>
     /// Optional: filter by member type
     /// </summary>
     public List<string> TargetMembershipTypes { get; set; } = new List<string>();
 }

 /// <summary>
 /// Available tags for broadcast targeting (for UI selection)
 /// </summary>
 public class BroadcastTagOptionsDTO
 {
   public List<TagOptionDTO> InterestTags { get; set; } = new List<TagOptionDTO>();
   public List<TagOptionDTO> IndustryTags { get; set; } = new List<TagOptionDTO>();
 }

 /// <summary>
 /// Single tag option for dropdown/selection
 /// </summary>
 public class TagOptionDTO
 {
  public int Id { get; set; }
     public string NameEN { get; set; } = string.Empty;
     public string NameZH { get; set; } = string.Empty;
   public int MemberCount { get; set; } // how many members have this tag
 }

 /// <summary>
 /// Preview of members who will receive the broadcast
 /// </summary>
 public class BroadcastTargetPreviewDTO
 {
 public int TotalMembersMatched { get; set; }
     public List<MemberPreviewDTO> SampleMembers { get; set; } = new List<MemberPreviewDTO>();
   public int SampleSize { get; set; } = 10; // how many shown as sample
 }

 /// <summary>
 /// Member preview for targeting
 /// </summary>
 public class MemberPreviewDTO
 {
     public int MemberId { get; set; }
 public string CompanyName { get; set; } = string.Empty;
     public string ContactPerson { get; set; } = string.Empty;
     public string Email { get; set; } = string.Empty;
     public List<string> InterestTagNames { get; set; } = new List<string>();
     public List<string> IndustryTagNames { get; set; } = new List<string>();
 }

 #endregion
}
