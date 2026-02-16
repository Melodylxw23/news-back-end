using System;

namespace News_Back_end.Models.SQLServer
{
    public class FetchMetric
    {
        public int FetchMetricId { get; set; }
        public int SourceId { get; set; }
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public int ItemsFetched { get; set; }
        public int DurationMs { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
