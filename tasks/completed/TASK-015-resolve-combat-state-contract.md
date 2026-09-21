# TASK-015 — Resolve Combat State Foundation Contract

---

## Metadata

```text
Task ID:           TASK-015
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: N/A
Workflow:          documentation/documentation-change.md
Skills:            documentation-consistency
Dependencies:      None
```

---

## Objective

Resolve the minimum set of genuinely missing combat values required before the
Combat State Foundation implementation task can be created. This task updates
authoritative game-rule documentation only — it does not implement combat
code.

---

## Context

The Combat State Foundation (adding HP/ATK/DEF/Power to `PlayerState`,
`BossState` to `BattleState`, resource generation from Gem matches, and the
damage pipeline) is the next implementation stage. However, `AGENTS.md` §7
prohibits implementing rules that don't exist in authoritative documentation,
and `AGENTS.md` §20 requires stopping when rules are missing.

---

## Correction Notice — Stale Task Defect (Revision 2)

**This task was previously written against a document structure that does not
exist.** An earlier revision of this file cited `COMBAT_RULES.md §6 (Balance
Constants)` as an authoritative source and as the write target in its Objective,
Authoritative Sources, Scope, and Acceptance Criteria.

```text
CLAIM (revision 1):  COMBAT_RULES.md §6 is "Balance Constants"
ACTUAL:              COMBAT_RULES.md §6 is "Power" — a three-line section
                     that deliberately delegates to GAME_RULES.md §12 and
                     CARD_RULES.md §3.
EVIDENCE:            COMBAT_RULES.md line 179 ("# 6. Power").
                     A repository-wide search for "Balance Constants" across
                     docs/ returns zero matches.
```

**No such section exists in any document, and this task does not create one.**
All `§6 (Balance Constants)` references have been removed. The values in scope
are assigned to the sections that **already own each concept**, preserving
single ownership per `documentation/documentation-change.md` §2.

Revision 1 also treated three already-resolved areas as gaps. They are
recorded below as **RESOLVED — OUT OF SCOPE**, with the owning section that
already states them.

---

## Authoritative Sources

The values in scope are owned by the sections listed below. Each concept has
exactly one owner; this task edits only those owners.

```text
Concept                        Owning section (canonical)
-----------------------------  ------------------------------------------
Player stats (HP/MaxHP/ATK/    docs/01-game-design/COMBAT_RULES.md §1.1
  DEF/Crit) — the field list    (labels each field; leaves Crit's value as
                                 "default base value: configuration")
Boss stats (field list)         docs/01-game-design/COMBAT_RULES.md §1.2
Gem resource conversion         docs/01-game-design/COMBAT_RULES.md §2
  (per-Gem output, tier rates)   (tier multiplier table + §2 items 1–4)
Defense mitigation constant K   docs/01-game-design/COMBAT_RULES.md §3.2
Crit multiplier                 docs/01-game-design/COMBAT_RULES.md §3.3
Power (range, generation,       docs/01-game-design/GAME_RULES.md §12
  spend validation)              docs/01-game-design/COMBAT_RULES.md §6
                                 docs/01-game-design/CARD_RULES.md §2, §3, §4.1
```

Supporting (read-only) sources:

```text
docs/01-game-design/GAME_RULES.md §6 (Gem Rules), §12 (Power), §14 (Combat),
                                    §17 (resolution order), §20 (rule change)
docs/01-game-design/MATCH3_RULES.md §5.7 (resource generation hooks)
docs/01-game-design/ELEMENT_RULES.md §2.2 (element modifiers — already defined)
docs/01-game-design/BOSS_RULES.md §1 (Boss structure), §6 (MVP Boss reference)
docs/02-technical/GAME_STATE.md §2.2 (PlayerState), §2.4 (BossState)
docs/02-technical/TDD.md §6 (determinism & RNG)
docs/01-game-design/PASSIVE_RULES.md §8, PET_RULES.md §8 (percent-of-MaxHP
                                    precedents, e.g. Huyền Quy 15% Max HP)
```

---

## Already Resolved — OUT OF SCOPE

Revision 1 listed the following as missing. Each is **already stated in its
owning document**; this task changes none of them and only confirms the
existing text.

### 1. POWER Gem generation rate — RESOLVED

```text
COMBAT_RULES.md §2, line 47:  "POWER Gem  +10 Power (flat, per GDD example)"
```

`+10` per Gem is already documented. No confirmation work is required.

### 2. Match-tier multipliers applying to resource generation — RESOLVED

`COMBAT_RULES.md` §2 already states, above the table, that the multipliers are
"applied to the above base output, per Gem consumed in that match", and that
the table "is the sole source of truth for match-tier output, and it is the
rate every other document references."

```text
Match 3   → 1.0×
Match 4   → 1.5×
Match 5   → 2.0×
L/T       → 1.25×
```

The table's scope — including that Special Gem activation clears generate at
the Match-3 base rate and that Match 6+ has no multiplier of its own — is
already fixed by §2 items 1–4. Revision 1's Acceptance Criterion asking for
this to be "confirmed" is satisfied by existing text and is removed.

### 3. Card and Skill Power spend rules — RESOLVED

```text
CARD_RULES.md §2:      Heal 20 Power · Shield 20 Power · Power Charge 0 Power
CARD_RULES.md §4.1:    Xích Lang 100 · Huyền Quy 80 · Bạch Hổ 100
CARD_RULES.md §3:      cast validation (sufficient Power, rejection, order)
GAME_RULES.md §12:     Power range 0–100, cap invariant
```

Costs and spend validation are already documented. Revision 1's criterion
"or confirmed as already documented in CARD_RULES.md" is satisfied.

### 4. Stale attribution in `COMBAT_RULES.md` §2 — FLAGGED, NOT FIXED HERE

```text
COMBAT_RULES.md §2 line 47:
    "POWER Gem  +10 Power (flat, per GDD example)"
```

**Defect:** the parenthetical attributes the `+10` value to a GDD example, but
`GDD.md` §9 (Power System) contains **no numeric example** — it states only
that Power "is generated by matching POWER Gems, Special Matches, and Relics."
Line 84 of the same section independently asserts that "GDD.md intentionally
contains no exact numbers." The `+10` value itself is real and correctly owned
by `COMBAT_RULES.md` §2; only the **attribution** is unsupported.

**Classification:** stale reference / incorrect provenance
(`documentation-consistency` Mode A item 4). The value is not in question and
no rule changes — the minimal correction is to drop the parenthetical, leaving
the rate owned by `COMBAT_RULES.md` §2 alone.

**Action in this task:** flagged only. `COMBAT_RULES.md` is **not modified** by
this task. Per the task instruction, this correction requires its own separate
documentation correction task, or explicit human authorization to fold it into
the value-resolution task once that is unblocked.

---

## In Scope — Genuinely Unresolved Values

Only the following values are missing. **No document derives any of them**, and
this task does not invent them.

### 1. Initial Player stats

`COMBAT_RULES.md` §1.1 lists the fields but supplies no starting values:

```text
HP        current health                          — no initial value documented
Max HP    maximum health                          — no initial value documented
ATK       attack power                            — no initial value documented
DEF       defense                                 — no initial value documented
Crit      critical hit chance (%)                 — "default base value:
                                                     configuration"
```

`Crit` is explicitly deferred to configuration by §1.1; the other four are
absent entirely. **None is derivable** — no document states a player stat
baseline, and `GDD.md` §11 confirms only that the stats exist.

### 2. ATK / DEF / HP Gem conversion rates

`COMBAT_RULES.md` §2's table describes each Gem's **destination** but gives no
number for three of the four types:

```text
ATK Gem    "contributes to next offensive action's Base Damage pool"  — no rate
DEF Gem    "contributes to temporary Defense / mitigation pool"       — no rate
HP Gem     "contributes to passive Heal pool"                         — no rate
POWER Gem  +10 Power (flat)                                           — RESOLVED
```

The tier multipliers (§2) scale these outputs but do not define them. The
table's own header calls itself "MVP defaults," which is aspirational: only the
POWER row currently carries a value.

### 3. Defense mitigation constant `K`

`COMBAT_RULES.md` §3.2 states the formula shape and explicitly forbids
invention:

```text
Mitigated Damage = Pre-Defense Damage × ( K / (K + DEF) )
```

> "This is a placeholder formula shape; the authoritative constant and any
> alternate formula (flat reduction vs. percentage) must be finalized in this
> file before implementation and must not be invented ad hoc by an
> implementer."

`K` is therefore a documented **blocking** unknown, and §3.2 names itself as
its owner. Whether the placeholder formula shape is retained or replaced by a
flat-reduction formula is also part of this decision and is owned by §3.2.

### 4. Boss initial stats — DEFERRED pending a scope check

`COMBAT_RULES.md` §1.2 and `GAME_STATE.md` §2.4 fix the Boss field list
(`HP / MaxHP / ATK / DEF`), and `BOSS_RULES.md` §6 names three MVP Bosses
(Hỏa Long, Thủy Ma, Mộc Yêu) — but no document supplies Boss numbers, and two
of the five MVP Bosses are not yet content-defined (`BOSS_RULES.md` §6).

**Disposition for this task:** `BOSS_RULES.md` §6 is the owning document for
Boss content, and Boss passive/skill mechanics are already declared out of
scope. Boss initial stats are therefore marked **DEFERRED**, with the
requirement that they be resolved before any implementation step that creates
a `BossState`. The Combat State Foundation's *first* step (Player stats, Gem
conversion, mitigation) does not require them; the step that adds `BossState`
does. Confirm this staging boundary rather than resolving Boss numbers here.

---

## Scope

### In Scope

- Determine initial Player **HP / MaxHP / ATK / DEF / Crit** values for a
  standard battle.
- Determine the per-Gem conversion rate for **ATK / DEF / HP** Gems, expressed
  as per-Gem output at the Match-3 tier (the tier multipliers are already
  fixed and apply on top).
- Determine the mitigation constant **`K`** (and confirm or replace the §3.2
  formula shape).
- Record each resolved value **in the section that already owns it** —
  `COMBAT_RULES.md` §1.1 (player stats), §2 (Gem conversion), §3.2
  (mitigation) — as configuration values, not hardcoded constants.
- Mark **Boss initial stats** as DEFERRED with a rationale and a named
  unblocking condition.

### Out of Scope

- **Creating any "Balance Constants" section.** No such section exists and none
  is created by this task.
- POWER Gem generation rate, match-tier multipliers, and Card/Skill Power cost
  confirmation — already resolved (see "Already Resolved" above).
- The `+10 Power (flat, per GDD example)` attribution fix in `COMBAT_RULES.md`
  §2 — flagged, needs its own correction task.
- Boss passive/skill mechanics (`BOSS_RULES.md` — separate task)
- Boss initial stats (DEFERRED by this task)
- Relic interactions (`RELIC_RULES.md` — separate task)
- Status Effects system (`COMBAT_RULES.md` §5 — separate task)
- Healing/Shields implementation details (`COMBAT_RULES.md` §4 — separate task)
- DamageCalculated wire schema (`GAME_EVENTS.md` — separate task)
- Any code changes
- Redis/PostgreSQL persistence decisions

---

## Acceptance Criteria

- [ ] Initial Player HP/MaxHP/ATK/DEF/Crit values are documented in
      `COMBAT_RULES.md` **§1.1**, with clear labels as configuration values.
      (Not §6 — §6 is Power.)
- [ ] ATK/DEF/HP Gem conversion rates are documented in `COMBAT_RULES.md` **§2**,
      with explicit per-Gem output for each Gem type at the Match-3 tier.
- [ ] Mitigation constant `K` is documented in `COMBAT_RULES.md` **§3.2**, and
      the placeholder-formula caveat there is replaced by the finalized
      decision (retain the formula shape with a stated `K`, or state the
      replacement formula).
- [ ] Match-tier multipliers are **referenced**, not restated, as already fixed
      by §2; no second copy of the table is introduced.
- [ ] Card/Skill Power costs are **referenced** as already owned by
      `CARD_RULES.md` §2/§4.1; no restatement.
- [ ] Boss initial stats are explicitly marked **DEFERRED**, with the
      unblocking condition named (`BossState` creation), per §"In Scope" item 4.
- [ ] **No values are invented.** Every value is either supplied by a human
      (per `GAME_RULES.md` §20) or already present in an authoritative
      document. No plausible-looking default is proposed as a placeholder.
- [ ] **No new section is created**, and no existing section is renumbered.
- [ ] `AGENTS.md` §7 compliance: no gameplay rule is added that isn't backed by
      an authoritative source.
- [ ] Each value is documented in exactly one owning section; no duplicate
      definition is introduced (`documentation/documentation-change.md` §2).

---

## Affected Areas

```text
[ ] Domain (GameServer.Domain/)
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[ ] Tests (tests/)
[x] Documentation (docs/) — COMBAT_RULES.md §1.1, §2, §3.2 only
[ ] ADR (docs/03-decisions/ADR/)
```

---

## Implementation Notes

1. **This is a documentation task, not an implementation task.** No code is
   written, no tests are created.
2. **Values must be configuration, not hardcoded constants.** Per
   `COMBAT_RULES.md` §2 and `ELEMENT_RULES.md` §2.2, balance values are
   configuration. Document them as such.
3. **Every value in scope requires human input before it can be written.**
   Unlike TASK-004A and TASK-005A — where each contract item was derivable from
   existing documentation — **nothing in this task's scope is derivable.**
   Supplying these numbers is a balance design decision governed by
   `GAME_RULES.md` §20. Per `AGENTS.md` §23, the agent must not author them:

   ```text
   REQUEST → AI ASSUMPTION → NEW DESIGN → CODE        ✗ forbidden
   ```

   The task therefore does not proceed to a documentation edit until a human
   supplies the values. Producing a plausible stat spread
   (e.g. "HP 1000, ATK 50") and asking for rubber-stamp approval would be
   designing on the project's behalf.
4. **Placement — one concept, one owner.** Do not consolidate the values into
   a new section for convenience. Each value goes to the section that already
   owns it:

   ```text
   Player HP/MaxHP/ATK/DEF/Crit   → COMBAT_RULES.md §1.1
   ATK/DEF/HP Gem conversion      → COMBAT_RULES.md §2
   Mitigation K                    → COMBAT_RULES.md §3.2
   ```

   `COMBAT_RULES.md` §6 (Power) is left untouched: it already delegates to
   `GAME_RULES.md` §12 and `CARD_RULES.md` §3, and the rates it points at are
   already stated in §2.
5. **`Crit` has two distinct unknowns.** §1.1 leaves the base **chance** to
   configuration; §3.3 already fixes the Crit **multiplier** at `1.5×`
   (configuration). Only the base chance is in scope.
6. **Relative-scale consistency.** `CARD_RULES.md` §2's Heal/Shield effects are
   expressed as **20% Max HP**, `PASSIVE_RULES.md` §8 / `PET_RULES.md` §8 use
   **15% Max HP**, and `RELIC_RULES.md` §6's Emergency Core triggers at
   **HP < 30%**. These are percentile and therefore scale-independent — they
   constrain nothing numerically, but they are the only existing combat scale
   anchors and are worth stating when the human chooses values.
7. **Do not touch the §2 attribution defect.** The
   `+10 Power (flat, per GDD example)` parenthetical is flagged in
   "Already Resolved" item 4 and requires its own correction task.
8. **If a value cannot be supplied, STOP and report** — do not substitute a
   guess (`AGENTS.md` §7, §20).

---

## Testing Requirements

### Test Types Required

```text
[ ] Unit tests          — n/a (documentation task)
[ ] Integration tests   — n/a
[ ] Gameplay scenarios  — n/a
[ ] API tests           — n/a
[ ] Realtime tests      — n/a
[ ] Persistence tests   — n/a
```

### Verification

The task is complete when:

1. All Acceptance Criteria are met.
2. Each resolved value appears **only** in its owning section.
3. No "Balance Constants" section has been created.
4. A human has supplied and approved every value (all are proposed rather than
   derived).
5. The `+10 Power (flat, per GDD example)` defect is recorded as an open item
   with a follow-up task, not silently fixed.

---

## Stop Conditions

- **HUMAN INPUT REQUIRED (primary).** Every value in scope is non-derivable.
  If the values are not supplied by a human, **STOP** and report the gap.
  Do not invent values — including "obvious" defaults like `HP = 1000` or
  `K = 100` — and do not present a proposal as though it were derived.
- If resolving these values reveals a conflict between authoritative
  documents: **STOP** per `AGENTS.md` §4.
- If a value's owning section is unclear, or two sections both plausibly own
  one value: **STOP** and report the structural ambiguity
  (`documentation-change.md` §3) rather than choosing a location.
- If the task's own premises contradict the repository again (as the
  `§6 Balance Constants` reference did): **STOP** and correct the task file
  before editing `docs/`.
- If the task scope expands beyond the three value categories listed in
  "In Scope": **STOP** and report the expansion — create a separate task.
- If Boss stats turn out to be required by the immediate next implementation
  step (i.e. the staging boundary in "In Scope" item 4 is wrong): **STOP** and
  report, rather than resolving them here.

---

## Dependencies

- None technically. **Blocked on human balance-decision input** for every value
  in scope — see Stop Conditions.

---

## Completion Evidence

### Summary

TASK-015 is complete. All nine missing combat values have been documented in
their owning sections within COMBAT_RULES.md. The values are confirmed as
MVP baseline configuration — not permanent invariants — and future Pet
progression (Level, Star, Tier) may produce different actual Battle Stats.

### Changes

- COMBAT_RULES.md §1.1: Added MVP default values for HP (1000), MaxHP (1000),
  ATK (50), DEF (25), Crit (5%), plus a paragraph clarifying these are
  baseline configuration subject to progression.
- COMBAT_RULES.md §2: Replaced conceptual gem descriptions with concrete
  rates: ATK Gem +10 Base Damage, DEF Gem +5 Defense pool, HP Gem +20 Heal
  pool.
- COMBAT_RULES.md §3.2: Replaced placeholder caveat with finalized constant
  K = 100 (configuration). Removed "must be finalized before implementation"
  language.

### Documentation Consulted

- COMBAT_RULES.md §1.1, §2, §3.2, §6 (existing Power rules)
- GAME_RULES.md §12 (Power range), §14 (Combat)
- CARD_RULES.md §2 (Basic Card costs), §4.1 (Pet Skill costs)
- PET_RULES.md §8 (MVP Pet precedents — percent-of-MaxHP anchors)
- PASSIVE_RULES.md §8 (percent-of-MaxHP precedent)
- ELEMENT_RULES.md §2.2 (element modifiers — already defined)
- TASK-015 file (correction notice, ownership map)

### Documentation Changed

- docs/01-game-design/COMBAT_RULES.md — three sections updated (§1.1, §2,
  §3.2). No other files changed. No sections created or renumbered.

### Remaining Issues

- Boss initial stats remain DEFERRED. Required before the implementation
  step that creates BossState. Owning document: BOSS_RULES.md §6.
- The "+10 Power (flat, per GDD example)" attribution defect in §2 line 53
  is flagged but not fixed here (requires separate correction task).
- Player starting Power value (default 0 or otherwise) is not explicitly
  stated in §1.1 — may need confirmation before implementation, though
  0 is the only logical default given the 0–100 range.

### Agent

opencode/mimo-v2.5-free

### Workflow Used

documentation/documentation-change.md

### Skills Used

documentation-consistency

### Status

DONE — all Acceptance Criteria satisfied. All values written to owning
sections. No duplicates introduced. No code or tests changed.

---

## Handoff

TASK-015 is complete. The Combat State Foundation implementation task
(TASK-016 or equivalent) can now be created with the following values
documented:

- Player: HP=1000, MaxHP=1000, ATK=50, DEF=25, Crit=5%
- Gem conversion: ATK +10, DEF +5, HP +20 (per Gem, Match-3 tier)
- Mitigation: K = 100 (percentage-based formula)
- Boss stats: DEFERRED (required before BossState creation step)
