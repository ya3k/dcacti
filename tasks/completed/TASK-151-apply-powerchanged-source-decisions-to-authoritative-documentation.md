# TASK-151 — Apply the TASK-150 `PowerChanged` Decisions to the Authoritative Documentation

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK IS THE CONTRACT-APPLICATION HALF OF THE DECISION WORKFLOW.

    TASK-150 decision record (D-1 … D-8)
            ↓
    Apply the decisions to the canonical owners
            ↓
    STOP

  It performs the canonical-owner documentation write that
  documentation/documentation-change.md §1 describes. It consumes TASK-150's
  record; it does NOT re-open it, re-interpret it, or add a decision to it.

  BOUNDARY: documentation only. Zero files under src/ or tests/.
  Zero changes to TASK-150 or any completed task.

  THIS TASK DOES NOT IMPLEMENT R-1. TASK-150 recorded that the current
  CardCastExecutor aggregates multiple Power mutations into a single net write
  and therefore cannot satisfy D-8. That is the downstream IMPLEMENTATION
  task's work. This task records the implication; it changes no code.
-->

---

## Metadata

```text
Task ID:           TASK-151
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md)
Status:            DONE (direct execution recorded: Changed Files + Validation
                   Results in Completion Evidence; all acceptance criteria
                   verified. Lifecycle: BACKLOG → IN PROGRESS → DONE per
                   TASK_LIFECYCLE.md §3.)
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION LOW–MEDIUM; MEDIUM
                   because D-6 extends a CLOSED wire enum value set owned by
                   SIGNALR_PROTOCOL.md §3.2.24 and the change is
                   protocol-visible.)
Priority:          HIGH (the sole blocker between the resolved TASK-150 record
                   and the downstream backend emission task. AGENTS.md §17/§18
                   place the contract change before the implementation.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review")
Supporting Agents: realtime (SIGNALR_PROTOCOL.md §3.2.24 owns the wire member
                   table and the `source` value set),
                   gameplay (GAME_RULES.md §12 owns Power; GAME_EVENTS.md §2
                   owns the event's meaning and payload)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   realtime/realtime-protocol-validation,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-150 (DONE — the decision record D-1 … D-8 this task
                   applies),
                   TASK-133 (DONE — the Relic stage, the existing `"relic"`
                   emitter),
                   TASK-104 (DONE — authored §3.2.20–§3.2.25, including the
                   `PowerChanged` member table this task extends)
```

---

## Objective

Apply the TASK-150 decision record (D-1 through D-8) to the two canonical owners of the `PowerChanged` contract — `GAME_EVENTS.md` §2 and `SIGNALR_PROTOCOL.md` §3.2.24 — so that both documents state the four-value `source` vocabulary (`"match"`, `"card"`, `"relic"`, `"boss"`), the owning-stage emission rule, the per-mutation multiplicity rule, and the authoritative ordering rule, and so that neither document still describes the match and card sources as non-emitting.

This task changes no source code and implements no emission.

---

## Current State

Two documents own the `PowerChanged` contract, and one split exists between them: `GAME_EVENTS.md` §2 owns the event's **meaning and payload**, `SIGNALR_PROTOCOL.md` §3.2.24 owns its **wire shape** (`GAME_EVENTS.md` §3 item 1).

Both currently carry the pre-decision state:

```text
docs/02-technical/GAME_EVENTS.md  (Version 2.10)
  line 379   payload source set: "(Gem match / Card cost / Relic)"  — three values
  item 2     "its three values are owned by this payload line"
  item 2     "a Gem match, a Card cost, or a Relic"
  item 3     "the authoritative report of a Card cast's Power change"
  item 4     "the "match" and "card" sources remain their own stages' and are
             unchanged"                                    ← D-1/D-3 overturn this
  line 12    the version block repeats the same stale clause

docs/02-technical/SIGNALR_PROTOCOL.md  (Version 2.13)
  line 1186  | source | string | always | "match", "card", or "relic" |
  item 1     "a generation is positive and a Card cost spend is negative"
  item 3     projects the three-value set
  item 5     "The "match" and "card" sources remain their own stages' and are
             unchanged by that."                           ← D-1/D-3 overturn this
  line 10    the version block repeats the same stale clause
  §3.2.2 item 5  "PowerChanged by the Power stage (GAME_RULES.md §12)"
```

The five authoritative Power mutation sites TASK-150 inventoried:

```text
#  Site                                   Stage (§17)        source
-  -------------------------------------  -----------------  --------------
1  Match resource generation              Power, step 13     "match"
2  Card / Pet Skill cost deduction        Card, step 14      "card"
3  Card Power Charge gain                 Card, step 14      "card"
4  Relic Power effect                     Relic, step 11     "relic"
5  Boss Drain Power                      Boss, step 18b     "boss"
```

Sites 1–4 are covered by the existing vocabulary. Site 5 requires D-6's
addition. Site 3 requires D-7's widened `"card"` meaning.

---

## Authoritative References

- `AGENTS.md` **§4** — conflict resolution; **§17** — documentation change precedes code; **§18** — architecture/contract change precedes implementation
- `docs/00-overview/MVP_SCOPE.md` §1 — Power ("HP, ATK, DEF, Power, Crit, Status Effects, Damage") is IN scope
- `docs/01-game-design/GAME_RULES.md` **§12** — Power's 0–100 range and its generation sources (this document owns Power; it names no `source` value set and requires no change)
- `docs/01-game-design/GAME_RULES.md` **§17** — the fixed resolution order: step 11 (Relics), step 13 (Update Power), step 14 (Resolve Player Effects), step 18b (Boss Skill)
- `docs/01-game-design/BOSS_RULES.md` **§6.3.1 item 2** — Drain Power's −20 flat Pet Power, instant, no duration
- `docs/01-game-design/CARD_RULES.md` **§2** — Power Charge: cost 0, "Active Pet gains 25 Power"
- `docs/02-technical/GAME_STATE.md` **§2.3.1 item 9** — Drain Power is an immediate `PetState.Power` mutation
- `docs/02-technical/GAME_EVENTS.md` **§3 item 1** — the semantics/wire split this task must preserve
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.2 item 5** — admitting/extending an event's schema is owned by §3.2
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.4** — enum-valued wire members are strings carrying the documented contract name
- `docs/02-technical/ARCHITECTURE.md` §5 — anti-overengineering
- `tasks/backlog/TASK-150-resolve-powerchanged-source-semantics-for-boss-drain-and-card-power-charge.md` — the decision record (D-1 … D-8) this task applies

---

## Scope

### In Scope

- `docs/02-technical/GAME_EVENTS.md` §2 `PowerChanged` — the payload source set (line 379), items 2, 3, and 4, and the version block.
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.24 — the member table's `source` row (line 1186), items 1, 3, and 5, the version block, and the minimum cross-reference in `§3.2.2 item 5` whose "the Power stage" phrasing becomes incomplete once emission is per-owning-stage.
- Stating the D-1/D-3 owning-stage emission rule in place of the stale "remain their own stages' and are unchanged" wording.
- Stating the D-8 per-mutation multiplicity rule and the authoritative ordering rule at contract level.
- Correcting any wording these changes make stale, without duplicating a rule across the two documents (each keeps its own side of the semantics/wire split).

### Out of Scope

- **Any source change.** `CardCastExecutor.cs`, `BattleStateService.cs`, `RelicResolver.cs`, and every other file under `src/` are untouched.
- **Implementing R-1.** TASK-150 recorded that `CardCastExecutor` collapses multiple Power mutations into one net write and therefore cannot satisfy D-8. Correcting that is the downstream implementation task's work; this task records the implication only.
- **Creating the downstream implementation task.**
- **Modifying `GAME_STATE.md`.** No Power state field is added, no `BattleState` change, no Redis change. D-1 … D-8 need no state change — they govern emission of an existing event over existing state.
- **Modifying `GAME_RULES.md`.** §12 owns Power's range and generation sources; it names no `source` value set and no emission owner, so nothing in it is stale.
- **Adding any SignalR event, discriminator value, method, subscription, or payload member.** The payload stays exactly `{ type, delta, power, source }`; D-6 adds a `source` **value**, not a member.
- **Modifying TASK-150**, or any completed task (`TASK_LIFECYCLE.md` §3).
- **Modifying any frontend file.** D-5 requires no client change: the client already presents `PowerChanged` verbatim and forwards `source` as a delivered string without validating it against a client-side enum (TASK-148).
- **Inventing a gameplay rule, a `source` value, or an event.** Every change traces to a TASK-150 decision item.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Implementation Requirements

### `docs/02-technical/GAME_EVENTS.md`

```text
Source vocabulary (payload line 379 + item 2)
  → four values: "match", "card", "relic", "boss"
  → semantics stated per value:
      match = Match-owned Power mutation
      card  = Card-owned Power mutation   (NOT cost-only)
      relic = Relic-owned Power mutation
      boss  = Boss-owned Power mutation

Emission ownership (item 4)
  → REMOVE "the "match" and "card" sources remain their own stages' and are
    unchanged"
  → REPLACE with the D-1/D-3 rule: the stage that owns the authoritative Power
    mutation emits PowerChanged

Multiplicity and ordering (item 4 or a new item)
  → one PowerChanged per authoritative Power mutation
  → events preserve authoritative mutation order
  → phrased at contract level; no method names, no internal variables

Item 3
  → widen so it no longer reads as if a Card cast's Power change is only its
    cost; the Card-side meaning is D-7's

Version block
  → record this revision and drop the stale "Match and Card PowerChanged
    sources remain their own stages'" clause
```

### `docs/02-technical/SIGNALR_PROTOCOL.md`

```text
§3.2.24 member table (line 1186)
  → | source | string | always | "match", "card", "relic", or "boss" |

§3.2.24 item 1 (delta sign)
  → remove the reading that ties the sign to a source KIND ("a Card cost spend
    is negative") and state that delta carries the mutation's actual sign;
    the source identifies the owner, not the direction

§3.2.24 item 3 (value-set projection)
  → project the four-value set; keep the per-event value-set convention and
    the existing warning that a shared member name does not fix a value set

§3.2.24 item 5 (emission)
  → REMOVE "The "match" and "card" sources remain their own stages' and are
    unchanged by that."
  → REPLACE with the owning-stage rule, and define "boss" as a Boss-owned
    Power mutation (Boss Drain Power is the provisioned case)

§3.2.24 (multiplicity)
  → state that each authoritative Power mutation produces one PowerChanged and
    that event order follows authoritative mutation order

§3.2.2 item 5 (cross-reference)
  → its "PowerChanged by the Power stage (GAME_RULES.md §12)" phrasing becomes
    incomplete once emission is per-owning-stage; correct the minimum needed

Version block
  → record this revision and drop the stale clause
```

### Constraints on both

- Add no event, discriminator value, method, subscription, or payload member.
- Preserve the semantics/wire split: `GAME_EVENTS.md` states meaning and payload, `SIGNALR_PROTOCOL.md` states the wire shape. Do not restate one in the other.
- Do not restate `GAME_RULES.md` §17's stage order — reference it.
- Do not restate `BOSS_RULES.md` §6.3.1's Drain Power magnitude or `CARD_RULES.md` §2's Power Charge magnitude — reference them.

---

## Acceptance Criteria

- [x] `GAME_EVENTS.md` §2's `PowerChanged` payload source set names exactly `"match"`, `"card"`, `"relic"`, and `"boss"`
- [x] `GAME_EVENTS.md` §2 item 2 records the four value semantics, with `match`/`card`/`relic`/`boss` each defined by the stage that owns the mutation
- [x] `GAME_EVENTS.md` §2 no longer defines `"card"` as a Card cost only
- [x] `GAME_EVENTS.md` §2 no longer contains the wording "the `"match"` and `"card"` sources remain their own stages'"
- [x] `GAME_EVENTS.md` §2 states the owning-stage emission rule (D-1, D-3)
- [x] `GAME_EVENTS.md` §2 states that one `PowerChanged` is emitted per authoritative Power mutation, in authoritative mutation order (D-8)
- [x] `SIGNALR_PROTOCOL.md` §3.2.24's member table gives `source` as `"match"`, `"card"`, `"relic"`, or `"boss"`
- [x] `SIGNALR_PROTOCOL.md` §3.2.24 item 1 no longer ties `delta`'s sign to a source kind
- [x] `SIGNALR_PROTOCOL.md` §3.2.24 item 3 projects the four-value set
- [x] `SIGNALR_PROTOCOL.md` §3.2.24 defines `"boss"` as a Boss-owned Power mutation, naming Boss Drain Power as the provisioned case
- [x] `SIGNALR_PROTOCOL.md` §3.2.24 no longer contains "The `"match"` and `"card"` sources remain their own stages'"
- [x] `SIGNALR_PROTOCOL.md` §3.2.24 states the per-mutation multiplicity rule and the authoritative ordering rule
- [x] The `PowerChanged` payload remains exactly `{ type, delta, power, source }` in both documents — no member added or removed
- [x] No new SignalR event, discriminator value, method, or subscription is introduced
- [x] All five inventoried Power mutation sites are covered by the four-value vocabulary
- [x] Both documents' version blocks record this revision, and no version block retains the stale non-emitting clause
- [x] The two documents agree on the value set and the emission rule
- [x] No rule is duplicated across the two documents; each keeps its own side of the semantics/wire split
- [x] Zero files under `src/` are modified
- [x] Zero files under `tests/` are modified
- [x] `GAME_STATE.md` and `GAME_RULES.md` are unmodified
- [x] No completed task is modified; TASK-150 is byte-identical
- [x] No new gameplay decision, rule, `source` value, or event is introduced

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api) — NONE
[ ] src/frontend/client/ — NONE
[ ] tests/ — NONE
[x] docs/ (GAME_EVENTS.md §2 `PowerChanged`; SIGNALR_PROTOCOL.md §3.2.24 and
           §3.2.2 item 5; both version blocks)
```

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — N/A. No code is modified.
[x] Integration tests  — N/A. No code is modified.
[x] Gameplay scenarios — N/A. No gameplay behavior is implemented.
```

This task's verification is a documentation-consistency review:

```text
[ ] Every changed statement traces to a TASK-150 decision item (D-1 … D-8)
[ ] No statement contradicts GAME_RULES.md §12 or §17
[ ] No statement contradicts BOSS_RULES.md §6.3.1 item 2 or CARD_RULES.md §2
[ ] The two documents agree on the value set and the emission rule
[ ] No duplicate rule was introduced across the two owners
[ ] Every section reference still resolves
```

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- If TASK-150's decisions are found to conflict with another authoritative document: STOP per `AGENTS.md` §4 — report the conflict, do not pick a side.
- If another `PowerChanged` source value is discovered beyond the four: STOP and report — the value set is a Product Owner decision, not this task's.
- If the existing payload cannot represent D-1 … D-8: STOP and report.
- If D-8 appears to require a new wire field: STOP and report.
- If a new event type appears necessary: STOP and report.
- If `GAME_STATE.md` must change to represent the decision: STOP and report.
- If the work requires a new gameplay decision: STOP and report.
- If implementing R-1 is required to complete this task: STOP — it is the downstream implementation task's work.
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose.

---

## Completion Evidence

### Changed Files
- `docs/02-technical/GAME_EVENTS.md` — §2 `PowerChanged`: payload source set widened to four values; item 2 rewritten to define each value by its owning stage and to stop defining `"card"` as cost-only; item 3 reworded so a Card cast's Power change is no longer framed as its cost; item 4 rewritten to state the owning-stage emission rule (D-1, D-3) and the per-mutation multiplicity and ordering rules (D-8) in place of the stale non-emitting clause; version block advanced to 2.11 recording this revision.
- `docs/02-technical/SIGNALR_PROTOCOL.md` — §3.2.24: member table `source` row widened to four values; item 1 reworded so `delta` carries the mutation's actual sign and the source identifies the owner rather than the direction; item 3 projects the four-value set; item 5 rewritten to state the owning-stage rule and to define `"boss"` (Boss Drain Power), and extended with the per-mutation multiplicity and ordering rules; §3.2.2 item 5's emission cross-reference corrected from "the Power stage" to the owning-stage rule; version block advanced to 2.14 recording this revision.

### Validation Results
- Documentation-consistency review (`quality/documentation-consistency.md`): both owners agree on the four-value set and the owning-stage rule; each keeps its own side of the semantics/wire split; no rule duplicated across the two.
- Traceability: every changed statement maps to a TASK-150 decision item — `"boss"` and Boss ownership to **D-6**; the widened `"card"` meaning to **D-7**; per-mutation multiplicity and ordering to **D-8**; owning-stage emission to **D-1**/**D-3**; the payload member set to **D-2**; no client-side change to **D-5**; no new event to **D-4**.
- Stale-wording scan: zero occurrences of "remain their own stages" remain in either document except the two version blocks, which cite the clause only to mark it superseded. Zero occurrences of the three-value statements `"Gem match / Card cost / Relic"` and `"match", "card", or "relic"` remain.
- Payload integrity: `SIGNALR_PROTOCOL.md` §3.2.24's member table still carries exactly `type`, `delta`, `power`, `source`. The §3.2.2 discriminator still carries the same 16 event values — `PowerChanged` present, none added.
- Five-site coverage: the four-value vocabulary names all five inventoried mutations — `"match"` (step 13), `"card"` (step 14, covering both the cost spend and the Power Charge gain), `"relic"` (step 11), `"boss"` (step 18b).
- Scope: `src/` modified files = 0; `tests/` = 0; `GAME_STATE.md` and `GAME_RULES.md` unmodified; TASK-150 and all completed tasks modified = 0.

### Server Authority & Scope Verification
- [x] Confirmed no source code modified — zero files under `src/` and `src/frontend/`
- [x] Confirmed zero files under `tests/` modified
- [x] Confirmed `GAME_STATE.md` unmodified — no Power state field, no `BattleState` change, no Redis change
- [x] Confirmed `GAME_RULES.md` unmodified — §12 owns Power's range and names no `source` set
- [x] Confirmed no new SignalR event, discriminator value, method, subscription, or payload member
- [x] Confirmed the `PowerChanged` payload remains `{ type, delta, power, source }`
- [x] Confirmed R-1 was NOT implemented — `CardCastExecutor.cs` is unmodified, and the implication is recorded for the downstream implementation task
- [x] Confirmed TASK-150 is byte-identical
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Power is IN)
