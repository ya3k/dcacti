# TASK-055 — Record JWT Session Security Configuration in ADR-015

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
-->

---

## Metadata

```text
Task ID:           TASK-055
Type:              ARCHITECTURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH (P1 — closes ADR-015's explicit open scope that
                   TASK-034 must implement)
Primary Agent:     backend
Supporting Agents: review
Workflow:          architecture/architecture-change.md, gated by
                   architecture/adr-change.md and
                   documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   quality/documentation-consistency,
                   quality/architecture-conformance,
                   quality/scope-validation
Dependencies:      TASK-054 (DONE — ADR-015 D1-D6) + one human decision
                   round (D7-D11 answers supplied 2026-09-27)
Blocks:            TASK-034 (implementation consumes D7-D11); read-only
                   context: TASK-041, TASK-033, TASK-036 (must not be
                   edited by this task)
```

---

## Objective

Record the five human-supplied session security values — signing algorithm,
token issuer, audience, signing-key storage, and key rotation — into
`ADR-015` as decision items D7-D11, replacing ADR-015's "Not decided in this
ADR" section while preserving D1-D6 unchanged, then reconcile `TASK-034`'s
references to that scope. Documentation/decision work only: no code, no
packages, no invented values.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md`
  — the ADR being amended (Decision items 1-6 = D1-D6 stand; the "Not
  decided in this ADR" section is the open scope this task closes)
- `docs/03-decisions/README.md` §2, §3, §5 — ADR rules: why decisions are
  recorded, an ADR never overrides technical docs, open items are registered
  explicitly
- `AGENTS.md` §7 (never invent rules), §17 (documentation change rule),
  §18 (architecture change rule), §20 (stop conditions)
- `docs/02-technical/API_CONTRACTS.md` §2.7 (security requirements), §2.8
  (session wire/behavior contract) — checked for wire impact (none)
- `src/backend/.env.example` — existing secret-handling convention the
  storage decision must stay consistent with
- `tasks/backlog/TASK-034-application-authentication-session-mechanism.md`
  — the implementation consumer; references this scope in five places
- `tasks/completed/TASK-054-resolve-application-session-authentication-mechanism-contract.md`
  — precedent: human decision round recorded as ADR-015 D1-D6

---

## Scope

### In Scope

- Replacing ADR-015's "Not decided in this ADR" section with D7-D11
  (Decision / Rationale / Consequences / Implementation constraints)
- Fixing stale references to that section elsewhere **inside ADR-015**
  (Context sentence, Trade-offs bullet, Related Documents) so no
  cross-reference dangles (`AGENTS.md` §17)
- Reconciling `TASK-034`'s five references to the open scope so it cites
  D7-D11 — Status stays READY
- Creating this task file and its completion evidence

### Out of Scope

- Any `src/` or `tests/` change (implementation is TASK-034)
- `API_CONTRACTS.md` / `SIGNALR_PROTOCOL.md` changes — verified: the five
  values are server-side security configuration, not wire shape (the client
  never validates the token)
- A new ADR — the change is recorded in the ADR that owns the scope
- `TASK-041`, `TASK-033`, `TASK-036` (and all other tasks) — read-only
- OAuth/Discord redesign, refresh tokens, logout/revocation, cookies,
  session storage, JWKS/KMS/automation, gameplay authorization
- Discord credential hygiene — remains `TASK-036` (BLOCKED); D7-D11 cover
  the JWT signing key only
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

`ADR-015` is Accepted (D1-D6) but deliberately leaves algorithm, issuer,
audience, key storage, and rotation undecided (section "Not decided in this
ADR"). No document in `docs/` defines a value for any of them (verified:
only ADR-015's own exclusion statement matches). `TASK-034` is READY and
cites that open scope in its Resolution, In Scope, Acceptance Criteria,
Implementation Notes, and Stop Conditions. No auth code exists in `src/`
yet.

---

## Acceptance Criteria

- [ ] ADR-015 records D7 = HS256; D8 = no `iss` claim (issuer validation
      off); D9 = explicit `aud` = `dcacti-backend` validated for REST and
      BattleHub alike; D10 = signing key from configuration only (host
      environment variable in production, uncommitted `.env` /
      `dotnet user-secrets` in local development, never in a tracked file);
      D11 = manual rotation with `kid` header and current + previous key
      overlap — each with rationale, consequences, and implementation
      constraints
- [ ] ADR-015 D1-D6 are unchanged (diff shows no edit inside items 1-6)
- [ ] No stale "Not decided" cross-reference remains anywhere in ADR-015
- [ ] `TASK-034` cites ADR-015 D7-D11 instead of the open scope, and its
      Status field still reads `READY`
- [ ] `docs/02-technical/API_CONTRACTS.md` and `SIGNALR_PROTOCOL.md`
      unchanged (no wire impact — verified before editing)
- [ ] `git diff` shows changes only in `docs/03-decisions/ADR/ADR-015-*.md`,
      `tasks/backlog/TASK-034-*.md`, and this task file
- [ ] No `src/`, `tests/`, or `appsettings*` content added, changed, or
      removed; no secret value written anywhere
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 /
      ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/   — NO CHANGE (configuration implementation is TASK-034)
[ ] src/frontend/client/ — NO CHANGE
[ ] tests/         — NO CHANGE (documentation/decision task)
[x] docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md
                     — replace "Not decided" section with D7-D11; fix
                       internal cross-references
[ ] docs/03-decisions/README.md — NO CHANGE (index row already accurate)
[x] tasks/backlog/TASK-034-application-authentication-session-mechanism.md
                     — reconcile five references to D7-D11; Status READY
[x] tasks/backlog/TASK-055-*.md — this file
```

---

## Implementation Notes

- The five answers came from the human decision round on 2026-09-27:
  HS256; omit `iss`; `aud` = `dcacti-backend` (exact string chosen by the
  human); env var (prod) + uncommitted `.env`/user-secrets (dev); manual
  rotation with `kid` overlap. Record them verbatim — no rewording that
  changes meaning, no added defaults.
- Scope boundary to state explicitly in ADR-015: D7-D11 cover the JWT
  signing key only and do **not** decide `TASK-036`'s Discord credential
  hygiene (which stays BLOCKED with its own undecided items).
- Do not add a configuration key name, expiry value, or claim name beyond
  what the human supplied — exact config key naming is TASK-034's
  implementation detail.
- `docs/03-decisions/README.md` §7's ADR-015 row summarizes D1-D6
  accurately and does not mention the open scope — no index edit required.
- Precedent for structure: `TASK-054` §13.1 (D1-D6 round) and the ADR it
  produced.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — N/A: no code changed
[ ] Integration tests  — N/A: no code changed
[ ] Documentation      — grep for stale "Not decided" / open-scope wording
                         in ADR-015 and TASK-034; `git diff --stat` limited
                         to the three expected files; D1-D6 region
                         unchanged
```

### Key Edge Cases
- ADR-015 "Trade-offs" bullet and Context paragraph must not still claim
  the five items are undecided
- `TASK-034` must not flip out of READY, and its Revision 1/2 history text
  stays as history (add a Revision 3 instead of rewriting)
- No credential or key value (real or invented) may appear in any file

---

## Stop Conditions

- If any of the five human decisions were missing or ambiguous: STOP per
  `AGENTS.md` §7 — do not supply a default (answers were supplied
  2026-09-27, so this condition did not fire)
- If recording the values required changing D1-D6 or another ADR's decision:
  STOP per `AGENTS.md` §4
- If the work expanded into code, tests, or secret material: STOP per
  `AGENTS.md` §16 / `.ai/README.md` §13 (destructive change)
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose

---

## Completion Evidence

*(Completed by the executing agent at DONE, 2026-09-27.)*

### Changed Files
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md`
  — "Not decided in this ADR" replaced by the D7–D11 section (Decision /
  Rationale / Consequences / Implementation constraints); Context sentence,
  Trade-offs bullet, and Related Documents updated so no cross-reference
  dangles; Decision items 1–6 (D1–D6) unchanged
- `tasks/backlog/TASK-034-application-authentication-session-mechanism.md`
  — five references to the former open scope reconciled to cite D7–D11;
  Status field remains `READY`; Revision 3 appended (Revisions 1–2 kept as
  history)
- `tasks/active/TASK-055-record-jwt-session-security-configuration.md`
  — this file (created BACKLOG → IN PROGRESS → DONE)

### Validation Results
- `git status --porcelain` / `git diff --stat` — PASS: exactly the two
  expected modified files plus this task file; no `src/`, `tests/`,
  `appsettings*`, `API_CONTRACTS.md`, `SIGNALR_PROTOCOL.md`,
  `docs/03-decisions/README.md`, TASK-041, TASK-033, or TASK-036 change
- `git diff` hunk locations on ADR-015 (lines 29, 96, 174, 195) — PASS:
  no hunk inside Decision items 1–6; D1–D6 byte-identical
- Repo-wide grep `Not decided in this ADR` / `not-decided` — PASS: only
  remaining hits are TASK-054's historical completion evidence and this
  task's own objective text
- Tests: **Not run — documentation/decision task** (no code changed, so
  `core/validation.md` §2 depth does not apply)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — no new system;
      security configuration for the in-scope authentication contract)
- [x] Confirmed no secret or key value written anywhere; the human-chosen
      audience string `dcacti-backend` is the only value recorded, verbatim
- [x] Confirmed no credential, session, or Discord exchange implemented
      (TASK-034 / TASK-035 / TASK-036 boundaries)

---

## Revision History

**Revision 1 — task created and executed (BACKLOG → DONE).** Created with
the next sequential ID (highest existing = TASK-054) after the human
supplied the five ADR-015 security decisions in the decision round of
2026-09-27. Execution recorded D7-D11 in ADR-015, reconciled TASK-034, and
verified no other file changed.
