using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Provisions the <c>root</c> Boss Card content row — Mộc Yêu's one MVP
    /// ability in its target <c>BossCard</c> form — and sets that Boss's approved
    /// <c>CastPriorityScore = 85</c> (<c>DATABASE.md</c> §1, §5 item 4;
    /// <c>CARD_RULES.md</c> §1.1; <c>BOSS_RULES.md</c> §4.2, §4.3 item 5,
    /// §4.4 item 3, §6.3, §6.3.1 item 3; ADR-029 Slice 4; TASK-309 §1 B-1/B-3,
    /// D-1, Power Cost table).
    ///
    /// <b>Order: second of the four Slice 4 migrations.</b> TASK-309/ADR-029
    /// §6.3 mandates <c>flame-burst</c> → <c>root</c> → <c>drain-power</c> →
    /// <c>thunder-strike</c>; the migration stamps are monotonic, so the chain
    /// applies in exactly that order. After all four, the provisioned
    /// <c>CardDefinition</c> set is 13 rows.
    ///
    /// <b>Data only — no schema operation.</b> The <c>CardDefinition</c> table, its
    /// structured <c>EffectDefinition</c> column, and the nullable
    /// <c>BossDefinition.CastPriorityScore</c> already exist
    /// (<c>20260925150903_AddCardPersistence</c>,
    /// <c>20261001112446_StructureCardDefinitionEffectDefinition</c>,
    /// <c>20261019125417_AddBossCastPriorityScore</c>). EF detected no model
    /// difference — this migration was scaffolded empty and
    /// <c>GameDbContextModelSnapshot.cs</c> is unchanged — so this file adds no
    /// column, table, index, or constraint. Its <see cref="Up(MigrationBuilder)"/>
    /// contains exactly one <c>InsertData</c> and one <c>UpdateData</c>; its
    /// <see cref="Down(MigrationBuilder)"/> is the exact mirror.
    ///
    /// <b>Migration-level <c>InsertData</c>/<c>UpdateData</c> is the decided
    /// mechanism</b> (<c>DATABASE.md</c> §5 item 4; the TASK-053 / TASK-085 /
    /// TASK-168 / TASK-184 / TASK-308 precedents; ADR-024's
    /// <c>migrationBuilder.UpdateData</c> balance-adjustment path).
    /// <c>HasData</c>, a startup loader, a JSON content pipeline, an external
    /// content service, an API provisioning path, and upsert are not used;
    /// idempotency is EF's migration history plus the primary keys.
    ///
    /// <b>Both operations are in one migration on purpose.</b>
    /// <c>BOSS_RULES.md</c> §4.3 item 5 / §4.4 item 6: the <c>BossCard</c> form
    /// exists iff the row exists, and that form requires a non-null authored
    /// score or battle creation fails closed. One migration keeps every committed
    /// state valid instead of committing a row-with-<c>NULL</c> window.
    ///
    /// <b>The identity is the ability's <c>SkillId</c>.</b>
    /// <c>CARD_RULES.md</c> §1.1 item 3's approved exception makes the
    /// <c>CardDefinitionId</c> <c>"root"</c> — <c>BOSS_RULES.md</c> §6.4's identity
    /// for Mộc Yêu's ability — so <c>BossSkillCast.skillId</c> and
    /// <c>BossCardCast.cardId</c> report the same string. No Player Card identity
    /// changes.
    ///
    /// <b>Every row value is transcribed from its owning document</b>
    /// (<c>DATABASE.md</c> §5 item 4 rule (b)); none is computed or invented:
    /// <list type="bullet">
    /// <item><c>CardDefinitionId</c> — <c>"root"</c>, the ability identity
    /// (<c>BOSS_RULES.md</c> §6.4; <c>CARD_RULES.md</c> §1.1 item 3).</item>
    /// <item><c>Name</c> — <c>"Root"</c>, the ability's display name
    /// (<c>BOSS_RULES.md</c> §6.3; presentation text, never the identity).</item>
    /// <item><c>Category</c> — <c>2</c>, the Domain <c>CardCategory.BossCard</c>
    /// ordinal (<c>CARD_RULES.md</c> §1; <c>DATABASE.md</c> §1/§3;
    /// <c>Basic = 0</c>, <c>PetSkill = 1</c>, <c>BossCard = 2</c>).</item>
    /// <item><c>PowerCost</c> — <c>35</c>, the approved initial cost for
    /// <c>root</c> (TASK-309 §1 Power Cost table).</item>
    /// <item><c>LoadoutCopyLimit</c> — <c>1</c>, persisted only because the column
    /// is required and non-nullable and <b>never read</b> for a <c>BossCard</c>
    /// row (<c>CARD_RULES.md</c> §1.1 items 4–5; <c>DATABASE.md</c> §3).</item>
    /// <item><c>EffectDefinition</c> — the one-element ARRAY
    /// <c>[{"effectType":"AtkDebuff","valueType":"PercentagePoints","value":30,"duration":3}]</c>:
    /// the approved active-Pet ATK debuff <c>-30%</c> for <c>3</c> Turns
    /// (<c>BOSS_RULES.md</c> §6.3.1 item 3; TASK-309 §1 B-3 and D-1's
    /// <c>AtkDebuff</c> identity). Written in the ARRAY shape <c>DATABASE.md</c>
    /// §1 fixes, born structured, byte-equal to
    /// <c>CardEffectDefinitions.ToPersistedPayload()</c>. <c>duration</c> is
    /// required on an <c>AtkDebuff</c> element and is the approved <c>3</c> — the
    /// TASK-309 re-authored value, which supersedes the transitional
    /// <c>BossSkill</c> declaration's historical <c>2</c>; that legacy declaration
    /// is unchanged in Domain until Slice 5 (the documented divergence). The
    /// magnitude is stored as the positive <c>30</c> that
    /// <c>BOSS_RULES.md</c> §6.3.1 item 3 authors ("Percentage-based ATK
    /// reduction (-30% active Pet ATK)"); the actor-relative direction — the
    /// active Pet's ATK is reduced — is the cast/execution contract's
    /// (<c>CARD_RULES.md</c> §3 item 9, <c>COMBAT_RULES.md</c> §5.4), not an
    /// <c>EffectDefinition</c> member, so no negative encoding or new member is
    /// introduced. <c>BaseDamage</c> stays <c>0</c> (no direct damage).</item>
    /// </list>
    ///
    /// <b>The score change is the fifth Domain-Overlay value.</b> The
    /// <c>UpdateData</c> sets <c>BossDefinition.CastPriorityScore = 85</c> on
    /// <c>boss-def-moc-yeu</c>, replacing the not-authored <c>NULL</c>.
    /// <c>BOSS_RULES.md</c> §4.3 item 5 owns the value: the approved initial score
    /// (TASK-309 §1 B-1), to be monitored and adjusted from real gameplay data —
    /// not globally optimal, no in-battle cast-rate guarantee. Selection is
    /// unchanged: cast only if <c>CastPriorityScore &gt; BoardScore</c>; tie goes
    /// to the board. No other column of the row is touched; the legacy
    /// <c>SkillDefinition</c> JSON keeps its historical values until Slice 5.
    ///
    /// <b>Nothing here executes.</b> The payload is <b>definition data only</b>
    /// (<c>DATABASE.md</c> §1 item 5): no cast, no Power spend, no debuff instance
    /// is created, and no battle state is read.
    ///
    /// <b>No Player surface.</b> This row creates no <c>PlayerUnlockedCard</c> row,
    /// no loadout slot, no collection projection and no API exposure
    /// (<c>CARD_RULES.md</c> §1.1 item 5; <c>BOSS_RULES.md</c> §4.2 items 1, 8).
    ///
    /// <b>Reversibility, independent of the later migrations.</b>
    /// <see cref="Down(MigrationBuilder)"/> deletes exactly the one
    /// <c>CardDefinition</c> row this migration inserted, keyed by <c>"root"</c>,
    /// and restores <c>boss-def-moc-yeu.CastPriorityScore = NULL</c>. It touches
    /// no other row and depends on no later Slice 4 migration. With the row gone,
    /// the form signal disappears (<c>BOSS_RULES.md</c> §4.4 item 6) and the Boss
    /// returns to the documented transitional <c>BossSkill</c> path.
    /// </summary>
    public partial class ProvisionRootBossCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DATABASE.md §1; CARD_RULES.md §1.1 item 3/5; BOSS_RULES.md §4.2,
            // §6.3.1 item 3; TASK-309 §1 B-3 (cost 35, -30% Pet ATK, duration 3).
            //
            // The Boss Card identity is the ability SkillId ("root" — §1.1 item 3's
            // approved exception), category BossCard (2), the approved 35 Power
            // cost, LoadoutCopyLimit 1 (required column, never read for this row),
            // and the approved AtkDebuff 30% duration-3 effect, written in the
            // contract's ARRAY shape and byte-equal to
            // CardEffectDefinitions.ToPersistedPayload().
            migrationBuilder.InsertData(
                table: "CardDefinition",
                columns: new[] { "CardDefinitionId", "Name", "Category", "PowerCost", "LoadoutCopyLimit", "EffectDefinition" },
                values: new object[] { "root", "Root", 2, 35, 1, "[{\"effectType\":\"AtkDebuff\",\"valueType\":\"PercentagePoints\",\"value\":30,\"duration\":3}]" });

            // BOSS_RULES.md §4.3 item 5 / TASK-309 §1 B-1: the approved initial
            // cast-priority score for the newly provisioned Boss Card, set in the
            // same migration as the row so the committed form never lacks its
            // required non-null score (BOSS_RULES.md §4.3 item 5, §4.4 item 6).
            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-moc-yeu",
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
                keyValue: "root");

            // Restore the pre-migration not-authored state for this Boss only; the
            // other four Bosses' rows are untouched, and no later Slice 4
            // migration is required.
            migrationBuilder.UpdateData(
                table: "BossDefinition",
                keyColumn: "BossDefinitionId",
                keyValue: "boss-def-moc-yeu",
                column: "CastPriorityScore",
                value: null);
        }
    }
}
