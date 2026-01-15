using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;
using News_Back_end.Services;

namespace News_Back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Consultant,Admin")]
    public class ContentCreationController : ControllerBase
    {
        private readonly MyDBContext _db;
        private readonly IContentCreationService? _contentCreationService;

        public ContentCreationController(MyDBContext db, IContentCreationService? contentCreationService = null)
  {
          _db = db;
            _contentCreationService = contentCreationService;
        }

        // GET: api/contentcreation/active
        // Get all articles in "Active Articles" section
        [HttpGet("active")]
  public async Task<IActionResult> GetActiveArticles()
        {
     var activeArticles = await _db.ArticleLabels
      .Where(al => al.WorkflowStatus == "Active")
                .Include(al => al.NewsArticle)
                .Include(al => al.Summary)
       .OrderByDescending(al => al.AddedAt)
          .ToListAsync();

            var response = activeArticles.Select(al => new ArticleLabelResponseDTO
      {
     LabelId = al.LabelId,
          NewsId = al.NewsId,
       WorkflowStatus = al.WorkflowStatus,
       AddedAt = al.AddedAt,
       AddedBy = al.AddedBy,
       CompletedAt = al.CompletedAt,
    CompletedBy = al.CompletedBy,
   Notes = al.Notes,
        Article = al.NewsArticle == null ? null : new ArticleBasicInfoDTO
      {
    NewsArticleId = al.NewsArticle.NewsArticleId,
           Title = al.NewsArticle.Title,
     OriginalContent = al.NewsArticle.OriginalContent,
       TranslatedContent = al.NewsArticle.TranslatedContent,
       OriginalLanguage = al.NewsArticle.OriginalLanguage,
        TranslationLanguage = al.NewsArticle.TranslationLanguage,
      SourceURL = al.NewsArticle.SourceURL,
         PublishedAt = al.NewsArticle.PublishedAt,
      CrawledAt = al.NewsArticle.CrawledAt
      },
        Summary = al.Summary == null ? null : MapToSummaryResponseDTO(al.Summary)
     });

   return Ok(response);
    }

        // GET: api/contentcreation/summary/{newsId}
        // Get summary for a specific article by newsId
        [HttpGet("summary/{newsId}")]
        public async Task<IActionResult> GetSummaryByNewsId(int newsId)
        {
      var summary = await _db.Summaries.FirstOrDefaultAsync(s => s.NewsId == newsId);
 
   if (summary == null)
     return NotFound(new { message = "No summary found for this article" });
            
      return Ok(MapToSummaryResponseDTO(summary));
        }

        // GET: api/contentcreation/poster/{newsId}
        // Get saved poster path for a specific article
        [HttpGet("poster/{newsId}")]
        public async Task<IActionResult> GetPosterByNewsId(int newsId)
        {
       var summary = await _db.Summaries.FirstOrDefaultAsync(s => s.NewsId == newsId);
   
  if (summary == null || string.IsNullOrEmpty(summary.PdfPath))
       return NotFound(new { message = "No poster found for this article" });
 
      return Ok(new { pdfPath = summary.PdfPath });
        }

  // POST: api/contentcreation/save-poster
        // Save poster path to database
        [HttpPost("save-poster")]
        public async Task<IActionResult> SavePoster([FromBody] SavePosterDTO dto)
        {
    var article = await _db.NewsArticles.FindAsync(dto.NewsId);
 if (article == null)
        return NotFound(new { message = "Article not found" });

var summary = await _db.Summaries.FirstOrDefaultAsync(s => s.NewsId == dto.NewsId);
      if (summary == null)
         {
  summary = new Summary
     {
       NewsId = dto.NewsId,
   PdfPath = dto.PdfPath,
     PdfGeneratedBy = User?.Identity?.Name ?? "system",
  PdfGeneratedAt = DateTime.Now,
      Status = "draft",
      CreatedAt = DateTime.Now
      };
       _db.Summaries.Add(summary);
       }
            else
 {
     summary.PdfPath = dto.PdfPath;
       summary.PdfEditedBy = User?.Identity?.Name;
   summary.PdfEditedAt = DateTime.Now;
           summary.UpdatedAt = DateTime.Now;
            }

   await _db.SaveChangesAsync();

 return Ok(new { message = "Poster saved successfully", pdfPath = summary.PdfPath });
        }

        // GET: api/contentcreation/completed
        // Get all articles in "Completed Articles" section
        [HttpGet("completed")]
        public async Task<IActionResult> GetCompletedArticles()
        {
            var completedArticles = await _db.ArticleLabels
          .Where(al => al.WorkflowStatus == "Completed")
   .Include(al => al.NewsArticle)
  .Include(al => al.Summary)
    .OrderByDescending(al => al.CompletedAt)
     .ToListAsync();

     var response = completedArticles.Select(al => new ArticleLabelResponseDTO
     {
      LabelId = al.LabelId,
      NewsId = al.NewsId,
        WorkflowStatus = al.WorkflowStatus,
     AddedAt = al.AddedAt,
       AddedBy = al.AddedBy,
     CompletedAt = al.CompletedAt,
         CompletedBy = al.CompletedBy,
    Notes = al.Notes,
   Article = al.NewsArticle == null ? null : new ArticleBasicInfoDTO
    {
         NewsArticleId = al.NewsArticle.NewsArticleId,
            Title = al.NewsArticle.Title,
     OriginalContent = al.NewsArticle.OriginalContent,
    TranslatedContent = al.NewsArticle.TranslatedContent,
       OriginalLanguage = al.NewsArticle.OriginalLanguage,
    TranslationLanguage = al.NewsArticle.TranslationLanguage,
          SourceURL = al.NewsArticle.SourceURL,
           PublishedAt = al.NewsArticle.PublishedAt,
        CrawledAt = al.NewsArticle.CrawledAt
        },
       Summary = al.Summary == null ? null : MapToSummaryResponseDTO(al.Summary)
    });

            return Ok(response);
        }

        // POST: api/contentcreation/add
    // Add article to Content Creation workflow (becomes "Active Article")
      [HttpPost("add")]
     public async Task<IActionResult> AddToContentCreation([FromBody] AddToContentCreationDTO dto)
      {
    var article = await _db.NewsArticles.FindAsync(dto.NewsId);
   if (article == null)
       return NotFound(new { message = "Article not found" });

       // Check if already in workflow
   var existing = await _db.ArticleLabels.FirstOrDefaultAsync(al => al.NewsId == dto.NewsId);
       if (existing != null)
            return BadRequest(new { message = "Article is already in Content Creation workflow" });

            var articleLabel = new ArticleLabel
   {
       NewsId = dto.NewsId,
     WorkflowStatus = "Active",
     AddedAt = DateTime.Now,
    AddedBy = User?.Identity?.Name,
      Notes = dto.Notes
   };

            _db.ArticleLabels.Add(articleLabel);
            await _db.SaveChangesAsync();

     return Ok(new { message = "Article added to Content Creation", labelId = articleLabel.LabelId });
        }

        // PUT: api/contentcreation/complete/{labelId}
     // Move article from "Active" to "Completed"
        [HttpPut("complete/{labelId}")]
public async Task<IActionResult> MarkAsCompleted(int labelId, [FromBody] UpdateWorkflowStatusDTO dto)
     {
            var articleLabel = await _db.ArticleLabels.FindAsync(labelId);
       if (articleLabel == null)
     return NotFound(new { message = "Article not found in workflow" });

            if (articleLabel.WorkflowStatus == "Completed")
              return BadRequest(new { message = "Article is already completed" });

         articleLabel.WorkflowStatus = "Completed";
    articleLabel.CompletedAt = DateTime.Now;
            articleLabel.CompletedBy = User?.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
      articleLabel.Notes = dto.Notes;

      await _db.SaveChangesAsync();

            return Ok(new { message = "Article marked as completed" });
  }

        // POST: api/contentcreation/generate-summary
// Generate text summary using AI
 [HttpPost("generate-summary")]
public async Task<IActionResult> GenerateTextSummary([FromBody] GenerateTextSummaryDTO dto)
        {
            if (_contentCreationService == null)
       return StatusCode(503, new { message = "Content Creation service not configured" });

   var article = await _db.NewsArticles.FindAsync(dto.NewsId);
  if (article == null)
 return NotFound(new { message = "Article not found" });

    // Use translated content if available, otherwise original
        var contentToSummarize = article.TranslatedContent ?? article.OriginalContent;

            try
            {
      var summaryText = await _contentCreationService.GenerateTextSummaryAsync(contentToSummarize, dto.CustomPrompt);

        // Create or update Summary record
                var summary = await _db.Summaries.FirstOrDefaultAsync(s => s.NewsId == dto.NewsId);
     if (summary == null)
      {
  summary = new Summary
         {
        NewsId = dto.NewsId,
         SummaryText = summaryText,
        SummaryGeneratedBy = "GPT-3.5-turbo",
     SummaryGeneratedAt = DateTime.Now,
           Status = "draft",
     CreatedAt = DateTime.Now
   };
      _db.Summaries.Add(summary);
  }
         else
      {
       summary.SummaryText = summaryText;
  summary.SummaryGeneratedBy = "GPT-3.5-turbo";
         summary.SummaryGeneratedAt = DateTime.Now;
          summary.UpdatedAt = DateTime.Now;
   }

         await _db.SaveChangesAsync();

       // Update ArticleLabel with SummaryId if exists
    var articleLabel = await _db.ArticleLabels.FirstOrDefaultAsync(al => al.NewsId == dto.NewsId);
if (articleLabel != null && articleLabel.SummaryId == null)
                {
          articleLabel.SummaryId = summary.SummaryId;
           await _db.SaveChangesAsync();
        }

              return Ok(new { message = "Summary generated successfully", summary = MapToSummaryResponseDTO(summary) });
   }
  catch (Exception ex)
            {
             return StatusCode(500, new { message = "Failed to generate summary", error = ex.Message });
         }
        }

        // POST: api/contentcreation/generate-pdf
 // Generate PDF poster using AI
        [HttpPost("generate-pdf")]
        public async Task<IActionResult> GeneratePdfPoster([FromBody] GeneratePdfDTO dto)
        {
if (_contentCreationService == null)
                return StatusCode(503, new { message = "Content Creation service not configured" });

         var article = await _db.NewsArticles.FindAsync(dto.NewsId);
            if (article == null)
       return NotFound(new { message = "Article not found" });

 var contentToUse = article.TranslatedContent ?? article.OriginalContent;

try
    {
             var pdfPath = await _contentCreationService.GeneratePdfPosterAsync(contentToUse, article.Title, dto.Template, dto.CustomPrompt);

    var summary = await _db.Summaries.FirstOrDefaultAsync(s => s.NewsId == dto.NewsId);
      if (summary == null)
   {
         summary = new Summary
   {
       NewsId = dto.NewsId,
          PdfPath = pdfPath,
       PdfGeneratedBy = "GPT-3.5-turbo",
             PdfGeneratedAt = DateTime.Now,
           Status = "draft",
    CreatedAt = DateTime.Now
         };
           _db.Summaries.Add(summary);
         }
   else
   {
       summary.PdfPath = pdfPath;
        summary.PdfGeneratedBy = "GPT-3.5-turbo";
   summary.PdfGeneratedAt = DateTime.Now;
         summary.UpdatedAt = DateTime.Now;
    }

           await _db.SaveChangesAsync();

             return Ok(new { message = "PDF poster generated successfully", pdfPath, summary = MapToSummaryResponseDTO(summary) });
    }
       catch (Exception ex)
            {
         return StatusCode(500, new { message = "Failed to generate PDF", error = ex.Message });
    }
      }

        // POST: api/contentcreation/generate-ppt
        // Generate PPT slides using AI
    [HttpPost("generate-ppt")]
        public async Task<IActionResult> GeneratePptSlides([FromBody] GeneratePptDTO dto)
        {
          if (_contentCreationService == null)
        return StatusCode(503, new { message = "Content Creation service not configured" });

            var article = await _db.NewsArticles.FindAsync(dto.NewsId);
       if (article == null)
                return NotFound(new { message = "Article not found" });

            var contentToUse = article.TranslatedContent ?? article.OriginalContent;

   try
         {
       var pptPath = await _contentCreationService.GeneratePptSlidesAsync(
                    contentToUse, 
     article.Title, 
             dto.NumberOfSlides ?? 5, 
          dto.Template, 
          dto.CustomPrompt
  );

    var summary = await _db.Summaries.FirstOrDefaultAsync(s => s.NewsId == dto.NewsId);
         if (summary == null)
    {
     summary = new Summary
                {
   NewsId = dto.NewsId,
                  PptPath = pptPath,
      PptGeneratedBy = "GPT-3.5-turbo",
              PptGeneratedAt = DateTime.Now,
   Status = "draft",
      CreatedAt = DateTime.Now
        };
      _db.Summaries.Add(summary);
       }
    else
      {
   summary.PptPath = pptPath;
    summary.PptGeneratedBy = "GPT-3.5-turbo";
         summary.PptGeneratedAt = DateTime.Now;
     summary.UpdatedAt = DateTime.Now;
          }

     await _db.SaveChangesAsync();

     return Ok(new { message = "PPT slides generated successfully", pptPath, summary = MapToSummaryResponseDTO(summary) });
    }
         catch (Exception ex)
    {
  return StatusCode(500, new { message = "Failed to generate PPT", error = ex.Message });
   }
  }

        // PUT: api/contentcreation/edit-summary
        // Manually edit text summary
        [HttpPut("edit-summary")]
     public async Task<IActionResult> EditTextSummary([FromBody] EditTextSummaryDTO dto)
        {
   var summary = await _db.Summaries.FindAsync(dto.SummaryId);
            if (summary == null)
    return NotFound(new { message = "Summary not found" });

     summary.SummaryText = dto.SummaryText;
   summary.SummaryEditedBy = User?.Identity?.Name;
     summary.SummaryEditedAt = DateTime.Now;
     summary.UpdatedAt = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(dto.EditNotes))
   summary.EditNotes = dto.EditNotes;

 await _db.SaveChangesAsync();

            return Ok(new { message = "Summary updated successfully", summary = MapToSummaryResponseDTO(summary) });
        }

        // PUT: api/contentcreation/update-status
        // Update summary status (draft/reviewed/approved)
        [HttpPut("update-status")]
        public async Task<IActionResult> UpdateSummaryStatus([FromBody] UpdateSummaryStatusDTO dto)
        {
        var summary = await _db.Summaries.FindAsync(dto.SummaryId);
     if (summary == null)
          return NotFound(new { message = "Summary not found" });

       summary.Status = dto.Status;
            summary.UpdatedAt = DateTime.Now;

  if (!string.IsNullOrWhiteSpace(dto.EditNotes))
    summary.EditNotes = dto.EditNotes;

   await _db.SaveChangesAsync();

     return Ok(new { message = "Summary status updated", summary = MapToSummaryResponseDTO(summary) });
      }

        // DELETE: api/contentcreation/summary/{newsId}
        // Delete text summary for an article
        [HttpDelete("summary/{newsId}")]
        public async Task<IActionResult> DeleteSummary(int newsId)
        {
       var summary = await _db.Summaries.FirstOrDefaultAsync(s => s.NewsId == newsId);
    
            if (summary == null)
        return NotFound(new { message = "No summary found for this article" });

            // Check if summary text exists
            if (string.IsNullOrEmpty(summary.SummaryText))
         return NotFound(new { message = "No text summary found for this article" });

 // Clear the summary text (keep the record for PDF/PPT if they exist)
     summary.SummaryText = null;
  summary.SummaryGeneratedBy = null;
            summary.SummaryGeneratedAt = null;
summary.SummaryEditedBy = null;
            summary.SummaryEditedAt = null;
            summary.UpdatedAt = DateTime.Now;

         // If no other assets exist, we can't delete the Summary record 
            // because ArticleLabel might reference it - just leave it empty
    await _db.SaveChangesAsync();

 return Ok(new { message = "Summary deleted successfully" });
        }

        // DELETE: api/contentcreation/poster/{newsId}
        // Delete PDF poster for an article
        [HttpDelete("poster/{newsId}")]
        public async Task<IActionResult> DeletePoster(int newsId)
  {
            var summary = await _db.Summaries.FirstOrDefaultAsync(s => s.NewsId == newsId);
      
        if (summary == null || string.IsNullOrEmpty(summary.PdfPath))
    return NotFound(new { message = "No poster found for this article" });

            // Try to delete the actual file
 try
 {
      if (System.IO.File.Exists(summary.PdfPath))
     {
                    System.IO.File.Delete(summary.PdfPath);
}
            }
   catch (Exception ex)
         {
          Console.WriteLine($"Failed to delete poster file: {ex.Message}");
            }

    // Clear the poster fields
            summary.PdfPath = null;
      summary.PdfGeneratedBy = null;
          summary.PdfGeneratedAt = null;
            summary.PdfEditedBy = null;
            summary.PdfEditedAt = null;
            summary.UpdatedAt = DateTime.Now;
   
 await _db.SaveChangesAsync();

            return Ok(new { message = "Poster deleted successfully" });
        }

      // DELETE: api/contentcreation/ppt/{newsId}
     // Delete PPT slides for an article
        [HttpDelete("ppt/{newsId}")]
        public async Task<IActionResult> DeletePpt(int newsId)
        {
        var summary = await _db.Summaries.FirstOrDefaultAsync(s => s.NewsId == newsId);
   
            if (summary == null || string.IsNullOrEmpty(summary.PptPath))
     return NotFound(new { message = "No PPT found for this article" });

            // Try to delete the actual file
    try
   {
   if (System.IO.File.Exists(summary.PptPath))
      {
         System.IO.File.Delete(summary.PptPath);
     }
            }
            catch (Exception ex)
      {
             Console.WriteLine($"Failed to delete PPT file: {ex.Message}");
            }

            // Clear the PPT fields
        summary.PptPath = null;
       summary.PptGeneratedBy = null;
         summary.PptGeneratedAt = null;
 summary.PptEditedBy = null;
     summary.PptEditedAt = null;
  summary.UpdatedAt = DateTime.Now;
      
          await _db.SaveChangesAsync();

       return Ok(new { message = "PPT deleted successfully" });
        }

        // Helper method to map Summary to DTO
 private SummaryResponseDTO MapToSummaryResponseDTO(Summary summary)
     {
            return new SummaryResponseDTO
    {
        SummaryId = summary.SummaryId,
      NewsId = summary.NewsId,
  SummaryText = summary.SummaryText,
        SummaryGeneratedBy = summary.SummaryGeneratedBy,
        SummaryGeneratedAt = summary.SummaryGeneratedAt,
    SummaryEditedBy = summary.SummaryEditedBy,
       SummaryEditedAt = summary.SummaryEditedAt,
             PdfPath = summary.PdfPath,
         PdfGeneratedBy = summary.PdfGeneratedBy,
    PdfGeneratedAt = summary.PdfGeneratedAt,
   PdfEditedBy = summary.PdfEditedBy,
  PdfEditedAt = summary.PdfEditedAt,
      PptPath = summary.PptPath,
          PptGeneratedBy = summary.PptGeneratedBy,
          PptGeneratedAt = summary.PptGeneratedAt,
    PptEditedBy = summary.PptEditedBy,
          PptEditedAt = summary.PptEditedAt,
                EditNotes = summary.EditNotes,
     Status = summary.Status,
CreatedAt = summary.CreatedAt,
       UpdatedAt = summary.UpdatedAt
            };
        }
    }
}
