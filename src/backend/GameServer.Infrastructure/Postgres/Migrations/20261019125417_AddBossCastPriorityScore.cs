using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Adds the nullable <c>BossDefinition.CastPriorityScore</c> column — the fifth
    /// <c>ADR-024</c> Domain-Overlay value, approved in place by TASK-308 / OD-3a —
    /// populates Sơn Thạch Vệ's approved initial score, and activates the
    /// documented nullable-range check constraint (<c>DATABASE.md</c> §1, §3;
    /// <c>BOSS_RULES.md</c> §4.3 item 5).
    ///
    /// <b>Schema plus one row update; no row inserted or deleted.</b> The five
    /// canonical <c>BossDefinition</c> rows already exist from
    /// <c>20260926151112_ProvisionBossDefinitions</c> and
    /// <c>20261004094100_ProvisionTwoRemainingMvpBosses</c>. This migration adds the
    /// column, sets the one approved non-null value, and then activates the
    /// constraint — the same "add column, populate rows, activate constraint" order
    /// <c>20261011042819_AddBossBalanceConfiguration</c> established for the first
    /// four overlay values (<c>ADR-024</c> §2.5), except that no transitional
    /// default is needed: a nullable column needs none, and
    /// <c>ADR-024</c> §2.5 forbids one precisely so an omission cannot silently
    /// become a value.
    ///
    /// <b>The column is nullable by contract.</b> <c>DATABASE.md</c> §1/§3:
    /// <c>NULL</c> is the explicit "no cast-priority score authored" marker and is
    /// legal only while the Boss has no Boss Card. The three earlier Bosses and
    /// Kim Lôi Vương therefore keep <c>NULL</c> — they have no provisioned Boss
    /// Card (<c>CARD_RULES.md</c> §1.1 item 3's identity is the ability
    /// <c>SkillId</c>, and no row exists for theirs), and their ability migrates in
    /// <c>ADR-029</c> Slice 4. No default and no guessed score is written for them.
    ///
    /// <b>The one populated value is the approved content.</b> TASK-308 §1
    /// (OD-3b) / <c>BOSS_RULES.md</c> §4.3 item 5 / §4.4 item 5 fix
    /// <c>earthquake.CastPriorityScore = 16</c>. It is transcribed, not computed:
    /// the score is content, not a formula, and the value is not derived from any
    /// other Boss's or from the board weights.
    ///
    /// <b>The constraint is DATABASE.md §1/§3's exact expression.</b>
    /// <c>CastPriorityScore IS NULL OR CastPriorityScore &gt;= 0</c> — the nullable
    /// range form, so the database accepts the documented absence but refuses a
    /// negative score outright. The runtime overlay validates the same invariant
    /// (<c>BossDefinitionLookup</c>), so a row written before the constraint existed
    /// still fails closed rather than being applied.
    ///
    /// <b>Reversibility.</b> <see cref="Down(MigrationBuilder)"/> drops the
    /// constraint and then the column, restoring the pre-migration nine-column
    /// structure. It deletes no row — <c>BattleResult</c> has a foreign key to this
    /// table (<c>DATABASE.md</c> §2) — and it touches no structural column, index,
    /// or key. The row it updated loses only this column, which is what the column
    /// drop does; no other data is modified.
    ///
    /// <b>Determinism and idempotency.</b> One fixed <c>ADD COLUMN</c>, one fixed
    /// <c>UPDATE</c> against the fixed primary key, one fixed constraint. There is
    /// no ordering dependence, no RNG, no clock, and no environment-derived value.
    /// EF records the migration in <c>__EFMigrationsHistory</c>, so re-running
    /// <c>dotnet ef database update</c> applies nothing further.
    /// </summary>
    public partial class AddBossCastPriorityScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DATABASE.md §1/§3: the fifth overlay value, nullable. No default is
            // declared (ADR-024 §2.5): NULL means "not authored", and an insert that
            // omits the value must stay NULL rather than silently take a score.
            migrationBuilder.AddColumn<int>(
                name: "CastPriorityScore",
                table: "BossDefinition",
                type: "integer",
                nullable: true);

            // BOSS_RULES.md §4.3 item 5 / §4.4 item 5 and TASK-308 §1 (OD-3b):
            // earthquake's approved initial cast-priority score is 16. The row is
            // addressed by its canonical persistence key (DATABASE.md §1 note item
            // 2), never by display name or by a pattern.
            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-son-thach-ve",
                column: "CastPriorityScore",
                value: 16);

            // DATABASE.md §3 / ADR-024 §2.4 as amended by TASK-308: the nullable
            // range form, activated after the canonical rows carry satisfying
            // values. The other four rows hold NULL here, which the constraint
            // explicitly permits.
            migrationBuilder.AddCheckConstraint(
                name: "CK_BossDefinition_CastPriorityScore_NonNegative",
                table: "BossDefinition",
                sql: "\"CastPriorityScore\" IS NULL OR \"CastPriorityScore\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ADR-024 §2.5 "Safe Rollback Behavior": drop the constraint and then
            // the column, restoring the four-value overlay. No row is deleted (the
            // BattleResult FK references this table), and no structural column,
            // index, or key is touched.
            migrationBuilder.DropCheckConstraint(
                name: "CK_BossDefinition_CastPriorityScore_NonNegative",
                table: "BossDefinition");

            migrationBuilder.DropColumn(
                name: "CastPriorityScore",
                table: "BossDefinition");
        }
    }
}
