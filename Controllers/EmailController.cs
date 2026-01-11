using Microsoft.AspNetCore.Mvc;
using News_Back_end.Services;
using News_Back_end.Models;
using Microsoft.Extensions.Logging;

namespace News_Back_end.Controllers
{
    [ApiController]
    [Route("api/email")]
    public class EmailController : ControllerBase
    {
        private readonly IEmailSender _emailSender;
        private readonly ILogger<EmailController> _logger;

        public EmailController(IEmailSender emailSender, ILogger<EmailController> logger)
        {
            _emailSender = emailSender;
            _logger = logger;
        }

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] EmailRequest request)
        {
            if (request == null)
                return BadRequest("Request body required");
            if (string.IsNullOrWhiteSpace(request.To))
                return BadRequest("To is required");
            if (string.IsNullOrWhiteSpace(request.Subject))
                return BadRequest("Subject is required");
            if (string.IsNullOrWhiteSpace(request.Body))
                return BadRequest("Body is required");

            try
            {
                await _emailSender.SendEmailAsync(
                    request.To,
                    request.Subject,
                    request.Body);

                return Ok("Email sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", request.To);
                return StatusCode(500, "Failed to send email: " + ex.Message);
            }
        }
    }

}
