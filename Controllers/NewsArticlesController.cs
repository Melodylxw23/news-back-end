using Microsoft.AspNetCore.Mvc;
using News_Back_end.Models.SQLServer;
using News_Back_end.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace News_Back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsArticlesController : ControllerBase
    {
        private readonly MyDBContext _db;

        public NewsArticlesController(MyDBContext db)
        {
            _db = db;
        }

        // GET: /api/newsarticles/recent?limit=20
        // If limit == 0 will return all articles (use with care). Otherwise limit is clamped to [1, 10000].
        [HttpGet("recent")]
        public async Task<IActionResult> Recent([FromQuery] int? limit = 20)
        {
            const int AbsoluteMax = 10000;

            IQueryable<ArticleDto> query = _db.NewsArticles
                .AsNoTracking()
                .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
                .Select(a => new ArticleDto(
                    a.NewsArticleId,
                    a.Title,
                    a.OriginalContent ?? string.Empty,
                    a.OriginalLanguage ?? string.Empty,
                    a.TranslationLanguage,
                    a.TranslationStatus,
                    a.SourceURL,
                    a.PublishedAt,
                    a.CrawledAt,
                    a.SourceId));

            if (limit.HasValue)
            {
                if (limit.Value == 0)
                {
                    // no limit, return all
                }
                else
                {
                    var l = limit.Value;
                    if (l < 1) l = 1;
                    if (l > AbsoluteMax) l = AbsoluteMax;
                    query = query.Take(l);
                }
            }

            var dtos = await query.ToListAsync();
            return Ok(dtos);
        }
    }
}
