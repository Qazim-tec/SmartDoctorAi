using HealthTech.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace HealthTech.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<DiagnosisHistory> DiagnosisHistories { get; set; }
        public DbSet<QuizCategory> QuizCategories { get; set; }
        public DbSet<QuizScore> QuizScores { get; set; }
        public DbSet<QuizSession> QuizSessions { get; set; }
        public DbSet<LifestyleHistory> LifestyleHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure QuizSession to store Questions as JSONB
            modelBuilder.Entity<QuizSession>()
                .Property(s => s.Questions)
                .HasColumnType("jsonb") // Explicitly set to jsonb for PostgreSQL
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions { PropertyNamingPolicy = null }),
                    v => JsonSerializer.Deserialize<List<QuizQuestion>>(v, new JsonSerializerOptions { PropertyNamingPolicy = null }) ?? new List<QuizQuestion>(),
                    new ValueComparer<List<QuizQuestion>>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()));
        }
    }
}