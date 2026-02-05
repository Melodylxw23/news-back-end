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

    public class AudienceCountsDTO
    {
        public int AllMembers { get; set; }
        public int ActiveMembers { get; set; }
        public int EmailSubscribers { get; set; }
     public InterestCategoriesDTO InterestCategories { get; set; } = new InterestCategoriesDTO();
        public DemographicsDTO Demographics { get; set; } = new DemographicsDTO();
     public double EmailEngagementRate { get; set; }
    }

    public class InterestCategoriesDTO
    {
        public int TechnologyInterested { get; set; }
        public int BusinessInterested { get; set; }
        public int SportsInterested { get; set; }
        public int EntertainmentInterested { get; set; }
        public int PoliticsInterested { get; set; }
    }

    public class DemographicsDTO
 {
  public List<CountByGroupDTO> ByCountry { get; set; } = new List<CountByGroupDTO>();
        public List<CountByGroupDTO> ByLanguage { get; set; } = new List<CountByGroupDTO>();
    }

    public class CountByGroupDTO
 {
        public string Group { get; set; } = string.Empty;
  public int Count { get; set; }
    }

public class BroadcastStatisticsDTO
  {
     public int BroadcastId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
     public int TotalSent { get; set; }
      public int Delivered { get; set; }
     public int Failed { get; set; }
        public int Opened { get; set; }
  public int UniqueOpens { get; set; } // Unique recipients who opened
        public double OpenRate { get; set; } // Percentage of delivered emails that were opened
   public double DeliveryRate { get; set; } // Percentage of sent emails that were delivered
     public DateTime? SentAt { get; set; }
        public List<DeliveryDetailDTO> DeliveryDetails { get; set; } = new List<DeliveryDetailDTO>();
    }

    public class DeliveryDetailDTO
    {
    public int MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
  public bool DeliverySuccess { get; set; }
        public string? DeliveryError { get; set; }
  public bool EmailOpened { get; set; }
        public DateTime? OpenedAt { get; set; }
     public int OpenCount { get; set; }
   public DateTime SentAt { get; set; }
    }

public class EmailOpenTrackingDTO
    {
  public int BroadcastId { get; set; }
      public int MemberId { get; set; }
  public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
    }
}