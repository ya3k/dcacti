using GameServer.Domain.Cards;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;

namespace GameServer.Application.Players;

/// <summary>
/// The starter ownership set a newly created Player receives, staged for a
/// single atomic commit (<c>DATABASE.md</c> §2 items 1–4; TASK-084 / TASK-082
/// decision E / R1-4).
///
/// <code>
/// 1 Pet  ownership instance   (PetInstanceId minted here)
/// 3 Card unlock rows          (PlayerId + CardDefinitionId)
/// 3 Relic ownership instances (RelicInstanceId minted here)
/// </code>
///
/// <b>It is the composition, not the decision.</b> Which Pet, which Cards, and
/// which Relics are the starter set is fixed by <c>DATABASE.md</c> §2 item 1 and
/// resolved from the provisioned definition rows by
/// <see cref="PlayerStarterGrantFactory"/> — this type only carries the
/// already-resolved rows so the persistence boundary can stage them beside the
/// <c>Player</c> row.
///
/// <b>It carries ownership rows only.</b> The three entities here are the three
/// ownership tables of <c>DATABASE.md</c> §1 — there is no acquisition rule, no
/// reward, no progression value, and no equipping: the Card rows are unlock
/// flags (<c>CARD_RULES.md</c> §1 item 3, ADR-012 item 9) and the Relic rows are
/// owned instances, never equipped ones (<c>RELIC_RULES.md</c> §2 item 1,
/// ADR-012 item 7).
///
/// <b>It is not persisted on its own.</b> The rows travel to the persistence
/// boundary as one value so the Player row and all seven ownership rows commit
/// through one <c>SaveChangesAsync</c> — or none of them does
/// (<c>DATABASE.md</c> §2 item 4).
/// </summary>
public sealed class PlayerStarterGrant
{
    /// <summary>
    /// Creates the grant from its already-resolved ownership rows.
    /// </summary>
    /// <param name="starterPet">
    /// The one owned starter Pet instance (<c>DATABASE.md</c> §2 item 1 —
    /// <c>pet-xich-lang</c>). Its <see cref="Pet.PlayerId"/> is already the
    /// Player the grant belongs to.
    /// </param>
    /// <param name="starterCards">
    /// The three <c>PlayerUnlockedCard</c> rows — all three content-defined
    /// Basic Cards (<c>CARD_RULES.md</c> §2).
    /// </param>
    /// <param name="starterRelics">
    /// The three owned starter Relic instances (<c>RELIC_RULES.md</c> §6), each
    /// with its own server-minted <see cref="Relic.RelicInstanceId"/>.
    /// </param>
    public PlayerStarterGrant(
        Pet starterPet,
        IReadOnlyList<PlayerUnlockedCard> starterCards,
        IReadOnlyList<Relic> starterRelics)
    {
        StarterPet = starterPet ?? throw new ArgumentNullException(nameof(starterPet));
        StarterCards = starterCards ?? throw new ArgumentNullException(nameof(starterCards));
        StarterRelics = starterRelics ?? throw new ArgumentNullException(nameof(starterRelics));
    }

    /// <summary>
    /// The one owned starter Pet instance (<c>DATABASE.md</c> §2 item 1).
    /// </summary>
    public Pet StarterPet { get; }

    /// <summary>
    /// The three Card unlock rows (<c>DATABASE.md</c> §2 item 1: "3
    /// <c>PlayerUnlockedCard</c> rows").
    /// </summary>
    public IReadOnlyList<PlayerUnlockedCard> StarterCards { get; }

    /// <summary>
    /// The three owned Relic instances (<c>DATABASE.md</c> §2 item 1: "3 owned
    /// <c>Relic</c> rows").
    /// </summary>
    public IReadOnlyList<Relic> StarterRelics { get; }
}
