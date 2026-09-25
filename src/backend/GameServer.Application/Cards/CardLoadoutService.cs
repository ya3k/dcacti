using GameServer.Domain.Cards;

namespace GameServer.Application.Cards;

/// <summary>
/// Validates a battle's Card loadout and produces the battle-scoped 4-card
/// snapshot (<c>CARD_RULES.md</c> §1; <c>API_CONTRACTS.md</c> §3).
///
/// <code>
/// cardLoadout: exactly 3 submitted Basic CardDefinitionIds
///         ↓
///   1. count        exactly 3                      API_CONTRACTS.md §3
///   2. ownership    every entry unlocked by the Player   §3
///   3. category     every entry is Category = Basic      §3
///   4. copy limit   occurrences(id) ≤ LoadoutCopyLimit   §3, CARD_RULES.md §1
///         ↓
///   derive 1 Signature Skill from PetDefinition.SignatureSkillCardId
///         ↓
///   EquippedCardIdentity[4]   = 3 Basic + 1 Pet Skill
/// </code>
///
/// <b>Server-authoritative.</b> Ownership is established from persistence via
/// <see cref="ICardRepository"/>, never from a client-supplied ownership claim
/// (<c>GAME_RULES.md</c> §18, ADR-001). A caller supplies only the requesting
/// <c>PlayerId</c>, the selected definition identities, and the active Pet's
/// Signature Skill reference; it never supplies the resulting snapshot.
///
/// <b>What this service deliberately does not do.</b> It casts no Card, spends
/// no Power, applies no effect, resolves no targeting, evaluates no cooldown,
/// and mutates no unlock row. <c>EffectDefinition</c> is data at this stage
/// (<c>CARD_RULES.md</c> §3 is a separate, unimplemented concern). It returns a
/// value; the caller copies it into <c>PetState.EquippedCards[]</c> at battle
/// start (<c>CARD_RULES.md</c> §1).
///
/// <b>It is a read-only pass.</b> Nothing here writes to the database — no
/// unlock change, no equip row — so the snapshot cannot alter the Player's
/// collection (<c>DATABASE.md</c> §2, ADR-012 item 10).
///
/// <b>No default copy limit exists.</b> The limit is read from the definition
/// itself (<c>CARD_RULES.md</c> §1 item 2: "There is no default"); a value that
/// cannot satisfy the submission rejects it rather than being substituted.
/// </summary>
public sealed class CardLoadoutService
{
    /// <summary>
    /// The exact number of Basic Cards a battle loadout submits
    /// (<c>CARD_RULES.md</c> §1: "A battle loadout always contains exactly 3
    /// Basic Cards + 1 Pet Skill Card"; <c>API_CONTRACTS.md</c> §3 step 1).
    /// </summary>
    public const int RequiredBasicCardCount = 3;

    private readonly ICardRepository _cards;

    /// <summary>
    /// Creates the loadout validator over the Card persistence boundary.
    /// </summary>
    /// <param name="cards">The Card persistence boundary (ownership source).</param>
    public CardLoadoutService(ICardRepository cards)
    {
        _cards = cards;
    }

    /// <summary>
    /// Validates <paramref name="cardDefinitionIds"/> as the Basic loadout for
    /// <paramref name="playerId"/>, derives the active Pet's Signature Skill
    /// Card from <paramref name="signatureSkillCardId"/>, and returns the
    /// 4-entry snapshot.
    ///
    /// The rules are applied in the documented order — count, ownership,
    /// category, copy limit — and the first violation is reported, so a
    /// selection failing two rules is always rejected for the earlier one.
    /// Every rejection maps to the one documented <c>INVALID_LOADOUT</c>
    /// outcome (<c>API_CONTRACTS.md</c> §3).
    /// </summary>
    /// <param name="playerId">
    /// The requesting Player — the owner every selected definition must be
    /// unlocked by.
    /// </param>
    /// <param name="cardDefinitionIds">
    /// The submitted <c>cardLoadout</c>: exactly 3 Basic Card definition
    /// identities. The Signature Skill is <b>not</b> part of this list
    /// (<c>CARD_RULES.md</c> §1 item 4).
    /// </param>
    /// <param name="signatureSkillCardId">
    /// The active Pet definition's <c>SignatureSkillCardId</c>
    /// (<c>DATABASE.md</c> §1). It is the source of the derived fourth Card and
    /// is never client-submitted.
    /// </param>
    /// <param name="cancellationToken">Cancels the ownership and definition reads.</param>
    /// <returns>
    /// A valid result carrying the 4-card snapshot, or an invalid result naming
    /// the first documented rule the selection violated.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="playerId"/> is null, empty, or whitespace. The requesting
    /// identity is required: without it ownership cannot be established, and
    /// defaulting it would be a client-authoritative claim
    /// (<c>GAME_RULES.md</c> §18).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="signatureSkillCardId"/> is null, empty, or whitespace.
    /// The Pet's Signature Skill reference is required — it is the documented
    /// source of the fourth Card, and inventing one would be fabricating game
    /// content (<c>AGENTS.md</c> §7).
    /// </exception>
    public async Task<CardLoadoutValidation> ValidateAsync(
        string playerId,
        IReadOnlyList<string> cardDefinitionIds,
        string signatureSkillCardId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signatureSkillCardId);
        ArgumentNullException.ThrowIfNull(cardDefinitionIds);

        // Step 1 — count (API_CONTRACTS.md §3 step 1, CARD_RULES.md §1):
        // exactly 3 submitted Basic Cards. The count is of ELEMENTS, so a
        // repeated definition is counted as the entry it is; it is judged by
        // the copy limit at step 4, never collapsed away here (§1 item 1).
        if (cardDefinitionIds.Count != RequiredBasicCardCount)
        {
            return CardLoadoutValidation.Invalid(
                CardLoadoutRejectionReason.CountInvalid);
        }

        // Steps 2–4 need the definitions themselves. One Player-scoped read
        // resolves both ownership and content: a definition that is not
        // unlocked by this Player is simply absent from the result, so an
        // unowned Card is never materialized for this Player at all
        // (GAME_RULES.md §18, ADR-001). The read is by DISTINCT id because it
        // is a definition lookup — repeats are resolved below, not here.
        var requestedIds = cardDefinitionIds
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var unlocked = await _cards.ListUnlockedDefinitionsAsync(
            playerId,
            requestedIds,
            cancellationToken);

        var unlockedById = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);

        foreach (var definition in unlocked)
        {
            unlockedById[definition.CardDefinitionId] = definition;
        }

        // Step 2 — ownership (API_CONTRACTS.md §3 step 2): every submitted
        // CardDefinitionId must have a PlayerUnlockedCard row for the
        // requesting Player. Checked for every entry, including repeats, so a
        // repeated unowned id is still an ownership failure.
        foreach (var cardDefinitionId in cardDefinitionIds)
        {
            if (!unlockedById.ContainsKey(cardDefinitionId))
            {
                return CardLoadoutValidation.Invalid(
                    CardLoadoutRejectionReason.NotUnlocked);
            }
        }

        // Step 3 — category (API_CONTRACTS.md §3 step 3, CARD_RULES.md §1
        // item 4): every submitted entry must be Category = Basic. A PetSkill
        // Card keeps its own composition slot as the derived Signature Skill
        // and can never satisfy one of the three submitted Basics, so
        // submitting one is a rejection and not a fulfilled Basic slot.
        foreach (var cardDefinitionId in cardDefinitionIds)
        {
            if (unlockedById[cardDefinitionId].Category != CardCategory.Basic)
            {
                return CardLoadoutValidation.Invalid(
                    CardLoadoutRejectionReason.NotBasic);
            }
        }

        // Step 4 — copy limit (API_CONTRACTS.md §3 step 4, CARD_RULES.md §1
        // items 1–2): for each submitted definition, its occurrence count in
        // this one loadout must not exceed that definition's own
        // LoadoutCopyLimit. The limit is read from the definition and no
        // default is applied — a stored value that cannot satisfy the
        // submission rejects it (§1 item 2: "There is no default").
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var cardDefinitionId in cardDefinitionIds)
        {
            occurrences.TryGetValue(cardDefinitionId, out var count);
            occurrences[cardDefinitionId] = count + 1;
        }

        foreach (var (cardDefinitionId, count) in occurrences)
        {
            if (count > unlockedById[cardDefinitionId].LoadoutCopyLimit)
            {
                return CardLoadoutValidation.Invalid(
                    CardLoadoutRejectionReason.CopyLimitExceeded);
            }
        }

        // The Signature Skill is derived, never submitted (CARD_RULES.md §1
        // item 4, API_CONTRACTS.md §3). It is resolved from the active Pet's
        // definition through the same content read: it is deliberately NOT
        // subject to the Player's unlock of the submitted Basics, because the
        // rule derives it from the Pet rather than validating it as a
        // selection — and it is never counted against any copy limit.
        var signatureSkill = await _cards.GetDefinitionAsync(
            signatureSkillCardId,
            cancellationToken);

        // A Pet whose Signature Skill Card does not resolve cannot produce the
        // documented "3 Basic + 1 Pet Skill" composition (CARD_RULES.md §1).
        // Creating a battle with fewer than 4 Cards would contradict that rule,
        // and inventing a substitute definition would fabricate content
        // (AGENTS.md §7), so the composition is rejected instead.
        if (signatureSkill is null)
        {
            return CardLoadoutValidation.Invalid(
                CardLoadoutRejectionReason.SignatureSkillUnavailable);
        }

        // The snapshot: the 3 submitted Basics in submitted order, then the
        // derived Signature Skill. Every entry is a definition identity, so a
        // repeated Basic repeats the same CardDefinitionId
        // (GAME_STATE.md §2.3). Order carries no gameplay significance, and
        // nothing is de-duplicated, sorted, or re-indexed.
        var snapshot = new EquippedCardIdentity[RequiredBasicCardCount + 1];

        for (var index = 0; index < cardDefinitionIds.Count; index++)
        {
            snapshot[index] = new EquippedCardIdentity(cardDefinitionIds[index]);
        }

        snapshot[RequiredBasicCardCount] =
            new EquippedCardIdentity(signatureSkill.CardDefinitionId);

        return CardLoadoutValidation.Valid(snapshot);
    }
}
