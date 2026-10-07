using GameServer.Domain.Cards;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;

namespace GameServer.Application.Players;

/// <summary>
/// The ownership set a newly created Player receives, staged for a single atomic
/// commit (<c>DATABASE.md</c> §2 items 1–4; <c>MVP_SCOPE.md</c> §1 "Content
/// ownership &amp; reachability"; TASK-084 / TASK-082 decision E / R1-4; TASK-213
/// decision; TASK-221 implementation).
///
/// <code>
/// 5 Pet ownership instances    (PetInstanceId minted here)
/// 3 Card unlock rows           (PlayerId + CardDefinitionId)
/// 10 Relic ownership instances (RelicInstanceId minted here)
/// </code>
///
/// <b>It is the composition, not the decision.</b> Which Pets, which Cards, and
/// which Relics the grant carries is fixed by <c>DATABASE.md</c> §2 item 1 and
/// <c>MVP_SCOPE.md</c> §1, and resolved from the provisioned definition rows by
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
/// <b>Ownership is not a loadout.</b> Every owned Pet instance and every owned
/// Relic instance is an entry in the Player's collection, and the battle loadout
/// — exactly one active Pet and 3–5 Relics — is chosen at battle start and
/// snapshotted into <c>PetState</c> (<c>RELIC_RULES.md</c> §2.5,
/// <c>API_CONTRACTS.md</c> §3). Nothing here selects, equips, orders, or
/// privileges one owned instance over another.
///
/// <b>It is not persisted on its own.</b> The rows travel to the persistence
/// boundary as one value so the Player row and all eighteen ownership rows commit
/// through one <c>SaveChangesAsync</c> — or none of them does
/// (<c>DATABASE.md</c> §2 item 4).
/// </summary>
public sealed class PlayerStarterGrant
{
    /// <summary>
    /// Creates the grant from its already-resolved ownership rows.
    /// </summary>
    /// <param name="starterPets">
    /// The owned Pet instances — one per MVP Pet definition
    /// (<c>DATABASE.md</c> §2 item 1; <c>MVP_SCOPE.md</c> §1: 5 Pets). Each
    /// <see cref="Pet.PlayerId"/> is empty until the creation boundary binds the
    /// Player it minted.
    /// </param>
    /// <param name="starterCards">
    /// The three <c>PlayerUnlockedCard</c> rows — all three content-defined
    /// Basic Cards (<c>CARD_RULES.md</c> §2).
    /// </param>
    /// <param name="starterRelics">
    /// The owned Relic instances — one per MVP Relic definition
    /// (<c>RELIC_RULES.md</c> §6; <c>MVP_SCOPE.md</c> §1: 10 Relics), each with
    /// its own server-minted <see cref="Relic.RelicInstanceId"/>.
    /// </param>
    public PlayerStarterGrant(
        IReadOnlyList<Pet> starterPets,
        IReadOnlyList<PlayerUnlockedCard> starterCards,
        IReadOnlyList<Relic> starterRelics)
    {
        StarterPets = starterPets ?? throw new ArgumentNullException(nameof(starterPets));
        StarterCards = starterCards ?? throw new ArgumentNullException(nameof(starterCards));
        StarterRelics = starterRelics ?? throw new ArgumentNullException(nameof(starterRelics));
    }

    /// <summary>
    /// The owned Pet instances (<c>DATABASE.md</c> §2 item 1;
    /// <c>MVP_SCOPE.md</c> §1: one instance per MVP Pet definition).
    /// </summary>
    public IReadOnlyList<Pet> StarterPets { get; }

    /// <summary>
    /// The three Card unlock rows (<c>DATABASE.md</c> §2 item 1: "3
    /// <c>PlayerUnlockedCard</c> rows").
    /// </summary>
    public IReadOnlyList<PlayerUnlockedCard> StarterCards { get; }

    /// <summary>
    /// The owned Relic instances (<c>DATABASE.md</c> §2 item 1;
    /// <c>MVP_SCOPE.md</c> §1: one instance per MVP Relic definition).
    /// </summary>
    public IReadOnlyList<Relic> StarterRelics { get; }
}
