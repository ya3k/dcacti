using GameServer.Application.Battle;
using GameServer.Domain.Bosses;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Postgres.Repositories;

/// <summary>
/// The PostgreSQL implementation of both Boss Identity lookups
/// (<c>DATABASE.md</c> §1 note item 2) — <c>ARCHITECTURE.md</c> §3's
/// "PersistenceRepository (Postgres)", Infrastructure layer.
///
/// <b>It is the resolver owner TASK-051 named.</b> <c>DATABASE.md</c> §1 note
/// item 2: "The <b><c>Infrastructure</c> layer owns both queries</b> … This
/// introduces <b>no new abstraction, resolver service, registry, or read
/// model</b>: the lookup is a query on the existing persistence boundary." This
/// type is exactly those queries.
///
/// <b>The battle-start lookup is the Domain Overlay</b> (<c>ADR-024</c> §2.2).
/// PostgreSQL is authoritative at battle creation for the four balance values
/// <c>MaxHP</c>, <c>ATK</c>, <c>DEF</c>, and <c>EnrageThreshold</c>; the
/// structural definition — Identity, Element, Passive, Skill, and the Skill's
/// <c>SecondaryEffect</c> (<c>BOSS_RULES.md</c> §6.3.1) — is read from the
/// canonical Domain baseline <c>BossDefinitions.All</c>, so no non-persisted
/// mechanics member can be nulled by materializing a row. The two sources are
/// never merged ambiguously: every structural member comes from the baseline and
/// only the four balance members come from the row.
///
/// <b>The battle-end lookup returns a key, not a definition.</b> That path needs
/// the row's persistence key for the foreign key, and the battle's combat values
/// were already composed and snapshotted at battle creation
/// (<c>DATABASE.md</c> §1 note item 3, <c>GAME_STATE.md</c> §0 item 5).
///
/// <b>The lookup is by <c>Identity</c> alone.</b> <c>DATABASE.md</c> §3 makes
/// <c>BossDefinition.Identity NOT NULL, UNIQUE</c> — "the unique target of the
/// FK lookup in §1" — so each query is a single-row read. Neither ever matches on
/// <c>BossDefinitionId</c> (the two are separate values that must never be
/// substituted for each other, §1 note item 2), never on a display name (which
/// is not a column, §1 note item 1), and never on a pattern or a convention.
///
/// <b>Failures are distinguished by source, never conflated</b> (<c>ADR-024</c>
/// §2.1). An Identity outside the content-defined MVP set is the client-visible
/// miss and returns <c>null</c>; an absent or out-of-range provisioned row is a
/// server-side configuration-integrity failure and throws. PostgreSQL
/// connectivity failures are neither caught nor translated: an
/// <c>NpgsqlException</c> propagates. Nothing here ever falls back to the static
/// baseline's balance values.
///
/// <b>Not found on the battle-end path returns <c>null</c>.</b> That is the
/// defined outcome the battle-end path fails closed on (<c>DATABASE.md</c> §1
/// sourcing item 3): no result row, no active-state delete, battle recoverable.
/// No fallback key is produced here.
/// </summary>
public sealed class BossDefinitionLookup : IBossDefinitionLookup
{
    private readonly GameDbContext _dbContext;

    public BossDefinitionLookup(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<string?> FindBossDefinitionIdByIdentityAsync(
        string bossIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bossIdentity);

        // DATABASE.md §1 note item 2 / §3: the row whose Identity equals the
        // battle's canonical technical Boss Identity. `Identity` is UNIQUE, so
        // this is the documented single-row lookup; the projection reads only the
        // persistence key, because that is the value the foreign key needs.
        //
        // The comparison is against the mapped property itself — not against the
        // wrapper's `Value` member — because the property conversion is what the
        // provider translates. Comparing through the wrapper's member would make
        // the expression untranslatable, and client-evaluating it would read the
        // whole table to find one row.
        var identity = new BossId(bossIdentity);

        return await _dbContext.BossDefinitions
            .AsNoTracking()
            .Where(definition => definition.BossId == identity)
            .Select(definition => definition.BossDefinitionId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<BossDefinition?> GetDefinitionByIdentityAsync(
        string bossIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bossIdentity);

        // ADR-024 §2.2 item 3 / §2.1 item 1: the identity authority is the
        // structural Domain baseline, not persistence. An identity no
        // content-defined MVP Boss carries (BOSS_RULES.md §6.4) is the
        // client-visible miss — no definition is invented for it, and persistence
        // is not queried for it.
        var structuralBaseline = FindStructuralBaseline(bossIdentity);

        if (structuralBaseline is null)
        {
            return null;
        }

        var identity = new BossId(bossIdentity);

        // ADR-024 §2.2 item 3 / §2.3: only the four runtime balance values are
        // read from the row. Projecting them keeps the structural definition on
        // the Domain baseline, so the Skill's non-persisted SecondaryEffect
        // (BOSS_RULES.md §6.3.1) is taken from the baseline and can never be
        // nulled by a round-trip through the stored document.
        var balance = await _dbContext.BossDefinitions
            .AsNoTracking()
            .Where(definition => definition.BossId == identity)
            .Select(definition => new BossBalanceValues(
                definition.MaxHP,
                definition.ATK,
                definition.DEF,
                definition.EnrageThreshold))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // ADR-024 §2.1 item 3: a valid canonical Boss whose provisioned row is
        // absent is a server-side provisioning failure. It fails closed — the
        // static baseline's balance values are never substituted, because that
        // would be the silent split-brain configuration drift §2.1 forbids.
        if (balance is null)
        {
            throw new InvalidOperationException(
                $"The provisioned BossDefinition row for Identity '{bossIdentity}' is missing. "
                + "Boss balance is authoritative in PostgreSQL (ADR-024); it is never answered "
                + "from the static Domain baseline.");
        }

        // ADR-024 §2.4: the documented range invariants are enforced before any
        // value is used. A corrupt row fails closed rather than driving combat
        // math.
        ValidateBalance(bossIdentity, balance);

        return structuralBaseline with
        {
            MaxHP = balance.MaxHP,
            ATK = balance.ATK,
            DEF = balance.DEF,
            EnrageThreshold = balance.EnrageThreshold,
        };
    }

    /// <summary>
    /// The structural Domain baseline for a canonical technical Boss Identity
    /// (<c>BOSS_RULES.md</c> §6.4), or <c>null</c> when no content-defined MVP
    /// Boss carries it. The comparison is ordinal, because the identity is an
    /// opaque stable ASCII id and not a culture-sensitive string — the same
    /// comparison <c>BattleStartService</c> applied before this lookup moved to
    /// persistence.
    /// </summary>
    private static BossDefinition? FindStructuralBaseline(string bossIdentity)
    {
        foreach (var definition in BossDefinitions.All)
        {
            if (string.Equals(definition.BossId.Value, bossIdentity, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    /// <summary>
    /// Enforces <c>ADR-024</c> §2.4's range invariants on a row's balance values:
    /// <c>MaxHP &gt; 0</c>, <c>ATK &gt;= 0</c>, <c>DEF &gt;= 0</c>, and
    /// <c>0.0 &lt; EnrageThreshold &lt; 1.0</c>. The same four invariants are the
    /// database check constraints (<c>BossDefinitionConfiguration</c>), so a row
    /// written before a constraint existed can still never be applied.
    /// </summary>
    private static void ValidateBalance(string bossIdentity, BossBalanceValues balance)
    {
        if (balance.MaxHP <= 0)
        {
            throw InvalidBalance(bossIdentity, "MaxHP", balance.MaxHP, "MaxHP > 0");
        }

        if (balance.ATK < 0)
        {
            throw InvalidBalance(bossIdentity, "ATK", balance.ATK, "ATK >= 0");
        }

        if (balance.DEF < 0)
        {
            throw InvalidBalance(bossIdentity, "DEF", balance.DEF, "DEF >= 0");
        }

        // Written as the documented invariant in its positive form, not as the
        // negation of it: `!(x > 0.0 && x < 1.0)` also rejects NaN, which a
        // `double precision` column can hold and which satisfies neither bound. The
        // database constraint has the same effect, because PostgreSQL orders NaN
        // above every other value.
        if (!(balance.EnrageThreshold > 0.0 && balance.EnrageThreshold < 1.0))
        {
            throw InvalidBalance(
                bossIdentity,
                "EnrageThreshold",
                balance.EnrageThreshold,
                "0.0 < EnrageThreshold < 1.0");
        }
    }

    /// <summary>
    /// The one fail-closed shape for a corrupt balance value: it names the Boss,
    /// the column, the stored value, and the violated invariant, and it is an
    /// <see cref="InvalidOperationException"/> so it surfaces as a server-side
    /// failure rather than a client rejection (<c>ADR-024</c> §2.1 item 3).
    /// </summary>
    private static InvalidOperationException InvalidBalance(
        string bossIdentity,
        string column,
        object storedValue,
        string invariant) =>
        new(
            $"The provisioned BossDefinition row for Identity '{bossIdentity}' carries an invalid "
            + $"balance value: {column} = {storedValue}. The documented invariant is {invariant} "
            + "(ADR-024). Boss creation fails closed rather than applying a corrupt value.");

    /// <summary>
    /// The four runtime balance values of one <c>BossDefinition</c> row
    /// (<c>DATABASE.md</c> §1, <c>ADR-024</c> §2.2) — the projection
    /// <see cref="GetDefinitionByIdentityAsync"/> reads, and nothing else from the
    /// row.
    /// </summary>
    private sealed record BossBalanceValues(
        int MaxHP,
        int ATK,
        int DEF,
        double EnrageThreshold);
}
