using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace News_Back_end.Services
{
 // Dedicated AI generation service using OpenAI Chat Completions.
 // This does not touch ITranslationService or OpenAITranslationService.
 public class OpenAIBroadcastService : IAiBroadcastService
 {
 private readonly HttpClient _http;
 private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

 public OpenAIBroadcastService(HttpClient http)
 {
 _http = http;
 }

 public async Task<string> GenerateAsync(string prompt, string language = "en")
 {
 var payload = new
 {
 model = "gpt-3.5-turbo",
 messages = new[] {
 new { role = "system", content = "You are an assistant that produces JSON with keys: title, subject, body. Do not add extra text."},
 new { role = "user", content = prompt }
 },
 temperature =0.3,
 max_tokens =800
 };

 var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
 using var resp = await _http.PostAsync("v1/chat/completions", content);
 var body = await resp.Content.ReadAsStringAsync();
 if (!resp.IsSuccessStatusCode)
 {
 Console.WriteLine($"OpenAI generation error: {(int)resp.StatusCode} {resp.ReasonPhrase}: {body}");
 throw new HttpRequestException($"OpenAI returned {(int)resp.StatusCode}: {body}");
 }
 try
 {
 using var stream = await resp.Content.ReadAsStreamAsync();
 var doc = await JsonSerializer.DeserializeAsync<JsonElement>(stream, _jsonOptions);
 if (doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty("choices", out var choices) && choices.GetArrayLength() >0)
 {
 var first = choices[0];
 if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentEl))
 {
 return contentEl.GetString() ?? string.Empty;
 }
 }
 return string.Empty;
 }
 catch (JsonException ex)
 {
 Console.WriteLine($"Failed to parse OpenAI generation response: {ex.Message}. Body: {body}");
 throw;
 }
 }
 }
}
