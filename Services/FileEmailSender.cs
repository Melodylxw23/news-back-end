using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace News_Back_end.Services
{
    /// <summary>
    /// Development email sender that writes outgoing emails to disk for inspection.
    /// Defaults to a temp folder outside the project to avoid triggering file-watch reloads.
    /// </summary>
    public class FileEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<FileEmailSender> _logger;

        public FileEmailSender(IConfiguration config, ILogger<FileEmailSender> logger)
        {
            _config = config;
            _logger = logger;
        }

        public Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            // Prefer explicit config, otherwise use a temp folder outside the project directory
            var configured = _config["Email:OutputDirectory"];
            string outDir;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                outDir = configured;
            }
            else
            {
                var tmp = Path.GetTempPath();
                outDir = Path.Combine(tmp, "NewsBackEnd_Emails");
            }

            try
            {
                Directory.CreateDirectory(outDir);

                var fileName = Path.Combine(outDir, $"email_{System.DateTime.UtcNow:yyyyMMdd_HHmmss_ffff}.html");
                var content = $"To: {toEmail}\nSubject: {subject}\n\n{htmlMessage}";
                File.WriteAllText(fileName, content);

                _logger.LogInformation("Wrote email to {Path}", fileName);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed writing email to disk");
                throw;
            }

            return Task.CompletedTask;
        }
    }
}
