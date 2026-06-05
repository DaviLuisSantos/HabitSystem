using HabitSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitSystem.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<Domain.User>
{
    public void Configure(EntityTypeBuilder<Domain.User> builder)
    {
        builder.HasKey(u => u.Id);

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

        builder.Property(u => u.Plan)
            .IsRequired()
            .HasDefaultValue(Plan.Free)
            .HasConversion<string>();

        builder.Property(u => u.IsEmailVerified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.EmailVerificationToken)
            .HasMaxLength(256);

        builder.Property(u => u.PasswordResetToken)
            .HasMaxLength(256);

        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("datetime('now')");

        builder.HasIndex(u => u.Email).IsUnique();

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
    }
}
