using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using News_Back_end.Models.SQLServer;
using System.Security.Claims;

namespace News_Back_end.Controllers
{
 [ApiController]
 [Route("api/[controller]")]
 public class AutoFetchController : ControllerBase
 {
 private readonly MyDBContext _db;

 public AutoFetchController(MyDBContext db)
 {
 _db = db;
 }

 private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

 public sealed class UpdateAutoFetchDto
 {
 public bool Enabled { get; set; }
 public int? IntervalSeconds { get; set; }
 }

 // GET: api/autofetch
 [HttpGet]
 [Authorize(Roles = "Consultant")]
 public async Task<IActionResult> Get()
 {
 var userId = GetUserId();
 if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

 var s = await _db.AutoFetchSettings
 .AsNoTracking()
 .FirstOrDefaultAsync(x => x.ApplicationUserId == userId);

 if (s == null)
 {
 // default for a user that has never saved settings
 return Ok(new { Enabled = false, IntervalSeconds =300, UpdatedAt = (DateTime?)null });
 }

 return Ok(new { s.Enabled, s.IntervalSeconds, s.UpdatedAt });
 }

 // PUT: api/autofetch
 [HttpPut]
 [Authorize(Roles = "Consultant")]
 public async Task<IActionResult> Update([FromBody] UpdateAutoFetchDto dto)
 {
 var userId = GetUserId();
 if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

 var s = await _db.AutoFetchSettings.FirstOrDefaultAsync(x => x.ApplicationUserId == userId);
 if (s == null)
 {
 s = new AutoFetchSetting { ApplicationUserId = userId };
 _db.AutoFetchSettings.Add(s);
 }

 s.Enabled = dto.Enabled;

 if (dto.IntervalSeconds.HasValue)
 {
 if (dto.IntervalSeconds.Value <10)
 return BadRequest(new { message = "IntervalSeconds must be >=10" });

 s.IntervalSeconds = dto.IntervalSeconds.Value;
 }

 s.UpdatedAt = DateTime.UtcNow;
 await _db.SaveChangesAsync();

 return Ok(new { s.Enabled, s.IntervalSeconds, s.UpdatedAt });
 }

 // POST: api/autofetch/enable
 [HttpPost("enable")]
 [Authorize(Roles = "Consultant")]
 public Task<IActionResult> Enable() => Update(new UpdateAutoFetchDto { Enabled = true });

 // POST: api/autofetch/disable
 [HttpPost("disable")]
 [Authorize(Roles = "Consultant")]
 public Task<IActionResult> Disable() => Update(new UpdateAutoFetchDto { Enabled = false });
 }
}
