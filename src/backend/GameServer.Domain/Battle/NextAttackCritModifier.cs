namespace GameServer.Domain.Battle;

/// <summary>
/// One active temporary Crit modifier awaiting consumption by a qualifying owner
/// attack — an element of <c>PetState.NextAttackCritModifiers[]</c>
/// (<c>GAME_STATE.md</c> §2.3.4, <c>ADR-017</c>).
///
/// <code>
/// NextAttackCritModifier
/// ├── SourceIdentity     string, required  — the removal key
/// └── CritContribution   int, required     — percentage points
/// </code>
///
/// <b>Exactly two members is the whole schema, and there is no third.</b>
/// <c>GAME_STATE.md</c> §2.3.4 item 4 forbids a duration, a Turn counter, an
/// expiry label, a "consumed" flag, a priority, an ordering index, a target
/// reference, a remaining-use counter, or a timestamp: no rule reads any of them,
/// and a field kept "for later" is the speculative representation
/// <c>GAME_STATE.md</c> §0 item 5 forbids.
///
/// <b>The modifier is not a <see cref="StatusEffect"/>, deliberately.</b>
/// <c>GAME_STATE.md</c> §2.3.4 item 1 and <c>COMBAT_RULES.md</c> §3.3 item 11
/// state the boundary: it carries no <c>RemainingTurns</c> and no
/// <c>ExpiryCondition</c>, so it does not engage §2.3.1 item 3's exclusive
/// duration dichotomy (that rule is neither widened nor relaxed by this type) and
/// does not engage item 6's one-instance-per-identity rule, which continues to
/// govern <c>StatusEffects[]</c> alone. It is also not a pending/queued
/// application — §2.3.3's prohibition is honored because a modifier held here has
/// <i>already been applied</i>; it is awaiting an attack, not a write-back.
///
/// <b><see cref="SourceIdentity"/> is source-scoped and stable.</b>
/// <c>GAME_STATE.md</c> §2.3.4 item 2 requires it to identify the source that
/// created the modifier so consumption can remove exactly that modifier and
/// nothing else (§5.1.2 item 4). It is <b>not</b> a per-cast unique key, a GUID, a
/// timestamp, an allocation order, or an array position: it must be deterministic
/// and reproducible for a given input state (<c>TDD.md</c> §6), so a replayed or
/// recovered battle re-derives the same identities. It is a value, not a
/// definition — the source's rule, magnitude, and lifetime are not copied into it
/// (§0 item 5).
///
/// Because the identity is per <b>source</b>, a source re-applying refreshes its
/// own element rather than appending a second one
/// (<see cref="NextAttackCritModifiers.Apply"/>). Two simultaneous elements
/// therefore always mean two distinct sources — which is exactly what
/// <c>COMBAT_RULES.md</c> §3.3 item 10 needs in order to consume Iron Fang's and
/// Bạch Hổ's contributions together while removing each individually.
///
/// <b><see cref="CritContribution"/> is typed but not interpreted here.</b>
/// <c>GAME_STATE.md</c> §2.3.4 item 3 fixes only that the value is stored on the
/// element and that its unit is percentage points (matching
/// <c>CARD_RULES.md</c> §4.1 and <c>DATABASE.md</c> §3 item 1). How contributions
/// compose, and the cap they compose under, are gameplay rules owned by
/// <c>COMBAT_RULES.md</c> §3.3 items 7 and 10 and are not restated here.
///
/// This type is deliberately minimal and framework-independent
/// (<c>ARCHITECTURE.md</c> §2.1): it references no ASP.NET Core, SignalR, EF Core,
/// Redis, HTTP, Phaser, or Discord concern.
/// </summary>
/// <param name="SourceIdentity">
/// The stable identity of the source that created this modifier
/// (<c>GAME_STATE.md</c> §2.3.4 item 2). It is the collection's removal key: a
/// qualifying attack removes exactly the elements it consumed, by identity, and
/// removes nothing else (<c>COMBAT_RULES.md</c> §3.3 item 9).
/// </param>
/// <param name="CritContribution">
/// The Crit increase this modifier contributes, in percentage points
/// (<c>GAME_STATE.md</c> §2.3.4 item 3).
/// </param>
public readonly record struct NextAttackCritModifier(
    string SourceIdentity,
    int CritContribution)
{
    /// <summary>
    /// Whether this modifier carries a well-formed identity — the condition
    /// <c>GAME_STATE.md</c> §2.3.4 item 2 makes the removal key meaningful.
    ///
    /// A blank or whitespace identity is not a representable source: two
    /// different sources would collide on it and consumption could not tell them
    /// apart, which is the one property the identity exists to provide.
    /// </summary>
    public bool HasSourceIdentity => !string.IsNullOrWhiteSpace(SourceIdentity);
}
