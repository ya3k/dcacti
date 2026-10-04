# TASK-157 — Apply the TASK-156 Decision to `GAME_STATE.md` §2.3.1 Item 7, Its Schema Line, and §2.3.2 Item 3

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK IS THE DOCUMENTATION-APPLICATION HALF OF THE TASK-156 DECISION.

    TASK-156 decision record (D-1 … D-14 / Option B)   ← DECIDED, sole source
            ↓
    Apply the decision to its canonical owners         ← this task
            ↓
    TASK-153 may be reconciled to READY through the repository lifecycle
    workflow (NOT performed here)
            ↓
    STOP

  It performs the canonical-owner documentation write that
  documentation/documentation-change.md §1 describes. It consumes TASK-156's
  record; it does NOT re-open it, re-interpret it, re-decide it, or add a
  decision to it.

  THE DECISION IS ALREADY MADE. TASK-156 is DECIDED and is the SOLE decision
  source. Nothing in this task is a Product Owner question. If applying the
  decision surfaces something the decision did not cover, that is a STOP (this
  file's Stop Conditions), not a new decision to be authored here.

  EXACTLY ONE INVARIANT CHANGES. TASK-156 D-11 selected "Only item 7 changes —
  I will supply the new wording." The Product Owner's supplied rule is recorded
  verbatim in TASK-156 and is transcribed here at Required Change 1. Every other
  §2.3.1 invariant, §2.3.3, §2.4, §2.4.1, §5.1.1, and §0 item 5 remain
  UNCHANGED.

  BOUNDARY: documentation only, and within that, ONE document only —
  docs/02-technical/GAME_STATE.md. Zero files under src/ or tests/. Zero changes
  to TASK-153, TASK-154, TASK-155, or TASK-156. No ADR. No implementation task.

  THE EXCEPTION IS THỦY-MA-SPECIFIC. TASK-156 D-13 records "Thủy-Ma-specific —
  no other BuffDebuff semantics affected". The relaxed item 7 is expressed as a
  property of the instance (whether its Magnitude is consumed as a stat
  modifier), but it changes NO other existing BuffDebuff behavior: Root's and
  Hỏa Long's Rage's TargetStat = "ATK" instances keep the pairing, and Burn,
  Shield, and Stun are untouched. This task must NOT broaden the decision into a
  general new BuffDebuff mechanic.
-->

---

## Metadata

```text
Task ID:           TASK-157
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change `docs/` content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md. Not GAMEPLAY-CHANGE:
                   the representation rule was decided by the Product Owner in
                   TASK-156 and recorded verbatim there; this task authors no
                   rule of its own. It transcribes a decided contract at its
                   canonical owner, exactly as TASK-124, TASK-151, and TASK-155
                   did for TASK-123, TASK-150, and TASK-154.)
Status:            DONE — documentation applied to GAME_STATE.md per TASK-156
                   Option B; TASK-153 remains BLOCKED.
Risk:              HIGH (TASK_TYPES.md §4 — baseline is LOW–MEDIUM for
                   DOCUMENTATION, classified HIGH here because the edit RELAXES
                   an existing authoritative state-contract invariant —
                   `GAME_STATE.md` §2.3.1 item 7's TargetStat-iff-BuffDebuff
                   pairing — which is the invariant the domain model enforces
                   at `StatusEffect.TurnBased`. It is not MEDIUM: a state-model
                   invariant changes, and a mis-widened wording would alter
                   BuffDebuff behavior beyond the TASK-156 decision. It is not
                   merely a wording refresh of a correct statement; it is the
                   one authoritative invariant TASK-156 D-14 records as
                   ceasing to hold as currently written.)
Priority:          HIGH (TASK-156 "Blocks" names this task first, and
                   AGENTS.md §17 places the authoritative-documentation update
                   before implementation. TASK-153 cannot leave BLOCKED and
                   ROADMAP.md §1 Phase 1's "Boss Response (Passive → Skill →
                   Attack → Victory/Defeat)" for the 3 MVP Bosses cannot
                   complete until this lands.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review")
Supporting Agents: backend (GAME_STATE.md §2.3.1 owns the StatusEffect instance
                   schema, the TargetStat absence convention, and the
                   duration-model assignment; §2.3.2 owns the serialized shape;
                   §2.4.1 owns the Boss carrier the relaxed rule must remain
                   consistent with),
                   gameplay (BOSS_RULES.md §6.2.2 and COMBAT_RULES.md §4 item 7
                   own the Thủy Ma effect this relaxation exists to admit —
                   consulted to confirm the unchanged documents stay
                   consistent under the relaxed item 7, NOT to edit them)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-156 (DECIDED — the SOLE decision source this task
                     applies; D-11 supplies the replacement item 7 wording and
                     D-12 the superseded-in-part TASK-154 D-10 wording.
                     IMMUTABLE; read-only; must NOT be modified, reopened, or
                     re-statused),
                   TASK-154 (DECIDED — its D-10 is superseded only in its
                     invariant-confirmation portion by TASK-156 D-12.
                     IMMUTABLE; read-only; must NOT be modified),
                   TASK-155 (DONE — applied the TASK-154 GAP-5 decision and
                     authored `Id = "boss-thuy-ma-heal"`. IMMUTABLE; read-only;
                     must NOT be modified),
                   TASK-153 (BLOCKED — the downstream implementation this task
                     makes applicably determinate. IMMUTABLE; read-only; must
                     NOT be modified, re-statused, or moved by this task),
                   TASK-124 (DONE — documentation-apply precedent. IMMUTABLE;
                     read-only)
Blocks:            (1) TASK-153 leaving BLOCKED and being implemented;
                   (2) the Thủy Ma half of the MVP Boss Passive set;
                   (3) ROADMAP.md §1 Phase 1 "Boss Response" completeness for
                     the 3 MVP Bosses.
Estimate:          Simple–Normal (ONE authoritative document, THREE named edit
                   sites, no code and no tests; the decision is already made,
                   its replacement wording is already supplied verbatim, and
                   the edit sites are enumerated below)
```

---

## Objective

Apply the TASK-156 decision record (Option B; D-1 … D-14) to the canonical owner of the `StatusEffect` instance schema — `docs/02-technical/GAME_STATE.md` — by replacing §2.3.1 item 7's unconditional `TargetStat`-iff-`BuffDebuff` pairing statement and its schema line with the Product Owner's supplied rule, and by reconciling §2.3.2 item 3's restatement of that pairing with the same rule, so that the Thủy Ma instance documented as `Type = BuffDebuff` / `TargetStat` absent / `Id = "boss-thuy-ma-heal"` is representable in the documented state model while every other `GAME_STATE.md` invariant, and every other BuffDebuff behavior, remains unchanged.

This task changes no source code, authors no gameplay rule, and re-decides nothing.

---

## Authoritative References

- `AGENTS.md` **§4** — never silently resolve a conflict; **§7** — invent no rule; **§9** — anti-overengineering; **§12** — domain boundaries; **§17** — documentation change precedes code; **§18** — architecture change rule (an ADR is required only if architecture changes); **§20** — stop conditions; **§23** — implement documented intent, do not design on the project's behalf
- `.ai/README.md` **§6** — source-of-truth rule; **§13** — stop conditions; **§18** — documentation-update policy (classify the situation before editing)
- `.ai/workflow/documentation/documentation-change.md` **§1** — the flow this task executes (identify the canonical owner → read related docs → check conflicts → update the smallest authoritative source → update dependent references only if stale → validate consistency); **§2** — no duplication, ever; **§3** — determining the canonical owner; **§4** — this workflow composes with `quality/review.md` and `core/completion.md`
- `tasks/README.md` **§9** — no business-rule duplication in task files; **§10** — stop conditions; **§12** — skill budget
- `docs/00-overview/MVP_SCOPE.md` §1 (Thủy Ma, the Boss Passive system, and healing are IN), §2/§4 (no OUT-of-scope system may be introduced)
- **`tasks/backlog/TASK-156-resolve-thuy-ma-healing-reduction-statuseffect-representation-vs-game-state-buffdebuff-targetstat-invariant.md`** — **THE SOLE DECISION SOURCE.** Its "Decision Record" section (D-1 … D-14) is the complete, Product-Owner-supplied contract this task applies. **D-11 supplies the exact replacement item 7 wording; D-12 supplies the exact replacement TASK-154 D-10 wording; D-14 names the required and non-required documents.** Read it; do not modify it.
- `docs/02-technical/GAME_STATE.md` **§2.3.1** — **the canonical owner this task edits.** Its schema block's `TargetStat` line ("the modified stat for Type = `BuffDebuff`, e.g. `"ATK"`; absent otherwise") and its **item 7** ("Absence conventions." — the `TargetStat`/`ExpiryCondition` absence rule and the pairing it encodes) are the two sites TASK-156 D-14 requires changing; **item 1** (`Id` is an identity, not a definition), **item 2** (`Magnitude` typed but not interpreted; the `BuffDebuff` interpretation is owned by entity via `COMBAT_RULES.md` §5.4/§5.5.1), **item 3** (the exclusive duration models; the `DoT | BuffDebuff | Shield | State` vocabulary; `RemainingTurns` assigned to `DoT` and `BuffDebuff`), **item 6** (at most one instance per identity; `Id` is the uniqueness key), **item 8** (a stored zero is never active), **item 12** (the section adds no gameplay rule) — each explicitly recorded UNCHANGED by TASK-156 D-11
- `docs/02-technical/GAME_STATE.md` **§2.3.2** — the serialized shape; its **item 3** records `targetStat … optional (present iff type = "BuffDebuff")`, which is the third site TASK-156 D-14 requires reconciling; its items 1, 2, 4, 5, 6, and 7 are otherwise unchanged
- `docs/02-technical/GAME_STATE.md` **§2.3.3** (the `PendingStatusEffects[]` / queued-collection prohibition — UNCHANGED), **§2.4 / §2.4.1** (the `BossState` tree and the Boss Status Effect carrier contract, including the authorized cross-entity read and the `Id` selector — UNCHANGED), **§5.1 / §5.1.1** (the single write-back and the Status Effect mutation lifecycle — UNCHANGED), **§0 item 5** (one representation per fact — UNCHANGED), **§2.1.7 item 3** (the absent-member convention item 7 follows)
- `docs/01-game-design/BOSS_RULES.md` **§6.2.2** — the Thủy Ma effect the relaxed rule must admit unchanged: the "Representation" bullet (the existing Turn-based Buff/Debuff model held in `BossState.StatusEffects[]`; "The instance's `Type` does not select a stat consumer"), the "Applicable-instance selector" bullet (`Id = "boss-thuy-ma-heal"`; "it is **not** a `TargetStat`-consumed `BuffDebuff`"), the authorized cross-entity read, the −50% application point, the duration/expiry rule, and the reapplication rule. **TASK-156 D-14 records NO change required here; this task must not edit it.** Also **§6.2** (trigger table), **§6.2.1** (Hỏa Long Rage — the contrasting `TargetStat = "ATK"` instance that must remain paired), **§6.2.3** (Mộc Yêu regeneration — outside Heal Resolution), **§6.4** (the Identity Contract carrying Thủy Ma's recorded `PassiveId`)
- `docs/01-game-design/COMBAT_RULES.md` **§5.4.5** and **§5.5.3** — the `TargetStat = "ATK"` case only, and the boundary that any other stat "would require its own recorded decision before it could be implemented"; each states Thủy Ma's healing reduction is **not** a `TargetStat`-consumed `BuffDebuff` and opens no new non-`"ATK"` case. **TASK-156 D-13/D-14 record NO change required here; this task must not edit them.** Also **§5.1** (the Buff/Debuff type definition), **§5.4 / §5.4.1** and **§5.5 / §5.5.1** (the `"ATK"` consumption rules), **§4 item 7** (Heal Resolution and its cross-entity read)
- `docs/01-game-design/GAME_RULES.md` **§17** (the fixed resolution order — step 18a, step 19a), **§16** (the canonical event list), **§18** (server authority) — all evaluated NO-change by TASK-156 D-14
- `docs/02-technical/REDIS_STATE.md` **§7 item 9** (the round-trip obligation) — `TargetStat`'s absence is already a representable serialized case per §2.3.1 item 7, so the member set and key set are unchanged
- `src/backend/GameServer.Domain/Battle/StatusEffect.cs` (`TurnBased()`, lines ~251-257 — the enforced pairing) — **read-only reference, NOT an edit site.** It is consulted only to state accurately that the relaxation makes the documented instance representable in the state model while the enforced validator remains a LATER implementation task's concern (`AGENTS.md` §17, TASK-156's "Implementation consequence (recorded, NOT performed)")
- `tasks/backlog/TASK-155-apply-gap-5-thuy-ma-healing-reduction-decisions-to-authoritative-documentation.md` — the documentation-apply precedent (shape, version-block convention, Decision Traceability and Completion Evidence form)
- `tasks/completed/TASK-151-apply-powerchanged-source-decisions-to-authoritative-documentation.md` — the earlier documentation-apply precedent
- `tasks/blocked/TASK-153-implement-mvp-boss-passive-effects-at-step-18a.md` — the blocked implementation this task's application makes determinately representable; its "Second Stop Condition Report" records the contradiction. Read-only; must NOT be modified.

---

## Current State

`GAME_STATE.md` currently records the pairing TASK-156 decided to relax, at three sites, in a document whose version block still asserts the pairing holds.

```text
§2.3.1 schema block (line ~1229)
  "├── TargetStat      (string, optional — the modified stat for Type =
   │                    "BuffDebuff", e.g. "ATK"; absent otherwise)"

§2.3.1 item 7 — "Absence conventions." (lines ~1283-1289)
  "`TargetStat` and `ExpiryCondition` are absent when they do not apply
   (never `null`, never a sentinel string), following the absent-member
   convention of §2.1.7 item 3. ..."

§2.3.2 item 3 (line ~1358)
  "targetStat      string    optional    (present iff type = "BuffDebuff")"
```

The document's version block (line ~13-17) additionally records, from TASK-155's application, that "item 7's `TargetStat`-iff-`BuffDebuff` pairing still holds as written" — a statement the TASK-156 decision makes stale, and which the version-block convention requires this revision to supersede.

TASK-156 is `DECIDED` and is the sole decision source. TASK-153 is `BLOCKED` on exactly the contradiction these three sites encode.

Version state at the time of writing (verify before editing; do not assume):

```text
docs/02-technical/GAME_STATE.md       Version 2.17
```

TASK-156's Completion Evidence records `GAME_STATE.md`'s SHA-256 as
`9ECC6F5E1673C39C00E0887CAF57DFE64098CA34EC856FDF53F86CB1C54A39C0`; a mismatch
means the document has moved and this task's line references must be re-derived
before editing.

---

## Exact Decision Input

The authoritative contract to be applied, transcribed from TASK-156's "Decision Record" (D-1 … D-14). **This section restates the decision's shape only; TASK-156 remains the source of record.**

```text
Representation:     Option B — keep Type = BuffDebuff with TargetStat ABSENT
                    and explicitly relax GAME_STATE.md §2.3.1 item 7   (D-1)
Type:               BuffDebuff — UNCHANGED; item 3's vocabulary and the
                    duration-model assignment are untouched            (D-2)
Target:             Boss-owned, affecting Pet HP healing ONLY         (D-3)
TargetStat:         ABSENT — never `null`, never a sentinel string; NO new
                    TargetStat value is introduced                    (D-4)
Identity:           Id = "boss-thuy-ma-heal" — UNCHANGED, and now the SOLE
                    selector because no TargetStat is carried          (D-5)
Carrier:            BossState.StatusEffects[] — UNCHANGED              (D-6)
Duration:           RemainingTurns = 3; Turns 1–3 — UNCHANGED         (D-7)
Lifecycle:          step-19a decrement; expires before Turn 4 — UNCHANGED (D-8)
Heal Resolution:    the authorized cross-entity read is UNCHANGED and is NOT
                    invalidated by the selected representation         (D-9)
Reapplication:      refresh to full 3 turns; no stack — UNCHANGED     (D-10)

State invariants:   EXACTLY ONE ITEM CHANGES — §2.3.1 item 7 (and its schema
                    line). The Product Owner supplied the exact replacement
                    rule, recorded verbatim:
                      "a BuffDebuff carries TargetStat iff its Magnitude is
                       consumed as a stat modifier; a BuffDebuff consumed by a
                       non-stat rule selects by Id and omits TargetStat"
                    UNCHANGED, each explicitly: §2.3.1 items 1, 2, 3, 6, 8, 12;
                    §2.3.3's PendingStatusEffects[] prohibition; §2.4/§2.4.1's
                    Boss carrier contract; §5.1.1's lifecycle; §0 item 5's
                    one-representation-per-fact rule                       (D-11)

TASK-154 D-10:      CHANGED (not fully superseded). Recorded verbatim:
                      "No new TargetStat value is introduced; §2.3.1 item 7's
                       TargetStat-iff-BuffDebuff pairing is relaxed for
                       rule-consumed BuffDebuffs. All other §2.3.1 invariants
                       unchanged."
                    Its no-new-TargetStat-value core is PRESERVED; only its
                    invariant-confirmation clause changes                 (D-12)

Other BuffDebuff
semantics:          NONE affected. Recorded verbatim: "Thủy-Ma-specific — no
                    other BuffDebuff semantics affected". Root's and Hỏa
                    Long's Rage's TargetStat = "ATK" instances keep the pairing
                    (their Magnitude IS stat-consumed); Burn (DoT), Shield
                    (Shield), and Stun (State) never carried it   (D-13)

Documentation
consequences:       REQUIRED — GAME_STATE.md §2.3.1 item 7 + the TargetStat
                    schema line; §2.3.1 item 3 and §2.3.2 item 3 need a
                    consistency pass; §2.4.1 needs no substantive change.
                    NO CHANGE — BOSS_RULES.md §6.2.2 (recorded verbatim: "Keep
                    §6.2.2 as-is — the relaxed item 7 makes its statement
                    consistent"); COMBAT_RULES.md §5.4.5 / §5.5.3 / §5.1 /
                    §4 item 7; GAME_RULES.md; GAME_EVENTS.md;
                    SIGNALR_PROTOCOL.md; REDIS_STATE.md; DATABASE.md;
                    ARCHITECTURE.md; TDD.md; PASSIVE_RULES.md; MVP_SCOPE.md.
                    ADR required: NO — recorded verbatim: "No ADR — the change
                    is a state-contract wording change, not architectural" (D-14)
```

**Do not add to this list, and do not drop an item from it.** Every statement this task writes traces to one of these items.

---

## Documentation Ownership

Per `.ai/workflow/documentation/documentation-change.md` §1 and §3, each concept has exactly one canonical owner. This task edits only the owner of the concept it changes, and does not duplicate the rule into documents that merely reference it.

```text
Concept                                          Canonical owner (this task edits)
-----------------------------------------------  --------------------------------
The StatusEffect instance schema; the             docs/02-technical/GAME_STATE.md
  TargetStat absence convention and the             §2.3.1 schema line + item 7
  condition under which a BuffDebuff
  carries TargetStat
The serialized member-presence shape of           docs/02-technical/GAME_STATE.md
  StatusEffects[]                                   §2.3.2 item 3
```

Cross-owner constraint (`documentation-change.md` §2): the Thủy Ma effect's magnitude, trigger, duration, carrier, selector, and consumption boundary stay in `BOSS_RULES.md` §6.2.2 and are **referenced**, never restated, in `GAME_STATE.md`; and the `"ATK"`-consumption rules and their boundary stay in `COMBAT_RULES.md` §5.4/§5.5 and are **referenced**, never restated. The relaxed item 7 must therefore be worded as a property of the instance and must not enumerate, restate, or define any particular effect's behavior.

---

## Required Changes

The smallest exact edits that apply the decision. Each is tied to a TASK-156 decision item. **All three sites are in `docs/02-technical/GAME_STATE.md`.**

### 1. `GAME_STATE.md` §2.3.1 item 7 — replace the unconditional pairing statement

```text
[D-11, D-12] Replace the unconditional TargetStat-iff-BuffDebuff pairing
             statement with the Product Owner's supplied rule, TRANSCRIBED
             VERBATIM:

               "a BuffDebuff carries TargetStat iff its Magnitude is consumed
                as a stat modifier; a BuffDebuff consumed by a non-stat rule
                selects by Id and omits TargetStat"

             PRESERVE UNCHANGED within item 7:
               - the absence convention itself: TargetStat and ExpiryCondition
                 are absent when they do not apply — never `null`, never a
                 sentinel string;
               - the reference to §2.1.7 item 3's absent-member convention;
               - the mutual exclusivity of RemainingTurns and ExpiryCondition
                 (exactly one is present), which follows from item 3;
               - the always-present statement for Magnitude and Source.

             The rule is a PROPERTY OF THE INSTANCE (whether its Magnitude is
             consumed as a stat modifier) and a SELECTOR consequence (an
             instance consumed by a non-stat rule selects by Id). Do NOT
             name Thủy Ma here, do NOT name any specific Id value here, and do
             NOT enumerate which effects are rule-consumed — the effect's own
             rule (BOSS_RULES.md §6.2.2) owns that, and item 1 already
             establishes that an Id names an instance without §2.3.1
             enumerating an effect registry.
```

### 2. `GAME_STATE.md` §2.3.1 schema line — bring the `TargetStat` line into line

```text
[D-11, D-14] The schema block's TargetStat line currently reads
             "the modified stat for Type = "BuffDebuff", e.g. "ATK"; absent
             otherwise", which asserts the pairing unconditionally. Bring it
             into line with the replacement rule: state that TargetStat is the
             modified stat for a BuffDebuff whose Magnitude is consumed as a
             stat modifier (e.g. "ATK"), and is absent — never `null`, never a
             sentinel — for a BuffDebuff consumed by a non-stat rule.

             The line remains a STRING typing statement at the same optional
             status. Do NOT change the member's type, its optionality, its
             position in the tree, or any other line of the schema block.
             Do NOT add a new member, a new value, or an example Id.
             The "e.g. "ATK"" example is an existing illustrative value and is
             NOT a new TargetStat value (D-4, D-12).
```

### 3. `GAME_STATE.md` §2.3.2 item 3 — reconcile the serialized restatement

```text
[D-14] The serialized-shape list records `targetStat  string  optional
       (present iff type = "BuffDebuff")`, which restates the pairing that
       item 7 no longer states unconditionally. Reconcile this line with the
       replacement rule so the serialized shape agrees with §2.3.1 item 7.

       The member's TYPE (string) and its OPTIONAL status are UNCHANGED. Do
       NOT change any other line of the list (id, type, source, magnitude,
       remainingTurns, expiryCondition). Do NOT change §2.3.2 items 1, 2, 4, 5,
       6, or 7, and do NOT introduce a serialization mechanism or a
       Redis-only field.

       §2.3.2 item 1's statement that "Absence of a *member within* an element
       follows §2.3.1 item 7" remains correct and unchanged — it points at
       item 7, which is where the rule now differs.
```

### 4. `GAME_STATE.md` §2.3.1 item 3 — consistency pass only

```text
[D-14] TASK-156 D-14 lists §2.3.1 item 3 among the sites needing "a
       consistency pass". Its type/duration-model assignment is UNCHANGED
       (D-11: item 3 is explicitly UNCHANGED): the `DoT | BuffDebuff | Shield |
       State` vocabulary is unchanged, and `BuffDebuff` keeps the Turn
       countdown (`RemainingTurns`).

       Therefore: make NO substantive change to item 3, and add NO new
       bullet, example, or effect name to it. If — and only if — item 3's text
       contains a statement that unconditionally asserts the BuffDebuff /
       TargetStat pairing, correct that clause and report it in Completion
       Evidence with the exact before/after wording. If no such statement
       exists, record explicitly that item 3 required no edit. Do NOT
       restructure item 3's duration-model assignment.
```

### 5. `GAME_STATE.md` version block

```text
[D-11, D-12, D-14] Record this revision in the document's version block,
       following the existing convention in that file (newest revision first;
       prior revisions retained as "Prior N.N:"; 2.17 → 2.18).

       State what changed: §2.3.1 item 7's TargetStat pairing and the schema
       line are relaxed per the TASK-156 Product Owner decision; §2.3.2 item 3
       reconciled to match.

       State explicitly what did NOT change: no new TargetStat value, no new
       StatusEffect Type, no PendingStatusEffects[], no second in-flight
       representation, no member/value/type/collection added to §2.3.1's member
       set or the BossState tree, no change to §2.3.3, §2.4, §2.4.1, §5.1.1, or
       §0 item 5, no new Battle Event, no SignalR member, no Redis key, and no
       database column.

       Cite TASK-156 as the decision source and TASK-157 as the applying task.
       The 2.17 block's statement that "item 7's TargetStat-iff-BuffDebuff
       pairing still holds as written" is now superseded by this revision; it
       is retained as part of the prior-revision history, not deleted.
```

---

## Explicit Non-Changes

Per TASK-156 D-13 and D-14. **Do not modify these.** The only permitted exception is an unavoidable cross-reference correction — a reference that has become factually stale because of the edits above — and any such correction must be reported in Completion Evidence with its justification.

```text
docs/01-game-design/BOSS_RULES.md        NO change. Recorded verbatim by the
                                         Product Owner: "Keep §6.2.2 as-is —
                                         the relaxed item 7 makes its statement
                                         consistent". Its magnitude (50%),
                                         duration (3 turns), Battle Start
                                         trigger, carrier, Id selector, target,
                                         reapplication rule, and "no new event
                                         or protocol" boundary are all
                                         unchanged. §6.2.1 (Hỏa Long Rage) and
                                         §6.2.3 (Mộc Yêu regeneration) are
                                         untouched.
docs/01-game-design/COMBAT_RULES.md      NO change. §5.4.5 / §5.5.3 remain
                                         correct ("not a TargetStat-consumed
                                         BuffDebuff"; no new non-"ATK" case)
                                         and their "would require its own
                                         recorded decision" boundary is neither
                                         weakened nor exercised. §5.1's type
                                         definition, §4 item 7's mechanism /
                                         scope / ordering / clamp position, and
                                         §5.4/§5.5's "ATK" rules are unchanged.
docs/01-game-design/GAME_RULES.md        NO change. §16's event list, §17's steps
                                         18a/19a, and §18's server authority
                                         are unaffected.
docs/02-technical/GAME_EVENTS.md         NO change. No event is added.
docs/02-technical/SIGNALR_PROTOCOL.md    NO change. No wire member is added;
                                         §6.2.4's intentional BossState
                                         client-invisibility limitation stands.
docs/02-technical/REDIS_STATE.md         NO change. No new key; TargetStat's
                                         absence is already a representable
                                         serialized case per §2.3.1 item 7, so
                                         the serialized member set is
                                         unchanged and the effect rides the
                                         existing single write-back.
docs/02-technical/DATABASE.md            NO change. No column, no schema.
docs/02-technical/ARCHITECTURE.md        NO change. No boundary moves.
docs/02-technical/TDD.md                 NO change. No determinism or hot-path
                                         contract change.
docs/01-game-design/PASSIVE_RULES.md     NO change. The Battle Start trigger form
                                         is unchanged.
docs/00-overview/MVP_SCOPE.md            NO change. No scope change; nothing OUT
                                         is introduced.
docs/02-technical/GAME_STATE.md          §2.3.3, §2.4, §2.4.1, §5.1, §5.1.1, and
  (internal)                             §0 item 5: NO change. §2.3.1 items 1,
                                         2, 3 (subject to Required Change 4's
                                         consistency pass only), 6, 8, 9, 10,
                                         11, and 12: NO change.
```

**No ADR.** TASK-156 D-14 records verbatim: "No ADR — the change is a state-contract wording change, not architectural", with the reasoning that no architecture, database strategy, realtime strategy, module boundary, infrastructure, carrier, member set, or lifecycle changes. Creating an ADR here would contradict the decision (`AGENTS.md` §18, `architecture/adr-change.md`).

**No new mechanic, and no broadened exception.** The exception is Thủy-Ma-specific (TASK-156 D-13). This task must not:
- add, rename, or retype a StatusEffect Type, or touch item 3's duration-model assignment;
- change any other BuffDebuff instance's behavior — Root's and Hỏa Long's Rage's `TargetStat = "ATK"` instances keep the pairing unchanged;
- introduce a TargetStat value, a sentinel, or a `null` representation;
- generalize the relaxation beyond "a BuffDebuff consumed by a non-stat rule selects by Id and omits TargetStat".

**No source code, and no implementation workaround.** Zero files under `src/` or `tests/`. Relaxing `StatusEffect.TurnBased`'s enforced pairing, and any test asserting the current pairing, belongs to a LATER implementation task (TASK-156's "Implementation consequence (recorded, NOT performed)"). This task must not touch that validator or any test.

---

## Scope

### In Scope

1. Replace `GAME_STATE.md` §2.3.1 **item 7**'s unconditional `TargetStat`-iff-`BuffDebuff` pairing statement with the TASK-156 D-11 rule, transcribed verbatim, preserving item 7's absence convention, its §2.1.7 item 3 reference, its mutual-exclusivity statement, and its always-present statement.
2. Bring `GAME_STATE.md` §2.3.1's **schema line** for `TargetStat` into line with that rule, preserving the member's type, optionality, and tree position.
3. Reconcile `GAME_STATE.md` §2.3.2 **item 3**'s `targetStat` presence condition with the same rule, preserving the member's type and optional status and every other line.
4. Perform the **consistency pass** on `GAME_STATE.md` §2.3.1 **item 3** (Required Change 4), changing nothing substantive and reporting the outcome either way.
5. Record the revision in `GAME_STATE.md`'s **version block**, stating what changed and explicitly what did not.
6. Verify that `BOSS_RULES.md` §6.2.2, `COMBAT_RULES.md` §4 item 7 / §5.4.5 / §5.5.3, and every other document named in "Explicit Non-Changes" remain consistent under the relaxed item 7 — **by reading, not by editing**.

### Out of Scope

- **Any change under `docs/` other than `docs/02-technical/GAME_STATE.md`.** In particular `BOSS_RULES.md` and `COMBAT_RULES.md` are NOT modified (TASK-156 D-14).
- **Any source code or test.** Zero files under `src/` or `tests/`. Relaxing `StatusEffect.TurnBased` and any test asserting the pairing is a LATER implementation task.
- **Reopening or modifying TASK-153, TASK-154, TASK-155, or TASK-156.** All four are immutable read-only sources. TASK-154's D-10 is superseded **for downstream purposes only** by the TASK-156 record; TASK-154 itself is not edited.
- **Reconciling TASK-153's lifecycle.** TASK-153 remains `BLOCKED` after this task. Leaving `BLOCKED` is a separate lifecycle act performed through the repository lifecycle workflow (`tasks/TASK_LIFECYCLE.md` §2, `BLOCKED → IN PROGRESS`), and this task must not perform it or re-status TASK-153 in any way.
- **Creating any implementation task.** This task applies the documentation; the implementation work is sequenced by the Orchestrator, not created here.
- **Creating a second decision-input task.** TASK-156 is the sole decision source; nothing here is a Product Owner question.
- **Any ADR** (`docs/03-decisions/ADR/`) — TASK-156 D-14 records that none is required.
- **Broadening the exception.** No new BuffDebuff mechanic, no new StatusEffect Type, no item 3 restructuring, no change to other BuffDebuff instances' semantics (TASK-156 D-13).
- **Changing any value.** The −50% magnitude, the 3-turn duration, the Battle Start trigger, the target (Pet HP healing only), the carrier, and the identity are unchanged; no new `TargetStat` value, sentinel, or `null` representation is introduced.
- **Introducing any new Battle Event, SignalR member, Redis key, or database column** (`BOSS_RULES.md` §6.2.2's "no new event or protocol").
- **`BossState` client exposure** — `BOSS_RULES.md` §6.2.4 records this as an intentional protocol limitation requiring its own separate decision.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Dependencies

```text
TASK-156  DECIDED   THE SOLE DECISION SOURCE this task applies (Option B;
                    D-11 supplies the replacement item 7 wording; D-12 the
                    changed TASK-154 D-10 wording; D-14 the document scope and
                    the no-ADR finding). IMMUTABLE; read-only; NOT reopened.
TASK-154  DECIDED   the GAP-5 decision record. Its D-10 is superseded only in
                    its invariant-confirmation portion (TASK-156 D-12).
                    IMMUTABLE; read-only.
TASK-155  DONE      applied the GAP-5 decision; authored
                    Id = "boss-thuy-ma-heal" and the §6.2.2 statements the
                    relaxed item 7 now makes consistent. IMMUTABLE; read-only.
TASK-153  BLOCKED   the downstream implementation. Remains BLOCKED until this
                    documentation application is completed. IMMUTABLE;
                    read-only; NOT modified, re-statused, or moved here.
TASK-124  DONE      documentation-apply precedent.
TASK-151  DONE      documentation-apply precedent (shape, version-block and
                    Completion Evidence conventions).
TASK-123  DONE      the step-18a decision set.
```

All dependencies are satisfied in the sense required by this task: the decision exists and is complete, its replacement wording was supplied verbatim by the Product Owner, the canonical owner document and the three edit sites are enumerated, MVP scope is confirmed, the primary agent and workflow are assigned, and the acceptance criteria are testable. The one outstanding input — the Product Owner decision — is already supplied by TASK-156.

---

## TASK-153 Dependency

```text
TASK-153 remains BLOCKED until this documentation application is completed.
```

That is an explicit statement of this task's dependency direction, not an action:

- This task **must not** re-status TASK-153, move it between folders, or edit it in any way. It is immutable and read-only.
- This task **must not** perform the `BLOCKED → IN PROGRESS` (or `→ READY`) transition. Per `tasks/README.md` §5 and `tasks/TASK_LIFECYCLE.md` §2, `BLOCKED → IN PROGRESS` is a transition a human resolves after the blocking condition is removed; the repository's lifecycle workflow owns it.
- **After** this documentation application is completed — i.e. after all Acceptance Criteria below are satisfied and the task reaches DONE — TASK-153 may be reconciled to READY through the repository lifecycle workflow. That reconciliation is a separate act with its own authority, performed outside this task.
- Ordering is therefore: TASK-156 (DECIDED, done) → **this task** (documentation applied) → TASK-153 reconciliation (separate lifecycle act) → TASK-153 implementation (separate implementation task, not created here).

The reason for the ordering: the TASK-156 decision is not self-executing. Until item 7 and its two restatements are updated, `BOSS_RULES.md` §6.2.2's documented instance remains unrepresentable in the documented state model, so TASK-153's implementation still has no consistent authoritative contract to implement against (`AGENTS.md` §17, §23).

---

## Acceptance Criteria

All binary and testable.

```text
[x] `GAME_STATE.md` §2.3.1 item 7 no longer states the unconditional
    `TargetStat`-iff-`BuffDebuff` pairing.
[x] `GAME_STATE.md` §2.3.1 item 7 states the TASK-156 D-11 rule, transcribed
    verbatim: "a BuffDebuff carries TargetStat iff its Magnitude is consumed as
    a stat modifier; a BuffDebuff consumed by a non-stat rule selects by Id and
    omits TargetStat".
[x] `GAME_STATE.md` §2.3.1 item 7 still states the absence convention: absent
    when it does not apply, never `null`, never a sentinel string.
[x] `GAME_STATE.md` §2.3.1 item 7 still references §2.1.7 item 3's
    absent-member convention.
[x] `GAME_STATE.md` §2.3.1 item 7 still states that `RemainingTurns` and
    `ExpiryCondition` are mutually exclusive, exactly one being present.
[x] `GAME_STATE.md` §2.3.1 item 7 still states that `Magnitude` and `Source` are
    always present.
[x] `GAME_STATE.md` §2.3.1 item 7 names no specific effect and no specific `Id`
    value, and enumerates no list of rule-consumed effects.
[x] `GAME_STATE.md` §2.3.1's `TargetStat` schema line no longer asserts the
    pairing unconditionally and is consistent with the new item 7 rule.
[x] `GAME_STATE.md` §2.3.1's `TargetStat` schema line remains a `string`
    member at `optional` status, in the same tree position.
[x] `GAME_STATE.md` §2.3.2 item 3's `targetStat` presence condition is
    reconciled with the new item 7 rule.
[x] `GAME_STATE.md` §2.3.2 item 3 keeps `targetStat` typed `string` and marked
    `optional`, and changes no other line of the serialized-shape list.
[x] `GAME_STATE.md` §2.3.1 item 3 is substantively unchanged: the
    `DoT | BuffDebuff | Shield | State` vocabulary is intact, and `BuffDebuff`
    still uses the Turn countdown (`RemainingTurns`).
[x] Either §2.3.1 item 3 required no edit and the Completion Evidence says so
    explicitly, or its only change was a clause that unconditionally asserted
    the pairing, reported with exact before/after wording.
[x] `GAME_STATE.md`'s version block is advanced (2.17 → 2.18) and states what
    changed.
[x] `GAME_STATE.md`'s version block states explicitly what did NOT change: no
    new `TargetStat` value, no new StatusEffect `Type`, no
    `PendingStatusEffects[]`, no second in-flight representation, no member,
    value, type, or collection added, no change to §2.3.3, §2.4, §2.4.1,
    §5.1.1, or §0 item 5, no new Battle Event, SignalR member, Redis key, or
    database column.
[x] `GAME_STATE.md`'s version block cites TASK-156 as the decision source and
    TASK-157 as the applying task.
[x] `GAME_STATE.md` §2.3.1 items 1, 2, 6, 8, 9, 10, 11, and 12 are unchanged.
[x] `GAME_STATE.md` §2.3.3 is unchanged; no `PendingStatusEffects[]` and no
    queued/pending collection reference is added or removed.
[x] `GAME_STATE.md` §2.4 and §2.4.1 are unchanged.
[x] `GAME_STATE.md` §5.1 and §5.1.1 are unchanged, and §0 item 5 is unchanged.
[x] No new `TargetStat` value appears anywhere in `docs/` (targeted search;
    the pre-existing illustrative `"ATK"` example is not a new value).
[x] No sentinel `TargetStat` string and no `null` `TargetStat` representation
    is introduced anywhere in `docs/`.
[x] Root's and Hỏa Long's Rage's `TargetStat = "ATK"` instances are unchanged
    in `docs/`, and nothing in the edit states or implies that they stop
    carrying `TargetStat`.
[x] Burn (`DoT`), Shield (`Shield`), and Stun (`State`) semantics are unchanged
    in `docs/`.
[x] No new `BuffDebuff` behavior beyond the TASK-156 decision is stated,
    implied, or enabled by the wording.
[x] `docs/01-game-design/BOSS_RULES.md` is byte-identical.
[x] `docs/01-game-design/COMBAT_RULES.md` is byte-identical.
[x] Every document listed under "Explicit Non-Changes" is byte-identical.
[x] Only `docs/02-technical/GAME_STATE.md` is modified under `docs/`.
[x] Zero files under `src/` are modified.
[x] Zero files under `tests/` are modified.
[x] No source code changes.
[x] No BOSS_RULES.md changes.
[x] No COMBAT_RULES.md changes.
[x] No ADR is created.
[x] No new TargetStat value.
[x] No new BuffDebuff behavior beyond TASK-156.
[x] No gameplay implementation.
[x] TASK-156 is byte-identical to its DECIDED state.
[x] TASK-154 and TASK-155 are byte-identical.
[x] TASK-153 is byte-identical and its Status is still `BLOCKED` (not
    re-statused, not moved, not edited).
[x] No other task file is modified or created, and no implementation task and
    no second decision-input task is created.
[x] Every statement written traces to a named TASK-156 D-item; no gameplay rule
    was authored, reinterpreted, or added.
[x] No rule is duplicated across owners (`documentation-change.md` §2):
    `GAME_STATE.md` still references `BOSS_RULES.md` §6.2.2 and
    `COMBAT_RULES.md` §5.4/§5.5 rather than restating any effect's behavior or
    any `"ATK"` consumption rule.
[x] Every section and document cross-reference introduced or touched resolves.
[x] The task explicitly states that TASK-153 remains BLOCKED until this
    documentation application is completed, and does not perform that
    lifecycle transition.
[x] Quality review checklist passes (`quality/review.md` §1)
[x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)
```

---

## Affected Files & Areas

```text
[ ] src/backend/ — NONE
[ ] src/frontend/client/ — NONE
[ ] tests/ — NONE
[x] docs/02-technical/GAME_STATE.md
      — §2.3.1 schema line (TargetStat), §2.3.1 item 7, §2.3.1 item 3
        (consistency pass only — no substantive change), §2.3.2 item 3,
        and the version block
[ ] docs/01-game-design/BOSS_RULES.md — NONE (TASK-156 D-14: no change)
[ ] docs/01-game-design/COMBAT_RULES.md — NONE (TASK-156 D-14: no change)
[ ] docs/ — every other document: NONE (see "Explicit Non-Changes")
[ ] docs/03-decisions/ADR/ — NONE (no ADR; TASK-156 D-14)
[ ] tasks/blocked/TASK-153-*.md — NONE (immutable; stays BLOCKED)
[ ] tasks/backlog/TASK-154-*.md — NONE (immutable; read-only)
[ ] tasks/backlog/TASK-155-*.md — NONE (immutable; read-only)
[ ] tasks/backlog/TASK-156-*.md — NONE (immutable; the sole decision source)
[x] tasks/backlog/TASK-157-<this file>.md — this task file and its Completion
      Evidence section
```

---

## Implementation Notes

- **The decision is complete; this is transcription, not design.** Every statement this task writes traces to a TASK-156 D-item. If a statement cannot be traced, do not write it.
- **The replacement wording is supplied, not authored.** TASK-156 D-11 records the Product Owner's rule verbatim. Transcribe it; do not improve, generalize, or narrow it. In particular, do not add an effect-specific example to it and do not list which instances are rule-consumed.
- **Three sites, one document.** TASK-156 D-14 names `GAME_STATE.md` §2.3.1 item 7, the schema line, and §2.3.2 item 3. Item 3 gets a consistency pass only. Do not assume additional documentation changes are needed; the decision explicitly decided that `BOSS_RULES.md` §6.2.2, `COMBAT_RULES.md` §5.4.5, and `COMBAT_RULES.md` §5.5.3 remain unchanged.
- **The rule is a property of the instance, not a registry.** Keying on "whether its Magnitude is consumed as a stat modifier" keeps the relaxation expressible generically while leaving it Thủy-Ma-specific in effect (TASK-156 D-13). `GAME_STATE.md` §2.3.1 item 1 already establishes that an `Id` names an instance without the schema enumerating an effect registry — so the rule must not enumerate effects and must not add an `Id` value to the schema.
- **Why the other BuffDebuffs are safe.** Root's (Pet-side) and Hỏa Long's Rage's (Boss-side) `TargetStat = "ATK"` instances have a `Magnitude` that **is** consumed as a stat modifier (`COMBAT_RULES.md` §5.4 / §5.5.1), so the pairing still applies to them. Burn (`DoT`), Shield (`Shield`), and Stun (`State`) are not `BuffDebuff`, so item 7 never applied to them and their `TargetStat` absence was already governed by the unchanged "absent otherwise" clause and by item 3. This is why the edit changes no other behavior — and why the wording must not accidentally read as licensing a non-stat `BuffDebuff` that has no non-stat consumer.
- **§2.3.2 item 3 is a restatement, not a second owner.** `documentation-change.md` §2 forbids duplication; reconcile the line to point consistently at item 7's rule rather than restating the pairing in a divergent form.
- **Version blocks are the convention, not optional.** `GAME_STATE.md` carries a long inline version history; follow the existing style (newest first, prior revisions retained as "Prior N.N:"). The 2.17 block's claim that the pairing "still holds as written" becomes stale and must be superseded by the new revision's text.
- **The enforced validator is NOT this task's concern.** `StatusEffect.TurnBased` still enforces the old pairing in code. That mismatch is expected and is precisely what the later implementation task exists to fix; this task must not touch it, and must not treat its presence as a reason to weaken the documentation edit (`AGENTS.md` §17: update the source-of-truth documentation first).
- **Verify the document has not moved.** Re-derive the edit sites from the current file rather than trusting the recorded line numbers; confirm the `GAME_STATE.md` SHA-256 before editing.
- **TASK-155 and TASK-151 are the precedents.** Both applied a decided contract at canonical owners without re-deciding it, and both recorded Decision Traceability plus Scope Verification in Completion Evidence. Match that shape.

---

## Testing / Documentation Validation

### Required Verification

```text
[x] Unit tests         — N/A. No code is modified by this task.
[x] Integration tests  — N/A. No code is modified by this task.
[x] Gameplay scenarios — N/A. No gameplay behavior is implemented by this task.
```

This task produces no runnable artifact, so its verification is a
documentation-consistency review (`quality/review.md` §1,
`quality/documentation-consistency.md`), performed by reading the changed
sections and their referencing documents together:

```text
[ ] Every changed statement traces to a named TASK-156 D-item.
[ ] The documented Thủy Ma instance is now representable and non-
    contradictory: read GAME_STATE.md §2.3.1 item 7 + the schema line +
    §2.3.2 item 3 together with BOSS_RULES.md §6.2.2, and confirm the stated
    Type = BuffDebuff with TargetStat absent and Id = "boss-thuy-ma-heal" is
    admitted with no residual conflict.
[ ] BOSS_RULES.md §6.2.2 requires no edit and is byte-identical; its
    "Representation" and "Applicable-instance selector" statements remain
    consistent under the relaxed item 7.
[ ] COMBAT_RULES.md §5.4.5 and §5.5.3 require no edit and are byte-identical;
    their "not a TargetStat-consumed BuffDebuff" / "no new non-"ATK" case"
    statements remain correct, and their "would require its own recorded
    decision" boundary is neither weakened nor exercised.
[ ] Targeted search of docs/ for TargetStat: no value other than the
    pre-existing illustrative "ATK" appears; no sentinel and no `null`
    representation is introduced.
[ ] Targeted search of docs/ for PendingStatusEffects[]: only the pre-existing
    prohibition statements appear; nothing new was added.
[ ] Targeted search of docs/ for new event/wire/storage names: no new Battle
    Event, SignalR member, Redis key, or database column is named.
[ ] Root's and Hỏa Long's Rage's TargetStat = "ATK" instances are byte-
    identical in docs/ and still carry their TargetStat.
[ ] Burn, Shield, and Stun semantics are byte-identical in docs/.
[ ] §2.3.1 item 3's type/duration-model assignment is byte-identical in
    substance.
[ ] No rule is duplicated across owners; each owner still owns its concept.
[ ] Every section and document cross-reference introduced or touched resolves.
[ ] The version block is internally consistent and states what did not change.
[ ] Scope: docs/ files other than GAME_STATE.md = 0; src/ = 0; tests/ = 0;
    TASK-153, TASK-154, TASK-155, and TASK-156 = 0; other tasks = 0; ADRs = 0.
```

### Key Edge Cases

- **The relaxation is written so broadly that it becomes a general new BuffDebuff mechanic.** Every non-stat `BuffDebuff` would then appear licensed to omit `TargetStat` with no `Id`-selected consumer. Key the rule on the instance's `Magnitude` consumption as TASK-156 D-11 does, and do not generalize further — STOP and re-read D-11/D-13 if the wording drifts.
- **A `TargetStat` value is introduced to carry the selection instead.** Directly contradicts TASK-156 D-4/D-12 and `COMBAT_RULES.md` §5.4.5/§5.5.3 — STOP.
- **A `TargetStat` sentinel or `null` representation is introduced.** Contradicts TASK-156 D-4 and the preserved absence convention — STOP.
- **§2.3.1 item 3 is restructured to accommodate the relaxation.** TASK-156 D-11 records item 3 UNCHANGED — STOP and revert to a consistency pass only.
- **§2.3.2 item 3 is rewritten into a second, divergent statement of the pairing.** Duplication across a restatement (`documentation-change.md` §2) — reconcile it, do not re-author it.
- **The Thủy Ma `Id` value is added to §2.3.1's schema block.** Contradicts item 1's identity-not-definition rule and TASK-156 D-11's unchanged-item list — the identity belongs to `BOSS_RULES.md` §6.2.2.
- **`BOSS_RULES.md` §6.2.2 or `COMBAT_RULES.md` §5.4.5/§5.5.3 is edited "for consistency".** TASK-156 D-14 records no change required — STOP.
- **The edit is extended to cover a source-code or lifecycle mismatch.** `StatusEffect.TurnBased` still enforcing the old pairing is expected and is a later implementation task's work — STOP and record it as reported downstream scope instead.
- **TASK-153 is re-statused or moved to make the sequence look complete.** This task asserts the dependency and must not perform the transition — STOP.
- **An ADR is proposed because the invariant is part of the state contract.** TASK-156 D-14 records "No ADR" with its reasoning — STOP.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- **If TASK-156's decision cannot be mapped cleanly onto the cited `GAME_STATE.md` sections:** **STOP** and report which item fails to map. Do not improvise a mapping.
- **If the required wording would change another `GAME_STATE.md` invariant:** **STOP** per `AGENTS.md` §4. TASK-156 D-11 records exactly one changed item (item 7); a required second change means the decision and the state contract disagree.
- **If §2.3.2 item 3 cannot be updated without changing unrelated state semantics:** **STOP**. Only the `targetStat` presence condition may change; the member's type, optionality, and every other line stay.
- **If §2.3.1 item 3 cannot be left substantively unchanged:** **STOP**. D-11 records it unchanged; a required substantive change is a conflict between the decision and the document.
- **If `BOSS_RULES.md` or `COMBAT_RULES.md` must change contrary to TASK-156 D-14:** **STOP** and report the conflict with both sources (file + section) rather than picking a side.
- **If a new architectural decision is required:** **STOP** per `AGENTS.md` §18. D-14 records "No ADR"; an architectural change needs its own ADR-first task, not this one.
- **If the documentation change would broaden the Thủy-Ma decision beyond TASK-156:** **STOP**. In particular, if the wording would alter Root's or Hỏa Long's Rage's `TargetStat = "ATK"` behavior, Burn, Shield, Stun, or a new `TargetStat` case, the exception has been over-widened (D-13).
- **If applying the decision requires a new `TargetStat` value, a sentinel, or a `null` representation:** **STOP**. D-4 and D-12 forbid all three.
- **If TASK-156's decision is found to be incomplete or internally inconsistent:** **STOP**. TASK-156 is DECIDED and immutable; reopening it is not this task's authority.
- **If applying the decision requires re-opening TASK-154 or TASK-155:** **STOP**. They are immutable; the change to D-10's invariant-confirmation clause is recorded in TASK-156 for downstream purposes only.
- **If the edits cannot be confined to the three named `GAME_STATE.md` sites plus the version block** (and, at most, a clause in item 3's consistency pass): **STOP** and report — that indicates the invariant's ownership is more entangled than TASK-156 D-14 recorded.
- **If the change requires source code or a test change to be coherent:** **STOP**. This task is documentation only; the enforced validator is a later implementation task's concern.
- **If TASK-153 must be modified, re-statused, or moved to complete this task:** **STOP**. TASK-153 is immutable and remains BLOCKED; its reconciliation is a separate lifecycle act.
- **If the task exceeds 7 skills or crosses multiple uncoupled architectural boundaries:** **STOP & decompose** (`tasks/README.md` §12).

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

```text
Decision source:
TASK-156

Documentation applied:
GAME_STATE.md §2.3.1 item 7
GAME_STATE.md §2.3.1 schema line
GAME_STATE.md §2.3.2 item 3
GAME_STATE.md version block

Consistency check:
GAME_STATE.md §2.3.1 item 3 — unchanged (no edit required; type and duration-model assignment unchanged)

Unchanged:
BOSS_RULES.md §6.2.2
COMBAT_RULES.md §5.4.5
COMBAT_RULES.md §5.5.3

TASK-153:
BLOCKED pending separate lifecycle reconciliation
```

### Decision source
- **TASK-156** —
  `tasks/backlog/TASK-156-resolve-thuy-ma-healing-reduction-statuseffect-representation-vs-game-state-buffdebuff-targetstat-invariant.md`,
  "Decision Record" (D-1 … D-14), **Status: DECIDED**, Option B. TASK-156 is
  the **sole decision source** and was **not modified** by this task.

### Canonical documentation updated

```text
docs/02-technical/GAME_STATE.md      Version 2.17 → 2.18
    §2.3.1 item 7        — replaced the unconditional
                           TargetStat-iff-BuffDebuff pairing with the TASK-156
                           D-11 rule, transcribed verbatim; preserved the
                           absence convention, the §2.1.7 item 3 reference,
                           the RemainingTurns/ExpiryCondition mutual
                           exclusivity, and the Magnitude/Source
                           always-present statement
    §2.3.1 schema line   — TargetStat line brought into line with the new
                           rule (same `string` type, same `optional` status,
                           same tree position)
    §2.3.1 item 3        — consistency pass only; type/duration-model
                           assignment unchanged
    §2.3.2 item 3        — targetStat presence condition reconciled with the
                           new item 7 rule (type and optional status unchanged)
    version block        — 2.18 recorded, stating what changed and what did
                           not; cites TASK-156 (decision) and TASK-157
                           (applying task)
```

### Changed Files
- `docs/02-technical/GAME_STATE.md` — §2.3.1 item 7, the §2.3.1 schema line,
  §2.3.1 item 3 (consistency pass), §2.3.2 item 3, and the `**Version:**` block.
- `tasks/backlog/TASK-157-…md` — this Completion Evidence section.

**No other file was created, modified, or deleted by this task.** Zero files under
`src/`, zero under `tests/`, and zero `docs/` files other than `GAME_STATE.md`.
`BOSS_RULES.md` and `COMBAT_RULES.md` are byte-identical. `TASK-153`, `TASK-154`,
`TASK-155`, and `TASK-156` are byte-identical to their pre-task state.

### Decision Traceability

```text
D-1  Option B — keep Type = BuffDebuff with TargetStat absent; relax item 7
     → GAME_STATE.md §2.3.1 item 7 (relaxed) and the schema line
D-2  Type = BuffDebuff unchanged; no vocabulary change
     → §2.3.1 item 3 NOT substantively changed; the schema's Type line unchanged
D-3  Boss-owned, Pet HP healing only
     → no §2.3.1 text added; the target remains BOSS_RULES.md §6.2.2's (D-14:
       no change there)
D-4  No TargetStat carried; absent, never null, never a sentinel; no new value
     → §2.3.1 item 7's absence convention preserved verbatim in effect; the
       schema line keeps `optional` and adds no value
D-5  Id = "boss-thuy-ma-heal" remains the sole selector
     → §2.3.1 item 7's relaxed rule states the Id-based selection; the Id VALUE
       is NOT added to §2.3.1 (it stays in BOSS_RULES.md §6.2.2)
D-6  Carrier BossState.StatusEffects[] unchanged
     → §2.4.1 NOT modified
D-7  Duration RemainingTurns = 3 unchanged
     → §2.3.1 item 3's Turn-countdown assignment for BuffDebuff unchanged
D-8  Lifecycle / expiry unchanged
     → §5.1.1 NOT modified
D-9  Heal Resolution read unchanged and not invalidated
     → §2.4.1's authorized-cross-entity-read paragraph NOT modified
D-10 Reapplication unchanged
     → §2.3.1 item 6 NOT modified
D-11 Exactly one item changes (item 7 + its schema line); everything else
     unchanged
     → §2.3.1 items 1, 2, 6, 8, 9, 10, 11, 12 NOT modified; §2.3.3, §2.4,
       §2.4.1, §5.1.1, §0 item 5 NOT modified
D-12 TASK-154 D-10 changed in its invariant-confirmation portion only; no new
     TargetStat value
     → targeted search confirms no TargetStat value other than the
       pre-existing illustrative "ATK" appears in docs/
D-13 Thủy-Ma-specific; no other BuffDebuff semantics affected
     → wording keys on the instance's Magnitude consumption; Root's and Hỏa
       Long's Rage's TargetStat = "ATK" instances and Burn/Shield/Stun stay
       byte-identical
D-14 Documentation consequences; no ADR
     → only GAME_STATE.md §2.3.1 item 7 + schema line + §2.3.2 item 3 (plus the
       version block) modified; BOSS_RULES.md and COMBAT_RULES.md byte-identical;
       no ADR created
```

### Validation Results
```text
[x] Every changed statement traces to a named TASK-156 D-item (table above).
[x] BOSS_RULES.md §6.2.2, read against the updated §2.3.1 item 7, requires no
    edit and is byte-identical (SHA-256: 1EE72E371CBFDE60E60BC9BF9755F35C54F9EC1525FAE61BF3F9D7B7D76D3215).
[x] COMBAT_RULES.md §5.4.5 / §5.5.3, read against the updated item 7, remain
    correct and byte-identical (SHA-256: C0C5E5A12B43A70077337548570B7717C450067BB5E6EF2B84BE09D58647BE16).
[x] Targeted search of docs/ for TargetStat: only the pre-existing "ATK"
    spelling appears; no sentinel, no null, no new value.
[x] Targeted search of docs/ for PendingStatusEffects[]: only pre-existing
    prohibition statements; nothing added.
[x] Targeted search for new event/wire/storage names: none introduced.
[x] Root's and Hỏa Long's Rage's TargetStat = "ATK" instances byte-identical.
[x] Burn / Shield / Stun semantics byte-identical.
[x] §2.3.1 item 3: consistency pass performed; no edit required, type and duration-model assignment unchanged.
[x] No rule duplicated across owners; all introduced cross-references resolve.
[x] Version block internally consistent and states what did not change.
```

### Scope Verification
- [x] Confirmed zero files under `src/` modified
- [x] Confirmed zero files under `tests/` modified
- [x] Confirmed only `docs/02-technical/GAME_STATE.md` modified under `docs/`
- [x] Confirmed `BOSS_RULES.md` byte-identical (no change)
- [x] Confirmed `COMBAT_RULES.md` byte-identical (no change)
- [x] Confirmed no new `TargetStat` value, no sentinel, no `null` representation
- [x] Confirmed no `PendingStatusEffects[]` added
- [x] Confirmed no new Battle Event, SignalR member, Redis key, or DB column
- [x] Confirmed no ADR created
- [x] Confirmed TASK-156 byte-identical (immutable sole decision source)
- [x] Confirmed TASK-154 and TASK-155 byte-identical
- [x] Confirmed TASK-153 byte-identical and still BLOCKED
- [x] Confirmed no source code, no gameplay implementation, no new BuffDebuff
      behavior beyond TASK-156
- [x] Confirmed no other task created (no implementation task, no second
      decision-input task)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed zero client-authoritative gameplay logic (no code produced)

### New gameplay decisions
**NONE.** Every statement written is a transcription of a TASK-156 D-item or of
an existing rule referenced by it. The replacement item 7 wording was supplied
verbatim by the Product Owner in TASK-156 D-11.

### Downstream scope (reported; not created or performed here)
```text
Still BLOCKED pending this documentation application:
  TASK-153 (MVP Boss Passive effects at step 18a)
    — remains BLOCKED; it is NOT re-statused, moved, or edited by this task.

Separate lifecycle act after this task reaches DONE:
  TASK-153 BLOCKED → READY reconciliation, through the repository lifecycle
  workflow (tasks/TASK_LIFECYCLE.md §2). Not performed here.

Later implementation task (not created here):
  StatusEffect.TurnBased's BuffDebuff/TargetStat validation, and any test
  asserting the current pairing — the enforced domain model must be relaxed to
  match the updated §2.3.1 item 7 before TASK-153's implementation can proceed.
```

---

**Documentation-application only. No source code. No BOSS_RULES.md changes. No
COMBAT_RULES.md changes. No ADR. No new TargetStat value. No new BuffDebuff
behavior beyond TASK-156. No gameplay implementation.**
