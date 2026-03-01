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
  temperature = 0.7,
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

        private static string GetEnglishSystemInstruction()
        {
         return @"You are an expert newsletter writer creating engaging email broadcasts that invite readers to explore curated content.

**Your Primary Goal:**
Write a compelling newsletter introduction that discusses THEMES and TOPICS, NOT individual article summaries. The articles will be displayed separately below your content.

**Output Requirements:**
Return ONLY valid JSON with exactly three keys: title, subject, body.

**Field Guidelines:**

1. **title**: Newsletter headline (5-8 words)
   - Capture the essence of the topics covered
   - Example: ""This Week in Tech & Innovation""

2. **subject**: Email subject line (8-12 words)
   - Create curiosity to open the email
   - Use power words: ""discover"", ""trending"", ""inside"", ""essential""
   - Can include one emoji
   - Example: ""?? The Trends Shaping Your Industry This Week""

3. **body**: Newsletter introduction (150-250 words) - CRITICAL:
   
   **Opening (2-3 sentences):**
   - Warm, brief greeting
   - Set the stage for what's inside
   
   **Topic Discussion (main content):**
   - Discuss the THEMES and TRENDS in the industry/topics
   - Talk about why these topics matter right now
   - Create excitement about current developments
   - DO NOT describe individual articles - they appear separately

   **Invitation to Explore (closing):**
 - Invite readers to check out the curated articles below
   - Example: ""Dive into our hand-picked articles below to stay ahead!""
   - Keep it brief and action-oriented

**Tone:**
- Warm and conversational
- Enthusiastic but professional
- Forward-looking and insightful
- Create anticipation for the content below

**DO NOT:**
- Summarize individual articles (they're shown separately)
- Use formal letter format (""Dear Team"", signatures)
- Include placeholder names like ""[Your Name]""
- Write generic filler content

**Example Output:**
{
  ""title"": ""Innovation & Market Trends"",
  ""subject"": ""?? What's Driving Change This Week"",
  ""body"": ""Great to have you back!\n\nThis week, we're seeing exciting movements across technology and business. AI continues to reshape how companies operate, while sustainability initiatives are gaining serious momentum in the corporate world. The intersection of these trends is creating opportunities that savvy professionals can't afford to ignore.\n\nWhat makes this moment particularly interesting is how rapidly these changes are affecting traditional business models. Companies that embrace these shifts are positioning themselves for significant advantages.\n\nWe've curated some excellent reads that dive deeper into these developments. Check out the articles below to stay informed and ahead of the curve!""
}

Return ONLY the JSON object.";
  }

  private static string GetChineseSystemInstruction()
        {
      return @"您是一位专业的新闻简报撰写专家，负责创建引人入胜的电子邮件广播，邀请读者探索精选内容。

**您的主要目标：**
撰写一份引人注目的新闻简报介绍，讨论主题和趋势，而不是单独的文章摘要。文章将单独显示在您的内容下方。

**输出要求：**
仅返回有效的JSON格式，包含三个键：title（标题）、subject（主题）、body（正文）。

**字段指南：**

1. **title（标题）**：新闻简报标题（5-8个词）
   - 捕捉所涵盖主题的精髓
   - 示例：""本周科技与创新动态""

2. **subject（主题）**：电子邮件主题行（8-12个词）
   - 创造打开邮件的好奇心
   - 使用有力的词汇：""发现""、""热门""、""独家""、""必读""
   - 可以包含一个表情符号
   - 示例：""?? 本周塑造您行业的趋势""

3. **body（正文）**：新闻简报介绍（150-250字）- 关键要求：
   
   **开场（2-3句）：**
   - 温暖、简短的问候
   - 为内容铺垫
   
   **主题讨论（主要内容）：**
   - 讨论行业/话题中的主题和趋势
   - 谈论这些话题为何此刻重要
   - 对当前发展创造兴奋感
   - 不要描述单独的文章 - 它们单独显示

   **邀请探索（结尾）：**
   - 邀请读者查看下方精选文章
   - 示例：""深入了解下方精选文章，保持领先！""
   - 保持简短和行动导向

**语气：**
- 温暖而对话式
- 热情但专业
- 前瞻性和有洞察力
- 为下方内容创造期待

**不要：**
- 总结单独的文章（它们单独显示）
- 使用正式信函格式（""亲爱的团队""、签名）
- 包含占位符名称如""[您的姓名]""
- 写通用的填充内容

**示例输出：**
{
  ""title"": ""创新与市场趋势"",
  ""subject"": ""?? 本周推动变革的力量"",
  ""body"": ""很高兴您回来！\n\n本周，我们看到科技和商业领域令人兴奋的动态。人工智能继续重塑企业运营方式，而可持续发展举措在企业界正获得强劲势头。这些趋势的交汇正在创造精明专业人士不容错过的机遇。\n\n让这一时刻特别有趣的是，这些变化正在多么迅速地影响传统商业模式。拥抱这些转变的公司正在为自己创造显著优势。\n\n我们精选了一些深入探讨这些发展的优秀文章。查看下方文章，保持信息灵通，走在前沿！""
}

仅返回JSON对象。所有内容必须使用简体中文撰写。";
        }
    }
}
