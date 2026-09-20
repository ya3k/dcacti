# skills/gameplay/gameplay-behavior-derivation.md — Skill: Gameplay Behavior Derivation

**Version:** 1.0

> Derive, from the game-design and state/event documents only, what a
> gameplay action must do: preconditions, ordered steps, state transitions,
> event sequence, documented edge cases — and what the docs leave unanswered.

## Purpose

Produce a **documentation-cited expected-behavior record** for a gameplay
action or mechanic (Match-3 resolution, Cascade/Combo, Element matchups,
damage, Passive charge/trigger, Card/Pet Skill cast, Relic triggers, Boss
response). It is the shared answer to "what *should* happen?" for
implementation, bug-fix, gameplay-change, testing, and review.

One capability, several **domain lenses** (§ Procedure step 4). The
procedure is identical for every domain; only the source documents differ.

## When to Use

- Building or changing behavior for a documented mechanic (`development/feature.md`).
- Identifying "expected behavior" for a bug (`development/bug-fix.md` §2).
- Checking whether a requested mechanic already exists as requested
  (`development/gameplay-change.md` §2).
- Deriving test expectations (`quality/testing.md` §2–§3) and judging
  correctness in review (`quality/review.md` "Correctness").

## When Not to Use

- To *design* or *modify* rules (that is `development/gameplay-change.md`
  §3, which requires explicit authorization).
- For non-gameplay contracts (use the API / realtime / persistence skills).
- For state-ownership questions (use `authority-determinism-audit`).

## Inputs

```text
The action or mechanic in question (e.g. "Swap that produces a Cascade",
   "Boss response after damage", "Relic trigger on Combo")
Documentation Context (documentation-discovery output) — or run discovery
Optionally: observed/implemented behavior to compare against (then hand the
   comparison to documentation-consistency)
```

## Prerequisites & Required Context

- `MVP_SCOPE.md` checked (the mechanic is IN) — `scope-validation`.
- All documents routed by `AGENTS.md` §6 for the domain(s) are read.

## Authoritative Sources

Minimum starting set; re-derive from `AGENTS.md` §6 and the current tree.

```text
Always:            GAME_RULES.md (esp. §16 events, §17 resolution order,
                   §18 server authority), GAME_STATE.md, GAME_EVENTS.md
Match-3:           MATCH3_RULES.md
Elements/Combat:   ELEMENT_RULES.md, COMBAT_RULES.md
Passive / Pet:     PASSIVE_RULES.md, PET_RULES.md
Card / Relic:      CARD_RULES.md, RELIC_RULES.md
Boss:              BOSS_RULES.md (+ COMBAT_RULES.md, GAME_EVENTS.md)
Design intent:     GDD.md (context only — the domain rule doc wins)
```

Conflict precedence: specific domain rule > `GAME_RULES.md` > `GDD.md`
(`AGENTS.md` §2). Values and formulas are **read from the owning document at
the time of use and are never copied into the record** — cite them as
`<value per COMBAT_RULES.md §3>`.

## Procedure

1. **Name the action** precisely: which player request or internal trigger
   starts it (Swap, Card Cast, Pet Skill Cast, Boss timing, Passive/Relic
   trigger).
2. **Get the documents.** Use the Documentation Context; if a domain document
   the action touches is missing from it, run `documentation-discovery`.
3. **Place the action in the fixed resolution order.** Locate which steps of
   `GAME_RULES.md` §17 it touches. The order is canonical; domain documents
   may expand steps but never reorder them. Note that the observable event
   order (`GAME_EVENTS.md` §1) follows that order but is not necessarily one
   event per step.
4. **Read through the domain lens.** For each involved domain, extract *what
   the document says* about the aspects below (do not supply missing pieces
   from genre convention):

   | Lens | Look at (document · aspect) |
   | --- | --- |
   | Match-3 | `MATCH3_RULES.md` — board and generation, swap validity, match detection, cascade, special gems, gravity/spawn, resource-generation hooks, determinism |
   | Combat / Element | `COMBAT_RULES.md` — stats, resource generation, damage pipeline **order**, mitigation, crits, healing/shield, status, power; `ELEMENT_RULES.md` — matchup resolution, where element applies |
   | Passive / Pet | `PASSIVE_RULES.md` — charge, alternate triggers, reset, multiple matches in one cascade; `PET_RULES.md` — stats composition, identity |
   | Card / Relic | `CARD_RULES.md` — casting rules, Pet Skill Card; `RELIC_RULES.md` — supported triggers, deterministic trigger order, anti-infinite-chain rule |
   | Boss | `BOSS_RULES.md` — passive, skill, state, server authority |

5. **Map to state and events.** For each step, name the `BattleState` fields
   it reads/writes (`GAME_STATE.md` §2–§3) and the events it emits
   (`GAME_EVENTS.md` §2), by reference — no payload re-definition.
6. **Follow cross-domain dependencies the documents state** (e.g. a value
   produced in one domain consumed by another). Do not add dependencies the
   documents do not state.
7. **Collect documented edge cases** — those the domain documents call out
   explicitly (special notes, anti-loop rules, ordering rules). Do not invent
   edge cases; if you suspect an unspecified case, list it under *Gaps*.
8. **Record gaps and inconsistencies.** Any question the documents cannot
   answer, any two documents that disagree, and any ASSUMPTION-marked area
   goes to the output verbatim as a gap — not resolved.
9. **Apply the ownership question** for each piece of authoritative state in
   the chain (who owns/calculates/validates/broadcasts) and hand it to
   `authority-determinism-audit` if implementation is involved.

## Outputs

```text
Expected-Behavior Record
- Action + trigger
- Domains and documents used (path + section for every claim)
- Preconditions / validation (owning document)
- Ordered steps mapped to GAME_RULES.md §17 (with source per step)
- State transitions: field · before → after (by reference to GAME_STATE.md)
- Event sequence (by reference to GAME_EVENTS.md §1–§2)
- Documented edge cases (with source)
- Gaps / ambiguities (question the docs cannot answer)
- Inconsistencies between documents (both sides + sections)
- Assumption-level areas (unconfirmed)
```

Consumers: `test-scenario-generation`, `authority-determinism-audit`,
`implementation-review`, `documentation-consistency`,
`development/bug-fix.md`, `development/gameplay-change.md`.

## Validation

```text
[ ] Every statement cites document + section
[ ] No value, formula, threshold, or modifier is restated (only referenced)
[ ] Step order agrees with GAME_RULES.md §17 and event order with GAME_EVENTS.md §1
[ ] Every touched domain was read from its own rule document (not only GAME_RULES.md)
[ ] Nothing is included that no document states
[ ] Gaps and conflicts are listed, not smoothed over
```

## Stop Conditions

- A required rule is missing or ambiguous (`AGENTS.md` §7): stop and report
  the exact unanswerable question; do not supply a "reasonable" rule.
- Two rule documents disagree (`AGENTS.md` §4).
- The requested mechanic is not in `MVP_SCOPE.md` §1 (`AGENTS.md` §8).
- The action would require a client-authoritative value (report as defect,
  `authority-determinism-audit`).
- The task actually asks for a rule change without authorization → hand to
  `development/gameplay-change.md`, do not derive a "new expected behavior".

Baseline stop conditions in `.ai/skills/README.md` §6 also apply.

## Common Failure Modes

- Filling gaps with common match-3 / RPG conventions.
- Reading `GAME_RULES.md` or `GDD.md` only and skipping the domain document.
- Copying numbers into the record (turns the record into a second source of truth).
- Re-ordering pipeline steps because it is "cleaner".
- Forgetting the Boss response, Passive/Relic hooks, or Cascade repetition in
  the chain.
- Treating the GDD design intent as a rule that overrides a domain document.
- Ignoring the anti-loop / deterministic-order notes in the Relic and Match-3
  documents.

## Traceability

```text
Used by:    development/feature.md, bug-fix.md, gameplay-change.md,
            refactor.md (domain behavior); core/implementation.md;
            quality/testing.md; quality/review.md
Reads:      GAME_RULES.md, domain rule docs, GAME_STATE.md, GAME_EVENTS.md,
            GDD.md (context), MVP_SCOPE.md
Produces:   Expected-Behavior Record
Depends on: documentation-discovery (only if context not already supplied)
```
