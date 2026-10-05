using GameServer.Api.Hubs;

namespace GameServer.Api.Tests;

/// <summary>
/// TASK-032 — the Api layer's share of the Player/Pet authority regression
/// suite.
///
/// <code>
/// ADR-011 / ADR-012 authority model
/// Player       = account / owner
/// Pet          = combat character
/// BattleState  = authoritative battle snapshot
/// PetState     = combat-state owner
/// PlayerState  = NOT a combat-stat owner (no such node)
/// </code>
///
/// The Api layer is one of the layers a removed Player combat-readiness
/// mechanism could return through: it composes the Application boundary and owns
/// the hub, so a readiness abstraction reintroduced here would be as much a
/// regression as one in Application (<c>DATABASE.md</c> §1 — <c>BattleResult</c>
/// persistence requires no Player combat-readiness condition; <c>ADR-011</c>
/// item 5 — the Player owns no combat statistics at all).
///
/// <b>Each layer asserts its own assembly.</b> The regression can reappear in any
/// of them, and a scan of Domain would not see an Api-layer reintroduction. The
/// same four checks are therefore applied per layer by
/// <c>Application.Tests.AuthorityRegressionSuiteTests</c>,
/// <c>Infrastructure.Tests.InfrastructureAuthorityRegressionTests</c>, and this
/// class — duplicated deliberately rather than shared through a cross-project
/// reference, which would couple four independent test projects to gain fifteen
/// lines (<c>AGENTS.md</c> §9).
/// </summary>
public class ApiAuthorityRegressionTests
{
    /// <summary>
    /// The type names TASK-056/TASK-057 removed from the contract. They are not
    /// renamed, not deprecated, and not replaced: <c>DATABASE.md</c> §1 states
    /// that <c>BattleResult</c> persistence requires <b>no</b> Player
    /// combat-readiness condition, so there is no predicate of any name to
    /// reintroduce (<c>ADR-011</c> item 5 — the Player owns no combat statistics
    /// at all).
    /// </summary>
    private static readonly string[] RemovedCombatReadinessTypes =
    [
        "IsCombatReady",
        "ICombatStatsSource",
        "PlayerCombatProfile",
        "PlayerCombatProfileSource",
    ];

    [Fact]
    public void Api_ShouldDeclareNoStarterOwnershipEndpointOrManager()
    {
        // TASK-083 §16 / §"Out of Scope": the starter ownership bootstrap adds no
        // public endpoint and no manager. It is reached only through the existing
        // Player-creation step of POST /api/auth/discord (API_CONTRACTS.md §2),
        // and ownership is observed only through the §5 collection reads.
        //
        // Declared-absence rather than route-table inspection: a controller or
        // manager of this shape is the thing that would have to appear first, and
        // catching it here fails the build's tests rather than a review.
        var declared = typeof(GameServer.Api.Controllers.AuthController).Assembly
            .GetTypes()
            .Select(type => type.Name)
            .ToArray();

        foreach (var forbidden in new[]
        {
            "StarterOwnershipController",
            "StarterController",
            "StarterOwnershipManager",
            "StarterGrantService",
            "StarterGrantController",
        })
        {
            Assert.DoesNotContain(forbidden, declared);
        }
    }

    [Fact]
    public void AuthResponse_ShouldCarryOnlyTheDocumentedMembers()
    {
        // API_CONTRACTS.md §2 / ADR-020: the auth response carries
        // `sessionToken`, `playerId`, and `username`.
        var members = typeof(GameServer.Api.Controllers.AuthResponse)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["PlayerId", "SessionToken", "Username"], members);
    }

    [Fact]
    public void RegisterRequest_ShouldCarryOnlyTheDocumentedMembers()
    {
        // API_CONTRACTS.md §2.1: RegisterRequest carries Username and Password.
        var members = typeof(GameServer.Api.Controllers.RegisterRequest)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Password", "Username"], members);
    }

    [Fact]
    public void LoginRequest_ShouldCarryOnlyTheDocumentedMembers()
    {
        // API_CONTRACTS.md §2.2: LoginRequest carries Username and Password.
        var members = typeof(GameServer.Api.Controllers.LoginRequest)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Password", "Username"], members);
    }

    [Fact]
    public void Api_ShouldDeclareNoPlayerCombatReadinessType()
    {
        // GAME_RULES.md §18 / ADR-001 make the server authoritative for HP and the
        // battle outcome; the removed mechanism was a Player-gated prerequisite on
        // the battle-end write (TASK-056/TASK-057). Nothing in this layer may
        // reintroduce a predicate, a profile, or a stats source of that shape.
        //
        // The scan is declared-absence rather than usage-absence, so a
        // reintroduced but currently-unreferenced type is still caught — the same
        // technique MatchComboAccountingTests.BattleState_ShouldExposeNoPlayerStateNode
        // uses for the removed PlayerState node.
        var declared = typeof(BattleHub).Assembly.GetTypes().Select(t => t.Name).ToArray();

        foreach (var forbidden in RemovedCombatReadinessTypes)
        {
            Assert.DoesNotContain(forbidden, declared);
        }

        // Stated for any name that merely mentions the removed mechanism, so a
        // renamed reintroduction cannot slip past the exact-name check
        // (e.g. IPlayerCombatProfileSource, PlayerCombatReadiness).
        foreach (var fragment in new[] { "CombatReady", "CombatReadiness", "CombatStatsSource" })
        {
            Assert.DoesNotContain(
                declared,
                name => name.Contains(fragment, StringComparison.Ordinal));
        }

        // The scan is a real scan: the layer's documented types are present, so an
        // empty or unloaded assembly cannot make the assertions above vacuous.
        Assert.NotEmpty(declared);
        Assert.Contains(nameof(BattleHub), declared);
    }

    [Fact]
    public void Api_ShouldDeclareNoPlayerStateNode()
    {
        // GAME_STATE.md §2: "There is no PlayerState member." The wire label
        // `playerState` of SIGNALR_PROTOCOL.md §4.2 is a fixed protocol label for
        // the Combo/MatchCount projection and not a state path (ADR-011 item 6).
        // The Api layer is included because the label is defined here and is
        // therefore the plausible place a node of that name would be reintroduced
        // beside it.
        var declared = typeof(BattleHub).Assembly.GetTypes().Select(t => t.Name).ToArray();

        Assert.DoesNotContain("PlayerState", declared);

        // The documented projection type is still present under its own name, so
        // the assertion above is an exclusion rather than a renamed field in
        // disguise: `playerState` is the fixed protocol label ADR-011 item 6 keeps
        // and renaming it is a protocol-breaking change, not this task's.
        Assert.Contains("PlayerStatePayload", declared);
        Assert.Contains("BattleStateUpdated", declared);
    }

    [Fact]
    public void PlayerStatePayload_ShouldCarryOnlyTheDocumentedComboAndMatchCount()
    {
        // SIGNALR_PROTOCOL.md §4.2 item 2 fixes the `playerState` object to
        // EXACTLY two members — `combo` and `matchCount` — both <c>BattleState</c>
        // root members (GAME_STATE.md §2.2). The Player is the account/owner with
        // no battle-time combat pool (ADR-011), so no combat stat may reappear on
        // this payload.
        //
        // ApiIntegrationTests asserts the serialized member set against a live
        // push; this asserts the payload TYPE's own member set, so a member added
        // to the record is caught even if no current projection site populates it.
        var members = typeof(PlayerStatePayload)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Combo", "MatchCount" }, members);

        foreach (var combatStat in new[] { "HP", "MaxHP", "ATK", "DEF", "Crit", "Power", "Element" })
        {
            Assert.DoesNotContain(combatStat, members);
        }
    }
}
