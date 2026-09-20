# development/refactor.md — Refactor Workflow

**Version:** 1.0

> Use for: restructuring existing code without an intended behavior
> change.

---

# 1. Default Rule

**Refactoring must preserve behavior unless a behavior change is
explicitly part of the task.** If a task description implies both a
restructure and a behavior change, split it: treat the behavior change
through `development/feature.md`, `development/bug-fix.md`, or
`development/gameplay-change.md` as appropriate, and the refactor
separately.

---

# 2. What Must Be Checked Before and After

```text
existing tests            (must still pass, unchanged in intent)
public contracts             (API_CONTRACTS.md, SIGNALR_PROTOCOL.md —
                              must not shift)
domain behavior                 (the relevant docs/01-game-design/ rule
                                 must still hold)
events                             (GAME_EVENTS.md — same events, same
                                   order, same payload meaning)
state transitions                    (GAME_STATE.md — same shape/semantics)
persistence behavior                    (DATABASE.md — same data, same
                                         meaning)
realtime behavior                          (SIGNALR_PROTOCOL.md — same
                                            wire behavior)
```

If a refactor cannot preserve one of these without also changing it, that
item is a behavior change in disguise — treat it as such (§1), do not
proceed as a pure refactor.

---

# 3. Flow

```text
Task classified as REFACTOR (core/task-intake.md)
 ↓
core/context-discovery.md         (identify the exact behavior/contracts
                                   in §2 that must not move)
 ↓
core/planning.md                    (plan explicitly states: "no behavior
                                     change" and lists what §2 items were
                                     checked)
 ↓
core/implementation.md
 ↓
quality/testing.md                       (existing tests must still pass;
                                          no new behavior asserted)
 ↓
quality/review.md
 ↓
core/completion.md
```

---

# 4. Scope Discipline

Do not expand a refactor into unrelated areas because "while I'm in
there." `core/implementation.md` §2's boundaries apply in full — a
refactor task is exactly the kind of task most tempted to violate them,
so this is called out explicitly here.
