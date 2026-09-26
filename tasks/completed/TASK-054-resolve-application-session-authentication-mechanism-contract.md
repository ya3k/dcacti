# TASK-054 — Resolve Application Session and Authentication Mechanism Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
-->

---

## Metadata

```text
Task ID:           TASK-054
Type:              ARCHITECTURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH (P1 — resolves the contract that unblocks TASK-034,
                   the only hard blocker for CRITICAL TASK-041)
Primary Agent:     backend
Supporting Agents: review, realtime, testing
Workflow:          architecture/architecture-change.md, gated by
                   architecture/adr-change.md and
                   documentation/documentation-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   backend/api-contract-validation,
                   quality/documentation-consistency,
                   quality/architecture-conformance
Dependencies:      None (inputs are docs/ + one human decision round)
Blocks:            TASK-034 (implementation may now start against the recorded
                   contract); read-only context: TASK-041 (must not be
                   edited), TASK-051 (B1/B2/B3 preserved), TASK-035 (session
                   mechanism deliberately excluded from its scope)
```

**Status note.** `BACKLOG` was correct for file creation (`tasks/README.md`
§6). The first execution pass reached the §13 stop condition and the file
moved `backlog/` → `blocked/`. The human then answered D1–D6 (§13.1); the
second pass recorded the contract (ADR-015 + `API_CONTRACTS.md` §2.8 +
`SIGNALR_PROTOCOL.md` §1 + README §7), verified §5 unchanged, and marked the
task DONE. Per `tasks/README.md` lifecycle (`blocked/ → active/ when block
resolved`, `active/ → completed/ when done`), the file moved `blocked/` →
`active/` → `completed/`; the DONE status is authoritative over the path
(`tasks/README.md` §6: status lives in the file's `Status:` field).
Implementation of the recorded contract is TASK-034's work and remains
untouched here.

---

## 1. Objective

Obtain and record the application session/authentication mechanism contract that
`ADR-007` item 4 requires but no document defines, so that TASK-034 can later
implement it without inventing anything:

```text
human decision (§13.1, D1–D6)
        ↓
recorded contract: new ADR (next sequential = ADR-015) + owning
                   technical document (API_CONTRACTS.md session sections)
                   + ADR index (docs/03-decisions/README.md §7)
        ↓
TASK-034 (implementation) unblocks and builds against the record
```

This task is **contract resolution only**. It decides nothing itself: every
item in §13.1 is answered by a human, and this task's job is to capture the
answer in the owning documents (AGENTS.md §7, §17, §18, §20).

---

## 2. Authoritative References

- `docs/03-decisions/ADR/ADR-007-discord-activity-authentication.md` — items 2–4: the exchange boundary, the issued "application session token", and its downstream use on REST + SignalR; decides **no mechanism**
- `docs/03-decisions/ADR/ADR-013-discord-identity-exchange-contract.md` — fixes code → `DiscordUserId`; explicitly leaves the application session open (API_CONTRACTS.md §2.4 inequality list)
- `docs/03-decisions/ADR/ADR-014-battle-state-player-identity.md` — battle-end owner identity comes from `BattleState.PlayerId`, never from a session
- `docs/02-technical/API_CONTRACTS.md` §1 (all endpoints except `/api/auth/discord` require an authenticated session), §2.4, §2.5 (mechanism explicitly owned by TASK-034), §2.7 (access token ≠ session), §4 notes 6–7 (`401 UNAUTHENTICATED`; owner-only result read), §6 (error envelope)
- `docs/02-technical/ARCHITECTURE.md` §2.1 (layer direction), §2.3 items 3–4 (returns an application session token; SignalR connects with it)
- `docs/02-technical/TDD.md` §2.1 items 2, 4 (session issued after exchange; SignalR connects only after the session is authenticated)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §1 — `BattleHub` connection is authenticated using the application session (no failure contract defined)
- `docs/03-decisions/README.md` §2, §5, §7, §8 — when to create an ADR, status values, index, Known Open Items (this gap is **not** listed there today)
- `docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md`, `ADR-006` — storage boundaries that any session-storage choice must respect
- `docs/00-overview/MVP_SCOPE.md` §1 — "Player account (Discord identity, collection owner)" is IN
- `AGENTS.md` §4, §7, §13, §17, §18, §20

---

## 3. Scope

### In Scope

- Enumerate the decisions a human must make (§13.1) and capture the answers
  verbatim — no substitution, no defaults, no "reasonable" values chosen here
- Record the answered contract: new ADR (next sequential number **ADR-015**;
  ADR-014 is the highest existing), the owning technical contract in
  `API_CONTRACTS.md` (what/how), the ADR index entry (README §7) (why)
- Define at contract level: session artifact + transport, identity/claims
  carried and per-request identity resolution (`DiscordUserId → PlayerId`),
  request propagation for REST and SignalR, lifecycle (expiry/revocation),
  storage boundary, authentication scheme and enforcement/coverage, and the
  response for missing vs invalid vs expired sessions
- Verify the preserved invariants in §5 survive the recorded answer unchanged

### Out of Scope

- **All implementation** — middleware, scheme registration, token issuance,
  validation, `Program.cs`, `BattleHub`, controllers, client changes → TASK-034
- **The Discord exchange client** — TASK-035's downstream implementation
  (currently a stub; see §4) — reported, not fixed here (`AGENTS.md` §16)
- **Player persistence / match-create** (TASK-023), Player/Pet progression
  (TASK-033, TASK-024), Cards/Relics (TASK-027/TASK-028)
- BattleState, BattleResult rows, Redis battle state, SignalR protocol shape,
  Discord Activity architecture, gameplay of any kind
- Editing `TASK-034`, `TASK-041`, or any file under `tasks/completed/`
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## 4. Current State — Evidence (verified at creation; re-locate by search)

### 4.1 The requirement exists; the mechanism does not

Every authoritative source states **that** an authenticated application session
is required and **where** it is used; none defines **what it is**:

```text
ADR-007 item 4         all REST + SignalR authenticate "using the application
                        session token" — no format, claims, lifetime, validation
API_CONTRACTS.md §2.5   "The application session mechanism is not defined by
                        this contract ... owned by TASK-034"
API_CONTRACTS.md §4 n.6 outcome fixed (401 UNAUTHENTICATED); "the mechanism ...
                        is not defined here"
ARCHITECTURE.md §2.3    backend "returns an application session token"; hub
                        connects "using the authenticated application session"
TDD.md §2.1 item 4      SignalR connections established only after the session
                        is authenticated
SIGNALR_PROTOCOL.md §1  connection authenticated via that session — no failure
                        contract for a rejected connection
README.md §7/§8         no ADR decides it; the gap is not even listed as a
                        Known Open Item
docs/ search            no "JWT", "cookie", "Bearer" (application session),
                        "authentication scheme", or lifetime value appears
                        anywhere in docs/02-technical, docs/00-overview, or
                        docs/01-game-design
```

`API_CONTRACTS.md` §2.5 now states outright that the mechanism "is not defined
by this contract" and is "owned by TASK-034" — the "(e.g. JWT token or session
cookie)" parenthetical TASK-034's blocked record cites was removed by the
TASK-035 §2 rewrite, so the current document offers no candidate mechanism at
all.

### 4.2 The code is a stub, not a decision

| Element | Actual state (verified) |
|---|---|
| `AuthController.cs:105–107` | returns `SessionToken: $"session_{Guid.NewGuid():N}"`; `PlayerId` is the real matched/created Player |
| Session validation | none — nothing reads `sessionToken` anywhere in `src/` |
| `Program.cs` | no `AddAuthentication` / `UseAuthentication` / `UseAuthorization` |
| Authentication packages | no `JwtBearer` (or any scheme) referenced; no `[Authorize]` anywhere in `src/backend/` |
| `BattleController.cs:167–179` | reads `HttpContext.Items["GameServer.PlayerId"]`; **the key is never written by anyone** → every `POST /api/battle/start` returns `401 UNAUTHENTICATED` |
| `BattleHub` | no authentication |
| `ApiService.ts:6` | client holds `sessionToken` and never sends it (no `Authorization` header, no credentials) |
| Identity exchange | `DependencyInjection.cs:71` registers `UnconfiguredDiscordIdentityResolver`, which returns `503 DISCORD_UNAVAILABLE` — `POST /api/auth/discord` cannot currently complete step 1 (TASK-035 downstream; adjacent, report-only) |

**Implementation convention must not be promoted into design** — the existence
of `session_{guid}` is evidence of a stub, not of a chosen mechanism.

---

## 5. Decisions Already Authoritative (preserved — must not be reopened)

These are settled by documentation and survive any answer to §13.1:

| # | Settled decision | Source |
|---|---|---|
| 1 | Exchange happens server-side; Client Secret never reaches the frontend | ADR-007 items 2–3, ADR-013, API_CONTRACTS §2.7 |
| 2 | Verified identity = `DiscordUserId` (the `/users/@me` `id`); ≠ code, ≠ access token, ≠ session | API_CONTRACTS §2.4, ADR-013 |
| 3 | Identity mapping: `DiscordUserId` → Player match/create → `PlayerId`; battle-end owner = `BattleState.PlayerId` | DATABASE.md §1, ADR-014 |
| 4 | The backend issues an authenticated application session after Player match/create | ADR-007 item 2, ARCHITECTURE §2.3 item 3 |
| 5 | All REST endpoints except `/api/auth/discord`, and the SignalR `BattleHub`, authenticate with that session; SignalR connects only after it is authenticated | API_CONTRACTS §1, ADR-007 item 4, TDD §2.1 item 4 |
| 6 | Caller with **no** session → `401` + §6 envelope + `UNAUTHENTICATED` (never `404`) | API_CONTRACTS §4 note 6 (TASK-051 B2) |
| 7 | Only the authenticated owner reads a result; foreign/missing → same `404 BATTLE_NOT_FOUND`; existence never disclosed | API_CONTRACTS §4 note 7 (TASK-051 B3) |
| 8 | Ownership never from client-supplied input; identity derived server-side from the session | GAME_RULES §18, ADR-001, ADR-014, API_CONTRACTS §4 note 7 |
| 9 | The Discord access token is not the session and is never issued to the client | API_CONTRACTS §2.7 item 4 |
| 10 | `POST /api/auth/discord` response shape `{ "sessionToken", "playerId" }` | API_CONTRACTS §2.5 |
| 11 | `ADR-007` remains `Accepted` and is not superseded or edited; the new record is an additional ADR | AGENTS.md §4, §18; README §2 |
| 12 | `TASK-034` remains the prerequisite for `TASK-041` (B1) | TASK-051 §16 decision B1 |

---

## 6. Decision Points (the deliverable — human-answered)

Six decisions are undefined by every document. Each is listed with the options
that are **consistent with §5**; choosing is the human's call. Full statement,
including per-decision constraints, is §13.1.

```text
D1  Session mechanism      opaque server-side session id | self-contained
                           signed token | other (specify)
D2  Session storage        none (stateless) | Redis (extends ADR-005
                           boundary) | PostgreSQL (extends ADR-006 boundary)
                           | in-memory | other (specify)
D3  Identity representation  what the session carries (PlayerId / DiscordUserId
                           / both), claim names, per-request resolution to the
                           authenticated identity
D4  Request propagation    REST transport (Authorization header vs cookie) and
                           how BattleHub receives it
D5  Lifecycle              absolute lifetime, idle timeout, renewal, logout/
                           revocation, and missing vs invalid vs expired
                           responses
D6  Enforcement            ASP.NET Core authentication scheme, pipeline
                           placement, coverage (REST + hub methods), rejected-
                           connection behavior
```

---

## 7. Preserved Decisions from TASK-051 (must remain true)

- **B1** — `TASK-034` stays the prerequisite for `TASK-041`; this task resolves
  the contract that unblocks it and does **not** reverse the order.
- **B2** — a caller presenting no authenticated session receives
  `401 UNAUTHENTICATED`.
- **B3** — only the authenticated owner may read a result; a foreign or missing
  battle returns `404 BATTLE_NOT_FOUND`.

---

## 8. Acceptance Criteria

**This task's deliverable is the contract.** Implementation criteria belong to
TASK-034 and are listed only so it can inherit them.

### Contract deliverable (this task)

- [x] Every §13.1 decision (D1–D6) is answered by a human and recorded **verbatim** — no option chosen by the agent
- [x] The answered mechanism is recorded in a new ADR (next sequential = **ADR-015**), status `Accepted` only once the decision exists, registered in `docs/03-decisions/README.md` §7
- [x] The wire/behavior contract is recorded in the owning technical document (`API_CONTRACTS.md` §2.8 session section) — the technical doc owns *what*, the ADR owns *why* (AGENTS.md §2)
- [x] §5 rows 1–12 remain byte-identical in meaning after the record (verified by re-read, not assumed)
- [x] `401 UNAUTHENTICATED` for a session-less caller is unchanged; owner-only result read and the no-disclosure rule are unchanged
- [x] `ADR-007` and `ADR-013` are not edited or superseded
- [x] No session format, claim name, lifetime value, signing algorithm, scheme, or storage location is invented at any point before the human answers
- [x] Scope respected: `TASK-034` and `TASK-041` files untouched; no file under `src/` or `tests/` changed while any §13.1 item is unanswered

### Downstream criteria (owned by TASK-034, inherited, not satisfied here)

- [ ] Scheme registered and enforced at the recorded pipeline location; hub connects only with a valid session
- [ ] Valid session resolves to the owning `PlayerId` per D3; `BattleController`'s context key is actually written
- [ ] Invalid/expired sessions rejected per D5; unauthenticated → documented response
- [ ] `POST /api/auth/discord` still satisfies §2.5's response shape
- [ ] Tests pass at the required validation depth (`core/validation.md` §2)

---

## 9. Affected Files & Areas

```text
[ ] src/backend/  (no change while BLOCKED; TASK-034 implements later)
[ ] src/frontend/client/ (no change — propagation change, if any, is TASK-034's)
[ ] tests/        (no change — contract task adds no tests)
[ ] docs/03-decisions/ADR/ADR-015-<slug>.md      — create, AFTER D1–D6 answered
[ ] docs/02-technical/API_CONTRACTS.md            — session contract recorded,
                                                    AFTER D1–D6 answered
[ ] docs/03-decisions/README.md §7                — index entry, AFTER ADR-015
[ ] tasks/completed/                              — never modified
[ ] tasks/backlog/TASK-034-*.md, TASK-041-*.md    — not modified by this task
```

---

## 10. Implementation Notes

- **Recording order (after answers):** 1) write ADR-015 with the human's
  choices; 2) register it in README §7; 3) record the wire contract in
  `API_CONTRACTS.md`; 4) only then may TASK-034 transition
  `BLOCKED → IN PROGRESS` and implement. An ADR alone does not satisfy this
  task — `docs/02-technical/` owns the *what* (AGENTS.md §2).
- **ADR numbering:** TASK-034's "ADR Requirement" section still says the next
  number is ADR-013 — that text predates ADR-013/ADR-014 and is stale. The next
  sequential number today is **ADR-015**. (Report-only; TASK-034 is not edited
  here, AGENTS.md §16.)
- **Known Open Items:** README §8 does not list the session mechanism, so the
  gap is currently an *unrecorded* gap rather than a tracked one (TASK-034's
  blocked record). Recommend recording it there while the task is open —
  report-only decision for the requester, not made unilaterally here.
- **Storage boundary:** any answer to D2 that places sessions in Redis or
  PostgreSQL deliberately extends ADR-005/ADR-006 (AGENTS.md §13: Redis =
  active battle state only, PostgreSQL = persistent data). That extension must
  appear in the new ADR and the owning storage document — never silently in
  code.
- **Layering:** authentication is an Api/Infrastructure concern
  (`ARCHITECTURE.md` §2.1); the decision must not push identity logic into
  Domain, and `BattleHub` must not grow authorization logic of its own.
- **Client gap:** `ApiService.ts` receives `sessionToken` and never sends it —
  a consequence of the missing mechanism, fixed by TASK-034 once D4 is
  recorded, not an independent task.
- **Adjacent (report-only, AGENTS.md §16):** the TASK-035 exchange client is
  unimplemented (`UnconfiguredDiscordIdentityResolver` → `503
  DISCORD_UNAVAILABLE`), so authentication cannot complete end-to-end today.
  It is a separate downstream implementation, not part of this contract.

---

## 11. Testing Requirements

### Required Verification

**This task adds no tests.** Its deliverable is the recorded contract; the
tests below belong to TASK-034.

```text
[ ] Unit tests         — session validation; identity → PlayerId resolution (TASK-034)
[ ] Integration tests  — unauthenticated → 401 UNAUTHENTICATED; authenticated
                         resolves to the owning PlayerId; invalid and expired
                         rejected per D5; player A cannot act as player B (TASK-034)
[ ] Gameplay scenarios — N/A: authentication is not gameplay
```

### Key Edge Cases (contract must specify behavior for each)

- Missing session vs invalid/tampered session vs expired session — distinct or
  grouped responses, decided under D5 within the §6 envelope
- A `BattleHub` connection attempted without a valid session (SIGNALR_PROTOCOL
  defines no failure contract today — D6 must)
- A session for Player A presenting a battle owned by Player B →
  `404 BATTLE_NOT_FOUND` unchanged (§5 row 7)

---

## 12. Stop Conditions

- **While BLOCKED: any attempt to choose a mechanism, format, claim, lifetime,
  propagation, storage, or scheme is itself the violation.**
- If any §13.1 decision is unanswered when work resumes: STOP per `AGENTS.md` §7
- If an answer would require inventing an identity representation, claim name,
  expiration value, or storage location: STOP per `AGENTS.md` §7
- If an answer conflicts with a §5 row or requires editing `ADR-007`/`ADR-013`
  rather than adding an ADR: STOP per `AGENTS.md` §4
- If an answer extends a storage boundary (D2): record it in the ADR + owning
  storage doc first — never implement it implicitly
- If scope expands into implementation (TASK-034), Player persistence
  (TASK-023), the exchange client (TASK-035 downstream), or any gameplay
  system: STOP & decompose
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose

---

## 13. STOP CONDITION

```text
STOP CONDITION

Problem:
The application session mechanism required by ADR-007 item 4 and depended on by
every authentication-required contract (API_CONTRACTS §1/§4, ARCHITECTURE §2.3,
TDD §2.1, SIGNALR_PROTOCOL §1) is not defined by any authoritative document, and
choosing it is an architectural decision with security consequences that this
task may not make.

Relevant sources:
docs/03-decisions/ADR/ADR-007-discord-activity-authentication.md item 4
docs/02-technical/API_CONTRACTS.md §1, §2.4, §2.5, §2.7, §4 notes 6–7, §6
docs/02-technical/ARCHITECTURE.md §2.3 items 3–4
docs/02-technical/TDD.md §2.1 items 2, 4
docs/02-technical/SIGNALR_PROTOCOL.md §1
docs/03-decisions/README.md §7 (no ADR), §8 (gap not listed as an open item)
src/backend/GameServer.Api/Controllers/AuthController.cs:105 (placeholder token)
src/backend/GameServer.Api/Controllers/BattleController.cs:167–179 (context key
  is read but never written → all battle starts 401)
src/backend/GameServer.Api/Program.cs (no authentication registered)

Conflict / missing information:
Not a document conflict — a documented gap. Undefined: session mechanism,
storage boundary, identity/claims representation, request propagation (REST +
SignalR), expiration/revocation, authentication scheme, enforcement coverage,
and the missing-vs-invalid-vs-expired response set. Multiple materially
different implementations (opaque server-side session, signed token, cookie
backing, in-memory vs Redis vs PostgreSQL storage) are all equally consistent
with the existing documents — AGENTS.md §20 "Ambiguous requirement".

Proposed resolution:
Human answers the six decisions in §13.1 (D1–D6); this task then records them
as ADR-015 + the API_CONTRACTS.md session contract + the README §7 index entry,
verifying §5 unchanged; only then does TASK-034 implement.

Waiting for:
Human decision on D1–D6 (§13.1) — no value may be selected by the agent.
```

### 13.1 Smallest exact decision required from the human

Constraints common to all six: identity is always server-derived from the
verified `DiscordUserId` → `PlayerId` (§5 rows 2–3, 8); no client-supplied
identity; `401 UNAUTHENTICATED` and owner-only `404` stay as recorded (§5 rows
6–7); the Discord access token is never the session (§5 row 9).

**D1 — Session mechanism: what is issued as `sessionToken`?**
- (a) Opaque server-side session id (random token; server holds the state)
- (b) Self-contained signed token (e.g. JWT; validated without a lookup)
- (c) Other — specify exactly
- Note: Discord-token passthrough is excluded by §5 row 9 unless you are
  explicitly changing that decision (which would be an AGENTS.md §4 conflict).

**D2 — Session storage boundary: if (a), where does session state live?**
- (a) None (stateless — only with D1(b))
- (b) Redis — extends ADR-005 (documented as a deliberate second Redis use)
- (c) PostgreSQL — extends ADR-006 (persistent-data boundary)
- (d) In-memory — single instance, lost on restart (state that explicitly)
- (e) Other — specify exactly

**D3 — Identity representation: what does the session carry, and how does a
request resolve to an identity?**
- Which of `PlayerId` / `DiscordUserId` / both are carried
- The claim/key names (today's read-side convention is the context key
  `GameServer.PlayerId` in `BattleController.cs:179` — adopt, rename, or replace)
- The resolution rule: session → authenticated request identity → `PlayerId`

**D4 — Request propagation: how does the client present it?**
- REST: `Authorization: Bearer <token>` header, or HTTP-only cookie (note:
  CORS already sets `AllowCredentials()` — that is not a decision)
- SignalR: how `BattleHub` receives it (e.g. SignalR's access-token mechanism)
- Whatever is chosen, `ApiService.ts` must start sending it (implementation =
  TASK-034)

**D5 — Lifecycle: expiry, renewal, revocation, and failure responses**
- Absolute lifetime value; idle timeout (yes/no + value); renewal or refresh
- Logout / revocation semantics; behavior when a new Discord code is exchanged
- Response set for: no session, invalid/tampered session, expired session
  (which code(s), within the API_CONTRACTS §6 envelope)

**D6 — Enforcement: scheme, placement, coverage**
- Which ASP.NET Core authentication scheme(s) are registered (none today)
- Pipeline placement and attributes; where enforcement happens for `BattleHub`
  and what a connection without a valid session receives
- Coverage confirmation: all REST except `/api/auth/discord` (already required
  by API_CONTRACTS §1) plus which hub methods

**After answers:** record as ADR-015 + `API_CONTRACTS.md` session contract +
README §7 index (§10), verify §5 unchanged, then hand to TASK-034.

---

## 14. Completion Evidence

### Human Decisions (recorded verbatim — answered by the human, not the agent)

```text
D1  Session mechanism:   Self-contained signed JWT, issued after the Discord
                         identity exchange (the Discord access token is not
                         the session).
D2  Session storage:     Stateless — no session storage in Redis, PostgreSQL,
                         or in-memory; ADR-005 / ADR-006 boundaries NOT
                         extended.
D3  Identity:            Claim player_id = PlayerId; resolution
                         JWT player_id → authenticated request identity →
                         PlayerId (server-authoritative; the client never
                         supplies PlayerId / DiscordUserId;
                         GameServer.PlayerId may remain the internal context
                         key; DiscordUserId is not an authoritative ownership
                         claim).
D4  Propagation:         REST Authorization: Bearer <sessionToken> (no
                         cookie); SignalR standard access-token mechanism;
                         a Discord access token is never a BattleHub
                         credential; frontend transmission = TASK-034.
D5  Lifecycle:           24h absolute expiry; no idle timeout; no renewal/
                         refresh; no logout/revocation in MVP; a new exchange
                         may issue a new JWT; missing / invalid / tampered /
                         expired all → 401 {"error":"UNAUTHENTICATED"} (one
                         code, no validation detail leaked); owner → 200,
                         foreign-or-missing → 404 BATTLE_NOT_FOUND.
D6  Enforcement:         ASP.NET Core authentication/authorization pipeline
                         with the JWT Bearer scheme; exactly one auth
                         mechanism; all REST except POST /api/auth/discord
                         enforced; BattleHub requires the same JWT, rejected
                         at the auth boundary; no second custom auth inside
                         the hub (transport-focused).

NOT decided (implementation security config for TASK-034): signing algorithm
(HS256/RS256/ES256), issuer, audience, signing-key storage, key rotation.
```

### Changed Files

- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` — created (Status: Accepted, Date: 2026-09-26; Decision items 1–6 = D1–D6, "Not decided in this ADR" scope note, Alternatives, Why, Consequences, Related Documents)
- `docs/02-technical/API_CONTRACTS.md` — Version 1.8 → 1.9: §1 preamble (mechanism now specified in §2.8/ADR-015), §2.4 inequality-list reference (TASK-034 → ADR-015), §2.5 "not defined by this contract" paragraph replaced with a pointer to §2.8, new **§2.8 Application Session Mechanism (`ADR-015`)**, §4 note 6 mechanism sentence now points to §2.8
- `docs/02-technical/SIGNALR_PROTOCOL.md` — Version 2.5 → 2.6: §1 item 3 amended (session = signed JWT via SignalR access-token mechanism), new §1 items 4–5 (Discord access token never a hub credential; missing/invalid/expired rejected at the auth boundary, no second mechanism), item 6 = prior item 4 (no external §1.4 references existed)
- `docs/03-decisions/README.md` — Version 1.4 → 1.5: §7 index row for ADR-015 (§8 untouched — the gap was never listed as a Known Open Item)
- `tasks/blocked/TASK-054-*.md` → `tasks/active/` → `tasks/completed/TASK-054-*.md` — Status BLOCKED → DONE with this evidence

### Validation Results

- ADR sequence verified: `docs/03-decisions/ADR/` contains exactly ADR-001 … ADR-015 — 15 files, no duplicate, no skipped number; ADR-015 registered in README §7
- Terminology sweep (JWT / sessionToken / Bearer / cookie / opaque / Redis-session / undecided-session): no authoritative document states the session is a cookie, opaque id, Redis/PostgreSQL record, Discord token, or undefined — remaining "session cookie"/"opaque" strings are only ADR-015's own rejected Alternatives; ADR-013's historical "still-undecided" lines and API_CONTRACTS 1.8 changelog line are historical text (ADR-013 left unedited per §5 row 11)
- §5 rows 1–12 re-read against the new text: all 12 preserved (401 one-code set, 404 owner-or-missing, no-disclosure, server-derived identity, ADR-007 Accepted/unedited, TASK-034→TASK-041 prerequisite)
- `REDIS_STATE.md`, `DATABASE.md`, `ARCHITECTURE.md`, `TDD.md` reviewed — neutral wording, no session-storage claim, no conflict; no edit required
- TASK-034 / TASK-041 / `tasks/backlog/` / ADR-007 / ADR-013: zero changes (`git status` clean for those paths)
- `src/` and `tests/`: zero changes by this task (working-tree modifications present there are the pre-existing BossDefinition/provisioning work of TASK-050–053, unrelated to session auth)
- No test suite run: documentation-only change (no source, migration, or configuration file modified by this task)

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no gameplay touched; identity remains server-derived per §5 rows 2–3, 8)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Player account, Discord identity; no new system introduced)
- [x] Confirmed no algorithm, issuer, audience, key storage, or rotation invented — ADR-015 states them as not decided
- [x] Confirmed no implementation added (TASK-034 boundary) and TASK-041 untouched (TASK-051 B1)

---

## Revision History

**Revision 1 — task created (BACKLOG → BLOCKED on first pass).** Created as the
next sequential ID (highest existing = TASK-053) to own the contract-resolution
half of TASK-034's blocker, so that decision capture and implementation are
separately verifiable (the TASK-035 pattern: contract first, implementation
in the owning task). Inspection re-confirmed TASK-034's blocked record and
extended it with current code evidence: the Discord exchange resolves through
an unconfigured stub (503), the auth pipeline registers no scheme, the
`GameServer.PlayerId` context key has no writer, and the client never transmits
the session token. Repository-wide search of `docs/02-technical/`,
`docs/00-overview/`, and `docs/01-game-design/` found no session mechanism,
transport, scheme, lifetime, or storage decision anywhere. Per AGENTS.md §7/§18/
§20 and `.ai/skills/backend/api-contract-validation.md` Stop Conditions, no
mechanism was chosen; the six required human decisions are enumerated in §13.1.

**Revision 2 — contract recorded, BLOCKED → DONE (D1–D6 answered by the
human).** The human answered §13.1's six decisions (recorded verbatim in
§14); no option was selected by the agent. ADR-015 was created with Status
`Accepted`, registered in README §7 (Version 1.5), and the WHAT contract was
recorded in `API_CONTRACTS.md` §2.8 (Version 1.9) with §1/§2.4/§2.5/§4-note-6
references updated from the open TASK-034 decision to ADR-015;
`SIGNALR_PROTOCOL.md` §1 was amended/extended (Version 2.6) for D4/D6.
`ADR-007`/`ADR-013` unedited (§5 row 11); ADR-013's historical
"still-undecided session mechanism" wording (lines ~113, ~209) is stale-but-
frozen — report-only, per AGENTS.md §16. `REDIS_STATE.md`/`DATABASE.md`
needed no change (D2 = stateless, no boundary extension). §5 rows 1–12
re-read and preserved; TASK-034/TASK-041/`src/`/`tests/` untouched. File
moved `blocked/` → `active/` → `completed/` per tasks/README.md lifecycle;
TASK-034 is now unblocked to implement against this record.
