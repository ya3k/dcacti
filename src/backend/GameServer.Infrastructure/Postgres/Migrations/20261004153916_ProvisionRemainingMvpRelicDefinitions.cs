using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Provisions the six remaining content-defined MVP <c>RelicDefinition</c>
    /// rows: <c>relic-burning-curse</c> (Burning Curse), <c>relic-combo-fang</c>
    /// (Combo Fang), <c>relic-arcane-battery</c> (Arcane Battery),
    /// <c>relic-execution-mark</c> (Execution Mark), <c>relic-cascade-core</c>
    /// (Cascade Core), and <c>relic-battle-instinct</c> (Battle Instinct) —
    /// <c>DATABASE.md</c> §1's Relic block, §5 item 4; <c>RELIC_RULES.md</c>
    /// §6 and §8.5.
    ///
    /// <b>Why these six rows are provisionable now.</b> Migration
    /// <c>20260929152651_ProvisionPetCardRelicContentDefinitions</c> provisioned
    /// the four Relics content-defined at that time (Berserker Core, Mana
    /// Crystal, Assassin Eye, Emergency Core), and
    /// <c>20261003074309_StructureRelicDefinitionStructuredColumns</c>
    /// re-encoded exactly those four into the structured shape §8 defines.
    /// <c>RELIC_RULES.md</c> §6 note 3 / §8.5 item 4 then recorded the remaining
    /// six as <b>content-defined and ready for provisioning in a downstream
    /// migration task</b> — TASK-176 authored the full 10-Relic content contract
    /// and TASK-178 canonicalized its runtime blockers, so none of the six is
    /// content-blocked. <c>DATABASE.md</c> §5 item 4 rule (a) therefore permits
    /// them, and this is that separate provisioning task.
    ///
    /// <b>Data only — no schema operation.</b> The table, its primary key, and
    /// every column already exist
    /// (<c>20260925134303_AddRelicPersistence</c>,
    /// <c>20261003074309_StructureRelicDefinitionStructuredColumns</c>), which
    /// stay untouched. This migration adds no column, table, index, or
    /// constraint: its <see cref="Up(MigrationBuilder)"/> contains exactly six
    /// <c>InsertData</c> calls and its <see cref="Down(MigrationBuilder)"/> the
    /// mirror six <c>DeleteData</c> calls. EF generated no schema difference for
    /// it — it scaffolded an empty Up/Down and
    /// <c>GameDbContextModelSnapshot.cs</c> is unchanged — which is what makes it
    /// a pure data migration.
    ///
    /// <b>Migration-level <c>InsertData</c> is the decided mechanism</b>
    /// (<c>DATABASE.md</c> §5 item 4; the TASK-052/TASK-053
    /// <c>ProvisionBossDefinitions</c>, TASK-085
    /// <c>ProvisionPetCardRelicContentDefinitions</c>, and TASK-168
    /// <c>ProvisionThanhXaAndSonHungSignatureSkills</c> precedents).
    /// <c>HasData</c>/model seed data, a startup loader, a JSON content
    /// pipeline, an external content service, an API-based provisioning path,
    /// and <c>INSERT ... ON CONFLICT</c>/upsert are all forbidden and are not
    /// used. Idempotency comes solely from EF's migration history: the migration
    /// runs once per database, so re-running <c>dotnet ef database update</c>
    /// inserts nothing further, and <c>RelicDefinitionId</c> (PK) is the
    /// database-level guarantee against duplicates.
    ///
    /// <b>Row set — exactly these six, and no other row.</b> The four rows the
    /// TASK-085 migration provisioned (and TASK-132 re-encoded) are <b>not</b>
    /// touched, re-inserted, updated, or deleted: they keep both their identity
    /// and their structured values (<c>AGENTS.md</c> §16). No placeholder row is
    /// created, and no seventh Relic is invented — the MVP set is closed at ten
    /// by <c>RELIC_RULES.md</c> §6.
    ///
    /// <b>Row content is transcribed, never computed or invented</b>
    /// (<c>DATABASE.md</c> §5 item 4 rule (b)), and every column is drawn from
    /// an existing closed vocabulary. Per column:
    /// <list type="bullet">
    /// <item><c>RelicDefinitionId</c> — <c>DATABASE.md</c> §1's value form
    /// <c>relic-&lt;ascii-kebab-case-name&gt;</c> of the Relic's documented name
    /// (TASK-082 decision B / R2-9), e.g. <c>relic-combo-fang</c> for
    /// <b>Combo Fang</b>.</item>
    /// <item><c>Name</c> — the Relic's display name from <c>RELIC_RULES.md</c>
    /// §6's MVP Relic Reference row.</item>
    /// <item><c>Trigger</c> — the single primary Trigger <c>RELIC_RULES.md</c>
    /// §8.5 records for the Relic, drawn from §3's <b>closed list</b>. §8.5
    /// item 3 (TASK-131 D8) leaves §3 unchanged, so this stays a prose identity
    /// and is encoded as the member name — never an ordinal.</item>
    /// <item><c>Condition</c> — the structured form §8.1 defines
    /// (<c>conditionType</c> + <c>threshold</c>), or SQL <c>NULL</c> where §8.5
    /// records <c>null</c>. §8.1 item 4 makes the member optional and states
    /// that a Relic whose Trigger alone is its complete condition carries none,
    /// so the absent case is <c>NULL</c> — never a sentinel condition that would
    /// read as a real one.</item>
    /// <item><c>EffectDefinition</c> — the structured <c>EffectDefinition[]</c>
    /// of §8.2–§8.4, stored as <c>jsonb</c>. Each element carries its
    /// <c>effectType</c>/<c>valueType</c>/<c>value</c> triple plus
    /// <c>target</c>/<c>lifetime</c>. Every <c>effectType</c> is a §8.2 item 1
    /// member (<c>ATK | Power | Crit | CardCost | BurnDamage</c>), every
    /// <c>valueType</c> is a §8.2 item 2 member, <c>target</c> is §8.3 item 1's
    /// only value (<c>Pet</c>), and every <c>lifetime</c> is §8.3 item 2's — so
    /// each row is one of the combinations §8.3's table lists. §8.3's closing
    /// rule ("a combination not listed is not defined and may not be inferred")
    /// is therefore satisfied, and no member beyond <c>target</c> and
    /// <c>lifetime</c> is written (§8.3 item 5).</item>
    /// </list>
    ///
    /// <b>Encoder note.</b> These six rows are born structured, so unlike
    /// TASK-132 this migration writes no prose and performs no
    /// representation relocation — the <c>jsonb</c> columns already exist and
    /// are written directly through EF's <c>InsertData</c>, exactly as
    /// <c>DATABASE.md</c> §1 declares them. Each payload is the array shape
    /// §8.2 defines, and each one-element array holds the single effect §8.5
    /// gives that Relic.
    ///
    /// <b>Nothing here executes.</b> Storing a Trigger, Condition, or effect
    /// executes none of them: no Relic trigger is evaluated, no condition is
    /// compared, no effect is applied, no <c>RelicTriggered</c> event is
    /// emitted, and no Power, Crit, ATK, or Burn value is computed. The Relic
    /// resolution stage is <c>GAME_RULES.md</c> §17 step 11's and already exists;
    /// this migration supplies definition data for it and moves no runtime rule
    /// into provisioning.
    ///
    /// <b>Reversibility.</b> <see cref="Down(MigrationBuilder)"/> removes exactly
    /// the six rows this migration inserted, keyed by the same canonical
    /// primary-key values, and no other row (no broad table delete). The four
    /// pre-existing rows survive a <c>Up</c> → <c>Down</c> round trip untouched.
    ///
    /// <b>Determinism.</b> Every statement is a fixed <c>INSERT</c> of fixed
    /// values against a fixed primary key — there is no ordering dependence, no
    /// RNG, no clock, no environment-derived value, and no data read from
    /// elsewhere (<c>TDD.md</c> §6). EF records the migration in
    /// <c>__EFMigrationsHistory</c>, so re-running <c>dotnet ef database
    /// update</c> applies nothing further and the table ends with exactly ten
    /// canonical MVP Relic rows.
    /// </summary>
    public partial class ProvisionRemainingMvpRelicDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------
            // RelicDefinition — DATABASE.md §1; RELIC_RULES.md §1, §3, §6, §8.5.
            //
            // Relics have no foreign key and reference no other provisioned
            // table, so insertion order among them is not constrained and is
            // taken here in RELIC_RULES.md §6's own row order (the same order
            // §8.5's Canonical Relic Contract table uses).
            //
            // Every row below is transcribed from RELIC_RULES.md §8.5's table,
            // which states that no value in it is authored by it — each is
            // transcribed from §6 and §8.1–§8.4:
            //
            //   Relic            Trigger        Condition (§8.1)       EffectDefinition[] (§8.2–§8.4)
            //   Burning Curse    OnBattleStart  null                   BurnDamage, Percentage, 30, Pet, Battle
            //   Combo Fang       OnCombo        ComboAtLeast(5)        Crit, PercentagePoints, 20, Pet, NextAttack
            //   Arcane Battery   OnPowerGain    null                   Power, Flat, 5, Pet, Immediate
            //   Execution Mark   OnHpBelow      HpPercentageBelow(30)  Crit, PercentagePoints, 15, Pet, NextAttack
            //   Cascade Core     OnCascade      null                   Power, Flat, 5, Pet, Immediate
            //   Battle Instinct  OnDamageTaken  null                   ATK, Percentage, 10, Pet, NextAttack
            //
            // The structured Condition is written as the
            // `conditionType` + `threshold` object §8.1 defines and TASK-132
            // already stored for the four existing rows; the effect array is
            // written as the one-element array §8.2 defines. Both are the same
            // shapes DATABASE.md §1 records, so a reader needs no second
            // representation and no prose fallback (§8.2 item 5).
            // ---------------------------------------------------------------

            // Burning Curse — RELIC_RULES.md §8.5: Trigger `OnBattleStart` (§3,
            // "fires once, at battle start"); Condition `null` (§8.1 item 4 —
            // its Trigger alone is its complete condition); effect
            // `{ "effectType": "BurnDamage", "valueType": "Percentage",
            // "value": 30, "target": "Pet", "lifetime": "Battle" }`, from §6's
            // "+30% Burn damage" row read through §8.2 item 2 (a proportion of
            // the stat's own value) and §8.3's allowed combination for
            // BurnDamage.
            //
            // The `30` is §6's authored magnitude, transcribed. It is NOT
            // pre-resolved against any Burn tick: §8.2 item 2 states the value it
            // applies to is read from battle state when the effect is applied,
            // and no such reading happens here.
            //
            // The Pet-owned-only reach of this modifier (§6 note 1, TASK-178
            // Q-4 = C: Pet-owned/source Burn applies, Boss-owned Burn does NOT)
            // is a RUNTIME rule owned by §6 note 1 and carried by
            // GAME_STATE.md §2.3.9's BurnDamageModifiers[]. It is deliberately
            // NOT encoded here: DATABASE.md §1 closes the effect member set at
            // effectType/valueType/value/target/lifetime (§8.3 item 5 forbids a
            // further member), so "Pet-owned Burn only" has no column and
            // inventing one would invent a contract. The stored `target: Pet`
            // identifies the Pet as the owner/source context per §8.3 item 1,
            // which is exactly §8.5 item 5's reading.
            migrationBuilder.InsertData(
                table: "RelicDefinition",
                columns: new[] { "RelicDefinitionId", "Name", "Trigger", "Condition", "EffectDefinition" },
                values: new object[]
                {
                    "relic-burning-curse", "Burning Curse", "OnBattleStart", null,
                    """[{"effectType":"BurnDamage","valueType":"Percentage","value":30,"target":"Pet","lifetime":"Battle"}]""",
                });

            // Combo Fang — RELIC_RULES.md §8.5: Trigger `OnCombo` (§3, "fires
            // when Combo reaches/crosses a threshold within one Swap"); Condition
            // `ComboAtLeast(5)`; effect `{ "effectType": "Crit",
            // "valueType": "PercentagePoints", "value": 20, "target": "Pet",
            // "lifetime": "NextAttack" }`, from §6's "+20pp Crit" row.
            //
            // `PercentagePoints` is §8.2 item 2's percentage-point
            // interpretation, not a proportion of anything; §8.3 item 4's
            // `NextAttack` REUSES the existing Card Crit consumption boundary
            // (ADR-017) rather than introducing a second one. No Crit modifier is
            // created and no roll is performed here.
            migrationBuilder.InsertData(
                table: "RelicDefinition",
                columns: new[] { "RelicDefinitionId", "Name", "Trigger", "Condition", "EffectDefinition" },
                values: new object[]
                {
                    "relic-combo-fang", "Combo Fang", "OnCombo",
                    """{"conditionType":"ComboAtLeast","threshold":5}""",
                    """[{"effectType":"Crit","valueType":"PercentagePoints","value":20,"target":"Pet","lifetime":"NextAttack"}]""",
                });

            // Arcane Battery — RELIC_RULES.md §8.5: Trigger `OnPowerGain` (§3,
            // "fires when the active Pet's Power increases from a qualifying
            // non-Relic-generated Power gain", §3.1); Condition `null`; effect
            // `{ "effectType": "Power", "valueType": "Flat", "value": 5,
            // "target": "Pet", "lifetime": "Immediate" }`, from §6's "+5 Power"
            // row read through §8.2 item 2 (an absolute amount) and §8.3's
            // allowed combination for Power.
            //
            // `Immediate` is §8.3 item 3's: applied once, at the moment it
            // triggers, leaving no standing modification behind — not a duration.
            // Storing it grants no Power and emits no power-change event.
            //
            // §3.1's qualification boundary (a Relic-granted Power gain is NOT a
            // qualifying gain, so `OnPowerGain → Arcane Battery → +5 Power →
            // OnPowerGain` cannot recur) and §3.2's per-cascade-iteration event
            // boundary are TRIGGER-EVALUATION rules owned by §3. They have no
            // column and are not encoded here; §8.3 item 5 forbids a further
            // member, and the runtime stage already implements them.
            migrationBuilder.InsertData(
                table: "RelicDefinition",
                columns: new[] { "RelicDefinitionId", "Name", "Trigger", "Condition", "EffectDefinition" },
                values: new object[]
                {
                    "relic-arcane-battery", "Arcane Battery", "OnPowerGain", null,
                    """[{"effectType":"Power","valueType":"Flat","value":5,"target":"Pet","lifetime":"Immediate"}]""",
                });

            // Execution Mark — RELIC_RULES.md §8.5: Trigger `OnHpBelow` (§3,
            // "fires when the active Pet's HP crosses below a configured
            // percentage"); Condition `HpPercentageBelow(30)`; effect
            // `{ "effectType": "Crit", "valueType": "PercentagePoints",
            // "value": 15, "target": "Pet", "lifetime": "NextAttack" }`, from
            // §6's "+15pp Crit" row (§6 note 3: it conforms to the existing
            // `OnHpBelow` and `HpPercentageBelow` semantics).
            //
            // The threshold `30` is §8.1 item 1's integer `N`, carried as
            // data rather than embedded in prose — "HP < 30%" as a string is
            // explicitly not a valid Condition. §8.1 item 8 places the read at
            // GAME_RULES.md §17 step 11; nothing is read or compared here.
            migrationBuilder.InsertData(
                table: "RelicDefinition",
                columns: new[] { "RelicDefinitionId", "Name", "Trigger", "Condition", "EffectDefinition" },
                values: new object[]
                {
                    "relic-execution-mark", "Execution Mark", "OnHpBelow",
                    """{"conditionType":"HpPercentageBelow","threshold":30}""",
                    """[{"effectType":"Crit","valueType":"PercentagePoints","value":15,"target":"Pet","lifetime":"NextAttack"}]""",
                });

            // Cascade Core — RELIC_RULES.md §8.5: Trigger `OnCascade` (§3, "fires
            // on each Cascade iteration", MATCH3_RULES.md §4); Condition `null`;
            // effect `{ "effectType": "Power", "valueType": "Flat", "value": 5,
            // "target": "Pet", "lifetime": "Immediate" }`, from §6's "+5 Power"
            // row.
            //
            // §3.2's event boundary — each actual cascade iteration is an
            // independent `OnCascade` event, so this Relic may fire once per
            // iteration and several cascades in one Swap are NOT collapsed into
            // one event — is a TRIGGER-EVALUATION rule owned by §3.2. It has no
            // column and is not encoded here; the stored Trigger is the §3
            // identity alone.
            migrationBuilder.InsertData(
                table: "RelicDefinition",
                columns: new[] { "RelicDefinitionId", "Name", "Trigger", "Condition", "EffectDefinition" },
                values: new object[]
                {
                    "relic-cascade-core", "Cascade Core", "OnCascade", null,
                    """[{"effectType":"Power","valueType":"Flat","value":5,"target":"Pet","lifetime":"Immediate"}]""",
                });

            // Battle Instinct — RELIC_RULES.md §8.5: Trigger `OnDamageTaken`
            // (§3, "fires when the active Pet takes damage"); Condition `null`;
            // effect `{ "effectType": "ATK", "valueType": "Percentage",
            // "value": 10, "target": "Pet", "lifetime": "NextAttack" }`, from
            // §6's "+10% ATK" row read through §8.2 item 2 (a proportion of the
            // stat's own value) and §8.3's ALLOWED `ATK` + `NextAttack`
            // combination — the second `ATK` row TASK-176 added to §8.3's table
            // for exactly this Relic (§8.5 item 10, TASK-178 Q-1 = A).
            //
            // The `10` is §6's authored magnitude, transcribed, and is NOT
            // pre-resolved against any assumed ATK value (§8.2 item 2).
            //
            // `ATK`'s runtime carrier is deliberately NOT decided or created by
            // this migration: §8.2–§8.4 declare the content only, so this stores
            // the declaration and introduces no modifier collection, no
            // StatusEffect, and no temporary-ATK representation. §8.5 item 10
            // records that this modifier rides the SAME
            // PetState.ATKModifiers[] collection Berserker Core's Battle-lifetime
            // modifier uses (GAME_STATE.md §2.3.7) — no
            // `NextAttackATKModifiers[]` and no generic `NextAttackModifiers[]` —
            // and that consumption happens at COMBAT_RULES.md §3.3 items 7–11's
            // existing qualifying-attack boundary. None of that is a content
            // member and none of the carriers is introduced here.
            migrationBuilder.InsertData(
                table: "RelicDefinition",
                columns: new[] { "RelicDefinitionId", "Name", "Trigger", "Condition", "EffectDefinition" },
                values: new object[]
                {
                    "relic-battle-instinct", "Battle Instinct", "OnDamageTaken", null,
                    """[{"effectType":"ATK","valueType":"Percentage","value":10,"target":"Pet","lifetime":"NextAttack"}]""",
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The mirror of Up: remove exactly the six rows this migration
            // inserted, keyed by the same canonical primary-key values, and no
            // other row (no broad table delete).
            //
            // The four Relic rows TASK-085 provisioned and TASK-132 re-encoded
            // are NOT named here: they predate this migration, this migration
            // never wrote them, and a reversal must not delete content it did
            // not insert (AGENTS.md §16).

            migrationBuilder.DeleteData(
                table: "RelicDefinition",
                keyColumn: "RelicDefinitionId",
                keyValue: "relic-burning-curse");

            migrationBuilder.DeleteData(
                table: "RelicDefinition",
                keyColumn: "RelicDefinitionId",
                keyValue: "relic-combo-fang");

            migrationBuilder.DeleteData(
                table: "RelicDefinition",
                keyColumn: "RelicDefinitionId",
                keyValue: "relic-arcane-battery");

            migrationBuilder.DeleteData(
                table: "RelicDefinition",
                keyColumn: "RelicDefinitionId",
                keyValue: "relic-execution-mark");

            migrationBuilder.DeleteData(
                table: "RelicDefinition",
                keyColumn: "RelicDefinitionId",
                keyValue: "relic-cascade-core");

            migrationBuilder.DeleteData(
                table: "RelicDefinition",
                keyColumn: "RelicDefinitionId",
                keyValue: "relic-battle-instinct");
        }
    }
}
