# TASK-036 — Discord Credential Secret Hygiene

---

## Metadata

```text
Task ID:           TASK-036
Type:              ARCHITECTURE
Status:            BLOCKED
Risk:              HIGH
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: review, testing
Workflow:          architecture/architecture-change.md, gated by
                   architecture/adr-change.md and
                   documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   quality/architecture-conformance,
                   quality/documentation-consistency,
                   quality/scope-validation
Dependencies:      None
```

---

## BLOCKED — Remediation Requires a Security / Credential Decision

This task is **BLOCKED**. The credential-handling gap below is verified and
real, but **how to remediate it is not defined by any authoritative document**,
and this task **must not choose a remediation** (`AGENTS.md` §7, §18; `.ai/README.md`
§13).

Specifically, no document defines:

```text
[ ] Whether the exposed credential must be rotated, and by whom. NOT DECIDED.
[ ] The developer-secrets convention for local backend configuration —
    user-secrets, environment variables, a gitignored file, or another
    mechanism. NOT DECIDED.
[ ] Whether the tracked appsettings.json placeholder shape is the intended
    convention or should change. NOT DECIDED.
[ ] Whether startup should fail loudly when a required Discord credential is
    absent, rather than silently proceeding. NOT DECIDED.
[ ] Whether the credential needs to be present at all before TASK-035
    defines the exchange contract that would consume it. NOT DECIDED.
```

Do **not** invent a credential-rotation procedure, a secrets-management
mechanism, or a configuration-loading strategy. Creating the decision is the
first step after a human supplies it.

---

## Verified Finding

The finding is reproduced here **without the credential value**, per this task's
own rule and `ADR-007` item 3.

### What was verified

| Check | Result |
|---|---|
| `src/backend/GameServer.Api/appsettings.Development.json` contains a non-empty `Discord:ClientSecret` | **Yes** |
| Value shape | 32 characters, alphanumeric with `-`/`_` — consistent with Discord's Client Secret format |
| File tracked by git? | **No** — `git ls-files --error-unmatch` fails |
| Listed in `.gitignore`? | **Yes** — `.gitignore` line 6, first entry under `# Secrets / local config` |
| Ever committed anywhere in history? | **No** — `git log --all -- '*appsettings.Development.json'` returns nothing |
| Tracked `appsettings.json` `ClientSecret` | **Empty string** (verified against `HEAD`) |
| Tracked `appsettings.json` `ClientId` | Present (a non-secret client id, per `ADR-007` item 3) |

### Accurate characterization

**This is not a repository leak.** The file is correctly gitignored and has
never been committed, so no secret exists in git history. The `ClientSecret`
boundary that `ADR-007` item 3 and `TDD.md` §2.1 item 3 establish is **intact
with respect to version control**.

The remaining issue is narrower but still real:

```text
A real-looking Discord Client Secret is stored in plaintext in the working
tree, in a file whose only protection is a .gitignore entry.
```

`.gitignore` protects against *accidental commit*; it does not protect against
a file-system reader, a backup or sync tool, an IDE index, a shared machine, or
an archive. A gitignore entry is a commit guard, not a secrets-management
mechanism.

### Scope limit, stated honestly

This task verified the **format and tracking status** of the value. It did
**not** and **cannot** verify whether the credential is live, valid, or already
revoked — that requires Discord-side knowledge this repository does not carry.
The value *looks* like a real Discord Client Secret; confirming it is a
human/operator action.

**The credential value must never be reproduced in this task, in any task, in
a commit message, in a log, or in a report.**

---

## Objective

Once the required decisions are recorded, bring local Discord credential
handling into line with `ADR-007` item 3 / `TDD.md` §2.1 item 3's server-side
secret boundary, so that no real credential sits in plaintext in the working
tree and the backend has a documented way to obtain its Discord credentials.

This task owns **credential storage and hygiene only**. It does not own the
OAuth exchange that consumes the credential.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-007-discord-activity-authentication.md` item 3 — the Client Secret security boundary: backend-held via "server-side environment/secret configuration", never exposed via client bundles, environment variables exposed to browsers, React config, Phaser code, or **Git**
- `docs/02-technical/TDD.md` §2.1 item 3 — the same boundary, restated for the backend
- `docs/02-technical/ARCHITECTURE.md` §2.3 item 2 — `Discord Client ID` / `Discord Client Secret` managed "through secure server-side configuration/environment secrets"
- `docs/03-decisions/README.md` §2, §5, §8 — when an ADR is warranted and how an open item is registered
- `.gitignore` — the existing secrets/local-config section
- `tasks/backlog/TASK-035-discord-identity-exchange-contract.md` — the consumer of this credential (BLOCKED)

---

## Scope

### In Scope (once unblocked)

- The documented decision for how the backend obtains its Discord credentials locally and in deployment
- Bringing the working tree's local configuration into line with that decision, without committing any credential
- A documented, credential-free convention for the tracked `appsettings.json` (placeholder shape)
- Verification that no credential value is committed, logged, or returned in any response
- If rotation is decided: rotating the affected Discord credential is an operator action this task **records and verifies**, not one it performs by invention

### Out of Scope

- **The Discord OAuth exchange itself** — owned by TASK-035 (BLOCKED)
- Player persistence and match/create (TASK-023)
- The application session mechanism (TASK-034)
- The `ClientId` — it is a public identifier, not a secret (`ADR-007` item 3)
- Frontend `VITE_DISCORD_CLIENT_ID` handling — the frontend must hold no secret (`ADR-007` item 3)
- Any gameplay system, and any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

```text
src/backend/GameServer.Api/appsettings.json              tracked; ClientSecret is "" (empty)
src/backend/GameServer.Api/appsettings.Development.json  gitignored, untracked; holds a
                                                         non-empty, real-looking ClientSecret
```

No backend code reads either `Discord:ClientId` or `Discord:ClientSecret`
today — there is no Discord OAuth client in `src/`. The credential is currently
inert configuration.

---

## Acceptance Criteria

*(Provisional — each value must reference the authoritative document section
that defines it once the decision is recorded. No mechanism may be chosen by
the implementing agent.)*

- [ ] How the backend obtains its Discord credentials is defined by an approved authoritative document, not by implementation convention
- [ ] No Discord Client Secret value exists in plaintext in a tracked file, in git history, or in any committed artifact
- [ ] The local development configuration follows the documented secrets convention (`<doc §>`)
- [ ] The tracked `appsettings.json` carries no secret, only the documented placeholder shape
- [ ] The Client Secret is never exposed to the frontend, a client bundle, a response body, or a log line (`ADR-007` item 3)
- [ ] If rotation is required by the decision, rotation is recorded as completed by an operator
- [ ] The repository's existing `.gitignore` secrets entries remain effective (not weakened or removed)
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Api/ (appsettings*.json, and startup configuration
                                 binding if the decision requires it)
[ ] src/frontend/client/ (no change expected — the frontend holds no secret)
[x] tests/ (only if the decision introduces a verifiable configuration contract)
[x] docs/ (a new ADR and/or a technical-document note — REQUIRED before
           changing credential handling)
```

---

## ADR Requirement

`ADR-007` item 3 already decides **the boundary** — the Client Secret is
backend-only and never reaches Git, the browser, or client artifacts. That
decision stands and must not be duplicated, rewritten, or superseded.

What is **not** decided is the **mechanism** for satisfying that boundary
locally and in deployment (and whether rotation is required). Per `AGENTS.md`
§18, a secrets-handling mechanism with security consequences needs a recorded
decision before code or configuration changes land.

The next sequential ADR number is **ADR-013** (highest existing is ADR-012).

**This task does not create that ADR.** `docs/03-decisions/README.md` §1 and §5
and `.ai/workflow/architecture/adr-change.md` §2 forbid recording a decision
that has not actually been made — and writing it here would mean inventing the
very mechanism this task is blocked on.

### Numbering note

TASK-035 also expects to claim the next ADR number (ADR-013) for the identity
exchange contract. Whichever decision is approved first takes ADR-013; the other
takes the following number. **Numbers are never reused**
(`docs/03-decisions/README.md` §4) — resolve the ordering when the decisions are
approved, not by guessing now.

---

## Implementation Notes

- **Never reproduce the credential value** in code, comments, tests, task
  files, commit messages, logs, or reports. Refer to it as "the Discord
  `ClientSecret`" and nothing more.
- The `.gitignore` entry is already correct as a *commit guard* and must remain.
  This task is about the plaintext-at-rest gap, not about a missing ignore rule.
- Do not remove, rewrite, or `git rm --cached` anything as part of the
  decomposition that created this task. Changing tracked-file state is a
  remediation action gated on the decision above.
- The tracked `appsettings.json` currently carries an **empty** `ClientSecret`
  string. Whether that empty-string placeholder is the intended convention is
  part of the undecided contract.
- `ADR-007` item 3's frontend rule is already satisfied: the frontend uses
  `VITE_DISCORD_CLIENT_ID` (a public id) and obtains its code via
  `DiscordService.getAuthorizationCode`. Do not add a secret to the frontend.
- Startup behavior when a credential is absent is currently unspecified and is
  one of the required decisions; do not make the application fail closed or
  open by choice here.
- See also **TASK-035 — Discord Identity Exchange Contract**, the consumer of
  this credential. These two tasks are related but separate: TASK-035 owns *how
  the credential is used*, TASK-036 owns *how it is stored*.
- Do not modify `tasks/completed/`.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — only if the decision introduces a verifiable
                         configuration contract (e.g. absent-credential
                         behavior)
[x] Integration tests  — no response body, header, or error message contains
                         the credential; the tracked configuration carries no
                         secret value
[ ] Gameplay scenarios — N/A: this task introduces no gameplay behavior
```

### Key Edge Cases

- A response from `POST /api/auth/discord` never contains the Client Secret
- An error path (upstream failure, invalid configuration) never echoes the
  credential into a response or a log line
- The tracked configuration contains no secret, in every environment file that
  is committed
- `git log --all` contains no credential value after remediation
- The frontend bundle contains no secret (`ADR-007` item 3)

---

## Stop Conditions

- **While BLOCKED: any attempt to remediate is itself the violation.** Do not choose a secrets mechanism, a rotation procedure, or a startup behavior.
- If the required decisions above are still undefined when work resumes: STOP per `AGENTS.md` §7
- If remediation would require a destructive repository action (history rewrite, force-push) without explicit authorization: STOP per `.ai/README.md` §13 ("destructive change")
- If this task is asked to *use* the credential rather than *store* it: STOP — that is TASK-035 scope
- If the credential value would have to be reproduced to complete the work: STOP — never reproduce it
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files
- `<file path>` — <summary of change; never a credential value>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)

### Server Authority & Scope Verification
- [ ] Confirmed zero client-authoritative gameplay logic
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [ ] Confirmed no credential value is committed, logged, or exposed
- [ ] Confirmed no OAuth exchange implemented (TASK-035 boundary)

---

## Revision History

**Revision 1 — task created (BLOCKED).** TASK-036 was created by the TASK-023
decomposition revision, which verified that
`src/backend/GameServer.Api/appsettings.Development.json` holds a non-empty,
real-looking Discord `ClientSecret` in plaintext.

Verification refined the earlier report: the file is **gitignored and was never
committed**, and the tracked `appsettings.json` carries an **empty**
`ClientSecret`. So this is **not a repository leak** — `ADR-007` item 3's Git
boundary is intact. The residual issue is a real credential sitting in plaintext
in the working tree, protected only by a `.gitignore` entry, which is a commit
guard rather than a secrets-management mechanism.

The task is BLOCKED because no document decides the remediation mechanism
(local secrets convention, whether to rotate, tracked-file placeholder shape, or
absent-credential startup behavior). No secrets mechanism, rotation procedure,
or configuration strategy was invented, and no ADR was created, because the
repository's ADR convention forbids recording a decision that has not been made.
No credential value was reproduced anywhere.
