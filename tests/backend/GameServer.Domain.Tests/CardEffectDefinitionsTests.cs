using GameServer.Domain.Cards;

namespace GameServer.Domain.Tests;

/// <summary>
/// The <b>array</b> half of the structured Card <c>EffectDefinition</c> contract
/// — <c>DATABASE.md</c> §1 items 1–3 and item 6, plus §3's present-iff
/// constraints (TASK-111 decisions D-1/D-1a/D-1b/D-5, implemented by TASK-112).
///
/// What is verified is the documented storage contract only: the uniform array
/// shape, one element per effect with each element keeping its own triple,
/// non-semantic element ordering that the serializer nevertheless preserves, the
/// strict loud rejection of every malformed form — including the superseded
/// TASK-109 single-object shape, for which no compatibility reader exists — and
/// the round trip for all six provisioned content rows.
///
/// <b>No test here applies an effect.</b> No Card is cast, no Crit is rolled, no
/// Burn is ticked, and no magnitude is used for anything: the values are the ones
/// <c>CARD_RULES.md</c> §2/§4.1 states, asserted as stored data.
/// </summary>
public class CardEffectDefinitionsTests
{
    // -----------------------------------------------------------------------
    // The shape is always an array — DATABASE.md §1 item 1, TASK-111 D-1b
    // -----------------------------------------------------------------------

    [Fact]
    public void AOneEffectCard_ShouldStoreAOneElementArray()
    {
        // D-1b: "always an array, even with one effect". CARD_RULES.md §2's three
        // Basic Cards each state one effect, so each stores a one-element array —
        // there is no single-object form in this contract.
        var definition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(
                CardEffectType.Heal,
                CardEffectValueType.PercentMaxHp,
                20));

        Assert.Equal(1, definition.Count);
        Assert.Equal(CardEffectType.Heal, definition[0].EffectType);

        Assert.Equal(
            """[{"effectType":"Heal","valueType":"PercentMaxHp","value":20}]""",
            definition.ToPersistedPayload());
    }

    [Fact]
    public void AMultiEffectCard_ShouldStoreOneElementPerEffect()
    {
        // CARD_RULES.md §4.1's Pet Skill Cards each state two effects, so each
        // stores a two-element array — the shape the superseded single object could
        // not express (D-1).
        var definition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2));

        Assert.Equal(2, definition.Count);
        Assert.Equal(CardEffectType.Damage, definition[0].EffectType);
        Assert.Equal(CardEffectType.Burn, definition[1].EffectType);

        // Each element keeps its own triple (D-1a), and the array is the stored
        // value.
        Assert.Equal(100, definition[0].Value);
        Assert.Equal(50, definition[1].Value);
        Assert.Equal(2, definition[1].Duration);
    }

    [Fact]
    public void TheArrayShape_ShouldBeUniformRegardlessOfEffectCount()
    {
        // There is exactly one shape: one effect, two effects, and three or more
        // are all the same array of the same element type. No representation varies
        // with arity.
        var one = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25));
        var two = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20),
            CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20));
        var three = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20),
            CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20),
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25));

        Assert.Equal(1, one.Count);
        Assert.Equal(2, two.Count);
        Assert.Equal(3, three.Count);

        // All three serialize as JSON arrays, and every element is the same shape.
        foreach (var definition in new[] { one, two, three })
        {
            var payload = definition.ToPersistedPayload();

            Assert.StartsWith("[", payload, StringComparison.Ordinal);
            Assert.EndsWith("]", payload, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Create_ShouldRejectAnEmptyEffectSet()
    {
        // DATABASE.md §1 stores one element per effect and every MVP Card states at
        // least one (CARD_RULES.md §2/§4.1), so a Card whose rule states nothing is
        // not a representable definition. Rejecting is the loud behaviour; an empty
        // array must never be read as "a Card that does nothing".
        Assert.Throws<ArgumentException>(() => CardEffectDefinitions.Create());

        Assert.Throws<ArgumentException>(
            () => CardEffectDefinitions.Create(Array.Empty<CardEffectDefinition>()));
    }

    [Fact]
    public void Create_ShouldRejectAnUnsetElement()
    {
        // default(CardEffectDefinition) names no effect identity; storing it would
        // assert an effect the contract does not define (AGENTS.md §7).
        Assert.Throws<ArgumentException>(
            () => CardEffectDefinitions.Create(default(CardEffectDefinition)));
    }

    [Fact]
    public void DefaultInstance_ShouldNotBeAValidDefinition()
    {
        // default(CardEffectDefinitions) holds no elements, so it is not a Card's
        // effect rule and must not be written as one.
        CardEffectDefinitions unset = default;

        Assert.Equal(0, unset.Count);
        Assert.ThrowsAny<ArgumentException>(() => unset.ToPersistedPayload());
    }

    // -----------------------------------------------------------------------
    // Element order — stored, preserved, and NOT semantic
    // -----------------------------------------------------------------------

    [Fact]
    public void ElementOrder_ShouldBePreservedExactlyAsStored()
    {
        // DATABASE.md §1 item 3 / TASK-111 D-5: the sequence is a STORAGE sequence
        // only. The contract fixes no canonical order, so the serializer must not
        // sort or normalize — it preserves what it was given, which is what makes
        // the round trip lossless.
        var damageThenBurn = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2));

        var burnThenDamage = CardEffectDefinitions.Create(
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2),
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100));

        Assert.Equal(CardEffectType.Damage, damageThenBurn[0].EffectType);
        Assert.Equal(CardEffectType.Burn, damageThenBurn[1].EffectType);
        Assert.Equal(CardEffectType.Burn, burnThenDamage[0].EffectType);
        Assert.Equal(CardEffectType.Damage, burnThenDamage[1].EffectType);

        // Different stored orders are different stored values — no canonical
        // sorting rule was invented to make them equal.
        Assert.NotEqual(damageThenBurn, burnThenDamage);
        Assert.NotEqual(
            damageThenBurn.ToPersistedPayload(),
            burnThenDamage.ToPersistedPayload());
    }

    [Fact]
    public void ElementOrder_ShouldNotChangeWhichEffectsTheCardApplies()
    {
        // The non-semantics stated as a test: both stored orders hold the same
        // effects, so a reader that treats the array as a SET of effects (which is
        // what any future resolver must do, because position is not a rule) sees
        // the same Card. Nothing in this contract reads an index as a resolution
        // step.
        var forward = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2));

        var reversed = CardEffectDefinitions.Create(
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2),
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100));

        static IEnumerable<CardEffectDefinition> AsSet(CardEffectDefinitions effects) =>
            Enumerable.Range(0, effects.Count).Select(index => effects[index]);

        Assert.Equal(
            AsSet(forward).OrderBy(effect => effect.EffectType),
            AsSet(reversed).OrderBy(effect => effect.EffectType));

        // And neither order is privileged by any member the type exposes.
        var members = typeof(CardEffectDefinitions)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "Primary", "Priority", "Order", "Sequence", "First",
                     "Next", "Resolve",
                 })
        {
            Assert.DoesNotContain(forbidden, members);
        }
    }

    // -----------------------------------------------------------------------
    // Serialization — deterministic, and canonical per element
    // -----------------------------------------------------------------------

    [Fact]
    public void ToPersistedPayload_ShouldProduceTheDocumentedArray()
    {
        // DATABASE.md §1 item 1's example, in the canonical member order.
        var definition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(
                CardEffectType.Shield,
                CardEffectValueType.PercentMaxHp,
                20));

        Assert.Equal(
            """[{"effectType":"Shield","valueType":"PercentMaxHp","value":20}]""",
            definition.ToPersistedPayload());
    }

    [Fact]
    public void ToPersistedPayload_ShouldBeDeterministic()
    {
        // The same rule always produces the same bytes (TDD.md §6).
        var definition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 120),
            CardEffectDefinition.Crit(10, CardEffectDefinition.NextAttackScope));

        var first = definition.ToPersistedPayload();
        var second = definition.ToPersistedPayload();

        Assert.Equal(first, second);

        Assert.Equal(
            """[{"effectType":"Damage","valueType":"Flat","value":120},{"effectType":"Crit","valueType":"PercentagePoints","value":10,"scope":"NextAttack"}]""",
            first);
    }

    [Fact]
    public void Elements_ShouldSerializeAsTheirOwnObjects()
    {
        // Each element is one object carrying its own triple and its own extra
        // members (D-1a/D-3) — never flattened into the parent, and never shared
        // between elements.
        var definition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2));

        Assert.Equal(
            """[{"effectType":"Damage","valueType":"Flat","value":100},{"effectType":"Burn","valueType":"Flat","value":50,"duration":2}]""",
            definition.ToPersistedPayload());
    }

    [Fact]
    public void ElementsDifferingOnlyInEffectType_ShouldRemainDistinctInTheArray()
    {
        // A damage effect and a Power Charge effect can carry the same number; D-4
        // requires them to stay distinguishable, including inside one array.
        var definition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 100));

        Assert.NotEqual(definition[0], definition[1]);
        Assert.Equal(CardEffectType.Damage, definition[0].EffectType);
        Assert.Equal(CardEffectType.Power, definition[1].EffectType);
    }

    // -----------------------------------------------------------------------
    // Reading — valid arrays, in every documented shape
    // -----------------------------------------------------------------------

    [Fact]
    public void FromPersistedPayload_ShouldReadAOneElementArray()
    {
        var definition = CardEffectDefinitions.FromPersistedPayload(
            """[ { "effectType": "Heal", "valueType": "PercentMaxHp", "value": 20 } ]""");

        Assert.Equal(1, definition.Count);
        Assert.Equal(CardEffectType.Heal, definition[0].EffectType);
        Assert.Equal(20, definition[0].Value);
    }

    [Fact]
    public void FromPersistedPayload_ShouldReadAMultiElementArray()
    {
        var definition = CardEffectDefinitions.FromPersistedPayload(
            """
            [
              { "effectType": "Damage", "valueType": "Flat", "value": 100 },
              { "effectType": "Burn", "valueType": "Flat", "value": 50, "duration": 2 }
            ]
            """);

        Assert.Equal(2, definition.Count);
        Assert.Equal(CardEffectType.Damage, definition[0].EffectType);
        Assert.Equal(CardEffectType.Burn, definition[1].EffectType);
        Assert.Equal(2, definition[1].Duration);
    }

    [Fact]
    public void FromPersistedPayload_ShouldPreserveTheStoredElementOrder()
    {
        // The reader keeps the stored sequence; it applies no canonical sort.
        var definition = CardEffectDefinitions.FromPersistedPayload(
            """
            [ { "effectType": "Crit", "valueType": "PercentagePoints", "value": 10, "scope": "NextAttack" },
              { "effectType": "Damage", "valueType": "Flat", "value": 120 } ]
            """);

        Assert.Equal(CardEffectType.Crit, definition[0].EffectType);
        Assert.Equal(CardEffectType.Damage, definition[1].EffectType);
    }

    [Fact]
    public void FromPersistedPayload_ShouldAcceptUndeterminedWhereTheContractPermitsIt()
    {
        // DATABASE.md §1 item 9 / TASK-111 D-3: `Undetermined` remains a valid
        // member for an effect whose authored magnitude is not yet determined. No
        // provisioned content row is in that state after TASK-112, but the contract
        // must still represent such an effect rather than force an invented number.
        var definition = CardEffectDefinitions.FromPersistedPayload(
            """[ { "effectType": "Damage", "valueType": "Undetermined" } ]""");

        Assert.Equal(1, definition.Count);
        Assert.Equal(CardEffectType.Damage, definition[0].EffectType);
        Assert.Equal(CardEffectValueType.Undetermined, definition[0].ValueType);
        Assert.False(definition[0].HasValue);
        Assert.Null(definition[0].Value);
    }

    [Fact]
    public void TryFromPersistedPayload_ShouldAgreeWithTheThrowingReader()
    {
        // The probe defines no rule of its own.
        Assert.True(CardEffectDefinitions.TryFromPersistedPayload(
            """[ { "effectType": "Power", "valueType": "Flat", "value": 25 } ]""",
            out var accepted));

        Assert.Equal(1, accepted.Count);
        Assert.Equal(CardEffectType.Power, accepted[0].EffectType);

        Assert.False(CardEffectDefinitions.TryFromPersistedPayload(null, out _));
        Assert.False(CardEffectDefinitions.TryFromPersistedPayload("effect", out _));
        Assert.False(CardEffectDefinitions.TryFromPersistedPayload(
            """{ "effectType": "Power", "valueType": "Flat", "value": 25 }""",
            out _));
    }

    // -----------------------------------------------------------------------
    // Reading — every malformed form is rejected, never defaulted
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("effect")]
    [InlineData("Restore the active Pet's HP by 20% of its Max HP")]
    [InlineData("[")]
    [InlineData("[ { \"effectType\": \"Heal\"")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"Heal\"")]
    [InlineData("true")]
    public void FromPersistedPayload_ShouldRejectAnythingThatIsNotAWellFormedArray(string payload)
    {
        // DATABASE.md §1 item 6: a stored value that is not a well-formed structured
        // effect is rejected loudly, with no fallback. A malformed or truncated
        // column value surfaces as a failure at the read rather than as a Card that
        // quietly does nothing.
        Assert.ThrowsAny<ArgumentException>(
            () => CardEffectDefinitions.FromPersistedPayload(payload));
    }

    [Theory]
    // The superseded TASK-109 single-object shape, which this contract no longer
    // defines. No compatibility reader exists, so it is rejected.
    [InlineData("{ \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": 20 }")]
    [InlineData("{ \"effectType\": \"Power\", \"valueType\": \"Undetermined\" }")]
    [InlineData("{ \"effectType\": \"Shield\", \"valueType\": \"PercentMaxHp\", \"value\": 20 }")]
    public void FromPersistedPayload_ShouldRejectTheSupersededSingleObjectShape(string payload)
    {
        // TASK-111 D-1 replaced the single object with the array, and its own note
        // records that the two cannot both hold. DATABASE.md §1 item 6 requires ONE
        // well-formed contract, so accepting the old shape would defeat the loud
        // rejection — and a "reader that accepted both" is exactly what TASK-112's
        // Implementation Notes forbid.
        var exception = Assert.ThrowsAny<ArgumentException>(
            () => CardEffectDefinitions.FromPersistedPayload(payload));

        Assert.Contains("array", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    // A malformed element inside an otherwise valid array.
    [InlineData("[ { \"effectType\": \"Heal\" } ]")]
    [InlineData("[ { \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\" } ]")]
    [InlineData("[ { \"valueType\": \"PercentMaxHp\", \"value\": 20 } ]")]
    [InlineData("[ { \"effectType\": \"Nope\", \"valueType\": \"Flat\", \"value\": 20 } ]")]
    [InlineData("[ { \"effectType\": \"Heal\", \"valueType\": \"Nope\", \"value\": 20 } ]")]
    [InlineData("[ { \"effectType\": \"heal\", \"valueType\": \"Flat\", \"value\": 20 } ]")]
    [InlineData("[ { \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": 0 } ]")]
    [InlineData("[ { \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": \"20\" } ]")]
    [InlineData("[ { \"effectType\": 0, \"valueType\": 0, \"value\": 20 } ]")]
    [InlineData("[ { \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": 20, \"extra\": 1 } ]")]
    [InlineData("[ null ]")]
    [InlineData("[ 20 ]")]
    [InlineData("[ \"Heal\" ]")]
    [InlineData("[ [] ]")]
    [InlineData("[ [ { \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": 20 } ] ]")]
    // A Burn with no duration, and a Crit with no scope — the missing required
    // extra members of §1 item 6.
    [InlineData("[ { \"effectType\": \"Burn\", \"valueType\": \"Flat\", \"value\": 50 } ]")]
    [InlineData("[ { \"effectType\": \"Burn\", \"valueType\": \"Flat\", \"value\": 50, \"duration\": 0 } ]")]
    [InlineData("[ { \"effectType\": \"Crit\", \"valueType\": \"PercentagePoints\", \"value\": 10 } ]")]
    [InlineData("[ { \"effectType\": \"Crit\", \"valueType\": \"PercentagePoints\", \"value\": 10, \"scope\": \"AllAttacks\" } ]")]
    // An extra member on an effect that defines none.
    [InlineData("[ { \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": 20, \"duration\": 2 } ]")]
    // One malformed element poisons the whole value rather than being skipped.
    [InlineData("[ { \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": 20 }, { \"effectType\": \"Nope\", \"valueType\": \"Flat\", \"value\": 20 } ]")]
    public void FromPersistedPayload_ShouldRejectAMalformedElementInAnArray(string payload)
    {
        // Every element is validated, and the first failure surfaces — no element is
        // skipped, defaulted, or replaced, and the valid ones do not make the value
        // acceptable (DATABASE.md §1 item 6).
        Assert.ThrowsAny<ArgumentException>(
            () => CardEffectDefinitions.FromPersistedPayload(payload));
    }

    [Fact]
    public void FromPersistedPayload_ShouldReportAnUnknownEffectTypeAtTheElement()
    {
        // A content typo must be diagnosable rather than silent.
        var exception = Assert.Throws<ArgumentException>(
            () => CardEffectDefinitions.FromPersistedPayload(
                "[ { \"effectType\": \"BurnDamage\", \"valueType\": \"Flat\", \"value\": 20 } ]"));

        Assert.Contains("BurnDamage", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromPersistedPayload_ShouldNotConvertAnUnknownValueTypeIntoUndetermined()
    {
        // The explicit anti-requirement: an unrecognized interpretation must never
        // be laundered into `Undetermined`. Doing so would turn "this magnitude's
        // unit is unknown" into "no magnitude is authored", which would then read as
        // a legitimate open content gap instead of a corrupt row.
        Assert.False(CardEffectDefinitions.TryFromPersistedPayload(
            "[ { \"effectType\": \"Heal\", \"valueType\": \"PerTick\", \"value\": 20 } ]",
            out _));

        Assert.ThrowsAny<ArgumentException>(
            () => CardEffectDefinitions.FromPersistedPayload(
                "[ { \"effectType\": \"Heal\", \"valueType\": \"PerTick\", \"value\": 20 } ]"));
    }

    [Fact]
    public void FromPersistedPayload_ShouldNotInferAnEffectFromAnythingButTheStoredMembers()
    {
        // D-1: the effect identity comes from the stored structured data — never
        // from a Card's name, its CardDefinitionId, or parsed prose. A payload that
        // names no effect is therefore rejected, however suggestive its other
        // content is; there is no name- or prose-based inference to fall back on.
        foreach (var payload in new[]
                 {
                     "[ { \"name\": \"Inferno\", \"valueType\": \"Flat\", \"value\": 100 } ]",
                     "[ { \"cardDefinitionId\": \"card-heal\", \"valueType\": \"Flat\", \"value\": 20 } ]",
                     "[ \"Restore the active Pet's HP by 20% of its Max HP\" ]",
                 })
        {
            Assert.False(
                CardEffectDefinitions.TryFromPersistedPayload(payload, out _),
                $"'{payload}' must be rejected: only the stored effectType identifies an effect.");
        }
    }

    // -----------------------------------------------------------------------
    // Round trips — the six provisioned content rows
    // -----------------------------------------------------------------------

    /// <summary>
    /// The six provisioned <c>CardDefinition</c> rows as <c>CARD_RULES.md</c>
    /// §2/§4.1 authors them, in their stored shape. The ids are TASK-109's Row
    /// Content Migration table's; the values are §2/§4.1's, transcribed.
    /// </summary>
    public static TheoryData<string, CardEffectDefinitions> ProvisionedContentRows => new()
    {
        {
            "card-heal",
            CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20))
        },
        {
            "card-shield",
            CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20))
        },
        {
            "card-power-charge",
            CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25))
        },
        {
            "card-inferno",
            CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
                CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2))
        },
        {
            "card-tidal-barrier",
            CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20),
                CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20))
        },
        {
            "card-iron-fang",
            CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 120),
                CardEffectDefinition.Crit(10, CardEffectDefinition.NextAttackScope))
        },
    };

    [Theory]
    [MemberData(nameof(ProvisionedContentRows))]
    public void RoundTrip_ShouldPreserveEveryProvisionedContentRow(
        string cardId,
        CardEffectDefinitions effects)
    {
        // DATABASE.md's row chain, for each of the six: the stored array reads back
        // into the domain, serializes to the same contract shape, and reads back
        // equal — losslessly, with order intact.
        Assert.NotEmpty(cardId);

        var payload = effects.ToPersistedPayload();
        var reloaded = CardEffectDefinitions.FromPersistedPayload(payload);

        Assert.Equal(effects, reloaded);
        Assert.Equal(effects.Count, reloaded.Count);
        Assert.Equal(payload, reloaded.ToPersistedPayload());

        // The encoded form is an array in every case, including the three
        // single-effect Basic Cards (D-1b).
        Assert.StartsWith("[", payload, StringComparison.Ordinal);
        Assert.EndsWith("]", payload, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ProvisionedContentRows))]
    public void ProvisionedContentRows_ShouldCarryNoUndeterminedMagnitude(
        string cardId,
        CardEffectDefinitions effects)
    {
        // TASK-112 retires the three `Undetermined` markers, because CARD_RULES.md
        // §4.1 now authors every Pet Skill magnitude (TASK-110). No provisioned
        // content row may remain in the old unauthored state, and no element may
        // carry a missing magnitude.
        Assert.NotEmpty(cardId);

        for (var index = 0; index < effects.Count; index++)
        {
            Assert.NotEqual(CardEffectValueType.Undetermined, effects[index].ValueType);
            Assert.True(effects[index].HasValue);
            Assert.NotNull(effects[index].Value);
        }
    }

    [Fact]
    public void ProvisionedContentRows_ShouldUseTheirOwnMagnitudesAndNeverInvent()
    {
        // The values below are CARD_RULES.md §2/§4.1's, asserted per row so an
        // encoded balance change fails here. They are transcribed, never computed:
        // §4.1's Tidal Barrier Shield is 20 (not the Shield Basic Card's value by
        // inheritance), Inferno's Burn is 50 per tick for 2 Turns exactly, and Iron
        // Fang's Crit is 10 percentage points.
        var inferno = ProvisionedContentRows.First(row => row[0] as string == "card-inferno")[1]
            as CardEffectDefinitions?;

        var tidal = ProvisionedContentRows.First(row => row[0] as string == "card-tidal-barrier")[1]
            as CardEffectDefinitions?;

        var ironFang = ProvisionedContentRows.First(row => row[0] as string == "card-iron-fang")[1]
            as CardEffectDefinitions?;

        Assert.NotNull(inferno);
        Assert.NotNull(tidal);
        Assert.NotNull(ironFang);

        // card-inferno — Damage 100 flat, then Burn 50/tick for 2 Turns (§4.1).
        Assert.Equal(2, inferno!.Value.Count);
        Assert.Equal(CardEffectType.Damage, inferno.Value[0].EffectType);
        Assert.Equal(CardEffectValueType.Flat, inferno.Value[0].ValueType);
        Assert.Equal(100, inferno.Value[0].Value);
        Assert.Equal(CardEffectType.Burn, inferno.Value[1].EffectType);
        Assert.Equal(50, inferno.Value[1].Value);
        Assert.Equal(2, inferno.Value[1].Duration);

        // card-tidal-barrier — Heal 20% Max HP, then Shield 20% Max HP (§4.1).
        Assert.Equal(2, tidal!.Value.Count);
        Assert.Equal(CardEffectType.Heal, tidal.Value[0].EffectType);
        Assert.Equal(20, tidal.Value[0].Value);
        Assert.Equal(CardEffectType.Shield, tidal.Value[1].EffectType);
        Assert.Equal(20, tidal.Value[1].Value);

        // card-iron-fang — Damage 120 flat, then Crit +10 percentage points
        // scoped to the next attack. Its damage identity is `Damage`, NOT `Power`
        // (DATABASE.md §1 item 8; TASK-111 Reported Discrepancy 3).
        Assert.Equal(2, ironFang!.Value.Count);
        Assert.Equal(CardEffectType.Damage, ironFang.Value[0].EffectType);
        Assert.NotEqual(CardEffectType.Power, ironFang.Value[0].EffectType);
        Assert.Equal(120, ironFang.Value[0].Value);
        Assert.Equal(CardEffectType.Crit, ironFang.Value[1].EffectType);
        Assert.Equal(CardEffectValueType.PercentagePoints, ironFang.Value[1].ValueType);
        Assert.Equal(10, ironFang.Value[1].Value);
        Assert.Equal("NextAttack", ironFang.Value[1].Scope);
    }

    [Fact]
    public void ProvisionedContentRows_ShouldCoverBothSingleAndMultiEffectCards()
    {
        // The three §2 Basic Cards are single-effect (one-element arrays) and the
        // three §4.1 Pet Skill Cards are two-effect (two-element arrays) — the two
        // shapes the contract must express, on real content.
        var counts = ProvisionedContentRows
            .Select(row => ((CardEffectDefinitions)row[1]!).Count)
            .ToArray();

        Assert.Equal(6, counts.Length);
        Assert.Equal(3, counts.Count(count => count == 1));
        Assert.Equal(3, counts.Count(count => count == 2));
    }
}
