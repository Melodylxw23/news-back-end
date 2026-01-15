using System.Threading.Tasks;

namespace News_Back_end.Services
{
    /// <summary>
    /// Service interface for Content Creation feature - AI-powered asset generation
    /// </summary>
    public interface IContentCreationService
    {
 /// <summary>
      /// Generate a text summary for an article using AI
        /// </summary>
    /// <param name="articleContent">The article content to summarize</param>
        /// <param name="customPrompt">Optional custom instructions for AI</param>
        /// <returns>Generated summary text</returns>
        Task<string> GenerateTextSummaryAsync(string articleContent, string? customPrompt = null);

        /// <summary>
 /// Generate a PDF poster/infographic for an article using AI
        /// </summary>
/// <param name="articleContent">The article content</param>
        /// <param name="articleTitle">The article title</param>
        /// <param name="template">Optional template selection</param>
    /// <param name="customPrompt">Optional custom instructions</param>
     /// <returns>Path to the generated PDF file</returns>
Task<string> GeneratePdfPosterAsync(string articleContent, string articleTitle, string? template = null, string? customPrompt = null);

   /// <summary>
   /// Generate PPT slides for an article using AI
        /// </summary>
        /// <param name="articleContent">The article content</param>
/// <param name="articleTitle">The article title</param>
        /// <param name="numberOfSlides">Number of slides to generate</param>
  /// <param name="template">Optional template selection</param>
        /// <param name="customPrompt">Optional custom instructions</param>
        /// <returns>Path to the generated PPT file</returns>
    Task<string> GeneratePptSlidesAsync(string articleContent, string articleTitle, int numberOfSlides = 5, string? template = null, string? customPrompt = null);
    }
}
