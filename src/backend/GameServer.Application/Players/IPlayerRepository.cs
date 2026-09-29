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
    /// authentication does not create a duplicate Player row). On the matching
    /// branch <paramref name="composeStarterGrant"/> is never invoked: the
    /// starter set is creation-time only and no top-up or repair path exists
    /// (<c>DATABASE.md</c> §2 item 3).
    /// </summary>
    /// <param name="discordUserId">
    /// The verified <c>DiscordUserId</c> of <c>API_CONTRACTS.md</c> §2.4 — the
    /// Discord User object's <c>id</c>, a snowflake string. The caller writes
    /// a Player only after this identity has been verified
    /// (<c>API_CONTRACTS.md</c> §2.6 rule 5, §2.7 item 6).
    /// </param>
    /// <param name="composeStarterGrant">
    /// Composes the starter ownership set a <b>newly created</b> Player receives
    /// (<c>DATABASE.md</c> §2 item 1). It is staged together with the Player row
    /// and committed through <b>one</b> <c>SaveChangesAsync</c>, so the Player
    /// and its seven ownership rows either all persist or none does
    /// (<c>DATABASE.md</c> §2 item 4).
    ///
    /// <b>It is a callback, not a value, because it must run on the creation
    /// branch only.</b> Composing it eagerly would resolve the starter content on
    /// every authentication — including the repeated authentications of existing
    /// Players, which grant nothing — and would make a match-branch login depend
    /// on content it never uses. Deferring it also keeps "creation-time only"
    /// structural: this operation is the only caller, and it invokes the callback
    /// on exactly the branch that inserted the Player row.
    ///
    /// It is the one combined commit-scope surface <c>DATABASE.md</c> §2 item 4
    /// records — no unit-of-work, transaction manager, outbox, or domain-event
    /// abstraction is introduced for it.
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
        Func<CancellationToken, Task<PlayerStarterGrant>> composeStarterGrant,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Player identified by <paramref name="playerId"/>, or
    /// <c>null</c> when no such Player exists.
    ///
    /// It is a pure read of the persistent account row
    /// (<c>DATABASE.md</c> §1) — it never creates a Player and never rewrites a
    /// value. The battle-end reward path reads the owning Player through this
    /// lookup before applying the documented XP grant (<c>COMBAT_RULES.md</c>
    /// §7.2).
    /// </summary>
    /// <param name="playerId">The Player's identifier (the auth boundary's
    /// <c>playerId</c>, <c>API_CONTRACTS.md</c> §2.5).</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Player?> GetByIdAsync(
        string playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a mutated Player's progression values — the one write this
    /// boundary exposes (<c>DATABASE.md</c> §1, <c>COMBAT_RULES.md</c> §7.2).
    ///
    /// <b>Why the boundary gains exactly one member.</b> The Player's
    /// <c>XP</c> and <c>Level</c> are persisted columns that the battle-end
    /// reward path must maintain, and today's surface is read-only plus
    /// match-or-create, so there is no way to store a mutated Player. Adding
    /// this one operation keeps the change inside the existing boundary rather
    /// than introducing a progression service, wallet, or manager
    /// (<c>AGENTS.md</c> §9, <c>ARCHITECTURE.md</c> §5).
    ///
    /// <b>The caller owns the values; this boundary only stores them.</b> It
    /// computes no XP and no Level: <see cref="Player.LevelForXp"/> owns the
    /// documented relationship (<c>COMBAT_RULES.md</c> §7.4) and
    /// <c>§7.2</c> owns the amount. A missing row is reported as absence
    /// rather than silently creating a Player, so a reward can never bring a
    /// Player into existence.
    /// </summary>
    /// <param name="player">
    /// The Player whose current <see cref="Player.XP"/> and
    /// <see cref="Player.Level"/> are to be stored. Its
    /// <see cref="Player.PlayerId"/> identifies the row.
    /// </param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// <c>true</c> when the row was found and updated; <c>false</c> when no
    /// Player exists for that identifier, in which case nothing is written.
    /// </returns>
    Task<bool> SaveProgressionAsync(
        Player player,
        CancellationToken cancellationToken = default);
}
