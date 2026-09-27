using GameServer.Domain.Pets;

namespace GameServer.Application.Pets;

/// <summary>
/// The Pet persistence boundary (<c>DATABASE.md</c> §1–§2).
///
/// It exposes the operations the Application layer needs: add an owned
/// instance, resolve an owned instance by its identifier, read the
/// static definition row the battle-start path needs, and store a mutated
/// instance's own progression values.
///
/// <b>It is an Application boundary, not a persistence implementation.</b>
/// The interface lives here so the Application layer can orchestrate Pet
/// work without depending on EF Core; the implementation is an
/// Infrastructure concern (<c>ARCHITECTURE.md</c> §2.1, §3
/// "PersistenceRepository (Postgres) — Infrastructure"). Domain types cross
/// this boundary; persistence types do not (<c>ARCHITECTURE.md</c> §2 item
/// 3).
///
/// <b>No recompute surface exists here.</b> The retired Pet Level recompute
/// pass (<c>Player.Level × PetLevelMultiplier</c>) was removed with the
/// derivation itself (<c>PET_RULES.md</c> §5.6 item 1, ADR-016 item 13), so
/// this boundary exposes no bulk-listing or bulk-save operation. Pet Level is
/// re-derived per instance from that instance's own XP
/// (<c>PET_RULES.md</c> §5.4), which the single progression write below
/// stores.
/// </summary>
public interface IPetRepository
{
    /// <summary>
    /// Persists a new owned Pet instance (<c>DATABASE.md</c> §1).
    ///
    /// The caller supplies the instance's stored <see cref="Pet.Level"/> —
    /// this boundary stores the value it is given; it does not derive or
    /// invent one.
    /// </summary>
    /// <param name="pet">The owned instance to persist.</param>
    /// <param name="cancellationToken">Cancels the persistence work.</param>
    Task AddAsync(Pet pet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the definition row for <paramref name="petDefinitionId"/>,
    /// or <c>null</c> when no such definition exists.
    ///
    /// The battle-start path reads the selected definition's static content
    /// through this lookup (<c>API_CONTRACTS.md</c> §3).
    /// </summary>
    /// <param name="petDefinitionId">The definition's identifier.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<PetDefinition?> GetDefinitionAsync(
        string petDefinitionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the owned Pet instance identified by
    /// <paramref name="petInstanceId"/>, or <c>null</c> when no such
    /// instance exists.
    ///
    /// The battle-start path resolves the selected <c>petId</c> through this
    /// lookup and checks the returned instance's
    /// <see cref="Pet.PlayerId"/> against the requesting Player
    /// (<c>API_CONTRACTS.md</c> §3: "petId must be owned by the player —
    /// <c>PET_RULES.md</c> §2"). Ownership is established from persistence,
    /// never from a client-supplied claim (<c>GAME_RULES.md</c> §18,
    /// ADR-001).
    ///
    /// It is deliberately an instance lookup and not a Player-scoped one: the
    /// caller must be able to distinguish "no such Pet exists" from "the Pet
    /// exists but belongs to another Player", which a Player-filtered query
    /// would collapse into one absent result. Both are rejected, and the
    /// caller applies that single rejection — this boundary reports only what
    /// the store holds.
    /// </summary>
    /// <param name="petInstanceId">
    /// The owned instance's identifier (<c>DATABASE.md</c> §1:
    /// <c>PetInstanceId</c> (PK)) — the value the battle-start request submits
    /// as <c>petId</c>.
    /// </param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Pet?> GetByIdAsync(
        string petInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a mutated Pet instance's progression values — the one write
    /// this boundary exposes (<c>DATABASE.md</c> §1, <c>PET_RULES.md</c>
    /// §5.3/§5.4).
    ///
    /// <b>Why the boundary gains exactly one member.</b> A Pet instance's
    /// <c>XP</c> and <c>Level</c> are persisted columns that the battle-end
    /// reward path must maintain (<c>PET_RULES.md</c> §5.1 item 6: Pet XP
    /// persists permanently with the instance), and the surface above is
    /// read-only plus add, so there is no way to store a mutated instance.
    /// Adding this one operation keeps the change inside the existing
    /// boundary rather than introducing a progression service, manager, or
    /// wallet (<c>AGENTS.md</c> §9, <c>ARCHITECTURE.md</c> §5).
    ///
    /// <b>The caller owns the values; this boundary only stores them.</b> It
    /// computes no XP and no Level: <see cref="Pet.GrantBattleXp"/> owns the
    /// documented grant, the hard cap, and the
    /// <see cref="Pet.LevelForXp"/> relationship (<c>PET_RULES.md</c>
    /// §5.3–§5.5). A missing row is reported as absence rather than silently
    /// creating a Pet, so a reward can never bring a Pet instance into
    /// existence.
    ///
    /// <b>It is deliberately instance-scoped.</b> <c>PET_RULES.md</c> §5.3
    /// item 1 makes exactly one Pet — the active combat Pet — the recipient
    /// of a battle's Pet XP, and items 3–4 give every other owned Pet
    /// <c>+0</c>: there is no bulk, party-wide, or account-wide Pet XP path,
    /// so this boundary exposes no bulk save and no recompute pass.
    /// </summary>
    /// <param name="pet">
    /// The Pet instance whose current <see cref="Pet.XP"/> and
    /// <see cref="Pet.Level"/> are to be stored. Its
    /// <see cref="Pet.PetInstanceId"/> identifies the row.
    /// </param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// <c>true</c> when the row was found and updated; <c>false</c> when no
    /// Pet instance exists for that identifier, in which case nothing is
    /// written.
    /// </returns>
    Task<bool> SaveProgressionAsync(
        Pet pet,
        CancellationToken cancellationToken = default);
}
