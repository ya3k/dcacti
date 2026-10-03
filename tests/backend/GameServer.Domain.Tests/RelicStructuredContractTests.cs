using GameServer.Domain.Relics;

namespace GameServer.Domain.Tests;

/// <summary>
/// The structured Relic <c>Condition</c> and <c>EffectDefinition</c> contract —
/// <c>RELIC_RULES.md</c> §8.1 (the condition grammar), §8.2 (the structured
/// effect array, its <c>effectType</c> and <c>valueType</c> sets, and its
/// loud-rejection rule), §8.3 (the <c>target</c>/<c>lifetime</c> vocabulary and
/// the per-<c>effectType</c> allowed combinations) and §8.5 (the four provisioned
/// rows), as recorded by <c>ADR-018</c> items 1–4 and implemented by TASK-132.
///
/// What is verified is the documented representation only: the closed sets, the
/// required members, the fixed per-effect combinations, lossless and
/// deterministic (de)serialization, and — the load-bearing half — that invalid
/// or superseded data <b>fails loudly</b> instead of defaulting (§8.2 item 5).
///
/// <b>Nothing here resolves a Relic.</b> No test in this file evaluates a
/// trigger, compares a condition against battle state, applies an effect, emits
/// <c>RelicTriggered</c>, or touches any battle-state member: §8.7 records every
/// one of those as NOT IMPLEMENTED, and TASK-132 is a storage task.
/// </summary>
public class RelicStructuredContractTests
{
    // -----------------------------------------------------------------------
    // The closed vocabularies — RELIC_RULES.md §8.1, §8.2, §8.3
    // -----------------------------------------------------------------------

    [Fact]
    public void ConditionType_ShouldBeExactlyTheThreeDocumentedForms()
    {
        // RELIC_RULES.md §8.1 defines exactly three forms; §8.1's grammar is the
        // whole set (TASK-131 D5). A fourth form is a gameplay rule no document
        // authors (AGENTS.md §7), not an engineering choice.
        var forms = Enum.GetNames<RelicConditionType>();

        Assert.Equal(
            new[] { "ComboAtLeast", "HpPercentageBelow", "MatchCountAtLeast" },
            forms.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(3, forms.Length);
    }

    [Fact]
    public void ConditionType_ShouldNotCarryTheSpeculativeFormsTheGrammarRejects()
    {
        // None of these is a form §8.1 defines. They are the shapes a
        // "convenient" generalization would invent — a modulo reading §8.1 item 7
        // explicitly disclaims, a comparison the grammar does not state, and a
        // negation — so none may appear.
        var forms = Enum.GetNames<RelicConditionType>();

        foreach (var rejected in new[]
                 {
                     "MatchCountEveryMultiple", "MatchCountEquals", "ComboAtMost",
                     "HpPercentageAbove", "HpPercentageAtLeast", "Always", "Never",
                 })
        {
            Assert.DoesNotContain(rejected, forms);
        }
    }

    [Fact]
    public void EffectType_ShouldBeExactlyTheFourDocumentedIdentities()
    {
        // RELIC_RULES.md §8.2 item 1 closes the set at ATK | Power | Crit |
        // CardCost. A fifth identity is a gameplay rule no document authors.
        var identities = Enum.GetNames<RelicEffectType>();

        Assert.Equal(
            new[] { "ATK", "CardCost", "Crit", "Power" },
            identities.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(4, identities.Length);
    }

    [Fact]
    public void EffectType_ShouldNotReuseTheCardVocabulary()
    {
        // RELIC_RULES.md §8.2 item 1's set is NOT DATABASE.md §1's Card set: the
        // two overlap on Power and Crit and differ on the other four members.
        // Reusing CardEffectType would make this contract accept identities its
        // own owner document does not define.
        var identities = Enum.GetNames<RelicEffectType>();

        foreach (var cardOnly in new[] { "Heal", "Shield", "Damage", "Burn" })
        {
            Assert.DoesNotContain(cardOnly, identities);
        }
    }

    [Fact]
    public void EffectValueType_ShouldBeExactlyTheFourDocumentedInterpretations()
    {
        // RELIC_RULES.md §8.2 item 2 (TASK-131 D2): Flat | Percentage |
        // PercentagePoints | Undetermined.
        var interpretations = Enum.GetNames<RelicEffectValueType>();

        Assert.Equal(
            new[] { "Flat", "Percentage", "PercentagePoints", "Undetermined" },
            interpretations.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(4, interpretations.Length);
    }

    [Fact]
    public void EffectValueType_ShouldNotCarryTheCardSpecificInterpretation()
    {
        // `PercentMaxHp` is the Card contract's (DATABASE.md §1), not the
        // Relic's: RELIC_RULES.md §8.2 item 2 defines `Percentage` as "a
        // proportion of the stat's own value", which is a different quantity from
        // a proportion of the target's Max HP. Substituting one would misstate a
        // Relic magnitude's unit.
        Assert.DoesNotContain("PercentMaxHp", Enum.GetNames<RelicEffectValueType>());
    }

    [Fact]
    public void EffectTarget_ShouldBeExactlyPet()
    {
        // RELIC_RULES.md §8.3 item 1: "The defined value is Pet". §8.3's table
        // fixes it for every defined effectType, and a second target is a
        // gameplay rule no document authors.
        var targets = Enum.GetNames<RelicEffectTarget>();

        Assert.Equal(new[] { "Pet" }, targets);
    }

    [Fact]
    public void EffectLifetime_ShouldBeExactlyTheThreeDocumentedValues()
    {
        // ADR-018 item 3: "`lifetime` values are `Immediate`, `Battle`, and
        // `NextAttack`", restated by RELIC_RULES.md §8.3.
        var lifetimes = Enum.GetNames<RelicEffectLifetime>();

        Assert.Equal(
            new[] { "Battle", "Immediate", "NextAttack" },
            lifetimes.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(3, lifetimes.Length);
    }

    // -----------------------------------------------------------------------
    // The fixed per-effectType combinations — RELIC_RULES.md §8.3
    // -----------------------------------------------------------------------

    [Fact]
    public void RequiredValueTypeFor_ShouldBeTheDocumentedCombination()
    {
        // RELIC_RULES.md §8.3's table, read as the contract's own lookup:
        //   ATK Pet Battle Percentage
        //   Power Pet Immediate Flat
        //   Crit Pet NextAttack PercentagePoints
        //   CardCost Pet Battle Percentage
        Assert.Equal(
            RelicEffectValueType.Percentage,
            RelicEffectDefinition.RequiredValueTypeFor(RelicEffectType.ATK));
        Assert.Equal(
            RelicEffectValueType.Flat,
            RelicEffectDefinition.RequiredValueTypeFor(RelicEffectType.Power));
        Assert.Equal(
            RelicEffectValueType.PercentagePoints,
            RelicEffectDefinition.RequiredValueTypeFor(RelicEffectType.Crit));
        Assert.Equal(
            RelicEffectValueType.Percentage,
            RelicEffectDefinition.RequiredValueTypeFor(RelicEffectType.CardCost));
    }

    [Fact]
    public void RequiredLifetimeFor_ShouldBeTheDocumentedCombination()
    {
        Assert.Equal(
            RelicEffectLifetime.Battle,
            RelicEffectDefinition.RequiredLifetimeFor(RelicEffectType.ATK));
        Assert.Equal(
            RelicEffectLifetime.Immediate,
            RelicEffectDefinition.RequiredLifetimeFor(RelicEffectType.Power));
        Assert.Equal(
            RelicEffectLifetime.NextAttack,
            RelicEffectDefinition.RequiredLifetimeFor(RelicEffectType.Crit));
        Assert.Equal(
            RelicEffectLifetime.Battle,
            RelicEffectDefinition.RequiredLifetimeFor(RelicEffectType.CardCost));
    }

    [Fact]
    public void Create_ShouldRejectACombinationTheTableDoesNotList()
    {
        // RELIC_RULES.md §8.3's closing rule: "a combination not listed is not
        // defined and may not be inferred". Each mismatch below is a pairing the
        // table does not contain, so storing it would invent a rule.
        var wrongValueTypes = new[]
        {
            (RelicEffectType.ATK, RelicEffectValueType.Flat),
            (RelicEffectType.ATK, RelicEffectValueType.PercentagePoints),
            (RelicEffectType.Power, RelicEffectValueType.Percentage),
            (RelicEffectType.Crit, RelicEffectValueType.Percentage),
            (RelicEffectType.CardCost, RelicEffectValueType.Flat),
            (RelicEffectType.CardCost, RelicEffectValueType.PercentagePoints),
        };

        foreach (var (effectType, valueType) in wrongValueTypes)
        {
            Assert.Throws<ArgumentException>(() => RelicEffectDefinition.Create(
                effectType,
                valueType,
                1,
                RelicEffectTarget.Pet,
                RelicEffectDefinition.RequiredLifetimeFor(effectType)));
        }
    }

    [Fact]
    public void Create_ShouldRejectALifetimeOtherThanTheEffectTypesOwn()
    {
        // §8.3 item 2: "A value outside the allowed combination for its
        // `effectType` is not defined." ATK is `Battle`, so `Immediate` and
        // `NextAttack` are both wrong for it.
        foreach (var lifetime in new[]
                 {
                     RelicEffectLifetime.Immediate, RelicEffectLifetime.NextAttack,
                 })
        {
            Assert.Throws<ArgumentException>(() => RelicEffectDefinition.Create(
                RelicEffectType.ATK,
                RelicEffectValueType.Percentage,
                5,
                RelicEffectTarget.Pet,
                lifetime));
        }
    }

    // -----------------------------------------------------------------------
    // The provisioned rows — RELIC_RULES.md §8.5
    // -----------------------------------------------------------------------

    [Fact]
    public void ProvisionedRows_ShouldEncodeExactlyTheDocumentedContract()
    {
        // RELIC_RULES.md §8.5's table, transcribed. Each row's Condition and
        // effect element is asserted member by member, so a transcription error
        // in any of them fails here rather than silently shipping.
        //
        // §8.5 states that no value in its table is authored by it — every one is
        // transcribed from §6 and §8.1–§8.4 — so these expectations are the
        // documented values, not the implementation's current output.
        var berserker = RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
            RelicEffectType.ATK, RelicEffectValueType.Percentage, 5,
            RelicEffectTarget.Pet, RelicEffectLifetime.Battle));

        var manaCrystal = RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
            RelicEffectType.Power, RelicEffectValueType.Flat, 10,
            RelicEffectTarget.Pet, RelicEffectLifetime.Immediate));

        var assassinEye = RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
            RelicEffectType.Crit, RelicEffectValueType.PercentagePoints, 10,
            RelicEffectTarget.Pet, RelicEffectLifetime.NextAttack));

        var emergencyCore = RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
            RelicEffectType.CardCost, RelicEffectValueType.Percentage, 50,
            RelicEffectTarget.Pet, RelicEffectLifetime.Battle));

        // The four §8.5 conditions, each read back through its own stored
        // payload so the assertion covers the representation as well as the
        // value:
        //   Berserker Core  MatchCountAtLeast(3)
        //   Mana Crystal    MatchCountAtLeast(4)
        //   Assassin Eye    ComboAtLeast(3)
        //   Emergency Core  HpPercentageBelow(30)
        var conditions = new[]
        {
            (RelicConditionType.MatchCountAtLeast, 3),
            (RelicConditionType.MatchCountAtLeast, 4),
            (RelicConditionType.ComboAtLeast, 3),
            (RelicConditionType.HpPercentageBelow, 30),
        };

        foreach (var (form, threshold) in conditions)
        {
            var condition = RelicCondition.Create(form, threshold);
            var read = RelicCondition.FromPersistedPayload(condition.ToPersistedPayload());

            Assert.Equal(condition, read);
            Assert.Equal(form, read.ConditionType);
            Assert.Equal(threshold, read.Threshold);
        }

        // Berserker Core — §8.5: MatchCountAtLeast(3); ATK, Percentage, 5, Pet, Battle.
        Assert.Equal(1, berserker.Count);
        Assert.Equal(RelicEffectType.ATK, berserker[0].EffectType);
        Assert.Equal(RelicEffectValueType.Percentage, berserker[0].ValueType);
        Assert.Equal(5, berserker[0].Value);
        Assert.Equal(RelicEffectTarget.Pet, berserker[0].Target);
        Assert.Equal(RelicEffectLifetime.Battle, berserker[0].Lifetime);

        // Mana Crystal — §8.5: MatchCountAtLeast(4); Power, Flat, 10, Pet, Immediate.
        Assert.Equal(1, manaCrystal.Count);
        Assert.Equal(RelicEffectType.Power, manaCrystal[0].EffectType);
        Assert.Equal(RelicEffectValueType.Flat, manaCrystal[0].ValueType);
        Assert.Equal(10, manaCrystal[0].Value);
        Assert.Equal(RelicEffectTarget.Pet, manaCrystal[0].Target);
        Assert.Equal(RelicEffectLifetime.Immediate, manaCrystal[0].Lifetime);

        // Assassin Eye — §8.5: ComboAtLeast(3); Crit, PercentagePoints, 10, Pet, NextAttack.
        Assert.Equal(1, assassinEye.Count);
        Assert.Equal(RelicEffectType.Crit, assassinEye[0].EffectType);
        Assert.Equal(RelicEffectValueType.PercentagePoints, assassinEye[0].ValueType);
        Assert.Equal(10, assassinEye[0].Value);
        Assert.Equal(RelicEffectTarget.Pet, assassinEye[0].Target);
        Assert.Equal(RelicEffectLifetime.NextAttack, assassinEye[0].Lifetime);

        // Emergency Core — §8.5: HpPercentageBelow(30); CardCost, Percentage, 50, Pet, Battle.
        Assert.Equal(1, emergencyCore.Count);
        Assert.Equal(RelicEffectType.CardCost, emergencyCore[0].EffectType);
        Assert.Equal(RelicEffectValueType.Percentage, emergencyCore[0].ValueType);
        Assert.Equal(50, emergencyCore[0].Value);
        Assert.Equal(RelicEffectTarget.Pet, emergencyCore[0].Target);
        Assert.Equal(RelicEffectLifetime.Battle, emergencyCore[0].Lifetime);
    }

    [Fact]
    public void Condition_ShouldCarryItsThresholdAsAnIntegerForEveryForm()
    {
        // RELIC_RULES.md §8.1 item 1: "The threshold is part of the value, never
        // embedded in prose — `"every 3 Matches"` as a string is not a valid
        // `Condition`." Each of §8.5's transcribed thresholds is asserted.
        Assert.Equal(
            3,
            RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 3).Threshold);
        Assert.Equal(
            4,
            RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 4).Threshold);
        Assert.Equal(
            3,
            RelicCondition.Create(RelicConditionType.ComboAtLeast, 3).Threshold);
        Assert.Equal(
            30,
            RelicCondition.Create(RelicConditionType.HpPercentageBelow, 30).Threshold);
    }

    // -----------------------------------------------------------------------
    // Serialization — the stored shape
    // -----------------------------------------------------------------------

    [Fact]
    public void Condition_ToPersistedPayload_ShouldWriteTheDocumentedObject()
    {
        // The member names are the storage contract, and the form is written as
        // its member NAME rather than its ordinal, so a persisted row is
        // self-describing.
        Assert.Equal(
            """{"conditionType":"MatchCountAtLeast","threshold":3}""",
            RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 3)
                .ToPersistedPayload());

        Assert.Equal(
            """{"conditionType":"ComboAtLeast","threshold":3}""",
            RelicCondition.Create(RelicConditionType.ComboAtLeast, 3)
                .ToPersistedPayload());

        Assert.Equal(
            """{"conditionType":"HpPercentageBelow","threshold":30}""",
            RelicCondition.Create(RelicConditionType.HpPercentageBelow, 30)
                .ToPersistedPayload());
    }

    [Fact]
    public void Effect_ToPersistedPayload_ShouldWriteTheDocumentedArray()
    {
        // RELIC_RULES.md §8.2's example shape, plus §8.5's other three rows. The
        // array is the stored value even for a single effect, and the member
        // names are the contract's.
        Assert.Equal(
            """[{"effectType":"ATK","valueType":"Percentage","value":5,"target":"Pet","lifetime":"Battle"}]""",
            RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
                RelicEffectType.ATK, RelicEffectValueType.Percentage, 5,
                RelicEffectTarget.Pet, RelicEffectLifetime.Battle)).ToPersistedPayload());

        Assert.Equal(
            """[{"effectType":"Power","valueType":"Flat","value":10,"target":"Pet","lifetime":"Immediate"}]""",
            RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
                RelicEffectType.Power, RelicEffectValueType.Flat, 10,
                RelicEffectTarget.Pet, RelicEffectLifetime.Immediate)).ToPersistedPayload());

        Assert.Equal(
            """[{"effectType":"Crit","valueType":"PercentagePoints","value":10,"target":"Pet","lifetime":"NextAttack"}]""",
            RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
                RelicEffectType.Crit, RelicEffectValueType.PercentagePoints, 10,
                RelicEffectTarget.Pet, RelicEffectLifetime.NextAttack)).ToPersistedPayload());

        Assert.Equal(
            """[{"effectType":"CardCost","valueType":"Percentage","value":50,"target":"Pet","lifetime":"Battle"}]""",
            RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
                RelicEffectType.CardCost, RelicEffectValueType.Percentage, 50,
                RelicEffectTarget.Pet, RelicEffectLifetime.Battle)).ToPersistedPayload());
    }

    [Fact]
    public void Effect_ShouldWriteNoCardOnlyMember()
    {
        // RELIC_RULES.md §8.3 item 5: "No member is defined beyond `target` and
        // `lifetime`." The Card contract's `duration` (Burn) and `scope` (Crit)
        // are therefore NOT part of a Relic element — Assassin Eye's Crit
        // carries `lifetime: NextAttack`, not `scope`.
        var payload = RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
            RelicEffectType.Crit, RelicEffectValueType.PercentagePoints, 10,
            RelicEffectTarget.Pet, RelicEffectLifetime.NextAttack)).ToPersistedPayload();

        Assert.DoesNotContain("duration", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("scope", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void Condition_ShouldRoundTripLosslessly()
    {
        // Domain → payload → Domain, for each of §8.1's three forms.
        foreach (var form in Enum.GetValues<RelicConditionType>())
        {
            var condition = RelicCondition.Create(form, 7);
            var payload = condition.ToPersistedPayload();
            var read = RelicCondition.FromPersistedPayload(payload);

            Assert.Equal(condition, read);
            Assert.Equal(form, read.ConditionType);
            Assert.Equal(7, read.Threshold);

            // And the re-serialized form is byte-identical — the output is
            // deterministic (TDD.md §6).
            Assert.Equal(payload, read.ToPersistedPayload());
        }
    }

    [Fact]
    public void Effect_ShouldRoundTripLosslessly()
    {
        // Domain → payload → Domain, for each of §8.3's four allowed
        // combinations — i.e. §8.5's four provisioned rows.
        foreach (var effectType in Enum.GetValues<RelicEffectType>())
        {
            var effects = RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
                effectType,
                RelicEffectDefinition.RequiredValueTypeFor(effectType),
                42,
                RelicEffectTarget.Pet,
                RelicEffectDefinition.RequiredLifetimeFor(effectType)));

            var payload = effects.ToPersistedPayload();
            var read = RelicEffectDefinitions.FromPersistedPayload(payload);

            Assert.Equal(effects, read);
            Assert.Equal(1, read.Count);
            Assert.Equal(effectType, read[0].EffectType);
            Assert.Equal(42, read[0].Value);
            Assert.Equal(payload, read.ToPersistedPayload());
        }
    }

    [Fact]
    public void Effect_ShouldPreserveMultiElementOrderWithoutTreatingItAsSemantic()
    {
        // RELIC_RULES.md §8.2 item 4: "Ordering within the array is NOT semantic
        // ... No rule reads element positions." The reader/writer therefore
        // preserve the stored sequence exactly — they neither sort nor normalize
        // — and two arrays differing only in order are NOT equal, because that
        // would be a claim about the data the contract does not make.
        var atk = RelicEffectDefinition.Create(
            RelicEffectType.ATK, RelicEffectValueType.Percentage, 5,
            RelicEffectTarget.Pet, RelicEffectLifetime.Battle);

        var power = RelicEffectDefinition.Create(
            RelicEffectType.Power, RelicEffectValueType.Flat, 10,
            RelicEffectTarget.Pet, RelicEffectLifetime.Immediate);

        var forward = RelicEffectDefinitions.Create(atk, power);
        var reverse = RelicEffectDefinitions.Create(power, atk);

        Assert.Equal(2, forward.Count);
        Assert.Equal(RelicEffectType.ATK, forward[0].EffectType);
        Assert.Equal(RelicEffectType.Power, forward[1].EffectType);

        Assert.NotEqual(forward, reverse);

        // The stored order survives the round trip unchanged.
        Assert.Equal(forward, RelicEffectDefinitions.FromPersistedPayload(
            forward.ToPersistedPayload()));
    }

    // -----------------------------------------------------------------------
    // An unauthored magnitude — RELIC_RULES.md §8.2 item 2/3
    // -----------------------------------------------------------------------

    [Fact]
    public void Undetermined_ShouldCarryNoValueMemberAtAll()
    {
        // RELIC_RULES.md §8.2 item 3: "an `Undetermined` effect carries no
        // `value` member at all, never `0` and never `null`, so an unauthored
        // magnitude cannot be read as a number."
        var effect = RelicEffectDefinition.Undetermined(
            RelicEffectType.ATK, RelicEffectTarget.Pet, RelicEffectLifetime.Battle);

        Assert.False(effect.HasValue);
        Assert.Null(effect.Value);
        Assert.Equal(RelicEffectValueType.Undetermined, effect.ValueType);

        var payload = RelicEffectDefinitions.Create(effect).ToPersistedPayload();

        // The member is absent — not present-with-null, and not zero. The check
        // is anchored on the member's own token (`"value":`) rather than on the
        // bare word, because `"valueType"` legitimately contains it.
        Assert.DoesNotContain("\"value\":", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("\"value\"", payload, StringComparison.Ordinal);
        Assert.Equal(
            """[{"effectType":"ATK","valueType":"Undetermined","target":"Pet","lifetime":"Battle"}]""",
            payload);

        Assert.Equal(
            effect,
            RelicEffectDefinitions.FromPersistedPayload(payload)[0]);
    }

    [Fact]
    public void Undetermined_ShouldBeRepresentableButNeverDefaulted()
    {
        // §8.2 item 2 keeps `Undetermined` valid, so the representation must
        // accept it — but nothing may reach it by omission, and pairing it with a
        // magnitude is rejected rather than reconciled.
        Assert.Throws<ArgumentOutOfRangeException>(() => RelicEffectDefinition.Create(
            RelicEffectType.ATK,
            RelicEffectValueType.Undetermined,
            5,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Battle));

        // A stored payload that pairs the marker with a value is malformed.
        Assert.Throws<ArgumentException>(() => RelicEffectDefinition.FromPersistedPayload(
            """{"effectType":"ATK","valueType":"Undetermined","value":5,"target":"Pet","lifetime":"Battle"}"""));
    }

    [Fact]
    public void NoProvisionedRelicRow_ShouldBeUndetermined()
    {
        // TASK-132's acceptance criteria: §8.5 authors every magnitude of all
        // four rows, so no provisioned row requires the marker and none is
        // introduced.
        foreach (var effectType in Enum.GetValues<RelicEffectType>())
        {
            var effects = RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
                effectType,
                RelicEffectDefinition.RequiredValueTypeFor(effectType),
                1,
                RelicEffectTarget.Pet,
                RelicEffectDefinition.RequiredLifetimeFor(effectType)));

            Assert.NotEqual(RelicEffectValueType.Undetermined, effects[0].ValueType);
            Assert.True(effects[0].HasValue);
        }
    }

    // -----------------------------------------------------------------------
    // Failure must be loud — RELIC_RULES.md §8.2 item 5
    // -----------------------------------------------------------------------

    [Fact]
    public void Condition_ShouldRejectAnUnrecognizedForm()
    {
        // §8.2 item 5's loud-rejection standard, carried to the condition by
        // DATABASE.md §1's Relic note item 6.
        Assert.Throws<ArgumentException>(() => RelicCondition.FromPersistedPayload(
            """{"conditionType":"AlwaysActive","threshold":3}"""));
    }

    [Fact]
    public void Condition_ShouldRejectAMissingOrInvalidThreshold()
    {
        // §8.1 item 1 requires the threshold to be carried; it is never
        // defaulted, and a non-positive one is not a threshold any document
        // authors.
        Assert.Throws<ArgumentException>(() => RelicCondition.FromPersistedPayload(
            """{"conditionType":"ComboAtLeast"}"""));

        // A non-positive threshold is rejected as out of range (the reader
        // delegates to the factory, which owns the bound).
        Assert.Throws<ArgumentOutOfRangeException>(() => RelicCondition.FromPersistedPayload(
            """{"conditionType":"ComboAtLeast","threshold":0}"""));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RelicCondition.Create(RelicConditionType.ComboAtLeast, 0));
    }

    [Fact]
    public void Condition_ShouldRejectTheSupersededProseForm()
    {
        // RELIC_RULES.md §8.1 item 1: `"every 3 Matches"` as a string is NOT a
        // valid Condition. There is deliberately no prose fallback and no
        // compatibility reader (TASK-132 Scope), so the old stored value is
        // rejected loudly rather than parsed.
        Assert.Throws<ArgumentException>(() => RelicCondition.FromPersistedPayload(
            "every 3 Matches"));
    }

    [Fact]
    public void Effect_ShouldRejectAnUnrecognizedEffectIdentity()
    {
        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload(
            """[{"effectType":"Burn","valueType":"Flat","value":50,"target":"Pet","lifetime":"Battle"}]"""));
    }

    [Fact]
    public void Effect_ShouldRejectAnUnrecognizedValueInterpretation()
    {
        // `PercentMaxHp` is the Card vocabulary, not the Relic's (§8.2 item 2).
        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload(
            """[{"effectType":"ATK","valueType":"PercentMaxHp","value":5,"target":"Pet","lifetime":"Battle"}]"""));
    }

    [Fact]
    public void Effect_ShouldRejectAnUnrecognizedTargetOrLifetime()
    {
        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload(
            """[{"effectType":"ATK","valueType":"Percentage","value":5,"target":"Boss","lifetime":"Battle"}]"""));

        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload(
            """[{"effectType":"ATK","valueType":"Percentage","value":5,"target":"Pet","lifetime":"Permanent"}]"""));
    }

    [Fact]
    public void Effect_ShouldRejectAMissingRequiredMember()
    {
        // §8.2 item 5 rejects "a missing required member" — target and lifetime
        // are both required by §8.3.
        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload(
            """[{"effectType":"ATK","valueType":"Percentage","value":5,"lifetime":"Battle"}]"""));

        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload(
            """[{"effectType":"ATK","valueType":"Percentage","value":5,"target":"Pet"}]"""));

        // A missing magnitude on an interpreting value type is also rejected.
        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload(
            """[{"effectType":"ATK","valueType":"Percentage","target":"Pet","lifetime":"Battle"}]"""));
    }

    [Fact]
    public void Effect_ShouldRejectTheSupersededProseFormAndTheCardMembers()
    {
        // The superseded R2-7 prose ("+5% ATK") is not an array and is rejected.
        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload(
            "+5% ATK"));

        // The Card-only members are not part of a Relic element (§8.3 item 5),
        // so a payload carrying one is malformed rather than partially accepted.
        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload(
            """[{"effectType":"Crit","valueType":"PercentagePoints","value":10,"target":"Pet","lifetime":"NextAttack","scope":"NextAttack"}]"""));

        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload(
            """[{"effectType":"Power","valueType":"Flat","value":10,"target":"Pet","lifetime":"Immediate","duration":2}]"""));
    }

    [Fact]
    public void Effect_ShouldRejectAnEmptyArrayAndANonArray()
    {
        // §8.5 gives every provisioned Relic at least one effect, so an empty
        // array is a definition the contract cannot express rather than a Relic
        // that does nothing.
        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload("[]"));

        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload(
            """{"effectType":"ATK","valueType":"Percentage","value":5,"target":"Pet","lifetime":"Battle"}"""));

        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.Create());
    }

    [Fact]
    public void Effect_ShouldRejectAMalformedOrAbsentPayload()
    {
        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload(""));
        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload("   "));
        Assert.Throws<ArgumentException>(() => RelicEffectDefinitions.FromPersistedPayload("[{"));
        Assert.Throws<ArgumentException>(() => RelicCondition.FromPersistedPayload(""));
        Assert.Throws<ArgumentException>(() => RelicCondition.FromPersistedPayload("not json"));
    }

    [Fact]
    public void TryFromPersistedPayload_ShouldAgreeWithTheThrowingReader()
    {
        // The non-throwing counterparts define no rule of their own: they reach
        // exactly the same verdict.
        Assert.True(RelicCondition.TryFromPersistedPayload(
            """{"conditionType":"ComboAtLeast","threshold":3}""", out var condition));
        Assert.Equal(RelicConditionType.ComboAtLeast, condition.ConditionType);

        Assert.False(RelicCondition.TryFromPersistedPayload(
            """{"conditionType":"AlwaysActive","threshold":3}""", out _));
        Assert.False(RelicCondition.TryFromPersistedPayload(null, out _));
        Assert.False(RelicCondition.TryFromPersistedPayload("every 3 Matches", out _));

        Assert.True(RelicEffectDefinitions.TryFromPersistedPayload(
            """[{"effectType":"ATK","valueType":"Percentage","value":5,"target":"Pet","lifetime":"Battle"}]""",
            out var effects));
        Assert.Equal(RelicEffectType.ATK, effects[0].EffectType);

        Assert.False(RelicEffectDefinitions.TryFromPersistedPayload("+5% ATK", out _));
        Assert.False(RelicEffectDefinitions.TryFromPersistedPayload("[]", out _));
    }

    [Fact]
    public void Create_ShouldRejectAnUndefinedVocabularyMember()
    {
        // An identity outside the closed set names an effect no document
        // authors, so it must not become a silent no-op (AGENTS.md §7).
        Assert.Throws<ArgumentOutOfRangeException>(() => RelicCondition.Create(
            (RelicConditionType)7, 3));

        Assert.Throws<ArgumentOutOfRangeException>(() => RelicEffectDefinition.Create(
            (RelicEffectType)7,
            RelicEffectValueType.Percentage,
            5,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Battle));

        Assert.Throws<ArgumentOutOfRangeException>(() => RelicEffectDefinition.Create(
            RelicEffectType.ATK,
            RelicEffectValueType.Percentage,
            5,
            (RelicEffectTarget)7,
            RelicEffectLifetime.Battle));
    }

    [Fact]
    public void Create_ShouldRejectANonPositiveMagnitude()
    {
        // `0` is the CLR default of an unset value, so admitting it would let a
        // missing magnitude pass as a real one.
        Assert.Throws<ArgumentOutOfRangeException>(() => RelicEffectDefinition.Create(
            RelicEffectType.ATK,
            RelicEffectValueType.Percentage,
            0,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Battle));
    }
}
