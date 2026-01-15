using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using W = DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDFDocument = QuestPDF.Fluent.Document;
using OpenXmlDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace News_Back_end.Services
{
    /// <summary>
    /// OpenAI-based implementation of Content Creation service
    /// Generates text summaries, visual PDF posters (with QuestPDF), and PowerPoint slides using AI
    /// </summary>
    public class OpenAIContentCreationService : IContentCreationService
 {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly string _assetsFolder = "GeneratedAssets";

        public OpenAIContentCreationService(HttpClient http)
      {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            
          if (!Directory.Exists(_assetsFolder))
            {
     Directory.CreateDirectory(_assetsFolder);
}

            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<string> GenerateTextSummaryAsync(string articleContent, string? customPrompt = null)
        {
       if (string.IsNullOrWhiteSpace(articleContent))
     throw new ArgumentException("Article content cannot be empty", nameof(articleContent));

            var systemMessage = "You are a professional content summarizer. Create concise, informative summaries of news articles in English.";

 var userMessage = string.IsNullOrWhiteSpace(customPrompt)
        ? $"Please summarize the following article in 3-5 sentences:\n\n{articleContent}"
     : $"{customPrompt}\n\nArticle:\n\n{articleContent}";

      return await CreateChatCompletionAsync(systemMessage, userMessage, maxTokens: 500);
        }

     public async Task<string> GeneratePdfPosterAsync(string articleContent, string articleTitle, string? template = null, string? customPrompt = null)
 {
if (string.IsNullOrWhiteSpace(articleContent))
     throw new ArgumentException("Article content cannot be empty", nameof(articleContent));

         var systemMessage = "You are a creative designer. Generate structured content for a professional poster/infographic about a news article. Format your response with clear sections: TITLE, KEY POINTS (as bullet list), MAIN INSIGHT, CALL TO ACTION.";
          
  var userMessage = string.IsNullOrWhiteSpace(customPrompt)
              ? $"Create poster content for this article:\n\nTitle: {articleTitle}\n\nContent:\n{articleContent}"
    : $"{customPrompt}\n\nTitle: {articleTitle}\n\nContent:\n{articleContent}";

      var posterContent = await CreateChatCompletionAsync(systemMessage, userMessage, maxTokens: 1000);

          var fileName = $"poster_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.pdf";
            var filePath = Path.Combine(_assetsFolder, fileName);

      CreateVisualPdfPoster(filePath, articleTitle, posterContent);

         return filePath;
        }

        public async Task<string> GeneratePptSlidesAsync(string articleContent, string articleTitle, int numberOfSlides = 5, string? template = null, string? customPrompt = null)
 {
   if (string.IsNullOrWhiteSpace(articleContent))
   throw new ArgumentException("Article content cannot be empty", nameof(articleContent));

            if (numberOfSlides < 3 || numberOfSlides > 20)
   throw new ArgumentException("Number of slides must be between 3 and 20", nameof(numberOfSlides));

      var systemMessage = $"You are a presentation designer. Create content for a {numberOfSlides}-slide PowerPoint presentation about a news article. Format each slide clearly as: SLIDE X: [Title]\\nCONTENT:\\n- Point 1\\n- Point 2\\n- Point 3";
   
     var userMessage = string.IsNullOrWhiteSpace(customPrompt)
                ? $"Create {numberOfSlides} slides for this article:\n\nTitle: {articleTitle}\n\nContent:\n{articleContent}"
                : $"{customPrompt}\n\nTitle: {articleTitle}\n\nContent:\n{articleContent}";

  var slidesContent = await CreateChatCompletionAsync(systemMessage, userMessage, maxTokens: 1500);

            var fileName = $"slides_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.pptx";
   var filePath = Path.Combine(_assetsFolder, fileName);

  CreatePowerPointPresentation(filePath, articleTitle, slidesContent, numberOfSlides);

     return filePath;
        }

        private async Task<string> CreateChatCompletionAsync(string systemMessage, string userMessage, int maxTokens = 1024)
      {
            var payload = new
     {
                model = "gpt-3.5-turbo",
      messages = new[]
      {
       new { role = "system", content = systemMessage },
    new { role = "user", content = userMessage }
       },
   temperature = 0.7,
    max_tokens = maxTokens
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync("v1/chat/completions", content);
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

       if (doc.ValueKind == JsonValueKind.Object &&
       doc.TryGetProperty("choices", out var choices) &&
             choices.GetArrayLength() > 0)
            {
      var first = choices[0];
          if (first.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var contentEl))
          {
                 return contentEl.GetString() ?? string.Empty;
            }
        }

            throw new InvalidOperationException("Unexpected OpenAI API response format");
            }
         catch (JsonException jex)
          {
                Console.WriteLine($"Failed to parse OpenAI response JSON: {jex.Message}. Body: {respBody}");
            throw;
        }
        }

     private void CreateVisualPdfPoster(string filePath, string title, string content)
        {
            var sections = ParsePosterContent(content);

  QuestPDFDocument.Create(container =>
            {
  container.Page(page =>
      {
   page.Size(PageSizes.A4);
    page.Margin(2, Unit.Centimetre);
  page.PageColor(Colors.White);
     page.DefaultTextStyle(x => x.FontSize(12));

     page.Content()
              .PaddingVertical(1, Unit.Centimetre)
          .Column(x =>
     {
          x.Spacing(20);

    x.Item().BorderBottom(2).BorderColor(Colors.Blue.Medium).PaddingBottom(10)
        .Text(title)
         .SemiBold().FontSize(24).FontColor(Colors.Blue.Darken4);

       if (sections.ContainsKey("KEY POINTS"))
    {
         x.Item().Text("Key Points").SemiBold().FontSize(18).FontColor(Colors.Green.Darken2);
   x.Item().PaddingLeft(20).Column(points =>
        {
 var keyPoints = sections["KEY POINTS"].Split('\n')
      .Where(p => !string.IsNullOrWhiteSpace(p))
              .Select(p => p.Trim().TrimStart('-').Trim());

         foreach (var point in keyPoints)
      {
 points.Item().Row(row =>
          {
    row.ConstantItem(20).Text("•").FontColor(Colors.Green.Medium);
  row.RelativeItem().Text(point);
 });
         }
       });
     }

        if (sections.ContainsKey("MAIN INSIGHT"))
       {
              x.Item().PaddingTop(20).Text("Main Insight").SemiBold().FontSize(18).FontColor(Colors.Orange.Darken2);
  x.Item().PaddingLeft(20).Background(Colors.Orange.Lighten5).Padding(15)
          .Text(sections["MAIN INSIGHT"])
          .FontSize(14).Italic();
  }

       if (sections.ContainsKey("CALL TO ACTION"))
              {
            x.Item().PaddingTop(30).AlignCenter().Column(action =>
      {
       action.Item().Background(Colors.Red.Medium).Padding(20)
          .Text(sections["CALL TO ACTION"])
        .FontSize(16).Bold().FontColor(Colors.White);
                });
    }
           });

  page.Footer()
     .AlignCenter()
     .Text(text =>
        {
        text.Span("Generated by SinoStream Content Creation").FontSize(10);
       text.Span($" • {DateTime.Now:yyyy-MM-dd}").FontSize(10);
            });
     });
        })
            .GeneratePdf(filePath);
    }

        private Dictionary<string, string> ParsePosterContent(string content)
        {
            var sections = new Dictionary<string, string>();
         var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
      string currentSection = "";
         var currentContent = new StringBuilder();

            foreach (var line in lines)
            {
            var upperLine = line.ToUpper().Trim();
      
     if (upperLine.Contains("TITLE") || upperLine.Contains("KEY POINTS") || 
        upperLine.Contains("MAIN INSIGHT") || upperLine.Contains("CALL TO ACTION"))
    {
         if (!string.IsNullOrEmpty(currentSection) && currentContent.Length > 0)
        {
            sections[currentSection] = currentContent.ToString().Trim();
       currentContent.Clear();
    }
         
      if (upperLine.Contains("TITLE")) currentSection = "TITLE";
   else if (upperLine.Contains("KEY POINTS")) currentSection = "KEY POINTS";
      else if (upperLine.Contains("MAIN INSIGHT")) currentSection = "MAIN INSIGHT";
    else if (upperLine.Contains("CALL TO ACTION")) currentSection = "CALL TO ACTION";
     }
     else if (!string.IsNullOrEmpty(currentSection))
            {
    currentContent.AppendLine(line);
}
     }

         if (!string.IsNullOrEmpty(currentSection) && currentContent.Length > 0)
            {
        sections[currentSection] = currentContent.ToString().Trim();
            }

            return sections;
        }

        private void CreatePowerPointPresentation(string filePath, string title, string content, int numberOfSlides)
 {
            using (PresentationDocument presentationDoc = PresentationDocument.Create(filePath, PresentationDocumentType.Presentation))
      {
  PresentationPart presentationPart = presentationDoc.AddPresentationPart();
         presentationPart.Presentation = new P.Presentation();

     CreatePresentationParts(presentationPart);

       var slides = ParseSlidesContent(content, numberOfSlides);
     
     CreateTitleSlide(presentationPart, title, 1);

                for (int i = 0; i < slides.Count && i < numberOfSlides - 1; i++)
       {
          CreateContentSlide(presentationPart, slides[i].Title, slides[i].Content, (uint)(i + 2));
   }

          presentationPart.Presentation.Save();
     }
        }

        private void CreatePresentationParts(PresentationPart presentationPart)
        {
            P.SlideMasterIdList slideMasterIdList = new P.SlideMasterIdList(new P.SlideMasterId() { Id = 2147483648U, RelationshipId = "rId1" });
    P.SlideIdList slideIdList = new P.SlideIdList();
   presentationPart.Presentation.Append(slideMasterIdList, slideIdList);
        }

        private void CreateTitleSlide(PresentationPart presentationPart, string title, uint slideId)
        {
   SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
  slidePart.Slide = new P.Slide(new P.CommonSlideData(new P.ShapeTree()));

            P.SlideId newSlideId = new P.SlideId() { Id = slideId, RelationshipId = presentationPart.GetIdOfPart(slidePart) };
    presentationPart.Presentation.SlideIdList.Append(newSlideId);

            var shapeTree = slidePart.Slide.CommonSlideData.ShapeTree;
   var shape = CreateTextShape(title, 0, 0, 9144000, 1828800);
      shapeTree.AppendChild(shape);
        }

        private void CreateContentSlide(PresentationPart presentationPart, string slideTitle, string slideContent, uint slideId)
   {
   SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
       slidePart.Slide = new P.Slide(new P.CommonSlideData(new P.ShapeTree()));

    P.SlideId newSlideId = new P.SlideId() { Id = slideId, RelationshipId = presentationPart.GetIdOfPart(slidePart) };
     presentationPart.Presentation.SlideIdList.Append(newSlideId);

 var shapeTree = slidePart.Slide.CommonSlideData.ShapeTree;
       
            var titleShape = CreateTextShape(slideTitle, 0, 0, 9144000, 1143000);
  shapeTree.AppendChild(titleShape);

       var contentShape = CreateTextShape(slideContent, 0, 1143000, 9144000, 4572000);
         shapeTree.AppendChild(contentShape);
        }

        private P.Shape CreateTextShape(string text, long x, long y, long width, long height)
        {
          return new P.Shape(
      new P.NonVisualShapeProperties(
   new P.NonVisualDrawingProperties() { Id = 1, Name = "TextBox" },
      new P.NonVisualShapeDrawingProperties(),
            new P.ApplicationNonVisualDrawingProperties()),
      new P.ShapeProperties(
     new A.Transform2D(
 new A.Offset() { X = x, Y = y },
   new A.Extents() { Cx = width, Cy = height })),
           new P.TextBody(
          new A.BodyProperties(),
    new A.ListStyle(),
  new A.Paragraph(new A.Run(new A.Text(text)))));
   }

        private List<(string Title, string Content)> ParseSlidesContent(string content, int maxSlides)
 {
     var slides = new List<(string Title, string Content)>();
      var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
     
    string currentTitle = "Slide";
            var currentContent = new StringBuilder();

         foreach (var line in lines)
     {
    if (line.ToUpper().Contains("SLIDE") && line.Contains(":"))
  {
  if (currentContent.Length > 0)
{
      slides.Add((currentTitle, currentContent.ToString().Trim()));
    currentContent.Clear();
}
              currentTitle = line.Substring(line.IndexOf(':') + 1).Trim();
  }
           else
      {
      currentContent.AppendLine(line.Trim());
    }
  }

            if (currentContent.Length > 0)
          {
      slides.Add((currentTitle, currentContent.ToString().Trim()));
     }

     return slides.Take(maxSlides - 1).ToList();
        }
    }
}