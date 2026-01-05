using Microsoft.AspNetCore.Identity;

namespace News_Back_end.Models.SQLServer
{
    public class ApplicationUser : IdentityUser
    {
        public string? Name { get; set; }
        public string? WeChatWorkId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? Lastlogin { get; set; }
        // One-to-one profile relation to Member
        public Member? Member { get; set; }
        public bool MustChangePassword { get; set; } = false;  // ADD THIS LINE
        public bool HasSelectedTopics { get; set; } = false; // Track if user has selected topics
        // Note: no explicit UserInterestTag navigation is required because Member <-> InterestTag uses EF many-to-many
    }
}
