using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Migrates <c>CardDefinition.EffectDefinition</c> from TASK-082 R2-7's
    /// verbatim prose to TASK-108's structured effect contract: the column
    /// becomes <c>jsonb</c>, and the six provisioned <c>CardDefinition</c> rows
    /// are rewritten from their prose values to the structured object
    /// (<c>DATABASE.md</c> §1; TASK-108 D-1/D-2; TASK-109).
    ///
    /// <b>Schema change — one column, one table.</b> The former
    /// <c>character varying(128)</c> was sized for prose and is neither large
    /// enough nor the right type for the structured payload, so the column is
    /// altered to <c>jsonb</c> (NOT NULL, unchanged). No other column, table,
    /// constraint, foreign key, or index is touched, and
    /// <b><c>RelicDefinition.EffectDefinition</c> is deliberately untouched</b>:
    /// TASK-082 R2-7 remains in force for Relics (ROADMAP.md Phase 2; no
    /// decision, no consumer), so the Relic half of the contract keeps its
    /// <c>character varying(128)</c> prose column and its
    /// <c>RelicDefinitionConfiguration</c> mapping exactly as they are.
    ///
    /// <b>Why the whole column is rewritten rather than a subset migrated.</b>
    /// The column now stores one contract for every row, so every row must hold
    /// a structured payload. The rewrite is restricted to the six
    /// <c>CardDefinitionId</c> values the provisioning migration inserted
    /// (<c>20260929152651_ProvisionPetCardRelicContentDefinitions</c>), matched
    /// exactly — not by prefix — so a row any other task or environment added is
    /// left alone. Rows outside that set are not touched by the
    /// <c>UPDATE</c>s and are not deleted.
    ///
    /// <b>Row content is transcribed, never computed or invented</b>
    /// (<c>DATABASE.md</c> §5 item 4 rule (b)). Each row's effect identity and
    /// value come from <c>CARD_RULES.md</c> §2 for the three Basic Cards and
    /// §4.1 for the three Pet Skill Cards, and each comment below cites the
    /// section it came from. <b>TASK-108's illustrative JSON values are NOT
    /// used</b>: its Heal example says 30 where §2 says 20 and its Power example
    /// says 5 where §2 says 25, so transcribing them would be a balance change.
    ///
    /// <b>The Pet Skill rows are migrated for schema consistency only.</b> They
    /// are not resolvable by any runtime in this task (PetSkillCast is out of
    /// scope), but the column now carries structured data and these three rows
    /// are among the six the provisioning migration wrote. Each records the
    /// effect identity <c>CARD_RULES.md</c> §4.1 names and an
    /// <b>Undetermined</b> magnitude, because §4.1 states no magnitude for any
    /// of them:
    /// <list type="bullet">
    /// <item><c>Inferno</c> and <c>Iron Fang</c> deal damage (and apply Burn /
    /// raise Crit chance). §4.1 states no magnitude for either ("high Fire
    /// damage", "High damage"), so the magnitude is recorded as undetermined
    /// rather than invented.</item>
    /// <item><c>Tidal Barrier</c> is "Heal; Gain Shield" with <b>no Shield
    /// magnitude</b>. The magnitude is an unauthored content gap
    /// (<c>CARD_RULES.md</c> §4.1; TASK-104 §5 / B-4; TASK-108 D-4) and is
    /// recorded as undetermined. No value is invented — see the row comment,
    /// which handles TASK-109 Stop Condition 2 without resolving it.</item>
    /// </list>
    ///
    /// <b>Nothing is invented, and the rows stay readable.</b> The undetermined
    /// form states which effect applies and that its magnitude is not yet
    /// authored. It carries no number — not <c>0</c>, not a placeholder, not a
    /// borrowed value — so no balance figure is created and nothing can be
    /// mistaken for one. Because it is a well-formed structured payload, a Card
    /// Definition read does not fail on these rows, and no runtime ever has to
    /// parse their prose (which D-1 forbids). The authored rule text remains in
    /// <c>CARD_RULES.md</c> §4.1, which stays its owner; it is deliberately not
    /// restated here as a second source of the same rule.
    ///
    /// <b>The Basic Card rows become fully resolvable structured data.</b> They
    /// are the three §2 effects that <b>are</b> completely authored, and they
    /// are what TASK-107 needs.
    ///
    /// <b>Reversibility.</b> <see cref="Down(MigrationBuilder)"/> restores the
    /// prose column and rewrites every affected row back to the exact prose
    /// string the provisioning migration wrote — the <c>CARD_RULES.md</c> §2/
    /// §4.1 rule text — and widens the column back to
    /// <c>character varying(128)</c>. Each structured branch matches the payload
    /// this migration wrote as well as the row key, so a row that does not hold
    /// it is left as it is rather than overwritten with a value it never had.
    /// The prose values are the same ones the provisioning migration inserted, so
    /// <c>Up</c> → <c>Down</c> returns the schema and the six provisioned rows
    /// to the pre-migration state.
    ///
    /// <b>Determinism and idempotency.</b> Every statement is a fixed <c>ALTER
    /// TABLE</c> or a fixed <c>UPDATE</c> against a fixed primary key with a
    /// fixed new value — there is no ordering dependence, no RNG, no
    /// environment-derived value, and no data read from elsewhere. EF records
    /// the migration in <c>__EFMigrationsHistory</c>, so re-running <c>dotnet ef
    /// database update</c> applies nothing further and the six rows end in
    /// exactly one documented state. Provisioning never inserts or deletes a
    /// row here.
    /// </summary>
    public partial class StructureCardDefinitionEffectDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------
            // Schema: the prose column becomes the structured payload column.
            //
            // `jsonb` (NOT NULL unchanged) is the type the EF mapping declares
            // and the type DATABASE.md §1 records. PostgreSQL casts the existing
            // `character varying` text to `jsonb`, which is why the stored
            // values must already be valid JSON objects before the ALTER — the
            // UPDATEs below run first for exactly that reason.
            // ---------------------------------------------------------------

            // ---------------------------------------------------------------
            // Content: CardDefinition rows, transcribed from CARD_RULES.md.
            //
            // Runs BEFORE the ALTER so every row already holds a JSON object
            // when the column type changes; a prose row would make the cast
            // fail. `jsonb_build_object` is used rather than a JSON literal so
            // the encoded values are ordinary SQL values with no quoting to get
            // wrong.
            //
            // The three Basic Cards — CARD_RULES.md §2, the only effects §2
            // fully authors. Each structured value is transcribed verbatim:
            //   Heal          "Restore the active Pet's HP by 20% of its Max HP"
            //                 → Heal,   PercentMaxHp, 20   (§2)
            //   Shield        "Active Pet gains Shield equal to 20% of its Max HP"
            //                 → Shield, PercentMaxHp, 20   (§2)
            //   Power Charge  "Active Pet gains 25 Power"
            //                 → Power,  Flat,         25   (§2)
            //
            // Power Charge's structured `value` is its EFFECT magnitude (25
            // Power), never its Cost — its Cost is 0 by design (§2 item 3) and
            // lives in the untouched `PowerCost` column.
            // ---------------------------------------------------------------

            // card-heal — CARD_RULES.md §2: "Restore the active Pet's HP by 20%
            // of its Max HP". A proportion of the active Pet's Max HP, so
            // valueType is PercentMaxHp and value is 20 (§2). Not pre-resolved
            // against any assumed Max HP: the Pet's MaxHP is battle state
            // (GAME_STATE.md §2.3) and is read at cast time by TASK-107.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_object(
                           'effectType', 'Heal',
                           'valueType', 'PercentMaxHp',
                           'value', 20)
                 WHERE "CardDefinitionId" = 'card-heal';
                """);

            // card-shield — CARD_RULES.md §2: "Active Pet gains Shield equal to
            // 20% of its Max HP". A proportion, encoded as such; the Shield
            // CONTRACT is untouched (one instance per entity, refresh replaces
            // the magnitude, no additive stacking — COMBAT_RULES.md §4,
            // TASK-105). No Shield semantic is defined or changed here.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_object(
                           'effectType', 'Shield',
                           'valueType', 'PercentMaxHp',
                           'value', 20)
                 WHERE "CardDefinitionId" = 'card-shield';
                """);

            // card-power-charge — CARD_RULES.md §2: "Active Pet gains 25
            // Power". A plain amount, so valueType is Flat and value is 25
            // (§2). The Cost is 0 (§2 item 3) and is NOT encoded here.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_object(
                           'effectType', 'Power',
                           'valueType', 'Flat',
                           'value', 25)
                 WHERE "CardDefinitionId" = 'card-power-charge';
                """);

            // ---------------------------------------------------------------
            // The three Pet Skill rows (CARD_RULES.md §4.1).
            //
            // These are migrated for SCHEMA CONSISTENCY only (TASK-109 Scope):
            // the column now carries structured data for every row, so these
            // rows must hold a structured payload too. Each encodes EXACTLY the
            // effect identity CARD_RULES.md §4.1 names, with the magnitude
            // recorded as UNDETERMINED.
            //
            // WHY UNDETERMINED AND NOT A NUMBER. §4.1 states no magnitude for
            // any of the three: Inferno is "high Fire (Hỏa) damage; apply Burn",
            // Iron Fang is "High damage; increased Crit chance", and Tidal
            // Barrier is "Heal; Gain Shield" with NO Shield magnitude at all.
            // That last one is an unauthored content gap explicitly recorded by
            // TASK-104 §5 / B-4 and re-confirmed by TASK-108 D-4, and TASK-109
            // Stop Condition 2 forbids resolving it by inventing a percentage, an
            // HP value, a reuse of the Shield Basic Card's §2 value, a
            // balance-derived figure, or a placeholder.
            //
            // `valueType: "Undetermined"` with no `value` member is the one
            // representation that is faithful to the documents AND valid under
            // this contract: it states which effect the Card applies, and
            // records that its magnitude is not yet authored rather than
            // supplying one. It carries no number, so nothing here can be
            // mistaken for a balance value, and it is never 0 or a sentinel.
            // A resolver must treat it as an unauthored content gap;
            // `HasValue` is false and the reader rejects any attempt to pair
            // it with a magnitude.
            //
            // It is also what makes the rows READABLE: no Card Definition read
            // fails because of them, and no prose has to be parsed at runtime
            // (which D-1 forbids). Their authored rule text remains in
            // CARD_RULES.md §4.1, which stays the owner of it — it is not
            // restated here as a second source of the same rule.
            //
            // The effect identities are §2's domain effects, because those are
            // the only effects this contract defines and §4.1's wording names
            // the same ones for Tidal Barrier (Heal; Shield). PetSkill execution
            // is out of TASK-109's scope entirely, and PetSkillCast remains
            // blocked until the magnitude decision exists.
            // ---------------------------------------------------------------

            // card-inferno — CARD_RULES.md §4.1 (Xích Lang's Signature Skill):
            // "Deal high Fire (Hỏa) damage; apply Burn". No magnitude is stated.
            // Its damage and Burn belong to the Damage Pipeline
            // (COMBAT_RULES.md §3) and are NOT a CardEffectType this contract
            // defines, so no identity is invented for them either.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_object(
                           'effectType', 'Power',
                           'valueType', 'Undetermined')
                 WHERE "CardDefinitionId" = 'card-inferno';
                """);

            // card-tidal-barrier — CARD_RULES.md §4.1 (Huyền Quy's Signature
            // Skill): "Heal; Gain Shield". Its Shield magnitude is UNAUTHORED
            // (TASK-104 §5 / B-4; TASK-108 D-4): no percentage, no HP value, no
            // reuse of the Shield Basic Card's §2 value, no balance inference,
            // no placeholder — this row TRIGGERS TASK-109 STOP CONDITION 2,
            // which is handled by recording the magnitude as Undetermined and
            // reporting it, not by resolving it. PetSkillCast remains blocked on
            // the same gap.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_object(
                           'effectType', 'Shield',
                           'valueType', 'Undetermined')
                 WHERE "CardDefinitionId" = 'card-tidal-barrier';
                """);

            // card-iron-fang — CARD_RULES.md §4.1 (Bạch Hổ's Signature Skill):
            // "High damage; increased Crit chance". No magnitude is stated and
            // its damage/Crit are not a CardEffectType this contract defines, so
            // no identity and no magnitude are invented.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = jsonb_build_object(
                           'effectType', 'Power',
                           'valueType', 'Undetermined')
                 WHERE "CardDefinitionId" = 'card-iron-fang';
                """);

            // Every provisioned row now holds a structured object. The column
            // type change follows, so the text → jsonb cast cannot encounter one
            // of those prose rows.
            //
            // The cast is written explicitly. PostgreSQL has no assignment cast
            // from `text`/`character varying` to `jsonb`, so EF's generated
            // `ALTER COLUMN … TYPE jsonb` alone is rejected with SQLSTATE 42804
            // ("column \"EffectDefinition\" cannot be cast automatically to type
            // jsonb"). `USING` states the conversion.
            //
            // -----------------------------------------------------------------
            // Rows OUTSIDE the six provisioned content rows.
            //
            // This table is shared: the API integration suites' smoke fixtures
            // also insert CardDefinition rows, and those rows carry arbitrary
            // text (e.g. the literal 'effect') that is not JSON. They are not
            // this migration's content and are NOT given a structured effect —
            // that would invent definition data for a row this task does not own
            // (AGENTS.md §7).
            //
            // They must still satisfy the column's new type, and the only
            // faithful conversion of a row that is not structured effect data is
            // to keep its text as-is. `CASE` therefore tests the value: a row
            // that already parses as JSON (every row the UPDATEs above wrote, on
            // this path) is taken as JSON verbatim, and any other row's text is
            // preserved as a JSON *string*. Nothing is dropped, defaulted,
            // nulled, or deleted, and no effect identity or magnitude is
            // supplied for a row this contract does not define.
            //
            // A preserved-as-string row is exactly the "authored as rule text,
            // not yet structured" case: reading it through
            // CardEffectDefinition.FromPersistedPayload fails loudly rather than
            // resolving to a fabricated effect, which is the documented
            // behaviour rather than a silent no-op.
            // -----------------------------------------------------------------
            migrationBuilder.Sql(
                """
                ALTER TABLE "CardDefinition"
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
            // The mirror of Up, in reverse order: the column returns to its
            // prose type first, then each migrated row is restored to the exact
            // CARD_RULES.md §2/§4.1 rule text the provisioning migration wrote.
            //
            // The cast is again explicit, and for the same reason as in Up:
            // converting `jsonb` back to a bounded `character varying` is not an
            // assignment cast either. `#>> '{}'` renders a value as its
            // unquoted text, so a JSON string yields exactly the prose it holds.
            // A JSON object (a structured effect this migration wrote) renders
            // as its JSON text — which the UPDATEs below then replace with the
            // documented rule text, so the final state is prose either way.
            migrationBuilder.Sql(
                """
                ALTER TABLE "CardDefinition"
                  ALTER COLUMN "EffectDefinition" TYPE character varying(128)
                  USING "EffectDefinition" #>> '{}';
                """);

            // ---------------------------------------------------------------
            // The three Basic Cards — restored to CARD_RULES.md §2's prose,
            // the value the provisioning migration stored. Each branch matches
            // the row's structured payload as well as its key, so a row that
            // does not hold the structured effect this migration wrote (for
            // example one changed by hand, or one another environment authored
            // differently) is left untouched instead of being overwritten with
            // a value it never had.
            //
            // The column is `character varying` again at this point, so the
            // recorded payload is compared by parsing it back with `::jsonb` —
            // `jsonb` equality is key-order- and whitespace-independent, which
            // an exact text comparison would not be. The `~ '^\s*\{'` guard
            // keeps the parse away from a row whose text is not JSON (a
            // preserved-rule-text or fixture row), so such a row simply does
            // not match. This is a data-shape test, not definition logic.
            // ---------------------------------------------------------------

            // card-heal — back to CARD_RULES.md §2's Heal rule text.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = 'Restore the active Pet''s HP by 20% of its Max HP'
                 WHERE "CardDefinitionId" = 'card-heal'
                   AND "EffectDefinition" ~ '^\s*\{'
                   AND "EffectDefinition"::jsonb = jsonb_build_object(
                           'effectType', 'Heal',
                           'valueType', 'PercentMaxHp',
                           'value', 20);
                """);

            // card-shield — back to CARD_RULES.md §2's Shield rule text.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = 'Active Pet gains Shield equal to 20% of its Max HP'
                 WHERE "CardDefinitionId" = 'card-shield'
                   AND "EffectDefinition" ~ '^\s*\{'
                   AND "EffectDefinition"::jsonb = jsonb_build_object(
                           'effectType', 'Shield',
                           'valueType', 'PercentMaxHp',
                           'value', 20);
                """);

            // card-power-charge — back to CARD_RULES.md §2's Power Charge rule
            // text.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = 'Active Pet gains 25 Power'
                 WHERE "CardDefinitionId" = 'card-power-charge'
                   AND "EffectDefinition" ~ '^\s*\{'
                   AND "EffectDefinition"::jsonb = jsonb_build_object(
                           'effectType', 'Power',
                           'valueType', 'Flat',
                           'value', 25);
                """);

            // ---------------------------------------------------------------
            // The three Pet Skill rows — restored to CARD_RULES.md §4.1's
            // prose, the value the provisioning migration stored. Their
            // structured form records an Undetermined magnitude, so the branch
            // matches that marker and restores the documented rule text.
            // ---------------------------------------------------------------

            // card-inferno — back to CARD_RULES.md §4.1's Inferno rule text.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = 'Deal high Fire (Hỏa) damage; apply Burn'
                 WHERE "CardDefinitionId" = 'card-inferno'
                   AND "EffectDefinition" ~ '^\s*\{'
                   AND "EffectDefinition"::jsonb = jsonb_build_object(
                           'effectType', 'Power',
                           'valueType', 'Undetermined');
                """);

            // card-tidal-barrier — back to CARD_RULES.md §4.1's Tidal Barrier
            // rule text. Still no Shield magnitude: the reversal restores the
            // authored rule text and authors nothing.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = 'Heal; Gain Shield'
                 WHERE "CardDefinitionId" = 'card-tidal-barrier'
                   AND "EffectDefinition" ~ '^\s*\{'
                   AND "EffectDefinition"::jsonb = jsonb_build_object(
                           'effectType', 'Shield',
                           'valueType', 'Undetermined');
                """);

            // card-iron-fang — back to CARD_RULES.md §4.1's Iron Fang rule text.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinition"
                   SET "EffectDefinition" = 'High damage; increased Crit chance'
                 WHERE "CardDefinitionId" = 'card-iron-fang'
                   AND "EffectDefinition" ~ '^\s*\{'
                   AND "EffectDefinition"::jsonb = jsonb_build_object(
                           'effectType', 'Power',
                           'valueType', 'Undetermined');
                """);
        }
    }
}