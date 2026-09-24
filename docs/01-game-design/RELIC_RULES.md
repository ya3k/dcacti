# Relic Rules

**Version:** 1.2 (§2 ownership vs. equipment restated with battle-start
snapshot into `PetState.EquippedRelics[]`; prior 1.1: §2 equip ownership
— Player owns collection, Relics are a per-active-Pet battle loadout;
§3 trigger subjects corrected to active Pet)
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

Expands GAME_RULES.md §13 (Relic Rules). Conflicts resolve in favor of
GAME_RULES.md.

---

# 1. Relic Structure

A Relic is defined by:

```text
Trigger        one supported event/condition (see §3)
Condition       optional extra condition (e.g. "Combo ≥ 3", "HP < 30%")
Effect          the passive modification applied when triggered
Reset/Cooldown  whether/how the trigger can re-fire (default: re-fires every
                 time its Trigger/Condition is met, no cooldown, unless
                 stated otherwise)
```

Relics are never cast by the player (CARD_RULES.md §5). They have no
Element restriction (ELEMENT_RULES.md §4, GAME_RULES.md §13.5).

---

# 2. Equip Rules

1. **Ownership vs. equipment are two different things.**
   * **Ownership (persistent):** the Player owns Relic *instances* in the
     collection (`DATABASE.md` §1–§2, `Player 1─N Relic`). Ownership
     survives across battles.
   * **Equipment (battle-scoped):** the active Pet carries 3–5 of those
     owned Relics for one battle (GAME_RULES.md §13.3). There is no
     global Player Relic loadout and no persistent "which Pet has this
     Relic equipped" column — equipment is selected at battle start and
     exists only for that battle.
2. Relics are equipped onto the active Pet before battle start as part of
   the pre-battle loadout (`POST /api/battle/start`, `API_CONTRACTS.md`
   §3; GDD §2 "Equip Relics" step), and are locked for the duration of
   the battle — no mid-battle Relic swapping in MVP.
3. At battle start the selected loadout is **snapshotted** into
   `PetState.EquippedRelics[]` (`GAME_STATE.md` §2.3), slot order fixed
   (§4). That battle-scoped array is the only equip representation inside
   active battle state (`REDIS_STATE.md` §2 — serialized with
   `BattleState`); it does not write back to PostgreSQL ownership rows.
4. Any Pet may carry any Relic; no Element or Tier gating in MVP
   (GAME_RULES.md §13.5).

---

# 3. Supported Triggers (MVP)

```text
OnBattleStart     fires once, at battle start
OnMatch           fires once per individual Match (including Cascade matches)
OnMatchCount       fires when cumulative Match count crosses a threshold
                    (distinct from OnMatch: this is a counter-based trigger,
                    analogous to Passive charging — see PASSIVE_RULES.md §2)
OnCombo            fires when Combo reaches/crosses a threshold within one Swap
OnCascade          fires on each Cascade iteration (MATCH3_RULES.md §4)
OnPowerGain        fires when the active Pet's Power increases
OnDamageDealt      fires when the active Pet deals damage
OnDamageTaken      fires when the active Pet takes damage
OnHpBelow          fires when the active Pet's HP crosses below a
                   configured percentage
OnCardCast          fires when any Card (Basic or Pet Skill) is cast
OnTurnStart         fires at the start of a Turn
OnTurnEnd           fires at the end of a Turn
```

A Relic must declare exactly one primary Trigger from this list (plus an
optional Condition, §1). New trigger types are a rule change and must go
through GAME_RULES.md §20 before implementation.

---

# 4. Deterministic Trigger Order

Per GAME_RULES.md §13.7, when multiple Relics are eligible to trigger from the
same event, order must be deterministic. MVP ordering rule:

```text
1. Order equipped Relics by their equip slot index (1 → 5), fixed at
   battle start.
2. For a single event, all eligible Relics resolve in that fixed slot order.
3. If a Relic's Effect causes a *new* event that makes another Relic
   eligible (a chain), the chained event is queued and processed after all
   Relics finish resolving the current event (breadth-first, not
   depth-first), to keep chains predictable.
```

This ordering must be stable across the battle (re-equipping is not possible
mid-battle per §2.2, so slot order cannot change mid-battle).

---

# 5. Anti-Infinite-Chain Rule

Per GAME_RULES.md §13.8, Relic effects must not create uncontrolled infinite
trigger chains. MVP safeguard:

1. A single triggering event (e.g. one Match) may cause at most one full pass
   through the chain-resolution queue described in §4.3.
2. If resolving that pass would cause the same Relic to re-trigger from an
   effect it itself caused (direct self-loop), that Relic does not re-trigger
   again within the same root event — it fires at most once per root event
   unless its declared Trigger is itself the kind of event that naturally
   repeats (e.g. OnMatch during a multi-match Cascade legitimately fires once
   per Match, which is not a self-loop).
3. Any Relic design that appears to require multiple fires from its own
   output within one event must be flagged as a conflict (GAME_RULES.md §20)
   rather than implemented ad hoc.

---

# 6. MVP Relic Reference

```text
Relic             Trigger        Condition        Effect
---------------   ------------   --------------   --------------------------------
Berserker Core    OnMatchCount   every 3 Matches   +5% ATK
Mana Crystal      OnMatchCount   every 4 Matches   +10 Power
Assassin Eye      OnCombo        Combo ≥ 3         Increased Crit chance
Burning Curse     (passive mod)  —                 +30% Burn damage
Emergency Core    OnHpBelow      HP < 30%          Heal Card cost −50%
```

Notes:

1. "Burning Curse" is a **static modifier**, not an event-triggered Relic in
   the strict sense — it continuously modifies the Burn damage formula
   (COMBAT_RULES.md §5) rather than firing on a discrete event. Static
   modifier Relics are allowed in MVP and apply during the relevant
   COMBAT_RULES.md calculation step rather than through the Event trigger
   table in §3.
2. "Emergency Core" re-evaluates continuously (it is "armed" whenever
   HP < 30%, not a one-shot fire) — it modifies Heal Card cost for as long as
   the condition holds, reverting when HP rises back above 30%.

---

# 7. Events

```text
RelicTriggered   emitted each time a Relic's Effect actually applies
```

Part of the Battle Event Model (GAME_RULES.md §16), fired at the point shown
in the Event Resolution Rules (GAME_RULES.md §17, step 11 "Trigger Relics").
