namespace GameServer.Domain.Relics;

/// <summary>
/// Which entity a <see cref="RelicEffectDefinition"/> modifies — the
/// <c>target</c> member <c>RELIC_RULES.md</c> §8.3 defines.
///
/// <code>
/// Pet   the active Pet
/// </code>
///
/// <b><see cref="Pet"/> is the only defined value</b> (§8.3 item 1): "The defined
/// value is <c>Pet</c>: §3 fixes the active Pet as the trigger subject, and §2
/// item 1 fixes Relics as carried by the active Pet." A second target is a
/// gameplay rule no document authors (<c>AGENTS.md</c> §7).
///
/// <b>A combination not listed in §8.3's table is not defined and may not be
/// inferred</b> (§8.3's own closing rule and item 2). The table fixes
/// <c>target</c> as <c>Pet</c> for all four defined <c>effectType</c> values, so
/// this enum is deliberately minimal: the allowed combination is what the
/// <c>effectType</c> fixes, not something this member varies.
///
/// <b>It stores a target; it applies nothing.</b> No effect is applied to any
/// entity by this type (<c>RELIC_RULES.md</c> §8.7 — effect application is NOT
/// IMPLEMENTED).
///
/// <b>Member order carries no documented meaning</b> — the persisted contract
/// stores the member name, never this ordinal.
/// </summary>
public enum RelicEffectTarget
{
    /// <summary>
    /// The active Pet (<c>RELIC_RULES.md</c> §8.3 item 1, §3, §2 item 1) — the
    /// only target §8.3 defines, and the target of every provisioned row.
    /// </summary>
    Pet = 0,
}
