using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly GmailEmailService _emailService;

    public AccountController(GmailEmailService emailService)
    {
        _emailService = emailService;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendEmail(string toEmail, string name)
    {
        string subject = "Welcome to Our Platform";
        string body = $"<h2>Hello {name},</h2><p>Your account has been created successfully.</p>";

        await _emailService.SendEmailAsync(toEmail, subject, body);

        return Ok($"✅ Email sent to {toEmail}");
    }
}