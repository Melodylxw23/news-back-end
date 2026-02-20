//using System.ComponentModel.DataAnnotations;
//using News_Back_end.Models.SQLServer; // Add this to access the Language enum

//namespace News_Back_end.DTOs
//{
//    public class SelectTopicsDTO
//    {
//        public List<int> InterestTagIds { get; set; } = new List<int>();
//        public List<string> CustomTopics { get; set; } = new List<string>();

//        [Required]
//        public Language PreferredLanguage { get; set; } = Language.EN; // Changed from string to Language enum

//        public bool NotifyNewArticles { get; set; } = true;
//    }
//}

using System.ComponentModel.DataAnnotations;
using News_Back_end.Models.SQLServer; // Add this to access the Language enum

namespace News_Back_end.DTOs
{
    public class SelectTopicsDTO
    {
        public List<int> InterestTagIds { get; set; } = new List<int>();
        public List<string> CustomTopics { get; set; } = new List<string>();

        [Required]
        public Language PreferredLanguage { get; set; } = Language.EN;

        public bool NotifyNewArticles { get; set; } = true;

        // Notification channels as comma-separated string: "whatsapp,email,sms,inApp"
        public string? NotificationChannels { get; set; }
    }
}