using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
    public class SourceDescriptionSetting
    {
        [Key]
        public int SettingId { get; set; }
        public int SourceId { get; set; }
        public Source Source { get; set; } = null!;

        public bool TranslateOnFetch { get; set; } = true;
        public int SummaryWordCount { get; set; } = 150;
        public string SummaryTone { get; set; } = "neutral";
        public string SummaryFormat { get; set; } = "paragraph";
        public string? CustomKeyPoints { get; set; }
        public int MaxArticlesPerFetch { get; set; } = 10;

        public bool IncludeOriginalChinese { get; set; } = true;
        public bool IncludeEnglishSummary { get; set; } = true;
        public bool IncludeChineseSummary { get; set; } = false;

        public int MinArticleLength { get; set; } = 200;
        public string? SummaryFocus { get; set; }
        public bool SentimentAnalysisEnabled { get; set; } = false;
        public bool HighlightEntities { get; set; } = false;
        public string SummaryLanguage { get; set; } = "EN";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
    
}
