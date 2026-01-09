using Microsoft.Extensions.Options;
using News_Back_end.Models;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace News_Back_end.Services
{
    public class EmailService : IEmailSender
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var message = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            message.To.Add(toEmail);

            using var smtp = new SmtpClient(_settings.SmtpServer, _settings.Port)
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                EnableSsl = true
            };

            try
            {
                await smtp.SendMailAsync(message);
            }
            catch (SmtpException sex)
            {
                Console.WriteLine($"SMTP error: {sex.StatusCode} {sex.Message}");
                throw;
            }
        }
    }
}

