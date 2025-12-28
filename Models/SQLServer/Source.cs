using System;
using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
    public enum SourceType { rss, api, html }
    public enum Languages { EN, ZH }
    public enum MediaOwnership { Public, Private }
    public enum RegionLevel { National, Local }

    public class Source
    {
        [Key]
        public int SourceId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        public string BaseUrl { get; set; } = null!;

        // how we crawl
        [Required]
        public SourceType Type { get; set; }

        // language of content
        [Required]
        public Languages Language { get; set; }

        // editorial metadata (from your media list)
        public MediaOwnership Ownership { get; set; }
        public RegionLevel RegionLevel { get; set; }

        // crawling management
        public int CrawlFrequency { get; set; }
        public DateTime? LastCrawledAt { get; set; }
        public bool IsActive { get; set; } = true;

        // human-readable explanation
        [MaxLength(1000)]
        public string? Description { get; set; }

        // technical notes (selectors, auth, API keys)
        [MaxLength(500)]
        public string? Notes { get; set; }
    }

}
