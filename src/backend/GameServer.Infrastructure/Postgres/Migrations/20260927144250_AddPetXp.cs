using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPetXp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "XP",
                table: "Pet",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pet_XP_Range",
                table: "Pet",
                sql: "\"XP\" >= 0 AND \"XP\" <= 4900");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Pet_XP_Range",
                table: "Pet");

            migrationBuilder.DropColumn(
                name: "XP",
                table: "Pet");
        }
    }
}
