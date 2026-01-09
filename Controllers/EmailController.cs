using Microsoft.AspNetCore.Mvc;
using News_Back_end.Services;
using News_Back_end.Models;

namespace News_Back_end.Controllers
{
    [ApiController]
    [Route("api/email")]
    public class EmailController : ControllerBase
    {
        private readonly IEmailSender _emailSender;

        public EmailController(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] EmailRequest request)
        {
            await _emailSender.SendEmailAsync(
                request.To,
                request.Subject,
                request.Body);

            return Ok("Email sent");
        }
    }

}
