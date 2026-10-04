# TASK-185 — Implement MVP Boss Selection in Lobby

<!--
  GEN-TASK EXECUTION MANIFEST — FEATURE (Frontend / Cross-Layer)
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  References docs/ and src/ by path and section — it does NOT copy game
  rules, formulas, magnitudes, schemas, or API payload shapes.

  SCOPE OF THIS TASK: expose the five already-provisioned MVP Bosses as a
  player-selectable step in LobbyScene and carry the choice through the
  EXISTING battle-start contract. The backend already accepts any of the
  five; nothing about Boss gameplay, validation, or provisioning moves.

  IT ADDS NO GAMEPLAY. IT CHANGES NO BOSS RULE OR STAT.

  READ "Documentation Conflict That Must Be Reconciled FIRST" BEFORE
  PLANNING: `TDD.md` §2.1 currently states there is NO MVP Boss-selection
  step. This task cannot silently contradict it.
-->

---

## Metadata

```text
Task ID:           TASK-185
Type:              FEATURE — TASK_TYPES.md §2; crosses frontend Lobby state +
                   battle-start request construction + one backend-contract
                   reconciliation, so core/validation.md §2's integration
                   depth applies.
Status:            DONE
Risk:              MEDIUM (a presentation-layer selection plus one
                   existing-contract pass-through. No rule, no stat, no
                   schema, and no authorization boundary changes. The one
                   HIGH-risk edge — inventing Boss semantics — is bounded by
                   the NON-GOALS below and by the explicit instruction to
                   reuse BOSS_RULES.md §6 content verbatim.)
Priority:          HIGH (the last known MVP playability gap: the backend
                   provisions all five canonical Bosses but the player can
                   reach exactly one of them.)
Primary Agent:     client (the work is LobbyScene selection + request
                   construction; the backend already implements the
                   selection-accepting contract)
Supporting Agents: review (the TDD.md §2.1 reconciliation is a
                   documentation-consistency decision and MUST be reviewed,
                   not decided silently by the implementer), testing
                   (selection coverage + the browser smoke test)
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/scope-validation,
                   testing/test-scenario-generation,
                   quality/architecture-conformance
Dependencies:      TASK-181 (DONE — supplies the development authentication
                   the browser smoke test needs; without it the real-browser
                   AC-13 run cannot reach Lobby), TASK-182 (DONE — board
                   input; AC-11 requires it to keep working), TASK-183 (DONE
                   — START BATTLE clickability; AC-12 requires it to keep
                   working), TASK-184 (DONE — completed the canonical Relic
                   set, so the Lobby's Relic loadout step is unaffected and
                   this task does not touch it). TASK-078 authored the fixed
                   Boss behaviour this task replaces and is the direct
                   predecessor of the change (see "Relationship to TASK-078").
```

---

## Objective

Let the player choose **which** of the five canonical MVP Bosses
(`BOSS_RULES.md` §6) the battle is fought against, in `LobbyScene`, and
carry that choice through the **existing** `POST /api/battle/start`
contract — instead of the scene hardcoding one Boss
(`LobbyScene.ts:19`, `MVP_BOSS_ID = 'boss-hoa-long'`).

The backend already resolves any of the five canonical identities
(`BattleStartService.ResolveBoss` iterates `BossDefinitions.All`), and
`API_CONTRACTS.md` §3 already documents `bossId` as "must be a valid MVP
Boss's canonical technical Identity". **This task therefore removes a
client-side limitation; it does not add a backend capability and it does
not add gameplay.**

```text
TODAY                                  TARGET
Lobby: 5 Boss choices → impossible     Lobby: 5 Boss choices → selectable
   ↓                                      ↓
bossId: MVP_BOSS_ID (hardcoded)        bossId: the player's selection
   ↓                                      ↓
POST /api/battle/start   ←──────────→  POST /api/battle/start   (UNCHANGED)
   ↓                                      ↓
ResolveBoss(bossId)      ←──────────→  ResolveBoss(bossId)      (UNCHANGED)
```

---

## Documentation Conflict That Must Be Reconciled FIRST

**This is the single most important section of this task. Read it before
planning. It is a `AGENTS.md` §4 documentation-conflict condition, and
§4 forbids resolving it silently.**

`docs/02-technical/TDD.md` §2.1 (`LobbyScene` bullet, ~L135) currently
states:

```text
There is **no MVP Boss-selection step** — the Boss is supplied by the
battle-start flow, not chosen by the player, and no Boss-selection UI
exists.
```

`docs/00-overview/MVP_SCOPE.md` §1 states the MVP includes **"5 Bosses"**,
and `BOSS_RULES.md` §6 defines exactly five content-defined Bosses. The
backend provisions and resolves all five. The five Bosses are therefore
IN scope as content, while the *selection step* is currently documented as
not existing.

These two are not in direct contradiction — MVP_SCOPE.md decides what
content exists; TDD.md §2.1 decides what the Lobby UI does with it. What
this task proposes is a **change to a documented technical design
statement**, which is why it must be explicit rather than implicit.

Per `AGENTS.md` §2, `TDD.md` is a technical-design document and owns this
statement. Per §17, changing behaviour a document describes requires the
design to be decided **before** implementation:

```text
1. Confirm MVP_SCOPE.md §1 keeps 5 Bosses IN scope          (no change)
2. Decide TDD.md §2.1's "no Boss-selection step" sentence    ← THE DECISION
3. Update TDD.md §2.1 to describe the selection step         ← doc change
4. Implement the selection in LobbyScene                     ← code change
```

**The first implementation step of this task is therefore a
Product-Owner-facing confirmation, not code.** The implementing agent
must:

1. Confirm `MVP_SCOPE.md` §1 keeps "5 Bosses" IN scope and that a
   selection *step* is not listed OUT in §2 (MVP_SCOPE.md §2's OUT list
   does not mention Boss selection).
2. Obtain explicit confirmation that the Lobby **should** gain a Boss
   selection step — because TDD.md §2.1 is an authoritative technical
   statement and this task is deliberately overriding it.
3. Update `TDD.md` §2.1's `LobbyScene` bullet to describe the selection
   step, and record the change in that document's version note per its own
   convention.
4. **Then** implement.

**If step 2 is not confirmed, STOP** (see Stop Conditions). Do not
implement against a document that says the feature does not exist, and do
not quietly edit `TDD.md` to match code that was written first.

**Do NOT change the Boss-definition content itself.** `BOSS_RULES.md` §6
owns the five Bosses, their Elements, stats, Passives, and Skills. This
task changes *which one a battle uses*, never *what any of them does*.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — "5 Bosses / Element, Passive, Skill
  per Boss" is IN scope; §2's OUT list must be re-checked before adding
  any UI surface
- `docs/01-game-design/BOSS_RULES.md` §6, §6.1–§6.4 — **the authoritative
  Boss content and identity contract.** §6 defines exactly five Bosses;
  §6.4 fixes each canonical technical `BossId`. This task consumes these
  values verbatim and invents none
- `docs/01-game-design/GDD.md` §2 — the pre-battle selection flow (Choose
  Pet → Equip Cards → Equip Relics → Start Battle). **Verify whether GDD.md
  §2 enumerates the Lobby steps and, if it does, whether a Boss step must
  be added there too** — GDD.md outranks TDD.md (`AGENTS.md` §2)
- `docs/02-technical/TDD.md` §2.1 — the `LobbyScene` responsibility bullet
  containing the statement this task changes (see the conflict section)
- `docs/02-technical/API_CONTRACTS.md` §3 — the `POST /api/battle/start`
  request/validation contract. `bossId` is already documented as an
  arbitrary valid MVP Boss identity, so **the contract needs no change**
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 rule 1 / §2.2.3 rules 3–5 —
  the scene↔transport boundary (a scene never calls `fetch`/`ApiService`)
  and the runtime-port capability model; §5 — anti-overengineering standard
- `docs/02-technical/GAME_STATE.md` §2.4 — `BossState` and its `BossId`
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.4 — the delivered `bossState`
  projection is `hp`/`maxHp` **only** (relevant to AC-10's verification
  path — see "Battle State Verification")
- `docs/03-decisions/ADR/ADR-001` — server-authoritative battle resolution
- `docs/03-decisions/ADR/ADR-011` — one active Pet and the battle-scoped
  loadout model (the Pet/Card/Relic steps stay as they are)

---

## Current State (verified against the repository)

```text
FRONTEND — the hardcoded Boss
src/frontend/client/src/game/scenes/LobbyScene.ts
    L19   export const MVP_BOSS_ID = 'boss-hoa-long';
    L422  buildStartRequest(): petId / bossId: MVP_BOSS_ID /
                              cardLoadout / relicLoadout   ← exactly 4 members
    L549  reviewText: `4. REVIEW — Boss: ${MVP_BOSS_ID}`
    L490-500  three column headers: "1. CHOOSE PET",
              "2. EQUIP CARDS", "3. EQUIP RELICS"
    L102  selectedPetId: string | null
    L107  selectedCardIds: string[]
    L116  selectedRelicIds: string[]
    → The scene holds its whole in-progress selection as ephemeral scene
      fields, cleared on shutdown() (L183–L211). There is NO Boss field and
      NO Boss list.

FRONTEND — supporting
src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
    GameRuntimePort.startBattle(request: BattleStartRequest): Promise<void>
    → the contract already takes the WHOLE request, `bossId` included. No
      port change is needed for a selected Boss to reach the server.
src/frontend/client/src/services/api/BattleModels.ts
    L404  BattleStartBossState { bossId, element, hp, maxHp, atk, def, state }
    L213  BattleStartInitialState.bossState
    → the start RESPONSE carries the created Boss's `bossId`, `element`,
      and stats. This is the authoritative confirmation surface (AC-10).

FRONTEND — Boss data source: NONE
    There is no Boss list API, no Boss definition API, no static canonical
    Boss catalog, and no Boss metadata in the client. A repository-wide
    search for Boss accessors over the client finds only `bossId` on the
    request/response models. The five Bosses exist in the client ONLY as
    the single literal 'boss-hoa-long'.

BACKEND — already accepts all five
src/backend/GameServer.Domain/Bosses/BossDefinitions.cs
    HoaLong / ThuyMa / MocYeu / SonThachVe / KimLoiVuong
    L338  public static readonly IReadOnlyList<BossDefinition> All = [ …five… ]
src/backend/GameServer.Application/Battle/BattleStartService.cs
    L258  if (ResolveBoss(request.BossId) is not { } bossDefinition) → BossNotFound
    L505  ResolveBoss iterates BossDefinitions.All, ordinal comparison on
          BossId.Value, returns null when none matches
src/backend/GameServer.Api/Controllers/BattleController.cs
    L332  BattleStartOutcome.BossNotFound => "BOSS_NOT_FOUND"
    → Any of the five canonical identities already starts a battle. An
      unknown identity is already rejected with 400 BOSS_NOT_FOUND. The
      backend is authoritative and needs NO change.

BACKEND — no Boss-list endpoint
    src/backend/GameServer.Api/Controllers/ has exactly these routes:
      CollectionController: GET api/pets, api/pets/{petId}, api/cards, api/relics
      BattleController:     POST api/battle/start, GET api/battle/{id}/result,
                            GET api/battle/history
      AuthController:       POST api/auth/discord
    → There is NO GET /api/bosses. API_CONTRACTS.md §1's endpoint summary
      does not list one, and §5's collection reads cover pets/cards/relics
      only.

KNOWN STALE COMMENTS (not authoritative, must not be trusted)
src/backend/GameServer.Application/Battle/BattleStartService.cs L249–253
    still says "BOSS_RULES.md §6 defines exactly three content-defined MVP
    Bosses". TASK-171/TASK-172 completed the set at five; BossDefinitions.cs
    L30–36 records the correction. The CODE is correct (it iterates All);
    the COMMENT is stale. See "Unrelated Findings".
```

---

## Canonical Bosses

`BOSS_RULES.md` §6 defines exactly these five, and §6.4 fixes each
canonical technical Identity. These are transcribed from `BossDefinitions.cs`
(which itself transcribes §6.1–§6.4) — **not guessed, and not to be
re-derived from display names**:

```text
Display name     Canonical technical BossId    Element   BossDefinitionId
--------------   ---------------------------   -------   ---------------------
Hỏa Long         boss-hoa-long                 Hỏa       boss-def-hoa-long
Thủy Ma          boss-thuy-ma                  Thủy      boss-def-thuy-ma
Mộc Yêu          boss-moc-yeu                  Mộc       boss-def-moc-yeu
Sơn Thạch Vệ     boss-son-thach-ve             Thổ       boss-def-son-thach-ve
Kim Lôi Vương    boss-kim-loi-vuong            Kim       boss-def-kim-loi-vuong
```

**The submitted value is the middle column only** — the canonical
technical Identity (`BOSS_RULES.md` §6.4). The display name is
presentation-only content and is never submitted; `BossDefinitionId` is
the persistence key, is a third distinct concept, and is likewise never
submitted (`DATABASE.md` §1, `BOSS_RULES.md` §6.4, `API_CONTRACTS.md` §3).

**These five values are the acceptance-critical literals.** An
implementation that re-spells them (e.g. slugifying "Hỏa Long" at run
time, or dropping the `boss-` prefix) is wrong even if the result happens
to look right.

---

## Proposed Scope

**The smallest implementation that lets a player pick any of the five.**

### 1. A selection source for the five Bosses (client-side)

The client has **no** Boss data source (see Current State). Three options
exist; the implementing agent must choose the one the repository actually
supports and must not build a new backend architecture without proving it
is required:

```text
OPTION A — a small read-only backend endpoint (GET /api/bosses)
  Requires: a new controller action + an API_CONTRACTS.md §1/§5 update +
            a matching client service method + the runtime-port capability.
  Cost:     the largest of the three. Touches the documented REST surface.

OPTION B — a client-side static catalog of the five canonical identities
  Requires: one client module holding the five §6.4 identities (and
            whatever presentation labels the UI needs).
  Cost:     smallest. Adds no endpoint and no protocol surface.
  Risk:     a second copy of the identities that could drift from
            BOSS_RULES.md §6.4.

OPTION C — extend an EXISTING collection read
  Requires: proving an existing read is the documented home for Boss
            content.
  Cost:     depends entirely on that proof.
```

**Guidance, not a decision — the implementing agent must verify:**

- `ARCHITECTURE.md` §5 and `AGENTS.md` §9 both forbid speculative
  abstraction, so **Option B is the presumptive default**: it is the
  smallest change, it adds no protocol surface, and the five identities
  are stable, documented, and content-frozen.
- **Option A is acceptable only if** the repository shows the Lobby must
  not hold content literals — e.g. if `ARCHITECTURE.md` or the collection
  contract already establishes that all selectable content comes from a
  server read. The client already reads Pets/Cards/Relics from the server,
  so this precedent argued *for* a server source — but it equally shows
  the reads exist because ownership varies **per player**, whereas the
  Boss set is global static content with no ownership.
- **If Option A is chosen and the endpoint grows beyond a small read-only
  endpoint returning the five canonical identities, STOP** (see Stop
  Conditions). Do not build a Boss-definition subsystem.
- Whichever option is chosen, **the five identities must be traceable to
  `BOSS_RULES.md` §6.4** and the code must say so. If Option B is chosen,
  the module must reference §6.4 as the owner so a future reader can see
  it is a transcription and not an invention.

### 2. A Boss selection field and interaction in `LobbyScene`

- Add a `selectedBossId` scene field alongside `selectedPetId` /
  `selectedCardIds` / `selectedRelicIds` — same ephemeral scene-local
  ownership, cleared in `shutdown()`.
- Render five selectable rows using the existing `renderOption(...)` helper
  and the existing single-selection behaviour `selectPet(...)` already
  demonstrates (selecting another replaces; selecting the current one
  clears). Reuse the existing interaction pattern rather than adding a new
  one.
- Add a fourth column/step header in the existing style — the current
  headers are `1. CHOOSE PET` / `2. EQUIP CARDS` / `3. EQUIP RELICS`, and
  the review block is labelled `4. REVIEW`. **Renumbering those headers is
  a presentation decision that must be made consistently in one pass**, so
  the review step does not end up labelled `4` while a Boss step is also
  `4`. Do not leave two steps sharing a number.
- Update the review block (`reviewText`) to show the **selected** Boss
  instead of `MVP_BOSS_ID`.

### 3. Wire the selection into the existing request

- `buildStartRequest()` submits `bossId: this.selectedBossId` — the
  selected canonical Identity.
- **Delete `MVP_BOSS_ID` entirely** rather than leaving it as a fallback.
  Leaving it is the AC-05 defect: a fallback silently re-creates exactly
  the hardcoding this task removes (see Default Behaviour below).
- No other member of the request changes. It stays exactly the four
  documented members (`API_CONTRACTS.md` §3).

### 4. Default behaviour — must be an explicit, documented decision

**This is the subtlest acceptance criterion, and the repository does not
currently answer it.** Today the Boss is fixed, so "no choice" was
coherent. Once a choice exists, exactly one of these must be true:

```text
(a) NO DEFAULT — the player must select a Boss; START BATTLE reports an
    unfilled selection until one is chosen.
(b) A DEFAULT — one Boss is pre-selected (Hỏa Long / boss-hoa-long is the
    value this task removes, and is the natural candidate).
```

**This task does not choose between them, because that is a product
decision and `AGENTS.md` §7/§20 forbid inventing one.** Evidence the
implementing agent must weigh:

- **For (a):** the scene's existing behaviour for every other slot. Pet
  starts `null` and the scene reports `'Choose a Pet first.'`
  (`describeIncompleteSelection()`, L434–L445). Cards and Relics likewise
  start empty. There is **no** pre-selected default anywhere in the Lobby
  today. Choosing (a) is therefore the option that is consistent with
  existing product behaviour and requires no new rule.
- **For (b):** a default would preserve a one-click path to a battle and
  avoids adding a fourth required step to the flow.

**Default for this task: (a), on the grounds that it is the only option
consistent with existing, observable product behaviour** — and AC-03
requires the choice to be *documented from existing behaviour*, not
invented. If the implementing agent concludes (b) is intended, that is a
**new product decision** and it must be confirmed before implementing
(see Stop Conditions). It must not be chosen merely because it is easier
to test.

### 5. Server authority is untouched

`requestStart()`'s completeness check is a *slot-completeness* check, not
a legality check — the scene must not begin deciding whether a Boss is
valid, owned, or unlocked. `BOSS_NOT_FOUND` remains the server's answer
for an invalid identity and must still be surfaced through the existing
`startError` path.

---

## Acceptance Criteria

### AC-01 — All five canonical MVP Bosses are available
The Lobby exposes exactly the five canonical MVP Bosses of
`BOSS_RULES.md` §6 — `boss-hoa-long`, `boss-thuy-ma`, `boss-moc-yeu`,
`boss-son-thach-ve`, `boss-kim-loi-vuong` — and no sixth entry, no
placeholder, and no invented Boss.

### AC-02 — One Boss can be selected
Exactly one Boss is selected at a time. Selecting a different Boss
replaces the selection; the scene never submits a list, a set, or a
count.

### AC-03 — Default behaviour is documented from existing product behaviour
The no-selection behaviour is stated explicitly in the completion
evidence and is justified by the Lobby's existing behaviour (see
"Default Behaviour"). It is not left implicit and not invented.

### AC-04 — The selected Boss ID reaches the existing Start Battle request
The submitted `BattleStartRequest.bossId` is the **selected** Boss's
canonical technical Identity, taken from the selection — not a literal,
not a derived value, and not `BossDefinitionId`.

### AC-05 — No hardcoded override back to `boss-hoa-long`
`MVP_BOSS_ID` is **removed**, and no fallback, default-valued, or
branch-substituted path re-introduces a fixed Boss. A grep for
`MVP_BOSS_ID` and for a hardcoded `'boss-hoa-long'` in the Lobby path
returns no submission-path hit. (`boss-hoa-long` may still legitimately
appear as *one of the five options* and in tests — the prohibition is on
it being imposed rather than chosen.)

### AC-06 — The backend remains authoritative
The client decides no Boss legality: it does not validate the identity,
does not check ownership, and does not compute any Boss stat. An invalid
submitted identity still produces the documented `400 BOSS_NOT_FOUND`
through the unchanged server path. No backend validation logic is
modified.

### AC-07 — The battle starts using the selected Boss
After a successful start, the created battle's Boss **is** the selected
Boss — confirmed by the authoritative surface identified in "Battle
State Verification", not by the client's own request echo.

### AC-08 — Existing Boss gameplay is unchanged
No Boss stat, Element, Passive, Skill, threshold, effect, magnitude, or
timing changes. No file under `src/backend/GameServer.Domain/Bosses/`
changes. Every existing Boss test passes unmodified.

### AC-09 — Existing authentication remains unchanged
No authentication, session, authorization, or development-auth code is
touched. An unauthenticated start still returns `401 UNAUTHENTICATED`.

### AC-10 — TASK-182 board input remains working
The board-input path still works after a battle started against a
non-default Boss. Verified by the existing board-input verification and,
in the browser smoke test, by real pointer input on the board.

### AC-11 — TASK-183 START BATTLE clickability remains working
START BATTLE is still clickable in the real browser. The render pass
still rebuilds hit areas correctly with the added Boss rows — note that
`LobbyScene` rebuilds every hit area on each render (`clearDynamicObjects`
+ `renderOption`), so a Boss row that is created but not registered (or
registered but not cleared) is a defect to catch here.

### AC-12 — Tests cover Boss selection
Unit/integration coverage exists for at least: all five Bosses are
offered; selecting each submits that Boss's identity; re-selection
replaces; the no-selection case behaves per AC-03; and a non-default
Boss's start is accepted. Existing `LobbyScene.test.ts` and
`BattleService.test.ts` assertions of the fixed `boss-hoa-long` must be
**updated to the new contract, not deleted** (`AGENTS.md` §15) — see
"Tests That Will Need Updating".

### AC-13 — Real browser smoke test
The complete flow is verified in a real browser with real pointer input
(see "Browser Acceptance"). Direct JavaScript invocation of a scene
callback is **not** acceptable as the sole proof.

### AC-14 — No unrelated gameplay systems are modified
Nothing outside the Lobby selection path, its tests, and the TDD.md §2.1
reconciliation changes. See NON-GOALS.

### Baseline criteria

- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)
- [ ] `TDD.md` §2.1 reconciled **before** implementation (`AGENTS.md` §4/§17)
- [ ] No Boss rule, stat, or identity changed (`BOSS_RULES.md` §6 owner)

---

## Battle State Verification

**How to prove the selected Boss actually became the authoritative Boss.**
The client must not self-certify this — an echoed request proves nothing
(`AGENTS.md` §10).

**The primary authoritative surface is the battle-start RESPONSE**, which
already carries the created Boss (`API_CONTRACTS.md` §3's "BattleState
summary"; `BattleModels.ts` `BattleStartInitialState.bossState`):

```text
POST /api/battle/start
  → 200 { battleId, initialState: { …, bossState: { bossId, element,
                                                     hp, maxHp, atk, def,
                                                     state } } }
```

`initialState.bossState.bossId` is the server's own statement of which
Boss the battle was created against, and `element`/`maxHp` independently
corroborate it per `BOSS_RULES.md` §6.1 — e.g. selecting
`boss-kim-loi-vuong` must report `element: "Kim"` and `maxHp: 2800`, not
the Hỏa Long row. **That cross-check is the strongest available proof**,
because it cannot be satisfied by echoing the request back.

**Do NOT rely on the SignalR `bossState` projection.** Per
`SIGNALR_PROTOCOL.md` §4.4 it carries **`hp` and `maxHp` only** — there is
no `bossId` member on the live push. `maxHp` is still a usable
corroborating signal (2800 vs 5000 vs 3000), but the live `bossId` must be
read from the start response or from the server-side battle state.

**Server-side / integration verification**, mirroring the existing
`tests/backend/GameServer.Api.Tests/BattleStartSmokeTest.cs` pattern
(L173 `Assert.Equal("boss-hoa-long", authoritative.BossState.BossId.Value)`):
assert against `BattleStartService`'s authoritative `BattleState` for each
of the five identities. `BattleStartEndpointTests.cs` (L298) already
loops the identities in its own validation test and is the closest
existing precedent.

---

## Browser Acceptance

A **real browser smoke test is required** and must use real pointer input.
The repository already has the harness pattern to follow:
`src/frontend/client/scripts/board-input-smoke.mjs` (TASK-182) and
`lobby-start-overlay-smoke.mjs` (TASK-183) drive a real Chromium over the
DevTools Protocol and produce trusted DOM/canvas input. Reuse that pattern
rather than inventing a new one.

```text
Open application
  → authenticate with the existing development auth        (TASK-181)
  → Lobby
  → observe 5 Boss choices rendered and interactive
  → select a NON-default Boss (e.g. boss-kim-loi-vuong)
  → select/confirm the Pet + Card + Relic loadout
  → click START BATTLE using REAL pointer interaction
  → BattleScene opens
  → authoritative state identifies the SELECTED Boss
```

Requirements on the run:

- The Boss selection and the START BATTLE click are real pointer events
  (`Input.dispatchMouseEvent`), not `scene.requestStart()` and not a
  synthetic `.click()` on a detached object.
- The selected Boss must be a **non-default** one — proving selection
  works and not merely that the flow still succeeds.
- The authoritative identification uses the surface in "Battle State
  Verification" (`initialState.bossState.bossId`, corroborated by
  `element`/`maxHp`).
- The run is **actually performed and its observed result reported**. An
  unverified claim of success is a failed criterion (see TASK-181's
  "Local Playability Verification" for the repository's stated standard).
- The completion evidence records which Boss was selected and what the
  authoritative response reported.

---

## Scope Boundaries

### In Scope

- A Boss selection source for the five canonical Bosses, chosen from
  Options A/B/C above with the repository's evidence as justification
- A `selectedBossId` scene field and a five-option selection interaction
  in `LobbyScene`, following the existing selection pattern
- Submitting the selected identity as `bossId` in the existing request
- Removing `MVP_BOSS_ID` and any fixed-Boss path
- Updating the review/summary presentation to show the selected Boss
- Updating affected frontend tests and adding selection coverage
- The `TDD.md` §2.1 reconciliation (and `GDD.md` §2 if it enumerates the
  steps), which must precede the code
- The real-browser smoke verification

### Out of Scope (NON-GOALS — do not implement any of these)

```text
Boss gameplay changes
Boss passive changes
Boss balance changes
Boss provisioning changes
Match-3 changes
Turn-order changes
Cards changes
Relics changes
Pets changes
Authentication changes
Discord OAuth
ResultScene
Campaign
Energy
Tickets
RPS
Speed mechanics
```

Also explicitly out of scope:

- **Any Boss-definition content edit.** `BOSS_RULES.md` §6, §6.1–§6.4 and
  `BossDefinitions.cs` are read-only references for this task.
- **A Boss-definition subsystem.** No Boss registry, no Boss repository,
  no Boss provisioning, no Boss unlock/ownership model, no Boss
  progression, no Boss difficulty/stage selection. Bosses are global
  static content with no ownership; nothing here introduces ownership.
- **Changing `ResolveBoss` or any backend validation.** It already
  implements the required contract.
- **Changing the `BattleStartRequest` shape or
  `GameRuntimePort.startBattle`'s signature.** It already carries
  `bossId`.
- **Multi-Boss, Boss rotation, random Boss, Boss phases, Boss rush, or
  stage/campaign selection.** Any of these is a new product feature.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

### Protocol Change Disclosure

**No protocol change is expected or authorized by this task.**
`API_CONTRACTS.md` §3 already documents `bossId` as an arbitrary valid MVP
Boss identity, and `SIGNALR_PROTOCOL.md` needs no change because no wire
message gains or loses a member.

- If the implementing agent concludes a **new REST endpoint** is required
  (Option A), that **is** a documented REST-surface change and must be
  handled explicitly under `AGENTS.md` §17 — `API_CONTRACTS.md` §1's
  endpoint summary and §5's collection-read sections must be updated in
  the same task, and the completion evidence must name the change.
- If anything appears to require a **new or changed SignalR message,
  member, or event** — STOP. That is a `SIGNALR_PROTOCOL.md` change and
  ADR-004 territory, well outside this task.
- **The implementation agent must not silently redesign the protocol.**
  Name any change explicitly or stop.

---

## Affected Files & Areas

```text
[x] src/frontend/client/ (LobbyScene selection + request construction;
                          a Boss selection source per the chosen option;
                          possibly a GameRuntimePort capability if Option A)
[ ] src/backend/ (NOT expected. ResolveBoss + BossDefinitions already
                  implement the contract. Only Option A would add a small
                  read-only endpoint — and that requires API_CONTRACTS.md
                  to be updated in the same task.)
[x] tests/ (frontend LobbyScene/BattleService selection coverage; backend
            start-per-Boss coverage if not already present)
[x] docs/ (TDD.md §2.1 — REQUIRED, before implementation; GDD.md §2 if it
           enumerates the Lobby steps; API_CONTRACTS.md only if Option A)
[ ] Database schema / migrations — NOT expected. If a schema change appears
    necessary, STOP: that is not this task.
[ ] Boss definitions / gameplay rules — FORBIDDEN. Read-only references.
```

---

## Tests That Will Need Updating

These existing assertions encode the fixed-Boss behaviour and will fail
once the contract changes. They must be **updated to assert the new
contract**, never deleted or weakened (`AGENTS.md` §15):

```text
src/frontend/client/tests/LobbyScene.test.ts
    L529-537  'submits the fixed MVP Boss identity' → must become
              selection-driven coverage
    L619      fixture asserting bossId: 'boss-hoa-long'
src/frontend/client/tests/BattleService.test.ts
    L40, L120, L248, L477  use 'boss-hoa-long' as the request/response
              fixture value. These assert the transport faithfully carries
              what it is given and remain valid as *fixtures* — verify they
              do not assert that the value is imposed.
src/frontend/client/tests/GameRuntime.test.ts
    L1068, L2072  same fixture role — verify, do not assume.
src/frontend/client/tests/RuntimeBoundaries.test.ts
    L206  lists 'boss-hoa-long' — check what it asserts before changing.
```

The same applies backend-side: `BattleStartEndpointTests.cs` L298 already
loops the three-then-five identities and is a validation test, not a
fixed-Boss assertion — **read it before touching it.**

---

## Unrelated Findings (report, do not fix)

Per `AGENTS.md` §16, these are recorded as notes. Do **not** fix them in
this task:

1. **Stale comment in `BattleStartService.cs` L249–253** — says
   `BOSS_RULES.md` §6 "defines exactly three content-defined MVP Bosses"
   and that "[t]he remaining two" are "not yet content-defined". The code
   correctly iterates all five (`BossDefinitions.All`); only the comment is
   stale after TASK-172. **Impact:** misleading to the next reader, and
   directly in the neighbourhood this task edits. **Suggested follow-up:**
   a small documentation-synchronization task (or fold into this task only
   if the reviewer explicitly widens scope — otherwise leave it and report
   it).
2. **`BossStartService.ResolveBoss`'s XML doc (L498–500)** repeats the
   same "three transcribed definitions" wording.
3. **`BossDefinitionLookup` / `IBossDefinitionLookup`** exist for the
   persistence/initialization path. **Do not assume** this task must use
   it — establish from the code whether it is reachable from a read
   endpoint. It is not a Boss-list source for the Lobby today.

---

## Implementation Notes

- Reuse, in rough order: the existing `renderOption` / `renderNote`
  helpers, the existing `selectPet` single-selection pattern, the existing
  `selected*` scene-field + `shutdown()` clearing convention, and the
  existing `render()` rebuild discipline. **Do not** add a store, manager,
  service, or module for the selection (`AGENTS.md` §9,
  `ARCHITECTURE.md` §5; the scene's own header comment states the
  in-progress selection "lives nowhere else").
- **Do not let the scene import `fetch`, `ApiService`, or
  `@microsoft/signalr`.** If a server read is needed (Option A), it is a
  `GameRuntimePort` capability, exactly as `getPets()`/`getCards()`/
  `getRelics()` are (`ARCHITECTURE.md` §2.2.1 rule 1, §2.2.3 rule 3).
- `describeIncompleteSelection()` is the natural place to add the
  no-selection message **if** AC-03 resolves to option (a). Keep the
  message style consistent with the existing ones (`'Choose a Pet first.'`).
- START BATTLE's enabled/disabled styling currently keys off
  `startPending`/`loading` only (`drawStartTrigger`, L592). Whether the
  Boss slot should also gate the button's *appearance* is a presentation
  decision — be consistent with how the Pet slot behaves today.
- The review block's Boss line currently reads `` `4. REVIEW — Boss: ${MVP_BOSS_ID}` ``.
  Update the label and the value together, and keep the step numbering
  coherent with the header changes (see Proposed Scope §2).
- A browser smoke script belongs in `src/frontend/client/scripts/`
  following the existing `*-smoke.mjs` convention.
- If an unrelated gameplay ambiguity is discovered while working,
  **record it as a note/blocker only** — do not fix it here.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — LobbyScene selection: five options offered; each
                         of the five identities submitted when chosen;
                         re-selection replaces; no-selection behaviour per
                         AC-03; the request still has exactly the four
                         documented members
[x] Integration tests  — a battle started with a NON-default Boss resolves
                         that Boss authoritatively (start response's
                         bossState.bossId + element/maxHp corroboration);
                         an invalid identity still yields 400 BOSS_NOT_FOUND
[ ] Gameplay scenarios — N/A: this task changes no gameplay rule. Every
                         existing Boss/combat test must pass UNMODIFIED.
```

### Key Edge Cases

- Selecting a **non-default** Boss and confirming the authoritative Boss
  changed — the central case; a default-Boss-only test proves nothing
- Selecting Boss A, then Boss B: **B** is submitted, never both and never A
- Selecting the currently selected Boss: behaviour is consistent with the
  Pet slot's toggle (verify against `selectPet`, do not assume)
- Starting with **no** Boss selected: behaves per AC-03
- An invalid Boss identity reaching the server: still
  `400 BOSS_NOT_FOUND`, surfaced through the existing error path, with the
  selection intact for retry
- All five identities round-trip exactly — including the diacritic-derived
  `boss-son-thach-ve` / `boss-kim-loi-vuong` spellings, which are the most
  likely to be mis-transcribed
- The interaction still works after a collection reload re-render (hit
  areas are rebuilt on every render)
- `TASK-182` board input and `TASK-183` START BATTLE clickability still
  work after the change

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20) always apply. Stop and report
— do not guess and do not implement — if any of these fires:

- **The canonical five Bosses cannot be determined** from `BOSS_RULES.md`
  §6/§6.4 and `BossDefinitions.cs`, or those two disagree. (`§6.4` is the
  identity contract; a disagreement is an `AGENTS.md` §4 conflict.)
- **The TDD.md §2.1 reconciliation is not confirmed.** If the Lobby is not
  authorized to gain a Boss-selection step, STOP — do not implement
  against a document that says the feature does not exist, and do not edit
  `TDD.md` to match already-written code.
- **Existing frontend/backend Boss contracts conflict** — e.g. if a Boss
  list source exists that contradicts `BOSS_RULES.md` §6, or if
  `API_CONTRACTS.md` §3's `bossId` contract disagrees with
  `BattleStartRequest`.
- **A new Product Owner gameplay decision is required** — including the
  AC-03 default question if the implementing agent cannot justify option
  (a) from existing behaviour, and any question about *which* Bosses are
  selectable, whether Bosses must be unlocked, or whether the player picks
  a stage/difficulty.
- **Implementing selection requires changing Boss gameplay** — any change
  to a Boss stat, Element, Passive, threshold, Skill, effect, magnitude,
  or timing. That is a GAMEPLAY-CHANGE task, not this one.
- **The existing Start Battle contract is insufficient** and requires
  substantial protocol redesign — e.g. `bossId` cannot carry the
  selection, or a new member/event is needed. Report the exact gap.
- **A new backend API would become more than a small read-only endpoint**
  (Option A). If it grows a Boss repository, a definition-management
  surface, an ownership model, or anything resembling a subsystem: STOP —
  reconsider Option B, or report for decomposition.
- **A database schema change or migration appears necessary.** Not this
  task.
- **Reaching the Lobby/Battle for verification is blocked** by something
  other than development authentication (e.g. the TASK-183 overlay
  regresses, or `god`-mode local config is unavailable) — report the
  blocker rather than weakening the verification to a non-browser proof.
- If this task exceeds 7 skills or crosses multiple uncoupled
  architectural boundaries: **STOP & decompose**.

---

## Relationship to TASK-078

`TASK-078` (`tasks/completed/TASK-078-client-lobby-scene-match-start.md`)
implemented the Lobby's match-start flow and explicitly chose D4 — "MVP
battle start uses the fixed Boss ID `boss-hoa-long`" — while noting
(L256) that "`bossId` is one fixed MVP value (`"boss-hoa-long"`); there is
no Boss" selection. That decision was consistent with `TDD.md` §2.1's
"no MVP Boss-selection step".

**TASK-185 supersedes TASK-078's fixed-Boss decision.** The implementing
agent should read TASK-078's D4 and its L425 note to confirm this reading,
and should **not** modify the completed task file (`tasks/completed/` is
historical record).

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual. Report the ACTUAL observed result.
-->

### Changed Files
- `docs/02-technical/TDD.md` — §2.1 `LobbyScene` bullet reconciled: "There is
  **no MVP Boss-selection step**" is replaced by the selection step (five
  canonical MVP Bosses, no pre-selection, server-resolved identity); §2.1 flow
  list and version note 1.3 updated.
- `docs/00-overview/GDD.md` — §2's pre-battle step list gains "Choose Boss"
  (`GDD.md` outranks `TDD.md`); version note 2.5.
- `docs/02-technical/ARCHITECTURE.md` — §2.2.3 flow list, selection diagram
  (`petId · bossId · cardLoadout[] · relicLoadout[]`), rule 1's description of
  the in-progress selection, and rule 5's server-validation sentence; version
  note 1.5. Rule 6 is unchanged (the catalog is not a port capability).
- `src/frontend/client/src/game/scenes/LobbyScene.ts` — `MVP_BOSS_ID` deleted;
  `MvpBossOption` + `MVP_BOSSES` static catalog (the five §6.4 identities);
  `selectedBossId` scene field (starts `null`); `selectBoss()`; `renderBosses()`
  and the `4. CHOOSE BOSS` band; review renumbered to `5. REVIEW` and reporting
  the selected Boss; `buildStartRequest()` submits `this.selectedBossId`;
  `describeIncompleteSelection()` gains `Choose a Boss.`; `shutdown()` clears the
  new field.
- `src/frontend/client/tests/LobbyScene.test.ts` — the fixed-Boss assertions
  replaced by selection-driven coverage: the five catalog identities; initial
  `null`; each Boss selectable; replacement; toggle-off; complete-loadout
  no-submit; exact identity submission; review; shutdown reset; boundary
  assertions re-aimed at "no Boss data source / no Boss gameplay value".
- `src/frontend/client/tests/RuntimeBoundaries.test.ts` — comment and test name
  that described the Boss as "one fixed request value the scene supplies"
  corrected (comments/name only; every assertion unchanged).
- `tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs` —
  `Start_ShouldEmitTheDocumentedWireValueForTheBossElement` now covers all five
  canonical Bosses (`boss-son-thach-ve` → `Earth`, `boss-kim-loi-vuong` →
  `Metal` were missing) and its stale "exactly three content-defined MVP Bosses"
  comment is corrected; `Start_ShouldNeverEmitADomainEnumMemberName_ForEitherElementMember`
  now loops all five identities, as its own comment already claimed. No
  production backend file changed.
- `src/frontend/client/scripts/boss-selection-smoke.mjs` — new TASK-185
  real-browser (CDP) verification harness.

### Documentation Reconciliation
- [x] `TDD.md` §2.1 updated **before** implementation (the PO decision in this
      task is the confirmation required by the task's reconciliation section;
      recorded in the document's own version note as 1.3)
- [x] `GDD.md` §2 checked: it **does** enumerate the pre-battle steps
      ("Choose Pet → Equip Cards → Equip Relics → Start Battle"), so the Boss
      step was added there too — GDD.md outranks TDD.md, and leaving it out
      would have left the higher-authority enumeration contradicting the new
      TDD statement. Version note 2.5
- [x] `API_CONTRACTS.md` **not** changed: Option B was chosen (no endpoint), and
      §3 already documents `bossId` as any valid MVP Boss canonical Identity
- [x] `MVP_SCOPE.md` §1/§2 re-checked: §1 keeps "5 Bosses / Element, Passive,
      Skill per Boss" IN; §2's OUT list does not mention Boss selection; no OUT
      item touched. No `MVP_SCOPE.md` change needed
- Also reconciled (dependent technical boundary that enumerates the same flow):
      `ARCHITECTURE.md` §2.2.3. `ROADMAP.md` and `GAME_RULES.md` contain no
      Boss-selection statement and were not changed. `TASK-078` and every other
      completed task file were left untouched.

### Boss Selection Source Decision
**Option B — a static client-side selection catalog**, held in `LobbyScene.ts`
as `MVP_BOSSES` (the scene already owns its in-progress selection and its own
display constants, e.g. `CATEGORY_COLORS`; `ARCHITECTURE.md` §5 and `AGENTS.md`
§9 forbid adding protocol surface speculatively).

Repository support: the client has **no** Boss data source and no Boss endpoint
exists (`API_CONTRACTS.md` §1 lists pets/cards/relics collection reads only);
Bosses are global static content with **no ownership**, which is exactly why
Pets/Cards/Relics are read from the server (their ownership varies per player)
and Bosses are not; and the five identities are content-frozen by
`BOSS_RULES.md` §6.4.

Traceability to `BOSS_RULES.md` §6.4: each entry is the §6.4 Identity verbatim
(`boss-hoa-long`, `boss-thuy-ma`, `boss-moc-yeu`, `boss-son-thach-ve`,
`boss-kim-loi-vuong`), the catalog's doc comment names §6/§6.4 as the owner and
states it is a transcription rather than a definition, the unit test asserts the
five literals, and the browser harness asserts them from its own independent
copy. The catalog contains only `bossId` / `displayName` / `element` — no stat,
threshold, Passive, Skill, or balance value.

### Default Behaviour Decision (AC-03)
**Option (a) — no default; the player must choose.** `selectedBossId` starts
`null` and `START BATTLE` does not submit without it (it reports
`Choose a Boss.`).

This is the option the Lobby's existing, observable behaviour already
implements for every other slot: the Pet starts `null` and reports
`Choose a Pet first.`, and the Card and Relic slots start empty. No slot in the
Lobby is pre-selected, so adding a pre-selected Boss would have introduced the
only default in the flow — and it would have re-created the hardcoded
`boss-hoa-long` this task removes. Both the unit suite and the browser smoke
verify that a complete loadout with no Boss sends no request.

### Validation Results
- `npx tsc --noEmit` (src/frontend/client) — PASS (0 errors)
- `npm run test:run` (src/frontend/client) — PASS (562 tests, 19 files)
- `npm run build` (src/frontend/client) — PASS (`tsc` + `vite build`)
- `dotnet test tests/backend/GameServer.Api.Tests` — PASS (345 tests, 0 failed;
  34 in `BattleStartEndpointTests`, up from 32)
- `dotnet test tests/backend/GameServer.Application.Tests` — PASS (557 tests)
- `node scripts/boss-selection-smoke.mjs http://localhost:5173/` — PASS
  (2 sessions × 28 checks, 0 failures)

### Authoritative Boss Verification (AC-07)

Report the **actual** observed values.

```text
CASE A — Thủy Ma
Selected Boss (canonical Identity)     : boss-thuy-ma
Request body bossId                    : boss-thuy-ma
Response initialState.bossState.bossId : boss-thuy-ma
Response initialState.bossState.element: Water
Response initialState.bossState.maxHP  : 5000
Response initialState.bossState.atk/def: 100 / 50
→ Consistent with BOSS_RULES.md §6.1?   YES

CASE B — Kim Lôi Vương
Selected Boss (canonical Identity)     : boss-kim-loi-vuong
Request body bossId                    : boss-kim-loi-vuong
Response initialState.bossState.bossId : boss-kim-loi-vuong
Response initialState.bossState.element: Metal
Response initialState.bossState.maxHP  : 2800
Response initialState.bossState.atk/def: 140 / 0
→ Consistent with BOSS_RULES.md §6.1?   YES
```

Both `bossId` values and both bodies were read from the POST's own network
traffic (request body and response body), not from the client's request echo.

### Browser Smoke Test (AC-13)

Report what was **actually** observed in the real browser run.

```text
[x] Application opened and dev auth reached an authenticated session
    ("Runtime Status", BACKEND Connected, SIGNALR Connected)
[x] Lobby reached (MainMenu trigger clicked with a real pointer event)
[x] 5 Boss choices rendered — Hỏa Long/Hỏa, Thủy Ma/Thủy, Mộc Yêu/Mộc,
    Sơn Thạch Vệ/Thổ, Kim Lôi Vương/Kim — and exactly those five
[x] No Boss initially selected: selectedBossId = null, all five rows '○',
    review line "5. REVIEW — Boss: (none chosen)"
[x] Complete loadout (Pet + 3 Basic Cards + 3 Relics) with NO Boss chosen:
    real click on START BATTLE produced 0 runtime-port calls and
    0 POST /api/battle/start requests; the scene stayed in LobbyScene and
    reported "Choose a Boss."            (AC-05)
[x] A NON-default Boss selected with a real pointer event:
    Case A boss-thuy-ma, Case B boss-kim-loi-vuong
[x] Loadout selected (1 Pet + 3 Cards + 3 Relics) with real pointer events
[x] START BATTLE clicked with a real pointer event at its own centre
    (1080,600); elementFromPoint there is CANVAS, not the overlay
[x] Exactly one POST /api/battle/start left the browser per activation
    (network-level count 1; runtime-port count 1; no duplicate)
[x] BattleScene opened
[x] Authoritative state identified the SELECTED Boss
    (initialState.bossState.bossId == selectedBossId in both cases,
     corroborated by element and maxHP as above)
[x] Board input still works (TASK-182 regression check): 128 board children
    = 64 cells; two real pointer clicks on adjacent cells reached
    onCellTapped; exactly one Swap request per gesture; the server accepted
    the Swap; the authoritative board re-rendered from the pushed state
[x] START BATTLE clickability still works (TASK-183 regression check):
    Runtime Status overlay is present with pointer-events: none, and the
    topmost element at the button's centre and at the Boss rows is CANVAS
[x] Smoke script path: src/frontend/client/scripts/boss-selection-smoke.mjs
```

Screenshots and the JSON report were written to
`src/frontend/client/boss-selection-shots/` during the run and removed
afterwards (local verification artifacts; not added to the repository).

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic: the scene submits
      `string` identities and computes no Boss value
- [x] Confirmed the client validates no Boss legality — it does not check
      ownership, unlock state, or validity of the selected identity; the
      submitted identity is the catalog value as chosen
- [x] Confirmed the backend was **not** modified (Option B: no endpoint, no
      `ResolveBoss` or `BossDefinitions` change; only the two endpoint test
      coverages above were widened)
- [x] Confirmed no Boss rule, stat, Element, Passive, or Skill changed
      (`BOSS_RULES.md` §6.1–§6.4 values are reproduced only as the two
      corroborating assertions in the smoke harness)
- [x] Confirmed no `MVP_BOSS_ID` and no imposed fixed `boss-hoa-long` literal
      remains on the submission path: `MVP_BOSS_ID` is deleted, `bossId` is
      `this.selectedBossId`, and a unit test asserts each of the five identities
      reaches the request from the selection
- [x] Confirmed no schema or migration change
- [x] Confirmed no authentication change (dev auth reused unchanged)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no NON-GOAL item was implemented
- [x] Confirmed the "Unrelated Findings" were reported, not fixed

---

## Revision History

**Revision 2 — completed.** Implementation, documentation reconciliation, tests,
and the real-browser verification recorded under "Completion Evidence" above.
Two notes for the reviewer:

1. `GDD.md` §2 and `ARCHITECTURE.md` §2.2.3 enumerate the same pre-battle flow
   `TDD.md` §2.1 does, so both were reconciled with it (GDD.md outranks TDD.md;
   ARCHITECTURE.md is the dependent technical boundary for the same flow). The
   minimum wording changed in each: the step list, the selection diagram, and
   rule 1's description of the in-progress selection.
2. Two stale backend **test** statements about the Boss roster ("exactly three
   content-defined MVP Bosses") are in the same file whose five-Boss coverage
   this task widened, so they were corrected with it; the stale comment in
   `BattleStartService.cs` L249–253 and its `ResolveBoss` XML doc (Unrelated
   Findings 1–2) were left untouched and are still open.

**Revision 1 — task created.** Generated from the MVP playability gap
identified after TASK-181/182/183/184: the backend provisions and resolves
all five canonical MVP Bosses, but `LobbyScene` hardcodes exactly one.

Three findings shaped this specification rather than being copied from the
request that generated it:

1. **`TDD.md` §2.1 explicitly states "There is no MVP Boss-selection
   step."** The requested feature therefore *contradicts an authoritative
   technical-design statement*. Per `AGENTS.md` §4/§17 this is a
   documentation decision that must be confirmed and applied **before**
   implementation, so the task leads with that reconciliation instead of
   treating it as an implementation detail.
2. **The backend contract already accepts all five Bosses.**
   `BattleStartService.ResolveBoss` iterates `BossDefinitions.All` and
   `API_CONTRACTS.md` §3 already documents `bossId` as an arbitrary valid
   MVP Boss identity — so this task removes a *client* limitation and
   requires no backend capability. The task forbids the new-endpoint
   reflex and makes a small read-only endpoint the conditional Option A,
   not the default.
3. **The client has no Boss data source at all**, and the delivered live
   SignalR `bossState` projection carries **only `hp`/`maxHp`** — no
   `bossId`. The verification path is therefore the battle-start
   response's `initialState.bossState.bossId` corroborated by `element`
   and `maxHp`, which the task states explicitly so the implementing agent
   does not design a verification against a field that does not exist.

The default-selection question (AC-03) is deliberately left as a decision
to be justified from existing product behaviour rather than resolved
here, because choosing one would be inventing a product decision
(`AGENTS.md` §7/§20).
