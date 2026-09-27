using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Creates the durable battle result table (<c>DATABASE.md</c> §1, §2, §3,
    /// §4).
    ///
    /// <code>
    /// BattleResult
    /// ├── BattleResultId   (PK — the battle's own BattleId; one row per battle)
    /// ├── PlayerId         (FK → Player)
    /// ├── PetInstanceId    (FK → Pet)
    /// ├── BossDefinitionId (FK → BossDefinition)
    /// ├── Outcome          ("victory" | "defeat")
    /// ├── DurationTurns
    /// ├── CompletedAt
    /// └── RewardSummary    (jsonb)
    ///
    /// IX_BattleResult_PlayerId_CompletedAt (PlayerId, CompletedAt DESC)
    /// </code>
    ///
    /// <b>Schema only — no row is written by this migration.</b> A
    /// <c>BattleResult</c> is a battle's own terminal record and is inserted by
    /// the battle-end path, never provisioned: <c>DATABASE.md</c> §5 item 4's
    /// provisioning contract covers static-content tables, and §1 sourcing item 1
    /// makes the row's key the battle's own <c>BattleId</c>, which cannot exist
    /// before the battle does.
    ///
    /// <b>The primary key is the duplicate protection</b> (<c>DATABASE.md</c> §1
    /// sourcing item 1, <c>REDIS_STATE.md</c> §3): because
    /// <c>BattleResultId</c> <i>is</i> the battle's <c>BattleId</c>, at most one
    /// row can ever exist per battle, so no idempotency column or unique
    /// constraint beyond the key is needed.
    ///
    /// <b>The three foreign keys are <c>DATABASE.md</c> §2's relationships</b>
    /// (<c>Player 1 ── N BattleResult</c>, <c>BattleResult N ── 1 Pet</c>,
    /// <c>BattleResult N ── 1 BossDefinition</c>), each <c>Restrict</c> because
    /// §2 documents the relationship shape and no cascade rule: deleting a Player
    /// must not silently erase their battle history.
    ///
    /// <b>One index, and it is §4's:</b>
    /// <c>BattleResult(PlayerId, CompletedAt DESC)</c> — "battle history, most
    /// recent first". §4 states no further index is specified, so none is
    /// declared (anti-overengineering).
    /// </summary>
    public partial class AddBattleResultPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BattleResult",
                columns: table => new
                {
                    BattleResultId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PetInstanceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BossDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DurationTurns = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RewardSummary = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleResult", x => x.BattleResultId);
                    table.ForeignKey(
                        name: "FK_BattleResult_BossDefinition_BossDefinitionId",
                        column: x => x.BossDefinitionId,
                        principalTable: "BossDefinition",
                        principalColumn: "BossDefinitionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BattleResult_Pet_PetInstanceId",
                        column: x => x.PetInstanceId,
                        principalTable: "Pet",
                        principalColumn: "PetInstanceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BattleResult_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Player",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BattleResult_PlayerId_CompletedAt",
                table: "BattleResult",
                columns: new[] { "PlayerId", "CompletedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattleResult");
        }
    }
}
