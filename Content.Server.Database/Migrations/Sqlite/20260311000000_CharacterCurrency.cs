using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <summary>
    /// #Misfits Change - Add character_currency table for persistent Bottle Caps per character.
    /// </summary>
    public partial class CharacterCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_currency",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    player_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    character_name = table.Column<string>(type: "TEXT", nullable: false),
                    bottlecaps = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_currency", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_character_currency_player_id_character_name",
                table: "character_currency",
                columns: new[] { "player_id", "character_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "character_currency");
        }
    }
}
