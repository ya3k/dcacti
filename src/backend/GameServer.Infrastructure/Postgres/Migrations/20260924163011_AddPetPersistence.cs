using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPetPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PetDefinition",
                columns: table => new
                {
                    PetDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Identity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Element = table.Column<int>(type: "integer", nullable: false),
                    PetLevelMultiplier = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    PassiveId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PassiveThreshold = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetDefinition", x => x.PetDefinitionId);
                    table.CheckConstraint("CK_PetDefinition_PetLevelMultiplier_Positive", "\"PetLevelMultiplier\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "Pet",
                columns: table => new
                {
                    PetInstanceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PetDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    Star = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    AcquiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pet", x => x.PetInstanceId);
                    table.CheckConstraint("CK_Pet_Level_Range", "\"Level\" >= 1 AND \"Level\" <= 50");
                    table.CheckConstraint("CK_Pet_Star_Range", "\"Star\" >= 1 AND \"Star\" <= 5");
                    table.ForeignKey(
                        name: "FK_Pet_PetDefinition_PetDefinitionId",
                        column: x => x.PetDefinitionId,
                        principalTable: "PetDefinition",
                        principalColumn: "PetDefinitionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pet_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Player",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pet_PlayerId",
                table: "Pet",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pet");

            migrationBuilder.DropTable(
                name: "PetDefinition");
        }
    }
}
