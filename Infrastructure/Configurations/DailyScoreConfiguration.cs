using HabitSystem.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitSystem.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for DailyScore entity
/// </summary>
public class DailyScoreConfiguration : IEntityTypeConfiguration<DailyScore>
{
    public void Configure(EntityTypeBuilder<DailyScore> builder)
    {
        // Primary key
        builder.HasKey(d => d.Id);

        // Properties
        builder.Property(d => d.Date)
            .IsRequired();

        builder.Property(d => d.TotalPossible)
            .IsRequired();

        builder.Property(d => d.TotalEarned)
            .IsRequired();

        builder.Property(d => d.Percentage)
            .IsRequired()
            .HasPrecision(5, 2); // 999.99 max

        builder.Property(d => d.CalculatedAt)
            .IsRequired()
            .HasDefaultValueSql("datetime('now')");

        // Indexes
        // CRITICAL: Unique constraint - one score per user per date
        builder.HasIndex(d => new { d.UserId, d.Date })
            .IsUnique();

        builder.HasIndex(d => d.Date);

        // Relationships
        builder.HasOne(d => d.User)
            .WithMany(u => u.DailyScores)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
