using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Provisions the one authored Boss Card content row —
    /// <c>earthquake</c>, category <c>BossCard</c>, <c>PowerCost = 30</c>,
    /// <c>LoadoutCopyLimit = 1</c>, effect <c>Shield</c> / <c>Flat</c> / <c>150</c>
    /// — the ninth content-defined <c>CardDefinition</c> row (<c>DATABASE.md</c> §1,
    /// §5 item 4; <c>CARD_RULES.md</c> §1.1; <c>BOSS_RULES.md</c> §4.2, §4.4 item 5,
    /// §6.3.1 item 4; ADR-029).
    ///
    /// <b>Data only — no schema operation.</b> The <c>CardDefinition</c> table, its
    /// primary key, and every column already exist
    /// (<c>20260925150903_AddCardPersistence</c>, with the structured
    /// <c>EffectDefinition</c> column introduced by
    /// <c>20261001112446_StructureCardDefinitionEffectDefinition</c>). EF generated
    /// no schema difference for this migration — it scaffolded an empty Up/Down and
    /// <c>GameDbContextModelSnapshot.cs</c> is unchanged — so this file must add no
    /// column, table, index, or constraint. Its <see cref="Up(MigrationBuilder)"/>
    /// contains exactly one <c>InsertData</c> call and its
    /// <see cref="Down(MigrationBuilder)"/> the mirror <c>DeleteData</c> call.
    ///
    /// <b>Migration-level <c>InsertData</c> is the decided mechanism</b>
    /// (<c>DATABASE.md</c> §5 item 4; <c>CARD_RULES.md</c> §1.1 item 5 records the
    /// insert-only content migration explicitly; the TASK-053 / TASK-085 /
    /// TASK-168 precedents). <c>HasData</c>/model seed data, a startup loader, a
    /// JSON content pipeline, an external content service, an API-based
    /// provisioning path, and <c>INSERT ... ON CONFLICT</c>/upsert are all
    /// forbidden and are not used. Idempotency comes solely from EF's migration
    /// history: the migration runs once per database, and the primary key is the
    /// database-level guarantee against duplicates.
    ///
    /// <b>The identity is the ability's <c>SkillId</c>, not a
    /// <c>card-&lt;kebab&gt;</c> key.</b> <c>CARD_RULES.md</c> §1.1 item 3 owns the
    /// approved exception to <c>DATABASE.md</c> §1/§3's Player Card value form: a
    /// Boss Card's <c>CardDefinitionId</c> is the ability identity already recorded
    /// in <c>BOSS_RULES.md</c> §6.4's <c>SkillId</c> column — <c>"earthquake"</c> —
    /// so the transitional <c>BossSkillCast.skillId</c> and the new
    /// <c>BossCardCast.cardId</c> name the same ability with the same string. The
    /// exception is confined to <c>BossCard</c> rows and changes no Player Card's
    /// identity.
    ///
    /// <b>Every value is transcribed from its owning document</b>
    /// (<c>DATABASE.md</c> §5 item 4 rule (b)), and none is computed or invented:
    /// <list type="bullet">
    /// <item><c>CardDefinitionId</c> — <c>"earthquake"</c>, the ability identity
    /// (<c>BOSS_RULES.md</c> §6.4; <c>CARD_RULES.md</c> §1.1 item 3).</item>
    /// <item><c>Name</c> — <c>"Earthquake"</c>, the ability's display name
    /// (<c>BOSS_RULES.md</c> §6.3/§6.3.1 item 4; presentation text, never the
    /// identity).</item>
    /// <item><c>Category</c> — <c>2</c>, the Domain <c>CardCategory.BossCard</c>
    /// ordinal (<c>CARD_RULES.md</c> §1; <c>DATABASE.md</c> §1/§3; the same
    /// <c>HasConversion&lt;int&gt;()</c> mapping <c>CardDefinitionConfiguration</c>
    /// applies — <c>Basic = 0</c>, <c>PetSkill = 1</c>, <c>BossCard = 2</c>).
    /// </item>
    /// <item><c>PowerCost</c> — <c>30</c>, the approved initial cost
    /// (<c>BOSS_RULES.md</c> §4.4 item 5; TASK-308 §1 OD-2a).</item>
    /// <item><c>LoadoutCopyLimit</c> — <c>1</c>, persisted only because the column
    /// is required and non-nullable and <b>never read</b> for a <c>BossCard</c> row:
    /// a Boss Card has no Player loadout (<c>CARD_RULES.md</c> §1.1 item 5;
    /// <c>DATABASE.md</c> §3).</item>
    /// <item><c>EffectDefinition</c> — the one-element ARRAY
    /// <c>[{"effectType":"Shield","valueType":"Flat","value":150}]</c>, the
    /// authored <c>Shield</c> <c>150 Flat</c> effect of <c>BOSS_RULES.md</c>
    /// §6.3.1 item 4 / §4.4 item 5 / TASK-308 §1 OD-2b. It is written in the
    /// ARRAY shape <c>DATABASE.md</c> §1 items 1–9 fixes (TASK-111 D-1/D-1a/D-1b),
    /// born structured like the TASK-168 rows, and its text must equal
    /// <c>CardEffectDefinitions.ToPersistedPayload()</c> byte-for-byte so the strict
    /// reader accepts it. Every member is drawn from the EXISTING closed sets:
    /// <c>Shield</c> is a §1 <c>effectType</c> member (the same identity
    /// <c>card-shield</c> uses), <c>Flat</c> is a §1 <c>valueType</c> member, and
    /// the effect is <b>self-targeted at the Boss</b> (<c>BOSS_RULES.md</c> §6.3.1
    /// item 4: "Grants a temporary Shield to the Boss on
    /// <c>BossState.ActiveStatusEffects</c>") — targeting is the cast contract's
    /// (<c>CARD_RULES.md</c> §3 item 9), not an <c>EffectDefinition</c> member, so
    /// no new member is introduced.</item>
    /// </list>
    ///
    /// <b>Nothing here executes.</b> The stored <c>EffectDefinition</c> payload is
    /// <b>definition data only</b> (<c>DATABASE.md</c> §1 item 5): this migration
    /// casts no Card, deducts no Power, creates no Shield instance, and reads no
    /// battle state.
    ///
    /// <b>No Player surface.</b> This row creates no <c>PlayerUnlockedCard</c> row,
    /// no loadout slot, no collection projection, and no API exposure; the Boss
    /// Card stays unowned and invisible to the Player (<c>CARD_RULES.md</c> §1.1
    /// item 5; <c>BOSS_RULES.md</c> §4.2 items 1, 8).
    ///
    /// <b>Reversibility.</b> <see cref="Down(MigrationBuilder)"/> removes exactly
    /// the one row this migration inserted, keyed by the same canonical primary-key
    /// value — no broad table delete and no other row. With the row gone, the form
    /// signal disappears (<c>BOSS_RULES.md</c> §4.4 item 6: the ability's form is
    /// <c>BossCard</c> iff this row exists), so the Boss returns to the documented
    /// transitional <c>BossSkill</c> path — the pre-migration state.
    /// </summary>
    public partial class ProvisionBossCardContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DATABASE.md §1; CARD_RULES.md §1.1; BOSS_RULES.md §4.2, §4.4 item 5,
            // §6.3.1 item 4.
            //
            // The single authored Boss Card: identity = the ability SkillId
            // ("earthquake", §1.1 item 3's approved exception), category BossCard
            // (2), the approved 30 Power cost (OD-2a), LoadoutCopyLimit 1 (required
            // column, never read for this row), and the authored Shield 150 Flat
            // effect (OD-2b), written in the contract's ARRAY shape and byte-equal
            // to CardEffectDefinitions.ToPersistedPayload().
            migrationBuilder.InsertData(
                table: "CardDefinition",
                columns: new[] { "CardDefinitionId", "Name", "Category", "PowerCost", "LoadoutCopyLimit", "EffectDefinition" },
                values: new object[] { "earthquake", "Earthquake", 2, 30, 1, "[{\"effectType\":\"Shield\",\"valueType\":\"Flat\",\"value\":150}]" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The mirror of Up: remove exactly the one row this migration inserted,
            // keyed by its canonical primary key (the ability SkillId). No other
            // row is touched and no broad table delete is issued.
            migrationBuilder.DeleteData(
                table: "CardDefinition",
                keyColumn: "CardDefinitionId",
                keyValue: "earthquake");
        }
    }
}
