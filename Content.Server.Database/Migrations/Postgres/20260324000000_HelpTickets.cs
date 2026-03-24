using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <summary>
    /// #Misfits Change - Add help_ticket_event table for persistent cross-round admin ticket audit log.
    /// Append-only; one row per lifecycle event (Created, Claimed, Unclaimed, Resolved, Reopened, AutoResolved).
    /// </summary>
    public partial class HelpTickets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "help_ticket_event",
                columns: table => new
                {
                    help_ticket_event_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_name = table.Column<string>(type: "text", nullable: false),
                    ticket_id = table.Column<int>(type: "integer", nullable: false),
                    ticket_type = table.Column<int>(type: "integer", nullable: false),
                    event_type = table.Column<int>(type: "integer", nullable: false),
                    admin_name = table.Column<string>(type: "text", nullable: true),
                    admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_help_ticket_event", x => x.help_ticket_event_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_help_ticket_event_player_id",
                table: "help_ticket_event",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_help_ticket_event_occurred_at",
                table: "help_ticket_event",
                column: "occurred_at");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "help_ticket_event");
        }
    }
}
