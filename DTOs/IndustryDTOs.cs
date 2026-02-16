using System.ComponentModel.DataAnnotations;

namespace News_Back_end.DTOs
{
 public class CreateIndustryDTO
 {
[Required, MaxLength(100)]
 public string NameEN { get; set; } = null!;
[Required, MaxLength(100)]
 public string NameZH { get; set; } = null!;
 }

 public class UpdateIndustryDTO
 {
[Required, MaxLength(100)]
 public string NameEN { get; set; } = null!;
[Required, MaxLength(100)]
 public string NameZH { get; set; } = null!;
 }

    public class IndustryResponseDTO
    {
        public int IndustryTagId { get; set; }
        public string NameEN { get; set; } = null!;
        public string NameZH { get; set; } = null!;
    }
}
