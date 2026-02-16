using System.ComponentModel.DataAnnotations;
using News_Back_end.Models.SQLServer;

namespace News_Back_end.DTOs
{
    public class MemberDTOs
    {
        [Required, MaxLength(100)]
        public string CompanyName { get; set; }

        [Required, MaxLength(50)]
        public string ContactPerson { get; set; }

        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        public string? WeChatWorkId { get; set; }

        [Required]
        public Countries Country { get; set; }
        [Required]
        public int IndustryTagId { get; set; }

        public Language PreferredLanguage { get; set; } = Language.EN;

        public Channels PreferredChannel { get; set; } = Channels.Email;

        public Types MembershipType { get; set; } = Types.Local;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Password and ConfirmPassword does not match.")]
        public string ConfirmPassword { get; set; }
    }


    public class LinkMemberDTO
    {
        public int MemberId { get; set; }
        public string Email { get; set; } = null!;
    }
}
