namespace GameServer.Domain.Battle;

/// <summary>
/// One applied, <c>Battle</c>-scoped Burn-damage modifier on the active Pet — an
/// element of <c>PetState.BurnDamageModifiers[]</c>.
///
/// <code>
/// BurnDamageModifier
/// ├── SourceIdentity            string, required — the replace/refresh and
/// │                                                 removal key
/// └── BurnDamagePercentage      int, required    — percentage points
/// </code>
///
/// <b>What it is.</b> The applied form of the Relic <c>BurnDamage</c> effect
/// <c>RELIC_RULES.md</c> §8.2 item 1 defines — "a percentage modifier to Burn
/// damage-over-time ticks (<c>COMBAT_RULES.md</c> §5)" — whose one provisioned
/// identity is Burning Curse (§8.5 item 5: <c>+30%</c>, <c>Percentage</c>,
/// <c>target: Pet</c>, <c>Battle</c> lifetime). It is the Pet's own standing
/// modification, which is what §6 note 1 means by "<c>target: Pet</c> identifies
/// the Pet as the owner/source context".
///
/// <b>It modifies Pet-owned Burn only, and ownership is the Burn instance's
/// source.</b> §6 note 1 and <c>COMBAT_RULES.md</c> §5.2 item 4 scope this
/// modifier by the Burn instance's <b>source/ownership</b>: a Burn the Pet
/// applied is modified, a Burn the Boss applied is not, and the distinction is
/// never the entity receiving the tick's damage (TASK-178 Product Owner decision
/// <b>Q-4 = C</b>). The smallest existing representation of that ownership is
/// the instance's own <see cref="StatusEffect.Source"/> — the one member
/// <c>COMBAT_RULES.md</c> §5.2 item 1 requires every Status Effect to carry —
/// which is why <see cref="BurnDamageModifiers.AppliesTo"/> selects on it and no
/// second ownership field is introduced.
///
/// <b>It is exactly two members, on the precedent of the sibling
/// <see cref="CardCostModifier"/>.</b> Both are <c>Battle</c>-lifetime,
/// Pet-scoped, source-keyed percentage modifiers, so this element carries the
/// same pair — an identity and a percentage — and no duration, Turn counter,
/// <c>ExpiresAt</c>, consumed flag, stack count, or ordering index: no rule reads
/// any of those, and a field kept "for later" is the speculative representation
/// <c>GAME_STATE.md</c> §0 item 5 forbids.
///
/// <b>It is not a <see cref="StatusEffect"/>, deliberately.</b>
/// <c>COMBAT_RULES.md</c> §5.2 item 4 states the boundary: a <c>BurnDamage</c>
/// modifier "must <b>not</b> emit, create, re-enter, or refresh a Burn event or
/// Burn instance, and it must <b>not</b> extend or consume the instance's
/// duration". It therefore carries no <c>RemainingTurns</c>, engages neither
/// duration model §2.3.1 item 3 defines, and adds no second Burn instance.
///
/// <b><see cref="SourceIdentity"/> is source-scoped and stable.</b> It
/// identifies the source that applied the modifier so that a re-application
/// replaces/refreshes <b>that</b> entry and nothing else, exactly as
/// <see cref="CardCostModifier.SourceIdentity"/> and
/// <see cref="ATKModifier.SourceIdentity"/> do for their collections. It is
/// <b>not</b> a per-application unique key, a GUID, a timestamp, an allocation
/// order, or an array position: it must be deterministic and reproducible for a
/// given input state (<c>TDD.md</c> §6), so a replayed or recovered battle
/// re-derives the same identities. For the provisioned source it is the equipped
/// Relic instance identity (<c>RELIC_RULES.md</c> §2.2 item 3), so Burning Curse
/// holds exactly one entry however often its §5 item 2 once-per-root-event
/// safeguard and §8.5 item 5 "non-stacking, once per battle" contract are read.
///
/// This type is deliberately minimal and framework-independent
/// (<c>ARCHITECTURE.md</c> §2.1): it references no ASP.NET Core, SignalR, EF
/// Core, Redis, HTTP, Phaser, or Discord concern.
/// </summary>
/// <param name="SourceIdentity">
/// The stable identity of the source that applied this modifier. It is the
/// collection's replace/refresh and removal key: a re-application by the same
/// source updates that source's own element and nothing else, and a removal
/// deletes exactly that element.
///
/// It must be a <b>non-empty</b> string: a blank identity would let two
/// different sources collide on it and destroy the one property the member
/// exists to provide. <see cref="HasSourceIdentity"/> spells that condition.
/// </param>
/// <param name="BurnDamagePercentage">
/// The Burn-damage modification this source contributes, in percentage points of
/// the Burn tick's damage. Its provisioned value is Burning Curse's <c>30</c>
/// (<c>RELIC_RULES.md</c> §8.5 item 5, transcribed through §8.2 item 2's
/// <c>valueType: Percentage</c>).
///
/// <b>The value is typed but not interpreted here.</b> That it scales a Burn
/// tick's damage, that it reaches only Pet-owned Burn, and that it changes
/// damage only — no event, no second instance, no duration — are
/// <c>COMBAT_RULES.md</c> §5.2 item 4's rules, and the composition of several
/// simultaneously-active modifiers is this collection's own
/// <see cref="BurnDamageModifiers.AppliedPercentage"/>; none of it is restated
/// here. This type fixes only that the value is stored on the element so a
/// refresh can re-apply it, exactly as §2.3.7 item 5 fixes for
/// <c>ATKModifierPercentage</c>.
///
/// <b>It is an <c>int</c>, not a fraction.</b> Every value the contract
/// describes is an integer percentage (Burning Curse's <c>+30%</c>), and §8.2
/// item 2's <c>Percentage</c> is stated as the proportion §8.5 records —
/// <c>30</c> — never pre-resolved to the damage it applies to (§8.2 item 2),
/// which is read from battle state when the tick resolves.
/// </param>
public readonly record struct BurnDamageModifier(
    string SourceIdentity,
    int BurnDamagePercentage)
{
    /// <summary>
    /// Whether this modifier carries a well-formed identity — the condition that
    /// makes the replace/refresh and removal key meaningful.
    ///
    /// A blank or whitespace identity is not a representable source: two
    /// different sources would collide on it and a re-application could not tell
    /// them apart, which is the one property the identity exists to provide. The
    /// read path rejects such an element rather than repairing, defaulting, or
    /// dropping it, on the same loud-rejection standard
    /// <c>RELIC_RULES.md</c> §8.2 item 5 takes for a malformed stored effect.
    /// </summary>
    public bool HasSourceIdentity => !string.IsNullOrWhiteSpace(SourceIdentity);
}
