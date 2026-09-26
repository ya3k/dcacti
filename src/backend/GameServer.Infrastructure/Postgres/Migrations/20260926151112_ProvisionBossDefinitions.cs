using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Provisions the three content-defined <c>BossDefinition</c> rows
    /// (<c>DATABASE.md</c> §1 note item 5, §5 item 4).
    ///
    /// <b>Data only — no schema operation.</b> The table, its primary key, and
    /// <c>IX_BossDefinition_Identity</c> were created by
    /// <c>20260926124429_AddBossPersistence</c>, which stays untouched and
    /// schema-only. This migration adds no column, index, or constraint; its
    /// <see cref="Up(MigrationBuilder)"/> contains exactly three
    /// <c>InsertData</c> calls and its <see cref="Down(MigrationBuilder)"/>
    /// the mirror three <c>DeleteData</c> calls.
    ///
    /// <b>Migration-level <c>InsertData</c> is the decided mechanism</b>
    /// (<c>DATABASE.md</c> §1 note item 5 "Mechanism — decided", TASK-052).
    /// <c>HasData</c>/model seed data, a startup loader, runtime provisioning,
    /// a manual-SQL deployment path, and <c>INSERT ... ON CONFLICT</c> are all
    /// forbidden and are not used. Idempotency comes solely from EF's migration
    /// history: the migration runs once per database, so re-running
    /// <c>dotnet ef database update</c> inserts nothing further, and
    /// <c>BossDefinitionId</c> (PK) plus <c>Identity</c> (UNIQUE) are the
    /// database-level guarantee against duplicates (<c>DATABASE.md</c> §3).
    ///
    /// <b>Row set — exactly three, no fourth.</b> The canonical
    /// <c>BossDefinitionId</c> values of <c>DATABASE.md</c> §1 note item 2,
    /// each with its <c>BOSS_RULES.md</c> §6.4 <c>Identity</c>. Only
    /// content-defined Bosses (<c>BOSS_RULES.md</c> §6) may ever be
    /// provisioned: the two remaining MVP-scope Bosses are not content-defined
    /// and are deliberately absent, and no placeholder row is created.
    ///
    /// <b>Row content is transcribed, never computed or invented</b>
    /// (<c>DATABASE.md</c> §1 note item 5 "Row content"). Per column:
    /// <c>BossDefinitionId</c> and <c>Identity</c> per §1 note item 2 and
    /// <c>BOSS_RULES.md</c> §6.4; <c>Element</c> from <c>BOSS_RULES.md</c> §6
    /// encoded as the Domain <c>Element</c> enum's numeric value (the same
    /// <c>HasConversion&lt;int&gt;()</c> mapping
    /// <c>BossDefinitionConfiguration</c> applies — <c>Moc = 0</c>,
    /// <c>Thuy = 2</c>, <c>Hoa = 3</c>); <c>PassiveDefinition</c> with exactly
    /// the members <c>passiveId</c>, <c>threshold</c>, <c>resetBehavior</c>
    /// (note item 3); <c>SkillDefinition</c> with exactly the members
    /// <c>skillId</c>, <c>baseDamage</c>, <c>chargeRequirement</c>,
    /// <c>cooldownTurns</c> (note item 4). The JSON text is produced by
    /// <c>BossDefinitionJson.WritePassive</c>/<c>WriteSkill</c> over the
    /// authoritative Domain <c>BossDefinitions</c> definitions, so the
    /// migration and the persistence converters cannot drift apart.
    ///
    /// <b>The combat stats are not columns and are never written.</b>
    /// <c>MaxHP</c>, <c>ATK</c>, <c>DEF</c>, and <c>EnrageThreshold</c>
    /// (<c>BOSS_RULES.md</c> §6.1) remain Domain-only configuration
    /// (<c>DATABASE.md</c> §1 note item 1). No display name is written either:
    /// the three-way contract <c>BossDefinitionId</c> ≠ <c>Identity</c> ≠
    /// display name holds, and only the first two appear in this migration.
    /// </summary>
    public partial class ProvisionBossDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hỏa Long (BOSS_RULES.md §6, §6.1–§6.4; DATABASE.md §1 note items
            // 2–4). Element = Hỏa (3). Passive "boss-hoa-long-rage", match-
            // charged threshold 5, documented default reset behavior. Skill
            // "flame-burst": base damage 150, charge requirement 5, cooldown 2.
            migrationBuilder.InsertData(
                table: "BossDefinition",
                columns: new[] { "BossDefinitionId", "Identity", "Element", "PassiveDefinition", "SkillDefinition" },
                values: new object[] { "boss-def-hoa-long", "boss-hoa-long", 3, "{\"passiveId\":\"boss-hoa-long-rage\",\"threshold\":5,\"resetBehavior\":\"Default\"}", "{\"skillId\":\"flame-burst\",\"baseDamage\":150,\"chargeRequirement\":5,\"cooldownTurns\":2}" });

            // Thủy Ma (§6, §6.1–§6.4). Element = Thủy (2). Passive
            // "boss-thuy-ma-heal" is "Passive (always active)" (§6.2) — an
            // alternate, non-match trigger — so storage records the documented
            // `threshold: null`, never the `0` sentinel (DATABASE.md §1 note
            // item 3, §3). Skill "drain-power": base damage 120, charge
            // requirement 4, cooldown 3.
            migrationBuilder.InsertData(
                table: "BossDefinition",
                columns: new[] { "BossDefinitionId", "Identity", "Element", "PassiveDefinition", "SkillDefinition" },
                values: new object[] { "boss-def-thuy-ma", "boss-thuy-ma", 2, "{\"passiveId\":\"boss-thuy-ma-heal\",\"threshold\":null,\"resetBehavior\":\"Default\"}", "{\"skillId\":\"drain-power\",\"baseDamage\":120,\"chargeRequirement\":4,\"cooldownTurns\":3}" });

            // Mộc Yêu (§6, §6.1–§6.4). Element = Mộc (0). Passive
            // "boss-moc-yeu-regen", match-charged threshold 5, documented
            // default reset behavior. Skill "root": base damage 100, charge
            // requirement 6, cooldown 2.
            migrationBuilder.InsertData(
                table: "BossDefinition",
                columns: new[] { "BossDefinitionId", "Identity", "Element", "PassiveDefinition", "SkillDefinition" },
                values: new object[] { "boss-def-moc-yeu", "boss-moc-yeu", 0, "{\"passiveId\":\"boss-moc-yeu-regen\",\"threshold\":5,\"resetBehavior\":\"Default\"}", "{\"skillId\":\"root\",\"baseDamage\":100,\"chargeRequirement\":6,\"cooldownTurns\":2}" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The mirror of Up: remove exactly the three provisioned rows, keyed
            // by the same canonical BossDefinitionId values (the PK, DATABASE.md
            // §1 note item 2). No other row is touched.
            migrationBuilder.DeleteData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-hoa-long");

            migrationBuilder.DeleteData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-thuy-ma");

            migrationBuilder.DeleteData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-moc-yeu");
        }
    }
}
