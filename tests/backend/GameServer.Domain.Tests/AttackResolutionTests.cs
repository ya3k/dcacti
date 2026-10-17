namespace GameServer.Domain.Tests;

using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Relics;
using Xunit;

/// <summary>
/// Domain ATK-qualification and ATK-gated damage resolution tests
/// (<c>COMBAT_RULES.md</c> §2 item 6 and §3, <c>ADR-026</c> D1, OD-6, <c>ADR-028</c>).
///
/// <code>
/// Rule (COMBAT_RULES.md §2 item 6 "Attack qualification (OD-6)")
///  ↓
/// Scenario (Given the acting actor's own resolution and the confirmed Matches it
///           contains, When the attack resolves, Then damage executes iff an ATK
///           Match was confirmed)
///  ↓
/// Test
/// </code>
///
/// <b>The evidence under test is match-resolution evidence.</b> Every fixture below
/// is either a resolution the real pipeline produced
/// (<see cref="CascadeResolver.Resolve"/> over a board built by
/// <see cref="TestBoard"/>) or — for the two states the post-retirement model
/// cannot reach, see the individual cases — a resolution assembled from the real
/// pipeline's own <see cref="MatchResolution"/> entries plus an explicit cleared-cell
/// list. No test asserts against <c>ClearedGemCounts</c> as an authorization input:
/// the parameter no longer exists on the resolution API, which is the structural
/// half of the invariant.
/// </summary>
public class AttackResolutionTests
{
    private const ulong TestSeed = 20260815UL;

    private static int I(int row, int column) => TestBoard.I(row, column);

    private static PetState DefaultPet() =>
        new(
            PetId: new Pets.PetId("pet-test"),
            Element: Element.Hoa,
            HP: 1000,
            MaxHP: 1000,
            ATK: 100,
            DEF: 50,
            Crit: 0,
            Power: 0,
            PassiveId: new Passives.PassiveId("test-passive"),
            PassiveProgress: Passives.PassiveProgress.AtStart(5));

    private static BossState DefaultBoss() =>
        BossDefinitions.HoaLong.ToInitialState();

    /// <summary>
    /// A committed resolution produced by the real cascade pipeline whose first pass
    /// contains exactly one Match — a horizontal run of three
    /// <paramref name="type"/> Gems on row 4 — and whose later passes are the
    /// cascade's own Spawn-driven follow-ups.
    ///
    /// The board is <see cref="TestBoard.Background"/> with that one run patched in.
    /// The background is match-free by construction, so the patched run is the only
    /// Match of the first pass. Where the background's own cell at column 3 already
    /// held <paramref name="type"/> the run would silently become a Match 4, so that
    /// cell is replaced; every other cell is the verified background.
    /// </summary>
    private static CascadeResolver.CascadeResult ResolutionWithSingleMatchOf(GemType type)
    {
        var cells = new List<(int Index, GemType Type)>
        {
            (I(4, 0), type),
            (I(4, 1), type),
            (I(4, 2), type),
        };

        var background = TestBoard.Background();

        // "ADHPADHP" row 4 holds POWER at column 3; leave it only when it differs.
        if (background[I(4, 3)] == type)
        {
            cells.Add((I(4, 3), GemType.Atk == type ? GemType.Hp : GemType.Atk));
        }

        var board = background.WithGems([.. cells]);
        var battle = BattleState.CreateWith("battle-atk-qualification", TestSeed);

        return CascadeResolver.Resolve(
            board,
            new Pcg32(battle.RngState.State, battle.RngState.Increment),
            swapOriginIndex: null);
    }

    /// <summary>
    /// Every confirmed Match of a resolution, read straight from the resolution's own
    /// passes — the precondition checks below use this rather than the type under
    /// test, so a fixture defect cannot be mistaken for a passing assertion.
    /// </summary>
    private static IReadOnlyList<MatchResolution> ConfirmedMatches(CascadeResolver.CascadeResult resolution) =>
        [.. resolution.Passes.SelectMany(pass => pass.Matches)];

    /// <summary>
    /// A resolution assembled from the pipeline's own <see cref="MatchResolution"/>
    /// entries together with an explicit cleared-cell list.
    ///
    /// It exists for the two states the post-retirement pipeline cannot produce: a
    /// positive cleared-ATK count with no confirmed ATK Match, and a confirmed ATK
    /// Match with no reporting count. Both are shapes the runtime must answer by
    /// evidence rather than by accounting, so they are stated as values rather than
    /// reached through a board — a board fixture could not express them, and the
    /// retired Special Gem model that once could is not to be used.
    /// </summary>
    private static CascadeResolver.CascadeResult ResolutionFrom(
        IReadOnlyList<MatchResolution> matches,
        IReadOnlyList<ClearedGem> clearedGems)
    {
        var board = TestBoard.Background();

        var pass = new PassResult(
            Board: board,
            RngState: default,
            Matches: matches,
            MatchedCellGemMatched: [],
            ActivationCellGemMatched: [],
            ActivatedSpecialGems: [],
            CreatedSpecialGems: [],
            ClearedCellUnion: [.. clearedGems.Select(gem => gem.CellIndex)],
            ClearedGems: clearedGems,
            DropMoves: [],
            SpawnedGems: []);

        return new CascadeResolver.CascadeResult(
            Board: board,
            RngState: default,
            Passes: [pass],
            CascadeDepth: 0,
            TotalMatches: matches.Count);
    }

    // =======================================================================
    // 1. Qualification evidence — the confirmed ATK Match decides (OD-6)
    // =======================================================================

    [Fact]
    public void Qualification_WhenTheActorsResolutionHasAConfirmedAtkMatch_ShouldQualify()
    {
        // COMBAT_RULES.md §2 item 6 / OD-6: an attack is calculated and applied iff
        // the acting actor's own resolution contains at least one confirmed valid
        // ATK-gem match.
        var resolution = ResolutionWithSingleMatchOf(GemType.Atk);

        // Precondition, read from the resolution's own evidence: an ATK Match exists.
        Assert.Contains(ConfirmedMatches(resolution), match => match.Shape.GemType == GemType.Atk);

        var qualification = AttackQualification.FromResolution(resolution);

        Assert.True(qualification.HasConfirmedAtkMatch);

        // And it authorizes the Player's attack instance.
        var boss = DefaultBoss();

        var result = AttackResolution.ResolvePlayerAttack(
            DefaultPet(),
            boss,
            qualification,
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        Assert.True(result.IsQualifyingAttack);
        Assert.NotNull(result.DamageResult);
        Assert.True(result.FinalDamage > 0);
        Assert.True(result.DefenderHp < boss.HP);
        Assert.Equal(3, result.Events.Count);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.DamageCalculated);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.DamageDealt);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.DamageTaken);
    }

    [Theory]
    [InlineData(GemType.Def)]
    [InlineData(GemType.Hp)]
    [InlineData(GemType.Power)]
    public void Qualification_WhenTheActorsResolutionHasOnlyANonAtkMatch_ShouldNotQualify(GemType matchType)
    {
        // COMBAT_RULES.md §2 item 6 / OD-6: a DEF, HP, or POWER Match — alone — must
        // not qualify an attack. The rule is per Gem type: only an ATK Match
        // qualifies.
        var resolution = ResolutionWithSingleMatchOf(matchType);

        // Precondition: the resolution's only Match is of the requested non-ATK type.
        var matches = ConfirmedMatches(resolution);
        Assert.NotEmpty(matches);
        Assert.DoesNotContain(matches, match => match.Shape.GemType == GemType.Atk);

        Assert.False(AttackQualification.FromResolution(resolution).HasConfirmedAtkMatch);
    }

    [Fact]
    public void Qualification_WhenNoResolutionEvidenceIsOffered_ShouldNotQualify()
    {
        // The absence of evidence is stated as a value, not inferred from a counter.
        Assert.False(AttackQualification.None.HasConfirmedAtkMatch);
        Assert.False(default(AttackQualification).HasConfirmedAtkMatch);

        // A resolution with no passes qualifies nothing.
        var empty = new CascadeResolver.CascadeResult(
            Board: TestBoard.Background(),
            RngState: default,
            Passes: [],
            CascadeDepth: 0,
            TotalMatches: 0);

        Assert.False(AttackQualification.FromResolution(empty).HasConfirmedAtkMatch);
    }

    // =======================================================================
    // 2. Cleared counts never authorize (COMBAT_RULES.md §2 item 6: "reporting")
    // =======================================================================

    [Fact]
    public void Attack_WhenOnlyTheClearedAtkCountIsPositive_ShouldNotExecuteDamage()
    {
        // COMBAT_RULES.md §2 item 6 / OD-6: "Neither the ATK pool's value nor a count
        // of cleared ATK cells may qualify an attack on its own: cleared-gem
        // accounting is a reporting quantity and is never the eligibility predicate."
        //
        // The scenario is the stale-count case: two ATK cells are cleared and the
        // reporting count says so, but no confirmed ATK Match exists in the
        // resolution. The attack must not execute.
        var resolution = ResolutionFrom(
            matches: ResolutionWithSingleMatchOf(GemType.Def).Passes[0].Matches,
            clearedGems:
            [
                new ClearedGem(I(4, 0), GemType.Atk),
                new ClearedGem(I(4, 1), GemType.Atk),
            ]);

        // Precondition: the reporting count really is positive — the value is derived
        // by the production generator, not written by hand.
        var cleared = ResourceGenerator.Generate(resolution).ClearedGemCounts;
        Assert.Equal(2, cleared.Atk);

        // ...and it authorizes nothing: the resolution carries no ATK Match.
        Assert.DoesNotContain(ConfirmedMatches(resolution), match => match.Shape.GemType == GemType.Atk);
        Assert.False(AttackQualification.FromResolution(resolution).HasConfirmedAtkMatch);

        var boss = DefaultBoss();

        var result = AttackResolution.ResolvePlayerAttack(
            DefaultPet(),
            boss,
            AttackQualification.FromResolution(resolution),
            baseDamagePool: 20,
            combo: 1,
            rngState: default);

        Assert.False(result.IsQualifyingAttack);
        Assert.Null(result.DamageResult);
        Assert.Equal(0, result.FinalDamage);
        Assert.Equal(boss.HP, result.DefenderHp);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Qualification_WhenTheAtkMatchExistsButTheReportIsAbsent_ShouldStillQualify()
    {
        // COMBAT_RULES.md §2 item 6 / OD-6: the qualification follows the authoritative
        // match evidence. A confirmed ATK Match qualifies even when the reporting
        // counters are absent, zero, or independently transformed — the counters are
        // not the predicate and their absence cannot cancel a proven Match.
        var resolution = ResolutionFrom(
            matches: ResolutionWithSingleMatchOf(GemType.Atk).Passes[0].Matches,
            clearedGems: []);

        // Precondition: nothing is reported for any Gem type...
        Assert.Equal(0, ResourceGenerator.Generate(resolution).ClearedGemCounts.Total);

        // ...and a confirmed ATK Match is present.
        Assert.Contains(ConfirmedMatches(resolution), match => match.Shape.GemType == GemType.Atk);

        var qualification = AttackQualification.FromResolution(resolution);
        Assert.True(qualification.HasConfirmedAtkMatch);

        var boss = DefaultBoss();

        var result = AttackResolution.ResolvePlayerAttack(
            DefaultPet(),
            boss,
            qualification,
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        Assert.True(result.IsQualifyingAttack);
        Assert.True(result.FinalDamage > 0);
        Assert.Equal(3, result.Events.Count);
    }

    // =======================================================================
    // 3. Per-actor and per-resolution isolation (ADR-026 item 4, OD-6)
    // =======================================================================

    [Fact]
    public void Qualification_WhenOnlyAnotherActorHasAnAtkMatch_ShouldNotQualifyTheActingActor()
    {
        // ADR-026 item 4 / OD-6: the acting side owns the resources — and the
        // qualification — of its own resolution. The Player's ATK Match is not the
        // Boss's.
        var playerResolution = ResolutionWithSingleMatchOf(GemType.Atk);
        var bossResolution = ResolutionWithSingleMatchOf(GemType.Def);

        Assert.True(AttackQualification.FromResolution(playerResolution).HasConfirmedAtkMatch);
        Assert.False(AttackQualification.FromResolution(bossResolution).HasConfirmedAtkMatch);

        // The Boss's own attack is resolved from the Boss's own evidence, so the
        // Player's Match cannot authorize it.
        var pet = DefaultPet();

        var bossAttack = AttackResolution.ResolveBossAttack(
            DefaultBoss(),
            pet,
            AttackQualification.FromResolution(bossResolution),
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        Assert.False(bossAttack.IsQualifyingAttack);
        Assert.Equal(pet.HP, bossAttack.DefenderHp);
        Assert.Empty(bossAttack.Events);
    }

    [Fact]
    public void Qualification_FromAnEarlierResolution_ShouldNotAuthorizeTheCurrentOne()
    {
        // COMBAT_RULES.md §2 item 6 / OD-6: the qualification is derived from the
        // resolution being resolved. It is not state, it is not persisted, and it
        // cannot be carried forward: the earlier resolution's ATK Match authorizes
        // the earlier attack only.
        var earlier = ResolutionWithSingleMatchOf(GemType.Atk);
        var current = ResolutionWithSingleMatchOf(GemType.Power);

        Assert.True(AttackQualification.FromResolution(earlier).HasConfirmedAtkMatch);
        Assert.False(AttackQualification.FromResolution(current).HasConfirmedAtkMatch);

        // Resolving the current attack from the current resolution's evidence is the
        // only supported path, and it answers "not qualified".
        var boss = DefaultBoss();

        var currentAttack = AttackResolution.ResolvePlayerAttack(
            DefaultPet(),
            boss,
            AttackQualification.FromResolution(current),
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        Assert.False(currentAttack.IsQualifyingAttack);
        Assert.Equal(boss.HP, currentAttack.DefenderHp);
    }

    // =======================================================================
    // 4. Player Attack Gating
    // =======================================================================

    [Fact]
    public void PlayerAttack_WhenNoAtkMatchIsConfirmed_ShouldDealZeroDamageAndEmitNoEvents()
    {
        // Given: a resolution with no confirmed ATK Match (a DEF Match only).
        var pet = DefaultPet();
        var boss = DefaultBoss();

        // When: Attack is resolved
        var result = AttackResolution.ResolvePlayerAttack(
            pet,
            boss,
            AttackQualification.FromResolution(ResolutionWithSingleMatchOf(GemType.Def)),
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        // Then: Not a qualifying attack, 0 damage dealt, no damage events emitted
        Assert.False(result.IsQualifyingAttack);
        Assert.Null(result.DamageResult);
        Assert.Equal(0, result.FinalDamage);
        Assert.Equal(boss.HP, result.DefenderHp);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void PlayerAttack_WhenTheAtkMatchIsConfirmed_ShouldCalculateDamageAndEmitEvents()
    {
        // Given: a confirmed ATK Match
        var pet = DefaultPet();
        var boss = DefaultBoss();

        // When: Attack is resolved
        var result = AttackResolution.ResolvePlayerAttack(
            pet,
            boss,
            new AttackQualification(HasConfirmedAtkMatch: true),
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        // Then: Qualifying attack, damage calculated, events emitted
        Assert.True(result.IsQualifyingAttack);
        Assert.NotNull(result.DamageResult);
        Assert.True(result.FinalDamage > 0);
        Assert.True(result.DefenderHp < boss.HP);
        Assert.Equal(3, result.Events.Count);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.DamageCalculated);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.DamageDealt);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.DamageTaken);
    }

    // =======================================================================
    // 5. Boss Attack Gating
    // =======================================================================

    [Fact]
    public void BossAttack_WhenNoAtkMatchIsConfirmed_ShouldDealZeroDamageAndNotExecuteBasicAttack()
    {
        // Given: the Boss's own resolution contains no confirmed ATK Match
        var boss = DefaultBoss();
        var pet = DefaultPet();

        // When: Attack is resolved
        var result = AttackResolution.ResolveBossAttack(
            boss,
            pet,
            AttackQualification.FromResolution(ResolutionWithSingleMatchOf(GemType.Def)),
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        // Then: Not a qualifying attack, zero damage, Pet HP unchanged
        Assert.False(result.IsQualifyingAttack);
        Assert.Null(result.DamageResult);
        Assert.Equal(0, result.FinalDamage);
        Assert.Equal(pet.HP, result.DefenderHp);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void BossAttack_WhenTheAtkMatchIsConfirmed_ShouldDealDamageToPet()
    {
        // Given: the Boss's own resolution contains a confirmed ATK Match
        var boss = DefaultBoss();
        var pet = DefaultPet();

        // When: Attack is resolved
        var result = AttackResolution.ResolveBossAttack(
            boss,
            pet,
            AttackQualification.FromResolution(ResolutionWithSingleMatchOf(GemType.Atk)),
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        // Then: Qualifying attack, damage dealt to Pet
        Assert.True(result.IsQualifyingAttack);
        Assert.NotNull(result.DamageResult);
        Assert.True(result.FinalDamage > 0);
        Assert.True(result.DefenderHp < pet.HP);
        Assert.Equal(3, result.Events.Count);
    }

    // =======================================================================
    // 6. Modifier Preservation vs. Consumption
    // =======================================================================

    [Fact]
    public void PlayerNextAttackModifiers_WhenNoAtkMatchIsConfirmed_ShouldBePreserved()
    {
        // Given: Pet with NextAttack Crit and ATK modifiers
        var pet = DefaultPet() with
        {
            NextAttackCritModifiers = [new NextAttackCritModifier("source_crit", 25)],
            ATKModifiers = [new ATKModifier("source_atk", 20, RelicEffectLifetime.NextAttack)],
        };
        var boss = DefaultBoss();
        var qualification = AttackQualification.FromResolution(ResolutionWithSingleMatchOf(GemType.Def));

        // When: Attack is resolved and applied
        var (updatedPet, _, result) = AttackResolution.ResolveAndApplyPlayerAttack(
            pet,
            boss,
            qualification,
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        // Then: Not qualifying, modifiers are preserved intact
        Assert.False(result.IsQualifyingAttack);
        Assert.Single(updatedPet.NextAttackCritModifiers);
        Assert.Single(updatedPet.ATKModifiers);
        Assert.Equal(25, updatedPet.NextAttackCritModifiers[0].CritContribution);
    }

    [Fact]
    public void PlayerNextAttackModifiers_WhenTheAtkMatchIsConfirmed_ShouldBeConsumed()
    {
        // Given: Pet with NextAttack Crit and ATK modifiers
        var pet = DefaultPet() with
        {
            NextAttackCritModifiers = [new NextAttackCritModifier("source_crit", 25)],
            ATKModifiers = [new ATKModifier("source_atk", 20, RelicEffectLifetime.NextAttack)],
        };
        var boss = DefaultBoss();
        var qualification = AttackQualification.FromResolution(ResolutionWithSingleMatchOf(GemType.Atk));

        // When: Attack is resolved and applied
        var (updatedPet, _, result) = AttackResolution.ResolveAndApplyPlayerAttack(
            pet,
            boss,
            qualification,
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        // Then: Qualifying attack, modifiers are consumed
        Assert.True(result.IsQualifyingAttack);
        Assert.Empty(updatedPet.NextAttackCritModifiers);
        Assert.Empty(updatedPet.ATKModifiers);
    }

    [Fact]
    public void BossNextQualifyingAttackModifier_WhenNoAtkMatchIsConfirmed_ShouldBePreserved()
    {
        // Given: Boss with Thunder Strike NextQualifyingAttack modifier
        var bossModifier = StatusEffect.TriggerBased(
            "thunder-strike",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: 50,
            StatusEffect.NextQualifyingAttackCondition,
            targetStat: "ATK");
        var boss = DefaultBoss() with
        {
            ActiveStatusEffects = [bossModifier],
        };
        var pet = DefaultPet();
        var qualification = AttackQualification.FromResolution(ResolutionWithSingleMatchOf(GemType.Def));

        // When: Boss attack resolved and applied
        var (updatedBoss, _, result) = AttackResolution.ResolveAndApplyBossAttack(
            boss,
            pet,
            qualification,
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        // Then: Not qualifying, modifier preserved
        Assert.False(result.IsQualifyingAttack);
        Assert.Single(updatedBoss.ActiveStatusEffects);
        Assert.Equal("thunder-strike", updatedBoss.ActiveStatusEffects[0].Id);
    }

    [Fact]
    public void BossNextQualifyingAttackModifier_WhenTheAtkMatchIsConfirmed_ShouldBeConsumed()
    {
        // Given: Boss with Thunder Strike NextQualifyingAttack modifier
        var bossModifier = StatusEffect.TriggerBased(
            "thunder-strike",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: 50,
            StatusEffect.NextQualifyingAttackCondition,
            targetStat: "ATK");
        var boss = DefaultBoss() with
        {
            ActiveStatusEffects = [bossModifier],
        };
        var pet = DefaultPet();
        var qualification = AttackQualification.FromResolution(ResolutionWithSingleMatchOf(GemType.Atk));

        // When: Boss attack resolved and applied
        var (updatedBoss, _, result) = AttackResolution.ResolveAndApplyBossAttack(
            boss,
            pet,
            qualification,
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        // Then: Qualifying attack, modifier consumed
        Assert.True(result.IsQualifyingAttack);
        Assert.Empty(updatedBoss.ActiveStatusEffects);
    }

    // =======================================================================
    // 7. Qualifying Attack with Zero Final Damage (Mitigation / Shield Absorption)
    // =======================================================================

    [Fact]
    public void QualifyingAttack_WhenFullyAbsorbedByShield_ShouldBeQualifyingAndConsumeModifiers()
    {
        // Given: Pet with NextAttack modifier, attacking Boss with massive Shield (5000)
        var pet = DefaultPet() with
        {
            NextAttackCritModifiers = [new NextAttackCritModifier("source_crit", 20)],
        };
        var shield = StatusEffect.TriggerBased(
            "Shield",
            StatusEffectType.Shield,
            StatusEffectSource.Boss,
            magnitude: 5000,
            StatusEffect.ShieldDepletedCondition);
        var boss = DefaultBoss() with
        {
            ActiveStatusEffects = [shield],
        };
        var qualification = AttackQualification.FromResolution(ResolutionWithSingleMatchOf(GemType.Atk));

        // When: Attack is resolved and applied
        var (updatedPet, updatedBoss, result) = AttackResolution.ResolveAndApplyPlayerAttack(
            pet,
            boss,
            qualification,
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        // Then:
        // 1. It IS a qualifying attack despite HP damage being 0
        Assert.True(result.IsQualifyingAttack);
        Assert.Equal(0, result.DamageResult!.Value.Calculation.FinalDamage - result.DamageResult.Value.AbsorbedDamage);
        Assert.Equal(boss.HP, updatedBoss.HP); // HP undamaged because Shield absorbed it
        // 2. Shield pool is reduced by the absorbed damage
        Assert.True(result.DefenderShieldPool < 5000);
        // 3. Damage events ARE emitted
        Assert.Equal(3, result.Events.Count);
        // 4. NextAttack modifier IS consumed
        Assert.Empty(updatedPet.NextAttackCritModifiers);
    }

    // =======================================================================
    // 8. Damage preservation — the qualification decides the branch and nothing else
    // =======================================================================

    [Fact]
    public void QualifyingAttack_ShouldProduceTheSameDamageWhateverTheQualificationCarriesAside()
    {
        // COMBAT_RULES.md §3.1 / D1: the qualification is the eligibility predicate
        // only. The instance's damage is the Damage Pipeline's own calculation over
        // the values its steps read (effective ATK, BaseDamagePool, Combo, Elements,
        // defender DEF), so resolving the same instance from a qualification derived
        // from real match evidence and from the bare qualifying value must produce the
        // identical result. In particular no cleared-gem or match-count evidence
        // reaches the formula.
        var pet = DefaultPet();
        var boss = DefaultBoss();
        var fromEvidence = AttackQualification.FromResolution(ResolutionWithSingleMatchOf(GemType.Atk));
        var fromValue = new AttackQualification(HasConfirmedAtkMatch: true);

        var viaEvidence = AttackResolution.ResolvePlayerAttack(
            pet,
            boss,
            fromEvidence,
            baseDamagePool: 30,
            combo: 2,
            rngState: default);

        var viaValue = AttackResolution.ResolvePlayerAttack(
            pet,
            boss,
            fromValue,
            baseDamagePool: 30,
            combo: 2,
            rngState: default);

        Assert.True(viaEvidence.IsQualifyingAttack);
        Assert.Equal(viaValue.FinalDamage, viaEvidence.FinalDamage);
        Assert.Equal(viaValue.DefenderHp, viaEvidence.DefenderHp);
        Assert.Equal(viaValue.DefenderShieldPool, viaEvidence.DefenderShieldPool);
        Assert.Equal(viaValue.RngState, viaEvidence.RngState);
        Assert.Equal(
            viaValue.DamageResult!.Value.Calculation,
            viaEvidence.DamageResult!.Value.Calculation);
        Assert.Equal(viaValue.Events.Count, viaEvidence.Events.Count);

        // And the non-qualifying value differs in the branch, not in any damage value:
        // it produces no instance at all rather than a zero-damage one.
        var nonQualifying = AttackResolution.ResolvePlayerAttack(
            pet,
            boss,
            AttackQualification.None,
            baseDamagePool: 30,
            combo: 2,
            rngState: default);

        Assert.False(nonQualifying.IsQualifyingAttack);
        Assert.Null(nonQualifying.DamageResult);
        Assert.Equal(boss.HP, nonQualifying.DefenderHp);
    }
}
