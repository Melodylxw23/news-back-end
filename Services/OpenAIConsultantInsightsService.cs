using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace News_Back_end.Services
{
    /// <summary>
 /// Uses OpenAI API to generate contextual consultant insights.
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
 // Default to English if language not specified
            return await GenerateInsightsAsync(territories, industries, frequency, consultantName, "en", cancellationToken);
     }

        /// <summary>
    /// Generate insights in specified language (en or zh).
        /// </summary>
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

            // Select prompt based on language
    var promptLines = language.ToLower() == "zh" || language.ToLower() == "chinese"
                ? BuildChinesePrompt(territoryList, industryList, displayName, freqLabel, null)
             : BuildEnglishPrompt(territoryList, industryList, displayName, freqLabel, null);

            var prompt = string.Join("\n", promptLines);

     return await CallOpenAIAsync(prompt, language, cancellationToken);
        }

    /// <summary>
      /// Generate insights with exclusion of previous content (for ensuring freshness daily).
        /// </summary>
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

          // Select prompt based on language, passing previous insights to exclude
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
            var body = new
            {
  model = "gpt-4o-mini",
                temperature = useHigherTemperature ? 0.7 : 0.5, // Higher temp for diversity when excluding previous
response_format = new { type = "json_object" },
   messages = new object[]
       {
           new
           {
            role = "system",
     content = language.ToLower() == "zh" || language.ToLower() == "chinese"
          ? "You are an expert business analyst and market research specialist for China market operations. Generate professional, actionable market insights in Simplified Chinese (中文). Ensure content is fresh and NOT repetitive from previous reports."
     : "You are an expert business analyst and market research specialist for China market operations. Generate professional, actionable market insights. Ensure content is fresh and NOT repetitive from previous reports."
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

       // Parse the JSON response from the model
      var aiResponse = JsonDocument.Parse(responseText);
       var aiRoot = aiResponse.RootElement;

        return new ConsultantInsightsAiResponse
   {
        KeyDevelopments = ParseStringArray(aiRoot, "keyDevelopments"),
   Opportunities = ParseStringArray(aiRoot, "opportunities"),
      Watchouts = ParseStringArray(aiRoot, "watchouts"),
         RecommendedActions = ParseStringArray(aiRoot, "recommendedActions"),
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

      return new List<string>
         {
 "You are an expert business analyst and market researcher specializing in China's business landscape.",
   "Your task is to generate actionable, professional market insights for a business consultant.",
     "",
           "**Consultant Profile:**",
      $"- Name: {displayName}",
      $"- Focus Territories/Provinces: {string.Join(", ", territories)}",
         $"- Focus Industries: {string.Join(", ", industries)}",
     $"- Report Frequency: {freqLabel}",
     exclusionNote,
     "**Generate insights in the following structure:**",
                "1. **Key Developments** (3-4 items): Recent policy, regulatory, or market changes",
         "   - Format: 'LOCATION/TERRITORY has updated their POLICY/REGULATION. Business implication: ...'",
         "- Example: 'Suzhou has updated their FnB policies. Business owners would need to ...'",
       "",
    "2. **Opportunities** (3-4 items): Actionable business opportunities and market positioning",
 "   - Include specific territories, industries, and timelines where relevant",
           "",
      "3. **Watch-outs** (3-4 items): Risks, compliance issues, and items to monitor",
      "   - Focus on practical business implications",
  "",
           "4. **Recommended Actions** (3-4 items): Specific, tactical next steps",
    "   - Include metrics, timelines, and owner responsibilities where applicable",
        "",
     "5. **Executive Summary**: A 2-3 sentence overview of the briefing focus",
    "   - Must reference territories and industries",
                "",
       "**Output Requirements:**",
                "- Use professional but accessible language (avoid jargon)",
         "- Be specific: include territory/province names and concrete business implications",
   "- Reference local context (regulations, procurement practices, buyer behavior)",
  "- Make it immediately actionable for a business leader",
         "- Ensure all content is FRESH and different from previous reports",
      "- Do not mention that you are an AI or reference this prompt",
        "",
              "Return ONLY valid JSON (no markdown, no extra text) with this exact schema:",
                "{",
    "  \"keyDevelopments\": [\"string array of 3-4 market/policy developments\"],",
      "  \"opportunities\": [\"string array of 3-4 business opportunities\"],",
   "  \"watchouts\": [\"string array of 3-4 watch-outs and risks\"],",
       "  \"recommendedActions\": [\"string array of 3-4 specific actions\"],",
           "  \"executiveSummary\": \"string - 2-3 sentence overview\"",
     "}"
      };
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

            return new List<string>
        {
             "你是一位专注于中国商业环境的专家商业分析师和市场研究员。",
    "你的任务是为商业顾问生成可行的、专业的市场洞察信息。",
       "",
     "**顾问信息:**",
           $"- 姓名: {displayName}",
          $"- 关注地区/省份: {string.Join(", ", territories)}",
         $"- 关注行业: {string.Join(", ", industries)}",
    $"- 报告频率: {(freqLabel == "weekly" ? "周报" : "日报")}",
         exclusionNote,
           "**按以下结构生成洞察信息:**",
         "1. **关键发展** (3-4 项): 最近的政策、监管或市场变化",
     "   - 格式: '地区/省份已更新其关于政策的规定。商业影响是: ...'",
         "   - 示例: '苏州已更新其食品与饮料政策。商业所有者需要 ...'",
          "",
        "2. **机遇** (3-4 项): 可行的商业机会和市场定位策略",
           "   - 包括具体的地区、行业和时间表",
           "",
     "3. **风险提示** (3-4 项): 需要监控的风险、合规性问题和注意事项",
       "   - 关注实际的商业影响",
      "",
    "4. **推荐行动** (3-4 项): 具体的、战术性的后续步骤",
"   - 包括度量指标、时间表和责任人",
                "",
                "5. **执行总结**: 2-3 句对简报的概述",
 "   - 必须提及地区和行业",
  "",
            "**输出要求:**",
 "- 使用专业但易于理解的语言(避免行话)",
       "- 具体明确: 包含省份名称和具体的商业影响",
            "- 参考当地背景(法规、采购实践、买家行为)",
         "- 使其对业务领导者立即可行",
        "- 确保所有内容是新的，不同于之前的报告",
     "- 不要提及你是人工智能或提及此提示",
       "",
         "仅返回有效的JSON(无markdown,无额外文本),使用以下确切的架构:",
       "{",
        "  \"keyDevelopments\": [\"3-4 个市场/政策发展的字符串数组\"],",
    "  \"opportunities\": [\"3-4 个商业机会的字符串数组\"],",
    "  \"watchouts\": [\"3-4 个风险和提示的字符串数组\"],",
    "  \"recommendedActions\": [\"3-4 个具体行动的字符串数组\"],",
  "  \"executiveSummary\": \"字符串 - 2-3 句概述\"",
     "}"
            };
        }

        private static List<string> ParseStringArray(JsonElement element, string propertyName)
        {
            var result = new List<string>();

            if (!element.TryGetProperty(propertyName, out var arr))
   return result;

            if (arr.ValueKind != JsonValueKind.Array)
            return result;

            foreach (var item in arr.EnumerateArray())
            {
      var str = item.GetString();
      if (!string.IsNullOrWhiteSpace(str))
                result.Add(str);
         }

            return result;
        }
    }
}
