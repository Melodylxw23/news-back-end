using System;

namespace News_Back_end.DTOs
{
    // ===== ArticleLabel DTOs =====
    
    /// <summary>
    /// Response DTO for articles in Content Creation workflow
    /// </summary>
    public class ArticleLabelResponseDTO
    {
   public int LabelId { get; set; }
        public int NewsId { get; set; }
  public string WorkflowStatus { get; set; } = null!;
        public DateTime AddedAt { get; set; }
        public string? AddedBy { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompletedBy { get; set; }
   public string? Notes { get; set; }
        
 // Nested article info (for display in sidebar)
        public ArticleBasicInfoDTO? Article { get; set; }
        
// Nested summary/assets info
  public SummaryResponseDTO? Summary { get; set; }
    }

 /// <summary>
    /// Request DTO to add article to Content Creation
    /// </summary>
    public class AddToContentCreationDTO
    {
     public int NewsId { get; set; }
        public string? Notes { get; set; }
    }

 /// <summary>
  /// Request DTO to update workflow status
    /// </summary>
    public class UpdateWorkflowStatusDTO
    {
        public int LabelId { get; set; }
        public string WorkflowStatus { get; set; } = null!; // "Active" or "Completed"
        public string? Notes { get; set; }
    }

    // ===== Summary/Assets DTOs =====

    /// <summary>
    /// Response DTO for summary and assets
    /// </summary>
    public class SummaryResponseDTO
    {
        public int SummaryId { get; set; }
        public int NewsId { get; set; }
        
        // Text Summary
        public string? SummaryText { get; set; }
        public string? SummaryGeneratedBy { get; set; }
        public DateTime? SummaryGeneratedAt { get; set; }
        public string? SummaryEditedBy { get; set; }
   public DateTime? SummaryEditedAt { get; set; }
        
        // PDF Poster
        public string? PdfPath { get; set; }
        public string? PdfGeneratedBy { get; set; }
        public DateTime? PdfGeneratedAt { get; set; }
        public string? PdfEditedBy { get; set; }
        public DateTime? PdfEditedAt { get; set; }
     
        // PPT Slides
   public string? PptPath { get; set; }
        public string? PptGeneratedBy { get; set; }
    public DateTime? PptGeneratedAt { get; set; }
        public string? PptEditedBy { get; set; }
      public DateTime? PptEditedAt { get; set; }
        
        // Overall metadata
        public string? EditNotes { get; set; }
   public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Request DTO to generate text summary
    /// </summary>
    public class GenerateTextSummaryDTO
    {
        public int NewsId { get; set; }
        public string? CustomPrompt { get; set; } // Optional: custom instructions for AI
    }

    /// <summary>
    /// Request DTO to edit text summary manually
    /// </summary>
    public class EditTextSummaryDTO
    {
      public int SummaryId { get; set; }
 public string SummaryText { get; set; } = null!;
        public string? EditNotes { get; set; }
    }

    /// <summary>
    /// Request DTO to generate PDF poster
    /// </summary>
    public class GeneratePdfDTO
    {
        public int NewsId { get; set; }
      public string? Template { get; set; } // Optional: template selection
        public string? CustomPrompt { get; set; }
    }

    /// <summary>
    /// Request DTO to generate PPT slides
    /// </summary>
    public class GeneratePptDTO
    {
    public int NewsId { get; set; }
     public string? Template { get; set; } // Optional: template selection
   public int? NumberOfSlides { get; set; } // Optional: how many slides
      public string? CustomPrompt { get; set; }
    }

    /// <summary>
  /// Request DTO to save poster path
    /// </summary>
    public class SavePosterDTO
    {
        public int NewsId { get; set; }
    public string PdfPath { get; set; } = null!;
    }

    /// <summary>
    /// Request DTO to update summary status
    /// </summary>
    public class UpdateSummaryStatusDTO
    {
        public int SummaryId { get; set; }
        public string Status { get; set; } = null!; // "draft", "reviewed", "approved"
        public string? EditNotes { get; set; }
    }

    // ===== Article Info DTOs =====

  /// <summary>
    /// Basic article info for display in Content Creation sidebar
/// </summary>
    public class ArticleBasicInfoDTO
    {
        public int NewsArticleId { get; set; }
        public string Title { get; set; } = null!;
     
        // For toggle between Chinese/English
        public string OriginalContent { get; set; } = null!; // Chinese
        public string? TranslatedContent { get; set; } // English
        public string OriginalLanguage { get; set; } = null!;
 public string? TranslationLanguage { get; set; }
     
        public string SourceURL { get; set; } = null!;
    public DateTime? PublishedAt { get; set; }
        public DateTime CrawledAt { get; set; }
    }

    /// <summary>
/// Detailed article info with all metadata
    /// </summary>
    public class ArticleDetailDTO
    {
        public int NewsArticleId { get; set; }
        public string Title { get; set; } = null!;
        public string OriginalContent { get; set; } = null!;
        public string? TranslatedContent { get; set; }
        public string OriginalLanguage { get; set; } = null!;
        public string? TranslationLanguage { get; set; }
        public string SourceURL { get; set; } = null!;
        public DateTime? PublishedAt { get; set; }
        public DateTime CrawledAt { get; set; }
    
        // NLP data
        public string? NLPKeywords { get; set; }
        public string? NamedEntities { get; set; }
        public double? SentimentScore { get; set; }
     
  // Source info
        public string? SourceName { get; set; }
    }
}
