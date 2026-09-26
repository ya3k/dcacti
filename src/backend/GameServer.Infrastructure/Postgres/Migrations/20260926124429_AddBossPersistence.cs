using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddBossPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BossDefinition",
                columns: table => new
                {
                    BossDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Identity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Element = table.Column<int>(type: "integer", nullable: false),
                    PassiveDefinition = table.Column<string>(type: "jsonb", nullable: false),
                    SkillDefinition = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BossDefinition", x => x.BossDefinitionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BossDefinition_Identity",
                table: "BossDefinition",
                column: "Identity",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BossDefinition");
        }
    }
}
