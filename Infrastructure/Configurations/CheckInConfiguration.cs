using HabitSystem.Domain;
using HabitSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitSystem.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for CheckIn entity
/// </summary>
public class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
{
    public void Configure(EntityTypeBuilder<CheckIn> builder)
    {
        // Primary key
        builder.HasKey(c => c.Id);

        // Properties
        builder.Property(c => c.Date)
            .IsRequired();

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>(); // Store enum as string for SQLite

        builder.Property(c => c.Note)
            .HasMaxLength(280);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("datetime('now')");

        // Indexes
        // CRITICAL: Unique constraint - one check-in per habit per date
        builder.HasIndex(c => new { c.HabitId, c.Date })
            .IsUnique();

        builder.HasIndex(c => new { c.UserId, c.Date });

        builder.HasIndex(c => c.HabitId);

        // Relationships
        builder.HasOne(c => c.Habit)
            .WithMany(h => h.CheckIns)
            .HasForeignKey(c => c.HabitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.User)
            .WithMany(u => u.CheckIns)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
