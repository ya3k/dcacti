namespace GameServer.Domain.Bosses;

/// <summary>
/// The identity of a Boss (<c>GAME_STATE.md</c> §2.4 <c>BossId</c>;
/// <c>BOSS_RULES.md</c> §6).
///
/// <b>A battle has exactly one Boss</b> (<c>GAME_RULES.md</c> §1.1,
/// <c>BOSS_RULES.md</c> §1), so this value does not select among several: it
/// names which Boss the battle is fought against. <c>GAME_STATE.md</c> §2.4 owns
/// that field, and it is the identity member the sibling identity fields use
/// (<c>PetState.PassiveId</c> §2.3, <c>PetState.EquippedRelics[]</c>,
/// <c>EquippedCards[]</c>).
///
/// <b>It is an identity, not a definition.</b> The Boss's Element, HP/MaxHP,
/// ATK, DEF, State, Passive, and Skill are its <b>definition</b>
/// (<c>BOSS_RULES.md</c> §1, §6; <c>GAME_STATE.md</c> §2.4) and live in the Boss
/// configuration this identity resolves against
/// (<see cref="BossDefinition"/>) — none of them is stored here, and no second
/// copy of the definition is introduced by this wrapper. The same split
/// <c>GAME_STATE.md</c> §2.3 item 1 states for <c>PassiveId</c> applies.
///
/// <b>Why a value type and not a bare <c>string</c>.</b> The field is an
/// identity that travels into an event payload — <c>GAME_EVENTS.md</c> §2's
/// <c>BattleStarted</c> reports <c>BossId</c> — and into the documented
/// <c>POST /api/battle/start</c> request (<c>API_CONTRACTS.md</c> §3), so it is
/// named so a caller cannot pass a Pet id, a Skill id, or any other string where
/// a Boss identity belongs. This follows the existing
/// <c>PassiveId</c> pattern (<c>GameServer.Domain.Passives</c>), which is the
/// same shape for the same reason.
///
/// There is no id format, scheme, or validation rule in any document —
/// <c>GAME_STATE.md</c> §2.4 defines the field's meaning, not its spelling, and
/// <c>API_CONTRACTS.md</c> §3 requires only that <c>bossId</c> "must be a valid
/// MVP Boss" (<c>BOSS_RULES.md</c> §6) — so this type imposes no format and
/// holds the identifier verbatim.
/// </summary>
/// <param name="Value">
/// The Boss's identifier, exactly as the Boss configuration
/// (<see cref="BossDefinition"/>) and <c>GAME_STATE.md</c> §2.4's
/// <c>BossState.BossId</c> hold it. It is resolved against the MVP Boss
/// reference of <c>BOSS_RULES.md</c> §6 and is never re-derived, re-numbered, or
/// invented by a reader.
/// </param>
public readonly record struct BossId(string Value)
{
    /// <summary>
    /// The documented identity, for diagnostics and for a caller that has one.
    /// </summary>
    public override string ToString() => Value;
}
