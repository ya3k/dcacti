namespace GameServer.Domain.Cards;

/// <summary>
/// Which side is performing a Card cast — the acting side the shared cast
/// contract resolves against (<c>CARD_RULES.md</c> §3 item 9, <c>BOSS_RULES.md</c>
/// §4.2 items 2–4, <c>ADR-029</c> §2.3 item 9).
///
/// <code>
/// Player   the active Pet's Card / Pet Skill cast — the existing cast contract
/// Boss     a Boss Card cast, resolved through the same contract against BossState
/// </code>
///
/// <b>It names the actor; it decides no rule.</b> <c>CARD_RULES.md</c> §3 item 9
/// is the canonical owner of the actor-relative split: the <i>shared</i> cast
/// contract is the validation order, the <c>EffectiveCardCost</c> composition
/// (§3.6) and the effect-resolution semantics; the <i>Player-only</i> rules are
/// §3 item 6's one-cast-per-committed-Turn allowance, §3 item 4's Relic trigger
/// step, §3.6's Relic <c>CardCost</c> modifiers, and §1's loadout/ownership
/// model. This type is the explicit actor identity
/// <see cref="CardCastExecutor"/> branches on so that split is stated in one
/// place instead of being implied by which state object a code path happens to
/// read.
///
/// <b>It is not a multi-actor framework.</b> There are exactly two sides in MVP
/// — one active Pet and one Boss (<c>GAME_RULES.md</c> §1.1,
/// <c>GAME_STATE.md</c> §2.3/§2.4) — and the two actors' states remain separate
/// and unmerged (<c>ADR-029</c> §2.3 item 8). This type therefore carries an
/// identity and nothing else: no policy, no dispatch table, no per-actor
/// service, and no second executor (<c>AGENTS.md</c> §9,
/// <c>ADR-029</c> §5).
///
/// <b>Numeric values are identifiers.</b> No rule derives from them and nothing
/// persists this value (<c>GAME_STATE.md</c> §0 item 5). <see cref="Player"/> is
/// <c>0</c> so <c>default(CardCastActor)</c> is the existing, backward-compatible
/// cast actor — a caller that names no actor gets the Player contract unchanged,
/// which is the same actor the pre-existing
/// <see cref="CardCastExecutor.Execute(Battle.BattleState, CardDefinition)"/>
/// entry point has always served.
/// </summary>
public enum CardCastActor
{
    /// <summary>
    /// The Player's side — the active Pet's Card or Pet Skill cast
    /// (<c>CARD_RULES.md</c> §3 items 1–8, §4). Validated against
    /// <c>PetState.EquippedCards[]</c> (the battle loadout), composed with
    /// <c>PetState.CardCostModifiers[]</c>, gated on <c>PetState.Power</c> and on
    /// the committed Turn's one-cast allowance, deducted from
    /// <c>PetState.Power</c>, and reported by the <c>CardCast</c> /
    /// <c>PetSkillCast</c> / <c>PowerChanged</c> events it emits.
    ///
    /// A Player cast is never a <see cref="CardCategory.BossCard"/>: the Boss Card
    /// is not part of the loadout and never appears in
    /// <c>PetState.EquippedCards[]</c> (<c>CARD_RULES.md</c> §1, §1.1 items 4–5),
    /// so a Player-actor cast of such a definition is rejected
    /// <see cref="CardCastRejectionReason.InvalidCard"/>.
    /// </summary>
    Player = 0,

    /// <summary>
    /// The Boss's side — a Boss Card cast (<c>BOSS_RULES.md</c> §4.2,
    /// <c>CARD_RULES.md</c> §1.1, §3 item 9). Validated as the Boss's own
    /// <see cref="CardCategory.BossCard"/> definition (there is no Boss loadout
    /// and no ownership row, §4.2 items 1 and 8), composed with an <b>empty</b>
    /// modifier set because Relics are Player-only (<c>ADR-029</c> §2.4 item 11),
    /// gated on <c>BossState.Power</c>, deducted from <c>BossState.Power</c>, and
    /// resolved with the casting actor's own state as <i>self</i> and the active
    /// Pet as <i>opponent</i> (§4.2 item 4).
    ///
    /// It neither reads nor spends §3 item 6's one-cast allowance, never triggers
    /// a Relic, never emits a Player <c>CardCast</c>, and does not advance
    /// <c>Sequence</c>: a Boss cast resolves inside the round's single write-back
    /// (<c>GAME_STATE.md</c> §5.1 item 9, <c>ADR-029</c>).
    /// </summary>
    Boss = 1,
}
