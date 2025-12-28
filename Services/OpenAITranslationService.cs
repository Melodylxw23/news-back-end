using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace News_Back_end.Services
{
    // Minimal OpenAI-based translator/detector using chat completions
    public class OpenAITranslationService : ITranslationService
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public OpenAITranslationService(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task<string> DetectLanguageAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "en";

            try
            {
                var system = "You are a language detection assistant. Reply with the two-letter ISO language code only (e.g., en, zh, fr).";
                var user = text.Length > 1000 ? text.Substring(0, 1000) : text;

                var resp = await CreateChatCompletionAsync(system, user);
                var code = resp?.Trim();
                if (string.IsNullOrWhiteSpace(code)) return "en";
                // normalize to lower 2-letter if possible
                code = code.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (code.Length > 2) code = code.Substring(0, 2);
                return code.ToLowerInvariant();
            }
            catch
            {
                return "en";
            }
        }

        public async Task<string> TranslateAsync(string text, string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            if (string.IsNullOrWhiteSpace(targetLanguage)) return text;

            try
            {
                var system = "You are a helpful translator. Translate the user's text to the target language and return only the translation without explanations.";
                var user = $"Translate the following text to {targetLanguage} (respond only with the translated text):\n\n" + (text.Length > 3000 ? text.Substring(0, 3000) : text);

                var resp = await CreateChatCompletionAsync(system, user);
                return resp ?? string.Empty;
            }
            catch
            {
                return text; // fallback to original
            }
        }

        private async Task<string?> CreateChatCompletionAsync(string systemMessage, string userMessage)
        {
            var payload = new
            {
                model = "gpt-3.5-turbo",
                messages = new[] {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = userMessage }
                },
                temperature = 0.0,
                max_tokens = 1024
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync("v1/chat/completions", content);
            if (!resp.IsSuccessStatusCode) return null;

            using var stream = await resp.Content.ReadAsStreamAsync();
            var doc = await JsonSerializer.DeserializeAsync<JsonElement>(stream, _jsonOptions);
            if (doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentEl))
                {
                    return contentEl.GetString();
                }
            }
            return null;
        }
    }
}
