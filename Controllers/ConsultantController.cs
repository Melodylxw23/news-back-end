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
 Email = user.Email ?? string.Empty,
 PreferredTime = "09:00"
 });
 }

 return Ok(new ConsultantPreferenceDTO
 {
 Territories = DeserializeList(pref.TerritoriesJson),
 Industries = DeserializeList(pref.IndustriesJson),
 Frequency = pref.Frequency == ConsultantInsightsFrequency.Weekly ? "weekly" : "daily",
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
 if (!string.Equals(dto.Email?.Trim(), userEmail, StringComparison.OrdinalIgnoreCase))
 return BadRequest(new { message = "Email must match the consultant's login email." });

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
 return h *60 + m;
 }

 private static string MinutesToTime(int minutes)
 {
 minutes = Math.Clamp(minutes,0,23 *60 +59);
 var h = minutes /60;
 var m = minutes %60;
 return $"{h:D2}:{m:D2}";
 }
 }
}
