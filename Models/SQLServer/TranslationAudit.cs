using System;
using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
    public class TranslationAudit
    {
        [Key]
        public int Id { get; set; }
        public int NewsArticleId { get; set; }
        public string Action { get; set; } = null!; // "Saved" or "Approved"
        public string PerformedBy { get; set; } = null!;
        public DateTime PerformedAt { get; set; }
        public string? Details { get; set; }
    }
}
