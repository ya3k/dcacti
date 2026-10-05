# TASK-036 — Discord Credential Secret Hygiene

---

## Metadata

```text
Task ID:           TASK-036
Type:              ARCHITECTURE
Status:            SUPERSEDED
Superseded by:     ADR-020, TASK-187
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
Blocked by:        None — superseded by ADR-020 / TASK-187
```

---

## Supersession Notice (2026-10-06)

**Status:** SUPERSEDED by `ADR-020` and `TASK-187`.

All intended deliverables of this task have been rendered 100% obsolete by `ADR-020` (*Standalone Web Account Authentication & Retirement of Discord Activity Dependency*) and `TASK-187`. `ADR-020` explicitly supersedes `ADR-019`, `ADR-013`, and `ADR-007`, retiring the Discord Embedded App SDK, backend OAuth token exchange endpoints, and all Discord configuration keys (`Discord:ClientId`, `Discord:ClientSecret`). No Discord credentials exist in the architecture or codebase; zero actionable scope remains.

---

## Resolution — No Longer Blocked

This task was `BLOCKED` because the credential-handling gap was verified and
real, but **how to remediate it was not defined by any authoritative document**,
and this task must not choose a remediation (`AGENTS.md` §7, §18, §20).

**Every required decision has now been taken explicitly by the Product Owner
(security decision session, 2026-10-04) and recorded as
`docs/03-decisions/ADR/ADR-019-discord-credential-secret-hygiene.md`
(`Status: Accepted`, decisions D1–D6).** The five items this task was blocked on
map to that ADR as follows:

| Originally undecided item | Decided by | Decision |
|---|---|---|
| Whether the exposed credential must be rotated, and by whom | D6, D5 | Rotate **once as a precaution**; rotation is an **operator** action; manual, event-triggered, restart-required |
| The developer-secrets convention for local backend configuration | D1 | **.NET user-secrets store** locally; **host environment variable** in deployment. No `.env` loader is introduced |
| Whether the tracked `appsettings.json` placeholder shape is the intended convention | D3 | **Keep the empty string**; "absent" means null/empty/whitespace |
| Whether startup should fail loudly when a required Discord credential is absent | D4 | **Environment-sensitive**: Production **fails startup**; Development starts and keeps the unchanged `503 DISCORD_UNAVAILABLE` |
| Whether the credential needs to be present before TASK-035 defines the exchange contract | TASK-035 DONE (`ADR-013`) + D4 | TASK-035 is DONE; the exchange client remains unimplemented, so the credential is still inert; D4 governs presence per environment |

`ADR-007` item 3's boundary, `ADR-013` item 11's configuration keys, and
`ADR-015` D7–D11 are **unchanged** by this resolution. The ADR does not supersede
any of them; it fills the one area they deliberately left open.

### Already completed by the decision session (documentation only)

- `ADR-019` created and registered in `docs/03-decisions/README.md` §7, with the
  open-item disposition recorded in §8.
- `src/backend/.env.example` corrected per D2: its Discord section and header no
  longer instruct developers to create `src/backend/GameServer.Api/.env` — a
  path that is neither read by the host (no `.env` provider) nor covered by
  `.gitignore` (which ignores `src/backend/.env` only). The file now documents
  configuration **key names** and the channels that actually supply values.

No credential was read, copied, rotated, or modified during the decision
session, and no application code was changed.

---

## Verified Finding (unchanged, reproduced without the credential value)

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
mechanism. `ADR-019` D1/D6 remove this condition.

### Scope limit, stated honestly

This task verified the **format and tracking status** of the value. It did
**not** and **cannot** verify whether the credential is live, valid, or already
revoked — that requires Discord-side knowledge this repository does not carry.
`ADR-019` D6 resolves the disposition (rotate once, as a precaution); performing
that rotation remains a human/operator action this task **records and verifies**,
not one it performs by invention.

**The credential value must never be reproduced in this task, in any task, in
a commit message, in a log, or in a report.**

---

## Objective

Bring Discord credential handling into line with `ADR-007` item 3 / `TDD.md`
§2.1 item 3's server-side secret boundary **and with `ADR-019` D1–D6**, so that
no real credential sits in plaintext in the working tree, the host has a
documented channel for the credential, and the absent-credential behavior is
implemented.

This task owns **credential storage and hygiene only**. It does not own the
OAuth exchange that consumes the credential.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-019-discord-credential-secret-hygiene.md` — **the
  authoritative decision for this task** (D1–D6 plus the security invariants)
- `docs/03-decisions/ADR/ADR-007-discord-activity-authentication.md` item 3 — the
  Client Secret security boundary: backend-held via "server-side
  environment/secret configuration", never exposed via client bundles,
  environment variables exposed to browsers, React config, Phaser code, or **Git**
- `docs/03-decisions/ADR/ADR-013-discord-identity-exchange-contract.md` item 11 —
  the canonical `Discord:ClientId` / `Discord:ClientSecret` configuration keys
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md`
  D7–D11 — the JWT signing key's own channel and rotation; its scope explicitly
  excludes Discord credential hygiene, and it is the precedent D5 follows
- `docs/02-technical/TDD.md` §2.1 item 3 — the same boundary, restated for the
  backend
- `docs/02-technical/ARCHITECTURE.md` §2.3 item 2 — `Discord Client ID` /
  `Discord Client Secret` managed "through secure server-side
  configuration/environment secrets"
- `docs/02-technical/API_CONTRACTS.md` §2.6, §2.7 item 5, §6 — the failure
  contract, the "secrets and tokens are never logged" rule, and the error envelope
- `docs/03-decisions/README.md` §2, §5, §7, §8
- `.gitignore` — the existing secrets/local-config section
- `tasks/completed/TASK-035-discord-identity-exchange-contract.md` — the exchange
  contract (**DONE**; its credential-storage side is this task)

---

## Scope

### In Scope

- Moving the local Discord credential to the **user-secrets store** (D1), so no
  credential value remains in `appsettings.Development.json`
- Implementing the **environment-sensitive absent-credential behavior** (D4):
  Production refuses to start when `Discord:ClientId` / `Discord:ClientSecret`
  is absent (null/empty/whitespace per D3); Development starts and
  `POST /api/auth/discord` keeps the unchanged `503 DISCORD_UNAVAILABLE`
- Recording the **operator rotation** (D6) as completed — an operator action this
  task records and verifies, not one it performs by invention
- Verification that no credential value is committed, logged, or returned in any
  response, and that the tracked `appsettings.json` placeholder stays empty (D3)

### Out of Scope

- **The Discord OAuth exchange itself** — owned by TASK-035 (contract DONE;
  exchange client still unimplemented and separately owned)
- Player persistence and match/create (TASK-023 — DONE)
- The application session mechanism (TASK-034 — DONE; `ADR-015`)
- The `ClientId` — it is a public identifier, not a secret (`ADR-007` item 3)
- Frontend `VITE_DISCORD_CLIENT_ID` handling — the frontend must hold no secret
  (`ADR-007` item 3)
- Changing `ADR-015` D7–D11 (JWT signing key algorithm, issuer, audience, key
  storage, or rotation) — a change to that ADR requires its own human decision
- Any gameplay system, and any item listed as OUT in
  `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

```text
src/backend/GameServer.Api/appsettings.json              tracked; ClientSecret is "" (empty) — D3 keeps this
src/backend/GameServer.Api/appsettings.Development.json  gitignored, untracked; holds a
                                                         non-empty, real-looking ClientSecret
                                                         → D1/D6 move it to the user-secrets store
src/backend/.env.example                                 corrected by the decision session (D2)
```

No backend code reads either `Discord:ClientId` or `Discord:ClientSecret`
today — there is no Discord OAuth client in `src/`. The credential is currently
inert configuration, and `UnconfiguredDiscordIdentityResolver` returns the
`API_CONTRACTS.md` §2.6 transient `503 DISCORD_UNAVAILABLE`.

---

## Acceptance Criteria

- [ ] The local Discord credential is supplied from the user-secrets store and no
      credential value remains in `appsettings.Development.json` (`ADR-019` D1)
- [ ] `src/backend/.env.example` documents key names and approved channels only,
      and names no path the host does not read (`ADR-019` D2) — **done in the
      decision session; verify only**
- [ ] The tracked `appsettings.json` carries no secret and keeps the empty
      placeholder, with "absent" meaning null/empty/whitespace (`ADR-019` D3)
- [ ] A missing credential fails startup in Production and any non-Development
      environment (`ADR-019` D4)
- [ ] In Development a missing credential starts the host and leaves
      `POST /api/auth/discord` returning the **unchanged** `503
      DISCORD_UNAVAILABLE`, creating no Player and issuing no session
      (`ADR-019` D4; `API_CONTRACTS.md` §2.6)
- [ ] TASK-181's development authentication is unaffected and remains
      unavailable in Production (`ADR-019` D4)
- [ ] The operator rotation of the existing local credential is **recorded as
      completed** (`ADR-019` D6, D5) — without reproducing the value
- [ ] No Discord Client Secret value exists in plaintext in a tracked file, in
      git history, or in any committed artifact
- [ ] The Client Secret is never exposed to the frontend, a client bundle, a
      response body, or a log line (`ADR-007` item 3; `ADR-019` security
      invariants)
- [ ] The repository's existing `.gitignore` secrets entries remain effective
      (not weakened or removed)
- [ ] All relevant tests pass at the required validation depth
      (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Api/appsettings*.json  (placeholder verification; no
                                                  secret value in any tracked file)
[x] src/backend/GameServer.Api/                   (startup configuration binding
                                                  for ADR-019 D4)
[x] src/backend/.env.example                      (D2 — corrected by the decision
                                                  session; verify only)
[ ] src/frontend/client/                          (no change expected — the
                                                  frontend holds no secret)
[x] tests/                                        (the D4 configuration contract is
                                                  verifiable: absent-credential
                                                  behavior per environment)
[x] docs/                                         (ADR-019 — CREATED by the
                                                  decision session; no further
                                                  ADR needed)
```

---

## Implementation Notes

- **Never reproduce the credential value** in code, comments, tests, task files,
  commit messages, logs, or reports. Refer to it as "the Discord `ClientSecret`"
  and nothing more.
- The `.gitignore` entry is already correct as a *commit guard* and must remain
  (`ADR-019` security invariants). This task is about the plaintext-at-rest gap,
  not about a missing ignore rule.
- Do not remove, rewrite, or `git rm --cached` anything as a shortcut. Moving the
  credential out of `appsettings.Development.json` is the D1 remediation; that
  file is gitignored and untracked, so no tracked-file state depends on it.
- The tracked `appsettings.json` keeps an **empty** `ClientSecret` string
  (`ADR-019` D3). For D4, "absent" is null, empty, or whitespace-only — the same
  test `ApplicationSessionSigningKeys` already applies to the session signing
  secret.
- `ADR-019` D4 is a **new** startup behavior in Production. Implement it without
  inventing a fallback: there is no anonymous path, no second authentication
  system, and no generated or default credential.
- `ADR-007` item 3's frontend rule is already satisfied: the frontend uses
  `VITE_DISCORD_CLIENT_ID` (a public id) and obtains its code via
  `DiscordService.getAuthorizationCode`. Do not add a secret to the frontend.
- Distinguish this task from the exchange: `ADR-019` decides only *how the
  credential is stored and supplied*. The exchange client's implementation
  remains separate.
- Do not modify `tasks/completed/`.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — the D4 absent-credential contract (absent = null/empty/
                         whitespace; Development vs non-Development behavior)
[x] Integration tests  — no response body, header, or error message contains the
                         credential; the tracked configuration carries no secret
                         value; Development's unconfigured path still answers
                         503 DISCORD_UNAVAILABLE and creates no Player
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
- A non-Development host with an absent credential refuses to start (`ADR-019` D4)
- A Development host with an absent credential starts, and `/api/auth/discord`
  answers the unchanged `503 DISCORD_UNAVAILABLE`
- TASK-181's development authentication still requires **both** a Development
  host **and** the explicit `DevelopmentAuthentication:Enabled` opt-in, and is
  inert outside Development

---

## Stop Conditions

- If satisfying any criterion would require changing `ADR-019` D1–D6, STOP —
  that is a change to the ADR and needs a recorded human decision first
  (`AGENTS.md` §4)
- If remediation would require a destructive repository action (history rewrite,
  force-push) without explicit authorization: STOP per `.ai/README.md` §13
- If this task is asked to *implement* the exchange rather than *store* the
  credential: STOP — that is the TASK-035 downstream's scope
- If the credential value would have to be reproduced to complete the work:
  STOP — never reproduce it
- If actual rotation of the live Discord credential is requested rather than
  *recorded*: STOP — rotation is an operator action (`ADR-019` D5/D6)

---

## Completion Evidence

### Changed Files
- `<file path>` — <summary of change; never a credential value>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)

### Operator Action Record
- [ ] Discord `ClientSecret` rotated once by an operator (`ADR-019` D6) — recorded
      by date and actor only; **no credential value recorded**

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

The task was BLOCKED because no document decided the remediation mechanism
(local secrets convention, whether to rotate, tracked-file placeholder shape, or
absent-credential startup behavior). No secrets mechanism, rotation procedure,
or configuration strategy was invented, and no ADR was created, because the
repository's ADR convention forbids recording a decision that has not been made.
No credential value was reproduced anywhere.

**Revision 2 — BLOCKED → READY; decisions recorded as ADR-019.** The Product
Owner supplied all six decisions in a security decision session (2026-10-04),
recorded as `docs/03-decisions/ADR/ADR-019-discord-credential-secret-hygiene.md`
(`Status: Accepted`, D1–D6). This revision:

- maps each of the five originally-undecided items to its resolving decision;
- records that the decision session corrected `src/backend/.env.example` per D2
  and registered ADR-019 in `docs/03-decisions/README.md` §7/§8;
- reframes the acceptance criteria against ADR-019 instead of a placeholder
  `<doc §>`;
- notes that TASK-035 is **DONE** (`ADR-013`), superseding Revision 1's
  "consumer of this credential (BLOCKED)" reference;
- retires the "while BLOCKED, any attempt to remediate is itself the violation"
  stop condition, replacing it with the ADR-change and operator-rotation stops.

The verified finding, the "never reproduce the credential value" rule, and the
scope limits are preserved unmodified. No implementation was performed, no
credential was read, rotated, or modified, and the credential value appears
nowhere in this revision.
