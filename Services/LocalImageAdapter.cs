using System;
using System.IO;
using System.Threading.Tasks;

namespace News_Back_end.Services
{
    // Adapter that wraps another IImageGenerationService and, when a URL is returned (pointing to
    // an externally hosted or temporary location), downloads the image and saves it to wwwroot/assets/generated/hero_{id}.jpg
    // If the inner service already returns a path under /assets/, the adapter leaves it alone.
    public class LocalImageAdapter : IImageGenerationService
    {
        private readonly IImageGenerationService _inner;
        private readonly string _webRootPath; // wwwroot

        public LocalImageAdapter(IImageGenerationService inner, string webRootPath)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _webRootPath = webRootPath ?? throw new ArgumentNullException(nameof(webRootPath));
        }

        public async Task<string?> GenerateImageAsync(int newsArticleId, string prompt, string? style = null)
        {
            var url = await _inner.GenerateImageAsync(newsArticleId, prompt, style);
            if (string.IsNullOrWhiteSpace(url)) return null;

            // If URL looks like an already-local asset path, return as-is
            if (url.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
                return url;

            try
            {
                using var http = new System.Net.Http.HttpClient();
                using var resp = await http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var bytes = await resp.Content.ReadAsByteArrayAsync();

                var dir = Path.Combine(_webRootPath, "assets", "generated");
                Directory.CreateDirectory(dir);
                var filename = $"hero_{newsArticleId}.jpg";
                var filePath = Path.Combine(dir, filename);
                await File.WriteAllBytesAsync(filePath, bytes);

                // return the relative URL
                return "/assets/generated/" + filename;
            }
            catch
            {
                return null;
            }
        }
    }
}
