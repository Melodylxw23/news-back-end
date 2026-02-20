using System.Collections.Generic;
using News_Back_end.Models.SQLServer;
using News_Back_end.DTOs;

namespace News_Back_end.DTOs
{
    // Renamed to avoid Swagger schema id collision with Controllers.SourcesController.FetchDto
    public class FetchRequestDto
    {
        public List<int>? SourceIds { get; set; }

        // Optional per-request override for description/summary settings.
        // If provided, these settings are used for the fetch run only and are not persisted.
        // Use DTO to avoid requiring full Source / enum values in request payload
        public SourceDescriptionSettingDto? SourceSettingOverride { get; set; }

        // When true, bypass duplicate check and force saving articles (useful for re-import/debug)
        public bool Force { get; set; } = false;

        // Total number of articles to fetch across all selected sources (1-10). 
        // This is the consultant's "number of articles" setting.
        public int? MaxArticles { get; set; }

        // Legacy alias — mapped to MaxArticles for backward compatibility
        public int? MaxArticlesPerSource { get; set; }

        // Top-level short-hand overrides (frontend sometimes sends these at top-level)
        public string? SummaryFormat { get; set; }
        public string? SummaryLength { get; set; }
        public int? SummaryWordCount { get; set; }
        public string? SummaryTone { get; set; }
        public bool? TranslateOnFetch { get; set; }
        public bool? IncludeEnglishSummary { get; set; }
        public bool? IncludeChineseSummary { get; set; }
        public string? SummaryLanguage { get; set; }
        public string? CustomKeyPoints { get; set; }
        public int? MinArticleLength { get; set; }
    }
}
