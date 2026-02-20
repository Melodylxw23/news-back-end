using System;

namespace News_Back_end.DTOs
{
    public class CrawlerDTO
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? SourceURL { get; set; }
        public DateTime PublishedDate { get; set; }
        public string? OriginalLanguage { get; set; }
    }
}
