using System.Text.Json;

namespace GameServer.Domain.Relics;

/// <summary>
/// The stored value of <see cref="RelicDefinition.EffectDefinition"/> — a
/// Relic's complete effect declaration as an <b>array of effect objects</b>, one
/// element per effect (<c>RELIC_RULES.md</c> §8.2; TASK-131 <b>D1</b>).
///
/// <code>
/// RelicDefinition.EffectDefinition
///     ↓  always an array
/// [ { "effectType": "ATK", "valueType": "Percentage", "value": 5,
///     "target": "Pet", "lifetime": "Battle" } ]
/// </code>
///
/// <b>Why an array rather than a single object.</b> §8.2 declares
/// "<c>EffectDefinition</c> is a <b>structured array</b> of effect objects",
/// following the contract shape <c>DATABASE.md</c> §1 records for
/// <c>CardDefinition.EffectDefinition</c>. A Relic that states one effect stores
/// a one-element array — which is every provisioned row in §8.5 — and a Relic
/// that states several stores one element per effect. Carrying the array shape
/// uniformly means no arity-dependent representation exists for a reader to
/// branch on.
///
/// <b>Array order carries no gameplay meaning.</b> §8.2 item 4 states it:
/// "Ordering within the array is NOT semantic ... No rule reads element
/// positions", and adds that a Relic's resolution order is §4's equip-slot order,
/// which is a property of the Relic sequence rather than of an effect's index
/// within one Relic. This type therefore <b>preserves the stored order exactly</b>
/// and never sorts, ranks, reorders, or deduplicates: reordering would be a
/// second, undocumented claim about the data, and the contract defines no
/// canonical order.
///
/// <b>Reading fails loudly.</b> The reader rejects a non-array, an empty array, a
/// malformed element, an unknown <c>effectType</c>/<c>valueType</c>/<c>target</c>/
/// <c>lifetime</c>, a missing magnitude, a missing required member, and a member
/// combination §8.3's table does not list — each at the read, with no fallback,
/// default, or silent no-op (§8.2 item 5). An empty array is rejected rather than
/// treated as "no effects": §8.5 gives every provisioned Relic at least one
/// effect, so an empty array is a definition this contract cannot express rather
/// than a Relic that does nothing.
///
/// <b>It is a plain validated wrapper</b> — no interface, no repository, no
/// factory, no LINQ surface beyond <see cref="Count"/>/indexing that a caller
/// needs to read an element (<c>ARCHITECTURE.md</c> §5, <c>AGENTS.md</c> §9).
///
/// <b>Nothing here resolves anything.</b> The collection is read and written as
/// data; no effect is applied, no trigger is evaluated, and no
/// <c>RelicTriggered</c> event is emitted (<c>RELIC_RULES.md</c> §8.7).
/// </summary>
public readonly struct RelicEffectDefinitions
    : IEquatable<RelicEffectDefinitions>
{
    private readonly RelicEffectDefinition[]? _effects;

    private RelicEffectDefinitions(RelicEffectDefinition[] effects)
    {
        _effects = effects;
    }

    /// <summary>
    /// The stored elements, in their stored order. Never <c>null</c>; empty only
    /// for <c>default</c>, which the reader and writer both reject.
    /// </summary>
    private RelicEffectDefinition[] Effects => _effects ?? [];

    /// <summary>
    /// The number of effects this Relic declares. Always at least one for a value
    /// built through <see cref="Create"/> or read from a persisted payload.
    /// </summary>
    public int Count => Effects.Length;

    /// <summary>
    /// The effect at <paramref name="index"/>, in stored order.
    ///
    /// Position is a storage detail: §8.2 item 4 states no rule reads it, so a
    /// caller must not treat the index as a resolution step. It is exposed only so
    /// an element can be read at all.
    /// </summary>
    public RelicEffectDefinition this[int index] => Effects[index];

    /// <summary>
    /// Builds a Relic effect array from one or more explicitly stated effects.
    ///
    /// <b>The order given is the order stored.</b> No sorting, grouping, or
    /// deduplication is applied — the contract defines no canonical order, so
    /// imposing one would be an undocumented rule (§8.2 item 4).
    /// </summary>
    /// <param name="effects">
    /// The Relic's effects, at least one. Every element must already satisfy
    /// §8.2/§8.3, which the element type enforces; an element that is
    /// <c>default</c> (no effect identity) is rejected here.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="effects"/> is null, empty, or contains a <c>default</c>
    /// element — a Relic whose effect rule states nothing is not a representable
    /// definition (<c>DATABASE.md</c> §1: the column is NOT NULL and every Relic
    /// carries its effects).
    /// </exception>
    public static RelicEffectDefinitions Create(params RelicEffectDefinition[] effects)
    {
        if (effects is null || effects.Length == 0)
        {
            throw new ArgumentException(
                "A Relic effect definition must state at least one effect. RELIC_RULES.md §8.2 "
                + "stores the Relic's effects as a structured array of effect objects and §8.5 "
                + "gives every provisioned Relic at least one effect; an empty array is a "
                + "definition no document authors rather than a Relic that does nothing "
                + "(AGENTS.md §7).",
                nameof(effects));
        }

        foreach (var effect in effects)
        {
            // `default(RelicEffectDefinition)` is not an effect: it names ATK only
            // because that member is ordinal 0, carries no magnitude, and was never
            // built by a factory. Because ATK happens to be a defined member, the
            // unset value is not detectable from EffectType alone — so the guards
            // are the magnitude's absence and the value type, neither of which a
            // factory can produce for a real ATK element (ATK requires Percentage
            // with a positive value).
            if (effect.ValueType != RelicEffectValueType.Undetermined && effect.Value is null)
            {
                throw new ArgumentException(
                    "A Relic effect definition cannot contain an unset element; "
                    + "default(RelicEffectDefinition) carries no value and was produced by no "
                    + "factory, so storing it would assert an effect the contract does not "
                    + "define (AGENTS.md §7).",
                    nameof(effects));
            }

            if (!RelicEffectDefinition.IsDefinedEffectType(effect.EffectType)
                || !RelicEffectDefinition.IsDefinedValueType(effect.ValueType)
                || !RelicEffectDefinition.IsDefinedTarget(effect.Target)
                || !RelicEffectDefinition.IsDefinedLifetime(effect.Lifetime))
            {
                throw new ArgumentException(
                    "A Relic effect definition cannot contain an element naming an effect "
                    + "identity, value interpretation, target, or lifetime this contract does "
                    + "not define (RELIC_RULES.md §8.2 item 5).",
                    nameof(effects));
            }
        }

        // Defensive copy: the stored sequence is the contract's, so a later
        // mutation of the caller's array must not change it.
        return new RelicEffectDefinitions((RelicEffectDefinition[])effects.Clone());
    }

    /// <summary>
    /// Writes the whole stored value — the JSON <b>array</b> of effect objects
    /// <c>RELIC_RULES.md</c> §8.2 defines, with each element written by
    /// <see cref="RelicEffectDefinition.ToPersistedPayload"/>.
    ///
    /// <code>
    /// [{"effectType":"ATK","valueType":"Percentage","value":5,"target":"Pet","lifetime":"Battle"}]
    /// </code>
    ///
    /// <b>The element order is the stored order, and it is not normalized.</b> The
    /// same effects in a different order produce a different payload, because the
    /// contract fixes no canonical order and this writer may not invent one
    /// (§8.2 item 4). The output is otherwise deterministic and canonical
    /// (<c>TDD.md</c> §6): a fixed member order per element, no whitespace, and no
    /// indentation.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The collection is <c>default</c> (no elements), or an element is not
    /// writable under §8.2/§8.3.
    /// </exception>
    public string ToPersistedPayload()
    {
        var effects = Effects;

        if (effects.Length == 0)
        {
            throw new ArgumentException(
                "The Relic effect definition holds no effects and cannot be written. "
                + "RELIC_RULES.md §8.2 stores a structured array of effect objects; "
                + "default(RelicEffectDefinitions) is not a definition.",
                nameof(effects));
        }

        // Hand-built rather than serializer-produced so the element payloads are
        // exactly the ones ToPersistedPayload emits — no second encoding of the
        // same contract, no whitespace, no indentation.
        return string.Concat(
            "[",
            string.Join(",", effects.Select(effect => effect.ToPersistedPayload())),
            "]");
    }

    /// <summary>
    /// Reads the stored <see cref="RelicDefinition.EffectDefinition"/> column
    /// value back into a Relic's effect array.
    ///
    /// <b>It accepts the contract's array shape only.</b> A valid array, a
    /// one-element array, and a multi-element array are all accepted; everything
    /// else is rejected loudly:
    /// <list type="bullet">
    /// <item>a null, empty, or whitespace value;</item>
    /// <item>text that is not JSON, or is truncated;</item>
    /// <item>anything that is not a JSON array — including the prose string
    /// TASK-082 R2-7 stored, for which <b>no compatibility reader exists</b> and
    /// which <c>RELIC_RULES.md</c> §8.1 item 1 declares is not a valid condition
    /// or effect;</item>
    /// <item>an empty array, which states no effect;</item>
    /// <item>an element that is not a well-formed effect object, names an
    /// unrecognized <c>effectType</c>/<c>valueType</c>/<c>target</c>/
    /// <c>lifetime</c>, omits a required magnitude or member, or states a
    /// combination §8.3's table does not list.</item>
    /// </list>
    ///
    /// There is no fallback magnitude, no default element, <b>no prose fallback</b>
    /// (<c>RELIC_RULES.md</c> §8.2 item 5), no inference from a Relic's name or
    /// <c>RelicDefinitionId</c>, no conversion of an unknown value into
    /// <see cref="RelicEffectValueType.Undetermined"/>, and no silent no-op. A
    /// malformed or truncated column value therefore surfaces as a failure at the
    /// read.
    /// </summary>
    /// <param name="payload">
    /// The stored JSON array, e.g.
    /// <c>[ { "effectType": "ATK", "valueType": "Percentage", "value": 5,
    /// "target": "Pet", "lifetime": "Battle" } ]</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="payload"/> is null, empty, whitespace, malformed, not an
    /// array, an empty array, or contains an element the element reader rejects.
    /// </exception>
    public static RelicEffectDefinitions FromPersistedPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException(
                "A Relic effect payload is required (DATABASE.md §1); an absent or empty value "
                + "is not a default effect array.",
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
                    $"The stored Relic effect payload is {DescribeKind(root.ValueKind)}, not an "
                    + "array. RELIC_RULES.md §8.2 stores the Relic's effects as a structured "
                    + "ARRAY of effect objects; the superseded prose form is not part of this "
                    + "contract, and no compatibility reader for it exists (§8.2 item 5 rejects "
                    + "a non-well-formed stored value rather than parsing prose).",
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
                $"The stored Relic effect payload is not valid JSON ({exception.Message}). "
                + "DATABASE.md §1 stores an array of structured effect objects; a malformed or "
                + "truncated value is rejected rather than defaulted.",
                nameof(payload),
                exception);
        }

        if (elements.Length == 0)
        {
            throw new ArgumentException(
                "The stored Relic effect payload is an empty array. RELIC_RULES.md §8.2 stores "
                + "one element per effect and §8.5 gives every provisioned Relic at least one "
                + "effect, so an empty array is a definition the contract cannot express "
                + "rather than a Relic that does nothing.",
                nameof(payload));
        }

        var effects = new RelicEffectDefinition[elements.Length];

        for (var index = 0; index < elements.Length; index++)
        {
            // The element reader owns the per-member rules, so every element is
            // validated by exactly one implementation — including its
            // unknown-member, unknown-token, missing-magnitude, and §8.3
            // combination checks.
            effects[index] = RelicEffectDefinition.FromPersistedPayload(
                elements[index].GetRawText());
        }

        return new RelicEffectDefinitions(effects);
    }

    /// <summary>
    /// Whether <paramref name="payload"/> is a well-formed Relic effect array —
    /// the non-throwing counterpart of <see cref="FromPersistedPayload"/>. It
    /// defines no rule of its own and delegates to the reader.
    /// </summary>
    public static bool TryFromPersistedPayload(
        string? payload,
        out RelicEffectDefinitions effects)
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
        // A comment or trailing comma would make one stored value readable by two
        // different parsers; the stored contract is plain JSON.
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>
    /// Element-wise equality in <b>stored order</b>, which is what makes the
    /// round-trip lossless: a definition read back from its own payload compares
    /// equal to the original, and two definitions that differ in element order are
    /// not equal. That is a statement about stored bytes only — the order still
    /// carries no gameplay meaning (§8.2 item 4), and nothing resolves on it.
    /// </summary>
    public bool Equals(RelicEffectDefinitions other)
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
        obj is RelicEffectDefinitions other && Equals(other);

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
    public static bool operator ==(RelicEffectDefinitions left, RelicEffectDefinitions right) =>
        left.Equals(right);

    /// <summary>Element-wise inequality in stored order.</summary>
    public static bool operator !=(RelicEffectDefinitions left, RelicEffectDefinitions right) =>
        !left.Equals(right);
}
