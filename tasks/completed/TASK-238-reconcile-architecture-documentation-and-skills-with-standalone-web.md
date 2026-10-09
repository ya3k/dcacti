# TASK-238 — Reconcile Architecture Documentation and Skills with Standalone Web Architecture

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
  Created from ADR-020 (Status: Accepted, 2026-10-06), ADR-023 (Status:
  Accepted, 2026-10-09), and the completed TASK-237 record.
-->

<!--
  Lifecycle: created BACKLOG 2026-10-09 by the orchestrator role at the
  requester's direction. Intake only — no documentation, source, test,
  configuration, or ADR file is modified by this task record's creation.
-->

<!--
  Lifecycle: BACKLOG → READY completed 2026-10-09 by the orchestrator role at the
  requester's direction, after validating the tasks/TASK_LIFECYCLE.md §3 READY
  criteria:
    [x] Task type confirmed (DOCUMENTATION — tasks/TASK_TYPES.md §2 /
        documentation/documentation-change.md)
    [x] Relevant documentation exists in docs/ and .ai/ (ADR-020, ADR-015,
        ADR-023, ARCHITECTURE.md §1/§2.2/§2.2.2/§2.3, TDD.md §2.1,
        .ai/README.md §19/§6)
    [x] MVP scope confirmed (MVP_SCOPE.md §1 — the standalone web client is the
        IN-scope frontend; Discord is unlisted, hence FUTURE per §4. This task
        adds no scope — it removes documentation residue that contradicts an
        accepted ADR)
    [x] Not blocked: Dependencies: TASK-237 (DONE, immutable; its Phase A commit
        is verified present in history)
    [x] Primary agent assigned (review) and workflow assigned
        (documentation/documentation-change.md)
    [x] Acceptance criteria are binary and testable
    [x] Uniqueness: TASK-237 is the highest assigned ID and no record names
        TASK-238
    [x] Ownership: tasks/active/ and tasks/blocked/ hold no open record, and no
        other backlog record names any declared file. The last record to name
        docs/02-technical/ARCHITECTURE.md was TASK-237 (DONE, Phase A commit
        1cdcd398ad4dc9d4ebc2854d9aecf0b1d5b48c4a, which declared no docs path).
-->

<!--
  Lifecycle: READY → IN PROGRESS completed 2026-10-10 by the review agent under
  documentation/documentation-change.md: preflight completed (AGENTS.md,
  docs/AGENTS.md, .ai/README.md, tasks/README.md, tasks/TASK_LIFECYCLE.md,
  tasks/TASK_TYPES.md, .ai/workflow/documentation/documentation-change.md,
  ADR-020, ADR-023, ADR-015, ARCHITECTURE.md, TDD.md §2.1, MVP_SCOPE.md §1/§4).
  READY-versus-BACKLOG question resolved as no discrepancy: TASK_LIFECYCLE.md §3
  sets a READY record's File location as `tasks/backlog/` and §4 records
  `BACKLOG → READY` as "no move; status field only", so `Status: READY` inside
  `tasks/backlog/` is the workflow's specified state and no exception, override,
  or record edit was needed. Dependencies verified: TASK-237 is DONE and
  immutable, and ADR-020 and ADR-023 are both Accepted. The declared stale
  references were re-inspected in context, which also surfaced
  `ARCHITECTURE.md`'s §2.2 diagram `Services (discord/ · realtime/ · api/)` node
  (`:209`) — inside the authorized §2.2 diagram edit and required by acceptance
  criterion 2, though the evidence table did not enumerate it.

  Lifecycle: IN PROGRESS → IN REVIEW completed 2026-10-10 by the review agent
  after implementing only the two declared edits. The pre-change baselines were
  measured before any edit (`npm run test:run` — 933 passed / 23 files;
  `dotnet test src/backend/GameServer.sln` — 2,970 passed / 0 failed: Domain
  1,558, Application 653, Infrastructure 422, Api 337), and both suites were
  re-run on the final tree with identical results.

  Lifecycle: IN REVIEW → DONE completed 2026-10-10 by the review agent
  (quality/review.md §1 against this manifest, ADR-020 D1/D4/D5, ADR-023 §2 item
  3, TASK-237's completion record, ARCHITECTURE.md §1's client tree, and TDD.md
  §2.1): PASS with no blocking finding — the Phase A slice is exactly the two
  declared Markdown files, no module boundary, layer direction, port capability,
  endpoint, wire member, state model, Redis key, database column, gameplay rule,
  or contract changed, every surviving "Discord" occurrence in both files is
  enumerated in ## Completion Evidence as historical, decision-record,
  contrastive, or still-true-negative, and the preserve list is byte-identical.
  The declared implementation slice was committed as Phase A commit
  3d2b4b504a912f312937de232c521117e4e1ba9c, and this completion record is filed
  under its own Phase B commit.
-->

---

## Metadata

```text
Task ID:           TASK-238
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          MEDIUM
Primary Agent:     review
Supporting Agents: client (frontend shell and service-boundary accuracy)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis, quality/documentation-consistency, quality/architecture-conformance
Dependencies:      TASK-237 (DONE — the physical seam removal this task's version history reports; read-only, immutable, never edited)
Declared Files:    docs/02-technical/ARCHITECTURE.md, .ai/skills/client/react-phaser-boundary/SKILL.md
```

---

## Objective

Reconcile the two documents that still present Discord Activity as the current frontend hosting target — `docs/02-technical/ARCHITECTURE.md` §2.2/§2.2.2/§2.3 and its version history, and `.ai/skills/client/react-phaser-boundary/SKILL.md`'s ownership statements — with the accepted standalone-web architecture (`ADR-020`, `ADR-015`, `ADR-023`) and the frontend structure that actually exists under `src/frontend/client/src/`, changing no source code, configuration, test, ADR, or executable behavior.

---

## Authoritative References

- `docs/02-technical/ARCHITECTURE.md` §1 (client tree), §2.2, §2.2.1, §2.2.2, §2.3, §3 — the technical-design owner of the frontend structure and boundaries this task corrects; §1's client tree and `TDD.md` §2.1's `services/api/` + `services/realtime/` list are the in-repo evidence for the correct service boundaries
- `docs/02-technical/TDD.md` §2.1 — frontend responsibility boundaries (already reconciled to the standalone web client by TASK-235); the document `ARCHITECTURE.md` is subordinate to
- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` D1, D4 item 2, D5 — Discord Embedded App SDK, frontend `DiscordService`, `services/discord/`, and the Discord Activity iframe target are retired; standalone browser is the platform
- `docs/03-decisions/ADR/ADR-023-backend-discord-identity-seam-resolution.md` §2 item 3 — records the seam retirement decision; its "physical deletion deferred" wording became historical once TASK-237 landed
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` — the application session contract that remains in force (unchanged by this task)
- `docs/00-overview/MVP_SCOPE.md` §1, §4 — standalone web client is the identity/frontend path; Discord is unlisted
- `docs/AGENTS.md` §2, §4, §17; `AGENTS.md` §2, §4, §16, §17, §19, §20 — hierarchy (technical docs outrank ADR), conflict handling, documentation-change rule, task discipline
- `tasks/completed/TASK-237-retire-dormant-backend-discord-identity-seam.md` — the completed seam removal and its reported out-of-scope residue (`ARCHITECTURE.md` version-history wording)
- `tasks/completed/TASK-235-reconcile-standalone-web-authentication-and-session-documentation.md` §"Out-of-Scope Observations" — the recorded deferral of exactly this residue, with the reason it was left in place
- `tasks/completed/TASK-236-resolve-backend-discord-identity-seam-architecture-decision.md` §"Reported, not fixed here" — the §2.2 Discord-Activity residue identified before this task existed

---

## Scope

### In Scope

1. **`ARCHITECTURE.md` §2.2 frontend architecture diagram and prose** — the diagram's top node (currently `Discord Activity`) and, only as far as the current code and `ADR-020` support, the platform-boundary wording of item 1.
2. **`ARCHITECTURE.md` §2.2 item 3 service boundaries** — the `services/discord/` bullet, which describes a directory that does not exist in the repository (`ADR-020` D1 item 2 retired it). `services/realtime/` and `services/api/` are verified present and stay.
3. **`ARCHITECTURE.md` §2.2 item 3 rule sentence** — the "Discord SDK and SignalR transport details" wording, corrected to the transports that actually exist.
4. **`ARCHITECTURE.md` §2.2.2 viewport diagram** — the `Discord Activity / Browser viewport` node label.
5. **`ARCHITECTURE.md` §2.3 item 1** — the parenthetical claiming physical deletion of the backend seam is still deferred; TASK-237 executed that deletion, so the statement is now factually false in the same sentence that cites `ADR-023`.
6. **`ARCHITECTURE.md` version header** — record this reconciliation and the completed TASK-237 seam removal, following the document's existing version-note convention.
7. **`.ai/skills/client/react-phaser-boundary/SKILL.md`** — remove the three Discord Activity SDK / lifecycle ownership claims (summary quotation, Purpose item 1, rule 1 "React owns" list) and replace them with the standalone-web shell boundary the code implements, without restating any `docs/` rule (`AGENTS.md` §19, `.ai/README.md` §6).

### Out of Scope

- Any source code, test, configuration, migration, or ADR change — this task's declared file set contains two Markdown files and nothing else.
- Every other `ADR-020`/`ADR-023` residue named in TASK-237's and TASK-235's reported-not-fixed lists (see Implementation Notes).
- Broad keyword replacement of "Discord" anywhere. The repository contains provably-still-true negative assertions and boundary statements that must survive (see the preserve list in Implementation Notes).
- Rewriting the game architecture, the Phaser ownership model, the layer direction, the scene lifecycle, the viewport/scaling rules, or any §1/§2.1/§2.2.1/§2.2.3/§3/§4/§5 content.
- Inventing new service directories, APIs, responsibilities, modules, or boundaries.
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` and its index row (an accepted historical ADR — immutable; `ADR-020`'s `Amends` header already records the amendment).
- `docs/03-decisions/ADR/ADR-015` D1/D6's `POST /api/auth/discord` references (already reported by TASK-237 and TASK-235 as an immutable-ADR `§4` matter, not this task's).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.
- Creating any follow-up task.

---

## Current State

`ADR-020` (Accepted) retired the Discord Activity platform target, and `ADR-023` (Accepted) retired the dormant backend seam, which `TASK-237` then physically deleted in Phase A commit `1cdcd398ad4dc9d4ebc2854d9aecf0b1d5b48c4a`. `TASK-235` had already reconciled `TDD.md` §2.1, `GameRuntimeState.ts`, `GameConfig.ts`, `GameViewport.ts`, `App.css`, and `ARCHITECTURE.md` §2.2.1 to the standalone web client, but deliberately left `ARCHITECTURE.md` §2.2/§2.2.2/§2.3 and `.ai/skills/client/react-phaser-boundary/SKILL.md` untouched because they were outside its declared file set and its authorized §2.2.1-only ARCHITECTURE change.

The result is a live contradiction inside `ARCHITECTURE.md`: §2.3 item 1 and the version preamble state the Discord SDK, Discord Activity dependencies, and the backend seam are retired, while §2.2's diagram still makes `Discord Activity` the platform above the React shell, §2.2 item 3 still describes a `services/discord/` directory that does not exist, and §2.2.2's viewport diagram still labels the viewport `Discord Activity / Browser`. `AGENTS.md` §2 ranks technical docs above ADR, and `docs/AGENTS.md` §4 forbids silently leaving such a conflict; the correction direction is fixed by the accepted ADRs and is not a new decision.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-238 (2 files — both edits):

```text
docs/02-technical/ARCHITECTURE.md
.ai/skills/client/react-phaser-boundary/SKILL.md
```

Evidence per declared path:

| Path | Action | Evidence |
|---|---|---|
| `docs/02-technical/ARCHITECTURE.md` | Edit | Carries all verified stale references: `:200` (platform node), `:216` (Discord Activity SDK lifecycle), `:224` (`services/discord/`), `:228` ("Discord SDK" transport rule), `:614` (`Discord Activity / Browser viewport`), `:683` (seam deletion "deferred to downstream implementation work", contradicted by `TASK-237`'s committed deletion), and `:3-11` (version preamble). No other document contains the frontend architecture diagram or viewport diagram this task corrects. |
| `.ai/skills/client/react-phaser-boundary/SKILL.md` | Edit | The only `.ai/` file in the repository containing a Discord reference (repo-wide grep: 3 matches, all in this file — `:12`, `:19`, `:72`). All three assert React owns the "Discord Activity SDK"/"Discord Activity lifecycle", which `ADR-020` D1 retired and `src/frontend/client/src/` does not implement (no `@discord/embedded-app-sdk` in `package.json`, no `services/discord/` directory). |

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

- [x] `ARCHITECTURE.md` §2.2 no longer presents `Discord Activity` as the frontend hosting target: the §2.2 diagram's top node and item 1's platform-boundary wording describe the standalone web browser client (`ADR-020` D1, D5), and no `ADR-007`/`ADR-013` Discord boundary is asserted as current.
- [x] `ARCHITECTURE.md` §2.2 item 3's service-boundary list matches the directories that exist under `src/frontend/client/src/services/`: the `services/discord/` bullet is removed or reconciled to `ADR-020` D1 item 2, and `services/realtime/` and `services/api/` remain described exactly as before.
- [x] `ARCHITECTURE.md` §2.2 item 3's rule sentence names only transports the client actually has (`services/realtime/` and `services/api/`) and no retired Discord SDK.
- [x] `ARCHITECTURE.md` §2.2.2's viewport diagram labels the viewport for the standalone web client; §2.2.2's six rules and the `GameViewport.ts` prose are otherwise unmodified.
- [x] No statement in `ARCHITECTURE.md` claims `TASK-237`'s backend seam removal is still pending: §2.3 item 1 and the version header accurately state that the seam is retired **and** its C# files, DI registration, and test compatibility paths were physically deleted by `TASK-237` (Phase A commit `1cdcd398ad4dc9d4ebc2854d9aecf0b1d5b48c4a`).
- [x] The `ARCHITECTURE.md` version header records this reconciliation following the document's existing `**Version:** N (… Prior X: …)` convention, naming the corrected sections and stating that no module boundary, layer direction, port capability, endpoint, wire member, state model, Redis key, database column, gameplay rule, or contract changes; the historical prior-version notes are not deleted or reworded.
- [x] `.ai/skills/client/react-phaser-boundary/SKILL.md` no longer states that React owns a Discord Activity SDK or its lifecycle: the summary quotation, Purpose item 1, and rule 1's "React owns" list describe the standalone-web shell ownership the code implements (`App.tsx` `AuthScreen`/`GameShell` switch, `GameShell.tsx` viewport, `ui/components/` overlays, `PhaserGame.tsx` single canvas mount).
- [x] The skill's Authoritative Sources and Traceability sections cite `ADR-020` (and, where it already cites it, `ADR-003` as amended) so the skill and `docs/` agree; the skill restates no game rule, contract value, or architectural rule that a `docs/` document owns (`.ai/README.md` §6, `AGENTS.md` §19).
- [x] All changes agree with `ADR-015`, `ADR-020`, `ADR-023`, `ARCHITECTURE.md`'s own §1 client tree, `TDD.md` §2.1, and the `docs/` > `.ai/` > task precedence order (`AGENTS.md` §2).
- [x] Historical ADRs (`docs/03-decisions/ADR/`), `docs/03-decisions/README.md`, EF migrations, and every completed task record are byte-identical to their pre-task state (`git diff` shows no change under those paths).
- [x] No source code, configuration, migration, test, or executable behavior changes: `git diff --stat` for this task's Phase A slice lists exactly the two declared Markdown files, and no path under `src/` or `tests/` appears.
- [x] The preserve list in Implementation Notes is intact — in particular `src/frontend/client/tests/LobbyScene.test.ts:1682–1700`'s `forbidden` array, `src/frontend/client/tests/CollectionViewerScene.test.ts:1256–1258`, `src/frontend/client/tests/RuntimeBoundaries.test.ts:84–85`, `src/frontend/client/tests/BattleService.test.ts:302/790/1062`, `src/frontend/client/tests/SignalRService.test.ts:794`, `src/frontend/client/src/services/api/BattleModels.ts:66`, `docs/02-technical/ARCHITECTURE.md:8` (prior-version `DiscordService` note), and the explanatory references in `TASK-235`/`TASK-236`/`TASK-237`.
- [x] Required documentation consistency and architecture-conformance checks pass (`quality/documentation-consistency`, `quality/architecture-conformance`, `quality/review.md` §1): a re-scan of both declared files finds no surviving claim of a current Discord Activity platform, Discord SDK ownership, or pending seam deletion, and every remaining "Discord" occurrence is an explicitly historical, decision-record, or still-true-negative reference (each one enumerated in the Completion Evidence).
- [x] Zero-regression suites pass unchanged from the pre-task baseline: `npm run test:run` in `src/frontend/client` and `dotnet test src/backend/GameServer.sln` (run only to prove no executable behavior changed, per `documentation/documentation-change.md` §4).
- [x] The staged file set contains only the two declared files (`TASK_LIFECYCLE.md` §6 P-5), and the two-stage commit protocol is followed: Phase A commits exactly the declared file set with `owns: TASK-238` in the body and no task ID in the subject; Phase B files this record under its own commit.
- [x] The completion report records the actual verification results, the observed test counts, and the commit evidence (Phase A SHA, exact changed paths).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios)
[x] docs/ (ARCHITECTURE.md §2.2, §2.2.2, §2.3, version header only)
[x] .ai/ (skills/client/react-phaser-boundary/SKILL.md only)
```

---

## Implementation Notes

### Verified stale references (each inspected in context, not inferred from the audit)

| # | Path:line | Current text (abridged) | Why it is stale | Supported correction |
|---|---|---|---|---|
| 1 | `ARCHITECTURE.md:200` | `Discord Activity` (top node of the §2.2 diagram) | `ADR-020` D1/D5: platform target is a standalone browser; no Discord iframe exists | Standalone Web Browser (the hosting surface), with React + Vite below it as the shell — the diagram's remaining nodes (`React + Vite`, `Phaser 4`, `Services`, `Backend`) stay |
| 2 | `ARCHITECTURE.md:216` | item 1: React "manages … and Discord Activity SDK lifecycle" | `ADR-020` D1 item 1 removed `@discord/embedded-app-sdk` (absent from `package.json`); the shell has no platform SDK lifecycle | Drop the SDK clause; keep the verified responsibilities (canvas mount, HTML overlays, menus, settings, auth-session presentation via `App.tsx`) |
| 3 | `ARCHITECTURE.md:224` | item 3: "`services/discord/` isolates Discord Embedded App SDK interactions" | The directory does not exist (`Test-Path src/frontend/client/src/services/discord` = false); `ADR-020` D1 item 2 retired it | Remove the bullet. Do **not** substitute a new folder — `services/api/` and `services/realtime/` are the only two, per the actual tree and `TDD.md` §2.1 |
| 4 | `ARCHITECTURE.md:228` | rule: "Discord SDK and SignalR transport details must never leak…" | Names a retired SDK; the rule's real content (transport isolation) is already owned by §2.2.1 rule 1 for both transports | Reword to the transports that exist (`services/realtime/` and `services/api/`), preserving the rule's intent |
| 5 | `ARCHITECTURE.md:614` | `Discord Activity / Browser viewport` (§2.2.2 diagram node) | Same as #1 | Standalone web browser viewport |
| 6 | `ARCHITECTURE.md:683` | §2.3 item 1: "…seam is formally retired per ADR-023 (physical deletion of C# source files, DI registrations, and test stubs **deferred to downstream implementation work**)" | `TASK-237` executed that deletion — Phase A commit `1cdcd39` deleted 5 C# files, the DI registrations, and `TestDiscordCredentials.cs`. The clause is false in the present tense | State the seam is retired **and** physically removed by `TASK-237`; keep `ADR-023` as the decision source |
| 7 | `ARCHITECTURE.md:3-11` | version preamble: "…with physical code and test cleanup deferred to downstream implementation work" | Same as #6 | Append a new version note recording the §2.2/§2.2.2/§2.3 reconciliation and the completed seam removal; leave the historical notes intact |
| 8 | `SKILL.md:12` | summary line: "…overlays, Discord Activity SDK)…" | `ADR-020` D1; no such SDK in the client | Describe the standalone-web shell |
| 9 | `SKILL.md:19` | Purpose 1: "React owns the DOM UI, Discord Activity SDK, and application routing." | Same | Replace the SDK clause with the ownership the code implements (auth-session presentation, app shell/routing) |
| 10 | `SKILL.md:72` | rule 1: "**React owns:** Discord Activity lifecycle, Lobby UI, …" | Same | Drop the lifecycle clause; keep the verified ownership list |

### The §2.2 render diagram and the "Layers & Dependency Direction" reading

The §2.2 block is titled "Frontend Layers (Phaser-First with React Shell)" and its top node names the **platform/hosting surface** the React shell runs inside — not a dependency edge. Correcting the label therefore preserves the section's layer semantics; do not reorder the remaining nodes, and do not touch §2.1 (backend layers) or the `Domain ◀ Application ◀ Infrastructure ◀ Api` direction.

### Explicit preserve list (do not modify — these are correct as written)

```text
src/frontend/client/tests/LobbyScene.test.ts:1682-1700
    it('imports no transport client and no Discord SDK') and its `forbidden`
    array. Executable test logic. A NEGATIVE assertion that DiscordService /
    services/discord / embedded-app-sdk are absent — it remains true and is
    the enforcement of ADR-020 D1. TASK-235 was forbidden from editing it and
    this task is too.
src/frontend/client/tests/CollectionViewerScene.test.ts:1256-1258  — same negative-assertion pattern
src/frontend/client/tests/RuntimeBoundaries.test.ts:84-85          — same negative-assertion pattern
src/frontend/client/tests/BattleService.test.ts:302,790,1062       — still-true negative assertions (no discordUserId on the wire)
src/frontend/client/tests/SignalRService.test.ts:794               — as above
src/frontend/client/src/services/api/BattleModels.ts:66            — "deliberately no playerId, no discordUserId" — still true (ADR-020 D2)
src/frontend/client/src/game/scenes/LobbyScene.ts:339              — boundary statement mirroring the protected forbidden array (TASK-235 kept it deliberately)
docs/02-technical/ARCHITECTURE.md:8                                — the prior-version note that RECORDS the 1.10 change ("removes the retired DiscordService row"), which must not be erased
src/backend/GameServer.Api/Program.cs:57-58                        — *.discordsays.com CORS allowance: reported by TASK-237 as outside its blast radius; NOT authorized by this task's declared set
docs/03-decisions/ADR/ADR-003, ADR-007, ADR-013, ADR-019           — immutable historical ADRs
docs/03-decisions/README.md:213,217,223,229-233                    — ADR index rows and ADR-023's own "deferred" wording (immutable decision-record text)
docs/02-technical/ARCHITECTURE.md:8-11, 23-84                      — historical version notes
```

### Reported, not fixed here (`AGENTS.md` §16)

The following are confirmed stale-or-ambiguous but are outside this task's two-file declared set. Report them in the completion record; do **not** edit them and do **not** create follow-up tasks.

- `src/backend/.env.example:46-80` still sets `DevelopmentAuthentication__Enabled=true` and documents the retired `POST /api/auth/discord` path, and `src/backend/GameServer.Api/appsettings.json:20-23` keeps an inert `"Discord"` section. Configuration — excluded by this task's type and by its exclusions.
- `src/backend/GameServer.Api/Program.cs:57-58`'s `*.discordsays.com` CORS allowance and `PlayerRepository.GetOrCreateByDiscordUserIdAsync` (`:102`). Configuration and production C# — excluded.
- `docs/03-decisions/ADR/ADR-003`'s title/body and `docs/03-decisions/README.md:213` still say "Discord Activity"; `ADR-020`'s `Amends` header and the index's own status column record the amendment. ADRs are immutable and this is a `docs/AGENTS.md` §4 reconciliation question, not a silent edit.
- `ADR-015` D1 (`:41`) and D6 (`:94-95`) still name `POST /api/auth/discord`; already reported by TASK-237 and TASK-235 under the same immutability rule.
- `TDD.md:278`'s single Discord mention is a correct historical statement ("replaces Discord Activity OAuth") and needs no change.
- `ADR-023` §2 item 3 and §4's "deferred to future implementation work" wording is now historical. An accepted ADR is immutable; the accurate present-tense statement belongs in `ARCHITECTURE.md`, which is exactly what criterion 5 requires. If the repository later wants the ADR's own wording reconciled, that is a separate `docs/AGENTS.md` §4 decision for a human — not this task.
- `src/frontend/client/package-lock.json:11` still lists `@discord/embedded-app-sdk` as a stale lockfile entry while `package.json` does not declare it. A dependency/lockfile change is configuration and is excluded.

### Audit cross-reference IDs — not found in the repository

The intake request references audit findings `F-03`, `F-04`, `F-05`, and `F-06` and requires that they be excluded "unless repository evidence proves that they belong in this exact documentation task." A repository-wide search of every `*.md`, `*.json`, `*.cs`, and `*.ts` file returns **zero** occurrences of any `F-0N` identifier, so:
1. The identifiers cannot be resolved to specific findings from the repository alone, and
2. By the stated condition, they are therefore excluded.

The findings this task *does* act on are reconstructible from repository evidence and are enumerated in the table above; the residual findings named in TASK-212 §A-20, TASK-217A §10 (R-1…R-10), TASK-235, and TASK-237 are covered by the exclusions and the reported-not-fixed list. If the requester can point to the audit document that defines `F-03`…`F-06`, the correct handling is a separate task — not an expansion of this two-file set.

### Terminology precision

Use **"standalone web browser"** or **"standalone web client"** for the hosting surface, matching `ADR-020` D5, `ROADMAP.md` §"Phase 3 — Standalone Web Polish", `TDD.md:74`, and `ARCHITECTURE.md` §2.3's existing title. Do not introduce a new platform term.

---

## Testing Requirements

### Required Verification
```text
[x] Documentation validation — every corrected claim re-read against ADR-020, ADR-015,
                               ADR-023, ARCHITECTURE.md §1's client tree, and TDD.md §2.1
[x] Architecture conformance  — confirm no module boundary, layer direction, port capability,
                               endpoint, wire member, state model, Redis key, database column,
                               or gameplay rule is altered by the wording change
[x] Zero-regression suites     — run both suites solely to prove executable behavior is unchanged
                               (documentation/documentation-change.md §4: a pure documentation
                               change skips the code-only review items, not the suites'
                               zero-regression check performed by TASK-235's precedent)
[ ] Unit tests                 — N/A: this task adds and changes no test
[ ] Integration tests          — N/A
[ ] Gameplay scenarios         — N/A: no game rule, event, state, or wire contract changes
```

### Commands (repository's actual commands — do not invent new scripts)

```text
npm run test:run      (working directory: src/frontend/client)
dotnet test src/backend/GameServer.sln
```

Both must report the same counts as the pre-task baseline. TASK-235 recorded the baseline as **933** frontend tests and **2,970** backend tests (Domain 1,558 / Application 653 / Infrastructure 422 / Api 337), all passing; re-measure the baseline before editing rather than assuming those numbers.

### Required documentation checks

```text
[x] Re-scan both declared files for: "Discord Activity", "services/discord",
    "DiscordService", "Embedded App SDK", "embedded-app-sdk", "discordsays",
    "deferred to downstream implementation work" — every surviving occurrence must be
    an enumerated historical/decision-record/negative-assertion reference, and the
    completion record must list each one with its justification.
[x] Verify the §2.2 service list against `Get-ChildItem src/frontend/client/src/services`
    and against `TDD.md` §2.1 — the two must agree on `api/` and `realtime/` only.
[x] Verify no cross-reference numbering changed: any `§` citation into ARCHITECTURE.md
    from other documents still resolves (this task renumbers no section and adds none).
[x] Verify the version header's new note names every section actually changed.
```

### Key Edge Cases

- **Do not erase "Discord" where it is the record.** `ARCHITECTURE.md:8`'s prior-version note recording the 1.10 `DiscordService` removal, and §2.3's statement that Discord Activity OAuth was *replaced*, are historical/contrastive statements that must survive. The test for editing is: does the sentence assert Discord is **currently** the platform, the SDK owner, a present service directory, or a **pending** deletion? If no, leave it.
- **`Discord Activity / Browser` is a compound label.** §2.2.2's node listed both because the viewport was shared; only the Discord half is retired. Keep the browser semantics.
- **The `forbidden` test array is runtime-enforced documentation.** Editing it to "clean up Discord references" would change executable test logic and is prohibited — it is also the only automated guard proving `ADR-020` D1 holds.
- **`ADR-023`'s deferral wording is not the same claim as `ARCHITECTURE.md`'s.** The ADR records what was true when it was accepted and is immutable; `ARCHITECTURE.md` describes the current system and must not repeat a superseded status. Correction belongs in the technical document only.
- **Skill files are not a second source of truth.** Rewrite the ownership list to match `docs/`; do not add new architectural explanation, examples, or rules that `ARCHITECTURE.md`/`TDD.md` do not contain (`.ai/README.md` §4, §6, §9).
- **Skill budget.** Four skills, inside the 3–5 "Normal Task" band and well under the hard limit of 7 (`tasks/README.md` §12).

---

## Stop Conditions

- If a file outside `## Declared File Set (P-2 / P-5)` requires a change to complete the reconciliation: STOP per `TASK_LIFECYCLE.md` §6.1 step 2 / P-5 — report the divergence; do not silently expand the declared set.
- If `ADR-020` or `ADR-023` is not `Accepted`, or `TASK-237` is not `DONE` with its Phase A deletion commit present: STOP — the correction is unauthorized.
- If correcting a stale reference would require choosing between two authoritative `docs/` documents that disagree, or would require overriding `ARCHITECTURE.md` with an ADR: STOP per `AGENTS.md` §4 (`docs/AGENTS.md` §2 ranks technical docs above ADR) and report both sides.
- If a correct wording cannot be determined from the current code and the authoritative ADRs — i.e. the replacement would have to be invented rather than derived — STOP per `AGENTS.md` §20 (ambiguous requirement) and report it.
- If the reconciliation appears to need a new service directory, module, port capability, endpoint, or boundary to be described: STOP — that is an architecture change, not a documentation correction (`AGENTS.md` §17, §18).
- If any edit would touch executable test logic, configuration, a migration, an ADR, or a completed task record: STOP — excluded by this manifest's declared set and by `AGENTS.md` §16.
- If the work splits into more than one independently verifiable slice: STOP and report rather than expanding scope.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Commit
- Implementation slice (Phase A): `3d2b4b504a912f312937de232c521117e4e1ba9c`
- Subject: `docs: reconcile the frontend architecture sections with the standalone web client`
- Owns: `TASK-238`
- Files: `docs/02-technical/ARCHITECTURE.md, .ai/skills/client/react-phaser-boundary/SKILL.md`
- Shared with: `none`
- Unowned / pre-existing: `none`
- Staging verification (P-5): `git diff --cached --name-status` showed exactly 2 `M` entries — `.ai/skills/client/react-phaser-boundary/SKILL.md` and `docs/02-technical/ARCHITECTURE.md` — with zero extraneous files and the task record excluded.

### Changed Files
- `docs/02-technical/ARCHITECTURE.md` — version header (new 1.11 note appended; the 1.10 note is preserved byte-identical), §2.2 diagram top node (`Discord Activity` → `Standalone Web Browser`), §2.2 diagram services node (`Services (discord/ · realtime/ · api/)` → `Services (realtime/ · api/)`), §2.2 item 1 (the Discord Activity SDK lifecycle clause is dropped; the standalone browser hosting surface and the `AuthScreen`/`GameShell` presentation are stated with the `ADR-020` D5 citation), §2.2 item 3 (the `services/discord/` bullet is removed; the isolation rule now names the SignalR and REST transports the client actually has), §2.2.2 viewport diagram node (`Discord Activity / Browser viewport` → `Standalone web browser viewport`), §2.3 item 1 (the seam deletion is no longer described as deferred; it records the physical deletion by `TASK-237`). §1, §2.1, §2.2.1, §2.2.3, §3, §4, §4.1, and §5 are untouched.
- `.ai/skills/client/react-phaser-boundary/SKILL.md` — the summary quotation, Purpose item 1, and rule 1's "React owns" list no longer claim a Discord Activity SDK or its lifecycle; they describe the shell ownership the code implements (`App.tsx`'s `AuthScreen`/`GameShell` switch). Authoritative Sources and Traceability now cite `ADR-020*`. No other guidance was changed.

### Validation Results
- `npm run test:run` (baseline, pre-change) — PASS: 933 passed / 0 failed, 23 files
- `npm run test:run` (post-change) — PASS: 933 passed / 0 failed, 23 files (identical to baseline)
- `dotnet test src/backend/GameServer.sln` (baseline, pre-change) — PASS: 2,970 passed / 0 failed / 0 skipped (Domain 1,558 / Application 653 / Infrastructure 422 / Api 337)
- `dotnet test src/backend/GameServer.sln` (post-change) — PASS: 2,970 passed / 0 failed / 0 skipped (Domain 1,558 / Application 653 / Infrastructure 422 / Api 337), identical to baseline
- Stale-reference re-scan of both declared files — every surviving occurrence, with its classification:
  1. `ARCHITECTURE.md:6-7` (new 1.11 note) — historical: records that the retired Discord Activity iframe, Embedded App SDK lifecycle, and the nonexistent `services/discord/` directory are **no longer** described as current.
  2. `ARCHITECTURE.md:10` (new 1.11 note) — historical/decision-record: the `ADR-023` seam was physically deleted by `TASK-237`.
  3. `ARCHITECTURE.md:12-13` (new 1.11 note) — decision-record: marks the 1.10 status as superseded ("superseding the "deferred to / downstream implementation work" status recorded in 1.10 below"); the phrase is quoted in order to supersede it, not asserted.
  4. `ARCHITECTURE.md:17,19,21` (the preserved 1.10 prior-version note) — historical prior-version record describing the TASK-236 decision as it stood at version 1.10. The Implementation Notes preserve list forbids rewording it, and the 1.11 note directly above it supersedes the deferral, so it cannot be read as a current-state claim.
  5. `ARCHITECTURE.md:697` (§2.3 item 1) — contrastive + still-true negative + current state: "replaces the former Discord Activity OAuth boundary" (historical contrast), "Discord SDK and Discord Activity dependencies are retired" (still-true negative assertion), and the physical-deletion statement (accurate current state).
  6. `SKILL.md` — zero occurrences of `Discord`, `discord`, or `SDK` remain after the correction.
  - No occurrence of `discordsays`, `embedded-app-sdk`, `@discord/embedded-app-sdk`, or `services/discord/` as a *current* directory remains in either declared file.
- Service-directory reconciliation — `Get-ChildItem src/frontend/client/src/services` returns `api` and `realtime` only; `Test-Path src/frontend/client/src/services/discord` = `False`; §2.2 item 3 now lists exactly `services/realtime/` and `services/api/`; `TDD.md` §2.1's Service Boundaries name the same two. The three agree.
- Cross-reference check — the `#`/`##`/`###` heading set of `ARCHITECTURE.md` is identical before and after (same titles, same order), so every external `§` citation into the file still resolves; no section was renumbered, added, or reordered. The new version note names every section actually changed (§2.2, §2.2.2, §2.3).
- Documentation consistency (`quality/documentation-consistency`, modes A/C) — no duplicate definition was introduced: §2.2 item 1 cites `ADR-020` D5 and §2.3 for the shell split rather than restating the authentication contract, and the skill cites its owner documents without restating any rule, value, or contract (`.ai/README.md` §6, `AGENTS.md` §19). No stale reference remains in either file.
- Architecture conformance (`quality/architecture-conformance`) — `Conforms`: no layer, module, or dependency-direction change (`ARCHITECTURE.md` §2.1's direction is untouched); no responsibility moved across a boundary (`AGENTS.md` §13); no interface, factory, bus, or module added (`AGENTS.md` §9); consistent with the Accepted `ADR-020` D1/D4/D5 and `ADR-023` §2 item 3; this is a documentation correction, not an ADR-worthy architecture change.
- `quality/review.md` §1 — PASS with no blocking finding: Correctness (both files now agree with the Accepted ADRs and the actual repository structure), Architecture (verdict above), Scope (exactly the two declared files), Tests (N/A — no behavior change; both suites re-run and unchanged), Documentation (this reconciliation), Security (no authentication or session behavior or contract changed), Performance (none), Maintainability (no complexity added), Determinism (no gameplay logic touched).

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic introduced
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
- [x] Confirmed no source, configuration, test, ADR, migration, or completed-record file changed
- [x] Confirmed the preserve list is byte-identical

### Reported, Not Fixed Here — Out-of-Scope Residue (`AGENTS.md` §16)
- `ADR-023` §2 item 3 and §4 still word the physical deletion as "deferred to future implementation work". An Accepted ADR is immutable; the accurate present-tense statement now lives in `ARCHITECTURE.md` §2.3 (acceptance criterion 5). Reconciling the ADR's own wording is a separate `docs/AGENTS.md` §4 decision for a human, not this task's.
- `docs/03-decisions/ADR/ADR-003` (`:74`) still lists `services/discord` among the transport services, and `ADR-007` (`:24`, `:82`) names `services/discord/DiscordService.ts`; `docs/03-decisions/README.md`'s index rows still carry Discord-era text. Immutable historical ADR/index records — reported, not edited.
- `ADR-015` D1 (`:41`) and D6 (`:94-95`) still name `POST /api/auth/discord`; already reported by TASK-235 and TASK-237 under the same immutability rule.
- Configuration and production residue outside this task's declared set and type: `src/backend/GameServer.Api/Program.cs:57-58` (`*.discordsays.com` CORS allowance), `src/backend/.env.example:46-80`, `src/backend/GameServer.Api/appsettings.json:20-23` (inert `"Discord"` section), `PlayerRepository.GetOrCreateByDiscordUserIdAsync`, and `src/frontend/client/package-lock.json:11`'s stale `@discord/embedded-app-sdk` entry.
- `TDD.md:278`'s single Discord mention is a correct historical statement ("replaces Discord Activity OAuth") and required no change.
