using Microsoft.Extensions.DependencyInjection;
using GameServer.Domain.Relics;

namespace GameServer.Application.Relics;

/// <summary>
/// A singleton wrapper that resolves the scoped <see cref="IRelicRepository"/>
/// on each lookup using <see cref="IServiceScopeFactory"/> — the
/// singleton/scoped adaptation <see cref="IRelicDefinitionLookup"/> requires,
/// mirroring <c>ScopedCardDefinitionLookup</c>
/// (<c>GameServer.Application.Cards</c>) exactly.
///
/// <b>Why the adapter exists.</b> The Relic definition read is a PostgreSQL read
/// through the scoped persistence boundary: <c>IRelicRepository</c> takes the
/// scoped <c>GameDbContext</c> (<c>ARCHITECTURE.md</c> §3's
/// "PersistenceRepository (Postgres)", Infrastructure layer), while its consumer
/// <c>BattleStateService</c> is registered as a singleton
/// (<c>GameServer.Application.DependencyInjection</c>) because it holds the
/// per-battle loadout configuration its later resolutions read. A singleton must
/// not capture a scoped <c>DbContext</c>, so this type deliberately holds no
/// repository and no context: it holds only the scope factory and creates one
/// scope per lookup, which is the repository's established adaptation pattern for
/// that lifetime difference.
///
/// <b>It adds no behavior of its own.</b> It shapes the call — argument
/// validation, one scope, resolve, delegate — and returns the Domain
/// <see cref="RelicDefinition"/> the repository produced. It is not a cache
/// (<c>REDIS_STATE.md</c>/<c>ARCHITECTURE.md</c> §5 item 3 introduce none), it
/// holds no definition between calls, and it resolves no Relic: the returned
/// value is content only (<c>RELIC_RULES.md</c> §8.7).
/// </summary>
public sealed class ScopedRelicDefinitionLookup : IRelicDefinitionLookup
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedRelicDefinitionLookup(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <inheritdoc />
    public async Task<RelicDefinition?> GetDefinitionAsync(
        string relicDefinitionId,
        CancellationToken cancellationToken = default)
    {
        // An absent identity is not a lookup miss: null, empty, or whitespace
        // names no row, so it is rejected before any query rather than resolving
        // to the same null a genuinely unknown identity produces
        // (RELIC_RULES.md §8.5: every definition identity is a real value form).
        ArgumentException.ThrowIfNullOrWhiteSpace(relicDefinitionId);

        using var scope = _scopeFactory.CreateScope();
        var relicRepository = scope.ServiceProvider.GetRequiredService<IRelicRepository>();
        return await relicRepository.GetDefinitionAsync(relicDefinitionId, cancellationToken).ConfigureAwait(false);
    }
}
