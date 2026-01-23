using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace News_Back_end.Models.SQLServer
{
    public enum Countries
    {
        Singapore,
        China
    }
    public enum Language
    {
        EN,
        ZH,
        Both
    }

    public enum Channels
    {
        Wechat,
        Email,
        Both
    }
    public enum Types
    {
        Local,
        Overseas
    }

    public class IndustryTag
    {
        public int IndustryTagId { get; set; }

        // English name
        [Required, MaxLength(200)]
        public string NameEN { get; set; } = null!;

        // Chinese name
        [Required, MaxLength(200)]
        public string NameZH { get; set; } = null!;

        // Optional: store both if needed
        public string? DescriptionEN { get; set; }
        public string? DescriptionZH { get; set; }

        public ICollection<Member> Members { get; set; } = new List<Member>();
    }

    public class InterestTag
    {
        public int InterestTagId { get; set; }

        [Required, MaxLength(200)]
        public string NameEN { get; set; } = null!;

        [Required, MaxLength(200)]
        public string NameZH { get; set; } = null!;

        public string? DescriptionEN { get; set; }
        public string? DescriptionZH { get; set; }

        public ICollection<Member> Members { get; set; } = new List<Member>();
    }


    public class Member
    {
        public int MemberId { get; set; }
        [MaxLength(100)]
        public string CompanyName { get; set; }
        [Required, MaxLength(50)]
        public string ContactPerson { get; set; }

        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }
        public string? WeChatWorkId { get; set; }
        [Required]
        public Countries Country { get; set; }
        // normalized many-to-many
        public ICollection<IndustryTag> IndustryTags { get; set; } = new List<IndustryTag>();
        public ICollection<InterestTag> Interests { get; set; } = new List<InterestTag>();
        public Language PreferredLanguage { get; set; }
        public Channels PreferredChannel { get; set; }
        public Types MembershipType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Notification preferences - stores comma-separated channels: "whatsapp,email,sms,inApp"
        public string? NotificationChannels { get; set; }

        // link to Identity user if this member can sign in
        public string? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        // Add these fields after NotificationChannels
        public string? NotificationFrequency { get; set; } // "immediate", "daily", "weekly"
        public string? NotificationLanguage { get; set; } // "EN", "ZH"
        public bool ApplyToAllTopics { get; set; } = true;
    }
}