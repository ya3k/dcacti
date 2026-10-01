using GameServer.Domain.Cards;

namespace GameServer.Domain.Tests;

/// <summary>
/// The structured Card <c>EffectDefinition</c> contract — the <c>effectType</c>
/// closed set and the <c>valueType</c> interpretation set of
/// <c>DATABASE.md</c> §1 item 1 / §3 (TASK-108 decisions D-1/D-2, extended by
/// TASK-111 D-2/D-3/D-4), and the per-element shape those members live in.
///
/// What is verified is the documented contract only: the closed sets, the
/// required members, the effect-specific extra members with their present-iff
/// conditions, lossless and deterministic (de)serialization, and — the
/// load-bearing half — that invalid data <b>fails loudly</b> instead of
/// defaulting. Nothing here applies an effect: no test in this file may call a
/// Domain write site, roll a Crit, or tick a Burn.
/// </summary>
public class CardEffectDefinitionTests
{
    // -----------------------------------------------------------------------
    // The closed effect-identity set — DATABASE.md §1 item 1
    // -----------------------------------------------------------------------

    [Fact]
    public void EffectType_ShouldBeExactlyTheSixDocumentedIdentities()
    {
        // DATABASE.md §1 item 1 closes the set at Heal | Shield | Power | Damage |
        // Burn | Crit, and §3 restates it. The first three are CARD_RULES.md §2's
        // Basic Card effects; the last three are the ones the same owner named for
        // §4.1's Pet Skill Cards (TASK-111 D-2), which also confirmed the first
        // three remain. A seventh identity is a gameplay rule no document authors
        // (AGENTS.md §7), not an engineering choice.
        var identities = Enum.GetNames<CardEffectType>();

        Assert.Equal(
            new[] { "Burn", "Crit", "Damage", "Heal", "Power", "Shield" },
            identities.OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(6, identities.Length);
    }

    [Fact]
    public void EffectType_ShouldNotCarryTheSpeculativeIdentitiesTheContractRejects()
    {
        // None of these is a member §1 item 1 defines. `NextAttack` in particular
        // is the Crit `scope` value, not an effectType (TASK-111 D-3): storing it
        // as an effect identity would invent vocabulary the owner never supplied.
        var identities = Enum.GetNames<CardEffectType>();

        foreach (var rejected in new[]
                 {
                     "DirectDamage", "BurnDamage", "CriticalChance", "NextAttack",
                     "DamagePerTurn",
                 })
        {
            Assert.DoesNotContain(rejected, identities);
        }
    }

    [Fact]
    public void EffectType_ShouldKeepDamageDistinctFromPower()
    {
        // TASK-111 D-4: `Power` continues to denote Power Charge (CARD_RULES.md
        // §2), while `Damage` carries a damage-dealing effect's base value. The two
        // must be separate members, so a damage effect stored as Power — the
        // card-iron-fang defect DATABASE.md §1 item 8 records — cannot be
        // expressed.
        Assert.NotEqual(CardEffectType.Damage, CardEffectType.Power);

        Assert.NotEqual(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 120),
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 120));
    }

    // -----------------------------------------------------------------------
    // The closed value-interpretation set — DATABASE.md §1 item 1
    // -----------------------------------------------------------------------

    [Fact]
    public void ValueType_ShouldBeTheThreeInterpretationsPlusTheUndeterminedMarker()
    {
        // DATABASE.md §1 item 1 / §3 fix the set at Flat | PercentMaxHp |
        // PercentagePoints | Undetermined. `Flat` and `PercentMaxHp` are exactly
        // CARD_RULES.md §2's two distinctions; `PercentagePoints` is §4.1's Crit
        // unit (TASK-111 D-3). `Undetermined` is NOT an interpretation — it is the
        // documented marker for "the owning document states no magnitude yet"
        // (§1 item 9, which D-3 confirms remains valid).
        var interpretations = Enum.GetNames<CardEffectValueType>();

        Assert.Equal(
            new[] { "Flat", "PercentMaxHp", "PercentagePoints", "Undetermined" },
            interpretations.OrderBy(n => n, StringComparer.Ordinal));

        // Exactly three of them interpret a value.
        var interpreting = interpretations
            .Where(name => name != nameof(CardEffectValueType.Undetermined))
            .ToArray();

        Assert.Equal(
            new[] { "Flat", "PercentMaxHp", "PercentagePoints" },
            interpreting.OrderBy(n => n, StringComparer.Ordinal));
    }

    // -----------------------------------------------------------------------
    // Construction — the valid cases the contract permits
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20)]
    [InlineData(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20)]
    [InlineData(CardEffectType.Power, CardEffectValueType.Flat, 25)]
    [InlineData(CardEffectType.Damage, CardEffectValueType.Flat, 100)]
    public void Create_ShouldAcceptEveryPlainEffectCombination(
        CardEffectType effectType,
        CardEffectValueType valueType,
        int value)
    {
        // The effects whose magnitude `value` alone carries: CARD_RULES.md §2's
        // three Basic Cards, plus §4.1's damage effects. The values are transcribed
        // from §2/§4.1 and asserted here so an encoded balance change fails here as
        // well as at the migration.
        var effect = CardEffectDefinition.Create(effectType, valueType, value);

        Assert.Equal(effectType, effect.EffectType);
        Assert.Equal(valueType, effect.ValueType);
        Assert.Equal(value, effect.Value);

        // No extra member is carried by these effects.
        Assert.Null(effect.Duration);
        Assert.Null(effect.Scope);
    }

    [Fact]
    public void Burn_ShouldCarryThePerTickMagnitudeAndTheDuration()
    {
        // CARD_RULES.md §4.1 (Inferno): "Burn: 50 damage per tick for 2 Turns".
        // DATABASE.md §1 item 1 / TASK-111 D-3 fix the members: `value` is the
        // damage per tick and `duration` is the number of Turns.
        var effect = CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2);

        Assert.Equal(CardEffectType.Burn, effect.EffectType);
        Assert.Equal(CardEffectValueType.Flat, effect.ValueType);
        Assert.Equal(50, effect.Value);
        Assert.Equal(2, effect.Duration);

        // A Burn element carries no scope: §1 defines `scope` on a Crit only.
        Assert.Null(effect.Scope);
    }

    [Fact]
    public void Crit_ShouldCarryThePercentagePointIncreaseAndTheNextAttackScope()
    {
        // CARD_RULES.md §4.1 (Iron Fang): "increase Crit chance by 10 percentage
        // points for the next attack only". DATABASE.md §1 item 1 / TASK-111 D-3:
        // the interpretation is PercentagePoints, `value` is the increase in
        // percentage points, and `scope` is NextAttack.
        var effect = CardEffectDefinition.Crit(10, CardEffectDefinition.NextAttackScope);

        Assert.Equal(CardEffectType.Crit, effect.EffectType);
        Assert.Equal(CardEffectValueType.PercentagePoints, effect.ValueType);
        Assert.Equal(10, effect.Value);
        Assert.Equal("NextAttack", effect.Scope);

        // A Crit element carries no duration: §1 defines `duration` on a Burn only.
        Assert.Null(effect.Duration);
    }

    [Fact]
    public void NextAttack_ShouldBeAScopeValueAndNeverAnEffectIdentity()
    {
        // TASK-111 D-3 stores `scope: "NextAttack"`. It is a member VALUE of the
        // Crit element's scope, not an effectType and not a valueType — the
        // distinction the contract draws, so neither enum may gain it.
        Assert.Equal("NextAttack", CardEffectDefinition.NextAttackScope);

        Assert.DoesNotContain("NextAttack", Enum.GetNames<CardEffectType>());
        Assert.DoesNotContain("NextAttack", Enum.GetNames<CardEffectValueType>());
    }

    // -----------------------------------------------------------------------
    // Validation — invalid data fails loudly, never defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void Factories_ShouldRejectAnUndefinedEffectType()
    {
        // §1 item 6: an unrecognized `effectType` is rejected. Admitting one would
        // turn a content typo into a silent no-op (AGENTS.md §7).
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CardEffectDefinition.Create(
                (CardEffectType)99,
                CardEffectValueType.Flat,
                25));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CardEffectDefinition.Undetermined((CardEffectType)99));
    }

    [Fact]
    public void Factories_ShouldRejectAnUnsupportedValueType()
    {
        // §1 item 6: an unrecognized `valueType` is rejected rather than assumed to
        // mean Flat — assuming would silently reinterpret a magnitude.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CardEffectDefinition.Create(
                CardEffectType.Heal,
                (CardEffectValueType)99,
                20));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Factories_ShouldRejectANonPositiveValue(int value)
    {
        // DATABASE.md §3 states the bound as "int, > 0", and 0 is the CLR default
        // of an unset int — admitting it would let a missing value pass as a real
        // one (no default).
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.Flat, value));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CardEffectDefinition.Burn(CardEffectValueType.Flat, value, 2));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CardEffectDefinition.Crit(value, CardEffectDefinition.NextAttackScope));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Burn_ShouldRejectANonPositiveDuration(int duration)
    {
        // DATABASE.md §3: duration is "int, > 0, present iff effectType = Burn".
        // It is never defaulted, and a Burn without a real duration is exactly the
        // missing required extra member §1 item 6 rejects.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, duration));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nextattack")]
    [InlineData("Next Attack")]
    [InlineData("AllAttacks")]
    public void Crit_ShouldRejectAnyScopeOtherThanTheOneDefined(string scope)
    {
        // DATABASE.md §3 defines exactly one scope value — `string = "NextAttack"`.
        // No other token is defined, and inventing one would author a rule no
        // document states (AGENTS.md §7).
        Assert.Throws<ArgumentException>(
            () => CardEffectDefinition.Crit(10, scope));
    }

    [Fact]
    public void Create_ShouldRejectTheUndeterminedValueType()
    {
        // The two cases are mutually exclusive: an undetermined magnitude has no
        // value, and a value implies an interpreting type. `Create` takes a
        // magnitude, so pairing it with Undetermined would assert both.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CardEffectDefinition.Create(
                CardEffectType.Heal,
                CardEffectValueType.Undetermined,
                20));
    }

    [Fact]
    public void BurnAndCritFactories_ShouldRejectTheUndeterminedValueType()
    {
        // A Burn element's `value` is its per-tick damage, so it must interpret
        // one; the same holds for Crit's percentage points.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CardEffectDefinition.Burn(CardEffectValueType.Undetermined, 50, 2));

        // Crit fixes its interpretation to PercentagePoints, so the parameter it
        // takes is the value, not the interpretation; an Undetermined Crit has no
        // representation and is built only through Undetermined(effectType), which
        // rejects Crit because its scope would be missing.
        Assert.Throws<ArgumentException>(
            () => CardEffectDefinition.Undetermined(CardEffectType.Crit));
        Assert.Throws<ArgumentException>(
            () => CardEffectDefinition.Undetermined(CardEffectType.Burn));
    }

    [Fact]
    public void Create_ShouldRejectTheEffectsThatAlwaysCarryAnExtraMember()
    {
        // A Burn always carries `duration` and a Crit always carries `scope`
        // (DATABASE.md §3's present-iff conditions). Neither can be built through
        // the plain-value path, because the result would be a missing required
        // extra member — the case §1 item 6 rejects.
        Assert.Throws<ArgumentException>(
            () => CardEffectDefinition.Create(CardEffectType.Burn, CardEffectValueType.Flat, 50));

        Assert.Throws<ArgumentException>(
            () => CardEffectDefinition.Create(
                CardEffectType.Crit,
                CardEffectValueType.PercentagePoints,
                10));
    }

    [Fact]
    public void DefaultInstance_ShouldNotBeAValidEffect()
    {
        // `default(CardEffectDefinition)` carries no effect identity and no value.
        // It is not a usable effect, and the reader/writer must not launder it into
        // one.
        CardEffectDefinition unset = default;

        Assert.Throws<ArgumentOutOfRangeException>(() => unset.ToPersistedPayload());
    }

    // -----------------------------------------------------------------------
    // Serialization — member names, not ordinals
    // -----------------------------------------------------------------------

    [Fact]
    public void ToPersistedPayload_ShouldWriteExactlyTheDocumentedMembers()
    {
        // DATABASE.md §1 item 2: the type members are stored as their MEMBER
        // NAMES, not as enum ordinals, "so a persisted row is self-describing".
        // §1 item 1's example is exactly this object.
        var effect = CardEffectDefinition.Create(
            CardEffectType.Shield,
            CardEffectValueType.PercentMaxHp,
            20);

        Assert.Equal(
            """{"effectType":"Shield","valueType":"PercentMaxHp","value":20}""",
            effect.ToPersistedPayload());
    }

    [Fact]
    public void ToPersistedPayload_ShouldWriteNamesRatherThanOrdinals()
    {
        // The explicit anti-requirement: ordinal encoding is forbidden, because a
        // stored row's meaning would then depend on member order. `Damage` is
        // ordinal 3 and `PercentagePoints` ordinal 3 — a persisted `3`/`3` would
        // mean nothing without the enum.
        var effect = CardEffectDefinition.Create(
            CardEffectType.Damage,
            CardEffectValueType.Flat,
            100);

        var payload = effect.ToPersistedPayload();

        Assert.Equal("""{"effectType":"Damage","valueType":"Flat","value":100}""", payload);
        Assert.DoesNotContain("\"effectType\":3", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("\"valueType\":0", payload, StringComparison.Ordinal);
        Assert.Contains("\"effectType\":\"Damage\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"valueType\":\"Flat\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void ToPersistedPayload_ShouldWriteTheBurnExtraMember()
    {
        // DATABASE.md §1 item 1's Burn example, in the canonical member order.
        var effect = CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2);

        Assert.Equal(
            """{"effectType":"Burn","valueType":"Flat","value":50,"duration":2}""",
            effect.ToPersistedPayload());
    }

    [Fact]
    public void ToPersistedPayload_ShouldWriteTheCritExtraMember()
    {
        // DATABASE.md §1 item 1's Crit example, in the canonical member order.
        var effect = CardEffectDefinition.Crit(10, CardEffectDefinition.NextAttackScope);

        Assert.Equal(
            """{"effectType":"Crit","valueType":"PercentagePoints","value":10,"scope":"NextAttack"}""",
            effect.ToPersistedPayload());
    }

    [Fact]
    public void Elements_ShouldRoundTripEveryValidShape()
    {
        // DATABASE.md §1: the round trip is LOSSLESS — every member and its exact
        // type survive, with no coercion, no defaulting, and no reordering.
        var effects = new[]
        {
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20),
            CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20),
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25),
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 120),
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2),
            CardEffectDefinition.Crit(10, CardEffectDefinition.NextAttackScope),
            CardEffectDefinition.Undetermined(CardEffectType.Heal),
        };

        foreach (var original in effects)
        {
            var reloaded = CardEffectDefinition.FromPersistedPayload(original.ToPersistedPayload());

            Assert.Equal(original, reloaded);
            Assert.Equal(original.EffectType, reloaded.EffectType);
            Assert.Equal(original.ValueType, reloaded.ValueType);
            Assert.Equal(original.Value, reloaded.Value);
            Assert.Equal(original.Duration, reloaded.Duration);
            Assert.Equal(original.Scope, reloaded.Scope);
        }
    }

    [Fact]
    public void RoundTrip_ShouldBeDeterministic()
    {
        // The same element always produces the same payload, and reading that
        // payload always produces the same element (TDD.md §6).
        var effect = CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2);

        var first = effect.ToPersistedPayload();
        var second = effect.ToPersistedPayload();

        Assert.Equal(first, second);
        Assert.Equal(
            CardEffectDefinition.FromPersistedPayload(first),
            CardEffectDefinition.FromPersistedPayload(second));
    }

    [Fact]
    public void ElementsDifferingOnlyInValueType_ShouldNotDeserializeToTheSameThing()
    {
        // The whole reason for carrying `valueType`: a percentage-of-MaxHP effect
        // and a flat effect with the same number must remain distinguishable.
        // Collapsing them would silently reinterpret a CARD_RULES.md §2 magnitude.
        var proportional = CardEffectDefinition.Create(
            CardEffectType.Heal,
            CardEffectValueType.PercentMaxHp,
            20);
        var flat = CardEffectDefinition.Create(
            CardEffectType.Heal,
            CardEffectValueType.Flat,
            20);
        var percentagePoints = CardEffectDefinition.Crit(20, CardEffectDefinition.NextAttackScope);

        Assert.NotEqual(proportional, flat);
        Assert.NotEqual(proportional.ToPersistedPayload(), flat.ToPersistedPayload());

        // A percentage POINT is not a percentage OF MaxHP: the two must not
        // collapse even at the same number and effect-independent reading.
        Assert.NotEqual(CardEffectValueType.PercentMaxHp, percentagePoints.ValueType);
        Assert.Equal(CardEffectValueType.PercentagePoints, percentagePoints.ValueType);
    }

    [Fact]
    public void TheProportionIsNotPreResolvedToAnAbsoluteAmount()
    {
        // DATABASE.md §1 item 1: a percentage is stored AS the percentage and is
        // never resolved against an assumed Max HP — the active Pet's MaxHP is
        // battle state (GAME_STATE.md §2.3) and is read when the effect is applied.
        var effect = CardEffectDefinition.Create(
            CardEffectType.Shield,
            CardEffectValueType.PercentMaxHp,
            20);

        Assert.Equal(20, effect.Value);

        var payload = effect.ToPersistedPayload();

        Assert.Equal(
            """{"effectType":"Shield","valueType":"PercentMaxHp","value":20}""",
            payload);
        Assert.DoesNotContain("MaxHP\":", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void CritValue_ShouldBeTheCardsOwnAndCarryNoBorrowedPassiveConfig()
    {
        // CARD_RULES.md §4.1's closing note: Iron Fang's Crit increase is the
        // Card's own value and is INDEPENDENT of Bạch Hổ's Pet Passive
        // configuration value (PASSIVE_RULES.md §7/§8). The element stores only the
        // Card's 10 percentage points — no Passive-derived number may appear, and
        // PASSIVE_RULES.md is not read here.
        var crit = CardEffectDefinition.Crit(10, CardEffectDefinition.NextAttackScope);

        Assert.Equal(10, crit.Value);

        var payload = crit.ToPersistedPayload();

        Assert.Equal(
            """{"effectType":"Crit","valueType":"PercentagePoints","value":10,"scope":"NextAttack"}""",
            payload);
        Assert.DoesNotContain("passive", payload, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // An unauthored magnitude — represented, never invented
    // -----------------------------------------------------------------------

    [Fact]
    public void Undetermined_ShouldRecordTheEffectWithNoMagnitude()
    {
        // DATABASE.md §1 item 9: an unauthored magnitude is represented, never
        // invented. This is how such an effect is stored: the identity is named and
        // the magnitude is explicitly recorded as not yet authored — never 0, never
        // a placeholder, never a borrowed value.
        var effect = CardEffectDefinition.Undetermined(CardEffectType.Heal);

        Assert.Equal(CardEffectType.Heal, effect.EffectType);
        Assert.Equal(CardEffectValueType.Undetermined, effect.ValueType);
        Assert.Null(effect.Value);
        Assert.False(effect.HasValue);
        Assert.Null(effect.Duration);
        Assert.Null(effect.Scope);
    }

    [Fact]
    public void Undetermined_ShouldSerializeWithNoValueMember()
    {
        // No `value` member at all — not 0, not null. A number here would be an
        // invented magnitude, and `"value": null` would be a second representation
        // of the same absence (GAME_STATE.md §0 item 5).
        var effect = CardEffectDefinition.Undetermined(CardEffectType.Heal);

        var payload = effect.ToPersistedPayload();

        Assert.Equal("""{"effectType":"Heal","valueType":"Undetermined"}""", payload);
        Assert.DoesNotContain("\"value\"", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("0", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void Undetermined_ShouldRoundTripWithoutAcquiringAMagnitude()
    {
        var original = CardEffectDefinition.Undetermined(CardEffectType.Damage);

        var reloaded = CardEffectDefinition.FromPersistedPayload(original.ToPersistedPayload());

        Assert.Equal(original, reloaded);
        Assert.Null(reloaded.Value);
        Assert.False(reloaded.HasValue);
    }

    [Fact]
    public void Undetermined_ShouldNotEqualAnyMagnitudeBearingEffect()
    {
        // An unauthored magnitude must never be confusable with a real one.
        var undetermined = CardEffectDefinition.Undetermined(CardEffectType.Heal);
        var authored = CardEffectDefinition.Create(
            CardEffectType.Heal,
            CardEffectValueType.PercentMaxHp,
            20);

        Assert.NotEqual(undetermined, authored);
        Assert.NotEqual(undetermined.ToPersistedPayload(), authored.ToPersistedPayload());
    }

    [Theory]
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"Undetermined\", \"value\": 20 }")]
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"Undetermined\", \"value\": 0 }")]
    public void FromPersistedPayload_ShouldRejectAnUndeterminedEffectThatCarriesAValue(string payload)
    {
        // DATABASE.md §3: value is "present iff valueType interprets one". The two
        // cases are mutually exclusive, and accepting a combination would let a
        // placeholder magnitude enter the contract — including 0.
        Assert.ThrowsAny<ArgumentException>(
            () => CardEffectDefinition.FromPersistedPayload(payload));
    }

    [Fact]
    public void FromPersistedPayload_ShouldTreatAnExplicitNullValueAsAbsent()
    {
        // JSON `null` is not a magnitude, and an explicit null is indistinguishable
        // from an absent member once parsed. Treating it as "no magnitude" is the
        // ONLY tolerant reading here: it still yields an element with no value, so
        // no number can enter the contract.
        var reloaded = CardEffectDefinition.FromPersistedPayload(
            """{ "effectType": "Heal", "valueType": "Undetermined", "value": null }""");

        Assert.Equal(CardEffectValueType.Undetermined, reloaded.ValueType);
        Assert.Null(reloaded.Value);
        Assert.False(reloaded.HasValue);
    }

    // -----------------------------------------------------------------------
    // Deserialization — malformed payloads are rejected, never defaulted
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("effect")]
    [InlineData("Restore the active Pet's HP by 20% of its Max HP")]
    [InlineData("{")]
    [InlineData("{ \"effectType\": \"Heal\"")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\" }")]
    [InlineData("{ \"effectType\": \"Heal\", \"value\": 20 }")]
    [InlineData("{ \"valueType\": \"PercentMaxHp\", \"value\": 20 }")]
    [InlineData("{ \"effectType\": \"\", \"valueType\": \"Flat\", \"value\": 20 }")]
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"\", \"value\": 20 }")]
    [InlineData("{ \"effectType\": \"heal\", \"valueType\": \"Flat\", \"value\": 20 }")]
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"Percentage\", \"value\": 20 }")]
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": 0 }")]
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": -5 }")]
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": 1.5 }")]
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": \"20\" }")]
    [InlineData("{ \"effectType\": 0, \"valueType\": \"PercentMaxHp\", \"value\": 20 }")]
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": 20, \"extra\": 1 }")]
    public void FromPersistedPayload_ShouldRejectMalformedOrIncompleteData(string payload)
    {
        // DATABASE.md §1 item 6: validation fails LOUDLY. There is no fallback
        // magnitude, no default, no prose fallback, and no silent no-op for an
        // unrecognized identity — including a truncated payload, a missing required
        // member, an unsupported interpretation, a non-positive or non-integral
        // value, an ordinal instead of a member name, and a member this contract
        // does not define.
        //
        // ArgumentOutOfRangeException derives from ArgumentException, so both the
        // reader's own rejections and the value checks it delegates to are covered
        // by asserting the base type.
        Assert.ThrowsAny<ArgumentException>(
            () => CardEffectDefinition.FromPersistedPayload(payload));
    }

    [Fact]
    public void FromPersistedPayload_ShouldRejectAMissingValueRatherThanDefaultingItToZero()
    {
        // The specific hazard: `value` absent must NOT arrive as the CLR default 0
        // and then be accepted. It is reported as missing.
        var exception = Assert.Throws<ArgumentException>(
            () => CardEffectDefinition.FromPersistedPayload(
                """{ "effectType": "Heal", "valueType": "Flat" }"""));

        Assert.Contains("value", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromPersistedPayload_ShouldRejectAnUnknownEffectTypeByName()
    {
        // An unrecognized effect must be reported with the offending token, so a
        // content typo is diagnosable rather than silent.
        var exception = Assert.Throws<ArgumentException>(
            () => CardEffectDefinition.FromPersistedPayload(
                """{ "effectType": "DirectDamage", "valueType": "Flat", "value": 20 }"""));

        Assert.Contains("DirectDamage", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromPersistedPayload_ShouldRejectAnUnsupportedValueTypeByName()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CardEffectDefinition.FromPersistedPayload(
                """{ "effectType": "Heal", "valueType": "PerTick", "value": 20 }"""));

        Assert.Contains("PerTick", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromPersistedPayload_ShouldNotAcceptAnOrdinalInsteadOfAMemberName()
    {
        // DATABASE.md §1 item 2: the stored tokens are the member NAMES. An ordinal
        // would make a persisted row's meaning depend on member order, which the
        // enum documentation states no rule derives from.
        Assert.False(CardEffectDefinition.TryFromPersistedPayload(
            """{ "effectType": 3, "valueType": 0, "value": 20 }""",
            out _));
    }

    // -----------------------------------------------------------------------
    // The present-iff conditions for the extra members — DATABASE.md §3
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("{ \"effectType\": \"Burn\", \"valueType\": \"Flat\", \"value\": 50 }")]
    [InlineData("{ \"effectType\": \"Burn\", \"valueType\": \"Flat\", \"value\": 50, \"duration\": 0 }")]
    [InlineData("{ \"effectType\": \"Burn\", \"valueType\": \"Flat\", \"value\": 50, \"duration\": -1 }")]
    [InlineData("{ \"effectType\": \"Burn\", \"valueType\": \"Flat\", \"value\": 50, \"duration\": \"2\" }")]
    public void FromPersistedPayload_ShouldRejectABurnWithoutAValidDuration(string payload)
    {
        // DATABASE.md §3: "duration int, > 0, present iff effectType = Burn", and §1
        // item 6 rejects a missing required extra member. A Burn with no usable
        // duration is therefore a read failure, not a defaulted 0 or 1.
        var exception = Assert.ThrowsAny<ArgumentException>(
            () => CardEffectDefinition.FromPersistedPayload(payload));

        Assert.NotNull(exception);
    }

    [Theory]
    [InlineData("{ \"effectType\": \"Crit\", \"valueType\": \"PercentagePoints\", \"value\": 10 }")]
    [InlineData("{ \"effectType\": \"Crit\", \"valueType\": \"PercentagePoints\", \"value\": 10, \"scope\": \"\" }")]
    [InlineData("{ \"effectType\": \"Crit\", \"valueType\": \"PercentagePoints\", \"value\": 10, \"scope\": \"AllAttacks\" }")]
    [InlineData("{ \"effectType\": \"Crit\", \"valueType\": \"PercentagePoints\", \"value\": 10, \"scope\": \"nextattack\" }")]
    public void FromPersistedPayload_ShouldRejectACritWithoutAValidScope(string payload)
    {
        // DATABASE.md §3: "scope string = \"NextAttack\", present iff effectType =
        // Crit", and §1 item 6 rejects a missing required extra member. A Crit with
        // no valid scope is therefore a read failure, not a defaulted NextAttack.
        var exception = Assert.ThrowsAny<ArgumentException>(
            () => CardEffectDefinition.FromPersistedPayload(payload));

        Assert.NotNull(exception);
    }

    [Theory]
    // An extra member on an effect that defines none.
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": 20, \"duration\": 2 }")]
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": 20, \"scope\": \"NextAttack\" }")]
    [InlineData("{ \"effectType\": \"Damage\", \"valueType\": \"Flat\", \"value\": 100, \"duration\": 2 }")]
    [InlineData("{ \"effectType\": \"Power\", \"valueType\": \"Flat\", \"value\": 25, \"scope\": \"NextAttack\" }")]
    // The two extra members swapped between the effects that define them.
    [InlineData("{ \"effectType\": \"Burn\", \"valueType\": \"Flat\", \"value\": 50, \"duration\": 2, \"scope\": \"NextAttack\" }")]
    [InlineData("{ \"effectType\": \"Crit\", \"valueType\": \"PercentagePoints\", \"value\": 10, \"duration\": 2, \"scope\": \"NextAttack\" }")]
    // A percentage-point interpretation on an effect whose document states none.
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"PercentagePoints\", \"value\": 20 }")]
    [InlineData("{ \"effectType\": \"Burn\", \"valueType\": \"PercentagePoints\", \"value\": 50, \"duration\": 2 }")]
    // A Crit whose unit is not percentage points.
    [InlineData("{ \"effectType\": \"Crit\", \"valueType\": \"Flat\", \"value\": 10, \"scope\": \"NextAttack\" }")]
    [InlineData("{ \"effectType\": \"Crit\", \"valueType\": \"PercentMaxHp\", \"value\": 10, \"scope\": \"NextAttack\" }")]
    public void FromPersistedPayload_ShouldEnforceEveryPresentIffCondition(string payload)
    {
        // DATABASE.md §3 states each extra member's condition in BOTH directions:
        // `duration` exists only on a Burn, `scope` only on a Crit. An element that
        // carries one where the contract defines none is malformed, and §1 item 6
        // rejects a malformed effect rather than ignoring what it does not define.
        //
        // The interpretation is likewise tied to the document that states it:
        // `PercentagePoints` exists because CARD_RULES.md §4.1 states the Crit
        // increase in percentage points, so pairing it with another effect would
        // assert a unit no document authors for that effect.
        Assert.ThrowsAny<ArgumentException>(
            () => CardEffectDefinition.FromPersistedPayload(payload));
    }

    [Fact]
    public void FromPersistedPayload_ShouldAcceptEveryDocumentedShapeAndRejectItsNeighbours()
    {
        // The accept/reject boundary stated in one place, so both directions are
        // asserted rather than only the failures.
        foreach (var payload in new[]
                 {
                     """{ "effectType": "Heal", "valueType": "PercentMaxHp", "value": 20 }""",
                     """{ "effectType": "Shield", "valueType": "PercentMaxHp", "value": 20 }""",
                     """{ "effectType": "Power", "valueType": "Flat", "value": 25 }""",
                     """{ "effectType": "Damage", "valueType": "Flat", "value": 100 }""",
                     """{ "effectType": "Burn", "valueType": "Flat", "value": 50, "duration": 2 }""",
                     """{ "effectType": "Crit", "valueType": "PercentagePoints", "value": 10, "scope": "NextAttack" }""",
                     """{ "effectType": "Damage", "valueType": "Undetermined" }""",
                 })
        {
            Assert.True(
                CardEffectDefinition.TryFromPersistedPayload(payload, out _),
                $"'{payload}' must be accepted.");
        }
    }

    // -----------------------------------------------------------------------
    // The non-throwing probe reaches the same verdict
    // -----------------------------------------------------------------------

    [Fact]
    public void TryFromPersistedPayload_ShouldAgreeWithTheThrowingReader()
    {
        // The probe defines no rule of its own; it must accept exactly what the
        // reader accepts and reject exactly what the reader rejects.
        Assert.True(CardEffectDefinition.TryFromPersistedPayload(
            """{ "effectType": "Power", "valueType": "Flat", "value": 25 }""",
            out var accepted));

        Assert.Equal(CardEffectType.Power, accepted.EffectType);
        Assert.Equal(CardEffectValueType.Flat, accepted.ValueType);
        Assert.Equal(25, accepted.Value);

        Assert.False(CardEffectDefinition.TryFromPersistedPayload(null, out _));
        Assert.False(CardEffectDefinition.TryFromPersistedPayload("effect", out _));
        Assert.False(CardEffectDefinition.TryFromPersistedPayload(
            """{ "effectType": "Nope", "valueType": "Flat", "value": 25 }""",
            out _));
    }

    // -----------------------------------------------------------------------
    // The value is the EFFECT magnitude, not the Card's Cost
    // -----------------------------------------------------------------------

    [Fact]
    public void TheEffectValue_ShouldNotBeConflatedWithThePowerChargeCost()
    {
        // CARD_RULES.md §2 item 3: Power Charge costs 0 Power by design while its
        // effect grants 25 Power. The structured value is the EFFECT magnitude and
        // must be positive even though the Cost is 0; the Cost is the separate
        // PowerCost column on the definition.
        var powerCharge = new CardDefinition
        {
            CardDefinitionId = "card-power-charge",
            Name = "Power Charge",
            Category = CardCategory.Basic,
            PowerCost = 0,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(
                    CardEffectType.Power,
                    CardEffectValueType.Flat,
                    25)),
            LoadoutCopyLimit = 1,
        };

        Assert.Equal(0, powerCharge.PowerCost);
        Assert.Equal(25, powerCharge.EffectDefinition[0].Value);
        Assert.Equal(CardEffectType.Power, powerCharge.EffectDefinition[0].EffectType);
    }

    // -----------------------------------------------------------------------
    // The definition carries the structured value — one concept, one owner
    // -----------------------------------------------------------------------

    [Fact]
    public void CardDefinition_ShouldCarryTheStructuredEffectArrayOnTheDocumentedMember()
    {
        // GAME_STATE.md §0 item 5: no parallel representation. The effects live on
        // CardDefinition.EffectDefinition and nowhere else; there is exactly one
        // member whose name mentions an effect.
        var effectMembers = typeof(CardDefinition)
            .GetProperties()
            .Where(p => p.Name.Contains("Effect", StringComparison.Ordinal))
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(new[] { "EffectDefinition" }, effectMembers);
        Assert.Equal(typeof(CardEffectDefinitions), typeof(CardDefinition)
            .GetProperty(nameof(CardDefinition.EffectDefinition))!
            .PropertyType);
    }

    [Fact]
    public void CardEffectDefinition_ShouldExposeExactlyTheDocumentedMembers()
    {
        // DATABASE.md §1: exactly `effectType`, `valueType`, and `value` — member
        // names fixed by TASK-108 D-2 — plus the effect-specific `duration` and
        // `scope` TASK-111 D-3 defines, plus the read-only `HasValue` projection
        // that distinguishes "magnitude authored" from "magnitude undetermined"
        // without a second stored member. No discriminator, no target, no handler,
        // and no registry member may appear, because the contract defines none.
        var members = typeof(CardEffectDefinition)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "Duration", "EffectType", "HasValue", "Scope", "Value", "ValueType" },
            members);

        // `HasValue` is a projection of `Value`, not a stored member: it has no
        // setter, so it cannot become a parallel representation of the same fact
        // (GAME_STATE.md §0 item 5).
        Assert.False(typeof(CardEffectDefinition).GetProperty("HasValue")!.CanWrite);
    }

    [Fact]
    public void CardEffectDefinition_ShouldIntroduceNoEffectExecutionSurface()
    {
        // This type is data and validation only. An effect framework, a handler
        // registry, a resolver entry point, or any mutate operation would cross
        // into CardCast / PetSkillCast scope (ARCHITECTURE.md §5, AGENTS.md §9).
        // No Crit roll and no Burn tick exists here either: neither is implemented
        // anywhere in MVP.
        var declaredMethods = typeof(CardEffectDefinition)
            .GetMethods(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Distinct()
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "Apply", "ApplyHeal", "ApplyShield", "ApplyPower",
                     "ApplyDamage", "ApplyBurn", "ApplyCrit", "Roll", "RollCrit",
                     "Tick", "TickBurn", "Execute", "Resolve", "Dispatch", "Handle",
                     "Invoke", "Register", "ResolveEffect", "Cast",
                 })
        {
            Assert.DoesNotContain(forbidden, declaredMethods);
        }
    }

    [Fact]
    public void CardEffectDefinition_ShouldCarryNoCardSpecificOrRuntimeState()
    {
        // The effect is content, so it must carry no Card identity, no target, and
        // no runtime/battle value: identifying which Card owns it is the definition
        // row's job, and targeting is the resolver's (D-1 forbids deriving behaviour
        // from the Card identity). `RemainingTurns` in particular would be a
        // battle-state member (GAME_STATE.md §2.3.1), not a definition member —
        // `duration` is the authored rule, not a live countdown.
        var members = typeof(CardEffectDefinition).GetProperties().Select(p => p.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "CardDefinitionId", "CardId", "Name", "Category",
                     "PowerCost", "Target", "TargetStat", "RemainingTurns",
                     "Source", "MaxHp", "PetId", "BattleId", "PassiveId",
                 })
        {
            Assert.DoesNotContain(forbidden, members);
        }
    }

    [Fact]
    public void CardEffectDefinition_ShouldBeAFrameworkIndependentValue()
    {
        // ARCHITECTURE.md §2.1: the Domain references no EF Core, ASP.NET Core,
        // Redis, SignalR, HTTP, Phaser, or Discord concern. It is a value type, so
        // two equal rules are interchangeable.
        Assert.True(typeof(CardEffectDefinition).IsValueType);
        Assert.True(typeof(CardEffectDefinitions).IsValueType);

        var referenced = typeof(CardEffectDefinition)
            .Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "Microsoft.EntityFrameworkCore",
                     "Microsoft.AspNetCore",
                     "StackExchange.Redis",
                     "Microsoft.AspNetCore.SignalR",
                     "Npgsql",
                 })
        {
            Assert.DoesNotContain(
                referenced,
                name => name.StartsWith(forbidden, StringComparison.Ordinal));
        }
    }
}
