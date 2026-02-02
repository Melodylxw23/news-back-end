using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
 public class PublicationDraft
 {
 public int PublicationDraftId { get; set; }

 // FK to the fetched article (do not modify NewsArticle schema)
 public int NewsArticleId { get; set; }
 public NewsArticle? NewsArticle { get; set; }

 // Who created/owns this draft
 [MaxLength(200)]
 public string? CreatedBy { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.Now;
 public DateTime? UpdatedAt { get; set; }

 // Hero image metadata
 [MaxLength(2000)]
 public string? HeroImageUrl { get; set; }
 [MaxLength(500)]
 public string? HeroImageAlt { get; set; }
 [MaxLength(200)]
 public string? HeroImageSource { get; set; }

 // Optional overrides for full content when publishing
 public string? FullContentEN { get; set; }
 public string? FullContentZH { get; set; }

 // Publication scheduling/state
 public bool IsPublished { get; set; } = false;
 public string? PublishedBy { get; set; }
 public DateTime? PublishedAt { get; set; }
 public DateTime? ScheduledAt { get; set; }

 // Single industry tag (required when saving)
 public int? IndustryTagId { get; set; }
 public IndustryTag? IndustryTag { get; set; }

 // One or more interest tags
 public ICollection<InterestTag> InterestTags { get; set; } = new List<InterestTag>();
 }
}
