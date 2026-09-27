namespace GameServer.Application.Battle;

/// <summary>
/// The boundary that resolves a battle's canonical Boss Identity to the
/// persistence key of its <c>BossDefinition</c> row — the documented battle-end
/// lookup of <c>DATABASE.md</c> §1 note item 2 ("Resolution mechanism and
/// resolver owner": "a <b>PostgreSQL lookup by <c>Identity</c></b>, performed on
/// the battle-end path … The <b><c>Infrastructure</c> layer owns the lookup</b>").
///
/// <code>
/// BattleState.BossState.BossId               canonical technical Identity
///         ↓  IBossDefinitionLookup (this contract)
/// BossDefinition row, by Identity            PostgreSQL (Infrastructure)
///         ↓
/// BossDefinitionId                            the row's persistence key
/// </code>
///
/// <b>It is the read half of a lookup, not a resolver service.</b>
/// <c>DATABASE.md</c> §1 note item 2 states the boundary explicitly: "This
/// introduces <b>no new abstraction, resolver service, registry, or read
/// model</b>: the lookup is a query on the existing persistence boundary
/// (<c>ARCHITECTURE.md</c> §5 item 3 — 'no separate read-model store')". The
/// interface carries one key in and one key out and owns no battle-state member:
/// adding a <c>BossDefinitionId</c> to <c>BossState</c> or <c>BattleState</c>
/// stays forbidden (<c>GAME_STATE.md</c> §2.4, <c>REDIS_STATE.md</c> §2 item 1).
///
/// <b>Not found is a defined outcome, not an error to absorb.</b> When no row
/// carries the Identity, this returns <c>null</c> and the battle-end path fails
/// closed (<c>DATABASE.md</c> §1 sourcing item 3): no <c>BattleResult</c> row is
/// written, the <c>battle:{battleId}:state</c> record is not deleted, and the
/// battle remains recoverable. A caller must never fabricate a key, fall back to
/// the Identity itself, or skip the foreign key.
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
}
