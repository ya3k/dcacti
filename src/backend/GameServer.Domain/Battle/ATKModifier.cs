namespace GameServer.Domain.Battle;

/// <summary>
/// One applied, Battle-scoped ATK modifier on the active Pet — an element of
/// <c>PetState.ATKModifiers[]</c> (<c>GAME_STATE.md</c> §2.3.7; <c>TASK-136</c>
/// D1/D2).
///
/// <code>
/// ATKModifier
/// ├── SourceIdentity          string, required — the replace/refresh and
/// │                                             removal key
/// └── ATKModifierPercentage   int, required    — signed percentage points
/// </code>
///
/// <b>Exactly two members is the whole schema, and there is no third.</b>
/// <c>GAME_STATE.md</c> §2.3.7 items 4 and 8 forbid a duration, a Turn counter,
/// an <c>ExpiresAt</c>, an <c>ExpiryCondition</c>, a "consumed" flag, a priority, a
/// stack count, an ordering index, a target reference, a remaining-use counter,
/// and a timestamp: no rule reads any of them, and a field kept "for later" is the
/// speculative representation <c>GAME_STATE.md</c> §0 item 5 forbids.
///
/// <b>The modifier is not a <see cref="StatusEffect"/>, deliberately.</b>
/// <c>GAME_STATE.md</c> §2.3.7 item 1 states the boundary: it carries no
/// <c>RemainingTurns</c> and no <c>ExpiryCondition</c>, so it does not engage
/// §2.3.1 item 3's exclusive duration dichotomy (that rule is neither widened nor
/// relaxed by this type) and does not engage item 6's one-instance-per-identity
/// rule, which continues to govern <c>StatusEffects[]</c> alone. It is also not a
/// pending/queued application — §2.3.3's prohibition is honored because a modifier
/// held here has <i>already been applied</i>.
///
/// <b>It is not a second ATK representation.</b> <c>GAME_STATE.md</c> §2.3.7 item 9
/// makes clear that <c>PetState.ATK</c> remains the permanent/base ATK and is never
/// mutated by an element here (<c>TASK-136</c> D5/D8); the composed value the Damage
/// Pipeline consumes is <b>derived</b> at attack resolution by
/// <c>COMBAT_RULES.md</c> §5.6.6's Effective Pet ATK rule and is never stored
/// (§2.3.7 item 9, §0 item 5). This type therefore holds a <i>contribution</i>, not
/// a resolved ATK value, and §2.3.7 item 5 requires that it must not be applied to
/// the entry a second time.
///
/// <b><see cref="SourceIdentity"/> is source-scoped and stable.</b>
/// <c>GAME_STATE.md</c> §2.3.7 item 3 requires it to identify the source that
/// applied the modifier so that a re-application replaces/refreshes <b>that</b>
/// entry and nothing else (§5.1.4). It is <b>not</b> a per-application unique key,
/// a GUID, a timestamp, an allocation order, or an array position: it must be
/// deterministic and reproducible for a given input state (<c>TDD.md</c> §6), so a
/// replayed or recovered battle re-derives the same identities. It is a value, not
/// a definition — no source's rule, magnitude, or lifetime is copied into it
/// (§0 item 5).
///
/// Because the identity is per <b>source</b>, a source re-applying updates its own
/// element rather than appending a second one
/// (<see cref="ATKModifiers.Apply"/>), so two simultaneous elements always mean two
/// distinct sources — which is what makes them independently composable and
/// independently removable (<c>TASK-136</c> D7, §2.3.7 item 4).
///
/// This type is deliberately minimal and framework-independent
/// (<c>ARCHITECTURE.md</c> §2.1): it references no ASP.NET Core, SignalR, EF Core,
/// Redis, HTTP, Phaser, or Discord concern.
/// </summary>
/// <param name="SourceIdentity">
/// The stable identity of the source that applied this modifier
/// (<c>GAME_STATE.md</c> §2.3.7 item 3). It is the collection's replace/refresh and
/// removal key: a re-application by the same source updates that source's own
/// element and nothing else, and a removal deletes exactly that element
/// (§5.1.4 item 4).
///
/// §2.3.5 item 7 fixes the same member on the sibling Card-cost carrier as a
/// <b>non-empty</b> string, and the same reading applies here: §2.3.7 item 3 makes
/// the identity the key a source is matched on, so a blank identity would let two
/// different sources collide on it and destroy the one property the member exists
/// to provide. <see cref="HasSourceIdentity"/> spells that condition.
/// </param>
/// <param name="ATKModifierPercentage">
/// The ATK modification this source contributes, in <b>signed</b> percentage
/// points (<c>GAME_STATE.md</c> §2.3.7 item 5) — positive increases, negative
/// decreases.
///
/// <b>The value is typed but not interpreted here.</b> §2.3.7 item 5 states that
/// what the number means, and how several of them compose into the value the
/// Damage Pipeline reads, is owned by <c>COMBAT_RULES.md</c> §5.6.6's Effective Pet
/// ATK composition rule (<c>TASK-136</c> D5/D11) and is not restated here. This type
/// fixes only that the value is stored on the element so a refresh can re-apply it,
/// exactly as §2.3.1 item 2 fixes for <c>Magnitude</c>.
///
/// <b>It is an <c>int</c>, not a fraction.</b> §2.3.7's schema fixes the member's
/// unit as percentage points, and §2.3.8 item 3 fixes its serialized type as a
/// JSON <c>number</c>; every value the contract describes is an integer percentage
/// (Berserker Core's <c>+5%</c>, <c>TASK-136</c> D5's additive sum, and §5.6.6's
/// single truncation at the end). Storing a fraction here would need converting at
/// every one of those boundaries and would invite the two representations §0 item 5
/// forbids — the same reasoning <c>PetState.Crit</c>'s percentage unit records.
/// </param>
public readonly record struct ATKModifier(
    string SourceIdentity,
    int ATKModifierPercentage)
{
    /// <summary>
    /// Whether this modifier carries a well-formed identity — the condition
    /// <c>GAME_STATE.md</c> §2.3.7 item 3 makes the replace/refresh and removal key
    /// meaningful.
    ///
    /// A blank or whitespace identity is not a representable source: two different
    /// sources would collide on it and a re-application could not tell them apart,
    /// which is the one property the identity exists to provide.
    /// </summary>
    public bool HasSourceIdentity => !string.IsNullOrWhiteSpace(SourceIdentity);
}
