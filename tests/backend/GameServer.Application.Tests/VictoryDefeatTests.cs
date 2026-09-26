using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// Victory / Defeat tests — <c>GAME_RULES.md</c> §1.4 ("a battle ends when either
/// the Boss or Player reaches 0 HP"), <c>BOSS_RULES.md</c> §5 item 4 and §7, and
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.19.
///
/// <code>
/// Rule (GAME_RULES.md §1.4, BOSS_RULES.md §5 item 4)
///  ↓
/// Scenario (Given a terminal HP, When the resolution reaches its check, Then the
///           documented outcome and payload)
///  ↓
/// Test
/// </code>
///
/// <b>The order is the contract.</b> <c>BOSS_RULES.md</c> §5 item 4: "Order:
/// Player→Boss Damage → Enrage → terminal Boss HP check → Boss Response 18a–18c
/// → terminal Player HP check." A Boss killed by the player's damage therefore
/// ends the battle before it can respond, and the Player check only runs after
/// the Boss has had its chance.
/// </summary>
public class VictoryDefeatTests
{
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>
    /// The owning Player of these battles (<c>GAME_STATE.md</c> §2.8) — the
    /// identity the creation path records. This suite asserts outcome and the
    /// terminal payload, not identity, so one fixture value is supplied in one
    /// place.
    /// </summary>
    private static readonly PlayerId Owner = new("player_victory_defeat_owner");

    /// <summary>
    /// A Boss definition whose HP is low enough that one committed Swap's damage
    /// kills it, and whose own attack is strong enough to kill the player in one hit
    /// — the two terminal paths, with every other value left at the MVP
    /// configuration <c>BOSS_RULES.md</c> §6.3–§6.4 documents. The stats are
    /// configuration, not invariants (<c>§6.1</c>: "not universal balance
    /// invariants"), so restating them for a scenario is what the document permits;
    /// no rule is invented.
    /// </summary>
    private static BossDefinition FragileBoss(int maxHp) =>
        BossDefinitions.HoaLong with { MaxHP = maxHp };

    /// <summary>A Boss that kills the player in one Basic Attack.</summary>
    private static BossDefinition LethalBoss() =>
        BossDefinitions.HoaLong with
        {
            ATK = 100_000,
            // Keep the Skill out of the way so the instance under test is the Basic
            // Attack, and keep the Passive inert so no other step changes the state
            // (a null Threshold is the always-active, non-charging marker).
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition
                with { ChargeRequirement = int.MaxValue },
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition
                with { Threshold = null },
        };

    // =======================================================================
    // BattleWon — GAME_RULES.md §1.4, SIGNALR_PROTOCOL.md §3.2.19
    // =======================================================================

    [Fact]
    public async Task BattleWon_ShouldBeEmittedWhenTheBossReachesZeroHp()
    {
        // GAME_RULES.md §1.4: the battle ends when the Boss reaches 0 HP. A Boss with
        // 1 HP cannot survive any damage instance, and a committed Swap always deals a
        // positive amount at the documented MVP stats (COMBAT_RULES.md §3).
        var service = NewService();
        var battleId = "victory-boss-dies";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, FragileBoss(maxHp: 1));

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        var won = result.Value.Events.Where(e => e.Type == BattleEventType.BattleWon).ToArray();

        Assert.Single(won);
        Assert.Equal(0, result.Value.State.BossState.HP);
    }

    [Fact]
    public async Task BattleWon_ShouldCarryTheTerminalHpValues()
    {
        // SIGNALR_PROTOCOL.md §3.2.19 item 2: "finalBossHp and finalPlayerHp are the
        // terminal HP values ... the state values at the moment the battle ended,
        // after all damage from the final action has been applied". On this path the
        // Boss is at 0 and the player is untouched by any Boss Response.
        var service = NewService();
        var battleId = "victory-hp-values";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, FragileBoss(maxHp: 1));

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        var won = result.Value.Events.Single(e => e.Type == BattleEventType.BattleWon).BattleWon;

        Assert.Equal(0, won.FinalBossHp);
        Assert.Equal(result.Value.State.BossState.HP, won.FinalBossHp);

        // The Boss died before it could respond, so the active Pet's HP is exactly
        // what this Swap's own resolution left it — which, before the Boss Response,
        // is the value the pre-swap state held. The payload member keeps the fixed
        // protocol label `finalPlayerHp` while carrying that Pet HP (ADR-011 item 6,
        // GAME_STATE.md §2.3).
        Assert.Equal(created.PetState.HP, won.FinalPlayerHp);
        Assert.Equal(result.Value.State.PetState.HP, won.FinalPlayerHp);
    }

    [Fact]
    public async Task BattleWon_ShouldPreemptTheEntireBossResponse()
    {
        // BOSS_RULES.md §5 item 4 / §3.3: the battle ends "with no Boss Response". A
        // dead Boss must not trigger its Passive, cast its Skill, or make a Basic
        // Attack — so none of those events appears after the BattleWon.
        var service = NewService();
        var battleId = "victory-no-response";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, FragileBoss(maxHp: 1));

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        var events = result.Value.Events;
        var wonIndex = events
            .Select((e, i) => (e, i))
            .Single(t => t.e.Type == BattleEventType.BattleWon)
            .i;

        // BattleWon is the resolution's LAST event — nothing follows it.
        Assert.Equal(events.Count - 1, wonIndex);

        // No Boss Passive event, no Skill cast, and no Boss→Player damage instance.
        Assert.DoesNotContain(
            events,
            e => e.Type == BattleEventType.PassiveCharged
                && e.PassiveCharged.Source == PassiveEventSource.Boss);

        Assert.DoesNotContain(
            events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.Source == PassiveEventSource.Boss);

        Assert.DoesNotContain(events, e => e.Type == BattleEventType.BossSkillCast);

        Assert.DoesNotContain(
            events,
            e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Boss);

        // The player took no damage at all: the only damage instance is the player's.
        Assert.Equal(created.PetState.HP, result.Value.State.PetState.HP);
    }

    [Fact]
    public async Task BattleWon_ShouldStillEvaluateEnrageFirst()
    {
        // BOSS_RULES.md §5 item 4 / TASK-022 §3.6: "the state transition is applied
        // whenever the HP condition holds, including when Player damage has just
        // reduced Boss HP to 0". Enrage is NOT skipped on death — only the Boss
        // Response is. So the Boss ends at 0 HP AND Enraged.
        var service = NewService();
        var battleId = "victory-enrage-first";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, FragileBoss(maxHp: 1));

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        Assert.Equal(0, result.Value.State.BossState.HP);
        Assert.Equal(BossStateKind.Enraged, result.Value.State.BossState.State);
    }

    [Fact]
    public async Task BattleWon_ShouldBeTheOnlyOutcomeEvent()
    {
        // Both outcomes cannot occur on one action: the Boss check runs first and ends
        // the resolution, so a BattleLost can never accompany a BattleWon.
        var service = NewService();
        var battleId = "victory-exclusive";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, FragileBoss(maxHp: 1));

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        Assert.Single(result.Value.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BattleLost);
    }

    // =======================================================================
    // BattleLost — GAME_RULES.md §1.4, SIGNALR_PROTOCOL.md §3.2.19
    // =======================================================================

    [Fact]
    public async Task BattleLost_ShouldBeEmittedWhenThePlayerReachesZeroHp()
    {
        // GAME_RULES.md §1.4: the battle also ends when the Player reaches 0 HP. With
        // a Boss whose Basic Attack exceeds the player's whole HP pool, the post-
        // response check fires.
        var service = NewService();
        var battleId = "defeat-player-dies";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, LethalBoss());

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        var lost = result.Value.Events.Where(e => e.Type == BattleEventType.BattleLost).ToArray();

        Assert.Single(lost);
        Assert.Equal(0, result.Value.State.PetState.HP);
    }

    [Fact]
    public async Task BattleLost_ShouldCarryTheTerminalHpValues()
    {
        // SIGNALR_PROTOCOL.md §3.2.19 item 2: both members are the terminal state
        // values. Here the player is at 0 and the Boss carries whatever the action left
        // — the boss survived, so it is above 0.
        var service = NewService();
        var battleId = "defeat-hp-values";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, LethalBoss());

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        var lost = result.Value.Events.Single(e => e.Type == BattleEventType.BattleLost).BattleLost;

        Assert.Equal(0, lost.FinalPlayerHp);
        Assert.Equal(result.Value.State.PetState.HP, lost.FinalPlayerHp);

        Assert.Equal(result.Value.State.BossState.HP, lost.FinalBossHp);
        Assert.True(lost.FinalBossHp > 0);
    }

    [Fact]
    public async Task BattleLost_ShouldFollowTheBossResponseAndBeLast()
    {
        // The Pet HP terminal check — the Player side's, since the Pet is its combat
        // character (ADR-011 items 3 and 5) — runs AFTER the Boss Response
        // (BOSS_RULES.md §5
        // item 4's order), because the Boss has just had its chance to reduce it — so
        // the Boss→Player damage instance precedes the BattleLost, and the outcome is
        // the resolution's last event.
        var service = NewService();
        var battleId = "defeat-ordering";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, LethalBoss());

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        var events = result.Value.Events;

        var bossDamageIndex = events
            .Select((e, i) => (e, i))
            .First(t => t.e.Type == BattleEventType.DamageDealt
                && t.e.DamageDealt.Source == DamageParty.Boss)
            .i;

        var lostIndex = events
            .Select((e, i) => (e, i))
            .Single(t => t.e.Type == BattleEventType.BattleLost)
            .i;

        Assert.True(bossDamageIndex < lostIndex);
        Assert.Equal(events.Count - 1, lostIndex);
    }

    [Fact]
    public async Task BattleLost_ShouldBeTheOnlyOutcomeEvent()
    {
        // A surviving Boss means the Boss terminal check did not fire, so no BattleWon
        // can accompany the defeat.
        var service = NewService();
        var battleId = "defeat-exclusive";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, LethalBoss());

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        Assert.Single(result.Value.Events, e => e.Type == BattleEventType.BattleLost);
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BattleWon);
    }

    [Fact]
    public async Task BattleLost_ShouldStillRunTheBossPassiveAndResponse()
    {
        // Only the Boss-death path short-circuits. When the Boss survives it runs its
        // full Response — Passive then Basic Attack — and the defeat is the
        // consequence of that response, so the Boss's own events are present.
        var service = NewService();
        var battleId = "defeat-full-response";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, LethalBoss());

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        // The Boss's damage instance is there — it is what caused the defeat.
        Assert.Contains(
            result.Value.Events,
            e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Boss);

        // This fixture's Passive is inert (PassiveThreshold = 0, the Always-Active
        // marker), so it emits no match-driven events — BOSS_RULES.md §6.2.
        Assert.DoesNotContain(
            result.Value.Events,
            e => e.Type == BattleEventType.PassiveCharged
                && e.PassiveCharged.Source == PassiveEventSource.Boss);
    }

    // =======================================================================
    // Both alive — no outcome event
    // =======================================================================

    [Fact]
    public async Task BothAlive_ShouldEmitNoOutcomeEvent()
    {
        // GAME_RULES.md §1.4: the battle ends only when a side reaches 0 HP. At the
        // documented MVP stats neither does in one Swap, so NEITHER outcome event is
        // emitted and the battle continues — the absence is the documented statement
        // that the action was not terminal.
        var service = NewService();
        var battleId = "outcome-none";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        Assert.True(result.Value.State.BossState.HP > 0);
        Assert.True(result.Value.State.PetState.HP > 0);

        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BattleLost);
    }

    [Fact]
    public async Task BothAlive_ShouldStillRunTheWholeResponse()
    {
        // The non-terminal path is the full documented loop: the player damages the
        // Boss, the Boss Passive runs, the Boss responds, and the player takes damage.
        var service = NewService();
        var battleId = "outcome-none-full";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        Assert.True(result.Value.State.BossState.HP < created.BossState.HP);
        Assert.True(result.Value.State.PetState.HP < created.PetState.HP);

        Assert.Contains(
            result.Value.Events,
            e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Player);

        Assert.Contains(
            result.Value.Events,
            e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Boss);
    }

    // =======================================================================
    // The terminal checks are ordered Boss-first
    // =======================================================================

    [Fact]
    public async Task BossDeath_ShouldWinEvenWhenTheBossWouldHaveKilledThePlayer()
    {
        // GAME_RULES.md §1.4 / BOSS_RULES.md §5 item 4: the Boss HP check precedes the
        // Boss Response, so a Boss killed by the player's damage never gets to attack.
        // This fixture's Boss is lethal AND fragile: if the order were reversed, the
        // player would die first. BattleWon is the documented outcome.
        var service = NewService();
        var battleId = "outcome-boss-first";

        var boss = LethalBoss() with { MaxHP = 1 };

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        Assert.Single(result.Value.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BattleLost);

        // The player is untouched — the lethal Boss never acted.
        Assert.Equal(created.PetState.HP, result.Value.State.PetState.HP);
        Assert.True(result.Value.State.PetState.HP > 0);
    }

    [Fact]
    public async Task Outcome_ShouldWriteExactlyOneStateIncludingTheTerminalHp()
    {
        // GAME_STATE.md §5.1: one action produces ONE post-resolution write-back. The
        // result the caller receives is the state the registry holds, and the terminal
        // HP the outcome event reports is the HP that state carries — an event is never
        // a substitute for the write-back (GAME_EVENTS.md §3 item 6).
        foreach (var (battleId, boss, outcomeType) in new[]
                 {
                     ("outcome-writeback-won", FragileBoss(maxHp: 1), BattleEventType.BattleWon),
                     ("outcome-writeback-lost", LethalBoss(), BattleEventType.BattleLost),
                 })
        {
            var service = NewService();
            var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

            var pair = FindMatchProducingPair(created.BoardState);
            var result = await service.ExecuteSwapAsync(battleId, pair);

            Assert.True(result!.Value.IsAccepted);

            var stored = (await service.GetBattleAsync(battleId))!;

            // The store's re-read is a separate deserialized record, so the equality
            // asserted is the value equality of the written-back state (BattleState is
            // a record, GAME_STATE.md §5.1's one write-back is what "the same state"
            // means) — never object identity.
            Assert.Equal(result.Value.State, stored);
            Assert.Equal(stored.BossState.HP, result.Value.State.BossState.HP);
            Assert.Equal(stored.PetState.HP, result.Value.State.PetState.HP);

            // The outcome event's two HP values are the stored state's own values.
            var outcome = result.Value.Events.Single(e => e.Type == outcomeType);

            var (finalBossHp, finalPlayerHp) = outcomeType == BattleEventType.BattleWon
                ? (outcome.BattleWon.FinalBossHp, outcome.BattleWon.FinalPlayerHp)
                : (outcome.BattleLost.FinalBossHp, outcome.BattleLost.FinalPlayerHp);

            Assert.Equal(stored.BossState.HP, finalBossHp);
            Assert.Equal(stored.PetState.HP, finalPlayerHp);
        }
    }

    [Fact]
    public async Task Outcome_ShouldBeReachableOnTheSameCommittedSwapContract()
    {
        // A terminal action is still one committed Swap: Turn and Sequence advance by
        // exactly 1, so the outcome does not change the counter contract
        // (MATCH3_RULES.md §8.1–§8.2).
        var service = NewService();
        var battleId = "outcome-counters";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, FragileBoss(maxHp: 1));

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Equal(created.Turn + 1, result.Value.State.Turn);
        Assert.Equal(created.Sequence + 1, result.Value.State.Sequence);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    /// <summary>
    /// Builds the service under test over an <see cref="InMemoryBattleStateRepository"/>.
    ///
    /// <b>The repository is a TEST DOUBLE for the active-battle-state store.</b>
    /// <c>REDIS_STATE.md</c> §1–§4 make Redis the owner of Active Battle State, and
    /// §2 item 2 / §7 item 5 permit no in-process substitute for it in the running
    /// server — the production composition registers
    /// <c>GameServer.Infrastructure.Redis.BattleStateRepository</c> instead. This
    /// helper exists only so a test that asserts a <i>gameplay</i> rule can run the
    /// real <see cref="BattleStateService"/> pipeline without a live Redis instance;
    /// the double still models the documented creation/absence and Sequence
    /// compare-and-set semantics, so a test cannot pass against a more permissive
    /// contract than the store actually offers.
    /// </summary>
    private static BattleStateService NewService() =>
        new(new InMemoryBattleStateRepository(), new FixedRngSeedSource());

    private static SwapRequest FindMatchProducingPair(BoardState board)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (MatchDetector.Detect(board.WithSwapped(from, to)).Count > 0)
            {
                return new SwapRequest(from, to);
            }
        }

        throw new InvalidOperationException(
            "A generated board has at least one valid Swap (MATCH3_RULES.md §1.4).");
    }

    private static IEnumerable<(int From, int To)> AllAdjacentPairs()
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            if (right >= 0)
            {
                yield return (index, right);
            }

            if (down >= 0)
            {
                yield return (index, down);
            }
        }
    }
}
