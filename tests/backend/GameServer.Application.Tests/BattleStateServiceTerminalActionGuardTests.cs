using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Domain.Battle;
using GameServer.Domain.Battle.Serialization;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// The terminal-battle action guard — <c>MATCH3_RULES.md</c> §2.1.4 item 7 (the
/// state precondition a terminal battle cannot satisfy),
/// <c>GAME_RULES.md</c> §1.4, <c>SIGNALR_PROTOCOL.md</c> §5 item 3.
///
/// <code>
/// Rule (MATCH3_RULES.md §2.1.4 items 2 and 7)
///  ↓
/// Scenario (Given an authoritative record whose battle has ended, When a Swap
///           action arrives, Then the documented rejection and no gameplay)
///  ↓
/// Test
/// </code>
///
/// <b>The reachable service-level path is the one under test.</b> The documented
/// battle end deletes the active record after the durable result is written
/// (<c>REDIS_STATE.md</c> §3), so in the running server with its result boundary
/// composed a terminal state normally does not survive to be acted on. It
/// survives whenever that boundary is absent or its write fails
/// (<c>DATABASE.md</c> §1 sourcing item 3), and the service is what must then
/// refuse the action. These tests compose the service <b>without</b> the durable
/// result boundary — the documented case in which termination is observed but the
/// record is not cleared — and assert the guard directly on the record the store
/// still holds.
///
/// <b>A missing battle and an ended battle are not the same response.</b> The
/// missing-battle outcome is the service's <c>null</c>, which the caller reports
/// as <c>BATTLE_NOT_FOUND</c>; an ended battle that is still stored produces a
/// non-null rejection with the state-precondition code. Both are asserted here, and
/// neither is described as the other.
/// </summary>
public class BattleStateServiceTerminalActionGuardTests
{
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    private static readonly PlayerId Owner = new("player_terminal_guard_owner");

    /// <summary>
    /// A Boss a single ATK-gated Player instance destroys, so a Round can be driven
    /// to the Boss terminal state deterministically (see
    /// <see cref="BattleStateServiceDeadlockRecoveryTests"/> for the same fixture).
    /// </summary>
    private static BossDefinition FragileBoss(int maxHp) => BossDefinitions.HoaLong with { MaxHP = maxHp };

    /// <summary>
    /// The seed whose pinned Player fixture is an ATK-clearing Swap on this stream —
    /// the qualifying attack that ends the battle.
    /// </summary>
    private const ulong KillingBlowSeed = 2UL;

    // =======================================================================
    // 1. Rejection for both terminal sides
    // =======================================================================

    [Fact]
    public async Task NewSwap_AgainstABossTerminalBattle_ShouldBeRejected()
    {
        // GAME_RULES.md §1.4: the battle ends when the Boss reaches 0 HP. §2.1.4
        // item 7: no further Swap is admitted, and the action is rejected with the
        // established state-precondition code rather than resolved.
        var arranged = await ArrangeTerminalAsync("terminal-guard-boss", bossHp: 0);

        var result = await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pair);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsRejected);
        Assert.Equal(SwapRejectionReason.StaleAction, result.Value.Reason);
        Assert.False(result.Value.IsIdempotentReplay);
    }

    [Fact]
    public async Task NewSwap_AgainstAPlayerTerminalBattle_ShouldBeRejected()
    {
        // GAME_RULES.md §1.4's other side: the active Pet's HP has reached 0. The
        // guard is the same condition read from the same state, so the rejection is
        // identical.
        var arranged = await ArrangeTerminalAsync("terminal-guard-player", petHp: 0);

        var result = await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pair);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsRejected);
        Assert.Equal(SwapRejectionReason.StaleAction, result.Value.Reason);
    }

    [Fact]
    public async Task TerminalRejection_ShouldNotDependOnTheStateVersionPrecondition()
    {
        // §2.1.4 item 7: the guard is the state precondition's own reading of an ended
        // battle, so it does not need a violated version to fire. A request that states
        // the CURRENT Sequence — a precondition that is satisfied — is still refused,
        // which is what distinguishes the guard from the version check of item 1.
        var arranged = await ArrangeTerminalAsync("terminal-guard-current-sequence", bossHp: 0);

        var request = new SwapRequest(
            arranged.Pair.From,
            arranged.Pair.To,
            ExpectedSequence: arranged.Before.Sequence);

        var result = await arranged.Service.ExecuteSwapAsync(arranged.BattleId, request);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsRejected);
        Assert.Equal(SwapRejectionReason.StaleAction, result.Value.Reason);
    }

    // =======================================================================
    // 2. Nothing happens on a terminal rejection
    // =======================================================================

    [Fact]
    public async Task TerminalRejection_ShouldRunNoResolutionConsumeNoRngAndWriteNothing()
    {
        // §2.1.4 item 7 with §2.1.5: the guard runs before any resolution, so no Swap
        // is exchanged, no cascade runs, no RNG is drawn, no Boss response is produced,
        // no Turn or Sequence advances, no Battle Event is emitted, and nothing is
        // written.
        var arranged = await ArrangeTerminalAsync("terminal-guard-no-gameplay", bossHp: 0);

        var writesBefore = arranged.Store.WriteCount;

        var result = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pair))!.Value;

        Assert.True(result.IsRejected);

        // A rejection carries no state, no events, and no resources
        // (MATCH3_RULES.md §2.1.5 items 5–6).
        Assert.Empty(result.Events);

        // No write at all: the record is the one the scenario arranged.
        Assert.Equal(writesBefore, arranged.Store.WriteCount);

        var stored = (await arranged.Service.GetBattleAsync(arranged.BattleId))!;

        Assert.Equal(arranged.Before.BoardState.Cells, stored.BoardState.Cells);
        Assert.Equal(arranged.Before.RngState, stored.RngState);
        Assert.Equal(arranged.Before.Turn, stored.Turn);
        Assert.Equal(arranged.Before.Sequence, stored.Sequence);
        Assert.Equal(arranged.Before.BossState.HP, stored.BossState.HP);
        Assert.Equal(arranged.Before.LastCommittedSwapPair, stored.LastCommittedSwapPair);
    }

    [Fact]
    public async Task TerminalRejection_ShouldProduceNoBossResponseAndNoNewRound()
    {
        // §2.1.4 item 7: an ended battle must not produce another Boss response, a new
        // Round, or a further gameplay event. Running the guard's rejection against a
        // state a Boss response phase would otherwise extend shows both.
        var arranged = await ArrangeTerminalAsync("terminal-guard-no-boss-response", bossHp: 0);

        var before = arranged.Before;
        var result = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pair))!.Value;

        Assert.True(result.IsRejected);
        Assert.Empty(result.Events);
        Assert.DoesNotContain(result.Events, e => e.Type is BattleEventType.SwapStarted or BattleEventType.SwapResolved);

        var stored = (await arranged.Service.GetBattleAsync(arranged.BattleId))!;

        Assert.Equal(before.BossState.SkillCharge, stored.BossState.SkillCharge);
        Assert.Equal(before.BossState.SkillCooldown, stored.BossState.SkillCooldown);
        Assert.Equal(before.PetState.Power, stored.PetState.Power);
        Assert.Equal(before.MatchCount, stored.MatchCount);
        Assert.Equal(before.Combo, stored.Combo);
    }

    // =======================================================================
    // 3. Idempotency and compatibility
    // =======================================================================

    [Fact]
    public async Task IdempotentReplay_OfTheTerminalAction_ShouldStillBeAnsweredAsAReplay()
    {
        // §2.1.4 items 2 and 7: the guard is evaluated AFTER the action-identity
        // recognition, so a retry of the very action that produced the terminal state —
        // the ordinary lost-acknowledgement case — is still answered as an idempotent
        // replay and is not mistaken for a new action.
        //
        // The battle is ended by the Player's own qualifying attack, and the durable
        // result boundary is absent, so the terminal record survives with the committed
        // pair recorded — which is exactly what the replay recognition reads.
        var store = new InMemoryBattleStateRepository();
        var service = new BattleStateService(store, new FixedRngSeedSource());
        var battleId = "terminal-guard-idempotent";

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, FragileBoss(maxHp: 1));

        var pinned = RoundTestSupport.PlayerMatch(
            GemType.Atk,
            Pcg32.FromSeed(KillingBlowSeed).CurrentState);

        store.ApplyConcurrentWrite(created with
        {
            BoardState = pinned.Board,
            RngState = Pcg32.FromSeed(KillingBlowSeed).CurrentState,
        });

        var before = (await store.GetAsync(battleId))!;
        var request = new SwapRequest(
            pinned.Swap.From,
            pinned.Swap.To,
            ExpectedSequence: before.Sequence,
            ActionId: "client-act-killing-blow");

        var committed = (await service.ExecuteSwapAsync(battleId, request))!.Value;

        Assert.True(committed.IsAccepted);
        Assert.Contains(committed.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.Equal(0, committed.State.BossState.HP);

        var writesAfterCommit = store.WriteCount;

        // The record survived, so the next action meets the guard rather than absence.
        Assert.NotNull(await service.GetBattleAsync(battleId));

        var replay = (await service.ExecuteSwapAsync(battleId, request))!.Value;

        Assert.True(replay.IsIdempotentReplay);
        Assert.True(replay.IsAccepted);
        Assert.Equal(committed.State, replay.State);
        Assert.Empty(replay.Events);
        Assert.Equal(writesAfterCommit, store.WriteCount);
    }

    [Fact]
    public async Task NewAction_AfterATerminalCommit_ShouldBeRejectedRatherThanReplayed()
    {
        // The other half of the same contract: an action that is NOT the committed one
        // has no identity to be recognised by, so the guard refuses it. Same account,
        // same terminal state, different logical action.
        var store = new InMemoryBattleStateRepository();
        var service = new BattleStateService(store, new FixedRngSeedSource());
        var battleId = "terminal-guard-new-action";

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, FragileBoss(maxHp: 1));

        var pinned = RoundTestSupport.PlayerMatch(
            GemType.Atk,
            Pcg32.FromSeed(KillingBlowSeed).CurrentState);

        store.ApplyConcurrentWrite(created with
        {
            BoardState = pinned.Board,
            RngState = Pcg32.FromSeed(KillingBlowSeed).CurrentState,
        });

        var before = (await store.GetAsync(battleId))!;

        var committed = (await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(pinned.Swap.From, pinned.Swap.To, before.Sequence, "client-act-first")))!.Value;

        Assert.True(committed.IsAccepted);
        Assert.Equal(0, committed.State.BossState.HP);

        var writesAfterCommit = store.WriteCount;

        // A different payload under a different identity, against the ended battle.
        var (from, to) = FindAnotherAdjacentPair(pinned.Swap);
        var rejected = (await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(from, to, committed.State.Sequence, "client-act-second")))!.Value;

        Assert.True(rejected.IsRejected);
        Assert.Equal(SwapRejectionReason.StaleAction, rejected.Reason);
        Assert.False(rejected.IsIdempotentReplay);
        Assert.Equal(writesAfterCommit, store.WriteCount);
    }

    [Fact]
    public async Task MissingBattle_ShouldRemainDistinctFromATerminalRejection()
    {
        // The two outcomes are different responses on different paths, and this asserts
        // both rather than equating them:
        //   * a battle with no record at all  → the service returns null, which the
        //     caller reports as BATTLE_NOT_FOUND (SIGNALR_PROTOCOL.md §5, §7 item 3);
        //   * a battle whose record still holds a terminal state → a non-null rejection
        //     whose reason is the state-precondition code.
        var arranged = await ArrangeTerminalAsync("terminal-guard-distinct", bossHp: 0);

        var missing = await arranged.Service.ExecuteSwapAsync("terminal-guard-no-such-battle", arranged.Pair);

        Assert.Null(missing);

        var terminal = await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pair);

        Assert.NotNull(terminal);
        Assert.True(terminal!.Value.IsRejected);
        Assert.Equal(SwapRejectionReason.StaleAction, terminal.Value.Reason);

        // The wire codes differ, and the terminal rejection introduces no new one:
        // SIGNALR_PROTOCOL.md §5 item 3 delegates a Swap rejection's code to
        // MATCH3_RULES.md §2.1.2/§2.1.4, whose set this guard reuses unchanged.
        Assert.Equal("STALE_ACTION", SwapRejectionCodes.ToContractCode(terminal.Value.Reason));
        Assert.NotEqual("BATTLE_NOT_FOUND", SwapRejectionCodes.ToContractCode(terminal.Value.Reason));
    }

    [Fact]
    public void SwapRejectionCodeSet_ShouldBeUnchangedByTheGuard()
    {
        // AGENTS.md §7: no additional restriction or code is invented. The guard reuses
        // §2.1.4's existing code, so the Domain reason set and its wire spellings are
        // exactly the documented ones — no BATTLE_ENDED and nothing else new.
        var reasons = Enum.GetNames<SwapRejectionReason>().OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(
            ["InvalidActionPayload", "InvalidCellIndex", "InvalidSwap", "NoMatchFromSwap", "None", "StaleAction"],
            reasons);

        Assert.Equal(
            [
                "INVALID_ACTION_PAYLOAD",
                "INVALID_CELL_INDEX",
                "INVALID_SWAP",
                "NO_MATCH_FROM_SWAP",
                "STALE_ACTION",
            ],
            new[]
            {
                SwapRejectionReason.InvalidActionPayload,
                SwapRejectionReason.InvalidCellIndex,
                SwapRejectionReason.InvalidSwap,
                SwapRejectionReason.NoMatchFromSwap,
                SwapRejectionReason.StaleAction,
            }.Select(SwapRejectionCodes.ToContractCode).OrderBy(c => c, StringComparer.Ordinal));

        Assert.DoesNotContain("BATTLE_ENDED", reasons);
    }

    [Fact]
    public async Task InvalidAction_AgainstALiveBattle_ShouldKeepItsOwnReason()
    {
        // Compatibility: the guard's presence must not change what a live battle's
        // invalid action reports. An adjacent pair that produces no Match is still
        // rejected with its own §2.1.2 item 5 reason, not the state-precondition one.
        var arranged = await ArrangeLiveAsync("terminal-guard-live-invalid");

        var (from, to) = FindAdjacentPairThatProducesNoMatch(arranged.Before.BoardState);

        var result = (await arranged.Service.ExecuteSwapAsync(
            arranged.BattleId,
            new SwapRequest(from, to, arranged.Before.Sequence)))!.Value;

        Assert.True(result.IsRejected);
        Assert.Equal(SwapRejectionReason.NoMatchFromSwap, result.Reason);
    }

    // =======================================================================
    // 4. Terminal Card and Pet Skill casts — CARD_RULES.md §3 item 8 (TASK-270)
    // =======================================================================

    private const string GuardHealCardId = "card-terminal-guard-heal";
    private const string GuardSkillCardId = "card-terminal-guard-skill";

    /// <summary>
    /// The Power the cast scenarios arrange: enough for the scenario Card's Cost, so a live
    /// battle accepts the cast and a rejection's "no Power was spent" is not a vacuous claim
    /// about a zero-Cost Card (<c>CARD_RULES.md</c> §3.6).
    /// </summary>
    private const int GuardPower = 50;

    /// <summary>
    /// A Basic Card the scenario casts. Its Cost is non-zero and its effect is irrelevant to
    /// what the guard asserts — the guard must run before any effect.
    /// </summary>
    private static readonly CardDefinition GuardHealCard = new()
    {
        CardDefinitionId = GuardHealCardId,
        Name = "Terminal Guard Heal",
        Category = CardCategory.Basic,
        PowerCost = 20,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20)),
    };

    /// <summary>
    /// The active Pet's one Pet Skill Card for the cast scenarios
    /// (<c>CARD_RULES.md</c> §4): <c>ExecutePetSkillCastAsync</c> resolves it from the
    /// loadout snapshot, so it must be equipped.
    /// </summary>
    private static readonly CardDefinition GuardSkillCard = new()
    {
        CardDefinitionId = GuardSkillCardId,
        Name = "Terminal Guard Skill",
        Category = CardCategory.PetSkill,
        PowerCost = 20,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 10)),
    };

    /// <summary>
    /// The active Pet for the cast scenarios, carrying the two Cards above in its battle
    /// loadout snapshot (<c>CARD_RULES.md</c> §3 item 2, <c>GAME_STATE.md</c> §2.3).
    /// </summary>
    private static readonly BattleStateService.PetConfiguration CastPet = new(
        new PetId("pet_instance_1"),
        Element.Hoa,
        new PassiveId("xich-lang"),
        PassiveThreshold: 5,
        EquippedCards:
        [
            new EquippedCardIdentity(GuardHealCardId),
            new EquippedCardIdentity(GuardSkillCardId),
        ]);

    // -----------------------------------------------------------------------
    // 4.1 Rejection for both terminal sides, both cast methods
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CardCast_AgainstABossTerminalBattle_ShouldBeRejectedAsStaleAction()
    {
        // CARD_RULES.md §3 item 8 / GAME_RULES.md §1.4: a battle whose Boss has reached 0 HP
        // admits no Card cast. The cast is rejected at the action boundary under the
        // established state-precondition code — the cast-side counterpart of
        // MATCH3_RULES.md §2.1.4 item 7's terminal Swap guard.
        var arranged = await ArrangeTerminalCastAsync("terminal-cast-card-boss", bossHp: 0);

        var result = await arranged.Service.ExecuteCardCastAsync(arranged.BattleId, GuardHealCardId);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsRejected);
        Assert.Equal(CardCastRejectionReason.StaleAction, result.Value.Reason);
    }

    [Fact]
    public async Task CardCast_AgainstAPlayerTerminalBattle_ShouldBeRejectedAsStaleAction()
    {
        // The other terminal side: the active Pet's HP has reached 0. The guard is the same
        // condition read from the same state, so the rejection is identical.
        var arranged = await ArrangeTerminalCastAsync("terminal-cast-card-player", petHp: 0);

        var result = await arranged.Service.ExecuteCardCastAsync(arranged.BattleId, GuardHealCardId);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsRejected);
        Assert.Equal(CardCastRejectionReason.StaleAction, result.Value.Reason);
    }

    [Fact]
    public async Task PetSkillCast_AgainstABossTerminalBattle_ShouldBeRejectedAsStaleAction()
    {
        // CARD_RULES.md §3 item 8 governs both cast methods (SIGNALR_PROTOCOL.md §2 item 4
        // delegates CardCast and PetSkillCast validation to §3), so a Pet Skill cast is
        // refused on exactly the same terms.
        var arranged = await ArrangeTerminalCastAsync("terminal-cast-skill-boss", bossHp: 0);

        var result = await arranged.Service.ExecutePetSkillCastAsync(arranged.BattleId);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsRejected);
        Assert.Equal(CardCastRejectionReason.StaleAction, result.Value.Reason);
    }

    [Fact]
    public async Task PetSkillCast_AgainstAPlayerTerminalBattle_ShouldBeRejectedAsStaleAction()
    {
        // The other terminal side, for the second cast method.
        var arranged = await ArrangeTerminalCastAsync("terminal-cast-skill-player", petHp: 0);

        var result = await arranged.Service.ExecutePetSkillCastAsync(arranged.BattleId);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsRejected);
        Assert.Equal(CardCastRejectionReason.StaleAction, result.Value.Reason);
    }

    [Fact]
    public void TerminalCastRejectionCode_ShouldBeTheEstablishedStaleAction()
    {
        // SIGNALR_PROTOCOL.md §5 item 3 owns no parallel code list and delegates a cast
        // rejection's code to CARD_RULES.md §3, and the code the rule names is the
        // established STALE_ACTION — the same string MATCH3_RULES.md §2.1.4 item 5 defines
        // for the Swap guard. The Domain carrier added for the cast path therefore
        // introduces no new wire code, no discriminator, and no hub method: the existing
        // (bool Accepted, string? Reason) acknowledgement carries it unchanged.
        Assert.Equal(
            "STALE_ACTION",
            CardCastRejectionCodes.ToContractCode(CardCastRejectionReason.StaleAction));

        var codes = new[]
        {
            CardCastRejectionReason.CardNotInLoadout,
            CardCastRejectionReason.InsufficientPower,
            CardCastRejectionReason.InvalidCard,
            CardCastRejectionReason.CardCastAlreadyUsedThisTurn,
            CardCastRejectionReason.StaleAction,
        }.Select(CardCastRejectionCodes.ToContractCode).OrderBy(c => c, StringComparer.Ordinal).ToArray();

        Assert.Equal(
            [
                "CARD_CAST_ALREADY_USED_THIS_TURN",
                "CARD_NOT_IN_LOADOUT",
                "INSUFFICIENT_POWER",
                "INVALID_CARD",
                "STALE_ACTION",
            ],
            codes);

        Assert.DoesNotContain("BATTLE_ENDED", codes);
    }

    // -----------------------------------------------------------------------
    // 4.2 Nothing happens on a terminal cast rejection
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TerminalCastRejection_ShouldChangeNoStateValue_SpendNoPower_AndWriteNothing()
    {
        // CARD_RULES.md §3 item 8 with §3 item 3: the rejection is a gameplay no-op. The whole
        // record is compared structurally through the serializer the Redis round trip uses
        // (GAME_STATE.md §2.1.7), never by reference — the board, HP, Power, statuses, Crit
        // modifiers, Sequence, Turn, the committed-pair record, and RngState are all in it —
        // and every value the rule names is asserted individually as well.
        var arranged = await ArrangeTerminalCastAsync("terminal-cast-no-op", bossHp: 0);

        var writesBefore = arranged.Store.WriteCount;
        var before = arranged.Before;

        var card = (await arranged.Service.ExecuteCardCastAsync(arranged.BattleId, GuardHealCardId))!.Value;
        var skill = (await arranged.Service.ExecutePetSkillCastAsync(arranged.BattleId))!.Value;

        Assert.True(card.IsRejected);
        Assert.True(skill.IsRejected);
        Assert.Equal(CardCastRejectionReason.StaleAction, card.Reason);
        Assert.Equal(CardCastRejectionReason.StaleAction, skill.Reason);

        // REDIS_STATE.md §4 item 7: no store call at all — not a write and not a TTL refresh.
        Assert.Equal(writesBefore, arranged.Store.WriteCount);

        var stored = (await arranged.Service.GetBattleAsync(arranged.BattleId))!;

        Assert.Equal(BattleStateSerializer.Serialize(before), BattleStateSerializer.Serialize(stored));

        Assert.Equal(GuardPower, before.PetState.Power);
        Assert.Equal(before.PetState.Power, stored.PetState.Power);
        Assert.Equal(before.PetState.HP, stored.PetState.HP);
        Assert.Equal(before.BossState.HP, stored.BossState.HP);
        Assert.Equal(before.RngState, stored.RngState);
        Assert.Equal(before.Sequence, stored.Sequence);
        Assert.Equal(before.Turn, stored.Turn);
        Assert.Equal(before.CardCastsUsedThisTurn, stored.CardCastsUsedThisTurn);
        Assert.Equal(before.MatchCount, stored.MatchCount);
        Assert.Equal(before.Combo, stored.Combo);
        Assert.Equal(before.LastCommittedSwapPair, stored.LastCommittedSwapPair);
        Assert.True(before.BoardState.CellsEqual(stored.BoardState));
        Assert.True(StatusEffectLifecycle.EffectsEqual(
            before.PetState.ActiveStatusEffects,
            stored.PetState.ActiveStatusEffects));
        Assert.True(StatusEffectLifecycle.EffectsEqual(
            before.BossState.ActiveStatusEffects,
            stored.BossState.ActiveStatusEffects));
    }

    [Fact]
    public async Task TerminalCastRejection_ShouldResolveNoRound_EmitNoEvent_AndReachNoBattleEnd()
    {
        // CARD_RULES.md §3 item 8: the guard runs before §3 item 2's validation, so the cast
        // resolves no effect, produces no Boss response, advances no Round, and — per
        // ARCHITECTURE.md §4 item 4 / REDIS_STATE.md §3 — reaches no durable battle end and
        // clears nothing.
        var arranged = await ArrangeTerminalCastAsync("terminal-cast-no-gameplay", petHp: 0);

        var before = arranged.Before;

        var card = (await arranged.Service.ExecuteCardCastAsync(arranged.BattleId, GuardHealCardId))!.Value;
        var skill = (await arranged.Service.ExecutePetSkillCastAsync(arranged.BattleId))!.Value;

        Assert.True(card.IsRejected);
        Assert.True(skill.IsRejected);

        // A rejected cast produces no result state and no event batch at all
        // (CARD_RULES.md §3 item 3), so their absence is structural rather than an empty
        // list: reading either is the documented failure.
        Assert.Throws<InvalidOperationException>(() => _ = card.Events);
        Assert.Throws<InvalidOperationException>(() => _ = card.State);
        Assert.Throws<InvalidOperationException>(() => _ = skill.Events);
        Assert.Throws<InvalidOperationException>(() => _ = skill.State);

        var stored = (await arranged.Service.GetBattleAsync(arranged.BattleId))!;

        Assert.Equal(before.BossState.SkillCharge, stored.BossState.SkillCharge);
        Assert.Equal(before.BossState.SkillCooldown, stored.BossState.SkillCooldown);
        Assert.Equal(before.BossState.PassiveProgress, stored.BossState.PassiveProgress);
        Assert.Equal(before.Turn, stored.Turn);
        Assert.Equal(before.Sequence, stored.Sequence);

        // No durable battle end, no reward, and no cleanup ran.
        Assert.Equal(0, arranged.Persistence.Calls);
        Assert.Equal(0, arranged.Store.DeleteCount);
    }

    // -----------------------------------------------------------------------
    // 4.3 The retry path's fresh record is guarded too
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TerminalCastGuard_ShouldAlsoHoldOnTheFreshRecordOfTheCardCastRetryPath()
    {
        // CARD_RULES.md §3 item 8 reads the same condition from the record the action read AND
        // from the fresh record a refused compare-and-set re-reads (REDIS_STATE.md §4 item 2).
        // A concurrent winner can end the battle between the two reads — and because a
        // committed Swap restores the cast allowance (CARD_RULES.md §3 item 6), a guard that
        // inspected only the first read would let this cast resolve against a battle that had
        // already ended.
        var arranged = await ArrangeContendedTerminalCastAsync("terminal-cast-card-fresh");

        arranged.Store.ConcurrentWinnerBeforeFirstWrite = stored => stored with
        {
            Sequence = stored.Sequence + 1,
            BossState = stored.BossState with { HP = 0 },
        };

        var writesBefore = arranged.Store.WriteCount;

        var result = await arranged.Service.ExecuteCardCastAsync(arranged.BattleId, GuardHealCardId);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsRejected);
        Assert.Equal(CardCastRejectionReason.StaleAction, result.Value.Reason);

        // The refused attempt wrote nothing and the refused retry committed nothing
        // (REDIS_STATE.md §4 items 5, 7) and reached no battle end.
        Assert.Equal(writesBefore, arranged.Store.WriteCount);
        Assert.Equal(0, arranged.Persistence.Calls);
    }

    [Fact]
    public async Task TerminalCastGuard_ShouldAlsoHoldOnTheFreshRecordOfThePetSkillCastRetryPath()
    {
        // The same retry-path condition for the second cast method.
        var arranged = await ArrangeContendedTerminalCastAsync("terminal-cast-skill-fresh");

        arranged.Store.ConcurrentWinnerBeforeFirstWrite = stored => stored with
        {
            Sequence = stored.Sequence + 1,
            PetState = stored.PetState with { HP = 0 },
        };

        var writesBefore = arranged.Store.WriteCount;

        var result = await arranged.Service.ExecutePetSkillCastAsync(arranged.BattleId);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsRejected);
        Assert.Equal(CardCastRejectionReason.StaleAction, result.Value.Reason);

        Assert.Equal(writesBefore, arranged.Store.WriteCount);
        Assert.Equal(0, arranged.Persistence.Calls);
    }

    // -----------------------------------------------------------------------
    // 4.4 Compatibility: live battles, missing battles, and the allowance
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LiveBattle_CardCastAndPetSkillCast_ShouldKeepTheirExistingBehaviour()
    {
        // The guard must not touch a battle that is in progress: a Card cast commits and
        // spends its Cost (CARD_RULES.md §3 item 4), and the committed Turn's one-cast
        // allowance then rejects the Pet Skill cast with its own reason (item 6, ADR-021) —
        // never with the terminal guard's.
        var store = new InMemoryBattleStateRepository();
        var persistence = new RecordingBattleResultPersistence();
        var service = new BattleStateService(
            store,
            new FixedRngSeedSource(),
            persistence,
            new StubCardDefinitionLookup(GuardHealCard, GuardSkillCard));

        var battleId = "terminal-cast-live";
        var created = await service.CreateBattleAsync(battleId, Owner, CastPet, BossDefinitions.HoaLong);

        store.ApplyConcurrentWrite(created with { PetState = created.PetState with { Power = GuardPower } });

        var card = (await service.ExecuteCardCastAsync(battleId, GuardHealCardId))!.Value;

        Assert.True(card.IsAccepted);
        Assert.Equal(GuardPower - GuardHealCard.PowerCost, card.State.PetState.Power);
        Assert.Equal(created.Sequence + 1, card.State.Sequence);

        var skill = (await service.ExecutePetSkillCastAsync(battleId))!.Value;

        Assert.True(skill.IsRejected);
        Assert.Equal(CardCastRejectionReason.CardCastAlreadyUsedThisTurn, skill.Reason);
        Assert.NotEqual(CardCastRejectionReason.StaleAction, skill.Reason);

        // CARD_RULES.md §3 item 6: the rejected second cast spends nothing, and a
        // non-terminal cast reaches no durable battle end.
        var stored = (await service.GetBattleAsync(battleId))!;

        Assert.Equal(card.State.PetState.Power, stored.PetState.Power);
        Assert.Equal(0, persistence.Calls);
    }

    [Fact]
    public async Task LiveBattle_PetSkillCastThatEndsTheBattle_ShouldStillPersistTheTerminalResult()
    {
        // The guard must not block a legitimate cast either: a Pet Skill cast against a live
        // battle whose damage reduces the Boss to 0 HP commits, emits BattleWon, and reaches
        // the documented battle end exactly once — the guard's precondition is that the battle
        // is still in progress, which it was when this request arrived
        // (ARCHITECTURE.md §4 item 4).
        var store = new InMemoryBattleStateRepository();
        var persistence = new RecordingBattleResultPersistence();
        var service = new BattleStateService(
            store,
            new FixedRngSeedSource(),
            persistence,
            new StubCardDefinitionLookup(GuardHealCard, GuardSkillCard));

        var battleId = "terminal-cast-live-kill";
        var created = await service.CreateBattleAsync(
            battleId,
            Owner,
            CastPet,
            BossDefinitions.HoaLong with { MaxHP = 1 });

        store.ApplyConcurrentWrite(created with { PetState = created.PetState with { Power = GuardPower } });

        var skill = (await service.ExecutePetSkillCastAsync(battleId))!.Value;

        Assert.True(skill.IsAccepted);
        Assert.Contains(skill.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.Equal(0, skill.State.BossState.HP);
        Assert.Equal(1, persistence.Calls);
    }

    [Fact]
    public async Task MissingBattle_ShouldRemainDistinctFromATerminalCastRejection()
    {
        // The two outcomes are different paths and different responses, and this asserts both
        // rather than equating them:
        //   * a battle with no record at all  → the service returns null, which the caller
        //     reports as BATTLE_NOT_FOUND (SIGNALR_PROTOCOL.md §5, §7 item 3);
        //   * a battle whose record still holds a terminal state → a non-null rejection whose
        //     reason is the state-precondition code (CARD_RULES.md §3 item 8).
        var arranged = await ArrangeTerminalCastAsync("terminal-cast-distinct", bossHp: 0);

        Assert.Null(await arranged.Service.ExecuteCardCastAsync("terminal-cast-no-such-battle", GuardHealCardId));
        Assert.Null(await arranged.Service.ExecutePetSkillCastAsync("terminal-cast-no-such-battle"));

        var card = await arranged.Service.ExecuteCardCastAsync(arranged.BattleId, GuardHealCardId);
        var skill = await arranged.Service.ExecutePetSkillCastAsync(arranged.BattleId);

        Assert.NotNull(card);
        Assert.NotNull(skill);
        Assert.True(card!.Value.IsRejected);
        Assert.True(skill!.Value.IsRejected);
        Assert.Equal(CardCastRejectionReason.StaleAction, card.Value.Reason);
        Assert.Equal(CardCastRejectionReason.StaleAction, skill.Value.Reason);
        Assert.Equal("STALE_ACTION", CardCastRejectionCodes.ToContractCode(card.Value.Reason));
        Assert.NotEqual("BATTLE_NOT_FOUND", CardCastRejectionCodes.ToContractCode(card.Value.Reason));
    }

    // -----------------------------------------------------------------------
    // 4.5 Cast-scenario fixtures
    // -----------------------------------------------------------------------

    private sealed record ArrangedCast(
        string BattleId,
        BattleStateService Service,
        InMemoryBattleStateRepository Store,
        RecordingBattleResultPersistence Persistence,
        BattleState Before);

    private sealed record ArrangedCastRace(
        string BattleId,
        BattleStateService Service,
        ContendedBattleStateRepository Store,
        RecordingBattleResultPersistence Persistence);

    /// <summary>
    /// Creates a battle with the cast loadout and forces the terminal side the scenario names
    /// — the reachable service-level path, since a terminal record survives whenever the
    /// documented battle end did not clear it (<c>DATABASE.md</c> §1 sourcing item 3).
    /// </summary>
    /// <remarks>
    /// The Pet's Power is arranged so the guard's "nothing was spent" half is observable. The
    /// write is the store double's documented concurrent-writer idiom, which is not counted as
    /// a write of the request under test. A recording durable-result boundary is composed so a
    /// rejection can prove it reached no battle end.
    /// </remarks>
    private static async Task<ArrangedCast> ArrangeTerminalCastAsync(
        string battleId,
        int? bossHp = null,
        int? petHp = null)
    {
        var store = new InMemoryBattleStateRepository();
        var persistence = new RecordingBattleResultPersistence();
        var service = new BattleStateService(
            store,
            new FixedRngSeedSource(),
            persistence,
            new StubCardDefinitionLookup(GuardHealCard, GuardSkillCard));

        var created = await service.CreateBattleAsync(battleId, Owner, CastPet, BossDefinitions.HoaLong);

        store.ApplyConcurrentWrite(created with
        {
            BossState = bossHp is { } bossHitPoints
                ? created.BossState with { HP = bossHitPoints }
                : created.BossState,
            PetState = (petHp is { } petHitPoints
                ? created.PetState with { HP = petHitPoints }
                : created.PetState) with { Power = GuardPower },
        });

        var before = (await store.GetAsync(battleId))!;

        return new ArrangedCast(battleId, service, store, persistence, before);
    }

    /// <summary>
    /// The same cast scenario over the store double that injects a competitor's commit at the
    /// first compare-and-set (<c>REDIS_STATE.md</c> §4 item 2), which is what makes a refused
    /// write and its fresh re-read deterministic rather than a scheduler coincidence.
    /// </summary>
    /// <remarks>
    /// The Pet's Power is armed so the cast clears <c>CARD_RULES.md</c> §3 item 2's checks on
    /// the live record and therefore reaches the compare-and-set the competitor refuses.
    /// </remarks>
    private static async Task<ArrangedCastRace> ArrangeContendedTerminalCastAsync(string battleId)
    {
        var store = new ContendedBattleStateRepository();
        var persistence = new RecordingBattleResultPersistence();
        var service = new BattleStateService(
            store,
            new FixedRngSeedSource(),
            persistence,
            new StubCardDefinitionLookup(GuardHealCard, GuardSkillCard));

        var created = await service.CreateBattleAsync(battleId, Owner, CastPet, BossDefinitions.HoaLong);

        var armed = created with { PetState = created.PetState with { Power = GuardPower } };

        Assert.True(await store.TryUpdateAsync(armed, created.Sequence));

        return new ArrangedCastRace(battleId, service, store, persistence);
    }

    /// <summary>
    /// The Card definition lookup the cast scenarios run against — the same in-memory
    /// double the Card-cast suite uses, so no production seam is added for the guard.
    /// </summary>
    private sealed class StubCardDefinitionLookup : ICardDefinitionLookup
    {
        private readonly Dictionary<string, CardDefinition> _definitions = new(StringComparer.Ordinal);

        public StubCardDefinitionLookup(params CardDefinition[] definitions)
        {
            foreach (var definition in definitions)
            {
                _definitions[definition.CardDefinitionId] = definition;
            }
        }

        public Task<CardDefinition?> GetDefinitionAsync(
            string cardDefinitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_definitions.TryGetValue(cardDefinitionId, out var definition) ? definition : null);
    }

    /// <summary>
    /// Records every call to the documented battle-end step (<c>ARCHITECTURE.md</c> §4 item 4),
    /// so a rejection can prove it reached no durable result and a committed terminal cast can
    /// prove it reached exactly one. It stores nothing: what that boundary does with the result
    /// is <c>BattleResultService</c>'s contract and is covered by its own suites.
    /// </summary>
    private sealed class RecordingBattleResultPersistence : IBattleResultPersistence
    {
        public int Calls { get; private set; }

        public Task<bool> PersistTerminalResultAsync(
            BattleState state,
            BattleOutcome outcome,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            return Task.FromResult(true);
        }
    }

    // =======================================================================
    // Fixtures
    // =======================================================================

    private sealed record ArrangedTerminal(
        string BattleId,
        BattleStateService Service,
        InMemoryBattleStateRepository Store,
        SwapRequest Pair,
        BattleState Before);

    /// <summary>
    /// Creates a battle, pins the scenario's board and RNG state, and forces the
    /// battle into the terminal state the scenario names — the reachable
    /// service-level path, since the service is composed without the durable result
    /// boundary and therefore does not clear the record.
    /// </summary>
    private static async Task<ArrangedTerminal> ArrangeTerminalAsync(
        string battleId,
        int? bossHp = null,
        int? petHp = null)
    {
        var store = new InMemoryBattleStateRepository();
        var service = new BattleStateService(store, new FixedRngSeedSource());

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        // Any match-producing pair will do: the guard must refuse before §2.1.2 is
        // consulted, so the scenario uses a legal Swap on a live board and proves the
        // refusal is the terminal precondition rather than a validator reason.
        var pinned = RoundTestSupport.PlayerMatch(GemType.Power, created.RngState);

        store.ApplyConcurrentWrite(created with
        {
            BoardState = pinned.Board,
            BossState = bossHp is { } bossHitPoints
                ? created.BossState with { HP = bossHitPoints }
                : created.BossState,
            PetState = petHp is { } petHitPoints
                ? created.PetState with { HP = petHitPoints }
                : created.PetState,
        });

        var before = (await store.GetAsync(battleId))!;

        return new ArrangedTerminal(battleId, service, store, pinned.Swap, before);
    }

    /// <summary>
    /// A live (non-terminal) battle with a pinned, match-producing Player Swap — the
    /// compatibility scenario, where the guard must not fire.
    /// </summary>
    private static async Task<ArrangedTerminal> ArrangeLiveAsync(string battleId)
    {
        var store = new InMemoryBattleStateRepository();
        var service = new BattleStateService(store, new FixedRngSeedSource());

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Power, created.RngState);

        store.ApplyConcurrentWrite(created with { BoardState = pinned.Board });

        var before = (await store.GetAsync(battleId))!;

        return new ArrangedTerminal(battleId, service, store, pinned.Swap, before);
    }

    /// <summary>
    /// An orthogonal adjacent pair that is not the pair the scenario committed — a
    /// different logical action against the same ended battle.
    /// </summary>
    private static (int From, int To) FindAnotherAdjacentPair(SwapRequest committed)
    {
        var row = BoardState.ToRow(committed.From);
        var column = BoardState.ToColumn(committed.From);

        if (column + 1 < BoardState.Columns)
        {
            var candidate = (committed.From, BoardState.ToIndex(row, column + 1));
            if (!Matches(candidate, committed))
            {
                return candidate;
            }
        }

        if (column - 1 >= 0)
        {
            var candidate = (committed.From, BoardState.ToIndex(row, column - 1));
            if (!Matches(candidate, committed))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "MATCH3_RULES.md §1.0: every cell of an 8x8 board has at least one orthogonal neighbour.");
    }

    private static bool Matches((int From, int To) pair, SwapRequest committed) =>
        (pair.From == committed.From && pair.To == committed.To)
        || (pair.From == committed.To && pair.To == committed.From);

    /// <summary>
    /// An orthogonal adjacent pair whose exchange produces no Match — §2.1.2 item 5's
    /// rejection on a live board.
    /// </summary>
    private static (int From, int To) FindAdjacentPairThatProducesNoMatch(BoardState board)
    {
        for (var row = 0; row < BoardState.Rows; row++)
        {
            for (var column = 0; column < BoardState.Columns; column++)
            {
                var index = BoardState.ToIndex(row, column);

                foreach (var neighbour in new[]
                         {
                             column + 1 < BoardState.Columns ? index + 1 : -1,
                             row + 1 < BoardState.Rows ? index + BoardState.Width : -1,
                         })
                {
                    if (neighbour < 0)
                    {
                        continue;
                    }

                    if (MatchDetector.Detect(board.WithSwapped(index, neighbour)).Count == 0)
                    {
                        return (index, neighbour);
                    }
                }
            }
        }

        throw new InvalidOperationException(
            "MATCH3_RULES.md §2.1.2 item 5: a board with at least one valid Swap still has a pair that produces none.");
    }
}
