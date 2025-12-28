namespace News_Back_end.Services
{
    public interface ITranslationService
    {
        Task<string> TranslateAsync(string text, string targetLanguage);
        Task<string> DetectLanguageAsync(string text);
    }
}
