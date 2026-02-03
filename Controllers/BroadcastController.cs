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
    [Authorize]
    public class BroadcastController : ControllerBase
    {
        private readonly MyDBContext _db;
     private readonly Services.IAiBroadcastService? _aiBroadcast;
        private readonly Services.IBroadcastSendingService _broadcastSending;

      public BroadcastController(MyDBContext db, Services.IAiBroadcastService? aiBroadcast = null, Services.IBroadcastSendingService? broadcastSending = null)
        {
    _db = db;
   _aiBroadcast = aiBroadcast;
      _broadcastSending = broadcastSending ?? throw new ArgumentNullException(nameof(broadcastSending));
        }

        /// <summary>
        /// Get all broadcasts with lightweight data for fast list loading
  /// </summary>
/// <returns>List of broadcasts with basic info and article counts only</returns>
     [HttpGet]
        public async Task<IActionResult> GetAll()
     {
   var items = await _db.BroadcastMessages
    .OrderByDescending(b => b.UpdatedAt)
     .Select(b => new BroadcastListItemDTO
            {
Id = b.Id,
          Title = b.Title,
      Subject = b.Subject,
          Channel = b.Channel,
   TargetAudience = b.TargetAudience,
           Status = b.Status,
           CreatedAt = b.CreatedAt,
         UpdatedAt = b.UpdatedAt,
  ScheduledSendAt = b.ScheduledSendAt,
            CreatedById = b.CreatedById,
SelectedArticlesCount = b.SelectedArticles.Count(),
             SelectedArticleIds = b.SelectedArticles.Select(a => a.PublicationDraftId).ToList()
                })
   .ToListAsync();

   return Ok(items);
        }

     /// <summary>
/// Get detailed broadcast information including full article data
        /// </summary>
        /// <param name="id">Broadcast ID</param>
 /// <returns>Complete broadcast details with article information</returns>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
   {
            var item = await _db.BroadcastMessages
     .Include(b => b.SelectedArticles)
    .ThenInclude(a => a.NewsArticle)
   .Include(b => b.SelectedArticles)
         .ThenInclude(a => a.IndustryTag)
         .Include(b => b.SelectedArticles)
           .ThenInclude(a => a.InterestTags)
        .FirstOrDefaultAsync(b => b.Id == id);

            if (item == null) return NotFound();

            var detailDTO = new BroadcastDetailDTO
      {
 Id = item.Id,
        Title = item.Title,
                Subject = item.Subject,
   Body = item.Body,
      Channel = item.Channel,
    TargetAudience = item.TargetAudience,
   Status = item.Status,
    CreatedAt = item.CreatedAt,
       UpdatedAt = item.UpdatedAt,
    ScheduledSendAt = item.ScheduledSendAt,
                CreatedById = item.CreatedById,
    SelectedArticles = item.SelectedArticles.Select(a => new PublishedArticleListDTO
             {
        PublicationDraftId = a.PublicationDraftId,
   Title = !string.IsNullOrWhiteSpace(a.NewsArticle?.TitleEN) ? a.NewsArticle.TitleEN : a.NewsArticle?.TitleZH ?? "",
       HeroImageUrl = a.HeroImageUrl,
          PublishedAt = a.PublishedAt,
  IndustryTagName = a.IndustryTag?.NameEN,
      InterestTagNames = a.InterestTags.Select(it => it.NameEN).ToList()
    }).ToList()
      };

     return Ok(detailDTO);
        }

        /// <summary>
        /// Get basic broadcast information without article details (fastest option)
        /// </summary>
        /// <param name="id">Broadcast ID</param>
        /// <returns>Basic broadcast info with article IDs only</returns>
        [HttpGet("{id:int}/basic")]
        public async Task<IActionResult> GetBasic(int id)
        {
    var item = await _db.BroadcastMessages
       .Select(b => new BroadcastListItemDTO
        {
          Id = b.Id,
   Title = b.Title,
         Subject = b.Subject,
        Channel = b.Channel,
       TargetAudience = b.TargetAudience,
          Status = b.Status,
        CreatedAt = b.CreatedAt,
     UpdatedAt = b.UpdatedAt,
   ScheduledSendAt = b.ScheduledSendAt,
     CreatedById = b.CreatedById,
      SelectedArticlesCount = b.SelectedArticles.Count(),
SelectedArticleIds = b.SelectedArticles.Select(a => a.PublicationDraftId).ToList()
                })
        .FirstOrDefaultAsync(b => b.Id == id);

       if (item == null) return NotFound();
     return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BroadcastCreateDTO dto)
      {
 var model = new BroadcastMessage
    {
        Title = dto.Title,
     Subject = dto.Subject,
        Body = dto.Body,
      Channel = dto.Channel,
            TargetAudience = dto.TargetAudience,
  Status = BroadcastStatus.Draft,
   ScheduledSendAt = dto.ScheduledSendAt,
                CreatedAt = DateTimeOffset.Now,
     UpdatedAt = DateTimeOffset.Now,
           CreatedById = User?.Identity?.Name
            };

      _db.BroadcastMessages.Add(model);
      await _db.SaveChangesAsync();

// Add selected articles if any
     if (dto.SelectedArticleIds?.Any() == true)
     {
          var selectedArticles = await _db.PublicationDrafts
            .Where(p => dto.SelectedArticleIds.Contains(p.PublicationDraftId) && p.IsPublished)
        .ToListAsync();

                foreach (var article in selectedArticles)
        {
      model.SelectedArticles.Add(article);
         }

     await _db.SaveChangesAsync();
            }

         return CreatedAtAction(nameof(Get), new { id = model.Id }, new BroadcastListItemDTO
            {
     Id = model.Id,
 Title = model.Title,
 Subject = model.Subject,
     Channel = model.Channel,
    TargetAudience = model.TargetAudience,
  Status = model.Status,
              CreatedAt = model.CreatedAt,
       UpdatedAt = model.UpdatedAt,
  ScheduledSendAt = model.ScheduledSendAt,
      CreatedById = model.CreatedById,
        SelectedArticlesCount = model.SelectedArticles.Count,
      SelectedArticleIds = model.SelectedArticles.Select(a => a.PublicationDraftId).ToList()
            });
        }

        [HttpPut("{id:int}")]
   public async Task<IActionResult> Update(int id, [FromBody] BroadcastUpdateDTO dto)
        {
            var existing = await _db.BroadcastMessages
    .Include(b => b.SelectedArticles)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (existing == null) return NotFound();

       // allow updates only when draft or scheduled
            if (existing.Status == BroadcastStatus.Sent || existing.Status == BroadcastStatus.Cancelled)
    return BadRequest("Cannot modify a sent or cancelled broadcast.");

  existing.Title = dto.Title;
       existing.Subject = dto.Subject;
    existing.Body = dto.Body;
   existing.Channel = dto.Channel;
 existing.TargetAudience = dto.TargetAudience;
    if (dto.Status.HasValue) existing.Status = dto.Status.Value;
     existing.ScheduledSendAt = dto.ScheduledSendAt;
   existing.UpdatedAt = DateTimeOffset.Now;

            // Update selected articles
 if (dto.SelectedArticleIds != null)
            {
      // Clear existing articles
    existing.SelectedArticles.Clear();

        // Add new selected articles
           if (dto.SelectedArticleIds.Any())
        {
   var selectedArticles = await _db.PublicationDrafts
       .Where(p => dto.SelectedArticleIds.Contains(p.PublicationDraftId) && p.IsPublished)
        .ToListAsync();

       foreach (var article in selectedArticles)
       {
               existing.SelectedArticles.Add(article);
   }
        }
            }

  await _db.SaveChangesAsync();
     return NoContent();
        }

        [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        {
 var existing = await _db.BroadcastMessages.FindAsync(id);
     if (existing == null) return NotFound();

            // allow delete only when draft or scheduled
            if (existing.Status == BroadcastStatus.Sent)
    return BadRequest("Cannot delete a sent broadcast.");

    _db.BroadcastMessages.Remove(existing);
            await _db.SaveChangesAsync();
       return NoContent();
     }

        [HttpPost("generate")]
   public async Task<IActionResult> Generate([FromBody] BroadcastGenerateRequestDTO req)
        {
            if (string.IsNullOrWhiteSpace(req.Prompt)) return BadRequest("Prompt is required.");

            if (_aiBroadcast == null)
            {
         return StatusCode(503, new { message = "AI generator not configured. Set OpenAIBroadcast:ApiKey in configuration." });
    }

         var promptBuilder = new System.Text.StringBuilder();
promptBuilder.AppendLine(req.Prompt.Trim());
            if (req.Channel.HasValue) promptBuilder.AppendLine($"Channel: {req.Channel.Value}");
            if (req.TargetAudience != BroadcastAudience.All) promptBuilder.AppendLine($"TargetAudience: {req.TargetAudience}");
   promptBuilder.AppendLine("IMPORTANT: Return JSON with keys: title, subject, body.");
            promptBuilder.AppendLine("- title: Keep title <=8 words");
 promptBuilder.AppendLine("- subject: Keep subject <=12 words");
       promptBuilder.AppendLine("- body: MUST be a detailed message of at least 150 words. This is the main content that will be sent to users.");
            promptBuilder.AppendLine("Ensure the body is comprehensive and informative.");

      var gen = await _aiBroadcast.GenerateAsync(promptBuilder.ToString(), req.Language ?? "en");

     string title = string.Empty, subject = string.Empty, body = string.Empty;

      // Try to extract JSON object if the model wrapped it in text
            var jsonCandidate = ExtractJsonObject(gen);
     var toParse = !string.IsNullOrWhiteSpace(jsonCandidate) ? jsonCandidate : gen;

            try
            {
          var doc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(toParse);
       if (doc.ValueKind == System.Text.Json.JsonValueKind.Object)
    {
             if (doc.TryGetProperty("title", out var t)) title = t.GetString() ?? string.Empty;
   if (doc.TryGetProperty("subject", out var s)) subject = s.GetString() ?? string.Empty;
        if (doc.TryGetProperty("body", out var b)) body = b.GetString() ?? string.Empty;
       }
            }
            catch (System.Text.Json.JsonException ex)
 {
              // Log the parsing error for debugging
          Console.WriteLine($"JSON parsing failed: {ex.Message}. Raw response: {gen}");

           // Fallback: try line-by-line parsing
 var lines = gen?.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
             if (lines.Length > 0) title = lines[0].Trim();
  if (lines.Length > 1) subject = lines[1].Trim();
            if (lines.Length > 2) body = string.Join("\n", lines.Skip(2)).Trim();
    }

            // Additional fallback: if body is still empty, try to extract from a different format
            if (string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(gen))
            {
        // Check if the response contains body content that wasn't parsed
                var lowerGen = gen.ToLower();
       if (lowerGen.Contains("body") && lowerGen.Contains(":"))
          {
        // Try to find body content after "body:"
         var bodyIndex = lowerGen.IndexOf("body");
     if (bodyIndex >= 0)
          {
           var afterBody = gen.Substring(bodyIndex);
      var colonIndex = afterBody.IndexOf(':');
     if (colonIndex >= 0 && colonIndex + 1 < afterBody.Length)
{
         var bodyContent = afterBody.Substring(colonIndex + 1).Trim();
            // Remove potential ending quotes or braces
    bodyContent = bodyContent.Trim('"', '}', ']', ',').Trim();
      if (!string.IsNullOrWhiteSpace(bodyContent))
        {
       body = bodyContent;
 }
         }
        }
           }
      }

 if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(subject) && string.IsNullOrWhiteSpace(body))
{
          return StatusCode(502, new { message = "AI did not produce usable output." });
   }

            var model = new BroadcastMessage
{
                Title = string.IsNullOrWhiteSpace(title) ? req.Prompt.Truncate(80) : title,
       Subject = string.IsNullOrWhiteSpace(subject) ? ("Update: " + req.Prompt.Truncate(120)) : subject,
      Body = string.IsNullOrWhiteSpace(body) ? req.Prompt : body,
   Channel = req.Channel ?? BroadcastChannel.Email,
             TargetAudience = req.TargetAudience,
   Status = BroadcastStatus.Draft,
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
           CreatedById = User?.Identity?.Name
      };

     _db.BroadcastMessages.Add(model);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = model.Id }, new BroadcastListItemDTO
            {
           Id = model.Id,
              Title = model.Title,
         Subject = model.Subject,
                Channel = model.Channel,
    TargetAudience = model.TargetAudience,
   Status = model.Status,
      CreatedAt = model.CreatedAt,
     UpdatedAt = model.UpdatedAt,
                ScheduledSendAt = model.ScheduledSendAt,
     CreatedById = model.CreatedById,
       SelectedArticlesCount = 0,
         SelectedArticleIds = new List<int>()
   });
     }

 /// <summary>
   /// Get all published articles available for broadcast selection
        /// </summary>
   /// <returns>List of published articles with basic information</returns>
        [HttpGet("published-articles")]
        public async Task<IActionResult> GetPublishedArticles()
        {
 var publishedArticles = await _db.PublicationDrafts
        .Include(p => p.NewsArticle)
  .Include(p => p.IndustryTag)
        .Include(p => p.InterestTags)
  .Where(p => p.IsPublished)
      .OrderByDescending(p => p.PublishedAt)
      .Select(p => new PublishedArticleListDTO
        {
        PublicationDraftId = p.PublicationDraftId,
     Title = !string.IsNullOrWhiteSpace(p.NewsArticle!.TitleEN) ? p.NewsArticle.TitleEN : p.NewsArticle.TitleZH,
       HeroImageUrl = p.HeroImageUrl,
        PublishedAt = p.PublishedAt,
        IndustryTagName = p.IndustryTag != null ? p.IndustryTag.NameEN : null,
    InterestTagNames = p.InterestTags.Select(it => it.NameEN).ToList()
  })
                .ToListAsync();

      return Ok(publishedArticles);
        }

      /// <summary>
        /// Get published articles filtered by industry and/or interest tags
        /// </summary>
        /// <param name="industryTagId">Optional industry tag ID to filter by</param>
        /// <param name="interestTagIds">Optional list of interest tag IDs to filter by</param>
        /// <returns>Filtered list of published articles</returns>
        [HttpGet("published-articles/filter")]
        public async Task<IActionResult> GetPublishedArticlesFiltered([FromQuery] int? industryTagId, [FromQuery] List<int>? interestTagIds)
     {
  var query = _db.PublicationDrafts
                .Include(p => p.NewsArticle)
     .Include(p => p.IndustryTag)
                .Include(p => p.InterestTags)
  .Where(p => p.IsPublished);

            if (industryTagId.HasValue)
        {
                query = query.Where(p => p.IndustryTagId == industryTagId.Value);
      }

   if (interestTagIds?.Any() == true)
 {
                query = query.Where(p => p.InterestTags.Any(it => interestTagIds.Contains(it.InterestTagId)));
  }

         var publishedArticles = await query
       .OrderByDescending(p => p.PublishedAt)
       .Select(p => new PublishedArticleListDTO
      {
           PublicationDraftId = p.PublicationDraftId,
        Title = !string.IsNullOrWhiteSpace(p.NewsArticle!.TitleEN) ? p.NewsArticle.TitleEN : p.NewsArticle.TitleZH,
 HeroImageUrl = p.HeroImageUrl,
       PublishedAt = p.PublishedAt,
        IndustryTagName = p.IndustryTag != null ? p.IndustryTag.NameEN : null,
         InterestTagNames = p.InterestTags.Select(it => it.NameEN).ToList()
         })
       .ToListAsync();

   return Ok(publishedArticles);
        }

        /// <summary>
        /// Get available industry and interest tags for filtering articles
        /// </summary>
        /// <returns>Available tags grouped by type</returns>
    [HttpGet("tags")]
        public async Task<IActionResult> GetAvailableTags()
        {
            var industryTags = await _db.IndustryTags
       .Select(it => new { Id = it.IndustryTagId, Name = it.NameEN, Type = "Industry" })
 .ToListAsync();

            var interestTags = await _db.InterestTags
                .Select(it => new { Id = it.InterestTagId, Name = it.NameEN, Type = "Interest" })
        .ToListAsync();

        var allTags = new
     {
            IndustryTags = industryTags,
  InterestTags = interestTags
            };

         return Ok(allTags);
        }

        /// <summary>
        /// Preview the list of recipients who would receive this broadcast
        /// </summary>
        /// <param name="broadcastId">Broadcast ID to preview</param>
        /// <returns>List of eligible recipients</returns>
        [HttpPost("preview-recipients")]
        public async Task<IActionResult> PreviewRecipients([FromBody] PreviewRecipientsRequestDTO request)
        {
      try
            {
   var recipients = await _broadcastSending.GetEligibleRecipientsAsync(request.BroadcastId);
       
      return Ok(new
     {
        BroadcastId = request.BroadcastId,
       TotalRecipients = recipients.Count,
       Recipients = recipients
 });
         }
   catch (ArgumentException ex)
            {
         return NotFound(new { message = ex.Message });
      }
         catch (Exception ex)
    {
         return StatusCode(500, new { message = "Error previewing recipients: " + ex.Message });
            }
        }

        /// <summary>
   /// Send the broadcast to eligible members
     /// </summary>
     /// <param name="request">Send broadcast request</param>
        /// <returns>Send result with statistics</returns>
        [HttpPost("send")]
        public async Task<IActionResult> SendBroadcast([FromBody] SendBroadcastRequestDTO request)
     {
    try
            {
    // Verify broadcast exists and can be sent
    var broadcast = await _db.BroadcastMessages.FindAsync(request.BroadcastId);
      if (broadcast == null)
     return NotFound(new { message = "Broadcast not found" });

     if (broadcast.Status == BroadcastStatus.Sent)
      return BadRequest(new { message = "Broadcast has already been sent" });

        if (!request.ConfirmSend)
  return BadRequest(new { message = "Send confirmation required" });

    // Send the broadcast
         var result = await _broadcastSending.SendBroadcastAsync(request.BroadcastId);
      
     if (result.SuccessfulSends == 0 && result.Errors.Any())
    {
      return BadRequest(new 
       {
       message = "Failed to send broadcast",
         errors = result.Errors,
            result = result
       });
     }

      return Ok(new
    {
          message = $"Broadcast sent successfully to {result.SuccessfulSends} recipients",
 result = result
          });
   }
         catch (ArgumentException ex)
  {
      return NotFound(new { message = ex.Message });
}
  catch (InvalidOperationException ex)
   {
      return BadRequest(new { message = ex.Message });
}
            catch (Exception ex)
     {
      return StatusCode(500, new { message = "Error sending broadcast: " + ex.Message });
      }
    }

     /// <summary>
        /// Get broadcast sending statistics
        /// </summary>
        /// <param name="broadcastId">Broadcast ID</param>
        /// <returns>Broadcast statistics and recipient information</returns>
        [HttpGet("{id:int}/statistics")]
        public async Task<IActionResult> GetBroadcastStatistics(int id)
        {
  try
            {
        var broadcast = await _db.BroadcastMessages.FindAsync(id);
        if (broadcast == null)
        return NotFound(new { message = "Broadcast not found" });

        var recipients = await _broadcastSending.GetEligibleRecipientsAsync(id);
       
        return Ok(new
         {
      BroadcastId = id,
        Title = broadcast.Title,
        Status = broadcast.Status.ToString(),
         TotalEligibleRecipients = recipients.Count,
         Recipients = recipients.Select(r => new
            {
  r.MemberId,
           r.Email,
     r.ContactPerson,
  r.CompanyName,
r.IndustryTags,
      r.InterestTags
        }),
        CreatedAt = broadcast.CreatedAt,
  UpdatedAt = broadcast.UpdatedAt,
      ScheduledSendAt = broadcast.ScheduledSendAt
         });
    }
   catch (Exception ex)
   {
 return StatusCode(500, new { message = "Error getting statistics: " + ex.Message });
     }
        }

   private static string? ExtractJsonObject(string? s)
    {
            if (string.IsNullOrWhiteSpace(s)) return null;
     var first = s.IndexOf('{');
   var last = s.LastIndexOf('}');
       if (first >= 0 && last > first)
 {
      var candidate = s.Substring(first, last - first + 1);
        // quick validity check
try
                {
 System.Text.Json.JsonDocument.Parse(candidate);
   return candidate;
}
      catch
      {
 return null;
              }
    }
   return null;
 }
    }

    internal static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength)
   {
        if (string.IsNullOrEmpty(value)) return value;
return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }
    }
}