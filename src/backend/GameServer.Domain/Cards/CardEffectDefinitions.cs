using System.Text.Json;

namespace GameServer.Domain.Cards;

/// <summary>
/// The stored value of <see cref="CardDefinition.EffectDefinition"/> — a Card's
/// complete effect rule as an <b>array of effect objects</b>, one element per
/// effect (<c>DATABASE.md</c> §1 item 1; TASK-111 decision D-1).
///
/// <code>
/// CardDefinition.EffectDefinition
///     ↓  always an array
/// [ { "effectType": "Heal", "valueType": "PercentMaxHp", "value": 20 } ]
/// </code>
///
/// <b>One shape, uniformly.</b> Per TASK-111 <b>D-1b</b> the shape is an array
/// <i>even for a Card with a single effect</i>: a one-effect Card stores a
/// one-element array, and a multi-effect Card stores one element per effect.
/// There is deliberately no second, single-object representation to fall back to
/// or interoperate with, and no arity-dependent type — the whole contract is this
/// one collection.
///
/// <b>Why an array rather than a single object.</b> Every Pet Skill Card in
/// <c>CARD_RULES.md</c> §4.1 states two effects (Inferno deals damage and applies
/// Burn; Tidal Barrier heals and shields; Iron Fang deals damage and raises Crit
/// chance), and nothing in §2/§4.1 restricts a future Card to one. Decision D-1
/// therefore supersedes the single-object shape TASK-109 landed, and per its own
/// note the two cannot both hold. This type carries no compatibility reader for
/// the superseded shape: <c>DATABASE.md</c> §1 item 6 states one well-formed
/// contract, so accepting a second shape would defeat the loud rejection it
/// requires.
///
/// <b>Array order carries no gameplay meaning.</b> Per TASK-111 <b>D-5</b> the
/// sequence is a <i>storage</i> sequence only: no rule reads element positions,
/// no effect resolves before or after another because of its index, and a
/// serializer must not imply otherwise (<c>DATABASE.md</c> §1 item 3). This type
/// therefore <b>preserves the stored order exactly</b> and never sorts, ranks,
/// reorders, or deduplicates: reordering would be a second, undocumented claim
/// about the data, and the contract defines no canonical order (the same
/// non-semantic convention <c>GAME_STATE.md</c> §2.3.1 item 10 records for
/// <c>StatusEffects[]</c>). Nothing here resolves, applies, or dispatches on an
/// effect.
///
/// <b>Reading fails loudly.</b> The reader rejects a non-array, an empty array, a
/// malformed element, an unknown <c>effectType</c>/<c>valueType</c>, a missing
/// magnitude, a missing required extra member, and the superseded single-object
/// shape — each at the read, with no fallback, default, or silent no-op
/// (<c>DATABASE.md</c> §1 item 6). An empty array is rejected rather than treated
/// as "no effects": every provisioned Card states at least one effect, so an
/// empty array is a definition this contract cannot express rather than a
/// Card that does nothing.
///
/// <b>It is empty-or-more only as a container.</b> The collection is a plain
/// validated wrapper — no interface, no repository, no factory, no LINQ surface
/// beyond <see cref="Count"/>/indexing that a caller needs to read an element
/// (<c>ARCHITECTURE.md</c> §5, <c>AGENTS.md</c> §9).
/// </summary>
public readonly struct CardEffectDefinitions
    : IEquatable<CardEffectDefinitions>
{
    private readonly CardEffectDefinition[]? _effects;

    private CardEffectDefinitions(CardEffectDefinition[] effects)
    {
        _effects = effects;
    }

    /// <summary>
    /// The stored elements, in their stored order. Never <c>null</c>; empty only
    /// for <c>default</c>, which the reader and writer both reject.
    /// </summary>
    private CardEffectDefinition[] Effects => _effects ?? [];

    /// <summary>
    /// The number of effects this Card applies. Always at least one for a value
    /// built through <see cref="Create"/> or read from a persisted payload.
    /// </summary>
    public int Count => Effects.Length;

    /// <summary>
    /// The effect at <paramref name="index"/>, in stored order.
    ///
    /// Position is a storage detail: <c>DATABASE.md</c> §1 item 3 states no rule
    /// reads it, so a caller must not treat the index as a resolution step. It is
    /// exposed only so an element can be read at all.
    /// </summary>
    public CardEffectDefinition this[int index] => Effects[index];

    /// <summary>
    /// Builds a Card effect array from one or more explicitly stated effects.
    ///
    /// <b>The order given is the order stored.</b> No sorting, grouping, or
    /// deduplication is applied — the contract defines no canonical order, so
    /// imposing one would be an undocumented rule (D-5).
    /// </summary>
    /// <param name="effects">
    /// The Card's effects, at least one. Every element must already satisfy
    /// <c>DATABASE.md</c> §1/§3, which the element type enforces; an element that
    /// is <c>default</c> (no effect identity) is rejected here.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="effects"/> is null, empty, or contains a <c>default</c>
    /// element — a Card whose effect rule states nothing is not a representable
    /// definition (<c>DATABASE.md</c> §1: the column is NOT NULL and every Card
    /// carries its effects).
    /// </exception>
    public static CardEffectDefinitions Create(params CardEffectDefinition[] effects)
    {
        if (effects is null || effects.Length == 0)
        {
            throw new ArgumentException(
                "A Card effect definition must state at least one effect. DATABASE.md §1 "
                + "stores the Card's effects as a non-empty array of effect objects, and "
                + "CARD_RULES.md §2/§4.1 gives every MVP Card at least one effect; an empty "
                + "array is a definition no document authors rather than a Card that does "
                + "nothing (AGENTS.md §7).",
                nameof(effects));
        }

        foreach (var effect in effects)
        {
            // `default(CardEffectDefinition)` is not an effect: it names Heal only
            // because that member is ordinal 0, carries no magnitude, and was never
            // built by a factory. Because Heal happens to be a defined member, the
            // unset value is not detectable from EffectType alone — so the guard is
            // the magnitude's absence, which no factory can produce for Heal
            // (Create requires a positive value; Undetermined pairs with
            // CardEffectValueType.Undetermined, not the default Flat).
            if (effect.ValueType != CardEffectValueType.Undetermined && effect.Value is null)
            {
                throw new ArgumentException(
                    "A Card effect definition cannot contain an unset element; "
                    + "default(CardEffectDefinition) carries no value and was produced by "
                    + "no factory, so storing it would assert an effect the contract does "
                    + "not define (AGENTS.md §7).",
                    nameof(effects));
            }

            if (!CardEffectDefinition.IsDefinedEffectType(effect.EffectType)
                || !CardEffectDefinition.IsDefinedValueType(effect.ValueType))
            {
                throw new ArgumentException(
                    "A Card effect definition cannot contain an element naming an effect "
                    + "identity or value interpretation this contract does not define "
                    + "(DATABASE.md §1 item 1, §1 item 6).",
                    nameof(effects));
            }
        }

        // Defensive copy: the stored sequence is the contract's, so a later
        // mutation of the caller's array must not change it.
        return new CardEffectDefinitions((CardEffectDefinition[])effects.Clone());
    }

    /// <summary>
    /// Writes the whole stored value — the JSON <b>array</b> of effect objects
    /// <c>DATABASE.md</c> §1 defines, with each element written by
    /// <see cref="CardEffectDefinition.ToPersistedPayload"/>.
    ///
    /// <code>
    /// [{"effectType":"Heal","valueType":"PercentMaxHp","value":20}]
    /// [{"effectType":"Damage","valueType":"Flat","value":100},{"effectType":"Burn","valueType":"Flat","value":50,"duration":2}]
    /// </code>
    ///
    /// <b>The element order is the stored order, and it is not normalized.</b> The
    /// same effects in a different order produce a different payload, because the
    /// contract fixes no canonical order and this writer may not invent one
    /// (D-5). The output is otherwise deterministic and canonical
    /// (<c>TDD.md</c> §6): a fixed member order per element, no whitespace, and no
    /// indentation.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The collection is <c>default</c> (no elements), or an element is not
    /// writable under <c>DATABASE.md</c> §1/§3.
    /// </exception>
    public string ToPersistedPayload()
    {
        var effects = Effects;

        if (effects.Length == 0)
        {
            throw new ArgumentException(
                "The Card effect definition holds no effects and cannot be written. "
                + "DATABASE.md §1 stores a non-empty array of effect objects; "
                + "default(CardEffectDefinitions) is not a definition.",
                nameof(effects));
        }

        // Hand-built rather than serializer-produced so the element payloads are
        // exactly the ones ToPersistedPayload emits — no second encoding of the
        // same contract (GAME_STATE.md §0 item 5), no whitespace, no indentation.
        return string.Concat(
            "[",
            string.Join(",", effects.Select(effect => effect.ToPersistedPayload())),
            "]");
    }

    /// <summary>
    /// Reads the stored <see cref="CardDefinition.EffectDefinition"/> column
    /// value back into a Card's effect array.
    ///
    /// <b>It accepts the contract's array shape only.</b> A valid array, a
    /// one-element array, and a multi-element array are all accepted; everything
    /// else is rejected loudly:
    /// <list type="bullet">
    /// <item>a null, empty, or whitespace value;</item>
    /// <item>text that is not JSON, or is truncated;</item>
    /// <item>anything that is not a JSON array — including the superseded
    /// single-object shape TASK-109 wrote, which <c>DATABASE.md</c> §1 no longer
    /// defines and for which no compatibility reader exists;</item>
    /// <item>an empty array, which states no effect;</item>
    /// <item>an element that is not a well-formed effect object, names an
    /// unrecognized <c>effectType</c> or <c>valueType</c>, omits a required
    /// magnitude, or omits <c>duration</c> on a Burn / <c>scope</c> on a Crit.
    /// </item>
    /// </list>
    ///
    /// There is no fallback magnitude, no default element, no prose fallback, no
    /// inference from a Card's name or <c>CardDefinitionId</c>, no conversion of an
    /// unknown value into <see cref="CardEffectValueType.Undetermined"/>, and no
    /// silent no-op (<c>DATABASE.md</c> §1 item 6). A malformed or truncated column
    /// value therefore surfaces as a failure at the read.
    /// </summary>
    /// <param name="payload">
    /// The stored JSON array, e.g.
    /// <c>[ { "effectType": "Shield", "valueType": "PercentMaxHp", "value": 20 } ]</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="payload"/> is null, empty, whitespace, malformed, not an
    /// array, an empty array, or contains an element the element reader rejects.
    /// </exception>
    public static CardEffectDefinitions FromPersistedPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException(
                "A Card effect payload is required (DATABASE.md §1); an absent or empty "
                + "value is not a default effect array.",
                nameof(payload));
        }

        JsonElement[] elements;

        try
        {
            using var document = JsonDocument.Parse(payload, ParseOptions);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException(
                    $"The stored Card effect payload is {DescribeKind(root.ValueKind)}, not "
                    + "an array. DATABASE.md §1 item 1 stores the Card's effects as an "
                    + "ARRAY of effect objects — the superseded single-object shape is not "
                    + "part of this contract, and no compatibility reader for it exists, so "
                    + "it is rejected rather than silently accepted.",
                    nameof(payload));
            }

            // The elements are captured before the document is disposed, so each
            // one's own text can be handed to the element reader for a single,
            // shared implementation of the member rules.
            elements = root.EnumerateArray().Select(element => element.Clone()).ToArray();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                $"The stored Card effect payload is not valid JSON ({exception.Message}). "
                + "DATABASE.md §1 stores an array of structured effect objects; a "
                + "malformed or truncated value is rejected rather than defaulted.",
                nameof(payload),
                exception);
        }

        if (elements.Length == 0)
        {
            throw new ArgumentException(
                "The stored Card effect payload is an empty array. DATABASE.md §1 item 1 "
                + "stores one element per effect and every MVP Card states at least one "
                + "effect (CARD_RULES.md §2/§4.1), so an empty array is a definition the "
                + "contract cannot express rather than a Card that does nothing.",
                nameof(payload));
        }

        var effects = new CardEffectDefinition[elements.Length];

        for (var index = 0; index < elements.Length; index++)
        {
            // The element reader owns the per-member rules, so every element is
            // validated by exactly one implementation (GAME_STATE.md §0 item 5) —
            // including its unknown-member, unknown-token, missing-magnitude, and
            // present-iff checks.
            effects[index] = CardEffectDefinition.FromPersistedPayload(
                elements[index].GetRawText());
        }

        return new CardEffectDefinitions(effects);
    }

    /// <summary>
    /// Whether <paramref name="payload"/> is a well-formed Card effect array —
    /// the non-throwing counterpart of <see cref="FromPersistedPayload"/>. It
    /// defines no rule of its own and delegates to the reader.
    /// </summary>
    public static bool TryFromPersistedPayload(
        string? payload,
        out CardEffectDefinitions effects)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            effects = default;
            return false;
        }

        try
        {
            effects = FromPersistedPayload(payload);
            return true;
        }
        catch (ArgumentException)
        {
            effects = default;
            return false;
        }
    }

    private static string DescribeKind(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Object => "a JSON object",
        JsonValueKind.String => "a JSON string",
        JsonValueKind.Number => "a JSON number",
        JsonValueKind.True or JsonValueKind.False => "a JSON boolean",
        JsonValueKind.Null => "JSON null",
        _ => "not a JSON array",
    };

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        // A comment or trailing comma would make one stored value readable by
        // two different parsers; the stored contract is plain JSON.
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>
    /// Element-wise equality in <b>stored order</b>, which is what makes the
    /// round-trip lossless: a definition read back from its own payload compares
    /// equal to the original, and two definitions that differ in element order are
    /// not equal. That is a statement about stored bytes only — the order still
    /// carries no gameplay meaning (D-5), and nothing resolves on it.
    /// </summary>
    public bool Equals(CardEffectDefinitions other)
    {
        var left = Effects;
        var right = other.Effects;

        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is CardEffectDefinitions other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var effect in Effects)
        {
            hash.Add(effect);
        }

        return hash.ToHashCode();
    }

    /// <summary>Element-wise equality in stored order.</summary>
    public static bool operator ==(CardEffectDefinitions left, CardEffectDefinitions right) =>
        left.Equals(right);

    /// <summary>Element-wise inequality in stored order.</summary>
    public static bool operator !=(CardEffectDefinitions left, CardEffectDefinitions right) =>
        !left.Equals(right);
}
