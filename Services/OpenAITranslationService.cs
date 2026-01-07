using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

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

        public async Task<string> TranslateAsync(string text, string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            if (string.IsNullOrWhiteSpace(targetLanguage)) return text;
            // stronger prompt and chunking to handle long articles reliably
            var system = "You are a precise translator. Translate the following text to the target language. Do NOT summarize, omit, or add content. Preserve paragraph breaks. Return only the translated text with the same paragraph structure.";

            // chunk input to avoid token limits; prefer paragraph boundaries
            var parts = ChunkText(text, 2000).ToArray();
            var results = new List<string>(parts.Length);
            foreach (var part in parts)
            {
                var user = $"Translate the following text to {targetLanguage}:\n\n" + part;
                var chunkResp = await CreateChatCompletionAsync(system, user, 2048);
                results.Add(chunkResp ?? string.Empty);
            }

            return string.Join("\n\n", results);
        }

        private async Task<string?> CreateChatCompletionAsync(string systemMessage, string userMessage, int maxTokens = 1024)
        {
            var payload = new
            {
                model = "gpt-3.5-turbo",
                messages = new[] {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = userMessage }
                },
                temperature = 0.0,
                max_tokens = maxTokens
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync("v1/chat/completions", content);
            var respBody = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                // surface error to logs for debugging
                Console.WriteLine($"OpenAI API error: {(int)resp.StatusCode} {resp.ReasonPhrase}: {respBody}");
                throw new HttpRequestException($"OpenAI API returned {(int)resp.StatusCode}: {respBody}");
            }
            try
            {
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
            catch (JsonException jex)
            {
                Console.WriteLine($"Failed to parse OpenAI response JSON: {jex.Message}. Body: {respBody}");
                throw;
            }
        }

        // Split text into chunks trying to respect paragraph boundaries and a maximum character size
        private static IEnumerable<string> ChunkText(string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;

            // split on empty line (paragraph break)
            var paragraphs = Regex.Split(text, @"\r?\n\s*\r?\n");
            foreach (var p in paragraphs)
            {
                var para = p?.Trim();
                if (string.IsNullOrEmpty(para)) continue;
                if (para.Length <= maxChars)
                {
                    yield return para;
                    continue;
                }

                // break long paragraph into sub-chunks at spaces
                int idx = 0;
                while (idx < para.Length)
                {
                    var remaining = para.Length - idx;
                    var take = Math.Min(maxChars, remaining);
                    if (take == remaining)
                    {
                        yield return para.Substring(idx).Trim();
                        break;
                    }

                    // try to break at last space within range
                    var substr = para.Substring(idx, take);
                    var lastSpace = substr.LastIndexOf(' ');
                    if (lastSpace <= 0)
                    {
                        // no space found, hard cut
                        yield return substr.Trim();
                        idx += take;
                    }
                    else
                    {
                        yield return para.Substring(idx, lastSpace).Trim();
                        idx += lastSpace;
                    }
                }
            }
        }
    }
}
