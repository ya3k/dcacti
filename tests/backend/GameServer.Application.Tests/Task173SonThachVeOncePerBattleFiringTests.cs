using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// The once-per-battle firing semantics of Sơn Thạch Vệ's Boss Passive,
/// <c>son-thach-ve-enrage</c> (<c>BOSS_RULES.md</c> §6.2.4;
/// <c>PASSIVE_RULES.md</c> §4's Boss Passive clause).
///
/// <code>
/// Rule (BOSS_RULES.md §6.2.4 / PASSIVE_RULES.md §4)
///  ↓
/// Scenario (Given the documented Boss at or below 50% HP, When the Boss Response
///           stage runs across Turns, Then the documented state)
///  ↓
/// Test
/// </code>
///
/// <b>What these tests prove beyond the TASK-172 suite.</b>
/// The <c>TASK-172</c> runtime suite proves the trigger, the magnitude, the
/// duration, and the representation while the applied instance is still alive. It
/// does not survive the instance: the defect this contract closes is that a Turn
/// <i>after</i> the 3-Turn effect expired re-applied the Rage, because the guard
/// read the temporary instance's presence. These tests therefore assert the
/// observables that outlive the effect — how many activations a battle produced,
/// and the absence of any ATK modifier on the Turns that follow expiry — with the
/// trigger condition held true on every one of those Turns.
///
/// <b>Every expected value traces to a section, never to the implementation.</b>
/// The Boss configuration is read from <see cref="BossDefinitions"/>, which
/// transcribes <c>BOSS_RULES.md</c> §6, and each Turn is driven through the real
/// <see cref="BattleStateService"/> pipeline rather than by calling an
/// effect-application helper.
///
/// <b>The only seeded values are documented state values.</b> A battle's Boss is
/// placed at the documented <c>≤ 50%</c> boundary (<c>HP = 1500</c> of
/// <c>MaxHP = 3000</c>, <c>BOSS_RULES.md</c> §6.1) before the Turns that must
/// evaluate the trigger, so the condition is held true without encoding a damage
/// value and without the battle ending mid-scenario. The Passive's effect, its
/// firing eligibility, and every other value asserted below are produced by the
/// pipeline; the effect under test is never written by a test.
/// </summary>
public class Task173SonThachVeOncePerBattleFiringTests
{
    /// <summary>
    /// The Pet these battles carry — the owned instance identity
    /// (<c>GAME_STATE.md</c> §2.3), Xích Lang's MVP Element and Passive
    /// (<c>ELEMENT_RULES.md</c> §6, <c>PASSIVE_RULES.md</c> §8).
    /// </summary>
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    private static readonly PlayerId Owner = new("player_task173_once_per_battle_owner");

    /// <summary>
    /// Sơn Thạch Vệ's canonical PassiveId (<c>BOSS_RULES.md</c> §6.4), recorded
    /// verbatim and used as the applied instance's <c>Id</c>.
    /// </summary>
    private const string SonThachVePassiveId = "son-thach-ve-enrage";

    /// <summary>
    /// The §6.2.4 trigger boundary: 50% of the §6.1 <c>MaxHP = 3000</c>.
    /// </summary>
    private const int HalfMaxHp = 1500;

    // =======================================================================
    // First activation — BOSS_RULES.md §6.2.4
    // =======================================================================

    [Fact]
    public async Task FirstActivation_WhenHpIsExactlyHalfMaxHp_ShouldApplyTheRageInstance()
    {
        // §6.2.4's boundary operator is `≤`, so `HP == 1500` — exactly 50% of the
        // documented MaxHP 3000 — DOES satisfy the trigger. This Turn therefore
        // proves both the inclusive comparison and the activation itself against
        // the post-damage state (§3.3 item 1), and it is the control the
        // post-expiry tests are read against: the same conditions that produce an
        // activation here must produce none once the eligibility is consumed.
        var (service, repository) = NewHarness();
        var battleId = "stv-t173-exact-half";

        var armed = await ArmBossHpAsync(service, repository, battleId, HalfMaxHp);

        // §6.1: MaxHP 3000, so the boundary this Turn sits on is 1500.
        Assert.Equal(3000, armed.BossState.MaxHP);
        Assert.Equal(HalfMaxHp, armed.BossState.MaxHP * 50 / 100);

        var result = await service.ExecuteSwapAsync(battleId, FindMatchProducingPair(armed));

        Assert.True(result!.Value.IsAccepted);

        var boss = result.Value.State.BossState;

        // The trigger saw the POST-damage HP (§3.3 item 1), which is ≤ 1500.
        Assert.True(
            boss.HP <= HalfMaxHp,
            $"expected post-damage HP ≤ {HalfMaxHp} but was {boss.HP}");

        // §6.2.4: the existing Boss ATK modifier representation, unchanged.
        var rage = Assert.Single(
            boss.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal));

        Assert.Equal(StatusEffectType.BuffDebuff, rage.Type);
        Assert.Equal(StatusEffectSource.Boss, rage.Source);
        Assert.Equal("ATK", rage.TargetStat);
        Assert.Equal(20, rage.Magnitude);

        // Applied at step 18a then consumed once at step 19a (COMBAT_RULES.md §5.3),
        // so a 3-Turn instance reads back as 2 remaining in the settled state.
        Assert.Equal(2, rage.RemainingTurns);

        // §5.5.4: the base stat is never overwritten.
        Assert.Equal(120, boss.ATK);
    }

    [Fact]
    public async Task FirstActivation_WhenHpIsBelowHalfMaxHp_ShouldApplyTheRageInstance()
    {
        // The other side of `≤`: strictly below 50% also satisfies the trigger. At
        // 1400 the Enrage transition (`BossHP < 1500`, §5 item 4) is true as well,
        // which is exactly the case §6.2.4 states must not be conflated with this
        // Passive — both hold, and the Passive activates independently of `State`.
        var (service, repository) = NewHarness();
        var battleId = "stv-t173-below-half";

        var armed = await ArmBossHpAsync(service, repository, battleId, hp: 1400);

        var result = await service.ExecuteSwapAsync(battleId, FindMatchProducingPair(armed));

        Assert.True(result!.Value.IsAccepted);

        var boss = result.Value.State.BossState;

        Assert.True(boss.HP <= HalfMaxHp, $"expected post-damage HP ≤ {HalfMaxHp} but was {boss.HP}");
        Assert.Equal(BossStateKind.Enraged, boss.State);

        var rage = Assert.Single(
            boss.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal));

        Assert.Equal(20, rage.Magnitude);
        Assert.Equal(2, rage.RemainingTurns);
    }

    // =======================================================================
    // No re-fire — BOSS_RULES.md §6.2.4 / PASSIVE_RULES.md §4
    // =======================================================================

    [Fact]
    public async Task WhileTheRageInstanceIsActive_ShouldNotApplyASecondInstance()
    {
        // §6.2.4: the Passive does not re-trigger once it has activated, so a second
        // Turn that still satisfies `HP ≤ 50%` neither creates a second instance nor
        // refreshes the first. The observables are the activation count and the
        // duration: a re-application would count as a second activation and would
        // reset RemainingTurns back to 2, and a stack would show a summed magnitude.
        var (service, repository) = NewHarness();
        var battleId = "stv-t173-while-active";

        await ArmBossHpAsync(service, repository, battleId, HalfMaxHp);

        var turns = await RunTurnsTakingStateAsync(service, repository, battleId, HalfMaxHp, HalfMaxHp);

        Assert.Equal(2, Assert.Single(
            turns[0].SettledBoss.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal)).RemainingTurns);

        Assert.True(turns[1].SettledBoss.HP <= HalfMaxHp, "the Boss must still satisfy the trigger on Turn 2");

        var second = Assert.Single(
            turns[1].SettledBoss.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal));

        // The refresh a re-trigger would perform did NOT happen: the duration
        // monotonically decremented, and the magnitude is the single +20%.
        Assert.Equal(1, second.RemainingTurns);
        Assert.Equal(20, second.Magnitude);
        Assert.Equal("ATK", second.TargetStat);
        Assert.Equal(StatusEffectType.BuffDebuff, second.Type);

        // Still exactly one activation for the battle.
        Assert.Equal(1, turns[1].ActivationsSoFar);
    }

    [Fact]
    public async Task AfterTheEffectExpires_ShouldNotFireAgainWhileHpIsAtOrBelowHalf()
    {
        // THE regression this contract exists for (BOSS_RULES.md §6.2.4 /
        // PASSIVE_RULES.md §4's Boss Passive clause).
        //
        // Turn 1: HP drops to the boundary → the Passive activates.
        // Turn 2: still active, duration 1.
        // Turn 3: the 3-Turn instance expires and is removed at step 19a.
        // Turn 4: HP is again at or below 50% and the applied instance is GONE —
        //         firing eligibility must therefore still be consumed, and no second
        //         activation may occur.
        //
        // Under the presence-based guard this replaces, Turn 4 re-applied the Rage
        // (RemainingTurns 2), and Turn 7 re-applied it again.
        var (service, repository) = NewHarness();
        var battleId = "stv-t173-after-expiry";

        await ArmBossHpAsync(service, repository, battleId, HalfMaxHp);

        var turns = await RunTurnsTakingStateAsync(
            service,
            repository,
            battleId,
            HalfMaxHp,
            HalfMaxHp,
            HalfMaxHp,
            HalfMaxHp);

        // Turn 1 — the single activation.
        Assert.Equal(2, Assert.Single(
            turns[0].SettledBoss.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal)).RemainingTurns);

        // Turn 2 — decrementing, never refreshed.
        Assert.Equal(1, Assert.Single(
            turns[1].SettledBoss.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal)).RemainingTurns);

        // Turn 3 — expiry removes the instance (GAME_STATE.md §5.1.1 items 4–5).
        Assert.False(HoldsRage(turns[2].SettledBoss), "the 3-Turn instance must have expired on Turn 3");

        // Turn 4 — the trigger condition holds and the instance is absent, yet the
        // Passive must not fire: its eligibility was consumed and expiry does not
        // restore it.
        Assert.True(
            turns[3].SettledBoss.HP <= HalfMaxHp,
            $"the trigger condition must still hold on Turn 4 but HP was {turns[3].SettledBoss.HP}");

        Assert.False(
            HoldsRage(turns[3].SettledBoss),
            "the expended firing eligibility must not be restored by the effect's expiry");

        // The gameplay consequence: the settled state carries no ATK modifier, so the
        // Boss's Step-1 Attack input is the unmodified base stat (COMBAT_RULES.md
        // §5.5.1).
        Assert.Equal(
            120,
            StatusEffectLifecycle.EffectiveBossAttack(
                turns[3].SettledBoss.ATK,
                turns[3].SettledBoss.ActiveStatusEffects));

        // Exactly one activation across the whole battle.
        Assert.Equal(1, turns[3].ActivationsSoFar);
    }

    [Fact]
    public async Task WhileHpRemainsAtOrBelowHalf_ShouldFireExactlyOnceAcrossManyTurns()
    {
        // §6.2.4's sustained case: the Boss stays at or below 50% HP for many Turns.
        // Firing eligibility is consumed by the first activation and is never
        // re-armed, so the battle produces exactly one activation and no later Turn
        // — including every Turn after Turn 3's expiry — applies the Rage instance.
        var (service, repository) = NewHarness();
        var battleId = "stv-t173-sustained";

        await ArmBossHpAsync(service, repository, battleId, HalfMaxHp);

        var turns = await RunTurnsTakingStateAsync(
            service,
            repository,
            battleId,
            HalfMaxHp,
            HalfMaxHp,
            HalfMaxHp,
            HalfMaxHp,
            HalfMaxHp,
            HalfMaxHp,
            HalfMaxHp);

        Assert.True(HoldsRage(turns[0].SettledBoss), "the first eligible Turn must activate the Passive");

        // Turns 1–2 hold the one instance as its duration counts down (2, then 1).
        Assert.Equal(1, Assert.Single(
            turns[1].SettledBoss.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal)).RemainingTurns);

        // From Turn 3 — the Turn after the 3-Turn effect expires — no later Turn may
        // apply it again, however long the Boss stays at or below 50% HP.
        foreach (var later in turns.Skip(2))
        {
            Assert.False(
                HoldsRage(later.SettledBoss),
                $"a Turn after the effect's expiry must not apply the Rage instance (Turn HP {later.SettledBoss.HP})");
        }

        Assert.Equal(1, turns[^1].ActivationsSoFar);

        // The last Turn still satisfied the trigger, which is what makes its absence
        // meaningful rather than an unmet condition.
        Assert.True(turns[^1].SettledBoss.HP <= HalfMaxHp);
    }

    [Fact]
    public async Task AfterLeavingAndReEnteringTheThreshold_ShouldNotFireAgain()
    {
        // §6.2.4 / PASSIVE_RULES.md §4: the limit is ONCE PER BATTLE, not once per
        // threshold crossing. A Boss that rises above 50% and later returns to or
        // below it is therefore not eligible again — the earlier crossing consumed
        // the eligibility, and leaving the threshold does not restore it.
        var (service, repository) = NewHarness();
        var battleId = "stv-t173-recrossing";

        await ArmBossHpAsync(service, repository, battleId, HalfMaxHp);

        // Turns 1–3 take the first activation to its expiry; Turn 4 starts above the
        // threshold and Turn 5 returns to it.
        var turns = await RunTurnsTakingStateAsync(
            service,
            repository,
            battleId,
            HalfMaxHp,
            HalfMaxHp,
            HalfMaxHp,
            2600,
            HalfMaxHp);

        Assert.False(
            HoldsRage(turns[2].SettledBoss),
            "the first activation's instance must have expired before the crossing");

        // Turn 4 leaves the threshold: it starts above 50% HP.
        Assert.True(
            turns[3].PreTurnBoss.HP > HalfMaxHp,
            $"Turn 4 must start above the threshold but started at {turns[3].PreTurnBoss.HP}");

        Assert.False(HoldsRage(turns[3].SettledBoss));
        Assert.Equal(1, turns[3].ActivationsSoFar);

        // Turn 5 re-enters the threshold. The Passive must NOT fire again.
        Assert.True(turns[4].SettledBoss.HP <= HalfMaxHp);
        Assert.False(
            HoldsRage(turns[4].SettledBoss),
            "re-entering the threshold must not restore the consumed firing eligibility");
        Assert.Equal(1, turns[4].ActivationsSoFar);
    }

    [Fact]
    public async Task RefusedWriteAndRetry_ShouldStillActivateExactlyOnce()
    {
        // REDIS_STATE.md §4 item 6: a refused Sequence compare-and-set aborts the
        // resolution and retries it against fresh state, and the retried resolution
        // "re-runs the same deterministic computation and produces the same result".
        // The firing eligibility must therefore not be spent by an attempt whose
        // write was refused — otherwise the retried resolution would observe a
        // consumed eligibility whose activation was never committed, and the
        // Passive's +20% ATK would be silently lost for the battle.
        //
        // The double's ForcedConflicts drives exactly that documented path
        // deterministically: the first write is refused, the retry resolves against
        // the same (unchanged) state, and the committed state must carry the Rage.
        var (service, repository) = NewHarness();
        var battleId = "stv-t173-refused-write";

        await ArmBossHpAsync(service, repository, battleId, HalfMaxHp);

        repository.ForcedConflicts = 1;

        var first = await service.ExecuteSwapAsync(battleId, FindMatchProducingPair(
            (await repository.GetAsync(battleId))!));

        Assert.True(first!.Value.IsAccepted);

        var rage = Assert.Single(
            first.Value.State.BossState.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal));

        Assert.Equal(20, rage.Magnitude);
        Assert.Equal(2, rage.RemainingTurns);

        // And the committed activation is the battle's only one: the eligibility was
        // consumed by the retry that committed it, so the Turns that follow — including
        // the Turn after the effect's expiry — activate nothing further.
        var later = await RunTurnsTakingStateAsync(
            service,
            repository,
            battleId,
            HalfMaxHp,
            HalfMaxHp,
            HalfMaxHp);

        Assert.Equal(0, later[^1].ActivationsSoFar);
        Assert.False(HoldsRage(later[^1].SettledBoss));
    }

    [Fact]
    public async Task NewBattle_ShouldStartWithFreshFiringEligibility()
    {
        // §6.2.4 / PASSIVE_RULES.md §4: a new battle creates fresh firing
        // eligibility, so the consumption is scoped to the battle session and is
        // never carried across battles. Both battles run on the SAME service
        // instance, so this also proves the scope is the battle and not the service.
        var (service, repository) = NewHarness();

        var firstBattleId = "stv-t173-battle-a";
        await ArmBossHpAsync(service, repository, firstBattleId, HalfMaxHp);

        var firstBattle = await RunTurnsTakingStateAsync(
            service,
            repository,
            firstBattleId,
            HalfMaxHp,
            HalfMaxHp,
            HalfMaxHp,
            HalfMaxHp);

        Assert.Equal(1, firstBattle[^1].ActivationsSoFar);

        // Battle B: a new battle id, the same Boss, the same boundary. The Passive
        // must be eligible again — the consumed record belongs to Battle A.
        var secondBattleId = "stv-t173-battle-b";
        await ArmBossHpAsync(service, repository, secondBattleId, HalfMaxHp);

        var secondBattle = await RunTurnsTakingStateAsync(
            service,
            repository,
            secondBattleId,
            HalfMaxHp);

        var rage = Assert.Single(
            secondBattle[0].SettledBoss.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal));

        Assert.Equal(20, rage.Magnitude);
        Assert.Equal("ATK", rage.TargetStat);
        Assert.Equal(2, rage.RemainingTurns);
        Assert.Equal(1, secondBattle[0].ActivationsSoFar);

        // Battle A's own settlement is unaffected by Battle B's activation.
        Assert.False(HoldsRage(firstBattle[^1].SettledBoss));
    }

    // =======================================================================
    // Shared harness
    // =======================================================================

    /// <summary>
    /// A Turn's Boss state: the state just before the Swap (the seeded position),
    /// the settled state the resolution produced, and how many activations of the
    /// Passive the battle has produced up to and including this Turn.
    /// </summary>
    /// <param name="PreTurnBoss">The Boss state the Turn resolved against.</param>
    /// <param name="SettledBoss">The Boss state the Turn settled at.</param>
    /// <param name="ActivationsSoFar">
    /// The running activation count. An activation is counted when a Turn's settled
    /// state newly holds the Rage instance — the instance is the observable a firing
    /// produces (<c>BOSS_RULES.md</c> §6.2.4), and counting the <i>appearance</i>
    /// rather than the <i>presence</i> is what distinguishes one activation whose
    /// effect spans three Turns from three activations.
    /// </param>
    private readonly record struct TurnObservation(
        BossState PreTurnBoss,
        BossState SettledBoss,
        int ActivationsSoFar);

    /// <summary>
    /// A fresh service over a fresh in-memory repository — the same harness the
    /// existing MVP Boss Passive suites use.
    /// </summary>
    private static (BattleStateService Service, InMemoryBattleStateRepository Repository) NewHarness()
    {
        var repository = new InMemoryBattleStateRepository();

        return (new BattleStateService(repository, new FixedRngSeedSource()), repository);
    }

    /// <summary>
    /// Creates a Sơn Thạch Vệ battle and places the Boss's HP at
    /// <paramref name="hp"/> — the documented §6.1 configuration with the documented
    /// boundary value written onto its own state. Only documented state values are
    /// written; the Passive's effect and its firing eligibility are not.
    /// </summary>
    private static async Task<BattleState> ArmBossHpAsync(
        BattleStateService service,
        InMemoryBattleStateRepository repository,
        string battleId,
        int hp)
    {
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.SonThachVe);

        Assert.Equal(3000, created.BossState.MaxHP);
        Assert.Equal(120, created.BossState.ATK);
        Assert.Empty(created.BossState.ActiveStatusEffects);

        return await PinBossHpAsync(repository, created, hp);
    }

    /// <summary>
    /// Writes <paramref name="hp"/> onto the stored battle's Boss HP without
    /// advancing <c>Sequence</c>, so the next resolution reads it as the
    /// authoritative state.
    /// </summary>
    private static async Task<BattleState> PinBossHpAsync(
        InMemoryBattleStateRepository repository,
        BattleState state,
        int hp)
    {
        var pinned = state with { BossState = state.BossState with { HP = hp } };

        Assert.True(
            await repository.TryUpdateAsync(pinned, pinned.Sequence),
            "the seeded Boss HP must be written to the authoritative record");

        return pinned;
    }

    /// <summary>
    /// Runs one committed Swap per entry of <paramref name="hpPerTurn"/>, pinning the
    /// Boss's HP to that entry before the Turn, and returns each Turn's observation.
    /// </summary>
    private static async Task<List<TurnObservation>> RunTurnsTakingStateAsync(
        BattleStateService service,
        InMemoryBattleStateRepository repository,
        string battleId,
        params int[] hpPerTurn)
    {
        var observations = new List<TurnObservation>(hpPerTurn.Length);
        var activations = 0;

        for (var turn = 1; turn <= hpPerTurn.Length; turn++)
        {
            var current = await repository.GetAsync(battleId);

            Assert.NotNull(current);

            var pinned = await PinBossHpAsync(repository, current!, hpPerTurn[turn - 1]);

            var result = await service.ExecuteSwapAsync(battleId, FindMatchProducingPair(pinned));

            Assert.True(result!.Value.IsAccepted, $"Turn {turn} was rejected");

            var settled = result.Value.State.BossState;

            // A firing is observable as the instance APPEARING: the settled state
            // holds it and the state the Turn resolved against did not. A Turn whose
            // effect is merely still counting down therefore adds nothing, and a Turn
            // that re-applied the Rage after an expiry is counted.
            if (HoldsRage(settled) && !HoldsRage(pinned.BossState))
            {
                activations++;
            }

            observations.Add(new TurnObservation(pinned.BossState, settled, activations));
        }

        return observations;
    }

    /// <summary>
    /// Whether a Boss state holds Sơn Thạch Vệ's Rage instance — the observable a
    /// firing produced (<c>BOSS_RULES.md</c> §6.2.4's representation).
    /// </summary>
    private static bool HoldsRage(BossState boss) =>
        boss.ActiveStatusEffects.Any(
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal));

    /// <summary>
    /// A match-producing adjacent pair on <paramref name="state"/>'s board that is
    /// not the pair already recorded as committed (<c>MATCH3_RULES.md</c> §2.1.4).
    /// </summary>
    private static SwapRequest FindMatchProducingPair(BattleState state)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (state.LastCommittedSwapPair is { } pair
                && CommittedSwapPair.FromCells(from, to)
                    == CommittedSwapPair.FromCells(pair.MinCellIndex, pair.MaxCellIndex))
            {
                continue;
            }

            if (MatchDetector.Detect(state.BoardState.WithSwapped(from, to)).Count > 0)
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
