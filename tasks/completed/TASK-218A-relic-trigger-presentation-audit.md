# TASK-218A — Relic Trigger Presentation Audit

**Type:** Audit (no implementation)
**Status:** COMPLETE
**Decision:** **PASS**
**Parent task:** TASK-218 (proposed — "name the Pet's Relic triggers in battle")
**Predecessors consulted:** TASK-131 / TASK-133 / TASK-141 / TASK-148 / TASK-176–179 /
TASK-204 / TASK-205 / TASK-209 / TASK-210 / TASK-211 / TASK-212 / TASK-212A / TASK-212A-1 /
TASK-213 / TASK-219A / TASK-221 / TASK-221A / TASK-223
**Scope of this record:** presentation/callout only. No mechanic, contract, loadout, ownership,
or server-behaviour change is proposed, and none was made.

---

## 0. Verdict and one-paragraph summary

**PASS.** This is an **implementation-only, frontend-only, no-contract-change** task with a narrow,
fully identified boundary.

`RelicTriggered` is already emitted server-side, already on the SignalR wire, and already parsed by
the client; the player-facing Relic **name** is already reachable through the existing
`GET /api/relics` read that the client already holds a port for (`GameRuntimePort.getRelics()`).
The only thing missing is that `BattleScene` never loads that read, so it has no Relic definition
source; consequently `describeEventCallout` returns `null` for `RelicTriggered` and the event reaches
only a developer log that is never drawn.

The audit **corrects TASK-210's classification**: this is a **presentation-only gap**, not a contract
gap. `SIGNALR_PROTOCOL.md` §3.2.23 item 5 (TASK-131 **D10**) fixes the event's member set as
**final** — `type` and `relicId`, explicitly "not a placeholder awaiting a follow-up decision" — and
`ADR-018` item 10 and `RELIC_RULES.md` §7 say the same. No new member, event, endpoint, or document
change is required or permitted.

Two coverage facts bound the outcome and must be carried into implementation:

1. **Burning Curse can never be named**, by documented design: the battle-start firing point passes
   `events: null` because battle creation emits no Battle Event (`GAME_STATE.md` §2.0.5.2 item 2).
2. **The existing callout mechanism presents at most ONE callout per delivered batch.** Relic
   activation therefore competes on a priority ladder with Combo / cast / Boss / Passive / Match, so
   "which Relic activated" is expressible but not for several Relics in one batch.

---

## 1. The Relic contract — what `trigger`, `condition`, and `effectDefinition` mean

### 1.1 Sources

```text
RELIC_RULES.md      §1 (structure), §2.1 (3–5 loadout), §3 (closed Trigger list),
                    §6 (the 10-row reference table), §7 (Events),
                    §8.1–§8.5 (the structured representation contract)
API_CONTRACTS.md    §3 (relicLoadout), §5.4 (GET /api/relics)
GAME_EVENTS.md      §2 RelicTriggered
SIGNALR_PROTOCOL.md §3.2.23 (RelicTriggered), §3.2.25 (effect summary omission), §4 (projection)
GAME_STATE.md       §2.3.x (the modifier carriers), §2.0.5.2 item 2, §5.1.x
ADR-018             structured Relic Trigger/Condition/Effect contract, item 10
```

### 1.2 The three members

| Member | What it is | Authority | Runtime meaning |
| --- | --- | --- | --- |
| `trigger` | One primary Trigger identity from §3's **closed 12-value list** | `RELIC_RULES.md` §3 (`:474-491`), §8.5 item 3 (`:1067-1070`) | **Descriptive content**, stored as a raw `string` and deliberately **not** structured (`RelicDefinition.cs:83-103`). It is read by ordinal string comparison, never parsed into an enum. |
| `condition` | Optional, **nullable**, structured — one of exactly three forms carrying an integer `N` | `RELIC_RULES.md` §8.1 (`:811-874`) | **Descriptive content**. `MatchCountAtLeast(N)` / `ComboAtLeast(N)` / `HpPercentageBelow(N)`. `null` means "the Trigger alone is the complete condition" (§8.1 item 4). |
| `effectDefinition` | A structured **array** of `{effectType, valueType, value?, target, lifetime}` | `RELIC_RULES.md` §8.2 (`:876-925`), §8.3 (`:927-987`) | **Descriptive content.** `effectType` ∈ `ATK\|Power\|Crit\|CardCost\|BurnDamage`; it is the effect *identity carrier* and §8.2 item 1 explicitly forbids deriving a Relic's effect from prose, `Name`, `RelicDefinitionId`, or hardcoded per-Relic logic. |

### 1.3 Descriptive content vs. authoritative runtime event

The distinction the task asks for is real and load-bearing:

```text
DESCRIPTIVE CONTENT (a definition)        AUTHORITATIVE RUNTIME (what happened)
RelicDefinition.Trigger                   RelicTriggeredEvent { RelicId }
RelicDefinition.Condition                 PowerChangedEvent { Source, Delta, Power }
RelicDefinition.EffectDefinition          the PetState carrier the effect reached
  → read from PostgreSQL via GET /api/relics   → emitted by the server during resolution
  → static, identical for every owner          → per-occurrence, server-decided
```

- `trigger` / `condition` / `effectDefinition` are **not** runtime events. They contain no
  occurrence, no magnitude-as-applied, and no result. Reading them cannot tell you whether a Relic
  fired.
- The runtime event `RelicTriggered` reports **that** a Relic's Effect applied and **which** Relic
  applied it (`RELIC_RULES.md` §7 `:790-794`). It carries `{ type, relicId }` and **no effect
  summary**.
- The *resulting state* is delivered separately — through the `BattleState` projection and, for
  Power, through `PowerChanged` (`RELIC_RULES.md` §7 last sentence; `SIGNALR_PROTOCOL.md` §3.2.23
  item 5).

### 1.4 What is safe for player-facing presentation

| Information | Safe? | Bound |
| --- | --- | --- |
| `name` (`RelicDefinition.Name`) | **YES** | It is the intended display name. `API_CONTRACTS.md` §5.4 `:995`. |
| `trigger` / `condition` / `effectDefinition` rendered as **display text** | **YES, already done** | `API_CONTRACTS.md` §5.4 `:1061-1063`: "the client may translate the tokens into display text but may not state a rule the payload does not carry". `ContentEffectFormat.ts` already does exactly this for the Lobby and Collection viewer. |
| Raw `relicId` / `RelicInstanceId` | **NO** as a player-facing label | A technical identity. Fail-closed fallback only (TASK-208 §D "never a guessed name"). |
| Technical Trigger names (`OnMatchCount`, …) | **NO** | The task's own constraint; also TASK-210 §7's precedent for `BossSkillCast` declining a technical `skillId`. |
| Any combat result derived from `effectDefinition` | **NO — forbidden** | `AGENTS.md` §10; `GAME_STATE.md` §0 item 5; `API_CONTRACTS.md` §5.4 `:1042-1047`. |

**No wording or mechanic is invented by this section.** Every value above is transcribed from its
owning document.

---

## 2. Runtime trigger path

### 2.1 The traced path

```text
RelicDefinition row (PostgreSQL)
  │  BattleStartService.ResolveEquippedRelicDefinitionsAsync
  │  (BattleStartService.cs:439-484 — instance ids → Relic.RelicDefinitionId
  │   → IRelicDefinitionLookup.GetDefinitionAsync → RelicDefinition, equip-slot order)
  ▼
_relicConfiguration[battleId]   (BattleStateService.cs:552-557; in-process registry)
  │  zipped with the snapshot PetState.EquippedRelics[] slot-for-slot
  ▼
EquippedRelicContent(InstanceIdentity, Definition)   (EquippedRelicContent.cs:49-51)
  ▼
RelicResolver.Resolve(equipped, petState, firingPoint, …)   (RelicResolver.cs:268-386)
  ├─ IsEvaluatedAt(trigger, firingPoint)      :422-446
  ├─ ConditionHolds(condition, …)             :488-515
  ├─ Apply(effects, …)                        :536-689
  └─ triggered.Add(new RelicTriggeredEvent(sourceIdentity))  :375-382
       — only `if (applied.AnyEffectApplied)`
  ▼
BattleEvent.ForRelicTriggered                (BattleEvent.cs:968-973; enum :230 = 14)
  ▼
BattleStateService.ApplyRelicFiringPoint     :2639-2643  (events.AddRange)
  ▼
SwapExecutionResult.WithEvents               :1557
  ▼
BattleHub: Clients.Group(battleId).SendAsync("ReceiveEvents", …)   BattleHub.cs:1102
  ▼
BattleEventWireProjection.RelicTriggered     BattleEventWireProjection.cs:553-556, :749, :767-768
  ▼
GameRuntime.forwardBattleEvents              GameRuntime.ts:946-959
  ▼
GameRuntimePort.onBattleEvents               GameRuntimeEvents.ts:455
  ▼
BattleScene.handleBattleEvents → presentEventBatch    BattleScene.ts:1769-1789, :1813-1845
```

### 2.2 Every existing Relic trigger mechanism

`RelicFiringPoint` (5 members, `RelicFiringPoint.cs:46-82`) is the runtime's spelling of "which
documented §3 event is being processed". `RelicResolver.IsEvaluatedAt` (`:422-446`) maps Triggers
to firing points:

| `RelicFiringPoint` | Evaluated Triggers (`IsEvaluatedAt`) | Call site |
| --- | --- | --- |
| `BoardResolution` | `OnMatchCount`, `OnCombo`, `OnHpBelow` | `BattleStateService.cs:1260-1268` (§17 step 11) |
| `BattleStart` | `OnBattleStart` | `:531-537` |
| `CascadeIteration` | `OnCascade` | `:1303-1311` (once per iteration, loop `:1301`) |
| `PowerGain` | `OnPowerGain` | `:1347-1354` (only when the match delta > 0, `:1345`) |
| `DamageTaken` | `OnDamageTaken` | `:1917-1923` (Boss Response) and `:2283-2289` (per DoT tick) |

**7 of §3's closed 12 Triggers are evaluated. 5 are not:** `OnMatch`, `OnDamageDealt`,
`OnCardCast`, `OnTurnStart`, `OnTurnEnd` (`RelicResolver.cs:394-411`; unrecognized values are
silently inert, `:342-349`, `:413-420`). **No provisioned Relic declares any of them**
(`RELIC_RULES.md` §8.5 item 3 lists exactly the 7; §6/§8.5's 10 rows use only those).

### 2.3 Which triggers occur during MVP gameplay

All 7 evaluated Triggers are reached by the 10 provisioned Relics. Every firing point is wired, and
the qualification/anti-chain boundaries are implemented:

- `OnPowerGain` qualification: `IsQualifyingPowerGain(source) => source != PowerChangeSource.Relic`
  (`RelicResolver.cs:145-146`), gated at `:294-299` — Relic-granted Power does not re-enter the chain.
- `OnCascade` per-iteration: the loop at `:1301-1312` calls the stage once per cascade iteration
  rather than aggregating.
- Once-per-root-event: `handledSources` keyed on **instance** identity (`:310`, `:329-332`).

### 2.4 Is trigger occurrence already observable by the client?

**Yes for 9 of the 10 Relics.** `RelicTriggered` is a discrete member of the `ReceiveEvents.events[]`
batch (`BattleHub.cs:1102` for Swap; `:1143` CardCast; `:1177` PetSkillCast). It is **not** inside
`BattleStateUpdated` (`:1092`; that payload, `:95-104`, has no events member).

**The one exception is Burning Curse.** `BattleStateService.cs:531-537` passes **`events: null`** for
the `BattleStart` firing point, with the documented rationale at `:525-528`: battle creation emits no
Battle Event (`GAME_STATE.md` §2.0.5.2 item 2 — "emits no Battle Events", `:2653`), so the effect
"reaches the client through the existing state projection rather than through a new event". That
projection does not carry it either (see §2.5). **Burning Curse therefore produces no client-observable
evidence of having triggered at all.**

Additionally, a client that joins or reconnects receives only `BattleStateUpdated` (join path
`BattleHub.cs:878`) — no `RelicTriggered` history. That is correct and requires nothing: the callout
is transient feedback, not a reconstruction contract.

### 2.5 Does the client receive enough information to present it?

The client receives the **instance identity only**. It does **not** receive any Relic modifier state:

`PetStatePayload` carries exactly 8 members (`BattleHub.cs:235-245`, populated `:1301-1353`):
`passiveId`, `passiveProgress`, the conditional `passiveResetOverride`, `equippedCards`,
`statusEffects`, `hp`, `maxHp`, `power`. `:1292-1296` records that "the sibling modifier collections,
and `EquippedRelics[]` … is not part of this stage and is not projected".

| Carrier | Projected? | Consequence |
| --- | --- | --- |
| `EquippedRelics[]` | **No** | The battle loadout snapshot is not on the wire. |
| `ATKModifiers[]` | **No** | Berserker Core / Battle Instinct results invisible. |
| `CardCostModifiers[]` | **No** | Emergency Core result invisible. |
| `NextAttackCritModifiers[]` | **No** | Assassin Eye / Combo Fang / Execution Mark results invisible. |
| `BurnDamageModifiers[]` | **No** | Burning Curse result invisible. |
| `PetState.Power` | **Yes** | Arcane Battery / Mana Crystal / Cascade Core arrive as a *result* through `PowerChanged { source: "relic" }` (`RelicResolver.cs:601-618`; `BattleEventWireProjection.cs:577-588`). |

**So the answer is yes — but only for the name.** The identity is delivered; the *player-facing name*
is reachable from `GET /api/relics` (`API_CONTRACTS.md` §5.4, `:991-1004`), which maps owned instance
identity → `name` (`CollectionQueryService.cs:288-347`; `CollectionResponses.cs:314-320`). The client
already has the port and the call (`GameRuntimePort.getRelics`, `GameRuntimeEvents.ts:534-538`;
`GameRuntime.getRelics()`, `GameRuntime.ts:812-814`; `ApiService.getRelics()`, `ApiService.ts:310-312`)
and already holds the DTO (`RelicResponse`, `CollectionModels.ts:274-300`). **`BattleScene` simply
never calls it** — `BattleScene.ts` contains **zero** occurrences of `relic` (case-insensitive) and
calls only `runtime.getCards()` (`:1198`) and `runtime.getPets()` (`:1244`).

### 2.6 Classification of each missing piece

```text
EXISTING DATA
  RelicTriggered delivery                     BattleHub.cs:1102 → BattleScene presentEventBatch
  the owned-instance → name mapping           GET /api/relics (API_CONTRACTS.md §5.4)
  the client DTO and the read call            CollectionModels.ts:274-300; ApiService.ts:310-312
  the client port                             GameRuntimeEvents.ts:534-538; GameRuntime.ts:812-814
  the Relic's authored effect text            ContentEffectFormat.ts:174-208, :262-308
  the Power result for 3 of 10 Relics         PowerChanged { source: "relic" }

EXISTING EVENT
  RelicTriggered, already parsed client-side  BattleEventPresenter.ts:201-202, :454-463

PRESENTATION-ONLY GAP                    ◄── the whole of TASK-218
  BattleScene has no Relic definition source  (never calls getRelics)
  describeEventCallout returns null for it     BattleEventPresenter.ts:631-683 default
  the event reaches only the undrawn dev log   BattleScene.ts:1823; :2120-2130

CONTRACT GAP
  NONE for the callout.  SIGNALR_PROTOCOL.md §3.2.23 item 5 fixes the shape as FINAL.

MECHANIC GAP (latent, no player-visible loss today)
  5 of §3's 12 Triggers have no firing point, and no provisioned Relic declares them.

EMISSION GAP (documented by design — do not change)
  BattleStart emits no event, so Burning Curse is never reported.
```

---

## 3. Existing presentation infrastructure (TASK-210) and what can be reused

All battle presentation is Phaser-side, in `BattleScene.ts`; the React overlays are DEV-only
infrastructure diagnostics and are **not** a battle HUD (`App.tsx:139-140`;
`StatusOverlay.tsx:6-16`).

### 3.1 The mechanism that must be reused

The **single selective callout** is the one player-facing statement a resolution produces
(TASK-210 §7 `:196-219`):

```text
describeEventCallout(event, resolveCardName): EventCallout | null   BattleEventPresenter.ts:631-683
selectBatchCallout(events, resolveCardName): EventCallout | null    BattleEventPresenter.ts:695-709
showCallout(message, color): void                                   BattleScene.ts:2046-2067
```

- `EventCallout { message, color, priority }` (`:549-560`); **lower priority number = more
  important**; among equals the **last** wins (`:704`).
- The current ladder (`:583-588`): Combo 1, cast 2, Boss 3, PassiveTriggered 4, PassiveCharged 5,
  Match 6.
- The text object is created once at `:799-808` (`CALLOUT_Y`, width 460, `maxLines 1`,
  constants `:120-123`), holds 700 ms and fades 350 ms, replacing any live callout
  (`killTweensOf` then one alpha tween).
- Wired once per delivered batch: `BattleScene.ts:1828-1831`, inside `presentEventBatch`
  (`:1813-1845`), which is called from `handleBattleEvents` (`:1769-1789`) on each
  `ReceiveEvents` envelope.

**Name resolution is already a parameter.** `describeEventCallout` and `selectBatchCallout` already
take a `resolveCardName: (cardId: string) => string` callback, supplied at the call site as
`(cardId) => this.cardDisplayName(cardId)`. A symmetric `resolveRelicName` is the smallest possible
extension of an existing seam — no new mechanism.

### 3.2 The collection-read + name-map pattern to mirror

`loadCardDefinitions()` (`BattleScene.ts:1192-1209`) reads `runtime.getCards()`, fills
`cardDefinitions: Map<string, CardResponse>` (`:395`), and `cardDisplayName(cardId)` (`:2103`) falls
back to the raw identity. It is started from `create()` (`:424`, alongside `loadPetCatalog`) and
cleared in `shutdown()` (`:566`). A Relic equivalent is the same shape against `getRelics()`.

### 3.3 Other reusable mechanisms (for completeness — not necessarily needed)

| Mechanism | API | Form / lifetime |
| --- | --- | --- |
| Floaters | `spawnFloater(x,y,message,color)` `:2006-2034`; `spawnDamageFloater` `:1968`; `spawnPowerFloater` `:1986` | Per-event, **multiple allowed**; rise 30 px over 600 ms then self-destroy. |
| Cell highlights | `spawnCellHighlight(cellIndex,…)` `:1924-1955` | Per matched cell, tweened alpha. |
| Status presentation | `describeStatusEffects` `:2285-2293`; `describeStatusDuration` `:2302-2312` | Text line only — **no icons exist**. |
| Combo / Match | `renderPlayerHud` `:1145-1160` (`comboText` `:731`, `matchesText` `:740`) | Persistent, state-driven. |
| Passive | `petPassiveText` `:753`, `passiveGauge` `:765`, `describePassiveReset` `:2253` | Persistent. |
| Gauges | `createGauge` `:898`, `updateGauge` `:929` | Created once, resized per push. |
| Timing / queue | — | **No queue and no timeline exist.** Only a boolean `presentationLocked` (`:373`, set `:1781`, released by a 150 ms tween `:1833-1844`) plus Phaser's own tween manager (`tweens.killAll()` `:559`, `killTweensOf` `:2060`). |

### 3.4 The constraint the reuse carries — **one callout per batch**

`selectBatchCallout` presents at most one statement per delivered `ReceiveEvents` batch, by design
("a resolution is told once rather than replayed as a second event feed", TASK-210 §7). Consequences
for this task, which the implementation must decide and record (a **presentation** choice, not a
contract decision):

1. A `RelicTriggered` callout must occupy a priority slot, so in a batch that also contains a Combo
   (`≥2`) the two compete; whichever wins, the other is silent for that batch.
2. **A batch can contain several Relic triggers.** §4.2 resolves all eligible Relics for one event in
   equip-slot order, and step 11 evaluates three Triggers at one point. A 5-Relic loadout can
   therefore produce more than one `RelicTriggered` in a single Swap batch, and **only one can be
   named**. "Which Relic activated" is expressible; "which Relics activated" is not, through this
   mechanism alone.
3. **Emergency Core re-emits on every Swap while armed.** Its Trigger re-evaluates continuously
   (`RELIC_RULES.md` §6 note 2 `:756-758`; §8.5 item 2 `:1033-1066`), and the `CardCost` arm sets
   `applied = true` on each refresh (`RelicResolver.cs:639-652`, `:651`), so `AnyEffectApplied` holds
   and `RelicTriggered` is emitted per pass. A Relic callout may therefore repeat every Swap for a
   battle, which is a repetition-rate decision for the implementation.

Reusing the callout is nonetheless correct: it is the existing mechanism, it already carries a
name-resolution seam, and creating a second notification system is explicitly forbidden by this
task and by `AGENTS.md` §9.

### 3.5 BattleScene lifecycle rules any new presentation must obey (TASK-204 / TASK-205)

- Cleanup is attached in `create()` to Phaser's own events, **detach-before-attach**:
  `events.off(SHUTDOWN)`/`off(DESTROY)` then `events.once(...)` (`BattleScene.ts:473-476`).
  Registering with `once` alone stacks an unfired handler across scene restarts.
- After teardown no subscription may remain on the runtime (`:527-532`); a stale listener writing to
  a destroyed object aborts `scene.start`.
- Per-battle caches must be cleared so a reused instance starts clean (`:565-569`, including
  `cardDefinitions.clear()` at `:566`) — a Relic map belongs in exactly this list.
- Any asynchronous continuation must be invalidated by a scene-owned run guard (the recorded
  `ResultScene.rewardLoadRun` pattern). `BattleScene` already starts `void this.loadCardDefinitions()`
  / `loadPetCatalog()` from `create()` (`:424-425`), so a new Relic read must follow that same guard.

---

## 4. Ten-Relic coverage

All 10 Relics are provisioned and all 10 are owned by a new Player:

- Definitions: migrations `20260929152651_ProvisionPetCardRelicContentDefinitions`,
  `20261003074309_StructureRelicDefinitionStructuredColumns`, and
  `20261004153916_ProvisionRemainingMvpRelicDefinitions` (Burning Curse `:185`, Combo Fang `:205`,
  Arcane Battery `:233`, Execution Mark, Cascade Core `:276`, Battle Instinct `:308`).
  `RELIC_RULES.md` §6 note 4 / §8.7 record the set as complete at ten.
- Ownership: `PlayerStarterGrantFactory.cs:128-137` grants exactly the 10 Relic definition ids
  (`relic-berserker-core` … `relic-battle-instinct`), one instance each
  (`RelicInstanceId = $"relicinst_{Guid.NewGuid():N}"`, `:273`).

### 4.1 Matrix

| Relic | Trigger | Condition | Effect | Trigger reachable in MVP? | Client-observable? |
| --- | --- | --- | --- | --- | --- |
| Berserker Core | `OnMatchCount` | `MatchCountAtLeast(3)` | `ATK` +5% `Percentage`, `Pet`, `Battle` | **YES** — `BoardResolution`; needs 3 cumulative Matches | **YES (event)** — `RelicTriggered`. Modifier not projected. |
| Mana Crystal | `OnMatchCount` | `MatchCountAtLeast(4)` | `Power` +10 `Flat`, `Immediate` | **YES** — needs 4 cumulative Matches | **YES (event + result)** — `RelicTriggered` + `PowerChanged{source:"relic"}` |
| Assassin Eye | `OnCombo` | `ComboAtLeast(3)` | `Crit` +10pp, `NextAttack` | **YES** — needs a 3-chain Combo | **YES (event)** — modifier not projected |
| Emergency Core | `OnHpBelow` | `HpPercentageBelow(30)` | `CardCost` −50% `Percentage`, `Battle` | **YES** — needs Pet HP < 30%; re-evaluates every Swap while armed | **YES (event)** — modifier not projected; **re-emits per Swap** |
| Burning Curse | `OnBattleStart` | `null` | `BurnDamage` +30% `Percentage`, `Battle` | **YES** — fires once at battle start | **NO** — battle creation emits no Battle Event (`events: null`), and `BurnDamageModifiers[]` is not projected |
| Combo Fang | `OnCombo` | `ComboAtLeast(5)` | `Crit` +20pp, `NextAttack` | **YES** — needs the Swap's Combo to reach 5 (deepest chain of the ten) | **YES (event)** — modifier not projected |
| Arcane Battery | `OnPowerGain` | `null` | `Power` +5 `Flat`, `Immediate` | **YES** — any qualifying non-Relic Power gain | **YES (event + result)** — `RelicTriggered` + `PowerChanged{source:"relic"}` |
| Execution Mark | `OnHpBelow` | `HpPercentageBelow(30)` | `Crit` +15pp, `NextAttack` | **YES** — same condition as Emergency Core | **YES (event)** — modifier not projected |
| Cascade Core | `OnCascade` | `null` | `Power` +5 `Flat`, `Immediate` | **YES** — needs ≥ 1 cascade iteration (depth ≥ 2) | **YES (event + result)** — `RelicTriggered` + `PowerChanged{source:"relic"}`, once per iteration |
| Battle Instinct | `OnDamageTaken` | `null` | `ATK` +10% `Percentage`, `NextAttack` | **YES** — the Pet must take a damage instance | **YES (event)** — modifier not projected |

### 4.2 Relics whose trigger cannot currently occur under MVP rules

**None of the ten.** Every one of the 7 Triggers the ten Relics declare has a wired firing point
(§2.2), and every firing point is reached by ordinary play. Three qualifications, none of which is an
unreachable trigger:

1. **Two Relics are condition-gated on a losing position.** Emergency Core and Execution Mark require
   Pet HP < 30% (`HpPercentageBelow(30)`), i.e. they fire only once Boss damage has accumulated. That
   is the authored condition, not a missing mechanic.
2. **Combo Fang needs the deepest chain.** `ComboAtLeast(5)` requires the Swap's Combo to reach 5.
   The firing point and condition are implemented; occurrence depends on the player producing that
   chain. (The TASK-221 E2E battle observed the delivered state reach `matchCount 4` / `combo 4`,
   which is one short of this Relic's threshold.)
3. **Burning Curse is reachable but not observable** — a presentation/emission boundary, not a
   reachability one (§2.4).

**No Relic is unreachable because of its mechanic, so nothing is modified in TASK-218** (and nothing
may be, per the task's non-goals).

**Latent, out of scope:** five of §3's twelve Triggers (`OnMatch`, `OnDamageDealt`, `OnCardCast`,
`OnTurnStart`, `OnTurnEnd`) have no firing point and are silently inert
(`RelicResolver.cs:342-349`, `:394-411`). No provisioned row declares any of them, so no
player-visible behaviour is missing today. This is TASK-213's **N-13**, carried there as an
undecided implementation-coverage question; it is **not** decided here and **not** a TASK-218
concern.

---

## 5. Player-facing presentation — minimum useful callout semantics

### 5.1 Is the existing definition data sufficient for `<Relic Name>` + `<effect/result>`?

**Yes for the name; partially for the result — and the two halves must be kept distinct.**

```text
<Relic Name>            ← SUFFICIENT.  RelicResponse.name, via GET /api/relics, keyed by the
                          delivered relicId (owned instance identity).

<effect/result>         ← TWO different things, only one of which is authoritative per-occurrence:

   (a) what the Relic DOES      the definition's authored rule, rendered as text
                                (ContentEffectFormat.formatRelicEffects).  This is
                                presentation of CONTENT — permitted by API_CONTRACTS.md
                                §5.4 :1061-1063 — but it is NOT "what happened".

   (b) what HAPPENED            authoritative, per-occurrence, and delivered for only
                                3 of the 10 Relics (the Power granters), through
                                PowerChanged { source: "relic", delta, power }.
                                For the other 7 there is NO authoritative result event
                                and no projected carrier (§2.5), so the result cannot be
                                stated without deriving it — which is forbidden.
```

### 5.2 What this means for the implementation

- **The name is the safe, sufficient, authoritative-by-identity half.** It answers "which Relic
  activated" exactly, from delivered data, with no derivation.
- **"What happened" may be stated only where the server stated it.** `PowerChanged{source:"relic"}`
  is the one delivered result; using it is reading, not deriving. Rendering the definition's effect
  text is also permissible but states the Relic's *rule*, not the occurrence's *outcome* — the
  distinction must be deliberate, and the audit does not choose the wording.
- **Never** show the raw `relicId` as the label (fail-closed fallback only), and never a technical
  Trigger name.

### 5.3 Non-goal honoured

The final UI is **not** designed here. This section fixes only what the data can and cannot support,
per the task's instruction.

---

## 6. Server-authoritative boundary — verified

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Client does not independently decide whether a Relic triggered | **HOLDS** | The client has no evaluation function of any kind. A search for `triggerRelic`/`EvaluateRelic`/`equipRelic`/`isRelicTriggered` across `src` returns only DTO field/prose uses of `equippedRelics` (`BattleModels.ts:108`, `:306`, `:379`). The callout is a pure mapping from a **delivered** event. |
| Client does not simulate Relic effects | **HOLDS** | `RelicTriggered` handling is limited to `parseRelicTriggered` (`BattleEventPresenter.ts:454-463`) and a log line (`:536-537`). No effect is applied, queued, or predicted. |
| Client does not derive combat results from `effectDefinition` | **HOLDS** | The only readers are in `ContentEffectFormat.ts`, whose comparisons (`:119`, `:125`, `:133`, `:146`, `:201`, `:202`, `:207`) are all `null`/`undefined` **presence** checks. No threshold is compared against any runtime value. Module doc: "**It reads; it never infers, defaults, or computes.**" Its only two call sites are `LobbyScene.ts:1496` and `CollectionViewerScene.ts:806` — never in battle. |
| Presentation is driven by authoritative runtime state/event data | **HOLDS** | `presentEventBatch` consumes the delivered `ReceiveEvents` batch verbatim; `GameRuntime` validates only the envelope shape and forwards unchanged, and is documented as never interpreting/filtering/reordering events (`GameRuntime.ts:106`, `:117-119`). |
| Existing deterministic battle resolution remains untouched | **HOLDS** | TASK-218 requires no server change; `RelicResolver` is pure and its order is §4.2's equip-slot order (`:215-220`). |
| Client does not reconstruct state from events | **HOLDS** | No HP is derived from a damage event and no Power is reconstructed from `PowerChanged` (`GameRuntimeEvents.ts:158-160`). |

**Client-side Relic derivation defects found: NONE.** The task's instruction to classify such a
defect does not apply. What *is* misclassified is a **comment**: `BattleEventPresenter.ts:624-628`
and TASK-210 §Remaining Issue 2 describe the missing callout as a "contract gap"; the contract is in
fact explicit and final, and the missing piece is a client-side read the scene never performs. This
is a documentation-truthfulness item owned by TASK-217A/217B (§9), not a boundary defect.

---

## 7. Contract-change assessment

### **A. NO CONTRACT CHANGE.**

Three independent authorities fix the event's member set as final, and none is a placeholder:

```text
SIGNALR_PROTOCOL.md §3.2.23 item 5   (TASK-131 D10)
  "The wire shape is confirmed final and carries no effect summary. This member set
   — type and relicId — is the decided shape, not a placeholder awaiting a follow-up
   decision. A Relic's resulting state reaches the client through the existing
   BattleState projection (§4) and not through this event."
                                        SIGNALR_PROTOCOL.md:1242-1248

ADR-018 item 10
  "RelicTriggered remains { type, relicId }. The resulting state is [delivered
   elsewhere]."                          ADR-018:124

RELIC_RULES.md §7
  "Its wire shape is { type, relicId } and carries no effect summary — the resulting
   state is delivered through the existing BattleState projection."
                                        RELIC_RULES.md:790-794
```

Corroborating: `API_CONTRACTS.md` §5.4 `:1070-1071` — "It delivers nothing on the SignalR wire:
`RelicTriggered` keeps its `{ type, relicId }` shape"; `GAME_EVENTS.md` §2 item 4 defers the member
question to §3.2.23/§3.2.25; `SIGNALR_PROTOCOL.md` §3.2.25 records the `effect summary` **omission**
convention (`:1334-1346`). The shipped projection carries exactly two members
(`BattleEventWireProjection.cs:553-556`), locked by
`RelicWireProjectionTests.cs:51-56` (`Members == ["relicId","type"]`) and `:59-74` (asserting the
absence of `effectSummary`/`orderIndex`/`condition`/`effect`/`magnitude`/`target`/`source`).

**Therefore:**

- No authoritative document is amended.
- No carrier/event is extended, and no member is added.
- No new event is proposed — an existing carrier expresses the requirement, because the requirement
  is "name the Relic the server said fired", and the identity is delivered while the name is
  available from an existing, already-consumed read.
- Reconnect/reconstruction semantics are unaffected: `GET /api/relics` is a plain authenticated read,
  and a callout is transient feedback rather than reconstructable state.

**Two documentation observations (reported, not fixed — TASK-217B's class):**

1. `SIGNALR_PROTOCOL.md:1205` illustrates `"relicId": "relic-berserker-core"` — a **definition-id
   spelling** — while its own item 1 (`:1214-1219`) and the implementation
   (`RelicResolver.cs:314`, `:381`) use the **owned instance identity**. `API_CONTRACTS.md:977`
   illustrates it correctly (`"relic-instance-1"`). An implementer reading only the example could
   build a definition-keyed map, which would never match `RelicTriggered.relicId`.
2. `MVP_SCOPE.md:80` lists "Trigger system (OnMatch, OnCombo, OnHpBelow, etc.)"; `OnMatch` is a
   defined Trigger but is **not** evaluated and no provisioned Relic declares it. Already reported as
   **A-11e** / **N-13** by TASK-213 and assigned to TASK-217B.

---

## 8. Test coverage — current state and exact gaps

### 8.1 What is well covered

| Area | Evidence |
| --- | --- |
| **3 / 4 / 5 equipped Relics** — all three counts | `RelicLoadoutServiceTests.cs`: `Validate_ShouldAcceptThreeRelics`, `_ShouldAcceptFourRelics`, `_ShouldAcceptFiveRelics`, `_ShouldRejectCountsOutsideThreeToFive`, `LoadoutBounds_ShouldBeTheDocumentedThreeToFive` |
| Loadout ownership, duplicates, slot order | `RelicLoadoutServiceTests.cs` (19 further cases incl. `_ShouldRejectTheSameInstanceTwice`, `_ShouldNotCollapseADuplicateIntoARangeCount`, `_ShouldPreserveTheSubmittedOrderAsSlotOrder`, `_ShouldPreserveOrderForAFiveRelicSelection`); `RelicLoadoutSnapshotTests.cs` |
| **All 10 Relics' triggers at runtime** | `Task177RelicFiringPointRuntimeTests.cs` covers every new firing point by name: Burning Curse (`CreateBattle_BurningCurse_ShouldApplyItsStandingModifierAtBattleStart`, `BurnTicks_ShouldApplyBurningCurseToPetOwnedBurnOnly`), Arcane Battery (2), Cascade Core (`…ShouldResolveOncePerCascadeIteration`), Battle Instinct (2), Execution Mark, Combo Fang (`…ShouldApplyAtComboFiveAndAbove_AndNothingBelowIt`), plus `CommittedSwap_ExistingFourRelics_ShouldResolveExactlyAsTheirDocumentedContracts` |
| `RelicTriggered` **emission** for the new Relics | `Task177RelicFiringPointRuntimeTests.cs:219-220`, `:320-326` (one per cascade iteration), `:376-377`, `:465-466`, `:543`, `:637`, `:705-706`; plus `PowerChanged{Source=Relic}` at `:226-228`, `:330-332`, `:723-724` |
| Emission + slot order (starter set) | `RelicStageResolutionTests.cs`: `CommittedSwap_ShouldEmitRelicTriggeredAndPowerChanged_InSlotOrder`, plus step-11 placement, condition-failure, content-miss, and the one-write-back cases |
| Wire shape is exactly two members | `RelicWireProjectionTests.cs:51-56`, `:59-74` |
| Client parses the event verbatim | `BattleEventPresentation.test.ts:486-498` (verbatim `relicId`), `:499-511` (an extra `effectSummary` is **not** surfaced), `:912-918`, `:957-981`, `:983-992` |
| Client never renders the raw line | `BattleEventPresentation.test.ts:952-954` |
| Relic content rendering (client) | `ContentEffectFormat.test.ts`; `LobbyScene.test.ts` (all 10 names); `CollectionViewerScene.test.ts` |
| E2E: all 10 owned Relics reachable + content rows | `standalone-web-smoke.mjs:1593` (`phase4.everyOwnedRelicIsReachable`), `:1622` (`phase4.relicRowsStateTriggerConditionAndEffect`), `:1715` |
| E2E: 3-of-10 loadout selection | `standalone-web-smoke.mjs:1775`, `:1816` |
| E2E: PLAY AGAIN preserved Relic selection is editable | `standalone-web-smoke.mjs:3447` (`phase8.preservedRelicSelectionIsEditable`), `:3524-3526`; client `PreservedLoadout.test.ts` |

**Does the suite only exercise the original starter Relics?** **No — not for runtime triggering.**
This is worth stating precisely because the pre-TASK-184 suites *are* starter-centric:
`RelicResolverTests.cs` names only Berserker Core, Assassin Eye, and Emergency Core;
`RelicStageResolutionTests.cs` names only Berserker Core, Assassin Eye, and Mana Crystal; and the
client suites `SceneLifecycle.test.ts`, `GameRuntime.test.ts`, `CollectionService.test.ts`,
`ContentEffectFormat.test.ts`, and `CollectionViewerScene.test.ts` reference only the starter set
(plus Burning Curse in two). The 6 TASK-184 Relics are covered by the provisioning/content suites
(`RemainingMvpRelicDefinitionProvisioningTests.cs`, `RelicProvisionedContent.cs`,
`PetCardRelicDefinitionPostgresProvisioningTests.cs`), by `Task177RelicFiringPoint*`, and by
`LobbyScene.test.ts` — so runtime reachability for all ten **is** asserted, and the gap is
*level* coverage rather than per-Relic absence.

### 8.2 Exact gaps

```text
G-1  NO test asserts a player-facing Relic callout.                      [PRESENTATION GAP]
     The only client assertion about RelicTriggered's callout is the
     OPPOSITE one: BattleEventPresentation.test.ts:601-623 asserts
     describeEventCallout(...) === null for every non-callout type, with
     RelicTriggered in the loop at :614. Adding a callout requires editing
     that table — it is a deliberate, currently-passing assertion.

G-2  NO resolver-level unit test for the 6 TASK-184 Relics.              [COVERAGE LEVEL]
     RelicResolverTests.cs exercises 3 named Relics; the new six are only
     covered one level up, in Task177RelicFiringPointRuntimeTests.cs.
     Not a correctness gap — a unit/property-level one.

G-3  Reconnect / resync asserts nothing about Relics.                    [COVERAGE GAP]
     RedisBattleRecoverySmokeTest.cs, BattleHubReconnectRecoveryTests.cs and
     BattleStateServiceReconnectRecoveryTests.cs contain ZERO relic
     references. The recovery suite covers the read path only
     (GetOwnedBattleStateAsync ownership + snapshot-unchanged), and never
     resolves a Swap on a recovered battle. See F-2.

G-4  Multi-Relic batch selection is untested.                            [PRESENTATION GAP]
     No test covers a batch carrying MORE THAN ONE RelicTriggered, which is
     the case the single-callout mechanism handles least well (§3.4 item 2).

G-5  The E2E callout probe pins the current vocabulary and never injects
     RelicTriggered.                                                     [E2E GAP]
     standalone-web-smoke.mjs:402-422 FEEDBACK_INJECTION carries
     MatchCreated/ComboChanged/PowerChanged/Damage* only.
     :2382 asserts every observed callout matches ^(MATCH|COMBO ×\d+)$;
     :2417 asserts callouts.length === 1 and includes('COMBO ×4').
     A Relic callout in the same ladder contradicts both assertions.

G-6  Burning Curse has no observable trigger evidence anywhere.          [BY DESIGN]
     Task177 asserts its MODIFIER (a state effect), not an event. No test
     can assert a RelicTriggered for it, because none is emitted. Correct
     as-is; recorded so the presentation's 9-of-10 coverage is not mistaken
     for a test omission.
```

### 8.3 How the suites are run (existing evidence)

```text
backend      dotnet test  (tests/backend — four xUnit projects:
                           Domain, Application, Api, Infrastructure)
client       pnpm vitest / the client package's test script
                           (src/frontend/client/tests — 22 spec files)
E2E          node scripts/standalone-web-smoke.mjs
                           ← a 203-byte root wrapper importing
                             src/frontend/client/scripts/standalone-web-smoke.mjs
                             (the real 177 KB suite; TASK-189)
                           Requires PostgreSQL + Redis + GameServer.Api (TASK-221 §evidence)
```

No test-count/summary figure is asserted here: no run was performed by this audit (it is
audit-only), and no stored count was verified.

---

## 9. Downstream impact

### 9.1 TASK-218 — **UNCHANGED, READY, still implementation-only**

```text
Priority            P2  (unchanged)
Contract impact     NONE  — confirmed independently (§7)
Backend impact      NONE
Blast radius        two client files (BattleEventPresenter.ts, BattleScene.ts)
                    + two client specs + one E2E phase
Coverage            3 of 10 relic definitions when first proposed in TASK-211/212
                    → 10 of 10 after TASK-221
```

TASK-221's own audit already raised this task's value on exactly this basis
(`TASK-221A:875` "coverage goes from 3 of 10 relic definitions to 10 of 10"). This audit confirms it
and supplies the reachability evidence (§4), which the earlier records did not enumerate per Relic.

**One correction to the inherited task statement:** TASK-210 §Remaining Issue 2 called this a
"read-contract gap". It is not. The read exists, is documented, and is already consumed by two other
scenes. TASK-211/212 corrected this; this record re-verifies it against source.

**Two facts the implementation must carry that the inherited statement does not mention:** the
single-callout-per-batch constraint (§3.4) and Burning Curse's permanent non-observability (§2.4).
Neither is a blocker; both bound what the task can deliver.

### 9.2 TASK-215A — **UNCHANGED** (P3, test-only)

Pins the Passive / Tier / Star deferral. Touches no Relic path, no battle-event presentation, and no
`BattleScene` presentation state. No assumption it relies on is changed by this audit.

### 9.3 TASK-216 — **UNCHANGED** (P2, corrected subject)

Assert the composed battle-end reward path persists Player XP, and report the three-write
non-atomicity. Unrelated to Relic presentation; no reward, XP, or persistence path is implicated
here. The `PlayerStarterGrantFactory` grant set is read by this audit but is not modified, so no
fixture it depends on moves.

### 9.4 TASK-217A / 217B / 217C — **UNCHANGED**, with two additions to 217B

- **217A** (architecture/component reconciliation + stale code comments): unchanged. This audit
  found **one additional stale comment**, in the same class 217A already owns:
  `BattleScene.ts:1143` names a `renderEventCallout` method that does not exist (the method is
  `showCallout`, `:2046`). Report only.
- **217B** (cross-reference / stale-claim sweep): **scope grows by two low-severity items**
  (both in §7 above) — `SIGNALR_PROTOCOL.md:1205`'s definition-id-looking `relicId` example, and
  `MVP_SCOPE.md:80`'s `OnMatch` wording (already 217B's as A-11e). Plus the misclassification note:
  TASK-210's "contract gap" phrasing and `BattleEventPresenter.ts:624-628`'s comment now describe a
  gap that the audit has shown is presentation-only. 217B must **not** re-decide anything; the
  contract is settled.
- **217C** (Card-cost authority ruling): unchanged and untouched; this audit adds no cost statement
  and no `CardCostModifiers` work.

### 9.5 TASK-219B — **UNCHANGED**, sequencing courtesy only

Its remaining scope (V-1, V-2, C-3, C-5, C-6 per `TASK-219A:846`) is unaffected. It shares
`BattleScene.ts`, `BattleEventPresenter.ts`, and `standalone-web-smoke.mjs`'s phase-5/6 regions with
TASK-218, so whichever lands second should rebase rather than merge blindly. **No dependency in
either direction.**

### 9.6 TASK-212B — **REMAINS DEFERRED** (P3, off the MVP order)

Its promotion triggers are still unmet: the only Card-cost source count is 1
(`relic-emergency-core`), and TASK-213 §7's ruling stands. **TASK-218 must not touch it.** In
particular, TASK-218 must not:

- introduce `CardCostModifiers` or any effective-cost member;
- display a Card cost or affordability;
- widen the wire for Emergency Core's modifier.

Emergency Core's *name* appearing in a Relic callout is not a cost display and does not promote
TASK-212B — but the boundary must be stated, because Emergency Core is the one Relic whose effect is
a cost modifier.

### 9.7 Not reopened

TASK-213's content-reachability decision (3–5 loadout kept; 10 Relics owned) and TASK-221's
provisioning are **not** reopened: no contradictory evidence was found. The loadout rule is 3–5 in
both `RELIC_RULES.md` §2.1 (`:195`) and `MVP_SCOPE.md` §1 (`:81`), `RelicLoadoutService` is unchanged,
and this audit makes no loadout change.

### 9.8 Issue reported, not fixed (AGENTS.md §16)

```text
F-1  Relic content is attached to an IN-PROCESS registry only.
     Location     BattleStateService.cs:323 (field), :552-557 (written at creation),
                  :2504-2508 (read), :1298 (read)
     Behaviour    _relicConfiguration is a ConcurrentDictionary never serialized into
                  BattleState/Redis (only PetState.EquippedRelics identities are).
                  :2504-2508 returns the state UNCHANGED when the battle has no entry,
                  so a process that did not create the battle resolves no Relic content:
                  no Relic fires and no RelicTriggered is emitted — silently.
     Impact       Latent. In a single-process deployment the creating process handles
                  every action, so this is invisible. It becomes player-visible if an
                  API process restarts while a battle survives in Redis, or if a second
                  instance serves an action for a battle it did not create. Relic effects
                  would stop applying, not merely stop presenting.
     Corroboration the boundary is process-affine: the Boss path indexes the same kind of
                  registry DIRECTLY — _bossConfiguration[battleId].Definition
                  (BattleStateService.cs:1510) — which would throw rather than degrade.
     Coverage     Untested (G-3). No reconnect/recovery test resolves a Swap on a
                  recovered battle with a fresh service instance.
     Classification  MECHANIC/RECOVERY gap. NOT a presentation gap and NOT TASK-218's.
     Suggested follow-up  Its own task: decide whether the Relic (and Boss) content inputs
                  belong in the recovered battle's state, or must be re-resolved on read.
                  This touches REDIS_STATE.md/GAME_STATE.md, so it is a contract decision
                  before it is an implementation one (AGENTS.md §18/§20).
     NOT fixed here — out of TASK-218's scope; the task forbids changing server behaviour
                  to support presentation, and the richer defect is independent of it.

F-2  The reconnect/recovery suites cover the read path only (G-3). Reported above.

F-3  Five of §3's twelve Triggers have no firing point (TASK-213's N-13). Latent; no
     provisioned Relic declares them. Not decided here.
```

---

## 10. Recommended implementation (the narrow boundary)

**Verdict: PASS.** The implementation boundary is the following and nothing more.

### 10.1 Production files

```text
src/frontend/client/src/game/scenes/BattleEventPresenter.ts       (extend)
  • Add a `RelicTriggered` arm to `describeEventCallout` (:631-683), returning an
    `EventCallout` whose message names the Relic and whose priority is a new ladder
    constant alongside :583-588.
  • Thread a second resolver through the two existing signatures — the seam already
    exists for Cards:
        describeEventCallout(event, resolveCardName, resolveRelicName)   :631
        selectBatchCallout(events, resolveCardName, resolveRelicName)    :695
  • Keep the module's rule: read delivered members only; no derivation, no effect text
    invented, no threshold compared (module doc :19-25).

src/frontend/client/src/game/scenes/BattleScene.ts               (extend)
  • `private relicDefinitions = new Map<string, RelicResponse>();`
  • `private async loadRelicDefinitions(): Promise<void>` — mirror `loadCardDefinitions()`
    (:1192-1209) against `this.runtime.getRelics()`; start it from `create()` beside
    `:424-425`; obey the TASK-205 run-guard pattern for the async continuation.
  • `private relicDisplayName(relicId: string): string` — mirror `cardDisplayName` (:2103):
    `this.relicDefinitions.get(relicId)?.name ?? relicId` (fail-closed to the identity;
    never a guessed name, TASK-208 §D).
  • Pass `(relicId) => this.relicDisplayName(relicId)` at the `selectBatchCallout`
    call site (:1828).
  • Clear the map in `shutdown()` beside `this.cardDefinitions.clear()` (:566).
```

**Explicitly NOT touched:** `GameRuntimeEvents.ts` — `getRelics()` is already on the port
(`:534-538`), and `RuntimeBoundaries.test.ts:576-606` forbids event-type names appearing in that
module. No new module, class, scene, or notification system.

### 10.2 Backend / contract / documentation

```text
Backend changes      NONE
Contract changes     NONE  (§7 — Option A)
Documentation        NONE in this task. The three drift items found (§7, §9.4) are
                     reported to TASK-217A/217B, which own them.
```

### 10.3 Presentation mechanism to reuse

```text
the existing single batch callout, in place:
  selectBatchCallout → showCallout          BattleScene.ts:1828-1831, :2046-2067
the existing collection-read + name-map pattern:
  loadCardDefinitions → cardDisplayName     BattleScene.ts:1192-1209, :2103
```

**No second notification system, no new queue, no new HUD region, no turn banner.**

### 10.4 Decisions the implementation task owns (presentation, not contract)

1. The Relic callout's **priority slot** in the ladder, given the single-callout-per-batch rule
   (§3.4 items 1–2) — including what it displaces, or is displaced by, when a Combo is present.
2. The **message wording**, and whether it states only the name or also the Relic's authored effect
   text — bounded by §5.1 (Content text is permitted; a *result* may be stated only where the server
   stated it, i.e. `PowerChanged{source:"relic"}`).
3. Whether **Emergency Core's per-Swap re-emission** (§3.4 item 3) should be presented every time.
4. How to behave when **several Relics trigger in one batch** (§3.4 item 2) — the mechanism names one.

### 10.5 Test coverage required

```text
src/frontend/client/tests/BattleEventPresentation.test.ts     (EDIT + ADD)
  • REMOVE RelicTriggered from the "no callout" table (:601-623; loop :614) —
    that assertion currently passes and must change by design.
  • ADD: a resolved name is shown; an unresolved id falls back to the raw identity;
    a malformed payload (`{}`, or empty relicId) yields no callout;
    batch selection with a Combo present, pinning the chosen priority;
    a batch with MORE THAN ONE RelicTriggered (G-4).

src/frontend/client/tests/SceneLifecycle.test.ts              (ADD)
  • The Relic map is loaded from getRelics() and cleared on shutdown, mirroring the
    existing cardDefinitions assertions; and no listener/map survives a scene restart.

Backend tests                                                 (NONE)
```

### 10.6 E2E evidence required

```text
src/frontend/client/scripts/standalone-web-smoke.mjs          (EDIT)
  • Extend the phase-5c FEEDBACK_INJECTION fixture batch (:402-422) with a
    RelicTriggered event carrying an owned instance id the running grant actually
    delivered (from the phase-4 `ownedRelics` snapshot).
  • Widen the two assertions that currently forbid it:
      :2382  every observed callout matches ^(MATCH|COMBO ×\d+)$
      :2417  callouts.length === 1 && includes('COMBO ×4')
  • Assert the rendered text carries the delivered `name` (not the raw `relicId`) —
    i.e. that the getRelics() read reached the battle scene.
  • Keep the existing phase-4 Relic assertions (:1593, :1622) unchanged.

Evidence to record: the two E2E runs (full + reduced-motion if the suite still runs both),
the callout text observed, and a screenshot of the callout row.
```

### 10.7 Scope guard

```text
MUST NOT   implement or alter any Relic mechanic, trigger, condition, or magnitude
MUST NOT   add Relic effects, ownership, acquisition, or a new event/wire member
MUST NOT   change the 3–5 loadout rule or RelicLoadoutService
MUST NOT   introduce CardCostModifiers, cost display, or affordability (TASK-212B stays deferred)
MUST NOT   change server-authoritative combat behaviour or the BattleStart no-event rule
MUST NOT   add a Relic icon set, relic HUD panel, turn banner, or event queue
MUST NOT   make the callout authoritative or let the client decide a trigger occurred
```

---

## 11. Audit record — method and limits

- **Verified in source, not inherited.** Every load-bearing claim carries a `file:line`. Where a
  prior task record disagreed with the code (TASK-210's "contract gap"; TASK-148's "BattleScene
  already presents every event type the presenter returns"), the code was read and the record is
  corrected here rather than repeated.
- **Three independent investigations** were run over the backend runtime path, the client
  presentation surface, and the Relic DTO/read, and were reconciled against this record's own reads;
  the per-Relic test matrix, the loadout-count coverage, the reconnect gap, and the E2E assertions
  were verified directly.
- **Excluded from every search:** `node_modules`, `bin`, `obj`, `dist`.
- **No test suite was executed.** This is an audit-only task; no build, test, or E2E run was
  performed and none is claimed.
- **One documentation inconsistency was found and is reported, not resolved** (§7 item 1): two
  authoritative documents are not in conflict — `SIGNALR_PROTOCOL.md` §3.2.23's normative item 1 and
  `API_CONTRACTS.md` §5.4 agree, and only the illustrative example at `:1205` is misleading. Under
  `AGENTS.md` §4 this is drift for TASK-217B, not a conflict requiring a stop.
- **No file other than this record was created or modified**, and no Git state was staged, committed,
  reset, or cleaned.

---

TASK-218A AUDIT COMPLETE

Decision: PASS

Relic baseline: 10 canonical Relics, all content-defined, all provisioned
(`20260929152651`, `20261003074309`, `20261004153916`; `RELIC_RULES.md` §6/§8.7), and all 10 granted
as owned instances by `PlayerStarterGrantFactory.cs:128-137`.

Loadout rule: 3–5 equipped Relics per battle — unchanged and unchallenged. `RELIC_RULES.md` §2.1
(`:195`), `MVP_SCOPE.md` §1 (`:81`), `API_CONTRACTS.md` §3 (`:384`).
`RelicLoadoutService`/`RelicLoadoutValidation` untouched.

10-Relic reachability: **All 10 reachable; none unreachable by mechanic.** All 7 Triggers the ten
declare are evaluated at 5 wired firing points (`RelicFiringPoint.cs`; `RelicResolver.cs:422-446`;
`BattleStateService.cs:531`, `:1260`, `:1303`, `:1347`, `:1917`, `:2283`). Qualifications:
Emergency Core / Execution Mark need Pet HP < 30%; Combo Fang needs the Swap's Combo to reach 5;
Burning Curse fires but is not observable. Five of §3's twelve Triggers (`OnMatch`, `OnDamageDealt`,
`OnCardCast`, `OnTurnStart`, `OnTurnEnd`) have no firing point, but **no provisioned Relic declares
them** (TASK-213 N-13; latent, not decided here).

Runtime trigger source: server-authoritative. `RelicResolver.Resolve` at `GAME_RULES.md` §17 step 11
(`BoardResolution`) plus `BattleStart`, `CascadeIteration`, `PowerGain`, `DamageTaken`; emission
gated on `applied.AnyEffectApplied` (`RelicResolver.cs:375-382`); projected by
`BattleEventWireProjection.cs:553-556`, `:749`, `:767-768`.

Client-observable trigger: **YES for 9 of 10.** `RelicTriggered` is delivered as a discrete member of
the `ReceiveEvents.events[]` batch (`BattleHub.cs:1102`, `:1143`, `:1177`) and parsed client-side
(`BattleEventPresenter.ts:201-202`, `:454-463`). **NO for Burning Curse** — the `BattleStart` firing
point passes `events: null` (`BattleStateService.cs:531-537`, rationale `:525-528`);
`GAME_STATE.md` §2.0.5.2 item 2. No Relic modifier carrier is projected (`PetStatePayload` = 8
members, `BattleHub.cs:235-245`, `:1292-1296`); only Power relics also deliver a result
(`PowerChanged{source:"relic"}`).

Presentation mechanism: reuse the **existing single batch callout** —
`describeEventCallout`/`selectBatchCallout` (`BattleEventPresenter.ts:631`, `:695`) →
`showCallout` (`BattleScene.ts:2046`) at its existing call site (`:1828-1831`) — plus the existing
collection-read + name-map pattern (`loadCardDefinitions` `:1192-1209` / `cardDisplayName` `:2103`)
against the already-available `Runtime.getRelics()` (`GameRuntime.ts:812-814`). The resolver seam
(`resolveCardName`) already exists and is extended, not duplicated. **Constraint carried:** the
mechanism shows **at most one callout per batch** (`:695-709`), and `BattleScene.ts` currently has
**zero** Relic awareness.

Contract change: **NONE — Option A.** `SIGNALR_PROTOCOL.md` §3.2.23 item 5 (TASK-131 **D10**) fixes
`{ type, relicId }` as the **final** shape, "not a placeholder"; `ADR-018` item 10 and
`RELIC_RULES.md` §7 agree; `API_CONTRACTS.md` §5.4 `:1070-1071` confirms nothing new reaches the
wire; `GAME_EVENTS.md` §2 item 4 defers the member question there; §3.2.25 records the omission
convention. **Do not** add an `effectSummary`, an order index, or a name member.

Server-authoritative boundary: **HOLDS, no client-side derivation defect found.** The client neither
decides a trigger, nor simulates an effect, nor derives a result from `effectDefinition`
(`ContentEffectFormat.ts` performs `null`-presence checks only; its two call sites are the Lobby and
the Collection viewer, never battle). `GameRuntime` forwards events uninterpreted
(`GameRuntime.ts:106`, `:117-119`). Deterministic resolution untouched. The identified defect is a
**comment** (`BattleEventPresenter.ts:624-628`, TASK-210's "contract gap"), i.e. documentation
drift for TASK-217A/217B — not a boundary defect.

Test gaps: **G-1** no test asserts a player-facing Relic callout (and
`BattleEventPresentation.test.ts:601-623`, loop `:614`, currently asserts the opposite — it must be
edited). **G-2** no resolver-level unit test for the 6 TASK-184 Relics (they are covered one level
up, in `Task177RelicFiringPointRuntimeTests.cs`). **G-3** the three reconnect/recovery test files
(`RedisBattleRecoverySmokeTest.cs`, `BattleHubReconnectRecoveryTests.cs`,
`BattleStateServiceReconnectRecoveryTests.cs`) contain **zero** Relic references. **G-4** no
multi-`RelicTriggered` batch selection test. **G-6** Burning Curse's non-observability is by design,
not a test omission. Covered well: 3/4/5 loadouts (`RelicLoadoutServiceTests.cs`), `RelicTriggered`
emission for the new Relics (`Task177RelicFiringPointRuntimeTests.cs:219`, `:320-326`, `:376`,
`:465`, `:543`, `:637`, `:705`), wire shape (`RelicWireProjectionTests.cs:51-56`, `:59-74`).

E2E gaps: **G-5** — `standalone-web-smoke.mjs` phase 5c injects
Match/Combo/Power/damage only (`:402-422`) and **actively forbids** a Relic callout: `:2382` pins
every callout to `^(MATCH|COMBO ×\d+)$` and `:2417` pins `callouts.length === 1`. Both must be
widened, and a `RelicTriggered` fixture added asserting the delivered **name** is shown. Phase 4's
Relic ownership/content reachability (`:1593`, `:1622`) and phase 8's
`preservedRelicSelectionIsEditable` (`:3447`) already exist and stay unchanged.

Downstream impact: **TASK-218** UNCHANGED — P2, implementation-only, no contract change, 2 client
files + 2 client specs + 1 E2E phase; coverage rises 3-of-10 → 10-of-10. **TASK-212B** remains
DEFERRED and must not be enabled by this work (no `CardCostModifiers`, no cost display — Emergency
Core's name is not a cost display). **TASK-215A / TASK-216 / TASK-217A / TASK-217C** UNCHANGED.
**TASK-217B** gains two low-severity drift items (`SIGNALR_PROTOCOL.md:1205`'s definition-id-looking
example; the TASK-210/presenter "contract gap" phrasing) plus already-owned A-11e;
**TASK-217A** gains `BattleScene.ts:1143`'s stale `renderEventCallout` name. **TASK-219B** UNCHANGED
— shared files are a sequencing courtesy, not a dependency. **New, reported not fixed (F-1):** Relic
content lives only in the in-process `_relicConfiguration` (`BattleStateService.cs:323`, `:552-557`,
`:2504-2508`), so a process that did not create the battle resolves no Relic — latent, untested
(G-3), a mechanic/recovery contract question needing its own task, explicitly outside TASK-218.
TASK-213's and TASK-221's decisions are not reopened.

Recommended implementation: client-only, no backend, no contract, no docs.
`BattleEventPresenter.ts` (add the `RelicTriggered` callout arm and thread
`resolveRelicName` through the two existing signatures) and `BattleScene.ts` (add
`relicDefinitions` map + `loadRelicDefinitions()` mirroring `loadCardDefinitions()` + `relicDisplayName()`
mirroring `cardDisplayName()`, pass the resolver at `:1828`, clear the map in `shutdown()` beside
`:566`). Mechanism reused: the existing single batch callout and the existing name-map pattern — no
new notification system. Tests: edit `BattleEventPresentation.test.ts:601-623` (remove the
"no callout" row) and add resolved-name / raw-id-fallback / malformed / priority / multi-trigger
cases; add load-and-clear coverage in `SceneLifecycle.test.ts`; no backend tests. E2E: add a
`RelicTriggered` fixture to phase 5c and widen `:2382` and `:2417`, asserting the delivered name (not
the raw id) is rendered. Decisions the implementation owns (presentation, not contract): the priority
slot, the wording, Emergency Core's per-Swap repetition, and multi-trigger batch behaviour.

Implementation: NOT DONE
Production changes: NONE
Test changes: NONE