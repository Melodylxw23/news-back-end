using Microsoft.EntityFrameworkCore;
using News_Back_end.Models.SQLServer;
using News_Back_end.Services;

namespace News_Back_end.Services
{
    /// <summary>
    /// Background service that automatically sends scheduled broadcasts
    /// Runs every minute to check for broadcasts that need to be sent
    /// </summary>
    public class BroadcastSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BroadcastSchedulerService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Check every minute

        public BroadcastSchedulerService(
            IServiceProvider serviceProvider,
            ILogger<BroadcastSchedulerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[BroadcastScheduler] Starting scheduled broadcast service...");

            // Wait a bit before first run to let the app fully start
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessScheduledBroadcastsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[BroadcastScheduler] Error occurred while processing scheduled broadcasts");
                }

                // Wait before checking again
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("[BroadcastScheduler] Scheduled broadcast service is stopping.");
        }

        private async Task ProcessScheduledBroadcastsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MyDBContext>();
            var broadcastService = scope.ServiceProvider.GetRequiredService<IBroadcastSendingService>();

            var now = DateTimeOffset.UtcNow;

            _logger.LogDebug("[BroadcastScheduler] Checking for scheduled broadcasts at {Time}", now);

            // Find broadcasts that should be sent now
            var scheduledBroadcasts = await context.BroadcastMessages
                .Where(b => b.Status == BroadcastStatus.Scheduled &&
                            b.ScheduledSendAt != null &&
                            b.ScheduledSendAt <= now)
                .ToListAsync(stoppingToken);

            if (!scheduledBroadcasts.Any())
            {
                _logger.LogDebug("[BroadcastScheduler] No scheduled broadcasts found for sending at {Time}", now);
                return;
            }

            _logger.LogInformation("[BroadcastScheduler] Found {Count} scheduled broadcasts ready to send", scheduledBroadcasts.Count);

            foreach (var broadcast in scheduledBroadcasts)
            {
                // Guard: if this broadcast was already sent by a concurrent scheduler instance
                if (broadcast.Status == BroadcastStatus.Sent)
                    continue;

                var broadcastId = broadcast.Id;
                var scheduledFor = broadcast.ScheduledSendAt;

                try
                {
                    _logger.LogInformation(
                        "[BroadcastScheduler] Processing scheduled broadcast: {BroadcastId} - '{Title}' (scheduled for {ScheduledTime})",
                        broadcastId, broadcast.Title, scheduledFor);

                    // IMPORTANT:
                    // Do NOT flip to Draft. `BroadcastSendingService.SendBroadcastAsync` will reject a Sent broadcast,
                    // but it allows Scheduled and will set status to Sent after successful sends.
                    // Setting to Draft can break tracking/analytics expectations and makes it harder to debug.

                    var result = await broadcastService.SendBroadcastAsync(broadcastId);

                    if (result.SuccessfulSends > 0)
                    {
                        _logger.LogInformation(
                            "[BroadcastScheduler] Successfully sent scheduled broadcast {BroadcastId}: {Successful}/{Total} recipients",
                            broadcastId, result.SuccessfulSends, result.TotalRecipients);

                        // Ensure status is persisted as Sent even if sending service didn't update it
                        broadcast.Status = BroadcastStatus.Sent;
                        broadcast.UpdatedAt = DateTimeOffset.UtcNow;
                        await context.SaveChangesAsync(stoppingToken);

                        if (result.Errors.Any())
                        {
                            _logger.LogWarning(
                                "[BroadcastScheduler] Broadcast {BroadcastId} had {ErrorCount} errors: {Errors}",
                                broadcastId, result.Errors.Count, string.Join("; ", result.Errors));
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[BroadcastScheduler] Scheduled broadcast {BroadcastId} produced zero successful sends. Errors: {Errors}",
                            broadcastId, string.Join("; ", result.Errors));

                        // Keep it scheduled so it can be retried after the underlying issue is fixed.
                        // (Optionally you could add a retry limit with a failure counter column.)
                        broadcast.Status = BroadcastStatus.Scheduled;
                        broadcast.UpdatedAt = DateTimeOffset.UtcNow;
                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // E.g., "already sent" from SendBroadcastAsync.
                    _logger.LogWarning(ex, "[BroadcastScheduler] Broadcast {BroadcastId} skipped", broadcastId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[BroadcastScheduler] Failed to send scheduled broadcast {BroadcastId} - '{Title}'",
                        broadcastId, broadcast.Title);

                    // Revert to Scheduled status on error so it can be retried
                    broadcast.Status = BroadcastStatus.Scheduled;
                    broadcast.UpdatedAt = DateTimeOffset.UtcNow;
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[BroadcastScheduler] Stopping scheduled broadcast service...");
            await base.StopAsync(stoppingToken);
        }
    }
}