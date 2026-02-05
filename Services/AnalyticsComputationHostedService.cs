using News_Back_end.Services;

namespace News_Back_end.Services
{
    /// <summary>
    /// Background service that periodically computes and updates broadcast analytics summaries
    /// </summary>
    public class AnalyticsComputationHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AnalyticsComputationHostedService> _logger;
        private readonly TimeSpan _computeInterval = TimeSpan.FromHours(1); // Run every hour

    public AnalyticsComputationHostedService(
            IServiceProvider serviceProvider,
        ILogger<AnalyticsComputationHostedService> logger)
        {
         _serviceProvider = serviceProvider;
        _logger = logger;
}

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
  _logger.LogInformation("Analytics Computation Service is starting.");

            // Wait a bit before first run to let the app fully start
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

      while (!stoppingToken.IsCancellationRequested)
    {
  try
        {
       await ComputeAnalyticsAsync(stoppingToken);
      }
       catch (Exception ex)
        {
      _logger.LogError(ex, "Error occurred while computing analytics summaries.");
     }

     await Task.Delay(_computeInterval, stoppingToken);
            }

     _logger.LogInformation("Analytics Computation Service is stopping.");
      }

        private async Task ComputeAnalyticsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var analyticsService = scope.ServiceProvider.GetRequiredService<IBroadcastAnalyticsService>();

         _logger.LogInformation("Starting analytics computation...");

 try
    {
      // Compute broadcast analytics summaries
    await analyticsService.ComputeAnalyticsSummariesAsync();
 _logger.LogInformation("Broadcast analytics summaries computed successfully.");

     // Update member engagement profiles
       await analyticsService.UpdateMemberEngagementProfilesAsync();
          _logger.LogInformation("Member engagement profiles updated successfully.");
            }
            catch (Exception ex)
   {
       _logger.LogError(ex, "Failed to compute analytics.");
    throw;
        }
        }
    }
}
