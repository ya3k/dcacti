using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Provisions the <c>flame-burst</c> Boss Card content row — Hỏa Long's one
    /// MVP ability in its target <c>BossCard</c> form — and sets that Boss's
    /// approved <c>CastPriorityScore = 85</c> (<c>DATABASE.md</c> §1, §5 item 4;
    /// <c>CARD_RULES.md</c> §1.1; <c>BOSS_RULES.md</c> §4.2, §4.3 item 5,
    /// §4.4 item 3, §6.3, §6.3.1 item 1; ADR-029 Slice 4; TASK-309 §1 B-1/B-2,
    /// D-1, Power Cost table).
    ///
    /// <b>Order: first of the four Slice 4 migrations.</b> TASK-309/ADR-029
    /// §6.3 mandates the sequence <c>flame-burst</c> → <c>root</c> →
    /// <c>drain-power</c> → <c>thunder-strike</c>; the migration stamps are
    /// monotonic, so the chain applies in exactly that order. After all four,
    /// the provisioned <c>CardDefinition</c> set is 13 rows.
    ///
    /// <b>Data only — no schema operation.</b> The <c>CardDefinition</c> table and
    /// its structured <c>EffectDefinition</c> column already exist
    /// (<c>20260925150903_AddCardPersistence</c>,
    /// <c>20261001112446_StructureCardDefinitionEffectDefinition</c>), and
    /// <c>BossDefinition.CastPriorityScore</c> was added by
    /// <c>20261019125417_AddBossCastPriorityScore</c> (TASK-308). EF detected no
    /// model difference — this migration was scaffolded empty and
    /// <c>GameDbContextModelSnapshot.cs</c> is unchanged — so this file adds no
    /// column, table, index, or constraint. Its <see cref="Up(MigrationBuilder)"/>
    /// contains exactly one <c>InsertData</c> (the Card row) and one
    /// <c>UpdateData</c> (the Boss's cast-priority score); its
    /// <see cref="Down(MigrationBuilder)"/> is the exact mirror.
    ///
    /// <b>Migration-level <c>InsertData</c>/<c>UpdateData</c> is the decided
    /// mechanism</b> (<c>DATABASE.md</c> §5 item 4; the TASK-053 / TASK-085 /
    /// TASK-168 / TASK-184 / TASK-308 precedents; ADR-024's "subsequent balance
    /// adjustments are applied via data migrations using
    /// <c>migrationBuilder.UpdateData</c>"). <c>HasData</c>/model seed data, a
    /// startup loader, a JSON content pipeline, an external content service, an
    /// API-based provisioning path, and upsert are all forbidden and are not
    /// used. Idempotency comes solely from EF's migration history: each migration
    /// runs once per database, and the primary keys are the database-level
    /// guarantee against duplicates.
    ///
    /// <b>Both operations are in one migration on purpose.</b> <c>BOSS_RULES.md</c>
    /// §4.4 item 6 / §4.3 item 5 fix the invariant: the <c>BossCard</c> form
    /// exists iff the row exists, and a <c>BossCard</c> form requires a non-null
    /// authored score, otherwise battle creation fails closed. Splitting the
    /// insert and the score update across two migrations would commit a window in
    /// which the row exists with <c>NULL</c> — invalid definition data. The single
    /// migration keeps every committed state valid.
    ///
    /// <b>The identity is the ability's <c>SkillId</c>.</b>
    /// <c>CARD_RULES.md</c> §1.1 item 3 owns the approved exception to
    /// <c>DATABASE.md</c> §1/§3's <c>card-&lt;kebab&gt;</c> value form for
    /// <c>BossCard</c> rows: the <c>CardDefinitionId</c> is <c>"flame-burst"</c>,
    /// the identity <c>BOSS_RULES.md</c> §6.4 fixes, so the transitional
    /// <c>BossSkillCast.skillId</c> and the new <c>BossCardCast.cardId</c> name the
    /// same ability with the same string. No Player Card identity changes.
    ///
    /// <b>Every row value is transcribed from its owning document</b>
    /// (<c>DATABASE.md</c> §5 item 4 rule (b)); none is computed or invented:
    /// <list type="bullet">
    /// <item><c>CardDefinitionId</c> — <c>"flame-burst"</c>, the ability identity
    /// (<c>BOSS_RULES.md</c> §6.4; <c>CARD_RULES.md</c> §1.1 item 3).</item>
    /// <item><c>Name</c> — <c>"Flame Burst"</c>, the ability's display name
    /// (<c>BOSS_RULES.md</c> §6.3; presentation text, never the identity).</item>
    /// <item><c>Category</c> — <c>2</c>, the Domain <c>CardCategory.BossCard</c>
    /// ordinal (<c>CARD_RULES.md</c> §1; <c>DATABASE.md</c> §1/§3; the same
    /// <c>HasConversion&lt;int&gt;()</c> mapping <c>CardDefinitionConfiguration</c>
    /// applies — <c>Basic = 0</c>, <c>PetSkill = 1</c>, <c>BossCard = 2</c>).
    /// </item>
    /// <item><c>PowerCost</c> — <c>40</c>, the approved initial cost for
    /// <c>flame-burst</c> (TASK-309 §1 Power Cost table; <c>BOSS_RULES.md</c>
    /// §4.2/§4.4 item 5).</item>
    /// <item><c>LoadoutCopyLimit</c> — <c>1</c>, persisted only because the column
    /// is required and non-nullable and <b>never read</b> for a <c>BossCard</c>
    /// row: a Boss Card has no Player loadout (<c>CARD_RULES.md</c> §1.1 items 4–5;
    /// <c>DATABASE.md</c> §3).</item>
    /// <item><c>EffectDefinition</c> — the one-element ARRAY
    /// <c>[{"effectType":"AtkBuff","valueType":"PercentagePoints","value":50,"duration":3}]</c>:
    /// the approved Boss self ATK buff <c>+50%</c> for <c>3</c> Turns
    /// (<c>BOSS_RULES.md</c> §6.3.1 item 1; TASK-309 §1 B-2 and D-1's
    /// <c>AtkBuff</c> identity). It is written in the ARRAY shape
    /// <c>DATABASE.md</c> §1 items 1–9 fixes, born structured, and its text must
    /// equal <c>CardEffectDefinitions.ToPersistedPayload()</c> byte-for-byte so
    /// the strict reader accepts it. <c>duration</c> is required on an
    /// <c>AtkBuff</c> element and is the approved <c>3</c> — the TASK-309
    /// re-authored value, which supersedes the transitional <c>BossSkill</c>
    /// declaration's historical <c>2</c>; that legacy declaration is unchanged in
    /// Domain until Slice 5 retires it (the documented divergence). The effect is
    /// <b>self-targeted at the Boss</b> (<c>BOSS_RULES.md</c> §6.3.1 item 1:
    /// "Applies a temporary Boss ATK buff to <c>BossState.ActiveStatusEffects</c>");
    /// targeting is the cast contract's actor-relative rule (<c>CARD_RULES.md</c>
    /// §3 item 9), not an <c>EffectDefinition</c> member, so no new member is
    /// introduced. <c>BaseDamage</c> is not a stored member; the ability retains
    /// <c>BaseDamage = 0</c> (<c>BOSS_RULES.md</c> §6.3; no direct damage).</item>
    /// </list>
    ///
    /// <b>The score change is the fifth Domain-Overlay value.</b> The
    /// <c>UpdateData</c> sets <c>BossDefinition.CastPriorityScore = 85</c> on
    /// <c>boss-def-hoa-long</c>, replacing the not-authored <c>NULL</c> the column
    /// carried after TASK-308. <c>BOSS_RULES.md</c> §4.3 item 5 owns the value:
    /// it is the approved initial cast-priority score (TASK-309 §1 B-1), a
    /// starting value to monitor and adjust from real gameplay data — not globally
    /// optimal, and it authorizes no in-battle cast-rate guarantee. The selection
    /// rule itself is unchanged: a cast is chosen only when
    /// <c>CastPriorityScore &gt; BoardScore</c>, and a tie goes to the board
    /// action. The row's other columns — stats, <c>Identity</c>,
    /// <c>PassiveDefinition</c>, <c>SkillDefinition</c>, and the other four
    /// overlay values — are not touched; in particular the legacy
    /// <c>SkillDefinition</c> JSON keeps its historical values until Slice 5.
    ///
    /// <b>Nothing here executes.</b> The stored <c>EffectDefinition</c> payload is
    /// <b>definition data only</b> (<c>DATABASE.md</c> §1 item 5): this migration
    /// casts no Card, deducts no Power, buffs no ATK, starts no cooldown, and reads
    /// no battle state.
    ///
    /// <b>No Player surface.</b> This row creates no <c>PlayerUnlockedCard</c> row,
    /// no loadout slot, no collection projection, and no API exposure; the Boss
    /// Card stays unowned and invisible to the Player (<c>CARD_RULES.md</c> §1.1
    /// item 5; <c>BOSS_RULES.md</c> §4.2 items 1, 8).
    ///
    /// <b>Reversibility, independent of the later migrations.</b>
    /// <see cref="Down(MigrationBuilder)"/> deletes exactly the one
    /// <c>CardDefinition</c> row this migration inserted, keyed by
    /// <c>"flame-burst"</c>, and restores
    /// <c>boss-def-hoa-long.CastPriorityScore = NULL</c> — the pre-migration state.
    /// It touches no other row and depends on no later Slice 4 migration, so it
    /// reverts correctly even when only this migration is rolled back. With the row
    /// gone, the form signal disappears (<c>BOSS_RULES.md</c> §4.4 item 6) and the
    /// Boss returns to the documented transitional <c>BossSkill</c> path.
    /// </summary>
    public partial class ProvisionFlameBurstBossCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DATABASE.md §1; CARD_RULES.md §1.1 item 3/5; BOSS_RULES.md §4.2,
            // §6.3.1 item 1; TASK-309 §1 B-2 (cost 40, +50% Boss ATK, duration 3).
            //
            // The Boss Card identity is the ability SkillId ("flame-burst" —
            // §1.1 item 3's approved exception), category BossCard (2), the
            // approved 40 Power cost, LoadoutCopyLimit 1 (required column, never
            // read for this row), and the approved AtkBuff +50% duration-3 effect,
            // written in the contract's ARRAY shape and byte-equal to
            // CardEffectDefinitions.ToPersistedPayload().
            migrationBuilder.InsertData(
                table: "CardDefinition",
                columns: new[] { "CardDefinitionId", "Name", "Category", "PowerCost", "LoadoutCopyLimit", "EffectDefinition" },
                values: new object[] { "flame-burst", "Flame Burst", 2, 40, 1, "[{\"effectType\":\"AtkBuff\",\"valueType\":\"PercentagePoints\",\"value\":50,\"duration\":3}]" });

            // BOSS_RULES.md §4.3 item 5 / TASK-309 §1 B-1: the approved initial
            // cast-priority score for the newly provisioned Boss Card. It is set
            // in the same migration as the row so the committed state never has a
            // BossCard form without the non-null score that form requires
            // (BOSS_RULES.md §4.3 item 5, §4.4 item 6).
            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-hoa-long",
                column: "CastPriorityScore",
                value: 85);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The mirror of Up: remove exactly the one row this migration inserted,
            // keyed by its canonical primary key (the ability SkillId).
            migrationBuilder.DeleteData(
                table: "CardDefinition",
                keyColumn: "CardDefinitionId",
                keyValue: "flame-burst");

            // Restore the pre-migration not-authored state for this Boss only; the
            // other four Bosses' rows are untouched, and no later Slice 4
            // migration is required.
            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-hoa-long",
                column: "CastPriorityScore",
                value: null);
        }
    }
}
