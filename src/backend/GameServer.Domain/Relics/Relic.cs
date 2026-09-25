namespace GameServer.Domain.Relics;

/// <summary>
/// A Player's owned instance of a Relic (<c>DATABASE.md</c> §1, §2,
/// <c>RELIC_RULES.md</c> §2, ADR-012 item 7).
///
/// <code>
/// Relic
/// ├── RelicInstanceId    (PK)
/// ├── PlayerId           (FK → Player — collection ownership)
/// ├── RelicDefinitionId  (FK → RelicDefinition)
/// └── AcquiredAt
/// </code>
///
/// <b>Ownership, not equipment.</b> The <c>PlayerId</c> FK is collection
/// ownership only (<c>DATABASE.md</c> §3, ADR-011 item 4, ADR-012 item 7):
/// the Player owns the instance, and ownership survives across battles. This
/// row carries <b>no</b> equip column and no loadout reference — equipment
/// is battle-scoped and selected at battle start
/// (<c>RELIC_RULES.md</c> §2 item 1; <c>API_CONTRACTS.md</c> §3). There is
/// deliberately no <c>PetRelicLoadout</c>/<c>EquippedRelic</c> table
/// (<c>DATABASE.md</c> §2: "There is likewise no persistent Relic-equip
/// table"; ADR-012 item 7).
///
/// <b>Ownership is instance-based.</b> <see cref="RelicInstanceId"/> is the
/// identity of this owned copy; <see cref="RelicDefinitionId"/> is the static
/// content it is a copy of. The two are distinct and are never collapsed
/// (<c>RELIC_RULES.md</c> §2.2): the battle snapshot carries the
/// <b>instance</b> identity, because ownership, and therefore the loadout
/// validation, is per instance (<c>RELIC_RULES.md</c> §2.1 item 2).
///
/// <b>Two instances of one definition are two rows here, and that is
/// intentional.</b> <c>RELIC_RULES.md</c> §2.4 item 3 permits two distinct
/// owned instances that reference the same <see cref="RelicDefinitionId"/>
/// to be equipped simultaneously, so there is no uniqueness constraint on
/// <c>PlayerId + RelicDefinitionId</c>. The rule constrains instance
/// identity, never definition identity.
///
/// <b>No per-instance state.</b> No MVP Relic in <c>RELIC_RULES.md</c> §6
/// carries instance-owned state (no charges, stacks, cooldowns, or
/// duration), so this row adds none. The Relic's Trigger, Condition, and
/// Effect are its definition and are read through
/// <see cref="RelicDefinitionId"/>, not duplicated here.
/// </summary>
public class Relic
{
    /// <summary>
    /// The owned instance's identifier (<c>DATABASE.md</c> §1:
    /// <c>RelicInstanceId</c> (PK)) — the identity the battle loadout
    /// references and the value <c>PetState.EquippedRelics[]</c> carries
    /// (<c>RELIC_RULES.md</c> §2.2, <c>GAME_STATE.md</c> §2.3).
    ///
    /// It is distinct from <see cref="RelicDefinitionId"/>, which identifies
    /// the static content this instance is a copy of
    /// (<c>RELIC_RULES.md</c> §2.2 item 1). The two are never collapsed.
    /// </summary>
    public required string RelicInstanceId { get; init; }

    /// <summary>
    /// The owning Player (<c>DATABASE.md</c> §1: <c>PlayerId</c> (FK →
    /// Player), §2: Player 1 ── N Relic). Collection ownership only
    /// (ADR-011 item 4, ADR-012 item 7) — not an equip slot, and not a
    /// combat-stat holder.
    ///
    /// Loadout validation reads this column to confirm every selected
    /// instance belongs to the requesting Player (<c>RELIC_RULES.md</c> §2.1
    /// item 2); the server never trusts a client-supplied ownership claim.
    /// </summary>
    public required string PlayerId { get; init; }

    /// <summary>
    /// The definition this instance is a copy of (<c>DATABASE.md</c> §1:
    /// <c>RelicDefinitionId</c> (FK → RelicDefinition), §2: Relic N ── 1
    /// RelicDefinition). Trigger, Condition, and Effect are read through this
    /// reference, not duplicated on the instance
    /// (<c>GAME_STATE.md</c> §0 item 5 — no parallel representation).
    ///
    /// Several owned instances may reference the same definition; that is
    /// permitted and carries no uniqueness rule
    /// (<c>RELIC_RULES.md</c> §2.4 item 3).
    /// </summary>
    public required string RelicDefinitionId { get; init; }

    /// <summary>
    /// When the Player acquired this instance (<c>DATABASE.md</c> §1:
    /// <c>AcquiredAt</c>) — a creation timestamp, set once.
    ///
    /// It is <b>not</b> a loadout ordering key. Equip slot order is the
    /// submitted <c>relicLoadout</c> array position, and sorting by
    /// acquisition order is explicitly prohibited
    /// (<c>RELIC_RULES.md</c> §2.3 item 2).
    /// </summary>
    public DateTimeOffset AcquiredAt { get; init; }
}
