using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        public async Task<string> SummarizeAsync(string text, SourceDescriptionSetting? settings = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var tone = string.IsNullOrWhiteSpace(settings?.SummaryTone) ? "neutral" : settings!.SummaryTone.Trim();
            var wordCount = settings?.SummaryWordCount ?? 150;
            var format = string.IsNullOrWhiteSpace(settings?.SummaryFormat) ? "paragraph" : settings!.SummaryFormat.Trim().ToLowerInvariant();
            var focus = string.IsNullOrWhiteSpace(settings?.SummaryFocus) ? null : settings!.SummaryFocus.Trim();
            var customKeyPoints = string.IsNullOrWhiteSpace(settings?.CustomKeyPoints) ? null : settings!.CustomKeyPoints!.Trim();
            var targetLangCode = string.IsNullOrWhiteSpace(settings?.SummaryLanguage) ? "en" : settings!.SummaryLanguage.Trim().ToLowerInvariant();
            var targetLangName = targetLangCode.StartsWith("zh") ? "Chinese" : "English";

            // determine target range (short/medium/long)
            int rangeMin, rangeMax;
            if (wordCount <= 100)
            {
                rangeMin = 50; rangeMax = 100; // short
            }
            else if (wordCount <= 200)
            {
                rangeMin = 100; rangeMax = 200; // medium
            }
            else
            {
                rangeMin = 200; rangeMax = 300; // long
            }

            // treat several synonyms as bullet/list format
            var isBullet = format == "bullet" || format == "bullets" || format == "list" || format == "bulleted";
            var system = isBullet
         ? "You are an assistant that must return ONLY a JSON array of short bullet strings. Example: [\"point one\", \"point two\"]. Do not include any extra text."
          : $"You are a professional news article summarizer. Produce a concise, factual summary of the article's actual content in {targetLangName}. Never describe what the article does (e.g., 'the article discusses...'); instead summarize the facts and events it reports.";

            var userSb = new StringBuilder();
            userSb.Append($"Summarize the key facts of the following news article in approximately {wordCount} words (target range: {rangeMin}-{rangeMax} words) using a {tone} tone. ");

            if (isBullet)
   {
        userSb.Append("Return the summary as a bullet list with 3-6 bullets. ");
         }
       else
     {
   userSb.Append("Return the summary as one short paragraph (2-4 sentences). ");
          }

  if (!string.IsNullOrWhiteSpace(focus))
       userSb.Append($"Focus on: {focus}. ");

     if (!string.IsNullOrWhiteSpace(customKeyPoints))
        userSb.Append($"If possible, include these key points: {customKeyPoints}. ");

      userSb.Append($"Write in {targetLangName}. Summarize the actual events, facts, and information in the article. Do not describe the article itself. Do not invent facts. Return only the summary, no preamble, no trailing commentary.");
      userSb.Append("\n\nArticle:\n\n");

   // Feed the article content but strip any translation instruction artifacts first
     var cleanedText = Regex.Replace(text, @"^Translate\s+the\s+following\s+text\s+to\s+\S+\s*:\s*", "", RegexOptions.IgnoreCase).Trim();
   userSb.Append(cleanedText);

            var user = userSb.ToString();
    var resp = await CreateChatCompletionAsync(system, user, 1024);
return resp ?? string.Empty;
     }

        public async Task<string> DetectLanguageAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "en";
            var system = "You are a language detection assistant. Reply with the two-letter ISO language code only (e.g., en, zh, fr).";
            var user = text.Length > 1000 ? text.Substring(0, 1000) : text;

            var resp = await CreateChatCompletionAsync(system, user);
            var code = resp?.Trim();
            if (string.IsNullOrWhiteSpace(code)) return "en";
            code = code.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            if (code.Length > 2) code = code.Substring(0, 2);
            return code.ToLowerInvariant();
        }

        public async Task<string> TranslateAsync(string text, string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            if (string.IsNullOrWhiteSpace(targetLanguage)) return text;

            // Map language codes to full names for clearer prompting
   var targetName = targetLanguage.Trim().ToLowerInvariant() switch
   {
     "en" or "english" => "English",
          "zh" or "chinese" or "zh-cn" or "zh-tw" => "Chinese",
           "fr" or "french" => "French",
             "de" or "german" => "German",
                "ja" or "japanese" => "Japanese",
     "ko" or "korean" => "Korean",
          _ => targetLanguage
    };

          var isList = Regex.IsMatch(text, "(^|\\n)\\s*([-•*]|\\d+\\.)\\s+", RegexOptions.Multiline);
  var system = isList
        ? $"You are a precise translator. Translate all user-provided text into {targetName}. Preserve list formatting exactly (bullets or numbered lists). Do NOT summarize, omit, add content, or include any instructions or preamble. Return ONLY the translated text."
 : $"You are a precise translator. Translate all user-provided text into {targetName}. Do NOT summarize, omit, add content, or include any instructions or preamble. Preserve paragraph breaks. Return ONLY the translated text.";

            var parts = ChunkText(text, 2000).ToArray();
      var results = new List<string>(parts.Length);
        foreach (var part in parts)
      {
           // Send ONLY the text to translate as the user message — no instruction prefix.
  var chunkResp = await CreateChatCompletionAsync(system, part, 2048);
          var cleaned = CleanTranslationResponse(chunkResp, targetLanguage);
    results.Add(cleaned);
  }

          return string.Join("\n\n", results);
     }

    /// <summary>
     /// Strip common instruction-leak patterns from translation responses.
        /// The model sometimes echoes "Translate the following text to X:" at the start.
   /// </summary>
        private static string CleanTranslationResponse(string? response, string targetLanguage)
        {
        if (string.IsNullOrWhiteSpace(response)) return string.Empty;

   var result = response.Trim();

   // Remove patterns like "Translate the following text to en:" or "Translate the following text to English:"
            var pattern = @"^Translate\s+the\s+following\s+text\s+to\s+\S+\s*:\s*";
  result = Regex.Replace(result, pattern, "", RegexOptions.IgnoreCase).Trim();

      // Also remove if it starts with the target language label like "English:" or "Chinese:"
       var langLabelPattern = @"^(English|Chinese|French|German|Japanese|Korean)\s*:\s*";
     result = Regex.Replace(result, langLabelPattern, "", RegexOptions.IgnoreCase).Trim();

            return result;
        }

        private async Task<string?> CreateChatCompletionAsync(string systemMessage, string userMessage, int maxTokens =1024)
        {
            var payload = new
            {
                model = "gpt-3.5-turbo",
                messages = new[] {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = userMessage }
                },
                temperature =0.0,
                max_tokens = maxTokens
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(3));
            using var resp = await _http.PostAsync("v1/chat/completions", content, cts.Token);
            var respBody = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"OpenAI API error: {(int)resp.StatusCode} {resp.ReasonPhrase}: {respBody}");
                throw new HttpRequestException($"OpenAI API returned {(int)resp.StatusCode}: {respBody}");
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

                int idx =0;
                while (idx < para.Length)
                {
                    var remaining = para.Length - idx;
                    var take = Math.Min(maxChars, remaining);
                    if (take == remaining)
                    {
                        yield return para.Substring(idx).Trim();
                        break;
                    }

                    var substr = para.Substring(idx, take);
                    var lastSpace = substr.LastIndexOf(' ');
                    if (lastSpace <=0)
                    {
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
