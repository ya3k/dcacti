using GameServer.Domain.Battle;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;

namespace GameServer.Domain.Combat;

/// <summary>
/// The Damage Pipeline — <c>GAME_RULES.md</c> §17 steps 15–17 (Calculate Damage,
/// Apply Element Modifier, Apply Final Damage), whose full formula is
/// <c>COMBAT_RULES.md</c> §3.
///
/// <code>
/// 1. Base Damage        Attacker ATK (+ Skill/Card base value) + BaseDamagePool
/// 2. × Combo Modifier   GAME_RULES.md §5's table; Combo = 1 for a Boss attack
/// 3. × Element Modifier ELEMENT_RULES.md §2.2, by the resolved matchup
/// 4. × Other Modifiers  pass-through 1.00× in MVP (no Relic/Passive/Buff/Crit)
/// 5. − Defense          × ( K / (K + Defender.DEF) ), K = 100  (COMBAT §3.2)
/// 6. → Final Damage     truncated toward zero, minimum 0, applied to Target HP
/// </code>
///
/// <b>The order is fixed and is not an implementation choice.</b>
/// <c>COMBAT_RULES.md</c> §3.1: "Steps 1–6 must execute in this order for every
/// damage instance." The method below is written in that order so the code reads
/// as §3 does, and <c>GAME_RULES.md</c> §14's conceptual flow
/// (<c>Base → Combo → Other → Element → Defense → Final</c>) and §17's step
/// list describe the same order.
///
/// <b>It is a pure, deterministic function.</b> Every input is a parameter — the
/// attacker's stats, the transient pools, the Combo, the attacker's and
/// defender's Elements, and the defender's DEF. The type reads no state, holds no
/// mutable field, draws no RNG, reads no clock, and depends on no enumeration
/// order, so one set of inputs always produces one result
/// (<c>GAME_RULES.md</c> §17, <c>AGENTS.md</c> §11). In particular there is
/// <b>no Crit roll</b>: <c>PlayerState.Crit</c> is deliberately not read here,
/// because <c>COMBAT_RULES.md</c> §3.3's roll and its RNG infrastructure do not
/// exist in MVP and inventing one would introduce a second randomization
/// mechanism beside the documented server-seeded stream (<c>ADR-009</c>,
/// <c>AGENTS.md</c> §11).
///
/// <b>The transient pools are consumed here, not stored.</b>
/// <see cref="ResourceGeneration.BaseDamagePool"/> is Transient Resolution State
/// (<c>GAME_STATE.md</c> §3): it is read as step 1's input and remains on the
/// resolution's result. <see cref="ResourceGeneration.DefensePool"/> is
/// deliberately <b>not</b> read: <c>COMBAT_RULES.md</c> §3.2's formula consumes
/// the <i>defending target's</i> DEF, not the attacker's generated pool, and no
/// documented rule places the DEF pool in this pipeline. <c>PlayerState.DEF</c>
/// is likewise not read directly — a caller supplying it does so as the
/// defender's DEF, which is what §3.2 asks for.
///
/// <b>One damage instance per committed Swap, in each direction.</b>
/// <c>GAME_RULES.md</c> §17 places steps 15–17 once (Player→Boss) and step 18b/18c
/// once (Boss→Player), and MVP has no multi-hit and no Damage-over-Time
/// (<c>COMBAT_RULES.md</c> §5.3's Burn is a Status Effect and is not
/// implemented). This type therefore performs one instance per call; a stage that
/// needs several calls it several times.
///
/// <b>What it owns, and what it does not.</b> It owns the calculation and the
/// resulting target HP. It does <b>not</b> decide Victory or Defeat
/// (<c>GAME_RULES.md</c> §1.4, §17 step 19 — the terminal checks belong to the
/// resolution), it fires no Boss Response (step 18), it transitions no Boss
/// State, and it applies no Shield (<c>COMBAT_RULES.md</c> §4 item 2 is not
/// implemented). HP reaching 0 is simply HP reaching 0; nothing here reads it as
/// an outcome.
///
/// This type is deliberately minimal and framework-independent
/// (<c>ARCHITECTURE.md</c> §2.1): it references no ASP.NET Core, SignalR, EF
/// Core, Redis, HTTP, Phaser, or Discord concern.
/// </summary>
public static class DamagePipeline
{
    /// <summary>
    /// The Defense Mitigation constant <c>K</c> (<c>COMBAT_RULES.md</c> §3.2:
    /// <c>Mitigated Damage = Pre-Defense Damage × ( K / (K + DEF) )</c>, "where
    /// <c>K</c> is a tunable constant controlling how quickly DEF diminishes
    /// incoming damage. MVP default: <c>K = 100</c> (configuration)").
    ///
    /// It is a named constant here rather than a literal at the arithmetic site,
    /// which is what §3.2's "tunable constant" wording asks for: the value
    /// appears once, its documented meaning is stated with it, and the formula
    /// below reads as §3.2 writes it. It is a <c>const</c> and not a parameter
    /// because §3.2 gives no caller-supplied configuration path for it, unlike
    /// the Element factors (<c>ELEMENT_RULES.md</c> §2.2) and the Combo table
    /// (<c>GAME_RULES.md</c> §5), which are passed in as
    /// <see cref="ElementModifiers"/> and <see cref="ComboModifiers"/>.
    /// </summary>
    public const int DefenseMitigationConstant = 100;

    /// <summary>
    /// The MVP value of the "Other Modifiers" stage — <c>COMBAT_RULES.md</c> §3
    /// step 4 (<c>COMBAT_RULES.md</c> §3.1 makes step 4 a stage the pipeline
    /// always runs).
    ///
    /// <b><c>1.00</c> is the identity factor, and step 4 is a pass-through in
    /// MVP.</b> §3 step 4 lists Relic bonuses, Passive bonuses, Buffs/Debuffs,
    /// and the Crit multiplier. None is implemented: <c>RELIC_RULES.md</c>,
    /// <c>PASSIVE_RULES.md</c> §8's damage effects, <c>COMBAT_RULES.md</c> §5's
    /// Buff/Debuff status effects, and §3.3's Crit roll are all separate
    /// unimplemented systems. Multiplying by this constant therefore leaves the
    /// value unchanged, and the stage is present, named, and reported in
    /// <see cref="DamageCalculation.OtherModifiers"/> so the breakdown and the
    /// pipeline keep the step rather than silently skipping it.
    ///
    /// <b>No extension framework is introduced for it.</b> The extension point is
    /// this stage in <see cref="Calculate"/>: the stage that implements a Relic,
    /// Passive, Buff, or Crit factor replaces the identity here with that factor
    /// (<c>AGENTS.md</c> §9 — a modifier chain, registry, or interface for zero
    /// current modifiers would be speculative machinery).
    /// </summary>
    public const double NoOtherModifiers = 1.00;

    /// <summary>
    /// The damage-relevant inputs of one damage instance — the values steps 1–5
    /// read (<c>COMBAT_RULES.md</c> §3).
    ///
    /// <b>It is a parameter group, not a new state type.</b> Every member is an
    /// existing documented value supplied by the caller:
    /// <c>PlayerState.ATK</c> and <c>PlayerState.Combo</c>
    /// (<c>GAME_STATE.md</c> §2.2), <c>ResourceGeneration.BaseDamagePool</c>
    /// (<c>GAME_STATE.md</c> §3), <c>PetState.Element</c> (§2.3),
    /// <c>BossState.Defense</c> and <c>BossState.Element</c> (§2.4). This type
    /// stores no <c>BattleState</c>, no <c>PlayerState</c>, and no
    /// <c>BossState</c> — it is not a second representation of any of them
    /// (<c>GAME_STATE.md</c> §0 item 5); it is exactly the argument list of the
    /// calculation, named so the call site states which value fills which role.
    /// </summary>
    /// <param name="Attack">
    /// Step 1's persistent half — <c>PlayerState.ATK</c>, "the stat the Damage
    /// Pipeline's base damage is read from" (<c>COMBAT_RULES.md</c> §3 step 1,
    /// <c>PlayerState.ATK</c>).
    /// </param>
    /// <param name="BaseDamagePool">
    /// Step 1's transient half — the ATK Gems this Swap cleared converted to
    /// absolute damage points (<c>COMBAT_RULES.md</c> §3 step 1: "any
    /// ATK-Gem-generated damage pool <b>for this action</b>";
    /// <c>ResourceGeneration.BaseDamagePool</c>).
    /// </param>
    /// <param name="Combo">
    /// Step 2's selector — the Combo of the Swap whose damage instance this is
    /// (<c>GAME_STATE.md</c> §2.2, <c>GAME_RULES.md</c> §5).
    /// </param>
    /// <param name="AttackerElement">
    /// Step 3's attacking side — the active Pet's one Element
    /// (<c>ELEMENT_RULES.md</c> §5, <c>GAME_STATE.md</c> §2.3), or <c>null</c>
    /// for an elementless source, which §3 resolves as Neutral.
    /// </param>
    /// <param name="DefenderElement">
    /// Step 3's defending side — the Boss's one Element
    /// (<c>ELEMENT_RULES.md</c> §5, <c>GAME_STATE.md</c> §2.4), or <c>null</c>
    /// for an elementless target (§3 item 2).
    /// </param>
    /// <param name="DefenderDefense">
    /// Step 5's mitigation input — the defending target's DEF
    /// (<c>COMBAT_RULES.md</c> §3.2: <c>K / (K + DEF)</c>), which is
    /// <c>BossState.DEF</c> for Player→Boss damage and the active Pet's <c>DEF</c>
    /// for Boss→Player damage (<c>COMBAT_RULES.md</c> §3.2, §3.4).
    /// </param>
    /// <param name="DefenderHp">
    /// The defending target's current HP — the value Final Damage is applied to
    /// (<c>COMBAT_RULES.md</c> §3 step 6). It is <c>BossState.HP</c> for
    /// Player→Boss damage and <c>PlayerState.HP</c> for Boss→Player damage.
    ///
    /// <b>It is an input because the target's type differs by direction.</b>
    /// <c>COMBAT_RULES.md</c> §3.4's Boss damage instance writes
    /// <c>Player.HP</c>, which is a different state record from the
    /// <c>BossState</c> the Player→Boss instance writes. Passing the value in —
    /// rather than passing one of the two records — keeps this pipeline a single
    /// calculation over both directions instead of two overloads or a second
    /// pipeline. <see cref="DamageResult.TargetHp"/> carries the result back.
    /// </param>
    /// <param name="Source">
    /// Which party is dealing this damage — the <c>source</c> member of the
    /// <c>DamageDealt</c>/<c>DamageTaken</c> reports (<c>GAME_EVENTS.md</c> §2,
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.14). <see cref="DamageParty.Player"/> for
    /// Player→Boss (steps 15–17), <see cref="DamageParty.Boss"/> for Boss→Player
    /// (§3.4).
    /// </param>
    /// <param name="Target">
    /// Which party is receiving this damage — the <c>target</c> member of the same
    /// two reports. <see cref="DamageParty.Boss"/> for Player→Boss,
    /// <see cref="DamageParty.Player"/> for Boss→Player.
    /// </param>
    public readonly record struct DamageInputs(
        int Attack,
        int BaseDamagePool,
        int Combo,
        Element? AttackerElement,
        Element? DefenderElement,
        int DefenderDefense,
        int DefenderHp,
        DamageParty Source,
        DamageParty Target);

    /// <summary>
    /// Resolves one damage instance through the full pipeline and applies the
    /// Final Damage to the target's HP (<c>GAME_RULES.md</c> §17 steps 15–17,
    /// <c>COMBAT_RULES.md</c> §3).
    ///
    /// <code>
    /// Base        = ATK + BaseDamagePool                        §3 step 1
    /// AfterCombo  = Base × ComboFactor                          §3 step 2, §5
    /// AfterElem   = AfterCombo × ElementFactor                  §3 step 3, §2.2
    /// AfterOther  = AfterElem × 1.00                            §3 step 4 (MVP)
    /// Defense     = AfterOther × ( K / (K + DEF) )              §3.2
    /// FinalDamage = truncate(Defense)                           §3 step 6
    /// newHp       = max(target.HP − FinalDamage, 0)             §3 step 6, §1.4
    /// </code>
    ///
    /// <b>Truncation, not rounding.</b> <c>COMBAT_RULES.md</c> §3.2's worked
    /// value is <c>96 × (100 / 140) ≈ 68.57</c> and §3 step 6 turns it into the
    /// integer damage <c>68</c> — the fraction is discarded, which is truncation
    /// toward zero for the non-negative values this pipeline produces. Rounding
    /// to nearest would give <c>69</c> and is not what the documented example
    /// shows, so <see cref="Math.Truncate(double)"/> is used explicitly rather
    /// than relying on a cast's behaviour.
    ///
    /// <b>Final Damage has a floor of 0.</b> <c>COMBAT_RULES.md</c> §3 step 6:
    /// "→ Final Damage (minimum 0; cannot reduce HP-restoring effects negative)".
    /// The multiplicative steps cannot produce a negative from the non-negative
    /// inputs above, but the floor is applied explicitly at the point the value
    /// becomes damage, so the documented minimum is enforced by the code that
    /// owns it rather than inferred from the inputs.
    ///
    /// <b>HP is clamped and never negative.</b> <c>GAME_RULES.md</c> §1.4 makes a
    /// battle end when the Boss or Player reaches <c>0</c> HP, so overkill stops
    /// at <c>0</c>: <c>500 − 600</c> leaves <c>0</c>, not <c>−100</c>.
    ///
    /// <b>Everything else on the Boss is carried across unchanged</b> — its
    /// identity, Element, MaxHP, ATK, DEF, and State. This stage applies damage
    /// and nothing else: no State transition, no Boss Response, no Victory or
    /// Defeat, and no HP ceiling.
    /// </summary>
    /// <param name="boss">
    /// The target's current state (<c>GAME_STATE.md</c> §2.4) — the source of
    /// <c>HP</c>, <c>DEF</c>, and <c>Element</c>. It is never mutated: it is an
    /// immutable value and the result is a new one.
    /// </param>
    /// <param name="inputs">
    /// The damage-relevant inputs of this instance (see
    /// <see cref="DamageInputs"/>).
    /// </param>
    /// <param name="comboModifiers">
    /// The <c>GAME_RULES.md</c> §5 table to read step 2's factor from. The
    /// default table is <see cref="ComboModifiers.Default"/>; it is supplied
    /// explicitly rather than reached for ambiently, so a resolution cannot be
    /// affected by another resolution's balance edit.
    /// </param>
    /// <param name="elementModifiers">
    /// The <c>ELEMENT_RULES.md</c> §2.2 factors to read step 3's factor from. The
    /// default set is <see cref="ElementModifiers.Default"/>, supplied explicitly
    /// for the same reason.
    /// </param>
    /// <b>Both directions reach this one method.</b> <c>COMBAT_RULES.md</c> §3.4
    /// makes the Boss's Basic Attack and Skill "the same Damage Pipeline (steps
    /// 1–6)" as the player's, so Player→Boss and Boss→Player are one calculation
    /// parameterized by <paramref name="inputs"/>'s source/target and by the
    /// attacker/defender values the caller supplies. The steps below are written
    /// once and are never duplicated per direction.
    ///
    /// <b>Everything else the target owns is the caller's to carry across.</b> The
    /// pipeline returns only the updated HP
    /// (<see cref="DamageResult.TargetHp"/>); the caller writes it onto whichever
    /// state it owns — <c>BossState</c> or <c>PlayerState</c> — in the same single
    /// post-resolution write-back (<c>GAME_STATE.md</c> §5.1). This stage
    /// transitions no State, fires no Boss mechanic, and decides no outcome.
    /// </summary>
    /// <param name="inputs">
    /// The damage-relevant inputs of this instance (see
    /// <see cref="DamageInputs"/>), including the direction and the defending
    /// target's current HP.
    /// </param>
    /// <param name="comboModifiers">
    /// The <c>GAME_RULES.md</c> §5 table to read step 2's factor from. The
    /// default table is <see cref="ComboModifiers.Default"/>; it is supplied
    /// explicitly rather than reached for ambiently, so a resolution cannot be
    /// affected by another resolution's balance edit.
    /// </param>
    /// <param name="elementModifiers">
    /// The <c>ELEMENT_RULES.md</c> §2.2 factors to read step 3's factor from. The
    /// default set is <see cref="ElementModifiers.Default"/>, supplied explicitly
    /// for the same reason.
    /// </param>
    /// <returns>
    /// The <c>DamageCalculated</c> breakdown, the target's post-damage HP, and the
    /// <c>DamageDealt</c>/<c>DamageTaken</c> reports (<c>GAME_EVENTS.md</c> §2).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="inputs"/> carries a Combo below
    /// <see cref="ComboModifiers.LowestCombo"/>, for which <c>GAME_RULES.md</c> §5
    /// defines no factor, or a DEF the formula cannot price
    /// (<c>K + DEF ≤ 0</c>, below the division's domain).
    /// </exception>
    public static DamageResult Calculate(
        DamageInputs inputs,
        ComboModifiers comboModifiers,
        ElementModifiers elementModifiers)
    {
        // §3 step 1 — Base Damage. COMBAT_RULES.md §3 step 1 names three
        // contributions: "ATK stat, Skill/Card base value, and any
        // ATK-Gem-generated damage pool for this action". The caller supplies the
        // first two summed in `Attack` — for a Swap that is PlayerState.ATK alone,
        // and for a Boss Skill it is Boss.ATK + SkillBaseDamage (§3.4, §6.3) — and
        // the transient pool separately. No term is invented here and none is
        // dropped: this step only sums what the direction's caller already
        // determined.
        var baseDamage = inputs.Attack + inputs.BaseDamagePool;

        // §3 step 2 — Combo Modifier. GAME_RULES.md §5's table, selected by the
        // Swap's Combo. The factor is applied as its rational numerator over
        // ComboModifiers.Denominator, so the multiplication is exact for every
        // documented factor and no floating-point error enters before step 5.
        //
        // A Boss attack passes Combo = 1: §3.4 states "Combo Modifier = 1 (Boss
        // attacks are not part of a Combo chain)", and §5's Combo 1 row is 1.00×.
        // The caller states that; no branch on direction is needed here.
        var comboNumerator = comboModifiers.NumeratorFor(inputs.Combo);
        var afterCombo = (baseDamage * comboNumerator) / ComboModifiers.Denominator;

        // §3 step 3 — Element Modifier. ELEMENT_RULES.md §2.1 resolves the
        // matchup and §2.2 assigns its factor; §3 resolves an elementless
        // attacker (null) as Neutral, which ElementMatchups owns. Neither rule is
        // re-implemented here: this step only multiplies the resolved factor in.
        var matchup = ElementMatchups.Resolve(inputs.AttackerElement, inputs.DefenderElement);
        var elementModifier = elementModifiers.For(matchup);
        var afterElement = afterCombo * elementModifier;

        // §3 step 4 — Other Modifiers. COMBAT_RULES.md §3.1 makes step 4 a stage
        // the pipeline always has, so it runs here even though MVP has no Relic,
        // Passive damage bonus, Buff/Debuff, or Crit multiplier to contribute
        // (§3.3, §3.4's "Other Modifiers = 1.0" for the Boss side, RELIC_RULES.md,
        // COMBAT_RULES.md §5 are separate unimplemented systems). The identity
        // factor is applied and reported, so the stage is visible in the breakdown
        // rather than skipped.
        var afterOtherModifiers = afterElement * NoOtherModifiers;

        // §3 step 5 / §3.2 — Defense Mitigation:
        //     Mitigated Damage = Pre-Defense Damage × ( K / (K + DEF) )
        // The DEF read is the DEFENDING TARGET's, which is what §3.2 means by
        // "target DEF" — BossState.DEF for Player→Boss damage and the active Pet's
        // DEF for Boss→Player damage (§3.2, §3.4). The divisor is guarded so the
        // division's domain is explicit: there is no "zero defense" special case
        // to encode — at DEF = 0 the factor is exactly K/K = 1 and the step is a
        // no-op by the formula, not by a branch.
        var mitigationDivisor = DefenseMitigationConstant + inputs.DefenderDefense;
        if (mitigationDivisor <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inputs),
                inputs.DefenderDefense,
                $"COMBAT_RULES.md §3.2's mitigation factor K / (K + DEF) requires "
                + $"K + DEF > 0 (K = {DefenseMitigationConstant}); this DEF is "
                + "outside the formula's domain.");
        }

        var defense = afterOtherModifiers
            * ((double)DefenseMitigationConstant / mitigationDivisor);

        // §3 step 6 — Final Damage. "minimum 0", and the fraction §3.2's worked
        // example leaves (68.57) is truncated toward zero to 68, which is the
        // integer §3.2 states. No rounding to nearest, and no rounding of the
        // reported Defense value either: the breakdown reports the formula's own
        // result and only the damage is an integer.
        var finalDamage = (int)Math.Truncate(defense);
        if (finalDamage < 0)
        {
            finalDamage = 0;
        }

        // §3 step 6 / §17 step 17 / GAME_RULES.md §1.4 — apply the Final Damage to
        // the target's HP, clamped so HP never becomes negative: overkill stops at
        // 0. Only the HP is returned; the caller writes it onto the state record it
        // owns, so this type cannot accidentally transition the Boss's State,
        // rewrite a stat, or reach into a PlayerState it does not model.
        var updatedHp = inputs.DefenderHp - finalDamage;
        if (updatedHp < 0)
        {
            updatedHp = 0;
        }

        var calculation = new DamageCalculation(
            Base: baseDamage,
            ComboModifier: (double)comboNumerator / ComboModifiers.Denominator,
            ElementModifier: elementModifier,
            OtherModifiers: NoOtherModifiers,
            Defense: defense,
            FinalDamage: finalDamage);

        // GAME_EVENTS.md §2 gives DamageDealt and DamageTaken identical members,
        // so both are built from the same source/target/amount — the direction the
        // caller stated, not a hardcoded one. §3.2.14 item 3 records that the
        // Player→Boss instance reports source="player"/target="boss" and the
        // Boss→Player instance (BOSS_RULES.md §4) reports source="boss"/
        // target="player", which is exactly what these two carry. The target's HP
        // is reduced once — above — and these two reports describe that one
        // application rather than applying it a second time.
        var dealt = new DamageDealtEvent(
            inputs.Source,
            inputs.Target,
            finalDamage);

        var taken = new DamageTakenEvent(
            inputs.Source,
            inputs.Target,
            finalDamage);

        return new DamageResult(
            calculation,
            updatedHp,
            dealt,
            taken);
    }
}
