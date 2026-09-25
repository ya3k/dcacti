using GameServer.Domain.Cards;
using GameServer.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Postgres.Configurations;

/// <summary>
/// The persistence mapping of <see cref="PlayerUnlockedCard"/>
/// (<c>DATABASE.md</c> §1, §2, §4; ADR-012 item 9).
///
/// <code>
/// PlayerUnlockedCard
/// ├── PlayerId          (FK → Player)
/// └── CardDefinitionId  (FK → CardDefinition)
/// </code>
///
/// <b>Ownership only — no equip column and no equip table.</b>
/// <c>DATABASE.md</c> §2 states "Battle equip of Cards is <b>not</b> persisted
/// here; it is battle-scoped and snapshotted into <c>PetState.EquippedCards[]</c>
/// at <c>POST /api/battle/start</c>". This mapping therefore adds no loadout,
/// slot, or "equipped" column, and no <c>CardLoadout</c>/<c>EquippedCard</c>
/// table is introduced.
///
/// <b>It is an unlock flag, not an inventory row.</b> There is no quantity,
/// count, level, tier, star, or acquisition column, because MVP Cards have no
/// progression and an instance table is unnecessary (ADR-012 item 9), and
/// because <c>CARD_RULES.md</c> §1 item 3 fixes ownership at "one unlock row per
/// (<c>Player</c>, <c>CardDefinition</c>) regardless of allowed copies".
///
/// <b>The key is the composite (PlayerId, CardDefinitionId).</b>
/// <c>DATABASE.md</c> §1 lists exactly those two columns and declares no
/// surrogate id, so the pair is the natural key and is also what makes the
/// documented one-row-per-(Player, Definition) ownership shape an enforced
/// invariant rather than a convention. That uniqueness is on the
/// <b>unlock</b>, not on the loadout: it does not constrain how many times a
/// loadout may select the definition, which
/// <c>CardDefinition.LoadoutCopyLimit</c> governs separately
/// (<c>CARD_RULES.md</c> §1 items 1–3).
///
/// <b>The declared index is the documented one.</b> <c>DATABASE.md</c> §4 lists
/// <c>PlayerUnlockedCard(PlayerId)</c> — "list a player's unlocked Cards". No
/// index is declared on the <c>CardDefinitionId</c> FK:
/// <c>GameDbContext.ConfigureConventions</c> removes
/// <c>ForeignKeyIndexConvention</c> (the TASK-024 behavior), so no implicit
/// <c>IX_PlayerUnlockedCard_CardDefinitionId</c> is scaffolded.
/// </summary>
public sealed class PlayerUnlockedCardConfiguration : IEntityTypeConfiguration<PlayerUnlockedCard>
{
    public void Configure(EntityTypeBuilder<PlayerUnlockedCard> builder)
    {
        builder.ToTable("PlayerUnlockedCard");

        // DATABASE.md §1: the unlock row is exactly (PlayerId,
        // CardDefinitionId) with no surrogate key, so the pair is the primary
        // key and is what enforces one unlock row per (Player, CardDefinition)
        // (CARD_RULES.md §1 item 3).
        builder.HasKey(unlockedCard => new
        {
            unlockedCard.PlayerId,
            unlockedCard.CardDefinitionId,
        });

        // DATABASE.md §1/§2: PlayerId FK → Player (collection ownership —
        // ADR-011 item 4, ADR-012 item 9). Restrict keeps an unlock from being
        // deleted as a side effect of a Player delete without an explicit
        // cascade decision, matching the Pet and Relic mappings.
        builder.Property(unlockedCard => unlockedCard.PlayerId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasOne<Player>()
            .WithMany()
            .HasForeignKey(unlockedCard => unlockedCard.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        // DATABASE.md §1/§2: CardDefinitionId FK → CardDefinition
        // (PlayerUnlockedCard N ── 1 CardDefinition). Restrict: an unlock
        // cannot outlive the static content it references without an explicit
        // cascade decision.
        builder.Property(unlockedCard => unlockedCard.CardDefinitionId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasOne<CardDefinition>()
            .WithMany()
            .HasForeignKey(unlockedCard => unlockedCard.CardDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // No index is declared on the CardDefinitionId FK: DATABASE.md §4 lists
        // exactly one PlayerUnlockedCard index — PlayerUnlockedCard(PlayerId) —
        // and ForeignKeyIndexConvention is removed by GameDbContext, so no
        // implicit FK index is scaffolded.

        // DATABASE.md §4: PlayerUnlockedCard(PlayerId) — list a player's
        // unlocked Cards and serve the loadout ownership check. The only
        // documented index for this table; no speculative extras are added.
        builder.HasIndex(unlockedCard => unlockedCard.PlayerId);
    }
}
