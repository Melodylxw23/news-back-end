using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;

namespace News_Back_end.Controllers
{
 [ApiController]
 [Route("api/[controller]")]
 [Authorize]
 public class BroadcastController : ControllerBase
 {
 private readonly MyDBContext _db;
 private readonly Services.IAiBroadcastService? _aiBroadcast;

 public BroadcastController(MyDBContext db, Services.IAiBroadcastService? aiBroadcast = null)
 {
 _db = db;
 _aiBroadcast = aiBroadcast;
 }

 // GET: api/broadcast
 [HttpGet]
 public async Task<IActionResult> GetAll()
 {
 var items = await _db.BroadcastMessages.OrderByDescending(b => b.UpdatedAt).ToListAsync();
 return Ok(items);
 }

 // GET: api/broadcast/5
 [HttpGet("{id:int}")]
 public async Task<IActionResult> Get(int id)
 {
 var item = await _db.BroadcastMessages.FindAsync(id);
 if (item == null) return NotFound();
 return Ok(item);
 }

 // POST: api/broadcast
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
 return CreatedAtAction(nameof(Get), new { id = model.Id }, model);
 }

 // PUT: api/broadcast/5
 [HttpPut("{id:int}")]
 public async Task<IActionResult> Update(int id, [FromBody] BroadcastUpdateDTO dto)
 {
 var existing = await _db.BroadcastMessages.FindAsync(id);
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

 await _db.SaveChangesAsync();
 return NoContent();
 }

 // DELETE: api/broadcast/5
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

 // POST: api/broadcast/generate
 // Accepts a free-form prompt and returns generated title/subject/body and saves it as a Draft
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
 promptBuilder.AppendLine("Return JSON with keys: title, subject, body. Keep title <=8 words, subject <=12 words.");

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
 catch
 {
 var lines = gen?.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
 if (lines.Length >0) title = lines[0].Trim();
 if (lines.Length >1) subject = lines[1].Trim();
 if (lines.Length >2) body = string.Join("\n", lines.Skip(2)).Trim();
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

 return CreatedAtAction(nameof(Get), new { id = model.Id }, model);
 }

 private static string? ExtractJsonObject(string? s)
 {
 if (string.IsNullOrWhiteSpace(s)) return null;
 var first = s.IndexOf('{');
 var last = s.LastIndexOf('}');
 if (first >=0 && last > first)
 {
 var candidate = s.Substring(first, last - first +1);
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
 return value.Length <= maxLength ? value : value.Substring(0, maxLength -3) + "...";
 }
 }
}
