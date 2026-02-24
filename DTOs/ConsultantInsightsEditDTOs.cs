namespace News_Back_end.DTOs
{
    /// <summary>
    /// Editable insights content that consultants can customize before sending.
    /// </summary>
    public class EditableConsultantInsightsDTO
    {
        public string ExecutiveSummary { get; set; } = string.Empty;
        public List<string> KeyDevelopments { get; set; } = new();
  public List<string> Opportunities { get; set; } = new();
      public List<string> Watchouts { get; set; } = new();
        public List<string> RecommendedActions { get; set; } = new();
    }

    /// <summary>
    /// Request to save edited insights before sending.
    /// </summary>
    public class SaveEditedInsightsRequestDTO
    {
        public string ExecutiveSummary { get; set; } = string.Empty;
  public List<string> KeyDevelopments { get; set; } = new();
        public List<string> Opportunities { get; set; } = new();
        public List<string> Watchouts { get; set; } = new();
        public List<string> RecommendedActions { get; set; } = new();
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
    /// </summary>
    public class SendEditedInsightsRequestDTO
    {
   public string ExecutiveSummary { get; set; } = string.Empty;
        public List<string> KeyDevelopments { get; set; } = new();
        public List<string> Opportunities { get; set; } = new();
 public List<string> Watchouts { get; set; } = new();
  public List<string> RecommendedActions { get; set; } = new();
        public bool Force { get; set; }
    }
}
