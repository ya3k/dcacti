using GameServer.Domain.Battle;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;

namespace GameServer.Domain.Tests;

/// <summary>
/// The <c>PetState.EquippedCards[]</c> Card-loadout member —
/// <c>GAME_STATE.md</c> §2.3, <c>CARD_RULES.md</c> §1 (TASK-028).
///
/// This verifies the member's <b>shape and representation semantics</b> only:
/// that it carries Card <b>definition</b> identities, that repeats are the same
/// definition repeated rather than instances, and that no undefined slot,
/// copy-index, or quantity concept is introduced. Card casting and effects are
/// explicitly out of scope (<c>CARD_RULES.md</c> §3).
/// </summary>
public class PetStateEquippedCardsTests
{
    private static EquippedCardIdentity[] Cards(params string[] definitionIds) =>
        definitionIds.Select(id => new EquippedCardIdentity(id)).ToArray();

    private static PetState CreatePet(EquippedCardIdentity[]? cards) =>
        PetState.AtBattleCreation(
            new PetId("pet_instance_loadout_1"),
            Element.Hoa,
            new PassiveId("passive_1"),
            passiveThreshold: 5,
            passiveResetOverride: null,
            equippedRelics: null,
            equippedCards: cards);

    // -----------------------------------------------------------------------
    // GAME_STATE.md §2.3 — the member exists and carries definition identities
    // -----------------------------------------------------------------------

    [Fact]
    public void PetState_ShouldExposeTheEquippedCardsMember()
    {
        // GAME_STATE.md §2.3 lists EquippedCards[] on PetState.
        var member = typeof(PetState).GetProperty(nameof(PetState.EquippedCards));

        Assert.NotNull(member);
        Assert.Equal(typeof(EquippedCardIdentity[]), member!.PropertyType);
    }

    [Fact]
    public void PetState_ShouldCarryTheFourCardSnapshotItWasCreatedWith()
    {
        // GAME_STATE.md §2.3 / CARD_RULES.md §1: exactly the four battle-scoped
        // cards — the 3 submitted Basic CardDefinitionIds plus the active Pet's
        // derived Signature Skill CardDefinitionId.
        var pet = CreatePet(Cards("A", "B", "C", "skill_signature"));

        Assert.NotNull(pet.EquippedCards);
        Assert.Equal(4, pet.EquippedCards!.Length);
        Assert.Equal(
            new[] { "A", "B", "C", "skill_signature" },
            pet.EquippedCards.Select(i => i.Value).ToArray());
    }

    [Fact]
    public void PetState_ShouldPreserveRepeatedDefinitionIdentities()
    {
        // GAME_STATE.md §2.3: "if the same CardDefinitionId appears more than
        // once, the repeated elements are that same definition repeated …
        // and never separate owned or persistent entities". The member must
        // therefore NEVER de-duplicate.
        var pet = CreatePet(Cards("A", "A", "A", "skill_signature"));

        Assert.Equal(4, pet.EquippedCards!.Length);
        Assert.Equal(
            new[] { "A", "A", "A", "skill_signature" },
            pet.EquippedCards.Select(i => i.Value).ToArray());
    }

    [Fact]
    public void PetState_ShouldNotDeDuplicateOrReorderTheSnapshot()
    {
        // No rule reads card array positions (GAME_STATE.md §2.3), and nothing
        // in the state sorts or filters: the snapshot is carried across exactly
        // as the validator produced it.
        var pet = CreatePet(Cards("Z", "A", "A", "skill_signature"));

        Assert.Equal(
            new[] { "Z", "A", "A", "skill_signature" },
            pet.EquippedCards!.Select(i => i.Value).ToArray());
    }

    [Fact]
    public void PetState_ShouldAllowAnAbsentCardLoadoutAtTheFoundationStage()
    {
        // EquippedCards arrives with the Card stage. A PetState created without
        // one (the pre-Card foundation stage) carries null rather than a
        // fabricated placeholder (GAME_STATE.md §0 item 5).
        var pet = CreatePet(null);

        Assert.Null(pet.EquippedCards);
    }

    // -----------------------------------------------------------------------
    // GAME_STATE.md §2.3 — no instance, slot, or quantity concept
    // -----------------------------------------------------------------------

    [Fact]
    public void EquippedCardIdentity_ShouldCarryOnlyTheDefinitionIdentity()
    {
        // ADR-012 item 9: Cards have no instances. The element therefore carries
        // a single identity and no slot index, copy index, quantity, or
        // per-instance state.
        var fields = typeof(EquippedCardIdentity)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(new[] { "Value" }, fields);
    }

    [Fact]
    public void EquippedCardIdentity_ShouldNotBeInterchangeableWithARelicIdentity()
    {
        // GAME_STATE.md §2.3 contrasts the two members: a Card element is a
        // DEFINITION identity and a Relic element is an owned INSTANCE
        // identity. Distinct types keep the two from being passed for one
        // another.
        Assert.NotEqual(typeof(EquippedCardIdentity), typeof(Relics.EquippedRelicIdentity));
        Assert.False(
            typeof(EquippedCardIdentity).IsAssignableFrom(typeof(Relics.EquippedRelicIdentity)));
    }

    [Fact]
    public void CardCategory_ShouldBeTheDocumentedClosedSet()
    {
        // CARD_RULES.md §1 / DATABASE.md §3: exactly Basic and PetSkill. No
        // third category exists in MVP.
        var members = Enum.GetNames<CardCategory>().OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(new[] { "Basic", "PetSkill" }, members);
    }
}
