using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using News_Back_end.Models.SQLServer;
using News_Back_end.DTOs;

namespace News_Back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InterestTagsController : ControllerBase
    {
        private readonly MyDBContext _context;

        public InterestTagsController(MyDBContext context)
        {
            _context = context;
        }

        // GET: api/interesttags
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _context.InterestTags
                .OrderBy(i => i.NameEN)
                .Select(i => new { i.InterestTagId, i.NameEN, i.NameZH })
                .ToListAsync();

            return Ok(new { message = "List retrieved successfully.", data = list });
        }

        // GET: api/interesttags/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var tag = await _context.InterestTags
                .Where(t => t.InterestTagId == id)
                .Select(t => new { t.InterestTagId, t.NameEN, t.NameZH })
                .FirstOrDefaultAsync();

            if (tag == null)
                return NotFound(new { message = $"Interest topic with id {id} does not exist." });

            return Ok(new { message = "Retrieved successfully.", data = tag });
        }

        // POST: api/interesttags
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateInterestDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var nameEn = dto.NameEN?.Trim();
            var nameZh = dto.NameZH?.Trim();
            if (string.IsNullOrEmpty(nameEn) || string.IsNullOrEmpty(nameZh)) return BadRequest(new { message = "Both NameEN and NameZH are required." });

            var exists = await _context.InterestTags
                .AnyAsync(t => t.NameEN.ToLower() == nameEn.ToLower() || t.NameZH.ToLower() == nameZh.ToLower());
            if (exists) return BadRequest(new { message = "Interest topic with the same name already exists." });

            var entity = new InterestTag { NameEN = nameEn, NameZH = nameZh };
            _context.InterestTags.Add(entity);
            await _context.SaveChangesAsync();

            var response = new InterestResponseDTO
            {
                InterestTagId = entity.InterestTagId,
                NameEN = entity.NameEN,
                NameZH = entity.NameZH
            };

            return CreatedAtAction(nameof(GetById), new { id = entity.InterestTagId },
                new { message = "Successfully created.", data = response });
        }

        // PUT: api/interesttags/5
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateInterestDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = await _context.InterestTags.FindAsync(id);
            if (entity == null) return NotFound(new { message = $"Interest topic with id {id} does not exist." });

            var nameEn = dto.NameEN?.Trim();
            var nameZh = dto.NameZH?.Trim();
            if (string.IsNullOrEmpty(nameEn) || string.IsNullOrEmpty(nameZh)) return BadRequest(new { message = "Both NameEN and NameZH are required." });

            var duplicate = await _context.InterestTags
                .AnyAsync(t => t.InterestTagId != id && (t.NameEN.ToLower() == nameEn.ToLower() || t.NameZH.ToLower() == nameZh.ToLower()));
            if (duplicate) return BadRequest(new { message = "Another interest topic with the same name already exists." });

            entity.NameEN = nameEn;
            entity.NameZH = nameZh;
            _context.InterestTags.Update(entity);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Successfully updated.", data = new { entity.InterestTagId, entity.NameEN, entity.NameZH } });
        }

        // DELETE: api/interesttags/5
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.InterestTags.FindAsync(id);
            if (entity == null)
                return NotFound(new { message = $"Interest topic with id {id} does not exist." });

            var inUse = await _context.Members
                .AnyAsync(m => m.Interests.Any(t => t.InterestTagId == id));
            if (inUse) return BadRequest(new { message = "Cannot delete interest topic because it is assigned to one or more members." });

            _context.InterestTags.Remove(entity);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Successfully deleted." });
        }
    }
}