# development/gameplay-change.md — Gameplay Change Workflow

**Version:** 1.0
**Priority:** High — this workflow governs changes to documented game
rules themselves, not just their implementation.

> Use for: a task that asks for a gameplay mechanic to behave differently
> than `docs/01-game-design/` currently says, or for a mechanic that
> doesn't exist yet in those docs.

---

# 1. Minimum Reading

```text
GDD.md
GAME_RULES.md
relevant domain rule doc(s) (MATCH3_/ELEMENT_/COMBAT_/PASSIVE_/PET_/
  CARD_/RELIC_/BOSS_RULES.md — whichever the change touches)
GAME_STATE.md
GAME_EVENTS.md
COMBAT_RULES.md (if the change affects damage in any way)
```

---

# 2. First Branch: Does the Mechanic Already Exist as Requested?

```text
If the requested behavior already matches docs/01-game-design/:
  → this is not a gameplay change, it's a feature/bug-fix task —
    hand off to development/feature.md or development/bug-fix.md.

If the requested mechanic does NOT exist in the documentation:
  → STOP.
```

**Do not invent the mechanic during implementation.** This applies even if
the requested mechanic seems small, obviously reasonable, or easy to
infer from adjacent rules. Report per `.ai/README.md` §13's STOP CONDITION
format and wait.

---

# 3. Second Branch: Task Explicitly Authorizes a Design Change

If — and only if — the task explicitly authorizes changing the game
design (not merely implementing it), follow:

```text
Design change
 ↓
Update authoritative game-design documentation
   (the owning domain rule doc per docs/01-game-design/GAME_RULES.md's own
   ownership rules, or GAME_RULES.md itself if the change is a core
   invariant)
 ↓
Review downstream implications
   (does this change ripple into other domain docs? e.g. an Element
   modifier change affects ELEMENT_RULES.md but is consumed by
   COMBAT_RULES.md's damage pipeline)
 ↓
Update technical documentation if required
   (GAME_STATE.md / GAME_EVENTS.md if the change adds/removes state or
   events)
 ↓
Create/update ADR if required
   (only if the change is architectural, per architecture/adr-change.md
   §2 — a balance number change is not architectural)
 ↓
core/planning.md → core/implementation.md
 ↓
quality/testing.md
 ↓
quality/review.md
 ↓
core/completion.md
```

---

# 4. MVP Boundary Applies Throughout

At every point in §3, re-check `docs/00-overview/MVP_SCOPE.md`. A design
change is never itself permission to add an out-of-scope system (PvP,
Gacha, Map mechanics, etc.) — if the "smallest documentation change" to
support a requested mechanic would require crossing into OUT-of-scope
territory, that is itself a reason to stop and report, not a reason to
expand scope quietly.

---

# 5. Composition

Reuses `core/planning.md`, `core/implementation.md`, `quality/testing.md`,
`quality/review.md`, `core/completion.md`, and, when the change is
architectural, `architecture/adr-change.md`. Does not redefine any of
these.
