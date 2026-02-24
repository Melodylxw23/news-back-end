using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
    public class FetchAttempt
    {
        public int FetchAttemptId { get; set; }

        // Consultant account that initiated this fetch
        [Required]
        public string ApplicationUserId { get; set; } = null!;
        public ApplicationUser? ApplicationUser { get; set; }

        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

        // Sequential attempt number per user (Fetch Attempt #1, #2, etc.)
        public int AttemptNumber { get; set; }

        // ?? Configuration snapshot: what settings were used for this fetch ??

        // How many articles were requested (1-10)
        public int? MaxArticlesPerFetch { get; set; }

        // Comma-separated source IDs that were selected for this fetch
        public string? SourceIdsSnapshot { get; set; }

        // Summary format used: "paragraph" or "bullet"
        [MaxLength(50)]
        public string? SummaryFormat { get; set; }

        // Summary length category: "short", "medium", "long"
        [MaxLength(20)]
        public string? SummaryLength { get; set; }

        // Actual word count target used
        public int? SummaryWordCount { get; set; }

        // Navigation: which articles were returned in this attempt
        public ICollection<FetchAttemptArticle> Articles { get; set; } = new List<FetchAttemptArticle>();
    }
}
