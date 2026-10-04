using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Provisions the two remaining content-defined <c>BossDefinition</c> rows:
    /// <c>boss-def-son-thach-ve</c> (Sơn Thạch Vệ) and
    /// <c>boss-def-kim-loi-vuong</c> (Kim Lôi Vương) — <c>DATABASE.md</c> §1 note
    /// item 5, §5 item 4.
    ///
    /// <b>Why these two rows are provisionable now.</b> TASK-053 provisioned the
    /// three Bosses content-defined at that time. <c>BOSS_RULES.md</c> §6.1 then
    /// recorded two further MVP Bosses as "not yet content-defined", and
    /// <c>DATABASE.md</c> §1/§3 item 5 forbids provisioning a Boss that is not
    /// content-defined, so no row could exist for either. TASK-172 has since
    /// applied the TASK-171 Product Owner decisions and authored both Bosses at
    /// their canonical owner (<c>BOSS_RULES.md</c> §6, §6.1–§6.4). Both are
    /// therefore <b>provisioned-later</b>, not content-blocked
    /// (<c>DATABASE.md</c> §5 item 4 rule (a)). This is that separate
    /// provisioning task, and it completes the MVP set at five.
    ///
    /// <b>Data only — no schema operation.</b> The table, its primary key, the
    /// <c>IX_BossDefinition_Identity</c> unique index, and every column
    /// constraint already exist (<c>20260926124429_AddBossPersistence</c>), which
    /// stays untouched and schema-only. This migration adds no column, table,
    /// index, or constraint: its <see cref="Up(MigrationBuilder)"/> contains
    /// exactly two <c>InsertData</c> calls and its
    /// <see cref="Down(MigrationBuilder)"/> the mirror two <c>DeleteData</c>
    /// calls. EF generated no schema difference for it — it scaffolded an empty
    /// Up/Down and its designer model is identical to
    /// <c>ProvisionThanhXaAndSonHungSignatureSkills</c>'s and to
    /// <c>GameDbContextModelSnapshot.cs</c> — which is what makes it a pure data
    /// migration.
    ///
    /// <b>Migration-level <c>InsertData</c> is the decided mechanism</b>
    /// (<c>DATABASE.md</c> §1 note item 5 "Mechanism — decided", TASK-052; the
    /// TASK-053 <c>ProvisionBossDefinitions</c> precedent).
    /// <c>HasData</c>/model seed data, a startup loader, runtime provisioning, a
    /// manual-SQL deployment path, and <c>INSERT ... ON CONFLICT</c> are all
    /// forbidden and are not used. Idempotency comes solely from EF's migration
    /// history: the migration runs once per database, so re-running
    /// <c>dotnet ef database update</c> inserts nothing further, and
    /// <c>BossDefinitionId</c> (PK) plus <c>Identity</c> (UNIQUE) are the
    /// database-level guarantee against duplicates (<c>DATABASE.md</c> §3).
    ///
    /// <b>Row set — exactly these two, and no other row.</b> The canonical
    /// <c>BossDefinitionId</c> values of <c>DATABASE.md</c> §1 note item 2, each
    /// with its <c>BOSS_RULES.md</c> §6.4 <c>Identity</c>. The three rows
    /// TASK-053 already provisioned are <b>not</b> touched, re-inserted,
    /// updated, or deleted. No placeholder row is created.
    ///
    /// <b>Row content is transcribed, never computed or invented</b>
    /// (<c>DATABASE.md</c> §1 note item 5 "Row content"). Per column:
    /// <c>BossDefinitionId</c> and <c>Identity</c> per §1 note item 2 and
    /// <c>BOSS_RULES.md</c> §6.4; <c>Element</c> from <c>BOSS_RULES.md</c> §6
    /// encoded as the Domain <c>Element</c> enum's numeric value (the same
    /// <c>HasConversion&lt;int&gt;()</c> mapping
    /// <c>BossDefinitionConfiguration</c> applies — <c>Tho = 1</c>,
    /// <c>Kim = 4</c>); <c>PassiveDefinition</c> with exactly the members
    /// <c>passiveId</c>, <c>threshold</c>, <c>resetBehavior</c> (note item 3);
    /// <c>SkillDefinition</c> with exactly the members <c>skillId</c>,
    /// <c>baseDamage</c>, <c>chargeRequirement</c>, <c>cooldownTurns</c>
    /// (note item 4). The JSON text is produced by
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
    ///
    /// <b>Both new Passives are non-match-charged, and Sơn Thạch Vệ's is the one
    /// documented non-default Reset Behavior.</b> <c>BOSS_RULES.md</c> §6.2 gives
    /// Sơn Thạch Vệ the <c>Boss HP ≤ 50%</c> trigger and Kim Lôi Vương the
    /// <c>Player Combo ≥ 4</c> trigger — both alternate trigger categories
    /// (<c>PASSIVE_RULES.md</c> §3), not Match counts — so each stores the
    /// documented <c>threshold: null</c>, never the <c>0</c> sentinel
    /// (<c>DATABASE.md</c> §1 note item 3, §3). <c>null</c> means
    /// <b>no match-charging threshold</b>; it is <b>not</b> a statement that the
    /// Passive is always-active. <c>BOSS_RULES.md</c> §6.2.4 authors Sơn Thạch
    /// Vệ's Passive one-time ("it does not re-trigger once it has activated"),
    /// the non-default <b>No reset / persistent</b> form
    /// (<c>PASSIVE_RULES.md</c> §4 items 2–3), whose storage token is the
    /// existing <c>Persistent</c> (<c>DATABASE.md</c> §1 note item 3, §3). No new
    /// token is introduced. Kim Lôi Vương's is the documented <c>Default</c>.
    ///
    /// <b>Neither Skill applies a secondary effect.</b> <c>BOSS_RULES.md</c>
    /// §6.3.1 items 4–5 record <c>Secondary Effect: None</c> and
    /// <c>Board Effect: None</c> for Earthquake and Thunder Strike, so both
    /// Skills are direct damage through the existing Damage Pipeline using each
    /// Boss's Element. The <c>SkillDefinition</c> document has no member for a
    /// secondary effect in any case (<c>DATABASE.md</c> §1 note item 4), so
    /// nothing is encoded for one and nothing is invented.
    /// </summary>
    public partial class ProvisionTwoRemainingMvpBosses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sơn Thạch Vệ (BOSS_RULES.md §6, §6.1–§6.4; DATABASE.md §1 note
            // items 2–4). Element = Thổ (1). Passive "son-thach-ve-enrage" on the
            // "Boss HP ≤ 50%" trigger (§6.2, §6.2.4) — not match-charged, so
            // `threshold: null` (never the 0 sentinel). §6.2.4 authors it one-time
            // / no re-trigger after activation, the non-default No reset /
            // persistent form, so `resetBehavior` is the existing `Persistent`
            // token. Skill "earthquake": base damage 150, charge requirement 5,
            // cooldown 0 (§6.3, §6.3.1 item 4). The §6.1 combat stats
            // (MaxHP 3000, ATK 120, DEF 0, EnrageThreshold 1500) are Domain-only
            // configuration and are deliberately not columns.
            migrationBuilder.InsertData(
                table: "BossDefinition",
                columns: new[] { "BossDefinitionId", "Identity", "Element", "PassiveDefinition", "SkillDefinition" },
                values: new object[] { "boss-def-son-thach-ve", "boss-son-thach-ve", 1, "{\"passiveId\":\"son-thach-ve-enrage\",\"threshold\":null,\"resetBehavior\":\"Persistent\"}", "{\"skillId\":\"earthquake\",\"baseDamage\":150,\"chargeRequirement\":5,\"cooldownTurns\":0}" });

            // Kim Lôi Vương (§6, §6.1–§6.4). Element = Kim (4). Passive
            // "kim-loi-vuong-combo" on the "Player Combo ≥ 4" trigger (§6.2,
            // §6.2.5) — not match-charged, so `threshold: null`. §6.2.5 declares
            // the documented Default Reset Behavior and the existing
            // refresh-not-stack default, so `resetBehavior` is `Default`. Skill
            // "thunder-strike": base damage 180, charge requirement 5, cooldown 0
            // (§6.3, §6.3.1 item 5). The §6.1 combat stats (MaxHP 2800, ATK 140,
            // DEF 0, EnrageThreshold 2100) are Domain-only configuration.
            migrationBuilder.InsertData(
                table: "BossDefinition",
                columns: new[] { "BossDefinitionId", "Identity", "Element", "PassiveDefinition", "SkillDefinition" },
                values: new object[] { "boss-def-kim-loi-vuong", "boss-kim-loi-vuong", 4, "{\"passiveId\":\"kim-loi-vuong-combo\",\"threshold\":null,\"resetBehavior\":\"Default\"}", "{\"skillId\":\"thunder-strike\",\"baseDamage\":180,\"chargeRequirement\":5,\"cooldownTurns\":0}" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The mirror of Up: remove exactly the two rows this migration
            // inserted, keyed by the same canonical BossDefinitionId values (the
            // PK, DATABASE.md §1 note item 2), and no other row (no broad table
            // delete). The three rows of TASK-053 are left untouched.
            migrationBuilder.DeleteData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-son-thach-ve");

            migrationBuilder.DeleteData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-kim-loi-vuong");
        }
    }
}
