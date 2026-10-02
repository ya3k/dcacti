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
/// Root's ATK consumption end-to-end through a real Turn —
/// <c>COMBAT_RULES.md</c> §5.4 (TASK-119's resolved contract), whose MVP
/// instance is <c>BOSS_RULES.md</c> §6.3.1 item 3's Root.
///
/// <code>
/// Rule (COMBAT_RULES.md §5.4.3's Turn ordering, §5.4.1's Step-1 input)
///  ↓
/// Scenario (Given Root applied at Step 18b of Turn N, When Turn N / N+1 / N+2's
///           Step-15 attack resolves, Then the Step-1 Attack input is the
///           documented value)
///  ↓
/// Test
/// </code>
///
/// <b>Why this suite exists separately from the Domain consumer tests.</b> §5.4.3
/// is a statement about <i>ordering</i>: Root applied at step 18b of Turn N must
/// not retroactively modify that Turn's already-resolved step-15 attack, and its
/// expiry at step 19a of Turn N+1 must take effect from Turn N+2. Only a real
/// multi-Turn resolution can demonstrate that, so these scenarios drive
/// <see cref="BattleStateService"/> and read the Step-1 value out of the
/// <c>DamageCalculated</c> breakdown the resolution itself reports — never a value
/// the test computes for itself.
///
/// <b>Expected values trace to §5.4 and §6.3.1, not to the implementation.</b>
/// §5.4.1 item 3's <c>50 → 35</c>, §5.4.1's worked example shape (the pool is a
/// separate Step-1 contribution and the rejected reading is
/// <c>(ATK + pool) × 70%</c>), and §5.4.3's Turn N/N+1/N+2 timeline are asserted
/// directly.
///
/// <b>How the Step-1 <c>Attack</c> input is read.</b> <c>DamageCalculation</c>
/// reports Step 1's <i>sum</i> (<c>Base</c>) and not its ATK term, and the ATK-Gem
/// pool is the resolution's transient <c>Resources.BaseDamagePool</c>
/// (<c>GAME_STATE.md</c> §3). §3 step 1 fixes <c>Base = Attack + BaseDamagePool</c>,
/// so the ATK term the rule reduced is recovered from the two reported values
/// rather than recomputed — no test here re-implements the formula under test.
/// </summary>
public class RootAttackConsumptionTests
{
    /// <summary>
    /// The Pet these battles carry (<c>GAME_STATE.md</c> §2.3), Xích Lang's MVP
    /// Element and Passive. Its ATK is the documented MVP default
    /// (<c>COMBAT_RULES.md</c> §1.1: <c>ATK = 50</c>), so the −30% result
    /// <c>35</c> is §5.4.2's first worked value.
    /// </summary>
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>The owning Player of these battles (<c>GAME_STATE.md</c> §2.8).</summary>
    private static readonly PlayerId Owner = new("player_root_consumption_owner");

    // =======================================================================
    // Same-Turn behavior — COMBAT_RULES.md §5.4.3
    // =======================================================================

    [Fact]
    public async Task Root_ShouldNotAffectTheAttackOfTheTurnThatAppliedIt()
    {
        // §5.4.3: "an instance applied at a later step of the SAME Turn cannot
        // affect that Turn's already-resolved attack". Step 15 runs before step 18b,
        // so the swap that casts Root attacks with the UNREDUCED ATK.
        //
        // The Pet's ATK is the documented MVP 50 (COMBAT_RULES.md §1.1), so the
        // resolving Turn's Step-1 ATK term is 50 — not 35.
        var harness = Harness.Create();
        var casting = await CastRootAsync(harness);

        Assert.Equal(50, casting.Attack);

        // And Root genuinely WAS applied by that same resolution — the negative
        // above is not the trivial result of the effect never firing.
        var root = Assert.Single(RootsOn(casting.Committed.PetState));

        Assert.Equal(30, root.Magnitude);
        Assert.Equal("ATK", root.TargetStat);
    }

    [Fact]
    public async Task Root_ShouldBeAppliedWithTwoTurns_AndConsumeOneAtTheResolvingTurnsStep19a()
    {
        // §6.3.1 item 3 applies Root with RemainingTurns = 2; §5.3 DR2/DR3 /
        // GAME_STATE.md §5.1.1 item 2 make the resolving Turn's step 19a consume
        // exactly one of them, so the COMMITTED value is 1. §5.4.3's timeline shows
        // exactly this (step 18b sets 2; step 19a turns it into 1), and the applied
        // 2 is asserted on the declaration, where it is not already decremented.
        var harness = Harness.Create();
        var casting = await CastRootAsync(harness);

        Assert.Equal(1, Assert.Single(RootsOn(casting.Committed.PetState)).RemainingTurns);

        var declared = BossDefinitions.MocYeu.SkillDefinition.SecondaryEffect!.Value;

        Assert.Equal(2, declared.DurationTurns);
    }

    // =======================================================================
    // Next-Turn behavior, expiry, and recovery — COMBAT_RULES.md §5.4.3
    // =======================================================================

    [Fact]
    public async Task Root_ShouldReduceTheNextTurnsAttack_ThenExpireAndRestoreIt()
    {
        // §5.4.3's complete documented timeline, driven through three real Turns:
        //
        //   Turn N   step 15   Root NOT yet applied — attack unaffected  (50)
        //            step 18b  Root applied, RemainingTurns = 2
        //            step 19a  2 → 1
        //
        //   Turn N+1 step 15   Root IS active — Pet ATK reduced by 30%  (35)
        //            step 19a  1 → 0 → expires
        //
        //   Turn N+2           inactive — normal derived value again     (50)
        var harness = Harness.Create();
        var casting = await CastRootAsync(harness);

        Assert.Equal(50, casting.Attack);

        // Turn N+1: Root is active DURING step 15, so the Step-1 ATK term is
        // truncate(50 × 70 / 100) = 35 (§5.4.1 item 3, §5.4.2's worked value).
        var turnNPlus1 = await ResolveNextTurnAsync(harness, casting.BattleId);

        Assert.Equal(35, turnNPlus1.Attack);

        // Turn N+1's step 19a consumes the instance's last Turn (1 → 0) and removes
        // it in that same resolution (§5.3 DR5, §2.3.1 item 8 — a stored 0 is never
        // observable), so it is already absent from the committed state of the Turn
        // whose attack it reduced. That is §5.4.3's timeline exactly: the reduction
        // is read at step 15, and the removal happens later in the same Turn.
        Assert.Empty(RootsOn(turnNPlus1.Committed.PetState));

        // Turn N+2: inactive — the attack is unreduced again.
        var turnNPlus2 = await ResolveNextTurnAsync(harness, casting.BattleId);

        Assert.Equal(50, turnNPlus2.Attack);
        Assert.Empty(RootsOn(turnNPlus2.Committed.PetState));
    }

    [Fact]
    public async Task Root_ShouldNeverOverwriteTheStoredPetAtk()
    {
        // §5.4.4: "PetState.ATK is never overwritten by the modifier, and there is
        // no 'restore' step"; §2.3.1 item 12 records that the rule composes an
        // effective value and stores nothing. The stored stat must therefore be
        // identical before, during, and after the modifier's whole lifetime — which
        // is what makes the Turn N+2 value correct without any reset.
        var harness = Harness.Create();
        var casting = await CastRootAsync(harness);

        Assert.Equal(50, casting.Committed.PetState.ATK);
        Assert.Equal(1, Assert.Single(RootsOn(casting.Committed.PetState)).RemainingTurns);

        // The Turn whose attack IS reduced: the stored stat is still 50 while the
        // attack used 35.
        var turnNPlus1 = await ResolveNextTurnAsync(harness, casting.BattleId);

        Assert.Equal(35, turnNPlus1.Attack);
        Assert.Equal(50, turnNPlus1.Committed.PetState.ATK);

        // And after expiry it is still 50 — no restore step existed or was needed.
        var turnNPlus2 = await ResolveNextTurnAsync(harness, casting.BattleId);

        Assert.Equal(50, turnNPlus2.Committed.PetState.ATK);
        Assert.Equal(50, turnNPlus2.Attack);
    }

    // =======================================================================
    // ATK-only application — COMBAT_RULES.md §5.4.1 item 2
    // =======================================================================

    [Fact]
    public async Task Root_ShouldNotReduceTheResourceGeneratedDamagePool()
    {
        // §5.4.1 item 2: the reduction applies to PetState.ATK ALONE, and the
        // ATK-Gem-generated damage pool is a separate Step-1 contribution that is
        // NOT modified. Step 1 is therefore EffectiveATK + pool — never
        // (ATK + pool) × 70%, which is §5.4.1's explicitly rejected 98-style
        // reading.
        //
        // A single Swap does not always clear ATK Gems, so the property is asserted
        // over several reduced Turns and the pooled ones are the ones that carry the
        // §5.4.1 worked example's shape.
        var harness = Harness.Create();
        var battleId = $"root-consumption-pool-{Guid.NewGuid():N}";

        // The Boss's Skill is made ineligible for the whole window (a Charge
        // Requirement no Swap's Matches can reach), so the ONLY Root instance in play
        // is the seeded one. Letting the Skill fire would refresh the seeded instance
        // through §5.3 DR3 — correct documented behavior, but it would reset the
        // duration to §6.3.1 item 3's 2 Turns and cut the window short before a
        // pooled Turn is guaranteed to occur.
        var silentBoss = BossDefinitions.MocYeu with
        {
            SkillDefinition = BossDefinitions.MocYeu.SkillDefinition with
            {
                ChargeRequirement = 100_000,
            },
        };

        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, silentBoss);

        // Seed Root through the documented Domain operation (the same Apply the
        // step 18b resolution uses). The instance's magnitude, TargetStat, and Type
        // are §6.3.1 item 3's and §5.4.1's exactly; only the duration is longer than
        // the applied 2, so the instance stays active across the observation window
        // instead of expiring midway through it.
        await SeedPetStatusEffectAsync(
            harness.Store,
            battleId,
            StatusEffect.TurnBased("Root", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 30, 20, "ATK"));

        // The seed wrote through the store, so the loop must start from the POST-seed
        // state rather than the pre-seed creation snapshot — re-reading it is what
        // keeps the seeded instance in scope for the first Turn.
        var state = (await harness.Service.GetBattleAsync(battleId))!;

        Assert.Single(RootsOn(state.PetState));
        Assert.DoesNotContain(state.BossState.ActiveStatusEffects, e => e.Id == "Root");

        var pooledReducedTurns = 0;

        for (var turn = 0; turn < 4; turn++)
        {
            var resolved = await ResolveNextTurnAsync(harness, battleId, state);

            // The Skill never fires, so Root can only have come from the seed — which
            // is stated rather than assumed.
            Assert.DoesNotContain(resolved.Events, e => e.Type == BattleEventType.BossSkillCast);

            // Root is active for every one of these Turns, so the ATK term is
            // truncate(50 × 70 / 100) = 35 regardless of what the Swap cleared.
            Assert.Equal(35, resolved.Attack);

            // Step 1 is the pipeline's own reported sum, and it is the sum of the
            // two separate contributions.
            Assert.Equal(resolved.Attack + resolved.Pool, resolved.Base);

            if (resolved.Pool > 0)
            {
                pooledReducedTurns++;

                // §5.4.1: "It is NOT (100 + 40) × 70% = 98". The general form of the
                // rejected reading is (ATK + pool) × 70%, asserted as a negative so a
                // sum-then-reduce implementation fails loudly. This is the assertion
                // an empty-pool Turn could not make, which is why the loop counts
                // them and the count is asserted below.
                Assert.NotEqual((50 + resolved.Pool) * 70 / 100, resolved.Base);
            }

            // The instance's 20-Turn duration is well beyond this 4-Turn window, so
            // the next iteration starts from the state this Turn committed.
            state = resolved.Committed;
        }

        // The negative above only means something if at least one observed reduced
        // Turn actually carried a pool, so the coverage is asserted rather than
        // assumed — otherwise the test could silently pass by never exercising the
        // case it exists for.
        Assert.True(
            pooledReducedTurns > 0,
            "at least one reduced Turn must carry an ATK-Gem-generated pool for the "
            + "§5.4.1 'not (ATK + pool) × 70%' guard to be exercised");
    }

    // =======================================================================
    // Data-driven dispatch — TASK-118 §10
    // =======================================================================

    [Fact]
    public async Task Root_ShouldBeConsumedFromTheDeclarationAndNotFromTheSkillId()
    {
        // TASK-118 §10: the behavior must come from
        // BossSkillDefinition.SecondaryEffect, and no resolution site may dispatch
        // on the SkillId string. This drives the consumption with the SkillId
        // renamed to a value that matches nothing, so a SkillId/name/description
        // dispatch would consume nothing and the reduced Turn's attack would stay 50.
        //
        // Every magnitude and duration is still §6.3.1 item 3's — only the
        // identifier the consumer must NOT be reading is changed. The Skill still
        // fires because eligibility is charge/cooldown (GAME_STATE.md §2.4.3).
        var harness = Harness.Create();
        var battleId = $"root-consumption-not-skill-id-dispatch-{Guid.NewGuid():N}";

        var renamed = BossDefinitions.MocYeu with
        {
            SkillDefinition = BossDefinitions.MocYeu.SkillDefinition with
            {
                SkillId = "not-the-documented-skill-id",
                ChargeRequirement = 1,
                CooldownTurns = 3,
            },
        };

        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, renamed);
        var casting = await ResolveNextTurnAsync(harness, battleId, created);

        Assert.Contains(casting.Events, e => e.Type == BattleEventType.BossSkillCast);
        Assert.Equal(50, casting.Attack);
        Assert.Single(RootsOn(casting.Committed.PetState));

        var reduced = await ResolveNextTurnAsync(harness, battleId);

        Assert.Equal(35, reduced.Attack);
    }

    [Fact]
    public async Task Root_ShouldNotAffectTheBossOwnAttack()
    {
        // COMBAT_RULES.md §5.4.5: the rule "Does NOT apply to the Boss's damage —
        // §3.4 pins the Boss side's Step 4 to 1.0, and this rule authors no
        // Boss-side factor". The Pet is Root's owner, so the Boss's own instance in
        // the same reduced Turn must be unaffected.
        //
        // The reduced Turn is the one AFTER the cast, and Mộc Yêu's cooldown makes
        // that Turn a §18c Basic Attack. Both arms are asserted by identity rather
        // than assumed, so the test states which documented Boss action it observed.
        var harness = Harness.Create();
        var casting = await CastRootAsync(harness);
        var turnNPlus1 = await ResolveNextTurnAsync(harness, casting.BattleId);

        // Turn N+1's Player attack IS reduced — the Boss-side assertion below is
        // therefore made on a Turn where Root is genuinely in force.
        Assert.Equal(35, turnNPlus1.Attack);

        // The Boss's instance follows the Player's (GAME_RULES.md §17 steps 15–17
        // then 18b/18c), so it is the first DamageCalculated after the Player's.
        var bossCalculations = turnNPlus1.Events
            .Where(e => e.Type == BattleEventType.DamageCalculated)
            .Skip(1)
            .Select(e => e.DamageCalculated)
            .ToArray();

        Assert.NotEmpty(bossCalculations);

        // §6.1 gives every MVP Boss ATK 100 and §6.3 gives Mộc Yêu SkillBaseDamage
        // 100. §3.4 fixes the Skill's Step 1 as EffectiveBossATK + SkillBaseDamage and
        // the Basic Attack's as EffectiveBossATK alone (§5.5.1 derives
        // EffectiveBossATK from BossState.ATK; no MVP path applies a Boss ATK modifier
        // yet). This Turn is the Basic Attack (the Skill is on cooldown), so the
        // documented value is the stored ATK, 100 — and a Boss-side ATK factor would
        // move it.
        var skillFired = turnNPlus1.Events.Any(e => e.Type == BattleEventType.BossSkillCast);

        Assert.False(skillFired);
        Assert.Equal(100, bossCalculations[0].Base);

        // §3.4: the Boss side's Step 4 stays 1.0 ("MVP: no Relic/Passive/Buff
        // modifiers on Boss side"). The Boss passes AttackerCrit 0, so no Crit roll
        // can succeed and OtherModifiers is exactly the identity — Root authors no
        // Boss-side factor.
        Assert.Equal(1.00, bossCalculations[0].OtherModifiers);
    }

    [Fact]
    public async Task Root_ShouldBeTheOnlyModifierConsumed_WhenABurnIsAlsoActive()
    {
        // §5.4.5: "Does NOT apply to a DoT tick or a Shield: those Types have their
        // own rules (§5.2 item 3, §4) and are not ATK modifiers." With a Boss-sourced
        // Burn active alongside Root, the ATK reduction is Root's alone — the DoT
        // instance neither adds a second reduction nor is reduced itself.
        var harness = Harness.Create();
        var battleId = $"root-consumption-with-burn-present-{Guid.NewGuid():N}";

        var created = await harness.Service.CreateBattleAsync(
            battleId, Owner, Pet, MocYeuCastingOnce());

        // Seed a Boss-sourced Burn instance through the documented Domain operation
        // — the same Apply the step 18b resolution uses — because Flame Burst is
        // Hỏa Long's Skill and no MVP Boss casts both.
        await SeedPetStatusEffectAsync(
            harness.Store,
            battleId,
            StatusEffect.TurnBased("Burn", StatusEffectType.DoT, StatusEffectSource.Boss, 50, 5));

        var casting = await ResolveNextTurnAsync(harness, battleId, created);
        var pet = casting.Committed.PetState;

        Assert.Contains(pet.ActiveStatusEffects, e => e.Id == "Burn");
        Assert.Contains(pet.ActiveStatusEffects, e => e.Id == "Root");

        // The seeded Burn has 5 Turns, so it survives into the reduced Turn.
        var reduced = await ResolveNextTurnAsync(harness, battleId);

        // Exactly the −30%: a DoT instance wrongly treated as an ATK modifier would
        // make this 35 × (100 − 50) / 100 = 17, and folding the Burn magnitude into
        // the ATK term would move it elsewhere.
        Assert.Equal(35, reduced.Attack);
        Assert.Contains(reduced.Committed.PetState.ActiveStatusEffects, e => e.Id == "Burn");
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    /// <summary>
    /// One resolved Turn's Player → Boss damage terms, the resolution's own events,
    /// and the resulting committed state.
    /// </summary>
    /// <param name="BattleId">The battle the Turn resolved on.</param>
    /// <param name="Attack">
    /// Step 1's ATK term — the value <c>COMBAT_RULES.md</c> §5.4 reduces. It is
    /// recovered as <c>Base − BaseDamagePool</c> from the pipeline's own report and
    /// the resolution's transient pool (§3 step 1), never recomputed by the test.
    /// </param>
    /// <param name="Pool">The resolution's ATK-Gem-generated pool (<c>GAME_STATE.md</c> §3).</param>
    /// <param name="Base">Step 1's sum as the pipeline reported it.</param>
    /// <param name="Events">The resolution's events (<c>GAME_EVENTS.md</c> §1).</param>
    /// <param name="Committed">The battle state the resolution committed.</param>
    private readonly record struct ResolvedTurn(
        string BattleId,
        int Attack,
        int Pool,
        int Base,
        IReadOnlyList<BattleEvent> Events,
        BattleState Committed);

    /// <summary>
    /// Resolves one Turn on a battle and reports the Player → Boss instance's Step-1
    /// terms. The Player's instance is the FIRST <c>DamageCalculated</c>
    /// (<c>GAME_RULES.md</c> §17 steps 15–17 precede 18b/18c), which is the
    /// documented correlation <c>BattleStateServiceTests</c> already relies on —
    /// <c>DamageCalculation</c> itself carries no direction member.
    /// </summary>
    private static async Task<ResolvedTurn> ResolveNextTurnAsync(
        Harness harness,
        string battleId,
        BattleState? startingState = null)
    {
        var service = harness.Service;
        var before = startingState ?? (await service.GetBattleAsync(battleId))!;

        var result = (await service.ExecuteSwapAsync(battleId, FindMatchProducer(before)))
            ?? throw new InvalidOperationException("the Swap must resolve");

        Assert.True(result.IsAccepted);

        var committed = (await service.GetBattleAsync(battleId))!;
        var calculation = result.Events
            .First(e => e.Type == BattleEventType.DamageCalculated)
            .DamageCalculated;

        // §3 step 1: Base = Attack + BaseDamagePool. Reading the ATK term as the
        // difference keeps the asserted value the pipeline's own arithmetic.
        var pool = result.Resources.BaseDamagePool;

        return new ResolvedTurn(
            battleId,
            calculation.Base - pool,
            pool,
            calculation.Base,
            result.Events,
            committed);
    }

    /// <summary>
    /// Resolves Mộc Yêu's Root on a fresh battle by making one Swap's Matches
    /// satisfy the Skill's Charge Requirement, and returns that Turn's terms.
    /// </summary>
    private static async Task<ResolvedTurn> CastRootAsync(Harness harness)
    {
        var battleId = $"root-consumption-cast-{Guid.NewGuid():N}";

        var created = await harness.Service.CreateBattleAsync(
            battleId, Owner, Pet, MocYeuCastingOnce());

        var result = await ResolveNextTurnAsync(harness, battleId, created);

        Assert.Contains(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        return result;
    }

    /// <summary>
    /// Mộc Yêu configured so Root is applied exactly ONCE and then has room to
    /// expire: a Charge Requirement one Match satisfies, and a cooldown long enough
    /// that the Skill cannot fire again within the Turns a scenario observes.
    ///
    /// <b>Why a cooldown is required, and why it is not an invented scenario.</b>
    /// With <c>CooldownTurns = 0</c> the Skill is eligible on every Swap, so Root is
    /// re-applied each Turn and §5.3 DR3/DR4 refresh it back to 2 before every
    /// step-19a decrement — a state in which the instance never expires and
    /// <c>RemainingTurns</c> stays at 1 indefinitely. That is the documented refresh
    /// behavior working correctly, but it cannot demonstrate §5.4.3's
    /// expiry-and-recovery Turn. §6.3 gives Mộc Yêu <c>CD 2T</c>, so a cooldown is
    /// this Skill's own documented shape; the value here only has to outlast the
    /// three Turns a scenario observes.
    /// </summary>
    private static BossDefinition MocYeuCastingOnce() =>
        BossDefinitions.MocYeu with
        {
            SkillDefinition = BossDefinitions.MocYeu.SkillDefinition with
            {
                ChargeRequirement = 1,
                CooldownTurns = 3,
            },
        };

    private static StatusEffect[] RootsOn(PetState pet) =>
        [.. pet.ActiveStatusEffects.Where(e => e.Id == "Root")];

    /// <summary>
    /// Seeds a Pet Status Effect instance through the documented Domain operation
    /// (<c>GAME_STATE.md</c> §5.1.1 item 1), preserving every other member and
    /// leaving <c>Sequence</c> untouched.
    ///
    /// <b>Why the store is written directly.</b> A Burn instance alongside Root is
    /// not a state any MVP Boss's own Skill can produce — Flame Burst is Hỏa
    /// Long's and Root is Mộc Yêu's — so the coexistence case §5.4.5's scope rule
    /// bears on is arranged by applying the instance with the same public
    /// <see cref="StatusEffectLifecycle.Apply"/> the resolution uses. This applies
    /// no game rule and widens no production API for a test.
    /// </summary>
    private static async Task SeedPetStatusEffectAsync(
        IBattleStateRepository repository,
        string battleId,
        StatusEffect effect)
    {
        var stored = (await repository.GetAsync(battleId))!;

        var applied = await repository.TryUpdateAsync(
            stored with
            {
                PetState = stored.PetState with
                {
                    ActiveStatusEffects = StatusEffectLifecycle.Apply(
                        stored.PetState.ActiveStatusEffects,
                        effect),
                },
            },
            stored.Sequence);

        Assert.True(applied);
    }

    private static SwapRequest FindMatchProducer(BattleState state) =>
        FindMatchProducingPair(state);

    /// <summary>
    /// A battle service over an in-memory store, plus the store itself, so a
    /// scenario can both run real Turns and arrange the one documented state no
    /// single Boss's Skill produces.
    ///
    /// The double models the documented creation and <c>Sequence</c>
    /// compare-and-set (<c>REDIS_STATE.md</c> §4), so no test here passes against a
    /// more permissive contract than the store offers.
    /// </summary>
    private sealed record Harness(BattleStateService Service, InMemoryBattleStateRepository Store)
    {
        public static Harness Create()
        {
            var store = new InMemoryBattleStateRepository();

            return new Harness(new BattleStateService(store, new FixedRngSeedSource()), store);
        }
    }

    private static SwapRequest FindMatchProducingPair(BattleState state)
    {
        var committed = state.LastCommittedSwapPair;

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
