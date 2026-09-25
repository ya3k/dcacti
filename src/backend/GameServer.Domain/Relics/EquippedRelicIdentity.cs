namespace GameServer.Domain.Relics;

/// <summary>
/// The identity of one owned Relic instance — the `RelicInstanceId` a
/// battle loadout selects and the value `PetState.EquippedRelics[]` carries
/// (<c>GAME_STATE.md</c> §2.3, <c>RELIC_RULES.md</c> §2.2).
///
/// <b>It is an identity, not a definition.</b> The Relic's
/// <c>Trigger</c>, <c>Condition</c>, and <c>EffectDefinition</c> are its
/// definition and live on <see cref="RelicDefinition"/>
/// (<c>DATABASE.md</c> §1); none of them is carried here, and no second copy
/// of the definition is introduced by this wrapper
/// (<c>GAME_STATE.md</c> §2.3 items 1–4, §0 item 5). This mirrors
/// <see cref="Passives.PassiveId"/> and
/// <see cref="Bosses.BossId"/>, which <c>GAME_STATE.md</c> §2.3 names as the
/// sibling identity fields of the same shape.
///
/// <b>It is the instance identity, never the definition identity.</b> A
/// Player owns Relic <b>instances</b> (<c>DATABASE.md</c> §2:
/// <c>Player 1─N Relic</c>; <c>RELIC_RULES.md</c> §2 item 1), and loadout
/// validation checks ownership of the selected <b>instances</b>
/// (<c>API_CONTRACTS.md</c> §3). Two owned instances of the same
/// <see cref="RelicDefinition"/> are therefore two distinct identities and
/// may occupy two different slots (<c>RELIC_RULES.md</c> §2.4 item 3).
/// Collapsing this to <c>RelicDefinitionId</c> would make that impossible and
/// would lose which owned copy is equipped.
///
/// <b>Why a value type and not a bare `string`.</b> The value travels into
/// authoritative battle state and is the identity
/// <c>GAME_EVENTS.md</c> §2's <c>RelicTriggered</c> reports as its
/// <c>RelicId</c>, so it is named so a caller cannot pass a definition id, a
/// Boss id, or any other string where a Relic instance identity belongs. This
/// follows the existing <see cref="Passives.PassiveId"/> and
/// <see cref="Bosses.BossId"/> pattern, which are the same shape for the same
/// reason.
///
/// There is no id format, scheme, or validation rule in any document — the
/// contract defines the field's meaning, not its spelling — so this type
/// imposes none and holds the identifier verbatim.
/// </summary>
/// <param name="Value">
/// The owned instance's identifier, exactly as
/// <see cref="Relic.RelicInstanceId"/> holds it (<c>DATABASE.md</c> §1) and
/// exactly as the battle loadout selected it. It is reported and never
/// re-derived, re-numbered, or invented by a reader.
/// </param>
public readonly record struct EquippedRelicIdentity(string Value)
{
    /// <summary>
    /// The documented identity, for diagnostics and for a caller that has one.
    /// </summary>
    public override string ToString() => Value;
}
