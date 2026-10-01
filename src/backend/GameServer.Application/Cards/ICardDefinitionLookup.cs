using GameServer.Domain.Cards;

namespace GameServer.Application.Cards;

/// <summary>
/// Look up CardDefinition by its definition identity (<c>DATABASE.md</c> §1).
/// </summary>
public interface ICardDefinitionLookup
{
    /// <summary>
    /// Looks up a Card definition by its identity.
    /// </summary>
    /// <param name="cardDefinitionId">The identity of the Card definition to look up.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The Card definition, or <c>null</c> if not found.</returns>
    Task<CardDefinition?> GetDefinitionAsync(
        string cardDefinitionId,
        CancellationToken cancellationToken = default);
}
