using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace News_Back_end.Services
{
    /// <summary>
    /// Uses OpenAI API to generate contextual consultant insights with source references.
    /// Supports English and Chinese generation based on preferences.
    /// Implements freshness by excluding previously sent content.
    /// </summary>
    public sealed class OpenAIConsultantInsightsService : IConsultantInsightsAiService
    {
   private readonly HttpClient _http;
        private readonly ILogger<OpenAIConsultantInsightsService> _logger;

    public OpenAIConsultantInsightsService(
       HttpClient http,
  ILogger<OpenAIConsultantInsightsService> logger)
    {
     _http = http;
      _logger = logger;
        }

        public async Task<ConsultantInsightsAiResponse> GenerateInsightsAsync(
    List<string> territories,
   List<string> industries,
            string frequency,
     string? consultantName,
            CancellationToken cancellationToken)
        {
            return await GenerateInsightsAsync(territories, industries, frequency, consultantName, "en", cancellationToken);
        }

   public async Task<ConsultantInsightsAiResponse> GenerateInsightsAsync(
            List<string> territories,
            List<string> industries,
        string frequency,
    string? consultantName,
     string language,
    CancellationToken cancellationToken)
        {
    var territoryList = territories.Any() ? territories : new List<string> { "China (national)" };
        var industryList = industries.Any() ? industries : new List<string> { "Cross-sector" };
            var displayName = string.IsNullOrWhiteSpace(consultantName) ? "Consultant" : consultantName;
            var freqLabel = frequency == "Weekly" ? "weekly" : "daily";

            var promptLines = language.ToLower() == "zh" || language.ToLower() == "chinese"
    ? BuildChinesePrompt(territoryList, industryList, displayName, freqLabel, null)
           : BuildEnglishPrompt(territoryList, industryList, displayName, freqLabel, null);

var prompt = string.Join("\n", promptLines);

       return await CallOpenAIAsync(prompt, language, cancellationToken);
        }

        public async Task<ConsultantInsightsAiResponse> GenerateInsightsWithExclusionAsync(
            List<string> territories,
     List<string> industries,
   string frequency,
            string? consultantName,
         string language,
         ConsultantInsightsAiResponse? previousInsights,
       CancellationToken cancellationToken)
 {
       var territoryList = territories.Any() ? territories : new List<string> { "China (national)" };
            var industryList = industries.Any() ? industries : new List<string> { "Cross-sector" };
        var displayName = string.IsNullOrWhiteSpace(consultantName) ? "Consultant" : consultantName;
            var freqLabel = frequency == "Weekly" ? "weekly" : "daily";

   var promptLines = language.ToLower() == "zh" || language.ToLower() == "chinese"
     ? BuildChinesePrompt(territoryList, industryList, displayName, freqLabel, previousInsights)
     : BuildEnglishPrompt(territoryList, industryList, displayName, freqLabel, previousInsights);

 var prompt = string.Join("\n", promptLines);

   return await CallOpenAIAsync(prompt, language, cancellationToken, useHigherTemperature: true);
        }

        private async Task<ConsultantInsightsAiResponse> CallOpenAIAsync(
   string prompt,
 string language,
      CancellationToken cancellationToken,
        bool useHigherTemperature = false)
        {
            // Build the JSON schema for structured outputs
            var insightItemSchema = new
            {
     type = "object",
           properties = new
    {
      text = new { type = "string", description = "The insight content" },
          sourceName = new { type = "string", description = "Publication/source name (e.g., Reuters, South China Morning Post)" },
             sourceUrl = new { type = "string", description = "URL to the source article" },
     sourceDate = new { type = "string", description = "Date in YYYY-MM-DD format" }
                },
      required = new[] { "text", "sourceName", "sourceUrl", "sourceDate" },
 additionalProperties = false
            };

       var responseSchema = new
    {
       type = "object",
         properties = new
     {
       executiveSummary = new { type = "string", description = "2-3 sentence overview of the briefing" },
 keyDevelopments = new
    {
      type = "array",
   items = insightItemSchema,
  description = "3-4 key regulatory and policy developments"
       },
    opportunities = new
     {
          type = "array",
items = insightItemSchema,
     description = "3-4 business opportunities"
          },
     watchouts = new
            {
      type = "array",
    items = insightItemSchema,
       description = "3-4 risks and watch-outs"
         },
   recommendedActions = new
    {
   type = "array",
     items = insightItemSchema,
    description = "3-4 specific tactical actions"
     }
    },
         required = new[] { "executiveSummary", "keyDevelopments", "opportunities", "watchouts", "recommendedActions" },
           additionalProperties = false
        };

            var body = new
    {
   model = "gpt-4o-mini",
    temperature = useHigherTemperature ? 0.7 : 0.5,
      response_format = new
   {
         type = "json_schema",
      json_schema = new
   {
          name = "consultant_insights",
            strict = true,
       schema = responseSchema
       }
                },
     messages = new object[]
           {
       new
     {
  role = "system",
content = language.ToLower() == "zh" || language.ToLower() == "chinese"
           ? "You are an expert business analyst for China market operations. Generate professional insights in Simplified Chinese with source references. Each insight MUST include sourceName, sourceUrl, and sourceDate."
             : "You are an expert business analyst for China market operations. Generate professional insights with source references. Each insight MUST include sourceName, sourceUrl, and sourceDate."
         },
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
        _logger.LogWarning(
        "[OpenAIConsultantInsights] Non-success status {Status}: {Body}",
         (int)resp.StatusCode,
        content);
    throw new InvalidOperationException(
        $"OpenAI consultant insights request failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
   }

            try
       {
            using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
      var choices = root.GetProperty("choices");

     if (choices.GetArrayLength() == 0)
             throw new InvalidOperationException("OpenAI returned no choices");

      var msg = choices[0].GetProperty("message");
          var responseText = msg.GetProperty("content").GetString();

        if (string.IsNullOrWhiteSpace(responseText))
   throw new InvalidOperationException("OpenAI returned empty content");

       _logger.LogDebug("[OpenAIConsultantInsights] Raw response: {Response}", responseText);

     var aiResponse = JsonDocument.Parse(responseText);
         var aiRoot = aiResponse.RootElement;

           return new ConsultantInsightsAiResponse
            {
         KeyDevelopmentsWithSources = ParseInsightItemArray(aiRoot, "keyDevelopments"),
         OpportunitiesWithSources = ParseInsightItemArray(aiRoot, "opportunities"),
       WatchoutsWithSources = ParseInsightItemArray(aiRoot, "watchouts"),
         RecommendedActionsWithSources = ParseInsightItemArray(aiRoot, "recommendedActions"),
      ExecutiveSummary = aiRoot.TryGetProperty("executiveSummary", out var summary)
     ? summary.GetString() ?? ""
   : ""
           };
            }
  catch (Exception ex)
            {
      _logger.LogError(
 ex,
    "[OpenAIConsultantInsights] Failed parsing OpenAI response: {Body}",
        content);
          throw;
            }
        }

        private static List<string> BuildEnglishPrompt(
     List<string> territories,
  List<string> industries,
      string displayName,
   string freqLabel,
          ConsultantInsightsAiResponse? previousInsights = null)
  {
  var exclusionNote = previousInsights != null && (previousInsights.KeyDevelopments.Any() || previousInsights.Opportunities.Any())
      ? "\n**IMPORTANT - Avoid Repetition:**\nDo NOT include any of these previously sent items:\n" +
        $"- Previous Key Developments: {string.Join("; ", previousInsights.KeyDevelopments.Take(3))}\n" +
  $"- Previous Opportunities: {string.Join("; ", previousInsights.Opportunities.Take(3))}\n" +
  $"- Previous Watchouts: {string.Join("; ", previousInsights.Watchouts.Take(3))}\n" +
     "Generate completely fresh perspectives and insights for today.\n"
   : "";

       var lines = new List<string>
    {
"Generate actionable market insights for a business consultant focused on China.",
      "",
     "**Consultant Profile:**",
    $"- Name: {displayName}",
    $"- Focus Territories: {string.Join(", ", territories)}",
      $"- Focus Industries: {string.Join(", ", industries)}",
 $"- Report Frequency: {freqLabel}",
    exclusionNote,
      "**CRITICAL REQUIREMENTS:**",
 "1. Each insight in keyDevelopments, opportunities, watchouts, and recommendedActions MUST be an OBJECT with these EXACT properties:",
  "   - text: The insight content (detailed and actionable)",
      "   - sourceName: Use a REAL publication name from this list:",
      "  * Reuters, South China Morning Post, China Daily, Xinhua News, Caixin Global, ",
      "     * Global Times, CGTN, Nikkei Asia, Financial Times, Bloomberg, The Economist,",
      "     * Ministry of Commerce PRC, National Development and Reform Commission,",
      "     * State Council of China, Provincial Government Announcements",
   "   - sourceUrl: Use the HOMEPAGE URL of the publication (not fake article URLs):",
      "     * https://www.reuters.com/",
      "     * https://www.scmp.com/",
      "     * https://www.chinadaily.com.cn/",
 "     * https://english.news.cn/",
      "  * https://www.caixinglobal.com/",
"     * https://www.globaltimes.cn/",
      "     * https://www.ft.com/",
      "     * https://www.bloomberg.com/",
      "     * https://asia.nikkei.com/",
      "     * https://english.mofcom.gov.cn/",
      "     * https://en.ndrc.gov.cn/",
      "     * https://english.www.gov.cn/",
      "   - sourceDate: Recent date in YYYY-MM-DD format (within last 7 days)",
  "",
        "2. ONLY use homepage URLs from the list above - do NOT generate fake article URLs.",
   "",
 "3. Generate 3-4 items for each category.",
          "",
      "4. Make insights specific to the territories and industries mentioned.",
     "",
     "**Example of CORRECT format:**",
     @"{""keyDevelopments"": [{""text"": ""Shanghai has released new FDI guidelines for the tech sector..."", ""sourceName"": ""South China Morning Post"", ""sourceUrl"": ""https://www.scmp.com/"", ""sourceDate"": ""2025-03-01""}]}",
   };

    return lines;
        }

     private static List<string> BuildChinesePrompt(
  List<string> territories,
     List<string> industries,
     string displayName,
  string freqLabel,
         ConsultantInsightsAiResponse? previousInsights = null)
    {
      var exclusionNote = previousInsights != null && (previousInsights.KeyDevelopments.Any() || previousInsights.Opportunities.Any())
  ? "\n**重要 - 避免重复:**\n请勿包含这些之前发送过的项目:\n" +
      $"- 之前的关键发展: {string.Join("; ", previousInsights.KeyDevelopments.Take(3))}\n" +
         $"- 之前的机遇: {string.Join("; ", previousInsights.Opportunities.Take(3))}\n" +
 $"- 之前的风险提示: {string.Join("; ", previousInsights.Watchouts.Take(3))}\n" +
    "请为今天生成完全新的视角和洞察。\n"
   : "";

            var lines = new List<string>
    {
     "为专注于中国市场的商业顾问生成可行的市场洞察。",
    "",
      "**顾问信息:**",
         $"- 姓名: {displayName}",
  $"- 关注地区: {string.Join(", ", territories)}",
   $"- 关注行业: {string.Join(", ", industries)}",
  $"- 报告频率: {(freqLabel == "weekly" ? "周报" : "日报")}",
      exclusionNote,
 "**关键要求:**",
     "1. keyDevelopments、opportunities、watchouts、recommendedActions 中的每个洞察必须是包含以下属性的对象:",
     "   - text: 洞察内容（详细且可行）",
        "   - sourceName: 使用真实的出版物名称:",
      "     * 路透社, 南华早报, 中国日报, 新华社, 财新,",
      "     * 环球时报, CGTN, 日经亚洲, 金融时报, 彭博社,",
  "     * 商务部, 国家发改委, 国务院, 各省政府公告",
  "   - sourceUrl: 使用出版物的主页URL（不是虚假的文章URL）:",
     "     * https://www.reuters.com/",
        "   * https://www.scmp.com/",
    "     * https://www.chinadaily.com.cn/",
        "     * https://www.xinhuanet.com/",
     "   * https://www.caixin.com/",
        "     * https://www.globaltimes.cn/",
        "     * https://www.mofcom.gov.cn/",
        "     * https://www.ndrc.gov.cn/",
      "     * https://www.gov.cn/",
  "   - sourceDate: 最近的日期，格式为 YYYY-MM-DD（最近7天内）",
   "",
      "2. 只使用上面列表中的主页URL - 不要生成虚假的文章URL。",
 "",
    "3. 每个类别生成3-4个项目。",
       "",
         "4. 使洞察针对提到的地区和行业。",
       "",
 "所有内容必须使用简体中文撰写。"
    };

   return lines;
        }

        /// <summary>
        /// Parses an array of InsightItem objects from JSON.
        /// Falls back to plain string array for backward compatibility.
    /// </summary>
        private static List<InsightItem> ParseInsightItemArray(JsonElement element, string propertyName)
        {
            var result = new List<InsightItem>();

  if (!element.TryGetProperty(propertyName, out var arr))
      return result;

            if (arr.ValueKind != JsonValueKind.Array)
 return result;

            foreach (var item in arr.EnumerateArray())
            {
      if (item.ValueKind == JsonValueKind.Object)
      {
            // New format with source references
          var insightItem = new InsightItem
        {
   Text = item.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "",
         SourceName = item.TryGetProperty("sourceName", out var sn) ? sn.GetString() : null,
            SourceUrl = item.TryGetProperty("sourceUrl", out var su) ? su.GetString() : null,
    SourceDate = item.TryGetProperty("sourceDate", out var sd) ? sd.GetString() : null
    };

           if (!string.IsNullOrWhiteSpace(insightItem.Text))
         result.Add(insightItem);
  }
        else if (item.ValueKind == JsonValueKind.String)
     {
      // Legacy format - plain string (fallback)
           var str = item.GetString();
               if (!string.IsNullOrWhiteSpace(str))
    result.Add(new InsightItem { Text = str });
      }
      }

        return result;
        }
    }
}
