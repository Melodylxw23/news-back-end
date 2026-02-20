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
    public class IndustryTagsController : ControllerBase
    {
        private readonly MyDBContext _context;

        public IndustryTagsController(MyDBContext context)
        {
            _context = context;
        }

        // GET: api/industrytags
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _context.IndustryTags
                .OrderBy(i => i.NameEN)
                .Select(i => new { i.IndustryTagId, i.NameEN, i.NameZH })
                .ToListAsync();

            return Ok(new { message = "List retrieved successfully.", data = list });
        }

        // GET: api/industrytags/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var tag = await _context.IndustryTags
                .Where(t => t.IndustryTagId == id)
                .Select(t => new { t.IndustryTagId, t.NameEN, t.NameZH })
                .FirstOrDefaultAsync();

            if (tag == null)
                return NotFound(new { message = $"Industry with id {id} does not exist." });

            return Ok(new { message = "Retrieved successfully.", data = tag });
        }

        // POST: api/industrytags
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateIndustryDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var nameEn = dto.NameEN?.Trim();
            var nameZh = dto.NameZH?.Trim();
            if (string.IsNullOrEmpty(nameEn) || string.IsNullOrEmpty(nameZh)) return BadRequest(new { message = "Both NameEN and NameZH are required." });

            var exists = await _context.IndustryTags
                .AnyAsync(t => t.NameEN.ToLower() == nameEn.ToLower() || t.NameZH.ToLower() == nameZh.ToLower());
            if (exists) return BadRequest(new { message = "Industry with the same name already exists." });

            var entity = new IndustryTag { NameEN = nameEn, NameZH = nameZh };
            _context.IndustryTags.Add(entity);
            await _context.SaveChangesAsync();

            var response = new IndustryResponseDTO
            {
                IndustryTagId = entity.IndustryTagId,
                NameEN = entity.NameEN,
                NameZH = entity.NameZH
            };

            return CreatedAtAction(nameof(GetById), new { id = entity.IndustryTagId },
                new { message = "Successfully created.", data = response });
        }

        // PUT: api/industrytags/5
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateIndustryDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = await _context.IndustryTags.FindAsync(id);
            if (entity == null) return NotFound(new { message = $"Industry with id {id} does not exist." });
            var nameEn = dto.NameEN?.Trim();
            var nameZh = dto.NameZH?.Trim();
            if (string.IsNullOrEmpty(nameEn) || string.IsNullOrEmpty(nameZh)) return BadRequest(new { message = "Both NameEN and NameZH are required." });

            var duplicate = await _context.IndustryTags
                .AnyAsync(t => t.IndustryTagId != id && (t.NameEN.ToLower() == nameEn.ToLower() || t.NameZH.ToLower() == nameZh.ToLower()));
            if (duplicate) return BadRequest(new { message = "Another industry with the same name already exists." });

            entity.NameEN = nameEn;
            entity.NameZH = nameZh;
            _context.IndustryTags.Update(entity);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Successfully updated.", data = new { entity.IndustryTagId, entity.NameEN, entity.NameZH } });
        }

        // DELETE: api/industrytags/5
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.IndustryTags.FindAsync(id);
            if (entity == null)
                return NotFound(new { message = $"Industry with id {id} does not exist." });

            var inUse = await _context.Members
                .AnyAsync(m => m.IndustryTags.Any(t => t.IndustryTagId == id));
            if (inUse) return BadRequest(new { message = "Cannot delete industry because it is assigned to one or more members." });

            _context.IndustryTags.Remove(entity);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Successfully deleted." });
        }
    }
}