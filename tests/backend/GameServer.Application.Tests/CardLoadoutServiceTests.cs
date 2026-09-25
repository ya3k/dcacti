using GameServer.Application.Cards;
using GameServer.Domain.Cards;

namespace GameServer.Application.Tests;

/// <summary>
/// The Card loadout contract — <c>CARD_RULES.md</c> §1,
/// <c>API_CONTRACTS.md</c> §3 (TASK-028).
///
/// These tests assert the gameplay contract TASK-039 resolved: exactly 3
/// submitted Basic Cards (count), each unlocked by the requesting Player
/// (ownership), each <c>Category = Basic</c> (category), and each definition's
/// occurrence count within its own explicit <c>LoadoutCopyLimit</c> (copy
/// limit) — validated in that documented order, with the active Pet's
/// Signature Skill Card derived rather than submitted, producing a 4-entry
/// snapshot.
///
/// Every rejection maps to the one documented <c>INVALID_LOADOUT</c> outcome;
/// no distinct wire code is introduced.
///
/// All limits used here are <b>test-local explicit values</b>. No production
/// balance value is asserted or invented (<c>CARD_RULES.md</c> §1 item 5).
/// </summary>
public class CardLoadoutServiceTests
{
    private const string Owner = "player_1";
    private const string OtherPlayer = "player_2";
    private const string SignatureSkillId = "card_skill_signature";

    /// <summary>
    /// Builds a service over definitions the <see cref="Owner"/> has unlocked.
    /// Each definition supplies its own explicit <c>LoadoutCopyLimit</c> — the
    /// value under test, never a default (<c>CARD_RULES.md</c> §1 item 2).
    ///
    /// Every Pet has exactly one Signature Skill Card (<c>CARD_RULES.md</c> §4
    /// item 1), so the active Pet's <see cref="SignatureSkillId"/> definition is
    /// always present as content: a valid battle snapshot is 3 Basic + 1 Pet
    /// Skill, and a fixture without it could not produce one.
    /// </summary>
    private static CardLoadoutService CreateService(params CardDefinition[] definitions) =>
        new(new FakeCardRepository(
            definitions.Append(PetSkill(SignatureSkillId)),
            Owner));

    private static CardDefinition Basic(string id, int loadoutCopyLimit) => new()
    {
        CardDefinitionId = id,
        Name = id,
        Category = CardCategory.Basic,
        PowerCost = 0,
        EffectDefinition = $"{id}_effect",
        LoadoutCopyLimit = loadoutCopyLimit,
    };

    private static CardDefinition PetSkill(string id) => new()
    {
        CardDefinitionId = id,
        Name = id,
        Category = CardCategory.PetSkill,
        PowerCost = 0,
        EffectDefinition = $"{id}_effect",
        LoadoutCopyLimit = 1,
    };

    private static string[] SnapshotValues(CardLoadoutValidation result) =>
        result.EquippedCards.Select(identity => identity.Value).ToArray();

    // -----------------------------------------------------------------------
    // Step 1 — count: exactly 3 submitted Basic Cards
    // CARD_RULES.md §1; API_CONTRACTS.md §3 step 1
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldAcceptExactlyThreeBasicCards()
    {
        // Given a Player who has unlocked three Basic Cards,
        // When the loadout submits exactly those three,
        // Then the selection is accepted (CARD_RULES.md §1).
        var service = CreateService(Basic("A", 1), Basic("B", 1), Basic("C", 1));

        var result = await service.ValidateAsync(Owner, new[] { "A", "B", "C" }, SignatureSkillId);

        Assert.True(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.None, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldRejectTwoCards()
    {
        // Two submitted entries is not the documented "exactly 3".
        var service = CreateService(Basic("A", 1), Basic("B", 1));

        var result = await service.ValidateAsync(Owner, new[] { "A", "B" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.CountInvalid, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldRejectFourSubmittedCards()
    {
        // The loadout is exactly 3 Basic entries: a fourth is rejected even
        // when every entry is individually valid and unlocked.
        var service = CreateService(
            Basic("A", 1), Basic("B", 1), Basic("C", 1), Basic("D", 1));

        var result = await service.ValidateAsync(
            Owner, new[] { "A", "B", "C", "D" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.CountInvalid, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldRejectAnEmptySelection()
    {
        var service = CreateService(Basic("A", 1), Basic("B", 1), Basic("C", 1));

        var result = await service.ValidateAsync(Owner, Array.Empty<string>(), SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.CountInvalid, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldCountRepeatedEntriesAsEntries()
    {
        // CARD_RULES.md §1 item 1: a repeat is a legitimate entry and is never
        // collapsed. A 3-entry loadout naming one definition three times is in
        // COUNT; whether it is valid is the copy limit's question (step 4), so
        // the rejection must not be reported as a count failure.
        var service = CreateService(Basic("A", 3));

        var result = await service.ValidateAsync(Owner, new[] { "A", "A", "A" }, SignatureSkillId);

        Assert.True(result.IsValid);
    }

    // -----------------------------------------------------------------------
    // Step 2 — ownership: every entry unlocked by the requesting Player
    // CARD_RULES.md §1; API_CONTRACTS.md §3 step 2
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldRejectAnUnownedCard()
    {
        // The server establishes ownership from persistence, never from a
        // client-supplied claim (GAME_RULES.md §18, ADR-001).
        var service = CreateService(Basic("A", 1), Basic("B", 1));

        var result = await service.ValidateAsync(Owner, new[] { "A", "B", "C" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.NotUnlocked, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldRejectACardUnlockedByAnotherPlayer()
    {
        // "C" exists but is unlocked by a different Player: it must not be
        // visible as owned by the requester (ADR-001).
        var service = new CardLoadoutService(
            new FakeCardRepository(
                new[] { Basic("A", 1), Basic("B", 1), Basic("C", 1) },
                unlockOwner: Owner,
                ownedByOtherPlayer: "C"));

        var result = await service.ValidateAsync(Owner, new[] { "A", "B", "C" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.NotUnlocked, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldRejectARepeatedUnownedCard()
    {
        // Ownership is checked for EVERY entry, so a repeated unowned id is
        // still an ownership failure rather than being masked by the repeat.
        var service = CreateService(Basic("A", 3));

        var result = await service.ValidateAsync(Owner, new[] { "A", "A", "ZZ" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.NotUnlocked, result.Reason);
    }

    // -----------------------------------------------------------------------
    // Step 3 — category: every entry is Category = Basic
    // CARD_RULES.md §1 item 4; API_CONTRACTS.md §3 step 3
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldRejectASubmittedPetSkillCard()
    {
        // A PetSkill Card keeps its own composition slot as the DERIVED
        // Signature Skill and can never satisfy one of the three submitted
        // Basic slots (CARD_RULES.md §1 item 4). It is unlocked here, so the
        // rejection is the category rule — not ownership.
        var service = CreateService(Basic("A", 1), Basic("B", 1), PetSkill(SignatureSkillId));

        var result = await service.ValidateAsync(
            Owner, new[] { "A", "B", SignatureSkillId }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.NotBasic, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldRejectThreePetSkillCards()
    {
        var service = CreateService(
            PetSkill("S1"), PetSkill("S2"), PetSkill("S3"));

        var result = await service.ValidateAsync(Owner, new[] { "S1", "S2", "S3" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.NotBasic, result.Reason);
    }

    // -----------------------------------------------------------------------
    // Step 4 — copy limit: occurrences(id) <= LoadoutCopyLimit
    // CARD_RULES.md §1 items 1–2; API_CONTRACTS.md §3 step 4
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldAcceptTwoCopiesAtLimitTwo()
    {
        // CARD_RULES.md §1 item 1: [A, A, B] is valid iff limit(A) >= 2.
        var service = CreateService(Basic("A", 2), Basic("B", 1));

        var result = await service.ValidateAsync(Owner, new[] { "A", "A", "B" }, SignatureSkillId);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldRejectTwoCopiesAtLimitOne()
    {
        // A limit of 1 makes the Card loadout-unique: [A, A, B] is invalid
        // (CARD_RULES.md §1 item 1).
        var service = CreateService(Basic("A", 1), Basic("B", 1));

        var result = await service.ValidateAsync(Owner, new[] { "A", "A", "B" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.CopyLimitExceeded, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldAcceptThreeCopiesAtLimitThree()
    {
        var service = CreateService(Basic("A", 3));

        var result = await service.ValidateAsync(Owner, new[] { "A", "A", "A" }, SignatureSkillId);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldRejectThreeCopiesAtLimitTwo()
    {
        var service = CreateService(Basic("A", 2));

        var result = await service.ValidateAsync(Owner, new[] { "A", "A", "A" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.CopyLimitExceeded, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldApplyEachCardsOwnLimitIndependently()
    {
        // The limit is PER CARD DEFINITION. B's limit of 1 must not be relaxed
        // by A's limit of 2 in the same submission.
        var service = CreateService(Basic("A", 2), Basic("B", 1));

        var result = await service.ValidateAsync(Owner, new[] { "A", "B", "B" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.CopyLimitExceeded, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldRejectWhenTheStoredLimitCannotBeSatisfied()
    {
        // CARD_RULES.md §1 item 2: a missing/invalid limit is invalid
        // definition data with NO default. A stored limit of 0 can never be
        // satisfied by any submission, and must not be silently treated as 1
        // (the behaviour a substituted default would produce).
        var service = CreateService(Basic("A", 0), Basic("B", 1));

        var result = await service.ValidateAsync(Owner, new[] { "A", "B", "B" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.CopyLimitExceeded, result.Reason);
    }

    // -----------------------------------------------------------------------
    // Validation order — the documented deterministic sequence
    // count → ownership → category → copy limit (API_CONTRACTS.md §3)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldReportCountBeforeOwnership()
    {
        // A 2-entry selection containing an unowned Card fails COUNT first:
        // the earlier documented step wins.
        var service = CreateService(Basic("A", 1));

        var result = await service.ValidateAsync(Owner, new[] { "A", "UNOWNED" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.CountInvalid, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldReportOwnershipBeforeCategory()
    {
        // 3 entries where the PetSkill Card is ALSO not unlocked: ownership
        // (step 2) precedes category (step 3).
        var service = CreateService(Basic("A", 1), Basic("B", 1), PetSkill("S1"));

        var result = await service.ValidateAsync(Owner, new[] { "A", "B", "S2" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.NotUnlocked, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldReportCategoryBeforeCopyLimit()
    {
        // The submitted PetSkill Card is ALSO over its limit (submitted twice
        // with limit 1), but category (step 3) precedes the copy limit
        // (step 4).
        var service = CreateService(Basic("A", 1), PetSkill("S1"));

        var result = await service.ValidateAsync(Owner, new[] { "S1", "S1", "A" }, SignatureSkillId);

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.NotBasic, result.Reason);
    }

    // -----------------------------------------------------------------------
    // Signature Skill derivation and the 4-card snapshot
    // CARD_RULES.md §1 item 4, §4; API_CONTRACTS.md §3; GAME_STATE.md §2.3
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldDeriveTheSignatureSkillIntoAFourCardSnapshot()
    {
        // Given 3 submitted Basic Cards and a Pet whose definition names a
        // Signature Skill Card,
        // When the loadout is validated,
        // Then the snapshot is 3 Basic + 1 derived Pet Skill (CARD_RULES.md §1).
        var service = CreateService(
            Basic("A", 1), Basic("B", 1), Basic("C", 1), PetSkill(SignatureSkillId));

        var result = await service.ValidateAsync(Owner, new[] { "A", "B", "C" }, SignatureSkillId);

        Assert.True(result.IsValid);
        Assert.Equal(4, result.EquippedCards.Length);
        Assert.Equal(
            new[] { "A", "B", "C", SignatureSkillId },
            SnapshotValues(result));
    }

    [Fact]
    public async Task Validate_ShouldNotCountTheSignatureSkillAgainstABasicCopyLimit()
    {
        // CARD_RULES.md §1 item 4: the derived Signature Skill is not counted
        // against the Basic copy limit. A's limit of 1 leaves it loadout-unique
        // among the submitted Basics, and the Skill's presence must not consume
        // or constrain any Basic slot.
        var service = CreateService(
            Basic("A", 1), Basic("B", 1), Basic("C", 1), PetSkill(SignatureSkillId));

        var result = await service.ValidateAsync(Owner, new[] { "A", "B", "C" }, SignatureSkillId);

        Assert.True(result.IsValid);
        Assert.Equal(1, SnapshotValues(result).Count(id => id == "A"));
    }

    [Fact]
    public async Task Validate_ShouldPreserveRepeatedBasicIdentitiesInTheSnapshot()
    {
        // GAME_STATE.md §2.3: repeated ids are the same DEFINITION repeated,
        // not instances. Nothing is de-duplicated, and the snapshot still holds
        // exactly 4 entries.
        var service = CreateService(Basic("A", 3), PetSkill(SignatureSkillId));

        var result = await service.ValidateAsync(Owner, new[] { "A", "A", "A" }, SignatureSkillId);

        Assert.True(result.IsValid);
        Assert.Equal(
            new[] { "A", "A", "A", SignatureSkillId },
            SnapshotValues(result));
    }

    [Fact]
    public async Task Validate_ShouldRejectWhenTheSignatureSkillCardIsUnresolvable()
    {
        // The Pet names a Signature Skill Card that does not exist, so the
        // documented 3 Basic + 1 Pet Skill composition cannot be produced. The
        // battle must not be created with fewer than 4 Cards, and no substitute
        // definition may be invented (AGENTS.md §7).
        var service = CreateService(Basic("A", 1), Basic("B", 1), Basic("C", 1));

        var result = await service.ValidateAsync(Owner, new[] { "A", "B", "C" }, "card_missing");

        Assert.False(result.IsValid);
        Assert.Equal(CardLoadoutRejectionReason.SignatureSkillUnavailable, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldNotRequireTheSignatureSkillToBeUnlocked()
    {
        // The Signature Skill is DERIVED from the Pet definition, not selected
        // from the Player's unlocks, so it is resolved as content: a Player who
        // has not "unlocked" it still gets it in the battle snapshot
        // (CARD_RULES.md §1 item 4).
        var service = new CardLoadoutService(
            new FakeCardRepository(
                new[] { Basic("A", 1), Basic("B", 1), Basic("C", 1), PetSkill(SignatureSkillId) },
                unlockOwner: Owner,
                contentOnly: SignatureSkillId));

        var result = await service.ValidateAsync(Owner, new[] { "A", "B", "C" }, SignatureSkillId);

        Assert.True(result.IsValid);
        Assert.Equal(SignatureSkillId, SnapshotValues(result).Last());
    }

    // -----------------------------------------------------------------------
    // Rejection behavior — no partial snapshot
    // API_CONTRACTS.md §3: "A rejected request equips nothing"
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldCarryNoSnapshotWhenRejected()
    {
        // Every rejection path must leave nothing that could be equipped or
        // persisted as a partially valid loadout.
        var service = CreateService(Basic("A", 1), Basic("B", 1));

        var countFailure = await service.ValidateAsync(Owner, new[] { "A" }, SignatureSkillId);
        var ownershipFailure = await service.ValidateAsync(
            Owner, new[] { "A", "B", "ZZ" }, SignatureSkillId);
        var categoryFailure = await service.ValidateAsync(
            Owner, new[] { "A", "B", "ZZ" }, SignatureSkillId);

        Assert.All(
            new[] { countFailure, ownershipFailure, categoryFailure },
            result => Assert.Empty(result.EquippedCards));
    }

    [Fact]
    public async Task Validate_ShouldRejectWhenThePlayerIdIsMissing()
    {
        // Without a requesting identity, ownership cannot be established, and
        // defaulting it would be a client-authoritative claim
        // (GAME_RULES.md §18).
        var service = CreateService(Basic("A", 1), Basic("B", 1), Basic("C", 1));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ValidateAsync("  ", new[] { "A", "B", "C" }, SignatureSkillId));
    }

    [Fact]
    public async Task Validate_ShouldRejectWhenTheSignatureSkillReferenceIsMissing()
    {
        // The Pet's Signature Skill reference is required; inventing one would
        // fabricate game content (AGENTS.md §7).
        var service = CreateService(Basic("A", 1), Basic("B", 1), Basic("C", 1));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ValidateAsync(Owner, new[] { "A", "B", "C" }, ""));
    }

    /// <summary>
    /// The ownership/content read the validator depends on, backed by in-memory
    /// definitions.
    ///
    /// It deliberately returns rows in the <b>reverse</b> of any plausible
    /// definition order, so a validator that depended on the repository's row
    /// order could not pass by accident. It also returns each definition once
    /// regardless of how many times it was requested, which is the
    /// definition-lookup shape the contract requires.
    /// </summary>
    private sealed class FakeCardRepository : ICardRepository
    {
        private readonly Dictionary<string, CardDefinition> _definitions;
        private readonly HashSet<string> _unlockedByOwner;
        private readonly HashSet<string> _contentOnly;

        public FakeCardRepository(
            IEnumerable<CardDefinition> definitions,
            string unlockOwner,
            string? ownedByOtherPlayer = null,
            string? contentOnly = null)
        {
            // A fixture may legitimately mention one definition twice (the
            // active Pet's Signature Skill is appended by CreateService, and a
            // test may also supply it directly), so the last one wins rather
            // than throwing on a duplicate key.
            _definitions = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);

            foreach (var definition in definitions)
            {
                _definitions[definition.CardDefinitionId] = definition;
            }

            _unlockedByOwner = _definitions.Keys.ToHashSet(StringComparer.Ordinal);
            _contentOnly = new HashSet<string>(StringComparer.Ordinal);

            if (ownedByOtherPlayer is not null)
            {
                _unlockedByOwner.Remove(ownedByOtherPlayer);
            }

            if (contentOnly is not null)
            {
                _unlockedByOwner.Remove(contentOnly);
                _contentOnly.Add(contentOnly);
            }

            _ = unlockOwner;
        }

        public Task AddDefinitionAsync(
            CardDefinition definition,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The loadout validator never writes definition rows.");

        public Task AddUnlockAsync(
            PlayerUnlockedCard unlockedCard,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The loadout validator never writes unlock rows.");

        public Task<IReadOnlyList<CardDefinition>> ListUnlockedAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CardDefinition>>(
                _definitions.Values
                    .Where(definition => _unlockedByOwner.Contains(definition.CardDefinitionId))
                    .OrderByDescending(definition => definition.CardDefinitionId, StringComparer.Ordinal)
                    .ToList());

        public Task<IReadOnlyList<CardDefinition>> ListUnlockedDefinitionsAsync(
            string playerId,
            IReadOnlyCollection<string> cardDefinitionIds,
            CancellationToken cancellationToken = default) =>
            // Only the OWNER's unlocks are visible, and each definition at most
            // once (a definition lookup, not a positional match). Reverse
            // ordering is deliberate and is not a contract.
            Task.FromResult<IReadOnlyList<CardDefinition>>(
                _definitions.Values
                    .Where(definition => _unlockedByOwner.Contains(definition.CardDefinitionId))
                    .Where(definition => cardDefinitionIds.Contains(definition.CardDefinitionId))
                    .OrderByDescending(definition => definition.CardDefinitionId, StringComparer.Ordinal)
                    .ToList());

        public Task<CardDefinition?> GetDefinitionAsync(
            string cardDefinitionId,
            CancellationToken cancellationToken = default) =>
            // Content read: not an ownership check (CARD_RULES.md §1 item 4).
            Task.FromResult(
                _definitions.TryGetValue(cardDefinitionId, out var definition)
                    ? definition
                    : null);
    }
}
