using Microsoft.EntityFrameworkCore;
using News_Back_end.Models.SQLServer;
using System;
using System.Threading.Tasks;

namespace News_Back_end.Services
{
 public class PublicationService : IPublicationService
 {
 private readonly MyDBContext _db;
 public PublicationService(MyDBContext db)
 {
 _db = db;
 }

 public async Task<(bool Success, string? Error)> PublishDraftAsync(PublicationDraft draft, DateTime? scheduledAt = null, string? actor = null)
 {
 if (draft == null) return (false, "draft null");
 if (draft.IndustryTagId == null) return (false, "Industry tag is required.");
 if (draft.InterestTags == null || draft.InterestTags.Count ==0) return (false, "At least one interest tag is required.");

 var article = await _db.NewsArticles.FindAsync(draft.NewsArticleId);
 if (article == null) return (false, "article not found");

 // copy draft fields to article
 if (!string.IsNullOrWhiteSpace(draft.FullContentEN)) article.FullContentEN = draft.FullContentEN;
 if (!string.IsNullOrWhiteSpace(draft.FullContentZH)) article.FullContentZH = draft.FullContentZH;
 // optionally copy hero/seo/slug
 article.PublishedAt = scheduledAt ?? DateTime.Now;
 article.Status = ArticleStatus.Published;

 draft.IsPublished = true;
 draft.PublishedAt = article.PublishedAt;
 draft.PublishedBy = actor;

 try
 {
 await _db.SaveChangesAsync();
 return (true, null);
 }
 catch (Exception ex)
 {
 return (false, ex.Message);
 }
 }

 public async Task<(bool Success, string? Error)> UnpublishDraftAsync(PublicationDraft draft, string? actor = null)
 {
 if (draft == null) return (false, "draft null");
 var article = await _db.NewsArticles.FindAsync(draft.NewsArticleId);
 if (article == null) return (false, "article not found");

 article.Status = ArticleStatus.Draft;
 article.PublishedAt = null;
 draft.IsPublished = false;
 draft.PublishedAt = null;
 draft.PublishedBy = null;
 try
 {
 await _db.SaveChangesAsync();
 return (true, null);
 }
 catch (Exception ex)
 {
 return (false, ex.Message);
 }
 }
 }
}
