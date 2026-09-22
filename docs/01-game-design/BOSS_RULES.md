# Boss Rules

**Version:** 2.0 (Boss Response contract resolved — §3.3 timing within
resolution order, §4 Skill charge/cooldown, §5 Enrage/Stun clarification,
§6 per-Boss Skill timing and Base Damage, §7 event definitions)
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

Expands GAME_RULES.md §15 (Boss Rules). Conflicts resolve in favor of
GAME_RULES.md.

---

# 1. Boss Structure

```text
Boss
├── Element      (exactly one of the Five Elements)
├── HP / Max HP
├── ATK
├── DEF
├── Passive       (automatic, reactive — see §3)
├── Skill          (Boss-initiated action — see §4)
└── State          (internal enum, e.g. Idle / Charging / Enraged / Stunned)
```

A battle has exactly one Boss (GAME_RULES.md §1.1).

---

# 2. Design Principle: Mechanics Over Stats

Per GAME_RULES.md §15.1–15.3 and GDD §13:

1. Boss difficulty must come primarily from **mechanics** (Passive triggers,
   Skill timing, reactive punishes), not from HP/ATK inflation alone.
2. Every MVP Boss must have a Passive AND a Skill that are mechanically
   distinct from each other and from other Bosses' Passive/Skill.
3. Bosses must not share the same trigger pattern (GAME_RULES.md §15.2) —
   see §3.2 below for how MVP satisfies this.

---

# 3. Boss Passive

1. A Boss Passive is automatic and reactive, similar in structure to a Pet
   Passive (PASSIVE_RULES.md), but triggered by the *player's* actions or
   battle state rather than the Boss's own Matches (Bosses do not match
   Gems).
2. Supported Boss Passive triggers (mirrors GAME_RULES.md §15, "Bosses may
   react to"):
   ```text
   Turn
   Match Count (player's)
   Combo (player's)
   Player Power
   Player HP
   Boss HP
   Status (on Boss or Player)
   ```
3. Boss Passive effects are automatic — no player input required, and no Boss
   "casting" input required either; they resolve as part of normal battle
   flow (see Event Resolution, GAME_RULES.md §17).

## 3.1 Determinism

Boss Passive/Skill logic must be deterministic given the same battle state
history (GAME_RULES.md §15.4) — no hidden randomness beyond what is
explicitly declared as part of the Skill's design (e.g. a Skill that already
states "random target" may use server RNG, but an undeclared random branch is
not allowed).

## 3.2 Trigger Pattern Diversity

To satisfy "not all Bosses use the same trigger pattern," each MVP Boss's
Passive must use a different primary trigger category from §3.2's list than
the other MVP Bosses where possible. See §6 for the MVP assignment.

## 3.3 Timing Within the Resolution Order

Boss Passive fires at Step 18 of GAME_RULES.md §17, after Player Damage
(Steps 15–17) and before Boss Skill (Step 19) and Boss Attack (Step 20).

1. **The Passive fires once per player action, after all player damage is
   resolved.** This means the Passive sees the post-damage battle state
   (Player HP, Boss HP, Combo, match count). A Boss Passive that triggers
   on "Boss HP below threshold" evaluates against the HP after the player's
   damage, not before.
2. **The Passive fires before the Boss Skill.** The Boss Skill's timing
   rule (charge requirement, cooldown) is evaluated after the Passive
   resolves. If the Passive's effect changes the battle state (e.g. a
   self-buff), the Boss Skill sees that updated state.
3. **The Passive fires before the Boss Attack.** If the Boss Skill does
   not fire (charge not met or cooldown active), the Boss performs a basic
   attack (Step 20). The Passive's effect is already applied at that point.
4. **Boss Skill damage does not re-trigger the Boss Passive.** The Passive
   resolves once at Step 18 and does not re-evaluate after the Skill
   (Step 19) or Attack (Step 20). If a Boss Passive triggers on "Boss HP
   below threshold," it evaluates once against the post-player-damage state
   — not again after the Boss's own actions.

---

# 4. Boss Skill

1. A Boss Skill is a distinct, usually stronger action the Boss performs,
   analogous to a Pet's Signature Skill but Boss-initiated rather than
   player-cast.
2. Boss Skills are triggered by the Boss's own timing rule (e.g. every N
   Turns, or when a State condition is met) — this timing rule is itself
   part of the Boss's design and must be explicit, not implicit.
3. Boss Skill damage (if any) goes through the full Damage Pipeline
   (COMBAT_RULES.md §3), including the Boss's Element for Element Modifier
   purposes (ELEMENT_RULES.md §5).
4. Boss Skills may apply non-damage effects (debuffs, resource drain, etc.)
   as shown in the MVP examples below.

---

# 5. Boss State

1. Boss "State" is an internal enum used to gate which Passive/Skill logic is
   currently active (e.g. a Boss might be Idle until a Passive-triggered
   condition flips it to Enraged, changing its Skill behavior).
2. State is entirely server-authoritative (GAME_RULES.md §15.5) and must be
   exposed to the client only through emitted events (e.g. `BossSkillCast`)
   and rendered state, never computed client-side.
3. MVP Bosses may use State minimally (e.g. a simple Idle/Casting toggle); a
   full multi-phase State machine is a Boss Phases feature explicitly
   deferred to Future Expansion (GDD §20), not required for MVP.
4. **Enrage** is a permanent state transition triggered when `BossHP <
   EnrageThreshold` (per-Boss value in §6.1). Once Enraged, the Boss remains
   Enraged for the rest of the battle — no timer, no duration field. The
   Enrage threshold and any Enrage-specific behavior changes (e.g. Skill
   damage increase) are defined per Boss in §6.
5. **Stun** is a temporary state that prevents the Boss from acting. Duration
   is measured in Turns and tracked by `StatusEffects[]` (not yet
   implemented). For MVP, no content-defined Boss applies Stun.

---

# 6. MVP Boss Reference

```text
Boss        Element   Passive (trigger)                          Skill                    Skill Timing
---------   -------   -----------------------------------------  -----------------------  ----------------------
Hỏa Long    Hỏa       Every 5 Player Matches → gain Rage          Flame Burst → Dmg+Burn  Charge: 5 matches, CD: 2T
Thủy Ma     Thủy      Healing received reduced                    Drain Power → -PlayerPow Charge: 4 matches, CD: 3T
Mộc Yêu     Mộc       Every 5 Player Matches → Regen HP           Root → -PlayerATK        Charge: 6 matches, CD: 2T
```

### 6.1 MVP Boss Base Stats (Project-Owner Approved)

```text
Boss        Element   HP / MaxHP   ATK   DEF   EnrageThreshold   Initial State
---------   -------   ----------   ---   ---   ---------------   -------------
Hỏa Long    Hỏa       5000         100   50    1500 (30%)        Idle
Thủy Ma     Thủy      5000         100   50    1500 (30%)        Idle
Mộc Yêu     Mộc       5000         100   50    1500 (30%)        Idle
```

### 6.2 Boss Passive Details

```text
Boss        Passive Effect                                    Passive Trigger
---------   -----------------------------------------------   ----------------------
Hỏa Long    Gain +20% ATK (Rage) for 3 turns                  Every 5 Player Matches
Thủy Ma     Player Healing reduced by 50% for 3 turns          Passive (always active)
Mộc Yêu     Regenerate 5% MaxHP                               Every 5 Player Matches
```

### 6.3 Boss Skill Timing

Each Boss Skill has two timing parameters:

- **Charge Requirement**: number of player matches required before the Skill
  is eligible to fire. Matches increment `BossState.SkillCharge`
  (`GAME_STATE.md` §2.4.3). When `SkillCharge ≥ Charge Requirement` AND
  `SkillCooldown = 0`, the Skill fires.
- **Cooldown (CD)**: turns remaining after each Skill use before the Skill
  can fire again. Decrements by 1 at each Turn increment
  (`GAME_RULES.md` §17 step 17). The Skill is blocked while `CD > 0`.

After the Skill fires: `SkillCharge` resets to 0, `SkillCooldown` resets to
the Boss's cooldown value.

```text
Boss        Charge Req.   CD (T)   Skill Base Dmg   Notes
---------   -----------   ------   --------------   -----
Hỏa Long    5 matches     2        150              Aggressive, frequent
Thủy Ma     4 matches     3        120              Strategic, less frequent
Mộc Yêu     6 matches     2        100              Defensive, slower charge
```

These are **MVP base configuration** — not universal balance invariants. The
project owner approved these values. They are configuration defaults used at
battle creation; they do not represent formulas or scaling rules.

Two additional MVP Bosses (5 total per GAME_RULES.md §19 scope) are not yet
content-defined. When authored, each must:

1. Declare an Element (distinct pairing with the other Bosses is encouraged
   but not mandated — multiple Bosses may share an Element; only trigger
   *pattern* diversity is required, per §3.2).
2. Declare a Passive with an explicit trigger category from §3.2.
3. Declare a Skill with an explicit timing rule and effect.
4. Avoid duplicating an existing Boss's trigger category where reasonably
   possible (e.g. avoid a third "every N Player Matches" Passive if the
   other four Bosses already cover Match-count, HP-based, and Turn-based
   patterns).

---

# 7. Events

```text
PassiveCharged     emitted when Boss Passive progress increments
                   (shared event with Pet Passive — GAME_EVENTS.md §2,
                    SIGNALR_PROTOCOL.md §3.2.16; source = "boss")

PassiveTriggered   emitted when Boss Passive threshold is crossed
                   (shared event with Pet Passive — GAME_EVENTS.md §2,
                    SIGNALR_PROTOCOL.md §3.2.17; source = "boss")

BossSkillCast      emitted when a Boss Skill resolves
                   (SIGNALR_PROTOCOL.md §3.2.18)

BattleWon          emitted when Boss HP reaches 0
BattleLost         emitted when Player HP reaches 0
                   (SIGNALR_PROTOCOL.md §3.2.19)
```

Boss Passive triggers use the general Passive events (`PASSIVE_RULES.md` §7)
with `source = "boss"` to distinguish from Pet Passives. No Boss-specific
passive event name is needed.

Boss state changes (Idle → Enraged, Idle → Stunned) are inferable from the
sequence of `DamageDealt`, `DamageTaken`, `BossSkillCast`, and `PassiveTriggered`
events — no dedicated `BossStateChanged` event exists.

---

# 8. Server Authority

All Boss HP, State, Passive progress and Skill resolution are server
authoritative (GAME_RULES.md §15.5, §18). The client never determines when a
Boss Skill fires or what it does — it only renders the resulting events.
