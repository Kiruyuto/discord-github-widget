using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitHubWidgetBot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRefreshTargetBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_targets_discord_user_id_git_hub_username",
                schema: "github_widget",
                table: "refresh_targets");

            migrationBuilder.DropIndex(
                name: "ix_refresh_targets_last_update_utc",
                schema: "github_widget",
                table: "refresh_targets");

            migrationBuilder.AddColumn<bool>(
                name: "exclude_unknown",
                schema: "github_widget",
                table: "refresh_targets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_targets_discord_user_id",
                schema: "github_widget",
                table: "refresh_targets",
                column: "discord_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_targets_last_attempt_utc",
                schema: "github_widget",
                table: "refresh_targets",
                column: "last_attempt_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_targets_discord_user_id",
                schema: "github_widget",
                table: "refresh_targets");

            migrationBuilder.DropIndex(
                name: "ix_refresh_targets_last_attempt_utc",
                schema: "github_widget",
                table: "refresh_targets");

            migrationBuilder.DropColumn(
                name: "exclude_unknown",
                schema: "github_widget",
                table: "refresh_targets");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_targets_discord_user_id_git_hub_username",
                schema: "github_widget",
                table: "refresh_targets",
                columns: new[] { "discord_user_id", "git_hub_username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_targets_last_update_utc",
                schema: "github_widget",
                table: "refresh_targets",
                column: "last_update_utc");
        }
    }
}
