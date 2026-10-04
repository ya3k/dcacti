# Relic Rules

**Version:** 1.14 (§3 and §6/§8 gained the **TASK-178 canonical runtime
clarifications** of the four Product Owner-approved decisions **Q-1 = A**,
**Q-2 = B**, **Q-3 = A**, **Q-4 = C** — the blockers TASK-177 reported. No
Relic, Trigger, Condition, effect, magnitude, target, or lifetime value is
changed or added. **Q-2** — §3.1 added: `OnPowerGain` reacts only to
**qualifying non-Relic-generated** Power gains; Power granted by a Relic effect
is not a qualifying gain, so `OnPowerGain → Arcane Battery → +5 Power →
OnPowerGain` cannot recur (§8.5 item 7). **Q-3** — §3.2 added: **each actual
cascade iteration is an independent `OnCascade` event**, so Cascade Core resolves
once per iteration and several cascades in one Swap are not collapsed into one
event (§8.5 item 9). **Q-4** — §6 note 1 and §8.5 item 5 now state that Burning
Curse modifies **Pet-owned/source Burn only** and not Boss-owned Burn, and that
`target: Pet` identifies the Pet as the owner/source context rather than "every
Burn tick the Pet receives"; the TASK-176 contract (`OnBattleStart`, no
Condition, `BurnDamage` `+30%`, `Battle`, non-stacking) is unchanged. **Q-1** —
§8.3 item 4 and §8.5 item 10 now record that `ATK`'s `Battle` and `NextAttack`
lifetimes share the **one** carrier `PetState.ATKModifiers[]`
(`GAME_STATE.md` §2.3.7/§5.1.4), consumed at `COMBAT_RULES.md` §3.3 items 7–11's
existing qualifying-attack boundary; **no** `NextAttackATKModifiers[]` and **no**
generic `NextAttackModifiers[]` is introduced. §3's closed Trigger list, §8.1's
Condition forms, §8.2's `effectType` set, §8.3's table rows, and §6's 10 rows are
unchanged. Prior 1.13: (§6 and §8 updated per TASK-176: Product Owner approved the
full 10-Relic MVP content contract. Burning Curse's static-modifier conflict is
resolved: it declares `OnBattleStart`, no Condition, and `BurnDamage` (+30%,
`Battle` lifetime). Five new canonical Relics authored: Combo Fang (`OnCombo`,
`ComboAtLeast(5)`, +20pp `Crit`, `NextAttack`), Arcane Battery (`OnPowerGain`, +5
`Power`, `Immediate`), Execution Mark (`OnHpBelow`, `HpPercentageBelow(30)`, +15pp
`Crit`, `NextAttack`), Cascade Core (`OnCascade`, +5 `Power`, `Immediate`), and
Battle Instinct (`OnDamageTaken`, +10% `ATK`, `NextAttack`). `BurnDamage` is
formally defined as an approved effect type (§8.2, §8.3); `ATK` scope is expanded
to allow `NextAttack` lifetime; all 10 Relics are content-defined, with 4 provisioned
and 6 pending downstream provisioning. No runtime code or migrations changed.)
Prior 1.12: (§8.1 gained item 8 — the three Condition forms' exact
observation point, resolving the step-11 ordering question TASK-133 reported:
`GAME_RULES.md` §17 fixes step 11 after step 10 ("Charge Passive") and before
steps 12–14, so `HpPercentageBelow` reads the active Pet's HP **before** this
Swap's step-14 player-effect (heal) resolution, while `MatchCountAtLeast` and
`ComboAtLeast` read the existing `BattleState.MatchCount` / `BattleState.Combo`
at that same point. **This is a cross-reference to `GAME_RULES.md` §17's order,
not a second copy of it**: no Trigger, Condition form, effect, magnitude,
threshold, target, lifetime, state member, or event is added, removed, or
altered; §3's closed Trigger list, §6's row set, and §8.2–§8.7 are unchanged.
Prior 1.11: (§8.7's startup status and its trailing status paragraph
synchronized with the landed Relic resolution stage: TASK-133 implements
`GAME_RULES.md` §17 step 11 — `GAME_RULES.md` §17's "Trigger Relics" step is now
executed server-side, so the five `NOT IMPLEMENTED` lines §8.7 recorded are
replaced by `IMPLEMENTED`. **This is a status synchronization only: no Trigger,
Condition, effect, magnitude, threshold, target, or lifetime is added, removed,
or altered; §3's closed Trigger list, §6's row set, and §8.1–§8.6 are unchanged,
and `Burning Curse` stays deferred and unprovisioned.** Prior 1.10: (§2.4 item 6, §8.5 item 4, and §8.7 synchronized with the
resolved Relic Battle-lifetime ATK modifier × Turn-based `BuffDebuff` `TargetStat = "ATK"`
modifier composition contract: how the two coexist and compose is now authored
at its canonical owner `COMBAT_RULES.md` §5.6.6 per the TASK-137 Product-Owner
decision set D1–D7. Modifiers compose order-independently by summing signed
percentage adjustments against permanent Base Pet ATK, truncated toward zero
exactly once into integer `EffectivePetATK`, with independent carrier ownership
and lifetimes (`PetState.ATKModifiers[] ≠ PetState.StatusEffects[]`). The contract
is implementation-ready for TASK-133. §8.2, §8.3, §8.4, and §3–§7 are unchanged;
no new Trigger, Condition, effect, magnitude, or event is added. Relic trigger
evaluation remains UNIMPLEMENTED — `GAME_RULES.md` §17 step 11. Prior 1.9: (§2.4 item 6, §8.5 item 4, and §8.7 synchronized with the
applied `ATK` runtime contract: the `ATK` modifier's composition is now authored
by `COMBAT_RULES.md` §5.6 (signed percentage-point contributions, summed) and
its runtime carrier by `GAME_STATE.md` §2.3.7/§5.1.4, per the TASK-136
Product-Owner decision set D1–D12. **§2.4 item 6's "defines no stacking
behavior" disclaimer is not withdrawn for Relics in general**: it now names
**two** decided exceptions — `CardCost` (`CARD_RULES.md` §3.6) and `ATK`
(`COMBAT_RULES.md` §5.6) — and explicitly leaves `Power`, `Crit`, and every
other `effectType` where they were, undefined and pending a future rule change.
A new §8.5 item 4 records Berserker Core's runtime carrier, lifecycle, and
composition references, and records as **unresolved** how a Relic `ATK` modifier
interacts with a Turn-based `BuffDebuff` `TargetStat = "ATK"` modifier
(`COMBAT_RULES.md` §5.6.6). §8.7 now distinguishes the *contracts* that exist
from the *implementation* that does not: the five `NOT IMPLEMENTED` lines are
unchanged. §8.2's structured effect declaration
(`effectType`/`valueType`/`value`/`target`/`lifetime`), §8.3's allowed
combinations, §8.4, and §3–§7 are unchanged; no Trigger, Condition, effect,
magnitude, or event is added. Relic trigger evaluation remains UNIMPLEMENTED —
`GAME_RULES.md` §17 step 11. Prior 1.8: (§8.6/§8.7 status synchronized with the landed structured
storage: TASK-132 moved `RelicDefinition.Condition` and
`RelicDefinition.EffectDefinition` from `character varying(128)` prose to the
structured `jsonb` representation §8.1–§8.3 defines and §8.5 encodes — §8.7's
`Structured Relic storage` line now reads IMPLEMENTED, and §8.6 records that its
required separate migration has landed. **This is a status synchronization only:
no trigger, condition, effect, magnitude, threshold, target, or lifetime is
added, removed, or altered, and §3's closed Trigger list is unchanged.**
`RelicTriggered` emission, trigger evaluation, condition evaluation, and effect
application all remain NOT IMPLEMENTED — §8.7. Prior 1.7: §2.4 item 6 and §8.5 item 2 updated — the applied `CardCost`
modifier's runtime contract is now authoritative, applied per TASK-134 D1–D11
plus the Product Owner `EffectiveCardCost` truncation decision. **§2.4 item 6's
"defines no stacking behavior" disclaimer is not withdrawn for Relics in
general**: it now names the single decided exception — the composition of
multiple simultaneously-active `CardCost` modifiers, a `GAME_RULES.md` §20 rule
change owned by `CARD_RULES.md` §3.6 (additive percentage reduction, total
capped at 100%) — and explicitly leaves `ATK`, `Power`, `Crit`, and every other
`effectType` where they were, undefined and pending a future rule change. §8.5
item 2 now references the runtime state carrier (`GAME_STATE.md` §2.3.5), its
mutation lifecycle (`GAME_STATE.md` §5.1.3), and the cost composition
`CARD_RULES.md` §3.6 owns, and states that Emergency Core's continuous
re-evaluation replaces/refreshes its one entry rather than accumulating. Neither
section restates the runtime schema. §8.2's structured effect declaration
(`effectType`/`valueType`/`value`/`target`/`lifetime`), §8.3's allowed
combinations, §8.4, and §3–§7 are unchanged; no trigger, condition, effect,
magnitude, or event is added. Relic trigger evaluation remains UNIMPLEMENTED —
`GAME_RULES.md` §17 step 11. Prior 1.6: §8 added — the Trigger/Condition/Effect
contract RESOLVED per TASK-131 D1–D11: `EffectDefinition` is a structured
`EffectDefinition[]` with `valueType` `Flat`/`Percentage`/`PercentagePoints`/`Undetermined` and
explicit `target`/`lifetime` vocabulary; `Condition` is structured as
`MatchCountAtLeast(N)`/`ComboAtLeast(N)`/`HpPercentageBelow(N)` evaluated against
the current resolution state with no persistent Relic counters; Assassin Eye's
Crit magnitude is +10 percentage points with `NextAttack` lifetime; effect
lifetime and trigger re-evaluation are independent; §3's closed Trigger list is
unchanged; `Burning Curse` remains deferred with the §3-vs-§6-note-1 conflict
still reported and unresolved; the `varchar(128)` column is insufficient and a
separate migration task is required. This supersedes TASK-082 R2-7 for
`RelicDefinition.EffectDefinition`, closing the member TASK-109 had left under
it. Prior 1.5: §6
provisioned/deferred row set recorded per TASK-082
decisions A / R2-8 — the four event-triggered Relics are provisionable;
"Burning Curse" **deferred** pending a documented static-modifier
`RelicDefinition.Trigger`, with the §3 "exactly one primary Trigger" vs
§6 note 1 static-modifier tension reported, not resolved (`AGENTS.md` §4);
prior 1.4: §2.3 equip slot index source RESOLVED — request array
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

6. **This defines no stacking behavior, with one decided exception.** Item 3
   permits two instances of one definition to be equipped; it does **not**
   state that their effects combine, scale, or interact. Any such behavior is
   a future rule change (`GAME_RULES.md` §20), not something this section
   implies. §5's anti-infinite-chain rule is unchanged, and this section does
   not define what "the same Relic" means for §5 — §5 continues to operate as
   written.

   **The decided exceptions, and their boundary.** The composition of multiple
   simultaneously-active **`CardCost`** modifiers is no longer undefined: it
   was decided as a rule change under `GAME_RULES.md` §20 (Product Owner
   decision D5, recorded in TASK-134) and is owned by **`CARD_RULES.md` §3.6**
   — additive percentage reduction, total reduction capped at 100%. The
   composition of multiple simultaneously-active **`ATK`** modifiers is likewise
   no longer undefined: it was decided as a rule change under `GAME_RULES.md`
   §20 (Product Owner decisions D5/D6, recorded in TASK-136) and is owned by
   **`COMBAT_RULES.md` §5.6** — signed percentage-point contributions, summed.
   Each rule governs its own `effectType` **only**:

   ```text
   DECIDED     CardCost modifier composition       CARD_RULES.md §3.6
   DECIDED     ATK modifier composition            COMBAT_RULES.md §5.6
   NOT DECIDED every other Relic effect type        this item, unchanged
               (Power, Crit, and any future
                effect type)
   ```

   This section defines no stacking, scaling, or interaction behavior for
   `Power`, `Crit`, or any other `effectType` — those remain exactly where this
   item left them: undefined, and a future rule change (`GAME_RULES.md` §20).
   Nor do the exceptions widen `§8.4`'s lifetime independence or `§5`'s
   anti-infinite-chain rule. §8.3's combinations are unchanged, and §8.2's
   structured effect declaration is not extended.

   **ATK modifier interaction with BuffDebuff is resolved.** How a Relic `ATK`
   modifier interacts with a Turn-based `BuffDebuff` `TargetStat = "ATK"`
   modifier (`COMBAT_RULES.md` §5.4) is resolved by the TASK-137 Product-Owner
   decision (D1–D7) and canonically owned by **`COMBAT_RULES.md` §5.6.6**: both
   coexist and compose order-independently by summing signed percentage
   adjustments against permanent Base Pet ATK before a single truncation toward
   zero into `EffectivePetATK`. The two modifier carriers maintain independent
   ownership and lifetimes (`PetState.ATKModifiers[] ≠ PetState.StatusEffects[]`).

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
OnPowerGain        fires when the active Pet's Power increases from a
                    qualifying non-Relic-generated Power gain (§3.1)
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

## 3.1 `OnPowerGain` — the Qualifying Power-Gain Surface

**Decided** (TASK-178, Product Owner decision **Q-2 = B**). This subsection
fixes **what counts as the Power increase** `OnPowerGain` reacts to. It
introduces **no** new Trigger, **no** new Condition form, **no** new Power
source, and **no** new event type: `OnPowerGain` remains the §3 value it already
is, and the Power it observes is the existing Power surface
(`GAME_RULES.md` §12, `COMBAT_RULES.md` §2).

> **`OnPowerGain` reacts to qualifying non-Relic-generated Power gains.** Power
> granted by a Relic effect is **not** itself a qualifying `OnPowerGain` event.

```text
Match / gameplay Power gain
  → qualifying

Other existing non-Relic gameplay Power gain
  → qualifying when it is an existing Power-gain event

Relic effect grants Power
  → NOT qualifying: it does not create another OnPowerGain event
```

1. **The rule, stated once.** A Power gain qualifies iff it originates **outside
   Relic effect resolution**. Power that a Relic's own effect grants — Arcane
   Battery's `+5`, Cascade Core's `+5`, Mana Crystal's `+10`, or any future
   Relic's — does not qualify, and therefore does not fire `OnPowerGain`.
2. **This is what makes the Relic chain terminate.** Power generated by a Relic
   effect does not create another qualifying `OnPowerGain` event **in that Relic
   chain**, so the recursive shape §5's anti-infinite-chain rule forbids cannot
   be constructed:

   ```text
   OnPowerGain
   → Arcane Battery
   → +5 Power
   → OnPowerGain      ← BLOCKED: Relic-generated Power does not qualify
   → Arcane Battery
   → …
   ```

   This applies **regardless of which Relic generated the Power**: the
   exclusion is a property of the gain's origin, not of the Relic observing it.
   A Relic-generated gain therefore cannot re-enter the chain through a
   *different* Relic instance either.
3. **The excluded gains are exactly "Power granted by a Relic effect".** The
   exclusion is stated as that property, not as a per-Relic list: it covers any
   Relic whose effect grants Power (`effectType: Power`, §8.2 item 1), present
   or future, without enumerating them and without this section having to be
   amended when one is added.
4. **No new vocabulary is introduced.** This subsection adds no Trigger
   (§8.5 item 3: §3's closed 12-value list is unchanged), no Condition form
   (§8.1's three forms are unchanged), no Effect (`effectType` remains the
   §8.2 item 1 set), no Target and no Lifetime (§8.3's table is unchanged), and
   no event (`GAME_RULES.md` §16's list is unchanged). It states a qualification
   boundary on an existing Trigger only.
5. **Who owns what.** The Power surface itself — the 0–100 range and the
   resource-generation step that produces it — is `GAME_RULES.md` §12 and
   §17 step 12/step 13's and `COMBAT_RULES.md` §2's, and is referenced, not
   restated. This subsection owns the qualification boundary alone. The
   anti-recursion requirement it serves is §5's.

## 3.2 `OnCascade` — the Event Boundary

**Decided** (TASK-178, Product Owner decision **Q-3 = A**). This subsection
fixes **how many `OnCascade` events one Swap produces**. It introduces **no**
new Trigger, **no** new root-event Trigger, and changes **no** Match-3 cascade
mechanic and **no** Combo accounting.

> **Each actual cascade iteration is an independent `OnCascade` event.**

```text
Swap
 ├─ initial resolution   (MATCH3_RULES.md §4.2 depth 1 — NOT a Cascade)
 ├─ Cascade #1           → one OnCascade event
 ├─ Cascade #2           → one OnCascade event
 └─ Cascade #3           → one OnCascade event
```

1. **The boundary is the Cascade iteration, and `MATCH3_RULES.md` §4.2 owns
   iteration identity.** §4.2 item 1 fixes detection pass depth 1 as the pass run
   on the board produced by the committed Swap and states it is **not** a
   Cascade; §4.2 item 2 fixes `d ≥ 2` as a Cascade, whose index within the Swap
   is `d − 1`; §4.2 item 3 fixes the passes as strictly sequential; §4.3 fixes
   when the loop ends. A pass that detects no match produces no Cascade and
   therefore no `OnCascade` event. This subsection **references** that identity;
   it does not restate, widen, or re-derive it.
2. **Therefore a Relic whose Trigger is `OnCascade` may fire once per cascade
   iteration, not once per Swap.** Each qualifying iteration is its own event,
   and each is evaluated against the state at that iteration's own
   `GAME_RULES.md` §17 step 11 point:

   ```text
   Cascade #1 → Cascade Core: +5 Power
   Cascade #2 → Cascade Core: +5 Power
   Cascade #3 → Cascade Core: +5 Power
   ```

   Three cascade iterations are three independent resolutions of Cascade Core.
3. **Cascades from one Swap are NOT collapsed into one aggregated `OnCascade`.**
   An implementation that emits a single `OnCascade` for a Swap-and-all-its-
   cascades, or that aggregates their effects into one application, does not
   implement this contract.
4. **This is not a self-loop, and §5 is unchanged.** §5 item 2's
   once-per-root-event safeguard already carves this case out explicitly: a
   Relic whose declared Trigger "is itself the kind of event that naturally
   repeats" legitimately fires once per such event, and a Cascade iteration is
   one such event. Each iteration is the qualifying event for its own firing;
   the Relic does not re-trigger from an effect **it itself caused** within one
   iteration. §5's anti-infinite-chain rule therefore continues to operate as
   written and is neither widened nor relaxed.
5. **Power granted by a Cascade Core firing is not a further cascade.** Granting
   Power must not synthesize a Cascade iteration and must not make
   `OnCascade` eligible again within the same iteration. That is §3.1 item 2's
   qualification boundary (a Relic-generated gain is not a qualifying gain) plus
   `MATCH3_RULES.md` §4.2's iteration identity — a Power grant is neither a
   Match nor a detection pass.
6. **No new Trigger and no new root event.** `GAME_RULES.md` §16's event list is
   unchanged (`CascadeCreated` remains the event that reports a cascade), no
   "root-event" Trigger is invented, and `OnMatch`'s own per-Match semantics
   (§3) and the Combo accounting `MATCH3_RULES.md` §6.3 owns are unaffected.

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

**The two boundaries that make item 2 operative** are stated by §3 and are
referenced here rather than restated (TASK-178):

```text
§3.1  OnPowerGain reacts only to qualifying non-Relic-generated Power
      gains — Power granted by a Relic effect is NOT a qualifying gain,
      so a Relic's own Power output cannot re-enter the chain.
      This is what forbids: OnPowerGain → Arcane Battery → +5 Power →
      OnPowerGain → Arcane Battery → …

§3.2  Each actual cascade iteration is an independent OnCascade event
      (MATCH3_RULES.md §4.2 owns iteration identity). This is NOT a
      self-loop: it is item 2's "the declared Trigger is itself the kind
      of event that naturally repeats" case, so a per-Cascade-iteration
      firing is legitimate and item 2 is neither widened nor relaxed.
```

Neither boundary adds a Trigger, a Condition, an effect, a Power source, or an
event. Item 1's one-full-pass limit, item 2's once-per-root-event rule, and
item 3's conflict-reporting requirement are unchanged.

---

# 6. MVP Relic Reference

```text
Relic             Trigger        Condition              Effect
---------------   ------------   --------------------   --------------------------------
Berserker Core    OnMatchCount   MatchCountAtLeast(3)   +5% ATK (Battle)
Mana Crystal      OnMatchCount   MatchCountAtLeast(4)   +10 Power (Immediate)
Assassin Eye      OnCombo        ComboAtLeast(3)        +10pp Crit (NextAttack)
Emergency Core    OnHpBelow      HpPercentageBelow(30)  Heal Card cost −50% (Battle)
Burning Curse     OnBattleStart  —                      +30% Burn damage (Battle)
Combo Fang        OnCombo        ComboAtLeast(5)        +20pp Crit (NextAttack)
Arcane Battery    OnPowerGain    —                      +5 Power (Immediate)
Execution Mark    OnHpBelow      HpPercentageBelow(30)  +15pp Crit (NextAttack)
Cascade Core      OnCascade      —                      +5 Power (Immediate)
Battle Instinct   OnDamageTaken  —                      +10% ATK (NextAttack)
```

Notes:

1. **"Burning Curse"** triggers once per battle at battle start (`OnBattleStart`,
   GAME_RULES.md §17), granting the active Pet a standing `+30%` Burn damage
   modifier with `Battle` lifetime that enhances Burn damage ticks
   (COMBAT_RULES.md §5). The prior static-modifier tension (§6 note 1 / §8.5 item 5)
   is resolved per TASK-176.

   **It modifies Burn damage owned by the Pet, and not Burn damage owned by the
   Boss** (TASK-178, Product Owner decision **Q-4 = C**):

   > **Burning Curse applies to Pet-owned / Pet-sourced Burn damage. It does not
   > modify Boss-owned Burn damage.**

   ```text
   Pet applies Burn to Boss
     → the Pet is the source/owner → Burning Curse applies

   Pet-owned Burn tick resolves
     → +30% applies to that tick's damage

   Boss applies Burn to Pet
     → the Boss is the source/owner → Burning Curse does NOT apply

   Boss-owned Burn tick resolves
     → unmodified Burn damage
   ```

   - **The distinction is Burn source/ownership, not the damage recipient.** The
     test is who applied/owns the Burn instance, never which entity receives the
     damage. A Burn instance the Boss applied and that damages the Pet is
     Boss-owned and therefore outside Burning Curse, even though the Pet is the
     entity taking the damage.
   - **`target: Pet` identifies the Pet as the owned/source context of this
     modifier.** It does **not** mean "increase every Burn tick received by the
     Pet". The `target` vocabulary is §8.3 item 1's: §3 fixes the active Pet as
     the trigger subject, and the effect modifies the Pet's own outgoing Burn
     damage. Burning Curse's `target` is unchanged by this clarification.
   - **The Relic's own contract is unchanged** (TASK-176): `OnBattleStart`, no
     Condition, `BurnDamage` `+30%`, `Percentage`, `target: Pet`, `Battle`
     lifetime, non-stacking, once per battle. The row and §8.5 item 5 are
     unaltered; only the ownership the modifier applies to is now stated.
   - **No second Burn effect system is introduced.** `BurnDamage` remains the
     §8.2 item 1 percentage modifier on the existing Burn Status Effect; it does
     not create, emit, or re-enter a Burn instance, and it does not add a
     parallel DoT path (`COMBAT_RULES.md` §5.2 item 2's refresh-not-stack default
     is unchanged).
2. **"Emergency Core"** re-evaluates continuously (it is "armed" whenever
   HP < 30%, not a one-shot fire) — it modifies Heal Card cost for as long as
   the condition holds, reverting when HP rises back above 30%.
3. **"Execution Mark"** fires via `OnHpBelow` when active Pet HP is below 30%
   (`HpPercentageBelow(30)`), granting `+15pp` Crit to the Pet's next attack
   with `NextAttack` lifetime. It conforms to existing `OnHpBelow` and
   `HpPercentageBelow` semantics.
4. **Provisioned vs. unprovisioned row set.** (TASK-176)
   All 10 Relics are content-defined. The first four (**Berserker Core**,
   **Mana Crystal**, **Assassin Eye**, and **Emergency Core**) were provisioned
   under migrations `20260929152651_ProvisionPetCardRelicContentDefinitions` and
   `20261003074309_StructureRelicDefinitionStructuredColumns`. The remaining six
   (**Burning Curse**, **Combo Fang**, **Arcane Battery**, **Execution Mark**,
   **Cascade Core**, and **Battle Instinct**) are content-defined and ready for
   provisioning in a downstream migration task.

---

# 7. Events

```text
RelicTriggered   emitted each time a Relic's Effect actually applies
```

Part of the Battle Event Model (GAME_RULES.md §16), fired at the point shown
in the Event Resolution Rules (GAME_RULES.md §17, step 11 "Trigger Relics").

`RelicTriggered` reports **that** a Relic's Effect applied and **which** Relic
applied it. Its wire shape is `{ type, relicId }` and carries no effect
summary — the resulting state is delivered through the existing `BattleState`
projection (`SIGNALR_PROTOCOL.md` §3.2.23, §3.2.25, §4; `GAME_STATE.md` §0).
This section fixes no wire member and does not restate the projection.

---

# 8. Trigger, Condition, and Effect Contract

**Canonical owner.** This section owns the machine-readable shape a Relic's
`Trigger`, `Condition`, and `EffectDefinition` must take. `DATABASE.md` §1
stores these values; `SIGNALR_PROTOCOL.md` §3.2.23 fixes the event that
reports them; neither restates this contract.

It is a **representation** contract. It authors no trigger condition, no
magnitude, and no gameplay rule of its own: every value it carries is owned by
§3 (Triggers), §6 (the MVP Relic Reference), `COMBAT_RULES.md` (`RelicCrit`'s
composition), or `GAME_RULES.md` §17 (the resolution order). Where this section
records a value, that value is transcribed from its owner.

## 8.1 `Condition` — Structured Evaluation

**Decided** (TASK-131 **D5**). `Condition` is a **structured** value, not free
text. The defined forms are:

```text
MatchCountAtLeast(N)      the cumulative Match count reached N or more
ComboAtLeast(N)           the current Chain's Combo reached N or more
HpPercentageBelow(N)      the active Pet's HP fell below N percent
```

1. **Every form carries its threshold as `N`, an integer.** The threshold is
   part of the value, never embedded in prose — `"every 3 Matches"` as a string
   is not a valid `Condition`.
2. **Each is evaluated against the current resolution state**, at the point
   `GAME_RULES.md` §17 step 11 executes, and against the specific event being
   processed. `N` is compared to the value that state holds at that moment.
3. **No persistent Relic counters are introduced.** A Relic does not own, carry,
   or accumulate a counter of its own, and no Relic-scoped counter is added to
   active battle state. `MatchCountAtLeast` reads the battle's existing
   cumulative Match count and `ComboAtLeast` reads the existing Combo value
   (`GAME_RULES.md` §5, `GAME_STATE.md` §2.4/§2.6); this contract adds no state
   member to `GAME_STATE.md`.
4. **`Condition` remains optional** (§1). A Relic whose Trigger alone is its
   complete condition carries none.
5. **The `N` values are supplied by §6's rows, not by this section.** For the
   provisioned Relics the transcriptions are: Berserker Core `MatchCountAtLeast(3)`,
   Mana Crystal `MatchCountAtLeast(4)`, Assassin Eye `ComboAtLeast(3)`,
   Emergency Core `HpPercentageBelow(30)`.
6. **`HpPercentageBelow` reads the active Pet's HP** — the subject §3 fixes for
   the `OnHpBelow` trigger. Whether the comparison resolves against current HP
   or a maximum is a property of the percentage form itself and is stated by the
   implementing stage against `GAME_STATE.md` §2.3's HP members; this contract
   fixes the form, the subject, and the threshold carrier, and adds no
   alternative reading.
7. **`Berserker Core`'s "every 3 Matches" is a threshold form, not a modulo.**
   `MatchCountAtLeast(3)` states the condition the §6 row declares. This section
   authors no additional "fires only on the exact Nth Match" or "fires on every
   multiple" rule; §3's `OnMatchCount` description and §1's re-fire default
   govern re-firing, and §5's anti-infinite-chain rule is unchanged.
8. **The observation point — stated once, by reference to its owner.** Every
   form above is read at `GAME_RULES.md` §17 step 11, which §17 fixes after
   step 10 ("Charge Passive") and **before** step 12 ("Generate Resources"),
   step 13 ("Update Power"), and step 14 ("Resolve Player Effects"). The order
   is `GAME_RULES.md` §17's; this item records only what that position means for
   these three forms:
   - **`HpPercentageBelow(N)`** reads the active Pet's HP **before this Swap's
     step-14 player-effect resolution** — before the HP-Gem heal pool
     `COMBAT_RULES.md` §2 item 5 has step 12 generate and step 14 apply. For the
     Swap it is evaluated in, it never reads a post-healing HP.
   - **`MatchCountAtLeast(N)`** reads `BattleState.MatchCount` — the battle's
     cumulative Match count as of step 9 ("Count Matches"), so this Swap's
     Matches are already included.
   - **`ComboAtLeast(N)`** reads `BattleState.Combo` — this Swap's Combo as of
     step 8 ("Update Combo").
   The single post-resolution write-back (`GAME_STATE.md` §5.1) defers when the
   resulting state becomes visible; it does not move this point. How many times
   the stage executes is §3's trigger semantics and §1's re-fire default,
   unchanged; **each** execution, however many there are, reads the state at
   this point. So `Emergency Core`'s §6 note 2 re-evaluation is an evaluation at
   this point, and its reversion is what such an evaluation observes — never a
   re-evaluation taken after step 14 has already moved HP. No form reads a value
   this section does not name, and no counter, snapshot, or state member is
   introduced by this item (item 3).

## 8.2 `EffectDefinition` — Structured `EffectDefinition[]`

**Decided** (TASK-131 **D1**, **D2**, **D3**). `EffectDefinition` is a
**structured array** of effect objects, following the same representation
contract `DATABASE.md` §1 records for `CardDefinition.EffectDefinition`
(TASK-108 D-1/D-2, TASK-111 D-1/D-5). It is **not** verbatim prose.

```json
[ { "effectType": "ATK", "valueType": "Percentage", "value": 5,
    "target": "Pet", "lifetime": "Battle" } ]
```

Each element carries **its own** `effectType` / `valueType` / `value` triple
plus the target and lifetime members §8.3 and §8.4 define.

1. **`effectType` (string)** — which domain effect the Relic applies. The
   defined set is `ATK` | `Power` | `Crit` | `CardCost` | `BurnDamage`, carrying the effect
   identities §6's rows declare. It is the effect identity carrier: the runtime
   must never derive a Relic's effect from parsed prose, from the Relic's
   `Name`, from `RelicDefinitionId` mapping, or from hardcoded per-Relic logic.
   `ATK` is a stat modifier, `Power` is a Power grant (`GAME_RULES.md` §12),
   `Crit` is a Crit-chance increase participating in `COMBAT_RULES.md` §2 item 7's
   `EffectiveCrit` composition, `CardCost` is a Card-cost modifier, and
   `BurnDamage` is a percentage modifier to Burn damage-over-time ticks (`COMBAT_RULES.md` §5).
2. **`valueType` (string)** — how `value` is interpreted:
   `Flat` | `Percentage` | `PercentagePoints` | `Undetermined`.
   `Flat` is an absolute amount; `Percentage` is a proportion of the stat's own
   value; `PercentagePoints` is a proportion expressed in percentage points, the
   interpretation a Crit-chance increase states (`COMBAT_RULES.md` §2 item 2).
   `Undetermined` is **not an interpretation** — it records that the owning
   document states no magnitude yet, and it **remains valid** for such an effect.
   It is why no magnitude has to be invented to make such a row representable.
   A percentage is never pre-resolved to an absolute amount: the value it
   applies to is read from battle state when the effect is applied.
3. **`value` (int)** — the effect's magnitude, transcribed from §6 through the
   `valueType` above. It is present **iff `valueType` interprets one**: an
   `Undetermined` effect carries no `value` member at all, never `0` and never
   `null`, so an unauthored magnitude cannot be read as a number.
4. **Ordering within the array is NOT semantic**, following TASK-111 **D-5**'s
   convention for `CardDefinition.EffectDefinition` and `GAME_STATE.md` §2.3.1
   item 10's for `StatusEffects[]`. No rule reads element positions. A Relic's
   resolution order is §4's equip-slot order, which is a property of the Relic
   sequence, not of an effect's index within one Relic.
5. **A stored value that is not a well-formed structured effect is rejected
   loudly.** There is no fallback magnitude, no default, no prose fallback, and
   no silent no-op for an unrecognized `effectType`, an unrecognized `valueType`,
   a missing `value`, or a missing required member.
6. **This is the representation only.** Reading and applying a Relic effect is
   `GAME_RULES.md` §17 step 11's resolution stage, which is not implemented by
   this document.

## 8.3 Effect Target and Scope Vocabulary

**Decided** (TASK-131 **D3**, extended by TASK-176). Each effect element carries an explicit target
and lifetime vocabulary. The combinations allowed per `effectType` are fixed
below; a combination not listed is not defined and may not be inferred.

| `effectType` | `target` | `lifetime` | `valueType` |
| --- | --- | --- | --- |
| `ATK` | `Pet` | `Battle` | `Percentage` |
| `ATK` | `Pet` | `NextAttack` | `Percentage` |
| `Power` | `Pet` | `Immediate` | `Flat` |
| `Crit` | `Pet` | `NextAttack` | `PercentagePoints` |
| `CardCost` | `Pet` | `Battle` | `Percentage` |
| `BurnDamage` | `Pet` | `Battle` | `Percentage` |

1. **`target` (string)** — which entity the effect modifies. The defined value
   is `Pet`: §3 fixes the active Pet as the trigger subject, and §2 item 1 fixes
   Relics as carried by the active Pet.
2. **`lifetime` (string)** — how long the applied modification persists. A
   value outside the allowed combination for its `effectType` is not defined.
3. **`Immediate` is not a duration.** It denotes an effect applied once, at the
   moment it triggers, which leaves no standing modification behind — Mana
   Crystal's Power grant is applied to `PetState.Power` and the effect itself
   then ends.
4. **`Battle` denotes a standing modification for the remainder of the battle.**
   **`NextAttack`** denotes a modification consumed by the next qualifying owner
   attack, which is the lifetime `scope: "NextAttack"` already carries for the
   Card `Crit` effect (`DATABASE.md` §1; `COMBAT_RULES.md` §2 item 7;
   `ADR-017`). This contract reuses that established boundary and introduces no
   second consumption rule.

   **Where each lifetime is carried, and the single carrier per stat.** The
   applied modification's runtime carrier is not authored here; §8.5 references
   it per effect. Two consequences follow, and both are recorded so this
   vocabulary is not read as creating a carrier (TASK-178, Product Owner
   decision **Q-1 = A**):

   ```text
   ATK   | Battle      → GAME_STATE.md §2.3.7 / §5.1.4
   ATK   | NextAttack  → GAME_STATE.md §2.3.7 / §5.1.4   (the SAME carrier)
   Crit  | NextAttack  → GAME_STATE.md §2.3.4 / §5.1.2
   CardCost | Battle   → GAME_STATE.md §2.3.5 / §5.1.3
   ```

   - **`ATK`'s two lifetimes share one carrier, `PetState.ATKModifiers[]`.** The
     element carries its declared `lifetime`; the collection is therefore **not**
     `Battle`-only. No `NextAttackATKModifiers[]` and no generic
     `NextAttackModifiers[]` is introduced — the `NextAttack` ATK modifier rides
     the existing collection, so no stat's `NextAttack` modification has two
     representations.
   - **`NextAttack` is consumed at the qualifying-attack boundary
     `COMBAT_RULES.md` §3.3 items 7–11 own** — the same boundary the Crit
     modifier uses, not a second one. It is not Turn-based, it does not expire
     on a Trigger, and an unconsumed element persists across Turns until a
     qualifying attack consumes it.
   - **A re-triggering `NextAttack` source refreshes rather than accumulates.**
     The stacking behavior a Relic declares (§8.5) plus the one-entry-per-source
     invariant (`GAME_STATE.md` §2.3.7 item 4) produce that result; this
     contract adds no second mechanism.
5. **No member is defined beyond `target` and `lifetime`.** Additional members
   may not be added without a recorded owner decision.

## 8.4 Effect Lifetime and Trigger Re-evaluation Are Independent

**Decided** (TASK-131 **D6**). A Relic's **effect lifetime** and its **trigger
re-evaluation** are two separate things.

1. **Effect lifetime** answers how long an applied modification persists — §8.3's
   `lifetime` member.
2. **Trigger re-evaluation** answers whether the Relic's Trigger and Condition
   are evaluated again. This remains §1's `Reset/Cooldown` default: a Relic
   re-fires every time its Trigger/Condition is met, with no cooldown, unless
   §6 states otherwise.
3. **A Relic whose effect lifetime has ended is not disabled.** A `NextAttack`
   or `Immediate` effect ending does not stop the Relic's Trigger from being
   re-evaluated on later events; a Relic may fire repeatedly, each firing
   producing its own effect with its own lifetime.
4. **This section adds no cooldown, no charge, and no per-Relic reset state.**
   §5's anti-infinite-chain rule remains the only constraint on repeated firing.
5. **The canonical lifetimes are** (TASK-131 **D6**, TASK-176): Berserker Core `Battle`,
   Mana Crystal `Immediate`, Assassin Eye `NextAttack`, Emergency Core `Battle`,
   Burning Curse `Battle`, Combo Fang `NextAttack`, Arcane Battery `Immediate`,
   Execution Mark `NextAttack`, Cascade Core `Immediate`, Battle Instinct `NextAttack`.

## 8.5 The Canonical Relic Contract

Transcribed from §6 and this section. No value below is authored here.

| Relic | `Trigger` (§3) | `Condition` (§8.1) | `EffectDefinition[]` (§8.2–§8.4) |
| --- | --- | --- | --- |
| Berserker Core | `OnMatchCount` | `MatchCountAtLeast(3)` | `[{ "effectType": "ATK", "valueType": "Percentage", "value": 5, "target": "Pet", "lifetime": "Battle" }]` |
| Mana Crystal | `OnMatchCount` | `MatchCountAtLeast(4)` | `[{ "effectType": "Power", "valueType": "Flat", "value": 10, "target": "Pet", "lifetime": "Immediate" }]` |
| Assassin Eye | `OnCombo` | `ComboAtLeast(3)` | `[{ "effectType": "Crit", "valueType": "PercentagePoints", "value": 10, "target": "Pet", "lifetime": "NextAttack" }]` |
| Emergency Core | `OnHpBelow` | `HpPercentageBelow(30)` | `[{ "effectType": "CardCost", "valueType": "Percentage", "value": 50, "target": "Pet", "lifetime": "Battle" }]` |
| Burning Curse | `OnBattleStart` | `null` | `[{ "effectType": "BurnDamage", "valueType": "Percentage", "value": 30, "target": "Pet", "lifetime": "Battle" }]` |
| Combo Fang | `OnCombo` | `ComboAtLeast(5)` | `[{ "effectType": "Crit", "valueType": "PercentagePoints", "value": 20, "target": "Pet", "lifetime": "NextAttack" }]` |
| Arcane Battery | `OnPowerGain` | `null` | `[{ "effectType": "Power", "valueType": "Flat", "value": 5, "target": "Pet", "lifetime": "Immediate" }]` |
| Execution Mark | `OnHpBelow` | `HpPercentageBelow(30)` | `[{ "effectType": "Crit", "valueType": "PercentagePoints", "value": 15, "target": "Pet", "lifetime": "NextAttack" }]` |
| Cascade Core | `OnCascade` | `null` | `[{ "effectType": "Power", "valueType": "Flat", "value": 5, "target": "Pet", "lifetime": "Immediate" }]` |
| Battle Instinct | `OnDamageTaken` | `null` | `[{ "effectType": "ATK", "valueType": "Percentage", "value": 10, "target": "Pet", "lifetime": "NextAttack" }]` |

1. **`Assassin Eye`'s magnitude is `+10` percentage points** (TASK-131 **D4**),
   resolving the qualitative "Increased Crit chance" of §6. This is the
   `RelicCrit` term `COMBAT_RULES.md` §2 item 7 names in its `EffectiveCrit`
   composition; that composition, its cap, and its consumption boundary are that
   document's and `ADR-017`'s, and are not restated here.
2. **`Emergency Core`'s effect is a Card-cost modifier on the active Pet**
   (`CardCost`, `-50%` as the §6 row states), and its Trigger re-evaluates
   continuously while §6 note 2's condition holds. §8.4 item 3 governs: the
   `Battle` lifetime of the applied modification and the continuous
   re-evaluation of the Trigger are independent, and §6 note 2 is unchanged.

   **What the applied modifier is, and what it is not.** This section declares
   the effect; it does not define where an applied `CardCost` modifier lives,
   how repeated evaluation of the condition affects it, or how its value
   reaches Card casting. Those are owned elsewhere and are referenced, not
   restated:

   ```text
   the declaration itself     §8.2–§8.4 (this section)
   the runtime state carrier  GAME_STATE.md §2.3.5
                              (PetState.CardCostModifiers[], the applied
                               modifier — SourceIdentity +
                               CostReductionPercentage)
   its mutation lifecycle     GAME_STATE.md §5.1.3
                              (create / replace-or-refresh / remove)
   its cost composition and   CARD_RULES.md §3.6
   the value Card casting      (TotalReduction, EffectiveCardCost)
   reads
   ```

   Continuous re-evaluation **re-evaluates**, it does not accumulate: every
   evaluation while the condition holds resolves to the *same* source
   identity, so the applied modifier is replaced/refreshed in place and there
   is exactly one `Emergency Core` entry at any time. Re-evaluation creates no
   second entry, no counter, no queue, and no event. When the §6 note 2
   condition no longer holds, the modifier is removed by the §6 note 2
   reversion semantics; `CARD_RULES.md` §3.6 then reads a TotalReduction that
   no longer includes it. The `-50%` value is the §6 row's, transcribed
   through §8.2's `valueType: Percentage`.
3. **`Trigger` remains §3's closed list** (TASK-131 **D8**, TASK-176). §3 is unchanged and
   no value is added, removed, or reinterpreted. All 10 Relics declare a single primary
   Trigger from §3's closed 12-value list (`OnBattleStart`, `OnMatchCount`, `OnCombo`,
   `OnPowerGain`, `OnHpBelow`, `OnCascade`, `OnDamageTaken`).
4. **`Berserker Core`'s effect is an ATK modifier on the active Pet**
   (`ATK`, `+5%` as the §6 row states, `Battle` lifetime). This section declares
   the effect; it does not define where an applied ATK modifier lives, how
   repeated evaluation of the condition affects it, or how its value reaches the
   Pet's damage. Those are owned elsewhere and are referenced, not restated
   (TASK-136 D1–D12):

   ```text
   the declaration itself     §8.2–§8.4 (this section)
   the runtime state carrier  GAME_STATE.md §2.3.7
                              (PetState.ATKModifiers[], the applied modifier —
                               SourceIdentity + ATKModifierPercentage)
   its mutation lifecycle     GAME_STATE.md §5.1.4
                              (create / replace-or-refresh / remove)
   its ATK composition and    COMBAT_RULES.md §5.6
   the value the Damage        (TotalATKModifierPercentage, EffectivePetATK)
   Pipeline reads
   ```

   The applied modifier's lifetime is `Battle` and its removal boundary is the
   battle's end or its source's removal (`GAME_STATE.md` §5.1.4 item 4). The
   `+5` value is the §6 row's, transcribed through §8.2's
   `valueType: Percentage`; §5.6 owns how that value composes, including its
   sign handling and its rounding.

   **Interaction with BuffDebuff ATK modifiers is resolved.** How this Relic ATK
   modifier interacts with a Turn-based `BuffDebuff` `TargetStat = "ATK"`
   modifier (`COMBAT_RULES.md` §5.4) is resolved by TASK-137 and owned by
   **`COMBAT_RULES.md` §5.6.6**: the two coexist and compose additively as
   signed percentage points against base `PetState.ATK` before a single
   truncation toward zero, with independent carrier lifecycles.
5. **`Burning Curse` resolved to `OnBattleStart`** (TASK-176). The previous static-modifier
   tension is resolved per explicit Product Owner decision: Burning Curse declares `OnBattleStart`
   (firing once at battle start per §3), no extra Condition (`null`), and an effect of
   `+30%` `BurnDamage` with `Battle` lifetime targeting `Pet`. It enhances Burn
   damage-over-time ticks (`COMBAT_RULES.md` §5) for the remainder of the battle. The row is
   now fully content-defined and ready for provisioning in a downstream migration task.

   **It modifies Pet-owned Burn only, and its `target: Pet` is not a "damage
   received" reading** (TASK-178, Product Owner decision **Q-4 = C**). Which Burn
   instances the `+30%` reaches is §6 note 1's rule and is owned there; it is
   referenced, not restated:

   ```text
   the declaration itself      §8.2–§8.4 (this section) — unchanged
   whose Burn damage it        §6 note 1
   modifies                    (Pet-owned / Pet-sourced Burn applies;
                                Boss-owned Burn does NOT apply. The
                                distinction is Burn source/ownership, not
                                the entity receiving the damage)
   the Burn Status Effect      COMBAT_RULES.md §5.1 / §5.2
   it modifies                 (the existing Burn DoT; §5.2 item 2's
                                refresh-not-stack default is unchanged)
   the runtime state carrier   GAME_STATE.md §2.3.9
                               (PetState.BurnDamageModifiers[], the applied
                                modifier — SourceIdentity +
                                BurnDamagePercentage; no `lifetime` member,
                                the lifetime being fixed as `Battle` by
                                §8.3's `BurnDamage` row)
   its mutation lifecycle      GAME_STATE.md §5.1.5
                               (create / replace-or-refresh / remove)
   ```

   **What `target: Pet` does and does not mean here.** By §8.3 item 1 the value
   `Pet` identifies the entity whose own modification this is — for `BurnDamage`
   it identifies the Pet as the **owner/source context** whose outgoing Burn
   damage is modified. It is **not** a statement that every Burn tick the Pet
   *receives* is increased: a Burn the Boss applied to the Pet is Boss-owned and
   receives nothing from this modifier (`§6` note 1). The `target` value is
   unchanged from TASK-176.
6. **`Combo Fang`'s effect is a Crit modifier on the active Pet** (`Crit`, `+20` percentage
   points, `NextAttack` lifetime). Triggered by `OnCombo` with `ComboAtLeast(5)` condition.
   Once consumed by the next qualifying attack, it re-fires when the trigger condition is
   met again.
7. **`Arcane Battery`'s effect is a flat Power grant on the active Pet** (`Power`, `+5`,
   `Immediate` lifetime). Triggered by `OnPowerGain` with no condition. Re-fires on each
   qualifying Power gain.

   **It does not re-trigger itself, and it does not re-trigger another
   `OnPowerGain` Relic** (TASK-178, Product Owner decision **Q-2 = B**). Which
   gains qualify is §3.1's rule and is owned there; it is referenced, not
   restated:

   ```text
   OnPowerGain
   → Arcane Battery
   → +5 Power
   → OnPowerGain      ← BLOCKED: a Relic-granted Power gain does NOT qualify
   → Arcane Battery
   → …
   ```

   The `+5` this Relic grants originates in Relic effect resolution, so it is
   **not** a qualifying `OnPowerGain` event — not for this Relic instance and
   not for any other `OnPowerGain` Relic either (§3.1 items 1–2). Each
   qualifying non-Relic Power gain still resolves independently, and `Immediate`
   (§8.3 item 3) means the `+5` leaves no standing modification behind. No new
   Trigger, Condition, Power source, or event is introduced to achieve this.
8. **`Execution Mark`'s effect is a Crit modifier on the active Pet** (`Crit`, `+15`
   percentage points, `NextAttack` lifetime). Triggered by `OnHpBelow` with
   `HpPercentageBelow(30)` condition. It conforms to the existing `OnHpBelow` and
   `HpPercentageBelow` semantics and re-evaluation model.
9. **`Cascade Core`'s effect is a flat Power grant on the active Pet** (`Power`, `+5`,
   `Immediate` lifetime). Triggered by `OnCascade` with no condition. Re-fires on each
   qualifying Cascade iteration.

   **Each cascade iteration is its own `OnCascade` event, so each iteration
   fires this Relic independently** (TASK-178, Product Owner decision
   **Q-3 = A**). The event boundary is §3.2's rule and is owned there; it is
   referenced, not restated:

   ```text
   Cascade #1 → Cascade Core: +5 Power
   Cascade #2 → Cascade Core: +5 Power
   Cascade #3 → Cascade Core: +5 Power
   ```

   Three cascade iterations are three independent resolutions — the cascades of
   one Swap are **not** collapsed into a single aggregated `OnCascade`. The
   `+5` this Relic grants must **not** synthesize a Cascade iteration or make
   `OnCascade` eligible again within the same iteration (§3.1 item 2's
   Relic-generated-gain exclusion and `MATCH3_RULES.md` §4.2's iteration
   identity). This is not a §5 self-loop: a naturally repeating Trigger fires
   once per occurrence of its own event (§5 item 2). No new Trigger, root event,
   or Cascade mechanic is introduced.
10. **`Battle Instinct`'s effect is an ATK modifier on the active Pet** (`ATK`, `+10%`,
    `NextAttack` lifetime). Triggered by `OnDamageTaken` with no condition. Once consumed
    by the next qualifying attack, it re-fires after each qualifying damage event.

    **Its runtime carrier is `PetState.ATKModifiers[]`, and it is not a second
    collection** (TASK-178, Product Owner decision **Q-1 = A**). This section
    declares the effect; the carrier, its lifecycle, and the consumption boundary
    are owned elsewhere and are referenced, not restated:

    ```text
    the declaration itself        §8.2–§8.4 (this section)
    the runtime state carrier     GAME_STATE.md §2.3.7
                                  (PetState.ATKModifiers[] — the applied
                                   modifier, SourceIdentity +
                                   ATKModifierPercentage + its declared
                                   `Lifetime`. This is the same carrier
                                   item 4 records for Berserker Core's
                                   `Battle` modifier; the two lifetimes
                                   coexist in it, distinguished by the
                                   element's own `Lifetime` member)
    its mutation lifecycle        GAME_STATE.md §5.1.4
                                  (create / replace-or-refresh / consume /
                                   remove)
    its consumption boundary      COMBAT_RULES.md §3.3 items 7–11
                                  (the qualifying owner attack; the SAME
                                   boundary the Crit modifier uses — this
                                   document defines no second one)
    its ATK composition and       COMBAT_RULES.md §5.6 / §5.6.6
    the value the Damage          (TotalATKModifierPercentage, EffectivePetATK)
    Pipeline reads
    ```

    **What a `NextAttack` ATK modifier is and is not**, stated once here because
    §8.3's table newly allows the combination:

    - it applies to the **next qualifying Pet attack** and is **consumed when
      that attack resolves** (`COMBAT_RULES.md` §3.3 item 8's boundary);
    - it does **not** modify the Pet's base ATK permanently —
      `PetState.ATK` is never written (`GAME_STATE.md` §5.1.4 item 7);
    - it does **not** remain active for subsequent attacks: consumption deletes
      it, and later attacks compose without it;
    - it is **non-stacking**: a re-trigger by the same source before consumption
      refreshes that source's one element rather than accumulating a duplicate
      (TASK-176's `Battle Instinct` contract; `GAME_STATE.md` §2.3.7 item 4);
    - it is **not** a Turn-scoped, trigger-expired, or Status Effect
      modification (`GAME_STATE.md` §2.3.7 item 8, `COMBAT_RULES.md` §3.3
      item 11);
    - **no `NextAttackATKModifiers[]` and no generic `NextAttackModifiers[]`
      exists.** Berserker Core (item 4) and Battle Instinct differ only in their
      declared `lifetime` and their Trigger; both flow through this one
      collection and the one composition rule.

## 8.6 Storage Consequence

**Decided** (TASK-131 **D9**). The existing `character varying(128)` column is
**insufficient** for a structured Relic effect array: the `CardDefinition`
precedent already required `jsonb` for the same contract shape
(`DATABASE.md` §1). A **separate schema/storage migration task** is required to
move `RelicDefinition.Condition` and `RelicDefinition.EffectDefinition` to their
structured representation.

This section records the requirement. It performs no migration and changes no
schema: the migration is that separate task's act, and the schema change is
gated on it (`AGENTS.md` §18).

**Status.** The migration has since **landed** (TASK-132): both columns are now
`jsonb` and the four provisioned rows of §8.5 hold the structured values this
section's contract defines. `DATABASE.md` §1 owns the storage shape; this
section's requirement is satisfied and §8.7's status line below is updated
accordingly. No rule of §8.1–§8.5 is changed by that migration — every encoded
value is §8.5's, transcribed.

## 8.7 Startup Status

```text
Relic trigger evaluation        IMPLEMENTED — GAME_RULES.md §17 step 11
Relic effect application        IMPLEMENTED
RelicTriggered emission         IMPLEMENTED — SIGNALR_PROTOCOL.md §3.2.23
Condition evaluation            IMPLEMENTED
EffectDefinition execution      IMPLEMENTED
Structured Relic storage        IMPLEMENTED — §8.6 migration landed (TASK-132)
```

This section defines the contract those stages conform to; the stages themselves
are `GAME_RULES.md` §17 step 11's, executed by the resolution pipeline, and they
are now landed. The last line records storage only.

**The implementation consumes this contract and adds nothing to it.** Every
Trigger the stage evaluates, every Condition form it compares, and every
`effectType` it applies is one of the values §3, §8.1, and §8.2 define; each
effect reaches the runtime carrier §8.5 references for it, the equip-slot
resolution order is §4's, and the once-per-root-event safeguard is §5's. No
Trigger, Condition, effect, magnitude, threshold, lifetime, state member, event,
or wire member is added by it, and no Relic is recognised by its `Name` or its
`RelicDefinitionId` (§8.2 item 1). The four initially provisioned rows (Berserker
Core, Mana Crystal, Assassin Eye, Emergency Core) are executed server-side; Burning
Curse and the five new Relics (Combo Fang, Arcane Battery, Execution Mark, Cascade
Core, Battle Instinct) are now canonically defined content contracts, awaiting
downstream runtime implementation and database provisioning.

**Runtime contracts are now complete for two effect types.** `CardCost`'s runtime
carrier and composition were decided by TASK-134 and applied (`GAME_STATE.md`
§2.3.5/§5.1.3, `CARD_RULES.md` §3.6); `ATK`'s were decided by TASK-136 and
applied (`GAME_STATE.md` §2.3.7/§5.1.4, `COMBAT_RULES.md` §5.6). `Power`'s is
`Immediate` and leaves no standing modification (§8.3 item 3), and `Crit`'s
`NextAttack` representation already existed (`GAME_STATE.md` §2.3.4, `ADR-017`).

**The Relic × BuffDebuff ATK interaction is resolved.** The composition contract
between a Relic Battle-lifetime `ATK` modifier and a Turn-based `BuffDebuff`
`TargetStat = "ATK"` modifier when both are live on the same Pet attack is
resolved by TASK-137 and canonically authored at `COMBAT_RULES.md` §5.6.6.
Coexistence, signed composition, single truncation, and independent carrier
lifetimes are fully specified, and `GAME_RULES.md` §17 step 11 composes both
carriers through that one rule.
