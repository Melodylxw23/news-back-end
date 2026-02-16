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
}
