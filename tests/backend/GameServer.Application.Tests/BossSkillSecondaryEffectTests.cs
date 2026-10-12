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
/// Boss Skill secondary effects — <c>GAME_RULES.md</c> §17 step 18c's
/// "execute the non-damaging Skill", whose per-Boss magnitudes and duration
/// semantics are <c>BOSS_RULES.md</c> §6.3/§6.3.1 and whose representation is
/// <c>ADR-026</c> D3 and D5–D7.
///
/// <code>
/// Rule (BOSS_RULES.md §6.3, §6.3.1; ADR-026 D3, D5–D7)
///  ↓
/// Scenario (Given the documented Boss with its charge pinned at the Charge
///           Requirement, When one atomic Round resolves, Then the documented
///           effect is applied and nothing else is)
///  ↓
/// Test
/// </code>
///
/// <b>The charge is pinned; the Player's Matches no longer fire the Skill.</b>
/// <c>ADR-026</c> D2 / <c>GAME_STATE.md</c> §2.4.3 make <c>SkillCharge</c> a
/// counter fed exclusively by the Boss's <i>own</i> POWER match groups (step
/// 18a): "player matches never charge the Boss". A scenario that wants the
/// Skill to fire therefore pins
/// <c>BossState.SkillCharge = SkillChargeRequirement</c> with
/// <c>SkillCooldown = 0</c> — the eligibility pair §2.4.3 states, and a state
/// the game genuinely reaches by accumulation — instead of lowering the
/// definition's Charge Requirement or relying on the Player's Match count.
///
/// <b>No MVP Skill deals direct damage.</b> All five declare
/// <c>SkillBaseDamage == 0</c> (<c>ADR-026</c> D3), and Burn DoT is retired
/// (D3/D5): a round's only Boss damage instance is the ATK-gated board attack at
/// step 18d, which exists iff the Boss's own cascade cleared an ATK gem
/// (<c>ADR-026</c> D1, <c>GAME_RULES.md</c> §17 step 18d). Every test below
/// asserts that relationship against the round's own committed outputs.
///
/// <b>The Player's action is pinned to a non-ATK Match.</b> Every scenario pins a
/// Player Swap whose resolved cascade clears Gems of exactly one non-ATK type
/// (<see cref="RoundTestSupport.PlayerMatch"/>; <c>DEF</c> unless a scenario needs
/// the acting actor's other non-offensive effect), so the Player deals no damage:
/// the Skill's effect is the round's only combat modifier and cannot be confused
/// with the Player's own attack.
///
/// <b>Every expected value traces to a document, never to the implementation.</b>
/// Flame Burst's Boss ATK buff (D5), Drain Power's −20 flat drain with its floor
/// at 0 (§6.3.1 item 2), Root's −30% for 2 Turns (§6.3.1 item 3), Earthquake's
/// Boss Shield pool refreshed not accumulated (D6, <c>COMBAT_RULES.md</c> §4
/// item 3), and Thunder Strike's next-qualifying-attack modifier (D7,
/// §6.3.1 item 5) are read from those documents and asserted against the
/// resulting authoritative state.
/// </summary>
public class BossSkillSecondaryEffectTests
{
    /// <summary>
    /// The Pet these battles carry (<c>GAME_STATE.md</c> §2.3), Xích Lang's MVP
    /// Element and Passive.
    /// </summary>
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>The owning Player of these battles (<c>GAME_STATE.md</c> §2.8).</summary>
    private static readonly PlayerId Owner = new("player_boss_secondary_owner");

    /// <summary>
    /// The active Pet's HP these scenarios pin.
    ///
    /// <c>GAME_RULES.md</c> §1.4 ends the battle when either side reaches 0 HP, and
    /// the Boss's ATK-gated instance may land on the Pet in the rounds a multi-round
    /// scenario runs. The scenario pins a Pet that survives its observation window so
    /// the assertions are about the Skill's effect rather than about who wins the
    /// battle; the terminal check itself is not this suite's subject and is covered
    /// where it belongs (<c>AtomicRoundOrchestrationTests</c>).
    /// </summary>
    private const int ObservationWindowHp = 100_000;

    /// <summary>
    /// The Power Drain's starting Power in the per-Boss binding scenario
    /// (<c>GAME_RULES.md</c> §12's 0–100 range).
    /// </summary>
    private const int BindingScenarioPower = 30;

    // =======================================================================
    // 0. Eligibility — ADR-026 D2, GAME_STATE.md §2.4.3, GAME_RULES.md §17 18c
    // =======================================================================

    [Fact]
    public async Task PlayerPowerMatch_ShouldNotChargeTheBossAndShouldNotFireItsSkill()
    {
        // ADR-026 D2 / GAME_STATE.md §2.4.3 / BOSS_RULES.md §6.3's own footnote:
        // SkillCharge "increments exclusively from POWER gems matched by the Boss
        // during the Boss's turn (player matches never charge the Boss)". The
        // Player's POWER Gems are the resource the PET gains (GAME_RULES.md §17
        // step 13) — this scenario is the one the retired reading of this file
        // relied on, and it must no longer reach step 18c's first condition.
        var harness = Harness.Create();

        // An unreachable Charge Requirement keeps the Skill ineligible whatever the
        // Boss's own POWER matches do, so this asserts the counter itself rather
        // than the balance value of a threshold.
        var definition = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 100_000 },
        };

        const string battleId = "boss-secondary-player-power";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, definition);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Power, created.RngState, created.LastCommittedSwapPair);
        Assert.True(pinned.Resources.ClearedGemCounts.Power > 0);

        await RoundTestSupport.PinAsync(
            harness.Store,
            created,
            pinned.Board,
            bossState: created.BossState with { SkillCharge = 0, SkillCooldown = 0 });

        var writesBeforeTheRound = harness.Store.WriteCount;

        var result = (await harness.Service.ExecuteSwapAsync(battleId, pinned.Swap))
            ?? throw new InvalidOperationException("the Swap must resolve");

        Assert.True(result.IsAccepted);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        // The counter holds exactly what the Boss's own POWER match groups contributed,
        // clamped at the Requirement (ADR-026 D2's accumulation rule).
        Assert.Equal(
            Math.Min(RoundTestSupport.BossPowerMatchGroups(result), definition.SkillChargeRequirement),
            result.State.BossState.SkillCharge);

        // The Player's POWER Gems did reach the PET's Power — that is the comparison
        // that makes "the Player's POWER match charged nobody else" meaningful.
        Assert.True(result.State.PetState.Power > created.PetState.Power);

        AssertOneWriteBack(harness, writesBeforeTheRound);
    }

    // =======================================================================
    // 1. Flame Burst — temporary Boss ATK buff (ADR-026 D5, BOSS_RULES.md §6.3.1 item 1)
    // =======================================================================

    [Fact]
    public async Task FlameBurst_ShouldApplyOneBossSourcedAtkBuffAndDealNoDirectDamage()
    {
        // BOSS_RULES.md §6.3.1 item 1 / ADR-026 D5 (Option 5B): "Applies a temporary
        // Boss ATK buff to BossState.ActiveStatusEffects. The buff modifies Boss ATK
        // but deals no damage independently ... Burn DoT is removed." The declaration
        // is Hỏa Long's (Id "flame-burst", BuffDebuff, TargetStat "ATK", +50%, 2 Turns).
        var fired = await FireTheSkillOnAFreshBattleAsync("boss-flame-burst-applied", BossDefinitions.HoaLong);
        var result = fired.Result;

        var buff = Assert.Single(result.State.BossState.ActiveStatusEffects, e => e.Id == "flame-burst");

        Assert.Equal(StatusEffectType.BuffDebuff, buff.Type);
        Assert.Equal(StatusEffectSource.Boss, buff.Source);
        Assert.Equal("ATK", buff.TargetStat);
        Assert.Equal(50, buff.Magnitude);
        Assert.Null(buff.ExpiryCondition);

        // Applied at step 18c and consumed by step 19a in the SAME round: of the two
        // Turns §6.3.1's duration value declares, the second is what remains
        // (COMBAT_RULES.md §5.3 DR2, GAME_STATE.md §5.1.1 items 2–3).
        Assert.Equal(1, buff.RemainingTurns);

        // D3/D5 retire the Burn DoT outright: no DoT instance exists on either entity,
        // and no "Burn" identity is present at all.
        Assert.DoesNotContain(result.State.PetState.ActiveStatusEffects, e => e.Type == StatusEffectType.DoT);
        Assert.DoesNotContain(result.State.BossState.ActiveStatusEffects, e => e.Type == StatusEffectType.DoT);
        Assert.DoesNotContain(result.State.BossState.ActiveStatusEffects, e => e.Id == "Burn");
        Assert.DoesNotContain(result.State.PetState.ActiveStatusEffects, e => e.Id == "Burn");

        // ADR-026 D3 / COMBAT_RULES.md §3.4: the Skill's own instance carries no damage.
        Assert.Equal(0, BossDefinitions.HoaLong.SkillBaseDamage);
        AssertNoDirectSkillDamage(result);
        AssertNoEffectSpecificEvent(result);

        // One atomic Round, one Sequence-gated write-back (ADR-026 D4,
        // GAME_STATE.md §5.1.1 item 9), and one Turn begun (MATCH3_RULES.md §8.1).
        Assert.Equal(fired.Before.Sequence + 1, result.State.Sequence);
        AssertOneWriteBack(fired);
    }

    [Fact]
    public async Task FlameBurst_ShouldRefreshTheSingleInstanceInsteadOfStacking()
    {
        // COMBAT_RULES.md §5.2 item 2 / §5.5.5 and GAME_STATE.md §2.3.1 item 6:
        // re-applying an already-active effect is a REFRESH — "set, do not stack" —
        // and the collection holds at most one element per Id. §5.3 DR3/DR4 make
        // Apply and Refresh the same "set remaining = duration" operation.
        var harness = Harness.Create();

        const string battleId = "boss-flame-burst-refresh";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var first = await FireTheSkillAsync(harness, battleId, BossDefinitions.HoaLong, Surviving(created));

        var afterFirst = first.Result.State;

        // Round 1 leaves exactly one instance with ONE Turn left (2 applied, one
        // consumed). Round 2 therefore starts with the effect already active, which is
        // the state the refresh rule is about.
        Assert.Equal(1, Assert.Single(afterFirst.BossState.ActiveStatusEffects, e => e.Id == "flame-burst").RemainingTurns);

        var second = await FireTheSkillAsync(harness, battleId, BossDefinitions.HoaLong, afterFirst);
        var afterSecond = second.Result.State;

        var refreshed = Assert.Single(afterSecond.BossState.ActiveStatusEffects, e => e.Id == "flame-burst");

        // One element with the declaration's own magnitude. The value 50 discriminates
        // the forbidden behaviors rather than merely being "in range": a magnitude-
        // summing implementation reads 100 and an incrementing-duration one reads 2,
        // while an implementation that did NOT refresh would have let the round-1
        // instance reach 0 and disappear at step 19a (GAME_STATE.md §5.1.1 items 4–5)
        // — so "present, at 1" is the refreshed value and nothing else.
        Assert.Equal(50, refreshed.Magnitude);
        Assert.Equal(1, refreshed.RemainingTurns);
        Assert.Equal("ATK", refreshed.TargetStat);

        AssertOneWriteBack(harness, second.WritesBeforeTheRound);
    }

    [Fact]
    public async Task FlameBurst_ShouldEmitNoEffectSpecificEvent()
    {
        // GAME_RULES.md §16's canonical event list is closed and BOSS_RULES.md §7
        // enumerates step 18's events; GAME_STATE.md §5.1.1 item 10 states the
        // lifecycle "adds no event, no payload member, and no SignalR method". The
        // effect is authoritative state mutation, not a transport event.
        var fired = await FireTheSkillOnAFreshBattleAsync("boss-flame-burst-no-event", BossDefinitions.HoaLong);

        AssertNoEffectSpecificEvent(fired.Result);
        Assert.Single(fired.Result.Events, e => e.Type == BattleEventType.BossSkillCast);

        // Hỏa Long's Skill has no Power-draining effect, so no Boss-owned PowerChanged
        // is produced (GAME_EVENTS.md §2 item 4 at step 18b).
        Assert.DoesNotContain(
            fired.Result.Events,
            e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Source == PowerChangeSource.Boss);
    }

    // =======================================================================
    // 2. Drain Power — instant flat Power reduction (BOSS_RULES.md §6.3.1 item 2)
    // =======================================================================

    [Theory]
    [InlineData(30)]
    [InlineData(21)]
    [InlineData(20)]
    [InlineData(10)]
    [InlineData(0)]
    public async Task DrainPower_ShouldApplyTheDocumentedFlattenedReduction(int powerBefore)
    {
        // BOSS_RULES.md §6.3.1 item 2: "Instantly subtracts 20 flat Power from the
        // active Pet (PetState.Power = max(0, PetState.Power - 20))" — the floor at 0
        // is that item's own formula and GAME_RULES.md §12 fixes the 0–100 range.
        var declaredDrain = (int)BossDefinitions.ThuyMa.SkillDefinition.SecondaryEffect!.Value.Magnitude;

        Assert.Equal(20, declaredDrain);

        var expectedPower = Math.Max(0, powerBefore - declaredDrain);

        var fired = await FireTheSkillOnAFreshBattleAsync(
            $"boss-drain-{powerBefore}",
            BossDefinitions.ThuyMa,
            petPower: powerBefore);

        Assert.Equal(expectedPower, fired.Result.State.PetState.Power);

        // GAME_EVENTS.md §2 item 1 / item 4: the event reports "the signed change" the
        // mutation ACTUALLY applied together with the resulting Power, and "a mutation
        // that does not occur emits nothing" — so a drain the floor fully absorbs (a
        // Pet already at 0) reports no event at all. When the floor only partly binds,
        // the reported Delta is the reduction that happened, never the requested 20.
        var changes = fired.Result.Events
            .Where(e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Source == PowerChangeSource.Boss)
            .Select(e => e.PowerChanged)
            .ToArray();

        if (expectedPower == powerBefore)
        {
            Assert.Empty(changes);
        }
        else
        {
            var change = Assert.Single(changes);

            Assert.Equal(expectedPower - powerBefore, change.Delta);
            Assert.Equal(expectedPower, change.Power);
        }

        AssertNoDirectSkillDamage(fired.Result);
        Assert.Equal(0, BossDefinitions.ThuyMa.SkillBaseDamage);
        AssertOneWriteBack(fired);
    }

    [Fact]
    public async Task DrainPower_ShouldCreateNoStatusEffectInstance()
    {
        // GAME_STATE.md §2.3.1 item 9: "Instant, non-duration effects create no
        // instance. Drain Power is an immediate PetState.Power mutation, not a Status
        // Effect ... so it never produces an element here." §6.3.1 item 2's own
        // duration is "None (instant stat reduction, not a persistent status effect)".
        var fired = await FireTheSkillOnAFreshBattleAsync(
            "boss-drain-no-instance",
            BossDefinitions.ThuyMa,
            petPower: 30);

        var pet = fired.Result.State.PetState;

        // The declaration carries no instance values at all, and produces none.
        var declaration = BossDefinitions.ThuyMa.SkillDefinition.SecondaryEffect!.Value;

        Assert.Equal(BossSkillSecondaryEffectKind.PowerDrain, declaration.Kind);
        Assert.Null(declaration.StatusEffectId);
        Assert.Null(declaration.StatusEffectType);
        Assert.Null(declaration.DurationTurns);
        Assert.Null(declaration.ToStatusEffect());

        // Nothing the Boss's Skill owns is present on the Pet. (The Pet's own DEF Match
        // may legitimately have granted the PET a Shield at step 18a — that instance is
        // Player-sourced and is not the Skill's.)
        Assert.DoesNotContain(pet.ActiveStatusEffects, e => e.Source == StatusEffectSource.Boss);
        Assert.DoesNotContain(pet.ActiveStatusEffects, e => e.Id == "Power");
        Assert.DoesNotContain(pet.ActiveStatusEffects, e => e.Id == "DrainPower");

        Assert.Equal(10, pet.Power);
    }

    // =======================================================================
    // 3. Root — Pet ATK debuff (BOSS_RULES.md §6.3.1 item 3)
    // =======================================================================

    [Fact]
    public async Task Root_ShouldApplyOneBossSourcedPetAtkDebuffAndDealNoDirectDamage()
    {
        // BOSS_RULES.md §6.3.1 item 3: "-30% Pet ATK debuff for 2 Turns", applied as a
        // Turn-based Buff/Debuff (COMBAT_RULES.md §5.1) whose TargetStat is "ATK"
        // (GAME_STATE.md §2.3.1 item 7's BuffDebuff stat modifier). COMBAT_RULES.md
        // §5.4.4 keeps the stored stat out of it.
        var fired = await FireTheSkillOnAFreshBattleAsync("boss-root-applied", BossDefinitions.MocYeu);
        var pet = fired.Result.State.PetState;

        var root = Assert.Single(pet.ActiveStatusEffects, e => e.Id == "Root");

        Assert.Equal(StatusEffectType.BuffDebuff, root.Type);
        Assert.Equal(StatusEffectSource.Boss, root.Source);
        Assert.Equal("ATK", root.TargetStat);
        Assert.Equal(30, root.Magnitude);
        Assert.Null(root.ExpiryCondition);

        // 2 Turns applied at step 18c, one consumed at that same round's step 19a
        // (COMBAT_RULES.md §5.3.2/DR2, GAME_STATE.md §5.1.1 item 3).
        Assert.Equal(1, root.RemainingTurns);

        // COMBAT_RULES.md §5.4.4 / §5.5.4: the base stat is never overwritten — the
        // instance is the only representation of the debuff.
        Assert.Equal(Pet.ToPetState().ATK, pet.ATK);

        Assert.Equal(0, BossDefinitions.MocYeu.SkillBaseDamage);
        AssertNoDirectSkillDamage(fired.Result);
        AssertNoEffectSpecificEvent(fired.Result);
        AssertOneWriteBack(fired);
    }

    [Fact]
    public async Task Root_ShouldRefreshTheSingleInstanceInsteadOfStacking()
    {
        // COMBAT_RULES.md §5.3 DR3/DR4 and GAME_STATE.md §2.3.1 item 6: re-applying an
        // active effect re-sets its duration on the ONE existing element and appends no
        // second one. §5.3.3's worked example ("duration = 2, refreshed in Turn N+1")
        // is the documented shape.
        var harness = Harness.Create();

        const string battleId = "boss-root-refresh";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.MocYeu);

        var first = await FireTheSkillAsync(harness, battleId, BossDefinitions.MocYeu, Surviving(created));
        var afterFirst = first.Result.State;

        Assert.Equal(1, Assert.Single(afterFirst.PetState.ActiveStatusEffects, e => e.Id == "Root").RemainingTurns);

        var second = await FireTheSkillAsync(harness, battleId, BossDefinitions.MocYeu, afterFirst);
        var refreshed = Assert.Single(second.Result.State.PetState.ActiveStatusEffects, e => e.Id == "Root");

        // Refreshed, not duplicated (one element) and not summed (30, never 60); and
        // genuinely refreshed rather than merely carried, because a round that applied
        // nothing would have taken the round-1 instance from 1 to 0 and removed it at
        // step 19a (GAME_STATE.md §5.1.1 items 4–5).
        Assert.Equal(30, refreshed.Magnitude);
        Assert.Equal(1, refreshed.RemainingTurns);
        Assert.Equal("ATK", refreshed.TargetStat);

        AssertOneWriteBack(harness, second.WritesBeforeTheRound);
    }

    [Fact]
    public async Task Root_ShouldEmitNoEffectSpecificEvent()
    {
        // GAME_RULES.md §16's list is closed, and a Pet-held instance reaches the client
        // through the existing state projection rather than a new event
        // (GAME_STATE.md §2.3.1's membership/delivery note, §5.1.1 item 10).
        var fired = await FireTheSkillOnAFreshBattleAsync("boss-root-no-event", BossDefinitions.MocYeu);

        AssertNoEffectSpecificEvent(fired.Result);
        Assert.Single(fired.Result.Events, e => e.Type == BattleEventType.BossSkillCast);
    }

    // =======================================================================
    // 4. Earthquake — temporary Boss Shield (ADR-026 D6, BOSS_RULES.md §6.3.1 item 4)
    // =======================================================================

    [Fact]
    public async Task Earthquake_ShouldGrantTheBossOneShieldAndDealNoDirectDamage()
    {
        // BOSS_RULES.md §6.3.1 item 4 / ADR-026 D6 (Option 2A): "Grants a temporary
        // Shield to the Boss on BossState.ActiveStatusEffects. Absorbs incoming player
        // attack damage before Boss HP via DamagePipeline (COMBAT_RULES.md §4)". It is
        // the same trigger-based pool §4 defines, so it carries the Shield Type,
        // the "Shield" identity, and the "ShieldDepleted" expiry condition — and no
        // Turn countdown (§4's preamble, GAME_STATE.md §2.3.1 item 3).
        var fired = await FireTheSkillOnAFreshBattleAsync("boss-earthquake-applied", BossDefinitions.SonThachVe);

        var shield = Assert.Single(fired.Result.State.BossState.ActiveStatusEffects, e => e.Type == StatusEffectType.Shield);

        Assert.Equal("Shield", shield.Id);
        Assert.Equal(StatusEffectSource.Boss, shield.Source);
        Assert.Equal(150, shield.Magnitude);
        Assert.Equal(StatusEffect.ShieldDepletedCondition, shield.ExpiryCondition);
        Assert.Null(shield.RemainingTurns);

        // The applied magnitude is the declaration's own: ADR-026's balance choices
        // leave the specific Shield points to the definition, and the applied pool must
        // be that value rather than a second, resolution-authored one.
        Assert.Equal(150, BossDefinitions.SonThachVe.SkillDefinition.SecondaryEffect!.Value.Magnitude);

        // The Boss's own DEF matches also grant the Boss a Shield at step 18a, and
        // COMBAT_RULES.md §4 item 3 makes them refresh ONE pool — which is why a
        // single "Shield" instance is all the round may hold.
        Assert.Single(fired.Result.State.BossState.ActiveStatusEffects, e => e.Id == "Shield");

        Assert.Equal(0, BossDefinitions.SonThachVe.SkillBaseDamage);
        AssertNoDirectSkillDamage(fired.Result);
        AssertNoEffectSpecificEvent(fired.Result);
        Assert.Equal(fired.Before.Sequence + 1, fired.Result.State.Sequence);
        AssertOneWriteBack(fired);
    }

    [Fact]
    public async Task Earthquake_ShouldSetTheRefreshedPoolInsteadOfAccumulating()
    {
        // COMBAT_RULES.md §4 item 3: "Applying a Shield to an entity that already has
        // an active Shield refreshes that existing Shield — the refreshed pool is SET
        // TO THE MAGNITUDE OF THE NEW APPLICATION. It is NOT summed with the existing
        // value ... no additive accumulation occurs under any MVP condition," and at
        // most one Shield is active per entity.
        var harness = Harness.Create();

        const string battleId = "boss-earthquake-refresh";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.SonThachVe);

        var first = await FireTheSkillAsync(harness, battleId, BossDefinitions.SonThachVe, Surviving(created));
        var afterFirst = first.Result.State;

        Assert.Equal(150, Assert.Single(afterFirst.BossState.ActiveStatusEffects, e => e.Id == "Shield").Magnitude);

        // A pool part-way consumed — §4 items 2 and 5's documented post-absorption state
        // — so "set" is distinguishable from "keep the old value" as well as from "sum".
        var partialPool = StatusEffect.TriggerBased(
            "Shield",
            StatusEffectType.Shield,
            StatusEffectSource.Boss,
            20,
            StatusEffect.ShieldDepletedCondition);

        var second = await FireTheSkillAsync(
            harness,
            battleId,
            BossDefinitions.SonThachVe,
            afterFirst,
            bossEffects: [partialPool]);

        var refreshed = Assert.Single(second.Result.State.BossState.ActiveStatusEffects, e => e.Id == "Shield");

        // 20 kept would read 20 and 20 + 150 summed would read 170; the refreshed pool is
        // the new application's 150 alone.
        Assert.Equal(150, refreshed.Magnitude);
        Assert.Equal(StatusEffect.ShieldDepletedCondition, refreshed.ExpiryCondition);
        Assert.Null(refreshed.RemainingTurns);

        // Exactly ONE pool exists, so "set" is distinguishable from a second instance as
        // well as from a kept or summed value.
        Assert.Single(
            second.Result.State.BossState.ActiveStatusEffects,
            e => e.Type == StatusEffectType.Shield);

        AssertOneWriteBack(harness, second.WritesBeforeTheRound);
    }

    [Fact]
    public async Task Earthquake_ShouldEmitNoShieldSpecificEvent()
    {
        // COMBAT_RULES.md §4 item 4: 'ShieldDepleted is NOT a Battle Event: no event of
        // that name exists in GAME_RULES.md §16's canonical list or in GAME_EVENTS.md
        // §2, and this section authors none.' Neither is the Shield's application.
        var fired = await FireTheSkillOnAFreshBattleAsync("boss-earthquake-no-event", BossDefinitions.SonThachVe);

        AssertNoEffectSpecificEvent(fired.Result);
        Assert.Single(fired.Result.Events, e => e.Type == BattleEventType.BossSkillCast);
    }

    // =======================================================================
    // 5. Thunder Strike — next qualifying Boss ATK buff (ADR-026 D7, §6.3.1 item 5)
    // =======================================================================

    [Fact]
    public async Task ThunderStrike_ShouldInstallTheNextQualifyingAttackModifierOrBeConsumedByTheSameRoundsAttack()
    {
        // BOSS_RULES.md §6.3.1 item 5 / ADR-026 D7 (Option 3A): "Applies a trigger-based
        // modifier on BossState.ActiveStatusEffects (ExpiryCondition =
        // 'NextQualifyingAttack') buffing the Boss's next valid ATK match. Never deals
        // damage independently." The modifier is therefore absent from the committed
        // state exactly when the same round's step 18d instance was the qualifying
        // attack that consumed it (COMBAT_RULES.md §3.3 item 8's
        // NextAttack consumption, §5.5.5's no-Turn-countdown note).
        var fired = await FireTheSkillOnAFreshBattleAsync("boss-thunder-strike-applied", BossDefinitions.KimLoiVuong);
        var result = fired.Result;

        var bossQualified = RoundTestSupport.BossClearedAtkGem(result);

        var modifiers = result.State.BossState.ActiveStatusEffects
            .Where(e => e.Id == "thunder-strike")
            .ToArray();

        if (bossQualified)
        {
            Assert.Empty(modifiers);

            // The consuming attack is the round's ATK-gated Boss instance, which is the
            // only Boss damage instance a round may hold (ADR-026 D1/D3).
            Assert.Single(
                result.Events,
                e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);
        }
        else
        {
            var buff = Assert.Single(modifiers);

            Assert.Equal(StatusEffectType.BuffDebuff, buff.Type);
            Assert.Equal(StatusEffectSource.Boss, buff.Source);
            Assert.Equal("ATK", buff.TargetStat);
            Assert.Equal(50, buff.Magnitude);
            Assert.Equal(StatusEffect.NextQualifyingAttackCondition, buff.ExpiryCondition);

            // Trigger-based, so it is not on the step 19a countdown at all
            // (GAME_STATE.md §5.1.1 item 7).
            Assert.Null(buff.RemainingTurns);
        }

        Assert.Equal(0, BossDefinitions.KimLoiVuong.SkillBaseDamage);
        AssertNoDirectSkillDamage(result);
        AssertNoEffectSpecificEvent(result);
        AssertOneWriteBack(fired);
    }

    [Fact]
    public async Task ThunderStrike_ShouldNeverDuplicateTheSingleModifierAcrossRounds()
    {
        // GAME_STATE.md §2.3.1 item 6: "There is never more than one instance per effect
        // identity per entity" — GAME_STATE.md §5.1.1 item 1's "set, not an increment"
        // applied to the trigger-based model, whose refresh operation is
        // StatusEffectLifecycle.ApplyTriggerBased (COMBAT_RULES.md §5.2 item 2, §5.5.5).
        //
        // Every round below STARTS with the modifier already active (the declaration's
        // own instance, pinned) and re-applies it at step 18c, so a round that carries it
        // to its commit is a REFRESH observed end-to-end: one element, the fresh
        // application's magnitude, unchanged trigger-based expiry. A round that ends
        // without it is the documented consumption by the same round's qualifying §18d
        // attack (COMBAT_RULES.md §3.3 item 8).
        //
        // The Player's action is the Pet's HP Match here rather than the suite's usual
        // DEF Match: ADR-026 §4 makes an HP match the acting actor's Heal, so the Player
        // still clears no ATK Gem and deals no damage, and this fixture is the one in
        // which the Boss's own deterministic move leaves a round with no ATK match. Both
        // branches below are therefore exercised, and neither assertion is vacuous.
        var harness = Harness.Create();

        const string battleId = "boss-thunder-strike-refresh";
        var definition = BossDefinitions.KimLoiVuong;
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, definition);

        var state = Surviving(created);
        var observedRefresh = false;
        var observedConsumption = false;

        var declared = StatusEffect.TriggerBased(
            "thunder-strike",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            50,
            StatusEffect.NextQualifyingAttackCondition,
            "ATK");

        for (var round = 0; round < 6; round++)
        {
            var fired = await FireTheSkillAsync(
                harness,
                battleId,
                definition,
                state,
                matchType: GemType.Hp,
                bossEffects: StatusEffectLifecycle.ApplyTriggerBased(state.BossState.ActiveStatusEffects, declared));

            var after = fired.Result.State;

            var modifiers = after.BossState.ActiveStatusEffects
                .Where(e => e.Id == "thunder-strike")
                .ToArray();

            Assert.True(
                modifiers.Length <= 1,
                "GAME_STATE.md §2.3.1 item 6: the array holds at most one element per Id");

            if (modifiers.Length == 1)
            {
                // Re-applied in place: the magnitude is the fresh application's, never a
                // sum of the two, and the trigger-based expiry is unchanged.
                Assert.Equal(50, modifiers[0].Magnitude);
                Assert.Equal(StatusEffect.NextQualifyingAttackCondition, modifiers[0].ExpiryCondition);
                Assert.Null(modifiers[0].RemainingTurns);
                observedRefresh = true;
            }
            else
            {
                // The only documented way a modifier that was active at the round's
                // start can be gone at its end is the qualifying attack that consumed it.
                Assert.True(
                    RoundTestSupport.BossClearedAtkGem(fired.Result),
                    "a next-qualifying modifier is consumed only by a qualifying Boss ATK match");

                observedConsumption = true;
            }

            state = after;

            if (state.PetState.HP <= 0 || state.BossState.HP <= 0)
            {
                break;
            }
        }

        Assert.True(observedRefresh, "at least one round must have carried the re-applied modifier to its commit");
        Assert.True(observedConsumption, "at least one round must have consumed the modifier with its own qualifying attack");
    }

    [Fact]
    public async Task ThunderStrike_ShouldEmitNoEffectSpecificEvent()
    {
        // ADR-026 D7 adds no Battle Event (GAME_RULES.md §16); the modifier reaches the
        // client as the state push's own value.
        var fired = await FireTheSkillOnAFreshBattleAsync("boss-thunder-strike-no-event", BossDefinitions.KimLoiVuong);

        AssertNoEffectSpecificEvent(fired.Result);
        Assert.Single(fired.Result.Events, e => e.Type == BattleEventType.BossSkillCast);
        Assert.DoesNotContain(
            fired.Result.Events,
            e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Source == PowerChangeSource.Boss);
    }

    // =======================================================================
    // 6. Per-Boss binding — BOSS_RULES.md §6.3's Secondary Effect column
    // =======================================================================

    [Fact]
    public void EachSkill_ShouldDeclareItsOwnDocumentedEffect()
    {
        // §6.3's table and §6.3.1's five items, transcribed per Boss, plus ADR-026's D3
        // ("BaseDamage = 0 on all five MVP skills"). The declaration lives on the Boss's
        // own definition, so no resolution site dispatches on the SkillId string.
        var flameBurst = BossDefinitions.HoaLong.SkillDefinition.SecondaryEffect!.Value;

        Assert.Equal(BossSkillSecondaryEffectKind.AtkBuff, flameBurst.Kind);
        Assert.Equal("flame-burst", flameBurst.StatusEffectId);
        Assert.Equal(StatusEffectType.BuffDebuff, flameBurst.StatusEffectType);
        Assert.Equal("ATK", flameBurst.TargetStat);
        Assert.Equal(50, flameBurst.Magnitude);
        Assert.Equal(2, flameBurst.DurationTurns);
        Assert.Null(flameBurst.ExpiryCondition);

        var drainPower = BossDefinitions.ThuyMa.SkillDefinition.SecondaryEffect!.Value;

        Assert.Equal(BossSkillSecondaryEffectKind.PowerDrain, drainPower.Kind);
        Assert.Equal(20, drainPower.Magnitude);
        Assert.Null(drainPower.StatusEffectId);
        Assert.Null(drainPower.StatusEffectType);
        Assert.Null(drainPower.TargetStat);
        Assert.Null(drainPower.DurationTurns);

        var root = BossDefinitions.MocYeu.SkillDefinition.SecondaryEffect!.Value;

        Assert.Equal(BossSkillSecondaryEffectKind.AtkDebuff, root.Kind);
        Assert.Equal("Root", root.StatusEffectId);
        Assert.Equal(StatusEffectType.BuffDebuff, root.StatusEffectType);
        Assert.Equal("ATK", root.TargetStat);
        Assert.Equal(30, root.Magnitude);
        Assert.Equal(2, root.DurationTurns);

        // D6: the Earthquake's Shield is trigger-based, so it declares an expiry
        // condition and no duration.
        var earthquake = BossDefinitions.SonThachVe.SkillDefinition.SecondaryEffect!.Value;

        Assert.Equal(BossSkillSecondaryEffectKind.BossShield, earthquake.Kind);
        Assert.Equal("Shield", earthquake.StatusEffectId);
        Assert.Equal(StatusEffectType.Shield, earthquake.StatusEffectType);
        Assert.Equal(150, earthquake.Magnitude);
        Assert.Null(earthquake.DurationTurns);
        Assert.Equal(StatusEffect.ShieldDepletedCondition, earthquake.ExpiryCondition);

        // D7: Thunder Strike's modifier is trigger-based on the next qualifying attack.
        var thunderStrike = BossDefinitions.KimLoiVuong.SkillDefinition.SecondaryEffect!.Value;

        Assert.Equal(BossSkillSecondaryEffectKind.NextAtkBuff, thunderStrike.Kind);
        Assert.Equal("thunder-strike", thunderStrike.StatusEffectId);
        Assert.Equal(StatusEffectType.BuffDebuff, thunderStrike.StatusEffectType);
        Assert.Equal("ATK", thunderStrike.TargetStat);
        Assert.Equal(50, thunderStrike.Magnitude);
        Assert.Null(thunderStrike.DurationTurns);
        Assert.Equal(StatusEffect.NextQualifyingAttackCondition, thunderStrike.ExpiryCondition);

        // ADR-026 D3 / COMBAT_RULES.md §3.4: no MVP Skill declares direct damage.
        foreach (var definition in BossDefinitions.All)
        {
            Assert.Equal(0, definition.SkillBaseDamage);
        }
    }

    [Fact]
    public async Task EachSkillsEffect_ShouldLandOnItsOwnEntityOnly()
    {
        // §6.3's Secondary Effect column gives each Skill exactly ONE effect, and §6.3.1
        // fixes which entity holds it: flame-burst and thunder-strike on the Boss's own
        // ActiveStatusEffects (D5, D7), earthquake's Shield on the Boss's (§6.3.1
        // item 4, D6), Root on the PET's (§6.3.1 item 3), and Drain Power nowhere at all
        // (instant, §2.3.1 item 9). A skill therefore cannot apply another skill's
        // effect.
        var scenarios = new (BossDefinition Definition, string? PetEffectId, string? BossEffectId, bool DrainsPower)[]
        {
            (BossDefinitions.HoaLong, null, "flame-burst", false),
            (BossDefinitions.ThuyMa, null, null, true),
            (BossDefinitions.MocYeu, "Root", null, false),
            (BossDefinitions.SonThachVe, null, "Shield", false),
            (BossDefinitions.KimLoiVuong, null, "thunder-strike", false),
        };

        var expectedPower = Math.Max(0, BindingScenarioPower - 20);

        foreach (var (definition, petEffectId, bossEffectId, drainsPower) in scenarios)
        {
            var fired = await FireTheSkillOnAFreshBattleAsync(
                $"boss-binding-{definition.SkillId}",
                definition,
                petPower: BindingScenarioPower);

            var pet = fired.Result.State.PetState;
            var boss = fired.Result.State.BossState;

            if (petEffectId is { } onPet)
            {
                Assert.Single(pet.ActiveStatusEffects, e => e.Id == onPet);
            }

            if (bossEffectId is { } onBoss)
            {
                var present = boss.ActiveStatusEffects.Any(e => e.Id == onBoss);

                // D7's modifier is the one skill effect the same round's step 18d
                // instance may legitimately consume.
                if (onBoss == "thunder-strike" && !present)
                {
                    Assert.True(RoundTestSupport.BossClearedAtkGem(fired.Result));
                }
                else
                {
                    Assert.Single(boss.ActiveStatusEffects, e => e.Id == onBoss);
                }
            }

            // The other four Skills' identities never appear on this round. ("Shield" is
            // excluded from the cross-check because a Boss DEF match legitimately grants
            // one at step 18a — §6.3.1 item 4's Shield is identified by the pool the
            // declaration's magnitude gives it, asserted in section 4 above.)
            foreach (var other in new[] { "flame-burst", "Root", "thunder-strike" })
            {
                if (other == petEffectId || other == bossEffectId)
                {
                    continue;
                }

                Assert.DoesNotContain(pet.ActiveStatusEffects, e => e.Id == other);
                Assert.DoesNotContain(boss.ActiveStatusEffects, e => e.Id == other);
            }

            // Only Drain Power moves the Pet's Power; the Player's DEF Match powers
            // nothing, so every other Skill leaves the pinned value exactly as it was.
            Assert.Equal(drainsPower ? expectedPower : BindingScenarioPower, pet.Power);
        }
    }

    [Fact]
    public async Task FiredBossSkill_ShouldNeverProduceAnAttackInstanceOfItsOwn()
    {
        // ADR-026 D3 / COMBAT_RULES.md §3.4: every MVP Skill declares BaseDamage = 0, so
        // the only Boss damage instance a round may hold is the ATK-gated board instance
        // of step 18d. Asserted for the whole MVP set at once, which is what makes this
        // a statement about the Skill contract rather than about one Boss.
        foreach (var definition in BossDefinitions.All)
        {
            var fired = await FireTheSkillOnAFreshBattleAsync($"boss-skill-no-damage-{definition.SkillId}", definition);

            Assert.Contains(fired.Result.Events, e => e.Type == BattleEventType.BossSkillCast);
            Assert.Equal(0, definition.SkillBaseDamage);
            AssertNoDirectSkillDamage(fired.Result);
        }
    }

    // =======================================================================
    // 7. The no-Skill boundary — §17 steps 18a–18d, ADR-026 D1 and §5
    // =======================================================================

    [Fact]
    public async Task BossSkillIneligible_ShouldApplyNoSecondaryEffectAndEmitNoCast()
    {
        // GAME_STATE.md §2.4.3 / BOSS_RULES.md §6.3: the Skill fires only when BOTH
        // conditions hold. Here SkillCooldown is 0 and the charge never reaches the
        // Requirement, so step 18c's "execute the non-damaging Skill" is not reached and
        // NONE of §6.3.1's effects may be applied.
        //
        // The Boss's board phase still runs (18a), and the legacy fallback basic attack
        // it used to prove itself with is RETIRED (ADR-026 §5) — a Boss deals attack
        // damage only through a confirmed ATK-gem match (D1).
        foreach (var definition in BossDefinitions.All)
        {
            var harness = Harness.Create();
            var battleId = $"boss-no-skill-{definition.SkillId}";

            var unreachable = definition with
            {
                SkillDefinition = definition.SkillDefinition with { ChargeRequirement = 100_000 },
            };

            var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, unreachable);
            var before = Surviving(created, BindingScenarioPower);

            var pinned = RoundTestSupport.PlayerMatch(GemType.Def, before.RngState, before.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(
                harness.Store,
                before,
                pinned.Board,
                bossState: before.BossState with { SkillCharge = 0, SkillCooldown = 0 });

            var writesBeforeTheRound = harness.Store.WriteCount;

            var result = (await harness.Service.ExecuteSwapAsync(battleId, pinned.Swap))
                ?? throw new InvalidOperationException("the Swap must resolve");

            Assert.True(result.IsAccepted);
            Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BossSkillCast);

            // Step 18c's effects are all absent: no Boss ATK buff, no next-qualifying
            // modifier, no Pet Root, and no Power drain.
            Assert.DoesNotContain(result.State.BossState.ActiveStatusEffects, e => e.Id == "flame-burst");
            Assert.DoesNotContain(result.State.BossState.ActiveStatusEffects, e => e.Id == "thunder-strike");
            Assert.DoesNotContain(result.State.PetState.ActiveStatusEffects, e => e.Id == "Root");
            Assert.DoesNotContain(result.State.PetState.ActiveStatusEffects, e => e.Source == StatusEffectSource.Boss);
            Assert.Equal(BindingScenarioPower, result.State.PetState.Power);

            // The round genuinely ran its Boss response phase: the Boss acted on the
            // shared board (18a), which is the evidence the retired fixed-damage
            // fallback used to supply.
            Assert.NotEmpty(RoundTestSupport.BossBoardEvents(result));

            // And the round's only Boss damage instance is the ATK-gated one.
            AssertNoDirectSkillDamage(result);
            AssertOneWriteBack(harness, writesBeforeTheRound);
        }
    }

    // =======================================================================
    // 8. Write-back and rejected-action boundary — GAME_STATE.md §5.1, §5.1.1
    // =======================================================================

    [Fact]
    public async Task RejectedSwap_ShouldApplyNoSecondaryEffect()
    {
        // GAME_STATE.md §5.1 item 6 / §5.1.1 item 11: a rejected action is not a
        // resolution, so step 18 never runs, step 19a does not run, and nothing is
        // applied.
        var harness = Harness.Create();

        const string battleId = "boss-effect-rejected";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.ThuyMa);

        var before = Surviving(created, BindingScenarioPower);

        await RoundTestSupport.PinAsync(
            harness.Store,
            before,
            before.BoardState,
            bossState: before.BossState with
            {
                SkillCharge = BossDefinitions.ThuyMa.SkillChargeRequirement,
                SkillCooldown = 0,
            });

        // A non-adjacent pair is rejected by the Swap rules (MATCH3_RULES.md §2.1.2);
        // it can never be a resolution.
        var rejected = await harness.Service.ExecuteSwapAsync(battleId, new SwapRequest(0, BoardState.CellCount - 1));

        Assert.True(rejected is null || !rejected.Value.IsAccepted);

        var after = (await harness.Service.GetBattleAsync(battleId))!;

        Assert.Equal(before.Sequence, after.Sequence);
        Assert.Equal(BindingScenarioPower, after.PetState.Power);
        Assert.Equal(before.PetState.ATK, after.PetState.ATK);
        Assert.DoesNotContain(after.PetState.ActiveStatusEffects, e => e.Source == StatusEffectSource.Boss);
    }

    [Fact]
    public async Task ExhaustedCompareAndSet_ShouldCommitNoSecondaryEffect()
    {
        // REDIS_STATE.md §4 items 2–3 / 7 and GAME_STATE.md §5.1.1 item 9: the effects
        // are intermediate values of ONE resolution and exist only inside its single
        // gated write-back. When that write-back never lands, no effect may be
        // observable — there is no partial commit and no second write.
        var harness = Harness.Create();

        const string battleId = "boss-effect-cas-exhausted";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.ThuyMa);

        var before = Surviving(created, BindingScenarioPower);

        var pinnedRound = await PinSkillRoundAsync(harness, before, BossDefinitions.ThuyMa);
        var writesAfterPinning = harness.Store.WriteCount;

        // Every compare-and-set attempt is refused (more than the service's bounded
        // retry budget), so the resolution can never be committed.
        harness.Store.ForcedConflicts = 64;

        var result = await harness.Service.ExecuteSwapAsync(
            battleId,
            pinnedRound.Swap with { ExpectedSequence = before.Sequence, ActionId = "act-boss-effect-exhausted" });

        Assert.False(result is { IsAccepted: true });

        // The stored record is exactly what the pinning wrote: no Power reduction, no
        // effect instance, and no Turn or Sequence advance.
        var after = (await harness.Service.GetBattleAsync(battleId))!;

        Assert.Equal(before.Sequence, after.Sequence);
        Assert.Equal(before.Turn, after.Turn);
        Assert.Equal(BindingScenarioPower, after.PetState.Power);
        Assert.Equal(before.PetState.ATK, after.PetState.ATK);
        Assert.DoesNotContain(after.PetState.ActiveStatusEffects, e => e.Source == StatusEffectSource.Boss);

        // And the refused attempts performed no write at all (§4 item 7).
        Assert.Equal(writesAfterPinning, harness.Store.WriteCount);
    }

    [Fact]
    public async Task RetriedCompareAndSet_ShouldCommitTheEffectExactlyOnce()
    {
        // REDIS_STATE.md §4 item 5: one action, one write-back. A refused first attempt
        // that the service retries must leave exactly ONE committed resolution — the
        // effect is not applied twice by the retry. The request states the state version
        // it observed, which is what permits the retry rather than a staleness rejection
        // (ADR-025 §1, SIGNALR_PROTOCOL.md §2 item 3).
        var harness = Harness.Create();

        const string battleId = "boss-effect-cas-retry";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.ThuyMa);

        var before = Surviving(created, BindingScenarioPower);

        var pinnedRound = await PinSkillRoundAsync(harness, before, BossDefinitions.ThuyMa);
        var writesAfterPinning = harness.Store.WriteCount;

        harness.Store.ForcedConflicts = 1;

        var result = await harness.Service.ExecuteSwapAsync(
            battleId,
            pinnedRound.Swap with { ExpectedSequence = before.Sequence, ActionId = "act-boss-effect-retry" });

        Assert.True(result!.Value.IsAccepted);
        Assert.Contains(result.Value.Events, e => e.Type == BattleEventType.BossSkillCast);

        var after = (await harness.Service.GetBattleAsync(battleId))!;

        // Exactly one resolution committed, and Drain Power's 20 was applied once: a
        // double-applied drain would have taken 30 to 0 rather than to 10.
        Assert.Equal(before.Sequence + 1, after.Sequence);
        Assert.Equal(Math.Max(0, BindingScenarioPower - 20), after.PetState.Power);
        Assert.Equal(after.PetState.Power, result.Value.State.PetState.Power);

        Assert.Equal(writesAfterPinning + 1, harness.Store.WriteCount);
    }

    [Fact]
    public async Task SuccessfulResolution_ShouldCommitAllEffectsInOneWriteBack()
    {
        // GAME_STATE.md §5.1.1 item 9 / ADR-026 D4: the application (steps 1–18) and the
        // step 19a consumption are intermediate values of ONE resolution, committed by
        // the single write-back under the Sequence compare-and-set. One accepted Swap
        // therefore advances Sequence by exactly one while carrying BOTH entities'
        // effects — the Boss's freshly applied flame-burst AND the Pet's own
        // Turn-countdown instance as step 19a consumes and expires it.
        var harness = Harness.Create();

        const string battleId = "boss-effect-one-write-back";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        // One Turn left on a Pet-held effect: §5.1.1 items 4–5 make it expire in this
        // very round, so the commit must carry the round's own application and the
        // countdown's own removal together.
        var expiring = StatusEffect.TurnBased(
            "Root",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            30,
            1,
            "ATK");

        var survivable = Surviving(created);

        var before = survivable with
        {
            PetState = survivable.PetState with
            {
                ActiveStatusEffects = StatusEffectLifecycle.Apply(
                    survivable.PetState.ActiveStatusEffects,
                    expiring),
            },
        };

        var fired = await FireTheSkillAsync(harness, battleId, BossDefinitions.HoaLong, before);

        var after = (await harness.Service.GetBattleAsync(battleId))!;

        Assert.Equal(before.Sequence + 1, after.Sequence);

        // The Boss's application survived the same round's step 19a at 1 Turn...
        Assert.Equal(1, Assert.Single(after.BossState.ActiveStatusEffects, e => e.Id == "flame-burst").RemainingTurns);

        // ...while the Pet's 1-Turn instance was consumed to 0 and removed in that same
        // pass, in that same write (GAME_STATE.md §5.1.1 items 4–5).
        Assert.DoesNotContain(after.PetState.ActiveStatusEffects, e => e.Id == "Root");

        // The committed record is what the round reported: one write, no second commit.
        Assert.Equal(after.Sequence, fired.Result.State.Sequence);
        Assert.Equal(after.PetState.ATK, fired.Result.State.PetState.ATK);
        Assert.Equal(
            after.BossState.ActiveStatusEffects.Length,
            fired.Result.State.BossState.ActiveStatusEffects.Length);
        Assert.Equal(
            after.PetState.ActiveStatusEffects.Length,
            fired.Result.State.PetState.ActiveStatusEffects.Length);

        AssertOneWriteBack(fired);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    /// <summary>
    /// The Pet with a survivable HP pool and, where the scenario needs it, the Power it
    /// starts from (<c>GAME_RULES.md</c> §12's 0–100 range). The HP pool keeps a
    /// multi-round scenario observing the Skill rather than
    /// <c>GAME_RULES.md</c> §1.4's terminal outcome.
    /// </summary>
    private static BattleState Surviving(BattleState state, int power = 0) =>
        state with
        {
            PetState = state.PetState with
            {
                HP = ObservationWindowHp,
                MaxHP = ObservationWindowHp,
                Power = power,
            },
        };

    /// <summary>
    /// Commits one atomic Round in which the named Boss's Skill fires, on a battle the
    /// scenario has not yet pinned, and returns the round with its store accounting.
    /// </summary>
    /// <param name="battleId">The battle session to create.</param>
    /// <param name="definition">The Boss whose Skill is under test.</param>
    /// <param name="petPower">
    /// The Pet's Power before the round, for the instance-less Drain Power scenario
    /// (<c>GAME_RULES.md</c> §12's 0–100 range).
    /// </param>
    private static async Task<FiredSkillRound> FireTheSkillOnAFreshBattleAsync(
        string battleId,
        BossDefinition definition,
        int petPower = 0)
    {
        var harness = Harness.Create();
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, definition);

        var before = Surviving(created, petPower);

        var round = await FireTheSkillAsync(harness, battleId, definition, before, petState: before.PetState);

        return new FiredSkillRound(harness, before, round.Result, round.WritesBeforeTheRound);
    }

    /// <summary>
    /// Pins the round's Player action and the Skill's eligibility into the store, and
    /// returns the Swap to execute plus the write count the round starts from.
    ///
    /// <b>The Player action is a non-ATK Match.</b> <see cref="RoundTestSupport.PlayerMatch"/>
    /// builds a board whose one legal Match-producing Swap clears only
    /// <paramref name="matchType"/> Gems and no ATK Gem, so the Player resolves no damage
    /// instance (ADR-026 D1) and the round's combat modifiers are the Skill's alone. The
    /// type is <see cref="GemType.Def"/> unless a scenario needs the acting actor's other
    /// non-offensive effect (ADR-026 §4's actor-relative Heal for
    /// <see cref="GemType.Hp"/>).
    ///
    /// <b>The charge is pinned, not lowered.</b> <c>BOSS_RULES.md</c> §6.3 /
    /// <c>GAME_STATE.md</c> §2.4.3: the Skill is eligible iff
    /// <c>SkillCharge &gt;= ChargeRequirement</c> AND <c>SkillCooldown == 0</c>. The
    /// scenario pins exactly that pair on the state the game reaches by accumulating the
    /// Boss's own POWER match groups (ADR-026 D2) and never edits the definition's own
    /// Charge Requirement.
    /// </summary>
    private static async Task<PinnedSkillRound> PinSkillRoundAsync(
        Harness harness,
        BattleState from,
        BossDefinition definition,
        PetState? petState = null,
        IReadOnlyList<StatusEffect>? bossEffects = null,
        GemType matchType = GemType.Def)
    {
        var pinned = RoundTestSupport.PlayerMatch(matchType, from.RngState, from.LastCommittedSwapPair);

        // The fixture's own precondition: the Player's cascade clears no ATK Gem, so the
        // Player contributes no damage instance to this round (ADR-026 D1).
        Assert.Equal(0, pinned.Resources.ClearedGemCounts.Atk);

        await RoundTestSupport.PinAsync(
            harness.Store,
            from,
            pinned.Board,
            bossState: from.BossState with
            {
                SkillCharge = definition.SkillChargeRequirement,
                SkillCooldown = 0,
                ActiveStatusEffects = bossEffects is null
                    ? from.BossState.ActiveStatusEffects
                    : [.. bossEffects],
            },
            petState: petState ?? from.PetState);

        return new PinnedSkillRound(pinned.Swap, harness.Store.WriteCount);
    }

    /// <summary>
    /// Commits one atomic Round in which the named Boss's Skill fires, and asserts the
    /// eligibility outcome this suite's scenarios all rest on: the Skill was cast.
    /// </summary>
    private static async Task<FiredSkillRound> FireTheSkillAsync(
        Harness harness,
        string battleId,
        BossDefinition definition,
        BattleState from,
        PetState? petState = null,
        IReadOnlyList<StatusEffect>? bossEffects = null,
        GemType matchType = GemType.Def)
    {
        var pinnedRound = await PinSkillRoundAsync(harness, from, definition, petState, bossEffects, matchType);

        var result = (await harness.Service.ExecuteSwapAsync(battleId, pinnedRound.Swap))
            ?? throw new InvalidOperationException("the Swap must resolve");

        Assert.True(result.IsAccepted);

        // GAME_RULES.md §17 step 18c / SIGNALR_PROTOCOL.md §3.2.18: the Skill announces
        // itself when it fires — the observable proof that step 18c was reached.
        Assert.Contains(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        return new FiredSkillRound(harness, from, result, pinnedRound.WritesAfterPin);
    }

    /// <summary>
    /// Asserts that the fired Skill produced no attack instance of its own: the round
    /// carries exactly the ATK-gated Boss instance when the Boss's own board action
    /// cleared an ATK Gem, and none otherwise (<c>ADR-026</c> D1, D3;
    /// <c>GAME_RULES.md</c> §17 step 18d).
    /// </summary>
    private static void AssertNoDirectSkillDamage(SwapExecutionResult result)
    {
        var bossDamageInstances = result.Events.Count(
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);

        Assert.Equal(RoundTestSupport.BossClearedAtkGem(result) ? 1 : 0, bossDamageInstances);

        // GAME_EVENTS.md §1: the three reports describe one instance, so the round holds
        // one DamageCalculated per damage instance and never an extra one for a Skill.
        Assert.Equal(
            bossDamageInstances,
            result.Events.Count(e => e.Type == BattleEventType.DamageCalculated));
    }

    /// <summary>
    /// Asserts that the resolution emitted no event naming a Status Effect or a Boss
    /// Skill's effect.
    ///
    /// <c>GAME_RULES.md</c> §16's canonical event list is closed and
    /// <c>BOSS_RULES.md</c> §7 enumerates step 18's events; <c>GAME_STATE.md</c> §5.1.1
    /// item 10 and <c>COMBAT_RULES.md</c> §4 item 4 each state that their lifecycle adds
    /// no event. The single documented exception is Drain Power, whose
    /// <c>PetState.Power</c> mutation is reported by the existing <c>PowerChanged</c>
    /// (<c>GAME_EVENTS.md</c> §2 item 4) — asserted in section 2 above, not here.
    /// </summary>
    private static void AssertNoEffectSpecificEvent(SwapExecutionResult result)
    {
        var types = result.Events.Select(e => e.Type.ToString()).ToArray();

        foreach (var forbidden in new[]
                 {
                     "BurnApplied", "BurnTicked", "StatusEffectApplied", "StatusTicked",
                     "BossSkillEffect", "DebuffApplied", "BuffApplied", "RootApplied",
                     "PowerDrained", "ShieldApplied", "ShieldGranted", "ShieldDepleted",
                     "AtkBuffApplied", "ThunderStrikeApplied",
                 })
        {
            Assert.DoesNotContain(forbidden, types);
        }
    }

    /// <summary>
    /// Asserts the round's one-write-back property: every effect it applied or consumed
    /// reached the store in exactly one accepted write after the scenario's own pinning
    /// (<c>REDIS_STATE.md</c> §4 item 5, <c>GAME_STATE.md</c> §5.1.1 item 9).
    /// </summary>
    private static void AssertOneWriteBack(FiredSkillRound fired) =>
        AssertOneWriteBack(fired.Harness, fired.WritesBeforeTheRound);

    private static void AssertOneWriteBack(Harness harness, int writesBefore) =>
        Assert.Equal(writesBefore + 1, harness.Store.WriteCount);

    /// <summary>
    /// One committed Skill-firing round, with what it started from and the store write
    /// count the round itself begins at (the scenario's pinning already written).
    /// </summary>
    private sealed record FiredSkillRound(
        Harness Harness,
        BattleState Before,
        SwapExecutionResult Result,
        int WritesBeforeTheRound);

    /// <summary>The Swap a pinned round acts on, plus the write count after the pinning.</summary>
    private sealed record PinnedSkillRound(SwapRequest Swap, int WritesAfterPin);

    /// <summary>
    /// A battle service over an in-memory store, plus the store itself, so a scenario
    /// can both run the real atomic-round resolution and read its own write accounting.
    ///
    /// The double models the documented creation, <c>Sequence</c> compare-and-set, and
    /// forced-conflict semantics (<c>REDIS_STATE.md</c> §4), so no test here passes
    /// against a more permissive contract than the store offers.
    /// </summary>
    private sealed record Harness(
        BattleStateService Service,
        InMemoryBattleStateRepository Store)
    {
        public static Harness Create()
        {
            var store = new InMemoryBattleStateRepository();

            return new Harness(new BattleStateService(store, new FixedRngSeedSource()), store);
        }
    }
}
