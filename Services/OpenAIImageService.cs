using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace News_Back_end.Services
{
    public class OpenAIImageService : IImageGenerationService
    {
        private readonly HttpClient _client;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly ILogger<OpenAIImageService> _logger;

        public OpenAIImageService(HttpClient client, IConfiguration config, ILogger<OpenAIImageService> logger)
        {
            _client = client;
            _apiKey = config["OpenAIHeroImageCreation:ApiKey"] ?? string.Empty;
            _baseUrl = config["OpenAIHeroImageCreation:BaseUrl"] ?? config["OpenAI:BaseUrl"] ?? "https://api.openai.com/";
            _logger = logger;
        }

        public async Task<string?> GenerateImageAsync(int newsArticleId, string prompt, string? style = null)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("OpenAI ApiKey not configured for hero image generation");

            var fullPrompt = prompt;
            if (!string.IsNullOrWhiteSpace(style))
                fullPrompt += $" Style: {style}";

            // Truncate prompt if too long for DALL-E (max ~4000 chars)
            if (fullPrompt.Length > 3800)
                fullPrompt = fullPrompt.Substring(0, 3800);

            var request = new
            {
                prompt = fullPrompt,
                n = 1,
                size = "1024x1024",
                response_format = "url"
            };

            var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_baseUrl), "v1/images/generations"))
            {
                Content = JsonContent.Create(request)
            };
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            _logger.LogInformation("Generating hero image for article {id}, prompt length={len}", newsArticleId, fullPrompt.Length);

            var res = await _client.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                var txt = await res.Content.ReadAsStringAsync();
                _logger.LogWarning("OpenAI image generation failed: {status} {response}", res.StatusCode, txt);
                throw new HttpRequestException($"OpenAI image generation failed: {res.StatusCode} - {txt}");
            }

            using var stream = await res.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
            {
                if (data[0].TryGetProperty("url", out var urlProp))
                {
                    var url = urlProp.GetString();
                    _logger.LogInformation("Hero image generated successfully for article {id}", newsArticleId);
                    return url;
                }
                if (data[0].TryGetProperty("b64_json", out var b64Prop))
                {
                    var b64 = b64Prop.GetString();
                    _logger.LogInformation("Hero image generated (base64) for article {id}", newsArticleId);
                    return $"data:image/png;base64,{b64}";
                }
            }

            _logger.LogWarning("OpenAI image generation returned unexpected response structure for article {id}", newsArticleId);
            return null;
        }
    }
}
