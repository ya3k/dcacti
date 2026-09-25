# Relic Rules

**Version:** 1.4 (§2.3 equip slot index source RESOLVED — request array
position + 1 determines the slot; §2.4 duplicate selection RESOLVED — the same
Relic instance may not occupy more than one slot, distinct instances of one
RelicDefinition may be equipped together, duplicates rejected as
`INVALID_LOADOUT`; §2.5 deterministic valid/invalid loadout behavior; prior
1.3: §2.3 equip-slot index source and §2.4 duplicate-selection policy recorded
as OPEN — blocking; §2.2 element representation of `PetState.EquippedRelics[]`
confirmed as Relic instance identity; prior 1.2: §2 ownership vs. equipment
restated with battle-start snapshot into `PetState.EquippedRelics[]`; prior
1.1: §2 equip ownership — Player owns collection, Relics are a per-active-Pet
battle loadout; §3 trigger subjects corrected to active Pet)
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

## 2.1 Loadout Selection and Validation

1. `relicLoadout` selects **3–5 Relics owned by the Player**
   (`API_CONTRACTS.md` §3; `GAME_RULES.md` §13.3). The count bound is stated
   here; the duplicate-instance rule is §2.4, the slot-index source is §2.3,
   and the resulting behavior is §2.5.
2. Each selected Relic must be **owned by the requesting Player** — an
   ownership check against the Player's owned Relic *instances*
   (`DATABASE.md` §2 `Player 1─N Relic`, ADR-011 item 4). A selection that
   is not owned is rejected; the documented rejection envelope is
   `API_CONTRACTS.md` §6, and `INVALID_LOADOUT` is the loadout rejection
   named by `API_CONTRACTS.md` §3.
3. Validation is **server-side and request-time** (Application layer, not
   a stored invariant — `DATABASE.md` §3's ownership-model note). The
   client supplies the selection only; it never supplies the resulting
   `PetState.EquippedRelics[]` (`GAME_RULES.md` §18, ADR-001).

## 2.2 Element Representation of `PetState.EquippedRelics[]`

Each element of `PetState.EquippedRelics[]` is **one owned Relic
instance's identity** — the instance the Player owns (`DATABASE.md` §1
`Relic.RelicInstanceId`), not the static `RelicDefinition` it references.

```text
PetState.EquippedRelics[]  →  3–5 elements
each element               →  one owned Relic instance identity
                              (identity only — see §2.2 item 2)
```

1. **Why instance identity, and not definition identity.** Ownership is
   instance-based (`§2.1`; `DATABASE.md` §1–§2 `Player 1─N Relic`;
   ADR-012 item 7), and `API_CONTRACTS.md` §3 validates "Player ownership
   of the selected **instances**". The snapshot is taken from *the
   Player's owned collection*, so the value that survives into battle is
   the one that identifies the owned instance. `RelicDefinitionId` is a
   property of the instance (`DATABASE.md` §1 `Relic.RelicDefinitionId`
   FK → `RelicDefinition`), so the definition is reachable from the
   element and is not what the element carries.
2. **It is an identity, not a definition.** The element carries the
   identifier only. The Relic's `Trigger`, `Condition`, `Effect`, and
   `Reset/Cooldown` (`§1`) are its **definition**, owned by
   `RelicDefinition` (`DATABASE.md` §1) and resolved data, and none of
   them is copied into the element. No second copy of the definition is
   introduced by this array (`GAME_STATE.md` §0 item 5 — no parallel
   representation). This mirrors the identity fields `GAME_STATE.md` §2.3
   records beside it — `PetState.PassiveId` and `BossState.BossId` — whose
   rule is "**an identity, not a definition**" (§2.3 items 1–4).
3. **One identity member, and it is the same identity the trigger event
   reports.** `GAME_EVENTS.md` §2 defines `RelicTriggered`'s payload as
   `RelicId` — one identity member, not a resolved effect. The array
   element and that `RelicId` are the same identity: a Relic the server
   resolved and triggered is the Relic that was equipped.
4. **The element is not the static content row.** `RelicDefinition`
   (`DATABASE.md` §1) is static MVP content (~10 Relics, `MVP_SCOPE.md`
   §1); the equipped set is the Player's owned *instances* of that
   content. Two owned instances of one definition are two distinct
   elements, and §2.4 item 3 permits both to be equipped simultaneously.
5. **No per-instance state is added by this section.** No MVP Relic in
   §6 carries per-instance state (no charges, stacks, cooldowns, or
   duration owned by the instance; `§1` places `Reset/Cooldown` on the
   Relic's definition). Whether `DATABASE.md` §1's `Relic` instance table
   or its alternative join-table form is the storage shape is **not
   resolved here** — it is a storage question owned by `DATABASE.md`, and
   this section states only what the battle-state element denotes.

## 2.3 Equip Slot Index Source

**Decided.** §4.1 orders equipped Relics by their **equip slot index
(1 → 5)**. The slot index is assigned from the **submitted `relicLoadout`
order**:

```text
slot index = request array position + 1

relicLoadout[0]  →  slot 1
relicLoadout[1]  →  slot 2
relicLoadout[2]  →  slot 3
relicLoadout[3]  →  slot 4
relicLoadout[4]  →  slot 5
```

1. **The request array order is the authoritative equip-slot order.** This
   is the rule candidate B1 of the resolved contract gap
   (`API_CONTRACTS.md` §3's `relicLoadout`), decided by human gameplay
   decision.
2. **No other property orders the slots.** The server must **not** sort the
   selection by `RelicInstanceId`, by `RelicDefinitionId`, by
   `Relic.AcquiredAt` (`DATABASE.md` §1), by database/row order, or by any
   other property. Position in the submitted array is the only input.
   Because the array is the order, a selection that is a *set* has no
   defined slot order until it is submitted as an array.
3. **The index space is `1..N` for the N selected Relics**, within the
   documented 3–5 bound (`§2.1`, `API_CONTRACTS.md` §3): a 3-Relic loadout
   occupies slots 1–3, a 5-Relic loadout slots 1–5.
4. **Fixed at battle start, and unchanged for that battle.** The assignment
   happens once, when the battle starts (§2 item 2), and §4's ordering is
   stable for the battle: re-equipping is impossible mid-battle, so slot
   order cannot change mid-battle.
5. **This makes §4 deterministic, and it is gameplay-visible.** §4.2 resolves
   all eligible Relics for a single event in this slot order, so the
   submitted order is the trigger resolution order. It is not a storage
   detail.
6. **The snapshot preserves the order.** `PetState.EquippedRelics[]` receives
   the selected instances **in slot order** (§2.5), so
   `EquippedRelics[0]` is slot 1, `EquippedRelics[1]` is slot 2, and so on —
   consistent with §2.2's element representation (one owned Relic instance
   identity) and `GAME_STATE.md` §2.3's "slot order fixed at battle start".

This section defines the slot index only. It introduces no trigger, effect,
or stacking rule: what happens when several Relics resolve from one event is
owned by §4 and §5 and is unchanged.

## 2.4 Duplicate Relic Selection

**Decided.** A selection is validated against the rules below. All three are
minimum rules for deterministic validation and snapshot semantics; none of
them defines a stacking, trigger, or effect behavior (`§5` remains the only
anti-chain rule and is unchanged).

1. **The same Relic instance MUST NOT appear more than once.** A
   `RelicInstanceId` may occupy **at most one** slot in a single
   `relicLoadout`. A Relic instance is one object; it cannot be equipped
   twice in the same battle.

   ```text
   ["relic-a", "relic-b", "relic-a"]   → INVALID (relic-a twice)
   ["relic-a", "relic-b", "relic-c"]   → valid (if all owned)
   ```

2. **Every selected element MUST be a distinct owned Relic instance.** This
   is the general form of item 1: the 3–5 selected `RelicInstanceId` values
   within one `relicLoadout` are pairwise distinct. It is an
   **ownership-instance** rule, not a definition-level uniqueness rule.

3. **Multiple distinct instances of the SAME `RelicDefinition` MAY be
   equipped simultaneously.** Two different owned instances that reference
   the same `RelicDefinitionId` (`DATABASE.md` §1) are two distinct
   instances, so they may occupy two different slots.

   ```text
   RelicInstance A → RelicDefinition "FireBoost"   → slot 2
   RelicInstance B → RelicDefinition "FireBoost"   → slot 4
   ```

   This is permitted. The rule constrains **instance** identity (item 1),
   never `RelicDefinitionId`; there is no "one instance per definition"
   restriction and no definition-level deduplication.

4. **The count bound counts selected instances, not definitions.** The
   documented 3–5 bound (`§2.1`, `API_CONTRACTS.md` §3) counts the elements
   of `relicLoadout`. A duplicate is **rejected**, not merged or silently
   collapsed, so it never reduces an otherwise-valid count into range: a
   3-element selection containing a repeated instance is invalid, not a
   valid 2-Relic loadout. Equally, two instances of one definition count as
   two.

5. **A duplicate instance selection is rejected with `INVALID_LOADOUT`.** No
   new error code is introduced: the rejection uses the existing documented
   invalid-loadout code (`API_CONTRACTS.md` §3's loadout rejection, §6's
   error envelope). A rejected request writes no battle state and equips
   nothing.

6. **This defines no stacking behavior.** Item 3 permits two instances of one
   definition to be equipped; it does **not** state that their effects
   combine, scale, or interact. Any such behavior is a future rule change
   (`GAME_RULES.md` §20), not something this section implies. §5's
   anti-infinite-chain rule is unchanged, and this section does not define
   what "the same Relic" means for §5 — §5 continues to operate as written.

Validation order: count and ownership (`§2.1`) and the distinctness rule
above are all request-time checks; §2.5 states the resulting behavior.

## 2.5 Deterministic Behavior

Every input to the loadout contract is now determined, so the behavior of a
valid and an invalid loadout is fully specified. This section states the
resulting contract; the rules themselves are owned by §2.1–§2.4 and
`API_CONTRACTS.md` §3.

```text
DETERMINED — validation order for a submitted `relicLoadout`

  1. count      must be 3–5 elements              (API_CONTRACTS.md §3, §2.1)
  2. ownership  every element must be an owned     (§2.1 item 2, DATABASE.md §2)
                Relic instance of the requesting
                Player
  3. distinctness  no RelicInstanceId may repeat   (§2.4 items 1–2)
                   within the array

  Any failure → rejected, API_CONTRACTS.md §6 envelope, INVALID_LOADOUT
                (API_CONTRACTS.md §3). Nothing is equipped; no battle state
                is written.
```

```text
DETERMINED — slot assignment (valid selection only)

  slot index = request array position + 1          (§2.3)

  relicLoadout[0] → slot 1
  relicLoadout[1] → slot 2
  relicLoadout[2] → slot 3
  relicLoadout[3] → slot 4     (when 5 Relics are selected)
  relicLoadout[4] → slot 5

  The submitted order is authoritative and is preserved verbatim: no sort
  by RelicInstanceId, RelicDefinitionId, AcquiredAt, or database order
  (§2.3 item 2).
```

```text
DETERMINED — snapshot

  PetState.EquippedRelics[] receives one element per selected Relic, in
  slot order, each element being that instance's identity (§2.2, §2.3 item 6):

      EquippedRelics[0] = relicLoadout[0]   (slot 1)
      EquippedRelics[1] = relicLoadout[1]   (slot 2)
      EquippedRelics[2] = relicLoadout[2]   (slot 3)
      EquippedRelics[3] = relicLoadout[3]   (slot 4, if selected)
      EquippedRelics[4] = relicLoadout[4]   (slot 5, if selected)

  Written once at battle start (POST /api/battle/start, API_CONTRACTS.md §3),
  then:
    - fixed for the duration of the battle (§2 item 2; no mid-battle
      re-equip or Relic swapping)
    - the only equip representation in active battle state (§2 item 3;
      REDIS_STATE.md §2 — serialized with BattleState)
    - never re-read from the Player's collection during the battle
      (ADR-012 item 8 — no live inventory reads)
    - never written back to the Player's ownership rows (ADR-012 item 8 —
      the snapshot changes no ownership data)
    - no persistent equip table exists for it (§2 item 1, ADR-012 item 7)
```

Neither the count bound nor the ownership check is restated as a new rule
here: both are `API_CONTRACTS.md` §3's, cited.

This section introduces **no** Relic trigger, effect, cooldown, reset, or
stacking behavior. Those remain owned by §4–§5 and are unchanged.

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

**The equip slot index is assigned by the request array order (§2.3).**
§4.1 orders by "equip slot index", and §2.3 defines that index as the position
in the submitted `relicLoadout` plus one — so the rule below is now
deterministic. §4.1–§4.3 are otherwise unchanged.

This ordering must be stable across the battle (re-equipping is not possible
mid-battle per §2 item 2, so slot order cannot change mid-battle).

**The "same Relic" of §5 is an owned Relic instance.** §2.4 makes each slot
hold a distinct owned Relic instance identity, so §5's per-Relic anti-chain
rule operates per equipped instance. §2.4 also permits two distinct instances
of one `RelicDefinition` to be equipped together; §5 is unchanged by this and
neither permits nor forbids anything on the basis of the definition.

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
