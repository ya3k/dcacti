# development/feature.md — Feature Workflow

**Version:** 1.0

> Use for: implementing a new feature/capability that already has a home
> in the documentation (the mechanic, contract, or system exists in
> `docs/`; the task is to build it).

---

# 1. Flow

```text
Feature request
 ↓
Check MVP scope                     (docs/00-overview/MVP_SCOPE.md)
 ↓
core/task-intake.md                    (classify)
 ↓
core/context-discovery.md                 (identify relevant docs, read,
                                            check conflicts)
 ↓
Discover dependencies                        (part of context-discovery.md
                                              §1 "cross-document
                                              dependencies")
 ↓
Define implementation boundary                  (what's in/out of this
                                                 task specifically)
 ↓
core/planning.md
 ↓
core/implementation.md
 ↓
quality/testing.md
 ↓
quality/review.md
 ↓
Check documentation impact                            (core/planning.md
                                                        §3; apply
                                                        documentation/
                                                        documentation-
                                                        change.md if docs
                                                        actually need
                                                        updating)
 ↓
core/completion.md
```

---

# 2. MVP Scope Check (First, Always)

Before any other step: confirm the feature is listed as `IN` in
`docs/00-overview/MVP_SCOPE.md` §1. If it is not there (whether it's listed
as `OUT` or simply absent), this is a scope-violation stop condition —
report it (`.ai/README.md` §13 format) and do not proceed. A feature
appearing in `docs/00-overview/ROADMAP.md`'s future direction section is
not sufficient authorization to implement it (`ROADMAP.md` §2 /
`MVP_SCOPE.md` §3).

---

# 3. Minimum Reading by Feature Category

```text
Gameplay feature    GAME_RULES.md, relevant domain rule doc(s),
                      GAME_STATE.md, GAME_EVENTS.md, COMBAT_RULES.md
                      (if damage is involved)

Backend feature        TDD.md, ARCHITECTURE.md, API_CONTRACTS.md,
                        DATABASE.md, GAME_STATE.md, relevant domain rules

Realtime feature           GAME_EVENTS.md, SIGNALR_PROTOCOL.md,
                            GAME_STATE.md, ARCHITECTURE.md
```

This is `core/context-discovery.md` §2 applied to the specific case of "a
new feature" — use that file's mapping as the source, this is only the
feature-specific emphasis.

---

# 4. If the Feature Requires a Mechanic That Doesn't Exist

If building the feature requires a gameplay mechanic, API shape, or
architectural piece that isn't documented anywhere: this is not a feature
task anymore — it is a design gap. Stop per `core/context-discovery.md` §3
and, if the task explicitly authorizes a design change, hand off to
`development/gameplay-change.md` or `architecture/architecture-change.md`
as appropriate, rather than inventing the missing piece inline.

---

# 5. Composition

This workflow does not redefine testing or review — see
`quality/testing.md` and `quality/review.md`. It does not redefine
completion — see `core/completion.md`.
