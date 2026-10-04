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
    /// </summary>
    /// <param name="petState">The Pet's state before healing.</param>
    /// <param name="rawHeal">The raw heal amount.</param>
    /// <param name="bossStatusEffects">The Boss's active status effects for cross-entity read.</param>
    /// <returns>The updated PetState with clamped HP.</returns>
    public static PetState ApplyPetHeal(
        PetState petState,
        int rawHeal,
        IReadOnlyList<StatusEffect>? bossStatusEffects)
    {
        var finalHeal = ResolveHealAmount(rawHeal, bossStatusEffects);
        var updatedHp = Math.Min(petState.HP + finalHeal, petState.MaxHP);
        return petState with { HP = updatedHp };
    }
}
