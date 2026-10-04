using GameServer.Application.Identity;

namespace GameServer.Infrastructure.Discord;

/// <summary>
/// The development-only identity source (TASK-181) — the
/// <see cref="IDiscordIdentityResolver"/> the composition root registers in place
/// of <see cref="UnconfiguredDiscordIdentityResolver"/> when, and only when, the
/// development authentication path is enabled.
///
/// <code>
/// development authorization code      (DevelopmentAuthenticationOptions.AuthorizationCode)
///         ↓
/// fixed development DiscordUserId     (DevelopmentAuthenticationOptions.DiscordUserId)
///         ↓
/// the unchanged IDiscordIdentityResolver output
///         ↓
/// Player match/create → application session   (AuthController, unchanged)
/// </code>
///
/// <b>It performs no exchange and invents no Discord fact.</b> It answers with the
/// identity this file's contract fixes, and only for the one sentinel code
/// (<see cref="DevelopmentAuthenticationOptions.AuthorizationCode"/>). Every other
/// code — including a genuine Discord authorization code — keeps the exact answer
/// the TASK-035 boundary gives today (<c>503 DISCORD_UNAVAILABLE</c>), so the
/// production/default pair's behaviour is not weakened or shadowed: it is
/// delegated to unchanged.
///
/// The seam is the whole point: <c>AuthController</c> is untouched, so the
/// development session is issued by the same three steps a Discord session is
/// (identity → Player → session), carries the same <c>player_id</c> claim, and is
/// accepted by the same authorization pipeline
/// (<c>API_CONTRACTS.md</c> §2.5/§2.8; <c>ADR-015</c> D1/D3/D6). No endpoint gains
/// an <c>[AllowAnonymous]</c> or a bypass for it.
/// </summary>
internal sealed class DevelopmentDiscordIdentityResolver : IDiscordIdentityResolver
{
    /// <summary>
    /// The unchanged answer for every code that is not the development trigger —
    /// the same <c>503</c> the unconfigured boundary already gives, so this
    /// resolver never becomes a second, more permissive exchange.
    /// </summary>
    private readonly IDiscordIdentityResolver _unavailable = new UnconfiguredDiscordIdentityResolver();

    /// <inheritdoc />
    public Task<DiscordIdentityResolution> ResolveAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(code, DevelopmentAuthenticationOptions.AuthorizationCode, StringComparison.Ordinal))
        {
            return _unavailable.ResolveAsync(code, cancellationToken);
        }

        // The identity is the fixed server-owned value. It is not derived from the
        // code, from any other request member, or from configuration, so a caller
        // cannot choose which Player the development session belongs to
        // (API_CONTRACTS.md §2.8 "Identity"; ADR-015 D3).
        return Task.FromResult(
            DiscordIdentityResolution.Success(new DiscordIdentity(DevelopmentAuthenticationOptions.DiscordUserId)));
    }
}
