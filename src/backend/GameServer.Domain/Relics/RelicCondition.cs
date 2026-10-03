using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameServer.Domain.Relics;

/// <summary>
/// A Relic's <b>structured</b> <c>Condition</c> — the trigger's optional extra
/// condition as <c>RELIC_RULES.md</c> §8.1 defines it, replacing the
/// <c>character varying(128)</c> prose form (<c>DATABASE.md</c> §1; TASK-131
/// <b>D5</b>/**D9**, implemented by TASK-132).
///
/// <code>
/// RelicDefinition
///   └── Condition
///         └── RelicCondition
///               ├── ConditionType  which evaluation form applies   (§8.1)
///               └── Threshold      the form's N, an integer        (§8.1 item 1)
/// </code>
///
/// <b>Why this type exists.</b> The prose form embedded its threshold in a
/// sentence ("every 3 Matches", "Combo ≥ 3", "HP &lt; 30%"), which no runtime
/// can read without inventing a prose parser — the anti-pattern the Card
/// contract already forbids (<c>DATABASE.md</c> §1 item 1: "the runtime must
/// never derive the effect from parsed prose"). §8.1 item 1 states the rule this
/// type implements: <b>the threshold is part of the value, never embedded in
/// prose.</b>
///
/// <b>The form set is closed.</b> <see cref="RelicConditionType"/> carries
/// exactly §8.1's three forms (TASK-131 D5). This type adds none, and it has
/// <b>no Card counterpart</b>: the Card contract defines no structured condition
/// member, so this representation is Relic-specific work
/// (<c>DATABASE.md</c> §1's Relic note).
///
/// <b>It is data, and it evaluates nothing.</b> §8.1 item 2 places evaluation at
/// the point <c>GAME_RULES.md</c> §17 step 11 executes, against the current
/// resolution state and the event being processed. This type reads no battle
/// state, performs no comparison, holds no Relic counter, and introduces no
/// member to <c>GAME_STATE.md</c> (§8.1 item 3; <c>ADR-018</c> item 5). Reading
/// and evaluating a Condition is the Relic stage's own task (§8.7).
///
/// <b>It is optional.</b> §8.1 item 4 keeps <c>Condition</c> optional
/// (<c>RELIC_RULES.md</c> §1): a Relic whose Trigger alone is its complete
/// condition carries none, so the Domain member holding this value is nullable
/// rather than defaulted to a sentinel condition that would read as a real one.
///
/// <b>Absence, not a sentinel.</b> A <see cref="RelicConditionType"/> must name
/// a documented member and the threshold must be a positive integer; a stored
/// payload that names an unrecognized form or omits its threshold is
/// <b>rejected</b> rather than defaulted (<c>DATABASE.md</c> §1's Relic note
/// item 6, applying the Card contract's item 6 standard).
///
/// <b>It is framework-independent.</b> The type references no EF Core, ASP.NET
/// Core, Redis, SignalR, HTTP, Phaser, or Discord concern
/// (<c>ARCHITECTURE.md</c> §2.1). It uses <c>System.Text.Json</c> only to
/// serialize its own documented payload.
/// </summary>
public readonly record struct RelicCondition : IEquatable<RelicCondition>
{
    private RelicCondition(RelicConditionType conditionType, int threshold)
    {
        ConditionType = conditionType;
        Threshold = threshold;
    }

    /// <summary>
    /// Which of §8.1's three evaluation forms this condition is. Read as a typed
    /// member, so a form outside <see cref="RelicConditionType"/> cannot be
    /// represented at all — it is rejected when a stored definition is read.
    /// </summary>
    [JsonPropertyName("conditionType")]
    public RelicConditionType ConditionType { get; }

    /// <summary>
    /// The form's <c>N</c> — the threshold the condition compares against
    /// (<c>RELIC_RULES.md</c> §8.1 item 1). For the provisioned rows these are
    /// §8.5's transcribed values: Berserker Core 3, Mana Crystal 4, Assassin Eye
    /// 3, Emergency Core 30.
    ///
    /// <b>It must be positive.</b> Every threshold §6/§8.5 states is a positive
    /// quantity — a Match count, a Combo value, or a percentage of HP — so a
    /// non-positive threshold is definition data no document authors. Since
    /// <c>0</c> is the CLR default of an unset <c>int</c>, admitting it would
    /// also let a missing threshold pass as a real one.
    /// </summary>
    [JsonPropertyName("threshold")]
    public int Threshold { get; }

    /// <summary>
    /// Builds a condition from an explicitly stated form and threshold — the
    /// shape every provisioned Relic row's Condition takes.
    /// </summary>
    /// <param name="conditionType">
    /// Which of §8.1's forms applies. It must be a defined
    /// <see cref="RelicConditionType"/> member: an undefined value (e.g.
    /// <c>(RelicConditionType)3</c>) names a form no document authors, and
    /// admitting it would turn a content typo into a silent no-op.
    /// </param>
    /// <param name="threshold">
    /// The form's <c>N</c>, positive, transcribed from <c>RELIC_RULES.md</c>
    /// §8.5. It is never computed here.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="conditionType"/> is not a defined member, or
    /// <paramref name="threshold"/> is not positive.
    /// </exception>
    public static RelicCondition Create(RelicConditionType conditionType, int threshold)
    {
        RequireDefinedConditionType(conditionType);
        RequirePositiveThreshold(threshold);

        return new RelicCondition(conditionType, threshold);
    }

    /// <summary>
    /// Whether <paramref name="conditionType"/> is a form this contract defines —
    /// the closed set of <see cref="RelicConditionType"/>.
    /// </summary>
    public static bool IsDefinedConditionType(RelicConditionType conditionType) =>
        Enum.IsDefined(conditionType);

    /// <summary>
    /// Writes this condition as its persisted JSON object (<c>DATABASE.md</c>
    /// §1).
    ///
    /// <code>
    /// { "conditionType": "MatchCountAtLeast", "threshold": 3 }
    /// </code>
    ///
    /// <b>The member names are the storage contract.</b> <c>conditionType</c>
    /// and <c>threshold</c> are the members this contract fixes; the internal
    /// representation maps to them, not vice versa. The form is written as its
    /// <b>member name</b> so a persisted value is self-describing rather than an
    /// enum ordinal whose meaning a reordering could silently change.
    ///
    /// <b>The output is deterministic and canonical</b> (<c>TDD.md</c> §6): a
    /// fixed member order, no whitespace, and no indentation, so the same
    /// condition always produces the same bytes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The condition holds an undefined <see cref="ConditionType"/> or a
    /// non-positive <see cref="Threshold"/> — which the factory makes
    /// unreachable.
    /// </exception>
    public string ToPersistedPayload()
    {
        RequireDefinedConditionType(ConditionType);
        RequirePositiveThreshold(Threshold);

        return string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"conditionType":"{{ConditionType}}","threshold":{{Threshold}}}""");
    }

    /// <summary>
    /// Reads a stored <c>RelicDefinition.Condition</c> column value back into a
    /// structured condition.
    ///
    /// <b>It fails loudly rather than defaulting.</b> A payload that is not a
    /// JSON object, that omits or misspells a required member, that names an
    /// unrecognized condition form, or that carries a non-positive or
    /// non-integral threshold is <b>rejected</b>. There is no fallback
    /// threshold, no fallback to treating the value as prose (which
    /// <c>DATABASE.md</c> §1 no longer stores, and which §8.1 item 1 declares is
    /// not a valid condition), and no silent no-op — the standard the Card
    /// contract's item 6 sets and the Relic note's item 6 carries to Relics.
    ///
    /// <b>An absent condition is not read here.</b> <c>Condition</c> is optional
    /// (§8.1 item 4), so <see langword="null"/> at the column means the Relic
    /// declares no extra condition; a caller tests that before reading, rather
    /// than this reader inventing a condition for it.
    /// </summary>
    /// <param name="payload">
    /// One stored JSON object, e.g.
    /// <c>{ "conditionType": "ComboAtLeast", "threshold": 3 }</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="payload"/> is null, empty, or whitespace; or it is not a
    /// valid structured condition.
    /// </exception>
    public static RelicCondition FromPersistedPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException(
                "A Relic condition payload is required (DATABASE.md §1); an absent or "
                + "empty value is not a default condition.",
                nameof(payload));
        }

        StoredConditionDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<StoredConditionDocument>(payload, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                $"The stored Relic condition payload is not valid JSON ({exception.Message}). "
                + "DATABASE.md §1 stores the structured object; a malformed or truncated "
                + "value is rejected rather than defaulted.",
                nameof(payload),
                exception);
        }

        if (document is null)
        {
            throw new ArgumentException(
                "The stored Relic condition payload deserialized to nothing; DATABASE.md §1 "
                + "requires an object carrying conditionType and threshold.",
                nameof(payload));
        }

        if (string.IsNullOrWhiteSpace(document.ConditionType))
        {
            throw new ArgumentException(
                "The stored Relic condition payload has no conditionType member; RELIC_RULES.md "
                + "§8.1 requires the form to be carried, and absence is not a default "
                + "(AGENTS.md §7).",
                nameof(payload));
        }

        if (!TryParseEnum(document.ConditionType, out RelicConditionType conditionType))
        {
            throw new ArgumentException(
                $"The stored Relic condition payload names a condition form this contract "
                + $"does not define ('{document.ConditionType}'). The defined forms are "
                + $"{string.Join(", ", Enum.GetNames<RelicConditionType>())}; an unrecognized "
                + "form is rejected rather than ignored (AGENTS.md §7).",
                nameof(payload));
        }

        if (document.Threshold is null)
        {
            throw new ArgumentException(
                "The stored Relic condition payload has no threshold member; RELIC_RULES.md "
                + "§8.1 item 1 requires every form to carry its threshold as an integer, and "
                + "it is never defaulted.",
                nameof(payload));
        }

        return Create(conditionType, document.Threshold.Value);
    }

    /// <summary>
    /// Whether <paramref name="payload"/> is a well-formed structured condition —
    /// the non-throwing counterpart of <see cref="FromPersistedPayload"/>. It
    /// defines no rule of its own and delegates to the reader.
    /// </summary>
    public static bool TryFromPersistedPayload(string? payload, out RelicCondition condition)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            condition = default;
            return false;
        }

        try
        {
            condition = FromPersistedPayload(payload);
            return true;
        }
        catch (ArgumentException)
        {
            condition = default;
            return false;
        }
    }

    /// <summary>
    /// Parses a stored enum token by its member name, and never by its numeric
    /// value — the same convention the Card members fix
    /// (<c>DATABASE.md</c> §1 item 2). The match is ordinal and case-sensitive:
    /// two spellings of one token are not two tokens.
    /// </summary>
    private static bool TryParseEnum<TEnum>(string token, out TEnum value)
        where TEnum : struct, Enum
    {
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(name, token, StringComparison.Ordinal))
            {
                value = Enum.Parse<TEnum>(name);
                return true;
            }
        }

        value = default;
        return false;
    }

    private static void RequireDefinedConditionType(RelicConditionType conditionType)
    {
        if (!IsDefinedConditionType(conditionType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(conditionType),
                conditionType,
                "The condition form must be a RelicConditionType member; RELIC_RULES.md §8.1 "
                + "closes the grammar at MatchCountAtLeast | ComboAtLeast | HpPercentageBelow, "
                + "and an undefined value must not become a silent no-op (AGENTS.md §7).");
        }
    }

    private static void RequirePositiveThreshold(int threshold)
    {
        if (threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threshold),
                threshold,
                "A Relic condition threshold must be positive: RELIC_RULES.md §8.1 item 1 "
                + "requires every form to carry its threshold as an integer, every threshold "
                + "§6/§8.5 states is a positive quantity, and the CLR default 0 of an unset "
                + "threshold is not a value any document authors (AGENTS.md §7).");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // The member names are pinned on the document type with
        // [JsonPropertyName], because DATABASE.md §1's storage names are the
        // contract and are not the Domain's property names.
        PropertyNameCaseInsensitive = false,

        // An unknown member is a content/schema error, not something to skip:
        // silently ignoring it would let a payload carry data this contract does
        // not define.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,

        // A repeated member is ambiguous, so the last one must not silently win.
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>
    /// The persisted members of one structured condition — exactly
    /// <c>conditionType</c> and <c>threshold</c>.
    ///
    /// Each member is nullable in this reader <b>deliberately</b>: that is what
    /// lets an omitted member be reported as omitted, rather than arriving as a
    /// CLR default indistinguishable from a real value. A present-but-wrong-typed
    /// member (a non-integral <c>threshold</c>, a numeric <c>conditionType</c>)
    /// is a <see cref="JsonException"/> the reader reports as malformed.
    /// </summary>
    private sealed record StoredConditionDocument
    {
        [JsonPropertyName("conditionType")]
        public string? ConditionType { get; init; }

        [JsonPropertyName("threshold")]
        public int? Threshold { get; init; }
    }
}
