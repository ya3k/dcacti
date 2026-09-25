using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddCardPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DATABASE.md §1: PetDefinition.SignatureSkillCardId (FK →
            // CardDefinition) completes the TASK-024 deferral. The
            // defaultValue the scaffolder emits is removed deliberately: it is
            // an artifact of adding a required column to a possibly non-empty
            // table, not a documented default. No Pet Signature Skill default
            // exists (CARD_RULES.md §4 item 1 gives every Pet exactly one real
            // Skill Card, and AGENTS.md §7 forbids inventing content), so the
            // column is added without one.
            migrationBuilder.AddColumn<string>(
                name: "SignatureSkillCardId",
                table: "PetDefinition",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false);

            migrationBuilder.CreateTable(
                name: "CardDefinition",
                columns: table => new
                {
                    CardDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    PowerCost = table.Column<int>(type: "integer", nullable: false),
                    EffectDefinition = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LoadoutCopyLimit = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardDefinition", x => x.CardDefinitionId);
                });

            migrationBuilder.CreateTable(
                name: "PlayerUnlockedCard",
                columns: table => new
                {
                    PlayerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CardDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerUnlockedCard", x => new { x.PlayerId, x.CardDefinitionId });
                    table.ForeignKey(
                        name: "FK_PlayerUnlockedCard_CardDefinition_CardDefinitionId",
                        column: x => x.CardDefinitionId,
                        principalTable: "CardDefinition",
                        principalColumn: "CardDefinitionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerUnlockedCard_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Player",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUnlockedCard_PlayerId",
                table: "PlayerUnlockedCard",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_PetDefinition_CardDefinition_SignatureSkillCardId",
                table: "PetDefinition",
                column: "SignatureSkillCardId",
                principalTable: "CardDefinition",
                principalColumn: "CardDefinitionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PetDefinition_CardDefinition_SignatureSkillCardId",
                table: "PetDefinition");

            migrationBuilder.DropTable(
                name: "PlayerUnlockedCard");

            migrationBuilder.DropTable(
                name: "CardDefinition");

            migrationBuilder.DropColumn(
                name: "SignatureSkillCardId",
                table: "PetDefinition");
        }
    }
}
