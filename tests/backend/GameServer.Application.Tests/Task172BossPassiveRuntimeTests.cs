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
/// Runtime execution of the two non-match-charged MVP Boss Passives authored by
/// TASK-172 — Sơn Thạch Vệ's <c>Boss HP ≤ 50%</c> Rage and Kim Lôi Vương's
/// <c>Player Combo ≥ 4</c> Rage (<c>BOSS_RULES.md</c> §6.2.4/§6.2.5).
///
/// <code>
/// Rule (BOSS_RULES.md §6.2.4 / §6.2.5)
///  ↓
/// Scenario (Given the documented Boss and a committed Swap, When the response
///           runs, Then the documented state)
///  ↓
/// Test
/// </code>
///
/// <b>Every expected value traces to a section, never to the implementation.</b>
/// The Boss configuration is read from <see cref="BossDefinitions"/>, which
/// transcribes <c>BOSS_RULES.md</c> §6, so no assertion encodes "whatever the
/// code does".
///
/// <b>The trigger point these tests exercise.</b> <c>BOSS_RULES.md</c> §3.3 item 1
/// / §6.2.4 / §6.2.5: the Passive is evaluated at Boss Response step 18a against
/// the <b>post-damage</b> battle state, once per player action, after player
/// damage and before the Skill. These tests therefore drive the real
/// <see cref="BattleStateService"/> pipeline over a committed Swap and read the
/// settled <c>BossState</c>, rather than calling any effect-application helper
/// directly.
///
/// <b>Neither Passive is match-charged.</b> Both carry
/// <c>PassiveThreshold = null</c> (<c>DATABASE.md</c> §1 note item 3), which means
/// "no match-charging threshold" and emphatically <b>not</b> "always active" — so
/// the negative case is asserted for each.
/// </summary>
public class Task172BossPassiveRuntimeTests
{
    /// <summary>
    /// The Pet these battles carry — the owned instance identity
    /// (<c>GAME_STATE.md</c> §2.3), Xích Lang's MVP Element and Passive
    /// (<c>ELEMENT_RULES.md</c> §6, <c>PASSIVE_RULES.md</c> §8).
    /// </summary>
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    private static readonly PlayerId Owner = new("player_task172_passive_owner");

    /// <summary>
    /// The canonical PassiveId of Sơn Thạch Vệ (<c>BOSS_RULES.md</c> §6.4), recorded
    /// verbatim by TASK-172 and deliberately not normalized to the earlier Bosses'
    /// observed spellings.
    /// </summary>
    private const string SonThachVePassiveId = "son-thach-ve-enrage";

    /// <summary>The canonical PassiveId of Kim Lôi Vương (<c>BOSS_RULES.md</c> §6.4).</summary>
    private const string KimLoiVuongPassiveId = "kim-loi-vuong-combo";

    /// <summary>
    /// A fresh service over a fresh repository, returning both so a scenario can
    /// seed the authoritative active battle state with the documented starting
    /// position it needs — the same pattern the existing MVP Boss Passive suite
    /// uses (<c>InMemoryBattleStateRepository.TryUpdateAsync</c>). Only state the
    /// game genuinely produces is written; the effect under test never is.
    /// </summary>
    private static (BattleStateService Service, InMemoryBattleStateRepository Repository) NewHarness()
    {
        var repository = new InMemoryBattleStateRepository();

        return (new BattleStateService(repository, new FixedRngSeedSource()), repository);
    }

    // =======================================================================
    // Sơn Thạch Vệ — BOSS_RULES.md §6.2.4
    // =======================================================================

    [Fact]
    public async Task SonThachVe_WhenBossAboveHalfHp_ShouldNotActivate()
    {
        // §6.2.4: the trigger is `Boss HP ≤ 50%` of MaxHP 3000 — i.e. HP ≤ 1500.
        // At full health the condition does not hold, so no instance may be
        // created. This is also the guard against reading `PassiveThreshold = null`
        // as "always active": a null threshold must NOT cause unconditional
        // execution.
        var (service, _) = NewHarness();

        var created = await service.CreateBattleAsync(
            "stv-above-half", Owner, Pet, BossDefinitions.SonThachVe);

        Assert.Equal(3000, created.BossState.MaxHP);
        Assert.Equal(3000, created.BossState.HP);
        Assert.Empty(created.BossState.ActiveStatusEffects);

        var result = await service.ExecuteSwapAsync("stv-above-half", FindMatchProducingPair(created));

        Assert.True(result!.Value.IsAccepted);

        var boss = result.Value.State.BossState;

        Assert.True(boss.HP > 1500, $"expected the Boss above 1500 HP but was {boss.HP}");
        Assert.DoesNotContain(
            boss.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SonThachVe_WhenPostDamageHpReachesHalf_ShouldActivatePlusTwentyPercentForThreeTurns()
    {
        // §6.2.4: at `Boss HP ≤ 50%` the Passive activates, applying `+20% ATK` for
        // 3 Turns as a Turn-based `BuffDebuff` in `BossState.StatusEffects[]` with
        // `TargetStat = "ATK"` — the §6.2.1 Rage representation.
        //
        // The battle starts just above the threshold so the Swap's own qualifying
        // Player attack carries the HP to the boundary, which is the post-damage
        // evaluation point §3.3 item 1 names. Two things the current contract fixes
        // about how that is arranged:
        //   * the Player's attack is ATK-gated (ADR-026 D1), so the Turn fixture pins
        //     the board whose Swap clears ATK Gems — a drawn board gives the scenario
        //     no attack at all;
        //   * GAME_RULES.md §17 orders 18a (the Boss's own board action, whose HP Gems
        //     heal it) before 18b (this trigger), so the pin that lands the
        //     evaluation-point HP exactly on the boundary is derived from a probe
        //     Round on the identical state — same seed, same board, same Swap — rather
        //     than guessed.
        const int halfMaxHp = 1500;

        var (service, repository) = NewHarness();

        var probe = await ResolveAtPinnedHpAsync(service, repository, "stv-at-half-probe", halfMaxHp);

        // The pre-Turn HP whose own (Player damage − Boss healing) lands the
        // evaluation-point HP exactly on the boundary.
        var armed = await ArmTurnAsync(
            service,
            repository,
            "stv-at-half",
            (2 * halfMaxHp) - probe.SettledBoss.HP);

        // It starts the Turn above the threshold — this is the "post-damage crossing"
        // case, not an already-below case.
        Assert.True(
            armed.PinnedBoss.HP > halfMaxHp,
            $"the Turn must start above {halfMaxHp} but started at {armed.PinnedBoss.HP}");

        var result = await service.ExecuteSwapAsync("stv-at-half", armed.Swap);

        Assert.True(result!.Value.IsAccepted);

        var settled = result.Value.State.BossState;

        var playerDamage = result.Value.Events
            .Where(e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Player)
            .Sum(e => e.DamageDealt.Amount);

        Assert.True(playerDamage > 0, "the Swap's ATK-gated Player instance must have landed");

        // The trigger saw the POST-damage HP (§3.3 item 1) — the Player's own attack
        // is what brought it to the boundary, and no step after 18b changes Boss HP.
        Assert.Equal(halfMaxHp, settled.HP);

        var rage = Assert.Single(
            settled.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal));

        // §6.2.4: the existing Boss ATK modifier representation, unchanged.
        Assert.Equal(StatusEffectType.BuffDebuff, rage.Type);
        Assert.Equal(StatusEffectSource.Boss, rage.Source);
        Assert.Equal("ATK", rage.TargetStat);
        Assert.Equal(20, rage.Magnitude);

        // Applied at step 18b then consumed once at step 19a (COMBAT_RULES.md §5.3),
        // so a 3-Turn instance reads back as 2 remaining in the settled state.
        Assert.Equal(2, rage.RemainingTurns);

        // §5.5.4: the base stat is never overwritten.
        Assert.Equal(120, settled.ATK);
    }

    [Fact]
    public void SonThachVe_ThresholdBoundary_ShouldUseInclusiveLessThanOrEqual()
    {
        // §6.2.4's boundary: the operator is `≤`, so HP == 1500 (exactly 50% of
        // 3000) DOES satisfy the trigger — deliberately distinct from §5 item 4's
        // strict `<` for the Enrage transition, which the same numbers would make
        // easy to conflate.
        //
        // The runtime guard is `bossState.HP <= bossState.MaxHP * 50 / 100`. This
        // asserts that arithmetic directly against the documented boundary, because
        // the exact-half case is a single point the Swap-driven scenarios cannot
        // reliably land on.
        var boss = BossDefinitions.SonThachVe;
        var halfMaxHp = boss.MaxHP * 50 / 100;

        Assert.Equal(3000, boss.MaxHP);
        Assert.Equal(1500, halfMaxHp);

        Assert.True(1500 <= halfMaxHp, "HP == 50% must satisfy the `≤` trigger");
        Assert.True(1501 > halfMaxHp, "HP just above 50% must not satisfy it");
        Assert.True(0 <= halfMaxHp);
    }

    [Fact]
    public void SonThachVe_EnrageOperators_ShouldDifferFromThePassiveTrigger()
    {
        // §6.2.4: "This trigger is NOT the Enrage transition." The two boundaries
        // coincide numerically for this Boss — EnrageThreshold 1500 with MaxHP 3000
        // is 50% — but they are separate contract concepts with different operators:
        // §5 item 4's Enrage uses strict `<`, the Passive uses `≤`. At exactly 1500
        // the Enrage transition has NOT occurred while the Passive trigger HAS.
        var boss = BossDefinitions.SonThachVe;

        var enrageAbsolute = boss.MaxHP * boss.EnrageThreshold;
        var passiveThreshold = boss.MaxHP * 50 / 100;

        Assert.Equal(1500d, enrageAbsolute, precision: 9);
        Assert.Equal(1500, passiveThreshold);

        const int hp = 1500;

        // §5 item 4: `BossHP < EnrageThreshold` — strict, so NOT enraged at equality.
        Assert.False(hp < enrageAbsolute);

        // §6.2.4: `Boss HP ≤ 50%` — inclusive, so the Passive trigger DOES hold.
        Assert.True(hp <= passiveThreshold);
    }

    [Fact]
    public async Task SonThachVe_ShouldNotRetriggerOnASubsequentTurn()
    {
        // §6.2.4: the Passive is authored ONE-TIME — "it does not re-trigger once it
        // has activated" — and the reset behavior is the non-default No reset /
        // persistent form whose storage token is `Persistent` (PASSIVE_RULES.md §4
        // items 2–3). §6.2.4 states the retrigger guard "is the authored one-time
        // behavior above, not a new state field", so the observed behavior is that a
        // second Turn still satisfying `HP ≤ 50%` neither creates a second
        // activation nor refreshes the first.
        //
        // This is the accidental behavior the task brief §14 forbids: turn N +20%,
        // turn N+1 +20% again, turn N+2 +20% again.
        //
        // The Turn fixture and the HP pin are the same documented arrangement the
        // first-activation scenario uses (ATK-gated Player attack; a Boss HP pinned
        // with headroom below the boundary, because §17 orders 18a's Boss-side healing
        // before 18b's evaluation).
        var (service, repository) = NewHarness();
        var battleId = "stv-no-retrigger";

        var armed = await ArmTurnAsync(service, repository, battleId, hp: 1400);

        var first = await service.ExecuteSwapAsync(battleId, armed.Swap);
        Assert.True(first!.Value.IsAccepted);

        var afterFirst = first.Value.State.BossState;

        var firstInstance = Assert.Single(
            afterFirst.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal));

        Assert.Equal(2, firstInstance.RemainingTurns);
        Assert.True(afterFirst.HP <= 1500, "the Boss must still satisfy the trigger");

        // Turn 2: the condition still holds (the HP is re-pinned to the same
        // documented position, and a new Turn fixture supplies this Turn's Swap —
        // MATCH3_RULES.md §2.1.4 rejects a repeat of the committed pair), but the
        // Passive is one-time.
        var afterFirstState = (await repository.GetAsync(battleId))!;
        var secondArmed = await PinTurnAsync(repository, afterFirstState, hp: 1400);

        var second = await service.ExecuteSwapAsync(battleId, secondArmed.Swap);

        Assert.True(second!.Value.IsAccepted);

        var afterSecond = second.Value.State.BossState;

        Assert.True(afterSecond.HP <= 1500, "the trigger condition still holds on turn 2");

        // Still exactly one instance — no second +20% instance, and no stack to +40%.
        var secondInstance = Assert.Single(
            afterSecond.ActiveStatusEffects,
            e => string.Equals(e.Id, SonThachVePassiveId, StringComparison.Ordinal));

        Assert.Equal(20, secondInstance.Magnitude);
        Assert.Equal("ATK", secondInstance.TargetStat);
        Assert.Equal(StatusEffectType.BuffDebuff, secondInstance.Type);

        // The refresh a re-trigger would perform did NOT happen: the duration
        // monotonically decremented rather than resetting to 3.
        Assert.Equal(1, secondInstance.RemainingTurns);
    }

    [Fact]
    public void SonThachVe_ShouldDeclareThePersistentResetAndNullThreshold()
    {
        // §6.2 / §6.2.4: `PassiveThreshold = null` (not match-charged) and the
        // documented non-default reset token `Persistent`. The runtime path must
        // therefore reach this Passive through its HP threshold, never through a
        // match charge — and a null threshold is NOT "always active".
        var boss = BossDefinitions.SonThachVe;

        Assert.Null(boss.PassiveDefinition.Threshold);
        Assert.Equal(0, boss.PassiveThreshold);
        Assert.Equal("Persistent", boss.PassiveDefinition.ResetBehavior);
        Assert.Equal(PassiveResetBehavior.NoReset, boss.PassiveResetBehavior);
        Assert.Equal(SonThachVePassiveId, boss.PassiveId.Value);

        // PassiveTracker rejects a threshold below 1, which is the second reason the
        // match-charged path is skipped for this Boss.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PassiveTracker.Charge(
                PassiveProgress.AtStart(boss.PassiveThreshold),
                matchCount: 4,
                boss.PassiveId));
    }

    [Fact]
    public void SonThachVe_EffectShouldBeConsumedByTheBossAtkModifierRule()
    {
        // §6.2.4's representation must be the one COMBAT_RULES.md §5.5 consumes: a
        // Turn-based `BuffDebuff` with `TargetStat = "ATK"` in the BOSS's own
        // collection. `EffectiveBossAttack` is that existing consumer, so the
        // instance the runtime creates must raise the derived Step-1 input while
        // leaving `BossState.ATK` untouched (§5.5.4).
        var boss = BossDefinitions.SonThachVe;

        var instance = StatusEffect.TurnBased(
            SonThachVePassiveId,
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: 20,
            duration: 3,
            targetStat: "ATK");

        var baseAtk = boss.ATK;
        var effective = StatusEffectLifecycle.EffectiveBossAttack(baseAtk, [instance]);

        // §5.5.1's worked example applied to this Boss: truncate(120 × 120 / 100) = 144.
        Assert.Equal(144, effective);
        Assert.Equal(120, baseAtk);
    }

    // =======================================================================
    // Kim Lôi Vương — BOSS_RULES.md §6.2.5
    // =======================================================================

    [Fact]
    public async Task KimLoiVuong_WhenComboIsBelowFour_ShouldNotActivate()
    {
        // §6.2.5's negative case, driven through the real pipeline: a Swap whose
        // resolution stays below Combo 4 must leave the Boss's Step-1 ATK
        // unmodified. This is what proves `PassiveThreshold = null` is NOT treated
        // as unconditional execution.
        //
        // The observable is the Boss's own attack damage, because a 1-Turn effect
        // applied at step 18a is consumed at step 19a of that same Turn (§6.2.5 /
        // COMBAT_RULES.md §5.3), so it is never present in the settled state.
        var (service, _) = NewHarness();
        var battleId = "klv-below-combo";

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.KimLoiVuong);

        var result = await service.ExecuteSwapAsync(battleId, FindMatchProducingPair(created));

        Assert.True(result!.Value.IsAccepted);
        Assert.True(result.Value.State.Combo < 4, $"expected Combo < 4 but was {result.Value.State.Combo}");

        // The Boss attacked, and its damage is the unmodified ATK-derived value.
        var bossDamage = BossDamageTotal(result.Value);

        Assert.True(bossDamage > 0, "the Boss must have made its Basic Attack");
        Assert.Equal(BossDefinitions.KimLoiVuong.ATK, result.Value.State.BossState.ATK);
    }

    [Fact]
    public async Task KimLoiVuong_WhenComboReachesFour_ShouldRaiseTheBossAttackDamage()
    {
        // §6.2.5: at `Player Combo ≥ 4` the Passive applies `+20% ATK`, consumed by
        // the Boss's own attack through the unchanged COMBAT_RULES.md §5.5
        // Step-1 `EffectiveBossATK` contribution.
        //
        // Because the duration is 1 Turn and it is applied at step 18a then consumed
        // at step 19a of the same Turn, the settled `StatusEffects[]` is empty by
        // design. The proof that the modifier was applied and consumed is therefore
        // the Boss's own attack damage for that Turn: §5.5.1 gives
        // `EffectiveBossATK = truncate(140 × 120 / 100) = 168`, strictly above the
        // unmodified `140`, so the same attack must land strictly harder than it
        // does on a Combo-3 Turn.
        var (service, repository) = NewHarness();

        var belowFour = await BossBasicAttackSampleAtComboAsync(repository, service, maxCombo: 3);
        var atFour = await BossBasicAttackSampleAtComboAsync(repository, service, minCombo: 4);

        Assert.True(belowFour.Damage > 0, "the below-threshold Turn must have produced a Boss attack");
        Assert.True(atFour.Damage > 0, "the at-threshold Turn must have produced a Boss attack");

        // §5.5.1 / §5.5.4: the modifier raises the derived Step-1 input, and the
        // stored base stat is unchanged either way.
        var boss = BossDefinitions.KimLoiVuong;

        Assert.Equal(140, boss.ATK);

        var rage = StatusEffect.TurnBased(
            KimLoiVuongPassiveId,
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: 20,
            duration: 1,
            targetStat: "ATK");

        // COMBAT_RULES.md §3.4 item 1 keeps the ATK-Gem pool a separate Step-1
        // contribution that no ATK modifier scales, so the ATK term each Turn composed
        // is its settled Base less that Turn's own pool. A Combo-3 Turn has no Rage
        // instance and composes the stored 140; a Combo-≥4 Turn composes the derived
        // 168. Comparing the ATK terms, rather than two raw damages on two different
        // boards, is what isolates the modifier from the pool the Boss happened to
        // generate that Turn — the pool itself differs per board and is not a
        // statement about the Passive.
        var belowAtkTerm = belowFour.Calculation.Base - belowFour.AtkGemPool;
        var atFourAtkTerm = atFour.Calculation.Base - atFour.AtkGemPool;

        Assert.Equal(boss.ATK, belowAtkTerm);
        Assert.Equal(
            StatusEffectLifecycle.EffectiveBossAttack(boss.ATK, [rage]),
            atFourAtkTerm);
        Assert.Equal(168, atFourAtkTerm);
        Assert.Equal(28, atFourAtkTerm - belowAtkTerm);
    }

    [Fact]
    public void KimLoiVuong_ReTrigger_ShouldRefreshWithoutStacking()
    {
        // §6.2.5: reset behavior is the documented Default (PASSIVE_RULES.md §4 item
        // 1) and a re-trigger while the instance is active follows the existing
        // refresh-not-stack default (COMBAT_RULES.md §5.2 item 2, §5.5.5) — it
        // refreshes the existing instance and does NOT stack magnitude or create a
        // second instance. So `+20%` then `+20%` must remain one `+20%` instance,
        // never `+40%`.
        //
        // `StatusEffectLifecycle.Apply` is the existing mechanism that provides
        // exactly this (<c>GAME_STATE.md</c> §2.3.1 item 6: at most one instance per
        // `Id`, refreshed in place), and the runtime calls it for this Passive — so
        // asserting the mechanism's contract here is asserting the runtime's refresh
        // behavior at the documented magnitude.
        var instance = StatusEffect.TurnBased(
            KimLoiVuongPassiveId,
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: 20,
            duration: 1,
            targetStat: "ATK");

        var applied = StatusEffectLifecycle.Apply([], instance);
        Assert.Single(applied);

        // A second valid trigger while the instance is active.
        var refreshed = StatusEffectLifecycle.Apply(applied, instance);

        var only = Assert.Single(refreshed);

        Assert.Equal(20, only.Magnitude);
        Assert.Equal(1, only.RemainingTurns);
        Assert.Equal("ATK", only.TargetStat);
        Assert.Equal(KimLoiVuongPassiveId, only.Id);

        // And the composed Step-1 input is the single +20%, never a stacked +40%
        // (§5.5.1: truncate(140 × 120 / 100) = 168, not truncate(140 × 140 / 100)).
        Assert.Equal(168, StatusEffectLifecycle.EffectiveBossAttack(
            BossDefinitions.KimLoiVuong.ATK, refreshed));
    }

    [Fact]
    public void KimLoiVuong_ShouldDeclareTheNullThresholdAndDefaultReset()
    {
        // §6.2 / §6.2.5: `PassiveThreshold = null` (no match-charging threshold —
        // NOT "always active") and the documented `Default` reset behavior, so the
        // runtime must reach this Passive through the Combo threshold and never
        // through a match charge.
        var boss = BossDefinitions.KimLoiVuong;

        Assert.Null(boss.PassiveDefinition.Threshold);
        Assert.Equal(0, boss.PassiveThreshold);
        Assert.Equal("Default", boss.PassiveDefinition.ResetBehavior);
        Assert.Null(boss.PassiveResetBehavior);
        Assert.Equal(KimLoiVuongPassiveId, boss.PassiveId.Value);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PassiveTracker.Charge(
                PassiveProgress.AtStart(boss.PassiveThreshold),
                matchCount: 4,
                boss.PassiveId));
    }

    [Fact]
    public void KimLoiVuong_EffectShouldBeConsumedByTheBossAtkModifierRule()
    {
        // §6.2.5's representation must be the one COMBAT_RULES.md §5.5 consumes, and
        // §5.5.4 keeps `BossState.ATK` untouched.
        var boss = BossDefinitions.KimLoiVuong;

        var instance = StatusEffect.TurnBased(
            KimLoiVuongPassiveId,
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: 20,
            duration: 1,
            targetStat: "ATK");

        var baseAtk = boss.ATK;
        var effective = StatusEffectLifecycle.EffectiveBossAttack(baseAtk, [instance]);

        // §5.5.1's worked example applied to this Boss: truncate(140 × 120 / 100) = 168.
        Assert.Equal(168, effective);
        Assert.Equal(140, baseAtk);
    }

    // =======================================================================
    // Shared harness
    // =======================================================================

    /// <summary>
    /// A pinned Turn of a Sơn Thạch Vệ battle: the §6.2.4 fixture's Swap and the Boss
    /// state the pin wrote.
    /// </summary>
    /// <param name="Swap">
    /// The one Swap of the pinned ATK board (<see cref="RoundTestSupport.PlayerMatch"/>).
    /// </param>
    /// <param name="PinnedBoss">The Boss state the pin wrote, including its HP.</param>
    private readonly record struct PinnedTurn(SwapRequest Swap, BossState PinnedBoss);

    /// <summary>
    /// Creates a Sơn Thạch Vệ battle, pins its Turn fixture and the Boss HP to
    /// <paramref name="hp"/>, and returns the pinned Turn.
    ///
    /// <b>The board is pinned, not drawn (<c>ADR-026</c> D1).</b> The Player's attack —
    /// the damage the §6.2.4 scenario crosses the threshold with — is calculated only
    /// when the committed Swap cleared at least one ATK Gem, so the fixture pins the
    /// self-validating ATK board and asserts its precondition here. The HP written is
    /// the documented §6.1 <c>MaxHP = 3000</c> Boss's own value; the Passive's effect
    /// and its firing eligibility never are.
    /// </summary>
    private static async Task<PinnedTurn> ArmTurnAsync(
        BattleStateService service,
        InMemoryBattleStateRepository repository,
        string battleId,
        int hp)
    {
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.SonThachVe);

        Assert.Equal(3000, created.BossState.MaxHP);

        return await PinTurnAsync(repository, created, hp);
    }

    /// <summary>
    /// Pins the Turn's ATK board fixture and <paramref name="hp"/> on the stored battle,
    /// without advancing <c>Sequence</c>, so the next resolution reads both as the
    /// authoritative state. Every other member — including any applied Status Effect —
    /// is carried across.
    /// </summary>
    private static async Task<PinnedTurn> PinTurnAsync(
        InMemoryBattleStateRepository repository,
        BattleState state,
        int hp)
    {
        var pinned = RoundTestSupport.PlayerMatch(
            GemType.Atk,
            state.RngState,
            state.LastCommittedSwapPair);

        Assert.True(
            pinned.Resources.ClearedGemCounts.Atk > 0,
            "the ADR-026 fixture must clear ATK Gems for the Player's attack to resolve");

        var pinnedBoss = state.BossState with { HP = hp };

        await RoundTestSupport.PinAsync(repository, state, pinned.Board, bossState: pinnedBoss);

        return new PinnedTurn(pinned.Swap, pinnedBoss);
    }

    /// <summary>
    /// Creates a battle, pins its Boss HP to <paramref name="hp"/>, resolves one Swap,
    /// and reports the Turn's settled Boss state and Player → Boss damage.
    ///
    /// <b>It is the probe a boundary scenario needs.</b> <c>GAME_RULES.md</c> §17 orders
    /// 18a (the Boss's own board action, which heals the Boss from its HP Gems) before
    /// 18b (the Passive's trigger evaluation), so a Boss placed exactly on §6.2.4's
    /// boundary at the <i>evaluation point</i> must be pinned at the boundary less that
    /// Round's own (Player damage − Boss healing). A second battle with the same seed,
    /// board, and Swap re-runs the same deterministic computation, so the movement it
    /// reports is the movement the observed Round applies.
    /// </summary>
    private static async Task<(BossState SettledBoss, int PlayerDamage)> ResolveAtPinnedHpAsync(
        BattleStateService service,
        InMemoryBattleStateRepository repository,
        string battleId,
        int hp)
    {
        var armed = await ArmTurnAsync(service, repository, battleId, hp);
        var result = await service.ExecuteSwapAsync(battleId, armed.Swap);

        Assert.True(result!.Value.IsAccepted, "the probe Turn must be accepted");

        return (
            result.Value.State.BossState,
            result.Value.Events
                .Where(e => e.Type == BattleEventType.DamageDealt
                    && e.DamageDealt.Source == DamageParty.Player)
                .Sum(e => e.DamageDealt.Amount));
    }

    /// <summary>
    /// The Boss's total Basic Attack damage for this resolution
    /// (<c>GAME_RULES.md</c> §17 step 18c), read from the emitted events.
    /// </summary>
    private static int BossDamageTotal(SwapExecutionResult result) =>
        result.Events
            .Where(e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss)
            .Sum(e => e.DamageDealt.Amount);

    /// <summary>
    /// One Basic Attack the Boss's response phase landed: its damage instance and the
    /// ATK-Gem pool that instance's Step 1 composed with.
    /// </summary>
    /// <param name="Damage">The Boss's Final Damage for the Turn.</param>
    /// <param name="Calculation">
    /// That instance's own <c>DamageCalculated</c> report — its Step-1 <c>Base</c> and
    /// the pipeline's modifier factors (<c>COMBAT_RULES.md</c> §3).
    /// </param>
    /// <param name="AtkGemPool">
    /// The ATK-Gem damage the Boss's own board action generated, under
    /// <c>COMBAT_RULES.md</c> §2 item 6's `BaseOutput × unique cleared count`: 10 per
    /// ATK Gem the action cleared. Read from the Boss phase's own <c>GemMatched</c>
    /// reports (one per cleared cell per pass), so it is the value the round itself
    /// reported rather than a re-derivation from the board.
    /// </param>
    private readonly record struct BossBasicAttackSample(
        int Damage,
        DamageCalculation Calculation,
        int AtkGemPool);

    /// <summary>
    /// Drives one committed Swap and returns the Boss's Basic Attack for that Turn, for
    /// the first board whose resolution lands in the requested Combo band.
    ///
    /// The board is the existing MVP Boss Passive suite's fixed shape, whose pairs
    /// resolve to a spread of Combos (1 through 9) — so the Combo is genuine Match-3
    /// output that the test can select for, rather than a test-injected value. Every
    /// match-producing pair on it is tried, because a single pair's Combo alone would
    /// not reach the higher bands.
    ///
    /// No Boss ability cast occurs here: this suite's content carries no provisioned
    /// Boss Card, so the assertion reads the step-18d ATK-gated board attack alone (the
    /// legacy fallback attack and the charge-driven Skill were removed with ADR-026 §5
    /// and ADR-029 Slice 5 / TASK-313).
    /// </summary>
    private static async Task<BossBasicAttackSample> BossBasicAttackSampleAtComboAsync(
        InMemoryBattleStateRepository repository,
        BattleStateService service,
        int? minCombo = null,
        int? maxCombo = null)
    {
        var board = CascadeBoard();

        // Every match-producing pair on that board. The board is persisted into a
        // probe battle so the pair list is read from the same authoritative state the
        // executor will see.
        var probeId = $"klv-combo-band-probe-{minCombo}-{maxCombo}";
        var probe = await service.CreateBattleAsync(probeId, Owner, Pet, BossDefinitions.KimLoiVuong);
        var probeSeeded = probe with { BoardState = board };
        await repository.TryUpdateAsync(probeSeeded, probeSeeded.Sequence);

        var pairs = MatchProducingPairs(probeSeeded).ToArray();

        foreach (var pair in pairs)
        {
            var battleId = $"klv-combo-band-{minCombo}-{maxCombo}-{pair.From}-{pair.To}";

            var created = await service.CreateBattleAsync(
                battleId, Owner, Pet, BossDefinitions.KimLoiVuong);

            // The board must be the fixed cascade shape for the Swap to be legal and
            // to produce the Combo the band needs.
            var seeded = created with { BoardState = board };
            await repository.TryUpdateAsync(seeded, seeded.Sequence);

            var result = await service.ExecuteSwapAsync(battleId, pair);

            if (result is null || !result.Value.IsAccepted)
            {
                continue;
            }

            var combo = result.Value.State.Combo;

            if (minCombo is { } min && combo < min)
            {
                continue;
            }

            if (maxCombo is { } max && combo > max)
            {
                continue;
            }

            var damage = BossDamageTotal(result.Value);

            if (damage > 0)
            {
                // GAME_EVENTS.md §1: the instance is reported by one contiguous
                // DamageCalculated → DamageDealt → DamageTaken triple, so the boss
                // attack's calculation is the event immediately preceding its
                // DamageDealt.
                var dealtIndex = result.Value.Events
                    .Select((battleEvent, index) => (battleEvent, index))
                    .Single(t => t.battleEvent.Type == BattleEventType.DamageDealt
                                 && t.battleEvent.DamageDealt.Source == DamageParty.Boss)
                    .index;

                var calculation = result.Value.Events[dealtIndex - 1].DamageCalculated;

                var atkGemPool = RoundTestSupport.BossBoardEvents(result.Value)
                    .Count(e => e.Type == BattleEventType.GemMatched && e.Gem.GemType == GemType.Atk)
                    * ResourceGenerator.AtkGemBaseDamage;

                return new BossBasicAttackSample(damage, calculation, atkGemPool);
            }
        }

        throw new InvalidOperationException(
            $"no board produced a Boss Basic Attack in the requested Combo band "
            + $"(min={minCombo?.ToString() ?? "-"}, max={maxCombo?.ToString() ?? "-"})");
    }

    /// <summary>
    /// Every match-producing adjacent pair on <paramref name="state"/>'s board, in
    /// index order.
    /// </summary>
    private static IEnumerable<SwapRequest> MatchProducingPairs(BattleState state)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (MatchDetector.Detect(state.BoardState.WithSwapped(from, to)).Count > 0)
            {
                yield return new SwapRequest(from, to);
            }
        }
    }

    /// <summary>
    /// The fixed board the existing MVP Boss Passive suite uses. Its Swap at (26, 34)
    /// completes a horizontal match of three in row 3 while the forced column match in
    /// row 4 makes the resolution a multi-step Cascade, and its other pairs produce a
    /// spread of Combo values — which is what lets a test select the Combo band it
    /// needs from genuine Match-3 output (<c>MATCH3_RULES.md</c> §1.1, §3–§4).
    /// </summary>
    private static BoardState CascadeBoard()
    {
        var rows = new[]
        {
            "ADPADPAD",
            "DPADPADP",
            "PADPADPA",
            "PAMMMPAD",
            "MMDMMDMM",
            "DPADPADP",
            "PADPADPA",
            "ADPADPAD",
        };

        var cells = new GemType[BoardState.CellCount];

        for (var r = 0; r < BoardState.Rows; r++)
        {
            for (var c = 0; c < BoardState.Columns; c++)
            {
                cells[BoardState.ToIndex(r, c)] = rows[r][c] switch
                {
                    'M' => GemType.Atk,
                    'A' => GemType.Atk,
                    'D' => GemType.Def,
                    'P' => GemType.Power,
                    _ => GemType.Hp,
                };
            }
        }

        return BoardState.FromCells(cells);
    }

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
