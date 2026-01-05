using System.ComponentModel.DataAnnotations;

namespace News_Back_end.DTOs
{
 public class CreateInterestDTO
 {
 [Required, MaxLength(100)]
 public string Name { get; set; } = null!;
 }

 public class UpdateInterestDTO
 {
 [Required, MaxLength(100)]
 public string Name { get; set; } = null!;
 }

 public class InterestResponseDTO
 {
 public int InterestTagId { get; set; }
 public string Name { get; set; } = null!;
 }
}
