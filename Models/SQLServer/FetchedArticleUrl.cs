using System;
using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
    /// <summary>
    /// Lightweight record of every article URL fetched by a consultant.
    /// Persists even after the article/fetch-attempt is deleted, so the
    /// dedup check can prevent re-fetching the same article.
    /// </summary>
    public class FetchedArticleUrl
    {
     public int FetchedArticleUrlId { get; set; }

        [Required]
        public string ApplicationUserId { get; set; } = null!;

        [Required, MaxLength(1000)]
        public string SourceURL { get; set; } = null!;

        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    }
}
