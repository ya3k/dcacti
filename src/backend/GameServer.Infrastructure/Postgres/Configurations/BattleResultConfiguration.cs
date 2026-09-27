using GameServer.Domain.Battle;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Postgres.Configurations;

/// <summary>
/// The persistence mapping of <see cref="BattleResult"/>
/// (<c>DATABASE.md</c> §1, §2, §3, §4).
///
/// <code>
/// BattleResult
/// ├── BattleResultId   (PK — the battle's own BattleId; one row per battle)
/// ├── PlayerId         (FK → Player)
/// ├── PetInstanceId    (FK → Pet)
/// ├── BossDefinitionId (FK → BossDefinition)
/// ├── Outcome          ("victory" | "defeat")
/// ├── DurationTurns
/// ├── CompletedAt
/// └── RewardSummary    (JSON)
/// </code>
///
/// <b>The field set is <c>DATABASE.md</c> §1's and is closed.</b> No
/// <c>Status</c>, battle-state snapshot, winner/loser id, extra turn count,
/// <c>Sequence</c>, RNG state, or board column exists: §1 defines exactly the
/// eight values above, and a battle's outcome is expressed as the
/// <c>BattleWon</c>/<c>BattleLost</c> events rather than as a lifecycle field
/// (<c>GAME_STATE.md</c> §2.0.3).
///
/// <b>The primary key is not database-generated.</b> §1 sourcing item 1 states
/// <c>BattleResultId</c> <i>is</i> the battle's own <c>BattleId</c> — "no second
/// identifier is introduced and no second row can exist". It is therefore mapped
/// as a bounded required string with no default and no store-generated behavior,
/// exactly as <c>BossDefinition.BossDefinitionId</c> is (TASK-049). That
/// primary-key uniqueness is the at-most-one-row guarantee
/// <c>REDIS_STATE.md</c> §3 relies on; no separate idempotency column or
/// mechanism is added.
///
/// <b>The three foreign keys are the documented relationships.</b> §1 lists
/// <c>PlayerId (FK → Player)</c>, <c>PetInstanceId (FK → Pet)</c>, and
/// <c>BossDefinitionId (FK → BossDefinition)</c>; §2 records
/// <c>Player 1 ── N BattleResult</c>, <c>BattleResult N ── 1 Pet</c>, and
/// <c>BattleResult N ── 1 BossDefinition</c>. Each is configured
/// <c>Restrict</c>, matching the sibling tables' convention: the relationship
/// shape is documented, no cascade rule is, and an explicit decision would be
/// required before deleting a Player could silently erase their battle history.
///
/// <b><c>Outcome</c> stores the contract values.</b> §1 spells the column values
/// <c>"victory" | "defeat"</c> — the value set owned by <c>GAME_EVENTS.md</c>
/// §2 — so the Domain enum is converted through
/// <see cref="BattleOutcomes"/> rather than written as its C# identifier. The
/// column is a required bounded string, so a row can never hold a third value
/// the contract does not define.
///
/// <b><c>RewardSummary</c> is JSON and NOT NULL.</b> §1 makes the staging value
/// the empty object <c>{}</c> — "always present, never absent" — so the column
/// is required and stores the document the caller supplies. The member list is
/// TASK-033's; nothing here defines, defaults, or validates one.
///
/// <b><c>CompletedAt</c> is the server's own instant.</b> §1 "Duration and
/// completion sourcing" item 2 makes it the server clock reading captured when
/// the durable result is written and asserts no timezone; the column stores the
/// <c>DateTimeOffset</c> the caller captured, with no default and no
/// store-generated value that could stand in for a client-supplied one.
///
/// <b>One index, and it is §4's.</b> §4 lists
/// <c>BattleResult(PlayerId, CompletedAt DESC)</c> — "battle history, most
/// recent first" — and states no further index is specified. No speculative
/// index is declared.
/// </summary>
public sealed class BattleResultConfiguration : IEntityTypeConfiguration<BattleResult>
{
    public void Configure(EntityTypeBuilder<BattleResult> builder)
    {
        builder.ToTable("BattleResult");

        // DATABASE.md §1 sourcing item 1: BattleResultId IS the battle's own
        // BattleId — the row's key is server-authored by the battle it records,
        // never database-generated, and its uniqueness is what makes a second
        // row for one battle impossible (REDIS_STATE.md §3).
        builder.HasKey(result => result.BattleResultId);

        builder.Property(result => result.BattleResultId)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1/§2: PlayerId (FK → Player) — the owning account,
        // copied from BattleState.PlayerId at battle end.
        builder.Property(result => result.PlayerId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasOne<GameServer.Domain.Players.Player>()
            .WithMany()
            .HasForeignKey(result => result.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        // DATABASE.md §1/§2: PetInstanceId (FK → Pet) — the owned Pet INSTANCE
        // that fought the battle, copied from BattleState.PetState.PetId. Never a
        // PetDefinitionId (GAME_STATE.md §2.3).
        builder.Property(result => result.PetInstanceId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasOne<GameServer.Domain.Pets.Pet>()
            .WithMany()
            .HasForeignKey(result => result.PetInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        // DATABASE.md §1/§2: BossDefinitionId (FK → BossDefinition) — the
        // persistence key resolved by the documented Identity lookup
        // (DATABASE.md §1 note item 2). The FK is satisfiable only by a real
        // provisioned row (TASK-053): an unresolved definition fails the
        // battle-end write closed rather than writing anything here
        // (DATABASE.md §1 sourcing item 3).
        builder.Property(result => result.BossDefinitionId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasOne<GameServer.Domain.Bosses.BossDefinition>()
            .WithMany()
            .HasForeignKey(result => result.BossDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // DATABASE.md §1: Outcome — the documented value set ("victory" |
        // "defeat", GAME_EVENTS.md §2), never the enum's identifier. The
        // conversion states the contract's spelling once so the stored value
        // cannot drift from the REST and wire vocabulary.
        builder.Property(result => result.Outcome)
            .HasConversion(
                outcome => BattleOutcomes.ToContractValue(outcome),
                value => BattleOutcomes.FromContractValue(value))
            .HasMaxLength(16)
            .IsRequired();

        // DATABASE.md §1 "Duration and completion sourcing" item 1: the
        // authoritative Turn at terminal resolution. A game value, not a
        // derived duration — the column stores exactly what the state held.
        builder.Property(result => result.DurationTurns)
            .IsRequired();

        // DATABASE.md §1 item 2: the server clock reading captured when this
        // durable result was written. No default and no store-generated value:
        // a timestamp the server did not capture would be a fabricated
        // completion instant.
        builder.Property(result => result.CompletedAt)
            .IsRequired();

        // DATABASE.md §1: RewardSummary — the JSON document, NOT NULL, holding
        // the documented staging value {} until TASK-033 owns its member list.
        builder.Property(result => result.RewardSummary)
            .HasColumnType("jsonb")
            .IsRequired();

        // DATABASE.md §4: the one documented BattleResult index —
        // "battle history, most recent first". The descending order is part of
        // the contract, so it is declared explicitly rather than left to the
        // provider's default ascending order.
        builder.HasIndex(result => new { result.PlayerId, result.CompletedAt })
            .HasDatabaseName("IX_BattleResult_PlayerId_CompletedAt")
            .IsDescending(false, true);
    }
}
