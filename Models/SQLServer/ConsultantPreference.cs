using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
 public enum ConsultantInsightsFrequency
 {
 Daily,
 Weekly
 }

 /// <summary>
 /// Stores a consultant's preferences for receiving China insights.
 /// Consultant is an Identity user with Role = Consultant.
 /// </summary>
 public class ConsultantPreference
 {
 public int ConsultantPreferenceId { get; set; }

 [Required]
 public string ConsultantUserId { get; set; } = string.Empty;

 public ApplicationUser ConsultantUser { get; set; } = null!;

 /// <summary>
 /// List of territories (regions) the consultant is interested in.
 /// Stored as JSON for flexibility.
 /// </summary>
 public string TerritoriesJson { get; set; } = "[]";

 /// <summary>
 /// List of industries the consultant is interested in.
 /// Stored as JSON for flexibility.
 /// </summary>
 public string IndustriesJson { get; set; } = "[]";

 /// <summary>
 /// Delivery frequency: Daily (Mon-Fri) or Weekly (Monday).
 /// </summary>
 public ConsultantInsightsFrequency Frequency { get; set; } = ConsultantInsightsFrequency.Daily;

 /// <summary>
 /// Destination email (defaults to consultant's login email).
 /// You can allow override but must be same as consultant's identity email to enforce "send to themselves".
 /// </summary>
 [MaxLength(320)]
 public string Email { get; set; } = string.Empty;

 /// <summary>
 /// Preferred delivery time in UTC, stored as minutes since midnight.
 /// Example:09:00 =>540
 /// </summary>
 public int PreferredTimeMinutesUtc { get; set; } =540;

 public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
 }
}
