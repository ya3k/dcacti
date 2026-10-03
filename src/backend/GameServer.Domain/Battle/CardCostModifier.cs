namespace GameServer.Domain.Battle;

/// <summary>
/// One applied, Battle-scoped Card-cost modifier on the active Pet — an element of
/// <c>PetState.CardCostModifiers[]</c> (<c>GAME_STATE.md</c> §2.3.5;
/// <c>TASK-134</c> D1/D2).
///
/// <code>
/// CardCostModifier
/// ├── SourceIdentity           string, required — the replace/refresh and
/// │                                              removal key
/// └── CostReductionPercentage  int, required    — percentage points
/// </code>
///
/// <b>Exactly two members is the whole schema, and there is no third.</b>
/// <c>GAME_STATE.md</c> §2.3.5 item 5 forbids a <c>Duration</c>, a
/// <c>RemainingTurns</c>, an <c>ExpiresAt</c>, an <c>ExpiryCondition</c>, a
/// <c>StackCount</c>, a Turn counter, an expiry label, a "consumed" flag, a
/// priority, an ordering index, a target reference, a remaining-use counter, and a
/// timestamp: no rule reads any of them, and a field kept "for later" is the
/// speculative representation <c>GAME_STATE.md</c> §0 item 5 forbids. In particular
/// there is <b>no stack count</b> — §2.3.5 item 5 states that a repeated application
/// from one source is a replace/refresh (§5.1.3 item 1), never an increment of a
/// counter.
///
/// <b>The modifier is not a <see cref="StatusEffect"/>, deliberately.</b>
/// <c>GAME_STATE.md</c> §2.3.5 item 1 states the boundary: it carries no
/// <c>RemainingTurns</c> and no <c>ExpiryCondition</c>, so it does not engage
/// §2.3.1 item 3's exclusive duration dichotomy (that rule is neither widened nor
/// relaxed by this type) and does not engage item 6's one-instance-per-identity
/// rule, which continues to govern <c>StatusEffects[]</c> alone. It is also not a
/// pending/queued application — §2.3.3's prohibition is honored because a modifier
/// held here has <i>already been applied</i>; it is awaiting a Card cast that reads
/// it, not a write-back.
///
/// <b>Target is <c>Pet</c>, which is why the collection lives on
/// <c>PetState</c>.</b> <c>GAME_STATE.md</c> §2.3.5 item 2 and <c>RELIC_RULES.md</c>
/// §8.3 fix the <c>CardCost</c> effect's <c>target</c> as <c>Pet</c>, so the
/// modifier modifies the <b>active Pet's own Card costs</b>. The Boss carries no
/// such collection (§2.3.5 item 2, contrast §2.3.4 item 11), and Cards are cast by
/// the active Pet (<c>CARD_RULES.md</c> §3).
///
/// <b>It is not a second Card-cost representation.</b> <c>CARD_RULES.md</c> §3.6
/// item 2 makes <c>EffectiveCardCost</c> a runtime value derived for the cast being
/// resolved — explicitly "not stored state", not a <c>PetState</c> member, and not a
/// Redis field. This type therefore holds a <i>contribution</i>, and §2.3.5 item 4
/// leaves the composition arithmetic, its cap, and the <c>EffectiveCardCost</c>
/// term to <c>CARD_RULES.md</c> §3.6 rather than restating them here. Modifying
/// authored <c>CardDefinition.PowerCost</c> is likewise not this type's concern
/// (§2.3.5 preamble).
///
/// <b><see cref="SourceIdentity"/> is source-scoped and stable.</b>
/// <c>GAME_STATE.md</c> §2.3.5 item 3 requires it to identify the source that
/// applied the modifier so that a re-application replaces/refreshes <b>that</b>
/// entry and nothing else (§5.1.3). It is <b>not</b> a per-application unique key,
/// a GUID, a timestamp, an allocation order, or an array position: it must be
/// deterministic and reproducible for a given input state (<c>TDD.md</c> §6), so a
/// replayed or recovered battle re-derives the same identities. It is a value, not
/// a definition — no source's rule, magnitude, or lifetime is copied into it
/// (§0 item 5). Which token a given source uses is owned by that source's rule
/// document; for the provisioned <c>CardCost</c> source it is the Relic's identity
/// (<c>RELIC_RULES.md</c> §2.2 item 3), so Emergency Core holds exactly one entry
/// however often its §6 note 2 condition re-evaluates.
///
/// This type is deliberately minimal and framework-independent
/// (<c>ARCHITECTURE.md</c> §2.1): it references no ASP.NET Core, SignalR, EF Core,
/// Redis, HTTP, Phaser, or Discord concern.
/// </summary>
/// <param name="SourceIdentity">
/// The stable identity of the source that applied this modifier
/// (<c>GAME_STATE.md</c> §2.3.5 item 3). It is the collection's replace/refresh and
/// removal key: a re-application by the same source updates that source's own
/// element and nothing else, and a removal deletes exactly that element
/// (§5.1.3 item 4).
///
/// §2.3.5 item 7 fixes this member as a <b>non-empty</b> string — "Both members are
/// required and neither is nullable" — so a blank identity is not a representable
/// source. <see cref="HasSourceIdentity"/> spells that condition.
/// </param>
/// <param name="CostReductionPercentage">
/// The Card-cost reduction this source contributes, in percentage points of the
/// Card's cost (<c>GAME_STATE.md</c> §2.3.5 item 4). Its provisioned value is
/// Emergency Core's <c>-50%</c> (<c>RELIC_RULES.md</c> §6, §8.5).
///
/// <b>The value is typed but not interpreted here.</b> §2.3.5 item 4 states that
/// how several of them compose, the cap that composition is subject to, and the
/// arithmetic that reaches an actual Card cost are gameplay rules owned by
/// <c>CARD_RULES.md</c> §3.6 and are not restated here. This type fixes only that
/// the value is stored on the element.
///
/// <b>It is an <c>int</c>, not a fraction.</b> §2.3.5's schema fixes the member's
/// unit as percentage points and §2.3.6 item 3 fixes its serialized type as a JSON
/// <c>number</c>. Storing a fraction would need converting at every boundary and
/// would invite the two representations §0 item 5 forbids — the same reasoning
/// <c>PetState.Crit</c>'s percentage unit records.
/// </param>
public readonly record struct CardCostModifier(
    string SourceIdentity,
    int CostReductionPercentage)
{
    /// <summary>
    /// Whether this modifier carries a well-formed identity — the condition
    /// <c>GAME_STATE.md</c> §2.3.5 item 3 makes the replace/refresh and removal key
    /// meaningful.
    ///
    /// A blank or whitespace identity is not a representable source: two different
    /// sources would collide on it and a re-application could not tell them apart,
    /// which is the one property the identity exists to provide. §2.3.5 item 7
    /// requires the member to be a non-empty string and states that a malformed
    /// element is not silently repaired, defaulted, or dropped — the same
    /// loud-rejection position <c>RELIC_RULES.md</c> §8.2 item 5 takes for a
    /// malformed stored effect — so this condition is what the apply and read paths
    /// reject on rather than a value they normalize.
    /// </summary>
    public bool HasSourceIdentity => !string.IsNullOrWhiteSpace(SourceIdentity);
}
