using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Provisions the content-defined MVP <c>PetDefinition</c> (3 rows),
    /// <c>CardDefinition</c> (6 rows), and <c>RelicDefinition</c> (4 rows)
    /// (<c>DATABASE.md</c> §1, §5 item 4; TASK-082 decision A/D).
    ///
    /// <b>Data only — no schema operation.</b> The three tables, their primary
    /// keys, the <c>PetDefinition.SignatureSkillCardId</c> foreign key, and
    /// every column constraint were created by
    /// <c>20260924163011_AddPetPersistence</c>,
    /// <c>20260925134303_AddRelicPersistence</c>, and
    /// <c>20260925150903_AddCardPersistence</c>, which stay untouched and
    /// schema-only. This migration adds no column, table, index, or constraint:
    /// its <see cref="Up(MigrationBuilder)"/> contains exactly thirteen
    /// <c>InsertData</c> calls and its <see cref="Down(MigrationBuilder)"/> the
    /// mirror thirteen <c>DeleteData</c> calls. EF generated no schema
    /// difference for it (<c>GameDbContextModelSnapshot.cs</c> is unchanged),
    /// which is what makes it a pure data migration.
    ///
    /// <b>Migration-level <c>InsertData</c> is the decided mechanism</b>
    /// (<c>DATABASE.md</c> §5 item 4; the TASK-052 / TASK-053
    /// <c>ProvisionBossDefinitions</c> precedent). <c>HasData</c>/model seed
    /// data, a startup loader, a JSON content pipeline, an external content
    /// service, an API-based provisioning path, and
    /// <c>INSERT ... ON CONFLICT</c>/upsert are all forbidden and are not used.
    /// Idempotency comes solely from EF's migration history: the migration runs
    /// once per database, so re-running <c>dotnet ef database update</c> inserts
    /// nothing further, and each table's primary key is the database-level
    /// guarantee against duplicates.
    ///
    /// <b>Row set — exactly thirteen, all content-defined.</b> Only rows whose
    /// required members have a documented value may be provisioned
    /// (<c>DATABASE.md</c> §5 item 4 rule (a)). The deferred content stays
    /// absent and no placeholder row is created:
    /// <list type="bullet">
    /// <item><c>Thanh Xà</c> and <c>Sơn Hùng</c> — deferred until their
    /// Signature Skill Cards are content-defined (<c>PET_RULES.md</c> §8); their
    /// <c>SignatureSkillCardId</c> targets do not exist (<c>CARD_RULES.md</c>
    /// §4.1) and the FK is required.</item>
    /// <item>The two TBD Signature Skill Cards themselves
    /// (<c>CARD_RULES.md</c> §4.1).</item>
    /// <item><c>Burning Curse</c> — deferred pending a documented
    /// static-modifier <c>Trigger</c>; the §3-vs-note-1 tension is reported and
    /// not resolved by <c>RELIC_RULES.md</c> §6 note 3, so no invented
    /// <c>Trigger</c> is written.</item>
    /// </list>
    ///
    /// <b>Row content is transcribed, never computed or invented</b>
    /// (<c>DATABASE.md</c> §5 item 4 rule (b)). Per table:
    /// <c>CardDefinition</c> keys/names/<c>EffectDefinition</c> and the
    /// <c>PowerCost</c> values come from <c>CARD_RULES.md</c> §2 and §4.1,
    /// <c>Category</c> from §1 (encoded as the Domain <c>CardCategory</c> enum's
    /// numeric value — the same <c>HasConversion&lt;int&gt;()</c> mapping
    /// <c>CardDefinitionConfiguration</c> applies, <c>Basic = 0</c>,
    /// <c>PetSkill = 1</c>), and <c>LoadoutCopyLimit</c> = 1 from §1 item 5.
    /// <c>PetDefinition</c> keys/identities/elements come from
    /// <c>PET_RULES.md</c> §8, <c>PassiveId</c>/<c>PassiveThreshold</c> from
    /// <c>PASSIVE_RULES.md</c> §8 (the Pet's Passive is carried directly on this
    /// row — <c>DATABASE.md</c> §1 — and there is no <c>PassiveDefinition</c>
    /// table), and <c>SignatureSkillCardId</c> from the Pet's documented
    /// Signature Skill (<c>CARD_RULES.md</c> §4.1). <c>RelicDefinition</c>
    /// keys/names/<c>Trigger</c>/<c>Condition</c>/<c>EffectDefinition</c> come
    /// from <c>RELIC_RULES.md</c> §6, with <c>Trigger</c> drawn from §3's closed
    /// list. <c>Element</c> is encoded as the Domain <c>Element</c> enum's
    /// numeric value (<c>Moc = 0</c>, <c>Tho = 1</c>, <c>Thuy = 2</c>,
    /// <c>Hoa = 3</c>, <c>Kim = 4</c>).
    ///
    /// <b>Insertion order follows the foreign key.</b>
    /// <c>PetDefinition.SignatureSkillCardId</c> is an FK to
    /// <c>CardDefinition.CardDefinitionId</c> with the existing
    /// <c>OnDelete(DeleteBehavior.Restrict)</c> behavior (<c>DATABASE.md</c> §2),
    /// so every Card row is inserted before the Pet rows that reference it.
    /// Relics have no FK and are provisioned independently.
    ///
    /// <b>Nothing here executes.</b> The stored <c>EffectDefinition</c>,
    /// <c>Trigger</c>, and <c>Condition</c> strings are <b>definition data
    /// only</b>: no Card effect, Relic trigger, or Pet Skill resolution reads or
    /// runs them in this task.
    /// </summary>
    public partial class ProvisionPetCardRelicContentDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------
            // CardDefinition — DATABASE.md §1; CARD_RULES.md §1, §2, §4.1.
            //
            // Inserted FIRST: PetDefinition.SignatureSkillCardId is an FK to
            // this table (DATABASE.md §2), so these rows must exist before the
            // Pet rows below reference them.
            // ---------------------------------------------------------------

            // Heal — CARD_RULES.md §2: Cost 20 Power, "Restore the active
            // Pet's HP by 20% of its Max HP". Category Basic (0),
            // LoadoutCopyLimit 1 (§1 item 5).
            migrationBuilder.InsertData(
                table: "CardDefinition",
                columns: new[] { "CardDefinitionId", "Name", "Category", "PowerCost", "LoadoutCopyLimit", "EffectDefinition" },
                values: new object[] { "card-heal", "Heal", 0, 20, 1, "Restore the active Pet's HP by 20% of its Max HP" });

            // Shield — CARD_RULES.md §2: Cost 20 Power, "Active Pet gains
            // Shield equal to 20% of its Max HP". Category Basic (0).
            migrationBuilder.InsertData(
                table: "CardDefinition",
                columns: new[] { "CardDefinitionId", "Name", "Category", "PowerCost", "LoadoutCopyLimit", "EffectDefinition" },
                values: new object[] { "card-shield", "Shield", 0, 20, 1, "Active Pet gains Shield equal to 20% of its Max HP" });

            // Power Charge — CARD_RULES.md §2: Cost 0 Power by design (§2
            // item 3: it must never be blocked by insufficient Power),
            // "Active Pet gains 25 Power". Category Basic (0).
            migrationBuilder.InsertData(
                table: "CardDefinition",
                columns: new[] { "CardDefinitionId", "Name", "Category", "PowerCost", "LoadoutCopyLimit", "EffectDefinition" },
                values: new object[] { "card-power-charge", "Power Charge", 0, 0, 1, "Active Pet gains 25 Power" });

            // Inferno — CARD_RULES.md §4.1 (Xích Lang's Signature Skill):
            // Cost 100 Power, "Deal high Fire (Hỏa) damage; apply Burn".
            // Category PetSkill (1).
            migrationBuilder.InsertData(
                table: "CardDefinition",
                columns: new[] { "CardDefinitionId", "Name", "Category", "PowerCost", "LoadoutCopyLimit", "EffectDefinition" },
                values: new object[] { "card-inferno", "Inferno", 1, 100, 1, "Deal high Fire (Hỏa) damage; apply Burn" });

            // Tidal Barrier — CARD_RULES.md §4.1 (Huyền Quy's Signature
            // Skill): Cost 80 Power, "Heal; Gain Shield". Category PetSkill (1).
            migrationBuilder.InsertData(
                table: "CardDefinition",
                columns: new[] { "CardDefinitionId", "Name", "Category", "PowerCost", "LoadoutCopyLimit", "EffectDefinition" },
                values: new object[] { "card-tidal-barrier", "Tidal Barrier", 1, 80, 1, "Heal; Gain Shield" });

            // Iron Fang — CARD_RULES.md §4.1 (Bạch Hổ's Signature Skill):
            // Cost 100 Power, "High damage; increased Crit chance".
            // Category PetSkill (1).
            migrationBuilder.InsertData(
                table: "CardDefinition",
                columns: new[] { "CardDefinitionId", "Name", "Category", "PowerCost", "LoadoutCopyLimit", "EffectDefinition" },
                values: new object[] { "card-iron-fang", "Iron Fang", 1, 100, 1, "High damage; increased Crit chance" });

            // ---------------------------------------------------------------
            // PetDefinition — DATABASE.md §1; PET_RULES.md §1, §8;
            // PASSIVE_RULES.md §8.
            //
            // Each row carries its Passive directly (PassiveId +
            // PassiveThreshold): DATABASE.md §1 names the field
            // "PassiveDefinition (threshold/effect reference)" and there is no
            // separate PassiveDefinition table or entity. SignatureSkillCardId
            // resolves against the Card rows inserted above.
            // ---------------------------------------------------------------

            // Xích Lang — PET_RULES.md §8: Element Hỏa (3), Passive
            // "every 5 Matches" (PASSIVE_RULES.md §8), Signature Skill
            // Inferno (CARD_RULES.md §4.1). PassiveId per PASSIVE_RULES.md §8.
            migrationBuilder.InsertData(
                table: "PetDefinition",
                columns: new[] { "PetDefinitionId", "Identity", "Element", "PassiveId", "PassiveThreshold", "SignatureSkillCardId" },
                values: new object[] { "pet-xich-lang", "Xích Lang", 3, "passive-xich-lang", 5, "card-inferno" });

            // Bạch Hổ — PET_RULES.md §8: Element Kim (4), Passive "Every 4
            // Matches" (PASSIVE_RULES.md §8), Signature Skill Iron Fang
            // (CARD_RULES.md §4.1).
            migrationBuilder.InsertData(
                table: "PetDefinition",
                columns: new[] { "PetDefinitionId", "Identity", "Element", "PassiveId", "PassiveThreshold", "SignatureSkillCardId" },
                values: new object[] { "pet-bach-ho", "Bạch Hổ", 4, "passive-bach-ho", 4, "card-iron-fang" });

            // Huyền Quy — PET_RULES.md §8: Element Thủy (2), Passive
            // "Every 6 Matches" (PASSIVE_RULES.md §8), Signature Skill
            // Tidal Barrier (CARD_RULES.md §4.1).
            migrationBuilder.InsertData(
                table: "PetDefinition",
                columns: new[] { "PetDefinitionId", "Identity", "Element", "PassiveId", "PassiveThreshold", "SignatureSkillCardId" },
                values: new object[] { "pet-huyen-quy", "Huyền Quy", 2, "passive-huyen-quy", 6, "card-tidal-barrier" });

            // ---------------------------------------------------------------
            // RelicDefinition — DATABASE.md §1; RELIC_RULES.md §1, §3, §6.
            //
            // No FK: Relics are independent of the tables above.
            // ---------------------------------------------------------------

            // Berserker Core — RELIC_RULES.md §6: Trigger OnMatchCount (§3),
            // Condition "every 3 Matches", Effect "+5% ATK".
            migrationBuilder.InsertData(
                table: "RelicDefinition",
                columns: new[] { "RelicDefinitionId", "Name", "Trigger", "Condition", "EffectDefinition" },
                values: new object[] { "relic-berserker-core", "Berserker Core", "OnMatchCount", "every 3 Matches", "+5% ATK" });

            // Mana Crystal — RELIC_RULES.md §6: Trigger OnMatchCount (§3),
            // Condition "every 4 Matches", Effect "+10 Power".
            migrationBuilder.InsertData(
                table: "RelicDefinition",
                columns: new[] { "RelicDefinitionId", "Name", "Trigger", "Condition", "EffectDefinition" },
                values: new object[] { "relic-mana-crystal", "Mana Crystal", "OnMatchCount", "every 4 Matches", "+10 Power" });

            // Assassin Eye — RELIC_RULES.md §6: Trigger OnCombo (§3),
            // Condition "Combo ≥ 3", Effect "Increased Crit chance".
            migrationBuilder.InsertData(
                table: "RelicDefinition",
                columns: new[] { "RelicDefinitionId", "Name", "Trigger", "Condition", "EffectDefinition" },
                values: new object[] { "relic-assassin-eye", "Assassin Eye", "OnCombo", "Combo ≥ 3", "Increased Crit chance" });

            // Emergency Core — RELIC_RULES.md §6: Trigger OnHpBelow (§3),
            // Condition "HP < 30%", Effect "Heal Card cost −50%" (§6 note 2:
            // it re-evaluates continuously while the condition holds).
            migrationBuilder.InsertData(
                table: "RelicDefinition",
                columns: new[] { "RelicDefinitionId", "Name", "Trigger", "Condition", "EffectDefinition" },
                values: new object[] { "relic-emergency-core", "Emergency Core", "OnHpBelow", "HP < 30%", "Heal Card cost −50%" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The mirror of Up: remove exactly the thirteen rows this migration
            // inserted, keyed by the same canonical primary-key values, and no
            // other row (no broad table delete).

            // ---------------------------------------------------------------
            // PetDefinition first: it holds the FK to CardDefinition, so it is
            // deleted before the Cards it references (DATABASE.md §2). The
            // Card/Pet relationship is Restrict, so the reverse order would be
            // rejected by the database.
            // ---------------------------------------------------------------

            migrationBuilder.DeleteData(
                table: "PetDefinition",
                keyColumn: "PetDefinitionId",
                keyValue: "pet-xich-lang");

            migrationBuilder.DeleteData(
                table: "PetDefinition",
                keyColumn: "PetDefinitionId",
                keyValue: "pet-bach-ho");

            migrationBuilder.DeleteData(
                table: "PetDefinition",
                keyColumn: "PetDefinitionId",
                keyValue: "pet-huyen-quy");

            // ---------------------------------------------------------------
            // CardDefinition — no Pet row references these any more.
            // ---------------------------------------------------------------

            migrationBuilder.DeleteData(
                table: "CardDefinition",
                keyColumn: "CardDefinitionId",
                keyValue: "card-heal");

            migrationBuilder.DeleteData(
                table: "CardDefinition",
                keyColumn: "CardDefinitionId",
                keyValue: "card-shield");

            migrationBuilder.DeleteData(
                table: "CardDefinition",
                keyColumn: "CardDefinitionId",
                keyValue: "card-power-charge");

            migrationBuilder.DeleteData(
                table: "CardDefinition",
                keyColumn: "CardDefinitionId",
                keyValue: "card-inferno");

            migrationBuilder.DeleteData(
                table: "CardDefinition",
                keyColumn: "CardDefinitionId",
                keyValue: "card-tidal-barrier");

            migrationBuilder.DeleteData(
                table: "CardDefinition",
                keyColumn: "CardDefinitionId",
                keyValue: "card-iron-fang");

            // ---------------------------------------------------------------
            // RelicDefinition — independent of the tables above.
            // ---------------------------------------------------------------

            migrationBuilder.DeleteData(
                table: "RelicDefinition",
                keyColumn: "RelicDefinitionId",
                keyValue: "relic-berserker-core");

            migrationBuilder.DeleteData(
                table: "RelicDefinition",
                keyColumn: "RelicDefinitionId",
                keyValue: "relic-mana-crystal");

            migrationBuilder.DeleteData(
                table: "RelicDefinition",
                keyColumn: "RelicDefinitionId",
                keyValue: "relic-assassin-eye");

            migrationBuilder.DeleteData(
                table: "RelicDefinition",
                keyColumn: "RelicDefinitionId",
                keyValue: "relic-emergency-core");
        }
    }
}
