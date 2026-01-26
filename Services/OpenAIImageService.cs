using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace News_Back_end.Services
{
 public class OpenAIImageService : IImageGenerationService
 {
 private readonly HttpClient _client;
 private readonly string _apiKey;
 private readonly string _baseUrl;

 public OpenAIImageService(HttpClient client, IConfiguration config)
 {
 _client = client;
 _apiKey = config["OpenAI:ApiKey"] ?? string.Empty;
 _baseUrl = config["OpenAI:BaseUrl"] ?? "https://api.openai.com/";
 }

 public async Task<string?> GenerateImageAsync(int newsArticleId, string prompt, string? style = null)
 {
 if (string.IsNullOrWhiteSpace(_apiKey)) throw new InvalidOperationException("OpenAI ApiKey not configured");

 // Use the Images endpoint for DALL·E-style generation (OpenAI v1/images/generations). This is a simple example; adapt per your OpenAI plan.
 var request = new
 {
 prompt = prompt + (string.IsNullOrWhiteSpace(style) ? string.Empty : (" style: " + style)),
 n =1,
 size = "1024x1024"
 };

 var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_baseUrl), "v1/images/generations"))
 {
 Content = JsonContent.Create(request)
 };
 req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

 var res = await _client.SendAsync(req);
 if (!res.IsSuccessStatusCode)
 {
 var txt = await res.Content.ReadAsStringAsync();
 Console.WriteLine("OpenAI image generation failed: " + txt);
 return null;
 }

 using var stream = await res.Content.ReadAsStreamAsync();
 using var doc = await JsonDocument.ParseAsync(stream);
 // response has data[0].url in many OpenAI responses
 if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() >0)
 {
 var url = data[0].GetProperty("url").GetString();
 return url;
 }

 return null;
 }
 }
}
