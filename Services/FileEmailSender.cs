using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace News_Back_end.Services
{
    /// <summary>
    /// Development email sender that writes outgoing emails to disk for inspection.
    /// </summary>
    public class FileEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;

        public FileEmailSender(IConfiguration config)
        {
            _config = config;
        }

        public Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var outDir = _config["Email:OutputDirectory"] ?? Path.Combine(Directory.GetCurrentDirectory(), "Emails");
            Directory.CreateDirectory(outDir);

            var fileName = Path.Combine(outDir, $"email_{DateTime.UtcNow:yyyyMMdd_HHmmss_ffff}.html");
            var content = $"To: {toEmail}\nSubject: {subject}\n\n{htmlMessage}";
            File.WriteAllText(fileName, content);
            return Task.CompletedTask;
        }
    }
}
