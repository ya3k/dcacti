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
/// The DEF-Gem buff end to end through the atomic round
/// (<c>COMBAT_RULES.md</c> §2, §3.2, §5.4.6, §5.5.6; <c>GAME_RULES.md</c> §17;
/// <c>ADR-026</c> item 4 as amended by TASK-299).
///
/// <code>
/// Rule (COMBAT_RULES.md §5.4.6)                    Scenario → Test
///   applied by the acting actor's DEF match          PlayerDefMatch_…
///   raises the effective DEF the pipeline reads      …_MitigatesTheBossAttack…
///   active through the opponent's next attack        …CoveredTurn…
///   expires at that Turn's step 19a                  …ExpiresAt…
///   no Shield is created                             …WithoutCreatingAShield
///   actor-relative (Boss side)                       BossDefBuff_…
///   non-retroactive (applied at step 18a)            BossDefBuff_DoesNotReach…
///   duplicates cannot apply it twice                 DuplicateSubmission_…
/// </code>
///
/// <b>Every expectation is read from the documented formula and the round's own
/// reports</b> — the generated <c>DefensePool</c> the round itself produced and the
/// <c>DamageCalculated</c> breakdown it emitted — never from a re-implementation of
/// the rule in the test.
/// </summary>
public class DefenseBuffLifecycleTests
{
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_def_buff"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    private static readonly PlayerId Owner = new("player_def_buff_owner");

    private const int BossDefense = 50;
    private const int PetDefense = 200;

    private static int I(int row, int column) => BoardState.ToIndex(row, column);

    private static (BattleStateService Service, InMemoryBattleStateRepository Repository) NewService()
    {
        var repository = new InMemoryBattleStateRepository();
        return (new BattleStateService(repository, new FixedRngSeedSource()), repository);
    }

    /// <summary>
    /// The <c>DamageCalculated</c> report of the one damage instance the supplied
    /// side dealt in a committed round, or <c>null</c> when that side produced no
    /// confirmed ATK-gem match and therefore attacked nothing (<c>ADR-026</c> D1).
    ///
    /// <b>It is paired by direction, not by position.</b> A round carries a
    /// Player→Boss instance at step 15 and possibly a Boss→Pet instance at step 18d
    /// (<c>GAME_RULES.md</c> §17), and each instance's three damage events are
    /// emitted consecutively (<c>GAME_EVENTS.md</c> §2). The direction is read from
    /// the <c>DamageDealt</c>, which carries <c>source</c>/<c>target</c>; the
    /// calculation is the report immediately preceding it.
    /// </summary>
    private static DamageCalculation? DamageCalculationFor(
        SwapExecutionResult result,
        DamageParty source)
    {
        var events = result.Events;

        for (var index = 1; index < events.Count; index++)
        {
            if (events[index].Type == BattleEventType.DamageDealt
                && events[index].DamageDealt.Source == source
                && events[index - 1].Type == BattleEventType.DamageCalculated)
            {
                return events[index - 1].DamageCalculated;
            }
        }

        return null;
    }

    /// <summary>
    /// <c>COMBAT_RULES.md</c> §3.2's post-mitigation value for one reported
    /// breakdown with the supplied defending DEF:
    /// <c>Base × Combo × Element × Other × 100 / (100 + DEF)</c>.
    /// </summary>
    private static double ExpectedMitigated(DamageCalculation calculation, int defense) =>
        calculation.Base
        * calculation.ComboModifier
        * calculation.ElementModifier
        * calculation.OtherModifiers
        * ((double)100 / (100 + defense));

    // =======================================================================
    // 1. The Player's DEF match mitigates the same round's Boss attack
    // =======================================================================

    [Fact]
    public async Task PlayerDefMatch_MitigatesTheBossAttackThroughTheEffectiveDefense()
    {
        var (service, repository) = NewService();
        var created = await service.CreateBattleAsync("def-buff-mitigation", Owner, Pet, BossDefinitions.HoaLong);

        // The Pet's stored DEF is pinned to 0 so the only DEF the Boss's attack can
        // be mitigated by is the buff the round's own DEF match grants. §5.4.6 item 2
        // then makes the effective value exactly the generated pool.
        var pet = created.PetState with { DEF = 0, HP = 1000, MaxHP = 1000 };
        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board, created.BossState, pet);

        var result = await service.ExecuteSwapAsync("def-buff-mitigation", pinned.Swap);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        var defensePool = result.Value.Resources.DefensePool;
        Assert.True(defensePool > 0, "the pinned DEF match must generate DEF");
        Assert.Equal(
            ResourceGenerator.DefGemBaseDefense * result.Value.Resources.ClearedGemCounts.Def,
            defensePool);

        var calculation = DamageCalculationFor(result.Value, DamageParty.Boss);
        Assert.NotNull(calculation);

        // The Boss attacked with the buff active: its mitigation input is
        // 0 (stored DEF) + DefensePool, not the stored DEF alone.
        var expectedDefense = ExpectedMitigated(calculation!.Value, defensePool);
        Assert.Equal(expectedDefense, calculation.Value.Defense, precision: 6);
        Assert.Equal((int)Math.Truncate(expectedDefense), calculation.Value.FinalDamage);

        // And it really is a reduction: without the buff the factor would be 100/100.
        var preDefense = calculation.Value.Base
            * calculation.Value.ComboModifier
            * calculation.Value.ElementModifier
            * calculation.Value.OtherModifiers;
        Assert.True(calculation.Value.Defense < preDefense);
    }

    [Fact]
    public async Task PlayerDefMatch_GrantsNoShieldAndExpiresAtTheTurnBoundary()
    {
        var (service, repository) = NewService();
        const string battleId = "def-buff-expiry";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var result = await service.ExecuteSwapAsync(battleId, pinned.Swap);
        Assert.True(result!.Value.IsAccepted);

        var petAfter = result.Value.State.PetState;

        // COMBAT_RULES.md §4 item 3 / TASK-299: DEF Gems grant no Shield, during or
        // after the round.
        Assert.DoesNotContain(petAfter.ActiveStatusEffects, e => e.Type == StatusEffectType.Shield);
        Assert.Equal(0, StatusEffectLifecycle.ShieldPool(petAfter.ActiveStatusEffects));

        // COMBAT_RULES.md §5.4.6 window table, Pet row: applied at step 12/14 with
        // RemainingTurns = 1, consumed and removed at that same Turn's step 19a — so
        // no "DefenseBuff" survives the round, and nothing leaks into the next Turn.
        Assert.DoesNotContain(
            petAfter.ActiveStatusEffects,
            e => e.Id == StatusEffectLifecycle.DefenseBuffId);
        Assert.Equal(1, result.Value.State.Turn);
    }

    [Fact]
    public async Task PlayerDefBuff_DoesNotLeakIntoTheFollowingTurn()
    {
        // Round N: a DEF-only Player swap grants the buff for that round's Boss
        // attack; it expires at that Turn's step 19a. Round N+1 (a non-DEF Player
        // swap) must price the Boss's attack with the stored DEF alone.
        var (service, repository) = NewService();
        const string battleId = "def-buff-no-leak";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pet = created.PetState with { DEF = 0, HP = 1000, MaxHP = 1000 };
        var first = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, first.Board, created.BossState, pet);

        var firstResult = await service.ExecuteSwapAsync(battleId, first.Swap);
        Assert.True(firstResult!.Value.IsAccepted);
        Assert.True(firstResult.Value.Resources.DefensePool > 0);

        var afterFirst = firstResult.Value.State;
        Assert.DoesNotContain(
            afterFirst.PetState.ActiveStatusEffects,
            e => e.Id == StatusEffectLifecycle.DefenseBuffId);

        // Round N+1: a POWER-only Player swap generates no DEF (so no new buff), and
        // the board the Boss acts on is the one the fixture pins.
        var second = RoundTestSupport.PlayerMatch(
            GemType.Power,
            afterFirst.RngState,
            afterFirst.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, afterFirst, second.Board);

        var secondResult = await service.ExecuteSwapAsync(battleId, second.Swap);
        Assert.True(secondResult!.Value.IsAccepted);
        Assert.Equal(0, secondResult.Value.Resources.DefensePool);

        var calculation = DamageCalculationFor(secondResult.Value, DamageParty.Boss);
        if (calculation is { } bossCalculation)
        {
            // Stored DEF is 0, so the effective value is 0 and the factor is 100/100:
            // if the round-N buff had leaked, this would be 100/(100 + pool).
            var expectedDefense = ExpectedMitigated(bossCalculation, 0);
            Assert.Equal(expectedDefense, bossCalculation.Defense, precision: 6);
        }
    }

    // =======================================================================
    // 2. The Boss's DEF buff covers the NEXT Player attack (Boss side)
    // =======================================================================

    [Fact]
    public async Task BossDefBuff_CoversTheNextPlayerAttackAndExpiresAtThatTurnsBoundary()
    {
        var (service, repository) = NewService();
        const string battleId = "boss-def-buff-covered";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        // The state a Boss DEF match leaves after Turn N's step 19a: the
        // §5.4.6 Boss row applies RemainingTurns = 2, and one step-19a pass has
        // consumed it to 1. That is the buff the Player's Turn N+1 attack meets.
        const int magnitude = 20;

        var state = created;
        var pet = created.PetState with { HP = 5000, MaxHP = 5000 };
        var coverageVerified = false;
        var expiryVerified = false;

        for (var round = 0; round < 12 && !(coverageVerified && expiryVerified); round++)
        {
            var boss = state.BossState with
            {
                DEF = BossDefense,
                HP = 5000,
                MaxHP = 5000,
                ATK = 100,
                ActiveStatusEffects =
                [
                    StatusEffect.TurnBased(
                        StatusEffectLifecycle.DefenseBuffId,
                        StatusEffectType.BuffDebuff,
                        StatusEffectSource.Boss,
                        magnitude,
                        1,
                        StatusEffectLifecycle.DefenseStat),
                ],
            };

            var pinned = RoundTestSupport.PlayerMatch(
                GemType.Atk,
                state.RngState,
                state.LastCommittedSwapPair);
            await RoundTestSupport.PinAsync(repository, state, pinned.Board, boss, pet);

            var result = await service.ExecuteSwapAsync(battleId, pinned.Swap);
            Assert.True(result!.Value.IsAccepted);

            // The Player's attack — an ATK-only swap, so it qualifies unconditionally —
            // is mitigated by the Boss's stored DEF plus the active buff (§5.5.6).
            var playerDamage = DamageCalculationFor(result.Value, DamageParty.Player);
            Assert.NotNull(playerDamage);
            var expectedDefense = ExpectedMitigated(playerDamage!.Value, BossDefense + magnitude);
            Assert.Equal(expectedDefense, playerDamage.Value.Defense, precision: 6);
            Assert.Equal((int)Math.Truncate(expectedDefense), playerDamage.Value.FinalDamage);
            coverageVerified = true;

            // §5.4.6 Boss row: 1 -> 0 at this Turn's step 19a and removed in that
            // pass. Asserted on a round in which the Boss's own action cleared no DEF
            // Gem, so the only "DefenseBuff" the Boss could hold is the seeded one —
            // a round in which the Boss matched DEF legitimately refreshes the
            // instance instead (a new application, not a survival).
            var bossDefCells = RoundTestSupport.BossBoardEvents(result.Value)
                .Count(e => e.Type == BattleEventType.GemMatched && e.Gem.GemType == GemType.Def);

            if (bossDefCells == 0)
            {
                Assert.DoesNotContain(
                    result.Value.State.BossState.ActiveStatusEffects,
                    e => e.Id == StatusEffectLifecycle.DefenseBuffId);
                expiryVerified = true;
            }

            state = result.Value.State;
            pet = state.PetState;
        }

        Assert.Equal(BossDefense, state.BossState.DEF);
        Assert.True(coverageVerified);
        Assert.True(expiryVerified, "No round without a Boss DEF match was found in 12 rounds — fixture defect.");
    }

    [Fact]
    public async Task BossDefMatch_AppliesTheBuffForTheNextTurnAndNotTheSameTurn()
    {
        // Search rounds until the Boss's own board action clears at least one DEF
        // Gem. At that round:
        //   * the Player's attack of the SAME round is priced with the stored DEF
        //     (step 18a is after step 15 — §5.4.6's non-retroactivity statement),
        //   * the Boss ends the round holding the "DefenseBuff" instance with the
        //     §5.4.6 Boss duration minus that round's single step-19a pass, and
        //   * its magnitude is the Boss action's own generated Defense.
        var (service, repository) = NewService();
        const string battleId = "boss-def-buff-application";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var boss = created.BossState with { DEF = BossDefense, ATK = 100 };
        var pet = created.PetState with { HP = 5000, MaxHP = 5000 };

        var state = created;
        var found = false;

        for (var round = 0; round < 12 && !found; round++)
        {
            var bossHadBuff = state.BossState.ActiveStatusEffects
                .Any(e => e.Id == StatusEffectLifecycle.DefenseBuffId);

            var pinned = RoundTestSupport.PlayerMatch(
                GemType.Atk,
                state.RngState,
                state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(
                repository,
                state,
                pinned.Board,
                boss,
                pet);

            var result = await service.ExecuteSwapAsync(battleId, pinned.Swap);
            Assert.True(result!.Value.IsAccepted);

            var bossDefCells = RoundTestSupport.BossBoardEvents(result.Value)
                .Count(e => e.Type == BattleEventType.GemMatched && e.Gem.GemType == GemType.Def);

            if (bossDefCells == 0)
            {
                state = result.Value.State;
                continue;
            }

            found = true;
            var bossAfter = result.Value.State.BossState;

            // Non-retroactive: the Player's step-15 attack of this same round could
            // not see a buff applied at step 18a. (It is only asserted when no
            // earlier round's buff is still active.)
            if (!bossHadBuff)
            {
                var playerDamage = DamageCalculationFor(result.Value, DamageParty.Player);
                Assert.NotNull(playerDamage);
                var expectedDefense = ExpectedMitigated(playerDamage!.Value, BossDefense);
                Assert.Equal(expectedDefense, playerDamage.Value.Defense, precision: 6);
            }

            // Applied with the §5.4.6 Boss duration, then one step-19a pass.
            var buff = Assert.Single(
                bossAfter.ActiveStatusEffects.Where(e => e.Id == StatusEffectLifecycle.DefenseBuffId));
            Assert.Equal(StatusEffectType.BuffDebuff, buff.Type);
            Assert.Equal(StatusEffectSource.Boss, buff.Source);
            Assert.Equal(StatusEffectLifecycle.DefenseStat, buff.TargetStat);
            Assert.Equal(ResourceGenerator.DefGemBaseDefense * bossDefCells, buff.Magnitude);
            Assert.Equal(ResourceGenerator.BossDefBuffDurationTurns - 1, buff.RemainingTurns);

            // No Shield was created on the Boss by the DEF match (§4 item 3).
            Assert.DoesNotContain(
                bossAfter.ActiveStatusEffects,
                e => e.Type == StatusEffectType.Shield);
            Assert.Equal(BossDefense, bossAfter.DEF);
        }

        Assert.True(found, "The Boss never cleared a DEF Gem in 12 pinned rounds — fixture defect.");
    }

    // =======================================================================
    // 3. Duplicate submission cannot apply the buff twice (ADR-025, ADR-026 §7)
    // =======================================================================

    [Fact]
    public async Task DuplicateActionSubmission_ShouldNotApplyTheBuffTwice()
    {
        var (service, repository) = NewService();
        const string battleId = "def-buff-duplicate";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        // A seeded Boss DEF buff with the full §5.4.6 Boss duration (2) so that the
        // round's single step-19a pass leaves it ACTIVE (2 -> 1): the buff is then
        // observable in the committed state the replay must reproduce.
        var boss = created.BossState with
        {
            ActiveStatusEffects =
            [
                StatusEffect.TurnBased(
                    StatusEffectLifecycle.DefenseBuffId,
                    StatusEffectType.BuffDebuff,
                    StatusEffectSource.Boss,
                    20,
                    ResourceGenerator.BossDefBuffDurationTurns,
                    StatusEffectLifecycle.DefenseStat),
            ],
        };

        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board, boss, created.PetState);

        var request = pinned.Swap with { ActionId = "def-buff-action-1" };

        var first = await service.ExecuteSwapAsync(battleId, request);
        Assert.NotNull(first);
        Assert.True(first!.Value.IsAccepted);
        Assert.False(first.Value.IsIdempotentReplay);

        var firstState = first.Value.State;
        Assert.Single(
            firstState.BossState.ActiveStatusEffects.Where(
                e => e.Id == StatusEffectLifecycle.DefenseBuffId));
        Assert.DoesNotContain(
            firstState.PetState.ActiveStatusEffects,
            e => e.Type == StatusEffectType.Shield);

        var second = await service.ExecuteSwapAsync(battleId, request);
        Assert.NotNull(second);
        Assert.True(second!.Value.IsAccepted);
        Assert.True(second.Value.IsIdempotentReplay);

        // The replay is the committed state itself: no second application, no
        // duplicate instance, no extra buff expiry, and no re-emitted events.
        Assert.Equal(firstState.Sequence, second.Value.State.Sequence);
        Assert.Empty(second.Value.Events);
        Assert.Single(
            second.Value.State.BossState.ActiveStatusEffects.Where(
                e => e.Id == StatusEffectLifecycle.DefenseBuffId));
        Assert.True(StatusEffectLifecycle.EffectsEqual(
            firstState.PetState.ActiveStatusEffects,
            second.Value.State.PetState.ActiveStatusEffects));
        Assert.True(StatusEffectLifecycle.EffectsEqual(
            firstState.BossState.ActiveStatusEffects,
            second.Value.State.BossState.ActiveStatusEffects));
    }

    // =======================================================================
    // 4. Actor-relative ownership at the round boundary
    // =======================================================================

    [Fact]
    public async Task PlayerDefMatch_ShouldNotGrantTheBuffToTheBoss()
    {
        var (service, repository) = NewService();
        const string battleId = "def-buff-player-ownership";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var result = await service.ExecuteSwapAsync(battleId, pinned.Swap);
        Assert.True(result!.Value.IsAccepted);

        // ADR-026 item 4: the acting side owns what it generates. The Player's DEF
        // match never puts the buff on the Boss (the Boss may independently hold one
        // from its own DEF match, which this fixture checks by magnitude identity).
        var playerPool = result.Value.Resources.DefensePool;
        Assert.DoesNotContain(
            result.Value.State.BossState.ActiveStatusEffects,
            e => e.Id == StatusEffectLifecycle.DefenseBuffId
                && e.Source == StatusEffectSource.Player);
        Assert.True(playerPool > 0);
    }
}
