using System.Threading.Tasks;

namespace News_Back_end.Services
{
 public interface IAiBroadcastService
 {
 /// <summary>
 /// Generate content from a free-form prompt. Return string (ideally JSON) with title/subject/body keys.
 /// </summary>
 Task<string> GenerateAsync(string prompt, string language = "en");
 }
}
