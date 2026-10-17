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
///
/// <b>Both attack directions are ATK-gated, so both terminal scenarios are
/// arranged.</b> <c>ADR-026</c> D1 / <c>GAME_RULES.md</c> §17 steps 15–17 and 18d
/// make each side's attack damage conditional on <i>that same side's</i> confirmed
/// ATK-Gem match, and <c>ADR-026</c> §5 retires the legacy fallback basic attack
/// (<c>Base Damage = Boss.ATK</c> independently of the board). A scenario here
/// therefore controls the Gem type its acting side clears — a win by pinning a Swap
/// that clears an ATK Gem, a loss by running rounds until the Boss's own ATK-gated
/// instance lands — instead of trusting whatever type a board drawn from the
/// battle's RNG happens to match (<see cref="RoundTestSupport"/>, <c>ADR-026</c> §2).
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
    /// A Boss definition whose HP is low enough that one ATK-gated Swap's damage
    /// kills it — the victory path (<c>ADR-026</c> D1). Its other values are left at
    /// the MVP configuration <c>BOSS_RULES.md</c> §6.3–§6.4 documents. The stats are
    /// configuration, not invariants (<c>§6.1</c>: "not universal balance
    /// invariants"), so restating them for a scenario is what the document permits;
    /// no rule is invented.
    /// </summary>
    private static BossDefinition FragileBoss(int maxHp) =>
        BossDefinitions.HoaLong with { MaxHP = maxHp };

    /// <summary>
    /// A Boss whose own <c>GAME_RULES.md</c> §17 step 18d instance is lethal in one
    /// hit — the defeat path. The legacy fallback basic attack is retired
    /// (<c>ADR-026</c> §5), so this <c>ATK</c> value decides the magnitude of the
    /// instance the Boss's own board action qualifies for, never of an unconditional
    /// attack: the Boss must still clear an ATK Gem for the instance to exist at all.
    /// </summary>
    private static BossDefinition LethalBoss() =>
        BossDefinitions.HoaLong with
        {
            ATK = 100_000,
            // Keep the Skill out of the way so the instance under test is the §18d one,
            // and keep the Passive from charging or triggering by recording the
            // documented "no match-charging threshold" marker (BOSS_RULES.md §6.2).
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
        // GAME_RULES.md §1.4: the battle ends when the Boss reaches 0 HP. The round's
        // Boss damage is ATK-gated (ADR-026 D1), so the committed Swap is pinned to
        // clear an ATK Gem, and a Boss created at 1 HP cannot survive that qualifying
        // instance (COMBAT_RULES.md §3).
        var battle = await ArrangeAsync("victory-boss-dies", FragileBoss(maxHp: 1));

        var result = await battle.Service.ExecuteSwapAsync(battle.BattleId, battle.Pinned.Swap);

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
        var battle = await ArrangeAsync("victory-hp-values", FragileBoss(maxHp: 1));

        var result = await battle.Service.ExecuteSwapAsync(battle.BattleId, battle.Pinned.Swap);

        Assert.True(result!.Value.IsAccepted);

        var won = result.Value.Events.Single(e => e.Type == BattleEventType.BattleWon).BattleWon;

        Assert.Equal(0, won.FinalBossHp);
        Assert.Equal(result.Value.State.BossState.HP, won.FinalBossHp);

        // The Boss died before it could respond, so the active Pet's HP is exactly
        // what this round's own resolution left it — which, before the Boss Response,
        // is the value the pre-round state held. The pinned board clears ATK Gems only
        // (RoundTestSupport), so no HP Gem healed the Pet either. The payload member
        // keeps the fixed protocol label `finalPlayerHp` while carrying that Pet HP
        // (ADR-011 item 6, GAME_STATE.md §2.3).
        Assert.Equal(battle.Before.PetState.HP, won.FinalPlayerHp);
        Assert.Equal(result.Value.State.PetState.HP, won.FinalPlayerHp);
    }

    [Fact]
    public async Task BattleWon_ShouldPreemptTheEntireBossResponse()
    {
        // BOSS_RULES.md §5 item 4 / §3.3 / ADR-026 §2: the battle ends "with no Boss
        // Response". A dead Boss must not trigger its Passive, cast its Skill, act on
        // the shared board, or damage the Pet — so none of those events appears, and
        // the outcome is the resolution's LAST event.
        var battle = await ArrangeAsync("victory-no-response", FragileBoss(maxHp: 1));

        var result = await battle.Service.ExecuteSwapAsync(battle.BattleId, battle.Pinned.Swap);

        Assert.True(result!.Value.IsAccepted);

        var events = result.Value.Events;
        var wonIndex = events
            .Select((e, i) => (e, i))
            .Single(t => t.e.Type == BattleEventType.BattleWon)
            .i;

        // BattleWon is the resolution's LAST event — nothing follows it, step 19a
        // included (it consumes durations and emits nothing).
        Assert.Equal(events.Count - 1, wonIndex);

        // The kill came from the Player's own ATK-gated instance, so the round did
        // reach steps 15–17 before the terminal check (ADR-026 D1).
        Assert.Contains(
            events,
            e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Player);

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

        // §17 step 18a is part of the response §5 item 4 skips, so the skipped phase
        // produced no Boss board cycle either.
        Assert.Empty(RoundTestSupport.BossBoardEvents(result.Value));

        // The player took no damage at all: the only damage instance is the player's.
        Assert.Equal(battle.Before.PetState.HP, result.Value.State.PetState.HP);
    }

    [Fact]
    public async Task BattleWon_ShouldStillEvaluateEnrageFirst()
    {
        // BOSS_RULES.md §5 item 4 / TASK-022 §3.6: "the state transition is applied
        // whenever the HP condition holds, including when Player damage has just
        // reduced Boss HP to 0". Enrage is NOT skipped on death — only the Boss
        // Response is. So the Boss ends at 0 HP AND Enraged.
        var battle = await ArrangeAsync("victory-enrage-first", FragileBoss(maxHp: 1));

        var result = await battle.Service.ExecuteSwapAsync(battle.BattleId, battle.Pinned.Swap);

        Assert.True(result!.Value.IsAccepted);

        Assert.Equal(0, result.Value.State.BossState.HP);
        Assert.Equal(BossStateKind.Enraged, result.Value.State.BossState.State);
    }

    [Fact]
    public async Task BattleWon_ShouldBeTheOnlyOutcomeEvent()
    {
        // Both outcomes cannot occur on one action: the Boss check runs first and ends
        // the resolution, so a BattleLost can never accompany a BattleWon.
        var battle = await ArrangeAsync("victory-exclusive", FragileBoss(maxHp: 1));

        var result = await battle.Service.ExecuteSwapAsync(battle.BattleId, battle.Pinned.Swap);

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
        // GAME_RULES.md §1.4: the battle also ends when the Player reaches 0 HP — the
        // active Pet's HP (ADR-011 item 5). ADR-026 D1 lets the Boss reduce it only
        // through its own §17 step 18d ATK-gated instance, so the scenario pins the Pet
        // to 1 HP and runs rounds until that instance lands; the ATK value above makes
        // the instance lethal in one hit.
        var battle = await ArrangeAsync("defeat-player-dies", LethalBoss(), petHp: 1);

        var (result, _) = await RunUntilTheBossInstanceLandsAsync(battle);

        var lost = result.Events.Where(e => e.Type == BattleEventType.BattleLost).ToArray();

        Assert.Single(lost);
        Assert.Equal(0, result.State.PetState.HP);
    }

    [Fact]
    public async Task BattleLost_ShouldCarryTheTerminalHpValues()
    {
        // SIGNALR_PROTOCOL.md §3.2.19 item 2: both members are the terminal state
        // values. Here the player is at 0 and the Boss carries whatever the action left
        // — the boss survived, so it is above 0.
        var battle = await ArrangeAsync("defeat-hp-values", LethalBoss(), petHp: 1);

        var (result, _) = await RunUntilTheBossInstanceLandsAsync(battle);

        var lost = result.Events.Single(e => e.Type == BattleEventType.BattleLost).BattleLost;

        Assert.Equal(0, lost.FinalPlayerHp);
        Assert.Equal(result.State.PetState.HP, lost.FinalPlayerHp);

        Assert.Equal(result.State.BossState.HP, lost.FinalBossHp);
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
        var battle = await ArrangeAsync("defeat-ordering", LethalBoss(), petHp: 1);

        var (result, _) = await RunUntilTheBossInstanceLandsAsync(battle);

        var events = result.Events;

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

        // The instance the defeat followed is the §18d one, qualified by the Boss's own
        // ATK-Gem match (ADR-026 D1) — never a fixed-damage fallback attack (ADR-026 §5).
        Assert.True(RoundTestSupport.BossClearedAtkGem(result));
    }

    [Fact]
    public async Task BattleLost_ShouldBeTheOnlyOutcomeEvent()
    {
        // A surviving Boss means the Boss terminal check did not fire, so no BattleWon
        // can accompany the defeat.
        var battle = await ArrangeAsync("defeat-exclusive", LethalBoss(), petHp: 1);

        var (result, _) = await RunUntilTheBossInstanceLandsAsync(battle);

        Assert.Single(result.Events, e => e.Type == BattleEventType.BattleLost);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BattleWon);
    }

    [Fact]
    public async Task BattleLost_ShouldStillRunTheBossPassiveAndResponse()
    {
        // Only the Boss-death path short-circuits. When the Boss survives it runs its
        // full Response — its own shared-board action (§17 step 18a), its Passive
        // (18b), its Skill (18c) and its ATK-gated attack (18d) — and the defeat is the
        // consequence of that response, so the Boss's own events are present.
        var battle = await ArrangeAsync("defeat-full-response", LethalBoss(), petHp: 1);

        var (result, _) = await RunUntilTheBossInstanceLandsAsync(battle);

        // The Boss's damage instance is there — it is what caused the defeat — and so
        // is the board cycle that qualified it.
        Assert.Contains(
            result.Events,
            e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Boss);

        Assert.NotEmpty(RoundTestSupport.BossBoardEvents(result));

        // This fixture's Passive is inert — a `null` Threshold is the documented
        // "no match-charging threshold" marker (BOSS_RULES.md §6.2, DATABASE.md §1
        // note item 3) — so it emits no match-driven events.
        Assert.DoesNotContain(
            result.Events,
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
        // documented MVP stats neither does in one action — the Boss's 5000 HP against
        // the Pet's 50 ATK, and the Pet's 1000 HP against the Boss's 100 ATK + its own
        // ATK-Gem pool (ADR-026 D1) — so NEITHER outcome event is emitted and the
        // battle continues; the absence is the documented statement that the action
        // was not terminal (GAME_STATE.md §2.0.3).
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
        // The non-terminal round is the full documented loop: the Player's own
        // ATK-gated instance damages the Boss (steps 15–17), the Boss acts on the
        // shared board (18a), runs its Passive (18b) and Skill (18c) as their
        // conditions allow, damages the Pet with its own ATK-gated instance (18d), and
        // both sides are still alive — so no outcome event is emitted
        // (GAME_RULES.md §1.4, §17).
        //
        // D1 gates the Boss's attack on the BOSS's own ATK match, which no Player-side
        // fixture controls, so the scenario runs rounds — each pinned to a Player Swap
        // that clears an ATK Gem, and with the Pet re-pinned to full HP so no round can
        // be terminal — until the Boss's instance lands, and asserts the documented
        // loop in that round.
        var battle = await ArrangeAsync("outcome-none-full", BossDefinitions.HoaLong);

        SwapExecutionResult? observed = null;
        BattleState? observedBefore = null;
        var state = battle.Before;

        // The window is a search over real rounds for the one whose Boss board action
        // qualified (ADR-026 D1). Its length only bounds how long the search may run:
        // every assertion below is made on the qualifying round itself, so a wider
        // window finds it without weakening anything.
        for (var round = 0; round < 40 && observed is null; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                GemType.Atk,
                state.RngState,
                state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(
                battle.Store,
                state,
                pinned.Board,
                petState: state.PetState with { HP = state.PetState.MaxHP });

            var before = (await battle.Store.GetAsync(battle.BattleId))!;
            var result = (await battle.Service.ExecuteSwapAsync(battle.BattleId, pinned.Swap))!.Value;

            Assert.True(result.IsAccepted);

            // The Player's instance is a qualifying attack by construction: the pinned
            // board's cascade clears an ATK Gem (RoundTestSupport, ADR-026 D1).
            Assert.True(
                result.Events
                    .Single(e => e.Type == BattleEventType.DamageDealt
                        && e.DamageDealt.Source == DamageParty.Player)
                    .DamageDealt.Amount > 0);

            if (RoundTestSupport.BossClearedAtkGem(result))
            {
                observed = result;
                observedBefore = before;
                break;
            }

            // No round ran to a terminal HP: the Pet began at full health and the
            // Boss's own instance is far below it, and the Boss's 5000 HP is far above
            // one Player instance.
            Assert.True(result.State.BossState.HP > 0);
            Assert.True(result.State.PetState.HP > 0);

            state = result.State;
        }

        Assert.NotNull(observed);
        Assert.NotNull(observedBefore);

        var events = observed!.Value.Events;

        // The round's two instances — one per direction, each qualified by its own
        // side's ATK-Gem match (GAME_RULES.md §17, COMBAT_RULES.md §3.4).
        Assert.Contains(
            events,
            e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Player);

        Assert.Contains(
            events,
            e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Boss);

        // The Boss's own board cycle ran before its instance (§17 steps 18a, 18d).
        Assert.NotEmpty(RoundTestSupport.BossBoardEvents(observed.Value));

        // Both sides are alive, so NEITHER outcome event is emitted — the absence is
        // the documented statement that the round was not terminal (GAME_RULES.md
        // §1.4, GAME_STATE.md §2.0.3).
        Assert.True(observed.Value.State.BossState.HP > 0);
        Assert.True(observed.Value.State.PetState.HP > 0);
        Assert.DoesNotContain(events, e => e.Type == BattleEventType.BattleWon);
        Assert.DoesNotContain(events, e => e.Type == BattleEventType.BattleLost);

        // The Pet's whole HP change in the round is that one Boss instance: the pinned
        // board's cascade clears ATK Gems only, so this round generated no HP-Gem heal
        // for the Pet, and the legacy fallback basic attack is retired (ADR-026 §5).
        var bossDamage = events
            .Single(e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Boss)
            .DamageDealt.Amount;

        Assert.Equal(observedBefore!.PetState.HP - observed.Value.State.PetState.HP, bossDamage);
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
        var battle = await ArrangeAsync("outcome-boss-first", LethalBoss() with { MaxHP = 1 });

        var result = await battle.Service.ExecuteSwapAsync(battle.BattleId, battle.Pinned.Swap);

        Assert.True(result!.Value.IsAccepted);

        Assert.Single(result.Value.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BattleLost);

        // The player is untouched — the lethal Boss never acted, on the board or
        // otherwise.
        Assert.Empty(RoundTestSupport.BossBoardEvents(result.Value));
        Assert.Equal(battle.Before.PetState.HP, result.Value.State.PetState.HP);
        Assert.True(result.Value.State.PetState.HP > 0);
    }

    [Fact]
    public async Task Outcome_ShouldWriteExactlyOneStateIncludingTheTerminalHp()
    {
        // GAME_STATE.md §5.1: one action produces ONE post-resolution write-back. The
        // result the caller receives is the state the registry holds, and the terminal
        // HP the outcome event reports is the HP that state carries — an event is never
        // a substitute for the write-back (GAME_EVENTS.md §3 item 6).
        //
        // The victory is arranged by pinning a Swap that clears an ATK Gem against a
        // 1-HP Boss; the defeat by pinning the Pet to 1 HP and running rounds until the
        // Boss's own ATK-gated instance lands (ADR-026 D1).
        var won = await ArrangeAsync("outcome-writeback-won", FragileBoss(maxHp: 1));

        var wonResult = (await won.Service.ExecuteSwapAsync(won.BattleId, won.Pinned.Swap))!.Value;

        Assert.True(wonResult.IsAccepted);
        await AssertTerminalRoundWasWrittenBackAsync(
            won.Service,
            won.BattleId,
            wonResult,
            BattleEventType.BattleWon);

        var lost = await ArrangeAsync("outcome-writeback-lost", LethalBoss(), petHp: 1);

        var (lostResult, _) = await RunUntilTheBossInstanceLandsAsync(lost);

        await AssertTerminalRoundWasWrittenBackAsync(
            lost.Service,
            lost.BattleId,
            lostResult,
            BattleEventType.BattleLost);
    }

    [Fact]
    public async Task Outcome_ShouldBeReachableOnTheSameCommittedSwapContract()
    {
        // A terminal action is still one committed round: Turn and Sequence advance by
        // exactly 1 each, in the one write-back, so the outcome does not change the
        // counter contract (MATCH3_RULES.md §8.1–§8.2, ADR-026 D4).
        var battle = await ArrangeAsync("outcome-counters", FragileBoss(maxHp: 1));

        var result = await battle.Service.ExecuteSwapAsync(battle.BattleId, battle.Pinned.Swap);

        Assert.True(result!.Value.IsAccepted);

        // The round really is terminal, so "a terminal action" is what is asserted here
        // rather than an ordinary Swap (ADR-026 D1: the pinned ATK board is what makes
        // the Player's instance exist).
        Assert.Contains(result.Value.Events, e => e.Type == BattleEventType.BattleWon);

        Assert.Equal(battle.Before.Turn + 1, result.Value.State.Turn);
        Assert.Equal(battle.Before.Sequence + 1, result.Value.State.Sequence);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    /// <summary>
    /// One arranged terminal-round scenario: the battle session, the store double it
    /// runs over, the pinned Player Swap, and the authoritative pre-round record.
    /// </summary>
    private sealed record ArrangedBattle(
        string BattleId,
        BattleStateService Service,
        InMemoryBattleStateRepository Store,
        PinnedPlayerRound Pinned,
        BattleState Before);

    /// <summary>
    /// Creates a battle and pins its authoritative record for one scenario: the board
    /// (a <see cref="RoundTestSupport.PlayerMatch"/> fixture whose Swap clears exactly
    /// <paramref name="matchType"/> Gems), and — when the scenario states them — the
    /// Boss's and the Pet's HP.
    ///
    /// <b>Why the board is pinned.</b> <c>ADR-026</c> D1 gates each side's attack on
    /// that side's own confirmed ATK-Gem match, so a scenario that wants the Player's
    /// instance to exist must control which Gem type the committed Swap clears; a board
    /// drawn from the battle's own RNG cannot offer that. The fixture verifies its own
    /// preconditions against the real pipeline, so a Swap that stopped producing the
    /// promised Match fails loudly instead of silently weakening a test.
    ///
    /// The returned <see cref="ArrangedBattle.Before"/> is read back from the store, so
    /// it is the record the round will actually read and not a value the test assumed.
    /// </summary>
    private static async Task<ArrangedBattle> ArrangeAsync(
        string battleId,
        BossDefinition boss,
        GemType matchType = GemType.Atk,
        int? petHp = null,
        int? bossHp = null)
    {
        var store = new InMemoryBattleStateRepository();
        var service = new BattleStateService(store, new FixedRngSeedSource());

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var pinned = RoundTestSupport.PlayerMatch(matchType, created.RngState, created.LastCommittedSwapPair);

        await RoundTestSupport.PinAsync(
            store,
            created,
            pinned.Board,
            bossHp is { } bossHitPoints ? created.BossState with { HP = bossHitPoints } : null,
            petHp is { } petHitPoints ? created.PetState with { HP = petHitPoints } : null);

        var before = (await store.GetAsync(battleId))!;

        return new ArrangedBattle(battleId, service, store, pinned, before);
    }

    /// <summary>
    /// Commits rounds — each pinned to a Player Swap that clears a non-ATK Gem, with
    /// the Pet held at 1 HP — until the Boss's own <c>GAME_RULES.md</c> §17 step 18d
    /// ATK-gated instance lands and ends the battle (<c>ADR-026</c> D1).
    ///
    /// <b>Why rounds rather than one round.</b> The Boss selects its swap
    /// deterministically, but from the board the Player's cascade left
    /// (<c>ADR-026</c> §8), which no Player-side fixture controls — so "the Boss kills
    /// the Pet" is not forceable in a single round. The scenario therefore asserts the
    /// documented equivalence (a <c>BattleLost</c> round is a round in which the Boss's
    /// own swap cleared an ATK Gem) rather than a fixed instance. It is deterministic
    /// for the battle's fixed seed, and a scenario that could not reach the instance is
    /// a failure, not a skip.
    /// </summary>
    private static async Task<(SwapExecutionResult Result, BattleState Before)> RunUntilTheBossInstanceLandsAsync(
        ArrangedBattle battle,
        int maxRounds = 40)
    {
        var state = battle.Before with { PetState = battle.Before.PetState with { HP = 1 } };

        for (var round = 0; round < maxRounds; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                round % 2 == 0 ? GemType.Def : GemType.Hp,
                state.RngState,
                state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(
                battle.Store,
                state,
                pinned.Board,
                petState: state.PetState with { HP = 1 });

            var before = (await battle.Store.GetAsync(battle.BattleId))!;
            var result = (await battle.Service.ExecuteSwapAsync(battle.BattleId, pinned.Swap))!.Value;

            Assert.True(result.IsAccepted);

            if (result.Events.Any(e => e.Type == BattleEventType.BattleLost))
            {
                // GAME_RULES.md §17 step 18d / ADR-026 D1 and §5: the Pet can only have
                // been reduced to 0 by the Boss's own confirmed ATK-Gem match — the
                // fallback basic attack is retired.
                Assert.True(
                    RoundTestSupport.BossClearedAtkGem(result),
                    "the BattleLost round must be the round the Boss's ATK-gated instance landed in");

                return (result, before);
            }

            state = result.State;
        }

        throw new InvalidOperationException(
            $"ADR-026 D1: the Boss's ATK-gated instance must be able to defeat a 1-HP Pet "
            + $"within {maxRounds} rounds.");
    }

    /// <summary>
    /// The terminal payload contract of <c>SIGNALR_PROTOCOL.md</c> §3.2.19 item 2 and
    /// the one-write-back rule of <c>GAME_STATE.md</c> §5.1, asserted for one committed
    /// terminal round: the state the caller receives is the state the store holds, and
    /// the outcome event's two HP values are that state's own values.
    /// </summary>
    private static async Task AssertTerminalRoundWasWrittenBackAsync(
        BattleStateService service,
        string battleId,
        SwapExecutionResult result,
        BattleEventType outcomeType)
    {
        // The store's re-read is a separate record, so the equality asserted is the
        // value equality of the written-back state (BattleState is a record,
        // GAME_STATE.md §5.1's one write-back is what "the same state" means) — never
        // object identity.
        var stored = (await service.GetBattleAsync(battleId))!;

        Assert.Equal(result.State, stored);
        Assert.Equal(stored.BossState.HP, result.State.BossState.HP);
        Assert.Equal(stored.PetState.HP, result.State.PetState.HP);

        var outcome = result.Events.Single(e => e.Type == outcomeType);

        var (finalBossHp, finalPlayerHp) = outcomeType == BattleEventType.BattleWon
            ? (outcome.BattleWon.FinalBossHp, outcome.BattleWon.FinalPlayerHp)
            : (outcome.BattleLost.FinalBossHp, outcome.BattleLost.FinalPlayerHp);

        Assert.Equal(stored.BossState.HP, finalBossHp);
        Assert.Equal(stored.PetState.HP, finalPlayerHp);
    }

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
