using GameServer.Domain.Players;

namespace GameServer.Application.Players;

/// <summary>
/// The Player persistence boundary the authentication flow consumes
/// (<c>DATABASE.md</c> §1).
///
/// It exposes the one ownership operation TASK-023 defines: resolve the Player
/// that owns a verified <c>DiscordUserId</c>, creating it at the documented
/// initial Level if no Player exists yet.
///
/// <b>It is an Application boundary, not a persistence implementation.</b>
/// The interface lives here so the Application layer can orchestrate Player
/// ownership without depending on EF Core; the implementation is an
/// Infrastructure concern (<c>ARCHITECTURE.md</c> §2.1, §3
/// "PersistenceRepository (Postgres) — Infrastructure"). Domain types cross
/// this boundary; persistence types do not (<c>ARCHITECTURE.md</c> §2 item 3).
/// </summary>
public interface IPlayerRepository
{
    /// <summary>
    /// Returns the Player owning <paramref name="discordUserId"/>, creating it
    /// at <see cref="Player.InitialLevel"/> when no Player exists yet
    /// (<c>DATABASE.md</c> §1, §3).
    ///
    /// The lookup key is the verified Discord identity alone: an authorization
    /// code, a Discord access token, or an application session is never this
    /// value (<c>API_CONTRACTS.md</c> §2.4).
    ///
    /// An existing Player is returned unchanged — its <c>Level</c> and
    /// <c>CreatedAt</c> are preserved — and no second Player row is created
    /// for an identity that already has one (<c>DATABASE.md</c> §3: a repeated
    /// authentication does not create a duplicate Player row).
    /// </summary>
    /// <param name="discordUserId">
    /// The verified <c>DiscordUserId</c> of <c>API_CONTRACTS.md</c> §2.4 — the
    /// Discord User object's <c>id</c>, a snowflake string. The caller writes
    /// a Player only after this identity has been verified
    /// (<c>API_CONTRACTS.md</c> §2.6 rule 5, §2.7 item 6).
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the persistence work with the request.
    /// </param>
    /// <returns>
    /// The existing Player for <paramref name="discordUserId"/>, or the newly
    /// created one at <see cref="Player.InitialLevel"/>.
    /// </returns>
    Task<Player> GetOrCreateByDiscordUserIdAsync(
        string discordUserId,
        CancellationToken cancellationToken = default);
}
