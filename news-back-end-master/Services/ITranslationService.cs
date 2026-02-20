namespace News_Back_end.Services
{
    public interface ITranslationService
    {
        Task<string> TranslateAsync(string text, string targetLanguage);
        Task<string> DetectLanguageAsync(string text);
        Task<string> SummarizeAsync(string text, Models.SQLServer.SourceDescriptionSetting? settings = null);
    }
}
