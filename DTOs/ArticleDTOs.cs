using System;
using News_Back_end.Models.SQLServer;
using System.Collections.Generic;

namespace News_Back_end.DTOs
{
    public record ArticleDto(
        int NewsArticleId,
        string Title,
        string Content,
        string OriginalLanguage,
        string? TranslationLanguage,
        TranslationStatus TranslationStatus,
        string SourceURL,
        DateTime? PublishedAt,
        DateTime CrawledAt,
        int? SourceId);

    public class PagedResult<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long Total { get; set; }
        public List<T> Items { get; set; } = new();
    }
}
