using Microsoft.EntityFrameworkCore;
using News_Back_end.Models.SQLServer;
using News_Back_end.DTOs;
using System.Text;

namespace News_Back_end.Services
{
    public interface IBroadcastSendingService
    {
        Task<BroadcastSendResultDTO> SendBroadcastAsync(int broadcastId);
        Task<List<MemberRecipientDTO>> GetEligibleRecipientsAsync(int broadcastId);
    }

    public class BroadcastSendingService : IBroadcastSendingService
    {
        private readonly MyDBContext _context;
        private readonly GmailEmailService _emailService;

        public BroadcastSendingService(MyDBContext context, GmailEmailService emailService)
     {
            _context = context;
      _emailService = emailService;
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

       // Get all members with their tags
  var membersQuery = _context.Members
        .Include(m => m.IndustryTags)
          .Include(m => m.Interests)
         .Where(m => m.PreferredChannel == Channels.Email || m.PreferredChannel == Channels.Both)
  .Where(m => !string.IsNullOrEmpty(m.Email));

       var allMembers = await membersQuery.ToListAsync();

            // Filter members based on broadcast target audience and article tags
         var eligibleMembers = new List<Member>();

            foreach (var member in allMembers)
       {
        bool isEligible = false;

        // Check if member matches target audience (if specific industry/interests are targeted)
       if (broadcast.SelectedArticles.Any())
      {
      // Member is eligible if they have interest in any of the article topics
        foreach (var article in broadcast.SelectedArticles)
           {
            // Check industry tag match
    if (article.IndustryTag != null && 
     member.IndustryTags.Any(it => it.IndustryTagId == article.IndustryTag.IndustryTagId))
     {
      isEligible = true;
             break;
      }

   // Check interest tag match
       if (article.InterestTags.Any(at => 
    member.Interests.Any(mi => mi.InterestTagId == at.InterestTagId)))
      {
     isEligible = true;
                   break;
            }
        }
         }
             else
              {
     // If no articles selected, send to all members (based on TargetAudience)
              isEligible = true;
            }

     // Apply additional filtering based on TargetAudience enum if needed
      if (isEligible && broadcast.TargetAudience != BroadcastAudience.All)
 {
          // You can implement more specific audience targeting here based on your needs
    // For now, we'll keep it simple and send to eligible members
        }

   if (isEligible)
   {
          eligibleMembers.Add(member);
      }
   }

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

      // Generate email content
   var emailContent = GenerateEmailContent(broadcast);

 // Send emails to all recipients
            foreach (var recipient in recipients)
            {
        try
  {
          // Customize content based on recipient's preferred language if needed
              var personalizedContent = PersonalizeContent(emailContent, recipient);
      
         await _emailService.SendEmailAsync(
                recipient.Email, 
   broadcast.Subject, 
    personalizedContent.HtmlBody
       );
             
              result.SuccessfulSends++;
}
          catch (Exception ex)
  {
  result.FailedSends++;
          result.Errors.Add($"Failed to send to {recipient.Email}: {ex.Message}");
      }
        }

    // Update broadcast status
         if (result.SuccessfulSends > 0)
       {
      broadcast.Status = BroadcastStatus.Sent;
                broadcast.UpdatedAt = DateTimeOffset.Now;
    await _context.SaveChangesAsync();
}

   return result;
        }

        private EmailContentDTO GenerateEmailContent(BroadcastMessage broadcast)
        {
            var htmlBuilder = new StringBuilder();
            
   // Email header
 htmlBuilder.AppendLine("<!DOCTYPE html>");
 htmlBuilder.AppendLine("<html><head><meta charset='UTF-8'></head><body>");
            htmlBuilder.AppendLine("<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>");
     
        // Main content
        htmlBuilder.AppendLine($"<h1 style='color: #333;'>{broadcast.Title}</h1>");
            htmlBuilder.AppendLine($"<div style='margin: 20px 0; line-height: 1.6;'>{broadcast.Body}</div>");

         // Include selected articles if any
            if (broadcast.SelectedArticles?.Any() == true)
      {
    htmlBuilder.AppendLine("<h2 style='color: #555; margin-top: 30px;'>Featured Articles:</h2>");
        htmlBuilder.AppendLine("<div style='margin: 20px 0;'>");
      
        foreach (var article in broadcast.SelectedArticles)
       {
     var title = !string.IsNullOrWhiteSpace(article.NewsArticle?.TitleEN) 
        ? article.NewsArticle.TitleEN 
             : article.NewsArticle?.TitleZH ?? "Untitled";
 
  htmlBuilder.AppendLine("<div style='margin-bottom: 15px; padding: 15px; border-left: 3px solid #007bff; background-color: #f8f9fa;'>");
           htmlBuilder.AppendLine($"<h3 style='margin: 0 0 10px 0; color: #333;'>{title}</h3>");
           
           if (!string.IsNullOrWhiteSpace(article.HeroImageUrl))
   {
     htmlBuilder.AppendLine($"<img src='{article.HeroImageUrl}' alt='Article image' style='max-width: 100%; height: auto; margin: 10px 0;'/>");
   }
          
           htmlBuilder.AppendLine("</div>");
   }
        
         htmlBuilder.AppendLine("</div>");
    }

       // Footer
     htmlBuilder.AppendLine("<div style='margin-top: 40px; padding-top: 20px; border-top: 1px solid #eee; color: #666; font-size: 12px;'>");
    htmlBuilder.AppendLine("<p>This email was sent from your News Service. If you no longer wish to receive these emails, please contact us.</p>");
   htmlBuilder.AppendLine("</div>");
   
            htmlBuilder.AppendLine("</div></body></html>");

            return new EmailContentDTO
         {
         HtmlBody = htmlBuilder.ToString(),
      PlainTextBody = $"{broadcast.Title}\n\n{broadcast.Body}"
            };
      }

        private EmailContentDTO PersonalizeContent(EmailContentDTO baseContent, MemberRecipientDTO recipient)
        {
            // Add personalization like recipient name
      var personalizedHtml = baseContent.HtmlBody.Replace(
        "<h1 style='color: #333;'>", 
       $"<p style='margin-bottom: 20px;'>Dear {recipient.ContactPerson},</p><h1 style='color: #333;'>"
    );

            return new EmailContentDTO
            {
     HtmlBody = personalizedHtml,
                PlainTextBody = $"Dear {recipient.ContactPerson},\n\n{baseContent.PlainTextBody}"
            };
        }
 }
}