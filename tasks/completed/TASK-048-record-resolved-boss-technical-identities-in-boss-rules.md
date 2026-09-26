# TASK-048 — Record Resolved Boss Technical Identities in BOSS_RULES §6.4

---

## Metadata

```text
Task ID:           TASK-048
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          HIGH (P1 — clears the precondition for a fresh
                   TASK-044 implementation-readiness verification)
Primary Agent:     review
Supporting Agents: gameplay (Boss domain content accuracy)
Workflow:          documentation/documentation-change.md
Skills:            documentation-discovery, documentation-consistency,
                   impact-analysis, scope-validation
Dependencies:      TASK-046 (DONE), TASK-047 (DONE)
```

---

## Objective

Record the human-resolved technical Identities of the three
content-defined MVP Bosses in the canonical owner document
(`docs/01-game-design/BOSS_RULES.md` §6.4), removing the stale
`UNRESOLVED` markers that TASK-047 left behind because it intentionally
did not modify `docs/`. The three-way distinction (technical Identity vs
Display Name vs `BossDefinitionId`) and the existing kebab-case
convention are preserved exactly as TASK-046 established them. This is a
documentation-only synchronization: no code, no tests, no schema, no
protocol, no new gameplay or architecture decision — the identity
decision was already made and must not be reopened.

---

## Authoritative References

- `docs/01-game-design/BOSS_RULES.md` §6.4 — **canonical owner** of the
  BossId values (self-declared: "Owned by this section"); the only
  document permitted to define them (`documentation-change.md` §2–§3)
- `tasks/completed/TASK-046-resolve-canonical-boss-identity-contract.md`
  §5A Decision C — the convention `boss-<ascii-kebab-case-name>` and the
  deliberate deferral of Thủy Ma / Mộc Yêu (context; do not modify)
- `tasks/completed/TASK-047-synchronize-boss-technical-identity-in-source-and-tests.md`
  — P1 Decision Record: human supplied the two remaining IDs; source
  synced; `docs/` intentionally untouched (context; do not modify)
- `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` — repo
  evidence of the applied values (read-only reference)
- Referencing docs (verification only, no content duplication):
  `docs/02-technical/GAME_STATE.md` §2.4, `GAME_EVENTS.md` §2,
  `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18, `API_CONTRACTS.md` §3,
  `DATABASE.md` §1
- `AGENTS.md` §2 (owner hierarchy), §4 (conflict resolution), §7 (no
  invented rules), §16 (task discipline), §17 (documentation change
  rule), §20 (stop conditions)
- `docs/00-overview/MVP_SCOPE.md` §1 — first check: this correction adds
  no new system or content (scope N/A, doc correction only)

---

## Scope

### In Scope

1. `BOSS_RULES.md` header version note (L3–11, currently v2.3 carrying
   "Thủy Ma / Mộc Yêu technical Identity UNRESOLVED") — bump to the next
   version following the file's existing header convention, recording the
   resolution; keep the `prior 2.3:` history chain intact.
2. `BOSS_RULES.md` §6.4 table (L271–L272) — replace
   `UNRESOLVED (see note)` with `"boss-thuy-ma"` / `"boss-moc-yeu"`.
3. `BOSS_RULES.md` §6.4 bullet (L275–L280) — replace the UNRESOLVED
   claim with the resolved statement; **preserve** the three-way
   distinction (BossId / Display name / `BossDefinitionId`), the
   convention line, and the rule that display names are never used as
   technical identifiers.
4. `BOSS_RULES.md` §6.4 bullet (L287–L289) — the "source literals …
   aligning the code is a separate follow-up implementation task
   (TASK-046 changed no source)" note is stale after TASK-047's
   completion; correct it factually and minimally (source is now
   aligned per TASK-047).
5. Verification pass over the five referencing technical docs listed in
   Authoritative References: search for `UNRESOLVED` and for Boss
   identity values. **Expected outcome: zero edits.** An edit to a
   referencing doc is permitted only if a directly stale
   identity-contract reference is found, and only to fix that wording —
   never to restate §6.4's values (`documentation-change.md` §1–§2: no
   duplication).

### Out of Scope

- Any change under `src/` or `tests/` — TASK-047 already synchronized
  source and tests; do not touch `BossDefinitions.cs`, `BossId.cs`,
  `BattleStateService.cs`, `BattleStartService.cs`, `BattleEvent.cs`,
  `PassiveEvents.cs`, or any other source/test file.
- `tasks/backlog/TASK-044-implement-bossdefinition-persistence.md` —
  do not implement, edit, re-scope, or mark DONE; this task only clears
  a precondition for its future readiness verification.
- Any other task file (only this new task file may be created).
- `BossDefinitionId`, `BossState.BossId`, API `bossId`, event
  `sourceId` semantics; Redis key/serialization; SignalR protocol;
  database schema or migrations; PassiveId / SkillId values.
- Adding `DisplayName` to `BattleState`; localization infrastructure;
  aliases; runtime slug derivation.
- A new ADR — none is required: this records an already-approved
  decision in its owning document; no existing ADR mentions Boss
  identity.
- Broad Boss documentation audit, or rewriting identity references in
  documents beyond the checks above (no `GAME_RULES.md`, GDD, or
  `MVP_SCOPE.md` edits).

---

## Current State (verified at task creation)

- `BOSS_RULES.md` is **v2.3** and the only document in `docs/` containing
  `UNRESOLVED` (grep across `docs/`): header L6; §6.4 table L271–L272;
  §6.4 note bullet L275–L280. Plus the stale TASK-046-era code-alignment
  bullet at L287–L289.
- The five referencing technical docs contain **no** `UNRESOLVED`; they
  cite the contract as `e.g. "boss-hoa-long"` per `BOSS_RULES.md` §6.4 —
  consistent, no edits expected.
- Repository evidence confirms the resolved values (STOP check 1):
  `BossDefinitions.cs` L87 `new BossId("boss-hoa-long")`,
  L111 `new BossId("boss-thuy-ma")`, L136 `new BossId("boss-moc-yeu")`;
  TASK-047 P1 Decision Record records the same three values with
  `BossId("<display-name>")` at 0 occurrences.
- No test reads `docs/` (grep `ReadAllText|File.Read|docs\0` in
  `tests/` → no matches), so this documentation change cannot break the
  test suite.

---

## Acceptance Criteria

- [x] TASK-046 canonical identity decision is treated as authoritative.
- [x] Hỏa Long is documented as `boss-hoa-long`.
- [x] Thủy Ma is documented as `boss-thuy-ma`.
- [x] Mộc Yêu is documented as `boss-moc-yeu`.
- [x] No Boss identity remains incorrectly marked `UNRESOLVED`
      (`grep UNRESOLVED docs/` → 0 matches, or only matches explicitly
      documenting historical version history as the file's convention
      requires — none should remain in §6.4 or the current header note).
- [x] DisplayName and technical Identity remain distinct (the three-way
      distinction in §6.4 intact).
- [x] BossDefinitionId remains the persistence identity (unchanged).
- [x] No source code changes.
- [x] No tests modified.
- [x] No database migration.
- [x] No Redis changes.
- [x] No SignalR changes.
- [x] No gameplay changes.
- [x] TASK-044 remains unimplemented (file untouched).
- [x] Version header note follows the file's existing convention with
      the prior-version history chain preserved.

---

## Affected Files & Areas

```text
[ ] src/backend/                (forbidden)
[ ] src/frontend/               (forbidden)
[ ] tests/                      (forbidden)
[x] docs/ (documentation-only — BOSS_RULES.md canonical owner edit;
           five referencing docs verification-only, expected zero edits)
[x] tasks/ (this new task file only; no existing task modified)
```

---

## Implementation Notes

- Follow `.ai/workflow/documentation/documentation-change.md`: identify
  canonical owner (§6.4, already identified) → read referencing docs →
  check conflicts → update the smallest authoritative source → update a
  dependent reference only if its wording became stale → validate
  consistency (no duplicate definitions introduced).
- Preserve the §6.4 structure and wording wherever it is already
  correct: the three-concept list, the convention paragraph, the table
  columns (`BossId | Display Name | PassiveId | SkillId`), the PassiveId
  and SkillId bullets. Change only what is stale (UNRESOLVED markers,
  the code-alignment note, the header version note).
- Do not derive, rename, or re-case the identity values — copy the three
  exact values from the Authoritative References.
- Line numbers are creation-time evidence; re-locate by search before
  editing.

---

## Testing Requirements

### Required Verification

```text
[ ] Search docs/ for `UNRESOLVED` — no Boss-identity occurrence remains
[ ] Search docs/ for `boss-hoa-long` / `boss-thuy-ma` / `boss-moc-yeu`
                          — all three recorded in BOSS_RULES §6.4
[ ] Re-read BOSS_RULES §6.4 together with GAME_STATE §2.4,
                          GAME_EVENTS §2, SIGNALR §3.2.16–§3.2.18,
                          API_CONTRACTS §3, DATABASE §1 — consistent,
                          no duplicated definition introduced
[ ] Confirm no source/test file changed (git diff scoped to this task)
[ ] Confirm no task file other than this new one was modified
```

### Key Edge Cases

- `boss-thuy-ma` / `boss-moc-yeu` are substrings of PassiveIds
  (`boss-thuy-ma-heal`, `boss-moc-yeu-regen`) — do not confuse or alter
  PassiveId values when searching or editing.
- Code tests are **not** required: this task must not modify source
  (confirmed: no test reads `docs/`).

---

## Stop Conditions

Universal stops in `AGENTS.md` §20 apply. Task-specific stops — STOP
and report instead of guessing if:

1. TASK-046 / TASK-047 canonical IDs cannot be confirmed from
   repository evidence (at creation: **confirmed** — see Current State;
   re-verify at execution).
2. The current `BOSS_RULES.md` §6.4 structure conflicts with the
   TASK-046 decision (at creation: structure matches — table +
   three-way distinction + convention present).
3. Another authoritative domain rule document defines different IDs (at
   creation: none found — `grep UNRESOLVED|boss-thuy-ma|boss-moc-yeu`
   across `docs/` matches only BOSS_RULES' stale markers).
4. The repository shows a new conflicting identity decision.
5. The update would require a new gameplay or architecture decision —
   it must not; the values are already decided. If editing appears to
   require deciding anything, report instead.

If such a conflict exists: identify both sources, explain, name the
owning document per `AGENTS.md` §2, propose the smallest correction,
and stop — do not invent a resolution. Unrelated issues discovered
while editing are report-only (`AGENTS.md` §16).

---

## Completion Evidence

**Status: DONE** (executed 2026-09-26). No STOP condition fired: the
TASK-046 canonical identities were confirmed from repository evidence, the
§6.4 structure matched the TASK-046 decision, no other authoritative document
defines different IDs, and no new gameplay or architecture decision was
required. `BOSS_RULES.md` v2.3 → v2.4.

### Changed Files

Documentation (1 — canonical owner only):

- `docs/01-game-design/BOSS_RULES.md` — v2.3 → **v2.4**; four stale passages
  corrected and §6.4 synchronized:
  1. **Header version note (L3–15)** — bumped to 2.4 with a resolution note
     (Thủy Ma / Mộc Yêu Identities recorded; code-alignment note corrected per
     TASK-047) prepended to the existing chain; the `prior 2.3:` (TASK-046) and
     `prior 2.2:` history entries are preserved verbatim, and the stale
     "Thủy Ma / Mộc Yêu technical Identity UNRESOLVED" clause is removed.
  2. **§6.4 table (L275–L276)** — `UNRESOLVED (see note)` replaced with
     `"boss-thuy-ma"` / `"boss-moc-yeu"`. Columns unchanged
     (`BossId | Display Name | PassiveId | SkillId`); Hỏa Long row, PassiveId
     and SkillId values untouched.
  3. **§6.4 bullets (L279–L289)** — the UNRESOLVED claim replaced with the
     resolved statement plus the explicit three-way distinction:
     **BossId** = canonical technical Identity (machine-readable, never the
     display name); **Display name** = human-readable content/presentation
     name, never a technical identifier; **`BossDefinitionId`** = the stable
     persistence/database identity of the `BossDefinition` row
     (`DATABASE.md` §1).
  4. **§6.4 closing bullet (L296–L298)** — the stale "Source literals that
     still carry display-name BossIds … aligning the code is a separate
     follow-up implementation task (TASK-046 changed no source)" note replaced
     with the factual current state: source and tests carry these exact
     canonical identities and are aligned (TASK-047).

  Preserved unchanged in §6.4: the three-concept list, the naming convention
  paragraph (`boss-<ascii-kebab-case-name>`), the PassiveId/SkillId bullets,
  and the `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18 example citation.

Task file (1 — this file only):

- `tasks/active/TASK-048-record-resolved-boss-technical-identities-in-boss-rules.md`
  — Status BACKLOG → IN PROGRESS → DONE; moved `backlog/` → `active/` →
  `completed/`; completion evidence recorded.

**Zero edits to referencing documents.** No other file in `docs/`, `tasks/`,
`src/`, or `tests/` was written (`documentation-change.md` §1–§2: update the
smallest authoritative source; do not duplicate the updated content into
referencing docs).

### Validation Results

- **`grep UNRESOLVED docs/`** — BOSS_RULES-identity `UNRESOLVED` markers:
  **all 3 removed** (header note, 2 table cells, note bullet). The single
  remaining `UNRESOLVED` token in `BOSS_RULES.md` (L4) is a *historical*
  reference inside the version note ("previously recorded `UNRESOLVED`"),
  which the file's version-history convention requires — it asserts no
  current unresolved state. No Boss identity remains incorrectly marked
  unresolved. The other `docs/` matches are unrelated to Boss identity and
  pre-existing: `MATCH3_RULES.md` L1017/L1049/L2388/L2474 (Special Gem
  resolution record) and `GAME_STATE.md` L418 (a recorded §2.1.2 gap).
- **Three identities present in BOSS_RULES §6.4** — **confirmed**:
  L274 Hỏa Long `"boss-hoa-long"`; L275 Thủy Ma `"boss-thuy-ma"`;
  L276 Mộc Yêu `"boss-moc-yeu"`.
- **Three-way distinction intact** — **confirmed**: §6.4 L279–L289 and the
  L251–L264 three-concept list both state Identity / DisplayName /
  `BossDefinitionId` as three never-collapsed concepts. `BossDefinitionId`
  still points to `DATABASE.md` §1 as its owner.
- **Repository evidence of applied values (STOP check 1 re-verified)** —
  `BossDefinitions.cs` L87 `new BossId("boss-hoa-long")`, L111
  `new BossId("boss-thuy-ma")`, L136 `new BossId("boss-moc-yeu")`;
  `BossId("…")` with any display name → 0 matches in `src/`.
- **Referencing docs consistency check** — the five technical docs were
  re-read against §6.4; **zero edits required and zero made**. Each cites
  §6.4 as the owner for the value and states the value is the canonical
  technical Identity, never a display name:
  `GAME_STATE.md` §2.4 (denotation paragraph), `GAME_EVENTS.md` §2
  (`BattleStarted.BossId`; Passive/Skill `sourceId`),
  `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18 (examples/tables/notes),
  `API_CONTRACTS.md` §3 (`bossId`), `DATABASE.md` §1
  (`BossDefinition.Identity`, `BattleResult.BossDefinitionId` sourcing).
  These already used `e.g. "boss-hoa-long"` and cite §6.4 as owner, so the
  TASK-046 cross-references did not become stale; no duplicate definition
  introduced. The one `display-name`-matching hit in `SIGNALR_PROTOCOL.md`
  (L12) is the retained `prior 2.1:` *history* entry, correctly superseded by
  the v2.4 note above it.
- **Source / test / task scope check** — aggregate SHA256 before and after
  identical: `src/` `35E21F91…CBA20EF4` (11102 files); `tests/` `B24645C9…EFBA0CF5`
  (894 files); `TASK-044` `447752BE…3DE242`. `TASK-046` and `TASK-047` hashes
  unchanged. Session write-mtime scan: `docs/` → `BOSS_RULES.md` only;
  `tasks/` → this task file only.
- **Code tests** — **N/A: documentation-only** (`docs/01-game-design/**`).
  No test reads `docs/` (verified by TASK-048 authoring), so this change
  cannot affect the test suite; no source compiled or executed.

### Server Authority & Scope Verification

- [x] Documentation-only: zero source, test, migration, schema, Redis,
      SignalR, API-contract, localization, gameplay, or architecture changes
- [x] TASK-044 untouched and unimplemented (hash unchanged; not re-scoped,
      not marked DONE; only its precondition cleared)
- [x] TASK-046 / TASK-047 / TASK-041 / TASK-043 / TASK-045 files unmodified
- [x] No new ADR — none required: this records an already-approved decision in
      its owning document; no ADR mentions Boss identity
- [x] No authoritative contract conflict introduced (`AGENTS.md` §4);
      §6.4 remains the single owner, referencing docs unchanged
- [x] MVP scope unaffected (`MVP_SCOPE.md` §1) — doc correction only, no new
      system, content, or Boss
- [x] PassiveId (`boss-hoa-long-rage`, `boss-thuy-ma-heal`,
      `boss-moc-yeu-regen`) and SkillId values untouched
