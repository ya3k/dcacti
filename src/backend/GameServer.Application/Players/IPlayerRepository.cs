using GameServer.Domain.Players;

namespace GameServer.Application.Players;

/// <summary>
/// The Player persistence boundary the authentication flow consumes
/// (<c>DATABASE.md</c> §1, <c>ADR-020</c>).
/// </summary>
public interface IPlayerRepository
{
    /// <summary>
    /// Returns the Player owning <paramref name="accountId"/>, creating it
    /// at <see cref="Player.InitialLevel"/> with starter grants when no Player exists yet
    /// (<c>DATABASE.md</c> §1, §3, <c>ADR-020</c>).
    /// </summary>
    Task<Player> GetOrCreateForAccountAsync(
        Guid accountId,
        Func<CancellationToken, Task<PlayerStarterGrant>> composeStarterGrant,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Player owning <paramref name="accountId"/>, or null if none.
    /// </summary>
    Task<Player?> GetByAccountIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Player identified by <paramref name="playerId"/>, or
    /// <c>null</c> when no such Player exists.
    /// </summary>
    Task<Player?> GetByIdAsync(
        string playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a mutated Player's progression values (<c>DATABASE.md</c> §1).
    /// </summary>
    Task<bool> SaveProgressionAsync(
        Player player,
        CancellationToken cancellationToken = default);
}
