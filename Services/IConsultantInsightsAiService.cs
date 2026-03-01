namespace News_Back_end.Services
{
 /// <summary>
    /// Service interface for generating AI-powered consultant insights
    /// based on province/territory, industry, and market trends.
    /// Supports multiple languages (English, Chinese).
    /// </summary>
    public interface IConsultantInsightsAiService
    {
      /// <summary>
        /// Generates insights for a consultant based on their territories and industries.
        /// Default language: English.
        /// </summary>
        Task<ConsultantInsightsAiResponse> GenerateInsightsAsync(
       List<string> territories,
    List<string> industries,
     string frequency,
       string? consultantName,
 CancellationToken cancellationToken);

        /// <summary>
        /// Generates insights for a consultant in a specified language.
        /// </summary>
      Task<ConsultantInsightsAiResponse> GenerateInsightsAsync(
        List<string> territories,
  List<string> industries,
      string frequency,
      string? consultantName,
            string language,
          CancellationToken cancellationToken);

        /// <summary>
     /// Generates insights with exclusion of previous content for ensuring daily freshness.
        /// </summary>
        Task<ConsultantInsightsAiResponse> GenerateInsightsWithExclusionAsync(
            List<string> territories,
       List<string> industries,
   string frequency,
 string? consultantName,
     string language,
  ConsultantInsightsAiResponse? previousInsights,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Represents a single insight item with its source reference.
    /// </summary>
    public class InsightItem
    {
        /// <summary>
     /// The insight text content.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Source reference name/title (e.g., "Reuters", "South China Morning Post").
        /// </summary>
        public string? SourceName { get; set; }

        /// <summary>
  /// URL link to the source article or document.
        /// </summary>
        public string? SourceUrl { get; set; }

   /// <summary>
        /// Date of the source (if available).
        /// </summary>
        public string? SourceDate { get; set; }

        /// <summary>
        /// Returns formatted insight with source for display.
        /// </summary>
        public string ToFormattedString()
     {
   if (string.IsNullOrWhiteSpace(SourceName) && string.IsNullOrWhiteSpace(SourceUrl))
          return Text;

            var source = !string.IsNullOrWhiteSpace(SourceName) ? SourceName : "Source";
            if (!string.IsNullOrWhiteSpace(SourceDate))
      source += $" ({SourceDate})";

            return Text;
        }

        /// <summary>
        /// Returns the source citation for display.
        /// </summary>
        public string GetSourceCitation()
     {
          if (string.IsNullOrWhiteSpace(SourceName) && string.IsNullOrWhiteSpace(SourceUrl))
            return string.Empty;

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(SourceName))
                parts.Add(SourceName);
            if (!string.IsNullOrWhiteSpace(SourceDate))
     parts.Add(SourceDate);

   return string.Join(", ", parts);
   }
    }

    /// <summary>
  /// Response model for AI-generated consultant insights.
    /// </summary>
    public class ConsultantInsightsAiResponse
    {
        /// <summary>
        /// Key regulatory and policy updates for the territories and industries.
      /// Each item includes source references.
/// </summary>
        public List<InsightItem> KeyDevelopmentsWithSources { get; set; } = new();

        /// <summary>
      /// Business opportunities and positioning strategies.
        /// Each item includes source references.
        /// </summary>
        public List<InsightItem> OpportunitiesWithSources { get; set; } = new();

    /// <summary>
        /// Risks and watch-outs to monitor.
        /// Each item includes source references.
        /// </summary>
      public List<InsightItem> WatchoutsWithSources { get; set; } = new();

        /// <summary>
        /// Specific recommended actions for the next period.
    /// Each item includes source references.
        /// </summary>
        public List<InsightItem> RecommendedActionsWithSources { get; set; } = new();

        /// <summary>
        /// Executive summary of the briefing.
     /// </summary>
        public string ExecutiveSummary { get; set; } = string.Empty;

     // Legacy properties for backward compatibility
        /// <summary>
        /// Key developments as plain strings (legacy support).
   /// </summary>
        public List<string> KeyDevelopments
        {
     get => KeyDevelopmentsWithSources.Select(i => i.Text).ToList();
    set => KeyDevelopmentsWithSources = value.Select(t => new InsightItem { Text = t }).ToList();
  }

        /// <summary>
   /// Opportunities as plain strings (legacy support).
/// </summary>
   public List<string> Opportunities
    {
    get => OpportunitiesWithSources.Select(i => i.Text).ToList();
set => OpportunitiesWithSources = value.Select(t => new InsightItem { Text = t }).ToList();
        }

 /// <summary>
    /// Watchouts as plain strings (legacy support).
        /// </summary>
        public List<string> Watchouts
   {
            get => WatchoutsWithSources.Select(i => i.Text).ToList();
  set => WatchoutsWithSources = value.Select(t => new InsightItem { Text = t }).ToList();
        }

        /// <summary>
        /// Recommended actions as plain strings (legacy support).
        /// </summary>
        public List<string> RecommendedActions
  {
            get => RecommendedActionsWithSources.Select(i => i.Text).ToList();
            set => RecommendedActionsWithSources = value.Select(t => new InsightItem { Text = t }).ToList();
      }
    }
}
