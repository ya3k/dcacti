using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using Xunit;

namespace GameServer.Application.Tests.Battle;

/// <summary>
/// The <c>PowerChanged</c> emission contract as the Application layer runs it —
/// <c>GAME_EVENTS.md</c> §2 item 4 and <c>SIGNALR_PROTOCOL.md</c> §3.2.24
/// items 5–6.
///
/// <code>
/// Rule (GAME_EVENTS.md §2 item 4, SIGNALR_PROTOCOL.md §3.2.24)
///  ↓
/// Scenario (Given authoritative Power mutation X, When the owning stage runs,
///           Then one PowerChanged carries its delta, its resulting Power,
///           and its owning source)
///  ↓
/// Test
/// </code>
///
/// <b>The contract under test is one mutation, one event.</b> Every
/// authoritative gameplay mutation of <c>PetState.Power</c> emits
/// <c>PowerChanged</c> from the stage that owns it, carrying the signed change
/// the mutation actually applied and the resulting authoritative value. Several
/// mutations in one action are never collapsed into a single net event, and the
/// events preserve the authoritative mutation order.
///
/// <b>The four sources are the whole value set</b> — <c>"match"</c>,
/// <c>"card"</c>, <c>"relic"</c>, and <c>"boss"</c> — and each names the stage
/// that owns the mutation rather than the direction of the change. The Relic
/// source is covered by its own suites
/// (<see cref="RelicStageResolutionTests"/>, <c>RelicResolverTests</c>) and is
/// exercised here only as a regression guard that its emission still sits at
/// step 11 with the values it always carried.
/// </summary>
public sealed class PowerChangedEmissionTests
{
    private const string BattleId = "battle-powerchanged";

    private static readonly PlayerId Owner = new("player_powerchanged");

    private static readonly BattleStateService.PetConfiguration PetConfig = new(
        PetId: new PetId("pet-powerchanged"),
        Element: Element.Hoa,
        PassiveId: new PassiveId("passive-powerchanged"),
        PassiveThreshold: 10,
        PassiveResetOverride: null,
        EquippedRelics: null,
        EquippedCards: [new EquippedCardIdentity("card-power-charge")]);

    private static readonly CardDefinition PowerChargeCard = new()
    {
        CardDefinitionId = "card-power-charge",
        Name = "Power Charge",
        Category = CardCategory.Basic,
        PowerCost = 0,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25)),
    };

    /// <summary>
    /// A Card that both pays a non-zero cost and grants Power. No
    /// MVP-provisioned Card declares both (<c>Power Charge</c>'s cost is 0), so
    /// the fixture declares it: the composition rule is a general-contract rule,
    /// not a property of today's content.
    /// </summary>
    private static readonly CardDefinition CostAndPowerCard = new()
    {
        CardDefinitionId = "card-cost-and-power",
        Name = "Cost And Power",
        Category = CardCategory.Basic,
        PowerCost = 10,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25)),
    };

    private sealed class StubCardDefinitionLookup : ICardDefinitionLookup
    {
        private readonly Dictionary<string, CardDefinition> _definitions = new(StringComparer.Ordinal);

        public StubCardDefinitionLookup(params CardDefinition[] definitions)
        {
            foreach (var definition in definitions)
            {
                _definitions[definition.CardDefinitionId] = definition;
            }
        }

        public Task<CardDefinition?> GetDefinitionAsync(
            string cardDefinitionId,
            CancellationToken cancellationToken = default)
        {
            _definitions.TryGetValue(cardDefinitionId, out var definition);
            return Task.FromResult(definition);
        }
    }

    private static (BattleStateService Service, InMemoryBattleStateRepository Repository) NewService(
        params CardDefinition[] definitions)
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(
            repository,
            new FixedRngSeedSource(),
            battleResults: null,
            cardDefinitions: new StubCardDefinitionLookup(definitions));

        return (service, repository);
    }

    private static BattleStateService.PetConfiguration Configuration(
        EquippedCardIdentity[] equippedCards,
        IReadOnlyList<EquippedRelicIdentity>? relics = null) =>
        PetConfig with { EquippedCards = equippedCards, EquippedRelics = relics?.ToArray() };

    // =======================================================================
    // 1. Match — the Power stage's resource generation (step 13)
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_ShouldEmitPowerChangedForTheMatchGeneration_WithSourceMatch()
    {
        // GAME_RULES.md §17 step 13 / §12: the Swap's generated Power is written into
        // PetState.Power by the Power stage, which is the stage that owns the
        // mutation and therefore emits its report with source "match".
        var (service, repository) = NewService(PowerChargeCard);
        var created = await service.CreateBattleAsync(BattleId, Owner, PetConfig, BossDefinitions.HoaLong);

        var setup = created with
        {
            PetState = created.PetState with { Power = 0 },
            BoardState = PowerBoard(),
        };
        await repository.TryUpdateAsync(setup, setup.Sequence);

        // Swapping (26, 34) completes a POWER Gem match (see PowerBoard).
        var result = await service.ExecuteSwapAsync(BattleId, new SwapRequest(26, 34));

        Assert.NotNull(result);
        var committed = result!.Value.State;

        Assert.True(result.Value.Resources.Power > 0, "the Swap generated Power");

        var matchChanges = result.Value.Events
            .Where(e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Match)
            .Select(e => e.PowerChanged)
            .ToArray();

        // One Match resource generation is one authoritative mutation, so it is
        // reported exactly once — never once per cleared Gem and never duplicated.
        var matchChange = Assert.Single(matchChanges);

        Assert.Equal(result.Value.Resources.Power, matchChange.Delta);
        Assert.Equal(committed.PetState.Power, matchChange.Power);

        // The pool is the transient amount the Swap generated and the state holds the
        // amount in force; nothing absorbed this one, so the two agree and the reported
        // delta is exactly the generated pool — never a net figure folded together with
        // another stage's mutation.
        Assert.Equal(result.Value.Resources.Power, committed.PetState.Power);
        Assert.True(matchChange.Delta > 0);
    }

    [Fact]
    public async Task CommittedSwap_ShouldReportTheMatchMutationAsTheActualClampedDelta()
    {
        // GAME_RULES.md §12 makes 0–100 an invariant of PetState.Power, so a
        // generation the cap absorbs moves the value by less than the pool. The
        // event reports the change the mutation ACTUALLY applied.
        var (service, repository) = NewService(PowerChargeCard);
        var created = await service.CreateBattleAsync(BattleId, Owner, PetConfig, BossDefinitions.HoaLong);

        var setup = created with
        {
            // The cap can absorb at most 5 of the generation this board produces.
            PetState = created.PetState with { Power = 95 },
            BoardState = PowerBoard(),
        };
        await repository.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync(BattleId, new SwapRequest(26, 34));

        Assert.NotNull(result);
        var committed = result!.Value.State;

        Assert.Equal(100, committed.PetState.Power);
        Assert.True(result.Value.Resources.Power > 5, "the pool exceeded the headroom");

        var matchChange = Assert.Single(
            result.Value.Events,
            e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Match)
            .PowerChanged;

        Assert.Equal(5, matchChange.Delta);
        Assert.Equal(100, matchChange.Power);
    }

    // =======================================================================
    // 2. Card cost (step 14)
    // =======================================================================

    [Fact]
    public async Task ExecuteCardCast_ShouldEmitPowerChangedForTheCostSpend_WithSourceCard()
    {
        // GAME_RULES.md §17 step 14 / CARD_RULES.md §3 item 4: the Card stage deducts
        // the effective cost and reports its own mutation with source "card", a
        // NEGATIVE signed delta, and the resulting authoritative Power.
        var (service, repository) = NewService(CostAndPowerCard);

        var created = await service.CreateBattleAsync(
            BattleId,
            Owner,
            Configuration([new EquippedCardIdentity("card-cost-and-power")]),
            BossDefinitions.HoaLong);

        var setup = created with { PetState = created.PetState with { Power = 50 } };
        await repository.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteCardCastAsync(BattleId, "card-cost-and-power");

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);
        Assert.Equal(65, result.Value.State.PetState.Power); // 50 - 10 + 25

        var cards = result.Value.Events
            .Where(e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Card)
            .Select(e => e.PowerChanged)
            .ToArray();

        // D-8: two authoritative mutations, two events — the cost first, then the
        // effect. NOT one collapsed delta of +15.
        Assert.Equal(2, cards.Length);

        Assert.Equal(-10, cards[0].Delta);
        Assert.Equal(40, cards[0].Power);

        Assert.Equal(25, cards[1].Delta);
        Assert.Equal(65, cards[1].Power);
    }

    // =======================================================================
    // 3. Card Power Charge (step 14)
    // =======================================================================

    [Fact]
    public async Task ExecuteCardCast_PowerCharge_ShouldEmitPowerChangedWithSourceCardAndAPositiveDelta()
    {
        // D-7: "card" identifies a Card-OWNED Power mutation, not only a cost spend.
        // Power Charge's cost is 0 (CARD_RULES.md §2), so its +25 grant is the
        // cast's only Power mutation and it is reported under source "card".
        var (service, repository) = NewService(PowerChargeCard);

        var created = await service.CreateBattleAsync(
            BattleId,
            Owner,
            Configuration([new EquippedCardIdentity("card-power-charge")]),
            BossDefinitions.HoaLong);

        var setup = created with { PetState = created.PetState with { Power = 10 } };
        await repository.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteCardCastAsync(BattleId, "card-power-charge");

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);
        Assert.Equal(35, result.Value.State.PetState.Power);

        var change = Assert.Single(
            result.Value.Events,
            e => e.Type == BattleEventType.PowerChanged).PowerChanged;

        Assert.Equal(PowerChangeSource.Card, change.Source);
        Assert.Equal(25, change.Delta);
        Assert.Equal(35, change.Power);
    }

    // =======================================================================
    // 4. Relic — regression guard for the existing emission (step 11)
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_ShouldKeepTheRelicEmissionAtStepEleven_WithSourceRelic()
    {
        // RELIC_RULES.md §8.3 item 3 / §8.5: Mana Crystal's immediate +10 Power
        // grant is applied at GAME_RULES.md §17 step 11 and reported by the Relic
        // stage with source "relic". This asserts the emission was not regressed or
        // duplicated by the other stages' additions: the Relic's grant is one
        // mutation of its own, reported once, and the Match generation that follows
        // it is a SEPARATE event.
        var manaCrystal = new RelicDefinition
        {
            RelicDefinitionId = "relic-mana-crystal",
            Name = "Mana Crystal",
            Trigger = RelicResolver.OnMatchCountTrigger,
            Condition = RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 1),
            EffectDefinition = RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.Power,
                    RelicEffectValueType.Flat,
                    10,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.Immediate)),
        };

        var (service, repository) = NewService(PowerChargeCard);
        var created = await service.CreateBattleAsync(
            BattleId,
            Owner,
            Configuration(PetConfig.EquippedCards!, [new EquippedRelicIdentity("relic_slot_1")]),
            BossDefinitions.HoaLong,
            equippedRelicDefinitions: [manaCrystal]);

        var setup = created with
        {
            PetState = created.PetState with { Power = 0 },
            BoardState = PowerBoard(),
        };
        await repository.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync(BattleId, new SwapRequest(26, 34));

        Assert.NotNull(result);

        var relicChanges = result!.Value.Events
            .Where(e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Relic)
            .Select(e => e.PowerChanged)
            .ToArray();

        var relicChange = Assert.Single(relicChanges);
        Assert.Equal(10, relicChange.Delta);
        Assert.Equal(10, relicChange.Power);

        // The Relic's report precedes the Match generation's, because step 11
        // precedes step 13 (GAME_RULES.md §17) — the batch preserves that order.
        var relicIndex = IndexOf(
            result.Value.Events,
            e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Relic);

        var matchIndex = IndexOf(
            result.Value.Events,
            e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Match);

        Assert.True(relicIndex >= 0, "the Relic stage emitted its PowerChanged");
        Assert.True(matchIndex >= 0, "the Power stage emitted its PowerChanged");
        Assert.True(relicIndex < matchIndex, "step 11 precedes step 13");
    }

    // =======================================================================
    // 5. Boss Drain (step 18b)
    // =======================================================================

    /// <summary>
    /// A Boss whose Skill declares <c>BOSS_RULES.md</c> §6.3.1 item 2's Drain Power
    /// — the provisioned case is Thủy Ma's <c>drain-power</c>. The fixture runs the
    /// real definition; the charge requirement is lowered so the Skill actually
    /// fires within one Swap while every other value stays §6.3.1's.
    /// </summary>
    private static BossDefinition DrainBoss()
    {
        var thuyMa = BossDefinitions.ThuyMa;

        return thuyMa with
        {
            SkillDefinition = thuyMa.SkillDefinition with
            {
                ChargeRequirement = 1,
                CooldownTurns = 0,
            },
        };
    }

    [Fact]
    public async Task CommittedSwap_ShouldEmitPowerChangedForBossDrain_WithSourceBoss()
    {
        // D-6 / GAME_EVENTS.md §2 item 4 / SIGNALR_PROTOCOL.md §3.2.24 item 5: Boss
        // Drain Power is an authoritative PetState.Power mutation owned by the Boss
        // Response stage (GAME_RULES.md §17 step 18b), so that stage emits it with
        // source "boss". It must NOT be mapped to "relic".
        var (service, repository) = NewService(PowerChargeCard);
        var created = await service.CreateBattleAsync(
            BattleId,
            Owner,
            PetConfig,
            DrainBoss());

        var setup = created with
        {
            PetState = created.PetState with { Power = 50, HP = 5000, DEF = 1000 },
        };
        await repository.TryUpdateAsync(setup, setup.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(setup.BoardState);
        var result = await service.ExecuteSwapAsync(BattleId, pair);

        Assert.NotNull(result);
        var committed = result!.Value.State;

        var change = Assert.Single(
            result.Value.Events,
            e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Boss).PowerChanged;

        // BOSS_RULES.md §6.3.1 item 2: -20 flat. The reported delta is the change
        // the mutation applied; the resulting Power is the authoritative value.
        Assert.Equal(-20, change.Delta);
        Assert.Equal(committed.PetState.Power, change.Power);

        // No Boss-sourced mutation was mislabelled as a Relic-owned one.
        Assert.DoesNotContain(
            result.Value.Events,
            e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Relic);
    }

    [Fact]
    public async Task CommittedSwap_ShouldReportBossDrainAsTheActualClampedMutation_NotTheRequestedMagnitude()
    {
        // BOSS_RULES.md §6.3.1 item 2 / GAME_RULES.md §12: the drain's own formula
        // floors the result at 0. A 20 drain against a Power of 10 therefore removes
        // only 10, and the event must reflect the ACTUAL mutation — delta -10 at
        // power 0 — never the requested delta -20.
        var (service, repository) = NewService(PowerChargeCard);
        var created = await service.CreateBattleAsync(
            BattleId,
            Owner,
            PetConfig,
            DrainBoss());

        var setup = created with
        {
            // Power 10 is below the 20 the drain requests, so the floor binds.
            PetState = created.PetState with { Power = 10, HP = 5000, DEF = 1000 },
        };
        await repository.TryUpdateAsync(setup, setup.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(setup.BoardState);
        var result = await service.ExecuteSwapAsync(BattleId, pair);

        Assert.NotNull(result);
        Assert.Equal(0, result!.Value.State.PetState.Power);

        var change = Assert.Single(
            result.Value.Events,
            e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Boss).PowerChanged;

        Assert.Equal(-10, change.Delta);
        Assert.Equal(0, change.Power);
    }

    [Fact]
    public async Task CommittedSwap_ShouldEmitNoBossPowerChanged_WhenPowerIsAlreadyZero()
    {
        // GAME_EVENTS.md §2 item 4: "a mutation that does not occur emits nothing".
        // A drain against a Power of 0 changes nothing, so no PowerChanged is
        // emitted for it — no event is invented for a no-op.
        var (service, repository) = NewService(PowerChargeCard);
        var created = await service.CreateBattleAsync(
            BattleId,
            Owner,
            PetConfig,
            DrainBoss());

        var setup = created with
        {
            PetState = created.PetState with { Power = 0, HP = 5000, DEF = 1000 },
        };
        await repository.TryUpdateAsync(setup, setup.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(setup.BoardState);
        var result = await service.ExecuteSwapAsync(BattleId, pair);

        Assert.NotNull(result);

        // The board is an ATK board, so no POWER Gem was cleared either — neither the
        // Boss drain nor the Match generation moved the value.
        Assert.Equal(0, result!.Value.State.PetState.Power);
        Assert.DoesNotContain(
            result.Value.Events,
            e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Boss);
    }

    // =======================================================================
    // 6. Payload and ordering invariants
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_ShouldEmitPowerChangesInAuthoritativeMutationOrder()
    {
        // GAME_RULES.md §17 / GAME_EVENTS.md §2 item 4: the batch preserves the
        // resolution's order, so a Relic's step-11 grant is reported before the
        // step-13 Match generation, which is reported before the step-18b Boss
        // drain. Applying the events in delivered order reaches the committed Power.
        var manaCrystal = new RelicDefinition
        {
            RelicDefinitionId = "relic-mana-crystal",
            Name = "Mana Crystal",
            Trigger = RelicResolver.OnMatchCountTrigger,
            Condition = RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 1),
            EffectDefinition = RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.Power,
                    RelicEffectValueType.Flat,
                    10,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.Immediate)),
        };

        var (service, repository) = NewService(PowerChargeCard);
        var created = await service.CreateBattleAsync(
            BattleId,
            Owner,
            Configuration(PetConfig.EquippedCards!, [new EquippedRelicIdentity("relic_slot_1")]),
            DrainBoss(),
            equippedRelicDefinitions: [manaCrystal]);

        var setup = created with
        {
            PetState = created.PetState with { Power = 0, HP = 5000, DEF = 1000 },
            BoardState = PowerBoard(),
        };
        await repository.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync(BattleId, new SwapRequest(26, 34));

        Assert.NotNull(result);

        var changes = result!.Value.Events
            .Where(e => e.Type == BattleEventType.PowerChanged)
            .Select(e => e.PowerChanged)
            .ToArray();

        Assert.Equal(3, changes.Length);

        // Step 11 (Relic) → step 13 (Match) → step 18b (Boss).
        Assert.Equal(
            [PowerChangeSource.Relic, PowerChangeSource.Match, PowerChangeSource.Boss],
            changes.Select(c => c.Source).ToArray());

        // Each event's `power` is the running total of the deltas before it, which
        // is what "applying them in delivered order reaches the committed state"
        // (GAME_RULES.md §12, SIGNALR_PROTOCOL.md §3.2.24 item 6) means concretely.
        var running = 0;
        foreach (var change in changes)
        {
            running += change.Delta;
        }

        Assert.Equal(result.Value.State.PetState.Power, running);
        Assert.Equal(result.Value.State.PetState.Power, changes[^1].Power);
    }

    private static int IndexOf(IReadOnlyList<BattleEvent> events, Func<BattleEvent, bool> predicate)
    {
        for (var index = 0; index < events.Count; index++)
        {
            if (predicate(events[index]))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// A board where swapping (26, 34) — row 3 col 2 with row 4 col 2 — completes a
    /// 4-match of POWER Gems across row 4 (columns 0–3). Every other row is arranged
    /// so that no other Match resolves, and the two swapped cells are the only gap.
    /// </summary>
    private static BoardState PowerBoard()
    {
        var rows = new[]
        {
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            // The completing POWER Gem at column 2; every other column breaks the row.
            "PAPPPADH",
            // Row 4: POWER Gems at columns 0–1 and 3, the gap at 2.
            "PPDHHDHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH",
        };

        var cells = new GemType[BoardState.CellCount];

        for (var row = 0; row < BoardState.Rows; row++)
        {
            for (var column = 0; column < BoardState.Columns; column++)
            {
                cells[BoardState.ToIndex(row, column)] = rows[row][column] switch
                {
                    'A' => GemType.Atk,
                    'D' => GemType.Def,
                    'H' => GemType.Hp,
                    'P' => GemType.Power,
                    _ => GemType.Atk,
                };
            }
        }

        return BoardState.FromCells(cells);
    }

    /// <summary>
    /// An adjacent pair of the board whose exchange produces a §3 Match — guaranteed
    /// to exist by <c>MATCH3_RULES.md</c> §1.4, and asserted found rather than
    /// assumed.
    /// </summary>
    private static SwapRequest FindAdjacentPairThatProducesAMatch(BoardState board)
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            if (right >= 0 && MatchDetector.Detect(board.WithSwapped(index, right)).Count > 0)
            {
                return new SwapRequest(index, right);
            }

            if (down >= 0 && MatchDetector.Detect(board.WithSwapped(index, down)).Count > 0)
            {
                return new SwapRequest(index, down);
            }
        }

        throw new InvalidOperationException(
            "A generated board has at least one valid Swap (MATCH3_RULES.md §1.4).");
    }
}
