using System;

namespace News_Back_end.Models.SQLServer
{
 // Join entity: which articles were returned in a given fetch attempt.
 public class FetchAttemptArticle
 {
 public int FetchAttemptArticleId { get; set; }

 public int FetchAttemptId { get; set; }
 public FetchAttempt? FetchAttempt { get; set; }

 public int NewsArticleId { get; set; }
 public NewsArticle? NewsArticle { get; set; }

 // Preserve ordering within the attempt
 public int SortOrder { get; set; }
 }
}
