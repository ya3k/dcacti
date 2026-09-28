# TASK-073 — Resolve the Element Value-Set Contract for Battle-Start and Redis State

---

## Metadata

```text
Task ID:           TASK-073
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM (baseline for DOCUMENTATION is LOW–MEDIUM; raised
                   because the battle-start/Redis element convergence
                   implementation task cannot start until this contract is
                   bound — cross-referenced contract, tasks/TASK_TYPES.md §4)
Priority:          HIGH (blocks the Known Follow-up recorded by TASK-071 and
                   TASK-072)
Primary Agent:     review (documentation consistency — TASK_TYPES.md §2 sets
                   DOCUMENTATION's Primary Agent to the Review Agent)
Supporting Agents: backend (API_CONTRACTS.md §3 battle-start response),
                   persistence (REDIS_STATE.md §2 serialization contract)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency, backend/api-contract-validation
Dependencies:      TASK-072 (DONE — owns the REST collection value set in
                   API_CONTRACTS.md §5.1; this task must not contradict or
                   edit it), TASK-071 (DONE — records this divergence as its
                   Known Follow-up). Blocks: the battle-start/Redis element
                   convergence implementation task (BUG/DOCUMENTATION, new
                   task ID assigned by task intake later — do NOT create it
                   from this task).
```

**Status note (resolved).** `Status: BLOCKED` was set at creation because the
D2/D3 fork was believed to require a human decision. Execution resolved all
three items as **DERIVED** from the authoritative documents (§Decision Table),
so no human input was needed and the block was discharged by the documents
themselves rather than by a guess. The task therefore runs
`BLOCKED → IN PROGRESS → IN REVIEW → DONE` and moves to `completed/`.

---

## Objective

Obtain explicit outcomes for D1–D3 (§Decisions Required) and record the
resulting Element serialized-value contract **once**, in its canonical
owner (`documentation/documentation-change.md` §1–§3), covering both
client-visible `POST /api/battle/start` `initialState` and the Redis
`battle:{battleId}:state` record — so the later convergence implementation
task builds against a documented contract instead of the incidental
`Element.ToString()` output. This task decides and documents; it changes no
code, no game rules, and no architecture. The value must not be chosen
because the code already emits it or because one branch costs less work
(requester instruction at creation; `AGENTS.md` §4).

---

## Exact Contract Ambiguity (verified at creation — 2026-09-28)

```text
GAME_STATE.md stated purpose (L29-32)
                             "This document answers: 'What state exists during a
                             running battle?' It does not define what each value
                             means … or how it is serialized/stored (see
                             REDIS_STATE.md, DATABASE.md)." — GAME_STATE defines
                             state composition, NOT serialization; it points at
                             REDIS_STATE.md/DATABASE.md for encoding.
API_CONTRACTS.md §3 (L527, L539-540)
                             initialState = "BattleState summary,
                             GAME_STATE.md §2" — no member-level types and no
                             Element value set; §3 binds nothing below the
                             placeholder.
API_CONTRACTS.md §5.1 (L668, L677)
                             the ONLY Element value-set binding anywhere:
                             element → "Fire" | "Water" | "Earth" | "Wood" |
                             "Metal"; "wire payloads always carry the English
                             form bound above" — written inside §5.1 (collection
                             responses); the v1.14 changelog scopes its entry to
                             "§5.1 element wire value set". Whether that
                             sentence governs ALL wire payloads or only
                             §5.1/§5.2 responses is unreadable as-is — this is
                             D2's ambiguity.
GAME_STATE.md §2.3 (L890)    PetState member `Element` — name only.
GAME_STATE.md §2.4 (L1072)   BossState member `Element` — name only.
GAME_STATE.md §2.4.1 (L1106-1107)
                             "Implement now: BossId, Element, …" — no
                             representation, value set, or serialization rule
                             for `Element` anywhere in GAME_STATE.md (verified
                             by search of all `Element` occurrences).
REDIS_STATE.md §2 (L41-43)   BattleState "is serialized as JSON (matches the
                             shape in GAME_STATE.md §2 exactly — no additional
                             Redis-only fields …)" — speaks of shape/fields
                             only; Element's JSON encoding is unstated (REDIS_STATE
                             is the document GAME_STATE L31 points to for
                             serialization).
DATABASE.md (L409-411)       Postgres-scoped precedent: "the storage encoding
                             of `Element` is an implementation detail (header,
                             §5 item 1)" — states encoding freedom for the
                             BossDefinition column; analogical support for
                             Redis-side freedom (Branch B), NOT a rule for
                             Redis JSON or REST payloads.
SIGNALR_PROTOCOL.md          NOT exposed: §3.2.1 event set carries no Element;
                             §3.2.4 enum table binds only GemType/SpecialGemType/
                             SpecialGemOrientation; §4.2 playerState =
                             combo/matchCount; §4.3 petState = Passive trio;
                             L1117 assigns `Element` to the collection API.
                             → no SignalR decision, no protocol change.
GAME_EVENTS.md (L240)        BattleStarted payload = "BattleId, PetId, BossId,
                             initial BattleState summary" — no Element spelled
                             out; event not in SIGNALR §3.2.1's wire set.
Design docs                  ELEMENT_RULES.md §1: design vocabulary
                             "Mộc (Wood), Hỏa (Fire), Thổ (Earth), Kim (Metal),
                             Thủy (Water)"; accented names appear as display
                             text (PET_RULES.md §8 "Xích Lang Hỏa";
                             BOSS_RULES.md "Hỏa Long Hỏa"). Design text, never
                             wire.
Code (evidence only — NOT a source of truth)
                             BattleStartStateSummary.cs L92, L113:
                             Element.ToString() → "Hoa"; its comment L90-91
                             claims "the documented contract name" citing
                             PET_RULES.md §1 / ELEMENT_RULES.md §6 — neither
                             defines a wire value, and "Hoa" matches neither
                             the design text "Hỏa" nor §5.1's "Fire".
                             BattleStateSerializer.cs L255-257, L280:
                             Element.ToString() → "Hoa" (comment: "Enum-valued
                             members are written by name").
                             ElementWireValues.ToWireValue
                             (Application/Collection, from TASK-072) maps
                             Moc→Wood … Hoa→Fire with closed-set tests
                             (ElementWireValuesTests.cs) — used only by the
                             collection query path; Application-layer, so the
                             Domain serializer cannot reference it (layering is
                             the follow-up task's concern, not this one's).
                             GameRuntime.test.ts L751 'element: "Hoa"' sits in
                             a deliberately over-sized petState payload asserted
                             to be DROPPED (L735-792: keys ==
                             ['passiveId','passiveProgress']) — a stripping
                             fixture, not a wire dependency.
Domain enum                  Element = Moc | Tho | Thuy | Hoa | Kim —
                             .ToString() emits the unaccented
                             Vietnamese-derived names.
```

Two materially different implementations are consistent with these
documents (`AGENTS.md` §20, ambiguous requirement):

```text
Branch A — one value set everywhere: the English set is bound for both the
  REST initialState and the Redis record; the convergence task later
  changes BattleStartStateSummary.cs L92/L113 AND
  BattleStateSerializer.cs L257/L280.
Branch B — wire vs storage split: the English set binds the client-facing
  REST payload (§5.1 L677 read as the general wire rule), while the Redis
  record's Element encoding stays free as DATABASE.md L410 does for
  Postgres; the convergence task later changes BattleStartStateSummary.cs
  only.
```

Both branches have citations; neither may be selected for convenience.

---

## Decisions Required (the task's output)

One explicit outcome per item — **DERIVED** (recorded with file + section
citation, no human input needed) or **HUMAN** (asked, decided, dated) per
`documentation/documentation-change.md` and the TASK-070 §3 precedent;
write each decided outcome **once** into the canonical owner:

### Decision Table (EXECUTED — all three DERIVED)

```text
D1  DERIVED — canonical ownership is split by surface, and the split is
    forced by the stated purposes themselves; the value set stays single.
    · REST request/response bodies (incl. §3 initialState) → API_CONTRACTS.md
      (stated purpose: "How does the client communicate with the server over
      REST?"; it already owns §5.1's set and §3's response shape)
    · the battle:{battleId}:state record → REDIS_STATE.md
      (stated purpose: "How is active battle state represented in Redis?";
      GAME_STATE.md L29-32 explicitly disclaims serialization and NAMES this
      document as the place that answers it)
    · GAME_STATE.md keeps §2's composition (member names, existence, meaning)
      and gains nothing — its stated purpose argues against it, exactly as the
      §Scope note predicted.
    · No structural ambiguity: the three purposes are disjoint. API_CONTRACTS
      answers what a REST body carries; REDIS_STATE answers how the Redis
      record is represented; GAME_STATE answers what state exists. Two owners
      for two surfaces is not two owners for one concept — the CONCEPT that
      must have one owner is the *value set*, and it has one: §5.1.
    Citation: documentation-change.md §3 (stated-purpose test); the stated
    purposes at API_CONTRACTS.md L79-83, GAME_STATE.md L29-32,
    REDIS_STATE.md L24-26.

D2  DERIVED — YES: initialState.petState.element / bossState.element carry
    "Fire" | "Water" | "Earth" | "Wood" | "Metal".
    The binding chain, all authoritative:
    (a) §3's initialState is "a summary of the full BattleState" returned in a
        REST response body → it is a wire payload
        (API_CONTRACTS.md §3 L535-542, L522-529).
    (b) §5.1 states the rule as "wire payloads always carry the English form
        bound above" (L675-678 pre-edit) — a statement about wire payloads,
        not about §5 responses only; the v1.14 changelog's phrase "§5.1
        element wire value set" names WHERE the set is defined, not which
        endpoints it reaches.
    (c) The bounds come from ELEMENT_RULES.md §1's English glosses (Wood,
        Fire, Earth, Metal, Water) — the design document's own bilingual
        pairing, so the wire form is the design document's, not an invented
        one.
    (d) Independent bar on the current output: BOSS_RULES.md §6.4 makes
        client-visible technical identities ASCII, machine-readable, and
        "never a display name" / "no Vietnamese diacritics" (L266-269,
        L279-286). "Hoa" is neither the design text ("Hỏa") nor the English
        gloss ("Fire") — it is the C# enum member name, which no document
        anywhere binds. Searched all of docs/: zero occurrences of "Hoa" as an
        Element value.
    Recorded in: API_CONTRACTS.md §3 (new paragraph) + §5.1 (scope statement).

D3  DERIVED — BRANCH B, and on a firmer ground than DATABASE.md's analogy:
    the Redis record's Element encoding is UNBOUND. It is NOT required to use
    the §5.1 wire set, and the convergence task therefore does NOT have to
    change BattleStateSerializer.cs.
    The chain:
    (a) REDIS_STATE.md §2 item 1 binds the record to GAME_STATE.md §2's SHAPE
        ("matches the shape … exactly — no additional Redis-only fields").
    (b) GAME_STATE.md §2 declares Element as a MEMBER NAME ONLY — §2.3 L890
        and §2.4 L1072 list it; searching every Element occurrence in that
        file yields no value set, no representation, and no serialization
        rule. There is nothing in §2 for §2 item 1 to match a spelling to.
    Thus the shape obligation binds members and structure, not an encoding —
    which is precisely what "no additional Redis-only fields" says.
    (c) GAME_STATE.md §2.1.7 item 4 (L618-620) already draws the repository's
        line for exactly this kind of question: a value's "existence and
        meaning" are owned by the state document while "the exact JSON member
        names and casing are an implementation detail of the serializer". §2.1.10
        item 10 (L797-799) repeats the rule. REDIS_STATE.md §6 item 1 declines
        to specify a field-by-field JSON schema for the same reason.
    (d) §7 item 9 + GAME_STATE.md §2.1.7 item 5 impose ROUND-TRIP
        LOSSLESSNESS — the serializer must return an EQUAL BattleState. That
        obligation is internal to the round trip: it requires this record's own
        reader to recover what its writer wrote. It never compares the Redis
        spelling to the REST spelling, and no document makes the two
        representations one value.
    (e) DATABASE.md L409-411 ("the storage encoding of Element is an
        implementation detail") is consistent as a storage-side precedent, but
        it is not the authority here and is not relied on: REDIS_STATE.md owns
        this record, and it is silent on a value set — silence in the document
        GAME_STATE.md names as the serialization owner is what makes the
        encoding free.
    Counter-check (why Branch A is NOT compelled): nothing binds Redis to
    §5.1. REDIS_STATE.md contains no "English", no wire-value rule, and no
    Element binding (searched). API_CONTRACTS.md owns REST bodies and, post-
    edit, explicitly declines to bind Redis. So Branch A would require
    inventing a cross-surface constraint no document states.
    Recorded in: REDIS_STATE.md §2 (new Element-encoding subsection).

SignalR: NOT a decision — confirmed absent. §3.2.1's event set carries no
Element; §3.2.4 binds only GemType/SpecialGemType/SpecialGemOrientation;
§4.2 playerState = combo/matchCount; §4.3 petState = the Passive trio, whose
item 2 (L1114-1120) names `Element` among the §2.3 members explicitly NOT
delivered. SIGNALR_PROTOCOL.md is untouched (L1117 remains accurate).
```

### Outcome summary

```text
D1  DERIVED — split ownership by surface (API_CONTRACTS.md for REST;
               REDIS_STATE.md for the Redis record; GAME_STATE.md unchanged);
               the Element value set has exactly one owner, §5.1.
D2  DERIVED — battle-start initialState element members = the §5.1 English set.
D3  DERIVED — Branch B: the Redis record's Element encoding is unbound and
               intentionally independent of the REST wire set.
No item required human input. No STOP condition fired.
```

---

## Authoritative References

- `.ai/README.md` §6 (concept-owner map: REST API → `API_CONTRACTS.md`;
  active battle state → `GAME_STATE.md`/`REDIS_STATE.md`)
- `.ai/workflow/documentation/documentation-change.md` §1–§3 (flow,
  no-duplication, canonical-owner test)
- `docs/AGENTS.md` §4 (conflict → propose, STOP), §7, §16 (no inline code
  fixes), §17 (documentation change rule), §18 (ADR rule), §20 (stop
  conditions)
- `docs/02-technical/API_CONTRACTS.md` §3, §5.1 (+ v1.14 changelog),
  `GAME_STATE.md` stated purpose L29-32 + §2.3/§2.4/§2.4.1,
  `REDIS_STATE.md` §2
- `docs/01-game-design/ELEMENT_RULES.md` §1 (value-set source),
  `BOSS_RULES.md` §6.4 (ASCII technical-identity precedent)
- `tasks/TASK_TYPES.md` §2, §4; `tasks/README.md` §3 (ID rule);
  `tasks/TASK_LIFECYCLE.md` §4 (folder rule)
- Immutable: TASK-069, TASK-070, TASK-071, TASK-072 — corrections ride on
  new tasks, never on those

---

## Scope

### In Scope

- Record D1–D3 outcomes; edit **only** the canonical owner selected by D1
  (plus dependent references whose wording goes stale, plus that doc's
  version header/changelog entry citing TASK-073 — precedent: API_CONTRACTS
  v1.14/TASK-072)
- Candidate edit targets (whichever D1 selects): `API_CONTRACTS.md`
  (§3, possibly §5.1 scope wording + changelog), `REDIS_STATE.md` (§2 +
  version header if the document is versioned), `GAME_STATE.md` only if D1
  genuinely requires it (its stated purpose argues against)
- This file: decision table + Completion Evidence

### Out of Scope

- Any change to `src/` or `tests/` — notably `BattleStartStateSummary.cs`
  L92/L113, `BattleStateSerializer.cs` L257/L280, and the
  `GameRuntime.test.ts` L751 stripping fixture (asserts the member is
  dropped; its value is irrelevant)
- Any change to `docs/01-game-design/*` (design names remain display text)
- `SIGNALR_PROTOCOL.md` (no Element on the wire) and `GAME_EVENTS.md`
  (touch only if D1 makes its L240 wording stale — wording repair only)
- New/changed ADRs (the ADR index has no serialization or localization ADR;
  TASK-070/TASK-072 recorded same-class decisions in owning docs)
- Creating the convergence implementation task (its ID is assigned by task
  intake later); modifying TASK-069/070/071/072; any architecture change

---

## Acceptance Criteria

- [x] D1, D2, D3 each carry an explicit outcome in §Decisions Required —
      HUMAN (with date) or DERIVED (with file + section citation); no item
      unanswered and none answered by implementation convenience
- [x] The Element value set is defined exactly once repo-wide
      (`documentation-change.md` §2): no second binding introduced; §5.1's
      existing binding either remains the single definition referenced by
      the state-side statements, or is precisely scoped per D1 without
      duplication
- [x] Canonical owner's content updated per D1; dependent references
      touched only where their wording went stale; owner doc's version
      header bumped with a changelog entry citing TASK-073 (if it has one)
- [x] Zero changes outside the In-Scope doc set; nothing changed in
      `src/`, `tests/`, `docs/01-game-design/*`,
      `SIGNALR_PROTOCOL.md`; TASK-069–072 untouched
- [x] SignalR non-exposure recorded as a scope confirmation (no SignalR
      surface added)
- [x] The convergence implementation task NOT created from this task
- [x] `documentation-change.md` §1 final consistency step passes (owner and
      referencing docs re-read together; no duplicate definition, no stale
      reference); `quality/review.md` §1 checklist passes (code-only items
      skipped per `documentation-change.md` §4)
- [x] `AGENTS.md` §8 (MVP scope unchanged) and §10 (no client-authoritative
      logic affected — no code change) verified

---

## Affected Files & Areas

```text
[ ] src/backend/            — none (convergence is the follow-up task's work)
[ ] src/frontend/client/    — none (GameRuntime.test.ts L751 is a stripping fixture)
[ ] tests/                  — none (documentation-validation depth,
                              core/validation.md §2)
[x] docs/                   — per D1: API_CONTRACTS.md and/or
                              REDIS_STATE.md (and GAME_STATE.md only if D1
                              requires) + owner's version header/changelog
[x] tasks/backlog/TASK-073-… — decision table + Completion Evidence
```

---

## Implementation Notes

- Follow `documentation-change.md` §1: determine the owner (§3
  stated-purpose test) → read everything referencing the concept → check
  for conflicts (a found conflict is a `core/context-discovery.md` §3 STOP,
  not an edit) → edit the owner only → repair stale references → re-read
  together to validate.
- Stated-purpose inputs for D1, verbatim: `API_CONTRACTS.md` answers what
  each endpoint requests/returns; `GAME_STATE.md` answers what state exists
  and **disclaims serialization** (L29-32, naming `REDIS_STATE.md`);
  `REDIS_STATE.md` owns the active record's serialization. If two stated
  purposes plausibly cover the same statement, that is a structural
  ambiguity (`documentation-change.md` §3) → HUMAN, do not guess.
- Keep it small: one value-set contract + its recording. Do not restructure
  §5 beyond what D1/D2 force.
- `ElementWireValuesTests.cs` L56-57 quotes §5.1's "wire payloads always
  carry…" as its rationale — if that sentence's wording changes, record the
  test-comment reference as a note for the follow-up task (report only; do
  not edit tests).

---

## Testing Requirements

### Required Verification

```text
[ ] Documentation validation — re-read owner + referencing docs together;
                          single definition, no stale references
                          (documentation-change.md §1 final step)
[ ] Final review          — quality/review.md §1 checklist (code-only items
                          skipped per documentation-change.md §4)
[x] Unit tests            — N/A (no code change)
[x] Integration tests     — N/A (no code change; core/validation.md §2,
                          documentation-validation depth)
```

### Key Edge Cases

- Bound values must match `ELEMENT_RULES.md` §1 English glosses exactly
  (`Fire`, `Water`, `Earth`, `Wood`, `Metal`) — no slugs, no diacritics.
- The accented design names (Hỏa, Thủy, Thổ, Mộc, Kim) must never become a
  bound value anywhere (`BOSS_RULES.md` §6.4).
- The Domain enum member names (`Hoa`, `Thuy`, …) must not silently become
  the bound set via D3 — whatever D3 decides, it decides explicitly.

---

## Stop Conditions

- A document found that already defines state-side Element serialization →
  STOP per `AGENTS.md` §4 (conflict or resolved premise — report it; do
  not edit past it)
- Resolution appears to require a new/changed ADR, or an architecture,
  battle-state-model, or persistence-strategy decision → STOP per
  `AGENTS.md` §18 (expected: none — DATABASE.md L410 + TASK-070/TASK-072
  same-class precedents)
- Any temptation to fix code or tests inline → STOP per `AGENTS.md` §16;
  record as the follow-up task
- Any required edit under `docs/01-game-design/*` → STOP per `AGENTS.md` §7`
- Skill budget exceeded (>7) or multiple uncoupled boundaries → STOP &
  decompose
- Human unavailable for a HUMAN item → report blocked
  (`core/completion.md` §3); never default to the implementation's current
  output

---

## Completion Evidence

### Decisions Recorded

```text
D1  DERIVED — split, surface-scoped ownership; the value set keeps one owner.
    REST bodies (incl. §3 initialState) → API_CONTRACTS.md
    battle:{battleId}:state record     → REDIS_STATE.md
    GAME_STATE.md                      → unchanged (composition only)
    Citation: documentation-change.md §3 stated-purpose test; stated purposes
    at API_CONTRACTS.md L79-83, GAME_STATE.md L29-32, REDIS_STATE.md L24-26.

D2  DERIVED — initialState.petState.element / bossState.element carry
    "Fire" | "Water" | "Earth" | "Wood" | "Metal".
    Citation: API_CONTRACTS.md §3 (initialState is a REST response body,
    L522-542) + §5.1 (the wire value set, L687-688) + ELEMENT_RULES.md §1
    (the English glosses) + BOSS_RULES.md §6.4 (ASCII technical identities,
    barring the enum member name).

D3  DERIVED — BRANCH B. The Redis record's Element encoding is UNBOUND and
    intentionally independent of the REST wire set; the convergence task does
    NOT change BattleStateSerializer.cs.
    Citation: REDIS_STATE.md §2 item 1 (shape obligation) + §2 new Element
    subsection; GAME_STATE.md §2.3 L890 / §2.4 L1072 (member name only) and
    §2.1.7 item 4 L618-620 + §2.1.10 item 10 L797-799 (exact JSON names and
    casing are the serializer's implementation detail) + §2.1.7 item 5
    L621-628 (round-trip is internal); REDIS_STATE.md §6 item 1 (no
    field-by-field schema restated) and §7 item 9 (round-trip obligation).

No item required human input. No STOP condition fired.
```

### Changed Files

- `docs/02-technical/API_CONTRACTS.md` — version 1.14 → **1.15** (changelog
  entry citing TASK-073):
  - **§5.1**: the former unqualified sentence ("wire payloads always carry
    the English form bound above") replaced by an explicit **scope statement**
    (the set is the Element wire set for this document's whole REST surface,
    including §3's `initialState`, and is defined *here once*), plus an
    explicit statement that it does **not** bind the Redis record's encoding,
    which `REDIS_STATE.md` §2 owns. The binding table (L687-688) is
    unchanged — the set's values did not change.
  - **§3**: new paragraph binding `initialState.petState.element` /
    `bossState.element` to the §5.1 set **by reference** (no value restated).
- `docs/02-technical/REDIS_STATE.md` — version 1.6 → **1.7** (changelog entry
  citing TASK-073):
  - **§2**: new Element-encoding subsection stating the record's
    `PetState.Element` / `BossState.Element` spelling is unbound, with the
    five supporting points (shape obligation binds members not spelling;
    `GAME_STATE.md` §2.1.7 item 4 already draws this line; round-trip is
    internal; the value set is not restated here, and accented design names /
    wrong-surface presentation are barred; a future binding is a change to
    this document). §2 items 1–2 are untouched, so "matches the shape …
    exactly" remains verbatim.
- `tasks/backlog/TASK-073-resolve-battle-state-element-value-set.md` —
  Status `BLOCKED` → **`DONE`** with the resolved status note; the
  §Decisions Required candidates replaced by the executed Decision Table;
  acceptance criteria ticked; this Completion Evidence.

**Not changed (verified):** `src/` (zero files), `tests/` (zero files),
`docs/01-game-design/*` (zero files), `SIGNALR_PROTOCOL.md`,
`GAME_STATE.md`, `DATABASE.md`, `GAME_EVENTS.md`, `ARCHITECTURE.md`,
`BattleStartStateSummary.cs`, `BattleStateSerializer.cs`,
`GameRuntime.test.ts`, and TASK-069/070/071/072.

### Validation Results

```text
[x] D1 explicit + citation          — DERIVED, documentation-change.md §3
[x] D2 explicit + citation          — DERIVED, API_CONTRACTS §3 + §5.1
[x] D3 explicit + citation          — DERIVED (Branch B), REDIS_STATE §2 +
                                      GAME_STATE §2.1.7 item 4 / §2.1.7 item 5
[x] No duplicate Element value-set definition
[x] No stale documentation references
[x] No Vietnamese accented Element names used as technical wire values
[x] No accidental "Hoa" contract introduced
[x] TASK-069/070/071/072 untouched
[x] No src/ changes
[x] No tests/ changes
```

- **Documentation consistency re-read** (`documentation-change.md` §1 final
  step) — **PASS.** `API_CONTRACTS.md` §3 + §5.1 and `REDIS_STATE.md` §2
  re-read together with `GAME_STATE.md` L29-32/§2.1.7/§2.3/§2.4 and
  `ELEMENT_RULES.md` §1. Single definition confirmed: `grep` for the value set
  across all of `docs/` returns §5.1's binding (the definition), the v1.14
  changelog line (immutable history of TASK-072), and `ELEMENT_RULES.md` §1's
  design glosses (the source of the names, correctly not a wire binding).
  The two documents are mutually consistent and non-overlapping: API_CONTRACTS
  scopes to REST and defers Redis; REDIS_STATE accepts that ownership and
  states its encoding is free.
- **No stale references** — `grep` confirms the replaced §5.1 sentence occurs
  nowhere else in `docs/` (its only other occurrence is my own v1.15 changelog
  line *quoting the superseded wording*, which is why it appears). No section
  numbers moved; `ElementWireValuesTests.cs` L56-57 quotes the old sentence as
  a test *comment* rationale, which is now superseded — recorded as a note for
  the convergence task below (not edited; this task changes no tests).
- **No accented wire values** — `grep` of `docs/` shows the accented names
  only as design display text in `ELEMENT_RULES.md` §1 and in the existing
  §5.1 display-values paragraph.
- **No `"Hoa"` contract** — `grep` of all of `docs/` for `"Hoa"` returns
  **zero** matches. The enum member name is bound by no document.
- **SignalR non-exposure** — reconfirmed: `SIGNALR_PROTOCOL.md` §3.2.1/§3.2.4/
  §4.2/§4.3 carry no Element, and §4.3 item 2 (L1114-1120) names `Element`
  among the §2.3 members NOT delivered. `SIGNALR_PROTOCOL.md` untouched.
- **`quality/review.md` §1 checklist** (code-only items skipped per
  `documentation-change.md` §4) — **PASS**: Correctness (matches the
  authoritative stated purposes and the cited sections); Architecture (no
  layering/ADR impact — no ADR exists for serialization and none is needed;
  confirmed against `docs/03-decisions/README.md`'s index); Scope (only the
  two In-Scope owner docs + this task file); Tests (N/A — no code change,
  `core/validation.md` §2 documentation depth); Documentation (updated, and
  versioned headers bumped with TASK-073 citations); Security (no auth or data
  exposure surface touched); Performance (none); Maintainability (no
  abstraction introduced, no duplication); Determinism (no gameplay or RNG
  behaviour touched).
- **`AGENTS.md` §8 / §10** — **PASS**: MVP scope unchanged; no code exists to
  become client-authoritative.
- **`core/validation.md` §2 depth** — DOCUMENTATION / MEDIUM: documentation
  validation + review applied; unit/integration layers correctly N/A.

### Status & Transition

```text
BLOCKED → IN PROGRESS → IN REVIEW → DONE
tasks/backlog/ → tasks/completed/TASK-073-resolve-battle-state-element-value-set.md
```

The `BLOCKED` status was set at creation on the premise that D2/D3 required a
human decision. Execution established that all three items are **DERIVED**
from the authoritative documents, so the block was discharged by the documents
themselves — no human input was needed, no guessed answer was encoded, and no
STOP condition fired. Per `tasks/TASK_LIFECYCLE.md` §2's allowed transitions,
a task whose blocking condition is resolved proceeds to IN PROGRESS; the
lifecycle's BLOCKED → IN PROGRESS edge is the one used here.

### Note for the convergence implementation task (reported, not done here)

1. **The convergence task is now fully unblocked and materially smaller than
   the fork assumed.** D2 requires changing **only**
   `BattleStartStateSummary.cs` (L92, L113) to emit the §5.1 English set. D3
   is Branch B, so `BattleStateSerializer.cs` (L257, L280) **keeps
   `Element.ToString()`** — it is not a defect, it is an unbound storage
   encoding. Do not "fix" it.
2. **Layering.** `ElementWireValues` lives in `GameServer.Application`
   (`Collection/`), so the Domain serializer cannot reference it (and, per D3,
   must not). `BattleStartStateSummary.cs` is in `GameServer.Api` and *can*
   reference it — the convergence task should reuse that single projection
   rather than adding a second element mapping.
3. **Test-comment note (report only — this task changes no tests).**
   `ElementWireValuesTests.cs` L56-57 quotes the old §5.1 sentence ("wire
   payloads always carry…") as its rationale comment. The behaviour it asserts
   is unchanged and still correct; only the quoted wording was superseded. The
   convergence task may want to refresh that comment.
4. **Fixture.** `GameRuntime.test.ts` L751's `element: "Hoa"` sits in a
   payload asserted to be **dropped** (L735-792), so it is not a wire
   dependency and does not need to change for D2. Report only.
