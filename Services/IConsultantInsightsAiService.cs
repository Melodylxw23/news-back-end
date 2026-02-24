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
        /// <param name="territories">List of provinces/territories (e.g., "Suzhou", "Shanghai")</param>
        /// <param name="industries">List of focus industries (e.g., "F&B", "Technology")</param>
  /// <param name="frequency">Daily or Weekly insights</param>
        /// <param name="consultantName">Name of the consultant for personalization</param>
      /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task containing JSON string with insights data for email body generation</returns>
        Task<ConsultantInsightsAiResponse> GenerateInsightsAsync(
            List<string> territories,
            List<string> industries,
 string frequency,
   string? consultantName,
   CancellationToken cancellationToken);

 /// <summary>
        /// Generates insights for a consultant in a specified language.
        /// </summary>
    /// <param name="territories">List of provinces/territories (e.g., "Suzhou", "Shanghai")</param>
    /// <param name="industries">List of focus industries (e.g., "F&B", "Technology")</param>
      /// <param name="frequency">Daily or Weekly insights</param>
        /// <param name="consultantName">Name of the consultant for personalization</param>
        /// <param name="language">Target language: "en" (English) or "zh"/"chinese" (Simplified Chinese)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task containing insights response with localized content</returns>
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
        /// <param name="territories">List of provinces/territories</param>
     /// <param name="industries">List of focus industries</param>
        /// <param name="frequency">Daily or Weekly insights</param>
        /// <param name="consultantName">Name of the consultant for personalization</param>
        /// <param name="language">Target language: "en" or "zh"</param>
    /// <param name="previousInsights">Previous insights to exclude from generation</param>
   /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Fresh insights excluding previous content</returns>
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
/// Response model for AI-generated consultant insights.
    /// </summary>
    public class ConsultantInsightsAiResponse
    {
   /// <summary>
   /// Key regulatory and policy updates for the territories and industries.
        /// </summary>
        public List<string> KeyDevelopments { get; set; } = new();

        /// <summary>
        /// Business opportunities and positioning strategies.
  /// </summary>
        public List<string> Opportunities { get; set; } = new();

        /// <summary>
        /// Risks and watch-outs to monitor.
        /// </summary>
  public List<string> Watchouts { get; set; } = new();

/// <summary>
        /// Specific recommended actions for the next period.
        /// </summary>
        public List<string> RecommendedActions { get; set; } = new();

    /// <summary>
        /// Executive summary of the briefing.
        /// </summary>
        public string ExecutiveSummary { get; set; } = string.Empty;
    }
}
