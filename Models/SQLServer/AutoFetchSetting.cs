using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
 /// <summary>
 /// Per-user auto-fetch setting for the background crawler.
 /// </summary>
 public class AutoFetchSetting
 {
 [Key]
 public int Id { get; set; }

 [Required]
 public string ApplicationUserId { get; set; } = string.Empty;

 public ApplicationUser ApplicationUser { get; set; } = null!;

 public bool Enabled { get; set; } = false;

 /// <summary>
 /// Minimum delay between background crawl cycles.
 /// </summary>
 public int IntervalSeconds { get; set; } =300; //5 minutes default

 public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
 }
}
