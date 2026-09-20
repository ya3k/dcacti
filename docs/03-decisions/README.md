# Architecture Decision Records (ADR)

**Version:** 1.0
**Status:** Active

> This document answers: **"Why did we make this technical/design
> decision?"** It does not describe how a system works — that belongs to
> `docs/02-technical/`. It does not define game rules — that belongs to
> `docs/01-game-design/`.

---

# 1. Purpose

An ADR records a decision that has **already been made** and is reflected in
the existing documentation (`docs/00-overview/`, `docs/01-game-design/`,
`docs/02-technical/`). It exists so a future developer or AI agent can
understand *why* the current architecture looks the way it does, without
re-deriving it from scratch or re-litigating it by accident.

ADRs are historical records, not design proposals. Writing an ADR does not
create or change a decision — it documents one that the rest of the
documentation already establishes.

---

# 2. When to Create an ADR

Create an ADR when a decision is:

```text
architecturally important
difficult to reverse
likely to be questioned later
important for AI agents implementing tasks
important for future developers
cross-cutting (affects multiple systems/layers)
security-sensitive
state-management-sensitive
infrastructure-sensitive
```

Do NOT create an ADR for a trivial implementation detail, a decision that
is not yet actually made (see §5), or one that duplicates content already
fully owned by a technical document.

---

# 3. Source of Truth

```text
docs/00-overview/
        ↓
docs/01-game-design/
        ↓
docs/02-technical/
        ↓
docs/03-decisions/ADR/
```

**An ADR must never override a game or technical document.** If an ADR
appears to conflict with `GDD.md`, `GAME_RULES.md`, any domain rule
document, `TDD.md`, `ARCHITECTURE.md`, `GAME_STATE.md`, `GAME_EVENTS.md`,
`API_CONTRACTS.md`, `SIGNALR_PROTOCOL.md`, `REDIS_STATE.md`, or
`DATABASE.md`, that is a contradiction to be reported and resolved in the
owning document — not something the ADR silently wins.

---

# 4. ADR Naming Convention

```text
ADR-NNN-short-kebab-case-name.md
```

- `NNN` is a sequential, zero-padded 3-digit number.
- Numbers are never reused, even if an ADR is later deprecated or
  superseded.
- Files live in `docs/03-decisions/ADR/`.

---

# 5. ADR Status Values

```text
Proposed    — decision drafted, not yet confirmed by existing documentation
Accepted    — decision is confirmed and currently in effect
Deprecated  — decision no longer recommended, but no replacement decided
Superseded  — decision replaced by a later ADR (reference the new ADR)
```

`Accepted` is only used when the decision is genuinely established in
`docs/00-overview/`, `docs/01-game-design/`, or `docs/02-technical/` — never
for a decision that is merely implied or still under discussion.

---

# 6. Relationship to Other Documents

```text
GAME_RULES.md / Domain Rules   → WHAT the game rules are
TDD.md / ARCHITECTURE.md /
GAME_STATE.md / GAME_EVENTS.md /
API_CONTRACTS.md /
SIGNALR_PROTOCOL.md /
REDIS_STATE.md / DATABASE.md    → HOW the system works
ADR                              → WHY a given technical/architectural
                                    choice was made
```

An ADR references the documents that motivated it under a
`## Related Documents` section, and must not copy their content. If a
reader wants implementation detail, they follow the reference to the owning
technical document.

---

# 7. ADR Index

| ADR     | Decision                                              | Status   |
| ------- | ------------------------------------------------------ | -------- |
| ADR-001 | Server-authoritative battle resolution                  | Accepted |
| ADR-002 | Modular monolith backend                                 | Accepted |
| ADR-003 | Use React + Vite with Phaser 4 for Discord Activity      | Accepted |
| ADR-004 | SignalR for realtime communication                        | Accepted |
| ADR-005 | Redis for active battle state                              | Accepted |
| ADR-006 | PostgreSQL for persistent data                              | Accepted |
| ADR-007 | Discord SDK integration & server-side auth boundary    | Accepted |
| ADR-008 | Snapshot-based battle reconnection                             | Accepted |

---

# 8. Known Open Items (Not ADRs)

The following are explicitly **not decided** by any current document and
therefore have no ADR. They are tracked here so they are not silently
forgotten, per `AGENTS.md` §2.2:

```text
- Backend runtime (ASP.NET Core / C#) is recorded only as an ASSUMPTION in
  TDD.md §0, not an independently confirmed decision.
- Card ownership persistence model (instance table vs. unlock-flag join
  table) is recorded only as an ASSUMPTION in DATABASE.md §2.
```

When any of these is resolved, add a new ADR (next sequential number) rather
than retroactively editing an existing one.
