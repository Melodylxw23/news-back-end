using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using News_Back_end.Services;

namespace News_Back_end.Controllers
{
 [ApiController]
 [Route("api/openai")]
 public class OpenAiTestController : ControllerBase
 {
 private readonly OpenAIChatClient _chat;

 public OpenAiTestController(OpenAIChatClient chat)
 {
 _chat = chat;
 }

 // GET api/openai/ping
 // Unauthenticated endpoint to validate the configured OpenAI API key and runtime wiring.
 [HttpGet("ping")]
 [AllowAnonymous]
 public async Task<IActionResult> Ping()
 {
 try
 {
 // Use a minimal, deterministic prompt and small token budget to validate connectivity
 var system = "You are a tiny test assistant. Respond with a short single-line confirmation.";
 var user = "Ping from backend. Reply only with: {\"pong\":true}";

 var aiText = await _chat.CreateChatCompletionAsync(system, user, maxTokens:30);

 // Return raw AI text so caller can inspect exact output
 return Ok(new { ok = true, raw = aiText });
 }
 catch (Exception ex)
 {
 return StatusCode(502, new { ok = false, error = ex.Message });
 }
 }
 }
}
