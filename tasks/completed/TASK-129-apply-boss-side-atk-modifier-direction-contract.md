# TASK-129 — Apply the Boss-Side `BuffDebuff` ATK Modifier Direction Contract (TASK-128 Decision)

<!--
  GEN-TASK EXECUTION MANIFEST — DOCUMENTATION-RESOLUTION TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  THIS TASK APPLIES A DECISION THAT IS ALREADY MADE. It decides nothing.
  The decision was supplied by the Product Owner and recorded, verbatim, in
  TASK-128 ("Decision — PRODUCT OWNER DECISION (RECORDED)"). This task
  transcribes that recorded decision into its canonical owner documents.

  PROVENANCE: the ambiguity was surfaced by the TASK-127 review (CHANGES
  REQUIRED, Major Finding 1 — the implementation branched on the SIGN of
  Magnitude, an invented semantic). TASK-127 correctly STOPPED per its own
  §8 stop condition 7. TASK-128 obtained and recorded the Product Owner
  decision. This task applies it. TASK-127's rework consumes the result.

  TASK-128 §7 (Handoff) names this task exactly: "A SUBSEQUENT
  DOCUMENTATION-RESOLUTION TASK (not created here), which applies the
  recorded decision to its canonical owners per
  documentation/documentation-change.md §3." This is that task.

  BOUNDARY: this task writes docs/01-game-design/COMBAT_RULES.md §5.5.1 and
  (if §5 below confirms it) docs/02-technical/GAME_STATE.md §2.3.1 item 12.
  It touches NO source file, NO test file, NO ADR, and NO existing task file.
  It implements nothing.
-->

---

## Metadata

```text
Task ID:           TASK-129
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change `docs/` content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md. The deliverable is an
                   authored contract statement at the canonical owner. See
                   "Type classification note". NOT GAMEPLAY-CHANGE: this task
                   changes no gameplay value — every value it states
                   (`+20%`, `30`, `150`) is authored elsewhere and is carried
                   through unchanged. It records an already-decided rule at its
                   owner.)
Status:            DONE
Risk:              LOW–MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION: "LOW for
                   corrections, MEDIUM if it affects a cross-referenced
                   contract". MEDIUM: §5.5.1 is a cross-referenced contract —
                   COMBAT_RULES.md §3.4, BOSS_RULES.md §6.2.1, GAME_STATE.md
                   §2.3.1, and TASK-127's implementation all depend on it.)
Priority:          HIGH (the sole remaining input for the TASK-127 rework,
                   which is the Boss Skill damage half of ROADMAP.md Phase 1's
                   "Boss Response (Passive → Skill → Attack → Victory/Defeat)"
                   and is currently BLOCKED on this contract.)
Primary Agent:     review (TASK_TYPES.md §5 Domain × Type → Documentation /
                   DOCUMENTATION → Review Agent; and TASK_TYPES.md §2
                   DOCUMENTATION names the Review Agent. The Review Agent's
                   own contract permits `documentation/documentation-change.md`
                   — .ai/agents/review.md §Allowed Workflows — and forbids it
                   to change game rules, which is exactly this task's posture:
                   the rule is already decided, so the task authors nothing.)
Supporting Agents: gameplay (content accuracy — the Boss/Combat domain the
                   rule belongs to; .ai/agents/review.md Handoff "Review →
                   Specialist Agent"). N/A for any decision — no domain agent
                   may supply or alter the decision, only verify that the
                   applied text matches TASK-128 verbatim.
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (3 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-128 (DONE — the recorded Product-Owner decision this
                   task applies. IMMUTABLE; READ-ONLY; must NOT be modified,
                   re-statused, moved, or rewritten),
                   TASK-127 (BLOCKED — the downstream implementation task this
                   task unblocks. IMMUTABLE; READ-ONLY; must NOT be modified.
                   Its §"Rework Session — Review Finding 1" records the
                   blocker),
                   TASK-119 (the PET-side §5.4 contract. IMMUTABLE;
                   read-only; its §5.4.1 semantics must NOT change),
                   TASK-125 / TASK-126 (the Boss Skill Step-1 composition.
                   IMMUTABLE; read-only; NOT reopened),
                   TASK-118 (the Boss Skill secondary effects, incl. Root.
                   IMMUTABLE; read-only; NOT modified)
Blocks:            TASK-127 rework, and through it the Boss Skill damage half
                   of ROADMAP.md Phase 1.
Estimate:          Simple–Normal (2 document sections; no code, no tests; the
                   decision is this task's INPUT, not its output)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE`. A
`GAMEPLAY-CHANGE` (`TASK_TYPES.md` §2) "authorize[s] *changing* existing rules
or *adding* new ones through the proper design-change branch". This task does
neither: the rule was changed/decided by the Product Owner in TASK-128, and the
job here is to write the decided rule **once**, at its canonical owner, so the
contract is no longer ambiguous. This is the same shape as TASK-126 (which
applied TASK-125's decision at `COMBAT_RULES.md` §3.4) and TASK-119 (which
authored the Pet-side §5.4 rule from a recorded decision).

**This task creates no ADR.** Checked `docs/03-decisions/README.md` §8, which
records the directly analogous precedent: the Pet-side `BuffDebuff`
`TargetStat = "ATK"` consumption gap "was **not** an ADR-level open item: the
Product Owner's decision (TASK-119 D-1–D-5) resolves it inside the existing
`StatusEffects[]` model, so it introduces **no** new battle-state concept and
requires **no** ADR." TASK-128 §6.7 resolved the identical question the same
way — sign of the existing `Magnitude` field, no new member, no new
representation. There is no persistence change, no realtime change, no
module-boundary change, and no new battle-state concept. `AGENTS.md` §18 is
therefore not triggered. If the applying agent finds evidence that an ADR *is*
genuinely required, that is a Stop Condition reported, not authored here.

**This task authors no value.** The only numbers it may state are the ones the
authoritative documents already carry and TASK-128 recorded: Rage's `+20%`
(`BOSS_RULES.md` §6.2.1), Root's `30` (`BOSS_RULES.md` §6.3.1 item 3 /
`BossDefinitions.cs`), Flame Burst's `150` (`BOSS_RULES.md` §6.3.1 item 1),
and `BossState.ATK = 100` (`COMBAT_RULES.md` §1.1). **No new value is
introduced.** The formula it writes is TASK-128's recorded formula, not a new
one.

---

## Objective

Author the Product Owner's decided Boss-side `BuffDebuff` ATK modifier
direction contract at its canonical owner, so that `COMBAT_RULES.md` §5.5.1
states the percentage direction, the direction representation, and the exact
`EffectiveBossATK` formula — and so that `GAME_STATE.md` §2.3.1's
owner-reference stops attributing the whole `Magnitude` → `TargetStat` concept
to the Pet-side rule alone.

The task is complete when:

```text
TASK-128 recorded decision (verbatim, immutable)
      ↓
Applied at the canonical owner: COMBAT_RULES.md §5.5.1
      ↓
GAME_STATE.md §2.3.1 owner-reference corrected to name both owners
      ↓
No other document changed; no value, rule, or representation altered
      ↓
Stop — TASK-127 rework is a separate task
```

Nothing beyond the two named sections is edited, and no implementation is
performed.

---

## Authoritative References

### The decision being applied (READ ONLY — this task transcribes it)

- `tasks/backlog/TASK-128-resolve-boss-side-atk-modifier-direction-contract.md`
  — **the decision source.** Its §"Decision — PRODUCT OWNER DECISION
  (RECORDED)" carries the eight §6.x coverage items, and its §"§6A. Decision
  Record" carries the coverage mapping (8 of 8 resolved). **This is the
  authoritative input. Do not modify it. Do not reinterpret it. Do not fill in
  or extend anything it left as NOT SUPPLIED** (it records that no rationale
  accompanied the decision — do **not** invent one while applying it).

### The canonical owners (MODIFIED by this task)

- `docs/01-game-design/COMBAT_RULES.md` **§5.5.1** (the Boss ATK Modifier
  Rule) — the **canonical owner** of how a Boss-side `BuffDebuff` `Magnitude`
  reaches `BossState.ATK`. It currently states the consumption point, the
  flow, `EffectiveBossATK = the modified BossState.ATK`, the truncation
  convention, and — verbatim — that "This rule authors **no percentage of its
  own**; the active instance's `Magnitude` supplies it". It authors **no**
  formula and **no** direction. **This is where the decided formula and
  direction statement are written.**
- `docs/02-technical/GAME_STATE.md` **§2.3.1 item 12** (and its **item 2**,
  which states the same ownership) — the `StatusEffect` instance schema. Its
  item 2 delegates `Magnitude`'s meaning to the effect's rule document and
  currently names **only** `COMBAT_RULES.md` §5.4; its item 12 says "What a
  `BuffDebuff` `Magnitude` does to its `TargetStat` is owned by
  `COMBAT_RULES.md` §5.4". Both now name an incomplete owner set — see §5.

### Documents that must NOT change (verify, do not edit)

- `docs/01-game-design/COMBAT_RULES.md` **§5.4 / §5.4.1 / §5.4.2 / §5.4.3 /
  §5.4.4 / §5.4.5** — the **Pet-side** rule. §5.4.1 item 3's
  `EffectiveATK = truncate( ATK × (100 − |Magnitude|) / 100 )` is **unchanged**.
  TASK-128 §6.6 requires explicit confirmation that this decision does not
  replace, modify, or redefine it.
- `docs/01-game-design/COMBAT_RULES.md` **§3.4** ("Boss Skill Step-1
  composition") — the resolved TASK-125/TASK-126 contract. Its worked example
  (`100` + `+20%` → `120`; `120 + 150 = 270`) is **preserved**, not reopened.
- `docs/01-game-design/COMBAT_RULES.md` **§5.5.2 / §5.5.3 / §5.5.4 / §5.5.5**
  — damage scope, boundaries, non-destructiveness, and duration/reapplication.
  §5.5.5's refresh-not-stack default is the **existing** stacking rule; this
  task authors no new one.
- `docs/01-game-design/BOSS_RULES.md` **§6.2.1** (Rage's `+20%` / `3 turns`)
  and **§6.3.1** (the authored Base Damages `150` / `120` / `100`, and Root's
  `-30%` Pet ATK debuff) — authored balance values, **unchanged**.
- `docs/02-technical/GAME_STATE.md` **§2.3.1 items 1, 3–11** and the §2.3.1
  member tree — no member is added, removed, renamed, retyped, or made
  optional/required differently. **No schema change.**
- `docs/01-game-design/GAME_RULES.md` §16 (the closed event list) and §17 (the
  resolution order) — unchanged; no event and no step is added.

### Governance and process

- `.ai/workflow/documentation/documentation-change.md` §1 (the flow), §2 (no
  duplication, ever), §3 (canonical owner), §4 (review + completion).
- `docs/AGENTS.md` §2 (precedence), §4 (conflict resolution), §7 (missing
  rule), §16 (task discipline), §17 (documentation change rule), §18
  (architecture change rule — not triggered; see Metadata), §20, §22.
- `.ai/README.md` §6 (source of truth), §13 (stop conditions + report format),
  §18 (documentation update policy — classify before "fixing").
- `docs/03-decisions/README.md` §3 (an ADR never overrides a technical
  document), §7 (ADR index), §8 (known open items — the TASK-119 precedent).
- `tasks/README.md` §9 (no business-rule duplication in task files), §12 (skill
  budget); `tasks/TASK_TYPES.md` §2/§3/§4/§5; `tasks/TASK_LIFECYCLE.md` §3.
- `docs/00-overview/MVP_SCOPE.md` §1 (Bosses and Combat/Damage are IN), §2.

---

## Current State

The decision is **recorded but not applied**. Verified in the working tree:

```text
TASK-128 (DONE, immutable)
    The Product Owner decision is recorded verbatim, with all 8 coverage
    items resolved and the coverage mapping filled (8 of 8).
    => Authoritative input for this task. Not modified here.

COMBAT_RULES.md §5.5.1 (L998–L1041)
    States the consumption point (L1008–L1010), the flow (L1012–L1020),
    "EffectiveBossATK = the modified BossState.ATK" / "Step 1 =
    EffectiveBossATK" (L1022–L1027), and the truncation convention
    (L1034–L1038).
    => Authors NO formula and NO direction. Its "Percentage application and
       rounding" bullet says verbatim: "This rule authors no percentage of
       its own; the active instance's `Magnitude` supplies it
       (`BOSS_RULES.md` §6.2)."
    => This is the gap. §5.5.1 is the canonical owner and is silent on
       direction.

COMBAT_RULES.md §5.4.1 item 3 (L858)
    EffectiveATK = truncate( ATK × (100 − |Magnitude|) / 100 )
    Pet-scoped (§5.4.1 L837–L838 "modifies the active Pet's ATK"; §5.4.5
    L965 "Does NOT apply to the Boss's damage") and unidirectional — it takes
    the ABSOLUTE value and only ever reduces.
    => Cannot express the Boss-side Rage increase. NOT the Boss formula.
    => UNCHANGED by this task.

GAME_STATE.md §2.3.1 item 2 (L1179–L1187)
    "Magnitude is typed but not interpreted here. What the number means ...
    is owned by the effect's rule document. ... For a `BuffDebuff` instance
    the meaning is now authored: `COMBAT_RULES.md` §5.4 owns how a
    `Magnitude` reaches the stat its `TargetStat` names, including the "ATK"
    case Root (`BOSS_RULES.md` §6.3.1 item 3) uses."
    => Names §5.4 ONLY. Stated as the general rule, not the Pet case.

GAME_STATE.md §2.3.1 item 12 (L1239–L1250)
    "What a `BuffDebuff` `Magnitude` does to its `TargetStat` is owned by
    `COMBAT_RULES.md` §5.4 and is likewise not an independent source for it."
    => Names §5.4 ONLY — the same incomplete attribution.
    => NOTE: the SAME item 12 already names TWO owners for the duration
       concept ("What a duration means in play is owned by
       `COMBAT_RULES.md` §5.3"), so naming a second owner for the
       Magnitude concept is consistent with this item's own existing shape.

BOSS_RULES.md §6.2.1 (L232–L263)
    Rage: `+20% ATK` / 3 turns; a Turn-based BuffDebuff in
    `BossState.StatusEffects[]` with TargetStat = "ATK", Magnitude = +20%,
    RemainingTurns = 3.
    => UNCHANGED. Referenced, not restated.

BOSS_RULES.md §6.3.1 item 3 (L378–L382)
    Root: "-30% Pet ATK debuff", stored as the POSITIVE Magnitude = 30.
    => UNCHANGED. Root remains Pet-side; its consumption is §5.4.1's.

src/backend/.../StatusEffectLifecycle.cs L742-L792
    EffectiveBossAttack(...) exists and currently branches on
    `Magnitude < 0` — the invented semantic the TASK-127 review rejected.
    => FORBIDDEN to touch. Reconciling it is TASK-127's rework, after this
       task lands.
```

**What this task does NOT change.** §3.4's composition, §3.1's six-step order,
§5.4's Pet-side rule, §5.5.2–§5.5.5, GAME_RULES.md §16's event list and §17's
order, BOSS_RULES.md §6.1/§6.2/§6.3/§6.3.1's balance values, Root's design,
Rage's magnitude/duration, and TASK-118's three Skill secondary effects.

---

## Decision Input

**This task decides nothing.** The decision below is TASK-128's recorded
Product-Owner decision. It is reproduced here **only** so the executing agent
applies exactly it; **TASK-128 remains the authoritative record**, and if this
summary and TASK-128 ever disagree, **TASK-128 governs**.

```text
D-1 — Boss-side `BuffDebuff` ATK modifier direction (TASK-128)

Direction       BOTH increase and decrease. No increase-only and no
                decrease-only semantic.

Representation  The SIGN of the existing `Magnitude` field:
                    Magnitude > 0  ->  increase
                    Magnitude < 0  ->  decrease
                    Magnitude = 0  ->  unchanged
                No new field, member, Type, or TargetStat.

Formula         EffectiveBossATK = truncate( ATK × (100 + Magnitude) / 100 )

                  base      = BossState.ATK
                  Magnitude = the active instance's Magnitude, WITH ITS SIGN
                  direction = sign(Magnitude)
                  rounding  = truncate toward zero, integer domain

Required outcomes (TASK-128 §6.8):
                  ATK 100, +20 -> 120
                  ATK 100, -30 ->  70
                  ATK 100,   0 -> 100
                  no instance  -> 100
                  ATK  51, +20 ->  61        (truncated toward zero)
                  ATK 200, +20 -> 240

Preserved       Rage:       100 + (+20%) -> 120; 120 + 150 = 270
                Root:       Magnitude = 30, Pet-side, 100 -> 70
                Pet side:   §5.4.1 unchanged; uses |Magnitude|
                Stacking:   refresh-not-stack (§5.5.5 / §2.3.1 item 6)
                State:      no schema change; sign of an existing field

Rationale       NOT SUPPLIED by the Product Owner. Do not invent one.
```

**Explicitly not to be treated as evidence.** TASK-127's current
`Magnitude < 0` implementation is an implementation assumption the review
rejected. It is **not** the source of this decision (the two disagree: §5.5.1
under this decision reads `+30` as `+30%`, whereas the review rejected the
sign branch — the decision's `Magnitude > 0 → increase` is the Product Owner's
stated rule, not a derivation from that code). Do not cite the implementation
as authority, and do not modify it.

---

## Documentation Changes Required

### §1 — Boss-side formula (canonical owner)

Author at `COMBAT_RULES.md` **§5.5.1**, as authored contract text:

```text
EffectiveBossATK = truncate( BossState.ATK × (100 + Magnitude) / 100 )
```

with **truncation toward zero**. It must be stated as this rule's own formula.
The existing bullet's claim that this rule "authors **no percentage** of its
own" remains true about the *percentage value* (which `Magnitude` supplies) and
must be reconciled so the wording does not also deny the rule its own *formula*
— the formula is this rule's, the percentage value is the instance's.

### §2 — Direction semantics

Author explicitly, at §5.5.1:

```text
Magnitude > 0  ->  increase
Magnitude < 0  ->  decrease
Magnitude = 0  ->  unchanged
```

The statement must say that direction is carried by `Magnitude`'s **sign**, and
that `Magnitude` is used **with its own sign** (not its absolute value) on the
Boss side.

### §3 — Scope

State that this interpretation belongs to **Boss-side** `BuffDebuff` ATK
modifiers, i.e. a Turn-based `BuffDebuff` instance with `TargetStat = "ATK"`
held in the **Boss's** `StatusEffects[]`. State that it does **not** redefine
Pet-side semantics.

```text
The Pet/Boss separation must be made explicit within §5.5.1 only.

Do NOT modify §5.5.3.

§5.5.3 MUST remain byte-identical.

If §5.5.1 cannot express the required separation without changing §5.5.3,
STOP and report the conflict instead of modifying §5.5.3
(see Stop Conditions 13).
```

`§5.5.1` is the **only** `COMBAT_RULES.md` subsection this task may modify.
The separation statement belongs in §5.5.1 and is written there; §5.5.3 is
read for context only and is not edited. Do not restate §5.4's formula
(`documentation-change.md` §2).

### §4 — Pet-side preservation

Preserve `§5.4.1`'s formula **byte-identically**:

```text
EffectiveATK = truncate( ATK × (100 − |Magnitude|) / 100 )
```

Do **not** change `COMBAT_RULES.md` §5.4.1. Add only a **reference** from
§5.5.1 to §5.4.1 recording that the two rules are counterparts with
**different** direction conventions — Boss-side uses the signed `Magnitude`,
Pet-side uses `|Magnitude|` — so a future reader does not "normalize" them into
one formula. Cross-reference only; never duplicate §5.4.1's formula text.

### §5 — Hỏa Long Rage

Preserve, explicitly:

```text
BossState.ATK = 100, Rage Magnitude = +20  ->  EffectiveBossATK = 120
```

and its existing Boss Skill Step-1 interaction (`EffectiveBossATK + authored
Skill Base Damage`). **Do not modify §3.4. Do not modify TASK-125/TASK-126.**
`BOSS_RULES.md` §6.2.1's `+20%` and `3 turns` are unchanged and are
**referenced, not restated**.

### §6 — Root

Preserve Root's authored `Magnitude = 30` and its status as a **Pet-side**
debuff consumed by the existing Pet-side formula (Pet `ATK 100` → `70`). **Do
not reinterpret Root using Boss-side signed semantics.** `BOSS_RULES.md`
§6.3.1 item 3 and `BossDefinitions.cs` are unchanged.

### §7 — Stacking

Introduce **no** new stacking semantics. Preserve `refresh-not-stack` as
already documented at `COMBAT_RULES.md` §5.5.5 and `GAME_STATE.md` §2.3.1
item 6. §5.5.1 should **reference** §5.5.5 for multiplicity rather than author
a stacking rule (TASK-128 §6.3 introduced none). Where more than one distinct
active instance is present, each applies the one decided formula in turn; do
not invent an ordering or a stacking model beyond what §5.1.1 / §2.3.1 already
fix.

### §8 — State representation

Confirm explicitly, in the applied text, that this introduces:

```text
No new state field.        No new StatusEffect member.
No new StatusEffect Type.  No new TargetStat value.
No new wire field.         No new Redis field/key.
No database change.
```

The existing `Magnitude` field carries the Boss-side sign.

### §9 — GAME_STATE owner reference (determined: REQUIRED, minimal)

**Determination — the clarification IS required.** `GAME_STATE.md` §2.3.1
item 2 (L1183–L1187) and item 12 (L1242–L1243) both attribute the
`Magnitude` → `TargetStat` concept to `COMBAT_RULES.md` **§5.4 alone**, and
both state it as the **general** rule rather than the Pet case. After §5.5.1
becomes the Boss-side owner of that same concept, the attribution is
incomplete: it would tell a reader that §5.4 governs Boss-side `Magnitude`
interpretation, which is false (§5.4.5: "Does NOT apply to the Boss's
damage"), and which is precisely the misreading TASK-127's review identified.

This is a **stale/incomplete reference** finding, which
`.ai/skills/quality/documentation-consistency.md` Mode A item 4 ("Check
references resolve ... Unresolvable ones are *stale reference* findings")
covers, and which `documentation/documentation-change.md` §1 authorizes as
"Update dependent references if required".

**Required change — owner-reference clarification ONLY:**

- In §2.3.1 item 2, name **both** owners: the Pet-side interpretation
  (§5.4) **and** the Boss-side interpretation (§5.5.1).
- In §2.3.1 item 12, likewise name both, so the item no longer says the
  concept is owned by §5.4 alone.

**Constraints on that edit:**

- Do **not** restate either formula in `GAME_STATE.md`
  (`documentation-change.md` §2). Point at the owners only.
- Do **not** add, remove, rename, retype, or re-flag any member. §2.3.1's
  member tree and items 1, 3–11 are **unchanged**.
- Do **not** broaden this into a GAME_STATE redesign.
- If the executing agent concludes the clarification is **not** required, it
  must record that finding with evidence and leave `GAME_STATE.md`
  **byte-identical** — the task still completes. The determination above is
  this task's analysis, not a license to edit beyond it.

### §5.5.1 — required shape of the applied text

The applied §5.5.1 must remain consistent with the document's existing shape
(numbered items + bullets, as §5.4.1 already uses). It must:

```text
[ ] keep the existing consumption point statement
[ ] keep the existing flow diagram (its "+20% Rage modifier" line is correct)
[ ] replace the direction-neutral item 3 with the decided formula
[ ] add the direction semantics (> 0 / < 0 / = 0)
[ ] keep the truncation-toward-zero convention statement
[ ] keep referencing BOSS_RULES.md §6.2 for the percentage VALUE
[ ] cross-reference §5.4.1 as the Pet-side counterpart (§6.6 separation)
[ ] reference §5.5.5 for duration/multiplicity (no new stacking rule)
[ ] make the Boss/Pet semantic separation explicit in §5.5.1
[ ] add no rule, value, event, member, or representation
[ ] do NOT modify §5.5.3
```

---

## Scope

### In Scope

1. Author the decided Boss-side direction semantics and formula at
   `COMBAT_RULES.md` §5.5.1 (§1–§2 above).
2. Make the Boss/Pet scope separation explicit **within §5.5.1** without
   restating §5.4.1 and without modifying §5.5.3 (§3–§4).
3. Cross-reference the preserved Rage outcome and Root's Pet-side status
   (§5–§6), without changing either.
4. Carry the state-representation confirmations into the applied text (§8).
5. **Only if** §9's determination holds on execution: minimally correct the
   owner reference in `GAME_STATE.md` §2.3.1 item 2 and item 12.

### Out of Scope

- **Deciding anything.** The decision is TASK-128's. Nothing is chosen,
  reinterpreted, extended, or "improved" while applying it.
- **Any source change.** Zero files under `src/`. In particular: no
  `EffectiveBossAttack` change, no `StatusEffectLifecycle` change, no
  `BattleStateService` change, no `DamagePipeline` change, no
  `BossDefinition`/`BossDefinitions` change.
- **Any test change.** Zero files under `tests/`.
- **Implementing `EffectiveBossATK`**, Boss Skill damage, Rage application,
  or Root. All excluded.
- **Modifying any other `docs/` file** — including `BOSS_RULES.md`,
  `GAME_RULES.md`, `GAME_STATE.md`'s members, `DATABASE.md`,
  `SIGNALR_PROTOCOL.md`, `API_CONTRACTS.md`, `REDIS_STATE.md`, and
  `ARCHITECTURE.md`/`TDD.md`. Only the two sections in §9/In-Scope change.
- **Modifying `TASK-127`, `TASK-128`, `TASK-125`, `TASK-126`, `TASK-119`, or
  `TASK-118`** — all IMMUTABLE. Including their Status, scope, or content.
- **Creating an ADR** (see Metadata), or editing
  `docs/03-decisions/README.md`.
- **Creating any other task** — including the TASK-127 rework task. That is
  created after this one lands.
- **Changing** Rage's `+20%`/`3 turns`, Root's `30`, Flame Burst's `150`,
  `BossState.ATK`'s MVP default, or the Boss Skill Step-1 composition.
- **New** Status Effect `Type`, `TargetStat` value, state member, Battle
  Event, SignalR member, API endpoint, Redis key, or database column.
- Frontend, Phaser, SignalR code, Redis, PostgreSQL, Match-3, Cards, Relics,
  Pet redesign, reward system, progression.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

All criteria are binary and testable.

- [ ] `COMBAT_RULES.md` §5.5.1 explicitly defines Boss-side **signed-Magnitude**
      direction.
- [ ] `COMBAT_RULES.md` §5.5.1 explicitly defines:
      `Magnitude > 0 → increase`; `Magnitude < 0 → decrease`;
      `Magnitude = 0 → unchanged`.
- [ ] `COMBAT_RULES.md` §5.5.1 explicitly defines:
      `EffectiveBossATK = truncate( BossState.ATK × (100 + Magnitude) / 100 )`.
- [ ] Truncation **toward zero** is explicit at §5.5.1.
- [ ] §5.5.1 states the Boss formula uses `Magnitude` **with its sign** (not
      `|Magnitude|`).
- [ ] Boss-side semantics are explicitly separated from the Pet-side §5.4.1
      rule (cross-reference + differing-convention statement).
- [ ] `COMBAT_RULES.md` §5.5.1 contains the complete Boss/Pet semantic
      separation.
- [ ] `COMBAT_RULES.md` §5.5.3 is **byte-identical** to its pre-task state.
- [ ] No section other than §5.5.1 in `COMBAT_RULES.md` is modified.
- [ ] If §5.5.1 cannot express the required separation without changing another
      section, the executing agent **STOPPED and reported the conflict**
      (Stop Condition 13) rather than modifying that section.
- [ ] `COMBAT_RULES.md` §5.4.1's formula is **byte-identical** — unchanged.
- [ ] Hỏa Long Rage `+20%` is unchanged, and §5.5.1/§3.4 still yield
      `100 + (+20%) → 120`.
- [ ] Root `Magnitude = 30` is unchanged and remains **Pet-side**; Root is not
      reinterpreted with Boss-side signed semantics.
- [ ] The Boss Skill Step-1 composition from TASK-125/TASK-126 (`§3.4`) is
      unchanged.
- [ ] No new state field, `StatusEffect` member, `StatusEffect` Type,
      `TargetStat` value, wire field, Redis field/key, or database column is
      introduced by the applied text.
- [ ] No new stacking rule is introduced; `refresh-not-stack`
      (§5.5.5 / §2.3.1 item 6) is referenced, not re-authored.
- [ ] `GAME_STATE.md` §2.3.1 item 2 and item 12 name **both** owners
      (Pet-side §5.4 and Boss-side §5.5.1) **or** the executing agent records
      an evidence-backed finding that the clarification is not required and
      leaves `GAME_STATE.md` byte-identical.
- [ ] Any `GAME_STATE.md` edit is **owner-reference clarification only** — its
      member tree and items 1, 3–11 are byte-identical, and §2.3.1 states no
      formula.
- [ ] `GAME_STATE.md` §2.3.1's member tree
      (`Id | Type | Source | Magnitude | TargetStat | RemainingTurns |
      ExpiryCondition`) is byte-identical.
- [ ] No formula from §5.4.1 or §5.5.1 is duplicated into any other document
      (`documentation/documentation-change.md` §2).
- [ ] `COMBAT_RULES.md` states each rule **once**, at its owner.
- [ ] Every value appearing in the applied text traces to an already-authored
      source (`BOSS_RULES.md` §6.2.1/§6.3.1, `COMBAT_RULES.md` §1.1); **no new
      value** is introduced.
- [ ] No source code is modified; zero files under `src/` change.
- [ ] No tests are modified; zero files under `tests/` change.
- [ ] `TASK-128` is **byte-identical** (unmodified, unmoved, not re-statused).
- [ ] `TASK-127` is **byte-identical**; `TASK-119`, `TASK-118`, `TASK-125`,
      `TASK-126`, and every other existing task file are byte-identical.
- [ ] No ADR is created or modified; `docs/03-decisions/README.md` is
      byte-identical.
- [ ] Documentation validation confirms **no contradiction** with TASK-119,
      TASK-125, TASK-126, TASK-118, or ADR-017
      (`quality/documentation-consistency.md` Mode C).
- [ ] Only `COMBAT_RULES.md` (and, if §9 requires it, `GAME_STATE.md`) differ
      from their pre-task SHA256 values; every other `docs/` file is
      byte-identical.
- [ ] **Within `COMBAT_RULES.md`, only §5.5.1 differs.** §3.4, §5.4.1,
      §5.5.2, §5.5.3, §5.5.4, and §5.5.5 are byte-identical to their
      pre-task state.
- [ ] Quality review checklist passes (`quality/review.md` §1, minus
      code-only items per `documentation-change.md` §4).

---

## Affected Files & Areas

```text
[ ] src/backend/            — FORBIDDEN. No Domain / Application /
                              Infrastructure / Api change. Explicitly: no
                              StatusEffectLifecycle, no BattleStateService,
                              no DamagePipeline, no BossDefinition,
                              no BossDefinitions change. The current
                              `Magnitude < 0` branch at
                              StatusEffectLifecycle.cs L780-L788 is TASK-127's
                              to reconcile AFTER this task — not here.
[ ] src/frontend/client/    — FORBIDDEN.
[ ] tests/                  — FORBIDDEN. No unit / integration / gameplay
                              scenario change.
[x] docs/01-game-design/COMBAT_RULES.md
                            — §5.5.1 ONLY: the decided Boss-side formula, the
                              direction semantics, and the explicit Pet/Boss
                              separation, all authored within §5.5.1.
                              §5.5.3 MUST remain byte-identical.
                              §3.4, §5.4.1, §5.5.2, §5.5.4, and §5.5.5 MUST
                              remain byte-identical. No other section of this
                              file may change.
                              If §5.5.1 cannot express the required
                              separation without changing another section,
                              STOP and report (Stop Condition 13).
[?] docs/02-technical/GAME_STATE.md
                            — §2.3.1 item 2 and item 12, ONLY if §9's
                              determination holds on execution: name both
                              owners. Owner-reference clarification only.
                              Member tree and items 1, 3–11 BYTE-IDENTICAL.
                              Otherwise this file is byte-identical.
[ ] docs/ (every other file) — FORBIDDEN. BOSS_RULES.md, GAME_RULES.md,
                              DATABASE.md, SIGNALR_PROTOCOL.md,
                              API_CONTRACTS.md, REDIS_STATE.md,
                              ARCHITECTURE.md, TDD.md all byte-identical.
[ ] docs/03-decisions/ADR/  — FORBIDDEN. No ADR.
[x] tasks/                  — THIS NEW TASK FILE ONLY
                              (tasks/backlog/TASK-129-apply-boss-side-atk-
                              modifier-direction-contract.md). No existing
                              task file is modified.
```

---

## Implementation Notes

None — this task implements nothing. Task-specific pointers only:

- **Read TASK-128 first.** Its §"Decision — PRODUCT OWNER DECISION (RECORDED)"
  is the specification; this task's `Decision Input` section is a pointer to
  it, not a replacement. Where they differ, **TASK-128 governs**.
- **§5.5.1 is the only place the rule is written.** `documentation-change.md`
  §2 is the single most important constraint: other documents **point at**
  §5.5.1 and never restate its formula. In particular, do not copy the formula
  into `BOSS_RULES.md`, §3.4, or `GAME_STATE.md`.
- **Do not "fix" §5.4.1.** The Pet-side rule's `|Magnitude|` reading is
  correct for the Pet and is deliberately different from the Boss side.
  TASK-128 §6.6 requires the separation to be **stated**, not resolved.
- **Do not "fix" Root.** Root's positive `30` is correct: Root is a Pet-side
  debuff consumed by §5.4.1's absolute-value formula. Applying the Boss-side
  signed reading to Root would break it. TASK-128 §6.5.
- **The existing bullet that says this rule "authors no percentage of its
  own"** is still true about the percentage *value*. Reconcile that wording so
  it does not also read as "authors no formula" — the formula is now this
  rule's own. Keep the reference to `BOSS_RULES.md` §6.2 for the value.
- **Multiplicity.** TASK-128 §6.3 defined the arithmetic for one instance and
  introduced no stacking rule. §5.5.1 should point at §5.5.5 for duration,
  reapplication, and the refresh-not-stack default rather than authoring any
  new multiplicity statement.
- **Record the §9 determination either way.** Whichever way it resolves, state
  the finding and its evidence in `Completion Evidence` so the reviewer can
  audit it. A "not required" finding that leaves `GAME_STATE.md` untouched is
  an acceptable outcome; an unrecorded silent choice is not.
- **Report-only items** (`AGENTS.md` §16): Hỏa Long's Rage *application*
  (step 18a) remains unimplemented — that is the separate Boss Passive half of
  ROADMAP.md Phase 1, not this task's concern.

---

## Testing / Validation Requirements

### Required Verification

```text
[x] N/A — documentation-only task. No code, no tests authored, and no test
        runner is evidence of correctness here
        (documentation/documentation-change.md §4).
```

Verification is documentation validation plus explicit byte-identity checks:

```text
[ ] Documentation consistency (`quality/documentation-consistency.md` Mode C):
    re-read COMBAT_RULES.md §5.5.1 together with §5.4.1, §3.4, §5.5.3,
    §5.5.5, BOSS_RULES.md §6.2.1/§6.3.1, GAME_STATE.md §2.3.1, and TASK-128.
    Confirm: no duplicate definition introduced, no stale reference remains,
    no contradiction with TASK-119 / TASK-125 / TASK-126 / TASK-118 / ADR-017.
[ ] No-duplication check: the formula text appears at §5.5.1 once; grep
    docs/ for a second authored copy (expect none outside §5.5.1).
[ ] Owner check: every applied statement names an already-authored value's
    owner rather than restating it.
[ ] SHA256 before/after for: COMBAT_RULES.md, BOSS_RULES.md, GAME_RULES.md,
    GAME_STATE.md, DATABASE.md, SIGNALR_PROTOCOL.md, API_CONTRACTS.md,
    REDIS_STATE.md, ARCHITECTURE.md, TDD.md, docs/03-decisions/README.md.

    Only:
        COMBAT_RULES.md
        and, if §9 requires it, GAME_STATE.md
    may differ. Every other file above must be byte-identical to its
    pre-task state.

    Within COMBAT_RULES.md:
        only §5.5.1 may differ.

        §3.4, §5.4.1, §5.5.2, §5.5.3, §5.5.4, and §5.5.5 must remain
        byte-identical.

    The whole-file COMBAT_RULES.md hash WILL change because §5.5.1 is
    intentionally modified — that is expected. This check must therefore be
    performed at SECTION granularity for COMBAT_RULES.md (§5.5.1 vs every
    other section), not by requiring the file to be unchanged. Verify the
    untouched sections by their own before/after text, e.g. by extracting
    each section's byte range and hashing it.
[ ] SHA256 before/after for TASK-127, TASK-128, TASK-119, TASK-118, TASK-125,
    TASK-126 — all byte-identical.
[ ] src/ and tests/ trees byte-identical (aggregate hash).
[ ] Exactly one file added: this task file.
```

### Required Scenario Traces (derived from the applied rule, not the code)

The applied §5.5.1 must be precise enough that these are unambiguous. **Do
not** author executable tests here — TASK-127's rework derives them
(`AGENTS.md` §15):

```text
ATK 100, Rage Magnitude +20   ->  EffectiveBossATK = 120
ATK 100, a Magnitude -30      ->  EffectiveBossATK =  70
ATK 100, a Magnitude  +0      ->  EffectiveBossATK = 100
ATK 100, no active instance   ->  EffectiveBossATK = 100
ATK  51, Magnitude +20        ->  truncate(61.2) = 61
ATK 200, Magnitude +20        ->  EffectiveBossATK = 240
```

### Key Edge Cases the applied text must not contradict

- **Truncation vs rounding** — `ATK 51, +20 → 61` (a rounding implementation
  would give `61` only by coincidence; `ATK 3, +20 → 3` distinguishes them).
  The applied text must say *truncate toward zero*.
- **Sign is not `Math.Abs`** — the Boss side must not silently reuse §5.4.1's
  absolute-value reading.
- **`Magnitude = 0`** — explicitly `unchanged`, not an error and not a 100%
  reduction/increase.
- **Same-Turn non-retroactivity** — §5.5.5 / §5.4.3: an instance applied later
  in the same Turn cannot affect that Turn's already-resolved attack.
- **Expiry at step 19a** — at `RemainingTurns = 0` the instance is removed
  (§5.3 DR5, §2.3.1 item 8), so the next Turn reads an absent instance.
- **Root must stay Pet-side** — the applied text must not make Root's `+30`
  read as a Boss-side increase.

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific stops — **STOP and report instead of editing** if:

1. **TASK-128 does not contain the complete decision above** (any of the eight
   §6.x coverage items unresolved, or the coverage mapping not showing 8 of
   8). **STOP** and report the gap. Do not complete it.
2. **The canonical owner for Boss-side ATK direction differs from
   `COMBAT_RULES.md` §5.5.1.** If §5.5.1's stated purpose or §5.5's scope does
   not actually own how a Boss-side `Magnitude` reaches `BossState.ATK`,
   **STOP** and report both candidate owners (`documentation-change.md` §3).
3. **The decision conflicts with existing authoritative documentation in a way
   TASK-128 does not cover.** **STOP** per `AGENTS.md` §4 and report both
   sides (file + section).
4. **Applying the decision requires a new gameplay decision** — e.g. an
   ordering, multiplicity, or clamping rule §5.5.5 / §2.3.1 item 6 does not
   already fix. **STOP** and report it (`AGENTS.md` §7).
5. **Applying it requires a new state representation** — any new member, Type,
   `TargetStat`, wire field, Redis field/key, or DB column. **STOP** and
   report (`AGENTS.md` §17/§18).
6. **Applying it requires an ADR this task is not authorized to create.**
   **Report** it; do not author it (`AGENTS.md` §18; check
   `docs/03-decisions/README.md` §8 first — the TASK-119 precedent says none is
   required here).
7. **Pet-side §5.4.1 would need to change.** **STOP** and report. TASK-128
   §6.6 forbids it.
8. **Root semantics would need to change.** **STOP** and report. TASK-128
   §6.5 forbids it.
9. **Hỏa Long Rage semantics would need to change** (its `+20%`, its
   `3 turns`, or the `120` outcome). **STOP** and report. TASK-128 §6.4
   forbids it, and TASK-125/TASK-126 / §3.4 must not be reopened.
10. **Boss Skill Step-1 composition would need to change.** **STOP** and
    report. TASK-125/TASK-126 are IMMUTABLE.
11. **Any work would touch `src/`, `tests/`, an ADR, `TASK-127`, `TASK-128`,
    `TASK-125`, `TASK-126`, `TASK-119`, `TASK-118`, or any existing task
    file.** **STOP.**
12. **The work would exceed the 3 listed skills or drift into creating another
    task** (including the TASK-127 rework task). **STOP.**
13. **The required Pet/Boss separation cannot be expressed within §5.5.1
    without changing another `COMBAT_RULES.md` section** — in particular
    §5.5.3, §5.5.2, §5.4.1, or §3.4. **STOP** and report the conflict; do
    **not** modify §5.5.3 or any other section. §5.5.1 is the only
    `COMBAT_RULES.md` subsection this task is authorized to change. Report
    which section would need to change, why §5.5.1 alone cannot carry the
    statement, and the smallest correction that would resolve it — then wait
    for approval (`AGENTS.md` §4, `.ai/README.md` §13).
14. **Any work would edit a `COMBAT_RULES.md` section other than §5.5.1.**
    **STOP** and report. §5.5.3 in particular MUST remain byte-identical.

Use `.ai/README.md` §13's exact report format. Unrelated issues discovered
while working are **report-only** (`AGENTS.md` §16) — do not fix them inline.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files

- `docs/01-game-design/COMBAT_RULES.md` — **§5.5.1 only.** Item 3's
  direction-neutral `EffectiveBossATK = the modified BossState.ATK` was replaced
  with the decided formula, and a new item 4 authors the signed-`Magnitude`
  direction semantics. The "Percentage application and rounding" bullet was
  split into a **Rounding** bullet (truncation toward zero retained, formula
  ownership reconciled) and new bullets for the worked example, the **Pet/Boss
  separation** (`§5.4.1` cross-referenced, its absolute-value convention named
  as deliberately different, never restated), the no-new-state confirmation, and
  an **Activity** bullet that now also points at §5.5.5 for duration and
  refresh-not-stack. The consumption point, the flow diagram, and the single
  sentence introducing the rule are unchanged.
- `docs/02-technical/GAME_STATE.md` — **§2.3.1 item 2 and item 12 only.**
  Owner-reference clarification: both items previously attributed the
  `Magnitude` → `TargetStat` concept to `COMBAT_RULES.md` §5.4 alone as the
  general rule; each now names the Pet-side owner (§5.4) and the Boss-side owner
  (§5.5.1) by entity. No formula was added; no member changed.

**No other file changed.** `src/`, `tests/`, `docs/03-decisions/`, and every
other `docs/` file are byte-identical.

### §9 Determination (GAME_STATE owner reference)

```text
Determination:      REQUIRED

Evidence:           GAME_STATE.md §2.3.1 item 2 (pre-edit L1183-L1187) named
                    `COMBAT_RULES.md` §5.4 as owning "how a `Magnitude` reaches
                    the stat its `TargetStat` names, including the "ATK" case
                    Root ... uses" — stated as the GENERAL rule, not the Pet
                    case.
                    GAME_STATE.md §2.3.1 item 12 (pre-edit L1242-L1243) said
                    "What a `BuffDebuff` `Magnitude` does to its `TargetStat`
                    is owned by `COMBAT_RULES.md` §5.4" — same incomplete
                    attribution.
                    COMBAT_RULES.md §5.4.5 states the Pet-side rule "Does NOT
                    apply to the Boss's damage", so §5.4 cannot govern
                    Boss-side `Magnitude` interpretation. After §5.5.1 became
                    the Boss-side owner of that same concept, the §5.4-only
                    attribution was stale/incomplete — the precise misreading
                    the TASK-127 review identified.
                    Classification: stale/incomplete reference
                    (.ai/skills/quality/documentation-consistency.md Mode A
                    item 4, "Check references resolve ... stale reference
                    findings"), which documentation/documentation-change.md §1
                    authorizes as "Update dependent references if required".
                    Corroboration: the SAME item 12 already names TWO owners
                    for the duration concept ("What a duration means in play
                    is owned by `COMBAT_RULES.md` §5.3"), so naming a second
                    owner for the `Magnitude` concept matches the item's own
                    existing shape.

Change applied:     §2.3.1 item 2 — "...the meaning is now authored, by entity:
                    `COMBAT_RULES.md` §5.4 owns how a `Magnitude` reaches the
                    stat its `TargetStat` names on the **Pet**
                    (`PetState.StatusEffects[]`), including the "ATK" case Root
                    (`BOSS_RULES.md` §6.3.1 item 3) uses; and `COMBAT_RULES.md`
                    §5.5.1 owns that concept on the **Boss**
                    (`BossState.StatusEffects[]`). The two are separate rules
                    with separate conventions and neither is restated here."
                    §2.3.1 item 12 — "...is owned by `COMBAT_RULES.md` §5.4 on
                    the **Pet** and by `COMBAT_RULES.md` §5.5.1 on the
                    **Boss**, and this section is likewise not an independent
                    source for it. In particular, each of those rules composes
                    an **effective** value ... neither stores the result, and
                    neither overwrites the stat ..."
                    Owner-reference clarification ONLY. No formula added —
                    verified: 0 occurrences of either formula in GAME_STATE.md.
                    Member tree and items 1, 3-11 byte-identical.
```

### Applied §5.5.1 (verbatim excerpt)

```text
3. What the percentage applies to — BossState.ATK ALONE

   EffectiveBossATK = truncate( BossState.ATK × (100 + Magnitude) / 100 )

   Step 1 = EffectiveBossATK

4. Percentage application and direction — the instance's Magnitude, signed

   The percentage value is supplied by the active instance's `Magnitude`
   (`BOSS_RULES.md` §6.2); this rule owns how that value is applied.

   The `Magnitude` is used WITH ITS OWN SIGN — it is the direction signal:

   Magnitude > 0  →  increase   EffectiveBossATK > BossState.ATK
   Magnitude < 0  →  decrease   EffectiveBossATK < BossState.ATK
   Magnitude = 0  →  unchanged  EffectiveBossATK = BossState.ATK

   A Boss-side ATK modifier therefore supports BOTH an increase and a
   decrease; there is no increase-only and no decrease-only semantic.
   Direction is carried by the sign of `Magnitude` itself — no separate
   direction field, flag, operation, or member exists or is introduced.
```

Rounding bullet as applied: "The formula's result is an integer, **truncated
toward zero** — the same integer convention §5.4.2 and §3 step 6 already use.
... This rule owns this formula; it authors no percentage of its own — the
active instance's `Magnitude` supplies that value (`BOSS_RULES.md` §6.2)."

### Section Boundary Verification

```text
COMBAT_RULES.md §5.5.1       — modified
COMBAT_RULES.md §3.4         — byte-identical
COMBAT_RULES.md §5.4.1       — byte-identical
COMBAT_RULES.md §5.5.2       — byte-identical
COMBAT_RULES.md §5.5.3       — byte-identical
COMBAT_RULES.md §5.5.4       — byte-identical
COMBAT_RULES.md §5.5.5       — byte-identical
GAME_STATE.md §2.3.1         — modified only if §9 requires it
```

Section-hash evidence (SHA256 of each section's exact text, before → after):

```text
§3.4    470550706a784875 -> 470550706a784875   identical
§5.4.1  50f3f4b566e37d85 -> 50f3f4b566e37d85   identical
§5.5    36b51687c7acb8cc -> 36b51687c7acb8cc   identical
§5.5.1  7cde8f6eba619029 -> 23afa61a92466c8b   MODIFIED (the only change)
§5.5.2  d5da6afec95f89ca -> d5da6afec95f89ca   identical
§5.5.3  3654c64e1582e0bb -> 3654c64e1582e0bb   identical
§5.5.4  f4c8a21a14deb70a -> f4c8a21a14deb70a   identical
§5.5.5  b593feaa3d7a229d -> b593feaa3d7a229d   identical
```

### Validation Results

```text
Documentation consistency (Mode C)  — PASS
  §5.5.1 re-read with §5.4.1, §5.4.5, §3.4, §5.5.2-§5.5.5, BOSS_RULES.md
  §6.2.1/§6.3.1, GAME_STATE.md §2.3.1 item 2/6/12, and TASK-128.
  No duplicate definition introduced; no stale reference remains; no
  contradiction with TASK-118 / TASK-119 / TASK-125 / TASK-126 / ADR-017.

No-duplication grep                  — PASS (1 authored copy)
  "100 + Magnitude" occurs exactly once across all docs/: COMBAT_RULES.md
  L1024 (§5.5.1). Zero occurrences in GAME_STATE.md. The Pet formula
  "100 − |Magnitude|" remains at §5.4.1 L858 only; §5.5.1 names the
  convention in prose (cross-reference), not as a second authored formula
  for the Pet.

Owner check                          — PASS
  Every applied number traces to an already-authored source: Rage's +20%
  and 3 turns (BOSS_RULES.md §6.2.1), Root's -30% (BOSS_RULES.md §6.3.1
  item 3), Flame Burst's 150 (BOSS_RULES.md §6.3.1 item 1), BossState.ATK
  default 100 (COMBAT_RULES.md §1.1). No new value introduced.

SHA256 before/after — docs/          — only COMBAT_RULES.md and
  COMBAT_RULES.md 5972466377602664 -> AB7756662E0E63B6   (intended)
  GAME_STATE.md   C2E129332D38575E -> B0E05D07B9238082   (intended, §9)
  BOSS_RULES.md   C00E554D8F488202 -> C00E554D8F488202   unchanged
  GAME_RULES.md   7514786E52AFE777 -> 7514786E52AFE777   unchanged
  GAME_EVENTS.md  454A6F82611B927F -> 454A6F82611B927F   unchanged
  docs/03-decisions/README.md
                  62377BBA74EAB8E7 -> 62377BBA74EAB8E7   unchanged

SHA256 before/after — COMBAT_RULES.md sections — see Section Boundary
  Verification above. Only §5.5.1 differs; §5.5.3 in particular is
  byte-identical at 3654c64e1582e0bb.

SHA256 before/after — task files      — all byte-identical
  TASK-127 92EED5BF81611B24 | TASK-128 B490F835C6C84BA6
  TASK-119 8C83599DC3C0CBD2 | TASK-118 F975FF0046135428
  TASK-125 E9374FBE033CF4D6 | TASK-126 7A6CC1D30072AE8D

src/ and tests/ trees                — byte-identical (zero files modified)

Exactly one file added: this task file.

Semantic scenario traces (derived from the applied text, not from code;
no executable tests authored — TASK-127's rework derives them):
  ATK 100, Magnitude +20   -> 120     PASS
  ATK 100, Magnitude -30   ->  70     PASS
  ATK 100, Magnitude   0   -> 100     PASS
  ATK 100, no instance     -> 100     PASS
  ATK  51, Magnitude +20   ->  61     PASS  (truncate 61.2)
  ATK 200, Magnitude +20   -> 240     PASS
  ATK   3, Magnitude +20   ->   3     PASS  (truncate 3.6 — distinguishes
                                             truncation from rounding)
  Pet side: ATK 100, |M| 30 -> 70     PASS  (§5.4.1 unchanged; Root intact)
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no code touched)
- [x] Confirmed no new state field, Battle Event, wire member, Redis key, or
      database column
- [x] Confirmed **only §5.5.1** changed within `COMBAT_RULES.md`
- [x] Confirmed `COMBAT_RULES.md` §5.4.1 and §3.4 byte-identical
- [x] Confirmed `COMBAT_RULES.md` §5.5.2, §5.5.3, §5.5.4, §5.5.5 byte-identical
- [x] Confirmed `BOSS_RULES.md` and `GAME_RULES.md` byte-identical
- [x] Confirmed zero files under `src/` or `tests/` modified
- [x] Confirmed no ADR created or modified
- [x] Confirmed TASK-127, TASK-128, TASK-119, TASK-118, TASK-125, TASK-126 and
      all other task files byte-identical
- [x] Confirmed adherence to MVP Scope (`docs/00-overview/MVP_SCOPE.md` §1)

### Reported (not fixed — `AGENTS.md` §16)

Hỏa Long's Rage **application** (step 18a) remains unimplemented, so no
production code path creates the Rage instance the modifier rule consumes.
That is the separate Boss Passive half of `ROADMAP.md` Phase 1, not this
task's concern. TASK-127's current `Magnitude < 0` branch at
`StatusEffectLifecycle.cs` L780–L788 remains as reviewed — reconciling it
against the now-authored §5.5.1 is the TASK-127 rework, not done here.

---

## Downstream Dependency

```text
TASK-128  (DONE — decision recorded)
    ↓
TASK-129  (THIS TASK — applies the decision at its canonical owners:
           COMBAT_RULES.md §5.5.1, and GAME_STATE.md §2.3.1 items 2/12 if §9
           requires the owner-reference clarification)
    ↓
TASK-127 rework  (NOT created here — it drops the rejected `Magnitude < 0`
                  branch and implements the decided formula at
                  StatusEffectLifecycle.EffectiveBossAttack, per
                  COMBAT_RULES.md §5.5.1)
    ↓
Review
```

```text
TASK-127 will later implement:
    StatusEffectLifecycle.EffectiveBossAttack(...)
according to the finalized documentation. It remains the implementation owner
and is NOT modified by this task.

Do NOT implement TASK-127 here.
Do NOT create the TASK-127 rework task here.
```

**Handoff this task must carry forward** (`documentation-change.md` §4):

```text
- The §5.5.1 formula and direction statement as applied.
- The §9 determination and its evidence.
- The Pet-side §5.4.1 separation statement.
- Confirmation that Rage's 120 outcome, Root's Pet-side status, and §3.4's
  composition are preserved.
- The byte-identity evidence for every untouched document.
```

**Must NOT be carried forward:** any rationale, interpretation, or additional
rule the Product Owner did not supply. TASK-128 records that no rationale was
supplied — do not invent one now.
