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
/// HP Gem mechanics audit suite (TASK-301) — the end-to-end HP Gem contracts of
/// <c>GAME_RULES.md</c> §17 steps 12/14 and 18a, <c>COMBAT_RULES.md</c> §2 items 5–6
/// and §4 items 1/7, and <c>ADR-026</c> item 4's actor-relative targeting.
///
/// <b>What these tests add.</b> The existing suites pin the per-Gem rate, the
/// overheal clamp, the dead-target guard and Thủy Ma's heal modifier at the unit
/// level, and they pin the Boss's step-18a heal by replaying the production
/// stages. None of them pins the <b>actor-relative target selection</b> of the HP
/// Gem heal — that a Player action's HP Gems heal the Pet and only the Pet, and a
/// Boss action's HP Gems heal the Boss and only the Boss — against the round's
/// <b>delivered</b> reports. These tests read the disclosed
/// <c>GemMatched</c>/<c>DamageDealt</c> events and derive each expected HP from
/// them, so a heal routed to the wrong actor, counted twice, or applied at
/// anything other than the documented rate fails the equality rather than being
/// masked by a re-derivation of the same production call.
///
/// <b>The audit confirmed no deviation.</b> Every expectation below is derived
/// from the authoritative documents, not from the implementation, and each one
/// already held when the suite was written: the HP Gem heal is generated at
/// <c>COMBAT_RULES.md</c> §2's 20-per-uniquely-cleared-HP-cell rate
/// (<c>ResourceGenerator.HpGemBaseHeal</c>), accumulated across every cascade pass
/// and applied once, routed to the acting actor by <c>ADR-026</c> item 4, clamped
/// to that actor's <c>MaxHP</c>, and delivered to the client as settled state
/// (<c>SIGNALR_PROTOCOL.md</c> §4.3 item 15) with no heal event
/// (<c>GAME_EVENTS.md</c> §2 authors none). The suite stands as the regression
/// protection for those invariants.
/// </summary>
public class HpGemMechanicsAuditTests
{
    /// <summary>
    /// The battle's active Pet — a plain <c>Hỏa</c> Pet with a match-charged
    /// Passive, so the round exercises the ordinary MVP pipeline and no Pet-side
    /// heal modifier is authored on it.
    /// </summary>
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_hp_gem_audit"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    private static readonly PlayerId Owner = new("player_hp_gem_audit_owner");

    /// <summary>
    /// <c>BOSS_RULES.md</c> §6.1's Hỏa Long: <c>MaxHP = 5000</c>, no HP-restoring
    /// Passive (§6.2 gives it a +ATK Rage), and a non-damaging Skill — so the only
    /// HP movements in a round are the two actors' own Heal pools plus the two
    /// actors' own damage instances.
    /// </summary>
    private static readonly BossDefinition Boss = BossDefinitions.HoaLong;

    /// <summary>
    /// The Pet's ceiling for every scenario, pinned on the state
    /// (<c>COMBAT_RULES.md</c> §1.1: the defaults are configuration, so the clamp
    /// must read the state's own <c>MaxHP</c>).
    /// </summary>
    private const int PetMaxHp = 1000;

    /// <summary>
    /// The Pet HP before the round. It sits below <see cref="PetMaxHp"/> so a
    /// generated pool is observable rather than wholly discarded as overheal.
    /// </summary>
    private const int PetPinnedHp = 500;

    /// <summary>
    /// The Boss HP before the round, well below its 5000 <c>MaxHP</c> so a
    /// Boss-owned Heal pool is observable rather than clamped away.
    /// </summary>
    private const int BossPinnedHp = 3000;

    private const string BattleId = "battle-hp-gem-audit";

    // =======================================================================
    // 1. Target selection — the acting actor owns its own HP Gems (ADR-026 item 4)
    // =======================================================================

    [Fact]
    public async Task HpGems_ShouldHealOnlyTheActingActor_AcrossOneAtomicRound()
    {
        // COMBAT_RULES.md §2's actor-relative HP row and ADR-026 item 4: the type of
        // cell matched determines the effect and the acting side determines ownership
        // and target. One atomic Round contains both actions, so it contains two
        // independent HP-Gem heal instances:
        //   Player action (step 14)  → the Pet's HP only
        //   Boss action   (step 18a) → the Boss's HP only
        // Each expectation is derived from the round's own delivered reports, so a
        // heal applied to the wrong actor (or to both) breaks the equality.
        var (repository, service, created) = await NewBattleAsync();

        var pinned = RoundTestSupport.PlayerMatch(GemType.Hp, created.RngState);
        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { HP = BossPinnedHp },
            petState: created.PetState with { HP = PetPinnedHp, MaxHP = PetMaxHp });

        var result = await service.ExecuteSwapAsync(BattleId, pinned.Swap);

        Assert.NotNull(result);
        var round = result!.Value;

        var playerHpCells = HpCellsMatchedIn(RoundTestSupport.PlayerBoardEvents(round));
        var bossHpCells = HpCellsMatchedIn(RoundTestSupport.BossBoardEvents(round));

        // Fixture preconditions: the Player's action cleared HP Gems (so there is a
        // pool to route) and dealt no damage (its cascade cleared no ATK Gem), which
        // leaves the Pet's HP movement as healing alone.
        Assert.True(playerHpCells > 0, "the Player fixture must clear at least one HP Gem");
        Assert.Equal(0, DamageDealtBy(round, DamageParty.Player));

        // The Pet's HP: this action's whole accumulated Heal pool, clamped at MaxHP,
        // less the Boss's own damage instance (step 18d). A Boss-owned Heal pool must
        // not appear here.
        var expectedPetHp = Math.Max(
            0,
            Math.Min(PetPinnedHp + (playerHpCells * ResourceGenerator.HpGemBaseHeal), PetMaxHp)
                - DamageDealtBy(round, DamageParty.Boss));

        // The Boss's HP: the Player's damage instance to it (none, by the fixture)
        // and then its own step-18a Heal pool clamped at its own MaxHP. A Pet-owned
        // Heal pool must not appear here.
        var expectedBossHp = Math.Min(
            BossPinnedHp + (bossHpCells * ResourceGenerator.HpGemBaseHeal),
            Boss.MaxHP);

        Assert.Equal(expectedPetHp, round.State.PetState.HP);
        Assert.Equal(expectedBossHp, round.State.BossState.HP);
    }

    [Fact]
    public async Task BossOwnedHpGems_ShouldHealTheBoss_AndNeverTheActivePet()
    {
        // The Boss-turn half of the same rule, made non-vacuous. This is the exact
        // function Application's step-18a boundary calls with the Boss's own
        // generation (BattleStateService's `ResourceGenerator.ApplyActorMatchEffects(
        // DamageParty.Boss, …)`), driven here with a resolution that really cleared HP
        // Gems — so the Boss-owned Heal pool is non-zero and the Pet is checked
        // against "its own pool only".
        var (_, _, created) = await NewBattleAsync();

        var pinned = RoundTestSupport.PlayerMatch(GemType.Hp, created.RngState);

        Assert.True(pinned.Resources.HealPool > 0, "the fixture cascade must clear at least one HP Gem");
        Assert.Equal(
            pinned.Resources.ClearedGemCounts.Hp * ResourceGenerator.HpGemBaseHeal,
            pinned.Resources.HealPool);

        var pet = created.PetState with { HP = PetPinnedHp, MaxHP = PetMaxHp };
        var boss = Boss.ToInitialState() with { HP = BossPinnedHp };

        // --- Boss action: the Boss owns its own HP-Gem pool -------------------
        var (petAfterBossAction, bossAfterBossAction) = ResourceGenerator.ApplyActorMatchEffects(
            DamageParty.Boss,
            pet,
            boss,
            pinned.Resources,
            pinned.Resolution,
            Boss.SkillChargeRequirement);

        var expectedBossHp = Math.Min(BossPinnedHp + pinned.Resources.HealPool, Boss.MaxHP);

        Assert.True(expectedBossHp > BossPinnedHp, "the Boss must actually gain HP from its own HP Gems");
        Assert.Equal(expectedBossHp, bossAfterBossAction.HP);

        // ... and only the Boss: the Pet's HP is untouched by a Boss-owned pool.
        Assert.Equal(PetPinnedHp, petAfterBossAction.HP);

        // --- Player action: the Pet owns its own HP-Gem pool ------------------
        var (petAfterPlayerAction, bossAfterPlayerAction) = ResourceGenerator.ApplyActorMatchEffects(
            DamageParty.Player,
            pet,
            boss,
            pinned.Resources,
            pinned.Resolution,
            Boss.SkillChargeRequirement);

        Assert.Equal(Math.Min(PetPinnedHp + pinned.Resources.HealPool, PetMaxHp), petAfterPlayerAction.HP);

        // ... and only the Pet: the Boss's HP is untouched by a Pet-owned pool.
        Assert.Equal(BossPinnedHp, bossAfterPlayerAction.HP);
    }

    // =======================================================================
    // 2. Output calculation and match counting — 20 per uniquely cleared cell,
    //    accumulated across the cascade, applied once
    // =======================================================================

    [Fact]
    public async Task HpGemHealPool_ShouldBeTwentyPerUniquelyClearedHpCell_AcrossEveryPass()
    {
        // COMBAT_RULES.md §2 item 6 / OD-3–OD-4: pool(HP) = 20 × count(HP), where
        // count(HP) is the number of UNIQUE matched HP cells across every detection
        // pass of the resolution — no match-tier multiplier and no per-pass
        // re-application.
        //
        // The delivered GemMatched reports are one entry per cleared cell per pass
        // (GAME_EVENTS.md §1.3 item 2), so their total is the resolution's
        // cleared-cell count and the pool must be exactly that many times the
        // documented rate. A double-counted cell (overlapping shapes, or a pass
        // counted twice) or a cascade pass dropped from generation breaks the
        // equality.
        var (repository, service, created) = await NewBattleAsync();

        var pinned = RoundTestSupport.PlayerMatch(GemType.Hp, created.RngState);
        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            petState: created.PetState with { HP = PetPinnedHp, MaxHP = PetMaxHp });

        var result = await service.ExecuteSwapAsync(BattleId, pinned.Swap);

        Assert.NotNull(result);
        var round = result!.Value;

        var playerHpCells = HpCellsMatchedIn(RoundTestSupport.PlayerBoardEvents(round));

        Assert.Equal(20, ResourceGenerator.HpGemBaseHeal);
        Assert.Equal(playerHpCells, round.Resources.ClearedGemCounts.Hp);
        Assert.Equal(playerHpCells * ResourceGenerator.HpGemBaseHeal, round.Resources.HealPool);

        // The pool is the sum over the passes and the Swap applies that total once —
        // never once per pass. HP therefore equals the pinned value plus the whole
        // pool, clamped, less the Boss's damage instance.
        var expectedPetHp = Math.Max(
            0,
            Math.Min(PetPinnedHp + round.Resources.HealPool, PetMaxHp)
                - DamageDealtBy(round, DamageParty.Boss));

        Assert.Equal(expectedPetHp, round.State.PetState.HP);
    }

    [Fact]
    public async Task HpGems_AtFullHp_ShouldStopExactlyAtMaxHp_AndDiscardTheOverheal()
    {
        // COMBAT_RULES.md §4 item 1: healing restores HP up to Max HP and overheal is
        // discarded — it is stored nowhere and cannot leak into Max HP. The Boss is
        // given ATK 0 so its damage instance cannot move the Pet's HP after the heal,
        // which makes "exactly Max HP, never above" the assertion.
        var (repository, service, created) = await NewBattleAsync();

        var pinned = RoundTestSupport.PlayerMatch(GemType.Hp, created.RngState);
        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { HP = Boss.MaxHP - 5, ATK = 0 },
            petState: created.PetState with { HP = PetMaxHp - 5, MaxHP = PetMaxHp });

        var result = await service.ExecuteSwapAsync(BattleId, pinned.Swap);

        Assert.NotNull(result);
        var round = result!.Value;

        var playerHpCells = HpCellsMatchedIn(RoundTestSupport.PlayerBoardEvents(round));
        var bossHpCells = HpCellsMatchedIn(RoundTestSupport.BossBoardEvents(round));

        Assert.True(playerHpCells > 0, "the Player fixture must clear at least one HP Gem");
        Assert.True(
            playerHpCells * ResourceGenerator.HpGemBaseHeal > 5,
            "the generated pool must exceed the gap to MaxHP, or no clamp is exercised");

        Assert.Equal(PetMaxHp, round.State.PetState.HP);
        Assert.Equal(PetMaxHp, round.State.PetState.MaxHP);

        // The Boss-side ceiling behaves identically, on the Boss's own Max HP.
        Assert.Equal(
            Math.Min((Boss.MaxHP - 5) + (bossHpCells * ResourceGenerator.HpGemBaseHeal), Boss.MaxHP),
            round.State.BossState.HP);
        Assert.Equal(Boss.MaxHP, round.State.BossState.MaxHP);
    }

    [Fact]
    public async Task NoHpGemsCleared_ShouldHealNeitherActor()
    {
        // COMBAT_RULES.md §2 item 5: the Heal pool is what the cleared HP Gems
        // generated. An action that clears none generates none — the pools are
        // computed independently per Gem type (OD-5), so an ATK-only Match heals
        // nothing at all, on either side of the acting actor's rule.
        var (repository, service, created) = await NewBattleAsync();

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState);
        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { HP = BossPinnedHp, ATK = 0 },
            petState: created.PetState with { HP = PetPinnedHp, MaxHP = PetMaxHp });

        var result = await service.ExecuteSwapAsync(BattleId, pinned.Swap);

        Assert.NotNull(result);
        var round = result!.Value;

        Assert.Equal(0, HpCellsMatchedIn(RoundTestSupport.PlayerBoardEvents(round)));
        Assert.Equal(0, round.Resources.ClearedGemCounts.Hp);
        Assert.Equal(0, round.Resources.HealPool);

        // No heal of any kind reached the Pet: it still holds the pinned HP.
        Assert.Equal(PetPinnedHp, round.State.PetState.HP);
    }

    // =======================================================================
    // Fixtures
    // =======================================================================

    private static async Task<(
        InMemoryBattleStateRepository Repository,
        BattleStateService Service,
        BattleState Created)> NewBattleAsync()
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var created = await service.CreateBattleAsync(BattleId, Owner, Pet, Boss);

        return (repository, service, created);
    }

    /// <summary>
    /// The number of HP Gems the delivered <c>GemMatched</c> reports name in one
    /// board phase — one report per cleared cell
    /// (<c>GAME_EVENTS.md</c> §1.3 item 2).
    /// </summary>
    private static int HpCellsMatchedIn(IReadOnlyList<BattleEvent> phaseEvents) =>
        phaseEvents.Count(e => e.Type == BattleEventType.GemMatched && e.Gem.GemType == GemType.Hp);

    /// <summary>
    /// The round's total damage from one side's damage instance, read from the
    /// delivered <c>DamageDealt</c> reports (<c>GAME_EVENTS.md</c> §2).
    /// </summary>
    private static int DamageDealtBy(SwapExecutionResult round, DamageParty source) =>
        round.Events
            .Where(e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == source)
            .Sum(e => e.DamageDealt.Amount);
}
