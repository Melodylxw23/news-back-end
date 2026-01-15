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
 var systemInstruction = "You are an assistant that produces valid JSON only with these keys: title, subject, body.\n" +
 "- title: a short headline appropriate for a broadcast (up to ~12 words).\n" +
 "- subject: a short subject line suitable for an email or notification (up to ~20 words).\n" +
 "- body: a detailed message intended for users; produce a long, informative, well-structured paragraph or multiple paragraphs (minimum150 words, prefer ~200-300 words).\n" +
 "Do NOT include any additional keys or commentary. Return a single JSON object and nothing else.\n" +
 "Example output (JSON shown using single quotes for clarity):\n" +
 "{'title':'Upcoming maintenance on Jan20','subject':'Service maintenance scheduled Jan20 ¡ª what to expect','body':'We will perform scheduled maintenance on Jan20. During this window, you may experience brief service interruptions. The maintenance will include... (continue with several sentences to reach the requested length)'}";

 var payload = new
 {
 model = "gpt-3.5-turbo",
 messages = new[] {
 new { role = "system", content = systemInstruction },
 new { role = "user", content = prompt }
 },
 temperature =0.6,
 max_tokens =1500
 };

 var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
 using var resp = await _http.PostAsync("v1/chat/completions", content);
 var respBody = await resp.Content.ReadAsStringAsync();
 if (!resp.IsSuccessStatusCode)
 {
 Console.WriteLine($"OpenAI generation error: {(int)resp.StatusCode} {resp.ReasonPhrase}: {respBody}");
 throw new HttpRequestException($"OpenAI returned {(int)resp.StatusCode}: {respBody}");
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
 Console.WriteLine($"Failed to parse OpenAI generation response: {ex.Message}. Body: {respBody}");
 throw;
 }
 }
 }
}
