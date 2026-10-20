using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameServer.Domain.Cards;

/// <summary>
/// <b>One element</b> of a Card's structured effect rule — the <c>effectType</c>
/// / <c>valueType</c> / <c>value</c> triple of <c>DATABASE.md</c> §1's stored
/// contract, plus the effect-specific extra members that contract defines.
/// A <see cref="CardDefinition.EffectDefinition"/> is a
/// <see cref="CardEffectDefinition"/>[] — one element per effect — and this type
/// is what each element holds.
///
/// <code>
/// CardDefinition
///   └── EffectDefinition            an ARRAY of CardEffectDefinition elements
///         └── CardEffectDefinition  (DATABASE.md §1, one JSON object per effect)
///               ├── EffectType    which domain effect applies     (D-1)
///               ├── ValueType     how the value is interpreted    (D-3)
///               ├── Value         the effect value                (D-2)
///               ├── Duration      Burn / AtkBuff / AtkDebuff only: Turns it lasts
///               └── Scope         Crit / NextAtkBuff only: "NextAttack"
/// </code>
///
/// <b>Why this type exists.</b> TASK-108's decision D-1 requires effect
/// identity to come from structured effect data, because the server must
/// deterministically identify which domain effect a Card applies. TASK-082
/// decision R2-7 previously stored the owning document's effect rule text
/// verbatim, which no runtime can read without inventing a prose parser. This
/// type is the structured carrier D-1 approves; R2-7 is superseded for
/// <see cref="CardDefinition"/> only, and remains in force for
/// <c>RelicDefinition</c> (<c>DATABASE.md</c> §1).
///
/// <b>One shape for every Card</b> (TASK-111 D-1b). A Card with one effect
/// stores a one-element array; a Card with several stores one element per
/// effect. There is deliberately no separate "single effect" representation, no
/// wrapper object for a multi-effect Card, and no arity-dependent behaviour
/// anywhere: the element is the same object in every case.
///
/// <b>The extra members are present iff their effect requires them</b>
/// (<c>DATABASE.md</c> §1 item 1/§3, as extended by TASK-309 decision D-1).
/// <see cref="Duration"/> exists only on a <see cref="CardEffectType.Burn"/>,
/// <see cref="CardEffectType.AtkBuff"/>, or <see cref="CardEffectType.AtkDebuff"/>
/// element, and <see cref="Scope"/> only on a <see cref="CardEffectType.Crit"/> or
/// <see cref="CardEffectType.NextAtkBuff"/> element; neither is a general-purpose
/// field, and neither is defaulted. <c>DATABASE.md</c> §1 item 1 states the rule
/// for both: "No further extra member is defined, and none may be added without a
/// recorded owner decision."
///
/// <b>It is data, and it executes nothing.</b> The type names an effect and
/// carries its magnitude and parameters; it does not apply, target, validate
/// castability, roll Crit, tick Burn, or spend Power. The domain write sites
/// that own the rules — the Heal clamp (<c>COMBAT_RULES.md</c> §4 item 1), the
/// Shield contract (§4, TASK-105), the Power range (<c>GAME_RULES.md</c> §12),
/// the Damage Pipeline's Crit and Burn rules (§3.3, §5), and the Boss-only
/// effects' status/Power writes (<c>BOSS_RULES.md</c> §6.3.1;
/// <c>GAME_STATE.md</c> §2.3.1) — are reached only by the shared resolver,
/// <see cref="CardCastExecutor"/> (<c>CARD_RULES.md</c> §3 item 9). There is no
/// handler, no registry, no dispatch, no RNG, and no plugin lookup here: D-1/D-3
/// require a data shape and its validation, nothing more
/// (<c>ARCHITECTURE.md</c> §5, <c>AGENTS.md</c> §9).
///
/// <b>The value is transcribed, never interpreted.</b> <c>CARD_RULES.md</c>
/// §2/§4.1 owns every concrete magnitude and remains the only source for them;
/// this type stores what a definition row carries. A proportion is stored as
/// the proportion <c>CARD_RULES.md</c> states and is <b>not</b> pre-resolved
/// against an assumed Max HP (the Pet's <c>MaxHP</c> is battle state,
/// <c>GAME_STATE.md</c> §2.3).
///
/// <b>Absence, not a sentinel.</b> Both <see cref="EffectType"/> and
/// <see cref="ValueType"/> must name a documented member, and the value's
/// presence is decided <i>by</i> the interpretation: an interpreting
/// <see cref="ValueType"/> requires a positive <see cref="Value"/>, and
/// <see cref="CardEffectValueType.Undetermined"/> requires that there be none.
/// The factories and the persisted-payload reader reject anything else rather
/// than substituting a default (<c>GAME_STATE.md</c> §2.3.1 item 8's
/// "zero/absent is not a sentinel"; <c>CardDefinition.LoadoutCopyLimit</c>'s
/// "no default" precedent).
///
/// <b>It is framework-independent.</b> The type references no EF Core, ASP.NET
/// Core, Redis, SignalR, HTTP, Phaser, or Discord concern
/// (<c>ARCHITECTURE.md</c> §2.1). It uses <c>System.Text.Json</c> only to
/// serialize its own documented payload, in the same way the persistence
/// configuration's converters do — the Domain holds no persistence model.
/// </summary>
public readonly record struct CardEffectDefinition
{
    /// <summary>
    /// The one scope value the contract defines — <c>NextAttack</c>
    /// (<c>DATABASE.md</c> §1 item 1, §3; TASK-111 D-3). The rule it stores is
    /// authored by <c>CARD_RULES.md</c> §4.1 ("for the next attack only").
    /// </summary>
    public const string NextAttackScope = "NextAttack";

    private CardEffectDefinition(
        CardEffectType effectType,
        CardEffectValueType valueType,
        int? value,
        int? duration,
        string? scope)
    {
        EffectType = effectType;
        ValueType = valueType;
        Value = value;
        Duration = duration;
        Scope = scope;
    }

    /// <summary>
    /// Which domain effect this element applies (D-1). Read as a typed member, so
    /// an identity outside <see cref="CardEffectType"/> cannot be represented at
    /// all — it is rejected when a stored card definition is read.
    /// </summary>
    [JsonPropertyName("effectType")]
    public CardEffectType EffectType { get; }

    /// <summary>
    /// How <see cref="Value"/> is interpreted — the effect's calculation rule.
    /// Read as a typed member, so an unsupported interpretation is rejected when
    /// a stored card definition is read rather than silently treated as a plain
    /// amount.
    /// </summary>
    [JsonPropertyName("valueType")]
    public CardEffectValueType ValueType { get; }

    /// <summary>
    /// The effect's magnitude (<c>CARD_RULES.md</c> §2/§4.1), interpreted by
    /// <see cref="ValueType"/>.
    ///
    /// <b>It is the effect magnitude, never the Card's Cost.</b> Power Charge
    /// costs <c>0</c> Power while its effect grants Power (<c>CARD_RULES.md</c>
    /// §2 item 3); the Cost is <see cref="CardDefinition.PowerCost"/>.
    ///
    /// <b>It must be positive when a magnitude is authored.</b> Every effect
    /// <c>CARD_RULES.md</c> §2/§4.1 states is a positive quantity — HP restored,
    /// absorption granted, Power granted, damage dealt, damage per Burn tick, or
    /// a percentage-point Crit increase — so a non-positive value is definition
    /// data no document authors; and since <c>0</c> is the CLR default of an
    /// unset <c>int</c>, admitting it would let a missing value pass as a real
    /// one (<c>DATABASE.md</c> §3: "int, &gt; 0, present iff valueType interprets
    /// one").
    ///
    /// <b>It is absent, not zero, when the documents state no magnitude.</b> A
    /// <see cref="CardEffectValueType.Undetermined"/> element carries no value at
    /// all (<c>null</c>), so an unauthored magnitude can never be read as the
    /// number <c>0</c> — the same "absent, never a sentinel" rule
    /// <c>GAME_STATE.md</c> §2.3.1 item 7 applies to status-effect members.
    /// </summary>
    [JsonPropertyName("value")]
    public int? Value { get; }

    /// <summary>
    /// <b>Burn / AtkBuff / AtkDebuff only</b> — the number of Turns the effect
    /// lasts (<c>DATABASE.md</c> §1 item 1, §3; TASK-111 D-3, extended by
    /// TASK-309 D-1). On a <see cref="CardEffectType.Burn"/> element,
    /// <see cref="Value"/> is the damage per tick and this member is the
    /// duration, in the authoritative Turn / End-Turn-tick unit owned by
    /// <c>GAME_RULES.md</c> §17 step 19a and <c>COMBAT_RULES.md</c> §5.2. On an
    /// <see cref="CardEffectType.AtkBuff"/> or <see cref="CardEffectType.AtkDebuff"/>
    /// element, <see cref="Value"/> is the ATK modifier in percentage points and
    /// this member is the buff/debuff lifetime in Turns
    /// (<c>BOSS_RULES.md</c> §6.3.1 items 1 and 3; <c>COMBAT_RULES.md</c> §5.3).
    ///
    /// <b>It is present iff <see cref="EffectType"/> is one of those three.</b>
    /// §3 states the condition in both directions: the member exists only on an
    /// element of one of those effects, and such an element must carry it — a
    /// missing required extra member is one of §1 item 6's loud-rejection cases.
    /// Any other effect carrying it is rejected rather than ignored, because the
    /// contract defines no meaning for a duration anywhere else.
    ///
    /// <b>Storing it schedules nothing.</b> It records a duration the owning
    /// documents already state; no Burn instance is created, no tick is
    /// performed, and no status effect is applied.
    /// </summary>
    [JsonPropertyName("duration")]
    public int? Duration { get; }

    /// <summary>
    /// <b>Crit / NextAtkBuff only</b> — which damage instances the ATK or Crit
    /// modifier applies to (<c>DATABASE.md</c> §1 item 1, §3; TASK-111 D-3,
    /// extended by TASK-309 D-1). The defined value is
    /// <see cref="NextAttackScope"/>.
    ///
    /// <b>It is present iff <see cref="EffectType"/> is
    /// <see cref="CardEffectType.Crit"/> or
    /// <see cref="CardEffectType.NextAtkBuff"/>.</b> §3 states the condition in
    /// both directions: the member exists only on an element of one of those
    /// effects, and such an element must carry it — a missing required extra
    /// member is one of §1 item 6's loud-rejection cases. Any other effect
    /// carrying it is rejected rather than ignored.
    ///
    /// <b>Its rule is already authored.</b> <c>CARD_RULES.md</c> §4.1 states the
    /// Crit increase applies "for the next attack only", and
    /// <c>BOSS_RULES.md</c> §6.3.1 item 5 authors Thunder Strike's
    /// <c>NextQualifyingAttack</c> lifetime; <c>PASSIVE_RULES.md</c> §7 uses the
    /// same scope for Bạch Hổ's Passive. This member only stores that rule. It
    /// performs no Crit roll, evaluates no trigger, and touches no formula.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; }

    /// <summary>
    /// Whether this element states a magnitude.
    ///
    /// <c>false</c> means the owning document states the effect but no magnitude
    /// for it, so the effect is <b>not resolvable</b> and a resolver must treat
    /// it as an open content gap rather than substitute a value
    /// (<c>DATABASE.md</c> §1 item 9). After the TASK-112 encoding no provisioned
    /// content row is in this state.
    /// </summary>
    [JsonIgnore]
    public bool HasValue => Value is not null;

    /// <summary>
    /// Builds a fully authored effect element from an explicitly stated effect
    /// identity, interpretation, and value — the shape
    /// <c>CARD_RULES.md</c> §2's three Basic Cards and the value-bearing halves
    /// of §4.1's Pet Skill Cards take.
    /// </summary>
    /// <param name="effectType">
    /// Which domain effect applies. It must be a defined
    /// <see cref="CardEffectType"/> member: an undefined value (e.g.
    /// <c>(CardEffectType)7</c>) names an effect no document authors, and
    /// admitting it would turn a content typo into a silent no-op.
    /// </param>
    /// <param name="valueType">
    /// How <paramref name="value"/> is interpreted. It must be one of the three
    /// <b>interpreting</b> members — <see cref="CardEffectValueType.Flat"/>,
    /// <see cref="CardEffectValueType.PercentMaxHp"/>, or
    /// <see cref="CardEffectValueType.PercentagePoints"/>.
    /// <see cref="CardEffectValueType.Undetermined"/> is rejected here because an
    /// undetermined effect has no value to interpret; build it with
    /// <see cref="Undetermined"/> instead, so the two cases cannot be confused.
    /// </param>
    /// <param name="value">
    /// The effect's magnitude, positive. It is transcribed from
    /// <c>CARD_RULES.md</c> §2/§4.1 and is never computed here.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="effectType"/> is not a defined member;
    /// <paramref name="valueType"/> is not one of the three interpreting
    /// members; or <paramref name="value"/> is not positive.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="effectType"/> is <see cref="CardEffectType.Burn"/> or
    /// <see cref="CardEffectType.Crit"/>, whose elements must carry the extra
    /// member §3 requires, or one of the four Boss-only effects, whose fixed
    /// interpretation and shape TASK-309 D-1 defines. Use
    /// <see cref="Burn"/>, <see cref="Crit"/>, <see cref="AtkBuff"/>,
    /// <see cref="PowerDrain"/>, <see cref="AtkDebuff"/>, or
    /// <see cref="NextAtkBuff"/> instead: a Burn without its duration, a Crit
    /// without its scope, or a Boss-only effect built through the plain-value path
    /// would be the missing-required-extra-member / wrong-interpretation case
    /// §1 item 6 rejects.
    /// </exception>
    public static CardEffectDefinition Create(
        CardEffectType effectType,
        CardEffectValueType valueType,
        int value)
    {
        RequireDefinedEffectType(effectType);
        RequireInterpretingValueType(valueType);
        RequireNoRequiredExtraMember(effectType, nameof(valueType));
        RequireDedicatedBossOnlyFactory(effectType, nameof(effectType));
        RequirePositiveValue(value);

        return new CardEffectDefinition(effectType, valueType, value, null, null);
    }

    /// <summary>
    /// Builds a <see cref="CardEffectType.Burn"/> element — damage per tick plus
    /// the number of Turns (<c>CARD_RULES.md</c> §4.1's Inferno; <c>DATABASE.md</c>
    /// §1 item 1, §3).
    /// </summary>
    /// <param name="valueType">
    /// How the per-tick damage is interpreted — an interpreting member, never
    /// <see cref="CardEffectValueType.Undetermined"/>.
    /// </param>
    /// <param name="value">
    /// The <b>damage per tick</b>, positive, transcribed from
    /// <c>CARD_RULES.md</c> §4.1.
    /// </param>
    /// <param name="duration">
    /// The number of Turns the effect lasts, positive, transcribed from
    /// <c>CARD_RULES.md</c> §4.1. <c>DATABASE.md</c> §3 states the bound as
    /// "int, &gt; 0", and it is never defaulted: a Burn element without its
    /// duration is a missing required extra member, which §1 item 6 rejects.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="valueType"/> is not an interpreting member, or
    /// <paramref name="value"/> / <paramref name="duration"/> is not positive.
    /// </exception>
    public static CardEffectDefinition Burn(
        CardEffectValueType valueType,
        int value,
        int duration)
    {
        RequireInterpretingValueType(valueType);
        RequirePositiveValue(value);

        if (duration <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "A Burn duration must be positive: DATABASE.md §3 states the bound as "
                + "'int, > 0, present iff effectType = Burn', and CARD_RULES.md §4.1 "
                + "authors the duration in whole Turns. An unset duration is not a "
                + "duration no document authors (AGENTS.md §7).");
        }

        return new CardEffectDefinition(
            CardEffectType.Burn,
            valueType,
            value,
            duration,
            scope: null);
    }

    /// <summary>
    /// Builds a <see cref="CardEffectType.Crit"/> element — the percentage-point
    /// increase plus the scope it applies to (<c>CARD_RULES.md</c> §4.1's Iron
    /// Fang; <c>DATABASE.md</c> §1 item 1, §3).
    /// </summary>
    /// <param name="value">
    /// The Crit increase in <b>percentage points</b>, positive, transcribed from
    /// <c>CARD_RULES.md</c> §4.1. The interpretation is fixed to
    /// <see cref="CardEffectValueType.PercentagePoints"/> because that is what
    /// §4.1 states; it is not a parameter, so the unit cannot be silently changed
    /// at a call site.
    /// </param>
    /// <param name="scope">
    /// Which damage instances the increase applies to. It must be
    /// <see cref="NextAttackScope"/>, the only value <c>DATABASE.md</c> §3 defines
    /// (<c>string = "NextAttack"</c>). It is required and never defaulted: a Crit
    /// element without its scope is a missing required extra member, which §1
    /// item 6 rejects.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is not positive.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="scope"/> is not <see cref="NextAttackScope"/> — no other
    /// scope token is defined, and inventing one would author a rule no document
    /// states (<c>AGENTS.md</c> §7).
    /// </exception>
    public static CardEffectDefinition Crit(int value, string scope)
    {
        RequirePositiveValue(value);

        if (!string.Equals(scope, NextAttackScope, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"A Crit scope must be '{NextAttackScope}': DATABASE.md §3 defines that "
                + "as the only scope value ('scope string = \"NextAttack\", present iff "
                + "effectType = Crit'), and it stores the rule CARD_RULES.md §4.1 "
                + "already authors. No other scope token is defined, and none may be "
                + "invented (AGENTS.md §7).",
                nameof(scope));
        }

        return new CardEffectDefinition(
            CardEffectType.Crit,
            CardEffectValueType.PercentagePoints,
            value,
            duration: null,
            scope: NextAttackScope);
    }

    /// <summary>
    /// Builds a <b>Boss-only</b> <see cref="CardEffectType.AtkBuff"/> element — a
    /// temporary self ATK buff with its authored magnitude and duration
    /// (<c>BOSS_RULES.md</c> §6.3.1 item 1's Flame Burst; <c>DATABASE.md</c> §1
    /// item 1/§3 as extended by TASK-309 D-1).
    /// </summary>
    /// <param name="value">
    /// The ATK increase in <b>percentage points</b>, positive, transcribed from the
    /// approved Boss Card content (TASK-309 §1 B-2: <c>50</c> for `flame-burst`).
    /// The interpretation is fixed to
    /// <see cref="CardEffectValueType.PercentagePoints"/> because that is the unit
    /// <c>COMBAT_RULES.md</c> §5.5.1 composes, and the positive magnitude is the
    /// buff's direction.
    /// </param>
    /// <param name="duration">
    /// The number of Turns the buff lasts, positive, transcribed from the approved
    /// content (TASK-309 §1 B-2: <c>3</c> for `flame-burst`). It is required and
    /// never defaulted: an <c>AtkBuff</c> element without its duration is a missing
    /// required extra member, which <c>DATABASE.md</c> §1 item 6 rejects.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> or <paramref name="duration"/> is not positive.
    /// </exception>
    public static CardEffectDefinition AtkBuff(int value, int duration) =>
        TimedBossAtkModifier(
            CardEffectType.AtkBuff,
            value,
            duration,
            "an AtkBuff (BOSS_RULES.md §6.3.1 item 1's Flame Burst)");

    /// <summary>
    /// Builds a <b>Boss-only</b> <see cref="CardEffectType.AtkDebuff"/> element — a
    /// temporary ATK debuff on the active Pet, with its authored magnitude and
    /// duration (<c>BOSS_RULES.md</c> §6.3.1 item 3's Root; <c>DATABASE.md</c> §1
    /// item 1/§3 as extended by TASK-309 D-1).
    /// </summary>
    /// <param name="value">
    /// The ATK reduction in <b>percentage points</b>, positive, transcribed from the
    /// approved Boss Card content (TASK-309 §1 B-3: <c>30</c> for `root`). The
    /// interpretation is fixed to
    /// <see cref="CardEffectValueType.PercentagePoints"/>; the stored magnitude is
    /// the applied magnitude, and <c>COMBAT_RULES.md</c> §5.6.6 item 2 /
    /// <c>EffectivePetATK</c> map a reduction to <c>-|Magnitude|</c>, so the
    /// element stores <c>30</c>, never <c>-30</c>.
    /// </param>
    /// <param name="duration">
    /// The number of Turns the debuff lasts, positive, transcribed from the
    /// approved content (TASK-309 §1 B-3: <c>3</c> for `root`). It is required and
    /// never defaulted: an <c>AtkDebuff</c> element without its duration is a
    /// missing required extra member, which <c>DATABASE.md</c> §1 item 6 rejects.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> or <paramref name="duration"/> is not positive.
    /// </exception>
    public static CardEffectDefinition AtkDebuff(int value, int duration) =>
        TimedBossAtkModifier(
            CardEffectType.AtkDebuff,
            value,
            duration,
            "an AtkDebuff (BOSS_RULES.md §6.3.1 item 3's Root)");

    /// <summary>
    /// Builds a <b>Boss-only</b> <see cref="CardEffectType.NextAtkBuff"/> element —
    /// a self ATK buff that expires on the Boss's next qualifying attack
    /// (<c>BOSS_RULES.md</c> §6.3.1 item 5's Thunder Strike; <c>DATABASE.md</c> §1
    /// item 1/§3 as extended by TASK-309 D-1).
    /// </summary>
    /// <param name="value">
    /// The ATK increase in <b>percentage points</b>, positive, transcribed from the
    /// approved Boss Card content (TASK-309 §1: <c>50</c> for `thunder-strike`).
    /// The interpretation is fixed to
    /// <see cref="CardEffectValueType.PercentagePoints"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is not positive.
    /// </exception>
    public static CardEffectDefinition NextAtkBuff(int value)
    {
        RequirePositiveValue(value);

        // The one scope value DATABASE.md §3 defines stores BOSS_RULES.md §6.3.1
        // item 5's "next qualifying attack" lifetime. It is fixed here rather than
        // taken as a parameter, so the lifetime cannot be silently changed at a
        // call site, and no second scope token is introduced (AGENTS.md §7).
        return new CardEffectDefinition(
            CardEffectType.NextAtkBuff,
            CardEffectValueType.PercentagePoints,
            value,
            duration: null,
            scope: NextAttackScope);
    }

    /// <summary>
    /// Builds a <b>Boss-only</b> <see cref="CardEffectType.PowerDrain"/> element —
    /// an instant flat reduction of the active Pet's Power
    /// (<c>BOSS_RULES.md</c> §6.3.1 item 2's Drain Power; <c>DATABASE.md</c> §1
    /// item 1/§3 as extended by TASK-309 D-1).
    /// </summary>
    /// <param name="value">
    /// The flat Power amount, positive, transcribed from the approved Boss Card
    /// content (TASK-309 §1: <c>20</c> for `drain-power`). The interpretation is
    /// fixed to <see cref="CardEffectValueType.Flat"/> because
    /// <c>BOSS_RULES.md</c> §6.3.1 item 2 makes the drain "flat Power reduction
    /// (not a percentage)".
    /// </param>
    /// <remarks>
    /// The element carries <b>no</b> <c>duration</c> and <b>no</b> <c>scope</c>:
    /// §6.3.1 item 2 records "Duration: None", because the drain is an instant stat
    /// reduction rather than a persistent status effect.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is not positive.
    /// </exception>
    public static CardEffectDefinition PowerDrain(int value)
    {
        RequirePositiveValue(value);

        return new CardEffectDefinition(
            CardEffectType.PowerDrain,
            CardEffectValueType.Flat,
            value,
            duration: null,
            scope: null);
    }

    /// <summary>
    /// Builds one of the two Turn-based Boss-only ATK modifiers
    /// (<see cref="CardEffectType.AtkBuff"/> / <see cref="CardEffectType.AtkDebuff"/>)
    /// — the shape <c>BOSS_RULES.md</c> §6.3.1 items 1 and 3 give Flame Burst and
    /// Root: a positive percentage-point magnitude plus the Turns it lasts
    /// (<c>DATABASE.md</c> §1 item 1/§3, TASK-309 D-1).
    /// </summary>
    private static CardEffectDefinition TimedBossAtkModifier(
        CardEffectType effectType,
        int value,
        int duration,
        string describedAs)
    {
        // An undefined member cannot reach a factory that names the identity itself,
        // but the guard keeps the same "defined member only" contract the other
        // factories state rather than assuming the caller's value.
        RequireDefinedEffectType(effectType);
        RequirePositiveValue(value);

        if (duration <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                $"A {effectType} duration must be positive: DATABASE.md §3 (extended by "
                + "TASK-309 D-1) states the bound as 'int, > 0, present iff effectType = "
                + $"AtkBuff | AtkDebuff', and {describedAs} authors its duration in whole "
                + "Turns. An unset duration is not a duration no document authors "
                + "(AGENTS.md §7).");
        }

        return new CardEffectDefinition(
            effectType,
            CardEffectValueType.PercentagePoints,
            value,
            duration,
            scope: null);
    }

    /// <summary>
    /// Builds an effect element whose owning document states the effect but
    /// <b>no magnitude</b>.
    ///
    /// <b>This is how an unauthored magnitude is represented without inventing
    /// one</b> (<c>DATABASE.md</c> §1 item 9). Inventing a number is forbidden
    /// (<c>AGENTS.md</c> §7), so the effect's identity is recorded with
    /// <see cref="CardEffectValueType.Undetermined"/> and <see cref="Value"/> left
    /// absent — never <c>0</c>, never a placeholder, never a borrowed value. A
    /// resolver must treat the result as an open content gap.
    ///
    /// <b>The extra members follow the same rule.</b>
    /// <see cref="CardEffectType.Burn"/> requires a duration and
    /// <see cref="CardEffectType.Crit"/> requires a scope, so neither can be
    /// represented without one; a <c>Burn</c> or <c>Crit</c> element with no
    /// magnitude is therefore rejected here rather than stored with a missing
    /// required member. Use <see cref="Burn"/> or <see cref="Crit"/> once the
    /// owning document authors the parameters.
    ///
    /// <b>The four Boss-only effects are rejected here too.</b> TASK-309 D-1 fixes
    /// each one's interpretation and shape — <see cref="CardEffectType.AtkBuff"/>
    /// and <see cref="CardEffectType.AtkDebuff"/> carry a duration,
    /// <see cref="CardEffectType.NextAtkBuff"/> carries the scope, and
    /// <see cref="CardEffectType.PowerDrain"/> is always
    /// <see cref="CardEffectValueType.Flat"/> with no extra member — so none of
    /// them has an undetermined-magnitude form. Build them through
    /// <see cref="AtkBuff"/>, <see cref="AtkDebuff"/>, <see cref="NextAtkBuff"/>,
    /// and <see cref="PowerDrain"/>.
    /// </summary>
    /// <param name="effectType">
    /// Which domain effect applies, from the closed <see cref="CardEffectType"/>
    /// set.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="effectType"/> is not a defined member.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="effectType"/> is <see cref="CardEffectType.Burn"/> or
    /// <see cref="CardEffectType.Crit"/>, whose elements must always carry
    /// <c>duration</c> / <c>scope</c>, or one of the four Boss-only effects, whose
    /// fixed shape has no undetermined-magnitude representation.
    /// </exception>
    public static CardEffectDefinition Undetermined(CardEffectType effectType)
    {
        RequireDefinedEffectType(effectType);
        RequireNoRequiredExtraMember(effectType, nameof(effectType));
        RequireDedicatedBossOnlyFactory(effectType, nameof(effectType));

        return new CardEffectDefinition(
            effectType,
            CardEffectValueType.Undetermined,
            value: null,
            duration: null,
            scope: null);
    }

    /// <summary>
    /// Whether <paramref name="effectType"/> is a member this contract defines
    /// — the closed set of <see cref="CardEffectType"/>.
    /// </summary>
    public static bool IsDefinedEffectType(CardEffectType effectType) =>
        Enum.IsDefined(effectType);

    /// <summary>
    /// Whether <paramref name="valueType"/> is an interpretation this contract
    /// defines — the closed set of <see cref="CardEffectValueType"/>.
    /// </summary>
    public static bool IsDefinedValueType(CardEffectValueType valueType) =>
        Enum.IsDefined(valueType);

    /// <summary>
    /// Writes this element as its persisted JSON object (<c>DATABASE.md</c>
    /// §1). One element is one object; the enclosing array is written by
    /// <see cref="CardEffectDefinitions.ToPersistedPayload"/>.
    ///
    /// <code>
    /// { "effectType": "Shield", "valueType": "PercentMaxHp", "value": 20 }
    /// { "effectType": "Burn", "valueType": "Flat", "value": 50, "duration": 2 }
    /// { "effectType": "Crit", "valueType": "PercentagePoints", "value": 10, "scope": "NextAttack" }
    /// { "effectType": "AtkBuff", "valueType": "PercentagePoints", "value": 50, "duration": 3 }
    /// { "effectType": "PowerDrain", "valueType": "Flat", "value": 20 }
    /// { "effectType": "AtkDebuff", "valueType": "PercentagePoints", "value": 30, "duration": 3 }
    /// { "effectType": "NextAtkBuff", "valueType": "PercentagePoints", "value": 50, "scope": "NextAttack" }
    /// </code>
    ///
    /// <b>The member names are the storage contract.</b> <c>effectType</c>,
    /// <c>valueType</c>, and <c>value</c> are TASK-108's, and <c>duration</c> /
    /// <c>scope</c> are TASK-111 D-3's; <c>DATABASE.md</c> §1 item 2 fixes them,
    /// and the internal representation maps to them, not vice versa — the same
    /// convention the <c>BossDefinition</c> JSON documents follow. The type
    /// members are written as their <b>member names</b> so a persisted element is
    /// self-describing rather than an enum ordinal whose meaning a reordering
    /// could silently change (§1 item 2).
    ///
    /// <b>The output is deterministic and canonical</b> (<c>TDD.md</c> §6): the
    /// members are written in a fixed order, no property-name policy or
    /// indentation is applied, and each optional member is emitted only when the
    /// contract requires it — so the same rule always produces the same bytes and
    /// two rules that differ in any member never share a payload.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The element holds an undefined <see cref="EffectType"/> or
    /// <see cref="ValueType"/> — which the factories make unreachable — or an
    /// interpreting <see cref="ValueType"/> with a non-positive or absent
    /// <see cref="Value"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The element's extra members do not satisfy §3's present-iff condition —
    /// a <see cref="CardEffectType.Burn"/>,
    /// <see cref="CardEffectType.AtkBuff"/>, or
    /// <see cref="CardEffectType.AtkDebuff"/> without a positive
    /// <see cref="Duration"/>, a <see cref="CardEffectType.Crit"/> or
    /// <see cref="CardEffectType.NextAtkBuff"/> without
    /// <see cref="NextAttackScope"/>, or a <c>duration</c>/<c>scope</c> on an
    /// effect that defines none. A Boss-only element whose interpretation is not
    /// the one TASK-309 D-1 fixes
    /// (<see cref="CardEffectValueType.PercentagePoints"/> for AtkBuff/AtkDebuff/
    /// NextAtkBuff, <see cref="CardEffectValueType.Flat"/> for PowerDrain) is
    /// rejected on the same basis.
    /// </exception>
    public string ToPersistedPayload()
    {
        RequireDefinedEffectType(EffectType);
        RequireDefinedValueType(ValueType);

        // An undetermined effect carries no magnitude member at all — emitting
        // `"value": null` would be a second representation of the same absence
        // (GAME_STATE.md §0 item 5), and emitting a number would invent one.
        if (ValueType == CardEffectValueType.Undetermined)
        {
            if (Value is not null)
            {
                throw new ArgumentException(
                    "An undetermined effect states no magnitude and must carry no value "
                    + "member (DATABASE.md §3). A value here would either be a placeholder "
                    + "or a magnitude no document authors (AGENTS.md §7).",
                    nameof(Value));
            }

            RequireNoExtraMembersOnWrite();

            return string.Create(
                CultureInfo.InvariantCulture,
                $$"""{"effectType":"{{EffectType}}","valueType":"{{ValueType}}"}""");
        }

        if (Value is not > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Value),
                Value,
                "An interpreting Card effect value type requires a positive value "
                + "(DATABASE.md §3); an absent magnitude must be recorded with "
                + "CardEffectValueType.Undetermined instead.");
        }

        return EffectType switch
        {
            CardEffectType.Burn => string.Create(
                CultureInfo.InvariantCulture,
                $$"""{"effectType":"{{EffectType}}","valueType":"{{ValueType}}","value":{{Value}},"duration":{{RequireDurationForBurn()}}}"""),

            CardEffectType.Crit => string.Create(
                CultureInfo.InvariantCulture,
                $$"""{"effectType":"{{EffectType}}","valueType":"{{ValueType}}","value":{{Value}},"scope":"{{RequireScopeForCrit()}}"}"""),

            CardEffectType.AtkBuff or CardEffectType.AtkDebuff => string.Create(
                CultureInfo.InvariantCulture,
                $$"""{"effectType":"{{EffectType}}","valueType":"{{ValueType}}","value":{{Value}},"duration":{{RequireDurationForTimedBossAtkModifier()}}}"""),

            CardEffectType.NextAtkBuff => string.Create(
                CultureInfo.InvariantCulture,
                $$"""{"effectType":"{{EffectType}}","valueType":"{{ValueType}}","value":{{Value}},"scope":"{{RequireScopeForNextAtkBuff()}}"}"""),

            CardEffectType.PowerDrain => string.Create(
                CultureInfo.InvariantCulture,
                $$"""{"effectType":"{{EffectType}}","valueType":"{{RequireFlatForPowerDrain()}}","value":{{Value}}}"""),

            _ => string.Create(
                CultureInfo.InvariantCulture,
                $$"""{"effectType":"{{EffectType}}","valueType":"{{ValueType}}","value":{{Value}}}"""),
        };
    }

    /// <summary>
    /// Reads <b>one</b> persisted effect element (<c>DATABASE.md</c> §1) back
    /// into an effect rule. A whole column value is an array and is read with
    /// <see cref="CardEffectDefinitions.FromPersistedPayload"/>.
    ///
    /// <b>It fails loudly rather than defaulting.</b> A payload that is not a
    /// JSON object, that omits or misspells a required member, that names an
    /// effect identity or value interpretation this contract does not define,
    /// that carries a non-positive or non-integral value, or whose extra members
    /// do not satisfy §3's present-iff condition, is <b>rejected</b>. There is no
    /// fallback magnitude, no fallback to treating the payload as prose (which
    /// <c>DATABASE.md</c> §1 no longer stores, and which D-1 forbids the runtime
    /// from parsing), and no silent no-op for an unrecognized effect
    /// (<c>AGENTS.md</c> §7). A malformed or truncated column value therefore
    /// surfaces as a failure at the read, not as a Card that quietly does
    /// nothing.
    /// </summary>
    /// <param name="payload">
    /// One stored JSON object, e.g.
    /// <c>{ "effectType": "Shield", "valueType": "PercentMaxHp", "value": 20 }</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="payload"/> is null, empty, or whitespace; or it is not a
    /// valid structured effect element.
    /// </exception>
    public static CardEffectDefinition FromPersistedPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException(
                "A Card effect payload is required (DATABASE.md §1); an absent or "
                + "empty value is not a default effect.",
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
                $"The stored Card effect payload is not valid JSON ({exception.Message}). "
                + "DATABASE.md §1 stores the structured object; a malformed or truncated "
                + "value is rejected rather than defaulted.",
                nameof(payload),
                exception);
        }

        if (document is null)
        {
            throw new ArgumentException(
                "The stored Card effect payload deserialized to nothing; DATABASE.md §1 "
                + "requires an object carrying effectType, valueType, and value.",
                nameof(payload));
        }

        return FromStoredDocument(document, payload);
    }

    /// <summary>
    /// Whether <paramref name="payload"/> is a well-formed structured effect
    /// element — the non-throwing counterpart of
    /// <see cref="FromPersistedPayload"/>.
    ///
    /// It exists so a caller that must decide rather than throw still reaches
    /// exactly the same verdict; it defines no rule of its own and delegates to
    /// the reader.
    /// </summary>
    public static bool TryFromPersistedPayload(
        string? payload,
        out CardEffectDefinition effect)
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
    /// instead of arriving as a CLR default — the same "no default" standard
    /// <c>CardDefinition.LoadoutCopyLimit</c> is held to.
    /// </summary>
    private static CardEffectDefinition FromStoredDocument(
        StoredEffectDocument document,
        string payload)
    {
        if (string.IsNullOrWhiteSpace(document.EffectType))
        {
            throw new ArgumentException(
                "The stored Card effect payload has no effectType member; D-1 requires "
                + "the effect identity to come from the structured data, and absence is "
                + "not a default (AGENTS.md §7).",
                nameof(payload));
        }

        if (string.IsNullOrWhiteSpace(document.ValueType))
        {
            throw new ArgumentException(
                "The stored Card effect payload has no valueType member; D-3 requires the "
                + "value's interpretation to be carried, and absence is not a default.",
                nameof(payload));
        }

        if (!TryParseEnum(document.EffectType, out CardEffectType effectType))
        {
            throw new ArgumentException(
                $"The stored Card effect payload names an effect identity this contract "
                + $"does not define ('{document.EffectType}'). The defined identities are "
                + $"{string.Join(", ", Enum.GetNames<CardEffectType>())}; an unrecognized "
                + "effect is rejected rather than ignored (AGENTS.md §7).",
                nameof(payload));
        }

        if (!TryParseEnum(document.ValueType, out CardEffectValueType valueType))
        {
            throw new ArgumentException(
                $"The stored Card effect payload names a value interpretation this contract "
                + $"does not define ('{document.ValueType}'). The defined interpretations "
                + $"are {string.Join(", ", Enum.GetNames<CardEffectValueType>())}; an "
                + "unsupported interpretation is rejected rather than assumed to be flat.",
                nameof(payload));
        }

        // The magnitude's presence is decided BY the interpretation, which is the
        // point of carrying it: an interpreting type must state a value, and an
        // undetermined type must not. Neither case is defaulted, and a mismatched
        // pair is rejected rather than reconciled.
        if (valueType == CardEffectValueType.Undetermined)
        {
            if (document.Value is not null)
            {
                throw new ArgumentException(
                    "The stored Card effect payload records an undetermined magnitude but "
                    + "also carries a value member. An undetermined effect states no "
                    + "magnitude, and a value there would either be a placeholder or a "
                    + "magnitude no document authors (AGENTS.md §7).",
                    nameof(payload));
            }

            return Undetermined(effectType);
        }

        if (document.Value is null)
        {
            throw new ArgumentException(
                "The stored Card effect payload has no value member; an interpreting "
                + "value type requires the effect magnitude, which is never defaulted "
                + "(DATABASE.md §3). Record an unauthored magnitude with the "
                + "Undetermined value type instead.",
                nameof(payload));
        }

        // §3's present-iff conditions, as extended by TASK-309 D-1. The pattern
        // match is total over the closed enum: Burn and the two Turn-based Boss ATK
        // modifiers require `duration` and may not carry `scope`; Crit and
        // NextAtkBuff require `scope` and may not carry `duration`; PowerDrain
        // carries neither and is flat; every other identity carries neither and is
        // rejected if it does.
        return effectType switch
        {
            CardEffectType.Burn => ParseBurn(document, valueType, payload),
            CardEffectType.Crit => ParseCrit(document, valueType, payload),
            CardEffectType.AtkBuff or CardEffectType.AtkDebuff =>
                ParseTimedBossAtkModifier(effectType, document, valueType, payload),
            CardEffectType.NextAtkBuff => ParseNextAtkBuff(document, valueType, payload),
            CardEffectType.PowerDrain => ParsePowerDrain(document, valueType, payload),
            _ => ParsePlain(document, effectType, valueType, payload),
        };
    }

    private static CardEffectDefinition ParseBurn(
        StoredEffectDocument document,
        CardEffectValueType valueType,
        string payload)
    {
        if (document.Duration is null)
        {
            throw new ArgumentException(
                "The stored Burn effect element has no duration member; DATABASE.md §3 "
                + "requires it ('int, > 0, present iff effectType = Burn'), and §1 item 6 "
                + "rejects a missing required extra member rather than defaulting it.",
                nameof(payload));
        }

        if (document.Scope is not null)
        {
            throw new ArgumentException(
                "The stored Burn effect element carries a scope member. DATABASE.md §1 "
                + "defines `scope` on a Crit or NextAtkBuff element only, and §1 item 6 "
                + "rejects a malformed effect rather than ignoring what it does not "
                + "define.",
                nameof(payload));
        }

        // Burn's per-tick damage is authored by CARD_RULES.md §4.1 and carries no
        // percentage-point unit, so the interpretation is validated by the shared
        // positive-value rule below rather than assumed.
        RequireNotPercentagePoints(valueType, CardEffectType.Burn, payload);

        return Burn(valueType, document.Value!.Value, document.Duration.Value);
    }

    private static CardEffectDefinition ParseCrit(
        StoredEffectDocument document,
        CardEffectValueType valueType,
        string payload)
    {
        if (string.IsNullOrWhiteSpace(document.Scope))
        {
            throw new ArgumentException(
                "The stored Crit effect element has no scope member; DATABASE.md §3 "
                + "requires it ('scope string = \"NextAttack\", present iff effectType = "
                + "Crit'), and §1 item 6 rejects a missing required extra member rather "
                + "than defaulting it.",
                nameof(payload));
        }

        if (document.Duration is not null)
        {
            throw new ArgumentException(
                "The stored Crit effect element carries a duration member. DATABASE.md §1 "
                + "defines `duration` on a Burn/AtkBuff/AtkDebuff element only, and §1 "
                + "item 6 rejects a malformed effect rather than ignoring what it does "
                + "not define.",
                nameof(payload));
        }

        // CARD_RULES.md §4.1 states the Crit increase in percentage points, so
        // DATABASE.md §1's example and this contract fix the interpretation. Any
        // other one would misstate the unit, so it is rejected rather than
        // silently reinterpreted.
        if (valueType != CardEffectValueType.PercentagePoints)
        {
            throw new ArgumentException(
                $"The stored Crit effect element names the value interpretation "
                + $"'{valueType}'. CARD_RULES.md §4.1 states the Crit increase in "
                + "percentage points, which DATABASE.md §1 item 1 carries as "
                + $"{nameof(CardEffectValueType.PercentagePoints)}; another "
                + "interpretation would misstate the magnitude rather than describe it.",
                nameof(payload));
        }

        return Crit(document.Value!.Value, document.Scope!);
    }

    /// <summary>
    /// Parses a stored <see cref="CardEffectType.AtkBuff"/> or
    /// <see cref="CardEffectType.AtkDebuff"/> element — the authored
    /// percentage-point magnitude plus its required duration, with no scope
    /// (<c>BOSS_RULES.md</c> §6.3.1 items 1 and 3; <c>DATABASE.md</c> §1 item 1/§3
    /// as extended by TASK-309 D-1).
    /// </summary>
    private static CardEffectDefinition ParseTimedBossAtkModifier(
        CardEffectType effectType,
        StoredEffectDocument document,
        CardEffectValueType valueType,
        string payload)
    {
        if (document.Duration is null)
        {
            throw new ArgumentException(
                $"The stored {effectType} effect element has no duration member; "
                + "DATABASE.md §3 (extended by TASK-309 D-1) requires it ('int, > 0, "
                + "present iff effectType = AtkBuff | AtkDebuff'), and §1 item 6 rejects "
                + "a missing required extra member rather than defaulting it.",
                nameof(payload));
        }

        if (document.Scope is not null)
        {
            throw new ArgumentException(
                $"The stored {effectType} effect element carries a scope member. "
                + "DATABASE.md §1 defines `scope` on a Crit or NextAtkBuff element only, "
                + "and §1 item 6 rejects a malformed effect rather than ignoring what it "
                + "does not define.",
                nameof(payload));
        }

        // The ATK modifier is a percentage-point quantity on both entities
        // (COMBAT_RULES.md §5.5.1, §5.6.6 item 2), so any other interpretation
        // would misstate the unit.
        if (valueType != CardEffectValueType.PercentagePoints)
        {
            throw new ArgumentException(
                $"The stored {effectType} effect element names the value interpretation "
                + $"'{valueType}'. BOSS_RULES.md §6.3.1 states an ATK modifier in "
                + "percentage points, which DATABASE.md §1 item 1 carries as "
                + $"{nameof(CardEffectValueType.PercentagePoints)}; another "
                + "interpretation would misstate the magnitude rather than describe it.",
                nameof(payload));
        }

        return TimedBossAtkModifier(
            effectType,
            document.Value!.Value,
            document.Duration.Value,
            effectType == CardEffectType.AtkBuff
                ? "an AtkBuff (BOSS_RULES.md §6.3.1 item 1's Flame Burst)"
                : "an AtkDebuff (BOSS_RULES.md §6.3.1 item 3's Root)");
    }

    /// <summary>
    /// Parses a stored <see cref="CardEffectType.NextAtkBuff"/> element — the
    /// authored percentage-point magnitude plus the required
    /// <see cref="NextAttackScope"/> scope, with no duration
    /// (<c>BOSS_RULES.md</c> §6.3.1 item 5; <c>DATABASE.md</c> §1 item 1/§3 as
    /// extended by TASK-309 D-1).
    /// </summary>
    private static CardEffectDefinition ParseNextAtkBuff(
        StoredEffectDocument document,
        CardEffectValueType valueType,
        string payload)
    {
        if (string.IsNullOrWhiteSpace(document.Scope))
        {
            throw new ArgumentException(
                "The stored NextAtkBuff effect element has no scope member; DATABASE.md "
                + "§3 (extended by TASK-309 D-1) requires it ('scope string = "
                + "\"NextAttack\", present iff effectType = Crit | NextAtkBuff'), and §1 "
                + "item 6 rejects a missing required extra member rather than defaulting "
                + "it.",
                nameof(payload));
        }

        // DATABASE.md §3 defines exactly one scope value. An unrecognized token is
        // rejected rather than normalized to NextAttack: the persistence contract
        // fixes the lifetime, and silently rewriting a different token would accept
        // definition data no document authors (AGENTS.md §7).
        if (!string.Equals(document.Scope, NextAttackScope, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The stored NextAtkBuff effect element names scope '{document.Scope}'. "
                + $"DATABASE.md §3 defines '{NextAttackScope}' as the only scope value, and "
                + "BOSS_RULES.md §6.3.1 item 5 authors that next-qualifying-attack "
                + "lifetime; no other scope token is defined and none may be invented "
                + "(AGENTS.md §7).",
                nameof(payload));
        }

        if (document.Duration is not null)
        {
            throw new ArgumentException(
                "The stored NextAtkBuff effect element carries a duration member. Its "
                + "lifetime is the trigger-based NextQualifyingAttack condition "
                + "(GAME_STATE.md §2.3.1 item 3, §5.1.1 item 7), and DATABASE.md §3 "
                + "defines `duration` on a Burn/AtkBuff/AtkDebuff element only.",
                nameof(payload));
        }

        if (valueType != CardEffectValueType.PercentagePoints)
        {
            throw new ArgumentException(
                $"The stored NextAtkBuff effect element names the value interpretation "
                + $"'{valueType}'. BOSS_RULES.md §6.3.1 item 5 states the ATK modifier in "
                + "percentage points, which DATABASE.md §1 item 1 carries as "
                + $"{nameof(CardEffectValueType.PercentagePoints)}; another "
                + "interpretation would misstate the magnitude rather than describe it.",
                nameof(payload));
        }

        return NextAtkBuff(document.Value!.Value);
    }

    /// <summary>
    /// Parses a stored <see cref="CardEffectType.PowerDrain"/> element — a flat
    /// magnitude with neither extra member (<c>BOSS_RULES.md</c> §6.3.1 item 2;
    /// <c>DATABASE.md</c> §1 item 1/§3 as extended by TASK-309 D-1).
    /// </summary>
    private static CardEffectDefinition ParsePowerDrain(
        StoredEffectDocument document,
        CardEffectValueType valueType,
        string payload)
    {
        if (document.Duration is not null)
        {
            throw new ArgumentException(
                "The stored PowerDrain effect element carries a duration member. "
                + "BOSS_RULES.md §6.3.1 item 2 states 'Duration: None', and DATABASE.md "
                + "§3 defines `duration` on a Burn/AtkBuff/AtkDebuff element only.",
                nameof(payload));
        }

        if (document.Scope is not null)
        {
            throw new ArgumentException(
                "The stored PowerDrain effect element carries a scope member. "
                + "DATABASE.md §3 defines `scope` on a Crit or NextAtkBuff element only, "
                + "and §1 item 6 rejects a malformed effect rather than ignoring what it "
                + "does not define.",
                nameof(payload));
        }

        if (valueType != CardEffectValueType.Flat)
        {
            throw new ArgumentException(
                $"The stored PowerDrain effect element names the value interpretation "
                + $"'{valueType}'. BOSS_RULES.md §6.3.1 item 2 makes the drain a flat "
                + "Power reduction, which DATABASE.md §1 item 1 carries as "
                + $"{nameof(CardEffectValueType.Flat)}; another interpretation would "
                + "misstate the magnitude rather than describe it.",
                nameof(payload));
        }

        return PowerDrain(document.Value!.Value);
    }

    private static CardEffectDefinition ParsePlain(
        StoredEffectDocument document,
        CardEffectType effectType,
        CardEffectValueType valueType,
        string payload)
    {
        if (document.Duration is not null)
        {
            throw new ArgumentException(
                $"The stored {effectType} effect element carries a duration member. "
                + "DATABASE.md §3 defines `duration` on a Burn/AtkBuff/AtkDebuff element "
                + "only ('present iff effectType = Burn | AtkBuff | AtkDebuff'), and §1 "
                + "item 6 rejects a malformed effect rather than ignoring what the "
                + "contract does not define.",
                nameof(payload));
        }

        if (document.Scope is not null)
        {
            throw new ArgumentException(
                $"The stored {effectType} effect element carries a scope member. "
                + "DATABASE.md §3 defines `scope` on a Crit or NextAtkBuff element only "
                + "('present iff effectType = Crit | NextAtkBuff'), and §1 item 6 rejects "
                + "a malformed effect rather than ignoring what the contract does not "
                + "define.",
                nameof(payload));
        }

        RequireNotPercentagePoints(valueType, effectType, payload);

        return Create(effectType, valueType, document.Value!.Value);
    }

    /// <summary>
    /// Rejects <see cref="CardEffectValueType.PercentagePoints"/> on an effect
    /// whose owning document states no percentage-point quantity.
    ///
    /// The interpretation is not free-floating: it exists because
    /// <c>CARD_RULES.md</c> §4.1 states the Crit increase in percentage points
    /// (TASK-111 D-3) and <c>BOSS_RULES.md</c> §6.3.1 states the Boss-only ATK
    /// modifiers in percentage points (TASK-309 D-1), so pairing it with any other
    /// effect would assert a unit no document authors for that effect.
    /// </summary>
    private static void RequireNotPercentagePoints(
        CardEffectValueType valueType,
        CardEffectType effectType,
        string payload)
    {
        if (valueType == CardEffectValueType.PercentagePoints)
        {
            throw new ArgumentException(
                $"The stored {effectType} effect element names the value interpretation "
                + $"'{nameof(CardEffectValueType.PercentagePoints)}'. Percentage points are "
                + "authored for the Crit increase (CARD_RULES.md §4.1) and the Boss-only "
                + "ATK modifiers (BOSS_RULES.md §6.3.1), so the interpretation is rejected "
                + "here rather than applied to an effect whose owning document states no "
                + "percentage points (AGENTS.md §7).",
                nameof(payload));
        }
    }

    /// <summary>
    /// Parses a stored enum token by its member name, and never by its numeric
    /// value.
    ///
    /// <b>The name is the contract.</b> The persisted tokens are
    /// <see cref="CardEffectType"/>'s and <see cref="CardEffectValueType"/>'s
    /// member names (<c>DATABASE.md</c> §1 item 2), so a stored ordinal is
    /// <b>not</b> accepted: it would make the meaning of a persisted row depend
    /// on member order, which the enum documentation states no rule derives
    /// from. The match is ordinal and case-sensitive for the same reason the Boss
    /// storage tokens are — two spellings of one token are not two tokens.
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

    private static void RequireDefinedEffectType(CardEffectType effectType)
    {
        if (!IsDefinedEffectType(effectType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectType),
                effectType,
                "The effect identity must be a CardEffectType member; DATABASE.md §1 item 1 "
                + "closes the set at Heal | Shield | Power | Damage | Burn | Crit plus the "
                + "four Boss-only identities TASK-309 D-1 adds (AtkBuff | PowerDrain | "
                + "AtkDebuff | NextAtkBuff), and an undefined value must not become a "
                + "silent no-op (AGENTS.md §7).");
        }
    }

    private static void RequireDefinedValueType(CardEffectValueType valueType)
    {
        if (!IsDefinedValueType(valueType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(valueType),
                valueType,
                "The value interpretation must be a CardEffectValueType member; an "
                + "unsupported interpretation must not be assumed to be Flat (DATABASE.md "
                + "§1 item 1).");
        }
    }

    /// <summary>
    /// Requires <paramref name="valueType"/> to be one of the three members that
    /// <b>interpret</b> a value, rather than
    /// <see cref="CardEffectValueType.Undetermined"/>.
    ///
    /// It guards the factories that take a value: pairing an undetermined
    /// interpretation with a number would represent both "a magnitude exists" and
    /// "no magnitude is authored", which <c>GAME_STATE.md</c> §0 item 5 forbids as
    /// two representations of one fact. The undetermined case has its own factory.
    /// </summary>
    private static void RequireInterpretingValueType(CardEffectValueType valueType)
    {
        RequireDefinedValueType(valueType);

        if (valueType == CardEffectValueType.Undetermined)
        {
            throw new ArgumentOutOfRangeException(
                nameof(valueType),
                valueType,
                "An undetermined value type states no magnitude and cannot be paired with "
                + "a value (DATABASE.md §3: 'present iff valueType interprets one'). Build "
                + "it with CardEffectDefinition.Undetermined instead.");
        }
    }

    /// <summary>
    /// Requires the effect to carry no extra member by contract, so the factories
    /// that take no extra member cannot build an element §3 would reject.
    ///
    /// <see cref="CardEffectType.Burn"/> always carries <c>duration</c> and
    /// <see cref="CardEffectType.Crit"/> always carries <c>scope</c>; neither can
    /// be omitted, so neither may be built through the plain-value path.
    /// </summary>
    private static void RequireNoRequiredExtraMember(
        CardEffectType effectType,
        string parameterName)
    {
        if (effectType is CardEffectType.Burn or CardEffectType.Crit)
        {
            string required = effectType == CardEffectType.Burn ? "duration" : "scope";

            throw new ArgumentException(
                $"An effect of type {effectType} must always carry its '{required}' member "
                + $"(DATABASE.md §3: 'present iff effectType = {effectType}'), so it cannot "
                + $"be built without one. Use CardEffectDefinition.{effectType} instead: a "
                + "missing required extra member is rejected rather than defaulted "
                + "(DATABASE.md §1 item 6).",
                parameterName);
        }
    }

    /// <summary>
    /// Requires that a Boss-only effect is not built through the plain-value or
    /// undetermined paths.
    ///
    /// TASK-309 decision D-1 fixes each Boss-only effect's interpretation and
    /// element shape, so every one of them has exactly one representation, built
    /// by its own factory: <see cref="AtkBuff"/> and <see cref="AtkDebuff"/> fix
    /// <see cref="CardEffectValueType.PercentagePoints"/> plus a required
    /// duration, <see cref="NextAtkBuff"/> fixes
    /// <see cref="CardEffectValueType.PercentagePoints"/> plus the
    /// <see cref="NextAttackScope"/> scope, and <see cref="PowerDrain"/> fixes
    /// <see cref="CardEffectValueType.Flat"/> with no extra member. Admitting any
    /// of them through <see cref="Create"/> or <see cref="Undetermined"/> would
    /// let the caller choose a shape or unit the approved contract does not define
    /// (<c>AGENTS.md</c> §7), so the call is rejected rather than normalized.
    /// </summary>
    private static void RequireDedicatedBossOnlyFactory(
        CardEffectType effectType,
        string parameterName)
    {
        if (effectType is not (
            CardEffectType.AtkBuff
            or CardEffectType.PowerDrain
            or CardEffectType.AtkDebuff
            or CardEffectType.NextAtkBuff))
        {
            return;
        }

        throw new ArgumentException(
            $"An effect of type {effectType} is a Boss-only effect whose interpretation "
            + "and shape TASK-309 decision D-1 fixes (DATABASE.md §1 item 1/§3 as "
            + $"extended by that decision), so it must be built with "
            + $"CardEffectDefinition.{effectType}: AtkBuff/AtkDebuff take a "
            + "percentage-point value and a required duration, NextAtkBuff takes a "
            + "percentage-point value and the NextAttack scope, and PowerDrain takes a "
            + "flat value with no duration or scope. Another shape or unit is "
            + "definition data no document authors (AGENTS.md §7).",
            parameterName);
    }

    private static void RequirePositiveValue(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A Card effect value must be positive: DATABASE.md §3 states the bound as "
                + "'int, > 0', every magnitude CARD_RULES.md §2/§4.1 states is a positive "
                + "quantity, and the CLR default 0 of an unset value is not a magnitude "
                + "any document authors (AGENTS.md §7).");
        }
    }

    /// <summary>
    /// §3's Burn condition, enforced on the write path as well as the read: a
    /// <c>Burn</c> element is written only with a positive duration.
    /// </summary>
    private int RequireDurationForBurn()
    {
        if (Duration is not > 0)
        {
            throw new ArgumentException(
                "A Burn effect element must carry a positive duration member "
                + "(DATABASE.md §3: 'int, > 0, present iff effectType = Burn'). It is "
                + "required and never defaulted; a missing required extra member is "
                + "rejected rather than written (DATABASE.md §1 item 6).",
                nameof(Duration));
        }

        if (Scope is not null)
        {
            throw new ArgumentException(
                "A Burn effect element must not carry a scope member; DATABASE.md §3 "
                + "defines `scope` on a Crit or NextAtkBuff element only.",
                nameof(Scope));
        }

        return Duration.Value;
    }

    /// <summary>
    /// §3's Crit condition, enforced on the write path as well as the read: a
    /// <c>Crit</c> element is written only with the defined scope, and with the
    /// percentage-point interpretation.
    /// </summary>
    private string RequireScopeForCrit()
    {
        if (!string.Equals(Scope, NextAttackScope, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"A Crit effect element must carry scope '{NextAttackScope}' "
                + "(DATABASE.md §3: 'scope string = \"NextAttack\", present iff effectType "
                + "= Crit'). No other scope token is defined, and none may be invented "
                + "(AGENTS.md §7).",
                nameof(Scope));
        }

        if (Duration is not null)
        {
            throw new ArgumentException(
                "A Crit effect element must not carry a duration member; DATABASE.md §1 "
                + "defines `duration` on a Burn/AtkBuff/AtkDebuff element only.",
                nameof(Duration));
        }

        if (ValueType != CardEffectValueType.PercentagePoints)
        {
            throw new ArgumentException(
                $"A Crit effect element must use the "
                + $"{nameof(CardEffectValueType.PercentagePoints)} interpretation: "
                + "CARD_RULES.md §4.1 states the Crit increase in percentage points. Any "
                + "other interpretation would misstate the magnitude's unit.",
                nameof(ValueType));
        }

        return NextAttackScope;
    }

    /// <summary>
    /// §3's Boss-only Turn-based ATK-modifier condition, enforced on the write path
    /// as well as the read: an <see cref="CardEffectType.AtkBuff"/> or
    /// <see cref="CardEffectType.AtkDebuff"/> element is written only with a
    /// positive duration, no scope, and the percentage-point interpretation
    /// (<c>DATABASE.md</c> §1 item 1/§3 as extended by TASK-309 D-1;
    /// <c>BOSS_RULES.md</c> §6.3.1 items 1 and 3).
    /// </summary>
    private int RequireDurationForTimedBossAtkModifier()
    {
        if (Duration is not > 0)
        {
            throw new ArgumentException(
                $"An {EffectType} effect element must carry a positive duration member "
                + "(DATABASE.md §3, extended by TASK-309 D-1: 'int, > 0, present iff "
                + "effectType = AtkBuff | AtkDebuff'). It is required and never "
                + "defaulted; a missing required extra member is rejected rather than "
                + "written (DATABASE.md §1 item 6).",
                nameof(Duration));
        }

        if (Scope is not null)
        {
            throw new ArgumentException(
                $"An {EffectType} effect element must not carry a scope member. "
                + "DATABASE.md §3 defines `scope` on a Crit or NextAtkBuff element "
                + "only, and §1 item 6 rejects a malformed effect rather than ignoring "
                + "what the contract does not define.",
                nameof(Scope));
        }

        if (ValueType != CardEffectValueType.PercentagePoints)
        {
            throw new ArgumentException(
                $"An {EffectType} effect element must use the "
                + $"{nameof(CardEffectValueType.PercentagePoints)} interpretation: "
                + "COMBAT_RULES.md §5.5.1 (Boss) and §5.6.6 item 2 (Pet) compose ATK "
                + "modifiers as percentage points. Any other interpretation would "
                + "misstate the magnitude's unit.",
                nameof(ValueType));
        }

        return Duration.Value;
    }

    /// <summary>
    /// §3's <see cref="CardEffectType.NextAtkBuff"/> condition, enforced on the
    /// write path as well as the read: the element is written only with the defined
    /// scope, no duration, and the percentage-point interpretation
    /// (<c>DATABASE.md</c> §1 item 1/§3 as extended by TASK-309 D-1;
    /// <c>BOSS_RULES.md</c> §6.3.1 item 5).
    /// </summary>
    private string RequireScopeForNextAtkBuff()
    {
        if (!string.Equals(Scope, NextAttackScope, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"A NextAtkBuff effect element must carry scope '{NextAttackScope}' "
                + "(DATABASE.md §3: 'scope string = \"NextAttack\", present iff effectType "
                + "= Crit | NextAtkBuff'). No other scope token is defined, and none may "
                + "be invented (AGENTS.md §7).",
                nameof(Scope));
        }

        if (Duration is not null)
        {
            throw new ArgumentException(
                "A NextAtkBuff effect element must not carry a duration member. Its "
                + "lifetime is the trigger-based NextQualifyingAttack condition "
                + "(GAME_STATE.md §2.3.1 item 3, §5.1.1 item 7), and DATABASE.md §3 "
                + "defines `duration` on a Burn/AtkBuff/AtkDebuff element only.",
                nameof(Duration));
        }

        if (ValueType != CardEffectValueType.PercentagePoints)
        {
            throw new ArgumentException(
                "A NextAtkBuff effect element must use the "
                + $"{nameof(CardEffectValueType.PercentagePoints)} interpretation: "
                + "COMBAT_RULES.md §5.5.1 composes Boss ATK modifiers as percentage "
                + "points. Any other interpretation would misstate the unit.",
                nameof(ValueType));
        }

        return NextAttackScope;
    }

    /// <summary>
    /// §3's <see cref="CardEffectType.PowerDrain"/> condition, enforced on the write
    /// path as well as the read: the element is written only with the flat
    /// interpretation and no extra member (<c>DATABASE.md</c> §1 item 1/§3 as
    /// extended by TASK-309 D-1; <c>BOSS_RULES.md</c> §6.3.1 item 2).
    /// </summary>
    private CardEffectValueType RequireFlatForPowerDrain()
    {
        if (ValueType != CardEffectValueType.Flat)
        {
            throw new ArgumentException(
                "A PowerDrain effect element must use the "
                + $"{nameof(CardEffectValueType.Flat)} interpretation: BOSS_RULES.md "
                + "§6.3.1 item 2 makes the drain a flat Power reduction, not a "
                + "percentage. Another interpretation would misstate the magnitude.",
                nameof(ValueType));
        }

        if (Duration is not null)
        {
            throw new ArgumentException(
                "A PowerDrain effect element must not carry a duration member: "
                + "BOSS_RULES.md §6.3.1 item 2 states 'Duration: None' (an instant stat "
                + "reduction, not a persistent status effect).",
                nameof(Duration));
        }

        if (Scope is not null)
        {
            throw new ArgumentException(
                "A PowerDrain effect element must not carry a scope member: DATABASE.md "
                + "§3 defines `scope` on a Crit or NextAtkBuff element only.",
                nameof(Scope));
        }

        return CardEffectValueType.Flat;
    }

    /// <summary>
    /// Requires that a written undetermined element carries no extra member:
    /// <see cref="Duration"/> and <see cref="Scope"/> are meaningless without the
    /// magnitude and effect they qualify.
    /// </summary>
    private void RequireNoExtraMembersOnWrite()
    {
        if (Duration is not null)
        {
            throw new ArgumentException(
                "An undetermined effect element must carry no duration member; "
                + "DATABASE.md §1 defines `duration` on a Burn/AtkBuff/AtkDebuff "
                + "element, which also states its magnitude.",
                nameof(Duration));
        }

        if (Scope is not null)
        {
            throw new ArgumentException(
                "An undetermined effect element must carry no scope member; DATABASE.md §1 "
                + "defines `scope` on a Crit or NextAtkBuff element.",
                nameof(Scope));
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
        // not define. DATABASE.md §1 item 6 rejects a malformed effect rather
        // than ignoring what it does not define.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,

        // A repeated member is ambiguous, so the last one must not silently win.
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>
    /// The persisted members of one Card effect element — exactly
    /// <c>effectType</c>, <c>valueType</c>, <c>value</c>, <c>duration</c>, and
    /// <c>scope</c> (<c>DATABASE.md</c> §1, §3).
    ///
    /// Each member is nullable in this reader <b>deliberately</b>: that is what
    /// lets an omitted member be reported as omitted, rather than arriving as a
    /// CLR default indistinguishable from a real value. A present-but-wrong-typed
    /// member (a non-integral <c>value</c>, a numeric <c>effectType</c>) is a
    /// <see cref="JsonException"/> the reader reports as malformed.
    /// </summary>
    private sealed record StoredEffectDocument
    {
        [JsonPropertyName("effectType")]
        public string? EffectType { get; init; }

        [JsonPropertyName("valueType")]
        public string? ValueType { get; init; }

        [JsonPropertyName("value")]
        public int? Value { get; init; }

        [JsonPropertyName("duration")]
        public int? Duration { get; init; }

        [JsonPropertyName("scope")]
        public string? Scope { get; init; }
    }
}
