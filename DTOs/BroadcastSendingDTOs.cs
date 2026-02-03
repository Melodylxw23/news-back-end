using News_Back_end.Models.SQLServer;

namespace News_Back_end.DTOs
{
    public class BroadcastSendResultDTO
    {
        public int BroadcastId { get; set; }
        public int TotalRecipients { get; set; }
        public int SuccessfulSends { get; set; }
        public int FailedSends { get; set; }
        public List<MemberRecipientDTO> Recipients { get; set; } = new List<MemberRecipientDTO>();
  public List<string> Errors { get; set; } = new List<string>();
        public DateTime SentAt { get; set; } = DateTime.Now;
  }

    public class MemberRecipientDTO
 {
        public int MemberId { get; set; }
        public string Email { get; set; } = string.Empty;
     public string ContactPerson { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string PreferredLanguage { get; set; } = string.Empty;
        public List<string> IndustryTags { get; set; } = new List<string>();
        public List<string> InterestTags { get; set; } = new List<string>();
    }

    public class EmailContentDTO
    {
     public string HtmlBody { get; set; } = string.Empty;
 public string PlainTextBody { get; set; } = string.Empty;
    }

    public class PreviewRecipientsRequestDTO
    {
        public int BroadcastId { get; set; }
    }

    public class SendBroadcastRequestDTO
    {
   public int BroadcastId { get; set; }
        public bool ConfirmSend { get; set; } = false;
    }
}