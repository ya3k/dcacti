# Architecture Decision Records (ADR)

**Version:** 1.22 (§7 — **ADR-029 added** per `TASK-303`, applying the explicit
Product Owner decision that the Boss behaves as a genuine opponent using the same
resource and cast principles as the Player. ADR-029 records: introduction of
`BossState.Power` (integer 0–100) into MVP, which **supersedes `TASK-258` §2 D2**
and partially supersedes `ADR-026` Decision item 4's POWER bullet and its
Unresolved Balance Choices #1–#2; the one shared actor-relative
resource-generation and cost-validation rule with actor-specific effects
preserved; preserved `PetState`/`BossState` separation with shared cast
contracts, validation and effect services; Relics remaining Player-only; the
`Boss Card` category with the minimum MVP capability (one authored Boss Card per
Boss); encounter mechanics staying passive/trigger-based; the explicit
deterministic one-action-per-response-phase policy with its eligibility,
ordering, cooldown and tie-break rules; the dedicated `BossCardCast` Boss action
event with the Player `CardCast` wire contract preserved; the Boss Power
projection and reconnect contract (`bossState.power`); the four-slice staged
migration with the `SkillCharge` removal gate; and six explicitly unresolved
balance values. **`ADR-026` remains Accepted** and receives the same
in-place-amendment treatment as its `TASK-265`/`TASK-267`/`TASK-299` amendments —
its POWER bullet is corrected, not silently rewritten, and its historical content
is preserved. No other ADR changes status. Prior 1.21: (§7 — **ADR-026 amended in place per `TASK-299`**: the DEF-Gem bullet of Decision item 4 is corrected so a confirmed DEF-Gem match grants the acting actor a temporary `TargetStat = "DEF"` `BuffDebuff` (the `"DefenseBuff"` identity) that raises its **effective DEF** through the opponent's next attack instead of converting the generated `DefensePool` into a `Shield` absorption pool. No new ADR was created and no decision is reversed: shared board, actor-relative ownership and target resolution, the `+5` per-Gem output, the atomic Round, the existing `StatusEffects[]` representation, and the HP/POWER/ATK bullets are unchanged, so `adr-change.md` §3's superseding path does not apply and the record receives the same in-place-amendment treatment as its own `TASK-265`/`TASK-267` amendments. Rule ownership stays with `COMBAT_RULES.md` (§2, §3.2, new §5.4.6/§5.5.6) and `GAME_RULES.md` §17; ADR-026 remains **Accepted**. Prior 1.20: §7 — ADR-028 added per `TASK-288`: retirement of Special Gems and match-tier multipliers from battle-state model and realtime contracts. Following operator design decisions OD-1 through OD-7 and reconciliation decision B-1 (finalized under TASK-286/TASK-287), Special Gems and match-tier resource multipliers are retired from the battle-state model (`BoardState.Cells[64]` carries pure `GemType`), SignalR contracts (`createdSpecialGems` and `CellPayload.specialGem` retired), and Redis persistence (tolerant-reader deserialization basis per B-2, draining battles prior to joint deployment). Runtime code changes are strictly gated behind subsequent technical documentation and implementation tasks. Status: Accepted. Prior 1.19: §7 — ADR-027 added per `TASK-278`: the authoritative cascade
pass transition reporting decision. The Operator Decision approving TASK-278
A-1(i) and A-4 ("Approve the addition of `CascadeStepResolved` to the existing
`ReceiveEvents` batch protocol") authorizes one **additive event** inside the
round's existing single atomic batch, reporting a detection pass's completed
Gravity and Spawn with `passDepth`, `dropMoves` (authoritative origin and
destination per gem movement) and `spawnedGems` (authoritative destination and
gem type per spawn), omitted when empty, emitted once per recorded pass as that
pass's last item, and never for the terminating pass that detects no Match. The
deltas-only choice is recorded and a **per-pass board is rejected** as redundant
with the state push, a second authoritative source of board state, and
prohibited by `SIGNALR_PROTOCOL.md` §3.2.12 item 1 / §8 item 5. Redis CAS, the
single atomic round, `Sequence` semantics, existing event sequence semantics,
turn banner behavior and snapshot recovery are **unchanged**; RNG stays
server-authoritative; intermediate board configurations stay Transient
Resolution State (never written to Redis mid-resolution, never in a snapshot,
never delivered as a board); the per-pass-board/message/partial-batch
prohibitions are **reconciled narrowly, not weakened**; client use is
presentation-only and a missing, malformed or inconsistent item is skipped; and
compatibility is one additive discriminator plus an explicit tolerant-reader
rule with **no version negotiation**. The governing documents
(`GAME_RULES.md` §16, `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`, `GAME_STATE.md`)
were amended first and the ADR written last as a historical record. Status:
Accepted. `TASK-278` remains open pending review. Prior 1.18: §7 — ADR-026 added per `TASK-259`: the shared-board gameplay and actor-relative match resolution architecture decision. Player and Boss share one 8×8 Match-3 board; both sides act by making valid swaps; matched cell type determines effect; acting side determines ownership and target; ATK damage is strictly gated on confirmed ATK matches; fallback fixed basic attacks are retired; Boss skills deal zero direct damage; Boss SkillCharge charges exclusively from Boss POWER matches; atomic round commits via single Redis CAS update. Status: Accepted. Prior 1.17: §7 — ADR-025 added per `TASK-256`: the swap action preconditions and idempotency architecture decision, superseding ADR-010. Replaces the coordinate-pair restriction with a dual state-precondition (`expectedSequence`) and action-idempotency (`actionId`) contract, restoring legitimate consecutive-turn coordinate reuse. Prior 1.16: §7 — ADR-024 added per `TASK-250`: the database-driven
boss balance configuration architecture decision. Adopts Option 1 (Domain
Overlay) to make PostgreSQL authoritative at runtime for boss combat balance
(`MaxHP`, `ATK`, `DEF`, `EnrageThreshold`) while preserving `BossSkillDefinition.SecondaryEffect`
(Flame Burst Burn, Drain Power, Root) and offline unit test isolation by overlaying
runtime database stats onto static Domain baselines. Records that implementation
(EF Core migrations and wiring) is deferred to `TASK-251`. Status: Accepted. Prior 1.15: §7 — ADR-023 added per `TASK-236`: the backend Discord
identity seam resolution decision. Following `ADR-020`'s adoption of standalone
web account authentication and retirement of Discord dependencies, the Product
Owner explicitly authorized `REMOVE` (Option 1) to formally retire the dormant
backend Discord identity dependency injection seam (`IDiscordIdentityResolver`,
`UnconfiguredDiscordIdentityResolver`, `DevelopmentDiscordIdentityResolver`,
`DiscordCredentialOptions`, `DevelopmentAuthenticationOptions`, and contract
definitions). ADR-023 records the accepted architectural decision and clarifies
that physical deletion of runtime C# files, DI registrations, and test stubs is
deferred to downstream implementation work. Prior 1.14: §7 — ADR-022 added per `TASK-203`: the post-result
preserved-loadout carrier. `D-202-03 = D` requires the loadout used in the battle
that just ended to survive the `LobbyScene` → `BattleScene` → `ResultScene` →
`LobbyScene` round trip and stay editable, which the ephemeral scene-local
selection of `ARCHITECTURE.md` §2.2.3 rule 1 cannot do. The ADR records the
carrier the requirement left open: a second documented key in Phaser's game-wide
registry, behind the `game/state/PreservedLoadout.ts` accessor, owned by the
client game-presentation layer, alive for the running game instance, written on
a successful battle start, and read only on the approved `ResultScene` →
`PLAY AGAIN` entry — so the preserved loadout, active battle state, and battle
result data stay three distinct concepts and `clearActiveBattleState()` never
clears the preserved loadout. It also records the dedicated client-local
`clearActiveBattleState()` port capability for `D-202-04 = A` (no disconnect, no
wire message) as distinct from `SIGNALR_PROTOCOL.md` §7.3's
`BATTLE_NOT_FOUND` path. No game rule, API contract, wire member, Redis key, or
database column changes; ADR-021 remains Accepted and unchanged. Prior 1.13: §7 — ADR-021 added per `TASK-191` Q-4: the Card-cast
restriction decision. The Product Owner approved **OPTION B — ONE CARD CAST PER
TURN**: a player may successfully cast at most one Card during each committed
Match-3 Turn, and the restriction is a **cast-count constraint, not Turn
consumption** — a Card cast still consumes no Turn, does not resolve the Match-3
board, and does not independently trigger the Boss response, so
`CARD_RULES.md` §3 item 5 and `MATCH3_RULES.md` §8.1 item 5 remain in force. The
Match-3 Turn remains the authoritative unit of combat progression and the Boss
response is reached only through a committed Swap (`GAME_RULES.md` §17 step 18).
Options A (cast consumes a Turn and triggers the Boss response) and C (keep
unlimited free casts and change only Power Charge's cost) are recorded as
rejected. ADR-020 remains Accepted and unchanged. Prior 1.12: §7 — ADR-019 added per TASK-036: the Discord credential
secret-hygiene decision. The developer-local channel is the project's .NET
user-secrets store and the deployment channel a host environment variable (D1);
`src/backend/.env.example` keeps a Discord section but is corrected to document
configuration key names and the approved channels rather than the unread,
un-ignored `src/backend/GameServer.Api/.env` path (D2); the tracked
`appsettings.json` placeholder stays an empty string, with "absent" defined as
null/empty/whitespace (D3); a missing credential fails startup in Production
while Development starts and keeps the unchanged `503 DISCORD_UNAVAILABLE`,
leaving TASK-181's opt-in development authentication independent (D4); rotation
is manual, operator-owned, and event-triggered with no scheduled cadence and a
restart/redeploy required (D5); and the existing local credential is rotated once
as a precaution, recorded as an operator action (D6). ADR-007, ADR-013, and
ADR-015 are unchanged, and TASK-036 is unblocked. Prior 1.11: §7 — ADR-018 amended per TASK-134 D11: the `CardCost` effect's
runtime state carrier is now recorded as Decision 12 —
`PetState.CardCostModifiers[]` (`SourceIdentity` + `CostReductionPercentage`), a
dedicated source-specific `BattleState` collection separate from
`StatusEffects[]`, `target = Pet` / `lifetime = Battle`, same-source
replace/refresh, additive composition capped at 100% for `CardCost` only, no
PostgreSQL persistence, no new Redis key, no new SignalR event/method. The
amendment is appended to ADR-018 and its items 1–11 and decision history are
preserved unmodified; no new ADR was created, because the amendment closes the
runtime gap ADR-018 item 5 deliberately left open rather than recording a new
decision. ADR-017 remains Accepted and unchanged. Prior 1.10: §7 — ADR-018 added per TASK-131 D11: the structured Relic
Trigger/Condition/Effect contract. The decision establishes a cross-layer
runtime/content/storage contract spanning `RELIC_RULES.md`, `DATABASE.md`, the
Domain Relic content model, and the realtime event surface, and it supersedes
TASK-082 R2-7 for `RelicDefinition.EffectDefinition` — the member TASK-109 had
left under it. ADR-017 remains Accepted and unchanged. Prior 1.9 — TASK-119 checked: the `BuffDebuff` `TargetStat = "ATK"`
consumption gap was not listed here, is not an ADR-level open item, and is
recorded as checked-and-closed; the resolved rule is `COMBAT_RULES.md` §5.4.
No ADR was added or edited. Prior 1.8: ADR-017 added per TASK-117 — the architecture decision
TASK-116's Product Owner decision set requires: `PetState.NextAttackCritModifiers[]`
is authoritative battle runtime state, separate from `StatusEffects[]`, persisting
across Turns until consumed by a qualifying owner attack. ADR-016 remains
Accepted and unchanged. Prior 1.7: ADR-016 note added per TASK-062 — the twelve Pet XP
balance/reward decisions that ADR-016 item 14 deferred to `PET_RULES.md`
§5.2 are now finalized in `PET_RULES.md` §5.1–§5.5 (Pet Level range 1–50,
Pet XP hard-capped at 4900). ADR-016 remains Accepted; its deferral record
is preserved as historical context. Prior 1.6: ADR-016 added — independent Player XP and Pet XP
progression tracks: Player owns persistent XP / Level with a capped Level
and uncapped XP, Pet owns independent per-instance XP / Level, the
`Player.Level × PetLevelMultiplier` derivation and the `PetLevelMultiplier`
field are RETIRED, and the Pet XP balance values were then unresolved human
gameplay decisions; ADR-012 items 3, 4, 6 and ADR-011 item 7 partially
superseded by ADR-016. Prior 1.5: ADR-015 added — application session
authentication contract:
signed JWT, stateless, `player_id` claim, Bearer + SignalR access-token
propagation, 24h absolute expiry, JWT Bearer enforcement;
prior 1.4: ADR-014 added — `BattleState.PlayerId` as the
battle-end owner-identity source (server-only, not a wire member);
`PetState.PetId` = owned Pet instance; `BattleResultId` = `BattleId`;
prior 1.3: ADR-013 added — Discord authorization-code → identity
exchange contract; prior 1.2: ADR-012 added — Player Level / Pet Level
formula / ownership-equipment / MVP scope closure)
**Status:** Active

> This document answers: **"Why did we make this technical/design
> decision?"** It does not describe how a system works — that belongs to
> `docs/02-technical/`. It does not define game rules — that belongs to
> `docs/01-game-design/`.

---

# 1. Purpose

An ADR records a decision that has **already been made** and is reflected in
the existing documentation (`docs/00-overview/`, `docs/01-game-design/`,
`docs/02-technical/`). It exists so a future developer or AI agent can
understand *why* the current architecture looks the way it does, without
re-deriving it from scratch or re-litigating it by accident.

ADRs are historical records, not design proposals. Writing an ADR does not
create or change a decision — it documents one that the rest of the
documentation already establishes.

---

# 2. When to Create an ADR

Create an ADR when a decision is:

```text
architecturally important
difficult to reverse
likely to be questioned later
important for AI agents implementing tasks
important for future developers
cross-cutting (affects multiple systems/layers)
security-sensitive
state-management-sensitive
infrastructure-sensitive
```

Do NOT create an ADR for a trivial implementation detail, a decision that
is not yet actually made (see §5), or one that duplicates content already
fully owned by a technical document.

---

# 3. Source of Truth

```text
docs/00-overview/
        ↓
docs/01-game-design/
        ↓
docs/02-technical/
        ↓
docs/03-decisions/ADR/
```

**An ADR must never override a game or technical document.** If an ADR
appears to conflict with `GDD.md`, `GAME_RULES.md`, any domain rule
document, `TDD.md`, `ARCHITECTURE.md`, `GAME_STATE.md`, `GAME_EVENTS.md`,
`API_CONTRACTS.md`, `SIGNALR_PROTOCOL.md`, `REDIS_STATE.md`, or
`DATABASE.md`, that is a contradiction to be reported and resolved in the
owning document — not something the ADR silently wins.

---

# 4. ADR Naming Convention

```text
ADR-NNN-short-kebab-case-name.md
```

- `NNN` is a sequential, zero-padded 3-digit number.
- Numbers are never reused, even if an ADR is later deprecated or
  superseded.
- Files live in `docs/03-decisions/ADR/`.

---

# 5. ADR Status Values

```text
Proposed    — decision drafted, not yet confirmed by existing documentation
Accepted    — decision is confirmed and currently in effect
Deprecated  — decision no longer recommended, but no replacement decided
Superseded  — decision replaced by a later ADR (reference the new ADR)
```

`Accepted` is only used when the decision is genuinely established in
`docs/00-overview/`, `docs/01-game-design/`, or `docs/02-technical/` — never
for a decision that is merely implied or still under discussion.

---

# 6. Relationship to Other Documents

```text
GAME_RULES.md / Domain Rules   → WHAT the game rules are
TDD.md / ARCHITECTURE.md /
GAME_STATE.md / GAME_EVENTS.md /
API_CONTRACTS.md /
SIGNALR_PROTOCOL.md /
REDIS_STATE.md / DATABASE.md    → HOW the system works
ADR                              → WHY a given technical/architectural
                                    choice was made
```

An ADR references the documents that motivated it under a
`## Related Documents` section, and must not copy their content. If a
reader wants implementation detail, they follow the reference to the owning
technical document.

---

# 7. ADR Index

| ADR     | Decision                                              | Status   |
| ------- | ------------------------------------------------------ | -------- |
| ADR-001 | Server-authoritative battle resolution                  | Accepted |
| ADR-002 | Modular monolith backend                                 | Accepted |
| ADR-003 | Use React + Vite with Phaser 4 for Discord Activity      | Accepted |
| ADR-004 | SignalR for realtime communication                        | Accepted |
| ADR-005 | Redis for active battle state                              | Accepted |
| ADR-006 | PostgreSQL for persistent data                              | Accepted |
| ADR-007 | Discord SDK integration & server-side auth boundary    | Superseded by ADR-020 |
| ADR-008 | Snapshot-based battle reconnection                             | Accepted |
| ADR-009 | Deterministic PRNG for server-authoritative gameplay randomness | Proposed |
| ADR-010 | Committed-swap state for idempotent Swap rejection            | Superseded by ADR-025 |
| ADR-011 | Player = account owner; Pet = combat character; PetState = battle combat runtime (no PlayerState) | Accepted (item 7 partially superseded by ADR-016) |
| ADR-012 | Player Level + Pet Level clamp formula; Relic/Card ownership vs battle equip; MVP scope closure; no Evolution | Accepted (items 3, 4, 6 partially superseded by ADR-016) |
| ADR-013 | Discord authorization-code → identity exchange contract (OAuth2 code grant, token endpoint, `/users/@me`, `DiscordUserId` source) | Superseded by ADR-020 |
| ADR-014 | `BattleState.PlayerId` = battle-end owner-identity source (not a wire member); `PetState.PetId` = owned Pet instance; `BattleResultId` = `BattleId` | Accepted |
| ADR-015 | Application session authentication contract (signed JWT, stateless, `player_id` claim, Bearer + SignalR access-token propagation, 24h absolute expiry, ASP.NET Core JWT Bearer enforcement) | Accepted |
| ADR-016 | Independent Player XP and Pet XP progression tracks — Player owns account XP / Level (capped Level 50, uncapped XP); Pet owns per-instance XP / Level (range 1–50, hard-capped at 4900); `Player.Level × PetLevelMultiplier` derivation and the `PetLevelMultiplier` field RETIRED; Pet XP balance decided in `PET_RULES.md` §5.1–§5.5 | Accepted |
| ADR-017 | `PetState.NextAttackCritModifiers[]` as authoritative battle state — a dedicated source-specific collection separate from `StatusEffects[]`, persisting across Turns until consumed by a qualifying owner attack; additive Crit composition capped at 100 percentage points; consumed modifiers removed source-specifically; `DefaultCrit` is never a runtime reset mechanism | Accepted |
| ADR-018 | Structured Relic Trigger/Condition/Effect contract — `EffectDefinition` is a structured `EffectDefinition[]` with `valueType` `Flat`/`Percentage`/`PercentagePoints`/`Undetermined` and explicit `target`/`lifetime`; `Condition` is structured and evaluated against the current resolution state with no persistent Relic counters; effect lifetime is independent of trigger re-evaluation; the closed Trigger list is unchanged; `Burning Curse` remains deferred; `varchar(128)` storage is insufficient and migration is a separate task; `RelicTriggered` stays `{ type, relicId }`; supersedes TASK-082 R2-7 for the Relic member. **Amended (Decision 12)** — the `CardCost` effect's runtime state carrier is `PetState.CardCostModifiers[]` (`SourceIdentity` + `CostReductionPercentage`), a dedicated source-specific `BattleState` collection separate from `StatusEffects[]`, with `target = Pet` and `lifetime = Battle`, same-source replace/refresh, additive composition capped at 100% for `CardCost` only, no PostgreSQL persistence, no new Redis key, and no new SignalR event/method | Accepted (amended) |
| ADR-019 | Discord credential secret hygiene — the developer-local channel is the `GameServer.Api` .NET user-secrets store and the deployment channel a host environment variable; `.env.example` documents key names and approved channels, not an unread `.env` path; the tracked `appsettings.json` `ClientSecret` placeholder stays empty with "absent" = null/empty/whitespace; a missing credential fails startup in Production but leaves Development's unchanged `503 DISCORD_UNAVAILABLE`; rotation is manual, operator-owned, event-triggered, restart-required, with no scheduled cadence; the existing local credential is rotated once as a precaution | Superseded by ADR-020 |
| ADR-020 | Standalone Web account authentication (Username/Password), new `Accounts` table, dropping `DiscordUserId` from `Players`, public `POST /api/auth/register` and `POST /api/auth/login`, retiring Discord Embedded App SDK and OAuth dependency completely | Accepted |
| ADR-021 | One Card cast per committed Match-3 Turn — a player may successfully cast at most one Card during each committed Match-3 Turn; the restriction is a cast-count constraint, **not** Turn consumption, so a Card cast still consumes no Turn, does not resolve the Match-3 board, and does not independently trigger the Boss response; the Match-3 Turn remains the authoritative unit of combat progression and the Boss response is reached only through a committed Swap. Option A (cast consumes a Turn and triggers the Boss response) and Option C (keep unlimited free casts, change only Power Charge's cost) are rejected. Records `TASK-191` §6 Q-4 = OPTION B and unblocks B-02 for implementation | Accepted |
| ADR-022 | Post-result preserved-loadout carrier — the loadout used in the battle that just ended is held as a second documented key in Phaser's game-wide registry behind the `game/state/PreservedLoadout.ts` accessor; owned by the client game-presentation layer (not `GameRuntime`), alive for the running game instance, written only on a successful battle start, and read only on the approved `ResultScene` → `PLAY AGAIN` entry, with `LobbyScene` remaining the editing surface. Records that the preserved loadout, active battle state, and battle result data are three distinct concepts, that the `D-202-04 = A` cleanup never clears the preserved loadout, and that the post-result exit clears active battle state through the dedicated client-local `clearActiveBattleState()` port capability (no disconnect, no wire message) rather than `SIGNALR_PROTOCOL.md` §7.3's `BATTLE_NOT_FOUND` path. Discharges the carrier requirement `ARCHITECTURE.md` §2.2.3 recorded without a mechanism per `TASK-202` `D-202-03 = D` | Accepted |
| ADR-023 | Backend Discord identity seam resolution — formally retire the dormant backend Discord identity dependency injection seam (`IDiscordIdentityResolver`), resolver implementations, configuration options, and contract definitions, aligning backend architecture 100% with ADR-020 D1; records that physical deletion of runtime C# files and test stubs is deferred to downstream implementation work | Accepted |
| ADR-024 | Database-driven boss balance configuration — PostgreSQL-authoritative runtime balance for `MaxHP`, `ATK`, `DEF`, and `EnrageThreshold` via Domain Overlay; static `BossDefinitions.All` retained as seed defaults and offline test fixtures; preserves `SecondaryEffect` (Burn, Drain Power, Root) without complex JSON migrations; snapshot isolation for in-progress battles; strict fail-closed validation | Accepted |
| ADR-025 | Swap action preconditions and idempotency — replaces the `LastCommittedSwapPair` coordinate restriction with a dual state version precondition (`expectedSequence`) and action idempotency (`actionId`) contract, superseding ADR-010. Permits legitimate consecutive-turn coordinate reuse while deterministically rejecting stale requests and deduplicating retries | Accepted |
| ADR-026 | Shared-board gameplay and actor-relative match resolution — Player and Boss share one 8×8 board; both act via valid swaps; cell type determines effect; acting side determines target; ATK damage gated on confirmed ATK matches; fallback fixed basic attacks retired; Boss skills deal zero direct damage; Boss SkillCharge charges exclusively from Boss POWER matches; atomic round state persistence via single Redis CAS | Accepted (Decision item 4's POWER bullet and Unresolved Balance Choices #1–#2 partially superseded by ADR-029) |
| ADR-027 | Authoritative cascade pass transition reporting — the Operator Decision approving TASK-278 A-1(i)/A-4 authorizes `CascadeStepResolved` as **one additive event item** inside the round's existing single atomic `ReceiveEvents` batch, reporting a detection pass's completed Gravity and Spawn with `passDepth` (always present; the pass's own 1-based depth), `dropMoves` (`fromCell`/`toCell` per surviving gem that changed cell; omitted when empty) and `spawnedGems` (`cellIndex`/`gemType` per spawned gem; omitted when empty), emitted once per recorded pass as that pass's last item, after that pass's Gravity and Spawn, and never for the terminating pass. Deltas-only: no `board`, no `actor`, no `clearedCells`, and no `specialGem` inside a `spawnedGems` entry — a per-pass board is rejected as redundant with the state push and prohibited by `SIGNALR_PROTOCOL.md` §3.2.12 item 1 / §8 item 5. Intermediate board configurations remain Transient Resolution State: never written to Redis mid-resolution, never in a snapshot, never delivered as a board (the board stays delivered only by `BattleStateUpdated` / `GetBattleState`). Redis CAS, the single atomic round, `Sequence` semantics, existing event sequence semantics, turn banner behavior and `ADR-008` snapshot recovery are unchanged, and RNG stays server-authoritative. The prohibitions on per-pass boards, messages and partial batches are reconciled narrowly rather than weakened; client use is presentation-only for the pass reported, and a missing, malformed or internally inconsistent item is skipped so presentation degrades to the existing settle convergence. Compatibility is one additive discriminator plus an explicit tolerant-reader rule; no version negotiation exists. The governing documents were amended first; the ADR was written last as a historical record. `TASK-278` remains open pending review | Accepted |
| ADR-028 | Retirement of Special Gems and match-tier multipliers from battle-state model and realtime contracts — formalizes the retirement of Special Gems from `BoardState.Cells[64]`, `MatchCreated.createdSpecialGems`, `CellPayload.specialGem`, and Redis active battle state persistence. Establishes tolerant-reader basis under Decision B-2 (default System.Text.Json deserialization safely ignores legacy properties without migration), mandates active battle drain prior to joint deployment, and gates production runtime code modifications behind technical documentation reconciliation | Accepted |
| ADR-029 | Boss as a genuine opponent — Boss Power, Boss Cards, and the response-phase action policy. Introduces `BossState.Power` (integer 0–100) into MVP, **superseding TASK-258 §2 D2** and partially superseding ADR-026's POWER bullet and `SkillCharge` balance questions; makes the POWER-Gem output actor-relative to one shared generation rule and shares the cast contract, validation and effect services while keeping `PetState`/`BossState` separate and Relics Player-only; adds the `Boss Card` category with the minimum MVP capability (one authored Boss Card per Boss) and keeps Enrage / HP-threshold regeneration / combo passives as passive mechanics; fixes the deterministic one-action-per-response-phase policy (eligibility, ordering, cooldown handling, tie-break: a cast must strictly out-score the board action); adds the dedicated `BossCardCast` event while preserving the Player `CardCast` payload; delivers `bossState.power` through the existing push and reconnect snapshot; and records a four-slice staged migration whose `SkillCharge` removal is gated on demonstrated parity plus six explicitly unresolved balance values. Runtime implementation is gated behind the slices; this record authors no numeric balance value | Accepted |

**Partial supersession (ADR-016).** ADR-011 and ADR-012 remain in force
except for the specific items named below, which ADR-016 supersedes. Their
historical content is preserved unmodified:

```text
ADR-011 item 7        The Pet Level formula
                      `clamp(floor(Player Level × Pet Level Multiplier),
                      1, 50)`, the `PetLevelMultiplier` configuration
                      input, and the statement "There is no Pet XP system"
                      — superseded. A Pet has its own XP (PET_RULES.md
                      §5.1). ADR-011's role model and state-ownership
                      decisions (items 1–5) and its wire-label decision
                      (item 6) remain in force, as does item 7's
                      "no Evolution system" clause.

ADR-012 item 3        The Pet Level formula
                      clamp(floor(Player.Level × PetLevelMultiplier), 1, 50)
                      — superseded. Pet Level is derived from the Pet
                      instance's own XP (PET_RULES.md §5.1). ADR-012 item 3's
                      statement that Player Level is an MVP persistent
                      account attribute remains in force.

ADR-012 item 4        The 1–50 clamp on the formula result — superseded; the
                      formula it clamped no longer exists. Player Level's own
                      [1, 50] range remains in force and is now owned by
                      COMBAT_RULES.md §7; the Pet Level range is now decided
                      as [1, 50] (PET_RULES.md §5.5).

ADR-012 item 6        "There is no Pet XP system" — superseded, as for
                      ADR-011 item 7. The no-Evolution clause of item 6
                      remains in force (PET_RULES.md §5.6 item 4).
```

**ADR-016 item 14 is now satisfied.** The twelve Pet XP balance/reward
decisions that ADR-016 deferred to `PET_RULES.md` §5.2 have been finalized
(see that document's §5.1–§5.5). The deferral record in ADR-016 is preserved
as historical context; the values themselves are owned by `PET_RULES.md`.

Everything else in ADR-011 and ADR-012 — the Player/Pet role model, the
"no `PlayerState`" decision, collection-ownership vs. battle-scoped equip,
`PlayerUnlockedCard`, the fixed wire labels, and the rest of ADR-012's
scope closure — is **unaffected** and remains Accepted.

**Partial supersession (ADR-029).** `ADR-026` remains Accepted. The only parts of
it that `ADR-029` supersedes are named below; its historical content is preserved
unmodified and nothing else in the record changes:

```text
ADR-026 Decision item 4, POWER bullet
                      "Generates Power for the active Pet (range 0–100) during
                      Player turn, or increments BossState.SkillCharge during
                      Boss turn" — superseded. The Boss generates and spends
                      BossState.Power (integer 0–100) by the same
                      actor-relative rule (BOSS_RULES.md §4.1,
                      COMBAT_RULES.md §2, GAME_RULES.md §12/§15).
                      The item's ATK, DEF and HP bullets, the +5 per-Gem
                      DEF output and the actor-relative ownership and target
                      resolution remain in force.

ADR-026 Unresolved Balance Choices #1–#2
                      The SkillCharge unit metric and its overflow/clamp
                      policy — closed by RETIRING the member rather than by
                      answering them, and gated by ADR-029 §2.9's parity gate.
                      The Boss's Power economy has its own unresolved values
                      (ADR-029 §3, U-1/U-2/U-3) and they are NOT derived from
                      these questions. Items #3 and #4 remain open as written.

TASK-258 §2 D2        "Retain BossState.SkillCharge as the single Boss ability
                      resource. Do not introduce BossState.Power." — SUPERSEDED
                      in full by ADR-029. The preserved clause is that the Boss
                      is charged only by its own POWER matches and never by the
                      Player's; "specific charge units and threshold values
                      remain balance-tuning decisions" carries forward as
                      ADR-029 §3's unresolved items. TASK-258 is a completed,
                      immutable record and is not edited.
```

`ADR-021` (one Card cast per committed Turn) is unchanged: the allowance remains
the Player's and a Boss Card cast neither reads nor spends it. `ADR-024`
(database-driven boss balance configuration) is unchanged: the ability's effect
carrier moves from the Skill definition to the Boss Card definition at ADR-029
Slice 4 without changing the Domain Overlay, the PostgreSQL authority, the static
baseline or the fail-closed validation, and no persistence change is authorized.

---

# 8. Known Open Items (Not ADRs)

The following are explicitly **not decided** by any current document and
therefore have no ADR. They are tracked here so they are not silently
forgotten, per `AGENTS.md` §2.2:

```text
- Backend runtime (ASP.NET Core / C#) is recorded only as an ASSUMPTION in
  TDD.md §0, not an independently confirmed decision.
- The deterministic PRNG algorithm (ADR-009, PCG32) is `Proposed`: it is
  required by MATCH3_RULES.md §7 and TDD.md §6, which state "server-seeded
  RNG" without naming an algorithm. Until a technical document owns the
  algorithm, ADR-009 is the only place it is selected. Its state contract is
  defined in GAME_STATE.md §2.6.
```

**Checked and not listed (TASK-119).** The `BuffDebuff` `TargetStat = "ATK"`
consumption gap — that no document stated how a Turn-based Buff/Debuff's
`Magnitude` reaches the stat its `TargetStat` names — was **not** an entry in
this section before TASK-119. It was recorded instead in the TASK-119 task
file's "Decision Inputs", and it was **not** an ADR-level open item: the
Product Owner's decision (TASK-119 D-1–D-5) resolves it inside the existing
`StatusEffects[]` model, so it introduces **no** new battle-state concept and
requires **no** ADR. The resolved rule is authored at its canonical owner,
`COMBAT_RULES.md` §5.4. This item is recorded here as checked-and-closed
rather than added to the open list above.

**Checked and closed (TASK-036 / ADR-019).** Discord credential secret
hygiene — how the `Discord ClientSecret` is supplied locally and in
deployment, the tracked `appsettings.json` placeholder, absent-credential
startup behavior, and rotation — was **not** an entry in this section. It was
tracked as a BLOCKED task instead
(`tasks/backlog/TASK-036-discord-credential-secret-hygiene.md`, at the time
under `tasks/blocked/`), because
`ADR-015`'s D7–D11 deliberately scoped themselves to the JWT signing key and
left Discord credential hygiene open. It was an ADR-level open item, and it is
now **resolved**: the Product Owner's decisions D1–D6 are recorded as
`ADR-019`, which TASK-036 implements. It is recorded here as checked-and-closed
so its absence from the open list above is not mistaken for an oversight.
`ADR-007` item 3's boundary, `ADR-013` item 11's configuration keys, and
`ADR-015` D7–D11 are all unchanged by that resolution.

When any of these is resolved, add a new ADR (next sequential number) rather
than retroactively editing an existing one.
