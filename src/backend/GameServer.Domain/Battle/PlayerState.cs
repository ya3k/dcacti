namespace GameServer.Domain.Battle;

/// <summary>
/// The player's battle progression state (<c>GAME_STATE.md</c> §2.2).
///
/// <code>
/// BattleState
/// └── PlayerState
///     ├── HP           (current health; COMBAT_RULES.md §1.1)
///     ├── MaxHP        (maximum health; COMBAT_RULES.md §1.1)
///     ├── ATK          (attack power; COMBAT_RULES.md §1.1)
///     ├── DEF          (defense; COMBAT_RULES.md §1.1)
///     ├── Power        (resource for casting Cards / Skills, 0–100;
///     │                 COMBAT_RULES.md §1.1, §6, GAME_RULES.md §12)
///     ├── Crit         (critical hit chance as a percentage;
///     │                 COMBAT_RULES.md §1.1, §3.3)
///     ├── Combo        (current Combo for the Swap that just resolved;
///     │                 GAME_RULES.md §5, MATCH3_RULES.md §6)
///     └── MatchCount   (cumulative Matches this battle; GAME_RULES.md §3)
/// </code>
///
/// <b>This is the documented owner, not a new decision.</b> <c>GAME_STATE.md</c>
/// §2.2 places every field above here, and §2 nests <c>PlayerState</c> inside
/// <c>BattleState</c>. All of them are therefore ordinary <b>Active Battle
/// State</b>: authoritative, server-produced, and written in the same single
/// post-resolution write-back as <c>Turn</c>, <c>Sequence</c>,
/// <c>BoardState</c>, and <c>RngState</c> (§5.1). There is no second
/// representation of any value — not on <c>BattleState</c>, not on
/// <c>BoardState</c>, and not on the transient <c>ResolutionContext</c> (§0
/// item 5).
///
/// <b>The combat stats are the MVP baseline configuration of
/// <c>COMBAT_RULES.md</c> §1.1, not permanent invariants.</b> §1.1 defines
/// <c>HP</c>/<c>MaxHP</c> = 1000, <c>ATK</c> = 50, <c>DEF</c> = 25, <c>Power</c>
/// in the range 0–100 (starting at 0), and <c>Crit</c> = 5%, and states
/// explicitly that these "are not permanent invariants — future Pet progression
/// (Level, Star, Tier) may produce different actual Battle Stats" and that
/// "changing balance values is a configuration change". The defaults are
/// therefore constants of the <b>initial</b> state only: nothing here reads them
/// as the current value of a running battle, and no formula is derived from
/// them. <c>Power</c>'s 0–100 range is likewise not enforced here: §1.1 and
/// <c>GAME_RULES.md</c> §12 own it as a documented invariant, and clamping on
/// every write would be the speculative machinery <c>ARCHITECTURE.md</c> §5
/// warns against before any Resource Generation exists to write the field.
///
/// <b>Only the fields this stage requires exist.</b> §2.2 also lists
/// <c>StatusEffects[]</c>, <c>EquippedRelics[]</c>, and <c>EquippedCards[]</c>.
/// Those belong to the Passive, Relic, and Card stages and are
/// <b>not yet implemented</b>, not <b>not required</b> (§0 item 4, §2.0.5.3):
/// each is added by its own owning task, exactly as this stage adds these.
///
/// <b>The combat stats exist in the state but are not delivered on the wire.</b>
/// <c>SIGNALR_PROTOCOL.md</c> §4.2 items 2–3 fix the <c>playerState</c> payload
/// member to <b>exactly</b> <c>combo</c> and <c>matchCount</c> and state that the
/// rest of §2.2 — <c>HP</c>/<c>MaxHP</c>, <c>ATK</c>/<c>DEF</c>/<c>Crit</c>, and
/// <c>Power</c> — "is <b>not</b> delivered, because §4 item 4 admits only the
/// implemented stage's own fields". This type does not change that: the fields
/// are authoritative state, exactly as <c>LastCommittedSwapPair</c> is
/// (§2.1.10 item 9, <c>SIGNALR_PROTOCOL.md</c> §4 item 12), and adding them here
/// adds no member to <c>BattleStateUpdated</c> and no message, method, or
/// subscription. Delivering them is a protocol change owned by its own task
/// (<c>SIGNALR_PROTOCOL.md</c> §4 item 4: "additional state is introduced by
/// extending <c>GAME_STATE.md</c> §2.0, not by the wire shape").
///
/// <b>No player identifier.</b> Neither §2 nor §2.2 declares one, and both are
/// closed field lists. MVP is exactly one player per battle, identified by the
/// battle (<c>BattleId</c>); a future multiplayer record is reached by
/// extending this container, not by inventing a field today
/// (<c>MVP_SCOPE.md</c> §3 lists multiplayer as FUTURE, not designed).
///
/// <b>No derived value is stored.</b> Nothing here is computed from
/// <c>Turn</c>, <c>Sequence</c>, the board, or the resolution: every field is
/// state, and none can be re-derived after the fact
/// (<c>GAME_STATE.md</c> §5.2 item 2).
/// </summary>
/// <param name="HP">
/// The player's current health (<c>COMBAT_RULES.md</c> §1.1,
/// <c>GAME_STATE.md</c> §2.2). It starts equal to <see cref="MaxHP"/> — a battle
/// begins at full health — and is the value the Damage Pipeline and healing
/// effects read (<c>COMBAT_RULES.md</c> §3, §4).
///
/// <b>Nothing in this type computes, clamps, or compares it.</b> The healing that
/// writes it is <c>COMBAT_RULES.md</c> §4 item 1's rule, applied by
/// <see cref="GameServer.Domain.Match3.ResourceGenerator.ApplyHeal"/> as
/// <c>GAME_RULES.md</c> §17 step 14 and clamped there to this field's
/// <c>MaxHP</c>. Damage and mitigation (§3) and Victory/Defeat are still
/// unimplemented and remain owned by their own stages.
/// </param>
/// <param name="MaxHP">
/// The player's maximum health (<c>COMBAT_RULES.md</c> §1.1,
/// <c>GAME_STATE.md</c> §2.2) — the ceiling heal effects restore up to
/// (<c>COMBAT_RULES.md</c> §4 item 1). Its documented MVP default is
/// <see cref="DefaultMaxHP"/>.
/// </param>
/// <param name="ATK">
/// The player's attack power (<c>COMBAT_RULES.md</c> §1.1,
/// <c>GAME_STATE.md</c> §2.2) — the stat the Damage Pipeline's base damage is
/// read from (<c>COMBAT_RULES.md</c> §3 step 1). Its documented MVP default is
/// <see cref="DefaultATK"/>.
/// </param>
/// <param name="DEF">
/// The player's defense (<c>COMBAT_RULES.md</c> §1.1,
/// <c>GAME_STATE.md</c> §2.2) — the value the mitigation formula consumes
/// (<c>COMBAT_RULES.md</c> §3.2). Its documented MVP default is
/// <see cref="DefaultDEF"/>.
/// </param>
/// <param name="Power">
/// The resource spent to cast Cards and Skills, in the documented range 0–100
/// (<c>COMBAT_RULES.md</c> §1.1, §6; <c>GAME_STATE.md</c> §2.2;
/// <c>GAME_RULES.md</c> §12).
///
/// A battle starts at <see cref="DefaultPower"/> = <c>0</c>, not at the cap:
/// Power is generated by Match-3 (<c>COMBAT_RULES.md</c> §2) and none has been
/// generated before the first Match. The range is <b>not</b> enforced by this
/// type; §1.1 and <c>GAME_RULES.md</c> §12 own it as a documented invariant,
/// and Resource Generation, the cap, and cast validation are unimplemented
/// (<c>COMBAT_RULES.md</c> §6, <c>CARD_RULES.md</c> §3).
/// </param>
/// <param name="Crit">
/// The critical hit chance as a <b>percentage</b> (<c>COMBAT_RULES.md</c> §1.1,
/// §3.3; <c>GAME_STATE.md</c> §2.2) — its documented MVP default is
/// <see cref="DefaultCrit"/> = <c>5</c>, the "5%" of §1.1.
///
/// It stays a percentage rather than becoming a <c>0.05</c> probability because
/// §1.1 defines the unit (Crit is "critical hit chance (%)") and every modifier
/// of it is expressed in the same unit — "increase Crit chance"
/// (<c>COMBAT_RULES.md</c> §3.3 item 3, <c>RELIC_RULES.md</c> §5's Assassin Eye,
/// <c>PASSIVE_RULES.md</c> §8's Bạch Hổ). Storing a fraction here would need
/// converting at every one of those boundaries and would invite the two
/// representations §0 item 5 forbids.
///
/// No Crit roll is performed here: §3.3 owns it and the Damage Pipeline is
/// unimplemented.
/// </param>
/// <param name="Combo">
/// The Combo of the most recently <b>committed</b> Swap
/// (<c>GAME_STATE.md</c> §2.2, <c>GAME_RULES.md</c> §5, <c>MATCH3_RULES.md</c>
/// §6).
///
/// Its rule-level value starts at <see cref="InitialCombo"/> and reads <c>0</c>
/// only before the battle's first committed Swap
/// (<c>MATCH3_RULES.md</c> §6.5 item 4). A committed Swap resets it to <c>0</c>
/// before its first Match is counted and then increments it by exactly <b>1 per
/// Match</b>, in the detection order of <c>MATCH3_RULES.md</c> §3.2 and §4.2,
/// across every pass of the cascade loop (§6.2, §6.3). It is therefore equal to
/// the number of Matches that Swap produced, and never <c>0</c> for a committed
/// Swap (§6.5 item 3).
///
/// It is <b>not</b> turn-cumulative: it does not carry across a Turn boundary,
/// and it does not accumulate over a battle (§6.1 item 4). Its next reset is the
/// next committed Swap — nothing else resets it, and the terminating pass that
/// detects no Match is not a Match and does not reset it (§6.4). A rejected Swap
/// neither resets nor changes it (§6.1 item 3, §6.5 item 2), Special Gem
/// activation, chaining, and creation never change it (§6.3.1), and no cleared
/// cell and no cascade depth is ever converted into it (§6.2 item 2).
/// </param>
/// <param name="MatchCount">
/// The cumulative number of Matches this battle has produced
/// (<c>GAME_STATE.md</c> §2.2, <c>GAME_RULES.md</c> §3).
///
/// Its value starts at <see cref="InitialMatchCount"/> and is
/// <b>battle-cumulative</b>: it increments by exactly <b>1 per Match</b> and
/// never resets — not per Turn, not per Swap, and not per Cascade. Matches
/// produced by cascades count, and several Matches produced by one Swap each
/// count separately, because each distinct detected shape is one Match however
/// many cells it spans (<c>MATCH3_RULES.md</c> §3 item 5, §6.2 item 3).
///
/// It counts <b>Matches only</b>: a Special Gem activation, chain, or the N
/// cells one clears are not Matches and never increment it
/// (<c>MATCH3_RULES.md</c> §5.5.5 item 8, §6.3.1), and it is not a cleared-cell
/// total. It is independent of <c>Turn</c> and of <c>Sequence</c>: one committed
/// Swap advances <c>Turn</c> and <c>Sequence</c> by exactly 1 each whatever its
/// Match count is (<c>MATCH3_RULES.md</c> §8.1, §8.2). A rejected Swap does not
/// change it (§2.1.5 item 5).
///
/// No Combo bonus, damage modifier, Passive charge, or Relic trigger is
/// implemented from it here: those are gameplay consumers owned by
/// <c>COMBAT_RULES.md</c> and <c>RELIC_RULES.md</c> and remain unimplemented.
/// </param>
public readonly record struct PlayerState(
    int HP,
    int MaxHP,
    int ATK,
    int DEF,
    int Power,
    int Crit,
    int Combo,
    int MatchCount)
{
    /// <summary>
    /// The initial <c>MatchCount</c> for a battle that has produced no Match
    /// (<c>GAME_STATE.md</c> §2.2, <c>GAME_RULES.md</c> §3).
    /// </summary>
    public const int InitialMatchCount = 0;

    /// <summary>
    /// The initial <c>Combo</c> for a battle whose first Swap has not been
    /// committed (<c>GAME_STATE.md</c> §2.2, <c>MATCH3_RULES.md</c> §6.1 item 1).
    ///
    /// <c>0</c> is a real, publishable value here — it is the value the state
    /// reads before the first committed Swap — not an "absent" convention and
    /// not an omitted field (<c>MATCH3_RULES.md</c> §6.5 item 4,
    /// <c>GAME_STATE.md</c> §2.1.7 item 5).
    /// </summary>
    public const int InitialCombo = 0;

    /// <summary>
    /// The documented MVP starting <c>MaxHP</c> (<c>COMBAT_RULES.md</c> §1.1:
    /// "Max HP — maximum health — MVP default: 1000").
    ///
    /// Configuration, not an invariant: §1.1 states these values "are not
    /// permanent invariants — future Pet progression (Level, Star, Tier) may
    /// produce different actual Battle Stats". It is the <b>initial</b> value
    /// of a created battle, which is why it is a constant here and not a clamp
    /// or a formula.
    /// </summary>
    public const int DefaultMaxHP = 1000;

    /// <summary>
    /// The documented MVP starting <c>HP</c>
    /// (<c>COMBAT_RULES.md</c> §1.1: "HP — current health — MVP default: 1000").
    ///
    /// A battle begins at full health, so this is the same <c>1000</c> as
    /// <see cref="DefaultMaxHP"/> — not because one is derived from the other,
    /// but because §1.1 gives both the same MVP default.
    /// </summary>
    public const int DefaultHP = 1000;

    /// <summary>
    /// The documented MVP starting <c>ATK</c> (<c>COMBAT_RULES.md</c> §1.1:
    /// "ATK — attack power — MVP default: 50"). Configuration, not an invariant
    /// (§1.1).
    /// </summary>
    public const int DefaultATK = 50;

    /// <summary>
    /// The documented MVP starting <c>DEF</c> (<c>COMBAT_RULES.md</c> §1.1:
    /// "DEF — defense — MVP default: 25"). Configuration, not an invariant
    /// (§1.1).
    /// </summary>
    public const int DefaultDEF = 25;

    /// <summary>
    /// The documented starting <c>Power</c>
    /// (<c>COMBAT_RULES.md</c> §1.1: "Power — resource for casting Cards /
    /// Skills, range 0–100").
    ///
    /// §1.1 gives no MVP default for Power other than the range, and the range's
    /// floor is where a battle begins: Power is generated by Match-3 (§2) and no
    /// Match has occurred at creation. Starting at <c>0</c> is therefore the
    /// documented starting value, not a chosen one. The cap of <c>100</c> is
    /// owned by §1.1 and <c>GAME_RULES.md</c> §12 and is not enforced here.
    /// </summary>
    public const int DefaultPower = 0;

    /// <summary>
    /// The documented MVP starting <c>Crit</c> (<c>COMBAT_RULES.md</c> §1.1:
    /// "Crit — critical hit chance (%) — MVP default: 5%").
    ///
    /// The unit is the percent of §1.1, so the value is <c>5</c> and not
    /// <c>0.05</c> — see the <c>Crit</c> parameter for why the unit is not
    /// converted. Configuration, not an invariant (§1.1).
    /// </summary>
    public const int DefaultCrit = 5;

    /// <summary>
    /// The documented initial state of a newly created battle: the combat stats
    /// at their <c>COMBAT_RULES.md</c> §1.1 MVP defaults at full health with no
    /// Power generated, and the progression values at their documented starting
    /// point — <c>MatchCount = 0</c> and <c>Combo = 0</c>
    /// (<c>GAME_STATE.md</c> §2.2).
    ///
    /// Board generation is not a player Swap/Action and starts no Turn
    /// (<c>MATCH3_RULES.md</c> §8.1 item 4), so the state a battle begins with
    /// carries every value at its documented starting point — not <c>null</c>,
    /// not a sentinel, and not a value created lazily on the first Swap.
    ///
    /// It is a static property rather than a constant, so it is a value of this
    /// type and not a ninth state-bearing member: the eight fields above remain
    /// the whole of the representation. The combat values are read from the
    /// constants above and are not restated, so §1.1's balance values have
    /// exactly one spelling in this type.
    /// </summary>
    public static PlayerState Initial => new(
        HP: DefaultHP,
        MaxHP: DefaultMaxHP,
        ATK: DefaultATK,
        DEF: DefaultDEF,
        Power: DefaultPower,
        Crit: DefaultCrit,
        Combo: InitialCombo,
        MatchCount: InitialMatchCount);
}