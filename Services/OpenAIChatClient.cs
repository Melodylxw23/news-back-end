using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace News_Back_end.Services
{
    public class OpenAIChatClient
    {
        private readonly HttpClient _http;
        private readonly string _model;

        public OpenAIChatClient(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _model = cfg["OpenAI:ChatModel"] ?? cfg["OpenAI:Model"] ?? "gpt-4o-mini";

            // Prefer primary OpenAI key, fall back to OpenAIRecommendation if provided
            var apiKey = cfg["OpenAIRecommendation:ApiKey"] ?? cfg["OpenAI:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey) && _http.DefaultRequestHeaders.Authorization == null)
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
        }

        // Existing convenience method (keeps previous signature) forwards to the messages-based API
        public Task<string?> CreateChatCompletionAsync(string systemPrompt, string userPrompt, int maxTokens = 1000, double temperature = 0.7)
        {
            var messages = new List<(string role, string content)>
            {
                ("system", systemPrompt),
                ("user", userPrompt)
            };
            return CreateChatCompletionAsync(messages, maxTokens, temperature);
        }

        // New: accept conversation history as a list of (role, content) tuples
        public async Task<string?> CreateChatCompletionAsync(IEnumerable<(string role, string content)> messages, int maxTokens = 1000, double temperature = 0.7)
        {
            var msgArray = messages.Select(m => new { role = m.role, content = m.content }).ToArray();

            var payload = new
            {
                model = _model,
                messages = msgArray,
                max_tokens = maxTokens,
                temperature = temperature
            };

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) throw new Exception($"OpenAI request failed: {res.StatusCode} {body}");

            using var doc = JsonDocument.Parse(body);
            try
            {
                var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                return content;
            }
            catch
            {
                return body;
            }
        }
    }
}