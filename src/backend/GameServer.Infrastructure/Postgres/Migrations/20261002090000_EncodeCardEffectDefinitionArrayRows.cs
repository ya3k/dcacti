using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Re-encodes the six provisioned <c>CardDefinition.EffectDefinition</c> rows
    /// from TASK-109's single-object shape to TASK-111's decided <b>array</b>
    /// shape (<c>DATABASE.md</c> §1 items 1–9; TASK-111 D-1/D-1a/D-1b), transcribes
    /// the Pet Skill magnitudes <c>CARD_RULES.md</c> §4.1 now authors (TASK-110),
    /// and corrects <c>card-iron-fang</c>'s effect identity per <c>DATABASE.md</c>
    /// §1 item 8 / TASK-111 Reported Discrepancy 3.
    ///
    /// <b>No schema change.</b> The column is already <c>jsonb NOT NULL</c> —
    /// TASK-109's migration
    /// <c>20261001112446_StructureCardDefinitionEffectDefinition</c> changed its
    /// type. This migration issues <b>data</b> statements only: no <c>ALTER
    /// TABLE</c>, no column, no table, no constraint, no index, no foreign key.
    /// <b><c>RelicDefinition.EffectDefinition</c> is deliberately untouched</b>:
    /// TASK-082 R2-7 remains in force for Relics (ROADMAP.md Phase 2; no decision,
    /// no consumer), so its <c>character varying(128)</c> prose column and its
    /// configuration keep exactly what they are.
    ///
    /// <b>Why the rows are rewritten rather than a subset patched.</b> The array
    /// shape is uniform for every Card (D-1b), so all six content-defined rows must
    /// hold it — the three Basic Cards included, whose single effect becomes the
    /// array's single element. A reader that accepted both shapes would violate
    /// §1 item 6's single well-formed contract, so no compatibility path exists and
    /// none is written here.
    ///
    /// <b>The row set is the six content-defined rows, by exact key.</b> The
    /// <c>CardDefinitionId</c> values are TASK-109's Row Content Migration table
    /// (which took them from the provisioning migration
    /// <c>20260929152651_ProvisionPetCardRelicContentDefinitions</c>): three Basic
    /// Cards and three Pet Skill Cards. Each statement matches its key exactly —
    /// never a prefix, never a <c>LIKE</c> pattern, never by name or by current
    /// effect value — so no seventh row can be swept in.
    ///
    /// <b>The five non-content fixture rows are out of scope and untouched.</b>
    /// The API/Infrastructure suites' smoke fixtures (<c>heal</c>, <c>shield</c>,
    /// <c>power_charge</c>, <c>thanh_xa_skill</c>, and any row another environment
    /// added) carry the literal JSON string <c>"effect"</c>; they are not this
    /// migration's content and are not among its keys (TASK-111 Reported
    /// Discrepancy 5), so no statement here reads or writes them. They keep their
    /// exact bytes, and reading one still fails loudly at the reader — the
    /// documented behaviour — rather than resolving to a fabricated effect.
    ///
    /// <b>Row content is transcribed, never computed or invented</b>
    /// (<c>DATABASE.md</c> §5 item 4 rule (b)). Every value below comes from
    /// <c>CARD_RULES.md</c> §2 (the three Basic Cards) or §4.1 (the three Pet Skill
    /// Cards), and each comment cites the sentence it came from. Nothing is
    /// computed, re-derived, balance-adjusted, borrowed from another document, or
    /// copied from TASK-108's / TASK-111's illustrative JSON (both of which
    /// <c>DATABASE.md</c> §1 item 4 records as examples only, not encoded values).
    ///
    /// <b>The three <c>Undetermined</c> markers are retired</b> (TASK-111 D-6b),
    /// because §4.1 now authors every magnitude (TASK-110). No content row is left
    /// in the unauthored state: <c>Undetermined</c> remains a valid contract member
    /// (<c>DATABASE.md</c> §1 item 9) but nothing provisioned needs it.
    ///
    /// <b><c>card-iron-fang</c>'s effect identity is corrected.</b> The row holds
    /// <c>effectType: "Power"</c>, which disagrees with §4.1 (Iron Fang deals
    /// damage and raises Crit chance). This is a <b>storage identity correction</b>
    /// required to conform the row to the already-authoritative contract
    /// (<c>DATABASE.md</c> §1 item 8; TASK-111 Reported Discrepancy 3) — it authors
    /// no new gameplay rule and changes no magnitude: <c>Power</c> keeps denoting
    /// Power Charge (TASK-111 D-4), so leaving it here would make a resolver select
    /// the wrong domain effect.
    ///
    /// <b>Element order is fixed by the transcription, and it is not semantic.</b>
    /// Each row's elements are written in the order <c>CARD_RULES.md</c> §2/§4.1
    /// states its effects. Per TASK-111 D-5 / <c>DATABASE.md</c> §1 item 3 that
    /// order is a storage sequence only: it authors no resolution step, no priority,
    /// and no ordering rule, and the reader preserves whatever order it finds rather
    /// than normalizing it.
    ///
    /// <b>Reversibility.</b> <see cref="Down(MigrationBuilder)"/> restores each of
    /// the six rows to the exact single-object payload TASK-109 wrote — the shape
    /// that migration's <c>Up</c> produced, which is what <c>Down</c> returns the
    /// column to. Each branch matches the row key <b>and</b> the array payload this
    /// migration wrote (<c>jsonb</c> equality is key-order- and
    /// whitespace-independent), so a row that does not hold it is left as it is
    /// rather than overwritten with a value it never had. <c>Up</c> → <c>Down</c>
    /// therefore returns the six rows to the pre-migration state, and
    /// <c>Up</c> again reproduces the identical array payloads.
    ///
    /// <b>Determinism and idempotency.</b> Every statement is a fixed <c>UPDATE</c>
    /// against a fixed primary key with a fixed new value — there is no ordering
    /// dependence, no RNG, no clock, no environment-derived value, and no data read
    /// from elsewhere. EF records the migration in <c>__EFMigrationsHistory</c>, so
    /// re-running <c>dotnet ef database update</c> applies nothing further and the
    /// six rows end in exactly one documented state. No row is inserted or deleted.
    /// </summary>
    public partial class EncodeCardEffectDefinitionArrayRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -------------------------------------------------------------------
            // The three Basic Cards — CARD_RULES.md §2.
            //
            // §2 gives each one effect, so per D-1b each row stores a ONE-ELEMENT
            // ARRAY holding the triple TASK-109 already landed for it. The effect
            // values are unchanged; only the enclosing shape becomes the contract's
            // array.
            //
            // `jsonb_build_array` wrapping `jsonb_build_object` is used rather than
            // a JSON literal so the encoded values stay ordinary SQL values with no
            // quoting to get wrong — the same reason TASK-109 used
            // `jsonb_build_object`.
            // -------------------------------------------------------------------

            // card-heal — CARD_RULES.md §2: "Restore the active Pet's HP by 20%
            // of its Max HP". A proportion of the active Pet's Max HP, so valueType
            // is PercentMaxHp and value is 20 (§2). Not pre-resolved against any
            // assumed Max HP: the Pet's MaxHP is battle state (GAME_STATE.md §2.3)
            // and is read when the effect is applied (DATABASE.md §1 item 1).
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Heal',
                           'valueType', 'PercentMaxHp',
                           'value', 20))
                 WHERE "CardDefinitionId" = 'card-heal';
                """);

            // card-shield — CARD_RULES.md §2: "Active Pet gains Shield equal to
            // 20% of its Max HP". A proportion, encoded as such; the Shield
            // CONTRACT is untouched (one instance per entity, refresh replaces the
            // magnitude, no additive stacking — COMBAT_RULES.md §4, TASK-105). No
            // Shield semantic is defined or changed here.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Shield',
                           'valueType', 'PercentMaxHp',
                           'value', 20))
                 WHERE "CardDefinitionId" = 'card-shield';
                """);

            // card-power-charge — CARD_RULES.md §2: "Active Pet gains 25 Power".
            // A plain amount, so valueType is Flat and value is 25 (§2). The Cost
            // is 0 by design (§2 item 3) and is NOT encoded here — it lives in the
            // untouched `PowerCost` column. `Power` keeps denoting Power Charge
            // (TASK-111 D-4), which is why this row keeps the `Power` identity.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Power',
                           'valueType', 'Flat',
                           'value', 25))
                 WHERE "CardDefinitionId" = 'card-power-charge';
                """);

            // -------------------------------------------------------------------
            // The three Pet Skill Cards — CARD_RULES.md §4.1 (TASK-110).
            //
            // Each states TWO effects, so per D-1 each row stores a TWO-ELEMENT
            // array — the shape the superseded single object could not express.
            // Every magnitude below is §4.1's authored value, transcribed verbatim;
            // the three `Undetermined` markers TASK-109 wrote are retired (D-6b).
            //
            // The extra members are §1 item 1's: `duration` on a Burn element
            // (Turns; its `value` is damage per tick) and `scope` on a Crit element
            // (`NextAttack`; its `value` is the increase in percentage points).
            // Both store rules the owning documents already state — the tick
            // schedule and unit by GAME_RULES.md §17 step 19a / COMBAT_RULES.md
            // §5.1–§5.2, the next-attack scope by CARD_RULES.md §4.1 — and neither
            // is executed anywhere by this migration.
            //
            // NOTHING IS APPLIED HERE. No Burn instance is created, no tick is
            // scheduled, no Crit roll exists, no damage is computed, and no Card is
            // cast: this writes definition data only (DATABASE.md §1 item 5;
            // TASK-112 Scope).
            // -------------------------------------------------------------------

            // card-inferno — CARD_RULES.md §4.1 (Xích Lang's Signature Skill):
            // "Deal 100 Fire (Hỏa) damage (flat base value, entering the Damage
            // Pipeline as the Card/Skill base value — COMBAT_RULES.md §3 step 1);
            // apply Burn", and "Burn: 50 damage per tick for 2 Turns".
            //   damage   → Damage, Flat, 100   (§4.1's stated base value; the
            //                                   Element/Fire half is the Pet's
            //                                   ELEMENT_RULES.md inheritance and is
            //                                   not a CardEffectDefinition member)
            //   burn     → Burn, Flat, 50, duration 2   (§4.1: 50 per tick, 2 Turns)
            // The order is §4.1's effect order, and per §1 item 3 it carries no
            // gameplay meaning.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Damage',
                           'valueType', 'Flat',
                           'value', 100),
                         jsonb_build_object(
                           'effectType', 'Burn',
                           'valueType', 'Flat',
                           'value', 50,
                           'duration', 2))
                 WHERE "CardDefinitionId" = 'card-inferno';
                """);

            // card-tidal-barrier — CARD_RULES.md §4.1 (Huyền Quy's Signature
            // Skill): "Heal the active Pet for 20% of its Max HP; the active Pet
            // gains Shield equal to 20% of its Max HP (refresh-not-stack —
            // COMBAT_RULES.md §4)".
            //   heal     → Heal,   PercentMaxHp, 20   (§4.1's stated 20%)
            //   shield   → Shield, PercentMaxHp, 20   (§4.1's stated 20%)
            // TASK-109 Stop Condition 2 recorded this row's Shield magnitude as an
            // unauthored content gap; TASK-110 §4.1 authored it, so the gap is
            // closed by transcription and no value is invented here. The
            // refresh-not-stack Shield rule is COMBAT_RULES.md §4's and is untouched
            // — this row states a magnitude, not a stacking behaviour.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Heal',
                           'valueType', 'PercentMaxHp',
                           'value', 20),
                         jsonb_build_object(
                           'effectType', 'Shield',
                           'valueType', 'PercentMaxHp',
                           'value', 20))
                 WHERE "CardDefinitionId" = 'card-tidal-barrier';
                """);

            // card-iron-fang — CARD_RULES.md §4.1 (Bạch Hổ's Signature Skill):
            // "Deal 120 damage (flat base value, entering the Damage Pipeline as
            // the Card/Skill base value — COMBAT_RULES.md §3 step 1); increase Crit
            // chance by 10 percentage points for the next attack only".
            //   damage   → Damage, Flat, 120   (§4.1's stated base value)
            //   crit     → Crit, PercentagePoints, 10, scope "NextAttack"
            //                                  (§4.1: 10 percentage points, next
            //                                   attack only)
            //
            // THE IDENTITY CORRECTION: this row previously held
            // effectType "Power" with an Undetermined magnitude, which disagrees
            // with §4.1 — Iron Fang raises Crit chance, it does not grant Power
            // Charge. DATABASE.md §1 item 8 records the correction as this encoding
            // task's act, and TASK-111 Reported Discrepancy 3 scopes it. It is a
            // STORAGE identity correction conforming the row to the already-authored
            // contract, not a new gameplay rule and not a balance change. Iron Fang's
            // Crit value is the Card's own and stays independent of Bạch Hổ's
            // Passive configuration value (CARD_RULES.md §4.1 closing note,
            // PASSIVE_RULES.md §7/§8) — no Passive config is read or written here.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Damage',
                           'valueType', 'Flat',
                           'value', 120),
                         jsonb_build_object(
                           'effectType', 'Crit',
                           'valueType', 'PercentagePoints',
                           'value', 10,
                           'scope', 'NextAttack'))
                 WHERE "CardDefinitionId" = 'card-iron-fang';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The mirror of Up: each of the six rows returns to the exact
            // single-object payload TASK-109's migration wrote. The shape restored
            // is the superseded one because that is the state this migration was
            // applied over — a reversal returns the data to where it came from, and
            // authors nothing.
            //
            // Each branch matches the row key AND the array payload this migration
            // wrote. `jsonb` equality is key-order- and whitespace-independent, so
            // the comparison is on the value rather than on its text — and a row
            // that does not hold this migration's payload (changed by hand, or
            // written differently in another environment) is therefore left
            // untouched instead of being overwritten with a value it never had.
            //
            // No row is deleted, no row is nulled, and no other row is matched: the
            // five non-content fixture rows are not named here and keep their bytes.
            // -------------------------------------------------------------------

            // card-heal — back to TASK-109's single-object Heal payload.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_object(
                         'effectType', 'Heal',
                         'valueType', 'PercentMaxHp',
                         'value', 20)
                 WHERE "CardDefinitionId" = 'card-heal'
                   AND "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Heal',
                           'valueType', 'PercentMaxHp',
                           'value', 20));
                """);

            // card-shield — back to TASK-109's single-object Shield payload.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_object(
                         'effectType', 'Shield',
                         'valueType', 'PercentMaxHp',
                         'value', 20)
                 WHERE "CardDefinitionId" = 'card-shield'
                   AND "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Shield',
                           'valueType', 'PercentMaxHp',
                           'value', 20));
                """);

            // card-power-charge — back to TASK-109's single-object Power payload.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_object(
                         'effectType', 'Power',
                         'valueType', 'Flat',
                         'value', 25)
                 WHERE "CardDefinitionId" = 'card-power-charge'
                   AND "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Power',
                           'valueType', 'Flat',
                           'value', 25));
                """);

            // card-inferno — back to TASK-109's single-object Undetermined Power
            // payload, the exact state that migration left.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_object(
                         'effectType', 'Power',
                         'valueType', 'Undetermined')
                 WHERE "CardDefinitionId" = 'card-inferno'
                   AND "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Damage',
                           'valueType', 'Flat',
                           'value', 100),
                         jsonb_build_object(
                           'effectType', 'Burn',
                           'valueType', 'Flat',
                           'value', 50,
                           'duration', 2));
                """);

            // card-tidal-barrier — back to TASK-109's single-object Undetermined
            // Shield payload. The reversal restores the pre-encoding state and
            // authors nothing: the magnitude exists in CARD_RULES.md §4.1 either
            // way, and the row simply stops carrying it.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_object(
                         'effectType', 'Shield',
                         'valueType', 'Undetermined')
                 WHERE "CardDefinitionId" = 'card-tidal-barrier'
                   AND "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Heal',
                           'valueType', 'PercentMaxHp',
                           'value', 20),
                         jsonb_build_object(
                           'effectType', 'Shield',
                           'valueType', 'PercentMaxHp',
                           'value', 20));
                """);

            // card-iron-fang — back to TASK-109's single-object Undetermined Power
            // payload, INCLUDING the identity this migration corrected. The
            // reversal is faithful to the pre-migration bytes; the corrected
            // identity is what a re-applied Up writes again, deterministically.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_object(
                         'effectType', 'Power',
                         'valueType', 'Undetermined')
                 WHERE "CardDefinitionId" = 'card-iron-fang'
                   AND "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Damage',
                           'valueType', 'Flat',
                           'value', 120),
                         jsonb_build_object(
                           'effectType', 'Crit',
                           'valueType', 'PercentagePoints',
                           'value', 10,
                           'scope', 'NextAttack'));
                """);
        }
    }
}
