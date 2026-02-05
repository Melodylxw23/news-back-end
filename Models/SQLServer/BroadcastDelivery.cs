using System.ComponentModel.DataAnnotations;

namespace News_Back_end.Models.SQLServer
{
    public enum DeliveryStatus
    {
        Pending,
     Sent,
        Delivered,
    Bounced,
        Failed
}

    public enum BounceType
    {
   None,
  Hard, // Permanent failure (invalid email, domain doesn't exist)
   Soft  // Temporary failure (mailbox full, server down)
    }

    public class BroadcastDelivery
    {
        public int BroadcastDeliveryId { get; set; }

        // Foreign keys
        public int BroadcastMessageId { get; set; }
   public int MemberId { get; set; }

   // Navigation properties
        public BroadcastMessage BroadcastMessage { get; set; } = null!;
        public Member Member { get; set; } = null!;

     // Delivery tracking
      public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool DeliverySuccess { get; set; } = false;
    public string? DeliveryError { get; set; }

        // Enhanced delivery status
      public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public BounceType BounceType { get; set; } = BounceType.None;
        public string? BounceReason { get; set; }

      // Open tracking
        public bool EmailOpened { get; set; } = false;
        public DateTime? FirstOpenedAt { get; set; }
  public DateTime? LastOpenedAt { get; set; }
      public int OpenCount { get; set; } = 0; // Track multiple opens

 // Click tracking summary
     public bool HasClicked { get; set; } = false;
   public DateTime? FirstClickedAt { get; set; }
        public DateTime? LastClickedAt { get; set; }
  public int ClickCount { get; set; } = 0;

        // Email details
        public string RecipientEmail { get; set; } = string.Empty;

        // Device/client info (captured on first open)
      public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
        [MaxLength(50)]
        public string? DeviceType { get; set; } // Desktop, Mobile, Tablet
        [MaxLength(100)]
        public string? EmailClient { get; set; } // Gmail, Outlook, Apple Mail, etc.
        [MaxLength(50)]
        public string? OperatingSystem { get; set; }

        // Unsubscribe tracking
        public bool Unsubscribed { get; set; } = false;
        public DateTime? UnsubscribedAt { get; set; }

        // Engagement time (seconds spent reading, if trackable)
    public int? EstimatedReadTimeSeconds { get; set; }

        // Navigation to click details
        public ICollection<BroadcastLinkClick> LinkClicks { get; set; } = new List<BroadcastLinkClick>();
    }
}