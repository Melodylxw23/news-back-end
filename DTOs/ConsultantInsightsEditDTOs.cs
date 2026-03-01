using News_Back_end.Services;

namespace News_Back_end.DTOs
{
    /// <summary>
    /// Represents a single insight item with source reference for the API.
    /// </summary>
    public class InsightItemDTO
    {
        public string Text { get; set; } = string.Empty;
        public string? SourceName { get; set; }
        public string? SourceUrl { get; set; }
        public string? SourceDate { get; set; }

        /// <summary>
        /// Creates DTO from service InsightItem.
        /// </summary>
        public static InsightItemDTO FromInsightItem(InsightItem item)
        {
            return new InsightItemDTO
            {
                Text = item.Text,
                SourceName = item.SourceName,
                SourceUrl = item.SourceUrl,
                SourceDate = item.SourceDate
            };
        }
    }

    /// <summary>
    /// Editable insights content that consultants can customize before sending.
    /// Includes source references for each insight.
    /// </summary>
    public class EditableConsultantInsightsDTO
    {
        public string ExecutiveSummary { get; set; } = string.Empty;

        /// <summary>
        /// Key developments with source references.
        /// </summary>
        public List<InsightItemDTO> KeyDevelopments { get; set; } = new();

        /// <summary>
        /// Opportunities with source references.
        /// </summary>
        public List<InsightItemDTO> Opportunities { get; set; } = new();

        /// <summary>
        /// Watch-outs with source references.
        /// </summary>
        public List<InsightItemDTO> Watchouts { get; set; } = new();

        /// <summary>
        /// Recommended actions with source references.
        /// </summary>
        public List<InsightItemDTO> RecommendedActions { get; set; } = new();
    }

    /// <summary>
    /// Request to save edited insights before sending.
    /// </summary>
    public class SaveEditedInsightsRequestDTO
    {
        public string ExecutiveSummary { get; set; } = string.Empty;
        public List<InsightItemDTO> KeyDevelopments { get; set; } = new();
        public List<InsightItemDTO> Opportunities { get; set; } = new();
        public List<InsightItemDTO> Watchouts { get; set; } = new();
        public List<InsightItemDTO> RecommendedActions { get; set; } = new();
    }

    /// <summary>
    /// Response containing editable insights ready for preview/sending.
    /// </summary>
    public class ConsultantInsightsEditPreviewDTO
    {
        public string Subject { get; set; } = string.Empty;
        public EditableConsultantInsightsDTO EditableContent { get; set; } = new();
        public DateTimeOffset GeneratedAtUtc { get; set; }
        public bool IsEdited { get; set; }
    }

    /// <summary>
    /// Request to send with edited content.
    /// Accepts InsightItemDTO for structured items or plain strings for backward compatibility.
    /// </summary>
    public class SendEditedInsightsRequestDTO
    {
        public string ExecutiveSummary { get; set; } = string.Empty;
        public List<InsightItemDTO> KeyDevelopments { get; set; } = new();
        public List<InsightItemDTO> Opportunities { get; set; } = new();
        public List<InsightItemDTO> Watchouts { get; set; } = new();
        public List<InsightItemDTO> RecommendedActions { get; set; } = new();
        public bool Force { get; set; }
    }
}
