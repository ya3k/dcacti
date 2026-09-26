using GameServer.Domain.Battle;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;

namespace GameServer.Domain.Tests;

/// <summary>
/// The <c>PetState.EquippedRelics[]</c> Relic-loadout member —
/// <c>GAME_STATE.md</c> §2.3, <c>RELIC_RULES.md</c> §2.2, §2.3, §2.5
/// (TASK-027).
///
/// This verifies the member's <b>shape and order semantics</b> only: that it
/// carries owned Relic instance identities, preserves the submitted order as
/// equip-slot order, and introduces no trigger, effect, or stacking state.
/// The Relic trigger engine is explicitly out of scope
/// (<c>RELIC_RULES.md</c> §4–§5).
/// </summary>
public class PetStateEquippedRelicsTests
{
    private static EquippedRelicIdentity[] Relics(params string[] instanceIds) =>
        instanceIds.Select(id => new EquippedRelicIdentity(id)).ToArray();

    private static PetState CreatePet(EquippedRelicIdentity[]? relics) =>
        PetState.AtBattleCreation(
            new PetId("pet_instance_loadout_1"),
            Element.Hoa,
            new PassiveId("passive_1"),
            passiveThreshold: 5,
            passiveResetOverride: null,
            equippedRelics: relics);

    // -----------------------------------------------------------------------
    // GAME_STATE.md §2.3 — the member exists and carries identities
    // -----------------------------------------------------------------------

    [Fact]
    public void PetState_ShouldExposeTheEquippedRelicsMember()
    {
        // GAME_STATE.md §2.3 lists EquippedRelics[] on PetState.
        var member = typeof(PetState).GetProperty(nameof(PetState.EquippedRelics));

        Assert.NotNull(member);
        Assert.Equal(typeof(EquippedRelicIdentity[]), member!.PropertyType);
    }

    [Fact]
    public void PetState_ShouldCarryTheSnapshotItWasCreatedWith()
    {
        var pet = CreatePet(Relics("R1", "R2", "R3"));

        Assert.NotNull(pet.EquippedRelics);
        Assert.Equal(
            new[] { "R1", "R2", "R3" },
            pet.EquippedRelics!.Select(i => i.Value).ToArray());
    }

    [Fact]
    public void PetState_ShouldPreserveSubmittedOrderExactly()
    {
        // RELIC_RULES.md §2.3, §2.5: slot index = submitted position + 1, so
        // element i is slot i + 1. The array is carried across unchanged — it
        // is never sorted.
        var pet = CreatePet(Relics("R7", "R2", "R9"));

        Assert.Equal("R7", pet.EquippedRelics![0].Value);
        Assert.Equal("R2", pet.EquippedRelics[1].Value);
        Assert.Equal("R9", pet.EquippedRelics[2].Value);
    }

    [Fact]
    public void PetState_ShouldNotSortRelicsWhenCreated()
    {
        // RELIC_RULES.md §2.3 item 2 forbids ordering by RelicInstanceId,
        // RelicDefinitionId, AcquiredAt, or database order. A descending
        // selection must stay descending.
        var pet = CreatePet(Relics("R9", "R5", "R1"));

        Assert.Equal(
            new[] { "R9", "R5", "R1" },
            pet.EquippedRelics!.Select(i => i.Value).ToArray());
    }

    [Fact]
    public void PetState_ShouldPreserveOrderForAFiveRelicLoadout()
    {
        var pet = CreatePet(Relics("R5", "R4", "R3", "R2", "R1"));

        Assert.Equal(
            new[] { "R5", "R4", "R3", "R2", "R1" },
            pet.EquippedRelics!.Select(i => i.Value).ToArray());
    }

    // -----------------------------------------------------------------------
    // RELIC_RULES.md §2.2 — identity, not definition
    // -----------------------------------------------------------------------

    [Fact]
    public void EquippedRelicIdentity_ShouldBeAnOpaqueIdentityWrapper()
    {
        // RELIC_RULES.md §2.2: each element is one owned Relic INSTANCE
        // identity. The type carries the identifier only — no Trigger,
        // Condition, or Effect, and no RelicDefinitionId.
        var members = typeof(EquippedRelicIdentity).GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(new[] { "Value" }, members);
    }

    [Fact]
    public void EquippedRelicIdentity_ShouldRenderItsIdentity()
    {
        Assert.Equal("relic_1", new EquippedRelicIdentity("relic_1").ToString());
    }

    // -----------------------------------------------------------------------
    // RELIC_RULES.md §4–§5 — no trigger/effect state is introduced
    // -----------------------------------------------------------------------

    [Fact]
    public void PetState_ShouldExposeNoRelicTriggerOrEffectState()
    {
        // TASK-027 Scope: no Relic trigger engine, no effect resolution, no
        // stacking. The snapshot is identity-only, so no such member exists.
        var members = typeof(PetState).GetProperties().Select(p => p.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "RelicTriggers", "RelicEffects", "RelicStacks", "RelicStack",
                     "RelicCooldown", "RelicTriggerState", "RelicDefinitions",
                     "EquippedRelicDefinitions", "RelicConditions",
                 })
        {
            Assert.DoesNotContain(forbidden, members);
        }
    }

    // -----------------------------------------------------------------------
    // GAME_STATE.md §0 item 4 — the staging position
    // -----------------------------------------------------------------------

    [Fact]
    public void PetState_ShouldAllowAnAbsentSnapshotBeforeTheRelicStageSuppliesIt()
    {
        // §0 item 4: a field absent from a stage is "not yet implemented", not
        // "not required". A caller that has no validated loadout supplies
        // none — which is not an empty (zero-Relic) loadout, a shape the rules
        // do not define (RELIC_RULES.md §2.1 item 1 requires 3-5).
        var pet = CreatePet(relics: null);

        Assert.Null(pet.EquippedRelics);
    }

    [Fact]
    public void DefaultPetState_ShouldNotFabricateAnEmptyLoadout()
    {
        // The record's default for this member is null, not an empty array: an
        // empty array would read as a real, publishable "no Relics equipped"
        // state, which no rule defines.
        var pet = default(PetState);

        Assert.Null(pet.EquippedRelics);
    }
}
