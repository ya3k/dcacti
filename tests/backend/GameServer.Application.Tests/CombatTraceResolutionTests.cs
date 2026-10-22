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
/// TASK-321 regression suite for the authoritative combat trace
/// (<c>SIGNALR_PROTOCOL.md</c> §3 item 5, §3.3).
///
/// <code>
/// Rule (COMBAT_RULES.md §3, §4; GAME_RULES.md §17; SIGNALR_PROTOCOL.md §3.3)
///  ↓
/// Scenario (a pinned round the engine really resolves)
///  ↓
/// Test (the trace's values are the resolution's own)
/// </code>
///
/// <b>What these tests pin.</b> That the trace reports individual damage
/// instances with their targets and the base/effective DEF step 5 actually read,
/// that shield absorption is never reported as HP loss, that a full-HP heal is
/// reported with zero applied amount, that a zero-DEF target is an explicit
/// zero, that a lethal round still carries a trace, and that the trace's Pet/Boss
/// snapshots are the committed states. Every expectation is read from the round's
/// own authoritative state and events, so a trace that invents or re-derives a
/// value fails the equality.
/// </summary>
public class CombatTraceResolutionTests
{
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_combat_trace"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    private static readonly PlayerId Owner = new("player_combat_trace_owner");

    private const int PetMaxHp = 1000;
    private const int PetPinnedHp = 500;
    private const int BossPinnedHp = 3000;
    private const string BattleId = "battle-combat-trace";

    // =======================================================================
    // Correlation and the player cycle
    // =======================================================================

    [Fact]
    public async Task CombatTrace_ShouldCorrelateWithTheCommittedSequenceAndCarryThePlayerCycle()
    {
        var (repository, service, created) = await NewBattleAsync();
        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState);

        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { HP = BossPinnedHp },
            petState: created.PetState with { HP = PetPinnedHp, MaxHP = PetMaxHp });

        var result = await service.ExecuteSwapAsync(BattleId, pinned.Swap);
        Assert.NotNull(result);
        var round = result!.Value;

        var trace = round.CombatTrace;
        Assert.NotNull(trace);
        var value = trace!.Value;

        // §3.3.2 item 2: the trace repeats the envelope correlation values, read from
        // the committed state — not computed by the trace.
        Assert.Equal(BattleId, value.BattleId);
        Assert.Equal(round.State.Sequence, value.ServerSequence);

        // The move identity is the committed Swap's own two cells.
        Assert.Equal(CombatTraceMoveKind.Swap, value.Move.Kind);
        Assert.Equal(pinned.Swap.From, value.Move.FromCell);
        Assert.Equal(pinned.Swap.To, value.Move.ToCell);

        // The player cycle is the first cycle and names its own actor — a consumer
        // never has to infer that from a position.
        var cycle = value.Cycles[0];
        Assert.Equal(1, cycle.Ordinal);
        Assert.Equal(DamageParty.Player, cycle.Actor);
        Assert.Equal(CombatTraceCycleAction.Board, cycle.Action);
        Assert.Equal(pinned.Swap.From, cycle.FromCell);
        Assert.Equal(pinned.Swap.To, cycle.ToCell);
        Assert.Null(cycle.CardId);

        // Its cleared-Gem counts are the resource stage's own accounting, in the
        // fixed ATK/DEF/HP/POWER order.
        Assert.Equal(4, cycle.MatchedGems.Count);
        Assert.Equal(GemType.Atk, cycle.MatchedGems[0].GemType);
        Assert.Equal(round.Resources.ClearedGemCounts.Atk, cycle.MatchedGems[0].Count);
        Assert.Equal(round.Resources.ClearedGemCounts.Def, cycle.MatchedGems[1].Count);
        Assert.Equal(round.Resources.ClearedGemCounts.Hp, cycle.MatchedGems[2].Count);
        Assert.Equal(round.Resources.ClearedGemCounts.Power, cycle.MatchedGems[3].Count);
        Assert.True(cycle.MatchedGems[0].Count > 0, "the ATK fixture must clear at least one ATK Gem");

        // Its cascade entries are the resolution's own passes, in depth order.
        Assert.Equal(round.Resolution.Passes.Count, cycle.Cascades.Count);
        for (var index = 0; index < cycle.Cascades.Count; index++)
        {
            Assert.Equal(index + 1, cycle.Cascades[index].Depth);
        }

        // The Pet/Boss snapshots are the committed before/after states.
        var pet = value.Pet;
        Assert.Equal(PetPinnedHp, pet.HpBefore);
        Assert.Equal(round.State.PetState.HP, pet.HpAfter);
        Assert.Equal(created.PetState.DEF, pet.DefBase);
        Assert.Equal(created.PetState.Power, pet.PowerBefore);
        Assert.Equal(round.State.PetState.Power, pet.PowerAfter);

        var boss = value.Boss;
        Assert.Equal(BossPinnedHp, boss.HpBefore);
        Assert.Equal(round.State.BossState.HP, boss.HpAfter);
        Assert.Equal(created.BossState.DEF, boss.DefBase);
        Assert.Equal(created.BossState.Power, boss.PowerBefore);
        Assert.Equal(round.State.BossState.Power, boss.PowerAfter);
    }

    [Fact]
    public async Task CombatTrace_ShouldKeepMultiplePlayerPassesInsideThePlayerCycle()
    {
        // MATCH3_RULES.md §4.2: a multi-pass cycle is one actor's cascades, not a
        // second actor's. §3.3.2 item 2 forbids attributing a cascade by position.
        var (repository, service, created) = await NewBattleAsync();
        var (stream, pinned) = RoundTestSupport.PlayerCascadeMatch(GemType.Atk, created.RngState);

        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { HP = BossPinnedHp },
            petState: created.PetState with { HP = PetPinnedHp, MaxHP = PetMaxHp },
            rngState: stream);

        var result = await service.ExecuteSwapAsync(BattleId, pinned.Swap);
        Assert.NotNull(result);
        var round = result!.Value;

        var trace = round.CombatTrace!.Value;
        var playerCycles = trace.Cycles.Where(c => c.Actor == DamageParty.Player).ToArray();

        // Exactly one player cycle owns every player pass.
        Assert.Single(playerCycles);
        Assert.True(playerCycles[0].Cascades.Count >= 2, "the fixture must resolve in at least two passes");
        Assert.Equal(round.Resolution.Passes.Count, playerCycles[0].Cascades.Count);

        // No cascade is attributed to the Boss by the mistake of counting them as
        // separate cycles: a boss cycle exists only when the Boss acted, and its own
        // pass count is the Boss resolution's.
        var bossCycles = trace.Cycles.Where(c => c.Actor == DamageParty.Boss).ToArray();
        Assert.All(bossCycles, c => Assert.Equal(CombatTraceCycleAction.Board, c.Action));
        Assert.All(trace.Cycles, c => Assert.NotEqual(0, c.Ordinal));
        Assert.Equal(trace.Cycles.Count, trace.Cycles.Select(c => c.Ordinal).Distinct().Count());
    }

    // =======================================================================
    // Damage instances
    // =======================================================================

    [Fact]
    public async Task CombatTrace_PlayerAttack_ShouldCarryTheAuthoritativeBreakdownAndDefenses()
    {
        var (repository, service, created) = await NewBattleAsync();
        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState);

        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { HP = BossPinnedHp },
            petState: created.PetState with { HP = PetPinnedHp, MaxHP = PetMaxHp });

        var result = await service.ExecuteSwapAsync(BattleId, pinned.Swap);
        Assert.NotNull(result);
        var round = result!.Value;

        var trace = round.CombatTrace!.Value;
        var attack = Assert.Single(
            trace.Damage.Where(d => d.Source == DamageParty.Player && d.Target == DamageParty.Boss));

        // The breakdown is the pipeline's DamageCalculated event for the same
        // instance — the trace adds no second calculation and no re-derivation.
        var calculated = round.Events
            .Single(e => e.Type == BattleEventType.DamageCalculated)
            .DamageCalculated;

        Assert.Equal(calculated.Base, attack.Base);
        Assert.Equal(calculated.ComboModifier, attack.ComboModifier);
        Assert.Equal(calculated.ElementModifier, attack.ElementModifier);
        Assert.Equal(calculated.OtherModifiers, attack.OtherModifiers);
        Assert.Equal(calculated.Defense, attack.MitigatedDamage);
        Assert.Equal(calculated.FinalDamage, attack.FinalDamage);

        // §3.3.1: the base DEF is the defender's stored stat; the effective DEF is
        // the value step 5 actually read. Hoa Long stores DEF 50 with no DEF buff on
        // a first round, so both are the stored 50 — never a client-side constant.
        Assert.Equal(created.BossState.DEF, attack.DefenderDefenseBase);
        Assert.Equal(attack.DefenderDefenseBase, attack.DefenderDefenseEffective);

        // The HP pair is the instance's own, and HP loss is what actually reduced HP.
        Assert.Equal(BossPinnedHp, attack.HpBefore);
        Assert.Equal(attack.HpBefore - attack.HpLoss, attack.HpAfter);
        Assert.Equal(attack.FinalDamage - attack.ShieldAbsorbed, attack.HpLoss);
        Assert.Equal(1, attack.Ordinal);
        Assert.Equal(CombatTraceDamagePhase.Attack, attack.Phase);
        Assert.Equal(1, attack.CycleOrdinal);
    }

    [Fact]
    public async Task CombatTrace_ZeroDefBoss_ShouldCarryAnExplicitZeroBaseAndEffectiveDefense()
    {
        // BOSS_RULES.md §6.4: Sơn Thạch Vệ stores DEF 0. A zero is a real value —
        // it must be delivered as 0, never replaced by a default.
        var (repository, service, created) = await NewBattleAsync(BossDefinitions.SonThachVe);
        Assert.Equal(0, created.BossState.DEF);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState);
        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { HP = BossPinnedHp },
            petState: created.PetState with { HP = PetPinnedHp, MaxHP = PetMaxHp });

        var result = await service.ExecuteSwapAsync(BattleId, pinned.Swap);
        var trace = result!.Value.CombatTrace!.Value;
        var attack = Assert.Single(
            trace.Damage.Where(d => d.Source == DamageParty.Player && d.Target == DamageParty.Boss));

        Assert.Equal(0, attack.DefenderDefenseBase);
        Assert.Equal(0, attack.DefenderDefenseEffective);
    }

    [Fact]
    public async Task CombatTrace_ShieldAbsorption_ShouldNotBeReportedAsHpLoss()
    {
        var (repository, service, created) = await NewBattleAsync();
        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState);

        // A Shield instance is a real state a Card can create (COMBAT_RULES.md §4);
        // pinning one lets a first-round instance be fully absorbed. The identity is
        // the documented "Shield" instance identity §4 item 3 owns — that is what
        // StatusEffectLifecycle.ShieldPool scans for.
        var shield = StatusEffect.TriggerBased(
            "Shield",
            StatusEffectType.Shield,
            StatusEffectSource.Boss,
            magnitude: 10_000,
            StatusEffect.ShieldDepletedCondition);

        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with
            {
                HP = BossPinnedHp,
                ActiveStatusEffects = [shield],
            },
            petState: created.PetState with { HP = PetPinnedHp, MaxHP = PetMaxHp });

        var result = await service.ExecuteSwapAsync(BattleId, pinned.Swap);
        var trace = result!.Value.CombatTrace!.Value;
        var attack = Assert.Single(
            trace.Damage.Where(d => d.Source == DamageParty.Player && d.Target == DamageParty.Boss));

        // COMBAT_RULES.md §4 item 5: the absorbed portion never also reduces HP.
        Assert.True(attack.ShieldAbsorbed > 0, "the shield must absorb part of the instance");
        Assert.Equal(attack.FinalDamage, attack.ShieldAbsorbed);
        Assert.Equal(0, attack.HpLoss);
        Assert.Equal(attack.HpBefore, attack.HpAfter);
    }

    [Fact]
    public async Task CombatTrace_MultipleDamageInstances_ShouldRemainIndividuallyCorrelated()
    {
        // A round may price more than one instance (the player's attack, the Boss's,
        // and step-19a ticks). Each is its own record, in event order, with its own
        // target — never one synthetic record.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());
        var state = await service.CreateBattleAsync(BattleId, Owner, Pet, BossDefinitions.HoaLong);

        SwapExecutionResult? multi = null;
        for (var roundIndex = 0; roundIndex < 60; roundIndex++)
        {
            var pair = FindMatchProducingPair(state);
            var outcome = await service.ExecuteSwapAsync(BattleId, pair);
            Assert.NotNull(outcome);
            var round = outcome!.Value;

            if (round.CombatTrace is { } trace && trace.Damage.Count >= 2)
            {
                multi = round;
                break;
            }

            if (round.Events.Any(e => e.Type is BattleEventType.BattleWon or BattleEventType.BattleLost))
            {
                break;
            }

            state = round.State;
        }

        Assert.NotNull(multi);
        var multiRound = multi!.Value;
        var multiTrace = multiRound.CombatTrace!.Value;

        var dealt = multiRound.Events
            .Where(e => e.Type == BattleEventType.DamageDealt)
            .Select(e => e.DamageDealt)
            .ToArray();

        Assert.True(dealt.Length >= 2, "the scenario must resolve at least two damage instances");
        Assert.Equal(dealt.Length, multiTrace.Damage.Count);

        for (var index = 0; index < dealt.Length; index++)
        {
            var record = multiTrace.Damage[index];

            // The record is correlated to the same instance's DamageDealt report by
            // order and by its own source/target/amount — not by being pooled.
            Assert.Equal(index + 1, record.Ordinal);
            Assert.Equal(dealt[index].Source, record.Source);
            Assert.Equal(dealt[index].Target, record.Target);
            Assert.Equal(dealt[index].Amount, record.FinalDamage);
            Assert.Equal(record.HpBefore - record.HpAfter, record.HpLoss);
            Assert.True(record.HpLoss >= 0, "HP loss is never negative");
        }

        // The instance sources agree with the event stream's own parties.
        Assert.Contains(multiTrace.Damage, d => d.Source == DamageParty.Player);
        Assert.Contains(multiTrace.Damage, d => d.Source == DamageParty.Boss);
    }

    // =======================================================================
    // Healing
    // =======================================================================

    [Fact]
    public async Task CombatTrace_HealAtFullHp_ShouldBeReportedWithZeroAppliedAmount()
    {
        var (repository, service, created) = await NewBattleAsync();

        // A full-HP Pet: the HP Gem pool is generated but the clamp restores 0.
        var pinned = RoundTestSupport.PlayerMatch(GemType.Hp, created.RngState);
        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { HP = BossPinnedHp },
            petState: created.PetState with { HP = PetMaxHp, MaxHP = PetMaxHp });

        var result = await service.ExecuteSwapAsync(BattleId, pinned.Swap);
        Assert.NotNull(result);
        var round = result!.Value;

        var trace = round.CombatTrace!.Value;
        var heal = Assert.Single(trace.Healing.Where(h => h.Target == DamageParty.Player));

        Assert.True(heal.RawAmount > 0, "the fixture must generate an HP-Gem heal pool");
        Assert.Equal(round.Resources.HealPool, heal.RawAmount);
        Assert.Equal(0, heal.Amount);
        Assert.Equal(PetMaxHp, heal.HpBefore);
        Assert.Equal(PetMaxHp, heal.HpAfter);
        Assert.Equal(1, heal.CycleOrdinal);
        Assert.Equal(1, heal.Ordinal);
    }

    // =======================================================================
    // Terminal round, rejected action, and passivity
    // =======================================================================

    [Fact]
    public async Task CombatTrace_LethalDamage_ShouldStillCarryTheTrace()
    {
        var (repository, service, created) = await NewBattleAsync();
        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState);

        // One point of Boss HP: the qualifying attack ends the battle at the step-8
        // terminal check, before any Boss response.
        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { HP = 1 },
            petState: created.PetState with { HP = PetPinnedHp, MaxHP = PetMaxHp });

        var result = await service.ExecuteSwapAsync(BattleId, pinned.Swap);
        Assert.NotNull(result);
        var round = result!.Value;

        Assert.Contains(round.Events, e => e.Type == BattleEventType.BattleWon);

        var trace = round.CombatTrace;
        Assert.NotNull(trace);
        var value = trace!.Value;

        Assert.Equal(0, value.Boss.HpAfter);
        Assert.Equal(1, value.Damage.Count);
        Assert.Equal(DamageParty.Player, value.Damage[0].Source);
        Assert.Equal(DamageParty.Boss, value.Damage[0].Target);
        Assert.Equal(0, value.Damage[0].HpAfter);

        // No Boss response ran, so no Boss cycle exists — the trace says so by
        // omission rather than inventing an action.
        Assert.DoesNotContain(value.Cycles, c => c.Actor == DamageParty.Boss);
    }

    [Fact]
    public async Task CombatTrace_ShouldBeAbsentForARejectedSwap()
    {
        var (_, service, created) = await NewBattleAsync();

        // Two non-adjacent cells: rejected before any resolution (MATCH3_RULES.md §2.1.2).
        var rejected = await service.ExecuteSwapAsync(BattleId, new SwapRequest(0, 63));

        Assert.NotNull(rejected);
        Assert.True(rejected!.Value.IsRejected);
        Assert.Null(rejected.Value.CombatTrace);
        Assert.Null(rejected.Value.PetHeal);
    }

    [Fact]
    public async Task CombatTrace_ShouldNotChangeTheResolution_StateOrEvents()
    {
        // The trace is a report: a round that produces one must still produce the same
        // committed state and the same event batch a trace-less implementation would.
        // The trace's own values are checked against those authoritative outputs.
        var (repository, service, created) = await NewBattleAsync();
        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState);

        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { HP = BossPinnedHp },
            petState: created.PetState with { HP = PetPinnedHp, MaxHP = PetMaxHp });

        var result = await service.ExecuteSwapAsync(BattleId, pinned.Swap);
        var round = result!.Value;
        var trace = round.CombatTrace!.Value;

        // The committed state is the authoritative one, and the trace's snapshots are
        // read from it exactly.
        Assert.Equal(round.State.PetState.HP, trace.Pet.HpAfter);
        Assert.Equal(round.State.BossState.HP, trace.Boss.HpAfter);
        Assert.Equal(round.State.PetState.Power, trace.Pet.PowerAfter);
        Assert.Equal(round.State.BossState.Power, trace.Boss.PowerAfter);

        // The damage the events report is the damage the trace reports — no log-only
        // recalculation, no second instance.
        var dealtTotal = round.Events
            .Where(e => e.Type == BattleEventType.DamageDealt)
            .Sum(e => e.DamageDealt.Amount);
        Assert.Equal(dealtTotal, trace.Damage.Sum(d => d.FinalDamage));
    }

    // =======================================================================
    // Fixtures
    // =======================================================================

    private static async Task<(
        InMemoryBattleStateRepository Repository,
        BattleStateService Service,
        BattleState Created)> NewBattleAsync(BossDefinition? boss = null)
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());
        var created = await service.CreateBattleAsync(BattleId, Owner, Pet, boss ?? BossDefinitions.HoaLong);

        return (repository, service, created);
    }

    private static SwapRequest FindMatchProducingPair(BattleState state) =>
        FindMatchProducingPair(state, state.LastCommittedSwapPair);

    private static SwapRequest FindMatchProducingPair(BattleState state, CommittedSwapPair? committed)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (committed is { } pair
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
