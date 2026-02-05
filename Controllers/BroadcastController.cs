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
        /// Get audience counts for different interest categories and subscription types
        /// </summary>
        /// <returns>Audience statistics for broadcast targeting</returns>
        [HttpGet("audience-counts")]
  public async Task<IActionResult> GetAudienceCounts()
        {
var allMembers = await _db.Members.CountAsync();
            
       // Email subscribers (members who have email enabled)
        var emailSubscribers = await _db.Members
      .Where(m => m.PreferredChannel == Channels.Email || m.PreferredChannel == Channels.Both)
.Where(m => !string.IsNullOrEmpty(m.Email))
   .CountAsync();
     
    var technologyInterested = await _db.Members
      .Where(m => m.Interests.Any(t => t.NameEN.ToLower().Contains("technology") || t.NameZH.ToLower().Contains("技术")))
 .CountAsync();
       
       var businessInterested = await _db.Members
 .Where(m => m.Interests.Any(t => t.NameEN.ToLower().Contains("business") || t.NameZH.ToLower().Contains("商业")))
 .CountAsync();
       
   var sportsInterested = await _db.Members
         .Where(m => m.Interests.Any(t => t.NameEN.ToLower().Contains("sports") || t.NameZH.ToLower().Contains("体育")))
         .CountAsync();
          
    var entertainmentInterested = await _db.Members
        .Where(m => m.Interests.Any(t => t.NameEN.ToLower().Contains("entertainment") || t.NameZH.ToLower().Contains("娱乐")))
.CountAsync();
 
    var politicsInterested = await _db.Members
      .Where(m => m.Interests.Any(t => t.NameEN.ToLower().Contains("politics") || t.NameZH.ToLower().Contains("政治")))
.CountAsync();

        // Additional useful metrics
var activeMembers = await _db.Members
       .Where(m => m.ApplicationUser != null && m.ApplicationUser.IsActive)
     .CountAsync();

 var membersByCountry = await _db.Members
    .GroupBy(m => m.Country)
  .Select(g => new CountByGroupDTO { Group = g.Key.ToString(), Count = g.Count() })
.ToListAsync();

 var membersByLanguage = await _db.Members
    .GroupBy(m => m.PreferredLanguage)
       .Select(g => new CountByGroupDTO { Group = g.Key.ToString(), Count = g.Count() })
           .ToListAsync();

   return Ok(new AudienceCountsDTO
        {
  AllMembers = allMembers,
  ActiveMembers = activeMembers,
  EmailSubscribers = emailSubscribers,
   InterestCategories = new InterestCategoriesDTO
  {
TechnologyInterested = technologyInterested,
           BusinessInterested = businessInterested,
       SportsInterested = sportsInterested,
    EntertainmentInterested = entertainmentInterested,
        PoliticsInterested = politicsInterested
        },
  Demographics = new DemographicsDTO
 {
ByCountry = membersByCountry,
    ByLanguage = membersByLanguage
      },
       EmailEngagementRate = allMembers > 0 ? Math.Round((double)emailSubscribers / allMembers * 100, 2) : 0
      });
    }

/// <summary>
    /// Track email opens (tracking pixel endpoint)
        /// </summary>
        /// <param name="broadcastId">Broadcast ID</param>
      /// <param name="memberId">Member ID</param>
        /// <returns>1x1 transparent PNG pixel</returns>
        [HttpGet("track-open/{broadcastId:int}/{memberId:int}")]
        [AllowAnonymous] // Allow anonymous access for email tracking
        public async Task<IActionResult> TrackEmailOpen(int broadcastId, int memberId)
    {
try
         {
       // Record the open event
    await _broadcastSending.RecordEmailOpenAsync(broadcastId, memberId);

      // Return a 1x1 transparent PNG pixel
  var pixel = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");
        return File(pixel, "image/png");
        }
        catch
        {
      // Even if tracking fails, return the pixel to avoid broken images
        var pixel = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");
       return File(pixel, "image/png");
   }
     }

 /// <summary>
   /// Get detailed broadcast statistics including delivery and open tracking
  /// </summary>
 /// <param name="id">Broadcast ID</param>
      /// <returns>Comprehensive broadcast statistics</returns>
  [HttpGet("{id:int}/statistics")]
      public async Task<IActionResult> GetBroadcastStatistics(int id)
   {
      try
   {
 var statistics = await _broadcastSending.GetBroadcastStatisticsAsync(id);
return Ok(statistics);
     }
     catch (ArgumentException ex)
  {
     return NotFound(new { message = ex.Message });
     }
         catch (Exception ex)
      {
       return StatusCode(500, new { message = "Error getting statistics: " + ex.Message });
  }
        }

     /// <summary>
        /// Debug endpoint to check member and broadcast data for troubleshooting
        /// </summary>
     [HttpGet("debug/recipients/{broadcastId:int}")]
        public async Task<IActionResult> DebugRecipients(int broadcastId)
        {
    try
{
       var broadcast = await _db.BroadcastMessages
  .Include(b => b.SelectedArticles)
  .ThenInclude(a => a.IndustryTag)
        .Include(b => b.SelectedArticles)
   .ThenInclude(a => a.InterestTags)
      .FirstOrDefaultAsync(b => b.Id == broadcastId);

        if (broadcast == null)
       return NotFound("Broadcast not found");

     var allMembers = await _db.Members
         .Include(m => m.IndustryTags)
      .Include(m => m.Interests)
    .ToListAsync();

     var emailEnabledMembers = allMembers
         .Where(m => m.PreferredChannel == Channels.Email || m.PreferredChannel == Channels.Both)
       .Where(m => !string.IsNullOrEmpty(m.Email))
      .ToList();

    return Ok(new
     {
        BroadcastInfo = new
      {
        Id = broadcast.Id,
         Title = broadcast.Title,
  TargetAudience = broadcast.TargetAudience.ToString(),
        SelectedArticleCount = broadcast.SelectedArticles.Count,
 SelectedArticles = broadcast.SelectedArticles.Select(a => new
      {
         a.PublicationDraftId,
        Title = a.NewsArticle?.TitleEN ?? a.NewsArticle?.TitleZH ?? "No title",
       IndustryTag = a.IndustryTag?.NameEN,
    InterestTags = a.InterestTags.Select(it => it.NameEN).ToList()
   }).ToList()
  },
    MemberStats = new
      {
      TotalMembers = allMembers.Count,
   EmailEnabledMembers = emailEnabledMembers.Count,
       MembersWithIndustryTags = emailEnabledMembers.Count(m => m.IndustryTags.Any()),
       MembersWithInterestTags = emailEnabledMembers.Count(m => m.Interests.Any())
     },
    EmailEnabledMembersDetails = emailEnabledMembers.Select(m => new
     {
     m.MemberId,
      m.ContactPerson,
  m.Email,
      m.PreferredChannel,
    IndustryTags = m.IndustryTags.Select(it => new { it.IndustryTagId, it.NameEN }).ToList(),
        InterestTags = m.Interests.Select(it => new { it.InterestTagId, it.NameEN }).ToList()
     }).ToList()
      });
     }
         catch (Exception ex)
      {
      return StatusCode(500, new { message = "Debug error: " + ex.Message });
      }
        }

        /// <summary>
        /// Debug endpoint to get all potential recipients (all email-enabled members)
  /// </summary>
        [HttpGet("debug/all-potential-recipients")]
 public async Task<IActionResult> DebugAllPotentialRecipients()
   {
    try
      {
       var recipients = await _broadcastSending.GetAllPotentialRecipientsAsync();
   return Ok(new
   {
     TotalPotentialRecipients = recipients.Count,
    Recipients = recipients
      });
      }
   catch (Exception ex)
  {
      return StatusCode(500, new { message = "Debug error: " + ex.Message });
     }
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
      /// Schedule a broadcast for future sending
        /// </summary>
        /// <param name="request">Schedule broadcast request</param>
    /// <returns>Confirmation of scheduling</returns>
   [HttpPost("schedule")]
        public async Task<IActionResult> ScheduleBroadcast([FromBody] ScheduleBroadcastRequestDTO request)
   {
     try
            {
   var broadcast = await _db.BroadcastMessages.FindAsync(request.BroadcastId);
                if (broadcast == null)
        return NotFound(new { message = "Broadcast not found" });

    if (broadcast.Status == BroadcastStatus.Sent)
            return BadRequest(new { message = "Broadcast has already been sent" });

                if (request.ScheduledSendAt <= DateTimeOffset.UtcNow)
           return BadRequest(new { message = "Scheduled send time must be in the future" });

           // Update the broadcast to scheduled status
    broadcast.ScheduledSendAt = request.ScheduledSendAt;
    broadcast.Status = BroadcastStatus.Scheduled;
        broadcast.UpdatedAt = DateTimeOffset.UtcNow;

    await _db.SaveChangesAsync();

     return Ok(new
          {
  message = "Broadcast scheduled successfully",
broadcastId = broadcast.Id,
         scheduledSendAt = broadcast.ScheduledSendAt,
   status = broadcast.Status.ToString()
        });
        }
            catch (Exception ex)
         {
      return StatusCode(500, new { message = "Error scheduling broadcast: " + ex.Message });
            }
  }

        /// <summary>
        /// Unschedule a broadcast (change from Scheduled back to Draft)
        /// </summary>
        /// <param name="id">Broadcast ID</param>
        /// <returns>Confirmation of unscheduling</returns>
        [HttpPost("{id:int}/unschedule")]
        public async Task<IActionResult> UnscheduleBroadcast(int id)
        {
            try
     {
       var broadcast = await _db.BroadcastMessages.FindAsync(id);
    if (broadcast == null)
   return NotFound(new { message = "Broadcast not found" });

                if (broadcast.Status != BroadcastStatus.Scheduled)
        return BadRequest(new { message = "Broadcast is not currently scheduled" });

    // Change back to draft
     broadcast.Status = BroadcastStatus.Draft;
          broadcast.ScheduledSendAt = null; // Clear the scheduled time
         broadcast.UpdatedAt = DateTimeOffset.UtcNow;

    await _db.SaveChangesAsync();

 return Ok(new
       {
   message = "Broadcast unscheduled successfully",
         broadcastId = broadcast.Id,
       status = broadcast.Status.ToString()
           });
      }
   catch (Exception ex)
        {
     return StatusCode(500, new { message = "Error unscheduling broadcast: " + ex.Message });
    }
        }

        /// <summary>
        /// Get all scheduled broadcasts
        /// </summary>
        /// <returns>List of broadcasts that are scheduled for future sending</returns>
   [HttpGet("scheduled")]
        public async Task<IActionResult> GetScheduledBroadcasts()
        {
      try
       {
                var scheduledBroadcasts = await _db.BroadcastMessages
    .Where(b => b.Status == BroadcastStatus.Scheduled)
               .OrderBy(b => b.ScheduledSendAt)
        .Select(b => new
  {
   Id = b.Id,
 Title = b.Title,
            Subject = b.Subject,
         ScheduledSendAt = b.ScheduledSendAt,
        CreatedAt = b.CreatedAt,
 CreatedById = b.CreatedById,
    SelectedArticlesCount = b.SelectedArticles.Count(),
    TimeUntilSend = b.ScheduledSendAt != null 
         ? (b.ScheduledSendAt.Value - DateTimeOffset.UtcNow).TotalMinutes 
        : 0
        })
     .ToListAsync();

  return Ok(new
        {
   totalScheduled = scheduledBroadcasts.Count,
     broadcasts = scheduledBroadcasts,
            currentTime = DateTimeOffset.UtcNow
 });
            }
      catch (Exception ex)
            {
     return StatusCode(500, new { message = "Error getting scheduled broadcasts: " + ex.Message });
         }
        }

        /// <summary>
     /// Reschedule a broadcast to a different time
      /// </summary>
  /// <param name="id">Broadcast ID</param>
        /// <param name="request">New scheduling details</param>
        /// <returns>Confirmation of rescheduling</returns>
        [HttpPut("{id:int}/reschedule")]
   public async Task<IActionResult> RescheduleBroadcast(int id, [FromBody] RescheduleBroadcastRequestDTO request)
        {
         try
    {
         var broadcast = await _db.BroadcastMessages.FindAsync(id);
          if (broadcast == null)
   return NotFound(new { message = "Broadcast not found" });

       if (broadcast.Status != BroadcastStatus.Scheduled)
      return BadRequest(new { message = "Broadcast is not currently scheduled" });

        if (request.NewScheduledSendAt <= DateTimeOffset.UtcNow)
      return BadRequest(new { message = "New scheduled send time must be in the future" });

              var oldScheduledTime = broadcast.ScheduledSendAt;
       broadcast.ScheduledSendAt = request.NewScheduledSendAt;
    broadcast.UpdatedAt = DateTimeOffset.UtcNow;

    await _db.SaveChangesAsync();

    return Ok(new
         {
          message = "Broadcast rescheduled successfully",
      broadcastId = broadcast.Id,
         oldScheduledSendAt = oldScheduledTime,
           newScheduledSendAt = broadcast.ScheduledSendAt,
         status = broadcast.Status.ToString()
      });
            }
         catch (Exception ex)
            {
          return StatusCode(500, new { message = "Error rescheduling broadcast: " + ex.Message });
   }
        }

    /// <summary>
        /// Get broadcast scheduler service status and statistics
   /// </summary>
        /// <returns>Information about the background scheduler service</returns>
      [HttpGet("scheduler/status")]
     public async Task<IActionResult> GetSchedulerStatus()
        {
  try
    {
          var totalScheduled = await _db.BroadcastMessages
      .CountAsync(b => b.Status == BroadcastStatus.Scheduled);

            var upcomingInNextHour = await _db.BroadcastMessages
        .CountAsync(b => b.Status == BroadcastStatus.Scheduled &&
          b.ScheduledSendAt != null &&
     b.ScheduledSendAt <= DateTimeOffset.UtcNow.AddHours(1));

                var upcomingInNext24Hours = await _db.BroadcastMessages
            .CountAsync(b => b.Status == BroadcastStatus.Scheduled &&
       b.ScheduledSendAt != null &&
        b.ScheduledSendAt <= DateTimeOffset.UtcNow.AddDays(1));

        var overdue = await _db.BroadcastMessages
       .CountAsync(b => b.Status == BroadcastStatus.Scheduled &&
        b.ScheduledSendAt != null &&
                 b.ScheduledSendAt <= DateTimeOffset.UtcNow);

        return Ok(new
      {
              schedulerInfo = new
   {
          isRunning = true, // The service is registered as a background service
         checkIntervalMinutes = 1,
    lastCheckedAt = DateTimeOffset.UtcNow, // Approximate
          description = "Background service checks for scheduled broadcasts every minute"
 },
     statistics = new
        {
      totalScheduledBroadcasts = totalScheduled,
    upcomingInNextHour = upcomingInNextHour,
             upcomingInNext24Hours = upcomingInNext24Hours,
overdueBroadcasts = overdue
         },
          currentServerTime = DateTimeOffset.UtcNow
     });
   }
            catch (Exception ex)
       {
        return StatusCode(500, new { message = "Error getting scheduler status: " + ex.Message });
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