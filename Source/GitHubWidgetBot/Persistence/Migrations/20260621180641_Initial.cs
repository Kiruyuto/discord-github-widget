using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GitHubWidgetBot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "github_widget");

            migrationBuilder.CreateTable(
                name: "refresh_targets",
                schema: "github_widget",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    discord_user_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    git_hub_username = table.Column<string>(type: "character varying(39)", maxLength: 39, nullable: false),
                    last_update_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_attempt_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    failure_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_targets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "setup_sessions",
                schema: "github_widget",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    discord_user_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    git_hub_device_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    git_hub_poll_interval_seconds = table.Column<long>(type: "bigint", nullable: false),
                    git_hub_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_setup_sessions", x => x.id);
                });

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

            migrationBuilder.CreateIndex(
                name: "ix_setup_sessions_discord_user_id",
                schema: "github_widget",
                table: "setup_sessions",
                column: "discord_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_setup_sessions_git_hub_expires_at_utc",
                schema: "github_widget",
                table: "setup_sessions",
                column: "git_hub_expires_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_targets",
                schema: "github_widget");

            migrationBuilder.DropTable(
                name: "setup_sessions",
                schema: "github_widget");
        }
    }
}
