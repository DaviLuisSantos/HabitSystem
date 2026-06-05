using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitSystem.Migrations
{
    /// <inheritdoc />
    public partial class SeedOwnerAsPro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Promote all existing accounts to Pro.
            // Safe to run: only affects rows already in the DB at this point in time.
            // New users registered after this migration will still default to Free.
            migrationBuilder.Sql("UPDATE Users SET Plan = 'Pro', IsEmailVerified = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Users SET Plan = 'Free', IsEmailVerified = 0;");
        }
    }
}
