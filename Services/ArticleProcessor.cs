using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace News_Back_end.Services
{
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
     settings ??= new SourceDescriptionSetting();
   if (string.IsNullOrWhiteSpace(raw.Content)) return null;

       // ?? Language detection and CJK heuristic ??
    var origLang = "en";
            var providedLang = (raw.OriginalLanguage ?? string.Empty).Trim();

     bool looksCJK = false;
         try
     {
    var sample = (raw.Content ?? string.Empty).Length > 500 ? (raw.Content ?? string.Empty).Substring(0, 500) : (raw.Content ?? string.Empty);
     var cjkMatches = Regex.Matches(sample, "[\u4E00-\u9FFF\u3400-\u4DBF\u20000-\u2A6DF]");
           looksCJK = cjkMatches.Count > 10;
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

          // ?? Always produce both English and Chinese full content ??
  string fullEn;
            string? fullZh;

     if (origLang == "zh")
  {
   fullEn = await TranslateWithRetries(raw.Content ?? string.Empty, "en") ?? (raw.Content ?? string.Empty);
    fullZh = raw.Content;
}
        else if (origLang == "en")
   {
  fullEn = raw.Content ?? string.Empty;
        fullZh = await TranslateWithRetries(raw.Content ?? string.Empty, "zh");
       }
          else
      {
        // Other language: translate to both EN and ZH
     fullEn = await TranslateWithRetries(raw.Content ?? string.Empty, "en") ?? (raw.Content ?? string.Empty);
     fullZh = await TranslateWithRetries(raw.Content ?? string.Empty, "zh");
     }

      // ?? Generate English title ??
            string? titleZh = raw.Title;
       string? titleEn = null;

        if (!string.IsNullOrWhiteSpace(titleZh))
            {
           if (origLang == "en")
             {
   // Title is already in English
      titleEn = titleZh;
          // Translate to Chinese
try
        {
            titleZh = await _translator.TranslateAsync(titleEn, "zh");
            if (string.IsNullOrWhiteSpace(titleZh)) titleZh = raw.Title;
     }
     catch { titleZh = raw.Title; }
        }
  else if (origLang == "zh")
                {
          // Title is in Chinese, translate to English
try
       {
        titleEn = await _translator.TranslateAsync(titleZh, "en");
  }
       catch { titleEn = null; }
                }
           else
     {
        // Other language: translate title to both EN and ZH
     try { titleEn = await _translator.TranslateAsync(raw.Title!, "en"); } catch { titleEn = null; }
     try
                {
     var zhTitle = await _translator.TranslateAsync(raw.Title!, "zh");
       if (!string.IsNullOrWhiteSpace(zhTitle)) titleZh = zhTitle;
     }
         catch { }
        }
            }

            // ?? Always generate English summary ??
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

  // ?? Always generate Chinese summary ??
      string? summaryZh = null;

        // First try: translate the English summary to Chinese
            if (!string.IsNullOrWhiteSpace(summaryEn))
   {
           try
      {
        var toTranslateEn = summaryEn.Length > 400 ? summaryEn.Substring(0, 400) : summaryEn;
         summaryZh = await _translator.TranslateAsync(toTranslateEn, "zh");
      }
            catch { summaryZh = null; }
    }

       // Fallback: summarize the Chinese full content directly
 if (string.IsNullOrWhiteSpace(summaryZh) && !string.IsNullOrWhiteSpace(fullZh))
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
     try { summaryZh = await _translator.SummarizeAsync(fullZh, settingsZh); } catch { summaryZh = null; }
            }

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
