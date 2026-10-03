using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Migrates <c>RelicDefinition.Condition</c> and
    /// <c>RelicDefinition.EffectDefinition</c> from TASK-082 R2-7's verbatim
    /// prose to the structured representation <c>RELIC_RULES.md</c> §8 defines:
    /// both columns become <c>jsonb</c>, and the four provisioned
    /// <c>RelicDefinition</c> rows are rewritten from their prose values to the
    /// structured forms (<c>DATABASE.md</c> §1's Relic block and its
    /// "Relic <c>EffectDefinition</c> and <c>Condition</c> contract" note;
    /// TASK-131 <b>D1</b>/<b>D5</b>/<b>D9</b>; <c>ADR-018</c> items 1–4 and 9;
    /// implemented by TASK-132).
    ///
    /// <b>Schema change — two columns, one table.</b> The former
    /// <c>character varying(128)</c> columns were sized for prose and are neither
    /// large enough nor the right type for the structured payload, which §8.6
    /// records as <b>insufficient</b> (TASK-131 D9). Both become <c>jsonb</c>;
    /// <c>EffectDefinition</c> stays NOT NULL and <c>Condition</c> stays NULLABLE
    /// (its documented optionality, §8.1 item 4). No other column, table,
    /// constraint, foreign key, or index is touched: in particular the
    /// <c>Relic</c> ownership table is not modified, and <c>Trigger</c> stays the
    /// bounded prose identity §3's closed list requires (TASK-131 D8 —
    /// §8.5 item 3 leaves §3 unchanged).
    ///
    /// <b>The relocation is not a rule change.</b> Zero gameplay values,
    /// magnitudes, thresholds, lifetimes, or targets are authored here. Every
    /// encoded value is transcribed from <c>RELIC_RULES.md</c> §8.5, which is
    /// itself transcribed from §6 and §8.1–§8.4; each comment below cites the
    /// table row it came from. Nothing is computed, re-derived, balance-adjusted,
    /// or borrowed (<c>DATABASE.md</c> §5 item 4 rule (b)).
    ///
    /// <b>Why the whole column is rewritten rather than a subset migrated.</b>
    /// Each column now stores one contract for every row, so the four rows the
    /// provisioning migration inserted
    /// (<c>20260929152651_ProvisionPetCardRelicContentDefinitions</c>) must hold
    /// the structured shape. The rewrite is restricted to those four
    /// <c>RelicDefinitionId</c> values, matched exactly — never a prefix, never a
    /// <c>LIKE</c> pattern, never by name or by current value — so no fifth row
    /// can be swept in. No row is inserted or deleted.
    ///
    /// <b><c>Burning Curse</c> is not provisioned and is not invented here.</b>
    /// §6 note 3 / §8.5 item 4 keep that row deferred: §3 requires exactly one
    /// primary Trigger from its closed list while §6 note 1 describes Burning
    /// Curse as a static modifier with none, and that tension is reported rather
    /// than resolved (<c>AGENTS.md</c> §4). This migration writes no placeholder
    /// <c>Trigger</c>, no <c>Condition</c>, and no effect for it, and inserts no
    /// row.
    ///
    /// <b>Nothing is executed.</b> No Relic trigger is evaluated, no condition is
    /// compared, no effect is applied, no <c>RelicTriggered</c> event is emitted,
    /// and no Card cost or ATK value is computed: §8.7 records every one of those
    /// as NOT IMPLEMENTED, and this is a DDL/DML migration over a content
    /// definition table (<c>GAME_RULES.md</c> §17 step 11 is untouched).
    ///
    /// <b>Reversibility.</b> <see cref="Down(MigrationBuilder)"/> reverses the
    /// column types first and then restores each of the four rows to the exact
    /// prose string the provisioning migration wrote, so <c>Up</c> → <c>Down</c>
    /// returns the schema and the four rows to the pre-migration state. Each
    /// content branch also matches the structured payload this migration wrote
    /// (<c>jsonb</c> equality is key-order- and whitespace-independent), so a row
    /// that does not hold it is left as it is rather than overwritten with a value
    /// it never had.
    ///
    /// <b>Determinism and idempotency.</b> Every statement is a fixed <c>ALTER
    /// TABLE</c> or a fixed <c>UPDATE</c> against a fixed primary key with a fixed
    /// new value — there is no ordering dependence, no RNG, no clock, no
    /// environment-derived value, and no data read from elsewhere
    /// (<c>TDD.md</c> §6). EF records the migration in
    /// <c>__EFMigrationsHistory</c>, so re-running <c>dotnet ef database
    /// update</c> applies nothing further and the four rows end in exactly one
    /// documented state.
    /// </summary>
    public partial class StructureRelicDefinitionStructuredColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -------------------------------------------------------------------
            // Content, BEFORE the ALTER.
            //
            // PostgreSQL has no assignment cast from `text`/`character varying`
            // to `jsonb`, so every row must already hold valid JSON when the
            // column type changes — the ALTERs below therefore run last. This is
            // the same ordering TASK-109's Card migration used, and for the same
            // reason.
            //
            // `jsonb_build_object` / `jsonb_build_array` are used rather than JSON
            // literals so the encoded values stay ordinary SQL values with no
            // quoting to get wrong.
            //
            // Every row below is transcribed from RELIC_RULES.md §8.5's
            // "Provisioned Relic Contract" table, which owns each value:
            //
            //   Relic            Trigger        Condition (§8.1)        EffectDefinition[] (§8.2–§8.4)
            //   Berserker Core   OnMatchCount   MatchCountAtLeast(3)    ATK, Percentage, 5, Pet, Battle
            //   Mana Crystal     OnMatchCount   MatchCountAtLeast(4)    Power, Flat, 10, Pet, Immediate
            //   Assassin Eye     OnCombo        ComboAtLeast(3)         Crit, PercentagePoints, 10, Pet, NextAttack
            //   Emergency Core   OnHpBelow      HpPercentageBelow(30)   CardCost, Percentage, 50, Pet, Battle
            //
            // §8.5 states that no value in that table is authored by it — all are
            // transcribed from §6 and §8.1–§8.4 — so nothing here is authored
            // either. Trigger is NOT written: it is already the §3 identity the
            // provisioning migration stored and TASK-131 D8 leaves §3 unchanged.
            // -------------------------------------------------------------------

            // relic-berserker-core — RELIC_RULES.md §8.5: Condition
            // `MatchCountAtLeast(3)`; effect `{ "effectType": "ATK",
            // "valueType": "Percentage", "value": 5, "target": "Pet",
            // "lifetime": "Battle" }`, from §6's "+5% ATK" row read through
            // §8.2 item 2 (a proportion of the stat's own value) and §8.3's
            // allowed combination for ATK.
            //
            // The `5` is §6's authored magnitude, transcribed. It is NOT
            // pre-resolved against any assumed ATK value: §8.2 item 2 states the
            // value it applies to is read from battle state when the effect is
            // applied, and no such reading happens here.
            //
            // `ATK`'s runtime carrier is deliberately NOT decided or created by
            // this migration: §8.2–§8.4 declare the content only, so this stores
            // the declaration and introduces no modifier collection, no
            // StatusEffect, and no temporary-ATK representation.
            migrationBuilder.Sql(
                """
                UPDATE "RelicDefinition"
                   SET "Condition" = jsonb_build_object(
                         'conditionType', 'MatchCountAtLeast',
                         'threshold', 3),
                       "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'ATK',
                           'valueType', 'Percentage',
                           'value', 5,
                           'target', 'Pet',
                           'lifetime', 'Battle'))
                 WHERE "RelicDefinitionId" = 'relic-berserker-core';
                """);

            // relic-mana-crystal — RELIC_RULES.md §8.5: Condition
            // `MatchCountAtLeast(4)`; effect `{ "effectType": "Power",
            // "valueType": "Flat", "value": 10, "target": "Pet",
            // "lifetime": "Immediate" }`, from §6's "+10 Power" row read through
            // §8.2 item 2 (an absolute amount) and §8.3's allowed combination for
            // Power.
            //
            // `Immediate` is §8.3 item 3's: applied once, at the moment it
            // triggers, leaving no standing modification behind — not a duration.
            // Storing it grants no Power and emits no power-change event.
            migrationBuilder.Sql(
                """
                UPDATE "RelicDefinition"
                   SET "Condition" = jsonb_build_object(
                         'conditionType', 'MatchCountAtLeast',
                         'threshold', 4),
                       "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Power',
                           'valueType', 'Flat',
                           'value', 10,
                           'target', 'Pet',
                           'lifetime', 'Immediate'))
                 WHERE "RelicDefinitionId" = 'relic-mana-crystal';
                """);

            // relic-assassin-eye — RELIC_RULES.md §8.5: Condition
            // `ComboAtLeast(3)`; effect `{ "effectType": "Crit",
            // "valueType": "PercentagePoints", "value": 10, "target": "Pet",
            // "lifetime": "NextAttack" }`, from §6's qualitative "Increased Crit
            // chance" row whose magnitude §8.5 item 1 authors as +10 percentage
            // points (TASK-131 D4).
            //
            // `PercentagePoints` is §8.2 item 2's percentage-point
            // interpretation, not a proportion of anything; §8.3 item 4's
            // `NextAttack` REUSES the existing Card Crit consumption boundary
            // (ADR-017) rather than introducing a second one. No Crit modifier is
            // created and no roll is performed here.
            migrationBuilder.Sql(
                """
                UPDATE "RelicDefinition"
                   SET "Condition" = jsonb_build_object(
                         'conditionType', 'ComboAtLeast',
                         'threshold', 3),
                       "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Crit',
                           'valueType', 'PercentagePoints',
                           'value', 10,
                           'target', 'Pet',
                           'lifetime', 'NextAttack'))
                 WHERE "RelicDefinitionId" = 'relic-assassin-eye';
                """);

            // relic-emergency-core — RELIC_RULES.md §8.5: Condition
            // `HpPercentageBelow(30)`; effect `{ "effectType": "CardCost",
            // "valueType": "Percentage", "value": 50, "target": "Pet",
            // "lifetime": "Battle" }`, from §6's "Heal Card cost −50%" row read
            // through §8.2 item 2 (a proportion of the stat's own value) and
            // §8.3's allowed combination for CardCost.
            //
            // The stored magnitude is `50` — the §6 row's reduction expressed as
            // the proportion §8.2 item 2 defines, which is what §8.5's table
            // records. Nothing here computes a Card cost, evaluates the HP
            // condition, or applies a modifier: §8.5 item 2 names where an
            // APPLIED modifier lives (GAME_STATE.md §2.3.5), how it mutates
            // (§5.1.3), and who owns the cost composition (CARD_RULES.md §3.6),
            // and none of that is a content member or this migration's act.
            migrationBuilder.Sql(
                """
                UPDATE "RelicDefinition"
                   SET "Condition" = jsonb_build_object(
                         'conditionType', 'HpPercentageBelow',
                         'threshold', 30),
                       "EffectDefinition" = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'CardCost',
                           'valueType', 'Percentage',
                           'value', 50,
                           'target', 'Pet',
                           'lifetime', 'Battle'))
                 WHERE "RelicDefinitionId" = 'relic-emergency-core';
                """);

            // -------------------------------------------------------------------
            // Schema: the two prose columns become the structured payload
            // columns.
            //
            // `jsonb` is the type the EF mapping declares and the type
            // DATABASE.md §1 records; `EffectDefinition` keeps NOT NULL and
            // `Condition` keeps its documented optionality (§8.1 item 4).
            //
            // The casts are written explicitly. PostgreSQL has no assignment
            // cast from `text`/`character varying` to `jsonb`, so EF's generated
            // `ALTER COLUMN ... TYPE jsonb` alone is rejected with SQLSTATE 42804
            // ("column ... cannot be cast automatically to type jsonb"). `USING`
            // states the conversion.
            //
            // -----------------------------------------------------------------
            // Rows OUTSIDE the four provisioned content rows.
            //
            // Both tables are shared: the API integration suites' smoke fixtures
            // also insert RelicDefinition rows, and those rows carry arbitrary
            // text (e.g. the literal 'seeded for the starter-ownership bootstrap
            // tests') that is not JSON. They are not this migration's content and
            // are NOT given a structured condition or effect — that would invent
            // definition data for a row this task does not own (AGENTS.md §7).
            //
            // They must still satisfy the columns' new type, and the only
            // faithful conversion of a row that is not structured data is to keep
            // its text as-is. `CASE` therefore tests the value: a row that
            // already parses as JSON (every row the UPDATEs above wrote, on this
            // path) is taken as JSON verbatim, and any other row's text is
            // preserved as a JSON *string*. Nothing is dropped, defaulted,
            // nulled, or deleted, and no effect identity or magnitude is supplied
            // for a row this contract does not define.
            //
            // A preserved-as-string row is exactly the "authored as prose, not
            // yet structured" case: reading it through the Domain reader fails
            // loudly rather than resolving to a fabricated Relic effect, which is
            // the documented behaviour (§8.2 item 5) rather than a silent no-op.
            //
            // `Condition` is NULLABLE, so a NULL must remain NULL: `NULL ~ '...'`
            // is NULL rather than false, and the `IS NULL` branch keeps that
            // case explicit instead of letting the CASE fall through to
            // `to_jsonb(NULL)` and turn an absent condition into JSON null.
            // -----------------------------------------------------------------
            migrationBuilder.Sql(
                """
                ALTER TABLE "RelicDefinition"
                  ALTER COLUMN "Condition" TYPE jsonb
                  USING (
                    CASE
                      WHEN "Condition" IS NULL THEN NULL
                      WHEN "Condition" ~ '^\s*[\{\[]' THEN "Condition"::jsonb
                      ELSE to_jsonb("Condition")
                    END);
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "RelicDefinition"
                  ALTER COLUMN "EffectDefinition" TYPE jsonb
                  USING (
                    CASE
                      WHEN "EffectDefinition" ~ '^\s*[\{\[]' THEN "EffectDefinition"::jsonb
                      ELSE to_jsonb("EffectDefinition")
                    END);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The mirror of Up, in reverse order: both columns return to their
            // bounded prose type first, then each content row is restored to the
            // exact RELIC_RULES.md §6 rule text the provisioning migration wrote.
            //
            // The casts are again explicit, and for the same reason as in Up:
            // converting `jsonb` back to a bounded `character varying` is not an
            // assignment cast either. `#>> '{}'` renders a value as its unquoted
            // text, so a JSON string yields exactly the prose it holds. A JSON
            // object (a structured value this migration wrote) renders as its JSON
            // text — which the UPDATEs below then replace with the documented rule
            // text, so the final state is prose either way.
            //
            // The reversal restores the superseded prose because that is the state
            // this migration was applied over: a reversal returns the data to
            // where it came from and authors nothing. The prose strings are
            // RELIC_RULES.md §6's rule text, the same values the provisioning
            // migration inserted.
            //
            // A row whose Condition is NULL stays NULL after the cast (`#>> '{}'`
            // on SQL NULL is NULL), so the documented absent-condition case
            // survives the reversal.
            //
            // Each content branch also matches the structured payload this
            // migration wrote, so a row that does not hold it (changed by hand, or
            // written differently in another environment) is left untouched
            // instead of being overwritten with a value it never had.
            // -----------------------------------------------------------------
            migrationBuilder.Sql(
                """
                ALTER TABLE "RelicDefinition"
                  ALTER COLUMN "Condition" TYPE character varying(128)
                  USING "Condition" #>> '{}';
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "RelicDefinition"
                  ALTER COLUMN "EffectDefinition" TYPE character varying(128)
                  USING "EffectDefinition" #>> '{}';
                """);

            // relic-berserker-core — back to RELIC_RULES.md §6's prose:
            // "every 3 Matches" / "+5% ATK".
            migrationBuilder.Sql(
                """
                UPDATE "RelicDefinition"
                   SET "Condition" = 'every 3 Matches',
                       "EffectDefinition" = '+5% ATK'
                 WHERE "RelicDefinitionId" = 'relic-berserker-core'
                   AND "EffectDefinition" ~ '^\s*\['
                   AND "EffectDefinition"::jsonb = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'ATK',
                           'valueType', 'Percentage',
                           'value', 5,
                           'target', 'Pet',
                           'lifetime', 'Battle'));
                """);

            // relic-mana-crystal — back to RELIC_RULES.md §6's prose:
            // "every 4 Matches" / "+10 Power".
            migrationBuilder.Sql(
                """
                UPDATE "RelicDefinition"
                   SET "Condition" = 'every 4 Matches',
                       "EffectDefinition" = '+10 Power'
                 WHERE "RelicDefinitionId" = 'relic-mana-crystal'
                   AND "EffectDefinition" ~ '^\s*\['
                   AND "EffectDefinition"::jsonb = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Power',
                           'valueType', 'Flat',
                           'value', 10,
                           'target', 'Pet',
                           'lifetime', 'Immediate'));
                """);

            // relic-assassin-eye — back to RELIC_RULES.md §6's prose, including
            // its documented "≥" character: "Combo ≥ 3" / "Increased Crit chance".
            migrationBuilder.Sql(
                """
                UPDATE "RelicDefinition"
                   SET "Condition" = 'Combo ≥ 3',
                       "EffectDefinition" = 'Increased Crit chance'
                 WHERE "RelicDefinitionId" = 'relic-assassin-eye'
                   AND "EffectDefinition" ~ '^\s*\['
                   AND "EffectDefinition"::jsonb = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'Crit',
                           'valueType', 'PercentagePoints',
                           'value', 10,
                           'target', 'Pet',
                           'lifetime', 'NextAttack'));
                """);

            // relic-emergency-core — back to RELIC_RULES.md §6's prose, including
            // its documented "−" character: "HP < 30%" / "Heal Card cost −50%".
            migrationBuilder.Sql(
                """
                UPDATE "RelicDefinition"
                   SET "Condition" = 'HP < 30%',
                       "EffectDefinition" = 'Heal Card cost −50%'
                 WHERE "RelicDefinitionId" = 'relic-emergency-core'
                   AND "EffectDefinition" ~ '^\s*\['
                   AND "EffectDefinition"::jsonb = jsonb_build_array(
                         jsonb_build_object(
                           'effectType', 'CardCost',
                           'valueType', 'Percentage',
                           'value', 50,
                           'target', 'Pet',
                           'lifetime', 'Battle'));
                """);
        }
    }
}
