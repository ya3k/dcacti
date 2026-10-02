# Passive Rules

**Version:** 1.2 (§8 — Bạch Hổ's "next attack gains increased Crit chance"
effect is now recorded as a `NextAttack` Crit modifier whose lifetime,
consumption boundary, composition, and source-specific removal are owned by
`COMBAT_RULES.md` §3.3 items 7–10 and whose state is
`PetState.NextAttackCritModifiers[]` (`GAME_STATE.md` §2.3.4), referenced
rather than restated. The Passive table, every Threshold, every trigger type,
every reset behavior, and every `PassiveId` value are unchanged. Prior 1.1: §8 Pet `PassiveId` values recorded per TASK-082
decisions B / R1-3 / R2-9 — `passive-<ascii-kebab-case-name>` of the
owning Pet, scoped to Pet passives only; Boss PassiveIds unchanged)
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

Expands GAME_RULES.md §10 (Passive Rules). Conflicts resolve in favor of
GAME_RULES.md.

---

# 1. Passive Structure

Every Pet has exactly one Passive. A Passive is defined by:

```text
Trigger Type     (default: Match-based; see §3 for alternates)
Threshold         (e.g. "every 5 Matches")
Effect            (what happens when triggered)
Reset Behavior    (default: resets to 0 after trigger)
Visibility        (progress must be shown to the player where practical)
```

---

# 2. Match-Based Charging (Default)

1. Every Match increases the active Pet's Passive progress by 1, regardless of
   whether it came from a direct Swap-match or a Cascade match
   (MATCH3_RULES.md §3–§4).
2. Special Gem detonations (MATCH3_RULES.md §5.5) do NOT count as a Match for
   Passive progress, consistent with them not counting toward Combo/Match
   count either.
3. Progress accumulates across all Matches within a single Cascade resolution.
   After all Matches in the Cascade have been counted, the Threshold is
   evaluated once. If progress ≥ Threshold, the Passive becomes **Ready** and
   triggers at most once per Cascade (no separate "activate" input required
   unless a specific Passive is explicitly designed as player-activated,
   which is out of MVP scope — all MVP Passives are automatic).
4. After triggering, progress resets according to the Passive's Reset Behavior
   (§4): Default resets to 0; Partial Reset reduces progress by Threshold,
   allowing overflow to carry into the next charge (GAME_RULES.md §10.6).
   Any overflow remaining after reset is NOT re-evaluated within the same
   Cascade — it waits for the next Cascade or Match to continue charging.

```text
Cascade produces N Matches
         ↓
Progress: 0 → +N (accumulate all Matches)
         ↓
   Evaluate: progress ≥ Threshold?
         ↓                    ↓
        Yes                  No → no trigger, progress carries
         ↓
   Passive Ready → Trigger (at most once per Cascade)
         ↓
   Reset per §4 (0 or progress − Threshold)
         ↓
   Overflow (if any) waits for next Cascade
```

---

# 3. Alternate Triggers

A Passive may instead use one of the following supported trigger types
(GAME_RULES.md §10, final note). These are exceptions and must be explicitly
declared per-Pet; Match-based is the default when unspecified:

```text
Combo            (e.g. "on Combo ≥ N")
HP Threshold     (e.g. "when HP < 30%")
Damage Dealt     (cumulative or per-instance threshold)
Battle Start     (one-time trigger)
Card Cast        (on casting a specific Card or Card category)
```

Alternate-trigger Passives still follow the Reset Behavior rule (§4) unless
stated otherwise, and still must expose progress/state visibly where the
trigger type supports a meaningful progress display (e.g. HP Threshold shows
as armed/not-armed rather than a counter).

---

# 4. Reset Behavior

1. Default: progress resets to 0 immediately after the Passive triggers.
2. A Passive may instead specify:
   * **Partial reset** (e.g. progress reduces by Threshold rather than to 0,
     allowing "overflow" matches from a single big Cascade to carry into the
     next charge).
   * **No reset / persistent** (rare, must be explicitly justified — e.g. a
     one-time Battle Start Passive).
3. Any non-default reset behavior must be documented on the specific Pet's
   Passive definition (see PET_RULES.md), not assumed.

---

# 5. Multiple Matches in One Cascade Resolution

If a single Swap's Cascade chain produces N matches in one resolution
(MATCH3_RULES.md §4), the Pet's Passive progress increases by N (§2.3 step 1).
After all N Matches have been counted, the Threshold is evaluated once (§2.3
step 3). If progress ≥ Threshold, the Passive triggers **at most once** per
Cascade and applies its Reset Behavior (§4). Any overflow remaining after
reset is NOT re-evaluated within the same Cascade.

**Default Reset example** (Threshold=3, N=7):

```text
Progress before cascade: 0
Cascade produces 7 Matches → progress = 0 + 7 = 7
7 ≥ 3 → Passive triggers (once)
Default Reset → progress = 0
```

**Partial Reset example** (Threshold=5, N=7):

```text
Progress before cascade: 0
Cascade produces 7 Matches → progress = 0 + 7 = 7
7 ≥ 5 → Passive triggers (once)
Partial Reset → progress = 7 − 5 = 2  (overflow carries into next charge)
```

**Multi-crossing example** (Threshold=3, N=7, Partial Reset):

```text
Progress before cascade: 0
Cascade produces 7 Matches → progress = 0 + 7 = 7
7 ≥ 3 → Passive triggers (once)
Partial Reset → progress = 7 − 3 = 4
4 ≥ 3, but trigger already fired → progress carries into next charge
```

In all cases the Passive triggers at most once per Cascade resolution. The
difference between Reset variants is what progress remains after the trigger:
Default Reset clears progress entirely; Partial Reset preserves overflow,
allowing a future Cascade to reach Threshold faster.

---

# 6. Visibility

1. Passive progress must be exposed via a UI-facing value (e.g.
   `7 / 10 Matches`) and/or a `PassiveCharged` event (GAME_RULES.md §16) for
   every Match-based Passive.
2. For alternate-trigger Passives where a numeric counter isn't meaningful
   (e.g. HP Threshold), the game must still surface an armed/ready state.

---

# 7. Events

```text
PassiveCharged     emitted each time progress increases
PassiveTriggered    emitted when the Passive activates and its Effect resolves
```

Both events are part of the Battle Event Model (GAME_RULES.md §16) and must
fire in server-authoritative order relative to Match/Cascade/Combo events per
the Event Resolution Rules (GAME_RULES.md §17, step 10 "Charge Passive").

---

# 8. MVP Passive Reference

```text
Pet          Threshold   Trigger      Effect (summary)
-----------  ----------  -----------  -------------------------------------
Xích Lang    5 Matches   Match-based  Empower next attack + apply Burn
Huyền Quy    6 Matches   Match-based  Gain Shield = 15% Max HP
Bạch Hổ      4 Matches   Match-based  Next attack gains increased Crit chance
Thanh Xà     7 Matches   Match-based  Restore 8% HP
Sơn Hùng     5 Matches   Match-based  Gain temporary Defense
```

All five MVP Pet Passives use the default Match-based trigger and default
full reset (§2, §4). Exact numeric effect magnitudes (Burn amount, Defense
amount, Crit increase %) are balance values and live in config, not in this
document.

**Bạch Hổ's `NextAttack` Crit increase is a Crit modifier, and its rule is
not owned here.** Bạch Hổ's Passive effect ("next attack gains increased Crit
chance") creates a temporary Crit modifier scoped to the next attack — the
same scope Iron Fang's Card Crit element uses (`DATABASE.md` §3 item 1,
`CARD_RULES.md` §4.1). The modifier's lifetime, the qualifying-attack
consumption boundary, the Crit composition it participates in, and
source-specific removal are owned by **`COMBAT_RULES.md` §3.3 items 7–10**,
and the state it is held in is `PetState.NextAttackCritModifiers[]`
(`GAME_STATE.md` §2.3.4). None of that is restated here
(`.ai/workflow/documentation/documentation-change.md` §2).

1. **This Passive's trigger is unchanged.** Bạch Hổ's Threshold (4 Matches),
   its default Match-based trigger, and its default full reset are owned by
   §2/§4 above and by the table; the Crit modifier is created when this
   Passive triggers, at `GAME_RULES.md` §17 step 10 ("Charge Passive"). This
   section does not add, move, or re-time that trigger.
2. **The increase is an independent source.** It is separate from Iron Fang's
   Card Crit value (`CARD_RULES.md` §4.1); the two are neither derived from
   nor shared with each other, both apply additively to the same attack, and
   each is individually removed after that attack consumes it
   (`COMBAT_RULES.md` §3.3 item 10).
3. **The numeric increase remains a config balance value** (paragraph above)
   and is not authored here. Whether the modifier stacks with itself on
   repeated triggers, and how a repeat application behaves, follow the
   modifier's own state rule (`GAME_STATE.md` §5.1.2 item 1) rather than a
   Passive-side stacking rule.
4. **The modifier is not a Status Effect.** It does not use this document's
   progress/threshold model, and it is not a Status Effect instance — see
   `COMBAT_RULES.md` §3.3 item 11. Bạch Hổ's *progress* remains the
   `StatusEffects[]`-independent `PassiveProgress` (`GAME_STATE.md` §2.3); the
   modifier it produces is a separate `PetState` collection.

**Pet `PassiveId` values.** (TASK-082 decisions B / R1-3 / R2-9) A Pet
passive's `PassiveId` is `passive-<ascii-kebab-case-name>` of the owning
Pet's documented name — Pets are the only named anchor, since Pet
passives have no separate name in any document:
```text
Xích Lang  →  passive-xich-lang
Bạch Hổ    →  passive-bach-ho
Huyền Quy  →  passive-huyen-quy
```

This format and the values above are scoped to **Pet passives only**
(R1-3): Boss PassiveIds are unchanged and remain the values fixed by
`BOSS_RULES.md` §6.4. The Thanh Xà and Sơn Hùng rows are deferred with
their Pets (`PET_RULES.md` §8); when their Skill Cards are authored and
their rows are provisioned, their `PassiveId` values are derived by this
same rule. The thresholds for all five Pets are the values in the table above
and are sourced from this document when `PetDefinition.PassiveDefinition`
is filled (`DATABASE.md` §1).
