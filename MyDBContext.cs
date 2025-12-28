using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using News_Back_end.Models.SQLServer;

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
        }
    }
}
