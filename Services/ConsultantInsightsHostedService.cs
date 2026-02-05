namespace News_Back_end.Services
{
 /// <summary>
 /// Background service that checks every minute and sends consultant insight emails when due.
 /// </summary>
 public class ConsultantInsightsHostedService : BackgroundService
 {
 private readonly IServiceProvider _serviceProvider;
 private readonly ILogger<ConsultantInsightsHostedService> _logger;
 private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

 public ConsultantInsightsHostedService(IServiceProvider serviceProvider, ILogger<ConsultantInsightsHostedService> logger)
 {
 _serviceProvider = serviceProvider;
 _logger = logger;
 }

 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 _logger.LogInformation("[ConsultantInsightsHosted] Starting...");

 // small delay so app starts fully
 await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

 while (!stoppingToken.IsCancellationRequested)
 {
 try
 {
 using var scope = _serviceProvider.CreateScope();
 var svc = scope.ServiceProvider.GetRequiredService<IConsultantInsightsEmailService>();
 var nowUtc = DateTimeOffset.UtcNow;
 await svc.SendDueInsightsAsync(nowUtc, stoppingToken);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "[ConsultantInsightsHosted] Error while sending due insights");
 }

 await Task.Delay(_interval, stoppingToken);
 }

 _logger.LogInformation("[ConsultantInsightsHosted] Stopped.");
 }
 }
}
