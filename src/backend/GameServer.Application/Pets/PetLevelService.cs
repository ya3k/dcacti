using GameServer.Application.Players;
using GameServer.Domain.Pets;

namespace GameServer.Application.Pets;

/// <summary>
/// The denormalized Pet Level recompute hook (<c>DATABASE.md</c> §1;
/// ADR-012 Consequences; <c>PET_RULES.md</c> §5 item 2).
///
/// <code>
/// Player Level changes        →  RecomputeForPlayerAsync
/// PetLevelMultiplier changes  →  RecomputeForDefinitionAsync
///
/// both paths:  Pet.Level = PetLevelDerivation.Derive(Player.Level,
///                                                 PetDefinition.PetLevelMultiplier)
/// </code>
///
/// <b>Why recompute exists.</b> <c>Pet.Level</c> is a denormalized snapshot
/// of the canonical derivation, not an independent XP store
/// (<c>DATABASE.md</c> §1, ADR-012 Consequences). When either input
/// changes outside battle, the stored snapshot must be rewritten to the
/// same derived value the battle-time representation would use
/// (<c>PET_RULES.md</c> §5 item 2: one rule for every representation).
///
/// <b>Integration point for TASK-033.</b> The Level-up path (TASK-033,
/// currently blocked — TASK-024 Implementation Notes) calls
/// <see cref="RecomputeForPlayerAsync"/> after Player Level changes; a
/// future multiplier configuration change calls
/// <see cref="RecomputeForDefinitionAsync"/>. This type is that hook: it
/// owns no XP, no level-up step, and no multiplier default.
///
/// <b>No battle-time path.</b> In-battle Level does not recompute here —
/// battle state is server-authoritative in Redis for the session
/// (<c>ADR-005</c>, <c>REDIS_STATE.md</c>) and this service writes the
/// persistent Postgres snapshot only.
/// </summary>
public sealed class PetLevelService
{
    private readonly IPetRepository _pets;
    private readonly IPlayerRepository _players;

    /// <summary>
    /// Creates the recompute hook over the Pet and Player persistence
    /// boundaries.
    /// </summary>
    /// <param name="pets">The Pet persistence boundary.</param>
    /// <param name="players">The Player persistence boundary (Level source).</param>
    public PetLevelService(IPetRepository pets, IPlayerRepository players)
    {
        _pets = pets;
        _players = players;
    }

    /// <summary>
    /// Recomputes <see cref="Pet.Level"/> for every Pet owned by
    /// <paramref name="playerId"/> from the Player's current Level and each
    /// definition's stored multiplier (<c>PET_RULES.md</c> §5 item 1,
    /// ADR-012 Consequences).
    ///
    /// Call this after Player Level changes outside battle (the TASK-033
    /// Level-up integration point).
    /// </summary>
    /// <param name="playerId">The owning Player whose Level is the input.</param>
    /// <param name="cancellationToken">Cancels the load and save.</param>
    /// <exception cref="InvalidOperationException">
    /// No Player exists for <paramref name="playerId"/>, or a referenced
    /// PetDefinition row is missing — the stored snapshot cannot be
    /// derived without both inputs.
    /// </exception>
    public async Task RecomputeForPlayerAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        var player = await _players.GetByIdAsync(playerId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Player '{playerId}' was not found; Pet Level cannot be derived without Player.Level.");

        var pets = await _pets.ListByPlayerIdAsync(playerId, cancellationToken);

        foreach (var pet in pets)
        {
            var definition = await _pets.GetDefinitionAsync(pet.PetDefinitionId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"PetDefinition '{pet.PetDefinitionId}' was not found; Pet Level cannot be derived without PetLevelMultiplier.");

            pet.Level = PetLevelDerivation.Derive(player.Level, definition.PetLevelMultiplier);
        }

        await _pets.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Recomputes <see cref="Pet.Level"/> for every Pet that references
    /// <paramref name="petDefinitionId"/> from each owner's current Player
    /// Level and the definition's current multiplier
    /// (<c>PET_RULES.md</c> §5 item 1, ADR-012 Consequences).
    ///
    /// Call this after PetLevelMultiplier changes outside battle.
    /// </summary>
    /// <param name="petDefinitionId">The definition whose multiplier changed.</param>
    /// <param name="cancellationToken">Cancels the load and save.</param>
    /// <exception cref="InvalidOperationException">
    /// No PetDefinition exists for <paramref name="petDefinitionId"/>, or a
    /// referenced Player row is missing — the stored snapshot cannot be
    /// derived without both inputs.
    /// </exception>
    public async Task RecomputeForDefinitionAsync(
        string petDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var definition = await _pets.GetDefinitionAsync(petDefinitionId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"PetDefinition '{petDefinitionId}' was not found; Pet Level cannot be derived without PetLevelMultiplier.");

        var pets = await _pets.ListByDefinitionIdAsync(petDefinitionId, cancellationToken);

        foreach (var pet in pets)
        {
            var player = await _players.GetByIdAsync(pet.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Player '{pet.PlayerId}' was not found; Pet Level cannot be derived without Player.Level.");

            pet.Level = PetLevelDerivation.Derive(player.Level, definition.PetLevelMultiplier);
        }

        await _pets.SaveChangesAsync(cancellationToken);
    }
}
