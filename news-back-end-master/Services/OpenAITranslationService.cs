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

        public async Task<string> SummarizeAsync(string text, Models.SQLServer.SourceDescriptionSetting? settings = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            // Use settings to compose a richer prompt
            var tone = string.IsNullOrWhiteSpace(settings?.SummaryTone) ? "neutral" : settings!.SummaryTone.Trim();
            var wordCount = settings?.SummaryWordCount ?? 150;
            var format = string.IsNullOrWhiteSpace(settings?.SummaryFormat) ? "paragraph" : settings!.SummaryFormat.Trim().ToLowerInvariant();
            var focus = string.IsNullOrWhiteSpace(settings?.SummaryFocus) ? null : settings!.SummaryFocus.Trim();
            var customKeyPoints = string.IsNullOrWhiteSpace(settings?.CustomKeyPoints) ? null : settings!.CustomKeyPoints!.Trim();
            var targetLangCode = string.IsNullOrWhiteSpace(settings?.SummaryLanguage) ? "en" : settings!.SummaryLanguage.Trim().ToLowerInvariant();
            var targetLangName = targetLangCode.StartsWith("zh") ? "Chinese" : "English";

            // More strict summarization prompt to produce consistent, short summaries
            var system = $"You are a professional summarizer. Produce a concise, factual summary in {targetLangName}.";

            var userSb = new StringBuilder();
            userSb.Append($"Write a concise summary of the following article in approximately {wordCount} words using a {tone} tone. ");

            if (format == "bullet" || format == "bullets" || format == "list")
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

            userSb.Append($"Use the target language: {targetLangName}. Do not invent facts. Return only the summary, no preamble, no trailing commentary.");
            userSb.Append("\n\nArticle:\n\n");
            userSb.Append(text);

            var user = userSb.ToString();

            var resp = await CreateChatCompletionAsync(system, user, 1024);
            return resp ?? string.Empty;
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
            // detect if input appears to be a bullet or numbered list and instruct the model to preserve list formatting
            var isList = System.Text.RegularExpressions.Regex.IsMatch(text, "(^|\\n)\\s*([-•*]|\\d+\\.)\\s+", System.Text.RegularExpressions.RegexOptions.Multiline);
            var system = isList
                ? "You are a precise translator. Translate the following text to the target language. Preserve list formatting exactly (bullets or numbered lists), do NOT summarize, omit, or add content. Preserve paragraph breaks and list markers. Return only the translated text with the same structure." 
                : "You are a precise translator. Translate the following text to the target language. Do NOT summarize, omit, or add content. Preserve paragraph breaks. Return only the translated text with the same paragraph structure.";

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
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(3));
            using var resp = await _http.PostAsync("v1/chat/completions", content, cts.Token);
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

    public class ArticleProcessor
    {
        private readonly ITranslationService _translator;

        public ArticleProcessor(ITranslationService translator)
        {
            _translator = translator;
        }

        // Helper: try translating full text then fall back to a shorter snippet if translation returns empty/too short.
        private async Task<string?> TranslateWithRetries(string text, string target)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            try
            {
                var full = await _translator.TranslateAsync(text, target);
                if (!string.IsNullOrWhiteSpace(full) && full.Length > 10) return full;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TranslateWithRetries full attempt failed: {ex.Message}");
            }

            // Try translating a shorter snippet (first 2000 chars)
            try
            {
                var snippet = text.Length > 2000 ? text.Substring(0, 2000) : text;
                var part = await _translator.TranslateAsync(snippet, target);
                if (!string.IsNullOrWhiteSpace(part) && part.Length > 10)
                {
                    return part + (snippet.Length < text.Length ? "\n..." : "");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TranslateWithRetries snippet attempt failed: {ex.Message}");
            }

            return null;
        }

        public async Task<ArticleDtos?> ProcessArticle(CrawlerDTO raw, SourceDescriptionSetting? settings)
        {
            // allow null settings by falling back to defaults so Fetch endpoints that don't have a setting still work
            settings ??= new SourceDescriptionSetting();
            if (string.IsNullOrWhiteSpace(raw.Content)) return null;

            // Always prefer detecting language from the article text. If the crawler provided a language,
            // use it only when it agrees with the detected language. Also use a lightweight CJK heuristic to
            // catch cases where source metadata is incorrect for Chinese content.
            var origLang = "en";
            var providedLang = (raw.OriginalLanguage ?? string.Empty).Trim();

            // quick heuristic: if content contains a significant amount of CJK characters, treat as zh
            bool looksCJK = false;
            try
            {
                var sample = (raw.Content ?? string.Empty).Length > 500 ? (raw.Content ?? string.Empty).Substring(0, 500) : (raw.Content ?? string.Empty);
                var cjkMatches = Regex.Matches(sample, "[\u4E00-\u9FFF\u3400-\u4DBF\u20000-\u2A6DF]");
                looksCJK = cjkMatches.Count > 10; // heuristic threshold
            }
            catch { }

            string detectedLang = string.Empty;
            try
            {
                detectedLang = await _translator.DetectLanguageAsync(raw.Content ?? string.Empty);
            }
            catch { detectedLang = string.Empty; }

            if (looksCJK)
            {
                origLang = "zh";
            }
            else if (!string.IsNullOrWhiteSpace(providedLang))
            {
                var p = providedLang.Substring(0, Math.Min(2, providedLang.Length)).ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(detectedLang))
                {
                    var d = detectedLang.Substring(0, Math.Min(2, detectedLang.Length)).ToLowerInvariant();
                    // if detected disagrees with provided, prefer detected
                    origLang = d != p ? d : p;
                }
                else
                {
                    origLang = p;
                }
            }
            else if (!string.IsNullOrWhiteSpace(detectedLang))
            {
                origLang = detectedLang.Substring(0, Math.Min(2, detectedLang.Length)).ToLowerInvariant();
            }

            // Normalize full content fields consistently when TranslateOnFetch is enabled.
            // When enabled, produce both English and Chinese full content so downstream summaries are stable.
            string fullEn;
            string? fullZh;
            if (settings.TranslateOnFetch)
            {
                if (origLang == "zh")
                {
                    // original Chinese -> translate to English, keep Chinese
                    fullEn = await TranslateWithRetries(raw.Content ?? string.Empty, "en");
                    if (string.IsNullOrWhiteSpace(fullEn))
                    {
                        // attempt shorter-chunk translation as fallback to get at least partial English content
                        try
                        {
                            var snippet = (raw.Content ?? string.Empty).Length > 2000 ? (raw.Content ?? string.Empty).Substring(0, 2000) : raw.Content ?? string.Empty;
                            var fallback = await _translator.TranslateAsync(snippet, "en");
                            if (!string.IsNullOrWhiteSpace(fallback))
                                fullEn = fallback + (snippet.Length < (raw.Content ?? string.Empty).Length ? "\n..." : "");
                            else
                                fullEn = raw.Content ?? string.Empty; // last resort
                        }
                        catch
                        {
                            fullEn = raw.Content ?? string.Empty;
                        }
                    }
                    fullZh = raw.Content;
                }
                else if (origLang == "en")
                {
                    // original English -> keep English, produce Chinese translation
                    fullEn = raw.Content ?? string.Empty;
                    // try full translation first
                    fullZh = await TranslateWithRetries(raw.Content ?? string.Empty, "zh");
                    if (string.IsNullOrWhiteSpace(fullZh))
                    {
                        // attempt a shorter-chunk translation as a fallback to get at least partial Chinese content
                        try
                        {
                            var snippet = (raw.Content ?? string.Empty).Length > 2000 ? (raw.Content ?? string.Empty).Substring(0, 2000) : raw.Content ?? string.Empty;
                            var fallback = await _translator.TranslateAsync(snippet, "zh");
                            if (!string.IsNullOrWhiteSpace(fallback))
                                fullZh = fallback + (snippet.Length < (raw.Content ?? string.Empty).Length ? "\n..." : "");
                            else
                                fullZh = null;
                        }
                        catch
                        {
                            fullZh = null;
                        }
                    }
                }
                else
                {
                    // other languages -> translate to both English and Chinese for consistency
                    fullEn = await TranslateWithRetries(raw.Content ?? string.Empty, "en");
                    fullZh = await TranslateWithRetries(raw.Content ?? string.Empty, "zh");
                    if (string.IsNullOrWhiteSpace(fullEn)) fullEn = raw.Content ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(fullZh)) fullZh = null;
                }
            }
            else
            {
                // preserve existing behavior when TranslateOnFetch disabled
                fullEn = raw.Content;
                fullZh = null;
            }

            // Generate English summary from English content
            // Ensure summarizer receives the correct target language setting for each call
            var settingsEn = new SourceDescriptionSetting
            {
                SummaryLanguage = "EN",
                SummaryWordCount = settings.SummaryWordCount,
                SummaryTone = settings.SummaryTone,
                SummaryFormat = settings.SummaryFormat,
                CustomKeyPoints = settings.CustomKeyPoints,
                SummaryFocus = settings.SummaryFocus
            };
            var summaryEn = await _translator.SummarizeAsync(fullEn, settingsEn);

            // Generate Chinese summary: prefer translating the (concise) English summary so lengths match.
            // If English summary is not available, fall back to summarizing the Chinese content with a smaller word count.
            string? summaryZh = null;
            if (settings.IncludeChineseSummary)
            {
                // Translate a truncated English summary to Chinese to keep length consistent
                if (!string.IsNullOrWhiteSpace(summaryEn))
                {
                    try
                    {
                        var toTranslateEn = summaryEn.Length > 400 ? summaryEn.Substring(0, 400) : summaryEn;
                        summaryZh = await _translator.TranslateAsync(toTranslateEn, "zh");
                    }
                    catch
                    {
                        summaryZh = null;
                    }
                }

                // 2) If translation failed or produced an unexpectedly long result, try summarizing the original Chinese text
                var needFallback = string.IsNullOrWhiteSpace(summaryZh);
                if (!needFallback && !string.IsNullOrWhiteSpace(fullZh))
                {
                    // detect if the "summary" is actually the full article or far too long
                    try
                    {
                        // use character-based thresholds to avoid comparing translations unevenly
                        var threshold = Math.Max((settings.SummaryWordCount > 0 ? settings.SummaryWordCount * 8 : 600), fullZh.Length / 2);
                        if (summaryZh.Length > threshold)
                            needFallback = true;
                        else
                        {
                            var probeLen = Math.Min(200, fullZh.Length);
                            if (probeLen > 0 && summaryZh.Contains(fullZh.Substring(0, probeLen)))
                                needFallback = true;
                        }
                    }
                    catch { needFallback = true; }
                }

                if (needFallback && !string.IsNullOrWhiteSpace(fullZh))
                {
                    var targetWords = settings.SummaryWordCount > 0 ? Math.Max(30, settings.SummaryWordCount / 3) : 50;
                    var settingsZh = new SourceDescriptionSetting
                    {
                        SummaryLanguage = "ZH",
                        SummaryWordCount = targetWords,
                        SummaryTone = settings.SummaryTone,
                        SummaryFormat = settings.SummaryFormat,
                        CustomKeyPoints = settings.CustomKeyPoints,
                        SummaryFocus = settings.SummaryFocus
                    };
                    try
                    {
                        summaryZh = await _translator.SummarizeAsync(fullZh, settingsZh);
                    }
                    catch
                    {
                        summaryZh = null;
                    }

                    // If summarizer still returns a very long output (model returned whole article) or returns the same as full content, as a last resort translate a short slice of summaryEn
                    if (!string.IsNullOrWhiteSpace(summaryEn) && (string.IsNullOrWhiteSpace(summaryZh) || (summaryZh.Length > Math.Max(200, targetWords * 8)) || (!string.IsNullOrWhiteSpace(fullZh) && summaryZh.Contains(fullZh.Substring(0, Math.Min(200, fullZh.Length))))))
                    {
                        try
                        {
                            var shortEn = summaryEn.Length > 200 ? summaryEn.Substring(0, 200) : summaryEn;
                            summaryZh = await _translator.TranslateAsync(shortEn, "zh");
                        }
                        catch
                        {
                            // leave summaryZh as-is or null
                        }
                    }
                }

                // Final safety: ensure summaryZh is not accidentally the full article text
                try
                {
                    if (!string.IsNullOrWhiteSpace(summaryZh) && !string.IsNullOrWhiteSpace(fullZh))
                    {
                        // if summary is longer than an absolute maximum or contains a large prefix of the full article, truncate to a concise length
                        var maxLen = Math.Max(200, (settings.SummaryWordCount > 0 ? settings.SummaryWordCount * 20 : 600));
                        var ratioLimit = Math.Max( Math.Min(fullZh.Length * 60 / 100, maxLen), 200 );
                        if (summaryZh.Length > maxLen || summaryZh.Length > ratioLimit || summaryZh.Contains(fullZh.Substring(0, Math.Min(200, fullZh.Length))))
                        {
                            summaryZh = summaryZh.Substring(0, Math.Min(summaryZh.Length, maxLen));
                            // ensure we cut at sentence boundary if possible
                            var lastPunct = Math.Max(summaryZh.LastIndexOf('?'), Math.Max(summaryZh.LastIndexOf('?'), summaryZh.LastIndexOf('?')));
                            if (lastPunct > 50) summaryZh = summaryZh.Substring(0, lastPunct + 1);
                        }
                    }
                }
                catch { }
            }

            // Determine title fields: prefer keeping original in its language and translating the other side when possible
            string? titleZh = null;
            string? titleEn = null;
            var rawTitle = raw.Title ?? string.Empty;
            try
            {
                var titleCjk = Regex.Matches(rawTitle.Length > 200 ? rawTitle.Substring(0, 200) : rawTitle, "[\u4E00-\u9FFF\u3400-\u4DBF\u20000-\u2A6DF]").Count > 0;
                if (origLang == "zh" || (origLang == "en" && titleCjk) || titleCjk)
                {
                    titleZh = rawTitle;
                    // translate short title to English
                    try { titleEn = await TranslateWithRetries(rawTitle, "en"); } catch { titleEn = null; }
                }
                else
                {
                    titleEn = string.IsNullOrWhiteSpace(rawTitle) ? null : rawTitle;
                    try { titleZh = await TranslateWithRetries(rawTitle, "zh"); } catch { titleZh = null; }
                }
            }
            catch { titleZh = rawTitle; }

            return new ArticleDtos
            {
                TitleZH = titleZh,
                TitleEN = titleEn,
                SourceURL = raw.SourceURL,
                PublishedAt = raw.PublishedDate,
                OriginalLanguage = origLang,
                OriginalContent = raw.Content,
                TranslatedContent = fullEn,
                FullContentEN = fullEn,
                FullContentZH = fullZh ?? string.Empty,
                SummaryEN = summaryEn,
                SummaryZH = summaryZh
            };
        }
    }
}
