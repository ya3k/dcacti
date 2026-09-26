# TASK-042 — Resolve BattleResult Identity and RewardSummary Contract

## Metadata

```text
Task ID:       TASK-042
Type:          DOCUMENTATION
Workflow:      .ai/workflow/documentation/documentation-change.md
Status:        DONE
Priority:      HIGH
Risk:          MEDIUM
Primary Agent: review
Supporting:    backend, persistence, realtime, testing
Dependencies:  None
Blocks:        TASK-041 (must not be edited by this task — see Scope)
Skills:        discovery/documentation-discovery,
               discovery/impact-analysis,
               quality/documentation-consistency,
               backend/persistence-analysis,
               backend/api-contract-validation,
               realtime/realtime-protocol-validation
Estimate:      Complex (documentation-only; two explicit human decision points)
```

**Status note.** `BACKLOG` is correct for file creation. This task must **not**
move to `READY` while the two decision points below are unanswered, because
execution cannot complete without a human answering them; if execution reaches
review with either decision unanswered, mark the task `BLOCKED` per
`tasks/TASK_LIFECYCLE.md` and report the unanswered decision.

---

## 1. Objective

Resolve two documentation contract gaps that currently block **TASK-041
(Implement BattleResult Persistence)** from being implementable without
inventing values:

- **Decision A — Identity carriage.** How the requesting **Player identity**
  and the **active Pet instance identity** are carried from battle creation to
  battle end on the server, so `BattleResult.PlayerId` and
  `BattleResult.PetInstanceId` (`DATABASE.md` §1) can be sourced from
  authoritative server state, and so it is explicit whether that identity is
  part of the serialized active battle record, runtime-only, or carried
  elsewhere.
- **Decision B — RewardSummary shape.** What the concrete JSON member shape of
  `BattleResult.RewardSummary` is, and which document canonically owns that
  shape — or that it is explicitly documented as containing **no line items**
  until TASK-033, without inventing a member, a value, or a placeholder shape.

Documentation-only task. **No source, test, or migration changes.** No game
rule, reward value, XP amount, or new system may be introduced (§7, §8 of
`AGENTS.md`). The answers to Decisions A and B are **human decision points** —
this task makes them explicit, evidenced, and answerable; it must not choose
for the project.

---

## 2. Authoritative References

**Read first (required):**

| # | Document | Why |
|---|---|---|
| 1 | `docs/02-technical/DATABASE.md` §1 | Owns `BattleResult` (incl. `RewardSummary`), `Pet.PetInstanceId`, `PlayerId` FKs |
| 2 | `docs/02-technical/GAME_STATE.md` §2, §2.0–§2.0.5, §2.3 | Owns `BattleState` / `PetState` members and staging positions |
| 3 | `docs/02-technical/API_CONTRACTS.md` §4 | Owns `GET /api/battle/{battleId}/result` response |
| 4 | `docs/02-technical/GAME_EVENTS.md` §2 (`BattleStarted`, `BattleWon`/`BattleLost`) | Owns event payloads, incl. reward summary line |
| 5 | `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.19, §4 | Owns wire schema; reward summary is explicitly not a wire member |
| 6 | `docs/02-technical/REDIS_STATE.md` §1, §2, §3, §7 | Owns the active-state key set, serialization round-trip, TTL/delete |
| 7 | `docs/02-technical/ARCHITECTURE.md` §4 | Battle-end lifecycle (result write, then active-state delete) |
| 8 | `tasks/backlog/TASK-041-implement-battle-result-persistence.md` | The blocked consumer; read-only for this task |
| 9 | `tasks/backlog/TASK-033-player-reward-xp-level-progression.md` | `BLOCKED`; owns reward magnitudes / XP curve — must stay separate |
| 10 | `docs/03-decisions/ADR/ADR-011*` and `ADR-012*` | No `PlayerState` node; reward/XP ownership boundaries |
| 11 | `docs/00-overview/MVP_SCOPE.md` §1, §4 | Scope check (Player Level / battle Rewards are IN; no new system is added here) |
| 12 | `docs/01-game-design/PET_RULES.md` §5 | Pet Level derivation; XP source — read only to keep Decision B inside TASK-033's boundary |

**Reference for process:** `tasks/TASK_TEMPLATE.md`,
`tasks/TASK_TYPES.md` (documentation matrix, risk baseline),
`.ai/workflow/documentation/documentation-change.md` (canonical owner, no
duplication), `.ai/skills/SKILL_REGISTRY.md`.

---

## 3. Scope

**In scope**

1. Establish and document the evidence for Decision A (identity carriage) and
   Decision B (RewardSummary shape), each ending in one explicit, human-approved
   answer or a `BLOCKED` report.
2. Update the **canonical owning document only** for each decided answer
   (candidate owners: `DATABASE.md` §1, `GAME_STATE.md` §2/§2.3,
   `API_CONTRACTS.md` §4, `GAME_EVENTS.md` §2) and make dependent references
   consistent where they become stale.
3. Record precisely what TASK-041 needs afterward, as a report item —
   **without editing TASK-041**.
4. Report discovered adjacent issues (§6) without fixing them.

**Out of scope**

- Any `src/`, `tests/`, or migration file change.
- Editing `tasks/backlog/TASK-041-*`, `TASK-033-*`, `TASK-034-*`, `TASK-032-*`.
- Reward values, XP amounts, XP curve, new reward fields, or
  `BattleResult` columns (TASK-033).
- `GET /api/battle/history` (documented gap in `API_CONTRACTS.md` §7),
  `GetBattleState`, Reconnect, Resync, session/auth (TASK-034).
- Redis or `BattleResult` implementation; adding a `Status`/lifecycle field.
- Adding a wire member, delivering `BattleStarted` on the wire, or any other
  protocol change (would be its own task/ADR).
- Creating or amending an ADR — unless the chosen Decision A option requires
  one, in which case **stop** (§8 below, `AGENTS.md` §18).

---

## 4. Current State — Evidence (do not re-derive; verify then cite)

### 4.1 Gap A — identity is required by the persistence contract but carried nowhere

| Fact | Location |
|---|---|
| `BattleResult` requires `PlayerId` and `PetInstanceId` (FK → `Pet`) | `DATABASE.md` §1 (`BattleResult`, `Pet`) |
| `Pet` entity has `PetInstanceId` (PK) and `PlayerId` | `Pet.cs` L59/L66; `DATABASE.md` §1 (`Pet`) |
| `BattleState` has **no** `PlayerId` and **no** `PetInstanceId`/`PetId` member | `GAME_STATE.md` §2 tree L101–131; `src/backend/GameServer.Domain/Battle/BattleState.cs` |
| Serialized record has **no** playerId / petId key | `BattleStateJson.cs` (all `const string` keys: `battleId`, `turn`, `sequence`, `rng*`, `boardState`, `combo`, `matchCount`, `petState`, `bossState`, `lastCommittedSwapPair`, …) |
| `GameServer...BattleStartService.StartAsync(string playerId, …)` uses `playerId` only for the ownership check (`pet.PlayerId == playerId`, L183) and then discards it | `BattleStartService.cs` L160–320 |
| `PetConfiguration` carried per battle holds Element/Passive/Threshold/ResetOverride/loadout input — **no ids**; it lives in an in-process `ConcurrentDictionary` and is documented as "not the authoritative battle state and … never the source of one" | `BattleStateService.cs` L164, L226–243 |
| Redis-only fields are forbidden; the stored record is the single source of truth; nothing permits state in process memory | `REDIS_STATE.md` §2 item 1, §2 item 2, §7 item 5 (as cited in `BattleStateService.cs` L236–241, L268–273) |
| Docs nevertheless reference identities as if present: `PetState.PetId` (event payload), `GAME_STATE.md` §2.3 tree `PetId / Identity`, `SIGNALR_PROTOCOL.md` §4.3 | `GAME_EVENTS.md` §2 `BattleStarted` L212–216; `GAME_STATE.md` §2.3 L865+; `SIGNALR_PROTOCOL.md` §4.3 |
| Implementation states `PetId`/`PetInstanceId`, Identity, Tier/Star/Level are **not yet implemented** | `src/backend/GameServer.Domain/Battle/PetState.cs` (file header) |

**Consequence:** at battle end there is no documented authoritative carrier from
which `BattleResult.PlayerId` / `BattleResult.PetInstanceId` can be written;
`playerId` is in-process at creation only (lost on restart), and the state
record — the only thing that survives to battle end — has no identity members.

### 4.2 Gap B — `RewardSummary` shape is a closed loop of placeholders

| Contract | Statement |
|---|---|
| `DATABASE.md` §1 (`BattleResult`) | `RewardSummary` is JSON **column prose** only: "(JSON — reward line items; may include Player XP granting Player Level — `PET_RULES.md` §5, `GDD.md` §14)". No member is named. |
| `API_CONTRACTS.md` §4 L407 | `"rewards": { "...": "see DATABASE.md for reward data shape" }` — an object whose members are undefined; the endpoint returns it for **any** completed battle (`outcome` `"Won"` \| `"Lost"`, L404–409). |
| `GAME_EVENTS.md` §2 L419–420 | `reward summary (BattleWon only — exact reward data shape: DATABASE.md)` — so rewards on a **Lost** outcome are asserted here but not by §4. |
| `SIGNALR_PROTOCOL.md` §3.2.19 note 3 L847–848 | "`reward summary` is deferred … not a wire member yet. The data shape is owned by `DATABASE.md`." |
| `TASK-041` L89–90, L245 | Stores `RewardSummary` as JSON with **no line items** until that task exists; "no reward field, value, or shape is invented here"; open question whether API §4 `rewards` is satisfied. |
| `TASK-033` | `BLOCKED`; owns reward magnitudes, XP curve, XP persistence (ADR-012 item 2, `PET_RULES.md` §5(2)/(6), `MVP_SCOPE.md` §1 Player). |

**Consequence:** every document points at another document for the shape; none
defines it. TASK-041 cannot write a truthful value, and `{}` may or may not be
an invented placeholder — this must be a human decision, not an implementation
default.

---

## 5. Decision Points (the deliverable)

### Decision A — Identity carriage for `PlayerId` and `PetInstanceId`

**Questions to answer explicitly:**

- **A1.** Which authoritative server carrier provides `BattleResult.PlayerId`
  at battle end, and which provides `BattleResult.PetInstanceId`?
- **A2.** Is that carrier (a) members serialized inside the stored battle
  record (`battle:{battleId}:state`), (b) runtime-only state outside the
  record, or (c) an external documented carrier keyed by `BattleId`? State the
  answer per identity — they may differ.
- **A3.** Which identity does `PetState`'s `PetId / Identity` staging entry
  denote: the **instance** (`Pet.PetInstanceId`, what `BattleResult` needs) or
  a **definition** id? Name it precisely and keep it consistent across
  `GAME_STATE.md` §2.3, `GAME_EVENTS.md` §2, `SIGNALR_PROTOCOL.md` §4.3.

**Options (evaluate against the listed criteria; choose one or report blocked):**

- **A-1 — In-record:** add identity member(s) to the state contract
  (`PlayerId` at `BattleState` root and/or `PetInstanceId` under `PetState`),
  serialized in the record.
  *Must check:* `GAME_STATE.md` §2 ownership vs §2.0.x staging lists (adding a
  member is a staged-position change, not a scope reduction); `REDIS_STATE.md`
  §2 round-trip losslessness (a record that drops the member does not
  round-trip) and §7 boundaries; `SIGNALR_PROTOCOL.md` §4 — adding state does
  **not** by itself add a wire member, so client exposure must be stated
  explicitly; duplicate-value rule (`GAME_STATE.md` §0 item 5 — no second copy
  of a value the record already owns).
- **A-2 — Runtime-only:** retain identity alongside the existing in-process
  `BattleStateService._petConfiguration` / `_bossConfiguration` registries.
  *Must check:* `REDIS_STATE.md` §7 item 5 (nothing permits state to live in
  process memory), restart/abandon behaviour (`REDIS_STATE.md` §3 TTL), and
  that identity is never read from the client (`AGENTS.md` §10, ADR-001).
- **A-3 — External carrier:** a documented battle-creation/lookup record keyed
  by `BattleId` supplies the identities at battle end.
  *Must check:* `ARCHITECTURE.md` §4 battle-end lifecycle and layer ownership
  (`ARCHITECTURE.md` §3), `TDD.md` §2/§4 (result write must not add a hot-path
  round-trip), and that no new store is implied (adding one would be an
  architecture change → stop per §8).

If any option requires changing the battle-state model, the authoritative
model, module boundaries, or a new store/ADR → **stop and report**
(`AGENTS.md` §18).

### Decision B — `RewardSummary` member shape

**Questions to answer explicitly:**

- **B1.** Which document canonically owns the `RewardSummary` **member list**
  (candidate: `DATABASE.md` §1, per `SIGNALR_PROTOCOL.md` §3.2.19 note 3), and
  what is that member list?
- **B2.** Until TASK-033 exists, is the value an explicitly documented empty
  object with a staging rule, an explicitly documented absence, or a defined
  set of members? **`{}` must not be introduced as an unexamined default** —
  if that is the chosen answer, the human must approve it as the documented
  staging value.
- **B3.** Is `rewards` present on a **Lost** outcome? (`API_CONTRACTS.md` §4
  returns it for `outcome: "Won" | "Lost"`; `GAME_EVENTS.md` §2 says reward
  summary is BattleWon only.) Whichever document is wrong must be corrected by
  its owner.
- **B4.** What does `API_CONTRACTS.md` §4 L407 become — a real shape, a
  documented empty/staging value, or a deliberate indirection retained as-is?

**Options:**

- **B-1** — Document `RewardSummary` in `DATABASE.md` §1 as an explicitly
  empty/no-line-items JSON value with a staging rule pointing at TASK-033, and
  align `API_CONTRACTS.md` §4 and `GAME_EVENTS.md` §2 to it.
- **B-2** — Document the shape as not-yet-defined and mark §4's `rewards` as
  deferred/absent until TASK-033 (this changes the REST response contract →
  requires explicit human approval).
- **B-3** — Define concrete line-item members now. **STOP**: member semantics
  (XP line items) are TASK-033's `BLOCKED` design inputs (ADR-012 item 2,
  `PET_RULES.md` §5, `MVP_SCOPE.md` §1) — choosing them here would invent game
  rules (§7) and duplicate TASK-033 (§16).

**Do not decide unilaterally.** If the shape cannot be derived from
authoritative documents without inventing semantics, report `BLOCKED` and name
the exact question for the human.

---

## 6. Discovered Adjacent Issues — report only, do not fix

1. **`SIGNALR_PROTOCOL.md` §3.2.19 note 1 (L837–838) and note 3 (L847)**
   cite "`GAME_EVENTS.md` §2 item 9" for the names `"victory"`/`"defeat"` and
   for the reward deferral. `GAME_EVENTS.md` §2 has no numbered item 9 and
   never uses `"victory"`/`"defeat"` (they appear only in
   `SIGNALR_PROTOCOL.md`). Stale/incorrect cross-reference; also relevant: the
   wire vocabulary (`victory`/`defeat`) differs from `DATABASE.md` /
   `API_CONTRACTS.md` §4 (`Won`/`Lost`). Impact: TASK-041 derives `Outcome`
   from events, so a wrong cross-reference invites the wrong mapping.
   Suggested follow-up: a small documentation-consistency task.
2. **`GAME_EVENTS.md` §2 `BattleStarted` (L212–216) declares payload
   `PetId`**, but `BattleStarted` has no entry in the `SIGNALR_PROTOCOL.md`
   §3.2 wire schema. Not resolvable here (adding a wire member is a protocol
   change). Relevant to Decision A as an event-side identity carrier that does
   not yet exist on the wire.

---

## 7. Preserved Decisions (must remain true after this task)

1. **No `Status`/lifecycle field** on `BattleState` — `GAME_STATE.md` §2.0.3
   items 1–3, and `SIGNALR_PROTOCOL.md` §8 item 3; battle outcome stays
   expressed as the `BattleWon` / `BattleLost` **events**.
2. **Server-authoritative identities** — identity is sourced from server state
   only, never authored or supplied by the client (`GAME_RULES.md` §18,
   ADR-001, `AGENTS.md` §10).
3. **No `PlayerState` node** — ADR-011; the Player is the account/owner with
   no battle-time combat pool. `finalPlayerHp` / `source="player"` remain
   fixed protocol labels.
4. **Battle-end ordering** — result write precedes active-state delete
   (`ARCHITECTURE.md` §4 item 4 as relied on by TASK-041; `REDIS_STATE.md`
   §3).
5. **`BattleResult` field set stays `DATABASE.md` §1** — no new columns; no
   reward/XP columns (TASK-033).
6. **Separation of tasks** — TASK-033 (reward magnitudes/XP), TASK-034
   (session/auth), TASK-032 (regression suite), TASK-040 (status note) are
   untouched; nothing here narrows or reinterprets them.
7. **SignalR wire schema unchanged** — `SIGNALR_PROTOCOL.md` §3.2.19 keeps
   `type`, `outcome`, `finalBossHp`, `finalPlayerHp`; reward summary remains
   not-a-wire-member unless a separate protocol task says otherwise.

---

## 8. Stop Conditions (report `BLOCKED`, do not guess)

- The authoritative carrier for `PlayerId`/`PetInstanceId` cannot be
  determined from the documents, or every option conflicts with another
  document (`AGENTS.md` §4, §20).
- Decision A requires a change to the battle-state model, the authoritative
  model, module boundaries, a new store, or a new/changed ADR
  (`AGENTS.md` §18) — propose an ADR task instead of editing docs.
- Decision B cannot be answered without inventing a reward/XP semantic, a
  value, or an unapproved `{}` (`AGENTS.md` §7, §21).
- `API_CONTRACTS.md` §4, `GAME_EVENTS.md` §2, and `DATABASE.md` §1 cannot be
  made consistent without a decision the human has not made (data-contract
  conflict, `AGENTS.md` §20).
- Any change would contradict `MVP_SCOPE.md`, `PET_RULES.md` §5, ADR-011/012,
  or require editing TASK-041/TASK-033.
- A discovered issue in §6 turns out to be a true cross-document conflict
  rather than a stale pointer → report per §4/§16, do not resolve silently.

---

## 9. Acceptance Criteria

- [x] **A1 resolved:** the authoritative carrier and read site for
      `BattleResult.PlayerId` are documented with file/section citations.
- [x] **A2 resolved:** the authoritative carrier and read site for
      `BattleResult.PetInstanceId` are documented, including whether it is the
      Pet **instance** identity.
- [x] **A3 resolved:** serialization status is explicit — in-record vs
      runtime-only vs external — per identity, with its `REDIS_STATE.md` §2
      round-trip implication named; `GAME_STATE.md` §2.3's `PetId / Identity`
      entry now denotes exactly one precisely named identity.
- [x] If identity enters the state contract: `GAME_STATE.md` (owning section
      only) is updated; staging lists (§2.0.x) are consistent; no value is
      duplicated across documents.
- [x] If identity is **not** a wire member: that is stated where `GAME_STATE.md`
      §2 and `SIGNALR_PROTOCOL.md` §4 are read together, so "state added" is
      not mistaken for "wire exposure added", and the client-exposure boundary
      is explicit.
- [x] **B1 resolved:** `RewardSummary`'s canonical owning document and its
      member list (or documented deferral) are named in one place.
- [x] **B2 resolved:** the staging value/absence until TASK-033 is documented;
      no reward field, value, member, magnitude, XP amount, or curve is
      invented, and `{}` appears only if explicitly human-approved as the
      documented staging value.
- [x] **B3 resolved:** `rewards` presence on a `Lost` outcome is decided and
      `API_CONTRACTS.md` §4 / `GAME_EVENTS.md` §2 agree.
- [x] **B4 resolved:** `API_CONTRACTS.md` §4 L407 is either a real shape, a
      documented staging value, or an intentional indirection — not an
      undefined placeholder loop.
- [x] `SIGNALR_PROTOCOL.md` §3.2.19 note 3 pointer is verified still correct
      after Decision B (not a wire member; owner named correctly).
- [x] TASK-033, TASK-034, TASK-032 ownership is untouched; this task states no
      reward magnitude, XP curve, auth, or regression-suite requirement.
- [x] **No source, test, or migration file is changed**; all edits are
      documentation edits in owning documents plus dependent-reference fixes
      only.
- [x] **TASK-041 is not modified**; the exact re-audit notes TASK-041 needs
      (identity sourcing lines, `RewardSummary` handling, the L245 open
      question) are delivered in the completion report as a separate,
      human-approved follow-up.
- [x] **No unrelated documentation is edited** and no game rule/contract is
      duplicated across documents (`documentation-change.md`); §6 issues are
      reported, not fixed.
- [x] Completion report delivered in the exact format in §11; if either
      decision was unanswered, status is `BLOCKED` naming the unanswered
      decision.

---

## 10. Expected Documentation Changes (only after a decision is approved)

| Decision | Canonical owner (edit here) | Dependent references (fix only if stale) |
|---|---|---|
| A | `docs/02-technical/GAME_STATE.md` (§2 / §2.3 / staging lists) | `REDIS_STATE.md` §2 (serialization completeness), `SIGNALR_PROTOCOL.md` §4 (exposure statement), `DATABASE.md` §1 (FK sourcing note) |
| B | `docs/02-technical/DATABASE.md` §1 (`RewardSummary`) | `API_CONTRACTS.md` §4, `GAME_EVENTS.md` §2, `SIGNALR_PROTOCOL.md` §3.2.19 note 3 |

If a decision's answer requires editing a document **not** listed as its
owner, stop and re-check §8 (`documentation-change.md` §2, canonical owner
rule).

---

## 11. Completion Evidence — required final report format

```text
Task Created          TASK-042 <file path>
Exact Contract Gaps   Gap A (identity carriage) and Gap B (RewardSummary shape)
                      with the file+section evidence for each
Decisions Required    A1 / A2 / A3 / B1 / B2 / B3 / B4 — each with the human's
                      answer, or "UNANSWERED → task BLOCKED"
Authoritative References   documents read, with sections
Documents Expected to Change   owner doc(s) actually edited + dependent fixes
Files Created         the TASK-042 file
Files Modified        list, or "none (BLOCKED before edit)"
Implementation:        None  (documentation-only)
```

---

## 12. Definition of Done (`AGENTS.md` §22)

- [x] Requirement understood; both gaps evidenced with file/section citations
- [x] Relevant docs read (§2 table); scope checked against `MVP_SCOPE.md`
- [x] Existing implementation checked (`BattleState`, `PetState`,
      `BattleStateJson`, `BattleStartService`, `BattleStateService`) — read-only
- [x] Plan created; human decision points made explicit, not auto-answered
- [x] Code implemented — **N/A (documentation-only)**
- [x] Relevant tests added/updated — **N/A** (verification is documentation
      consistency per `quality/review.md` §1 documentation subset)
- [x] Tests pass — **N/A**; consistency checks recorded instead
- [x] No unrelated behavior or documentation changed (§16)
- [x] Documentation updated only where a decision was approved (§17)
- [x] No source-of-truth conflict introduced (§4); §6 issues reported, not fixed
