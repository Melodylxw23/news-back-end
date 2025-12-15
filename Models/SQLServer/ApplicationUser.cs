using Microsoft.AspNetCore.Identity;

namespace News_Back_end.Models.SQLServer
{
    public class ApplicationUser : IdentityUser
    {
        public string? Name { get; set; }
        public string? WeChatWorkId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? Lastlogin { get; set; }
    }
}
