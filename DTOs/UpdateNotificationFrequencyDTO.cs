namespace News_Back_end.DTOs
{
    public class UpdateNotificationFrequencyDTO
    {
        public string? NotificationFrequency { get; set; } // "immediate", "daily", "weekly"
        public string? NotificationLanguage { get; set; } // "EN", "ZH"
        public bool ApplyToAllTopics { get; set; } = true;
    }
}