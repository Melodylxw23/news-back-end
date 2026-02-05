using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using News_Back_end.Models.SQLServer;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace News_Back_end
{
    public class MyDBContext : IdentityDbContext<ApplicationUser>
    {
        public MyDBContext(DbContextOptions<MyDBContext> options)
            : base(options)
        {
        }

        public DbSet<Member> Members { get; set; } = null!;
        public DbSet<IndustryTag> IndustryTags { get; set; } = null!;
        public DbSet<InterestTag> InterestTags { get; set; } = null!;
        public DbSet<Source> Sources { get; set; } = null!;
        public DbSet<NewsArticle> NewsArticles { get; set; } = null!;
        public DbSet<TranslationAudit> TranslationAudits { get; set; } = null!;
        public DbSet<BroadcastMessage> BroadcastMessages { get; set; } = null!;
        public DbSet<SourceDescriptionSetting> SourceDescriptionSettings { get; set; } = null!;
        public DbSet<FetchMetric> FetchMetrics { get; set; } = null!;
        public DbSet<PublicationDraft> PublicationDrafts { get; set; } = null!;
        public DbSet<BroadcastDelivery> BroadcastDeliveries { get; set; } = null!;

        // Analytics entities
        public DbSet<BroadcastLinkClick> BroadcastLinkClicks { get; set; } = null!;
        public DbSet<BroadcastAnalyticsSummary> BroadcastAnalyticsSummaries { get; set; } = null!;
        public DbSet<TopicPerformanceMetric> TopicPerformanceMetrics { get; set; } = null!;
        public DbSet<MemberEngagementProfile> MemberEngagementProfiles { get; set; } = null!;
        public DbSet<DailyBroadcastMetric> DailyBroadcastMetrics { get; set; } = null!;

        // Consultant insights preferences
        public DbSet<ConsultantPreference> ConsultantPreferences { get; set; } = null!;

        // Consultant insights send log (idempotency)
        public DbSet<ConsultantInsightsSendLog> ConsultantInsightsSendLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Member>()
                .HasMany(m => m.IndustryTags)
                .WithMany(t => t.Members)
                .UsingEntity(j => j.ToTable("MemberIndustryTags"));

            modelBuilder.Entity<Member>()
                .HasMany(m => m.Interests)
                .WithMany(t => t.Members)
                .UsingEntity(j => j.ToTable("MemberInterests"));

            // one-to-one between ApplicationUser and Member
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(a => a.Member)
                .WithOne(m => m.ApplicationUser)
                .HasForeignKey<Member>(m => m.ApplicationUserId)
                .IsRequired(false);

            // Store enum properties as strings for readability in the database
            modelBuilder.Entity<Member>()
                .Property(m => m.Country)
                .HasConversion<string>()
                .HasColumnType("nvarchar(50)");

            modelBuilder.Entity<Member>()
                .Property(m => m.PreferredLanguage)
                .HasConversion<string>()
                .HasColumnType("nvarchar(50)");

            modelBuilder.Entity<Member>()
                .Property(m => m.PreferredChannel)
                .HasConversion<string>()
                .HasColumnType("nvarchar(50)");

            modelBuilder.Entity<Member>()
                .Property(m => m.MembershipType)
                .HasConversion<string>()
                .HasColumnType("nvarchar(50)");

            // Ensure NewsArticle enum properties are stored as strings to match existing DB values
            modelBuilder.Entity<Models.SQLServer.NewsArticle>()
                .Property(n => n.Status)
                .HasConversion<string>()
                .HasColumnType("nvarchar(50)");

            modelBuilder.Entity<Models.SQLServer.NewsArticle>()
                .Property(n => n.TranslationStatus)
                .HasConversion<string>()
                .HasColumnType("nvarchar(50)");

            // BroadcastMessage enums as strings
            modelBuilder.Entity<BroadcastMessage>()
                .Property(b => b.Channel)
                .HasConversion<string>()
                .HasColumnType("nvarchar(50)");

            modelBuilder.Entity<BroadcastMessage>()
                .Property(b => b.Status)
                .HasConversion<string>()
                .HasColumnType("nvarchar(50)");

            // PublicationDraft: single IndustryTag FK relation
            modelBuilder.Entity<PublicationDraft>()
                .HasOne(p => p.IndustryTag)
                .WithMany(i => i.PublicationDrafts)
                .HasForeignKey(p => p.IndustryTagId)
                .IsRequired(false);

            // PublicationDraft many-to-many relationship with interest tags
            modelBuilder.Entity<PublicationDraft>()
                .HasMany(p => p.InterestTags)
                .WithMany(i => i.PublicationDrafts)
                .UsingEntity(j => j.ToTable("PublicationDraftInterestTags"));

            // BroadcastMessage many-to-many relationship with PublicationDraft (selected articles)
            modelBuilder.Entity<BroadcastMessage>()
                .HasMany(b => b.SelectedArticles)
                .WithMany(p => p.BroadcastMessages)
                .UsingEntity(j => j.ToTable("BroadcastMessageArticles"));

            // BroadcastDelivery relationships
            modelBuilder.Entity<BroadcastDelivery>()
                .HasOne(bd => bd.BroadcastMessage)
                .WithMany()
                .HasForeignKey(bd => bd.BroadcastMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BroadcastDelivery>()
                .HasOne(bd => bd.Member)
                .WithMany()
                .HasForeignKey(bd => bd.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ensure unique constraint: one delivery record per broadcast-member combination
            modelBuilder.Entity<BroadcastDelivery>()
                .HasIndex(bd => new { bd.BroadcastMessageId, bd.MemberId })
                .IsUnique();

            // BroadcastDelivery enums as strings
            modelBuilder.Entity<BroadcastDelivery>()
                .Property(bd => bd.Status)
                .HasConversion<string>()
                .HasColumnType("nvarchar(50)");

            modelBuilder.Entity<BroadcastDelivery>()
                .Property(bd => bd.BounceType)
                .HasConversion<string>()
                .HasColumnType("nvarchar(50)");

            // BroadcastLinkClick relationships
            modelBuilder.Entity<BroadcastLinkClick>()
                .HasOne(lc => lc.BroadcastDelivery)
                .WithMany(bd => bd.LinkClicks)
                .HasForeignKey(lc => lc.BroadcastDeliveryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BroadcastLinkClick>()
                .HasOne(lc => lc.PublicationDraft)
                .WithMany()
                .HasForeignKey(lc => lc.PublicationDraftId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // BroadcastAnalyticsSummary relationship
            modelBuilder.Entity<BroadcastAnalyticsSummary>()
                .HasOne(bas => bas.BroadcastMessage)
                .WithMany()
                .HasForeignKey(bas => bas.BroadcastMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BroadcastAnalyticsSummary>()
                .HasIndex(bas => bas.BroadcastMessageId)
                .IsUnique();

            // TopicPerformanceMetric relationships
            modelBuilder.Entity<TopicPerformanceMetric>()
                .HasOne(tpm => tpm.InterestTag)
                .WithMany()
                .HasForeignKey(tpm => tpm.InterestTagId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicPerformanceMetric>()
                .HasOne(tpm => tpm.IndustryTag)
                .WithMany()
                .HasForeignKey(tpm => tpm.IndustryTagId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique index for daily topic metrics
            modelBuilder.Entity<TopicPerformanceMetric>()
                .HasIndex(tpm => new { tpm.MetricDate, tpm.InterestTagId, tpm.IndustryTagId });

            // MemberEngagementProfile relationship
            modelBuilder.Entity<MemberEngagementProfile>()
                .HasOne(mep => mep.Member)
                .WithMany()
                .HasForeignKey(mep => mep.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MemberEngagementProfile>()
                .HasIndex(mep => mep.MemberId)
                .IsUnique();

            // DailyBroadcastMetric index
            modelBuilder.Entity<DailyBroadcastMetric>()
                .HasIndex(dbm => dbm.MetricDate)
                .IsUnique();

            // ConsultantPreference relationship + unique per consultant
            modelBuilder.Entity<ConsultantPreference>()
                .HasOne(p => p.ConsultantUser)
                .WithMany()
                .HasForeignKey(p => p.ConsultantUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ConsultantPreference>()
                .HasIndex(p => p.ConsultantUserId)
                .IsUnique();

            modelBuilder.Entity<ConsultantPreference>()
                .Property(p => p.Frequency)
                .HasConversion<string>()
                .HasColumnType("nvarchar(20)");

            // ConsultantInsightsSendLog relationship + uniqueness (ConsultantUserId, Period, PeriodDateUtc)
            modelBuilder.Entity<ConsultantInsightsSendLog>()
                .HasOne(l => l.ConsultantUser)
                .WithMany()
                .HasForeignKey(l => l.ConsultantUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ConsultantInsightsSendLog>()
                .Property(l => l.Period)
                .HasConversion<string>()
                .HasColumnType("nvarchar(20)");

            modelBuilder.Entity<ConsultantInsightsSendLog>()
                .HasIndex(l => new { l.ConsultantUserId, l.Period, l.PeriodDateUtc })
                .IsUnique();
        }


        // Ensure consistent semantics: if a translation was saved by the crawler, mark status InProgress
        private void ApplyCrawlerTranslationRules()
        {
            var entries = ChangeTracker.Entries<NewsArticle>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .Select(e => e.Entity);

            foreach (var article in entries)
            {
                if (!string.IsNullOrWhiteSpace(article.TranslationSavedBy) &&
                    article.TranslationSavedBy.Equals("crawler", StringComparison.OrdinalIgnoreCase))
                {
                    // Do not override a human-approved translation
                    if (article.TranslationStatus != TranslationStatus.Translated)
                    {
                        article.TranslationStatus = TranslationStatus.InProgress;
                    }
                }
            }
        }

        public override int SaveChanges()
        {
            ApplyCrawlerTranslationRules();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyCrawlerTranslationRules();
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
