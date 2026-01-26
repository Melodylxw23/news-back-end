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
        // ArticleLabel removed - not used

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
