using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using News_Back_end;
using News_Back_end.Services;
using News_Back_end.Models.SQLServer;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;
using System.Linq;

[ApiController]
[Route("api/chat")]
[Authorize(Roles = "Member,Admin")]
public class ChatController : ControllerBase
{
    private readonly OpenAIChatClient _chat;
    private readonly MyDBContext _db;
    private readonly ILogger<ChatController> _logger;
    private readonly IWebHostEnvironment _env;

    public ChatController(OpenAIChatClient chat, MyDBContext db, ILogger<ChatController> logger, IWebHostEnvironment env)
    {
        _chat = chat;
        _db = db;
        _logger = logger;
        _env = env;
    }

    [HttpPost("ask-about-article")]
    [AllowAnonymous] // allow anonymous for testing; remove or tighten once client auth works
    public async Task<IActionResult> AskAboutArticle([FromBody] ArticleQuestion request)
    {
        _logger.LogInformation("AskAboutArticle called. Title length: {Len}", request?.ArticleTitle?.Length ?? 0);

        if (request == null || string.IsNullOrWhiteSpace(request.ArticleContent) || string.IsNullOrWhiteSpace(request.ArticleTitle))
        {
            return BadRequest(new { error = "Invalid request: articleTitle and articleContent are required" });
        }

        try
        {
            var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            _logger.LogInformation("UserId from token: {UserId}", userId ?? "(none)");

            Member? member = null;
            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    _logger.LogInformation("Looking up member for userId: {UserId}", userId);
                    member = await _db.Members
                        .Include(m => m.IndustryTags)
                        .FirstOrDefaultAsync(m => m.ApplicationUserId == userId);
                    _logger.LogInformation("Member lookup result: {MemberId}", member?.MemberId ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DB lookup failed in AskAboutArticle");
                    if (_env.IsDevelopment())
                        return StatusCode(500, new { error = "DB lookup failed", detail = ex.Message, stack = ex.ToString() });
                    return StatusCode(500, new { error = "DB lookup failed" });
                }
            }

            // If DI failed and _chat is null, return a clear error
            if (_chat == null)
            {
                _logger.LogError("OpenAIChatClient (_chat) is null in ChatController");
                if (_env.IsDevelopment())
                    return StatusCode(500, new { error = "OpenAIChatClient not configured" });
                return StatusCode(500, new { error = "Internal server error" });
            }

            // If not authenticated or member not found, use general industry
            var industry = member?.IndustryTags?.FirstOrDefault()?.NameEN ?? "general";

            var systemPrompt = $@"You are a helpful assistant for business professionals in the {industry} industry. 
Answer questions about articles clearly and concisely. Keep responses under 150 words.
If the user asks something not related to the article, politely redirect them.";

            // Build message list: system, optional history, then current user prompt
            var messages = new List<(string role, string content)>();
            messages.Add(("system", systemPrompt));

            if (request.History != null && request.History.Any())
            {
                foreach (var h in request.History)
                {
                    // normalize role to 'user' or 'assistant'
                    var role = string.IsNullOrWhiteSpace(h.Role) ? "user" : h.Role.ToLower() == "assistant" ? "assistant" : "user";
                    messages.Add((role, h.Content ?? string.Empty));
                }
            }

            // current user message includes article context and the question
            var currentUserContent = $"Article Title: {request.ArticleTitle}\nArticle Content: {request.ArticleContent}\n\nUser Question: {request.Question}";
            messages.Add(("user", currentUserContent));

            string aiResponse;
            try
            {
                aiResponse = await _chat.CreateChatCompletionAsync(messages, maxTokens: 300) ?? string.Empty;
                _logger.LogInformation("OpenAI returned {Len} chars", aiResponse.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI call failed in AskAboutArticle");
                if (_env.IsDevelopment())
                    return StatusCode(502, new { error = "OpenAI call failed", detail = ex.Message, stack = ex.ToString() });
                return StatusCode(502, new { error = "OpenAI call failed" });
            }

            return Ok(new { answer = aiResponse });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AskAboutArticle");
            if (_env.IsDevelopment())
                return StatusCode(500, new { error = "Internal server error", detail = ex.Message, stack = ex.ToString() });
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public class ArticleQuestion
{
    public string ArticleTitle { get; set; } = string.Empty;
    public string ArticleContent { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;

    // optional conversation history from frontend: array of { role: 'user' | 'assistant', content: string }
    public List<MessageDto>? History { get; set; }
}

public class MessageDto
{
    public string Role { get; set; } = "user"; // 'user' or 'assistant'
    public string Content { get; set; } = string.Empty;
}