using Microsoft.AspNetCore.Mvc;
using News_Back_end.Models.SQLServer;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace News_Back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly MyDBContext _db;

        public AnalyticsController(MyDBContext db)
        {
            _db = db;
        }

        // GET api/analytics/fetch-overview?hours=24
        [HttpGet("fetch-overview")]
        public async Task<IActionResult> FetchOverview([FromQuery] int hours = 24)
        {
            var since = System.DateTime.Now.AddHours(-hours);
            var q = _db.FetchMetrics.Where(f => f.Timestamp >= since);
            var total = await q.CountAsync();
            var success = await q.CountAsync(f => f.Success);
            var failed = total - success;

            // per-source summary
            var perSource = await q.GroupBy(f => f.SourceId)
                .Select(g => new {
                    SourceId = g.Key,
                    Attempts = g.Count(),
                    Success = g.Count(x => x.Success),
                    Failures = g.Count(x => !x.Success),
                    AvgDurationMs = (int?)g.Average(x => x.DurationMs) ?? 0,
                    AvgItems = (int?)g.Average(x => x.ItemsFetched) ?? 0
                }).ToListAsync();

            return Ok(new {
                hours,
                total,
                success,
                failed,
                perSource
            });
        }
    }
}
