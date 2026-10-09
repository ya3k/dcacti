# TASK-233 — Fix AuthScreen Duplicate Username Error Code Mapping

## Metadata

```text
Task ID:           TASK-233
Type:              BUG
Status:            DONE
Risk:              LOW
Priority:          MEDIUM
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/bug-fix.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis, testing/test-scenario-generation, quality/implementation-review
Dependencies:      None
Declared Files:    src/frontend/client/src/ui/components/AuthScreen.tsx, src/frontend/client/tests/AuthScreen.test.tsx
```

---

## Objective

Correct the duplicate-username error mapping in `AuthScreen.tsx` so that a `409 Conflict` registration rejection carrying the authoritative `USERNAME_ALREADY_EXISTS` code displays the intended Vietnamese message `Tên đăng nhập đã tồn tại. Vui lòng chọn tên khác.` instead of falling through to the generic branch and exposing the raw machine code to the player. The bug is a client-side string mismatch only; expected behavior is already fully specified by the authoritative contract and requires no design decision.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Standalone Web Account Authentication (in-scope MVP surface)
- `docs/02-technical/API_CONTRACTS.md` §2.1 rule 3 — `POST /api/auth/register` returns `409 Conflict` with `USERNAME_ALREADY_EXISTS` when the username is already registered
- `docs/02-technical/API_CONTRACTS.md` §2.1 — username/password validation rules (the `INVALID_INPUT` boundary this task must not disturb)
- `docs/02-technical/API_CONTRACTS.md` §2.2 rule 2 — `POST /api/auth/login` returns `401 Unauthorized` with `INVALID_CREDENTIALS`
- `docs/02-technical/API_CONTRACTS.md` §6 — The error envelope (`error`, `message`) the client reads
- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` — Standalone web account authentication; records the `409`/`USERNAME_ALREADY_EXISTS` uniqueness behavior
- `tasks/TASK_LIFECYCLE.md` §6 — Two-stage commit protocol (Phase A implementation slice commit, Phase B completion record filing commit)

---

## Scope

### In Scope
- Correct the matched duplicate-username error code in `AuthScreen.tsx` so the `USERNAME_ALREADY_EXISTS` rejection maps to the intended Vietnamese duplicate-username message.
- Add a regression test suite covering duplicate-username mapping plus the adjacent unchanged mappings (`INVALID_CREDENTIALS`, `INVALID_INPUT`, generic fallback).
- Follow the two-stage commit protocol established under TASK-230.

### Out of Scope
- Any change to `AuthController.cs`, `ApiService.ts`, backend behavior, API contracts, or database behavior.
- Sign-out, session invalidation, or the unrelated NG-12 `401` mid-session gap reported by `tasks/completed/TASK-211-post-task-210-product-audit.md` §Part C.
- Any change to `docs/` — the documentation is correct and is the source of truth for this fix.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

`src/frontend/client/src/ui/components/AuthScreen.tsx` line 43 matches `msg.includes('USERNAME_ALREADY_TAKEN')`, a code no endpoint emits. The backend emits `USERNAME_ALREADY_EXISTS` (`src/backend/GameServer.Api/Controllers/AuthController.cs` line 84), and `API_CONTRACTS.md` §2.1 rule 3 specifies that code. `ApiService.register()` rethrows `new Error(err?.error)`, so the code does reach the client verbatim and the match fails; control falls through to the generic branch at line 50, rendering `Đăng nhập thất bại: USERNAME_ALREADY_EXISTS` and exposing the raw machine token. No `AuthScreen.test.tsx` exists, so the mapping is currently untested. Reported as NG-11 in `tasks/completed/TASK-211-post-task-210-product-audit.md` §Part C and as A-14 in `tasks/completed/TASK-213-post-task-212a-1-product-gameplay-audit.md`, and still open per `tasks/completed/TASK-212-post-task-211-product-audit.md` and `tasks/completed/TASK-211-non-battle-screen-robustness-and-recovery.md`.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-233:
```text
src/frontend/client/src/ui/components/AuthScreen.tsx
src/frontend/client/tests/AuthScreen.test.tsx
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above must strictly match the `Declared Files:` field in `## Metadata`. `src/frontend/client/tests/AuthScreen.test.tsx` is a NEW file — inspection confirmed no AuthScreen test suite exists anywhere under `src/frontend/client/tests/`, so this is a new file rather than a duplicate suite. The suite is named to match the component it covers, consistent with the existing `StatusOverlay.test.tsx` / `GameShell.test.tsx` convention.)*

---

## Acceptance Criteria

- [ ] A registration rejection whose error message contains `USERNAME_ALREADY_EXISTS` displays exactly `Tên đăng nhập đã tồn tại. Vui lòng chọn tên khác.`
- [ ] The raw `USERNAME_ALREADY_EXISTS` code is never present in any user-facing message rendered by `AuthScreen`
- [ ] `INVALID_CREDENTIALS` continues to display `Tên đăng nhập hoặc mật khẩu không chính xác.`
- [ ] `INVALID_INPUT` continues to display `Dữ liệu không hợp lệ. Vui lòng kiểm tra lại thông tin.`
- [ ] An unrecognized error code continues to fall through to the generic `Đăng nhập thất bại: <message>` branch
- [ ] Local client-side validation (username 3–32 characters; password minimum 6 characters) is unchanged
- [ ] Regression tests cover duplicate-username mapping and the adjacent unchanged mappings
- [ ] Frontend tests pass via `npm run test:run` in `src/frontend/client`
- [ ] TypeScript/build verification passes via `npx tsc --noEmit` (or `npm run build`) in `src/frontend/client`
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] Phase A implementation slice is committed with its staged set matching this record's Declared File Set exactly, and the Phase B completion record is filed (`tasks/TASK_LIFECYCLE.md` §6)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[x] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation updates if applicable)
```

---

## Implementation Notes

- `AuthScreen.tsx` `handleSubmit`'s `catch` block holds the mapping chain; only the first branch's matched literal is incorrect. The remaining branches and the generic fallback are correct and must be preserved.
- The error reaching the catch block is a plain `Error` whose `message` is the API error code (`ApiService.register`/`login` rethrow `new Error(err?.error)`), so the branch matches on the message string. Existing matching style should be retained.
- The test suite should mock `ApiService.getInstance()` to reject with each code, render `AuthScreen`, drive the register tab and form submit, and assert on the `auth-error-banner` element (`data-testid="auth-error-banner"`).
- Test conventions: `vitest` with `globals: true`, `jsdom` environment, `@testing-library/react`; see `src/frontend/client/vitest.config.ts` and `src/frontend/client/tests/StatusOverlay.test.tsx`.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — AuthScreen duplicate-username error mapping and adjacent mapping branches
[ ] Integration tests  — N/A: the mapping is a pure client-side string branch; no boundary is crossed
[ ] Gameplay scenarios — N/A: this task touches no gameplay rule or server-authoritative value (AGENTS.md §10 not engaged)
```

### Key Edge Cases
- Duplicate username on the **register** tab maps to the translated message (the only path the server emits `USERNAME_ALREADY_EXISTS` from — `API_CONTRACTS.md` §2.1 rule 3).
- The raw `USERNAME_ALREADY_EXISTS` token must not appear in the rendered banner.
- `INVALID_CREDENTIALS` and `INVALID_INPUT` remain correctly mapped (regression guard — `API_CONTRACTS.md` §2.2 rule 2 and §2.1 validation rules).
- An unrecognized code still falls through to the generic branch rather than being swallowed.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If the fix appears to require any backend, contract, or `docs/` change: STOP per `AGENTS.md` §17 — the documentation is authoritative and the code is the deviation
- If implementation requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If the task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Commit
- Implementation slice (Phase A): `f7807767b6626bfb65adb9cf90696a5cc0c9e40e`
- Subject: `fix: map the duplicate-username registration rejection to its message`
- Owns: `TASK-233`
- Files: `src/frontend/client/src/ui/components/AuthScreen.tsx`, `src/frontend/client/tests/AuthScreen.test.tsx`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `src/frontend/client/src/ui/components/AuthScreen.tsx` — the duplicate-username branch in `handleSubmit`'s `catch` now matches `USERNAME_ALREADY_EXISTS` (the code `AuthController.cs` line 84 emits and `API_CONTRACTS.md` §2.1 rule 3 specifies) instead of the `USERNAME_ALREADY_TAKEN` literal no endpoint emits. The matched message and every other branch are unchanged.
- `src/frontend/client/tests/AuthScreen.test.tsx` — NEW regression suite covering the four documented mapping branches plus the local validation gates.

### Validation Results
- `npx vitest run tests/AuthScreen.test.tsx` in `src/frontend/client` — PASS (6 tests)
- `npm run test:run` in `src/frontend/client` — PASS (23 files, 917 tests; 911 pre-existing + 6 new)
- `npx tsc --noEmit` in `src/frontend/client` — PASS (0 errors)

### Regression Guard Evidence
- Reverting only the `USERNAME_ALREADY_EXISTS` literal back to `USERNAME_ALREADY_TAKEN` in the working tree made exactly 2 of the 6 new tests fail, rendering `Đăng nhập thất bại: USERNAME_ALREADY_EXISTS` in the `auth-error-banner`. The suite therefore fails on the original defect and passes on the fix. The fix was then restored and re-verified.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
