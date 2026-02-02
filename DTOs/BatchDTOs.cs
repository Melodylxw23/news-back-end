using System;
using System.Collections.Generic;

namespace News_Back_end.DTOs
{
 public class BatchPublishDto
 {
 public List<int>? ArticleIds { get; set; }
 public DateTime? ScheduledAt { get; set; }
 }

 public class BatchIdsDto
 {
 public List<int>? ArticleIds { get; set; }
 }
}
