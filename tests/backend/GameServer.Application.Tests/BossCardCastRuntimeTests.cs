using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// The TASK-308 end-to-end Boss Card runtime — provisioned-content active-form
/// resolution at battle creation and the deterministic one-action selection
/// integrated into the atomic round.
///
/// <code>
/// Rule (BOSS_RULES.md §4.3–§4.4, §6.3; CARD_RULES.md §1.1/§3.6;
///       GAME_RULES.md §17 steps 18a–18d, 19a; GAME_STATE.md §2.4.3)
///  ↓
/// Scenario (Given the provisioned earthquake Boss Card and a committed response
///           phase, When the policy selects the board action / the cast, Then
///           exactly one action's state and events are produced)
///  ↓
/// Test
/// </code>
///
/// <b>These use the real <see cref="BattleStateService"/> pipeline</b> over the
/// in-memory active-state double and a fake content read that returns the real
/// provisioned Boss Card definitions (<see cref="ProvisionedBossCardContent"/>). The
/// only test-side substitutions are the two documented ones: the store (a test
/// double, as everywhere in this suite) and the cast-priority score where a scenario
/// must force one branch deterministically. The authored score's own <c>15/16</c>
/// boundary is a Domain policy concern tested with <c>BossActionPolicyTests</c>; the
/// integrated test below uses the real scored definition on a searched board, and
/// <c>TASK309BossCardEffectRuntimeTests</c> proves the TASK-309 <c>85</c> gate
/// through committed rounds.
///
/// <b>The legacy form keeps its coverage through an explicitly unmigrated fixture.</b>
/// A composition whose content read exposes no <c>BossCard</c> row is the
/// transitional position (<c>BOSS_RULES.md</c> §4.4 item 6) and still resolves the
/// legacy <c>BossSkill</c> form; that scenario is exercised deliberately rather than
/// by weakening the migrated-form coverage.
/// </summary>
public class BossCardCastRuntimeTests
{
    private const string EarthquakeCardId = "earthquake";
    private const string SonThachVeIdentity = "boss-son-thach-ve";

    /// <summary>The approved <c>earthquake</c> values (TASK-308 §1).</summary>
    private const int EarthquakePowerCost = 30;
    private const int EarthquakeShield = 150;

    private static readonly PlayerId Owner = new("player_boss_card_runtime_owner");

    private static readonly BattleStateService.PetConfiguration Pet =
        new(
            new PetId("pet_instance_boss_card_runtime"),
            Element.Hoa,
            new PassiveId("xich-lang"),
            PassiveThreshold: 5);

    private const string PlayerPowerChargeCardId = "card-power-charge";

    private static readonly BattleStateService.PetConfiguration PetWithCard =
        Pet with
        {
            EquippedCards =
            [
                new EquippedCardIdentity(PlayerPowerChargeCardId),
            ],
        };

    // =======================================================================
    // Creation: the active form, the score, and the fail-closed content gates
    // =======================================================================

    [Fact]
    public async Task CreateBattle_WithProvisionedBossCard_ShouldResolveTheFormAndScoreExactlyOnce()
    {
        // ADR-029 §2.5 as amended / OD-1c: the form is provisioned-content
        // presence, resolved exactly ONCE at battle creation through the id-only
        // lookup (identity = the ability SkillId), and cached with the Boss
        // content input. No turn re-reads it.
        var repository = new InMemoryBattleStateRepository();
        var lookup = new TestCardDefinitionLookup().With(EarthquakeCard());

        var service = new BattleStateService(
            repository,
            new FixedRngSeedSource(),
            cardDefinitions: lookup);

        var created = await service.CreateBattleAsync(
            "battle-boss-card-create",
            Owner,
            Pet,
            BossDefinitions.SonThachVe);

        // The content read ran once, keyed by the ability SkillId, and the battle
        // was created with the Boss at its authored creation values.
        Assert.Equal([EarthquakeCardId], lookup.Reads);
        Assert.Equal(SonThachVeIdentity, created.BossState.BossId.Value);
        Assert.Equal(BossDefinitions.SonThachVe.MaxHP, created.BossState.HP);

        // A committed round does not re-read the content: the definition is frozen
        // at creation (REDIS_STATE.md §4 item 6, TDD.md §4 item 3).
        var pair = FindMatchProducingPair(created.BoardState);
        var round = await service.ExecuteSwapAsync("battle-boss-card-create", pair);

        Assert.NotNull(round);
        Assert.True(round.Value.IsAccepted);
        Assert.Single(lookup.Reads);
    }

    [Fact]
    public async Task CreateBattle_WithThePostMigrationBossCardSet_ShouldResolveTheBossCardFormForEveryMvpBoss()
    {
        // TASK-309 Slice 4: after the four data-only migrations every MVP Boss has its
        // provisioned BossCard row, so the active form is the Boss Card for all five,
        // resolved once at creation and keyed by the ability SkillId
        // (BOSS_RULES.md §4.4 item 6; ADR-029 §2.5 as amended).
        foreach (var definition in BossDefinitions.All)
        {
            var repository = new InMemoryBattleStateRepository();
            var lookup = new TestCardDefinitionLookup().WithProvisionedBossCards();

            var service = new BattleStateService(
                repository,
                new FixedRngSeedSource(),
                cardDefinitions: lookup);

            var battleId = $"battle-boss-card-provisioned-{definition.SkillId}";
            var created = await service.CreateBattleAsync(battleId, Owner, Pet, definition);

            // The form resolved exactly once, by the ability's SkillId, and the battle
            // carries the Boss's own canonical identity.
            Assert.Equal([definition.SkillId], lookup.Reads);
            Assert.Equal(definition.BossId.Value, created.BossState.BossId.Value);

            // The authored score the selection reads (TASK-309 B-1): 16 for the
            // TASK-308 Boss and 85 for the four Slice 4 Bosses.
            Assert.Equal(
                definition.SkillId == EarthquakeCardId ? 16 : 85,
                definition.CastPriorityScore);

            // A committed round does not re-read the frozen content input.
            var pair = FindMatchProducingPair(created.BoardState);
            var round = await service.ExecuteSwapAsync(battleId, pair);

            Assert.NotNull(round);
            Assert.True(round.Value.IsAccepted);
            Assert.Single(lookup.Reads);
        }
    }

    [Fact]
    public async Task CreateBattle_WithACardOfTheWrongCategory_ShouldFailClosed()
    {
        // Present-but-invalid content fails creation closed — the legacy Skill
        // form is never substituted (OD-1c; AGENTS.md §7).
        var repository = new InMemoryBattleStateRepository();
        var lookup = new TestCardDefinitionLookup()
            .With(EarthquakeCard(CardCategory.Basic));

        var service = new BattleStateService(
            repository,
            new FixedRngSeedSource(),
            cardDefinitions: lookup);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateBattleAsync("battle-boss-card-bad-category", Owner, Pet, BossDefinitions.SonThachVe));

        Assert.Contains(EarthquakeCardId, thrown.Message, StringComparison.Ordinal);

        // Nothing was stored: a failed creation creates no battle.
        Assert.Equal(0, repository.RecordCount);
    }

    [Fact]
    public async Task CreateBattle_WithAProvisionedCardButNoCastPriorityScore_ShouldFailClosed()
    {
        // A migrated Boss whose authored score is NULL is invalid definition data
        // (BOSS_RULES.md §4.3 item 5; ADR-024 §2.4). No default, no board-only
        // degradation, no fallback form.
        var repository = new InMemoryBattleStateRepository();
        var lookup = new TestCardDefinitionLookup().With(EarthquakeCard());

        var service = new BattleStateService(
            repository,
            new FixedRngSeedSource(),
            cardDefinitions: lookup);

        var boss = BossDefinitions.SonThachVe with { CastPriorityScore = null };

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateBattleAsync("battle-boss-card-null-score", Owner, Pet, boss));

        Assert.Contains("CastPriorityScore", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(0, repository.RecordCount);
    }

    [Fact]
    public async Task CreateBattle_WithANegativeCardPowerCost_ShouldFailClosed()
    {
        // The authored cost must be non-negative (CARD_RULES.md §3.6 item 4).
        var repository = new InMemoryBattleStateRepository();
        var lookup = new TestCardDefinitionLookup()
            .With(EarthquakeCard(powerCost: -1));

        var service = new BattleStateService(
            repository,
            new FixedRngSeedSource(),
            cardDefinitions: lookup);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateBattleAsync("battle-boss-card-bad-cost", Owner, Pet, BossDefinitions.SonThachVe));

        Assert.Contains("PowerCost", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(0, repository.RecordCount);
    }

    [Fact]
    public async Task CreateBattle_WithAnUnmigratedBossFixture_ShouldCastNothingAndResolveTheBoardAction()
    {
        // Content presence is the signal (BOSS_RULES.md §4.4 item 5–6): this is the
        // explicit UNMIGRATED fixture — a content composition that exposes no BossCard
        // row — so the Boss has no castable ability. Its form resolution finds nothing,
        // no cast event of any kind is emitted, and the response phase is the board
        // action alone. The retired legacy BossSkill fallback that used to fire on the
        // charge/cooldown condition was removed with ADR-029 Slice 5 / TASK-313.
        var harness = BossCardTestHarness.CreateWithoutProvisionedCards();

        var created = await harness.Service.CreateBattleAsync(
            "battle-boss-card-legacy",
            Owner,
            Pet,
            BossDefinitions.SonThachVe);

        await PinBossStateAsync(
            harness.Service,
            harness.Store,
            "battle-boss-card-legacy",
            created.BossState with
            {
                Power = 100,
                SkillCooldown = 0,
            });

        var state = (await harness.Service.GetBattleAsync("battle-boss-card-legacy"))!;
        var round = await harness.Service.ExecuteSwapAsync(
            "battle-boss-card-legacy",
            FindMatchProducingPair(state.BoardState));

        Assert.NotNull(round);
        Assert.True(round.Value.IsAccepted);

        // The unmigrated fixture exposed no card row, so the creation-time form
        // resolution asked for the ability identity and found nothing.
        Assert.Equal([BossDefinitions.SonThachVe.SkillId], harness.CardDefinitions.Reads);

        // No cast: the Boss Card form is absent and the legacy form no longer exists.
        Assert.DoesNotContain(round.Value.Events, e => e.Type == BattleEventType.BossCardCast);

        // The board action still resolves (the Boss's own boundary pair is present).
        Assert.Contains(
            round.Value.Events,
            e => e.Type == BattleEventType.SwapStarted && e.SwapStarted.Actor == DamageParty.Boss);
    }

    // =======================================================================
    // A cast-selected round — cost, Shield, cooldown, event, suppressions
    // =======================================================================

    [Fact]
    public async Task Round_WhenCastSelected_ShouldDeductPowerApplyShieldSetCooldownAndEmitOneCastAfterPassiveEvents()
    {
        // The cast is forced through a high test score override so the branch is
        // deterministic. A match-charged Passive is attached so step 18b produces
        // its events in the same round, proving the cast is emitted after them.
        var boss = BossDefinitions.SonThachVe with
        {
            CastPriorityScore = int.MaxValue,
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition,
            SkillDefinition = BossDefinitions.SonThachVe.SkillDefinition with { CooldownTurns = 2 },
            MaxHP = 1_000_000,
            ATK = 0,
        };

        var (service, repository) = NewMigratedHarness();
        var created = await service.CreateBattleAsync("battle-boss-card-cast", Owner, Pet, boss);

        var pinned = created.BossState with
        {
            Power = 50,
            SkillCooldown = 0,
            PassiveProgress = created.BossState.PassiveProgress with { Current = 4 },
        };

        await PinBossStateAsync(service, repository, "battle-boss-card-cast", pinned);

        var state = (await service.GetBattleAsync("battle-boss-card-cast"))!;
        var round = await service.ExecuteSwapAsync(
            "battle-boss-card-cast",
            FindMatchProducingPair(state.BoardState));

        Assert.NotNull(round);
        Assert.True(round.Value.IsAccepted);

        var committed = round.Value.State;
        var events = round.Value.Events;

        // Exactly one BossCardCast, carrying the two documented identities.
        var casts = events.Where(e => e.Type == BattleEventType.BossCardCast).ToArray();
        Assert.Single(casts);
        Assert.Equal(EarthquakeCardId, casts[0].BossCardCast.CardId);
        Assert.Equal(SonThachVeIdentity, casts[0].BossCardCast.SourceId);

        // Step 18b still evaluated and emitted its events; the cast follows them,
        // and nothing follows the cast in a cast-selected phase.
        Assert.Contains(events, e => e.Type == BattleEventType.PassiveTriggered);
        Assert.Equal(BattleEventType.BossCardCast, events[^1].Type);

        // Power deducted exactly once: 50 - 30 = 20, in the one write-back.
        Assert.Equal(EarthquakePowerCost, pinned.Power - committed.BossState.Power);
        Assert.Equal(50 - EarthquakePowerCost, committed.BossState.Power);

        // The Shield effect resolved against the Boss's own state: 150 Flat,
        // one trigger-based instance (COMBAT_RULES.md §4).
        var shield = Assert.Single(committed.BossState.ActiveStatusEffects, e => e.Id == "Shield");
        Assert.Equal(StatusEffectType.Shield, shield.Type);
        Assert.Equal(StatusEffectSource.Boss, shield.Source);
        Assert.Equal(EarthquakeShield, (int)shield.Magnitude);

        // The authored cooldown was set in the same write-back.
        Assert.Equal(2, committed.BossState.SkillCooldown);

        // No Boss board action: no Boss boundary pair and no second board cycle.
        // The Player's own boundary pair and its MatchedGemSummary survive.
        Assert.Single(events, e => e.Type == BattleEventType.SwapStarted);
        Assert.Equal(
            DamageParty.Player,
            Assert.Single(events, e => e.Type == BattleEventType.SwapStarted).SwapStarted.Actor);
        Assert.Single(events, e => e.Type == BattleEventType.SwapResolved);
        Assert.Single(events, e => e.Type == BattleEventType.MatchedGemSummary);

        // No Boss attack/damage event and no Boss PowerChanged (C-6): a cast phase
        // suppresses step 18d entirely and its Power spend is state-only.
        Assert.DoesNotContain(
            events,
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);
        Assert.DoesNotContain(
            events,
            e => e.Type == BattleEventType.DamageTaken && e.DamageTaken.Source == DamageParty.Boss);
        Assert.DoesNotContain(
            events,
            e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Boss);

        // One form per ability: the Card event is the only Boss cast event the wire
        // contract defines (the legacy BossSkillCast was removed with ADR-029 Slice 5 /
        // TASK-313, so no second event can accompany it).
    }

    [Fact]
    public async Task Round_WhenBoardActionSelectedForTheMigratedBoss_ShouldEmitNoBossCastEventAndShouldTickCooldown()
    {
        // The migrated Boss casts only through its Boss Card, and a cast needs its
        // Power cost. With the Power unaffordable the board action is selected, so no
        // cast event is emitted and the cooldown simply ticks (BOSS_RULES.md §4.4
        // item 3; §4.3 rows 2/3/5). The legacy charge-driven Skill gate this test used
        // to contrast with was removed with ADR-029 Slice 5 / TASK-313.
        var boss = BossDefinitions.SonThachVe with
        {
            MaxHP = 1_000_000,
            ATK = 0,
        };

        var (service, repository) = NewMigratedHarness();
        var created = await service.CreateBattleAsync("battle-boss-card-board", Owner, Pet, boss);

        // The cast is made unaffordable, so the board action is selected; the
        // cooldown is a live value that ticks in this round.
        await PinBossStateAsync(
            service,
            repository,
            "battle-boss-card-board",
            created.BossState with
            {
                Power = 0,
                SkillCooldown = 1,
            });

        var state = (await service.GetBattleAsync("battle-boss-card-board"))!;
        var round = await service.ExecuteSwapAsync(
            "battle-boss-card-board",
            FindMatchProducingPair(state.BoardState));

        Assert.NotNull(round);
        Assert.True(round.Value.IsAccepted);

        Assert.DoesNotContain(round.Value.Events, e => e.Type == BattleEventType.BossCardCast);

        // The cooldown ticked once for the committed Round and was not reset.
        Assert.Equal(0, round.Value.State.BossState.SkillCooldown);

        // The board action still ran: the Boss's own boundary pair and cycle are
        // present (today's board path is unchanged for a migrated Boss).
        Assert.Contains(
            round.Value.Events,
            e => e.Type == BattleEventType.SwapStarted
                && e.SwapStarted.Actor == DamageParty.Boss);
    }

    [Fact]
    public async Task Round_WhenCastSelected_ShouldSurviveARejectedWriteWithOneCommitDeductionAndEvent()
    {
        // CAS retry determinism (REDIS_STATE.md §4 items 2–6): a refused write
        // commits nothing, the retried resolution re-runs from the same committed
        // state and the frozen content inputs, and exactly one write, one Power
        // deduction and one BossCardCast are produced.
        var boss = BossDefinitions.SonThachVe with
        {
            CastPriorityScore = int.MaxValue,
            MaxHP = 1_000_000,
            ATK = 0,
        };

        var (service, repository) = NewMigratedHarness();
        var created = await service.CreateBattleAsync("battle-boss-card-retry", Owner, Pet, boss);

        await PinBossStateAsync(
            service,
            repository,
            "battle-boss-card-retry",
            created.BossState with { Power = 50, SkillCooldown = 0 });

        var writesBefore = repository.WriteCount;

        // Refuse the next compare-and-set once, then let the retry commit.
        repository.ForcedConflicts = 1;

        var state = (await service.GetBattleAsync("battle-boss-card-retry"))!;
        var pair = FindMatchProducingPair(state.BoardState);
        var round = await service.ExecuteSwapAsync(
            "battle-boss-card-retry",
            new SwapRequest(pair.From, pair.To, ExpectedSequence: state.Sequence));

        Assert.NotNull(round);
        Assert.True(round.Value.IsAccepted, $"reason={round.Value.Reason}");

        // One committed write for the round (the refused attempt wrote nothing),
        // one Power deduction, one event.
        Assert.Equal(writesBefore + 1, repository.WriteCount);
        Assert.Equal(50 - EarthquakePowerCost, round.Value.State.BossState.Power);
        Assert.Single(round.Value.Events, e => e.Type == BattleEventType.BossCardCast);

        var stored = (await service.GetBattleAsync("battle-boss-card-retry"))!;
        Assert.Equal(50 - EarthquakePowerCost, stored.BossState.Power);
    }

    [Fact]
    public async Task Round_WithTheRealAuthoredScore_ShouldSelectTheCastOnACrafted15PointBoard()
    {
        // The real SonThachVe definition (authored score 16) can only out-score the
        // board action when the selected candidate scores exactly 15 — the
        // documented minimum board score and the only one the authored 16 strictly
        // beats (BOSS_RULES.md §4.3 steps 5–6; TASK-307 §3.2). This test crafts
        // that board and proves the integrated round selects the cast on it.
        //
        // The target is a stable 8x8 pattern (derived from a deadlock-free
        // periodic board) with exactly ONE legal swap, a 3-DEF match scoring
        // 5 x 3 = 15. The pre-swap board is derived from it so that one committed
        // player swap clears three top-row cells and the deterministic spawn
        // restores the target: the player swaps (0,3)/(0,4), creating the 3-DEF
        // match at (0,4)-(0,6). Because the spawn stream is seed-derived
        // (GAME_STATE.md §2.6), the seed is searched deterministically until the
        // spawned values reproduce the target's three cells.
        var boss = BossDefinitions.SonThachVe with
        {
            MaxHP = 1_000_000,
            ATK = 0,
        };

        var preSwapBoard = BoardFromRows(
            "ABCBDBBD",
            "BCDABCDA",
            "CDABCDAB",
            "DABCDABC",
            "ABCDABCD",
            "BCDABCDA",
            "CDABCDAB",
            "DABCDABC");

        var (service, repository) = NewMigratedHarness();

        for (var seed = 1; seed <= 1000; seed++)
        {
            var battleId = $"battle-boss-card-real-score-{seed}";
            var created = await service.CreateBattleAsync(
                battleId,
                Owner,
                Pet,
                boss,
                new BattleSeed((ulong)seed));

            // Pin the crafted pre-swap board and the Boss content the cast gate
            // reads: enough Power and a zero cooldown.
            var pinned = created with
            {
                BoardState = preSwapBoard,
                BossState = created.BossState with { Power = 100, SkillCooldown = 0 },
            };

            Assert.True(await repository.TryUpdateAsync(pinned, created.Sequence));

            var round = await service.ExecuteSwapAsync(
                battleId,
                new SwapRequest(3, 4, ExpectedSequence: created.Sequence));

            Assert.NotNull(round);
            Assert.True(round.Value.IsAccepted);

            if (round.Value.Events.Any(e => e.Type == BattleEventType.BossCardCast))
            {
                var cast = Assert.Single(round.Value.Events, e => e.Type == BattleEventType.BossCardCast);
                Assert.Equal(EarthquakeCardId, cast.BossCardCast.CardId);
                Assert.Equal(SonThachVeIdentity, cast.BossCardCast.SourceId);

                // The authored score 16 beat the 15-point board action, and the
                // card cost was deducted exactly once.
                Assert.Equal(100 - EarthquakePowerCost, round.Value.State.BossState.Power);
                return;
            }
        }

        Assert.Fail(
            "The real authored score (16) did not select the cast on the crafted 15-point board.");
    }

    /// <summary>
    /// Builds a board from eight row strings where <c>A</c>=Atk, <c>B</c>=Def,
    /// <c>C</c>=Hp, and <c>D</c>=Power — a readable way to express a crafted
    /// board in a test.
    /// </summary>
    private static BoardState BoardFromRows(params string[] rows)
    {
        var cells = new GemType[BoardState.CellCount];

        for (var row = 0; row < BoardState.Rows; row++)
        {
            for (var column = 0; column < BoardState.Columns; column++)
            {
                cells[BoardState.ToIndex(row, column)] = rows[row][column] switch
                {
                    'A' => GemType.Atk,
                    'B' => GemType.Def,
                    'C' => GemType.Hp,
                    _ => GemType.Power,
                };
            }
        }

        return BoardState.FromCells(cells);
    }
    [Fact]
    public async Task Round_WhenTheBossCardIsOnCooldown_ShouldNotCastAndShouldTickTheCooldown()
    {
        // BOSS_RULES.md §4.3 item 5 / §4.4 item 3: a cast is available only when its
        // cooldown has reached 0. With Power affordable but the cooldown running, the
        // migrated Boss therefore takes the board action instead: no Boss Card event is
        // emitted and the cooldown ticks down by exactly one for the committed round.
        var boss = BossDefinitions.SonThachVe with
        {
            MaxHP = 1_000_000,
            ATK = 0,
        };

        var (service, repository) = NewMigratedHarness();
        var created = await service.CreateBattleAsync("battle-boss-card-cooldown", Owner, Pet, boss);

        await PinBossStateAsync(
            service,
            repository,
            "battle-boss-card-cooldown",
            created.BossState with
            {
                Power = 100,
                SkillCooldown = 2,
            });

        var state = (await service.GetBattleAsync("battle-boss-card-cooldown"))!;
        var round = await service.ExecuteSwapAsync(
            "battle-boss-card-cooldown",
            FindMatchProducingPair(state.BoardState));

        Assert.NotNull(round);
        Assert.True(round.Value.IsAccepted, $"reason={round.Value.Reason}");

        Assert.DoesNotContain(round.Value.Events, e => e.Type == BattleEventType.BossCardCast);

        // The cooldown ticked once and was not reset by a cast.
        Assert.Equal(1, round.Value.State.BossState.SkillCooldown);

        // The board action ran instead: the Boss's own boundary pair is present.
        Assert.Contains(
            round.Value.Events,
            e => e.Type == BattleEventType.SwapStarted
                && e.SwapStarted.Actor == DamageParty.Boss);
    }

    // =======================================================================
    // Player cast contracts remain byte-identical
    // =======================================================================

    [Fact]
    public async Task PlayerCardCast_ShouldStayUnchangedOnAMigratedBattle()
    {
        // CARD_RULES.md §3 item 9: the Player's cast contract is untouched by the
        // Boss actor's. A Boss Card in content changes neither the Player's
        // CardCast event nor its Power accounting.
        var repository = new InMemoryBattleStateRepository();
        var lookup = new TestCardDefinitionLookup()
            .With(EarthquakeCard())
            .With(PlayerPowerChargeCard());

        var service = new BattleStateService(
            repository,
            new FixedRngSeedSource(),
            cardDefinitions: lookup);

        await service.CreateBattleAsync(
            "battle-boss-card-player-cast",
            Owner,
            PetWithCard,
            BossDefinitions.SonThachVe);

        var before = (await service.GetBattleAsync("battle-boss-card-player-cast"))!;
        var result = await service.ExecuteCardCastAsync(
            "battle-boss-card-player-cast",
            PlayerPowerChargeCardId);

        Assert.NotNull(result);
        Assert.True(result.Value.IsAccepted);

        var cast = Assert.Single(result.Value.Events, e => e.Type == BattleEventType.CardCast);
        Assert.Equal(PlayerPowerChargeCardId, cast.CardCast.CardId);
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BossCardCast);
        Assert.Equal(before.BossState.Power, result.Value.State.BossState.Power);
    }

    // =======================================================================
    // Fixtures and helpers
    // =======================================================================

    /// <summary>
    /// The real provisioned <c>earthquake</c> Boss Card definition
    /// (TASK-308 §1): identity = the ability <c>SkillId</c>, <c>BossCard</c>
    /// category, <c>PowerCost 30</c>, one <c>Shield</c>/<c>Flat</c>/<c>150</c>
    /// effect, and <c>LoadoutCopyLimit 1</c>.
    /// </summary>
    private static CardDefinition EarthquakeCard(
        CardCategory category = CardCategory.BossCard,
        int powerCost = EarthquakePowerCost) =>
        new()
        {
            CardDefinitionId = EarthquakeCardId,
            Name = "Earthquake",
            Category = category,
            PowerCost = powerCost,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.Flat, EarthquakeShield)),
            LoadoutCopyLimit = 1,
        };

    private static CardDefinition PlayerPowerChargeCard() =>
        new()
        {
            CardDefinitionId = PlayerPowerChargeCardId,
            Name = "Power Charge",
            Category = CardCategory.Basic,
            PowerCost = 0,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25)),
            LoadoutCopyLimit = 1,
        };

    /// <summary>
    /// A service whose content read serves the provisioned <c>earthquake</c> Boss
    /// Card, so <see cref="BossDefinitions.SonThachVe"/> resolves the migrated form.
    /// </summary>
    private static (BattleStateService Service, InMemoryBattleStateRepository Repository)
        NewMigratedHarness()
    {
        var repository = new InMemoryBattleStateRepository();

        return (
            new BattleStateService(
                repository,
                new FixedRngSeedSource(),
                cardDefinitions: new TestCardDefinitionLookup().With(EarthquakeCard())),
            repository);
    }

    /// <summary>
    /// Writes a Boss state into the authoritative record through the store's own
    /// <c>Sequence</c> compare-and-set — the same pinning pattern the existing
    /// Boss-response suites use, so a scenario starts from a state the game
    /// genuinely produces rather than an invented one.
    /// </summary>
    private static async Task PinBossStateAsync(
        BattleStateService service,
        InMemoryBattleStateRepository repository,
        string battleId,
        BossState bossState)
    {
        var state = (await service.GetBattleAsync(battleId))!;

        var written = await repository.TryUpdateAsync(
            state with { BossState = bossState },
            state.Sequence);

        Assert.True(written, "the scenario's Boss state could not be pinned: the stored Sequence moved on.");
    }

    /// <summary>
    /// The first adjacent pair of <paramref name="board"/> whose exchange produces
    /// a §3 Match, skipping the pair a previous committed Swap recorded
    /// (<c>MATCH3_RULES.md</c> §1.4, §2.1.4 item 2).
    /// </summary>
    private static SwapRequest FindMatchProducingPair(
        BoardState board,
        CommittedSwapPair? committed = null)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (committed is { } recorded && recorded.MatchesPair(from, to))
            {
                continue;
            }

            if (MatchDetector.Detect(board.WithSwapped(from, to)).Count > 0)
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
