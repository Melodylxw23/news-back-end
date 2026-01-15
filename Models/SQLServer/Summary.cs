using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace News_Back_end.Models.SQLServer
{
    public class Summary
    {
        [Key]
        public int SummaryId { get; set; }

        // Foreign key to NewsArticle
        [Required]
        public int NewsId { get; set; }

        // Navigation property
        [ForeignKey("NewsId")]
        public NewsArticle? NewsArticle { get; set; }

        // ===== TEXT SUMMARY (ENGLISH) =====
        // Text summary in English
        [Column(TypeName = "text")]
        public string? SummaryText { get; set; }

        // AI model used for summary generation
        [MaxLength(255)]
        public string? SummaryGeneratedBy { get; set; }

        public DateTime? SummaryGeneratedAt { get; set; }

        // Who last edited the summary
        public string? SummaryEditedBy { get; set; }

        public DateTime? SummaryEditedAt { get; set; }

        // ===== PDF POSTER =====
        // Path to generated PDF poster file
        [MaxLength(255)]
        public string? PdfPath { get; set; }

        // AI model used for PDF generation
        [MaxLength(255)]
        public string? PdfGeneratedBy { get; set; }

        public DateTime? PdfGeneratedAt { get; set; }

        // Who last regenerated/edited the PDF
        public string? PdfEditedBy { get; set; }

        public DateTime? PdfEditedAt { get; set; }

        // ===== PPT SLIDES =====
        // Path to generated PPT slides file
        [MaxLength(255)]
        public string? PptPath { get; set; }

        // AI model used for PPT generation
        [MaxLength(255)]
        public string? PptGeneratedBy { get; set; }

        public DateTime? PptGeneratedAt { get; set; }

        // Who last regenerated/edited the PPT
        public string? PptEditedBy { get; set; }

        public DateTime? PptEditedAt { get; set; }

        // ===== OVERALL METADATA =====
        // Review comments or edit notes from consultant
        [Column(TypeName = "text")]
        public string? EditNotes { get; set; }

        // Overall status: draft, reviewed, approved
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "draft";

        // Record creation timestamp
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Last update timestamp (any field change)
        public DateTime? UpdatedAt { get; set; }
    }
}
