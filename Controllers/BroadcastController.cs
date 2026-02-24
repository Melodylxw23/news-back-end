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
private readonly Services.ITranslationService? _translationService;

 public BroadcastController(
 MyDBContext db, 
     Services.IAiBroadcastService? aiBroadcast = null, 
 Services.IBroadcastSendingService? broadcastSending = null,
    Services.ITranslationService? translationService = null)
  {
      _db = db;
       _aiBroadcast = aiBroadcast;
     _broadcastSending = broadcastSending ?? throw new ArgumentNullException(nameof(broadcastSending));
        _translationService = translationService;
 }

        /// <summary>
        /// Get all broadcasts with lightweight data for fast list loading
        /// </summary>
        /// <returns>List of broadcasts with basic info and article counts only</returns>
        [HttpGet]
      public async Task<IActionResult> GetAll()
  {
            var items = await _db.BroadcastMessages
 .AsNoTracking()
         .Include(b => b.SelectedArticles) // Need to include for count
         .OrderByDescending(b => b.UpdatedAt)
  .ToListAsync();

      var result = items.Select(b => new BroadcastListItemDTO
     {
        Id = b.Id,
                Title = b.Title,
         Subject = b.Subject,
       Channel = b.Channel,
      TargetAudience = b.TargetAudience,
Language = b.Language,
     Status = b.Status,
        CreatedAt = b.CreatedAt,
   UpdatedAt = b.UpdatedAt,
    ScheduledSendAt = b.ScheduledSendAt,
        CreatedById = b.CreatedById,
         SelectedArticlesCount = b.SelectedArticles.Count,
   SelectedArticleIds = b.SelectedArticles.Select(a => a.PublicationDraftId).ToList(),
            SelectedInterestTagIds = b.SelectedInterestTagIds, // This will use the computed property after loading
          SelectedIndustryTagIds = b.SelectedIndustryTagIds,  // This will use the computed property after loading
     HasChineseTranslation = !string.IsNullOrWhiteSpace(b.BodyZH)
     }).ToList();

return Ok(result);
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
         .AsNoTracking()
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
         TitleZH = item.TitleZH,
    SubjectZH = item.SubjectZH,
BodyZH = item.BodyZH,
      Channel = item.Channel,
       TargetAudience = item.TargetAudience,
        Language = item.Language,
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
        InterestTagNames = a.InterestTags?.Select(it => it.NameEN).ToList() ?? new List<string>()
   }).ToList(),
     SelectedInterestTagIds = item.SelectedInterestTagIds,
 SelectedIndustryTagIds = item.SelectedIndustryTagIds
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
                .AsNoTracking()
         .Include(b => b.SelectedArticles) // Need to include for count and IDs
      .FirstOrDefaultAsync(b => b.Id == id);

    if (item == null) return NotFound();

   var result = new BroadcastListItemDTO
       {
         Id = item.Id,
  Title = item.Title,
     Subject = item.Subject,
   Channel = item.Channel,
TargetAudience = item.TargetAudience,
 Language = item.Language,
    Status = item.Status,
            CreatedAt = item.CreatedAt,
      UpdatedAt = item.UpdatedAt,
     ScheduledSendAt = item.ScheduledSendAt,
   CreatedById = item.CreatedById,
     SelectedArticlesCount = item.SelectedArticles.Count,
   SelectedArticleIds = item.SelectedArticles.Select(a => a.PublicationDraftId).ToList(),
     SelectedInterestTagIds = item.SelectedInterestTagIds,
          SelectedIndustryTagIds = item.SelectedIndustryTagIds,
   HasChineseTranslation = !string.IsNullOrWhiteSpace(item.BodyZH)
};

      return Ok(result);
        }

        /// <summary>
   /// Get all published articles available for broadcast selection
    /// </summary>
  /// <returns>List of published articles</returns>
        [HttpGet("published-articles")]
        public async Task<IActionResult> GetPublishedArticles()
        {
            var articles = await _db.PublicationDrafts
     .AsNoTracking()
  .Where(p => p.IsPublished)
                .Include(p => p.NewsArticle)
                .Include(p => p.IndustryTag)
          .Include(p => p.InterestTags)
       .OrderByDescending(p => p.PublishedAt)
                .ToListAsync();

       var result = articles.Select(a => new PublishedArticleListDTO
            {
        PublicationDraftId = a.PublicationDraftId,
  Title = !string.IsNullOrWhiteSpace(a.NewsArticle?.TitleEN) ? a.NewsArticle.TitleEN : a.NewsArticle?.TitleZH ?? "",
     HeroImageUrl = a.HeroImageUrl,
       PublishedAt = a.PublishedAt,
      IndustryTagName = a.IndustryTag?.NameEN,
           InterestTagNames = a.InterestTags?.Select(it => it.NameEN).ToList() ?? new List<string>()
            }).ToList();

return Ok(result);
        }

      /// <summary>
        /// Get available tags for broadcast targeting
     /// </summary>
  /// <returns>Available interest and industry tags</returns>
        [HttpGet("tags")]
    public async Task<IActionResult> GetAvailableTags()
   {
    var interestTags = await _db.InterestTags
       .AsNoTracking()
           .Select(t => new { Id = t.InterestTagId, Name = t.NameEN, Type = "Interest" })
   .ToListAsync();

        var industryTags = await _db.IndustryTags
    .AsNoTracking()
      .Select(t => new { Id = t.IndustryTagId, Name = t.NameEN, Type = "Industry" })
         .ToListAsync();

            var result = new
   {
      InterestTags = interestTags,
                IndustryTags = industryTags
        };

return Ok(result);
        }

     /// <summary>
        /// Create a new broadcast entry
  /// </summary>
        /// <param name="dto">Broadcast data to create</param>
  /// <returns>Action result including the created broadcast data</returns>
 [HttpPost]
  public async Task<IActionResult> Create([FromBody] BroadcastCreateDTO dto)
    {
            // Validate required fields
   if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest("Title is required.");
            if (string.IsNullOrWhiteSpace(dto.Subject))
return BadRequest("Subject is required.");
         if (string.IsNullOrWhiteSpace(dto.Body))
                return BadRequest("Body is required.");

            var model = new BroadcastMessage
       {
             Title = dto.Title,
     Subject = dto.Subject,
     Body = dto.Body,
            Channel = dto.Channel,
                TargetAudience = dto.TargetAudience,
    Language = dto.Language,
   Status = BroadcastStatus.Draft,
     ScheduledSendAt = dto.ScheduledSendAt,
 CreatedAt = DateTimeOffset.Now,
UpdatedAt = DateTimeOffset.Now,
         CreatedById = User?.Identity?.Name,
    SelectedInterestTagIds = dto.SelectedInterestTagIds ?? new List<int>(),
 SelectedIndustryTagIds = dto.SelectedIndustryTagIds ?? new List<int>()
     };

     try
     {
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
       Language = model.Language,
 Status = model.Status,
       CreatedAt = model.CreatedAt,
         UpdatedAt = model.UpdatedAt,
          ScheduledSendAt = model.ScheduledSendAt,
  CreatedById = model.CreatedById,
     SelectedArticlesCount = model.SelectedArticles.Count,
      SelectedArticleIds = model.SelectedArticles.Select(a => a.PublicationDraftId).ToList(),
 SelectedInterestTagIds = model.SelectedInterestTagIds,
   SelectedIndustryTagIds = model.SelectedIndustryTagIds,
   HasChineseTranslation = !string.IsNullOrWhiteSpace(model.BodyZH)
  });
  }
            catch (Exception ex)
            {
             return StatusCode(500, new { message = "Failed to create broadcast", error = ex.Message });
}
        }

        /// <summary>
        /// Update an existing broadcast entry
        /// </summary>
        /// <param name="id">Broadcast ID to update</param>
        /// <param name="dto">Updated broadcast data</param>
        /// <returns>Action result including the updated broadcast data</returns>
        [HttpPut("{id:int}")]
 public async Task<IActionResult> Update(int id, [FromBody] BroadcastUpdateDTO dto)
        {
            // Validate required fields
if (string.IsNullOrWhiteSpace(dto.Title))
             return BadRequest("Title is required.");
        if (string.IsNullOrWhiteSpace(dto.Subject))
       return BadRequest("Subject is required.");
            if (string.IsNullOrWhiteSpace(dto.Body))
              return BadRequest("Body is required.");

            var existing = await _db.BroadcastMessages
             .Include(b => b.SelectedArticles)
      .ThenInclude(a => a.NewsArticle)
 .Include(b => b.SelectedArticles)
      .ThenInclude(a => a.IndustryTag)
       .Include(b => b.SelectedArticles)
        .ThenInclude(a => a.InterestTags)
          .FirstOrDefaultAsync(b => b.Id == id);
            
    if (existing == null) return NotFound();

   // allow updates only when draft or scheduled
     if (existing.Status == BroadcastStatus.Sent || existing.Status == BroadcastStatus.Cancelled)
         return BadRequest("Cannot modify a sent or cancelled broadcast.");

     try
     {
     existing.Title = dto.Title;
              existing.Subject = dto.Subject;
      existing.Body = dto.Body;
  existing.Channel = dto.Channel;
   existing.TargetAudience = dto.TargetAudience;
existing.Language = dto.Language;
        if (dto.Status.HasValue) existing.Status = dto.Status.Value;
    existing.ScheduledSendAt = dto.ScheduledSendAt;
  existing.UpdatedAt = DateTimeOffset.Now;

   // Update tag selections
        existing.SelectedInterestTagIds = dto.SelectedInterestTagIds ?? new List<int>();
                existing.SelectedIndustryTagIds = dto.SelectedIndustryTagIds ?? new List<int>();

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

      // Reload to ensure all navigation properties are populated
  await _db.Entry(existing)
        .Collection(b => b.SelectedArticles)
  .LoadAsync();

    foreach (var article in existing.SelectedArticles)
   {
        await _db.Entry(article)
                  .Reference(a => a.NewsArticle)
            .LoadAsync();
            await _db.Entry(article)
                  .Reference(a => a.IndustryTag)
            .LoadAsync();
          await _db.Entry(article)
        .Collection(a => a.InterestTags)
   .LoadAsync();
   }

                // Return updated broadcast with tags
    var updatedDTO = new BroadcastDetailDTO
  {
  Id = existing.Id,
Title = existing.Title,
                    Subject = existing.Subject,
             Body = existing.Body,
         Channel = existing.Channel,
      TargetAudience = existing.TargetAudience,
        Status = existing.Status,
             CreatedAt = existing.CreatedAt,
              UpdatedAt = existing.UpdatedAt,
     ScheduledSendAt = existing.ScheduledSendAt,
         CreatedById = existing.CreatedById,
         SelectedArticles = existing.SelectedArticles.Select(a => new PublishedArticleListDTO
    {
    PublicationDraftId = a.PublicationDraftId,
      Title = !string.IsNullOrWhiteSpace(a.NewsArticle?.TitleEN) ? a.NewsArticle.TitleEN : a.NewsArticle?.TitleZH ?? "",
            HeroImageUrl = a.HeroImageUrl,
      PublishedAt = a.PublishedAt,
         IndustryTagName = a.IndustryTag?.NameEN,
  InterestTagNames = a.InterestTags?.Select(it => it.NameEN).ToList() ?? new List<string>()
     }).ToList(),
                    SelectedInterestTagIds = existing.SelectedInterestTagIds,
         SelectedIndustryTagIds = existing.SelectedIndustryTagIds
    };

    return Ok(updatedDTO);
         }
            catch (Exception ex)
          {
         return StatusCode(500, new { message = "Failed to update broadcast", error = ex.Message });
    }
        }

        /// <summary>
        /// Delete a broadcast entry
        /// </summary>
        /// <param name="id">Broadcast ID to delete</param>
     /// <returns>Action result</returns>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
  var existing = await _db.BroadcastMessages.FindAsync(id);
          if (existing == null) return NotFound();

            // allow delete only when draft or scheduled
            if (existing.Status == BroadcastStatus.Sent)
  return BadRequest("Cannot delete a sent broadcast.");

         try
        {
                _db.BroadcastMessages.Remove(existing);
    await _db.SaveChangesAsync();
     return NoContent();
         }
 catch (Exception ex)
   {
            return StatusCode(500, new { message = "Failed to delete broadcast", error = ex.Message });
            }
     }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] BroadcastGenerateRequestDTO req)
        {
            if (string.IsNullOrWhiteSpace(req.Prompt)) 
         return BadRequest("Prompt is required.");

         if (_aiBroadcast == null)
            {
            return StatusCode(503, new { message = "AI generator not configured. Set OpenAIBroadcast:ApiKey in configuration." });
            }

            // Determine language for generation
    var language = req.Language?.ToLower() ?? "en";
          var isChineseLanguage = language == "zh" || language == "chinese" || language == "zh-cn" || language == "zh-tw";

var promptBuilder = new System.Text.StringBuilder();
            promptBuilder.AppendLine(req.Prompt.Trim());
    
   if (req.Channel.HasValue) 
                promptBuilder.AppendLine($"Channel: {req.Channel.Value}");
        
  if (req.TargetAudience != BroadcastAudience.All) 
  promptBuilder.AppendLine($"TargetAudience: {req.TargetAudience}");

  // Add language-specific instructions
            if (isChineseLanguage)
    {
          promptBuilder.AppendLine();
       promptBuilder.AppendLine("重要：返回JSON格式，包含以下键：title（标题）、subject（主题）、body（正文）。");
           promptBuilder.AppendLine("- title（标题）：保持在8个词以内");
          promptBuilder.AppendLine("- subject（主题）：保持在12个词以内");
      promptBuilder.AppendLine("- body（正文）：必须是至少150字的详细消息。这是将发送给用户的主要内容。");
       promptBuilder.AppendLine("确保正文内容全面且信息丰富。所有内容必须使用简体中文撰写。");
         }
            else
 {
        promptBuilder.AppendLine();
  promptBuilder.AppendLine("IMPORTANT: Return JSON with keys: title, subject, body.");
    promptBuilder.AppendLine("- title: Keep title <=8 words");
      promptBuilder.AppendLine("- subject: Keep subject <=12 words");
    promptBuilder.AppendLine("- body: MUST be a detailed message of at least 150 words. This is the main content that will be sent to users.");
                promptBuilder.AppendLine("Ensure the body is comprehensive and informative.");
     }

  try
         {
          var gen = await _aiBroadcast.GenerateAsync(promptBuilder.ToString(), language);

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
     System.Diagnostics.Debug.WriteLine($"JSON parsing failed: {ex.Message}. Raw response: {gen}");

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
            Language = isChineseLanguage ? BroadcastLanguage.Chinese : BroadcastLanguage.English,
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
          SelectedArticleIds = new List<int>(),
    HasChineseTranslation = false
   });
         }
    catch (Exception ex)
    {
      return StatusCode(500, new { message = "Failed to generate broadcast", error = ex.Message });
        }
        }

        /// <summary>
        /// Get audience counts for different targeting options
        /// </summary>
        /// <returns>Audience counts broken down by different criteria</returns>
    [HttpGet("audience-counts")]
        public async Task<IActionResult> GetAudienceCounts()
        {
  try
       {
          var totalMembers = await _db.Members.CountAsync();

                var interestTagCounts = await _db.InterestTags
        .AsNoTracking()
  .Select(tag => new
  {
        TagId = tag.InterestTagId,
    TagName = tag.NameEN,
              MemberCount = tag.Members.Count()
     })
  .ToListAsync();

     var industryTagCounts = await _db.IndustryTags
      .AsNoTracking()
                .Select(tag => new
              {
                TagId = tag.IndustryTagId,
        TagName = tag.NameEN,
     MemberCount = tag.Members.Count()
   })
        .ToListAsync();

    var result = new
     {
         TotalMembers = totalMembers,
          InterestTags = interestTagCounts,
IndustryTags = industryTagCounts
        };

 return Ok(result);
   }
            catch (Exception ex)
   {
    return StatusCode(500, new { message = "Failed to get audience counts", error = ex.Message });
    }
        }

        /// <summary>
        /// Get available tags for targeting with member counts
     /// </summary>
        /// <returns>Available interest and industry tags with member counts</returns>
        [HttpGet("targeting/available-tags")]
        public async Task<IActionResult> GetTargetingAvailableTags()
        {
            try
            {
      var interestTags = await _db.InterestTags
  .AsNoTracking()
      .Select(t => new
         {
  Id = t.InterestTagId,
          Name = t.NameEN,
            Type = "Interest",
             MemberCount = t.Members.Count()
   })
         .ToListAsync();

     var industryTags = await _db.IndustryTags
          .AsNoTracking()
  .Select(t => new
  {
 Id = t.IndustryTagId,
           Name = t.NameEN,
       Type = "Industry",
            MemberCount = t.Members.Count()
             })
 .ToListAsync();

          var result = new
{
        InterestTags = interestTags,
   IndustryTags = industryTags
 };

return Ok(result);
   }
    catch (Exception ex)
     {
        return StatusCode(500, new { message = "Failed to get available tags", error = ex.Message });
       }
        }

  /// <summary>
        /// Send a broadcast message
 /// </summary>
        /// <param name="id">Broadcast ID to send</param>
        /// <returns>Send result</returns>
        [HttpPost("{id:int}/send")]
        public async Task<IActionResult> SendBroadcast(int id)
        {
     try
            {
     var broadcast = await _db.BroadcastMessages.FindAsync(id);
       if (broadcast == null)
      return NotFound($"Broadcast with ID {id} not found.");

      var result = await _broadcastSending.SendBroadcastAsync(id);
          return Ok(result);
            }
        catch (Exception ex)
            {
         return StatusCode(500, new { message = "Failed to send broadcast", error = ex.Message });
       }
        }

        /// <summary>
        /// Get broadcast statistics
   /// </summary>
     /// <param name="id">Broadcast ID</param>
        /// <returns>Broadcast statistics</returns>
        [HttpGet("{id:int}/statistics")]
        public async Task<IActionResult> GetBroadcastStatistics(int id)
        {
  try
            {
              var broadcast = await _db.BroadcastMessages.FindAsync(id);
    if (broadcast == null)
         return NotFound($"Broadcast with ID {id} not found.");

                var stats = await _broadcastSending.GetBroadcastStatisticsAsync(id);
                return Ok(stats);
        }
       catch (Exception ex)
            {
           return StatusCode(500, new { message = "Failed to get broadcast statistics", error = ex.Message });
         }
        }

    /// <summary>
        /// Get eligible recipients for a broadcast
    /// </summary>
    /// <param name="id">Broadcast ID</param>
        /// <returns>List of eligible recipients</returns>
 [HttpGet("{id:int}/recipients")]
        public async Task<IActionResult> GetEligibleRecipients(int id)
  {
        try
        {
             var broadcast = await _db.BroadcastMessages.FindAsync(id);
   if (broadcast == null)
   return NotFound($"Broadcast with ID {id} not found.");

              var recipients = await _broadcastSending.GetEligibleRecipientsAsync(id);
   return Ok(recipients);
     }
 catch (Exception ex)
      {
                return StatusCode(500, new { message = "Failed to get eligible recipients", error = ex.Message });
   }
        }

        /// <summary>
    /// Preview targeted members based on selected tags
        /// </summary>
        /// <param name="interestTagIds">Selected interest tag IDs</param>
     /// <param name="industryTagIds">Selected industry tag IDs</param>
        /// <returns>Preview of targeted members</returns>
        [HttpGet("targeting/preview")]
  public async Task<IActionResult> PreviewTargetedMembers([FromQuery] int[] interestTagIds, [FromQuery] int[] industryTagIds)
        {
            try
            {
      var query = _db.Members.AsQueryable();

           // Filter by interest tags if any are selected
                if (interestTagIds?.Any() == true)
         {
        query = query.Where(m => m.Interests.Any(tag => interestTagIds.Contains(tag.InterestTagId)));
      }

         // Filter by industry tags if any are selected
    if (industryTagIds?.Any() == true)
                {
             query = query.Where(m => m.IndustryTags.Any(tag => industryTagIds.Contains(tag.IndustryTagId)));
  }

         var totalCount = await query.CountAsync();

        var members = await query
               .AsNoTracking()
   .Include(m => m.Interests)
  .Include(m => m.IndustryTags)
   .Select(m => new
 {
              m.MemberId,
               m.ContactPerson,
 m.Email,
        InterestTags = m.Interests.Select(tag => tag.NameEN).ToList(),
          IndustryTags = m.IndustryTags.Select(tag => tag.NameEN).ToList()
      })
     .Take(50)
     .ToListAsync();

      var result = new
     {
       TotalCount = totalCount,
        Members = members,
   ShowingPreview = totalCount > 50
            };

    return Ok(result);
       }
   catch (Exception ex)
            {
        return StatusCode(500, new { message = "Failed to preview targeted members", error = ex.Message });
   }
      }

        /// <summary>
        /// Track email open
        /// </summary>
    /// <param name="broadcastId">Broadcast ID</param>
        /// <param name="memberId">Member ID</param>
   /// <returns>Action result</returns>
    [HttpPost("{broadcastId:int}/track-open/{memberId:int}")]
        public async Task<IActionResult> TrackEmailOpen(int broadcastId, int memberId)
{
       try
  {
      var broadcast = await _db.BroadcastMessages.FindAsync(broadcastId);
    if (broadcast == null)
  return NotFound($"Broadcast with ID {broadcastId} not found.");

  var member = await _db.Members.FindAsync(memberId);
     if (member == null)
        return NotFound($"Member with ID {memberId} not found.");

await _broadcastSending.RecordEmailOpenAsync(broadcastId, memberId);
    return Ok();
       }
      catch (Exception ex)
         {
   return StatusCode(500, new { message = "Failed to track email open", error = ex.Message });
     }
     }

        /// <summary>
/// Translate a broadcast to Chinese or English
     /// </summary>
  /// <param name="id">Broadcast ID</param>
        /// <param name="targetLanguage">Target language: 'zh' for Chinese, 'en' for English</param>
        /// <returns>Translation result</returns>
        [HttpPost("{id:int}/translate")]
  public async Task<IActionResult> TranslateBroadcast(int id, [FromQuery] string targetLanguage = "zh")
        {
    try
     {
          if (_translationService == null)
        {
         return StatusCode(503, new { message = "Translation service not configured." });
    }

   var broadcast = await _db.BroadcastMessages.FindAsync(id);
   if (broadcast == null)
    return NotFound($"Broadcast with ID {id} not found.");

   var normalizedTarget = targetLanguage.ToLower();
       if (normalizedTarget != "zh" && normalizedTarget != "en" && normalizedTarget != "chinese" && normalizedTarget != "english")
 {
          return BadRequest("Target language must be 'zh' (Chinese) or 'en' (English).");
     }

   var isTargetChinese = normalizedTarget == "zh" || normalizedTarget == "chinese";
           var sourceLanguage = broadcast.Language == BroadcastLanguage.Chinese ? "zh" : "en";
      var targetLang = isTargetChinese ? "zh" : "en";

    // Check if translation is needed
     if ((sourceLanguage == "en" && targetLang == "en") || (sourceLanguage == "zh" && targetLang == "zh"))
        {
 return BadRequest("Broadcast is already in the target language.");
        }

  // Translate title, subject, and body
     string translatedTitle, translatedSubject, translatedBody;
     
  try
      {
         translatedTitle = await _translationService.TranslateAsync(broadcast.Title, targetLang);
          translatedSubject = await _translationService.TranslateAsync(broadcast.Subject, targetLang);
            translatedBody = await _translationService.TranslateAsync(broadcast.Body, targetLang);
  }
                catch (Exception ex)
       {
        return StatusCode(500, new { message = "Translation failed", error = ex.Message });
        }

         // Save translated content to database
    if (isTargetChinese)
       {
      broadcast.TitleZH = translatedTitle;
               broadcast.SubjectZH = translatedSubject;
         broadcast.BodyZH = translatedBody;
   }
          else
         {
            // If translating back to English, update main fields
               broadcast.Title = translatedTitle;
      broadcast.Subject = translatedSubject;
      broadcast.Body = translatedBody;
       }

   broadcast.UpdatedAt = DateTimeOffset.Now;
      await _db.SaveChangesAsync();

        return Ok(new BroadcastTranslateResultDTO
      {
           BroadcastId = id,
        SourceLanguage = sourceLanguage,
  TargetLanguage = targetLang,
     TranslatedTitle = translatedTitle,
         TranslatedSubject = translatedSubject,
TranslatedBody = translatedBody,
              Success = true
       });
 }
  catch (Exception ex)
     {
   return StatusCode(500, new { message = "Failed to translate broadcast", error = ex.Message });
            }
  }

   /// <summary>
        /// Generate translations for a broadcast (both English and Chinese)
        /// </summary>
        /// <param name="id">Broadcast ID</param>
        /// <returns>Translation result</returns>
  [HttpPost("{id:int}/generate-translations")]
   public async Task<IActionResult> GenerateTranslations(int id)
   {
   try
{
       if (_translationService == null)
     {
 return StatusCode(503, new { message = "Translation service not configured." });
    }

  var broadcast = await _db.BroadcastMessages.FindAsync(id);
   if (broadcast == null)
        return NotFound($"Broadcast with ID {id} not found.");

          // Determine source language
 var isSourceChinese = broadcast.Language == BroadcastLanguage.Chinese;
           var sourceTitle = isSourceChinese ? (broadcast.TitleZH ?? broadcast.Title) : broadcast.Title;
    var sourceSubject = isSourceChinese ? (broadcast.SubjectZH ?? broadcast.Subject) : broadcast.Subject;
          var sourceBody = isSourceChinese ? (broadcast.BodyZH ?? broadcast.Body) : broadcast.Body;

     // Translate to the opposite language
    var targetLang = isSourceChinese ? "en" : "zh";

try
         {
        var translatedTitle = await _translationService.TranslateAsync(sourceTitle, targetLang);
 var translatedSubject = await _translationService.TranslateAsync(sourceSubject, targetLang);
   var translatedBody = await _translationService.TranslateAsync(sourceBody, targetLang);

               if (isSourceChinese)
  {
// Source is Chinese, translate to English
broadcast.Title = translatedTitle;
 broadcast.Subject = translatedSubject;
     broadcast.Body = translatedBody;
          }
    else
         {
         // Source is English, translate to Chinese
         broadcast.TitleZH = translatedTitle;
        broadcast.SubjectZH = translatedSubject;
            broadcast.BodyZH = translatedBody;
 }

  broadcast.UpdatedAt = DateTimeOffset.Now;
     await _db.SaveChangesAsync();

     return Ok(new 
      {
         broadcastId = id,
     sourceLanguage = isSourceChinese ? "zh" : "en",
          targetLanguage = targetLang,
         success = true,
message = "Translations generated successfully"
     });
    }
    catch (Exception ex)
 {
      return StatusCode(500, new { message = "Translation failed", error = ex.Message });
       }
   }
 catch (Exception ex)
     {
      return StatusCode(500, new { message = "Failed to generate translations", error = ex.Message });
    }
        }

        /// <summary>
        /// Preview broadcast content in a specific language
        /// </summary>
 /// <param name="id">Broadcast ID</param>
        /// <param name="language">Language to preview: 'en' or 'zh'</param>
   /// <returns>Broadcast preview in requested language</returns>
        [HttpGet("{id:int}/preview")]
        public async Task<IActionResult> PreviewBroadcast(int id, [FromQuery] string language = "en")
        {
    try
            {
        var broadcast = await _db.BroadcastMessages
                .Include(b => b.SelectedArticles)
         .ThenInclude(a => a.NewsArticle)
         .FirstOrDefaultAsync(b => b.Id == id);

    if (broadcast == null)
        return NotFound($"Broadcast with ID {id} not found.");

     var normalizedLang = language.ToLower();
        var useChinese = normalizedLang == "zh" || normalizedLang == "chinese";

   // Select appropriate content based on language
        string title, subject, body;
         bool translationAvailable;

  if (useChinese && !string.IsNullOrWhiteSpace(broadcast.BodyZH))
          {
       title = broadcast.TitleZH ?? broadcast.Title;
  subject = broadcast.SubjectZH ?? broadcast.Subject;
         body = broadcast.BodyZH;
            translationAvailable = true;
     }
      else if (!useChinese)
      {
  title = broadcast.Title;
   subject = broadcast.Subject;
    body = broadcast.Body;
          translationAvailable = true;
  }
    else
        {
         // Chinese requested but not available, return English with warning
    title = broadcast.Title;
 subject = broadcast.Subject;
             body = broadcast.Body;
           translationAvailable = false;
         }

     var articles = broadcast.SelectedArticles.Select(a => new
      {
       publicationDraftId = a.PublicationDraftId,
  title = useChinese && !string.IsNullOrWhiteSpace(a.NewsArticle?.TitleZH)
   ? a.NewsArticle.TitleZH
         : a.NewsArticle?.TitleEN ?? a.NewsArticle?.TitleZH ?? "",
             heroImageUrl = a.HeroImageUrl,
      publishedAt = a.PublishedAt
    }).ToList();

     return Ok(new
         {
        broadcastId = id,
      language = useChinese ? "zh" : "en",
    translationAvailable,
       title,
          subject,
           body,
   selectedArticles = articles,
         message = !translationAvailable
     ? "Chinese translation not available, showing English content"
  : null
                });
            }
       catch (Exception ex)
     {
return StatusCode(500, new { message = "Failed to preview broadcast", error = ex.Message });
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
