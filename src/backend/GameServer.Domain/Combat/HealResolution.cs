namespace GameServer.Domain.Combat;

using GameServer.Domain.Battle;

/// <summary>
/// Implements the shared Pet-scoped Heal Resolution step (<c>COMBAT_RULES.md</c> §4 item 7).
/// </summary>
public static class HealResolution
{
    /// <summary>
    /// The Status Effect identity for Thủy Ma's healing reduction
    /// (<c>BOSS_RULES.md</c> §6.2.2, §6.4).
    /// </summary>
    public const string ThuyMaHealingReductionId = "boss-thuy-ma-heal";

    /// <summary>
    /// Calculates the Final Heal Amount from Raw Heal and applicable heal modifiers
    /// (<c>COMBAT_RULES.md</c> §4 item 7).
    /// </summary>
    /// <param name="rawHeal">The heal amount before modifiers.</param>
    /// <param name="bossStatusEffects">The Boss's active status effects for cross-entity read.</param>
    /// <returns>The modified heal amount, before MaxHP clamp.</returns>
    public static int ResolveHealAmount(int rawHeal, IReadOnlyList<StatusEffect>? bossStatusEffects)
    {
        if (rawHeal <= 0)
        {
            return 0;
        }

        var heal = rawHeal;

        // COMBAT_RULES.md §4 item 7 / BOSS_RULES.md §6.2.2:
        // Cross-entity read: active "boss-thuy-ma-heal" on Boss reduces Raw Heal by 50%.
        if (bossStatusEffects != null)
        {
            for (var i = 0; i < bossStatusEffects.Count; i++)
            {
                var effect = bossStatusEffects[i];
                if (string.Equals(effect.Id, ThuyMaHealingReductionId, StringComparison.Ordinal))
                {
                    // Truncation toward zero in integers: truncate(rawHeal * (100 + Magnitude) / 100)
                    heal = (heal * (100 + (int)effect.Magnitude)) / 100;
                    break;
                }
            }
        }

        return Math.Max(0, heal);
    }

    /// <summary>
    /// Applies the shared Heal Resolution step to a Pet's HP (<c>COMBAT_RULES.md</c> §4 items 1, 7).
    ///
    /// <b>A dead Pet is not healed.</b> <c>GAME_RULES.md</c> §1.4 makes 0 HP
    /// terminal for the battle, and <c>COMBAT_RULES.md</c> §4 item 1 (and item 7's
    /// dead-target statement) forbid an ordinary healing source from restoring a
    /// target at <c>HP &lt;= 0</c>. When the Pet is already dead this returns it
    /// unchanged: no revival, no negative/zero-HP arithmetic, and no event
    /// (<c>GAME_EVENTS.md</c> §2 has no heal event). Every MVP healing source —
    /// the HP-Gem pool, a Heal Card, a Boss-side heal on the Boss's own pool —
    /// passes through here, so the guard is the single owner of the rule rather
    /// than a per-caller check.
    /// </summary>
    /// <param name="petState">The Pet's state before healing.</param>
    /// <param name="rawHeal">The raw heal amount.</param>
    /// <param name="bossStatusEffects">The Boss's active status effects for cross-entity read.</param>
    /// <returns>The updated PetState with clamped HP, or the unchanged PetState when it is dead.</returns>
    public static PetState ApplyPetHeal(
        PetState petState,
        int rawHeal,
        IReadOnlyList<StatusEffect>? bossStatusEffects)
    {
        // COMBAT_RULES.md §4 item 1 / §1.4: 0 HP is terminal, so a dead Pet is not
        // a valid healing target. Guard before the modifier read and the clamp so
        // no arithmetic is performed on a terminal state.
        if (petState.HP <= 0)
        {
            return petState;
        }

        var finalHeal = ResolveHealAmount(rawHeal, bossStatusEffects);
        var updatedHp = Math.Min(petState.HP + finalHeal, petState.MaxHP);
        return petState with { HP = updatedHp };
    }

    /// <summary>
    /// Applies the shared Heal Resolution step to a Boss's HP (<c>ADR-026</c> D2, <c>COMBAT_RULES.md</c> §4 item 1).
    ///
    /// <b>A dead Boss is not healed</b> — the same terminal-HP rule item 1 states
    /// for the Pet, applied to the Boss-side HP-Gem pool: when the Boss is already
    /// at <c>HP &lt;= 0</c> the state is returned unchanged. A Boss killed by the
    /// Player's attack is therefore never revived by its own step-18a HP Gems, and
    /// the step-8 terminal check that precedes the Boss Response
    /// (<c>BOSS_RULES.md</c> §5 item 4) is not contradicted.
    /// </summary>
    /// <param name="bossState">The Boss's state before healing.</param>
    /// <param name="rawHeal">The raw heal amount.</param>
    /// <returns>The updated BossState with clamped HP, or the unchanged BossState when it is dead.</returns>
    public static BossState ApplyBossHeal(
        BossState bossState,
        int rawHeal)
    {
        // COMBAT_RULES.md §4 item 1 / §1.4: 0 HP is terminal; a dead Boss is not a
        // valid healing target.
        if (bossState.HP <= 0)
        {
            return bossState;
        }

        if (rawHeal <= 0)
        {
            return bossState;
        }

        var updatedHp = Math.Min(bossState.HP + rawHeal, bossState.MaxHP);
        return bossState with { HP = updatedHp };
    }
}
