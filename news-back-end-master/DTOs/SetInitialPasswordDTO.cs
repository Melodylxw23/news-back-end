using System.ComponentModel.DataAnnotations;

namespace News_Back_end.DTOs
{
    public class SetInitialPasswordDTO
    {
        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; }

        [Required]
        public string ConfirmPassword { get; set; }
    }
}