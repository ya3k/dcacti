namespace GameServer.Domain.Relics;

/// <summary>
/// Static Relic content — one row per MVP Relic (<c>DATABASE.md</c> §1,
/// <c>RELIC_RULES.md</c> §1, §6).
///
/// <code>
/// RelicDefinition
/// ├── RelicDefinitionId  (PK)
/// ├── Name
/// ├── Trigger
/// ├── Condition
/// └── EffectDefinition
/// </code>
///
/// <b>Definition and instance are different things.</b> This type is the
/// static content shared by every owned copy; <see cref="Relic"/> is a
/// Player's owned instance that references this definition
/// (<c>DATABASE.md</c> §2: Relic N ── 1 RelicDefinition). Ownership
/// (<c>PlayerId</c>, <c>AcquiredAt</c>) lives on the instance, not here.
///
/// <b>The four members are <c>RELIC_RULES.md</c> §1's Relic structure.</b>
/// §1 defines a Relic by its <c>Trigger</c> (one supported event/condition,
/// §3), optional <c>Condition</c>, the <c>Effect</c> applied when triggered,
/// and its <c>Reset/Cooldown</c>. <c>DATABASE.md</c> §1 names the stored
/// fields <c>Trigger</c>, <c>Condition</c>, and <c>EffectDefinition</c>.
///
/// <b>These are carried as identities/content references, not as resolved
/// effects.</b> The <c>Trigger</c> is the name of one supported trigger
/// (<c>RELIC_RULES.md</c> §3's closed list), the <c>Condition</c> is the
/// optional extra condition, and <c>EffectDefinition</c> is the effect
/// reference — exactly as <see cref="Pets.PetDefinition"/> carries a
/// <c>PassiveId</c>/<c>PassiveThreshold</c> pair rather than inlining effect
/// content. Relic trigger evaluation and effect resolution are <b>not</b>
/// implemented by this type or by TASK-027
/// (<c>RELIC_RULES.md</c> §4–§5; TASK-027 Scope).
///
/// <b>No per-instance state is declared.</b> No MVP Relic in
/// <c>RELIC_RULES.md</c> §6 carries charges, stacks, cooldowns, or duration
/// owned by the instance; §1 places <c>Reset/Cooldown</c> on the Relic's
/// definition. Nothing here adds such state.
/// </summary>
public class RelicDefinition
{
    /// <summary>
    /// The definition's identifier (<c>DATABASE.md</c> §1:
    /// <c>RelicDefinitionId</c> (PK)) — the FK every owned
    /// <see cref="Relic"/> instance references (<c>DATABASE.md</c> §2).
    ///
    /// It is the <b>static content</b> identity and is never the
    /// battle-loadout identity: the battle snapshot carries the owned
    /// instance's <see cref="Relic.RelicInstanceId"/>
    /// (<c>RELIC_RULES.md</c> §2.2).
    /// </summary>
    public required string RelicDefinitionId { get; init; }

    /// <summary>
    /// The Relic's display name (<c>DATABASE.md</c> §1: <c>Name</c>) — e.g.
    /// "Berserker Core", "Mana Crystal" (<c>RELIC_RULES.md</c> §6's MVP
    /// reference list). It is the stable name of the Relic, not an owned
    /// instance id.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The identity of the Relic's one primary Trigger
    /// (<c>DATABASE.md</c> §1: <c>Trigger</c>; <c>RELIC_RULES.md</c> §1,
    /// §3).
    ///
    /// A Relic declares <b>exactly one</b> primary Trigger drawn from §3's
    /// closed list ("A Relic must declare exactly one primary Trigger from
    /// this list"). New trigger types are a rule change and must go through
    /// <c>GAME_RULES.md</c> §20 before implementation — this type stores the
    /// identity only and defines no new trigger.
    ///
    /// It is stored as an identity reference, not as evaluated trigger state:
    /// whether and when the Trigger fires is <c>RELIC_RULES.md</c> §3–§4's
    /// concern and is not implemented here.
    /// </summary>
    public required string Trigger { get; init; }

    /// <summary>
    /// The Trigger's optional extra Condition (<c>DATABASE.md</c> §1:
    /// <c>Condition</c>; <c>RELIC_RULES.md</c> §1: "optional extra condition
    /// (e.g. 'Combo ≥ 3', 'HP &lt; 30%')").
    ///
    /// It is optional in the rule ("A Relic must declare exactly one primary
    /// Trigger from this list (plus an optional Condition, §1)"), so a Relic
    /// with no extra condition carries none. This is a content reference;
    /// condition evaluation is not implemented here.
    /// </summary>
    public string? Condition { get; init; }

    /// <summary>
    /// The passive modification applied when the Relic triggers
    /// (<c>DATABASE.md</c> §1: <c>EffectDefinition</c>;
    /// <c>RELIC_RULES.md</c> §1: "the passive modification applied when
    /// triggered").
    ///
    /// It is an effect <b>reference</b>, matching how
    /// <see cref="Pets.PetDefinition.PassiveId"/> references the Passive
    /// rather than inlining its effect. Resolving an effect is
    /// <c>RELIC_RULES.md</c> §4–§5's concern and is not implemented here;
    /// static-modifier Relics such as "Burning Curse" (§6 note 1) apply
    /// during a COMBAT_RULES.md calculation step, which is likewise out of
    /// this task's scope.
    /// </summary>
    public required string EffectDefinition { get; init; }
}
