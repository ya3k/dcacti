using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;

namespace GameServer.Api.Controllers;

/// <summary>
/// Projects the created authoritative <c>BattleState</c> onto the
/// <c>POST /api/battle/start</c> response's <c>initialState</c> summary
/// (<c>API_CONTRACTS.md</c> §3, <c>GAME_STATE.md</c> §2).
///
/// <b>It is a pure field mapping.</b> Every member is read one-to-one from the
/// state the server created — the counters, the seed and RNG state, the board,
/// the Match/Combo accounting, the Pet (including both loadout snapshots), and
/// the Boss. Nothing is computed, defaulted, recomputed, sorted, or adjusted,
/// and no value is read from the database: the snapshots carried by the state
/// are the fixed ones battle creation produced
/// (<c>RELIC_RULES.md</c> §2.5, <c>CARD_RULES.md</c> §1, ADR-012 items 8 and 10).
///
/// <b>It reads the state, it does not own it.</b> <see cref="BattleStateService"/>
/// is the authoritative owner (<c>GAME_STATE.md</c> §5.1); this projection is the
/// endpoint's read of it for the one response §3 defines. It keeps no copy and
/// changes nothing — the same read-after-create boundary
/// <c>SIGNALR_PROTOCOL.md</c> §4.1 uses to deliver state on group join.
/// </summary>
internal static class BattleStartStateSummary
{
    /// <summary>
    /// Reads the created battle's authoritative state and projects it onto the
    /// §3 <c>initialState</c> summary.
    /// </summary>
    /// <param name="battleId">The created battle's identity.</param>
    /// <param name="battles">The authoritative battle-state boundary.</param>
    /// <exception cref="InvalidOperationException">
    /// The just-created battle does not resolve. <c>API_CONTRACTS.md</c> §3
    /// defines a success response for a created battle, and a created battle's
    /// state is the value §2 requires it to carry, so a missing state is a
    /// server defect and is never reported as a partial or empty summary.
    /// </exception>
    internal static BattleStartInitialState For(
        string battleId,
        BattleStateService battles)
    {
        var state = battles.GetBattle(battleId)
            ?? throw new InvalidOperationException(
                "The battle created by POST /api/battle/start did not resolve in the authoritative store.");

        return new BattleStartInitialState(
            BattleId: state.BattleId,
            Turn: state.Turn,
            Sequence: state.Sequence,
            RngSeed: state.RngSeed,
            // §2.6.2 item 1: the two components are one logical field and are
            // never split across separate state fields. They stay together here.
            RngState: new BattleStartRngState(state.RngState.State, state.RngState.Increment),
            // §2.1.1 / §2.1.7 item 2: the 64 entries in ascending index order;
            // the array position is the cell index, so no index is written per
            // element.
            Board: new BattleStartBoard(state.BoardState.Cells.Select(ToCell).ToArray()),
            // §2.2: both Match/Combo values are root members and both are always
            // present — a zero is delivered as a zero, never omitted
            // (MATCH3_RULES.md §6.5 item 4).
            Combo: state.Combo,
            MatchCount: state.MatchCount,
            PetState: ToPetState(state.PetState),
            BossState: ToBossState(state.BossState));
    }

    /// <summary>
    /// Projects <c>GameState.md</c> §2.3's <c>PetState</c> — including both
    /// battle-scoped loadout snapshots the battle was created with.
    ///
    /// The loaded arrays are carried across verbatim, element for element and in
    /// order: the Card snapshot's order is its reading order (no rule reads Card
    /// positions) and the Relic snapshot's order <b>is</b> the equip-slot order
    /// and must never be re-sorted (<c>RELIC_RULES.md</c> §2.3 item 2). Neither
    /// array is null on a created battle, because this endpoint always supplies
    /// both validated snapshots (<c>GAME_STATE.md</c> §2.3).
    /// </summary>
    private static BattleStartPetState ToPetState(PetState petState) =>
        new(
            HP: petState.HP,
            MaxHP: petState.MaxHP,
            ATK: petState.ATK,
            DEF: petState.DEF,
            Crit: petState.Crit,
            Power: petState.Power,
            // PET_RULES.md §1 / ELEMENT_RULES.md §6: the Pet's one Element, as
            // the documented contract name.
            Element: petState.Element.ToString(),
            PassiveId: petState.PassiveId.Value,
            PassiveThreshold: petState.PassiveProgress.Threshold,
            PassiveCurrent: petState.PassiveProgress.Current,
            EquippedCards: (petState.EquippedCards ?? [])
                .Select(card => card.Value)
                .ToArray(),
            EquippedRelics: (petState.EquippedRelics ?? [])
                .Select(relic => relic.Value)
                .ToArray());

    /// <summary>
    /// Projects <c>GAME_STATE.md</c> §2.4's <c>BossState</c> at its creation
    /// values — full health, Initial State, and no Behavior resolved. The Boss's
    /// Passive/Skill charging members are not part of this summary: they are
    /// resolution state, and this endpoint resolves no action
    /// (<c>GAME_STATE.md</c> §2.0.2, §5.1).
    /// </summary>
    private static BattleStartBossState ToBossState(BossState bossState) =>
        new(
            BossId: bossState.BossId.Value,
            Element: bossState.Element.ToString(),
            HP: bossState.HP,
            MaxHP: bossState.MaxHP,
            ATK: bossState.ATK,
            DEF: bossState.DEF,
            State: bossState.State.ToString());

    /// <summary>
    /// Projects one cell entry — the Gem type as its documented contract name
    /// and, when present, the Special Gem at that cell
    /// (<c>GAME_STATE.md</c> §2.1.7 items 1–4, <c>MATCH3_RULES.md</c> §1.1).
    /// </summary>
    private static BattleStartCell ToCell(Cell cell) =>
        new(
            GemType: GemTypes.ToContractName(cell.GemType),
            SpecialGem: cell.SpecialGem is not { } gem
                ? null
                : new BattleStartSpecialGem(gem.Type.ToString(), gem.Orientation?.ToString()));
}
