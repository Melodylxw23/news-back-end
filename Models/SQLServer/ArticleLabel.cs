using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace News_Back_end.Models.SQLServer
{
    public class ArticleLabel
    {
        [Key]
        public int LabelId { get; set; }

        // Foreign key to NewsArticle
        [Required]
        public int NewsId { get; set; }

        // Navigation property
        [ForeignKey("NewsId")]
        public NewsArticle? NewsArticle { get; set; }

        // Foreign key to Summary (assets for this article)
        public int? SummaryId { get; set; }

        // Navigation property to Summary
        [ForeignKey("SummaryId")]
        public Summary? Summary { get; set; }

        // Workflow status: "Active" or "Completed"
        [Required]
        [MaxLength(20)]
        public string WorkflowStatus { get; set; } = "Active";

        // When article was added to Content Creation feature
        public DateTime AddedAt { get; set; } = DateTime.Now;

        // Who added this article to Content Creation
        [MaxLength(255)]
        public string? AddedBy { get; set; }

        // When moved to "Completed Articles"
        public DateTime? CompletedAt { get; set; }

        // Who marked it as completed
        [MaxLength(255)]
        public string? CompletedBy { get; set; }

        // General notes about this article in the workflow
        [Column(TypeName = "text")]
        public string? Notes { get; set; }
    }
}
