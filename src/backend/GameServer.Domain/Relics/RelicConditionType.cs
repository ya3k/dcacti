namespace GameServer.Domain.Relics;

/// <summary>
/// The form a Relic's structured <see cref="RelicCondition"/> takes — the
/// condition grammar <c>RELIC_RULES.md</c> §8.1 defines.
///
/// <code>
/// MatchCountAtLeast   the cumulative Match count reached N or more
/// ComboAtLeast        the current Chain's Combo reached N or more
/// HpPercentageBelow   the active Pet's HP fell below N percent
/// </code>
///
/// <b>These three forms are the whole grammar.</b> §8.1 defines exactly these
/// (TASK-131 <b>D5</b>); a fourth form is a gameplay rule no document authors,
/// and adding one would invent content (<c>AGENTS.md</c> §7). The set is closed
/// for the same reason <see cref="RelicEffectType"/> is.
///
/// <b>Every form carries its threshold as an integer, not in prose.</b> §8.1
/// item 1 states it directly: "The threshold is part of the value, never
/// embedded in prose — <c>"every 3 Matches"</c> as a string is not a valid
/// <c>Condition</c>." The threshold travels in
/// <see cref="RelicCondition.Threshold"/>, so no runtime has to parse a
/// sentence to learn what a Relic compares against.
///
/// <b>Nothing here evaluates anything.</b> §8.1 item 2 places evaluation at the
/// point <c>GAME_RULES.md</c> §17 step 11 executes, against the current
/// resolution state; §8.1 item 3 states that <b>no persistent Relic counters
/// are introduced</b> — <see cref="MatchCountAtLeast"/> reads the battle's
/// existing cumulative Match count and <see cref="ComboAtLeast"/> the existing
/// Combo value. This type names the form and carries its threshold; it reads no
/// state, compares nothing, and adds no member to <c>GAME_STATE.md</c>.
///
/// <b>Member order carries no documented meaning.</b> The numeric values are
/// storage identifiers and no rule derives from them; the persisted contract
/// stores the member <b>names</b>, never these ordinals (the same convention
/// <c>DATABASE.md</c> §1 item 2 fixes for the Card and Boss members). The order
/// below is a stable reading aid, and it follows §8.1's own listing order.
/// </summary>
public enum RelicConditionType
{
    /// <summary>
    /// The battle's cumulative Match count reached the threshold or more
    /// (<c>RELIC_RULES.md</c> §8.1; the condition §6's Berserker Core and Mana
    /// Crystal rows declare).
    ///
    /// It reads the <b>existing</b> cumulative Match count
    /// (<c>GAME_RULES.md</c> §5, <c>GAME_STATE.md</c> §2.4/§2.6) — §8.1 item 3
    /// introduces no Relic-owned counter. §8.1 item 7 adds that the form is a
    /// threshold, <b>not a modulo</b>: §8.1 authors no "fires only on the exact
    /// Nth Match" and no "fires on every multiple" rule.
    /// </summary>
    MatchCountAtLeast = 0,

    /// <summary>
    /// The current Chain's Combo reached the threshold or more
    /// (<c>RELIC_RULES.md</c> §8.1; the condition §6's Assassin Eye row
    /// declares).
    ///
    /// It reads the <b>existing</b> Combo value (<c>GAME_STATE.md</c> §2.4/§2.6);
    /// no Relic-owned counter is introduced (§8.1 item 3).
    /// </summary>
    ComboAtLeast = 1,

    /// <summary>
    /// The active Pet's HP fell below the threshold percent
    /// (<c>RELIC_RULES.md</c> §8.1; the condition §6's Emergency Core row
    /// declares).
    ///
    /// §8.1 item 6 fixes the <b>subject</b> — the active Pet, the subject §3
    /// fixes for the <c>OnHpBelow</c> trigger — and states that whether the
    /// comparison resolves against current HP or a maximum is a property of the
    /// percentage form itself, to be stated by the implementing stage against
    /// <c>GAME_STATE.md</c> §2.3's HP members. This type stores the form and the
    /// threshold; it fixes no reading of its own.
    /// </summary>
    HpPercentageBelow = 2,
}
