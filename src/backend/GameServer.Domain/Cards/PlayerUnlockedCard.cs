namespace GameServer.Domain.Cards;

/// <summary>
/// A Player's unlock of a Card definition (<c>DATABASE.md</c> §1–§2,
/// ADR-012 item 9).
///
/// <code>
/// PlayerUnlockedCard
/// ├── PlayerId          (FK → Player)
/// └── CardDefinitionId  (FK → CardDefinition)
/// </code>
///
/// <b>Ownership is an unlock flag, not an inventory row.</b> ADR-012 item 9
/// resolves Card ownership to this join table because "MVP Cards have no
/// Tier/Star/Level, so an instance table is unnecessary". One row means the
/// Player has unlocked that definition; it does <b>not</b> mean one owned copy,
/// and it carries no quantity, count, level, tier, star, or acquisition
/// timestamp — <c>DATABASE.md</c> §1 lists exactly these two columns and
/// nothing more may be added.
///
/// <b>There is exactly one row per (Player, CardDefinition) regardless of how
/// many copies a loadout may use.</b> <c>CARD_RULES.md</c> §1 item 3 states the
/// loadout copy limit "is not inventory quantity, ownership quantity, or a
/// collection limit: ownership remains one unlock row per (Player,
/// CardDefinition) regardless of allowed copies … and no Card instance exists
/// at any time". The two concepts are independent: a limit of 3 does not mean
/// three rows, and three rows are never required to select the same definition
/// three times.
///
/// <b>Ownership is read to validate a submitted loadout.</b> The server
/// establishes that every submitted <c>CardDefinitionId</c> has a row here for
/// the requesting Player (<c>API_CONTRACTS.md</c> §3 step 2) — from
/// persistence, never from a client-supplied ownership claim
/// (<c>GAME_RULES.md</c> §18, ADR-001).
///
/// <b>No equip column.</b> Which Cards are equipped is battle-scoped and is
/// snapshotted into <c>PetState.EquippedCards[]</c> at battle start; it is
/// never persisted (<c>DATABASE.md</c> §2: "Battle equip of Cards is not
/// persisted here"; ADR-012 item 10). This row therefore holds ownership only.
/// </summary>
public class PlayerUnlockedCard
{
    /// <summary>
    /// The owning Player (<c>DATABASE.md</c> §1: <c>PlayerId</c> (FK →
    /// Player), §2: Player 1 ── N PlayerUnlockedCard). Collection ownership
    /// only (ADR-011 item 4, ADR-012 item 9) — not an equip slot and not a
    /// combat-state holder.
    ///
    /// The loadout validator reads this column to confirm every submitted
    /// definition is unlocked by the requesting Player
    /// (<c>API_CONTRACTS.md</c> §3 step 2).
    /// </summary>
    public required string PlayerId { get; init; }

    /// <summary>
    /// The unlocked definition (<c>DATABASE.md</c> §1:
    /// <c>CardDefinitionId</c> (FK → CardDefinition), §2: PlayerUnlockedCard
    /// N ── 1 CardDefinition).
    ///
    /// Name, Category, PowerCost, EffectDefinition, and LoadoutCopyLimit are
    /// read through this reference and are never duplicated onto the unlock row
    /// (<c>GAME_STATE.md</c> §0 item 5 — no parallel representation).
    /// </summary>
    public required string CardDefinitionId { get; init; }
}
