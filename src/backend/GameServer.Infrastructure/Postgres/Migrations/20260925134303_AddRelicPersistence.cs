using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRelicPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RelicDefinition",
                columns: table => new
                {
                    RelicDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Condition = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EffectDefinition = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelicDefinition", x => x.RelicDefinitionId);
                });

            migrationBuilder.CreateTable(
                name: "Relic",
                columns: table => new
                {
                    RelicInstanceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RelicDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AcquiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relic", x => x.RelicInstanceId);
                    table.ForeignKey(
                        name: "FK_Relic_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Player",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Relic_RelicDefinition_RelicDefinitionId",
                        column: x => x.RelicDefinitionId,
                        principalTable: "RelicDefinition",
                        principalColumn: "RelicDefinitionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Relic_PlayerId",
                table: "Relic",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Relic");

            migrationBuilder.DropTable(
                name: "RelicDefinition");
        }
    }
}
