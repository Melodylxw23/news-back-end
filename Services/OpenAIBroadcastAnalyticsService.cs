using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace News_Back_end.Services
{
    /// <summary>
    /// Uses the OpenAIBroadcastAnalytics HttpClient to generate
    /// human-readable recommendations from analytics snapshots.
    /// </summary>
    public sealed class OpenAIBroadcastAnalyticsService : IBroadcastAnalyticsAiService
    {
        private readonly HttpClient _http;
        private readonly ILogger<OpenAIBroadcastAnalyticsService> _logger;

    public OpenAIBroadcastAnalyticsService(HttpClient http, ILogger<OpenAIBroadcastAnalyticsService> logger)
        {
            _http = http;
            _logger = logger;
    }

        public async Task<string> GenerateRecommendationsAsync(object analyticsSnapshot, CancellationToken cancellationToken)
        {
var snapshotJson = JsonSerializer.Serialize(analyticsSnapshot, new JsonSerializerOptions
            {
    WriteIndented = false,
     DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
      });

            var promptLines = new List<string>
            {
   "You are an expert email marketing analyst.",
        "",
        "Given this broadcast analytics snapshot (JSON), produce recommendations that are:",
     "- Specific and actionable",
   "- Prioritized (High/Medium/Low)",
  "- Include the metric(s) used to justify each recommendation",
     "- Do not mention prompts, models, or that you are an AI",
       "",
    "Return ONLY valid JSON with this schema:",
          "{",
    "  \"summary\": \"string - brief overall assessment\",",
     "  \"recommendations\": [",
                "    {",
                "      \"priority\": \"High|Medium|Low\",",
    "      \"title\": \"string\",",
       "  \"why\": \"string - data-driven justification\",",
                "  \"actions\": [\"string array of specific steps\"],",
    " \"metricsReferenced\": [\"string array of metrics used\"]",
          "}",
      "  ]",
                "}",
       "",
     "SNAPSHOT_JSON:",
         snapshotJson
       };

            var prompt = string.Join("\n", promptLines);

          var body = new
   {
        model = "gpt-4o-mini",
temperature = 0.3,
    response_format = new { type = "json_object" },
          messages = new object[]
        {
                  new { role = "system", content = "You generate concise, data-driven email marketing recommendations." },
        new { role = "user", content = prompt }
     }
          };

     using var req = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
 Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

       req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

      using var resp = await _http.SendAsync(req, cancellationToken);
       var content = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
   {
     _logger.LogWarning("[OpenAIBroadcastAnalytics] Non-success status {Status}: {Body}", (int)resp.StatusCode, content);
                throw new InvalidOperationException($"OpenAI analytics request failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
            }

            try
            {
     using var doc = JsonDocument.Parse(content);
   var root = doc.RootElement;
                var choices = root.GetProperty("choices");
 if (choices.GetArrayLength() == 0)
       throw new InvalidOperationException("OpenAI returned no choices");

  var msg = choices[0].GetProperty("message");
                var text = msg.GetProperty("content").GetString();

     if (string.IsNullOrWhiteSpace(text))
    throw new InvalidOperationException("OpenAI returned empty content");

 return text;
}
            catch (Exception ex)
            {
          _logger.LogError(ex, "[OpenAIBroadcastAnalytics] Failed parsing OpenAI response: {Body}", content);
throw;
            }
        }
}
}
