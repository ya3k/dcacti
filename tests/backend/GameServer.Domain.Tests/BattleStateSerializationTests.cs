using System.Text.Json;
using GameServer.Domain.Battle;
using GameServer.Domain.Battle.Serialization;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Round-trip fidelity of the authoritative <c>BattleState</c> runtime
/// serialization mapping (<c>GAME_STATE.md</c> §2, §2.1.7 item 5;
/// <c>REDIS_STATE.md</c> §2 item 1, §7 items 9–11).
///
/// The obligation these tests discharge is stated in <c>GAME_STATE.md</c> §2.1.7
/// item 5: "Serializing a <c>BattleState</c> and deserializing it must return a
/// <c>BattleState</c> whose <c>Cells[64]</c> are identical … and whose
/// <c>Turn</c>, <c>Sequence</c>, <c>RngSeed</c>, and <c>RngState</c> are
/// unchanged."
///
/// <b>Equality is asserted member by member, not as equal JSON text.</b> A string
/// comparison would pass for a record that lost a member to a defaulted value, so
/// every serializable member is read back and compared by value, and the
/// collection members are compared by count, identity, order, and value.
///
/// <b>The fixtures use meaningful non-default values.</b> A round trip over an
/// all-defaults state would not distinguish "preserved" from "defaulted", so the
/// representative state carries non-zero counters, a populated commit record, real
/// loadout arrays, and Special Gems on the board.
///
/// No Redis client is involved: the mapping under test is a pure function, and
/// <c>REDIS_STATE.md</c> §7's deferral means nothing is written anywhere.
/// </summary>
public class BattleStateSerializationTests
{
    // =======================================================================
    // Fixtures — a representative, non-default authoritative state.
    // =======================================================================

    /// <summary>
    /// The identity of the Player who owns the representative battle
    /// (<c>GAME_STATE.md</c> §2.8) — a real, non-default value, so a round trip
    /// that defaulted the member could not pass.
    /// </summary>
    private static readonly PlayerId Owner = new("player_9f3c1d7e");

    /// <summary>
    /// The owned Pet instance the representative battle selected
    /// (<c>GAME_STATE.md</c> §2.3 — the <c>Pet.PetInstanceId</c>), likewise a real
    /// non-default value.
    /// </summary>
    private static readonly PetId OwnedPet = new("pet-instance-4b81e0c2");

    /// <summary>
    /// A board carrying every Special Gem shape, so the round trip exercises the
    /// conditional orientation member (<c>GAME_STATE.md</c> §2.1.4 item 2) as well
    /// as ordinary cells.
    /// </summary>
    private static BoardState RepresentativeBoard() =>
        TestBoard.Background().WithSpecial(
            (0, SpecialGem.LineClearHorizontal()),
            (9, SpecialGem.LineClearVertical()),
            (27, SpecialGem.Burst()),
            (63, SpecialGem.Area()));

    /// <summary>
    /// A representative <c>PetState</c> with non-default combat stats, a
    /// non-default Reset Behavior, and both battle-scoped loadouts populated.
    /// </summary>
    private static PetState RepresentativePetState() =>
        new(
            PetId: OwnedPet,
            HP: 723,
            MaxHP: 1042,
            ATK: 61,
            DEF: 17,
            Crit: 12,
            Power: 88,
            Element: Element.Thuy,
            PassiveId: new PassiveId("thanh-xa-poison"),
            PassiveProgress: new PassiveProgress(Threshold: 7, Current: 3),

            // §4 item 2's non-default behavior: present, so the member is written
            // rather than omitted.
            PassiveResetOverride: PassiveResetBehavior.Partial,

            // RELIC_RULES.md §2.3, §2.5: three owned instance identities in
            // submitted equip-slot order — deliberately NOT sorted, and including
            // two instances that reference different definitions.
            EquippedRelics:
            [
                new EquippedRelicIdentity("relic-instance-c"),
                new EquippedRelicIdentity("relic-instance-a"),
                new EquippedRelicIdentity("relic-instance-b"),
            ],

            // CARD_RULES.md §1 / GAME_STATE.md §2.3: 3 Basic + 1 derived Signature
            // Skill. The first two are the SAME definition repeated, which is the
            // documented same-definition repetition and must survive as a duplicate
            // rather than being deduplicated.
            EquippedCards:
            [
                new EquippedCardIdentity("card-heal"),
                new EquippedCardIdentity("card-heal"),
                new EquippedCardIdentity("card-shield"),
                new EquippedCardIdentity("card-thanh-xa-skill"),
            ]);

    /// <summary>
    /// A representative <c>BossState</c> in a non-Initial State with a charged
    /// Passive and a live Skill cooldown, so no member round-trips by accident of
    /// its default.
    /// </summary>
    private static BossState RepresentativeBossState() =>
        new(
            new BossId("boss-thuy-ma"),
            Element.Thuy,
            HP: 3117,
            MaxHP: 5000,
            ATK: 100,
            DEF: 50,
            State: BossStateKind.Enraged,
            PassiveId: new PassiveId("boss-thuy-ma-heal"),
            PassiveProgress: new PassiveProgress(Threshold: 5, Current: 4),
            SkillCharge: 3,
            SkillCooldown: 2);

    /// <summary>
    /// The representative authoritative state: every root member non-default, both
    /// loadouts populated, and the commit record present.
    /// </summary>
    private static BattleState RepresentativeState() =>
        new(
            BattleId: "battle-9f3c1d7e",
            PlayerId: Owner,
            Turn: 14,
            Sequence: 19,
            RngSeed: 0xFEDCBA9876543210UL,
            RngState: new RngState(State: 0x0123456789ABCDEFUL, Increment: 0x0000000000000001UL),
            BoardState: RepresentativeBoard(),
            Combo: 6,
            MatchCount: 137,
            PetState: RepresentativePetState(),
            BossState: RepresentativeBossState(),
            LastCommittedSwapPair: CommittedSwapPair.FromCells(41, 33));

    // =======================================================================
    // Root members (GAME_STATE.md §2, §2.2)
    // =======================================================================

    [Fact]
    public void RoundTrip_ShouldPreserveEveryRootMemberByValue()
    {
        // GAME_STATE.md §2: the root members are BattleId, Turn, Sequence,
        // RngSeed/RngState, BoardState, Combo, MatchCount, PetState, BossState, and
        // the optional LastCommittedSwapPair. Each is compared by value.
        var original = RepresentativeState();

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        Assert.Equal(original.BattleId, restored.BattleId);
        Assert.Equal(original.PlayerId, restored.PlayerId);
        Assert.Equal(original.Turn, restored.Turn);
        Assert.Equal(original.Sequence, restored.Sequence);
        Assert.Equal(original.RngSeed, restored.RngSeed);
        Assert.Equal(original.Combo, restored.Combo);
        Assert.Equal(original.MatchCount, restored.MatchCount);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveTheRootMatchComboAccounting()
    {
        // GAME_STATE.md §2.2 / REDIS_STATE.md §7 item 12: Combo and MatchCount are
        // BATTLE STATE ROOT members — they are not nested under any PlayerState node
        // (there is none), and they are state, not derived: neither can be
        // re-derived from the board, Turn, or Sequence after the fact.
        var original = RepresentativeState();

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        Assert.Equal(6, restored.Combo);
        Assert.Equal(137, restored.MatchCount);
        Assert.Equal(original.Combo, restored.Combo);
        Assert.Equal(original.MatchCount, restored.MatchCount);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveZeroComboAndMatchCountAsRealValues()
    {
        // GAME_STATE.md §2.2.1 item 1: "Combo = 0 is a real publishable value — the
        // value read before the battle's first committed Swap." Neither member is
        // nullable and neither is omitted, so zero must round-trip as zero rather
        // than by omission (REDIS_STATE.md §7 item 12 treats an omitted zero as a
        // lost state).
        var original = RepresentativeState() with { Combo = 0, MatchCount = 0 };

        var json = BattleStateSerializer.Serialize(original);

        // The member is written, not omitted — spelled out so omission cannot pass.
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("combo", out var combo));
        Assert.True(document.RootElement.TryGetProperty("matchCount", out var matchCount));
        Assert.Equal(0, combo.GetInt32());
        Assert.Equal(0, matchCount.GetInt32());

        var restored = BattleStateSerializer.Deserialize(json);

        Assert.Equal(0, restored.Combo);
        Assert.Equal(0, restored.MatchCount);
    }

    // =======================================================================
    // RNG (GAME_STATE.md §2.6)
    // =======================================================================

    [Fact]
    public void RoundTrip_ShouldPreserveBothRngStateComponents()
    {
        // GAME_STATE.md §2.6.2 item 1: RngState is a PAIR — internal state plus
        // stream selector — and "the two components are never split". The full
        // 64-bit range is exercised, so a narrowing conversion anywhere would fail.
        var original = RepresentativeState();

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        Assert.Equal(0xFEDCBA9876543210UL, restored.RngSeed);
        Assert.Equal(0x0123456789ABCDEFUL, restored.RngState.State);
        Assert.Equal(0x0000000000000001UL, restored.RngState.Increment);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveTheRngPairForEveryComponentValue()
    {
        // The seed and the pair are state, not derivable from each other
        // (§2.6.1 item 3, §2.6.2 item 3): the round trip must not re-seed, must not
        // advance the stream, and must not recompute the state from the seed. A pair
        // unrelated to the seed proves the values are carried rather than derived.
        var original = RepresentativeState() with
        {
            RngSeed = 1UL,
            RngState = new RngState(State: ulong.MaxValue, Increment: ulong.MaxValue - 1UL),
        };

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        Assert.Equal(1UL, restored.RngSeed);
        Assert.Equal(ulong.MaxValue, restored.RngState.State);
        Assert.Equal(ulong.MaxValue - 1UL, restored.RngState.Increment);
    }

    [Fact]
    public void Serialization_ShouldNotAdvanceTheRngOrChangeTheState()
    {
        // §2.6.2 item 3: "RngState changes only when a value is drawn." Mapping the
        // state draws nothing, so serializing twice must produce the identical
        // document and must leave the original untouched.
        var original = RepresentativeState();

        var first = BattleStateSerializer.Serialize(original);
        var second = BattleStateSerializer.Serialize(original);

        Assert.Equal(first, second);
        Assert.Equal(original, original with { });
        Assert.Equal(new RngState(0x0123456789ABCDEFUL, 0x0000000000000001UL), original.RngState);
    }

    // =======================================================================
    // Board (GAME_STATE.md §2.1)
    // =======================================================================

    [Fact]
    public void RoundTrip_ShouldPreserveEveryBoardCellInOrder()
    {
        // GAME_STATE.md §2.1.7 item 5: the restored Cells[64] must be "identical —
        // same 64 Gem types, in the same index order, with the same Special Gem type
        // and orientation at the same cells". Checked cell by cell, so a reorder or
        // a dropped Special Gem is caught at its own index.
        var original = RepresentativeState();

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        Assert.Equal(BoardState.CellCount, restored.BoardState.Cells.Count);
        Assert.True(original.BoardState.CellsEqual(restored.BoardState));

        for (var index = 0; index < BoardState.CellCount; index++)
        {
            Assert.Equal(original.BoardState[index], restored.BoardState[index]);
            Assert.Equal(original.BoardState.SpecialGemAt(index), restored.BoardState.SpecialGemAt(index));
        }
    }

    [Fact]
    public void RoundTrip_ShouldPreserveSpecialGemTypesAndOrientations()
    {
        // §2.1.4 item 2: orientation is present if and only if the type is
        // LineClear. The fixture places all four shapes, so each conditional case is
        // asserted explicitly.
        var restored = BattleStateSerializer.Deserialize(
            BattleStateSerializer.Serialize(RepresentativeState()));

        var horizontal = restored.BoardState.SpecialGemAt(0)!.Value;
        Assert.Equal(SpecialGemType.LineClear, horizontal.Type);
        Assert.Equal(SpecialGemOrientation.Horizontal, horizontal.Orientation);

        var vertical = restored.BoardState.SpecialGemAt(9)!.Value;
        Assert.Equal(SpecialGemType.LineClear, vertical.Type);
        Assert.Equal(SpecialGemOrientation.Vertical, vertical.Orientation);

        var burst = restored.BoardState.SpecialGemAt(27)!.Value;
        Assert.Equal(SpecialGemType.Burst, burst.Type);
        Assert.Null(burst.Orientation);

        var area = restored.BoardState.SpecialGemAt(63)!.Value;
        Assert.Equal(SpecialGemType.Area, area.Type);
        Assert.Null(area.Orientation);
    }

    [Fact]
    public void Serialization_ShouldWriteCellsInAscendingIndexOrderWithNoIndexMember()
    {
        // §2.1.1 item 6 / §2.1.7 item 2: the cells are written as a 64-element array
        // in ascending index order, and the array POSITION is the cell index — so no
        // element carries an index of its own.
        using var document = JsonDocument.Parse(
            BattleStateSerializer.Serialize(RepresentativeState()));

        var cells = document.RootElement.GetProperty("boardState").GetProperty("cells");

        Assert.Equal(JsonValueKind.Array, cells.ValueKind);
        Assert.Equal(BoardState.CellCount, cells.GetArrayLength());

        // The order is the board's own order, not merely the right length.
        var written = cells.EnumerateArray()
            .Select(cell => cell.GetProperty("gemType").GetString())
            .ToArray();
        var expected = RepresentativeBoard().Cells
            .Select(cell => GemTypes.ToContractName(cell.GemType))
            .ToArray();
        Assert.Equal(expected, written);

        foreach (var cell in cells.EnumerateArray())
        {
            Assert.False(cell.TryGetProperty("cellIndex", out _));
        }
    }

    [Fact]
    public void Serialization_ShouldOmitSpecialGemForOrdinaryCellsAndNeverSpellItAsARealType()
    {
        // §2.1.7 item 3: a cell with no Special Gem is serialized as a cell with no
        // Special Gem member — "not a null element, not a sentinel type, and not a
        // default value". The board here carries Special Gems only at 0, 9, 27, 63.
        using var document = JsonDocument.Parse(
            BattleStateSerializer.Serialize(RepresentativeState()));

        var cells = document.RootElement.GetProperty("boardState").GetProperty("cells")
            .EnumerateArray().ToArray();

        var withSpecials = new[] { 0, 9, 27, 63 };

        for (var index = 0; index < cells.Length; index++)
        {
            Assert.True(cells[index].TryGetProperty("gemType", out _));

            if (withSpecials.Contains(index))
            {
                Assert.True(cells[index].TryGetProperty("specialGem", out _));
                continue;
            }

            Assert.False(cells[index].TryGetProperty("specialGem", out _));
        }
    }

    [Fact]
    public void Serialization_ShouldNotIntroduceASecondBoardCollection()
    {
        // §2.1.2 item 1 / §2.1.7 item 1: BoardState has exactly one field, Cells[64].
        // A serializer writes the board with "no additional field, no parallel array,
        // and no second key" — there is no PendingSpecialGems[] and no SpecialGems[].
        using var document = JsonDocument.Parse(
            BattleStateSerializer.Serialize(RepresentativeState()));

        var board = document.RootElement.GetProperty("boardState");

        Assert.Single(board.EnumerateObject());
        Assert.True(board.TryGetProperty("cells", out _));

        foreach (var forbidden in new[] { "specialGems", "pendingSpecialGems", "cellIndexes" })
        {
            Assert.False(board.TryGetProperty(forbidden, out _));
        }
    }

    // =======================================================================
    // PetState (GAME_STATE.md §2.3) — combat, Passive, loadouts
    // =======================================================================

    [Fact]
    public void RoundTrip_ShouldPreserveEveryPetCombatStat()
    {
        // GAME_STATE.md §2.3: HP/MaxHP, ATK/DEF/Crit, and Power are the active Pet's
        // combat stats — the combat authority's own pool, not a Player's (ADR-011).
        // All six are non-default in the fixture.
        var restored = BattleStateSerializer.Deserialize(
            BattleStateSerializer.Serialize(RepresentativeState()));

        var pet = restored.PetState;

        Assert.Equal(723, pet.HP);
        Assert.Equal(1042, pet.MaxHP);
        Assert.Equal(61, pet.ATK);
        Assert.Equal(17, pet.DEF);
        Assert.Equal(12, pet.Crit);
        Assert.Equal(88, pet.Power);
    }

    [Fact]
    public void RoundTrip_ShouldPreservePetElementAndPassiveState()
    {
        // GAME_STATE.md §2.3: Element, PassiveId, PassiveProgress (the documented
        // current-vs-threshold pair), and the optional PassiveResetOverride.
        var restored = BattleStateSerializer.Deserialize(
            BattleStateSerializer.Serialize(RepresentativeState()));

        var pet = restored.PetState;

        Assert.Equal(Element.Thuy, pet.Element);
        Assert.Equal(new PassiveId("thanh-xa-poison"), pet.PassiveId);
        Assert.Equal(7, pet.PassiveProgress.Threshold);
        Assert.Equal(3, pet.PassiveProgress.Current);
        Assert.Equal(PassiveResetBehavior.Partial, pet.PassiveResetOverride);
        Assert.True(pet.HasResetOverride);
        Assert.Equal(PassiveResetBehavior.Partial, pet.ResetBehavior);
    }

    [Fact]
    public void Serialization_ShouldOmitAnAbsentPassiveResetOverride()
    {
        // §2.3 / PASSIVE_RULES.md §4: the field is "only present if this Pet's
        // Passive uses non-default reset behavior". Absence IS the documented
        // representation of Default — so no "Default" string and no explicit null is
        // written in its place.
        var original = RepresentativeState() with
        {
            PetState = RepresentativePetState() with { PassiveResetOverride = null },
        };

        var json = BattleStateSerializer.Serialize(original);

        using (var document = JsonDocument.Parse(json))
        {
            Assert.False(
                document.RootElement.GetProperty("petState")
                    .TryGetProperty("passiveResetOverride", out _));
        }

        var restored = BattleStateSerializer.Deserialize(json);

        Assert.Null(restored.PetState.PassiveResetOverride);
        Assert.Equal(PassiveResetBehavior.Default, restored.PetState.ResetBehavior);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveEquippedRelicsCountIdentityAndOrder()
    {
        // RELIC_RULES.md §2.3, §2.5 / GAME_STATE.md §2.3: the array holds one owned
        // Relic INSTANCE identity per element, in the submitted loadout order, so
        // element i is equip slot i + 1. Order is gameplay-significant and must be
        // preserved exactly — never sorted.
        var original = RepresentativeState();

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        var relics = restored.PetState.EquippedRelics;

        Assert.NotNull(relics);
        Assert.Equal(3, relics!.Length);

        // Count, identity, and order, element by element.
        Assert.Equal(new EquippedRelicIdentity("relic-instance-c"), relics[0]);
        Assert.Equal(new EquippedRelicIdentity("relic-instance-a"), relics[1]);
        Assert.Equal(new EquippedRelicIdentity("relic-instance-b"), relics[2]);

        // Stated as the order-preservation obligation itself: the restored sequence
        // equals the original sequence, and it is NOT the sorted one.
        Assert.Equal(
            original.PetState.EquippedRelics!.Select(r => r.Value),
            relics.Select(r => r.Value));
        Assert.NotEqual(
            original.PetState.EquippedRelics!.Select(r => r.Value).OrderBy(v => v, StringComparer.Ordinal),
            relics.Select(r => r.Value));
    }

    [Fact]
    public void RoundTrip_ShouldPreserveEquippedCardsIncludingARepeatedDefinition()
    {
        // CARD_RULES.md §1 / GAME_STATE.md §2.3: exactly 4 elements, each a
        // CardDefinitionId — 3 submitted Basic Cards plus the derived Signature
        // Skill. There are no Card instances (ADR-012 item 9), so a repeated id is
        // the SAME DEFINITION REPEATED and must survive as a duplicate, never
        // deduplicated.
        var original = RepresentativeState();

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        var cards = restored.PetState.EquippedCards;

        Assert.NotNull(cards);
        Assert.Equal(4, cards!.Length);

        Assert.Equal(
            ["card-heal", "card-heal", "card-shield", "card-thanh-xa-skill"],
            cards.Select(c => c.Value));

        // The duplicate is genuinely preserved rather than collapsed.
        Assert.Equal(2, cards.Count(c => c.Value == "card-heal"));
    }

    [Fact]
    public void Serialization_ShouldNotReconstructALoadoutFromAnyInventory()
    {
        // RELIC_RULES.md §2.5 / CARD_RULES.md §1 / ADR-012 item 8: the BattleState
        // snapshot is self-contained. A serializer reads the loadout from the state
        // it was given; it never re-reads player inventory, and it introduces no
        // second source for either array. The mapping therefore exposes exactly one
        // member per loadout and no lookup of any kind.
        var original = RepresentativeState();

        var json = BattleStateSerializer.Serialize(original);
        var restored = BattleStateSerializer.Deserialize(json);

        // A state whose loadouts are the ONLY source round-trips to itself — nothing
        // outside the record contributed to the result.
        Assert.Equal(
            original.PetState.EquippedRelics!.Select(r => r.Value),
            restored.PetState.EquippedRelics!.Select(r => r.Value));
        Assert.Equal(
            original.PetState.EquippedCards!.Select(c => c.Value),
            restored.PetState.EquippedCards!.Select(c => c.Value));

        using var document = JsonDocument.Parse(json);
        var pet = document.RootElement.GetProperty("petState");

        Assert.True(pet.TryGetProperty("equippedRelics", out _));
        Assert.True(pet.TryGetProperty("equippedCards", out _));

        foreach (var forbidden in new[] { "inventory", "relicDefinitions", "cardDefinitions", "ownedRelics" })
        {
            Assert.False(pet.TryGetProperty(forbidden, out _));
        }
    }

    [Fact]
    public void RoundTrip_ShouldPreserveANullLoadoutAsAbsenceRatherThanAnEmptyArray()
    {
        // The staging position: a loadout the owning stage has not supplied is null,
        // which is "not yet implemented" — NOT an empty loadout (RELIC_RULES.md §2.1
        // item 1 and CARD_RULES.md §1 define no zero-item battle). Substituting an
        // empty array would invent a battle the rules do not define.
        var original = RepresentativeState() with
        {
            PetState = RepresentativePetState() with
            {
                EquippedRelics = null,
                EquippedCards = null,
            },
        };

        var json = BattleStateSerializer.Serialize(original);

        using (var document = JsonDocument.Parse(json))
        {
            var pet = document.RootElement.GetProperty("petState");
            Assert.False(pet.TryGetProperty("equippedRelics", out _));
            Assert.False(pet.TryGetProperty("equippedCards", out _));
        }

        var restored = BattleStateSerializer.Deserialize(json);

        Assert.Null(restored.PetState.EquippedRelics);
        Assert.Null(restored.PetState.EquippedCards);
    }

    // =======================================================================
    // BossState (GAME_STATE.md §2.4)
    // =======================================================================

    [Fact]
    public void RoundTrip_ShouldPreserveEveryBossMember()
    {
        // GAME_STATE.md §2.4: BossId, Element, HP/MaxHP/ATK/DEF, State, PassiveId,
        // PassiveProgress, SkillCharge, SkillCooldown. The fixture is mid-battle —
        // damaged, Enraged, charged, and on cooldown — so no member round-trips by
        // accident of its initial value.
        var restored = BattleStateSerializer.Deserialize(
            BattleStateSerializer.Serialize(RepresentativeState()));

        var boss = restored.BossState;

        Assert.Equal(new BossId("boss-thuy-ma"), boss.BossId);
        Assert.Equal(Element.Thuy, boss.Element);
        Assert.Equal(3117, boss.HP);
        Assert.Equal(5000, boss.MaxHP);
        Assert.Equal(100, boss.ATK);
        Assert.Equal(50, boss.DEF);
        Assert.Equal(BossStateKind.Enraged, boss.State);
        Assert.Equal(new PassiveId("boss-thuy-ma-heal"), boss.PassiveId);
        Assert.Equal(5, boss.PassiveProgress.Threshold);
        Assert.Equal(4, boss.PassiveProgress.Current);
        Assert.Equal(3, boss.SkillCharge);
        Assert.Equal(2, boss.SkillCooldown);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveBossStateWithoutRecalculatingIt()
    {
        // GAME_STATE.md §2.4.3–§2.4.5 / TASK-029 §8: serialization must only
        // represent existing state — it must not derive or recalculate HP, damage,
        // or turn state, and it must not evaluate the Enrage or Skill condition. A
        // Boss whose values are inconsistent with any formula still round-trips
        // exactly, which is how "carried, not computed" is observable.
        var original = RepresentativeState() with
        {
            BossState = RepresentativeBossState() with
            {
                HP = 1,
                MaxHP = 5000,
                State = BossStateKind.Idle,
                SkillCharge = 999,
                SkillCooldown = 7,
                PassiveProgress = new PassiveProgress(Threshold: 5, Current: 900),
            },
        };

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        Assert.Equal(1, restored.BossState.HP);
        Assert.Equal(5000, restored.BossState.MaxHP);
        Assert.Equal(BossStateKind.Idle, restored.BossState.State);
        Assert.Equal(999, restored.BossState.SkillCharge);
        Assert.Equal(7, restored.BossState.SkillCooldown);
        Assert.Equal(900, restored.BossState.PassiveProgress.Current);
    }

    // =======================================================================
    // LastCommittedSwapPair — the optional member (GAME_STATE.md §2.1.10)
    // =======================================================================

    [Fact]
    public void RoundTrip_ShouldPreserveAPopulatedCommitRecordCanonically()
    {
        // §2.1.10 items 2 and 10: the pair is stored canonically as (min, max) and
        // the serialized order is itself canonical, so a round trip cannot reorder
        // or re-spell it. FromCells(41, 33) canonicalizes to (33, 41).
        var original = RepresentativeState();

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        Assert.NotNull(restored.LastCommittedSwapPair);
        Assert.Equal(33, restored.LastCommittedSwapPair!.Value.MinCellIndex);
        Assert.Equal(41, restored.LastCommittedSwapPair.Value.MaxCellIndex);

        using var document = JsonDocument.Parse(BattleStateSerializer.Serialize(original));
        var pair = document.RootElement.GetProperty("lastCommittedSwapPair");
        Assert.Equal(33, pair.GetProperty("minCellIndex").GetInt32());
        Assert.Equal(41, pair.GetProperty("maxCellIndex").GetInt32());
    }

    [Fact]
    public void RoundTrip_ShouldPreserveAnAbsentCommitRecordAsAbsence()
    {
        // §2.1.10 items 3 and 10 / REDIS_STATE.md §7 item 11: before the first
        // committed Swap the member is OMITTED — "not written as a null pair or a
        // zero pair", because a stand-in pair would record a commit the battle never
        // made. Absence must round-trip as absence.
        var original = RepresentativeState() with { LastCommittedSwapPair = null };

        var json = BattleStateSerializer.Serialize(original);

        using (var document = JsonDocument.Parse(json))
        {
            Assert.False(document.RootElement.TryGetProperty("lastCommittedSwapPair", out _));
        }

        var restored = BattleStateSerializer.Deserialize(json);

        Assert.Null(restored.LastCommittedSwapPair);
    }

    [Fact]
    public void RoundTrip_ShouldDistinguishAbsentFromPresentAndNotInventASentinel()
    {
        // §2.1.10 item 3: absence is never (0, 0), (-1, -1), or a defaulted pair.
        // Both documented states are exercised, and the absent one is shown not to
        // have acquired a sentinel.
        var absent = BattleStateSerializer.Deserialize(
            BattleStateSerializer.Serialize(RepresentativeState() with { LastCommittedSwapPair = null }));

        var present = BattleStateSerializer.Deserialize(
            BattleStateSerializer.Serialize(RepresentativeState()));

        Assert.Null(absent.LastCommittedSwapPair);
        Assert.NotNull(present.LastCommittedSwapPair);
        Assert.NotEqual(
            present.LastCommittedSwapPair!.Value,
            absent.LastCommittedSwapPair);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveTheLowestRepresentableCanonicalPair()
    {
        // §2.1.10 item 2: (0, 0) is not even a representable pair — a committed Swap
        // always names two DISTINCT cells. The lowest legal pair is (0, 1), and it
        // must round-trip as itself rather than being confused with absence.
        var original = RepresentativeState() with
        {
            LastCommittedSwapPair = CommittedSwapPair.FromCells(0, 1),
        };

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        Assert.NotNull(restored.LastCommittedSwapPair);
        Assert.Equal(0, restored.LastCommittedSwapPair!.Value.MinCellIndex);
        Assert.Equal(1, restored.LastCommittedSwapPair.Value.MaxCellIndex);
    }

    // =======================================================================
    // Document shape — member set and exclusions (GAME_STATE.md §2, §2.0.3)
    // =======================================================================

    [Fact]
    public void Serialization_ShouldWriteExactlyTheDocumentedRootMemberSet()
    {
        // GAME_STATE.md §2 is the whole contract: BattleId, Sequence, RngSeed/
        // RngState, BoardState, LastCommittedSwapPair?, Combo, MatchCount, PetState,
        // BossState, Turn — and nothing else. REDIS_STATE.md §2 item 1 requires the
        // record to match §2 "exactly — no additional Redis-only fields".
        using var document = JsonDocument.Parse(
            BattleStateSerializer.Serialize(RepresentativeState()));

        var written = document.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "battleId",
                "boardState",
                "bossState",
                "combo",
                "lastCommittedSwapPair",
                "matchCount",
                "petState",
                "playerId",
                "rngSeed",
                "rngState",
                "sequence",
                "turn",
            ],
            written);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveTheOwningPlayerIdentity()
    {
        // GAME_STATE.md §2.8 item 2 / DATABASE.md §1 note 2 / ADR-014 decision 1:
        // the owning Player's identity is a member of the state record and
        // round-trips with it, so the battle-end persistence path can source
        // BattleResult.PlayerId from the record rather than re-deriving it from a
        // session. A record that dropped or defaulted the member would not
        // round-trip — which is what the value assertions below establish, since
        // the fixture's owner is not a default.
        var original = RepresentativeState();

        var json = BattleStateSerializer.Serialize(original);

        // The member is genuinely written, and written under the serializer's own
        // explicit camelCase name — not omitted and not renamed.
        using (var document = JsonDocument.Parse(json))
        {
            Assert.True(document.RootElement.TryGetProperty("playerId", out var playerId));
            Assert.Equal("player_9f3c1d7e", playerId.GetString());
        }

        var restored = BattleStateSerializer.Deserialize(json);

        Assert.Equal(Owner, restored.PlayerId);
        Assert.Equal("player_9f3c1d7e", restored.PlayerId.Value);
        Assert.Equal(original.PlayerId, restored.PlayerId);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveTheOwnedPetInstanceIdentity()
    {
        // GAME_STATE.md §2.3 / ADR-014 decision 4 / DATABASE.md §1 note 2: the
        // PetState.PetId member IS the owned Pet instance (Pet.PetInstanceId) —
        // the value BattleResult.PetInstanceId is sourced from. It is carried in
        // the record and round-trips with it, so a later stage never has to re-read
        // the Player's collection to learn which owned Pet fought.
        //
        // The fixture value is deliberately a distinct string from the Pet
        // definition id, so a member mapped to the definition could not pass.
        var original = RepresentativeState();

        var json = BattleStateSerializer.Serialize(original);

        using (var document = JsonDocument.Parse(json))
        {
            var pet = document.RootElement.GetProperty("petState");
            Assert.True(pet.TryGetProperty("petId", out var petId));
            Assert.Equal("pet-instance-4b81e0c2", petId.GetString());

            // No second identity member is written beside it — ADR-014 decision 4
            // rejected a separate PetInstanceId member as a duplicate of a value the
            // record already owns (GAME_STATE.md §0 item 5).
            Assert.False(pet.TryGetProperty("petInstanceId", out _));
            Assert.False(pet.TryGetProperty("petDefinitionId", out _));
        }

        var restored = BattleStateSerializer.Deserialize(json);

        Assert.Equal(OwnedPet, restored.PetState.PetId);
        Assert.Equal("pet-instance-4b81e0c2", restored.PetState.PetId.Value);
        Assert.Equal(original.PetState.PetId, restored.PetState.PetId);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveBothIdentitiesAcrossRepeatedCycles()
    {
        // The identity members are state, not derived values (GAME_STATE.md §2.8
        // item 4): re-serializing a restored record must reproduce them exactly, so
        // a persisted record written twice from the same battle is identical and no
        // cycle drifts — the property a recovered battle (ADR-008) depends on for
        // the identity the result write needs.
        var restored = BattleStateSerializer.Deserialize(
            BattleStateSerializer.Serialize(RepresentativeState()));

        var second = BattleStateSerializer.Serialize(restored);
        var restoredAgain = BattleStateSerializer.Deserialize(second);

        Assert.Equal(restored.PlayerId, restoredAgain.PlayerId);
        Assert.Equal(restored.PetState.PetId, restoredAgain.PetState.PetId);
        Assert.Equal("player_9f3c1d7e", restoredAgain.PlayerId.Value);
        Assert.Equal("pet-instance-4b81e0c2", restoredAgain.PetState.PetId.Value);
    }

    [Fact]
    public void Deserialize_ShouldRejectAMissingPlayerIdRatherThanDefaultingIt()
    {
        // GAME_STATE.md §2.8 item 4: the owner identity is never re-derived and
        // must never be silently defaulted — a record without it cannot be shown to
        // have an owner, so it is rejected as the contract violation it is rather
        // than admitted with an invented (empty) identity.
        var json = BattleStateSerializer.Serialize(RepresentativeState());

        var withoutOwner = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        withoutOwner.AsObject().Remove("playerId");

        Assert.Throws<JsonException>(() =>
            BattleStateSerializer.Deserialize(withoutOwner.ToJsonString()));
    }

    [Fact]
    public void Deserialize_ShouldRejectAMissingPetIdRatherThanDefaultingIt()
    {
        // The same obligation for the Pet instance identity (GAME_STATE.md §2.3):
        // a record that lost it cannot identify which owned Pet fought, so it is
        // rejected rather than admitted with an empty instance id — which
        // BattleResult.PetInstanceId would then carry.
        var json = BattleStateSerializer.Serialize(RepresentativeState());

        var withoutPet = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        withoutPet["petState"]!.AsObject().Remove("petId");

        Assert.Throws<JsonException>(() =>
            BattleStateSerializer.Deserialize(withoutPet.ToJsonString()));
    }

    [Fact]
    public void Serialization_ShouldNotIntroduceAPlayerStateNode()
    {
        // GAME_STATE.md §2: "There is no PlayerState member." The wire label
        // `playerState` (SIGNALR_PROTOCOL.md §4.2) is a fixed protocol label for the
        // Combo/MatchCount projection — it is not a state path, and this mapping must
        // not reintroduce one (ADR-011).
        var json = BattleStateSerializer.Serialize(RepresentativeState());

        using var document = JsonDocument.Parse(json);

        Assert.False(document.RootElement.TryGetProperty("playerState", out _));
        Assert.False(
            document.RootElement.GetProperty("petState").TryGetProperty("playerState", out _));

        Assert.DoesNotContain("playerState", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialization_ShouldNotWriteAnyLifecycleOrStatusMember()
    {
        // GAME_STATE.md §2.0.3: "Status, READY, STARTING, ACTIVE, PAUSED, FINISHED,
        // WON, LOST — and any similar lifecycle enum — are NOT part of Battle State"
        // at any stage. A battle has no lifecycle state machine; outcome is an event
        // (BattleWon/BattleLost).
        var json = BattleStateSerializer.Serialize(RepresentativeState());

        using var document = JsonDocument.Parse(json);

        Assert.False(document.RootElement.TryGetProperty("status", out _));

        // No lifecycle VALUE appears either, anywhere in the document — including as
        // a Boss State (BOSS_RULES.md §1's Idle/Charging/Enraged/Stunned is the
        // Boss's own enum, not a battle lifecycle value).
        foreach (var lifecycle in new[] { "READY", "STARTING", "ACTIVE", "PAUSED", "FINISHED", "WON", "LOST" })
        {
            Assert.DoesNotContain($"\"{lifecycle}\"", json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Serialization_ShouldNotCarryClientOrTransportState()
    {
        // TASK-029 §22: no client-only presentation state, no React state, no Phaser
        // state, no SignalR connection state, and no Discord SDK state belongs in the
        // authoritative record. The mapping's member set is the state's own, so none
        // of those concepts has a member to be written into.
        using var document = JsonDocument.Parse(
            BattleStateSerializer.Serialize(RepresentativeState()));

        var names = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "clientSequence", "connectionId", "serverTime", "animation", "camera",
                     "discordUser", "sessionToken", "events", "serverSequence",
                 })
        {
            Assert.DoesNotContain(forbidden, names);
        }
    }

    [Fact]
    public void Serialization_ShouldNotWriteStatusEffectsThatAreNotYetImplemented()
    {
        // GAME_STATE.md §2.3 / §2.4: StatusEffects[] belongs to the Status Effects
        // system and is NOT yet implemented. §0 item 5 forbids representing it by a
        // placeholder, so the mapping must not write an empty collection in its
        // place on either PetState or BossState.
        using var document = JsonDocument.Parse(
            BattleStateSerializer.Serialize(RepresentativeState()));

        Assert.False(
            document.RootElement.GetProperty("petState").TryGetProperty("statusEffects", out _));
        Assert.False(
            document.RootElement.GetProperty("bossState").TryGetProperty("statusEffects", out _));
    }

    [Fact]
    public void Serialization_ShouldWriteEveryMemberUnderItsExplicitCamelCaseName()
    {
        // GAME_STATE.md §2.1.7 item 4 leaves the member names to the serializer, and
        // the mapping fixes camelCase explicitly rather than relying on a naming
        // policy. The authoritative DOMAIN names are unchanged — only the JSON
        // representation is lower-camel.
        using var document = JsonDocument.Parse(
            BattleStateSerializer.Serialize(RepresentativeState()));

        var root = document.RootElement;

        // Spot-check one member per nested object, so a policy change that silently
        // renamed a persisted member would fail here.
        Assert.True(root.TryGetProperty("rngState", out var rngState));
        Assert.True(rngState.TryGetProperty("state", out _));
        Assert.True(rngState.TryGetProperty("increment", out _));

        Assert.True(root.GetProperty("boardState").TryGetProperty("cells", out var cells));
        Assert.True(cells[0].TryGetProperty("gemType", out _));
        Assert.True(cells[0].TryGetProperty("specialGem", out var specialGem));
        Assert.True(specialGem.TryGetProperty("type", out _));
        Assert.True(specialGem.TryGetProperty("orientation", out _));

        Assert.True(root.GetProperty("petState").TryGetProperty("maxHp", out _));
        Assert.True(root.GetProperty("petState").TryGetProperty("passiveProgress", out var progress));
        Assert.True(progress.TryGetProperty("threshold", out _));
        Assert.True(progress.TryGetProperty("current", out _));

        Assert.True(root.GetProperty("bossState").TryGetProperty("skillCharge", out _));
        Assert.True(root.GetProperty("bossState").TryGetProperty("skillCooldown", out _));
    }

    [Fact]
    public void Serialization_ShouldWriteEnumsByNameRatherThanOrdinal()
    {
        // Enum-valued members are written by their names, so a stored record does not
        // depend on enum member ordering: the ordinals are an implementation detail no
        // document fixes, and a value written as `3` would silently change meaning if
        // a member were ever added ahead of it.
        using var document = JsonDocument.Parse(
            BattleStateSerializer.Serialize(RepresentativeState()));

        var pet = document.RootElement.GetProperty("petState");
        Assert.Equal("Thuy", pet.GetProperty("element").GetString());
        Assert.Equal("Partial", pet.GetProperty("passiveResetOverride").GetString());

        var boss = document.RootElement.GetProperty("bossState");
        Assert.Equal("Thuy", boss.GetProperty("element").GetString());
        Assert.Equal("Enraged", boss.GetProperty("state").GetString());

        // The board's Gem types use the documented contract names (MATCH3_RULES.md
        // §1.1: ATK, DEF, HP, POWER), not enum identifiers or ordinals.
        var cells = document.RootElement.GetProperty("boardState").GetProperty("cells");
        foreach (var cell in cells.EnumerateArray())
        {
            var gemType = cell.GetProperty("gemType").GetString();
            Assert.Contains(gemType, new[] { "ATK", "DEF", "HP", "POWER" });
        }
    }

    // =======================================================================
    // Determinism and reconstruction
    // =======================================================================

    [Fact]
    public void RoundTrip_ShouldBeStableAcrossRepeatedCycles()
    {
        // The mapping is deterministic: re-serializing a restored state produces the
        // identical document, so a persisted record written twice from the same state
        // is byte-for-byte the same and no cycle drifts.
        var original = RepresentativeState();

        var first = BattleStateSerializer.Serialize(original);
        var restored = BattleStateSerializer.Deserialize(first);
        var second = BattleStateSerializer.Serialize(restored);
        var restoredAgain = BattleStateSerializer.Deserialize(second);

        Assert.Equal(first, second);
        Assert.True(restored.BoardState.CellsEqual(restoredAgain.BoardState));
        Assert.Equal(restored.Turn, restoredAgain.Turn);
        Assert.Equal(restored.Sequence, restoredAgain.Sequence);
        Assert.Equal(restored.Combo, restoredAgain.Combo);
        Assert.Equal(restored.MatchCount, restoredAgain.MatchCount);
        Assert.Equal(restored.RngState, restoredAgain.RngState);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveAResolvedBoardWithoutRegeneratingIt()
    {
        // GAME_STATE.md §2.7.2 item 4 / TASK-029 §9: serialization must not generate,
        // regenerate, normalize, or mutate the board. A board that a real board
        // resolution produced is preserved exactly, which is the property a recovered
        // battle depends on (ADR-008).
        var board = RepresentativeBoard()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Burst()));

        var resolution = CascadeResolver.Resolve(board, Pcg32.FromSeed(31337UL), swapOriginIndex: null);

        var original = RepresentativeState() with
        {
            BoardState = resolution.Board,
            RngState = resolution.RngState,
        };

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        Assert.True(resolution.Board.CellsEqual(restored.BoardState));
        Assert.Equal(resolution.RngState, restored.RngState);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveABattleAtItsDocumentedInitialValues()
    {
        // The other end of the range: a freshly created battle. GAME_STATE.md §2.0.2
        // fixes Turn = 0 and Sequence = 0, §2.2 makes Combo/MatchCount 0, and
        // §2.1.10 item 3 makes the commit record absent. A created battle must
        // therefore round-trip with those documented values intact — they are state,
        // not omissions.
        var original = BattleState.CreateWith("battle-initial", 4242UL);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        Assert.Equal(BattleState.InitialTurn, restored.Turn);
        Assert.Equal(BattleState.InitialSequence, restored.Sequence);
        Assert.Equal(BattleState.InitialCombo, restored.Combo);
        Assert.Equal(BattleState.InitialMatchCount, restored.MatchCount);
        Assert.Null(restored.LastCommittedSwapPair);
        Assert.Equal(original.RngSeed, restored.RngSeed);
        Assert.Equal(original.RngState, restored.RngState);
        Assert.True(original.BoardState.CellsEqual(restored.BoardState));
        Assert.Equal(original.PetState, restored.PetState);
        Assert.Equal(original.BossState, restored.BossState);
    }

    [Fact]
    public void RoundTrip_ShouldRestoreTheValueMembersEqualByValueSemantics()
    {
        // BossState, RngState, and the commit record are value types whose members
        // are all themselves value-typed, so their generated record equality IS the
        // documented per-member comparison and can be asserted directly.
        var original = RepresentativeState();

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        Assert.Equal(original.BossState, restored.BossState);
        Assert.Equal(original.RngState, restored.RngState);
        Assert.Equal(original.LastCommittedSwapPair, restored.LastCommittedSwapPair);
        Assert.True(original.BoardState.CellsEqual(restored.BoardState));
    }

    [Fact]
    public void RoundTrip_ShouldRestorePetStateMemberByMember()
    {
        // PetState is NOT compared with record equality here, deliberately. It is a
        // `record struct` carrying two ARRAY members (EquippedRelics/EquippedCards),
        // and the generated equality compares those arrays by REFERENCE — so two
        // PetStates with identical contents but different array instances compare
        // unequal. Asserting `Assert.Equal(original.PetState, restored.PetState)`
        // would therefore fail on a correct round trip, and passing it would require
        // the serializer to alias the caller's arrays — the opposite of the
        // self-contained snapshot the contract requires.
        //
        // The documented obligation is per-member equality with the collections
        // verified by count, identity, order, and values (GAME_STATE.md §2.1.7
        // item 5, §2.3; REDIS_STATE.md §7 item 9). That is what is asserted, so the
        // comparison is exact rather than approximate.
        var original = RepresentativeState();

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        var before = original.PetState;
        var after = restored.PetState;

        Assert.Equal(before.PetId, after.PetId);
        Assert.Equal(before.HP, after.HP);
        Assert.Equal(before.MaxHP, after.MaxHP);
        Assert.Equal(before.ATK, after.ATK);
        Assert.Equal(before.DEF, after.DEF);
        Assert.Equal(before.Crit, after.Crit);
        Assert.Equal(before.Power, after.Power);
        Assert.Equal(before.Element, after.Element);
        Assert.Equal(before.PassiveId, after.PassiveId);
        Assert.Equal(before.PassiveProgress, after.PassiveProgress);
        Assert.Equal(before.PassiveResetOverride, after.PassiveResetOverride);
        Assert.Equal(before.ResetBehavior, after.ResetBehavior);
        Assert.Equal(before.HasResetOverride, after.HasResetOverride);

        // Collections: count, identity, order, and values — not reference identity.
        Assert.NotNull(after.EquippedRelics);
        Assert.Equal(before.EquippedRelics!.Length, after.EquippedRelics!.Length);
        Assert.Equal(
            before.EquippedRelics.Select(r => r.Value),
            after.EquippedRelics.Select(r => r.Value));

        Assert.NotNull(after.EquippedCards);
        Assert.Equal(before.EquippedCards!.Length, after.EquippedCards!.Length);
        Assert.Equal(
            before.EquippedCards.Select(c => c.Value),
            after.EquippedCards.Select(c => c.Value));
    }

    [Fact]
    public void RoundTrip_ShouldNotAliasTheOriginalLoadoutArrays()
    {
        // The snapshot must be self-contained (RELIC_RULES.md §2.5, ADR-012 item 8):
        // the restored state holds its OWN arrays, not the caller's. This is the
        // property that makes a deserialized record independent of the object graph it
        // came from — and it is also why PetState's reference-based array equality is
        // not the right instrument for asserting round-trip fidelity.
        var original = RepresentativeState();

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

        Assert.NotSame(original.PetState.EquippedRelics, restored.PetState.EquippedRelics);
        Assert.NotSame(original.PetState.EquippedCards, restored.PetState.EquippedCards);

        // The bytes are still equal, which is the obligation that matters.
        Assert.Equal(
            original.PetState.EquippedRelics!.Select(r => r.Value),
            restored.PetState.EquippedRelics!.Select(r => r.Value));
    }

    // =======================================================================
    // Contract violations are rejected, not repaired
    // =======================================================================

    [Fact]
    public void Deserialize_ShouldRejectABoardWithoutExactly64Cells()
    {
        // MATCH3_RULES.md §1.0 / GAME_STATE.md §2.1.1 item 1: a board holds exactly
        // 64 cells. A short board is a contract violation, so it is rejected rather
        // than padded into a state the battle never had.
        var json = BattleStateSerializer.Serialize(RepresentativeState());

        var document = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        var cells = document["boardState"]!["cells"]!.AsArray();

        // Drop the last cell, leaving a 63-entry board.
        cells.RemoveAt(cells.Count - 1);

        Assert.Equal(63, cells.Count);
        Assert.Throws<ArgumentException>(() =>
            BattleStateSerializer.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void Deserialize_ShouldRejectAnUnknownGemTypeName()
    {
        // MATCH3_RULES.md §1.1: there are exactly four Gem types. An unknown name is
        // not one of them, so it is rejected rather than coerced to a default.
        var json = BattleStateSerializer.Serialize(RepresentativeState())
            .Replace("\"gemType\":\"ATK\"", "\"gemType\":\"NOT_A_GEM\"", StringComparison.Ordinal);

        Assert.Throws<ArgumentException>(() => BattleStateSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_ShouldRejectAMalformedCommitRecordRatherThanNormalizingIt()
    {
        // GAME_STATE.md §2.1.10 item 2 / MATCH3_RULES.md §2.1.3 item 2: the pair is
        // stored canonically with MinCellIndex < MaxCellIndex. A reversing record is
        // rejected rather than silently normalized — the constructor enforces the
        // invariant instead of repairing it.
        var json = BattleStateSerializer.Serialize(RepresentativeState());

        var reordered = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        var pair = reordered["lastCommittedSwapPair"]!;
        var min = pair["minCellIndex"]!.GetValue<int>();
        var max = pair["maxCellIndex"]!.GetValue<int>();
        pair["minCellIndex"] = max;
        pair["maxCellIndex"] = min;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BattleStateSerializer.Deserialize(reordered.ToJsonString()));
    }

    [Fact]
    public void Deserialize_ShouldRejectAMissingRequiredMemberRatherThanDefaultingIt()
    {
        // A missing required member is an error, not a zero: silently substituting a
        // default would invent state the record never held.
        var json = BattleStateSerializer.Serialize(RepresentativeState());

        var withoutSequence = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        withoutSequence.AsObject().Remove("sequence");

        Assert.Throws<JsonException>(() =>
            BattleStateSerializer.Deserialize(withoutSequence.ToJsonString()));
    }

    [Fact]
    public void Serialize_ShouldRejectANullState()
    {
        Assert.Throws<ArgumentNullException>(() => BattleStateSerializer.Serialize(null!));
    }

    [Fact]
    public void Deserialize_ShouldRejectNullJson()
    {
        Assert.Throws<ArgumentNullException>(() => BattleStateSerializer.Deserialize(null!));
    }
}
