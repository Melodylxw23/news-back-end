using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using News_Back_end.Models.SQLServer;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace News_Back_end.Services
{
 public class ScheduledPublishHostedService : BackgroundService
 {
 private readonly IServiceProvider _services;
 private readonly ILogger<ScheduledPublishHostedService> _logger;
 private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

 public ScheduledPublishHostedService(IServiceProvider services, ILogger<ScheduledPublishHostedService> logger)
 {
 _services = services;
 _logger = logger;
 }

 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 _logger.LogInformation("ScheduledPublishHostedService started");
 while (!stoppingToken.IsCancellationRequested)
 {
 try
 {
 using var scope = _services.CreateScope();
 var db = scope.ServiceProvider.GetRequiredService<MyDBContext>();
 var pubService = scope.ServiceProvider.GetRequiredService<IPublicationService>();
 var now = DateTime.UtcNow;
 var due = await db.PublicationDrafts
 .Include(d => d.InterestTags)
 .Where(d => d.ScheduledAt != null && d.ScheduledAt <= now && !d.IsPublished)
 .ToListAsync(stoppingToken);

 foreach (var draft in due)
 {
 try
 {
 var (ok, err) = await pubService.PublishDraftAsync(draft, draft.ScheduledAt, "scheduler");
 if (!ok)
 {
 _logger.LogWarning("Scheduled publish failed for draft {id}: {err}", draft.PublicationDraftId, err);
 }
 else
 {
 _logger.LogInformation("Published draft {id} via scheduler", draft.PublicationDraftId);
 }
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error publishing draft {id}", draft.PublicationDraftId);
 }
 }
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Scheduled publish loop error");
 }

 await Task.Delay(_interval, stoppingToken);
 }
 }
 }
}
