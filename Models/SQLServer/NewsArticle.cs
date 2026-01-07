using System;
using System.ComponentModel.DataAnnotations;
namespace News_Back_end.Models.SQLServer
{
    public class NewsArticle
    {
        public int NewsArticleId { get; set; }

        [Required, MaxLength(500)]
        public string Title { get; set; } = null!;

        // original content fetched from source
        public string OriginalContent { get; set; } = null!;

        // AI translated content (optional)
        public string? TranslatedContent { get; set; }

        // original language code e.g. "en" or "zh"
        [MaxLength(10)]
        public string OriginalLanguage { get; set; } = "en";

        // target language of TranslatedContent
        [MaxLength(10)]
        public string? TranslationLanguage { get; set; }

        public TranslationStatus TranslationStatus { get; set; } = TranslationStatus.Pending;
        public string? TranslationReviewedBy { get; set; }
        public DateTime? TranslationReviewedAt { get; set; }
        // who saved the current TranslatedContent and when
        public string? TranslationSavedBy { get; set; }
        public DateTime? TranslationSavedAt { get; set; }

        [Required, MaxLength(1000)]
        public string SourceURL { get; set; } = null!;

        public DateTime? PublishedAt { get; set; }
        public DateTime CrawledAt { get; set; } = DateTime.Now;

        // FK to Source
        public int? SourceId { get; set; }
        public Source? Source { get; set; }

        // optional summary reference
        public int? SummaryId { get; set; }

        // NLP outputs as JSON/text
        public string? NLPKeywords { get; set; }
        public string? NamedEntities { get; set; }

        // analytics
        public double? SentimentScore { get; set; }

        public ArticleStatus Status { get; set; } = ArticleStatus.Draft;

        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }

    public enum TranslationStatus { Pending, InProgress, Translated }
    public enum ArticleStatus { Draft, Published }
}
