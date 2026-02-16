using System.ComponentModel.DataAnnotations;

namespace News_Back_end.DTOs
{
 public class CreateInterestDTO
 {
[Required, MaxLength(100)]
 public string NameEN { get; set; } = null!;
[Required, MaxLength(100)]
 public string NameZH { get; set; } = null!;
 }

 public class UpdateInterestDTO
 {
[Required, MaxLength(100)]
 public string NameEN { get; set; } = null!;
[Required, MaxLength(100)]
 public string NameZH { get; set; } = null!;
 }

 public class InterestResponseDTO
 {
 public int InterestTagId { get; set; }
 public string NameEN { get; set; } = null!;
 public string NameZH { get; set; } = null!;
 }
}
