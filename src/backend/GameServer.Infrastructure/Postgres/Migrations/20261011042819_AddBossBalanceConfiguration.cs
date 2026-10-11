using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Adds the four runtime Boss balance columns to <c>BossDefinition</c>,
    /// populates the five canonical rows with their <c>BOSS_RULES.md</c> §6.1
    /// baselines, and enforces the documented range invariants as database check
    /// constraints (<c>DATABASE.md</c> §1, §3; <c>ADR-024</c> §2.2, §2.4, §2.5).
    ///
    /// <b>Schema plus data, in one step, in this order.</b>
    /// <see cref="Up(MigrationBuilder)"/> runs exactly four phases:
    /// <list type="number">
    /// <item>add the four non-nullable columns with <b>transitional</b>
    /// defaults,</item>
    /// <item>update every canonical row to its exact
    /// <c>BOSS_RULES.md</c> §6.1 value,</item>
    /// <item>add the four check constraints,</item>
    /// <item>drop the four transitional defaults.</item>
    /// </list>
    /// The order is the contract, not a style choice: a non-nullable column
    /// cannot be added to a populated table without a default, the default is
    /// never a balance value any Boss may be fought with, the constraints can
    /// only be activated once every row satisfies them, and the defaults must
    /// be dropped so subsequent inserts cannot inherit them (<c>ADR-024</c> §2.5).
    ///
    /// <b>The transitional defaults are strictly transitional.</b> They exist only
    /// to let <c>AddColumn</c> satisfy <c>NOT NULL</c> on the existing rows for
    /// the duration of this migration; no runtime lookup ever relies on them
    /// (<c>ADR-024</c> §2.5). Once canonical rows are populated and constraints
    /// activated, the defaults are dropped via <c>AlterColumn</c> (<c>DROP DEFAULT</c>)
    /// so any direct SQL or entity insert that omits a balance value fails closed
    /// with a <c>NOT NULL</c> violation rather than silently taking a default.
    /// <c>MaxHP = 1</c> and <c>EnrageThreshold = 0.5</c> are chosen so
    /// that they satisfy the constraints about to be added: the constraint
    /// creation therefore cannot fail on a row outside the canonical five, and no
    /// row is ever left with a value that permits a zero-health or
    /// always-enraged Boss.
    ///
    /// <b>No row is inserted and no row is deleted.</b> The five canonical rows
    /// already exist — <c>20260926151112_ProvisionBossDefinitions</c> and
    /// <c>20261004094100_ProvisionTwoRemainingMvpBosses</c> provisioned them —
    /// so the data phase is an update keyed by the canonical
    /// <c>BossDefinitionId</c> primary keys (<c>DATABASE.md</c> §1 note item 2).
    /// No <c>HasData</c>/model seed data, no startup loader, and no runtime
    /// provisioning infrastructure is introduced (<c>DATABASE.md</c> §1 note item
    /// 5, §5 item 4).
    ///
    /// <b>Row content is transcribed, never computed or invented</b>
    /// (<c>DATABASE.md</c> §1 note item 5 "Row content"). Every value below is
    /// <c>BOSS_RULES.md</c> §6.1's, and each equals the canonical baseline the
    /// Domain definition carries (<c>BossDefinitions</c>), which is what
    /// <c>ADR-024</c> §2.1 item 1 keeps as the seed default and offline test
    /// fixture. <c>EnrageThreshold</c> is stored as the documented fraction
    /// ("1500 (30%)" ⇒ <c>0.30</c>), because <c>BOSS_RULES.md</c> §5 item 4's
    /// comparison is <c>BossHP &lt; MaxHP × EnrageThreshold</c>.
    ///
    /// <b>The columns are exactly four, and the structural content is
    /// untouched.</b> <c>Element</c>, <c>Identity</c>, <c>PassiveDefinition</c>,
    /// and <c>SkillDefinition</c> are unchanged; the Skill's <c>SecondaryEffect</c>
    /// (<c>BOSS_RULES.md</c> §6.3.1 — Burn, Drain Power, Root) stays
    /// non-persisted Domain configuration and is preserved by the Domain Overlay
    /// at battle creation (<c>ADR-024</c> §2.2 item 4), so this migration does not
    /// touch it.
    /// </summary>
    public partial class AddBossBalanceConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Phase 1: the four runtime balance columns (ADR-024 §2.5) ----
            //
            // Non-nullable, with the transitional defaults documented above. The
            // integer columns store the Boss's base combat values; the double
            // precision column stores the documented fraction of MaxHP.

            migrationBuilder.AddColumn<int>(
                name: "ATK",
                table: "BossDefinition",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DEF",
                table: "BossDefinition",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "EnrageThreshold",
                table: "BossDefinition",
                type: "double precision",
                nullable: false,
                defaultValue: 0.5);

            migrationBuilder.AddColumn<int>(
                name: "MaxHP",
                table: "BossDefinition",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // ---- Phase 2: the five canonical rows' BOSS_RULES.md §6.1 values ----
            //
            // One update per (row, column), keyed by the canonical
            // BossDefinitionId. Each value equals the Domain baseline's, so the
            // applied database and the canonical content cannot disagree at the
            // moment this migration lands.

            // Hỏa Long — 5000 / 100 / 50 / 1500 (30%).
            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-hoa-long",
                column: "MaxHP",
                value: 5000);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-hoa-long",
                column: "ATK",
                value: 100);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-hoa-long",
                column: "DEF",
                value: 50);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-hoa-long",
                column: "EnrageThreshold",
                value: 0.30);

            // Thủy Ma — 5000 / 100 / 50 / 1500 (30%).
            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-thuy-ma",
                column: "MaxHP",
                value: 5000);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-thuy-ma",
                column: "ATK",
                value: 100);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-thuy-ma",
                column: "DEF",
                value: 50);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-thuy-ma",
                column: "EnrageThreshold",
                value: 0.30);

            // Mộc Yêu — 5000 / 100 / 50 / 1500 (30%).
            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-moc-yeu",
                column: "MaxHP",
                value: 5000);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-moc-yeu",
                column: "ATK",
                value: 100);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-moc-yeu",
                column: "DEF",
                value: 50);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-moc-yeu",
                column: "EnrageThreshold",
                value: 0.30);

            // Sơn Thạch Vệ — 3000 / 120 / 0 / 1500 (50%).
            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-son-thach-ve",
                column: "MaxHP",
                value: 3000);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-son-thach-ve",
                column: "ATK",
                value: 120);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-son-thach-ve",
                column: "DEF",
                value: 0);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-son-thach-ve",
                column: "EnrageThreshold",
                value: 0.50);

            // Kim Lôi Vương — 2800 / 140 / 0 / 2100 (75%).
            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-kim-loi-vuong",
                column: "MaxHP",
                value: 2800);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-kim-loi-vuong",
                column: "ATK",
                value: 140);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-kim-loi-vuong",
                column: "DEF",
                value: 0);

            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-kim-loi-vuong",
                column: "EnrageThreshold",
                value: 0.75);

            // ---- Phase 3: the four documented range invariants (ADR-024 §2.4) ----
            //
            // Applied only now, after every row satisfies them: the constraints
            // become the database-level guarantee that no corrupt balance value
            // can be written at all. The runtime overlay validates the same four
            // ranges (BossDefinitionLookup), so a row written before a constraint
            // existed still fails closed rather than being applied.

            migrationBuilder.AddCheckConstraint(
                name: "CK_BossDefinition_ATK_NonNegative",
                table: "BossDefinition",
                sql: "\"ATK\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BossDefinition_DEF_NonNegative",
                table: "BossDefinition",
                sql: "\"DEF\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BossDefinition_EnrageThreshold_Range",
                table: "BossDefinition",
                sql: "\"EnrageThreshold\" > 0.0 AND \"EnrageThreshold\" < 1.0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BossDefinition_MaxHP_Positive",
                table: "BossDefinition",
                sql: "\"MaxHP\" > 0");

            // ---- Phase 4: drop transitional defaults (ADR-024 §2.5 fail-closed) ----
            //
            // The defaults (1, 0, 0, 0.5) were strictly transitional to allow AddColumn
            // to satisfy NOT NULL on existing rows. Once canonical rows are populated
            // and constraints activated, the defaults are dropped so any direct SQL
            // insert omitting balance fields fails closed with a NOT NULL violation
            // rather than silently inheriting transitional defaults.

            migrationBuilder.AlterColumn<int>(
                name: "ATK",
                table: "BossDefinition",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "DEF",
                table: "BossDefinition",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<double>(
                name: "EnrageThreshold",
                table: "BossDefinition",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldDefaultValue: 0.5);

            migrationBuilder.AlterColumn<int>(
                name: "MaxHP",
                table: "BossDefinition",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ADR-024 §2.5 "Safe Rollback Behavior": drop the four check
            // constraints, then the four balance columns, restoring the
            // pre-migration five-column structure.
            //
            // <b>No row is deleted and no foreign key is touched.</b>
            // BattleResult.BossDefinitionId references this table, so removing
            // rows would violate that foreign key and lose history. Reverting is
            // therefore a pure schema reversion: the five canonical rows stay
            // exactly as they are, and their structural columns (Identity,
            // Element, PassiveDefinition, SkillDefinition) are not modified.
            //
            // The balance values themselves are not reversed: they are the
            // canonical BOSS_RULES.md §6.1 baselines this migration copied from
            // the Domain, not a prior stored state, so there is nothing to
            // restore them to. The columns carry them, and the columns go.

            migrationBuilder.DropCheckConstraint(
                name: "CK_BossDefinition_ATK_NonNegative",
                table: "BossDefinition");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BossDefinition_DEF_NonNegative",
                table: "BossDefinition");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BossDefinition_EnrageThreshold_Range",
                table: "BossDefinition");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BossDefinition_MaxHP_Positive",
                table: "BossDefinition");

            migrationBuilder.DropColumn(
                name: "ATK",
                table: "BossDefinition");

            migrationBuilder.DropColumn(
                name: "DEF",
                table: "BossDefinition");

            migrationBuilder.DropColumn(
                name: "EnrageThreshold",
                table: "BossDefinition");

            migrationBuilder.DropColumn(
                name: "MaxHP",
                table: "BossDefinition");
        }
    }
}
