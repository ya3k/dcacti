using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameServer.Domain.Relics;

/// <summary>
/// <b>One element</b> of a Relic's structured effect declaration — the
/// <c>effectType</c> / <c>valueType</c> / <c>value</c> triple plus the
/// <c>target</c> / <c>lifetime</c> members <c>RELIC_RULES.md</c> §8.2–§8.3
/// define. A <see cref="RelicDefinition.EffectDefinition"/> is a
/// <see cref="RelicEffectDefinitions"/> — one element per effect — and this type
/// is what each element holds.
///
/// <code>
/// RelicDefinition
///   └── EffectDefinition            an ARRAY of RelicEffectDefinition elements
///         └── RelicEffectDefinition  (RELIC_RULES.md §8.2, one JSON object per effect)
///               ├── EffectType    which domain effect applies           (§8.2 item 1)
///               ├── ValueType     how the value is interpreted          (§8.2 item 2)
///               ├── Value         the effect's magnitude                (§8.2 item 3)
///               ├── Target        which entity it modifies              (§8.3 item 1)
///               └── Lifetime      how long the modification persists    (§8.3 item 2)
/// </code>
///
/// <b>Why this type exists.</b> §8.2 replaces TASK-082 R2-7's verbatim prose
/// ("+5% ATK", "Increased Crit chance") with a structured array, because "the
/// runtime must never derive a Relic's effect from parsed prose" (§8.2 item 1).
/// Consolidated with TASK-109's earlier Card-scoped supersession, R2-7 is now
/// superseded for both members it governed (<c>DATABASE.md</c> §1's Relic note
/// item 2).
///
/// <b>It follows the Card contract's shape, and is not a copy of it.</b> §8.2
/// says the representation "follow[s] the same representation contract
/// <c>DATABASE.md</c> §1 records for <c>CardDefinition.EffectDefinition</c>" —
/// the same problem class, one contract for it. The members are therefore the
/// same five names, but the <b>vocabularies are Relic-specific</b> and this type
/// uses them: <see cref="RelicEffectType"/> is
/// <c>ATK | Power | Crit | CardCost | BurnDamage</c> (§8.2 item 1, not the Card
/// set), and
/// <see cref="RelicEffectValueType"/> is
/// <c>Flat | Percentage | PercentagePoints | Undetermined</c> (§8.2 item 2, not
/// the Card set's <c>PercentMaxHp</c>). The Card members <c>duration</c> (Burn)
/// and <c>scope</c> (Crit) have <b>no Relic counterpart</b>: §8.3 item 5 states
/// "No member is defined beyond <c>target</c> and <c>lifetime</c>", so this type
/// declares exactly those two extra members and neither of the Card-specific
/// ones.
///
/// <b>The allowed combination is fixed per <c>effectType</c>.</b> §8.3's table is
/// the contract, and §8.3's closing rule states "a combination not listed is not
/// defined and may not be inferred". The construction and read paths therefore
/// reject a mismatched <c>valueType</c>, <c>target</c>, or <c>lifetime</c>
/// instead of storing it:
///
/// <code>
/// ATK        Pet   Battle       Percentage
/// ATK        Pet   NextAttack   Percentage
/// Power      Pet   Immediate    Flat
/// Crit       Pet   NextAttack   PercentagePoints
/// CardCost   Pet   Battle       Percentage
/// BurnDamage Pet   Battle       Percentage
/// </code>
///
/// <b>It is data, and it executes nothing.</b> The type names an effect and
/// carries its magnitude, target, and lifetime; it does not apply, target,
/// validate castability, roll Crit, tick Burn, spend Power, reduce a Card cost,
/// or modify ATK. The domain write sites that own those rules are reached only
/// by the Relic resolution stage, which is <b>not implemented</b>
/// (<c>RELIC_RULES.md</c> §8.7; <c>GAME_RULES.md</c> §17 step 11) and is not
/// introduced here. There is no handler, registry, dispatch, RNG, or plugin
/// lookup (<c>ARCHITECTURE.md</c> §5, <c>AGENTS.md</c> §9).
///
/// <b>The value is transcribed, never interpreted.</b> <c>RELIC_RULES.md</c>
/// §6/§8.5 owns every concrete magnitude and remains the only source for them;
/// this type stores what a definition row carries. A <c>Percentage</c> is stored
/// as the proportion §8.5 states and is <b>not</b> pre-resolved against an
/// assumed stat value (§8.2 item 2).
///
/// <b>Absence, not a sentinel.</b> <see cref="EffectType"/>, <see cref="ValueType"/>,
/// <see cref="Target"/>, and <see cref="Lifetime"/> must each name a documented
/// member, and the value's presence is decided <i>by</i> the interpretation: an
/// interpreting <see cref="ValueType"/> requires a positive <see cref="Value"/>,
/// and <see cref="RelicEffectValueType.Undetermined"/> requires that there be
/// none. The factories and the persisted-payload reader reject anything else
/// rather than substituting a default (§8.2 item 3, item 5;
/// <c>DATABASE.md</c> §1's Relic note item 6).
///
/// <b>It is framework-independent.</b> The type references no EF Core, ASP.NET
/// Core, Redis, SignalR, HTTP, Phaser, or Discord concern
/// (<c>ARCHITECTURE.md</c> §2.1). It uses <c>System.Text.Json</c> only to
/// serialize its own documented payload.
/// </summary>
public readonly record struct RelicEffectDefinition
{
    private RelicEffectDefinition(
        RelicEffectType effectType,
        RelicEffectValueType valueType,
        int? value,
        RelicEffectTarget target,
        RelicEffectLifetime lifetime)
    {
        EffectType = effectType;
        ValueType = valueType;
        Value = value;
        Target = target;
        Lifetime = lifetime;
    }

    /// <summary>
    /// Which domain effect this element applies (§8.2 item 1). Read as a typed
    /// member, so an identity outside <see cref="RelicEffectType"/> cannot be
    /// represented at all — it is rejected when a stored Relic definition is
    /// read.
    /// </summary>
    [JsonPropertyName("effectType")]
    public RelicEffectType EffectType { get; }

    /// <summary>
    /// How <see cref="Value"/> is interpreted (§8.2 item 2). Read as a typed
    /// member, so an unsupported interpretation is rejected when a stored Relic
    /// definition is read rather than silently treated as a plain amount.
    ///
    /// The allowed interpretation is fixed by <see cref="EffectType"/> through
    /// §8.3's table, so this member is validated against it rather than being
    /// free.
    /// </summary>
    [JsonPropertyName("valueType")]
    public RelicEffectValueType ValueType { get; }

    /// <summary>
    /// The effect's magnitude, transcribed from <c>RELIC_RULES.md</c> §6/§8.5
    /// through <see cref="ValueType"/> (§8.2 item 3).
    ///
    /// <b>It must be positive when a magnitude is authored.</b> Every magnitude
    /// §6/§8.5 states is a positive quantity — a percentage of ATK, a Power
    /// grant, a percentage-point Crit increase, or a percentage cost reduction —
    /// so a non-positive value is definition data no document authors; and since
    /// <c>0</c> is the CLR default of an unset <c>int</c>, admitting it would let
    /// a missing value pass as a real one.
    ///
    /// <b>It is absent, not zero, when the documents state no magnitude.</b> §8.2
    /// item 3: "<c>Undetermined</c> ... carries no <c>value</c> member at all,
    /// never <c>0</c> and never <c>null</c>, so an unauthored magnitude cannot be
    /// read as a number." No provisioned row is in that state (§8.5 authors every
    /// magnitude).
    /// </summary>
    [JsonPropertyName("value")]
    public int? Value { get; }

    /// <summary>
    /// Which entity the effect modifies (§8.3 item 1). The defined value is
    /// <see cref="RelicEffectTarget.Pet"/>, the only target §8.3 defines, and it
    /// is required: §8.2 item 5 rejects "a missing required member", of which
    /// <c>target</c> is one.
    /// </summary>
    [JsonPropertyName("target")]
    public RelicEffectTarget Target { get; }

    /// <summary>
    /// How long the applied modification persists (§8.3 item 2). It is required
    /// and the allowed value is fixed by <see cref="EffectType"/> through §8.3's
    /// table.
    ///
    /// <b>Storing it schedules nothing.</b> It records a lifetime the owning
    /// documents already state. No modification is created, tracked, or expired,
    /// and — per §8.4 — a Relic whose effect lifetime has ended remains eligible
    /// to re-trigger.
    /// </summary>
    [JsonPropertyName("lifetime")]
    public RelicEffectLifetime Lifetime { get; }

    /// <summary>
    /// Whether this element states a magnitude.
    ///
    /// <c>false</c> means the owning document states the effect but no magnitude
    /// for it, so the effect is <b>not resolvable</b> and a resolver must treat
    /// it as an open content gap rather than substitute a value (§8.2 item 2).
    /// No provisioned Relic row is in this state (§8.5).
    /// </summary>
    [JsonIgnore]
    public bool HasValue => Value is not null;

    /// <summary>
    /// Builds a fully authored effect element from an explicitly stated effect
    /// identity, interpretation, magnitude, target, and lifetime — the shape
    /// §8.5's four provisioned rows take.
    /// </summary>
    /// <param name="effectType">
    /// Which domain effect applies, from the closed <see cref="RelicEffectType"/>
    /// set. An undefined value (e.g. <c>(RelicEffectType)4</c>) names an effect no
    /// document authors, and admitting it would turn a content typo into a silent
    /// no-op.
    /// </param>
    /// <param name="valueType">
    /// How <paramref name="value"/> is interpreted. It must be one of the three
    /// <b>interpreting</b> members — <see cref="RelicEffectValueType.Flat"/>,
    /// <see cref="RelicEffectValueType.Percentage"/>, or
    /// <see cref="RelicEffectValueType.PercentagePoints"/>.
    /// <see cref="RelicEffectValueType.Undetermined"/> is rejected here because an
    /// undetermined effect has no value to interpret; build it with
    /// <see cref="Undetermined"/> instead, so the two cases cannot be confused.
    /// </param>
    /// <param name="value">
    /// The effect's magnitude, positive. It is transcribed from
    /// <c>RELIC_RULES.md</c> §8.5 and is never computed here.
    /// </param>
    /// <param name="target">
    /// Which entity the effect modifies. It must be
    /// <see cref="RelicEffectTarget.Pet"/>, the only value §8.3 defines, and the
    /// value §8.3's table fixes for every defined effect type.
    /// </param>
    /// <param name="lifetime">
    /// How long the applied modification persists. It must be the value §8.3's
    /// table fixes for <paramref name="effectType"/> — a combination the table
    /// does not list is not defined and may not be inferred.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="effectType"/>, <paramref name="valueType"/>,
    /// <paramref name="target"/>, or <paramref name="lifetime"/> is not a defined
    /// member; or <paramref name="value"/> is not positive.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// (<paramref name="effectType"/>, <paramref name="valueType"/>,
    /// <paramref name="target"/>, <paramref name="lifetime"/>) is not the
    /// combination <c>RELIC_RULES.md</c> §8.3's table defines.
    /// </exception>
    public static RelicEffectDefinition Create(
        RelicEffectType effectType,
        RelicEffectValueType valueType,
        int value,
        RelicEffectTarget target,
        RelicEffectLifetime lifetime)
    {
        RequireDefinedEffectType(effectType);
        RequireInterpretingValueType(valueType);
        RequireDefinedTarget(target);
        RequireDefinedLifetime(lifetime);
        RequirePositiveValue(value);
        RequireAllowedCombination(effectType, valueType, target, lifetime);

        return new RelicEffectDefinition(effectType, valueType, value, target, lifetime);
    }

    /// <summary>
    /// Builds an effect element whose owning document states the effect but
    /// <b>no magnitude</b>.
    ///
    /// <b>This is how an unauthored magnitude is represented without inventing
    /// one</b> (<c>RELIC_RULES.md</c> §8.2 item 2; <c>DATABASE.md</c> §1's Relic
    /// note item 6). Inventing a number is forbidden (<c>AGENTS.md</c> §7), so the
    /// effect's identity is recorded with
    /// <see cref="RelicEffectValueType.Undetermined"/> and <see cref="Value"/> left
    /// absent — never <c>0</c>, never <c>null</c>, never a placeholder, never a
    /// borrowed value (§8.2 item 3). A resolver must treat the result as an open
    /// content gap.
    ///
    /// <b>No provisioned Relic needs it.</b> §8.5 authors every magnitude of all
    /// four rows, so TASK-132 introduces none; the member stays representable
    /// because §8.2 item 2 keeps it valid.
    ///
    /// <b>The target and lifetime are still required.</b> §8.3 defines no
    /// combination without them, so they are taken as parameters and validated
    /// against the same table — an <c>Undetermined</c> element states its effect's
    /// target and lifetime, and only its magnitude is unauthored.
    /// </summary>
    /// <param name="effectType">
    /// Which domain effect applies, from the closed <see cref="RelicEffectType"/>
    /// set.
    /// </param>
    /// <param name="target">Which entity the effect modifies.</param>
    /// <param name="lifetime">How long the applied modification persists.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="effectType"/>, <paramref name="target"/>, or
    /// <paramref name="lifetime"/> is not a defined member.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// (<paramref name="effectType"/>, <paramref name="target"/>,
    /// <paramref name="lifetime"/>) is not a combination §8.3's table defines for
    /// an interpreting magnitude.
    /// </exception>
    public static RelicEffectDefinition Undetermined(
        RelicEffectType effectType,
        RelicEffectTarget target,
        RelicEffectLifetime lifetime)
    {
        RequireDefinedEffectType(effectType);
        RequireDefinedTarget(target);
        RequireDefinedLifetime(lifetime);

        // §8.3's table pairs each effectType with exactly one lifetime; the
        // `Undetermined` case states no valueType, so only the lifetime half of
        // the combination is checkable here.
        RequireLifetimeForEffectType(effectType, lifetime);

        return new RelicEffectDefinition(
            effectType,
            RelicEffectValueType.Undetermined,
            value: null,
            target,
            lifetime);
    }

    /// <summary>
    /// Whether <paramref name="effectType"/> is an identity this contract defines
    /// — the closed set of <see cref="RelicEffectType"/>.
    /// </summary>
    public static bool IsDefinedEffectType(RelicEffectType effectType) =>
        Enum.IsDefined(effectType);

    /// <summary>
    /// Whether <paramref name="valueType"/> is an interpretation this contract
    /// defines — the closed set of <see cref="RelicEffectValueType"/>.
    /// </summary>
    public static bool IsDefinedValueType(RelicEffectValueType valueType) =>
        Enum.IsDefined(valueType);

    /// <summary>
    /// Whether <paramref name="target"/> is a target this contract defines — the
    /// closed set of <see cref="RelicEffectTarget"/>.
    /// </summary>
    public static bool IsDefinedTarget(RelicEffectTarget target) =>
        Enum.IsDefined(target);

    /// <summary>
    /// Whether <paramref name="lifetime"/> is a lifetime this contract defines —
    /// the closed set of <see cref="RelicEffectLifetime"/>.
    /// </summary>
    public static bool IsDefinedLifetime(RelicEffectLifetime lifetime) =>
        Enum.IsDefined(lifetime);

    /// <summary>
    /// The <c>valueType</c> <c>RELIC_RULES.md</c> §8.3's table fixes for
    /// <paramref name="effectType"/> — the interpretation that effect's magnitude
    /// is stated in. Each <c>effectType</c> has exactly one such row, including
    /// the two <c>ATK</c> rows, which differ only in their lifetime.
    /// </summary>
    public static RelicEffectValueType RequiredValueTypeFor(RelicEffectType effectType) =>
        effectType switch
        {
            RelicEffectType.ATK => RelicEffectValueType.Percentage,
            RelicEffectType.Power => RelicEffectValueType.Flat,
            RelicEffectType.Crit => RelicEffectValueType.PercentagePoints,
            RelicEffectType.CardCost => RelicEffectValueType.Percentage,
            RelicEffectType.BurnDamage => RelicEffectValueType.Percentage,
            _ => throw new ArgumentOutOfRangeException(
                nameof(effectType),
                effectType,
                "The effect identity must be a RelicEffectType member; RELIC_RULES.md §8.2 "
                + "item 1 closes the set at ATK | Power | Crit | CardCost | BurnDamage."),
        };

    /// <summary>
    /// The lifetimes <c>RELIC_RULES.md</c> §8.3's table lists for
    /// <paramref name="effectType"/> — every value that effect type's row(s)
    /// define, in table order.
    ///
    /// <b>One effect type has two rows, and the rest have one.</b> §8.3's table
    /// carries <c>ATK</c> twice — <c>Battle</c> (Berserker Core, §8.5 item 4) and
    /// <c>NextAttack</c> (Battle Instinct, §8.5 item 10; TASK-178 Product Owner
    /// decision <b>Q-1 = A</b>). <c>Power</c>, <c>Crit</c>, <c>CardCost</c>, and
    /// <c>BurnDamage</c> each keep the single lifetime their own row fixes, so
    /// this is the one place the table's arity per effect type is stated.
    /// §8.3's closing rule still governs: a combination the table does not list is
    /// not defined and may not be inferred, so a lifetime outside the returned
    /// set is rejected rather than tolerated.
    /// </summary>
    /// <param name="effectType">The effect identity to read the table's rows for.</param>
    /// <returns>
    /// The table's lifetimes for that effect type, in table order and never
    /// empty.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="effectType"/> is not a <see cref="RelicEffectType"/> member.
    /// </exception>
    public static RelicEffectLifetime[] AllowedLifetimesFor(RelicEffectType effectType) =>
        effectType switch
        {
            // §8.3's table, in its own row order: `Battle` first, then the
            // `NextAttack` row TASK-176 added.
            RelicEffectType.ATK => [RelicEffectLifetime.Battle, RelicEffectLifetime.NextAttack],
            RelicEffectType.Power => [RelicEffectLifetime.Immediate],
            RelicEffectType.Crit => [RelicEffectLifetime.NextAttack],
            RelicEffectType.CardCost => [RelicEffectLifetime.Battle],
            RelicEffectType.BurnDamage => [RelicEffectLifetime.Battle],
            _ => throw new ArgumentOutOfRangeException(
                nameof(effectType),
                effectType,
                "The effect identity must be a RelicEffectType member; RELIC_RULES.md §8.2 "
                + "item 1 closes the set at ATK | Power | Crit | CardCost | BurnDamage."),
        };

    /// <summary>
    /// The <c>lifetime</c> <c>RELIC_RULES.md</c> §8.3's table fixes for
    /// <paramref name="effectType"/>.
    ///
    /// <b>It is the effect type's first table row, and it is not the whole
    /// contract for <c>ATK</c>.</b> <c>ATK</c> is the one effect type §8.3 lists
    /// twice, so its <c>NextAttack</c> row is not returned here — the complete
    /// allowed set is <see cref="AllowedLifetimesFor"/>, which is what
    /// validation uses. This member answers "which single lifetime does this
    /// effect type's row fix", which is the question a single-lifetime effect
    /// type has and the one §8.5's single-lifetime rows are read with; it is
    /// kept so an existing reader that asks that question keeps its meaning.
    /// </summary>
    public static RelicEffectLifetime RequiredLifetimeFor(RelicEffectType effectType) =>
        effectType switch
        {
            RelicEffectType.ATK => RelicEffectLifetime.Battle,
            RelicEffectType.Power => RelicEffectLifetime.Immediate,
            RelicEffectType.Crit => RelicEffectLifetime.NextAttack,
            RelicEffectType.CardCost => RelicEffectLifetime.Battle,
            RelicEffectType.BurnDamage => RelicEffectLifetime.Battle,
            _ => throw new ArgumentOutOfRangeException(
                nameof(effectType),
                effectType,
                "The effect identity must be a RelicEffectType member; RELIC_RULES.md §8.2 "
                + "item 1 closes the set at ATK | Power | Crit | CardCost | BurnDamage."),
        };

    /// <summary>
    /// Writes this element as its persisted JSON object (<c>DATABASE.md</c> §1).
    /// One element is one object; the enclosing array is written by
    /// <see cref="RelicEffectDefinitions.ToPersistedPayload"/>.
    ///
    /// <code>
    /// { "effectType": "ATK", "valueType": "Percentage", "value": 5,
    ///   "target": "Pet", "lifetime": "Battle" }
    /// </code>
    ///
    /// <b>The member names are the storage contract.</b> <c>effectType</c>,
    /// <c>valueType</c>, and <c>value</c> follow the Card contract's names, and
    /// <c>target</c> / <c>lifetime</c> are §8.3's; the internal representation
    /// maps to them, not vice versa. The type members are written as their
    /// <b>member names</b> so a persisted element is self-describing rather than
    /// an enum ordinal whose meaning a reordering could silently change.
    ///
    /// <b>The output is deterministic and canonical</b> (<c>TDD.md</c> §6): the
    /// members are written in a fixed order, no property-name policy or
    /// indentation is applied, and <c>value</c> is emitted only when the contract
    /// requires it — so the same rule always produces the same bytes and two
    /// rules that differ in any member never share a payload.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The element holds an undefined <see cref="EffectType"/>, <see cref="ValueType"/>,
    /// <see cref="Target"/>, or <see cref="Lifetime"/> — which the factories make
    /// unreachable — or an interpreting <see cref="ValueType"/> with a
    /// non-positive or absent <see cref="Value"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The element's members are not the combination §8.3's table defines for its
    /// <see cref="EffectType"/>.
    /// </exception>
    public string ToPersistedPayload()
    {
        RequireDefinedEffectType(EffectType);
        RequireDefinedTarget(Target);
        RequireDefinedLifetime(Lifetime);

        // An undetermined effect carries no magnitude member at all — emitting
        // `"value": null` would be a second representation of the same absence,
        // and emitting a number would invent one (§8.2 item 3).
        if (ValueType == RelicEffectValueType.Undetermined)
        {
            if (Value is not null)
            {
                throw new ArgumentException(
                    "An undetermined Relic effect states no magnitude and must carry no value "
                    + "member (RELIC_RULES.md §8.2 item 3). A value here would either be a "
                    + "placeholder or a magnitude no document authors (AGENTS.md §7).",
                    nameof(Value));
            }

            RequireLifetimeForEffectType(EffectType, Lifetime);

            return string.Create(
                CultureInfo.InvariantCulture,
                $$"""{"effectType":"{{EffectType}}","valueType":"{{ValueType}}","target":"{{Target}}","lifetime":"{{Lifetime}}"}""");
        }

        RequireDefinedValueType(ValueType);

        if (Value is not > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Value),
                Value,
                "An interpreting Relic effect value type requires a positive value "
                + "(RELIC_RULES.md §8.2 item 3); an absent magnitude must be recorded with "
                + "RelicEffectValueType.Undetermined instead.");
        }

        RequireAllowedCombination(EffectType, ValueType, Target, Lifetime);

        return string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"effectType":"{{EffectType}}","valueType":"{{ValueType}}","value":{{Value}},"target":"{{Target}}","lifetime":"{{Lifetime}}"}""");
    }

    /// <summary>
    /// Reads <b>one</b> persisted effect element (<c>DATABASE.md</c> §1) back
    /// into an effect rule. A whole column value is an array and is read with
    /// <see cref="RelicEffectDefinitions.FromPersistedPayload"/>.
    ///
    /// <b>It fails loudly rather than defaulting.</b> A payload that is not a JSON
    /// object, that omits or misspells a required member, that names an effect
    /// identity, value interpretation, target, or lifetime this contract does not
    /// define, that carries a non-positive or non-integral value, or whose members
    /// are not the combination §8.3's table fixes, is <b>rejected</b>
    /// (<c>RELIC_RULES.md</c> §8.2 item 5: "There is no fallback magnitude, no
    /// default, no prose fallback, and no silent no-op for an unrecognized
    /// <c>effectType</c>, an unrecognized <c>valueType</c>, a missing <c>value</c>,
    /// or a missing required member"). A malformed or truncated column value
    /// therefore surfaces as a failure at the read, not as a Relic that quietly
    /// does nothing.
    /// </summary>
    /// <param name="payload">
    /// One stored JSON object, e.g.
    /// <c>{ "effectType": "Power", "valueType": "Flat", "value": 10,
    /// "target": "Pet", "lifetime": "Immediate" }</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="payload"/> is null, empty, or whitespace; or it is not a
    /// valid structured effect element.
    /// </exception>
    public static RelicEffectDefinition FromPersistedPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException(
                "A Relic effect payload is required (DATABASE.md §1); an absent or empty "
                + "value is not a default effect.",
                nameof(payload));
        }

        StoredEffectDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<StoredEffectDocument>(payload, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                $"The stored Relic effect payload is not valid JSON ({exception.Message}). "
                + "DATABASE.md §1 stores the structured object; a malformed or truncated "
                + "value is rejected rather than defaulted.",
                nameof(payload),
                exception);
        }

        if (document is null)
        {
            throw new ArgumentException(
                "The stored Relic effect payload deserialized to nothing; DATABASE.md §1 "
                + "requires an object carrying effectType, valueType, value, target, and "
                + "lifetime.",
                nameof(payload));
        }

        return FromStoredDocument(document, payload);
    }

    /// <summary>
    /// Whether <paramref name="payload"/> is a well-formed structured effect
    /// element — the non-throwing counterpart of
    /// <see cref="FromPersistedPayload"/>. It defines no rule of its own and
    /// delegates to the reader.
    /// </summary>
    public static bool TryFromPersistedPayload(
        string? payload,
        out RelicEffectDefinition effect)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            effect = default;
            return false;
        }

        try
        {
            effect = FromPersistedPayload(payload);
            return true;
        }
        catch (ArgumentException)
        {
            effect = default;
            return false;
        }
    }

    /// <summary>
    /// Interprets one deserialized element. Each required member is checked for
    /// presence before it is interpreted, so a missing member reports itself
    /// instead of arriving as a CLR default — the same "no default" standard the
    /// Card contract's reader applies.
    /// </summary>
    private static RelicEffectDefinition FromStoredDocument(
        StoredEffectDocument document,
        string payload)
    {
        if (string.IsNullOrWhiteSpace(document.EffectType))
        {
            throw new ArgumentException(
                "The stored Relic effect payload has no effectType member; RELIC_RULES.md "
                + "§8.2 item 1 requires the effect identity to come from the structured data, "
                + "and absence is not a default (AGENTS.md §7).",
                nameof(payload));
        }

        if (string.IsNullOrWhiteSpace(document.ValueType))
        {
            throw new ArgumentException(
                "The stored Relic effect payload has no valueType member; RELIC_RULES.md "
                + "§8.2 item 2 requires the value's interpretation to be carried, and absence "
                + "is not a default.",
                nameof(payload));
        }

        if (string.IsNullOrWhiteSpace(document.Target))
        {
            throw new ArgumentException(
                "The stored Relic effect payload has no target member; RELIC_RULES.md §8.3 "
                + "item 1 defines it, and §8.2 item 5 rejects a missing required member "
                + "rather than defaulting it.",
                nameof(payload));
        }

        if (string.IsNullOrWhiteSpace(document.Lifetime))
        {
            throw new ArgumentException(
                "The stored Relic effect payload has no lifetime member; RELIC_RULES.md §8.3 "
                + "item 2 defines it, and §8.2 item 5 rejects a missing required member "
                + "rather than defaulting it.",
                nameof(payload));
        }

        if (!TryParseEnum(document.EffectType, out RelicEffectType effectType))
        {
            throw new ArgumentException(
                $"The stored Relic effect payload names an effect identity this contract does "
                + $"not define ('{document.EffectType}'). The defined identities are "
                + $"{string.Join(", ", Enum.GetNames<RelicEffectType>())}; an unrecognized "
                + "effect is rejected rather than ignored (AGENTS.md §7).",
                nameof(payload));
        }

        if (!TryParseEnum(document.ValueType, out RelicEffectValueType valueType))
        {
            throw new ArgumentException(
                $"The stored Relic effect payload names a value interpretation this contract "
                + $"does not define ('{document.ValueType}'). The defined interpretations are "
                + $"{string.Join(", ", Enum.GetNames<RelicEffectValueType>())}; an unsupported "
                + "interpretation is rejected rather than assumed to be Flat.",
                nameof(payload));
        }

        if (!TryParseEnum(document.Target, out RelicEffectTarget target))
        {
            throw new ArgumentException(
                $"The stored Relic effect payload names a target this contract does not define "
                + $"('{document.Target}'). The defined target is "
                + $"{string.Join(", ", Enum.GetNames<RelicEffectTarget>())}; RELIC_RULES.md "
                + "§8.3 item 1 defines no other, and §8.3's closing rule forbids inferring "
                + "a combination the table does not list.",
                nameof(payload));
        }

        if (!TryParseEnum(document.Lifetime, out RelicEffectLifetime lifetime))
        {
            throw new ArgumentException(
                $"The stored Relic effect payload names a lifetime this contract does not "
                + $"define ('{document.Lifetime}'). The defined lifetimes are "
                + $"{string.Join(", ", Enum.GetNames<RelicEffectLifetime>())}; RELIC_RULES.md "
                + "§8.3 item 2 states a value outside the allowed combination for its "
                + "effectType is not defined.",
                nameof(payload));
        }

        // The magnitude's presence is decided BY the interpretation, which is the
        // point of carrying it: an interpreting type must state a value, and an
        // undetermined type must not. Neither case is defaulted, and a mismatched
        // pair is rejected rather than reconciled (§8.2 item 3).
        if (valueType == RelicEffectValueType.Undetermined)
        {
            if (document.Value is not null)
            {
                throw new ArgumentException(
                    "The stored Relic effect payload records an undetermined magnitude but "
                    + "also carries a value member. An undetermined effect states no magnitude, "
                    + "and a value there would either be a placeholder or a magnitude no "
                    + "document authors (AGENTS.md §7).",
                    nameof(payload));
            }

            return Undetermined(effectType, target, lifetime);
        }

        if (document.Value is null)
        {
            throw new ArgumentException(
                "The stored Relic effect payload has no value member; an interpreting value "
                + "type requires the effect magnitude, which is never defaulted "
                + "(RELIC_RULES.md §8.2 item 3). Record an unauthored magnitude with the "
                + "Undetermined value type instead.",
                nameof(payload));
        }

        return Create(effectType, valueType, document.Value.Value, target, lifetime);
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

    private static void RequireDefinedEffectType(RelicEffectType effectType)
    {
        if (!IsDefinedEffectType(effectType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectType),
                effectType,
                "The effect identity must be a RelicEffectType member; RELIC_RULES.md §8.2 "
                + "item 1 closes the set at ATK | Power | Crit | CardCost | BurnDamage, and "
                + "an undefined value must not become a silent no-op (AGENTS.md §7).");
        }
    }

    private static void RequireDefinedValueType(RelicEffectValueType valueType)
    {
        if (!IsDefinedValueType(valueType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(valueType),
                valueType,
                "The value interpretation must be a RelicEffectValueType member; an "
                + "unsupported interpretation must not be assumed to be Flat "
                + "(RELIC_RULES.md §8.2 item 2).");
        }
    }

    private static void RequireDefinedTarget(RelicEffectTarget target)
    {
        if (!IsDefinedTarget(target))
        {
            throw new ArgumentOutOfRangeException(
                nameof(target),
                target,
                "The target must be a RelicEffectTarget member; RELIC_RULES.md §8.3 item 1 "
                + "defines Pet as the only target (AGENTS.md §7).");
        }
    }

    private static void RequireDefinedLifetime(RelicEffectLifetime lifetime)
    {
        if (!IsDefinedLifetime(lifetime))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                lifetime,
                "The lifetime must be a RelicEffectLifetime member; ADR-018 item 3 fixes the "
                + "set at Immediate | Battle | NextAttack (AGENTS.md §7).");
        }
    }

    /// <summary>
    /// Requires <paramref name="valueType"/> to be one of the three members that
    /// <b>interpret</b> a value, rather than
    /// <see cref="RelicEffectValueType.Undetermined"/>.
    ///
    /// It guards the factory that takes a value: pairing an undetermined
    /// interpretation with a number would represent both "a magnitude exists" and
    /// "no magnitude is authored", which is two representations of one fact. The
    /// undetermined case has its own factory.
    /// </summary>
    private static void RequireInterpretingValueType(RelicEffectValueType valueType)
    {
        RequireDefinedValueType(valueType);

        if (valueType == RelicEffectValueType.Undetermined)
        {
            throw new ArgumentOutOfRangeException(
                nameof(valueType),
                valueType,
                "An undetermined value type states no magnitude and cannot be paired with a "
                + "value (RELIC_RULES.md §8.2 item 3). Build it with "
                + "RelicEffectDefinition.Undetermined instead.");
        }
    }

    /// <summary>
    /// Requires the (<paramref name="effectType"/>, <paramref name="lifetime"/>)
    /// pair to be one <c>RELIC_RULES.md</c> §8.3's table lists for that effect
    /// type.
    ///
    /// It is the half of the combination that is checkable without a
    /// <c>valueType</c>, so the undetermined factory uses it too.
    ///
    /// <b>It checks membership, not equality with one fixed value.</b> §8.3's
    /// table lists <c>ATK</c> twice — <c>Battle</c> and <c>NextAttack</c> — so
    /// both are accepted for it while <c>Immediate</c> and any undefined value
    /// remain rejected; every other effect type has one row and therefore one
    /// accepted lifetime (<see cref="AllowedLifetimesFor"/>). Validation is
    /// widened to the table's rows, never removed.
    /// </summary>
    private static void RequireLifetimeForEffectType(
        RelicEffectType effectType,
        RelicEffectLifetime lifetime)
    {
        var allowed = AllowedLifetimesFor(effectType);

        foreach (var candidate in allowed)
        {
            if (lifetime == candidate)
            {
                return;
            }
        }

        throw new ArgumentException(
            $"The Relic effect element pairs effectType '{effectType}' with lifetime "
            + $"'{lifetime}'. RELIC_RULES.md §8.3's table lists "
            + $"{string.Join(" | ", allowed)} for that effect type; a combination the table "
            + "does not list is not defined and may not be inferred.",
            nameof(lifetime));
    }

    /// <summary>
    /// Requires the whole (<paramref name="effectType"/>,
    /// <paramref name="valueType"/>, <paramref name="target"/>,
    /// <paramref name="lifetime"/>) tuple to be a combination
    /// <c>RELIC_RULES.md</c> §8.3's table defines.
    ///
    /// §8.3's closing rule is explicit: "a combination not listed is not defined
    /// and may not be inferred". Storing an undefined combination would therefore
    /// be inventing a rule (<c>AGENTS.md</c> §7), so each part is rejected rather
    /// than tolerated.
    /// </summary>
    private static void RequireAllowedCombination(
        RelicEffectType effectType,
        RelicEffectValueType valueType,
        RelicEffectTarget target,
        RelicEffectLifetime lifetime)
    {
        var requiredValueType = RequiredValueTypeFor(effectType);

        if (valueType != requiredValueType)
        {
            throw new ArgumentException(
                $"The Relic effect element pairs effectType '{effectType}' with valueType "
                + $"'{valueType}'. RELIC_RULES.md §8.3's table fixes that effect type's "
                + $"valueType as '{requiredValueType}'; a combination the table does not list "
                + "is not defined and may not be inferred.",
                nameof(valueType));
        }

        RequireLifetimeForEffectType(effectType, lifetime);

        // §8.3's table fixes target = Pet for every defined effect type, so a
        // mismatch is reported against the table rather than against a per-type
        // expectation this contract does not have.
        if (target != RelicEffectTarget.Pet)
        {
            throw new ArgumentException(
                $"The Relic effect element pairs effectType '{effectType}' with target "
                + $"'{target}'. RELIC_RULES.md §8.3's table fixes the target as 'Pet' for "
                + "every defined effect type; a combination the table does not list is not "
                + "defined and may not be inferred.",
                nameof(target));
        }
    }

    private static void RequirePositiveValue(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A Relic effect value must be positive: every magnitude RELIC_RULES.md "
                + "§6/§8.5 states is a positive quantity, and the CLR default 0 of an unset "
                + "value is not a magnitude any document authors (AGENTS.md §7).");
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
        // not define — including the Card-only `duration`/`scope` members, which
        // RELIC_RULES.md §8.3 item 5 does not define for Relics.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,

        // A repeated member is ambiguous, so the last one must not silently win.
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>
    /// The persisted members of one Relic effect element — exactly
    /// <c>effectType</c>, <c>valueType</c>, <c>value</c>, <c>target</c>, and
    /// <c>lifetime</c> (<c>RELIC_RULES.md</c> §8.2–§8.3).
    ///
    /// Each member is nullable in this reader <b>deliberately</b>: that is what
    /// lets an omitted member be reported as omitted, rather than arriving as a
    /// CLR default indistinguishable from a real value. A present-but-wrong-typed
    /// member (a non-integral <c>value</c>, a numeric <c>effectType</c>) is a
    /// <see cref="JsonException"/> the reader reports as malformed.
    ///
    /// The Card-only <c>duration</c> and <c>scope</c> members are deliberately
    /// <b>absent</b>: §8.3 item 5 states no member beyond <c>target</c> and
    /// <c>lifetime</c> is defined, so
    /// <see cref="JsonUnmappedMemberHandling.Disallow"/> rejects a Relic payload
    /// carrying one instead of silently accepting a member this contract does not
    /// define.
    /// </summary>
    private sealed record StoredEffectDocument
    {
        [JsonPropertyName("effectType")]
        public string? EffectType { get; init; }

        [JsonPropertyName("valueType")]
        public string? ValueType { get; init; }

        [JsonPropertyName("value")]
        public int? Value { get; init; }

        [JsonPropertyName("target")]
        public string? Target { get; init; }

        [JsonPropertyName("lifetime")]
        public string? Lifetime { get; init; }
    }
}
