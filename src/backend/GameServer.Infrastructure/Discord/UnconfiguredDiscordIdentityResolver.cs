using GameServer.Application.Identity;

namespace GameServer.Infrastructure.Discord;

/// <summary>
/// The Discord identity exchange client's registration point
/// (<c>API_CONTRACTS.md</c> §2.2–§2.4, <c>ADR-013</c>).
///
/// <b>The exchange implementation does not exist yet, and this task does not
/// implement it.</b> <c>API_CONTRACTS.md</c> §2 fixes the token endpoint, grant
/// type, request encoding, credential mechanism, identity endpoint, extracted
/// field, and failure mapping, and <c>ADR-013</c> item 13 fixes its placement
/// (an Infrastructure sibling of <c>Postgres/</c>, <c>Redis/</c>,
/// <c>SignalR/</c>). Writing that client is the downstream implementation of
/// the TASK-035 contract; it is explicitly outside TASK-023, which consumes a
/// verified <c>DiscordUserId</c> rather than producing one.
///
/// Until that client is registered, the exchange cannot be performed, so the
/// resolver fails with the <c>API_CONTRACTS.md</c> §2.6 transient condition
/// rather than inventing an identity. That is deliberate and is the only safe
/// behaviour: manufacturing a <c>DiscordUserId</c> where verification did not
/// happen is exactly the conflation <c>API_CONTRACTS.md</c> §2.4 and
/// <c>ADR-013</c> item 12 forbid, and it would write a Player row for an
/// unverified identity — the failure <c>§2.6</c> rule 5 calls out.
///
/// Consequently <c>POST /api/auth/discord</c> returns <c>503
/// DISCORD_UNAVAILABLE</c> — the §2.6 code for "the exchange could not be
/// completed" — and creates no Player, until the TASK-035 exchange client is
/// registered in its place. No Player row is ever produced from it.
/// </summary>
internal sealed class UnconfiguredDiscordIdentityResolver : IDiscordIdentityResolver
{
    /// <inheritdoc />
    public Task<DiscordIdentityResolution> ResolveAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DiscordIdentityResolution.Failure(
            statusCode: 503,
            error: "DISCORD_UNAVAILABLE",
            message: "The Discord identity exchange is not available."));
}
