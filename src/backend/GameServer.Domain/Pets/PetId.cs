namespace GameServer.Domain.Pets;

/// <summary>
/// The identity of one owned Pet instance — the value
/// <c>BattleState.PetState.PetId</c> carries (<c>GAME_STATE.md</c> §2.3;
/// <c>ADR-014</c> decision 4).
///
/// <b>It is the instance, not the definition.</b> It is the same value as
/// <see cref="Pet.PetInstanceId"/> (<c>DATABASE.md</c> §1), the <c>petId</c>
/// submitted to <c>POST /api/battle/start</c> (<c>API_CONTRACTS.md</c> §3),
/// and <c>BattleResult.PetInstanceId</c> at battle end (<c>DATABASE.md</c>
/// §1). It is <b>not</b> <see cref="Pet.PetDefinitionId"/> and not the display
/// <c>Identity</c> name (<c>PET_RULES.md</c> §2,
/// <see cref="PetDefinition.Identity"/>) — those are persistent
/// definition-side values, not battle state (§2.3). A second
/// <c>PetInstanceId</c> member inside the same record would duplicate a value
/// the record already owns (<c>GAME_STATE.md</c> §0 item 5), which is why
/// <c>ADR-014</c> decision 4 records that no separate member is required.
///
/// <b>It is set once at battle creation and never changes</b>
/// (<c>GAME_STATE.md</c> §2.3 item 2): selecting a Pet locks it in for the
/// duration of the battle (<c>PET_RULES.md</c> §2 item 3), so no resolution
/// writes it and no event changes it.
///
/// <b>It is not a wire member of this stage.</b> <c>SIGNALR_PROTOCOL.md</c>
/// §4.3 item 2 fixes the <c>petState</c> wire payload to the Passive trio
/// only, so carrying this value in the state adds no member to
/// <c>BattleStateUpdated</c> and no message, method, or subscription
/// (<c>SIGNALR_PROTOCOL.md</c> §4 item 4).
///
/// <b>Why a value type and not a bare <c>string</c>.</b> The value travels
/// into authoritative battle state and is the identity the battle-end
/// persistence path writes, so it is named so a caller cannot pass a
/// definition id, a Boss id, or any other string where an owned Pet instance
/// identity belongs. This follows the existing
/// <see cref="GameServer.Domain.Relics.EquippedRelicIdentity"/> and
/// <see cref="GameServer.Domain.Bosses.BossId"/> pattern, which are the same
/// shape for the same reason.
///
/// There is no id format, scheme, or validation rule in any document — the
/// contract defines the field's meaning, not its spelling — so this type
/// imposes none and holds the identifier verbatim.
/// </summary>
/// <param name="Value">
/// The owned instance's identifier, exactly as
/// <see cref="Pet.PetInstanceId"/> holds it (<c>DATABASE.md</c> §1) and
/// exactly as the battle-start request selected it. It is reported and never
/// re-derived, re-numbered, or invented by a reader.
/// </param>
public readonly record struct PetId(string Value)
{
    /// <summary>
    /// The documented identity, for diagnostics and for a caller that has one.
    /// </summary>
    public override string ToString() => Value;
}
