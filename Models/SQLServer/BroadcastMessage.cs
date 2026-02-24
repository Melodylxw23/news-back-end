using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

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

    public enum BroadcastLanguage
    {
        English,
        Chinese,
        Both  // Send in both languages based on member preference
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

   // Translated versions (for multi-language support)
        public string? TitleZH { get; set; }
        public string? SubjectZH { get; set; }
        public string? BodyZH { get; set; }

  // Original language of the broadcast
        public BroadcastLanguage Language { get; set; } = BroadcastLanguage.English;

 // Which channel this broadcast is for
 public BroadcastChannel Channel { get; set; } = BroadcastChannel.Email;

 // Which audience this broadcast targets (flags allow multiple selections)
 public BroadcastAudience TargetAudience { get; set; } = BroadcastAudience.All;

 // Draft/scheduled/sent state
 public BroadcastStatus Status { get; set; } = BroadcastStatus.Draft;

 // Audit fields
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
     public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

 // Optional scheduling
 public DateTimeOffset? ScheduledSendAt { get; set; }

 // Optional reference to the creating user (ApplicationUser.Id)
 public string? CreatedById { get; set; }

 // Many-to-many relationship with PublicationDraft (selected articles)
 public ICollection<PublicationDraft> SelectedArticles { get; set; } = new List<PublicationDraft>();

 // Tag-based targeting (stored as JSON)
 public string SelectedInterestTagIdsJson { get; set; } = "[]";
 public string SelectedIndustryTagIdsJson { get; set; } = "[]";

 // Helper properties for easy access to tag IDs - NOT MAPPED to database columns
 [NotMapped]
 public List<int> SelectedInterestTagIds
 {
  get
 {
  try
  {
   var json = System.Text.Json.JsonSerializer.Deserialize<List<int>>(SelectedInterestTagIdsJson);
   return json ?? new List<int>();
  }
  catch
     {
   return new List<int>();
 }
 }
  set
  {
       SelectedInterestTagIdsJson = System.Text.Json.JsonSerializer.Serialize(value ?? new List<int>());
     }
 }

 [NotMapped]
 public List<int> SelectedIndustryTagIds
 {
 get
    {
       try
  {
    var json = System.Text.Json.JsonSerializer.Deserialize<List<int>>(SelectedIndustryTagIdsJson);
         return json ?? new List<int>();
   }
     catch
   {
        return new List<int>();
      }
   }
       set
   {
   SelectedIndustryTagIdsJson = System.Text.Json.JsonSerializer.Serialize(value ?? new List<int>());
   }
  }
 }
}