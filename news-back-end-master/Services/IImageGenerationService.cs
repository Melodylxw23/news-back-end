using System.Threading.Tasks;

namespace News_Back_end.Services
{
 public interface IImageGenerationService
 {
 /// <summary>
 /// Generate an image for the article based on prompt and style. Returns a URL (relative or absolute) where the generated image can be accessed.
 /// </summary>
 Task<string?> GenerateImageAsync(int newsArticleId, string prompt, string? style = null);
 }
}
