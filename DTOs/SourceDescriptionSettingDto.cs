namespace News_Back_end.DTOs
{
    public class SourceDescriptionSettingDto
    {
        public bool TranslateOnFetch { get; set; } = true;
        public int SummaryWordCount { get; set; } = 150;
        public string? SummaryTone { get; set; }
        public string? SummaryFormat { get; set; }
        public string? CustomKeyPoints { get; set; }
        public int? MaxArticlesPerFetch { get; set; }

        public bool IncludeOriginalChinese { get; set; } = true;
        public bool IncludeEnglishSummary { get; set; } = true;
        public bool IncludeChineseSummary { get; set; } = true;

        public int MinArticleLength { get; set; } = 200;
        public string? SummaryFocus { get; set; }
        public bool SentimentAnalysisEnabled { get; set; } = false;
        public bool HighlightEntities { get; set; } = false;
        public string? SummaryLanguage { get; set; } = "EN";

        // Optional category: "short", "medium", "long". If provided, server maps to word counts.
        public string? SummaryLength { get; set; }
    }
}
