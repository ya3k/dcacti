using GameServer.Application.Battle;
using GameServer.Application.Cards;
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
/// (<c>SIGNALR_PROTOCOL.md</c> Â§3 item 5, Â§3.3).
///
/// <code>
/// Rule (COMBAT_RULES.md Â§3, Â§4; GAME_RULES.md Â§17; SIGNALR_PROTOCOL.md Â§3.3)
///  â†“
/// Scenario (a pinned round the engine really resolves)
///  â†“
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

        // Â§3.3.2 item 2: the trace repeats the envelope correlation values, read from
        // the committed state â€” not computed by the trace.
        Assert.Equal(BattleId, value.BattleId);
        Assert.Equal(round.State.Sequence, value.ServerSequence);

        // The move identity is the committed Swap's own two cells.
        Assert.Equal(CombatTraceMoveKind.Swap, value.Move.Kind);
        Assert.Equal(pinned.Swap.From, value.Move.FromCell);
        Assert.Equal(pinned.Swap.To, value.Move.ToCell);

        // The player cycle is the first cycle and names its own actor â€” a consumer
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
        // MATCH3_RULES.md Â§4.2: a multi-pass cycle is one actor's cascades, not a
        // second actor's. Â§3.3.2 item 2 forbids attributing a cascade by position.
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
        // instance â€” the trace adds no second calculation and no re-derivation.
        var calculated = round.Events
            .Single(e => e.Type == BattleEventType.DamageCalculated)
            .DamageCalculated;

        Assert.Equal(calculated.Base, attack.Base);
        Assert.Equal(calculated.ComboModifier, attack.ComboModifier);
        Assert.Equal(calculated.ElementModifier, attack.ElementModifier);
        Assert.Equal(calculated.OtherModifiers, attack.OtherModifiers);
        Assert.Equal(calculated.Defense, attack.MitigatedDamage);
        Assert.Equal(calculated.FinalDamage, attack.FinalDamage);

        // Â§3.3.1: the base DEF is the defender's stored stat; the effective DEF is
        // the value step 5 actually read. Hoa Long stores DEF 50 with no DEF buff on
        // a first round, so both are the stored 50 â€” never a client-side constant.
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
        // BOSS_RULES.md Â§6.4: SÆ¡n Tháº¡ch Vá»‡ stores DEF 0. A zero is a real value â€”
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

        // A Shield instance is a real state a Card can create (COMBAT_RULES.md Â§4);
        // pinning one lets a first-round instance be fully absorbed. The identity is
        // the documented "Shield" instance identity Â§4 item 3 owns â€” that is what
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

        // COMBAT_RULES.md Â§4 item 5: the absorbed portion never also reduces HP.
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
        // target â€” never one synthetic record.
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
            // order and by its own source/target/amount â€” not by being pooled.
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

    
    /// <summary>
    /// TASK-324 — <c>BOSS_RULES.md</c> §6.2.3's regeneration is a heal the Round
    /// applied, so <c>SIGNALR_PROTOCOL.md</c> §3.3 requires it in <c>healing[]</c>.
    ///
    /// <b>The scenario is arranged so the record is the ONLY thing that can account
    /// for the HP movement.</b> Two battles carry the SAME real Mộc Yêu definition, the
    /// SAME pinned round, Boss HP, and Passive progress, and differ only in the
    /// Passive's authored threshold: the firing arm keeps the definition's own 8, and
    /// the control's is raised above it. The pinned progress is one Match below each
    /// arm's own threshold, because <c>PASSIVE_RULES.md</c> §2 item 3 evaluates the
    /// threshold once, after the whole batch, at <c>P + N</c> — so the firing arm's
    /// batch carries it across and the control's cannot reach it. The round's own
    /// fixture is chosen so the Boss's step-18a board action heals nothing, so the two
    /// arms' settled HP differs by the regen alone (<c>GAME_RULES.md</c> §17 step 18b).
    /// </summary>
    [Fact]
    public async Task CombatTrace_BossPassiveRegen_ShouldBeReportedAsTheBossHealOfTheRound()
    {
        var firing = await ResolveMocYeuRoundAsync(
            "battle-trace-regen",
            reachable: true);
        var control = await ResolveMocYeuRoundAsync(
            "battle-trace-regen-control",
            reachable: false,
            firingArmed: firing);

        // The fixture's precondition: the Boss's own board cycle restored no HP, so the
        // Round's only Boss heal is the Passive's regen — in both arms, which play the
        // same board, Swap, and RNG stream.
        Assert.Equal(0, firing.Fixture.BossResources.HealPool);
        Assert.Equal(0, control.Fixture.BossResources.HealPool);
        Assert.Equal(firing.Fixture.Swap, control.Fixture.Swap);

        // The Passive activates under its real precondition, in one arm only.
        Assert.Single(
            firing.Round.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");

        // Both arms are arranged from the round's own batch, and the control is
        // genuinely below its threshold: its identical batch, evaluated once after the
        // whole batch, cannot reach a threshold placed `N + 1` above 0.
        var batch = firing.Round.Resolution.TotalMatches;
        Assert.True(batch > 0, "the Round must charge the Passive with at least one Match");
        Assert.True(batch < BossDefinitions.MocYeu.PassiveThreshold, "an ATK-gem Match clears 3 cells, so one board action stays under 8 Matches; the firing arm's crossing is therefore arranged from its pin");
        Assert.Equal(firing.Round.Resolution.TotalMatches, control.Round.Resolution.TotalMatches);
        Assert.Equal(BossDefinitions.MocYeu.PassiveThreshold, firing.PinnedThreshold);
        Assert.Equal(BossDefinitions.MocYeu.PassiveThreshold - batch, firing.PinnedProgress);
        Assert.Equal(batch + 1, control.PinnedThreshold);
        Assert.Equal(0, control.PinnedProgress);
        Assert.True(
            firing.PinnedProgress + batch == firing.PinnedThreshold,
            "the firing arm's batch must reach its threshold exactly");
        Assert.True(
            control.PinnedProgress + batch < control.PinnedThreshold,
            "the control arm must stay below its threshold");

        // BOSS_RULES.md §6.2.3: 5% of MaxHP 5000 = 250, truncated and clamped. The two
        // identically pinned arms differ by the regen alone.
        const int regen = 250;
        Assert.Equal(5000, firing.Round.State.BossState.MaxHP);
        Assert.Equal(regen, firing.Round.State.BossState.HP - control.Round.State.BossState.HP);

        // The control's Passive still charges its own authored 1000-Match threshold; it
        // must simply never cross it. The trigger is asserted below on the heal itself.
        Assert.DoesNotContain(
            control.Round.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");

        // Exactly ONE Boss heal record: the same HP restoration is never reported
        // twice. The control arm — same round, Passive not activating — has none.
        var bossHeal = Assert.Single(firing.Trace.Healing.Where(h => h.Target == DamageParty.Boss));
        Assert.DoesNotContain(control.Trace.Healing, h => h.Target == DamageParty.Boss);

        // Its target, applied amount, raw amount, and HP pair are the mutation's own:
        // the amount is what the MaxHP clamp left, never the nominal 250 when the two
        // differ (the full-HP case is the test below).
        Assert.Equal(DamageParty.Boss, bossHeal.Target);
        Assert.Equal(regen, bossHeal.Amount);
        Assert.Equal(bossHeal.RawAmount, bossHeal.Amount);
        Assert.Equal(bossHeal.HpBefore + regen, bossHeal.HpAfter);
        Assert.Equal(firing.Trace.Boss.HpAfter, bossHeal.HpAfter);

        // It names no actor cycle: BOSS_RULES.md §3.3 item 1 applies the Passive once
        // per player action, outside any cycle's action — §3.3.1/§3.3.2 item 7's
        // not-applicable omission, reported as an absent member (null in the Domain
        // value) rather than as 0, which would be written to the wire.
        Assert.Null(bossHeal.CycleOrdinal);
        Assert.DoesNotContain(firing.Trace.Cycles, c => c.Ordinal == 0);

        // Ordinals are the round's own batch order; the Player cycle's heal still names
        // its cycle, and the regen follows it in application order.
        Assert.Equal(
            Enumerable.Range(1, firing.Trace.Healing.Count),
            firing.Trace.Healing.Select(h => h.Ordinal));
        Assert.All(
            firing.Trace.Healing.Where(h => h.Target == DamageParty.Player),
            h => Assert.Equal(1, h.CycleOrdinal));
        Assert.Equal(firing.Trace.Healing.Count, bossHeal.Ordinal);

        // The Boss's whole HP movement is exactly its damage instances plus the heals
        // the trace reports. Before TASK-324 this residual was the unrecorded regen.
        var damageLoss = firing.Trace.Damage
            .Where(d => d.Target == DamageParty.Boss)
            .Sum(d => d.HpLoss);
        Assert.Equal(
            firing.Trace.Boss.HpAfter - firing.Trace.Boss.HpBefore,
            -damageLoss + firing.Trace.Healing.Where(h => h.Target == DamageParty.Boss).Sum(h => h.Amount));
    }

    /// <summary>
    /// TASK-324 — the same Passive at full HP reports the heal with a zero applied
    /// amount and its raw amount intact, not as "no heal" (<c>SIGNALR_PROTOCOL.md</c>
    /// §3.3.2 item 6), and records it exactly once.
    /// </summary>
    [Fact]
    public async Task CombatTrace_BossPassiveRegenAtFullHp_ShouldBeReportedWithZeroAppliedAmount()
    {
        var firing = await ResolveMocYeuRoundAsync(
            "battle-trace-regen-full-hp",
            reachable: true,
            bossHp: 5000);

        Assert.Single(
            firing.Round.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");

        // One record, whose applied amount is the clamp's zero and whose raw amount is
        // still the authored 250.
        var bossHeal = Assert.Single(firing.Trace.Healing.Where(h => h.Target == DamageParty.Boss));

        Assert.Equal(250, bossHeal.RawAmount);
        Assert.Equal(0, bossHeal.Amount);
        Assert.Equal(bossHeal.HpBefore, bossHeal.HpAfter);
        Assert.Equal(firing.Trace.Boss.HpAfter, bossHeal.HpAfter);
        Assert.Null(bossHeal.CycleOrdinal);
    }

    /// <summary>
    /// Resolves one pinned Round of a Mộc Yêu battle.
    ///
    /// <b>The Passive's threshold is what the arms differ in.</b> The round is the
    /// suite's own deterministic fixture search (<see cref="BossRoundFixtures"/>) over
    /// generated boards, resolved by the production stages in the production order
    /// (<c>ADR-026</c> §8, <c>BOSS_RULES.md</c> §4.3); it is narrowed to a round whose
    /// Boss cycle heals it nothing, which is what makes the Boss HP movement
    /// attributable to the Passive. The stream is fixture state
    /// (<c>GAME_STATE.md</c> §2.6.2), never an asserted value.
    ///
    /// <b>Why the control reuses the firing arm's round.</b> The firing arm uses the
    /// real definition's authored threshold; the control raises only the threshold,
    /// exactly as <see cref="BossPassiveEffectsTests"/> arranges its non-firing arm.
    /// Progress is pinned one Match below the arm's own threshold — the arrangement
    /// <see cref="BossPassiveEffectsTests"/> derives from the Turn's own batch
    /// (<c>8 - batch</c>) — so the firing arm's batch carries it across and the
    /// control's identically pinned progress stays below.
    /// </summary>
    private static async Task<MocYeuTracedRound> ResolveMocYeuRoundAsync(
        string battleId,
        bool reachable,
        int bossHp = 3000,
        MocYeuTracedRound? firingArmed = null)
    {
        var fixture = firingArmed?.Fixture
            ?? BossRoundFixtures.Find(f => f.BossResources.HealPool == 0);

        // Both arms reach their crossing from the round's OWN Match batch, exactly as
        // BossPassiveEffectsTests arranges it: PASSIVE_RULES.md §2 item 3 evaluates the
        // threshold once, after the whole batch, at `P + N`. The firing arm therefore
        // enters at `8 - N`, which its batch carries to exactly the authored 8. The
        // control's threshold is placed exactly `N + 1` above the very same batch, so
        // it stays below even when the batch starts from progress 0; deriving it from
        // the round (rather than fixing it at 1000, as BossPassiveEffectsTests does) is
        // what makes that below-threshold property exact and self-evident.
        var batch = firingArmed?.Round.Resolution.TotalMatches
            ?? fixture.PlayerResolution.TotalMatches;
        Assert.True(batch > 0, "the fixture Round must charge the Boss Passive with at least one Match");

        var threshold = reachable
            ? BossDefinitions.MocYeu.PassiveThreshold
            : batch + 1;
        var progress = reachable
            ? BossDefinitions.MocYeu.PassiveThreshold - batch
            : 0;

        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(
            repository,
            new FixedRngSeedSource(),
            cardDefinitions: new TestCardDefinitionLookup().WithProvisionedBossCards());

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.MocYeu);

        await RoundTestSupport.PinAsync(
            repository,
            created,
            fixture.Board,
            bossState: created.BossState with
            {
                HP = bossHp,
                Power = 0,
                PassiveProgress = new PassiveProgress(Threshold: threshold, Current: progress),
            },
            petState: created.PetState with { HP = PetPinnedHp, MaxHP = PetMaxHp },
            rngState: fixture.RngState);

        var result = await service.ExecuteSwapAsync(battleId, fixture.Swap);
        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        var round = result.Value;
        Assert.NotNull(round.CombatTrace);

        return new MocYeuTracedRound(
            round,
            round.CombatTrace!.Value,
            fixture,
            PinnedThreshold: threshold,
            PinnedProgress: progress);
    }

    /// <summary>One resolved, trace-carrying Mộc Yêu round, its fixture, and its pin.</summary>
    private readonly record struct MocYeuTracedRound(
        SwapExecutionResult Round,
        CombatTrace Trace,
        BossRoundFixture Fixture,
        int PinnedThreshold,
        int PinnedProgress);


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

    /// <summary>
    /// <c>SIGNALR_PROTOCOL.md</c> Â§3.3.1 and Â§3.3.2 item 7 (as reconciled by
    /// TASK-322): <c>cycleOrdinal</c> is a *not-applicable* omission, not a free
    /// choice â€” so for every heal and every ATK-gated attack instance a Round
    /// really produces, the member is **applicable** and names an existing cycle.
    ///
    /// <b>This is the mandatory half of the corrected rule.</b> It pins the real
    /// Round producers (<see cref="ResourceGenerator.ApplyHeal"/>'s HP-Gem heal at
    /// the player cycle, <see cref="ResourceGenerator.ApplyActorMatchEffects"/>'s
    /// Boss heal at the Boss's own cycle, and
    /// <see cref="AttackResolution"/>'s attack instances) â€” a producer that stopped
    /// naming its cycle, or named a cycle the trace does not contain, fails here.
    /// The omission itself cannot arise on this path, because every heal and attack
    /// this stage applies belongs to a cycle of the round.
    /// </summary>
    [Fact]
    public async Task CombatTrace_EveryHealAndAttackCycleOrdinal_ShouldNameAnExistingCycle()
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());
        var state = await service.CreateBattleAsync(BattleId, Owner, Pet, BossDefinitions.HoaLong);

        var roundsWithAHeal = 0;
        var roundsWithABossCycle = 0;
        var roundsWithAnAttack = 0;

        for (var roundIndex = 0; roundIndex < 40; roundIndex++)
        {
            var pair = FindMatchProducingPair(state);
            var outcome = await service.ExecuteSwapAsync(BattleId, pair);
            Assert.NotNull(outcome);
            var round = outcome!.Value;

            Assert.True(round.CombatTrace is not null, "an accepted Swap always carries a trace");
            var trace = round.CombatTrace!.Value;

            var cycleOrdinals = trace.Cycles.Select(cycle => cycle.Ordinal).ToArray();
            Assert.NotEmpty(cycleOrdinals);

            foreach (var heal in trace.Healing)
            {
                Assert.NotNull(heal.CycleOrdinal);
                Assert.Contains(heal.CycleOrdinal!.Value, cycleOrdinals);
            }

            foreach (var instance in trace.Damage.Where(d => d.Phase == CombatTraceDamagePhase.Attack))
            {
                Assert.NotNull(instance.CycleOrdinal);
                Assert.Contains(instance.CycleOrdinal!.Value, cycleOrdinals);
            }

            // The ATK-gated attack of a board cycle is the only attack phase this
            // Round can price (a step-19a tick is StatusTick and belongs to no
            // cycle), so no instance of this phase may omit the member.
            Assert.DoesNotContain(
                trace.Damage,
                d => d.Phase == CombatTraceDamagePhase.Attack && d.CycleOrdinal is null);
            Assert.DoesNotContain(
                trace.Healing,
                h => h.CycleOrdinal is null || !cycleOrdinals.Contains(h.CycleOrdinal.Value));

            roundsWithAHeal += trace.Healing.Count > 0 ? 1 : 0;
            roundsWithABossCycle += trace.Cycles.Any(c => c.Actor == DamageParty.Boss) ? 1 : 0;
            roundsWithAnAttack += trace.Damage.Count > 0 ? 1 : 0;

            if (round.Events.Any(e => e.Type is BattleEventType.BattleWon or BattleEventType.BattleLost))
            {
                break;
            }

            state = round.State;
        }

        // The loop must have exercised the producers it claims to pin; an empty
        // sample would make the assertions above vacuous.
        Assert.True(roundsWithABossCycle > 0, "the scenario must include at least one Boss cycle");
        Assert.True(roundsWithAnAttack > 0, "the scenario must include at least one damage instance");
        Assert.True(roundsWithAHeal > 0, "the scenario must include at least one heal");
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

        // No Boss response ran, so no Boss cycle exists â€” the trace says so by
        // omission rather than inventing an action.
        Assert.DoesNotContain(value.Cycles, c => c.Actor == DamageParty.Boss);
    }

    [Fact]
    public async Task CombatTrace_ShouldBeAbsentForARejectedSwap()
    {
        var (_, service, created) = await NewBattleAsync();

        // Two non-adjacent cells: rejected before any resolution (MATCH3_RULES.md Â§2.1.2).
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

        // The damage the events report is the damage the trace reports â€” no log-only
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
            "A generated board has at least one valid Swap (MATCH3_RULES.md Â§1.4).");
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
