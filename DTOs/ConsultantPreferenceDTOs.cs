using System.ComponentModel.DataAnnotations;

namespace News_Back_end.DTOs
{
 public class ConsultantPreferenceDTO
 {
 public List<string> Territories { get; set; } = new();
 public List<string> Industries { get; set; } = new();
 public string Frequency { get; set; } = "daily"; // daily | weekly
 public string Email { get; set; } = string.Empty;
 public string PreferredTime { get; set; } = "09:00"; // HH:mm
 }

 public class ConsultantPreferenceUpsertDTO
 {
 [MinLength(1)]
 public List<string> Territories { get; set; } = new();

 [MinLength(1)]
 public List<string> Industries { get; set; } = new();

 [Required]
 [RegularExpression("^(daily|weekly)$", ErrorMessage = "Frequency must be 'daily' or 'weekly'")]
 public string Frequency { get; set; } = "daily";

 [Required]
 [EmailAddress]
 public string Email { get; set; } = string.Empty;

 [Required]
 [RegularExpression("^([01]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "PreferredTime must be in HH:mm format")]
 public string PreferredTime { get; set; } = "09:00";
 }
}
