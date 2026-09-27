using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class DropPetLevelMultiplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PetDefinition_PetLevelMultiplier_Positive",
                table: "PetDefinition");

            migrationBuilder.DropColumn(
                name: "PetLevelMultiplier",
                table: "PetDefinition");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PetLevelMultiplier",
                table: "PetDefinition",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PetDefinition_PetLevelMultiplier_Positive",
                table: "PetDefinition",
                sql: "\"PetLevelMultiplier\" > 0");
        }
    }
}
