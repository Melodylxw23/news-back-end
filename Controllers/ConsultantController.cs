using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;
using News_Back_end.Services;
using System.Security.Claims;
using System.Text.Json;

namespace News_Back_end.Controllers
{
 [ApiController]
 [Route("api/[controller]")]
 [Authorize(Roles = "Consultant")]
 public class ConsultantController : ControllerBase
 {
 private readonly MyDBContext _db;
 private readonly UserManager<ApplicationUser> _userManager;
 private readonly IConsultantInsightsEmailService _insights;

 public ConsultantController(MyDBContext db, UserManager<ApplicationUser> userManager, IConsultantInsightsEmailService insights)
 {
 _db = db;
 _userManager = userManager;
 _insights = insights;
 }

 [HttpGet("preferences")]
 public async Task<ActionResult<ConsultantPreferenceDTO>> GetPreferences()
 {
 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrEmpty(userId))
 return Unauthorized();

 var user = await _userManager.FindByIdAsync(userId);
 if (user == null)
 return Unauthorized();

 var pref = await _db.ConsultantPreferences.FirstOrDefaultAsync(p => p.ConsultantUserId == userId);
 if (pref == null)
 {
 // Return defaults derived from user email
 return Ok(new ConsultantPreferenceDTO
 {
 Territories = new(),
 Industries = new(),
 Frequency = "daily",
 Language = "english",
 Email = user.Email ?? string.Empty,
 PreferredTime = "09:00"
 });
 }

 return Ok(new ConsultantPreferenceDTO
 {
 Territories = DeserializeList(pref.TerritoriesJson),
 Industries = DeserializeList(pref.IndustriesJson),
 Frequency = pref.Frequency == ConsultantInsightsFrequency.Weekly ? "weekly" : "daily",
 Language = pref.Language == ConsultantInsightsLanguage.Chinese ? "chinese" : "english",
 Email = pref.Email,
 PreferredTime = MinutesToTime(pref.PreferredTimeMinutesUtc)
 });
 }

 [HttpPost("preferences")]
 public async Task<IActionResult> UpsertPreferences([FromBody] ConsultantPreferenceUpsertDTO dto)
 {
 if (!ModelState.IsValid)
 return BadRequest(ModelState);

 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrEmpty(userId))
 return Unauthorized();

 var user = await _userManager.FindByIdAsync(userId);
 if (user == null)
 return Unauthorized();

 // Enforce: consultant can only send to their own account
 var userEmail = user.Email ?? string.Empty;
 if (string.IsNullOrWhiteSpace(userEmail))
 return BadRequest(new { message = "User account has no email configured." });

 // If Email is provided in DTO, it must match the user's email
 if (!string.IsNullOrWhiteSpace(dto.Email))
 {
 if (!string.Equals(dto.Email.Trim(), userEmail, StringComparison.OrdinalIgnoreCase))
 return BadRequest(new { message = "Email must match the consultant's login email." });
 }

 var pref = await _db.ConsultantPreferences.FirstOrDefaultAsync(p => p.ConsultantUserId == userId);
 if (pref == null)
 {
 pref = new ConsultantPreference
 {
 ConsultantUserId = userId
 };
 _db.ConsultantPreferences.Add(pref);
 }

 pref.TerritoriesJson = JsonSerializer.Serialize(Normalize(dto.Territories));
 pref.IndustriesJson = JsonSerializer.Serialize(Normalize(dto.Industries));
 pref.Frequency = string.Equals(dto.Frequency, "weekly", StringComparison.OrdinalIgnoreCase)
 ? ConsultantInsightsFrequency.Weekly
 : ConsultantInsightsFrequency.Daily;
 pref.Language = string.Equals(dto.Language, "chinese", StringComparison.OrdinalIgnoreCase)
 ? ConsultantInsightsLanguage.Chinese
 : ConsultantInsightsLanguage.English;
 pref.Email = userEmail;
 pref.PreferredTimeMinutesUtc = TimeToMinutes(dto.PreferredTime);
 pref.UpdatedAt = DateTimeOffset.UtcNow;

 await _db.SaveChangesAsync();

 return Ok(new
 {
 message = "Preferences saved",
 preferences = new ConsultantPreferenceDTO
 {
 Territories = DeserializeList(pref.TerritoriesJson),
 Industries = DeserializeList(pref.IndustriesJson),
 Frequency = pref.Frequency == ConsultantInsightsFrequency.Weekly ? "weekly" : "daily",
 Language = pref.Language == ConsultantInsightsLanguage.Chinese ? "chinese" : "english",
 Email = pref.Email,
 PreferredTime = MinutesToTime(pref.PreferredTimeMinutesUtc)
 }
 });
 }

 /// <summary>
 /// Dummy preview of what the consultant insights email would look like right now.
 /// </summary>
 [HttpGet("insights/preview")]
 public async Task<ActionResult<ConsultantInsightsPreviewDTO>> PreviewInsights()
 {
 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrEmpty(userId))
 return Unauthorized();

 var nowUtc = DateTimeOffset.UtcNow;
 var (subject, html, generatedAt) = await _insights.BuildPreviewAsync(userId, nowUtc, HttpContext.RequestAborted);

 return Ok(new ConsultantInsightsPreviewDTO
 {
 Subject = subject,
 HtmlBody = html,
 GeneratedAtUtc = generatedAt
 });
 }

 /// <summary>
 /// Send the consultant insights email immediately (dummy content for now).
 /// </summary>
 [HttpPost("insights/send-now")]
 public async Task<ActionResult<ConsultantInsightsSendNowResultDTO>> SendInsightsNow([FromBody] ConsultantInsightsSendNowRequestDTO request)
 {
 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrEmpty(userId))
 return Unauthorized();

 var nowUtc = DateTimeOffset.UtcNow;
 var (success, message, email, attemptedAt) = await _insights.SendNowAsync(
 consultantUserId: userId,
 nowUtc: nowUtc,
 force: request?.Force ?? false,
 cancellationToken: HttpContext.RequestAborted);

 return Ok(new ConsultantInsightsSendNowResultDTO
 {
 Success = success,
 Message = message,
 Email = email,
 AttemptedAtUtc = attemptedAt
 });
 }

 /// <summary>
 /// Get editable preview of insights (allows consultant to edit content before sending).
 /// </summary>
 [HttpGet("insights/edit-preview")]
 public async Task<ActionResult<ConsultantInsightsEditPreviewDTO>> GetEditPreview()
 {
 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrEmpty(userId))
 return Unauthorized();

 try
 {
 var nowUtc = DateTimeOffset.UtcNow;
 var (subject, html, generatedAt) = await _insights.BuildPreviewAsync(userId, nowUtc, HttpContext.RequestAborted);

 var pref = await _db.ConsultantPreferences
 .Include(p => p.ConsultantUser)
 .FirstOrDefaultAsync(p => p.ConsultantUserId == userId, HttpContext.RequestAborted);

 if (pref == null)
 return NotFound(new { message = "No preferences found. Please save preferences first." });

 var territories = DeserializeList(pref.TerritoriesJson);
 var industries = DeserializeList(pref.IndustriesJson);

 // Generate AI response to get editable fields with sources
 ConsultantInsightsAiResponse? aiResponse = null;
 try
 {
 var aiService = HttpContext.RequestServices.GetRequiredService<IConsultantInsightsAiService>();
 var language = pref.Language == ConsultantInsightsLanguage.Chinese ? "zh" : "en";
 var consultantName = pref.ConsultantUser?.Name ?? "Consultant";
 aiResponse = await aiService.GenerateInsightsAsync(territories, industries, pref.Frequency.ToString() ?? "Daily", consultantName, language, HttpContext.RequestAborted);
 }
 catch (Exception ex)
 {
 return StatusCode(500, new { message = "Failed to generate AI insights: " + ex.Message });
 }

 return Ok(new ConsultantInsightsEditPreviewDTO
 {
 Subject = subject,
 EditableContent = new EditableConsultantInsightsDTO
 {
 ExecutiveSummary = aiResponse?.ExecutiveSummary ?? string.Empty,
 KeyDevelopments = aiResponse?.KeyDevelopmentsWithSources?.Select(InsightItemDTO.FromInsightItem).ToList() ?? new(),
 Opportunities = aiResponse?.OpportunitiesWithSources?.Select(InsightItemDTO.FromInsightItem).ToList() ?? new(),
 Watchouts = aiResponse?.WatchoutsWithSources?.Select(InsightItemDTO.FromInsightItem).ToList() ?? new(),
 RecommendedActions = aiResponse?.RecommendedActionsWithSources?.Select(InsightItemDTO.FromInsightItem).ToList() ?? new()
 },
 GeneratedAtUtc = generatedAt,
 IsEdited = false
 });
 }
 catch (Exception ex)
 {
 return StatusCode(500, new { message = "Failed to get edit preview: " + ex.Message });
 }
 }

 /// <summary>
 /// Get editable insights content from AI (separate endpoint).
 /// Returns insights with source references for each item.
 /// </summary>
 [HttpGet("insights/generate-editable")]
 public async Task<ActionResult<EditableConsultantInsightsDTO>> GenerateEditableInsights()
 {
 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrEmpty(userId))
 return Unauthorized();

 try
 {
 var pref = await _db.ConsultantPreferences
 .Include(p => p.ConsultantUser)
 .FirstOrDefaultAsync(p => p.ConsultantUserId == userId, HttpContext.RequestAborted);

 if (pref == null)
 return NotFound(new { message = "No preferences found. Please save preferences first." });

 if (pref.ConsultantUser == null)
 return StatusCode(500, new { message = "Consultant user profile not found." });

 var territories = DeserializeList(pref.TerritoriesJson);
 var industries = DeserializeList(pref.IndustriesJson);

 // Get the AI service directly
 var aiService = HttpContext.RequestServices.GetRequiredService<IConsultantInsightsAiService>();
 ConsultantInsightsAiResponse? aiResponse = null;

 try
 {
 var language = pref.Language == ConsultantInsightsLanguage.Chinese ? "zh" : "en";
 var consultantName = pref.ConsultantUser.Name ?? "Consultant";
 aiResponse = await aiService.GenerateInsightsAsync(
 territories,
 industries,
 pref.Frequency.ToString(),
 consultantName,
 language,
 HttpContext.RequestAborted);
 }
 catch (Exception ex)
 {
 return StatusCode(500, new { message = "Failed to generate insights: " + ex.Message });
 }

 // Return the full structured response with sources
 return Ok(new EditableConsultantInsightsDTO
 {
 ExecutiveSummary = aiResponse?.ExecutiveSummary ?? string.Empty,
 KeyDevelopments = aiResponse?.KeyDevelopmentsWithSources?.Select(InsightItemDTO.FromInsightItem).ToList() ?? new(),
 Opportunities = aiResponse?.OpportunitiesWithSources?.Select(InsightItemDTO.FromInsightItem).ToList() ?? new(),
 Watchouts = aiResponse?.WatchoutsWithSources?.Select(InsightItemDTO.FromInsightItem).ToList() ?? new(),
 RecommendedActions = aiResponse?.RecommendedActionsWithSources?.Select(InsightItemDTO.FromInsightItem).ToList() ?? new()
 });
 }
 catch (Exception ex)
 {
 return StatusCode(500, new { message = "Failed to generate editable insights: " + ex.Message });
 }
 }

 /// <summary>
 /// Send insights with edited content from the consultant.
 /// </summary>
 [HttpPost("insights/send-edited")]
 public async Task<ActionResult<ConsultantInsightsSendNowResultDTO>> SendEditedInsights([FromBody] SendEditedInsightsRequestDTO request)
 {
 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrEmpty(userId))
 return Unauthorized();

 try
 {
 var pref = await _db.ConsultantPreferences
 .Include(p => p.ConsultantUser)
 .FirstOrDefaultAsync(p => p.ConsultantUserId == userId, HttpContext.RequestAborted);

 if (pref == null)
 return BadRequest(new { message = "No consultant preferences found. Save preferences first." });

 if (pref.ConsultantUser == null)
 return StatusCode(500, new { message = "Consultant user profile not found." });

 // Guard: send only to themselves
 var consultantEmail = pref.ConsultantUser.Email ?? string.Empty;
 if (string.IsNullOrWhiteSpace(consultantEmail))
 return BadRequest(new { message = "Consultant user has no email." });

 var targetEmail = consultantEmail.Trim();
 var nowUtc = DateTimeOffset.UtcNow;
 var (period, periodDateUtc) = GetPeriodKey(pref.Frequency, nowUtc);

 if (!request.Force)
 {
 var alreadySent = await _db.ConsultantInsightsSendLogs
 .AnyAsync(l => l.ConsultantUserId == userId && l.Period == period && l.PeriodDateUtc == periodDateUtc, HttpContext.RequestAborted);

 if (alreadySent)
 return BadRequest(new { message = "Already sent for this period. Use force=true to resend." });
 }

 var territories = DeserializeList(pref.TerritoriesJson);
 var industries = DeserializeList(pref.IndustriesJson);

 var subject = pref.Frequency == ConsultantInsightsFrequency.Weekly
 ? $"China Insights (Weekly) - {nowUtc:yyyy-MM-dd}"
 : $"China Insights (Daily) - {nowUtc:yyyy-MM-dd}";

 // Convert InsightItemDTOs to InsightItems for email rendering
 var keyDevelopmentsWithSources = request.KeyDevelopments?.Select(dto => new InsightItem
 {
 Text = dto.Text,
 SourceName = dto.SourceName,
 SourceUrl = dto.SourceUrl,
 SourceDate = dto.SourceDate
 }).ToList() ?? new();

 var opportunitiesWithSources = request.Opportunities?.Select(dto => new InsightItem
 {
 Text = dto.Text,
 SourceName = dto.SourceName,
 SourceUrl = dto.SourceUrl,
 SourceDate = dto.SourceDate
 }).ToList() ?? new();

 var watchoutsWithSources = request.Watchouts?.Select(dto => new InsightItem
 {
 Text = dto.Text,
 SourceName = dto.SourceName,
 SourceUrl = dto.SourceUrl,
 SourceDate = dto.SourceDate
 }).ToList() ?? new();

 var recommendedActionsWithSources = request.RecommendedActions?.Select(dto => new InsightItem
 {
 Text = dto.Text,
 SourceName = dto.SourceName,
 SourceUrl = dto.SourceUrl,
 SourceDate = dto.SourceDate
 }).ToList() ?? new();

 // Build HTML from edited content with sources
 var html = ConsultantInsightsEmailService.BuildEditedEmailHtmlWithSources(
 pref.ConsultantUser.Name,
 territories,
 industries,
 request.ExecutiveSummary,
 keyDevelopmentsWithSources,
 opportunitiesWithSources,
 watchoutsWithSources,
 recommendedActionsWithSources,
 nowUtc);

 var attemptedAt = DateTimeOffset.UtcNow;
 var sendLog = new ConsultantInsightsSendLog
 {
 ConsultantUserId = userId,
 Period = period,
 PeriodDateUtc = periodDateUtc,
 SentAtUtc = attemptedAt,
 Email = targetEmail,
 Success = true
 };

 try
 {
 var emailService = HttpContext.RequestServices.GetRequiredService<GmailEmailService>();
 await emailService.SendEmailAsync(targetEmail, subject, html);
 _db.ConsultantInsightsSendLogs.Add(sendLog);
 await _db.SaveChangesAsync(HttpContext.RequestAborted);

 return Ok(new ConsultantInsightsSendNowResultDTO
 {
 Success = true,
 Message = "Edited insights sent successfully.",
 Email = targetEmail,
 AttemptedAtUtc = attemptedAt
 });
 }
 catch (Exception ex)
 {
 sendLog.Success = false;
 sendLog.Error = ex.Message;
 try
 {
 _db.ConsultantInsightsSendLogs.Add(sendLog);
 await _db.SaveChangesAsync(HttpContext.RequestAborted);
 }
 catch { }

 return StatusCode(500, new { success = false, message = "Failed to send edited insights: " + ex.Message });
 }
 }
 catch (Exception ex)
 {
 return StatusCode(500, new { message = "Unexpected error: " + ex.Message });
 }
 }

 private static List<string> Normalize(IEnumerable<string> values) => values
 .Where(v => !string.IsNullOrWhiteSpace(v))
 .Select(v => v.Trim())
 .Distinct(StringComparer.OrdinalIgnoreCase)
 .ToList();

 private static List<string> DeserializeList(string? json)
 {
 if (string.IsNullOrWhiteSpace(json)) return new();
 try
 {
 return JsonSerializer.Deserialize<List<string>>(json) ?? new();
 }
 catch
 {
 return new();
 }
 }

 private static int TimeToMinutes(string hhmm)
 {
 var parts = hhmm.Split(':', StringSplitOptions.RemoveEmptyEntries);
 var h = int.Parse(parts[0]);
 var m = int.Parse(parts[1]);
 return h * 60 + m;
 }

 private static string MinutesToTime(int minutes)
 {
 minutes = Math.Clamp(minutes, 0, 23 * 60 + 59);
 var h = minutes / 60;
 var m = minutes % 60;
 return $"{h:D2}:{m:D2}";
 }

 /// <summary>
 /// Helper method to get period key for idempotency.
 /// </summary>
 private static (ConsultantInsightsPeriod Period, DateTime PeriodDateUtc) GetPeriodKey(ConsultantInsightsFrequency frequency, DateTimeOffset nowUtc)
 {
 var date = nowUtc.UtcDateTime.Date;
 if (frequency == ConsultantInsightsFrequency.Weekly)
 {
 int diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
 var monday = date.AddDays(-diff);
 return (ConsultantInsightsPeriod.Weekly, monday);
 }

 return (ConsultantInsightsPeriod.Daily, date);
 }
 }
}
