using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
    public enum Countries
    { 
        Singapore,
        China
    }
    public enum IndustryTags
    { 
        Finance,
        Technology,
        Healthcare,
        Education,
        Retail,
        Manufacturing,
        Other
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
    public enum Type
    {
        Local, 
        Overseas
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
        public IndustryTags IndustryTag { get; set; }
        public Language PreferredLanguage { get; set; }
        public Channels PreferredChannel { get; set; }
        public Type MembershipType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

    }
}
