using System;
using News_Back_end.Models.SQLServer;
using System.Collections.Generic;

namespace News_Back_end.DTOs
{
    public record ArticleDto(
        int NewsArticleId,
        string TitleZH,
        string? TitleEN,
        string Content,
        string OriginalLanguage,
        string? TranslationLanguage,
        TranslationStatus TranslationStatus,
        string SourceURL,
        DateTime? PublishedAt,
        DateTime CrawledAt,
        int? SourceId,
        string? TranslationSavedBy = null,
        DateTime? TranslationSavedAt = null,
        // full AI-produced article content and summaries (optional)
        string? FullContentEN = null,
        string? FullContentZH = null,
        string? SummaryEN = null,
        string? SummaryZH = null);

    public class PagedResult<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long Total { get; set; }
        public List<T> Items { get; set; } = new();
    }

    public class ArticleDtos
    {
        public string? TitleZH { get; set; }
        public string? TitleEN { get; set; }
        public string? SourceURL { get; set; }
        public DateTime PublishedAt { get; set; }
        public string? OriginalLanguage { get; set; }
        public string? OriginalContent { get; set; }
        public string? TranslatedContent { get; set; }
        // Full article content produced by AI
        public string? FullContentEN { get; set; }
        public string? FullContentZH { get; set; }
        public string? SummaryEN { get; set; }
        public string? SummaryZH { get; set; }
    }
}
