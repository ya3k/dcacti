namespace GameServer.Application.Battle;

using GameServer.Domain.Bosses;

/// <summary>
/// The boundary that resolves a battle's canonical Boss Identity to its
/// <c>BossDefinition</c> row — the two documented lookups of <c>DATABASE.md</c>
/// §1 note item 2 ("Resolution mechanisms and resolver owner": "The
/// <b><c>Infrastructure</c> layer owns both queries</b>").
///
/// <code>
/// Battle Start                                Battle End
/// ------------                                ----------
/// request.BossId                              BattleState.BossState.BossId
///         ↓  GetDefinitionByIdentityAsync              ↓
/// BossDefinition row, by Identity             BossDefinition row, by Identity
///         ↓                                            ↓
/// Domain Overlay (ADR-024 §2.2)               BossDefinitionId
///         ↓                                     the row's persistence key
/// BossDefinition with the row's four
/// runtime balance values
/// </code>
///
/// <b>The battle-start lookup is the Domain Overlay</b> (<c>ADR-024</c> §2.2):
/// the structural definition — Identity, Element, Passive, Skill, and the
/// Skill's <c>SecondaryEffect</c> — comes from <c>BossDefinitions.All</c>, and
/// only the four runtime balance values (<c>MaxHP</c>, <c>ATK</c>, <c>DEF</c>,
/// <c>EnrageThreshold</c>) come from PostgreSQL, which is authoritative for them
/// at battle creation.
///
/// <b>It returns a definition, and one of two failures.</b> A <c>null</c> result
/// means the requested identity is not a content-defined MVP Boss
/// (<c>BOSS_RULES.md</c> §6.4) — the client-visible <c>BOSS_NOT_FOUND</c>
/// rejection of <c>API_CONTRACTS.md</c> §3 (<c>ADR-024</c> §2.1 item 1). A
/// provisioned row that is absent or violates the documented range invariants is
/// <b>not</b> a client error: it is a server-side configuration-integrity
/// failure and is raised, never returned as <c>null</c> and never answered from
/// the static baseline (<c>ADR-024</c> §2.1 item 3, §2.4).
///
/// <b>It is the read half of a lookup, not a resolver service.</b>
/// <c>DATABASE.md</c> §1 note item 2 states the boundary explicitly: "This
/// introduces <b>no new abstraction, resolver service, registry, or read
/// model</b>: the lookup is a query on the existing persistence boundary
/// (<c>ARCHITECTURE.md</c> §5 item 3 — 'no separate read-model store')". The
/// interface carries one identity in and one definition out and owns no
/// battle-state member: adding a <c>BossDefinitionId</c> to <c>BossState</c> or
/// <c>BattleState</c> stays forbidden (<c>GAME_STATE.md</c> §2.4,
/// <c>REDIS_STATE.md</c> §2 item 1).
///
/// <b>Not found is a defined outcome, not an error to absorb.</b> For the
/// battle-end lookup, when no row carries the Identity, <see
/// cref="FindBossDefinitionIdByIdentityAsync"/> returns <c>null</c> and the
/// battle-end path fails closed (<c>DATABASE.md</c> §1 sourcing item 3): no
/// <c>BattleResult</c> row is written, the <c>battle:{battleId}:state</c> record
/// is not deleted, and the battle remains recoverable. A caller must never
/// fabricate a key, fall back to the Identity itself, or skip the foreign key.
/// </summary>
public interface IBossDefinitionLookup
{
    /// <summary>
    /// The persistence key of the <c>BossDefinition</c> row whose
    /// <c>Identity</c> equals <paramref name="bossIdentity"/>, or <c>null</c>
    /// when no such row exists.
    /// </summary>
    /// <param name="bossIdentity">
    /// The battle's canonical technical Boss Identity
    /// (<c>BOSS_RULES.md</c> §6.4, e.g. <c>boss-hoa-long</c>) — the value read
    /// from <c>BattleState.BossState.BossId</c>, never a display name and never
    /// a persistence key.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The row's <c>BossDefinitionId</c>, or <c>null</c> when the Identity is
    /// unknown to persistence.
    /// </returns>
    Task<string?> FindBossDefinitionIdByIdentityAsync(
        string bossIdentity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The battle-creation definition for <paramref name="bossIdentity"/> — the
    /// structural Domain baseline with the row's four runtime balance values
    /// overlaid (<c>ADR-024</c> §2.2).
    /// </summary>
    /// <param name="bossIdentity">
    /// The submitted canonical technical Boss Identity (<c>BOSS_RULES.md</c>
    /// §6.4, e.g. <c>boss-hoa-long</c>) — never a display name
    /// (<c>API_CONTRACTS.md</c> §3) and never a persistence key.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The overlaid definition, or <c>null</c> when
    /// <paramref name="bossIdentity"/> is not a content-defined MVP Boss
    /// (<c>ADR-024</c> §2.1 item 1).
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="bossIdentity"/> is null, empty, or whitespace. A caller
    /// that cannot name an identity decides the documented <c>BOSS_NOT_FOUND</c>
    /// rejection itself rather than sending an unnamed query to persistence.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The identity is a content-defined MVP Boss but its provisioned row is
    /// absent, or carries a balance value outside the documented range
    /// invariants (<c>ADR-024</c> §2.1 item 3, §2.4). This is a server-side
    /// configuration-integrity failure: it fails closed and is never answered
    /// from the static baseline.
    /// </exception>
    Task<BossDefinition?> GetDefinitionByIdentityAsync(
        string bossIdentity,
        CancellationToken cancellationToken = default);
}
