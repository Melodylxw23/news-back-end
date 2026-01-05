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
                .OrderBy(i => i.Name)
                .Select(i => new { i.InterestTagId, i.Name })
                .ToListAsync();

            return Ok(new { message = "List retrieved successfully.", data = list });
        }

        // GET: api/interesttags/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var tag = await _context.InterestTags
                .Where(t => t.InterestTagId == id)
                .Select(t => new { t.InterestTagId, t.Name })
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

            var name = dto.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return BadRequest(new { message = "Name is required." });

            var exists = await _context.InterestTags
                .AnyAsync(t => t.Name.ToLower() == name.ToLower());
            if (exists) return BadRequest(new { message = "Interest topic with the same name already exists." });

            var entity = new InterestTag { Name = name };
            _context.InterestTags.Add(entity);
            await _context.SaveChangesAsync();

            var response = new InterestResponseDTO
            {
                InterestTagId = entity.InterestTagId,
                Name = entity.Name
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

            var name = dto.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return BadRequest(new { message = "Name is required." });

            var duplicate = await _context.InterestTags
                .AnyAsync(t => t.InterestTagId != id && t.Name.ToLower() == name.ToLower());
            if (duplicate) return BadRequest(new { message = "Another interest topic with the same name already exists." });

            entity.Name = name;
            _context.InterestTags.Update(entity);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Successfully updated.", data = new { entity.InterestTagId, entity.Name } });
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