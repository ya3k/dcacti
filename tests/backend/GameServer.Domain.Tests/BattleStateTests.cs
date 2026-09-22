using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Board Foundation State contract tests (GAME_STATE.md §2.0.5).
///
/// These verify the documented field set, the retained initial values, and the
/// fields this stage still does not add. The board stage is §2.0 plus the board
/// and RNG fields — it removes nothing, renames nothing, and redefines nothing
/// (§0 item 5). It is still not the full §2 gameplay state: no Pet, Boss, Player,
/// Card, or Relic state exists here and none is added.
/// </summary>
public class BattleStateTests
{
    /// <summary>A fixed seed, so the generated board is deterministic in tests.</summary>
    private const ulong TestSeed = 42UL;

    [Fact]
    public void Create_ShouldProduceDocumentedInitialValues()
    {
        // GAME_STATE.md §2.0.2, §2.0.5.2 item 1: Turn = 0, Sequence = 0 — board
        // generation is not a player Swap/Action and is not an action resolution.
        var state = BattleState.CreateWith("battle-1", TestSeed);

        Assert.Equal("battle-1", state.BattleId);
        Assert.Equal(0, state.Turn);
        Assert.Equal(0, state.Sequence);
    }

    [Fact]
    public void Create_ShouldMatchTheDocumentedInitialValueConstants()
    {
        var state = BattleState.CreateWith("battle-1", TestSeed);

        Assert.Equal(BattleState.InitialTurn, state.Turn);
        Assert.Equal(BattleState.InitialSequence, state.Sequence);
        Assert.Equal(0, BattleState.InitialTurn);
        Assert.Equal(0, BattleState.InitialSequence);
    }

    [Fact]
    public void BoardFoundationState_ShouldCarryExactlyTheDocumentedFields()
    {
        // GAME_STATE.md §2.0.5: BattleId, Turn, Sequence, RngSeed, RngState,
        // BoardState — nothing else, plus the Swap stage's LastCommittedSwapPair
        // (§2.1.10), the Match / Combo stage's PlayerState (§2.2), the Pet /
        // Passive stage's PetState (§2.3), and the Boss stage's BossState (§2.4),
        // each added to this same record by its own owning stage.
        var properties = typeof(BattleState)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "BattleId", "BoardState", "BossState", "LastCommittedSwapPair", "PetState",
                "PlayerState", "RngSeed", "RngState", "Sequence", "Turn",
            },
            properties);
    }

    [Fact]
    public void BoardFoundationState_ShouldKeepTheFoundationFieldsIntact()
    {
        // §0 item 5: the board stage is §2.0 plus fields — it removes nothing,
        // renames nothing, and redefines nothing.
        var declared = typeof(BattleState)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        foreach (var foundationField in new[] { "BattleId", "Turn", "Sequence" })
        {
            Assert.Contains(foundationField, declared);
        }
    }

    [Fact]
    public void BoardFoundationState_ShouldDeclareNoStatusOrLifecycleField()
    {
        // GAME_STATE.md §2.0.3 / §2.0.5: the board stage adds no Status field and
        // no lifecycle value.
        var declared = typeof(BattleState)
            .GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(BattleState).GetFields().Select(f => f.Name))
            .ToArray();

        Assert.DoesNotContain("Status", declared);

        foreach (var lifecycle in new[]
                 {
                     "READY", "STARTING", "ACTIVE", "PAUSED", "FINISHED", "WON", "LOST",
                 })
        {
            Assert.DoesNotContain(lifecycle, declared);
        }
    }

    [Fact]
    public void BoardFoundationState_ShouldStillDeclareNoLaterStageField()
    {
        // GAME_STATE.md §2.0.5.3: the rest of PlayerState (§2.2) — StatusEffects,
        // EquippedRelics, EquippedCards — is not implemented yet, the rest of
        // PetState (§2.3) — PetId/Identity, Tier/Star/Level — is likewise still
        // owned by the Pet identity and progression stage, and the rest of
        // BossState (§2.4) — PassiveProgress, StatusEffects[] — is still owned by
        // the Boss Passive and Status Effects systems.
        //
        // PendingSpecialGems is in the list for a different reason: it is not a deferred
        // field — the placeholder was removed and does not exist in any form
        // (GAME_STATE.md §2.1.2 item 1). Special Gem state is carried inside each
        // `Cells[64]` entry (§2.1.1), so no board-level collection for it may appear.
        //
        // PlayerState is deliberately NOT in the list: it is the field the Match /
        // Combo accounting stage implements (§2.2), and §2 nests it inside
        // BattleState. Its own fields are covered by the PlayerState contract tests.
        // LastCommittedSwapPair is likewise NOT in the list: it is the one field the
        // Swap stage adds, and §2.1.10 documents it as required state, not a
        // convenience. PetState is NOT in the list either: it is the field the Pet /
        // Passive stage implements (§2.3), added to this same record by its own
        // owning stage and delivered through BattleStateUpdated
        // (SIGNALR_PROTOCOL.md §4.3). Its own members are covered by the PetState
        // contract tests below.
        //
        // HP, MaxHP, ATK, DEF, Power, and Crit are NOT in the list either: the combat
        // stats stage adds them to PlayerState with the COMBAT_RULES.md §1.1 MVP
        // defaults, exactly as each earlier stage added its own fields. The check
        // here is that no OTHER later-stage field appeared.
        //
        // BossState is NOT in the list, and neither is the Pet's Element: the Boss
        // stage implements both — BossState as a field of this record (§2.4) and
        // Element as a PetState field (§2.3) — exactly as each earlier stage added
        // its own. Their own members are covered by the BossState contract tests.
        // The Boss's own Element is a member of BossState, not of BattleState, so
        // it is not a top-level field here either.
        var declared = typeof(BattleState)
            .GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(BattleState).GetFields().Select(f => f.Name))
            .Concat(typeof(BoardState).GetProperties().Select(p => p.Name))
            .Concat(typeof(PlayerState).GetProperties().Select(p => p.Name))
            .Concat(typeof(PetState).GetProperties().Select(p => p.Name))
            .Concat(typeof(BossState).GetProperties().Select(p => p.Name))
            .ToArray();

        foreach (var laterStageField in new[]
                 {
                     "PendingSpecialGems",
                     "StatusEffects", "EquippedRelics", "EquippedCards",
                     // The rest of §2.3, still owned by the Pet identity and
                     // progression stage (§2.3, SIGNALR_PROTOCOL.md §4.3 item 2).
                     "PetId", "Tier", "Star", "Level",
                     // The rest of §2.4, still owned by the Boss Passive and Status
                     // Effects systems (§2.4, BOSS_RULES.md §3).
                     "BossPassiveProgress", "BossStatusEffects",
                 })
        {
            Assert.DoesNotContain(laterStageField, declared);
        }
    }

    [Fact]
    public void Create_ShouldRetainTheSeedItWasGiven()
    {
        // GAME_STATE.md §2.6.1 item 3: RngSeed records the battle's origin point
        // and is never rewritten after creation.
        var state = BattleState.CreateWith("battle-1", TestSeed);

        Assert.Equal(TestSeed, state.RngSeed);
    }

    [Fact]
    public void Create_ShouldRecordTheRngStateAfterGeneration()
    {
        // GAME_STATE.md §2.7.1 step 6: the resulting RngState is retained.
        var state = BattleState.CreateWith("battle-1", TestSeed);

        // It is a state + increment pair, not a single word (§2.6.2 item 1), and
        // the stream selector must be odd for PCG-XSH-RR 64/32 (ADR-009).
        Assert.Equal(1UL, state.RngState.Increment & 1UL);
    }

    [Fact]
    public void Create_ShouldNotResetTheRngStateToItsSeededValue()
    {
        // §15 of the task and GAME_STATE.md §2.6.2: the state represents the
        // actual state after all RNG consumption used by the accepted generation
        // attempt. It is not the freshly seeded state, because the board consumed
        // at least 64 draws.
        var state = BattleState.CreateWith("battle-1", TestSeed);
        var freshlySeeded = Pcg32.FromSeed(TestSeed).CurrentState;

        Assert.NotEqual(freshlySeeded, state.RngState);
    }

    [Fact]
    public void Create_ShouldGenerateAValidAuthoritativeBoard()
    {
        // MATCH3_RULES.md §1.2 items 2–3, enforced during generation.
        var state = BattleState.CreateWith("battle-1", TestSeed);

        Assert.Equal(64, state.BoardState.Cells.Count);
        Assert.False(BoardGenerationValidator.HasMatch(state.BoardState));
        Assert.True(BoardGenerationValidator.HasValidSwap(state.BoardState));
    }

    [Fact]
    public void Create_ShouldBeDeterministicForTheSameSeed()
    {
        // MATCH3_RULES.md §7 item 4: the same seed produces the same initial
        // board, including the same §1.5 retry sequence.
        var first = BattleState.CreateWith("battle-1", TestSeed);
        var second = BattleState.CreateWith("battle-2", TestSeed);

        Assert.Equal(first.BoardState.Cells, second.BoardState.Cells);
        Assert.Equal(first.RngState, second.RngState);
    }

    [Fact]
    public void Create_ShouldScopesStateToTheRequestedBattleId()
    {
        // GAME_STATE.md §2.0.1: BattleId identifies the battle session and
        // carries no gameplay content.
        var first = BattleState.CreateWith("battle-1", TestSeed);
        var second = BattleState.CreateWith("battle-2", TestSeed);

        Assert.NotEqual(first.BattleId, second.BattleId);
        Assert.Equal(0, first.Turn);
        Assert.Equal(0, second.Turn);
    }

    [Fact]
    public void Create_ShouldRejectAnEmptyBattleId()
    {
        Assert.Throws<ArgumentException>(() => BattleState.CreateWith(string.Empty, TestSeed));
        Assert.Throws<ArgumentException>(() => BattleState.CreateWith("   ", TestSeed));
    }

    [Fact]
    public void Create_ShouldProduceAnImmutableSnapshot()
    {
        // State is server-authored (GAME_RULES.md §18, ADR-001): a caller
        // cannot mutate a snapshot in place.
        var state = BattleState.CreateWith("battle-1", TestSeed);

        Assert.IsType<BattleState>(state with { Turn = state.Turn }, exactMatch: false);
        Assert.Equal(0, state.Turn);
    }

    // =======================================================================
    // PetState — GAME_STATE.md §2.3, SIGNALR_PROTOCOL.md §4.3
    // =======================================================================

    private static readonly PassiveId XichLang = new("xich-lang");

    /// <summary>
    /// Xích Lang's MVP Element (<c>ELEMENT_RULES.md</c> §6 — Hỏa).
    /// </summary>
    private const Element XichLangElement = Element.Hoa;

    /// <summary>
    /// Hỏa Long, an MVP Boss of <c>BOSS_RULES.md</c> §6.1 (Hỏa, 5000 / 100 / 50),
    /// used wherever a test needs a battle without asserting Boss values.
    /// </summary>
    private static readonly BossDefinition MvpBoss = BossDefinitions.HoaLong;

    /// <summary>
    /// A battle created through the documented Pet configuration — the form
    /// <c>BattleStateService.CreateBattle</c> uses.
    /// </summary>
    private static BattleState BattleWithPassive(
        PassiveResetBehavior? reset = null,
        int threshold = 5) =>
        BattleState.Create("battle-pet", TestSeed, XichLangElement, XichLang, threshold, MvpBoss, reset);

    [Fact]
    public void Create_ShouldInitializePetStateWithThePassiveAtProgressZero()
    {
        // GAME_STATE.md §2.3 item 3: PetState is present from battle creation, and
        // §2.3 / SIGNALR_PROTOCOL.md §4.3 item 4 fix the progress it begins with —
        // the Passive's own Threshold with Current = 0.
        var state = BattleState.Create(
            "battle-pet", TestSeed, XichLangElement, XichLang, passiveThreshold: 5, MvpBoss);

        Assert.Equal(XichLang, state.PetState.PassiveId);
        Assert.Equal(XichLangElement, state.PetState.Element);
        Assert.Equal(new PassiveProgress(5, 0), state.PetState.PassiveProgress);
        Assert.Equal(0, state.PetState.PassiveProgress.Current);
        Assert.Equal(5, state.PetState.PassiveProgress.Threshold);
    }

    [Fact]
    public void Create_ShouldRequireThePetAndBossConfiguration()
    {
        // §2.3 item 3 and §2.4: PetState and BossState are not optional, not
        // defaulted, and not lazily initialized — there is no "no Passive yet" and
        // no "no Boss yet" state for an absent value to spell. No creation overload
        // therefore omits them: every one supplies either a PetState/BossState pair
        // or the Pet and Boss configuration that builds them.
        var overloads = typeof(BattleState)
            .GetMethods()
            .Where(m => m is { IsStatic: true, Name: "Create" })
            .Select(m => m.GetParameters().Length)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal([4, 7], overloads);
    }

    [Fact]
    public void Create_ShouldRecordADeclaredNonDefaultResetBehavior()
    {
        // PASSIVE_RULES.md §4 item 2 / GAME_STATE.md §2.3: a Passive that does not
        // use the default reset declares it, and the declaration is part of the
        // state so the tracker can apply it (SIGNALR_PROTOCOL.md §4.3 item 6).
        var state = BattleWithPassive(PassiveResetBehavior.Partial);

        Assert.Equal(PassiveResetBehavior.Partial, state.PetState.PassiveResetOverride);
        Assert.Equal(PassiveResetBehavior.Partial, state.PetState.ResetBehavior);
        Assert.True(state.PetState.HasResetOverride);
    }

    [Fact]
    public void Create_ShouldLeaveTheResetOverrideAbsentForTheDefault()
    {
        // GAME_STATE.md §2.3 / PASSIVE_RULES.md §4 item 1: the override is "only
        // present if this Pet's Passive uses non-default reset behavior", and §4
        // item 1's default is what its absence means. This is not "unset": an
        // absent override and an explicit Default describe the same behavior.
        var state = BattleWithPassive();

        Assert.Null(state.PetState.PassiveResetOverride);
        Assert.False(state.PetState.HasResetOverride);
        Assert.Equal(PassiveResetBehavior.Default, state.PetState.ResetBehavior);

        // The two spellings of "default" are deliberately the same behavior.
        var explicitDefault = BattleWithPassive(PassiveResetBehavior.Default);
        Assert.Equal(state.PetState.ResetBehavior, explicitDefault.PetState.ResetBehavior);
    }

    [Fact]
    public void Create_ShouldStartPetProgressAtZeroAgainstTheSuppliedThreshold()
    {
        // The progress begins at the Passive's own Threshold (§2.3) — the
        // Threshold is the Passive definition's value and is not invented here,
        // and Current begins at 0 whatever the Threshold is.
        foreach (var threshold in new[] { 1, 4, 5, 6, 7, 10 })
        {
            var state = BattleWithPassive(threshold: threshold);

            Assert.Equal(threshold, state.PetState.PassiveProgress.Threshold);
            Assert.Equal(0, state.PetState.PassiveProgress.Current);
            Assert.False(state.PetState.PassiveProgress.IsReady);
        }
    }

    [Fact]
    public void Create_ShouldNotChargeThePassive()
    {
        // PASSIVE_RULES.md §2 item 1 charges per Match, and board generation is
        // not a Swap/Action and produces no Match (MATCH3_RULES.md §8.1 item 4,
        // GAME_STATE.md §2.0.5.2 item 1). Creation therefore leaves progress at 0.
        var state = BattleState.Create("battle-pet", TestSeed, XichLangElement, XichLang, passiveThreshold: 5, MvpBoss);

        Assert.Equal(0, state.PetState.PassiveProgress.Current);
    }

    [Fact]
    public void PetState_ShouldCarryExactlyTheDocumentedFields()
    {
        // GAME_STATE.md §2.3: Element, PassiveId, PassiveProgress,
        // PassiveResetOverride — the four fields the Pet stages implement.
        // PetId/Identity and
        // Tier/Star/Level belong to a later stage and are not stubbed here (§0
        // item 4, §0 item 5).
        var dataMembers = typeof(PetState)
            .GetConstructors()
            .SelectMany(c => c.GetParameters().Select(p => p.Name!))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "Element", "PassiveId", "PassiveProgress", "PassiveResetOverride" },
            dataMembers);

        // ResetBehavior and HasResetOverride are derived readings of the recorded
        // override, not additional state: the four members above are the whole
        // representation (§0 item 5).
        var declared = typeof(PetState)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "Element", "HasResetOverride", "PassiveId", "PassiveProgress",
                "PassiveResetOverride", "ResetBehavior",
            },
            declared);
    }

    [Fact]
    public void PetState_ShouldReadThePassiveIdentityAsAValue()
    {
        // GAME_EVENTS.md §2 item 1 / GAME_STATE.md §2.3: the identity is the same
        // value the PassiveCharged/PassiveTriggered payload reports — read and
        // reported, never re-derived or re-numbered.
        var state = BattleState.Create("battle-pet", TestSeed, XichLangElement, XichLang, passiveThreshold: 5, MvpBoss);

        Assert.Equal("xich-lang", state.PetState.PassiveId.Value);
        Assert.Equal("xich-lang", state.PetState.PassiveId.ToString());
    }
}