# skills/backend/api-contract-validation.md — Skill: API Contract Validation

**Version:** 1.0

> Validate REST behavior (existing, changed, or proposed) against
> `API_CONTRACTS.md` and the documents it delegates to, without inventing
> endpoints, fields, or error codes.

## Purpose

Verify that REST endpoints, their request/response semantics, validation
delegation, and error convention match the documented contract, that no
endpoint lets the client submit authoritative gameplay values, and that the
contract is consistent with the state, realtime, and persistence documents.

## When to Use

- A backend task adds, changes, or reviews a REST endpoint or DTO.
- `development/refactor.md` §2 (public contracts must not shift).
- `quality/testing.md` §1 "API tests" (expected behavior source).
- `quality/review.md` for changes touching Controllers.
- Impact analysis flags an API contract change.

## When Not to Use

- In-battle actions and events (`realtime-protocol-validation`; the REST
  document itself excludes them).
- Authentication design — the mechanism is not specified in the docs (see
  Stop Conditions).
- Database schema questions (`persistence-analysis`).

## Inputs

```text
The endpoint(s), DTOs, controllers, or proposed contract change in scope
Documentation Context (documentation-discovery output)
```

## Prerequisites & Required Context

- The domain rule documents that an endpoint's validation delegates to.
- Knowledge that REST covers only non-realtime concerns (`TDD.md` §5).

## Authoritative Sources

```text
API_CONTRACTS.md                   endpoints, request/response, errors, exclusions
GAME_STATE.md                      BattleState summary returned by battle start
SIGNALR_PROTOCOL.md §1, §6         how REST hands off to realtime; fallback to result
REDIS_STATE.md §3, DATABASE.md     lifecycle tied to start/result endpoints
Domain rules referenced by the endpoint's validation (PET_RULES.md,
  CARD_RULES.md, RELIC_RULES.md, …)
GAME_RULES.md §18                  client may not send authoritative values
ARCHITECTURE.md §1–§2              Controllers are thin
TDD.md §5                          REST vs SignalR split
ADR-007                            session authentication decision (and its open item)
```

## Procedure

1. **Enumerate** the endpoints/DTOs in scope (from code, diff, or proposal).
2. **Find each in `API_CONTRACTS.md`.** An endpoint, field, or behavior with
   no documented home is *undocumented*, not "probably fine" — record it.
3. **Compare semantics, not just names:** method/path, purpose, request
   shape, validation and *which document owns each validation rule*,
   response shape, status/error convention. The document states exact wire
   casing is an implementation detail where relevant; do not enforce what it
   does not specify.
4. **Check delegation.** Endpoint validation must call/mirror the owning
   domain rule (per the document) and not re-implement or redefine it; find
   the owner in the document's own references.
5. **Check the authority boundary.** No endpoint accepts or trusts
   Damage/HP/Power/Match/Combo (or other protected values from `AGENTS.md`
   §10); no endpoint lets a client submit a gameplay result
   (`API_CONTRACTS.md` "What Is Not Here", `GAME_RULES.md` §18). Hand any
   hit to `authority-determinism-audit`.
6. **Check the REST/realtime split.** In-battle actions and event delivery
   must not be on REST (`TDD.md` §5); REST hands the client what it needs to
   connect (per `SIGNALR_PROTOCOL.md` §1).
7. **Cross-contract consistency.** Compare what the endpoint returns or
   triggers against `GAME_STATE.md` (state summary), `REDIS_STATE.md`
   (lifecycle on creation/end) and `DATABASE.md` (what the result endpoint
   reads). Disagreements are data-contract conflicts → stop.
8. **Layer check.** Controllers translate and delegate; no game logic
   (`ARCHITECTURE.md` §2). Hand structural findings to
   `architecture-conformance`.
9. **Error convention.** Compare error bodies to the documented convention;
   an error code the document does not define is a documentation gap to
   report (the document intentionally does not enumerate a global list).

## Outputs

```text
API Contract Validation Report
| Endpoint | Documented? | Semantics match | Validation owner | Authority OK | Notes |
- Undocumented endpoints/fields/error codes
- Contract mismatches (code/proposal vs API_CONTRACTS.md §, with both sides)
- Cross-contract inconsistencies (API ↔ state/redis/db/signalr)
- Defects (client authority, logic in controllers, wrong transport)
- Recommended smallest correction (code or docs — decision belongs to owner rule, AGENTS.md §4/§17)
```

## Validation

```text
[ ] Every endpoint in scope was matched to a document section or reported undocumented
[ ] Validation ownership traced to the domain document, not restated here
[ ] Authority boundary checked for every request and response field in scope
[ ] Cross-contract consistency checked against at least state, realtime, storage
[ ] No endpoint, field, or error code was invented to make the report tidy
```

## Stop Conditions

- The endpoint/field needed is not in `API_CONTRACTS.md` → stop and report;
  do not invent a contract (`AGENTS.md` §20 data-contract rule).
- API, state, event, Redis, or database documents disagree about the same
  contract → data-contract conflict.
- Behavior depends on how a client token becomes a server session: this is an
  explicit open item (`ADR-007` "Open Item", `docs/03-decisions/README.md` §8);
  do not design it — report.
- The proposed endpoint would carry authoritative gameplay values or belongs
  to an out-of-scope system (`MVP_SCOPE.md` §2).

Baseline stop conditions in `.ai/skills/README.md` §6 also apply.

## Common Failure Modes

- Adding a "convenient" endpoint or field not in the contract.
- Re-implementing loadout/ownership validation in the controller instead of
  delegating to the owning rule.
- Copying a documented limit or constraint value into code/tests from memory
  instead of reading the owning rule document.
- Letting the client echo back authoritative state.
- Enforcing exact JSON casing the documents deliberately leave open.
- Assuming an authentication mechanism the ADR marks as undecided.

## Traceability

```text
Used by:    development/feature.md (backend), refactor.md (§2 contracts),
            bug-fix.md; quality/testing.md (API tests); quality/review.md
Reads:      API_CONTRACTS.md; GAME_STATE.md; SIGNALR_PROTOCOL.md; REDIS_STATE.md;
            DATABASE.md; domain rule docs; TDD.md §5; ARCHITECTURE.md; ADR-007
Produces:   API Contract Validation Report
Depends on: documentation-discovery (only if context not already supplied)
```
