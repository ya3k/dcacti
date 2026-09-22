using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Resolve Player Effects tests (<c>GAME_RULES.md</c> §17 step 14,
/// <c>COMBAT_RULES.md</c> §4 item 1).
///
/// <code>
/// Rule (COMBAT_RULES.md §4 item 1)
///  ↓
/// Scenario (Given HP and a HealPool, When healing resolves, Then HP is …)
///  ↓
/// Test
/// </code>
///
/// The rule under test is one sentence, and every expected value below is derived
/// from it rather than from the implementation:
///
/// <code>
/// "Heal effects restore HP up to Max HP; overheal is discarded unless a Relic
///  explicitly grants overheal/temp-HP."            — COMBAT_RULES.md §4 item 1
///
/// HP = min(HP + HealPool, MaxHP)
/// </code>
///
/// No Relic grants overheal in MVP (<c>COMBAT_RULES.md</c> §4 item 4 notes Heal
/// modifiers exist but none is implemented), so the clamp is the whole rule and
/// nothing here asserts an overheal path.
///
/// <b>Two levels are covered, deliberately.</b> The <see cref="ResourceGenerator.ApplyHeal"/>
/// cases drive the write directly, so the arithmetic — including the exact-boundary
/// cases a board fixture cannot place exactly — is pinned without depending on what
/// a cascade spawns. The <see cref="SwapExecutor"/> cases then prove the write is
/// wired into the documented resolution position and that a real HP-Gem Match moves
/// the persistent <c>PlayerState.HP</c>.
///
/// <b>HealPool is transient.</b> A HealPool that the Swap did not generate is
/// supplied only as the input of a direct call; it is never stored on the state,
/// and the boards below read the pool off <c>SwapExecutionResult.Resources</c>
/// (<c>GAME_STATE.md</c> §3).
/// </summary>
public class PlayerEffectHealingTests
{
    private const ulong TestSeed = 20260816UL;

    private static int I(int row, int column) => TestBoard.I(row, column);

    private static BattleState BattleWith(BoardState board, PlayerState? player = null) =>
        BattleState.CreateWith("battle-019", TestSeed) with
        {
            BoardState = board,
            PlayerState = player ?? PlayerState.Initial,
        };

    /// <summary>
    /// A generation carrying only a HealPool — the transient input of the
    /// player-effects step, isolated from the other three pools so a failure names
    /// healing rather than a neighbouring conversion.
    /// </summary>
    private static ResourceGeneration HealOnly(int healPool) =>
        new(BaseDamagePool: 0, DefensePool: 0, HealPool: healPool, Power: 0, ClearedGemCounts.None);

    // =======================================================================
    // 1. HP + HealPool < MaxHP — the full pool is applied
    // =======================================================================

    [Fact]
    public void ApplyHeal_ShouldIncreaseHpByTheWholePoolWhenItFitsUnderMaxHp()
    {
        // COMBAT_RULES.md §4 item 1: the pool is applied in full while HP stays
        // "up to Max HP". 600 + 150 = 750, strictly below the 1000 ceiling, so no
        // part of the pool is discarded.
        var state = PlayerState.Initial with { HP = 600, MaxHP = 1000 };

        var updated = ResourceGenerator.ApplyHeal(state, HealOnly(150));

        Assert.Equal(750, updated.HP);
        Assert.Equal(1000, updated.MaxHP);
    }

    [Fact]
    public void ApplyHeal_ShouldHealFromOneHpBelowTheCeiling()
    {
        // The largest pool that still fits: 999 + 1 = 1000 = MaxHP. §4 item 1
        // restores HP "up to Max HP", so landing exactly on the ceiling is not
        // overheal and the pool is not trimmed.
        var state = PlayerState.Initial with { HP = 999, MaxHP = 1000 };

        var updated = ResourceGenerator.ApplyHeal(state, HealOnly(1));

        Assert.Equal(1000, updated.HP);
    }

    // =======================================================================
    // 2. HP + HealPool = MaxHP — exactly the ceiling
    // =======================================================================

    [Fact]
    public void ApplyHeal_ShouldLandExactlyOnMaxHpWhenThePoolClosesTheGap()
    {
        // §4 item 1: 400 + 600 = 1000 = MaxHP. The boundary is inclusive — the
        // heal is not reduced to leave a point of room.
        var state = PlayerState.Initial with { HP = 400, MaxHP = 1000 };

        var updated = ResourceGenerator.ApplyHeal(state, HealOnly(600));

        Assert.Equal(1000, updated.HP);
        Assert.Equal(state.MaxHP, updated.HP);
    }

    [Fact]
    public void ApplyHeal_ShouldLandOnMaxHpForANonDefaultCeiling()
    {
        // MaxHP is not a fixed 1000: COMBAT_RULES.md §1.1 states the MVP defaults
        // "are not permanent invariants — future Pet progression (Level, Star, Tier)
        // may produce different actual Battle Stats". The clamp therefore reads the
        // state's own MaxHP rather than any constant. 250 + 250 = 500 = MaxHP.
        var state = new PlayerState(
            HP: 250,
            MaxHP: 500,
            ATK: 50,
            DEF: 25,
            Power: 0,
            Crit: 5,
            Combo: 0,
            MatchCount: 0);

        var updated = ResourceGenerator.ApplyHeal(state, HealOnly(250));

        Assert.Equal(500, updated.HP);
    }

    // =======================================================================
    // 3. HP + HealPool > MaxHP — overheal is discarded
    // =======================================================================

    [Fact]
    public void ApplyHeal_ShouldClampToMaxHpAndDiscardOverheal()
    {
        // §4 item 1: "overheal is discarded unless a Relic explicitly grants
        // overheal/temp-HP". No Relic does in MVP, so 400 + 10000 stops at 1000 —
        // and the excess is stored nowhere: it is not carried forward, and no other
        // field of the state holds it.
        var state = PlayerState.Initial with { HP = 400, MaxHP = 1000 };

        var updated = ResourceGenerator.ApplyHeal(state, HealOnly(10_000));

        Assert.Equal(1000, updated.HP);

        // Discarded means discarded: every other field is untouched, so the excess
        // did not leak into one of them.
        Assert.Equal(state.MaxHP, updated.MaxHP);
        Assert.Equal(state.ATK, updated.ATK);
        Assert.Equal(state.DEF, updated.DEF);
        Assert.Equal(state.Power, updated.Power);
        Assert.Equal(state.Crit, updated.Crit);
        Assert.Equal(state.Combo, updated.Combo);
        Assert.Equal(state.MatchCount, updated.MatchCount);

        // And a second application of the same pool heals nothing, because there is
        // no stored overheal to spend.
        var again = ResourceGenerator.ApplyHeal(updated, HealOnly(10_000));
        Assert.Equal(1000, again.HP);
    }

    [Fact]
    public void ApplyHeal_ShouldClampToMaxHpWhenTheOverhealIsASinglePoint()
    {
        // The smallest overshoot: 999 + 2 = 1001, one point over the ceiling.
        var state = PlayerState.Initial with { HP = 999, MaxHP = 1000 };

        var updated = ResourceGenerator.ApplyHeal(state, HealOnly(2));

        Assert.Equal(1000, updated.HP);
    }

    // =======================================================================
    // 4. HealPool = 0 — no HP Gem was cleared
    // =======================================================================

    [Fact]
    public void ApplyHeal_ShouldLeaveHpUnchangedWhenThePoolIsZero()
    {
        // The ordinary case of a Swap that cleared no HP Gem: nothing was
        // generated, so nothing is applied. HP is unchanged — not reset, not
        // re-derived from MaxHP.
        var state = PlayerState.Initial with { HP = 375, MaxHP = 1000 };

        var updated = ResourceGenerator.ApplyHeal(state, HealOnly(0));

        Assert.Equal(375, updated.HP);
        Assert.Equal(state, updated);
    }

    [Fact]
    public void ApplyHeal_ShouldLeaveAHurtPlayerUnchangedWhenThePoolIsZero()
    {
        // The value a damage stage would have left (§3 owns damage, which is not
        // this stage) is carried across a Swap that heals nothing.
        var state = PlayerState.Initial with { HP = 1, MaxHP = 1000 };

        var updated = ResourceGenerator.ApplyHeal(state, HealOnly(0));

        Assert.Equal(1, updated.HP);
    }

    // =======================================================================
    // 5. HP already at MaxHP — nothing to restore, whatever the pool is
    // =======================================================================

    [Fact]
    public void ApplyHeal_ShouldLeaveHpAtMaxHpWhenAlreadyFull()
    {
        // §4 item 1's ceiling stated for a full-health player: HP is already "up to
        // Max HP", so a pool of any size changes nothing.
        var state = PlayerState.Initial with { HP = 1000, MaxHP = 1000 };

        Assert.Equal(1000, ResourceGenerator.ApplyHeal(state, HealOnly(0)).HP);
        Assert.Equal(1000, ResourceGenerator.ApplyHeal(state, HealOnly(20)).HP);
        Assert.Equal(1000, ResourceGenerator.ApplyHeal(state, HealOnly(1_000_000)).HP);
    }

    [Fact]
    public void ApplyHeal_ShouldLeaveAnAlreadyFullStateUnchanged()
    {
        // The whole state is unchanged, not only HP: the clamp returns the same
        // value rather than a rebuilt one.
        var state = PlayerState.Initial with { HP = 1000, MaxHP = 1000 };

        Assert.Equal(state, ResourceGenerator.ApplyHeal(state, HealOnly(500)));
    }

    // =======================================================================
    // 6. Only HP changes, and healing draws no RNG
    // =======================================================================

    [Fact]
    public void ApplyHeal_ShouldChangeOnlyHp()
    {
        // The write-back replaces PlayerState wholesale (GAME_STATE.md §5.1), so
        // every field this step does not own has to be carried across rather than
        // reinitialized. Power in particular belongs to the generation step that
        // ran before this one, and must survive it.
        var state = new PlayerState(
            HP: 777,
            MaxHP: 900,
            ATK: 61,
            DEF: 33,
            Power: 42,
            Crit: 9,
            Combo: 4,
            MatchCount: 12);

        var updated = ResourceGenerator.ApplyHeal(state, HealOnly(100));

        Assert.Equal(877, updated.HP);
        Assert.Equal(900, updated.MaxHP);
        Assert.Equal(61, updated.ATK);
        Assert.Equal(33, updated.DEF);
        Assert.Equal(42, updated.Power);
        Assert.Equal(9, updated.Crit);
        Assert.Equal(4, updated.Combo);
        Assert.Equal(12, updated.MatchCount);
    }

    [Fact]
    public void ApplyHeal_ShouldBeDeterministicForTheSameInputs()
    {
        // MATCH3_RULES.md §7.2 / AGENTS.md §11: healing is a pure function of the
        // previous state and the pool. It draws no RNG, reads no clock, and depends
        // on no enumeration order, so the same inputs always give the same state.
        var state = PlayerState.Initial with { HP = 500, MaxHP = 1000 };
        var generation = HealOnly(120);

        Assert.Equal(
            ResourceGenerator.ApplyHeal(state, generation),
            ResourceGenerator.ApplyHeal(state, generation));
    }

    // =======================================================================
    // 7. Healing inside the documented resolution
    // =======================================================================

    /// <summary>
    /// A board whose only Match is a horizontal run of exactly three HP Gems on
    /// row 4, completed by swapping the HP Gem at (3,2) into the gap at (4,2).
    ///
    /// Built from explicit rows — the run's own row and the completing Gem's — so
    /// the run is exactly three whatever the board would otherwise hold. Patching a
    /// shared background is what lets a run merge with a Gem beside it and silently
    /// become a Match 4, which would change the tier multiplier under test.
    /// </summary>
    private static BoardState HpMatch3Board() =>
        TestBoard.FromRows(
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            // The completing HP Gem at column 2; every other column breaks the row.
            "PAHHPADH",
            // Row 4: HP Gems at columns 0–1 and 3, the gap at 2, breakers beyond.
            "HHDPPDHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH");

    private static int From => I(3, 2); // the completing HP Gem
    private static int To => I(4, 2);   // the run's gap

    [Fact]
    public void CommittedSwap_ShouldHealThePlayerByTheGeneratedHealPool()
    {
        // GAME_RULES.md §17 steps 12 → 14: the HP Gems cleared during the cascade
        // generate a HealPool (COMBAT_RULES.md §2), and that pool is what the
        // player-effects step applies to HP (§4 item 1).
        //
        // Three HP Gems at the Match-3 tier generate 3 × 20 × 1.0× = 60 into the
        // pool, so a player starting 200 below full ends at HP + 60.
        var player = PlayerState.Initial with { HP = 800, MaxHP = 1000 };

        var result = SwapExecutor.Execute(BattleWith(HpMatch3Board(), player), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        // The first pass is the Match the fixture builds, so the pool is fully
        // determined by the test: 3 HP Gems at the Match-3 tier.
        var firstPassHeal = result.Resolution.Passes[0].ClearedGems
            .Where(g => g.GemType == GemType.Hp)
            .Sum(g => (ResourceGenerator.HpGemBaseHeal * MatchTierMultipliers.NumeratorFor(g.Tier))
                      / MatchTierMultipliers.Denominator);

        Assert.Equal(60, firstPassHeal);

        // The first pass is a Match 3 of HP Gems and nothing else: three cells, one
        // shape, the base tier. A fixture that had merged with a neighbour would be a
        // Match 4 and generate 4 × 20 × 1.5 = 120 instead.
        var firstPass = result.Resolution.Passes[0];
        Assert.Equal(3, firstPass.ClearedGems.Count);
        Assert.All(firstPass.ClearedGems, gem =>
        {
            Assert.Equal(GemType.Hp, gem.GemType);
            Assert.Equal(MatchTier.Match3, gem.Tier);
        });
        Assert.Equal(3, result.Resources.ClearedGemCounts.Hp);

        // The Swap's total pool is what the state applied — the pool accumulates
        // across cascades (§5.7 item 7) and the whole of it is applied once, in the
        // same resolution and the same write-back.
        Assert.True(result.Resources.HealPool >= 60, "The swap's own Match-3 always generates.");

        // Healed by exactly the pool, clamped to MaxHP. 800 + 60 = 860 fits, so no
        // part of it is discarded.
        Assert.Equal(
            Math.Min(800 + result.Resources.HealPool, 1000),
            result.State.PlayerState.HP);
    }

    [Fact]
    public void CommittedSwap_ShouldNotExceedMaxHpHoweverLargeThePoolIs()
    {
        // §4 item 1's ceiling inside the real resolution: a nearly full player who
        // clears HP Gems cannot pass MaxHP, whatever the cascade generated.
        var player = PlayerState.Initial with { HP = 995, MaxHP = 1000 };

        var result = SwapExecutor.Execute(BattleWith(HpMatch3Board(), player), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.True(result.Resources.HealPool >= 60, "The swap generated healing to apply.");
        Assert.Equal(1000, result.State.PlayerState.HP);
    }

    [Fact]
    public void CommittedSwap_ShouldLeaveHpAtMaxHpWhenAlreadyFull()
    {
        // The state a battle starts in is at full health (COMBAT_RULES.md §1.1,
        // PlayerState.Initial), so the very common case is that a heal has nothing
        // to restore and HP stays exactly where it was.
        var result = SwapExecutor.Execute(BattleWith(HpMatch3Board()), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.Equal(PlayerState.Initial.HP, result.State.PlayerState.HP);
        Assert.Equal(PlayerState.Initial.MaxHP, result.State.PlayerState.MaxHP);
    }

    [Fact]
    public void CommittedSwapWithoutHpGems_ShouldLeaveHpUnchanged()
    {
        // A Swap that clears no HP Gem generates no HealPool, so the player-effects
        // step writes nothing: the HP a previous Swap left is carried forward
        // through the whole resolution.
        //
        // The fixture is a Match-3 of ATK Gems, which feeds Base Damage alone.
        var board = TestBoard.FromRows(
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            // The completing ATK Gem at column 2.
            "PAHAPADH",
            // Row 4: ATK Gems at columns 0–1 and 3, the gap at 2, breakers beyond.
            "AADAPDHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH");

        var player = PlayerState.Initial with { HP = 640, MaxHP = 1000 };

        var result = SwapExecutor.Execute(BattleWith(board, player), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.Equal(0, result.Resources.HealPool);
        Assert.Equal(640, result.State.PlayerState.HP);
    }

    [Fact]
    public void RejectedSwap_ShouldHealNothing()
    {
        // MATCH3_RULES.md §2.1.5 item 5: a rejected action writes nothing — no
        // Match, no Combo, no Power, and therefore no healing either.
        var player = PlayerState.Initial with { HP = 500, MaxHP = 1000 };

        var result = SwapExecutor.Execute(BattleWith(HpMatch3Board(), player), new SwapRequest(0, 63));

        Assert.True(result.IsRejected);
        Assert.Throws<InvalidOperationException>(() => result.Resources);
    }

    [Fact]
    public void ExistingStateWriteBack_ShouldCarryHealingInTheSameValue()
    {
        // GAME_STATE.md §5.1: one action, one write-back. HP is written in the same
        // state value as Power, the board, and the counters — there is no second
        // write and no state in which HP has been healed but Power has not.
        var player = PlayerState.Initial with { HP = 700, MaxHP = 1000 };

        var result = SwapExecutor.Execute(BattleWith(HpMatch3Board(), player), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        // Every write-back field moved together in this one value.
        Assert.Equal(1, result.State.Turn);
        Assert.Equal(1, result.State.Sequence);
        Assert.Equal(result.Resolution.Board, result.State.BoardState);
        Assert.Equal(result.Resolution.RngState, result.State.RngState);
        Assert.Equal(
            Math.Min(700 + result.Resources.HealPool, 1000),
            result.State.PlayerState.HP);

        // The result's own factories carry the healed state across unchanged, so a
        // later pipeline stage composing WithEvents/WithState cannot lose it.
        Assert.Equal(result.State.PlayerState.HP, result.WithEvents([.. result.Events]).State.PlayerState.HP);
        Assert.Equal(result.State.PlayerState.HP, result.WithState(result.State).State.PlayerState.HP);
    }

    [Fact]
    public void Healing_ShouldNotAddOrChangeAnyEvent()
    {
        // GAME_EVENTS.md §2 has no heal event, and §3 item 7 gives any new event to
        // its own owning task. Healing is therefore a state-only change: a Swap that
        // heals produces exactly the Match-3 events it always did.
        var result = SwapExecutor.Execute(BattleWith(HpMatch3Board()), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.NotEmpty(result.Events);
        Assert.All(result.Events, e => Assert.True(
            e.Type is BattleEventType.MatchCreated
                or BattleEventType.CascadeCreated
                or BattleEventType.ComboChanged
                or BattleEventType.GemMatched,
            $"Unexpected event type {e.Type}: healing adds no event."));
    }

    [Fact]
    public void HealPool_ShouldNotBeStoredInAnyState()
    {
        // GAME_STATE.md §3: the HealPool is Transient Resolution State. It is
        // consumed by the player-effects step but never stored — not on
        // PlayerState, and not on BattleState.
        var player = PlayerState.Initial with { HP = 900, MaxHP = 1000 };

        var result = SwapExecutor.Execute(BattleWith(HpMatch3Board(), player), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        // The pool exists for the resolution ...
        Assert.True(result.Resources.HealPool >= 60);

        // ... and reaches no field of either state: the applied HP is bounded by
        // MaxHP, and every other player field is exactly what the battle started
        // with. There is no member of PlayerState that could hold a pool of 60 or
        // more without one of these failing.
        Assert.Equal(
            Math.Min(900 + result.Resources.HealPool, 1000),
            result.State.PlayerState.HP);
        Assert.Equal(PlayerState.Initial.MaxHP, result.State.PlayerState.MaxHP);
        Assert.Equal(PlayerState.Initial.ATK, result.State.PlayerState.ATK);
        Assert.Equal(PlayerState.Initial.DEF, result.State.PlayerState.DEF);
        Assert.Equal(PlayerState.Initial.Crit, result.State.PlayerState.Crit);
    }

    [Fact]
    public void Healing_ShouldConsumeNoRng()
    {
        // §7.2 item 1 / §4.5 item 4: Spawn is the only operation that advances the
        // RNG. Applying the HealPool draws nothing, so the retained stream is exactly
        // the one the resolution's Spawn produced.
        var result = SwapExecutor.Execute(BattleWith(HpMatch3Board()), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.Equal(result.Resolution.RngState, result.State.RngState);
    }

    [Fact]
    public void AccumulatedHealPool_ShouldBeAppliedOnceAsTheSwapTotal()
    {
        // MATCH3_RULES.md §5.7 item 7 / MATCH3_RULES.md §4.2: a Swap's pools
        // accumulate across every pass of the cascade, and the Swap's total is the
        // sum. The player-effects step applies that total once — not once per pass,
        // which would heal the per-pass amount repeatedly.
        //
        // Stated as the equality the resolution makes checkable without depending on
        // which Gems a spawn draws: the HP the committed Swap leaves is the starting
        // HP plus the Swap's whole HealPool, clamped — the pool the result reports,
        // applied exactly once.
        var player = PlayerState.Initial with { HP = 0, MaxHP = 100_000 };

        var result = SwapExecutor.Execute(BattleWith(HpMatch3Board(), player), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        // The 100000 ceiling cannot bind for a single Swap's cascade, so the pool is
        // applied in full and no part of it is discarded.
        Assert.Equal(result.Resources.HealPool, result.State.PlayerState.HP);

        // And the pool the result reports is the sum over every pass's cleared HP
        // Gems — the accumulation happens in generation, and healing reads the
        // accumulated total.
        var expected = result.Resolution.Passes
            .SelectMany(p => p.ClearedGems)
            .Where(g => g.GemType == GemType.Hp)
            .Sum(g => (ResourceGenerator.HpGemBaseHeal * MatchTierMultipliers.NumeratorFor(g.Tier))
                      / MatchTierMultipliers.Denominator);

        Assert.Equal(expected, result.Resources.HealPool);
    }

    [Fact]
    public void PowerAndHp_ShouldBothBeWrittenByTheSameResolution()
    {
        // GAME_RULES.md §17 steps 13 and 14 sit next to each other over the same
        // PlayerState: the generation step writes Power (step 13) and the
        // player-effects step writes HP (step 14). Neither may overwrite the other —
        // they are applied in sequence over the same value and the single write-back
        // carries both.
        var state = PlayerState.Initial with { Power = 20, HP = 500, MaxHP = 1000 };

        var generation = new ResourceGeneration(
            BaseDamagePool: 0,
            DefensePool: 0,
            HealPool: 100,
            Power: 30,
            ClearedGemCounts.None);

        var afterPower = ResourceGenerator.ApplyPower(state, generation);
        var afterBoth = ResourceGenerator.ApplyHeal(afterPower, generation);

        Assert.Equal(50, afterBoth.Power);
        Assert.Equal(600, afterBoth.HP);
    }
}
