using HabitSystem.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitSystem.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for User entity
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<Domain.User>
{
    public void Configure(EntityTypeBuilder<Domain.User> builder)
    {
        // Primary key
        builder.HasKey(u => u.Id);

        // Properties
        builder.Property(u => u.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.Timezone)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("America/Sao_Paulo");

        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("datetime('now')");

        // Indexes
        builder.HasIndex(u => u.Email)
            .IsUnique();

        // Relationships
        builder.HasMany(u => u.Habits)
            .WithOne(h => h.User)
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.CheckIns)
            .WithOne(c => c.User)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.DailyScores)
            .WithOne(d => d.User)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed default user for MVP
        builder.HasData(new Domain.User
        {
            Id = Constants.DefaultUserId,
            Name = "Dev User",
            Email = "dev@habitsystem.local",
            Timezone = "America/Sao_Paulo",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
