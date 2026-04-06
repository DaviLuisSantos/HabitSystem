using HabitSystem.Domain;
using HabitSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace HabitSystem.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for Habit entity
/// </summary>
public class HabitConfiguration : IEntityTypeConfiguration<Habit>
{
    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        // Primary key
        builder.HasKey(h => h.Id);

        // Properties
        builder.Property(h => h.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(h => h.Description)
            .HasMaxLength(280);

        builder.Property(h => h.Weight)
            .IsRequired()
            .HasDefaultValue((short)5);

        builder.Property(h => h.PartialWeight)
            .IsRequired()
            .HasDefaultValue((short)2);

        builder.Property(h => h.FrequencyType)
            .IsRequired()
            .HasDefaultValue(FrequencyType.Daily)
            .HasConversion<string>(); // Store enum as string for SQLite

        // Store array as JSON for SQLite
        builder.Property(h => h.FrequencyDays)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<int[]>(v, (JsonSerializerOptions?)null));

        builder.Property(h => h.FrequencyTimes);

        builder.Property(h => h.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(h => h.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("datetime('now')");

        builder.Property(h => h.ArchivedAt);

        // Indexes
        builder.HasIndex(h => h.UserId);

        builder.HasIndex(h => h.IsActive);

        // Relationships
        builder.HasOne(h => h.User)
            .WithMany(u => u.Habits)
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(h => h.CheckIns)
            .WithOne(c => c.Habit)
            .HasForeignKey(c => c.HabitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
