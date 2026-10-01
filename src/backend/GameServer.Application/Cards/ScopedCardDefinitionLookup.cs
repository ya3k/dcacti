using Microsoft.Extensions.DependencyInjection;
using GameServer.Domain.Cards;

namespace GameServer.Application.Cards;

/// <summary>
/// A singleton wrapper that resolves the scoped <see cref="ICardRepository"/>
/// on each lookup using <see cref="IServiceScopeFactory"/>.
/// </summary>
public sealed class ScopedCardDefinitionLookup : ICardDefinitionLookup
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedCardDefinitionLookup(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <inheritdoc />
    public async Task<CardDefinition?> GetDefinitionAsync(
        string cardDefinitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardDefinitionId);

        using var scope = _scopeFactory.CreateScope();
        var cardRepository = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        return await cardRepository.GetDefinitionAsync(cardDefinitionId, cancellationToken).ConfigureAwait(false);
    }
}
