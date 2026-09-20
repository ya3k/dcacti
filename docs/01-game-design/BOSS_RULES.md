# Boss Rules

**Version:** 1.0
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

---

# 6. MVP Boss Reference

```text
Boss        Element   Passive (trigger)                          Skill
---------   -------   -----------------------------------------  --------------------------------
Hỏa Long    Hỏa       Every 5 Player Matches → gain Rage          Flame Burst → Damage + Burn
Thủy Ma     Thủy      Passive: Healing received reduced           Drain Power → Reduce Player Power
Mộc Yêu     Mộc       Every 5 Matches → regenerate HP              Root → Reduce Player ATK
```

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
BossSkillCast   emitted when a Boss Skill resolves
```

Boss Passive triggers do not have a dedicated event name distinct from the
general Passive events (PASSIVE_RULES.md §7) unless a Boss-specific event is
introduced by a future rule change. Boss state changes should be inferable
from the sequence of `DamageDealt`, `DamageTaken`, `BossSkillCast`, and other
Battle Event Model events (GAME_RULES.md §16).

---

# 8. Server Authority

All Boss HP, State, Passive progress and Skill resolution are server
authoritative (GAME_RULES.md §15.5, §18). The client never determines when a
Boss Skill fires or what it does — it only renders the resulting events.
