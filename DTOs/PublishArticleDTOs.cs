using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace News_Back_end.DTOs
{
 public class PublishArticleDto
 {
 [Required]
 public int NewsArticleId { get; set; }

 public string? HeroImageUrl { get; set; }
 public string? HeroImageAlt { get; set; }
 public string? HeroImageSource { get; set; }

 public string? FullContentEN { get; set; }
 public string? FullContentZH { get; set; }

 // tag ids: single industry and many interests
 public int? IndustryTagId { get; set; }
 public List<int>? InterestTagIds { get; set; }

 public DateTime? ScheduledAt { get; set; }
 }

 public class PublishActionDto
 {
 [Required]
 public int NewsArticleId { get; set; }
 [Required]
 public string Action { get; set; } = "publish"; // publish | unpublish
 public DateTime? ScheduledAt { get; set; }
 }

 public class GenerateHeroImageDto
 {
 [Required]
 public int NewsArticleId { get; set; }
 public string? PromptOverride { get; set; }
 public string? Style { get; set; }
 }

 // Quick Publish: batch AI-process and publish immediately
 public class QuickPublishDto
 {
 [Required]
 public List<int> ArticleIds { get; set; } = new();
 }

 // Quick Schedule: batch AI-process and schedule for a specific time
 public class QuickScheduleDto
 {
 [Required]
 public List<int> ArticleIds { get; set; } = new();

 [Required]
 public DateTime? ScheduledAt { get; set; }
 }

 // Result for each article in Quick Publish/Schedule
 public class QuickPublishResultDto
 {
 public int NewsArticleId { get; set; }
 public bool Success { get; set; }
 public string? Error { get; set; }

 // Title cleaning
 public bool TitlesCleaned { get; set; }

 // Tag assignment
 public bool TagsAssigned { get; set; }
 public int? AssignedIndustryTagId { get; set; }
 public List<int>? AssignedInterestTagIds { get; set; }
 public string? ClassificationError { get; set; }

 // Hero image
 public bool HeroImageGenerated { get; set; }
 public string? HeroImageUrl { get; set; }
 public string? ImageError { get; set; }

 // For scheduling
 public DateTime? ScheduledAt { get; set; }
 }

 // Tag Analytics
 public class TagAnalyticsRequestDto
 {
 // Placeholder for future filters (e.g., date range)
 }

 public class TagAnalyticsResponseDto
 {
 public DateTime GeneratedAt { get; set; }
 public int TotalTaggedArticles { get; set; }
 public int RecentTaggedArticles { get; set; }
 public int PreviousPeriodTaggedArticles { get; set; }
 public List<TagUsageDto> IndustryDistribution { get; set; } = new();
 public List<TagUsageDto> InterestDistribution { get; set; } = new();
 public List<CoOccurrenceDto> TopCoOccurrences { get; set; } = new();
 public List<UnusedTagDto> UnusedIndustryTags { get; set; } = new();
 public List<UnusedTagDto> UnusedInterestTags { get; set; } = new();
 public string? AiAnalysis { get; set; }
 }

 public class TagUsageDto
 {
 public int TagId { get; set; }
 public string NameEN { get; set; } = "";
 public string NameZH { get; set; } = "";
 public int Count { get; set; }
 public int PublishedCount { get; set; }
 public int ScheduledCount { get; set; }
 public int DraftCount { get; set; }
 }

 public class CoOccurrenceDto
 {
 public string TagA { get; set; } = "";
 public string TagB { get; set; } = "";
 public int Count { get; set; }
 }

 public class UnusedTagDto
 {
 public int TagId { get; set; }
 public string NameEN { get; set; } = "";
 public string NameZH { get; set; } = "";
 }
}
