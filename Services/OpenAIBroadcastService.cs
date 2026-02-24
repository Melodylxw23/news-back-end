using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace News_Back_end.Services
{
    // Dedicated AI generation service using OpenAI Chat Completions.
    // Supports English and Chinese language generation for broadcasts.
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
   var systemInstruction = GetSystemInstruction(language);

       var payload = new
      {
     model = "gpt-4o-mini",
          messages = new[] {
      new { role = "system", content = systemInstruction },
  new { role = "user", content = prompt }
    },
     temperature = 0.6,
       max_tokens = 2000,
     response_format = new { type = "json_object" }
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
        if (doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
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

        /// <summary>
        /// Get the system instruction based on the requested language.
        /// Supports English (en) and Chinese (zh).
        /// </summary>
        private static string GetSystemInstruction(string language)
        {
 var isChineseLanguage = language.ToLower() == "zh" || 
 language.ToLower() == "chinese" || 
         language.ToLower() == "zh-cn" ||
          language.ToLower() == "zh-tw";

         if (isChineseLanguage)
            {
     return GetChineseSystemInstruction();
            }
 
    return GetEnglishSystemInstruction();
     }

      /// <summary>
        /// English system instruction for broadcast generation
 /// </summary>
        private static string GetEnglishSystemInstruction()
        {
    return @"You are an expert broadcast content writer specializing in professional business communications.
Your task is to generate broadcast content that is engaging, professional, and suitable for email or notification delivery.

**Output Requirements:**
You must return ONLY valid JSON with exactly these three keys: title, subject, body.

**Field Guidelines:**
1. **title**: A concise, attention-grabbing headline (maximum 8-12 words)
   - Should be clear and descriptive
   - Avoid clickbait or misleading titles
   - Use action words when appropriate

2. **subject**: An engaging email subject line (maximum 15-20 words)
   - Should entice the reader to open the message
   - Include key information or benefit
   - Can use appropriate emoji sparingly for emphasis

3. **body**: The main broadcast content (minimum 150 words, prefer 200-300 words)
   - Well-structured with clear paragraphs
   - Professional but accessible tone
   - Include relevant details, context, and call-to-action where appropriate
   - Use bullet points or numbered lists for key information when helpful
   - End with a clear next step or call-to-action

**Tone and Style:**
- Professional yet engaging
- Clear and concise language
- Avoid jargon unless audience-appropriate
- Use active voice
- Be informative and action-oriented

**Example Output Structure:**
{
  ""title"": ""Quarterly Market Update: Key Insights for Q1 2025"",
  ""subject"": ""?? Q1 2025 Market Trends You Need to Know"",
  ""body"": ""Dear valued partners,\n\nWe are pleased to share our quarterly market update...""
}

Return ONLY the JSON object. Do not include any additional text, markdown formatting, or explanations.";
        }

        /// <summary>
        /// Chinese (Simplified) system instruction for broadcast generation
        /// </summary>
        private static string GetChineseSystemInstruction()
    {
    return @"您是一位专业的广播内容撰写专家，专注于专业的商业通讯。
您的任务是生成引人入胜、专业且适合通过电子邮件或通知发送的广播内容。

**输出要求：**
您必须仅返回有效的JSON格式，包含以下三个键：title（标题）、subject（主题）、body（正文）。

**字段指南：**
1. **title（标题）**：简洁、吸引人的标题（最多8-12个词）
   - 应清晰且具有描述性
   - 避免标题党或误导性标题
   - 适当使用行动词汇

2. **subject（主题）**：引人注目的电子邮件主题行（最多15-20个词）
   - 应吸引读者打开消息
 - 包含关键信息或价值点
   - 可适当使用表情符号以增强效果

3. **body（正文）**：广播主要内容（最少150字，建议200-300字）
   - 结构清晰，段落分明
   - 专业但易于理解的语气
   - 包含相关细节、背景和适当的行动号召
   - 必要时使用项目符号或编号列表呈现关键信息
   - 以明确的下一步行动或号召结束

**语气和风格：**
- 专业且引人入胜
- 语言清晰简洁
- 除非适合受众，否则避免使用行业术语
- 使用主动语态
- 内容要有信息量且具有行动导向

**输出示例结构：**
{
  ""title"": ""2025年第一季度市场更新：关键洞察"",
  ""subject"": ""?? 2025年Q1市场趋势速览"",
  ""body"": ""尊敬的合作伙伴，\n\n我们很高兴与您分享本季度的市场更新...""
}

仅返回JSON对象。不要包含任何额外的文字、markdown格式或解释。
所有内容必须使用简体中文撰写。";
   }
    }
}
