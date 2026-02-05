using Microsoft.EntityFrameworkCore;
using News_Back_end.Models.SQLServer;
using News_Back_end.DTOs;
using System.Text;
using System.Text.RegularExpressions;

namespace News_Back_end.Services
{
  public interface IBroadcastSendingService
    {
      Task<BroadcastSendResultDTO> SendBroadcastAsync(int broadcastId);
Task<List<MemberRecipientDTO>> GetEligibleRecipientsAsync(int broadcastId);
   Task<BroadcastStatisticsDTO> GetBroadcastStatisticsAsync(int broadcastId);
        Task RecordEmailOpenAsync(int broadcastId, int memberId);
        Task<List<MemberRecipientDTO>> GetAllPotentialRecipientsAsync();
    }

    public class BroadcastSendingService : IBroadcastSendingService
    {
        private readonly MyDBContext _context;
 private readonly GmailEmailService _emailService;
        private readonly ILogger<BroadcastSendingService> _logger;
        private readonly string _baseUrl;
      private readonly string _frontendArticleUrl;

    public BroadcastSendingService(
            MyDBContext context, 
       GmailEmailService emailService, 
        IConfiguration configuration,
            ILogger<BroadcastSendingService> logger)
      {
   _context = context;
            _emailService = emailService;
       _logger = logger;
    
   // IMPORTANT: BaseUrl must be a publicly accessible URL for email tracking to work
    // In development, use ngrok or similar tunneling service
            _baseUrl = configuration.GetValue<string>("BaseUrl") ?? "https://localhost:7191";
   _frontendArticleUrl = configuration.GetValue<string>("Frontend:ArticleBaseUrl") ?? "http://localhost:3000/articles";
      
            _logger.LogInformation("[BroadcastSending] Configured BaseUrl for tracking: {BaseUrl}", _baseUrl);
   _logger.LogInformation("[BroadcastSending] Configured Frontend Article URL: {ArticleUrl}", _frontendArticleUrl);
            
   // Warn if using localhost - tracking won't work for external recipients
       if (_baseUrl.Contains("localhost"))
            {
 _logger.LogWarning("[BroadcastSending] WARNING: BaseUrl contains 'localhost'. Email tracking will NOT work for external recipients. Configure a public URL or use ngrok for testing.");
 }
        }

     public async Task<List<MemberRecipientDTO>> GetEligibleRecipientsAsync(int broadcastId)
        {
   var broadcast = await _context.BroadcastMessages
                .Include(b => b.SelectedArticles)
 .ThenInclude(a => a.IndustryTag)
                .Include(b => b.SelectedArticles)
          .ThenInclude(a => a.InterestTags)
                .FirstOrDefaultAsync(b => b.Id == broadcastId);

            if (broadcast == null)
    throw new ArgumentException("Broadcast not found", nameof(broadcastId));

      // Get all members with their tags (be more inclusive initially for debugging)
            var membersQuery = _context.Members
                .Include(m => m.IndustryTags)
    .Include(m => m.Interests)
     .Where(m => m.PreferredChannel == Channels.Email || m.PreferredChannel == Channels.Both)
     .Where(m => !string.IsNullOrEmpty(m.Email));

          var allMembers = await membersQuery.ToListAsync();

            // Debug: Log total members found
    Console.WriteLine($"[BroadcastSending] Total members with email preference: {allMembers.Count}");
    Console.WriteLine($"[BroadcastSending] Broadcast has {broadcast.SelectedArticles.Count} selected articles");

            // Simplified logic: if no articles selected, send to ALL email-enabled members
            if (!broadcast.SelectedArticles.Any())
{
                Console.WriteLine($"[BroadcastSending] No articles selected - sending to all {allMembers.Count} email-enabled members");
   return allMembers.Select(m => new MemberRecipientDTO
    {
   MemberId = m.MemberId,
             Email = m.Email,
   ContactPerson = m.ContactPerson,
  CompanyName = m.CompanyName,
          PreferredLanguage = m.PreferredLanguage.ToString(),
     IndustryTags = m.IndustryTags.Select(it => it.NameEN).ToList(),
            InterestTags = m.Interests.Select(it => it.NameEN).ToList()
 }).ToList();
   }

            // Filter members based on article tags
         var eligibleMembers = new List<Member>();

            foreach (var member in allMembers)
      {
      bool isEligible = false;

// Check if member has matching tags with any selected article
       foreach (var article in broadcast.SelectedArticles)
                {
             // Check industry tag match
         if (article.IndustryTag != null && 
     member.IndustryTags.Any(it => it.IndustryTagId == article.IndustryTag.IndustryTagId))
     {
          Console.WriteLine($"[BroadcastSending] Member {member.ContactPerson} matched industry tag {article.IndustryTag.NameEN}");
     isEligible = true;
       break;
     }

           // Check interest tag match
              if (article.InterestTags.Any() && article.InterestTags.Any(at => 
  member.Interests.Any(mi => mi.InterestTagId == at.InterestTagId)))
  {
var matchedTags = article.InterestTags
            .Where(at => member.Interests.Any(mi => mi.InterestTagId == at.InterestTagId))
   .Select(at => at.NameEN);
          Console.WriteLine($"[BroadcastSending] Member {member.ContactPerson} matched interest tags: {string.Join(", ", matchedTags)}");
             isEligible = true;
        break;
          }
          }

         // For debugging: if member has ANY tags but no matches, still make them eligible
    if (!isEligible && (member.IndustryTags.Any() || member.Interests.Any()))
   {
    Console.WriteLine($"[BroadcastSending] Member {member.ContactPerson} made eligible (has tags, permissive mode)");
            isEligible = true;
  }

         if (isEligible)
          {
                    eligibleMembers.Add(member);
           Console.WriteLine($"[BroadcastSending] Added {member.ContactPerson} ({member.Email}) to eligible recipients");
      }
      else
{
           Console.WriteLine($"[BroadcastSending] Member {member.ContactPerson} not eligible - no tag matches found");
                }
     }

   Console.WriteLine($"[BroadcastSending] Final eligible members count: {eligibleMembers.Count}");

            // Convert to DTOs
  return eligibleMembers.Select(m => new MemberRecipientDTO
        {
   MemberId = m.MemberId,
       Email = m.Email,
        ContactPerson = m.ContactPerson,
     CompanyName = m.CompanyName,
         PreferredLanguage = m.PreferredLanguage.ToString(),
     IndustryTags = m.IndustryTags.Select(it => it.NameEN).ToList(),
         InterestTags = m.Interests.Select(it => it.NameEN).ToList()
   }).ToList();
   }

        public async Task<List<MemberRecipientDTO>> GetAllPotentialRecipientsAsync()
        {
     // Get ALL members with email enabled, regardless of tags
       var allMembers = await _context.Members
    .Include(m => m.IndustryTags)
   .Include(m => m.Interests)
          .Where(m => m.PreferredChannel == Channels.Email || m.PreferredChannel == Channels.Both)
    .Where(m => !string.IsNullOrEmpty(m.Email))
      .ToListAsync();

  Console.WriteLine($"[BroadcastSending] Found {allMembers.Count} members with email enabled");

            return allMembers.Select(m => new MemberRecipientDTO
         {
     MemberId = m.MemberId,
   Email = m.Email,
 ContactPerson = m.ContactPerson,
           CompanyName = m.CompanyName,
         PreferredLanguage = m.PreferredLanguage.ToString(),
    IndustryTags = m.IndustryTags.Select(it => it.NameEN).ToList(),
  InterestTags = m.Interests.Select(it => it.NameEN).ToList()
 }).ToList();
        }

        public async Task<BroadcastSendResultDTO> SendBroadcastAsync(int broadcastId)
        {
var broadcast = await _context.BroadcastMessages
          .Include(b => b.SelectedArticles)
        .ThenInclude(a => a.NewsArticle)
           .FirstOrDefaultAsync(b => b.Id == broadcastId);

     if (broadcast == null)
             throw new ArgumentException("Broadcast not found", nameof(broadcastId));

    if (broadcast.Status == BroadcastStatus.Sent)
    throw new InvalidOperationException("Broadcast has already been sent");

            var recipients = await GetEligibleRecipientsAsync(broadcastId);
            var result = new BroadcastSendResultDTO
      {
      BroadcastId = broadcastId,
                TotalRecipients = recipients.Count,
                SuccessfulSends = 0,
       FailedSends = 0,
            Recipients = recipients,
       Errors = new List<string>()
         };

            if (!recipients.Any())
    {
         result.Errors.Add("No eligible recipients found for this broadcast");
        return result;
    }

      // Generate base email content with article tracking
      var emailContent = GenerateEmailContent(broadcast);

// Send emails to all recipients
     foreach (var recipient in recipients)
  {
            var deliveryRecord = new BroadcastDelivery
        {
         BroadcastMessageId = broadcastId,
       MemberId = recipient.MemberId,
  RecipientEmail = recipient.Email,
          SentAt = DateTime.UtcNow,
             Status = DeliveryStatus.Pending
        };

       try
    {
          // Personalize and add tracking to content
            var personalizedContent = PersonalizeContent(emailContent, recipient, broadcastId, broadcast.SelectedArticles.ToList());

      await _emailService.SendEmailAsync(
        recipient.Email,
   broadcast.Subject,
            personalizedContent.HtmlBody
);

         deliveryRecord.DeliverySuccess = true;
         deliveryRecord.Status = DeliveryStatus.Sent;
    result.SuccessfulSends++;
      }
                catch (Exception ex)
                {
      deliveryRecord.DeliverySuccess = false;
    deliveryRecord.Status = DeliveryStatus.Failed;
      deliveryRecord.DeliveryError = ex.Message;

  // Detect bounce type from error message
    if (ex.Message.Contains("invalid") || ex.Message.Contains("does not exist") || ex.Message.Contains("unknown user"))
  {
    deliveryRecord.BounceType = BounceType.Hard;
         }
         else if (ex.Message.Contains("temporary") || ex.Message.Contains("try again") || ex.Message.Contains("mailbox full"))
       {
          deliveryRecord.BounceType = BounceType.Soft;
 }

                result.FailedSends++;
 result.Errors.Add($"Failed to send to {recipient.Email}: {ex.Message}");
     }

       _context.BroadcastDeliveries.Add(deliveryRecord);
    }

       if (result.SuccessfulSends > 0)
            {
    broadcast.Status = BroadcastStatus.Sent;
    broadcast.UpdatedAt = DateTimeOffset.Now;
       }

   await _context.SaveChangesAsync();

            return result;
     }

        private EmailContentDTO GenerateEmailContent(BroadcastMessage broadcast)
 {
            var htmlBuilder = new StringBuilder();

            htmlBuilder.AppendLine("<!DOCTYPE html>");
     htmlBuilder.AppendLine("<html><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head><body>");
 htmlBuilder.AppendLine("<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>");

        htmlBuilder.AppendLine($"<h1 style='color: #333;'>{broadcast.Title}</h1>");
  htmlBuilder.AppendLine($"<div style='margin: 20px 0; line-height: 1.6;'>{broadcast.Body}</div>");

  if (broadcast.SelectedArticles?.Any() == true)
            {
        htmlBuilder.AppendLine("<h2 style='color: #555; margin-top: 30px;'>Featured Articles:</h2>");
           htmlBuilder.AppendLine("<div style='margin: 20px 0;'>");

      int articleIndex = 0;
   foreach (var article in broadcast.SelectedArticles)
     {
         var title = !string.IsNullOrWhiteSpace(article.NewsArticle?.TitleEN)
             ? article.NewsArticle.TitleEN
           : article.NewsArticle?.TitleZH ?? "Untitled";

    htmlBuilder.AppendLine($"<div style='margin-bottom: 15px; padding: 15px; border-left: 3px solid #007bff; background-color: #f8f9fa;' data-article-id='{article.PublicationDraftId}' data-article-index='{articleIndex}'>");
         htmlBuilder.AppendLine($"<h3 style='margin: 0 0 10px 0; color: #333;'><a href='{{{{ARTICLE_LINK_{articleIndex}}}}}' style='color: #007bff; text-decoration: none;'>{title}</a></h3>");

        if (!string.IsNullOrWhiteSpace(article.HeroImageUrl))
     {
       htmlBuilder.AppendLine($"<img src='{article.HeroImageUrl}' alt='Article image' style='max-width: 100%; height: auto; margin: 10px 0;'/>");
       }

      // Add a "Read More" button with tracking
   htmlBuilder.AppendLine($"<a href='{{{{ARTICLE_LINK_{articleIndex}}}}}' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 4px; margin-top: 10px;'>Read More</a>");
     htmlBuilder.AppendLine("</div>");
     articleIndex++;
     }

   htmlBuilder.AppendLine("</div>");
      }

     // Footer with unsubscribe link
        htmlBuilder.AppendLine("<div style='margin-top: 40px; padding-top: 20px; border-top: 1px solid #eee; color: #666; font-size: 12px;'>");
            htmlBuilder.AppendLine("<p>This email was sent from your News Service.</p>");
         htmlBuilder.AppendLine("<p><a href='{{UNSUBSCRIBE_LINK}}' style='color: #666;'>Unsubscribe from these emails</a></p>");
       htmlBuilder.AppendLine("</div>");

            htmlBuilder.AppendLine("</div></body></html>");

      return new EmailContentDTO
    {
 HtmlBody = htmlBuilder.ToString(),
           PlainTextBody = $"{broadcast.Title}\n\n{broadcast.Body}"
    };
        }

        private EmailContentDTO PersonalizeContent(EmailContentDTO baseContent, MemberRecipientDTO recipient, int broadcastId, List<PublicationDraft> articles)
     {
          var personalizedHtml = baseContent.HtmlBody;

         // Add personalized greeting
    personalizedHtml = personalizedHtml.Replace(
      "<h1 style='color: #333;'>",
         $"<p style='margin-bottom: 20px;'>Dear {recipient.ContactPerson},</p><h1 style='color: #333;'>"
            );

        // Replace article link placeholders with tracked URLs
          for (int i = 0; i < articles.Count; i++)
  {
         var article = articles[i];
         
            // Build the original URL - prefer SourceURL, then frontend article page
                string originalUrl;
      if (!string.IsNullOrWhiteSpace(article.NewsArticle?.SourceURL))
 {
  originalUrl = article.NewsArticle.SourceURL;
         _logger.LogDebug("[BroadcastSending] Article {ArticleId} using SourceURL: {Url}", article.PublicationDraftId, originalUrl);
     }
       else
       {
         // Fallback to frontend article view page (must be a full URL for email clients)
          originalUrl = $"{_frontendArticleUrl}/{article.PublicationDraftId}";
         _logger.LogWarning("[BroadcastSending] Article {ArticleId} has no SourceURL, using frontend URL: {Url}", article.PublicationDraftId, originalUrl);
   }
       
     // Create tracked URL that redirects through our analytics endpoint
   var trackedUrl = $"{_baseUrl}/api/analytics/track/click/{broadcastId}/{recipient.MemberId}?url={Uri.EscapeDataString(originalUrl)}&linkId=article-{i}&articleId={article.PublicationDraftId}";
             
  _logger.LogDebug("[BroadcastSending] Article {Index} tracked URL: {TrackedUrl}", i, trackedUrl);
        
personalizedHtml = personalizedHtml.Replace($"{{{{ARTICLE_LINK_{i}}}}}", trackedUrl);
 }

// Add unsubscribe link
     var unsubscribeUrl = $"{_baseUrl}/api/members/{recipient.MemberId}/unsubscribe?broadcastId={broadcastId}";
            personalizedHtml = personalizedHtml.Replace("{{UNSUBSCRIBE_LINK}}", unsubscribeUrl);

       // Add tracking pixel before closing body tag
        // Note: Many email clients block or proxy tracking pixels. Consider this metric as a minimum, not exact count.
         var trackingPixelUrl = $"{_baseUrl}/api/analytics/track/open/{broadcastId}/{recipient.MemberId}";
            var trackingPixel = $"<img src=\"{trackingPixelUrl}\" width=\"1\" height=\"1\" style=\"display:none;border:0;\" alt=\"\" />";
            personalizedHtml = personalizedHtml.Replace("</body>", trackingPixel + "</body>");
            
            _logger.LogDebug("[BroadcastSending] Added tracking pixel for member {MemberId}: {PixelUrl}", recipient.MemberId, trackingPixelUrl);

         return new EmailContentDTO
         {
     HtmlBody = personalizedHtml,
           PlainTextBody = $"Dear {recipient.ContactPerson},\n\n{baseContent.PlainTextBody}"
            };
        }

        public async Task<BroadcastStatisticsDTO> GetBroadcastStatisticsAsync(int broadcastId)
{
            var broadcast = await _context.BroadcastMessages.FindAsync(broadcastId);
if (broadcast == null)
                throw new ArgumentException("Broadcast not found", nameof(broadcastId));

      var deliveries = await _context.BroadcastDeliveries
      .Include(d => d.Member)
                .Where(d => d.BroadcastMessageId == broadcastId)
            .ToListAsync();

            var totalSent = deliveries.Count;
            var delivered = deliveries.Count(d => d.DeliverySuccess);
            var failed = deliveries.Count(d => !d.DeliverySuccess);
     var opened = deliveries.Sum(d => d.OpenCount);
     var uniqueOpens = deliveries.Count(d => d.EmailOpened);

    var openRate = delivered > 0 ? Math.Round((double)uniqueOpens / delivered * 100, 2) : 0;
            var deliveryRate = totalSent > 0 ? Math.Round((double)delivered / totalSent * 100, 2) : 0;

       return new BroadcastStatisticsDTO
      {
   BroadcastId = broadcastId,
    Title = broadcast.Title,
        Status = broadcast.Status.ToString(),
     TotalSent = totalSent,
  Delivered = delivered,
       Failed = failed,
          Opened = opened,
  UniqueOpens = uniqueOpens,
         OpenRate = openRate,
         DeliveryRate = deliveryRate,
 SentAt = deliveries.OrderBy(d => d.SentAt).FirstOrDefault()?.SentAt,
                DeliveryDetails = deliveries.Select(d => new DeliveryDetailDTO
       {
          MemberId = d.MemberId,
          MemberName = d.Member.ContactPerson,
        Email = d.RecipientEmail,
   DeliverySuccess = d.DeliverySuccess,
     DeliveryError = d.DeliveryError,
    EmailOpened = d.EmailOpened,
      OpenedAt = d.FirstOpenedAt,
   OpenCount = d.OpenCount,
    SentAt = d.SentAt
           }).ToList()
       };
        }

     public async Task RecordEmailOpenAsync(int broadcastId, int memberId)
        {
            var delivery = await _context.BroadcastDeliveries
     .FirstOrDefaultAsync(d => d.BroadcastMessageId == broadcastId && d.MemberId == memberId);

         if (delivery != null)
        {
    var now = DateTime.UtcNow;

       if (!delivery.EmailOpened)
           {
     delivery.EmailOpened = true;
    delivery.FirstOpenedAt = now;
                }

   delivery.LastOpenedAt = now;
  delivery.OpenCount++;

          await _context.SaveChangesAsync();
  }
        }
 }
}