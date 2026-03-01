using System;
using System.Collections.Generic;
using News_Back_end.Models.SQLServer;

namespace News_Back_end.DTOs
{
 public class BroadcastGenerateRequestDTO
 {
 // Free-form prompt text that the AI will use to generate title/subject/body
 public string Prompt { get; set; } = string.Empty;

 // Desired channel for context (optional)
 public BroadcastChannel? Channel { get; set; }

 // Desired audience (optional)
 public BroadcastAudience TargetAudience { get; set; } = BroadcastAudience.All;

 // Desired language code (e.g., en, zh) - optional
 public string? Language { get; set; }

 // Selected article IDs to include in the newsletter (optional but recommended)
 public List<int>? SelectedArticleIds { get; set; }
 }

 public class BroadcastGenerateResultDTO
 {
 public string Title { get; set; } = string.Empty;
 public string Subject { get; set; } = string.Empty;
 public string Body { get; set; } = string.Empty;
 }

 /// <summary>
 /// Article summary for AI context when generating newsletters
 /// </summary>
 public class ArticleSummaryForAiDTO
 {
 public int ArticleId { get; set; }
 public string Title { get; set; } = string.Empty;
 public string? Summary { get; set; }
 public string? IndustryTag { get; set; }
 public List<string> InterestTags { get; set; } = new List<string>();
 public DateTime? PublishedAt { get; set; }
 }
}
