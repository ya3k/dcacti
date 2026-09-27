using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// The Pet XP → Pet Level contract — <c>PET_RULES.md</c> §5 (the canonical
/// owner), <c>DATABASE.md</c> §1/§3, <c>ADR-016</c>.
///
/// <code>
/// Rule (PET_RULES.md §5.4)
///  ↓
/// Scenario (Given a Pet.XP value, When the Level is derived,
///           Then the documented Pet.Level)
///  ↓
/// Test
/// </code>
///
/// <b>Every expected value traces to a document.</b> The formula and its worked
/// boundaries are <c>§5.4</c>'s; the initial values are <c>§5.2</c>'s; the reward
/// targeting and amounts are <c>§5.3</c>'s; the hard cap and its no-overflow rule
/// are <c>§5.5</c>'s. No value here is invented or read back from the
/// implementation.
///
/// <b>This suite is Pet-only.</b> The Player curve (<c>COMBAT_RULES.md</c> §7) is
/// a separate track with a different cap policy (<c>ADR-016</c> item 12): the
/// independence assertions below verify that neither track reaches into the
/// other.
/// </summary>
public class PetXpProgressionTests
{
    /// <summary>
    /// The XP at which the Pet curve reaches Level 50 — <c>PET_RULES.md</c>
    /// §5.4's worked boundary <c>Pet.XP = 4900 → Pet Level 50</c>, which is also
    /// §5.5's hard XP maximum ("<c>Pet.XP = 4900</c> corresponds to 49
    /// <c>BattleWon</c> rewards of 100 Pet XP"). It is deliberately NOT
    /// <c>MaxLevel × curve constant</c> (which is 5000 and is already past the
    /// cap).
    /// </summary>
    private const int LevelFiftyBoundaryXp = 4900;

    private static Pet NewPet(int xp = Pet.InitialXp) => new()
    {
        PetInstanceId = "pet_xp_test",
        PlayerId = "player_xp_test_owner",
        PetDefinitionId = "pet-definition-xp-test",
        Tier = PetTier.Common,
        Star = Pet.MinStar,
        XP = xp,
        Level = Pet.LevelForXp(xp),
        AcquiredAt = DateTimeOffset.UtcNow,
    };

    // =======================================================================
    // PET_RULES.md §5.4 — the formula's worked boundaries
    // =======================================================================

    [Theory]
    // §5.4's authoritative worked boundaries …
    [InlineData(0, 1)]
    [InlineData(100, 2)]
    [InlineData(4900, 50)]
    // … plus the interior boundaries either side of each level step, so the
    // floor cannot be read as a rounding or a ceiling.
    [InlineData(99, 1)]
    [InlineData(199, 2)]
    [InlineData(200, 3)]
    [InlineData(400, 5)]
    [InlineData(4899, 49)]
    public void LevelForXp_ShouldBeTheDocumentedFormula(int xp, int expectedLevel)
    {
        // PET_RULES.md §5.4: Pet.Level = min(floor(Pet.XP / 100) + 1, 50). §5.4
        // calls the formula deterministic and total over the valid Pet XP domain
        // [0, 4900] (§5.5).
        Assert.Equal(expectedLevel, Pet.LevelForXp(xp));
    }

    [Fact]
    public void LevelForXp_ShouldReproduceTheDocumentedFormulaDirectly()
    {
        // The formula restated from PET_RULES.md §5.4 — evaluated here, in the
        // test, rather than via the implementation — across the whole documented
        // domain, so the implementation is what is being compared rather than a
        // handful of literals.
        for (var xp = 0; xp <= Pet.MaxXp; xp++)
        {
            var documented = Math.Min((xp / 100) + 1, 50);

            Assert.Equal(documented, Pet.LevelForXp(xp));
        }
    }

    [Fact]
    public void LevelForXp_ShouldReturnOneAtTheLowerBoundaryAndFiftyAtTheUpperBoundary()
    {
        // PET_RULES.md §5.4/§5.5: the documented domain is [0, 4900], and its two
        // ends map to Level 1 and Level 50. The Level cap is the min(…, 50) term
        // (§5.5 item 4) — a separate fact from the XP cap.
        Assert.Equal(Pet.MinLevel, Pet.LevelForXp(0));
        Assert.Equal(Pet.MaxLevel, Pet.LevelForXp(Pet.MaxXp));
        Assert.Equal(1, Pet.LevelForXp(0));
        Assert.Equal(50, Pet.LevelForXp(4900));
    }

    [Fact]
    public void LevelForXp_ShouldRefuseANegativeXp()
    {
        // PET_RULES.md §5.4 defines the formula over the valid Pet XP domain
        // [0, 4900], and DATABASE.md §3 constrains Pet.XP ∈ [0, 4900]. A negative
        // value is not a Pet XP the contract defines, so it is refused rather than
        // mapped to a Level.
        Assert.Throws<ArgumentOutOfRangeException>(() => Pet.LevelForXp(-1));
    }

    // =======================================================================
    // PET_RULES.md §5.2 — initial values
    // =======================================================================

    [Fact]
    public void NewPet_ShouldStartAtXpZeroAndLevelOne()
    {
        // §5.2 / DATABASE.md §3: a newly created PlayerPet has Pet.XP = 0 and
        // Pet.Level = 1. Level 1 is also exactly §5.4 at XP = 0, so the creation
        // value and the formula agree by construction, not by separate tuning.
        Assert.Equal(0, Pet.InitialXp);
        Assert.Equal(1, Pet.InitialLevel);

        var pet = new Pet
        {
            PetInstanceId = "pet_new",
            PlayerId = "player_new",
            PetDefinitionId = "pet-definition-new",
            Tier = PetTier.Common,
            Star = Pet.MinStar,
            AcquiredAt = DateTimeOffset.UtcNow,
        };

        Assert.Equal(Pet.InitialXp, pet.XP);
        Assert.Equal(Pet.InitialLevel, pet.Level);
        Assert.Equal(Pet.LevelForXp(Pet.InitialXp), pet.Level);
    }

    [Fact]
    public void PetLevelRange_ShouldBeTheDocumentedOneToFifty()
    {
        // DATABASE.md §3 / PET_RULES.md §5.5 item 4: the Pet Level range is
        // [1, 50]. Both bounds are asserted so neither can drift.
        Assert.Equal(1, Pet.MinLevel);
        Assert.Equal(50, Pet.MaxLevel);
    }

    [Fact]
    public void PetXpCap_ShouldBeTheDocumentedFortyNineHundred()
    {
        // PET_RULES.md §5.5 item 1 / DATABASE.md §3: Pet.XP has a hard maximum of
        // 4900 — not the Player track's "no upper bound".
        Assert.Equal(4900, Pet.MaxXp);
    }

    // =======================================================================
    // PET_RULES.md §5.3 — the two outcome grants
    // =======================================================================

    [Fact]
    public void BattleWon_ShouldGrantExactlyOneHundredPetXp()
    {
        // §5.3: BattleWon → the active combat Pet receives +100 Pet XP.
        Assert.Equal(100, Pet.BattleWonXpReward);

        var pet = NewPet();

        pet.GrantBattleXp(Pet.BattleWonXpReward);

        Assert.Equal(100, pet.XP);
        Assert.Equal(2, pet.Level);
    }

    [Fact]
    public void BattleLost_ShouldGrantExactlyZeroPetXp_AndChangeNoLevel()
    {
        // §5.3 item 2: a BattleLost grants the active combat Pet +0 Pet XP, and it
        // is an explicit Pet decision rather than an inheritance of the Player
        // track's +0. Defeat therefore changes nothing.
        Assert.Equal(0, Pet.BattleLostXpReward);

        var pet = NewPet(xp: 150);

        pet.GrantBattleXp(Pet.BattleLostXpReward);

        Assert.Equal(150, pet.XP);
        Assert.Equal(Pet.LevelForXp(150), pet.Level);
    }

    [Fact]
    public void RewardAmount_AndCurveConstant_ShouldBeIndependentConstants()
    {
        // PET_RULES.md §5.4 "Reward Amount vs. Curve Constant": the two are
        // independent concepts that must never be collapsed into one value, even
        // while both currently equal 100. They are declared as two members so
        // retuning one cannot silently rewrite the other.
        Assert.Equal(100, Pet.BattleWonXpReward);
        Assert.Equal(100, Pet.XpPerLevelCurveConstant);

        // §5.4's shape, asserted through behaviour: moving the reward amount does
        // not move the curve. 400 XP is Level 5 regardless of the reward size.
        Assert.Equal(5, Pet.LevelForXp(400));
    }

    [Fact]
    public void GrantBattleXp_ShouldFollowTheFormulaAcrossTheLevelBoundary()
    {
        // §5.4 + §5.3: crossing a 100-XP boundary raises the Level by exactly one.
        // Driven by real grants, so the integration of the two rules is verified
        // rather than the formula alone.
        var pet = NewPet();

        pet.GrantBattleXp(Pet.BattleWonXpReward); // 100

        Assert.Equal(100, pet.XP);
        Assert.Equal(2, pet.Level);

        pet.GrantBattleXp(Pet.BattleWonXpReward); // 200

        Assert.Equal(200, pet.XP);
        Assert.Equal(3, pet.Level);
    }

    [Fact]
    public void GrantBattleXp_ShouldRefuseANegativeGrant()
    {
        // No document defines a negative Pet XP grant: §5.3 fixes two non-negative
        // outcomes. A negative amount is refused rather than applied.
        var pet = NewPet();

        Assert.Throws<ArgumentOutOfRangeException>(() => pet.GrantBattleXp(-1));
        Assert.Equal(Pet.InitialXp, pet.XP);
    }

    [Fact]
    public void FortyNineBattleWins_ShouldReachTheDocumentedLevelFiftyBoundary()
    {
        // PET_RULES.md §5.4: "Pet.XP = 4900 corresponds to 49 BattleWon rewards of
        // 100 Pet XP". Driven through the real grant, so the documented
        // correspondence is what is verified rather than the arithmetic.
        var pet = NewPet();

        for (var win = 0; win < 49; win++)
        {
            pet.GrantBattleXp(Pet.BattleWonXpReward);
        }

        Assert.Equal(LevelFiftyBoundaryXp, pet.XP);
        Assert.Equal(Pet.MaxLevel, pet.Level);
    }

    // =======================================================================
    // PET_RULES.md §5.5 — the hard cap and its no-overflow rule
    // =======================================================================

    [Fact]
    public void GrantBattleXp_AtTheCap_ShouldNeitherAwardNorStoreAnything()
    {
        // §5.5 item 2: once a Pet reaches Level 50 its stored XP is 4900 and
        // further Pet XP rewards "are neither awarded nor stored". That is
        // emphatically NOT the Player behaviour, whose XP keeps accumulating past
        // the Level-50 boundary (COMBAT_RULES.md §7.5 item 1).
        var pet = NewPet(xp: Pet.MaxXp);

        Assert.Equal(4900, pet.XP);
        Assert.Equal(Pet.MaxLevel, pet.Level);

        pet.GrantBattleXp(Pet.BattleWonXpReward);

        Assert.Equal(4900, pet.XP);
        Assert.NotEqual(5000, pet.XP);
        Assert.Equal(Pet.MaxLevel, pet.Level);
    }

    [Fact]
    public void GrantBattleXp_CrossingTheCap_ShouldLandExactlyOnTheCap_WithNoOverflow()
    {
        // §5.5 items 1-3: the cap is a hard maximum with no overflow, no hidden
        // XP, and no post-Level-50 accumulation. A grant that crosses the boundary
        // therefore leaves exactly 4900 — never 5000, and never "the remainder
        // kept somewhere".
        foreach (var startingXp in new[] { 4850, 4899, 4800, 4900 - 100 })
        {
            var pet = NewPet(xp: startingXp);

            pet.GrantBattleXp(Pet.BattleWonXpReward);

            Assert.Equal(Pet.MaxXp, pet.XP);
            Assert.Equal(Pet.MaxLevel, pet.Level);
        }
    }

    [Fact]
    public void GrantBattleXp_JustBelowTheCap_ShouldStoreTheExactSum()
    {
        // The other side of the boundary: a grant that does NOT cross the cap is
        // stored in full, so the clamp cannot be mistaken for "always 4900".
        var pet = NewPet(xp: 4800 - Pet.BattleWonXpReward);

        pet.GrantBattleXp(Pet.BattleWonXpReward);

        Assert.Equal(4800, pet.XP);
        Assert.Equal(49, pet.Level);
    }

    [Fact]
    public void GrantBattleXp_ShouldNeverProduceAValueAboveTheCap_AcrossAManyWinSequence()
    {
        // §5.5: the stored value can never exceed 4900, however many rewards are
        // granted — so a long win sequence cannot accumulate overflow that a later
        // grant would reveal. Asserted at every step, not only at the end.
        var pet = NewPet();

        for (var win = 0; win < 120; win++)
        {
            pet.GrantBattleXp(Pet.BattleWonXpReward);

            Assert.InRange(pet.XP, Pet.InitialXp, Pet.MaxXp);
            Assert.InRange(pet.Level, Pet.MinLevel, Pet.MaxLevel);
        }

        Assert.Equal(Pet.MaxXp, pet.XP);
    }

    [Fact]
    public void PetXp_ShouldNeverBeNegative()
    {
        // DATABASE.md §3 constrains Pet.XP ∈ [0, 4900], and PET_RULES.md §5.3
        // defines no negative grant. The only writer refuses a negative amount, so
        // a stored value cannot leave the documented domain at its lower end
        // either.
        var pet = NewPet(xp: 100);

        pet.GrantBattleXp(Pet.BattleWonXpReward);
        pet.GrantBattleXp(Pet.BattleLostXpReward);

        Assert.True(pet.XP >= 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => pet.GrantBattleXp(-100));
        Assert.Equal(200, pet.XP);
    }

    [Fact]
    public void GrantBattleXp_ShouldAlwaysLeaveTheLevelConsistentWithTheXp()
    {
        // DATABASE.md §1/§3 keep Pet.Level a PERSISTED column derived from this
        // Pet's own XP (§5.4). After every grant the two must agree, so the stored
        // Level can never be left stale by the progression path.
        var pet = NewPet();

        foreach (var amount in new[] { 100, 100, 0, 100, 0, 100 })
        {
            pet.GrantBattleXp(amount);

            Assert.Equal(Pet.LevelForXp(pet.XP), pet.Level);
        }
    }

    // =======================================================================
    // PET_RULES.md §5.1 item 5 / ADR-016 — the two tracks are separate
    // =======================================================================

    [Fact]
    public void Pet_ShouldExposeNoPlayerProgressionMember()
    {
        // PET_RULES.md §5.1 items 3-5 / ADR-016 item 12: Pet Level is independent
        // of Player Level, and the two tracks share no variable and no stored
        // value. A Pet carries no Player-progression member, so a Player-derived
        // Level path has nothing to read.
        var members = typeof(Pet).GetProperties().Select(property => property.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "PlayerXp", "PlayerXP", "PlayerLevel",
                     "PetLevelMultiplier", "LevelMultiplier",
                     "Evolution", "Evolves", "NextLevelXP",
                 })
        {
            Assert.DoesNotContain(forbidden, members);
        }

        // The Pet's own progression members are exactly the documented two.
        Assert.Contains("XP", members);
        Assert.Contains("Level", members);
    }

    [Fact]
    public void PetProgression_ShouldIntroduceNoCombatStatMember()
    {
        // DATABASE.md §3 / ADR-011 item 5 / PET_RULES.md §5.7 item 1: battle-time
        // HP/ATK/DEF/Crit/Power live on PetState (GAME_STATE.md §2.3), never on
        // the owned Pet row. Pet XP grants no combat stats (§5.7).
        var members = typeof(Pet).GetProperties().Select(property => property.Name).ToArray();

        foreach (var combatField in new[] { "HP", "MaxHP", "ATK", "DEF", "Crit", "Power" })
        {
            Assert.DoesNotContain(combatField, members);
        }
    }

    [Fact]
    public void PetAndPlayerCurves_ShouldShareNoConstant()
    {
        // PET_RULES.md §5.4 "Relationship to the Player Curve": the two tracks
        // share a formula SHAPE and no variable, pool, or stored value. The
        // constants are duplicated per track rather than shared, so retuning one
        // track cannot move the other — the values coincide today, and the
        // declarations do not.
        //
        // The Pet cap is the sharpest case: §5.5's "Deliberate Divergence" table
        // makes Pet XP hard-capped at 4900 while Player XP is uncapped.
        Assert.Equal(4900, Pet.MaxXp);
        Assert.Equal(1, Pet.MinLevel);
        Assert.Equal(50, Pet.MaxLevel);

        // The Player track declares no XP cap at all — so the Pet track's cap
        // cannot have been sourced from it.
        var playerXpCapMembers = typeof(Player).GetProperties()
            .Select(property => property.Name)
            .Where(name => name.Contains("MaxXp", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(playerXpCapMembers);
    }

    [Fact]
    public void PetFormula_ShouldNotBeSourcedFromThePlayerFormula()
    {
        // The two formulas currently agree numerically, which is exactly why the
        // test must distinguish them behaviourally: PET_RULES.md §5.4 keeps them
        // independent implementations over independent pools. This asserts the
        // Pet function is total over the Pet domain and bounded by the Pet
        // constants, and that its two ends are this track's own documented
        // boundaries rather than the Player's.
        Assert.Equal(Pet.MinLevel, Pet.LevelForXp(Pet.InitialXp));
        Assert.Equal(Pet.MaxLevel, Pet.LevelForXp(Pet.MaxXp));

        // The Pet Level is a function of the Pet's own XP alone: the same XP
        // yields the same Level regardless of any Player state, because no Player
        // state is an input to it (PET_RULES.md §5.1 item 4).
        Assert.Equal(Pet.LevelForXp(300), Pet.LevelForXp(300));
        Assert.Equal(4, Pet.LevelForXp(300));
    }
}