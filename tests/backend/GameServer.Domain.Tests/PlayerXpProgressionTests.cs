using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// The Player XP → Player Level contract — <c>COMBAT_RULES.md</c> §7 (the
/// canonical owner), <c>DATABASE.md</c> §1/§3, <c>ADR-016</c> items 4–8.
///
/// <code>
/// Rule (COMBAT_RULES.md §7.4)
///  ↓
/// Scenario (Given a Player.XP value, When the Level is derived,
///           Then the documented Player.Level)
///  ↓
/// Test
/// </code>
///
/// <b>Every expected value traces to a document.</b> The formula and its worked
/// boundaries are <c>§7.4</c>'s; the uncapped-XP / capped-Level split is
/// <c>§7.5</c> items 1–2; the initial values are item 3; the defeat behaviour is
/// item 4. No value here is invented or read back from the implementation.
///
/// <b>This suite is Player-only.</b> The Pet curve (<c>PET_RULES.md</c> §5.4) is
/// a separate track with a different cap policy (<c>ADR-016</c> item 12) and is
/// not implemented or asserted here.
/// </summary>
public class PlayerXpProgressionTests
{
    /// <summary>
    /// The XP at which the Player curve first reaches Level 50 —
    /// <c>COMBAT_RULES.md</c> §7.4's worked boundary <c>Player.XP = 4900 →
    /// Level 50</c>. It is the curve's own boundary and is deliberately NOT
    /// <c>MaxLevel × curve constant</c> (which is 5000 and is already past it),
    /// so a test cannot accidentally assert a post-cap value as the boundary.
    /// </summary>
    private const int LevelFiftyBoundaryXp = 4900;

    private static Player NewPlayer(int xp = Player.InitialXp) => new()
    {
        PlayerId = "player_xp_test",
        DiscordUserId = "123456789012345678",
        XP = xp,
        Level = Player.LevelForXp(xp),
    };

    // =======================================================================
    // COMBAT_RULES.md §7.4 — the formula's worked boundaries
    // =======================================================================

    [Theory]
    // The authoritative boundary table of §7.4 …
    [InlineData(0, 1)]
    [InlineData(100, 2)]
    [InlineData(400, 5)]
    [InlineData(4900, 50)]
    [InlineData(5000, 50)]
    [InlineData(10000, 50)]
    // … plus the interior boundaries either side of each level step, so the
    // floor cannot be read as a rounding or a ceiling.
    [InlineData(99, 1)]
    [InlineData(199, 2)]
    [InlineData(200, 3)]
    [InlineData(4899, 49)]
    [InlineData(4901, 50)]
    public void LevelForXp_ShouldBeTheDocumentedFormula(int xp, int expectedLevel)
    {
        // COMBAT_RULES.md §7.4: Player.Level = min(floor(Player.XP / 100) + 1, 50).
        // §7.4 calls the formula deterministic and total: every non-negative
        // integer XP yields exactly one Level.
        Assert.Equal(expectedLevel, Player.LevelForXp(xp));
    }

    [Fact]
    public void LevelForXp_ShouldReproduceTheDocumentedFormulaDirectly()
    {
        // The formula restated from COMBAT_RULES.md §7.4 — evaluated here, in the
        // test, rather than via the implementation — over a wide sweep so the
        // implementation is what is being compared, not a handful of literals.
        for (var xp = 0; xp <= 20_000; xp++)
        {
            var documented = Math.Min((xp / 100) + 1, 50);

            Assert.Equal(documented, Player.LevelForXp(xp));
        }
    }

    [Fact]
    public void LevelForXp_ShouldRefuseANegativeXp()
    {
        // COMBAT_RULES.md §7.4 defines the formula over the non-negative
        // integers, and DATABASE.md §3 constrains Player.XP >= 0. A negative
        // value is not a Player XP the contract defines, so it is refused rather
        // than mapped to a Level.
        Assert.Throws<ArgumentOutOfRangeException>(() => Player.LevelForXp(-1));
    }

    // =======================================================================
    // COMBAT_RULES.md §7.5 items 1–2 — XP uncapped, Level capped
    // =======================================================================

    [Fact]
    public void PlayerXp_ShouldBeUncapped_WhileLevelIsCapped()
    {
        // §7.5 item 1: XP is cumulative with no ceiling, no reset, and no
        // discard at the cap — it keeps accumulating after Level 50. Item 2: the
        // min(…, 50) term is the whole Level cap.
        var player = NewPlayer();

        player.GrantBattleXp(Player.BattleWonXpReward * 500); // 50 000 XP

        Assert.Equal(50_000, player.XP);
        Assert.Equal(Player.MaxLevel, player.Level);

        // §7.5 item 1 explicitly: no XP is discarded at the cap — a further win
        // keeps growing XP while the Level holds.
        player.GrantBattleXp(Player.BattleWonXpReward);

        Assert.Equal(50_100, player.XP);
        Assert.Equal(Player.MaxLevel, player.Level);
    }

    [Fact]
    public void LevelForXp_ShouldNotClampXp_SinceItOnlyReturnsALevel()
    {
        // §7.5 item 1: the Player XP is never clamped. The Level function returns
        // a Level and has no XP output, so it cannot be the thing that clamps —
        // and the grants above prove the stored XP is not clamped either. The
        // Pet track's 4900 hard cap (PET_RULES.md §5.5) is a different policy and
        // appears nowhere in the Player contract.
        var player = NewPlayer(LevelFiftyBoundaryXp);

        Assert.Equal(4900, player.XP);
        Assert.Equal(Player.MaxLevel, player.Level);

        // Exactly one point past the documented Level-50 boundary: still Level
        // 50, and the XP is retained in full.
        player.GrantBattleXp(1);

        Assert.Equal(4901, player.XP);
        Assert.Equal(Player.MaxLevel, player.Level);
    }

    // =======================================================================
    // COMBAT_RULES.md §7.5 item 3 — initial values
    // =======================================================================

    [Fact]
    public void NewPlayer_ShouldStartAtXpZeroAndLevelOne()
    {
        // §7.5 item 3 / DATABASE.md §3: a newly created Player has XP = 0 and
        // Level = 1. Level 1 is also exactly §7.4 at XP = 0, so the creation
        // value and the formula agree by construction.
        Assert.Equal(0, Player.InitialXp);
        Assert.Equal(1, Player.InitialLevel);

        var player = new Player
        {
            PlayerId = "player_new",
            DiscordUserId = "1",
        };

        Assert.Equal(Player.InitialXp, player.XP);
        Assert.Equal(Player.InitialLevel, player.Level);
        Assert.Equal(Player.LevelForXp(Player.InitialXp), player.Level);
    }

    [Fact]
    public void PlayerLevelRange_ShouldBeTheDocumentedOneToFifty()
    {
        // DATABASE.md §3 / COMBAT_RULES.md §7 / ADR-016 item 2: the Player Level
        // range is [1, 50]. Both bounds are asserted so neither can drift.
        Assert.Equal(1, Player.MinLevel);
        Assert.Equal(50, Player.MaxLevel);
    }

    // =======================================================================
    // COMBAT_RULES.md §7.2 — the two outcome grants
    // =======================================================================

    [Fact]
    public void BattleWon_ShouldGrantExactlyOneHundredXp()
    {
        // §7.2 / ADR-016 item 4: BattleWon → Player XP +100. The amount is the
        // documented one — never the retired +50.
        Assert.Equal(100, Player.BattleWonXpReward);

        var player = NewPlayer();

        player.GrantBattleXp(Player.BattleWonXpReward);

        Assert.Equal(100, player.XP);
        Assert.Equal(2, player.Level);
    }

    [Fact]
    public void BattleLost_ShouldGrantExactlyZeroXp_AndChangeNoLevel()
    {
        // §7.2 / ADR-016 item 5: BattleLost → Player XP +0. §7.5 item 4: defeat
        // changes nothing, so neither the XP nor the Level moves.
        Assert.Equal(0, Player.BattleLostXpReward);

        var player = NewPlayer(xp: 150);

        player.GrantBattleXp(Player.BattleLostXpReward);

        Assert.Equal(150, player.XP);
        Assert.Equal(Player.LevelForXp(150), player.Level);
    }

    [Fact]
    public void RewardAmount_AndCurveConstant_ShouldBeIndependentConstants()
    {
        // COMBAT_RULES.md §7.3: the reward amount (configuration) and the curve
        // constant (formula) are two independent concepts that must never be
        // collapsed into one value, even while both currently equal 100. They are
        // declared as two members so retuning one cannot silently rewrite the
        // other.
        Assert.Equal(100, Player.BattleWonXpReward);
        Assert.Equal(100, Player.XpPerLevelCurveConstant);

        // §7.3's shape, asserted through behaviour: moving the reward amount does
        // not move the curve. 400 XP is Level 5 regardless of the reward size.
        Assert.Equal(5, Player.LevelForXp(400));
    }

    [Fact]
    public void GrantBattleXp_ShouldFollowTheFormulaAcrossTheLevelBoundary()
    {
        // §7.4 + §7.2: crossing a 100-XP boundary raises the Level by exactly one.
        // Driven by real grants, so the integration of the two rules is verified
        // rather than the formula alone.
        var player = NewPlayer();

        player.GrantBattleXp(Player.BattleWonXpReward); // 100

        Assert.Equal(100, player.XP);
        Assert.Equal(2, player.Level);

        player.GrantBattleXp(Player.BattleWonXpReward); // 200

        Assert.Equal(200, player.XP);
        Assert.Equal(3, player.Level);
    }

    [Fact]
    public void GrantBattleXp_AcrossTheLevelFiftyBoundary_ShouldCapTheLevel_NotTheXp()
    {
        // §7.5 items 1–2 at the exact documented boundary: reaching 4900 XP is
        // Level 50, and one more win keeps Level 50 while the XP grows past it.
        var player = NewPlayer(LevelFiftyBoundaryXp);

        Assert.Equal(4900, player.XP);
        Assert.Equal(50, player.Level);

        player.GrantBattleXp(Player.BattleWonXpReward);

        Assert.Equal(5000, player.XP);
        Assert.Equal(50, player.Level);
    }

    [Fact]
    public void GrantBattleXp_ShouldRefuseANegativeGrant()
    {
        // No document defines a negative Player XP grant: §7.2 fixes two
        // non-negative outcomes. A negative amount is refused rather than applied.
        var player = NewPlayer();

        Assert.Throws<ArgumentOutOfRangeException>(() => player.GrantBattleXp(-1));
        Assert.Equal(Player.InitialXp, player.XP);
    }

    // =======================================================================
    // ADR-016 item 12 — the two tracks are separate
    // =======================================================================

    [Fact]
    public void PlayerProgression_ShouldIntroduceNoPetOrCombatMember()
    {
        // ADR-016 item 12 / COMBAT_RULES.md §7.6 / AGENTS.md §16: Player
        // progression touches the Player's own two members only. No Pet
        // progression member and no Player combat stat may appear on the type —
        // a Player Level is an account/meta value with no combat stats.
        var members = typeof(Player).GetProperties().Select(property => property.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "HP", "MaxHP", "ATK", "DEF", "Crit", "Power",
                     "PetXp", "PetXP", "PetLevel", "PetId", "Pets",
                 })
        {
            Assert.DoesNotContain(forbidden, members);
        }

        // The Player's own progression members are exactly the documented two.
        Assert.Contains("XP", members);
        Assert.Contains("Level", members);
    }
}
