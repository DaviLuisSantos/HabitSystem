using HabitSystem.Domain;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Infrastructure;

/// <summary>
/// Entity Framework Core database context for Habit System
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<User> Users => Set<User>();
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<DailyScore> DailyScores => Set<DailyScore>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
