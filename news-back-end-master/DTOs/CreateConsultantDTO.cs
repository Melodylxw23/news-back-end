using System.ComponentModel.DataAnnotations;

namespace News_Back_end.DTOs
{
    public class CreateConsultantDTO
    {
        [Required]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
