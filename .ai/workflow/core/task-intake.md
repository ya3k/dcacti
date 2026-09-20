# core/task-intake.md — Task Intake

**Version:** 1.0

> Purpose: classify a task before any document is read in depth or any
> code is touched. Intake produces a classification, not an implementation
> decision.

---

# 1. What Intake Determines

```text
Task type        (see §2, aligned with AGENTS.md §6 categories)
Scope              (which domain(s)/module(s) are touched)
Expected output      (code, docs, both, or a decision/ADR)
Affected domain(s)     (per AGENTS.md §12/§13 boundaries)
Risk level               (§3)
Potential dependencies     (other systems the task might ripple into)
```

Do not implement before this classification exists, even informally.

---

# 2. Task Type

```text
GAME DESIGN
TECHNICAL DESIGN
BACKEND
FRONTEND
GAMEPLAY
DATABASE
REALTIME
TESTING
BUG FIX
REFACTOR
DOCUMENTATION
ARCHITECTURE CHANGE
```

(Same categories as `AGENTS.md` §6 / `.ai/README.md` §11 — intake does not
invent a separate taxonomy.) A task may span more than one category; list
all that apply rather than forcing a single label.

## Worked Example

```text
Task: "Implement Match-3 combo system"

Type:     GAMEPLAY (primary), possibly BACKEND (implementation location)
Scope:    Match-3 domain; Combo is cross-referenced by Passive/Relic
           triggers (RELIC_RULES.md §3 OnCombo) — note as a dependency
Output:   Code + tests; docs already define Combo (GAME_RULES.md §5,
           MATCH3_RULES.md) so no doc change expected unless a gap is found
Domain:   Match-3 (primary), Combat (Combo Modifier consumes this value)
Risk:     MEDIUM — gameplay implementation (see §3)
Dependencies: RelicTriggerEngine (OnCombo), DamagePipeline (Combo Modifier
           step), GAME_EVENTS.md ComboChanged event
```

---

# 3. Risk Level

```text
LOW       typo, isolated refactor, simple/isolated UI adjustment
MEDIUM    API change, database query change, gameplay implementation of
           an already-documented mechanic, SignalR change
HIGH      game rule change, combat formula change, battle state model
           change, architecture change, database migration,
           authentication/security change, persistence strategy change
```

Risk level determines validation depth (`core/validation.md` §2) and
whether an ADR is required (`architecture/adr-change.md`). When in doubt
between two levels, classify at the higher one.

---

# 4. Output of Intake

Intake produces a short classification block (type, scope, output, domain,
risk, dependencies) that feeds directly into `core/context-discovery.md`.
It is not a report to the requester — it is working input for the next
workflow step.
