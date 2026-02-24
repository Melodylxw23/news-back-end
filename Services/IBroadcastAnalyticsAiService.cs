using News_Back_end.DTOs;

namespace News_Back_end.Services
{
    public interface IBroadcastAnalyticsAiService
    {
        Task<string> GenerateRecommendationsAsync(object analyticsSnapshot, CancellationToken cancellationToken);
    }
}
