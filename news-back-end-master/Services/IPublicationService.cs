using System.Threading.Tasks;
using News_Back_end.Models.SQLServer;
using System;

namespace News_Back_end.Services
{
 public interface IPublicationService
 {
 Task<(bool Success, string? Error)> PublishDraftAsync(PublicationDraft draft, DateTime? scheduledAt = null, string? actor = null);
 Task<(bool Success, string? Error)> UnpublishDraftAsync(PublicationDraft draft, string? actor = null);
 }
}
