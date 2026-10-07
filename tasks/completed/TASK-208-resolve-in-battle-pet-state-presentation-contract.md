# TASK-208 — Resolve the In-Battle Pet-State Presentation Contract

```text
Task ID:            TASK-208
Type:               DOCUMENTATION (Product-Owner decision register; contract
                    resolution only — no production code, no docs/ file, no
                    test file, no asset, no ADR)
Status:             DONE (decision recorded; see "What DONE means here")
Risk:               LOW for this task (zero files under src/, tests/, docs/).
                    The downstream amendment this authorizes is MEDIUM–HIGH
                    (it widens a frozen realtime projection that is
                    cross-referenced by GAME_STATE.md, REDIS_STATE.md,
                    BOSS_RULES.md, API_CONTRACTS.md and several tests).
Priority:           P0 (gates TASK-209 and the audit's G1/G2/G3/G7)
Primary Agent:      review / product-decision (AGENTS.md §4; TASK-160 /
                    TASK-161 precedent)
Supporting Agents:  realtime (SIGNALR_PROTOCOL.md §4 owns the projection),
                    gameplay (GAME_STATE.md §2.3/§2.4 own the state; BOSS_RULES.md
                    §6.2.6 owns the Boss visibility constraint),
                    client (BattleScene / GameRuntime consumption boundary)
Workflow:           documentation/documentation-change.md (recording discipline
                    only — this task edits no docs/ file)
Skills:             realtime/realtime-protocol-validation,
                    discovery/impact-analysis,
                    discovery/documentation-discovery,
                    quality/scope-validation
                    (4 skills — Simple budget, tasks/README.md §12)
Dependencies:       TASK-207 (DONE — the audit that reported stop condition J-1
                      and reserved this ID; read-only, immutable),
                    TASK-160 (DONE — the D-1A/D-2A Product-Owner decision
                      precedent; immutable),
                    TASK-161 (DONE — the documentation-amendment precedent for
                      TASK-160; immutable),
                    TASK-200 (DONE — Q-6/Q-7 the §J-2 conflict cites),
                    TASK-150 / TASK-151 (DONE — PowerChanged source semantics),
                    TASK-095 / TASK-096 (DONE — StatusEffect state + round trip),
                    TASK-143 / TASK-144 (DONE — reconnect/resync, §7)
Blocks:             TASK-209 (projection + battle HUD) and the docs amendment
                    recorded in §K. TASK-210 is independent and is NOT blocked
                    by this task.
Evidence base:      working tree at HEAD `afe5b14` (2026-10-07)
Model:              Gemini 3.8 — Reasoning: High
```

**Scope discipline.** This task decides and records a contract. It did **not**
modify any production file (`src/`), any test (`tests/`), any authoritative
document (`docs/`), or any asset, and it performed **no implementation**. The
single file created is this record. Nothing in TASK-203–TASK-207 is re-opened
or reported as regressed.

**What DONE means here.** The contract question is resolved and the decision is
implementation-ready (D-208-01 … D-208-05, §I, §K, §L). It does **not** mean
`docs/` already contains the amended contract: per the TASK-160 → TASK-161
precedent (`AGENTS.md` §7, §17) the authoritative edit is its own gated
deliverable, specified verbatim in §K. **No code may implement the contract
before that amendment lands.**

---

## 0. Verification Performed (evidence base)

Everything asserted below was read in the repository, not inferred.

```text
Authoritative documents
  docs/00-overview/GDD.md                      §2 (loop, line 86), §6, §9,
                                               §11, §12, §14, §17
  docs/00-overview/MVP_SCOPE.md                §1 (Combat / Bosses / Pets), §2, §4
  docs/02-technical/SIGNALR_PROTOCOL.md        v2.16 — §4 (items 4, 6, 11, 12, 13,
                                               14, 15, 16, 17), §4.1, §4.2 (items
                                               2–3), §4.3 (items 2–14), §4.4
                                               (items 1–9), §3.2.14–§3.2.18,
                                               §3.2.24, §5, §6, §7 (items 1–3)
  docs/02-technical/GAME_STATE.md              §0 item 5, §2.0.3, §2.3 (member
                                               tree, "implemented so far",
                                               delivery split, combat-stat
                                               paragraph, absence conventions),
                                               §2.3.1, §2.3.2 item 1, §2.4,
                                               §2.4.1, §2.6, §2.8, §3, §4, §5.1
  docs/02-technical/API_CONTRACTS.md           §3 (+ §5.1 pointer), §4, §4.5,
                                               §5.3, §5.4, §5.6
  docs/02-technical/ARCHITECTURE.md            §2.2.1 rules 1–6, §2.2.3, §5
  docs/01-game-design/PET_RULES.md             §5.7, §6, §8
  docs/01-game-design/BOSS_RULES.md            §6.1, §6.2.6, §6.3.1, §6.4
  docs/01-game-design/GAME_RULES.md            §12 (Power: "Range: 0–100",
                                               the active Pet's battle resource);
                                               §16/§17/§18/§20 headings confirmed
                                               and cited through the cross-
                                               references in GAME_STATE.md /
                                               SIGNALR_PROTOCOL.md / MVP_SCOPE.md
  docs/01-game-design/PASSIVE_RULES.md         §6 item 1
  docs/03-decisions/ADR/ADR-001, ADR-004, ADR-008,
    ADR-011, ADR-014, ADR-017, ADR-021, ADR-022 (index: docs/03-decisions/README.md)

Backend implementation (read-only)
  Domain/Battle/PetState.cs                    record shape (PetId, HP, MaxHP,
                                               ATK, DEF, Crit, Power, Element,
                                               PassiveId, PassiveProgress, …
                                               + 5 collections), DefaultMaxHP/
                                               DefaultHP/DefaultATK/DefaultDEF/
                                               DefaultCrit/DefaultPower,
                                               AtBattleCreation
  Domain/Battle/BossState.cs                   record shape (BossId first member)
  Domain/Battle/Serialization/BattleStateJson.cs
                                               PetHP "hp", PetMaxHP "maxHp",
                                               PetPower "power", BossId "bossId",
                                               BossHP/BossMaxHP
  Domain/Battle/Serialization/BattleStateSerializer.cs
                                               ToBossStateJson (BossId.Value),
                                               pet mapper
  Domain/Match3/ResourceGenerator.cs           ApplyPower (clamp 0–100),
                                               ApplyHeal (clamp to MaxHP,
                                               "No event" — healing emits nothing)
  Domain/Cards/CardCastExecutor.cs             EffectiveCardCost vs PetState.Power
  Domain/Relics/RelicResolver.cs               Power effect write + PowerChanged
  Application/Battle/BattleStateService.cs     Power mutation sites, Boss
                                               PowerDrain, Boss HP writes
  Api/Hubs/BattleHub.cs                        BattleStateUpdated record,
                                               PetStatePayload, BossStatePayload,
                                               ToPayload(BattleState),
                                               ToPetStatePayload, GetBattleState
                                               (same ToPayload → §7 parity)
  Api/Controllers/BattleStartResponse.cs       BattleStartPetState (HP/MaxHP/ATK/
                                               DEF/Crit/Power/…), BattleStartBossState
                                               (BossId/Element/HP/MaxHP/…)
  Infrastructure/Postgres/Repositories/PlayerRepository.cs:191-222,
    PetRepository.cs:102-134, Application/Battle/BattleResultService.cs:358-359,
    511-531                                    §J-3 evidence

Frontend implementation (read-only)
  game/runtime/GameRuntimeEvents.ts            RuntimeBattleState, RuntimePetState
                                               (5 members), RuntimeBossState
  game/runtime/GameRuntime.ts                  startBattle (:598-613, initialState
                                               deliberately not consumed :562-567),
                                               resync (:630-691), receiveBattleState
                                               (:1011-1104), readPetState (:1166+),
                                               readBossState
  services/realtime/SignalRService.ts          transport PetStatePayload / BossState…
  services/api/BattleModels.ts                 battle-start response models
  game/scenes/BattleScene.ts                   HUD readout (:441-467), cast controls
  game/scenes/LobbyScene.ts                    MVP_BOSSES static catalog (:39-85),
                                               scene.start('BattleScene') with no
                                               scene data (:678)
  game/state/PreservedLoadout.ts               in-memory only (:48-49), bossId member

Tests (read-only, contract-pinning)
  tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
    :400 :632 :1031 :1123 :1191 :1312
  tests/backend/GameServer.Api.Tests/Hubs/PetStateWireProjectionTests.cs
  tests/backend/GameServer.Api.Tests/Hubs/BattleHubReconnectRecoveryTests.cs:292

Task records (read-only)
  tasks/completed/TASK-207 (audit), TASK-160 (decision precedent),
  TASK-161 (amendment precedent), TASK-200 (Q-6/Q-7), TASK-150/151,
  TASK-121, TASK-093/095/096, TASK-143/144, TASK-163/164
```

---

## A. Confirmed Missing State (what TASK-207 asserted, re-verified)

```text
HUD fact the player needs        Authoritative state member         Exists?  Delivered today?
-------------------------------  ---------------------------------  -------  ------------------------
Active Pet current HP            PetState.HP   (GAME_STATE §2.3)    YES      NO
Active Pet MaxHP                 PetState.MaxHP (GAME_STATE §2.3)   YES      NO
Power                            PetState.Power (GAME_STATE §2.3)   YES      NO (state); event-only
                                                                    (see §C)
Boss identity (presentation)     BossState.BossId (GAME_STATE §2.4) YES      NO (state); event-only
                                                                    (see §D)
Boss current HP / MaxHP          BossState.HP / MaxHP               YES      YES (§4.4) — no change
Boss display name                not a BossState member             NO       NO — and BOSS_RULES §6.4
                                                                             forbids it as an identifier
Boss Element in battle           BossState.Element                  YES      NO — deliberately not
                                                                             required (see §D)
Boss portrait / asset key        no such member or catalog exists   NO       NO
Pet Tier / Star / Level          not in PetState at all             NO (§J-2) NO
Pet ATK / DEF / Crit             PetState.ATK/DEF/Crit              YES      NO — not required by the HUD
Pet identity (PetId)             PetState.PetId                     YES      NO — not required, and its
                                                                             exclusion is separately guarded
```

The gap is therefore exactly one thing: **three authoritative Pet combat
values and one authoritative Boss identity are not carried by the state
projection**, although the state holds them, the Redis record round-trips them,
and `POST /api/battle/start` already returns them in `initialState`.

---

## B. Required Question 1 — Pet HP

**Authoritative source.** `PetState.HP` and `PetState.MaxHP`
(`GAME_STATE.md` §2.3; `PetState.cs` positional members `HP`, `MaxHP`). Both are
non-nullable `int`, initialized at battle creation to the documented MVP
configuration (`PetState.AtBattleCreation`; `COMBAT_RULES.md` §1.1 via
`DefaultHP`/`DefaultMaxHP`), and written only by the resolution's single
post-resolution write-back (`GAME_STATE.md` §5.1):

```text
HP   damage target   Domain/Match3/ResourceGenerator.cs ApplyHeal (clamp to MaxHP)
                     + the Damage Pipeline's caller (COMBAT_RULES.md §3.4 step 6)
MaxHP ceiling        read by ApplyHeal and by the Relic HP-threshold condition
                     (RelicResolver.cs HpPercentageBelow)
```

**Both values already exist in authoritative state?** Yes. They are also the
`PetState` members §2.3 lists under "implemented so far".

**Are they currently serialized?** Yes, to the Redis battle record —
`BattleStateJson.cs` names them `hp` / `maxHp` on the serialized `petState`
node and `BattleStateSerializer` carries them one-to-one
(`REDIS_STATE.md` §2 item 1 / §7 item 9's whole-`BattleState` round trip, cited
by `SIGNALR_PROTOCOL.md` §4.3 item 14). They therefore **survive** storage and
recovery.

**Present at battle start?** Yes, twice over: in authoritative state from
creation, and in the REST `initialState` summary (`BattleStartPetState.HP`,
`.MaxHP` — `API_CONTRACTS.md` §3). But `initialState` is *deliberately not read*
by the client (`GameRuntime.ts:562-567`: "**The synchronized copy is established
only by that push**"), and it is unavailable on reconnect, so it cannot be the
HUD's carrier.

**Present in subsequent state synchronization?** **No.** §4.3 item 2 fixes
`petState` at its enumerated member set and names the combat stats among the
§2.3 members that are **not** delivered; §4 item 4 makes the enumeration the
rule; §2.3 states the same split from the state side ("**The combat stats are
state, and they are not delivered on the wire** … Delivering them is a protocol
change owned by its own task").

**Can any existing event expose them?** **No, and this is the decisive point.**

```text
- No event carries a resulting HP. §3.2.14/§3.2.15 DamageDealt/DamageTaken
  carry only `amount` (the Final Damage applied/taken).
- Healing emits NO event at all: ResourceGenerator.ApplyHeal's own contract
  says "**No event.** Healing is a state-only change: GAME_EVENTS.md §2 has no
  heal event, so this step emits nothing", and that "The new HP is delivered,
  if at all, as state".
- Even a hypothetical damage/heal event stream could not reconstruct HP:
  §7 item 2 requires a resynchronizing client to discard local prediction and
  re-render from the snapshot and forbids replaying missed events.
- Reconstructing it client-side is forbidden regardless (AGENTS.md §10,
  ADR-001), and §4.4 item 7 already states the same prohibition for the Boss's
  HP.
```

**Is adding them to the existing projection sufficient?** **Yes, and it is the
only sufficient minimal change.** The join push (§4), every committed Swap's
resolved-state push (§4), and the reconnect/resync snapshot (§7) are produced by
the **one** projection function `BattleHub.ToPayload(BattleState)` (join:
`:815`; resolution pushes: `:1029`, `:1079`, `:1113`; snapshot: `:930-933`).
Widening that projection's `petState` object covers every path at once, with no
new method, subscription, or event.

**Client derivation.** Prohibited and unnecessary. The client must render the
delivered numbers (`AGENTS.md` §10; §4.3 item 9's "neither computes nor derives"
pattern).

---

## C. Required Question 2 — Power

**What `Power` means in the current implementation.**

```text
Definition        PetState.Power — `int`, "resource for casting Cards and
                  Pet Skills, range 0–100" (PetState.cs; COMBAT_RULES.md §1.1,
                  §6; GAME_STATE.md §2.3; GAME_RULES.md §12)
Owner             PetState only. There is no Player resource pool (ADR-011).
Initial value     0 at battle creation (PetState.DefaultPower; Power is
                  generated by Match-3, none before the first Match)
Generation        Domain/Match3/ResourceGenerator.ApplyPower — the SINGLE write
                  site for generation; clamps to 0–100 there, not on the
                  transient ResourceGeneration pool. Sources: POWER Gem matches
                  and the Relic Power effect (RelicResolver.cs PowerEffect)
Consumption       Card cost: Domain/Cards/CardCastExecutor.cs composes
                  EffectiveCardCost.Compose(CardDefinition.PowerCost,
                  PetState.CardCostModifiers) and rejects a cast when
                  PetState.Power < EffectiveCardCost
                  Boss drain: BattleStateService (BossSkillSecondaryEffectKind
                  .PowerDrain) — an instant flat PetState.Power reduction
Synchronization   PowerChanged event (§3.2.24) — `delta` (signed) + `power`
                  (the resulting PetState.Power) + `source`
                  ("match" | "card" | "relic" | "boss"); one mutation, one event
Persistence       Carried in the §5.1 write-back, serialized to Redis as
                  `petState.power` (BattleStateJson.cs:65/:413)
Documented range  0–100 is a documented invariant of GAME_RULES.md §12 /
                  COMBAT_RULES.md §1.1; it is not a state member and no
                  `MaxPower` field exists anywhere
```

**Answers.**

| Question | Answer |
|---|---|
| Is Power already authoritative? | **Yes.** `PetState.Power` is authoritative, server-produced state with a single generation write site and server-side consumption. |
| Is it persistent battle state? | **Yes.** It is written in the one post-resolution write-back (`GAME_STATE.md` §5.1) and round-trips through Redis. It is not transient-resolution state (`GAME_STATE.md` §3). |
| Does the client already receive it under another name? | **No state member.** The client receives the resulting value only as the transient `PowerChanged.power` (§3.2.24 item 2), and the resulting `delta` — neither survives a resync. |
| Is it required for the MVP gameplay decision? | **Yes, from the design's own text.** GDD §9: "the player continuously decides between spending Power for survival now or saving it for a stronger Pet Skill later" — an unrepresentable decision without a visible current value; GDD §17 requires the player to understand "what strategic choice comes next"; MVP_SCOPE.md §1 lists "Power" among MVP IN combat. |
| Is it safe to expose through the existing pet projection? | **Yes, narrowed to the Power value alone.** It exposes no cost, no modifier, and no affordability. |
| **NOT CURRENTLY AUTHORITATIVE** | **Does not apply to Power.** Unlike Tier/Star/Level (§J-2), Power exists, is implemented, and is authoritative. No new mechanic is required to expose it. |

**Explicit boundary (recorded so TASK-209 cannot over-read this decision).**

Delivering `Power` does **not** authorize the client to derive affordability,
cast legality, or a cost:

```text
- Card cost is composed server-side from CardDefinition.PowerCost and
  PetState.CardCostModifiers (CARD_RULES.md §3.6). CardCostModifiers[] is
  explicitly NOT delivered, and §4 item 15 states the client "must never
  compute, predict, or reconstruct a Card-cost modifier" — not from events,
  not from a PowerChanged delta, not from a Card's definition.
- The authenticated client read contract does not expose cost either:
  API_CONTRACTS.md §5.3 excludes `powerCost` from GET /api/cards, so no client
  surface can display an authoritative cost today. That is a separate read-
  contract gap (audit G2 / proposed TASK-212), NOT part of this decision.
- The server remains the only validator of a cast; a rejection arrives on the
  existing §5 acknowledgement path.
```

**Documentation defect found (must be corrected by the amendment).**
`SIGNALR_PROTOCOL.md` §4 item 15 asserts that "the authoritative record of the
resulting `Power` is the state push and `PowerChanged`" — but the state push
carries no Power, so the sentence is currently false. Approving D-208-02 makes
it true; the amendment task must confirm the wording either way (§K item 6).

**Why `PowerChanged` alone is insufficient.** (i) A resync discards events and
never replays them (§7 item 2), so Power is unrecoverable after reconnect;
(ii) one action can perform several Power mutations (§3.2.24 item 6), so a
single event is not the current value; (iii) each battle's events are a stream
the client is not required to retain; (iv) deriving the value client-side is
forbidden (`AGENTS.md` §10, ADR-001). The §4.3 item 11 / §4 item 6 rule applies
verbatim: "A client that wants the current position reads the state."

---

## D. Required Question 3 — Boss Identity

**Candidate-by-candidate verdict (the task's own check list).**

| Candidate | Needed? | Verdict |
|---|---|---|
| boss ID | **YES — required** | The only member this decision adds. See below. |
| boss name | No | The display name is content, not an identifier; it is **not** a `BossState` member. `BOSS_RULES.md` §6.4: the display name "is never used as a technical identifier in state, events, persistence, or the API". Putting it on the wire would also create a second copy of content the client already transcribes. |
| boss display name | No | Same as above. |
| boss type | No | No such concept exists in `BossState`, `BOSS_RULES.md` §6, or the client catalog. Nothing to expose. |
| portrait / asset key | No | **Does not exist.** No asset catalog, no portrait member, no boss art in the client; inventing one would be new content/assets, which this task forbids. |
| current HP | Already delivered | `bossState.hp` (§4.4). No change. |
| max HP | Already delivered | `bossState.maxHp` (§4.4). No change. |
| passive/skill presentation metadata | No | Not delivered, and not required: `PassiveCharged`/`PassiveTriggered`/`BossSkillCast` already carry the boss's `PassiveId`/`SkillId` and `sourceId` (§3.2.16–§3.2.18). A telegraph/charge display would need its own contract decision (see §M) — it is **not** authorized here. |

**The smallest presentation contract required by the existing GDD and assets**

> **One fact: the Boss's canonical technical Identity, read from
> `BossState.BossId`.**

Evidence that this is both minimal and already precedented:

```text
1. The identity is authoritative state, defined from battle creation and never
   changing (GAME_STATE.md §2.4; BossState.cs's first member), and it
   round-trips through Redis (BattleStateJson.cs:146/:786;
   BattleStateSerializer ToBossStateJson → BossId.Value).
2. The value form is exactly what the client catalog is keyed by: BOSS_RULES.md
   §6.4 fixes `boss-hoa-long`, `boss-thuy-ma`, `boss-moc-yeu`,
   `boss-son-thach-ve`, `boss-kim-loi-vuong`.
3. The same value ALREADY reaches the client on events — BossSkillCast.sourceId
   and PassiveCharged/PassiveTriggered.sourceId when source = "boss"
   (SIGNALR_PROTOCOL.md §3.2.16–§3.2.18, BOSS_RULES.md §6.4). This decision
   therefore discloses nothing new: it moves an already-delivered fact onto the
   path that survives a resync. The exact precedent is §4.3 item 3, which
   delivers `petState.passiveId` as "the same value GAME_EVENTS.md §2's
   PassiveCharged/PassiveTriggered already report" — state member + event
   report, not a second representation (§4 item 6: "Neither replaces the
   other").
4. The client ALREADY holds boss presentation content, keyed by that identity:
   LobbyScene.ts:39-85's exported `MVP_BOSSES` (`{ bossId, displayName,
   element }`) is a documented transcription of BOSS_RULES.md §6.4/§6.1, not a
   second definition ("The client has no Boss data source … so the five
   identities are held here rather than fetched"). Delivering the identity lets
   the HUD resolve name and element with **no display string on the wire**,
   which is exactly the task's stated preference.
5. There is no reconnect-safe client-side alternative. BattleScene receives no
   scene data (`LobbyScene.ts:678` is `this.scene.start('BattleScene')`), and
   the preserved loadout is explicitly in-memory only ("nothing about it is
   persisted to localStorage, sessionStorage, the URL, the backend, or a
   database" — PreservedLoadout.ts:48-49). Using the client's submitted
   `bossId` as the HUD's source would also be a second representation of a fact
   the authoritative state owns, and would silently mislabel the boss if the
   client's memory and the server's instantiated battle ever diverged.
```

**Client-boundary rules for TASK-209 (recorded with the decision).**

```text
- Presentation lookup only: bossId → { display name, element label } from the
  existing single transcription of BOSS_RULES.md §6.4. TASK-209 must NOT create
  a second boss catalog, and must not fetch one (no boss read endpoint exists —
  API_CONTRACTS.md §1).
- The resolved element label is a display label. The client must not use it for
  Element matchup logic, damage prediction, or any gameplay result
  (GAME_RULES.md §18, ELEMENT_RULES.md, ADR-001): the server remains the only
  place a matchup is resolved.
- An unrecognized bossId must render the identity or a neutral placeholder —
  never a guessed name.
```

---

## E. Projection Design — Options Evaluated

**Option A — extend the existing `petState` / `bossState`. ACCEPTED.**

```text
- It is the mechanism the protocol itself documents: §4 item 4 — "additional
  state is introduced by extending GAME_STATE.md §2.0, not by the wire shape …
  Adding a member to one of those sets is a protocol change owned by its own
  task, exactly as adding a top-level member is."
- Both objects already exist as narrowed projections (§4.3 item 2, §4.4 item 2);
  this adds members, not objects.
- One projection function serves the join push, every resolution push, and the
  §7 snapshot, so the two paths cannot drift (§7's "same projection, member for
  member" sentence holds unchanged).
- No new top-level member, no new method, no new event, no new subscription, and
  no second spelling of any fact (GAME_STATE.md §0 item 5).
- It is exactly the TASK-160 D-1A shape, already ratified and already applied
  for `petState.statusEffects[]` (§4 item 17, §4.3 item 14).
```

**Option B — a separate `battlePresentationState` object. REJECTED.**

```text
- It would add a top-level payload member for facts that already have documented
  homes under PetState/BossState, duplicating ownership (§0 item 5) and forcing
  a second member set to be kept in step with §4.3/§4.4.
- It requires a new §7 parity statement and a second client state object,
  i.e. a larger change than the requirement, for no capability gain.
- §4 item 11 fixes BattleStateUpdated as the only state-push method; a second
  presentation object would tempt a parallel state shape — the "parallel
  representation" the state contract forbids.
- It is architecture-shaped work where the documented mechanism is extension:
  it would need an ADR (§18), which Option A does not.
```

**Option C — event-only fields. REJECTED.**

```text
- GAME_EVENTS.md §3 item 6 and SIGNALR_PROTOCOL.md §4 item 6: events are not
  state and never substitute for the state write-back.
- §7 item 2: a resyncing client discards local prediction and does not replay
  missed events, so an event-only value cannot be reconstructed.
- Healing emits no event at all (ResourceGenerator.ApplyHeal), so Pet HP is not
  merely inconvenient to derive — it is impossible.
- AGENTS.md §10 forbids client-side reconstruction of HP and Power.
- The task's own rule: "The client must always be able to reconstruct the
  currently displayed state after reconnect, resync, scene recreation, browser
  refresh/rejoin where supported" — state projection is the only option that
  satisfies it.
```

---

## F. Contract Compatibility

```text
1. Nullable / optional fields?
   NOT USED. All four authorized facts are defined from battle creation
   (GAME_STATE.md §2.3, §2.4) and zero is a real published value for HP/MaxHP/
   Power (PetState.DefaultPower = 0 is what a battle begins with; BossState
   hp = 0 is a terminal value and is already sent as 0 — §4.4 item 4). There is
   no absent case for BossId either (a battle always has its one Boss, §2.4).
   They therefore follow the always-present, non-nullable, never-omitted,
   never-null convention of §4.2 item 3 / §4.3 item 4 / §4.4 item 4 — NOT the
   omitted-when-not-applicable convention of §3.2.5. Adding optional members
   would be actively wrong: absence would become a second spelling of a zero.

2. Do all consumers tolerate the additional members?
   The backend is the only producer (one ToPayload). The current client readers
   construct objects with explicit known members and ignore unknown JSON keys
   (GameRuntime.readPetState/readBossState), so an old client against a new
   server degrades gracefully — it simply renders nothing new; nothing throws.
   The reverse pairing (new client, old server) fails closed: the new readers
   would treat the fewer members as a malformed payload and reject the whole
   push. Mitigation is deployment ordering (server first, or both together);
   MVP ships one client and one server from one repository, so there is no
   independent client version to support. Recorded as a TASK-209 rollout note,
   not a contract change.

3. Does the frozen protocol need a version / amendment?
   YES. SIGNALR_PROTOCOL.md is the owner of the projection and is versioned
   (currently v2.16, whose own header records the previous projection widening
   as a protocol revision). Required: a version bump and revision-log entry,
   the §4.3/§4.4 member-set and boundary restatements, and one new §4 item that
   records the fact (mirroring the structure of item 17). §K lists the exact
   sections.

4. Does this require an ADR?
   NO. Direct precedent: TASK-161 classified the TASK-160 D-1A/D-2A projection
   widening as DOCUMENTATION, explicitly not ARCHITECTURE, on the grounds that
   "What changed is the *content* of an existing projection" while the realtime
   strategy, storage strategy, authoritative model, and module boundaries are
   untouched — and the protocol's own revision history treats projection
   changes as protocol revisions (2.9 equippedCards, 2.15 statusEffects/
   bossState). The same is true here: no new store, key, column, method,
   subscription, event, module, or boundary.
   ADR-014 is NOT contradicted: it decides `BattleState.PlayerId` (server-only)
   and the `PetState.PetId` denotation. It says nothing about `BossState.BossId`
   or about the Pet combat stats. Its decision 3's exclusion is about PlayerId
   only.

5. Do existing GameRuntime boundaries need adjustment?
   NO new boundary, NO new port method, NO new transport method. Only the
   synchronized-copy plumbing extends: the client wire types
   (SignalRService.ts's transport payload interfaces, GameRuntimeEvents.ts's
   RuntimePetState/RuntimeBossState), the two runtime readers
   (GameRuntime.readPetState/readBossState), and BattleScene's presentation.
   ARCHITECTURE.md §2.2.1 rule 3 ("the runtime coordinates; it does not
   compute") and rule 5 (the client holds a synchronized presentation copy) are
   preserved unchanged.

6. Does the projection change affect REST battle history/result contracts?
   NO. GET /api/battle/{battleId}/result (§4) and GET /api/battle/history
   (§4.5) project the persisted BattleResult row; §4.5 explicitly exposes no
   Boss/Pet identifying member. This decision must NOT be read as authorizing
   identity on those surfaces — they remain owned by their own contract. POST
   /api/battle/start's `initialState` already carries all four facts and is
   unchanged by this decision.
```

---

## G. TASK-160 / ADR Precedent — Should the Same Pattern Be Reused?

**Yes, member for member.** `petState.statusEffects[]` was added by exactly this
route, and every step applies unchanged:

| TASK-160 step | TASK-208 equivalent |
|---|---|
| A decision-first task that decides nothing about wire wording (`D-1A`, `D-2A`) | This record: D-208-01…05, with member-shape wording left to the amendment task |
| The state already existed; only delivery was unauthorized | Identical: `PetState.HP/MaxHP/Power` and `BossState.BossId` exist, are mutated by the landed pipeline, and round-trip through Redis |
| A narrowed projection under the **existing** carrier | Identical: members added to the existing `petState`/`bossState`, on `BattleStateUpdated` |
| The only state-push method; no new method/event/subscription | Identical (§4 item 11) |
| Existing `GAME_STATE.md`/domain-rule exclusion sentences reworded, not contradicted (`§4 item 2`'s closing sentence, `§2.3.1`'s "Not a wire member") | Identical: §4.3 item 2, §2.3's delivery split, §4.4 item 3, `BOSS_RULES.md` §6.2.6 |
| Client-boundary rules stated in the projection ("renders it and computes none of it") | Identical |
| A separate documentation-amendment task owned the authoritative edit (TASK-161), *then* implementation | Identical: §K (amendment) precedes TASK-209 |
| No ADR | No ADR |

**Conclusion: TASK-208 is a minimal, auditable contract correction on an
existing mechanism — not a new architecture.** It adds members to two
projections that already exist, through the carrier that already exists, using
the conventions that already exist.

---

## H. GDD / Rule Consistency — TASK-207 Stop Conditions

### §J-1 — GDD vs the frozen projection. **RESOLVED.**

Which GDD requirements actually require authoritative projection:

```text
GDD.md line 86   "The battle ends when either the Boss or the active Pet
                 reaches 0 HP."
                 → Pet HP is a battle-ending, player-visible value. Without it
                   the player cannot see the thing that ends the battle.
GDD.md §9        Power (0–100) is the active Pet's battle resource, and "the
                 player continuously decides between spending Power for
                 survival now or saving it for a stronger Pet Skill later."
                 → the spend/save decision requires the current value.
GDD.md §11       Pet and Boss combat stats include HP, Max HP, … Power …
                 → these are the stats the section names.
GDD.md §17       "The player should always understand: … why the Boss is
                 dangerous, and what strategic choice comes next."
                 → the Boss must be identifiable and its pressure readable.
MVP_SCOPE.md §1  "Combat: HP, ATK, DEF, Power, Crit, Status Effects, Damage,
                 Element interaction" is MVP IN; "Bosses: Element, Passive,
                 Skill per Boss" is MVP IN.
```

These reduce to exactly the **four** facts this decision authorizes. The
remaining GDD §17 clauses are **not** part of this contract and are recorded in
§M as separate gaps: "what each Card/Relic changes" needs cost/effect read
contracts (`API_CONTRACTS.md` §5.3/§5.4 exclude them — audit G2 / TASK-212), and
Boss telegraph/passive-skill presentation needs its own decision.

**Stop condition 1 (no authoritative source) is NOT fired for anything this
decision authorizes** — each fact traces to a named state member (§A). It *is*
fired for the fields that do not exist (Tier/Star/Level, portrait, display
name), which are therefore marked NOT CURRENTLY AUTHORITATIVE below and are
**not** fabricated.

### §J-2 — `PET_RULES.md` §5.7/§6 vs TASK-200 Q-6 and the implementation. **CONFLICT RECORDED, NOT RESOLVED.**

```text
PET_RULES.md §5.7 item 2   "Level scales base stats (HP/ATK/DEF) via a stat
                            curve."
PET_RULES.md §6            "Final Stat = f(Base Stat[Tier], Level Curve[Level],
                            Star Bonus[Star])" — "all three axes contribute".
MVP_SCOPE.md §1 (line 53)  "Tier, Star, Level progression" listed as MVP IN;
ROADMAP.md repeats it.
        versus
TASK-200 Q-6               "COSMETIC FOR MVP. Pet Tier/Star/Level remain
                            progression signals without combat stat scaling for
                            MVP."
Implementation             PetState.DefaultMaxHP = 1000, DefaultATK = 50 (fixed
                            constants, no curve); PetState has no Tier/Star/
                            Level member at all; PetTier.cs / Pet.cs record that
                            no Tier/Star progression rule exists in MVP;
                            GDD.md §14 names no owner for one.
```

Per `AGENTS.md` §2 a **specific domain rule outranks a task record**, so the
implemented behaviour and the Q-6 ledger are in tension with `PET_RULES.md`.
This task may not resolve it: resolving it either narrows a domain rule or
authorizes a stat curve, and both are gameplay decisions (`AGENTS.md` §7, §4).

**Consequence for this contract (explicit):**
`Tier`/`Star`/`Level` are **not members of `PetState`** in the current
implementation, so they cannot be exposed by the projection, and the HUD
requires none of them. They are excluded from this decision and recorded as
D-208-05 = *separate product decision required* (recommend TASK-215). Neither
`MVP_SCOPE.md`, `ROADMAP.md`, `PET_RULES.md`, `GDD.md` nor any code is changed
here.

### §J-3 — Player XP persistence ordering risk (`PlayerRepository` / `PetRepository`). **NOT FIXED. VERIFICATION IS REQUIRED.**

Re-verified mechanism (`BattleResultService.cs:358-359` fetches both entities
through the same scoped `DbContext`, then `:518-530` grants and saves):

```text
PlayerRepository.SaveProgressionAsync (:191-222)
    var entry = _dbContext.Entry(player);
    …
    else if (entry.State == EntityState.Unchanged
             && _dbContext.Players.Local.Contains(player))
    {
        return true;                       // ← returns WITHOUT SaveChangesAsync
    }
    await _dbContext.SaveChangesAsync(...)

PetRepository.SaveProgressionAsync (:102-134)
    var stored = await _dbContext.Pets.FirstOrDefaultAsync(...);
    if (stored is null) { return false; }  // ← returns BEFORE SaveChangesAsync
    …
    await _dbContext.SaveChangesAsync(...)
```

The grant is applied in place (`player.GrantBattleXp(...)`), *after* the durable
result write. If EF Core still reports the tracked entry as `Unchanged` at the
guard (change detection having not yet run), the Player write is skipped by its
own repository and is then flushed only incidentally by the very next call —
`PetRepository.SaveProgressionAsync`'s `SaveChangesAsync`. If the Pet row is
absent, that call returns before its `SaveChangesAsync`, and the Player grant
would be dropped while the already-persisted `RewardSummary` reports it as
applied. Nothing later on that path flushes the context (the next step is the
Redis delete).

```text
Verdict: the risk is REAL ENOUGH TO VERIFY, and it is not proven.
  - It is a genuine ordering dependency between two repositories over one
    scoped context, with an early return whose condition ("Unchanged") is a
    questionable proxy for "nothing to write" for an in-place-mutated entity.
  - It is narrow (it requires the Pet row to be absent while the battle's
    PetState names that instance) but the consequence is silent reward loss.
  - It is not fixable by reasoning alone: whether the guard is reached depends
    on EF Core change-detection timing, and no test composes
    BattleResultService with real EF repositories for that case.
  - It is explicitly OUT OF SCOPE here (task non-goals). Recommended:
    TASK-216 — verify with a composed persistence test, then, only if
    confirmed, fix under its own task.
```

---

## I. Required Decision — One Recommended Contract Approach

```text
Recommended approach:
    Option A — extend the two existing narrowed projections on the existing
    `BattleStateUpdated` carrier: add the active Pet's current HP, MaxHP and
    Power to `petState`, and the Boss's canonical technical Identity to
    `bossState`. No new top-level member, no new object, no new method, no new
    event, no new subscription, no new store, no ADR. The exact wire wording
    (member spelling, presence sentence, types, serialization) is authored by
    the documentation-amendment deliverable specified in §K, applying the
    existing conventions this record cites.

Fields added:
    petState  ← PetState.HP      (GAME_STATE.md §2.3) — current HP, int
    petState  ← PetState.MaxHP   (GAME_STATE.md §2.3) — Max HP, int
    petState  ← PetState.Power   (GAME_STATE.md §2.3) — resource, int, 0–100
                 invariant (GAME_RULES.md §12 / COMBAT_RULES.md §1.1); no
                 maxPower/max member is added, because no such state member
                 exists
    bossState ← BossState.BossId  (GAME_STATE.md §2.4) — canonical technical
                 Boss Identity (BOSS_RULES.md §6.4), string
    No other field is added. Explicitly NOT added: PetId, Pet Element,
    Tier/Star/Level, ATK/DEF/Crit, EquippedRelics[], the modifier collections,
    Boss display name/Element/ATK/DEF/State/PassiveProgress/SkillCharge/
    SkillCooldown/StatusEffects[], and any portrait or asset key.

Authoritative source:
    PetState.HP / PetState.MaxHP / PetState.Power and BossState.BossId —
    Active Battle State (GAME_STATE.md §2.3, §2.4), produced by the server,
    mutated by the landed resolution pipeline in the one post-resolution
    write-back (GAME_STATE.md §5.1), and already carried in the Redis battle
    record (BattleStateJson.cs `petState.hp`/`maxHp`/`power`,
    `bossState.bossId`; BattleStateSerializer one-to-one projection).
    No value is computed, defaulted, scaled, clamped, or re-derived by the
    projection.

Why this is the minimum change:
    (a) It adds members to two projections that already exist, through the
        only state-push method that already exists, on the trigger that already
        exists, read by the one projection function that already serves the join
        push, every resolution push, and the §7 reconnect snapshot.
    (b) It exposes exactly the facts the GDD requires for the battle HUD and
        nothing else: two HP numbers, one resource number, one identity.
        Everything else the HUD could show is either already delivered
        (Boss HP) or deliberately out of scope (§C boundary, §M).
    (c) It introduces no new gameplay rule, no new mechanic, no new state
        member, no new event, no new method, no new store, and no second
        representation of any fact (GAME_STATE.md §0 item 5).
    (d) It is strictly smaller than Option B (a new presentation object) and
        strictly sufficient where Option C (events) is not.

Backward compatibility:
    - Additive JSON members on two existing objects; no member removed, no
      member renamed, no member narrowed, no method/event/subscription changed.
    - The backend is the only producer.
    - All four members are always present and non-nullable (the always-present
      convention of §4.2 item 3 / §4.3 item 4 / §4.4 item 4). No optionality,
      no null, no omission: absence would be a second spelling of zero.
    - The current client ignores unknown members, so it degrades gracefully
      against the new server. A new client against an old server fails closed,
      so the server/build must roll out first or together; MVP ships one client
      and one server from one repository.
    - Contract-pinning tests that assert the pre-amendment member sets must be
      updated in the same change as the implementation (list in §L). They must
      continue to assert an exact member set — the new one.

Protocol / ADR changes required:
    - SIGNALR_PROTOCOL.md — REQUIRED: version bump and revision-log entry;
      §4.3 item 2 member set widened and a new §4.3 item authoring the three
      Pet members' source/name/type/presence/client boundary; §4.4 item 2/3
      restated so `bossState` carries its three members with a new item
      authoring the identity's source/casing/client boundary; one new §4 item
      recording the fact in item 17's structure; §4 item 15's currently false
      "the state push" claim corrected. (§K items 1–2, 6)
    - GAME_STATE.md — REQUIRED: the §2.3 delivery-split paragraph and the §2.4
      "Two BossState members are client-visible" paragraph reworded. No state
      shape change: no member is added to PetState or BossState. (§K item 3)
    - BOSS_RULES.md §6.2.6 — REQUIRED: the Boss visibility constraint reworded
      so the canonical Identity is client-visible while the rest of BossState
      remains server-side. Gameplay-visibility wording only: no stat, effect,
      threshold, magnitude, skill, or passive value changes. (§K item 4)
    - API_CONTRACTS.md, DATABASE.md, REDIS_STATE.md, GAME_EVENTS.md — NO change.
      (`initialState` already carries the facts; no new row, key, or event.)
    - ADR — NOT REQUIRED (reasoning and precedent in §F item 4). No new ADR, no
      ADR amendment, no change to docs/03-decisions.
    - Docs amendment must land BEFORE implementation (AGENTS.md §7, §17;
      TASK-160 → TASK-161 → implementation precedent).

Frontend impact:
    - Additive, presentation-only, and it requires NO client-side derivation:
      the delivered values are read and rendered as sent (§4.3 item 9 /
      §4.4 item 7 pattern).
    - Extend the synchronized-copy plumbing: transport wire types, the two
      runtime readers, and the runtime state interfaces. No new `GameRuntimePort`
      method, no new SignalR subscription, no transport/boundary change.
    - BattleScene renders a player-facing HUD from the delivered facts plus the
      already-delivered Boss HP and the already-delivered `playerState.combo`/
      `matchCount`/passive progress/status effects.
    - Boss presentation resolves the display name and element label from the
      existing single static transcription of BOSS_RULES.md §6.4 (`MVP_BOSSES`,
      LobbyScene.ts:79-85). No second catalog, no display string on the wire, no
      Element-matchup computation.
    - Boundary the client must not cross: no affordability/legality/cost
      derivation from `power` (§C).

Backend impact:
    - `BattleHub`: three members on `PetStatePayload`, one on `BossStatePayload`,
      populated in `ToPetStatePayload` / `ToPayload` as pure field mappings.
      Because `GetBattleState` uses the same projection, §7 parity is automatic.
    - No change to `PetState`, `BossState`, `BattleStateService`,
      `ResourceGenerator`, `DamagePipeline`, `CardCastExecutor`, the Redis
      serializer, the database, or any gameplay rule. No new dependency,
      abstraction, or layer (ARCHITECTURE.md §5).
    - Existing contract-pinning API tests must be updated to the amended member
      sets (they currently assert the members' absence — §L).

TASK-209 dependency:
    TASK-209 (projection + player-facing battle HUD) may implement ONLY after
    the §K amendment is authored in docs/ by the documentation-amendment
    deliverable. Its envelope, permitted scope, prohibited scope, required test
    updates, and binary acceptance criteria are recorded in §L so it cannot
    reopen this contract discussion.
```

**Marked NOT CURRENTLY AUTHORITATIVE (no fabrication; each is a separate
follow-up, not a projection field):**

```text
Pet Tier / Star / Level      NOT CURRENTLY AUTHORITATIVE as PetState members —
                             not implemented in PetState, and their intended
                             gameplay meaning is itself in conflict (§J-2).
                             Do not expose. Decision D-208-05; recommend
                             TASK-215.
Boss display name            NOT A STATE MEMBER, and BOSS_RULES.md §6.4 forbids
                             it as an identifier. Resolved client-side from the
                             existing catalog. Do not expose.
Boss portrait / asset key    DOES NOT EXIST in any state, catalog, or asset
                             set. Would be new content/assets —
                             out of scope. Do not expose.
Boss Element in battle       Authoritative in BossState but deliberately not
                             required: the existing static catalog already
                             carries the same content for the delivered
                             identity, and delivering it would add a second
                             spelling of definition content. Do not expose
                             (see §D, §M).
Pet Power maximum / gauge    DOES NOT EXIST as state: the 0–100 range is a
                             documented invariant, not a member. Do not add a
                             `maxPower` field to satisfy a gauge.
```

---

## J. Product-Owner Decision Record

### D-208-01 — Pet HP Projection

```text
Decision:
    APPROVED — the state projection carries the active Pet's current HP and
    Max HP.

    Fields: `PetState.HP` and `PetState.MaxHP` (GAME_STATE.md §2.3), both
    delivered under the existing `petState` object on `BattleStateUpdated`.

    Both members are always present and non-nullable, following the
    always-present convention §4.2 item 3 / §4.3 item 4 / §4.4 item 4 state for
    their own members. Zero is a real published value for HP (a lost battle's
    terminal Pet HP), exactly as zero is for the Boss's `hp` (§4.4 item 4):
    neither member is omitted and neither is `null`. `MaxHP` is delivered
    alongside `hp` and is never derived from it, mirroring §4.4 item 5.

    Source: the authoritative PetState. The projection maps the two fields
    one-to-one and computes, clamps, scales, and rounds nothing.

    Client boundary: the client renders the two numbers it was sent. It does
    not damage, heal, clamp, infer one from the other, reconstruct either from
    DamageDealt/DamageTaken (which carry only `amount`), expect any heal event
    (none exists), or re-derive either from `finalPlayerHp`
    (GAME_RULES.md §18, ADR-001, AGENTS.md §10).

    Rationale: GDD.md line 86 makes Pet HP the battle-ending value, GDD.md §11
    names HP/Max HP among the combat stats, GDD.md §17 requires the player to
    understand what is happening, and MVP_SCOPE.md §1 lists HP as MVP IN — while
    §4.3 item 2 currently withholds the state's own members and no event can
    carry them.
```

### D-208-02 — Power Projection

```text
Decision:
    EXPOSE THE EXISTING AUTHORITATIVE POWER — do not defer, and do not open a
    gameplay decision. Power is already authoritative, persistent battle state
    (PetState.Power); only its delivery was unauthorized.

    Field: `PetState.Power` (GAME_STATE.md §2.3), delivered under the existing
    `petState` object on `BattleStateUpdated`. Always present, non-nullable;
    `power = 0` is what a battle begins with and is sent as `0`.

    No `maxPower`/`max` member is added: no such state member exists, and the
    0–100 range is a documented invariant of GAME_RULES.md §12 /
    COMBAT_RULES.md §1.1. A gauge scale, if the HUD uses one, is static
    presentation of that documented invariant — not per-battle state and not a
    new wire member. (`GAME_RULES.md` §12's phrase "the configured maximum"
    denotes that same configured invariant; if a per-battle maximum ever became
    state, exposing it would be a separate protocol decision, and inventing a
    `maxPower` member now to satisfy a gauge is explicitly not authorized.)

    Not required for this decision (and therefore NOT authorized here): card
    cost display and affordability. Card cost is composed server-side
    (CARD_RULES.md §3.6) from `CardDefinition.PowerCost` and the undelivered
    `CardCostModifiers[]`; §4 item 15 forbids the client to reconstruct a
    cost modifier, and `API_CONTRACTS.md` §5.3 excludes `powerCost` from the
    read contract. That is a separate read-contract decision (recommend
    TASK-212).

    Client boundary: the client renders the delivered value. It does not
    compute a cost, a remaining resource after a cast, an affordability state,
    or a legality judgment, and it does not reconstruct Power from
    `PowerChanged` (which is transient, per-mutation, and lost on resync —
    §7 item 2). The server remains the only validator.

    Rationale: GDD.md §9 makes spending-or-saving Power the battle's central
    decision and MVP_SCOPE.md §1 lists Power as MVP IN. Withholding it leaves
    the decision unrepresentable, and the only current carrier is an event that
    a resync cannot recover.
```

### D-208-03 — Boss Presentation Identity

```text
Decision:
    APPROVED — the minimum Boss presentation contract is ONE fact: the Boss's
    canonical technical Identity, read from `BossState.BossId` (GAME_STATE.md
    §2.4; BOSS_RULES.md §6.4), delivered under the existing `bossState` object
    on `BattleStateUpdated`.

    Always present, non-nullable, never a display name, and never
    `BossDefinitionId` (the three are never collapsed — BOSS_RULES.md §6.4).
    The projection reads the identity and maps it one-to-one.

    Display name and Element label are resolved by the client from the EXISTING
    single transcription of BOSS_RULES.md §6.4/§6.1 that it already holds
    (LobbyScene.ts `MVP_BOSSES`). No display string, localization text,
    portrait, asset key, boss type, Element, ATK/DEF, State, Passive, Skill
    charge/cooldown, or Boss StatusEffects is added to the wire. TASK-209 must
    not create a second catalog, and must not use the resolved element label for
    matchup, damage, or any gameplay logic (ELEMENT_RULES.md / GAME_RULES.md
    §18; the server resolves matchups).

    Rationale: (i) the value already reaches the client on
    BossSkillCast.sourceId and PassiveCharged/PassiveTriggered.sourceId
    (§3.2.16–§3.2.18), so this discloses nothing new — it moves an
    already-delivered fact onto the path that survives a resync, exactly as
    §4.3 item 3 delivers `petState.passiveId` as the identity the events already
    report; (ii) GDD.md §12/§17 require the Boss to be identifiable and its
    danger readable; (iii) the client has no reconnect-safe alternative
    (BattleScene receives no scene data; the preserved loadout is in-memory
    only), and using client memory would be a second representation of a fact
    the state owns and could silently mislabel the boss.

    If the Product Owner declines this field, TASK-209 still ships the HP/Power
    HUD and the raw-ID boss line stays as it is; the decision must then be
    recorded as an explicit non-delivery in §4.4 (not left implicit).
```

### D-208-04 — Contract Amendment

```text
Decision:
    EXTEND THE EXISTING PROJECTION. Option A.

    The four facts are delivered as members of the two projections that already
    exist (`petState`, `bossState`), on the carrier that already exists
    (`BattleStateUpdated`, §4 item 11), on the triggers that already exist
    (group join; every committed Swap's resolved-state push), and on the
    reconnect/resync path that already exists (§7), with no new top-level
    member, no second state object, no new method, event, or subscription, and
    no new store, key, or column.

    Option B (a separate `battlePresentationState` object) is REJECTED: it
    duplicates ownership of facts that already have documented homes, forces a
    second member set and §7 parity statement, and would require an ADR for work
    the documented mechanism already covers. Option C (event-only) is REJECTED:
    events are not state, a resync discards and does not replay them, healing
    emits no event at all, and client-side reconstruction is forbidden.

    NOT REQUIRED: any ADR (TASK-161's classification of the TASK-160 widening is
    the direct precedent, and the protocol's own revision history treats a
    projection change as a protocol revision). REQUIRED: a
    SIGNALR_PROTOCOL.md version bump/amendment, the GAME_STATE.md delivery-split
    reword, and the BOSS_RULES.md §6.2.6 visibility reword — all specified in
    §K, all landing before TASK-209's code (AGENTS.md §7, §17).
```

### D-208-05 — GDD / Pet Progression Conflict

```text
Decision:
    SEPARATE PRODUCT DECISION REQUIRED. Recorded, not resolved, and no rule is
    changed.

    The conflict (§J-2): PET_RULES.md §5.7 item 2 ("Level scales base stats
    (HP/ATK/DEF) via a stat curve") and §6 ("Final Stat = f(Base Stat[Tier],
    Level Curve[Level], Star Bonus[Star])") versus TASK-200 Q-6 ("COSMETIC FOR
    MVP … without combat stat scaling") and the implementation (fixed-stat
    PetState with no Tier/Star/Level members), with MVP_SCOPE.md §1 line 53 and
    ROADMAP.md still promising "Tier, Star, Level progression" and GDD.md §14
    naming no owner for one. Under AGENTS.md §2 a specific domain rule outranks
    a task record, so the implementation and the Q-6 ledger are in tension with
    PET_RULES.md.

    Why it is not decided here: resolving it either narrows a domain rule or
    authorizes a stat curve — a gameplay/progression decision owned by the
    Product Owner and a gameplay-change workflow, not a projection task
    (AGENTS.md §7, §4, §16).

    Effect on this contract: none. Tier/Star/Level are not PetState members and
    are NOT exposed; the HUD requires none of them. Recommended follow-up:
    TASK-215 (narrow MVP_SCOPE.md §1/ROADMAP.md to "Level progression", or
    authorize the curve and the Tier/Star axes — then propagate through the
    owning domain doc and the implementation under their own tasks).
```

---

## K. Amendment Scope — the exact documentation deliverable (owner: not this task)

The authoritative edit is its own gated deliverable (`AGENTS.md` §7, §17;
TASK-160 → TASK-161 precedent). Recommend a dedicated documentation task
(**TASK-208A**, DOCUMENTATION type, Review Agent) OR TASK-209 step 1; either is
acceptable — the ordering requirement is that the contract lands in `docs/`
before any code implements it.

```text
1. docs/02-technical/SIGNALR_PROTOCOL.md  (the projection owner)
   a. Version header 2.16 → 2.17 with a revision-log entry recording the
      Product-Owner decision (this record, D-208-01/02/03/04) and stating
      explicitly what does NOT change: the method list, the state-push method,
      the delivery triggers, the storage contract, the event schema, and every
      other excluded member.
   b. §4 — one new item (the next free number, structurally mirroring item 17)
      recording that the active Pet's combat-stat subset is delivered: HP, MaxHP
      and Power, under the existing `petState`, per the D-208-01/D-208-02
      decisions; narrowed to those three members; nothing else from §2.3.
   c. §4.3 — the member tree gains the three members, and item 2's enumerated
      member set is restated from five to eight members; item 2's exclusion
      sentence must drop HP/MaxHP/Power from its list while keeping every other
      named member excluded. A new §4.3 item authors each member: source state
      field (PetState.HP / MaxHP / Power), wire name (camelCase per §3.2.3 item
      1, matching the serialized Redis record's own names), integer type,
      always-present/non-nullable (zero sent as zero), no unit/scale/clamp/
      rounding, no re-derivation of one member from another, no second spelling
      anywhere else in the payload, and the client boundary (renders and
      computes none of it; no affordability, cost, or legality derivation).
   d. §4.4 — item 2's "exactly two members" restated to three; item 3's
      exclusion list must drop `BossId`/Identity while keeping Element, ATK,
      DEF, State, PassiveId, PassiveProgress, SkillCharge, SkillCooldown and
      StatusEffects[] excluded; a new item authors the identity: source
      `BossState.BossId` (BOSS_RULES.md §6.4), string, canonical technical
      Identity only (never a display name, never BossDefinitionId),
      always-present, read-as-sent, client resolves presentation from its own
      catalog and computes nothing.
   e. §7 — the confirmation sentence must name the widened member sets so the
      snapshot and the push remain member-for-member identical (no §7 member set
      of its own).
   f. §4 item 15 — CORRECT the currently false claim that "the authoritative
      record of the resulting `Power` is the state push and `PowerChanged`".
      After this amendment the state push does carry Power; the sentence must be
      made accurate (and must not be read as authorizing any Card-cost member,
      which remains undelivered).
2. docs/02-technical/GAME_STATE.md
   a. §2.3's delivery-split paragraph ("The combat stats are state, and they are
      not delivered on the wire") reworded to name the delivered subset;
      its closing "Delivering them is a protocol change owned by its own task"
      sentence must record that this WAS that task, not silently disappear.
   b. §2.4's "Two BossState members are client-visible, and no others are"
      paragraph reworded for three members, and its "Delivering anything further
      from this tree is a protocol change owned by its own task" sentence
      likewise discharged.
   c. NO state-shape change: no member is added to or removed from PetState or
      BossState, and no new state member is invented (there is no `MaxPower`).
3. docs/01-game-design/BOSS_RULES.md §6.2.6
   The visibility constraint reworded: the Boss's canonical Identity and live
   HP/MaxHP are client-visible; the rest of BossState remains server-side.
   Gameplay-visibility wording only — no stat, effect, threshold, magnitude,
   skill, passive, or content value changes. (This is why the decision is a
   Product-Owner decision and not an agent-derivable edit: a domain rule
   document owns the constraint.)
4. docs/03-decisions — NO change. No ADR is created or amended and the ADR index
   is untouched (reasoning in §F item 4).
5. NO change to: docs/02-technical/API_CONTRACTS.md (initialState already
   carries the facts; §4/§4.5 remain identity-free), DATABASE.md, REDIS_STATE.md,
   GAME_EVENTS.md, PASSIVE_RULES.md, COMBAT_RULES.md, CARD_RULES.md,
   PET_RULES.md, GDD.md, MVP_SCOPE.md, ROADMAP.md, ARCHITECTURE.md (except,
   optionally and separately, its stale "No gameplay HUD exists yet" line —
   TASK-207 §J-4 / TASK-217, not this decision).
6. Recording rule: `tasks/README.md` §9 forbids copying wire shapes into task
   files. The amendment task must AUTHOR the member wording in
   SIGNALR_PROTOCOL.md §4.3/§4.4 (the canonical owner) and may then cite it; the
   field list in §I of this record is the DECISION content, not a substitute for
   the authored contract.
```

---

## L. TASK-209 Implementation Envelope (so the contract cannot be reopened)

**May do**

```text
1. Add the three Pet members and the one Boss member to the hub's projection:
   BattleHub.PetStatePayload / BossStatePayload and the mapping in
   ToPetStatePayload / ToPayload — pure field mappings. Verify GetBattleState
   parity (same projection function) with a test.
2. Extend the client synchronized copy: the transport wire types
   (SignalRService.ts), the runtime state interfaces (GameRuntimeEvents.ts
   RuntimePetState / RuntimeBossState), and the two runtime readers
   (GameRuntime.readPetState / readBossState), keeping the required-member
   strictness the contract's always-present rule implies.
3. Render a player-facing battle HUD in BattleScene from the currently
   delivered-authoritative values only: the Pet's HP/MaxHP (bar and/or numbers),
   Power, the Boss's identity (name resolved from the existing catalog) and the
   already-delivered Boss HP, plus the already-delivered combo/matchCount,
   passive progress, and status effects. Replace or suppress the diagnostic
   readout as far as this task's scope allows.
4. Update the contract-pinning tests to the amended member sets (they must keep
   asserting an exact expected set — the new one), and add coverage that the new
   members are delivered, survive the §7 snapshot, and are not derived
   client-side.
5. Keep every existing boundary: no client-side cost/affordability/legality
   computation, no Element-matchup logic, no HP or Power derivation, no new
   SignalR method/subscription, no second boss catalog.
```

**Must NOT do**

```text
- Change any gameplay rule, formula, magnitude, threshold, or content value.
- Modify Power generation/consumption, damage/healing rules, or Pet progression.
- Modify Boss mechanics, or expose any Boss member other than the identity
  (and hp/maxHp, already delivered).
- Add a new SignalR method, event, subscription, payload member, store, key, or
  column; or introduce a presentation-state object.
- Expose PetId, Pet Element, Tier/Star/Level, ATK/DEF/Crit, EquippedRelics, the
  modifier collections, a Boss display name/Element/portrait, or a maxPower.
- Begin before the §K amendment is authored in docs/ (AGENTS.md §7, §17).
```

**Tests that currently pin the pre-amendment contract (verified locations; they
must be updated, not weakened)**

```text
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
  :1031 BattleStateUpdated_PetState_ShouldCarryNoMemberOutsideTheDocumentedWireMembers
        — its `permitted` list, its exact-member-set assertion, and its
          Domain-only blocklist (which currently names "hp","maxHp","power")
  :1123 BattleStateUpdated_BossState_ShouldCarryExactlyTheDocumentedBossHpProjection
        — member set + blocklist (currently excludes "bossId")
  :1312 BattleStateUpdated_ShouldExcludeBothBattleIdentitiesFromTheWire
        — its forbidden list contains "bossId"; it must be re-scoped to the two
          battle identities it was written to guard (PlayerId / PetId), with its
          §4.4 rationale updated. It must keep asserting that PlayerId and the
          Pet instance identity are absent, and that the payload is still
          exactly the §4 envelope.
  :632  BattleStateUpdated_ShouldCarryNoGameplaySystemField
        — its later-stage blocklist contains "power"
  :400  BattleStateUpdated_ShouldCarryExactlyTheDocumentedBoardFoundationFields
        — envelope assertions (expected to remain valid; verify)
tests/backend/GameServer.Api.Tests/Hubs/PetStateWireProjectionTests.cs
  — constructor arity / serialization assertions for PetStatePayload
tests/backend/GameServer.Api.Tests/Hubs/BattleHubReconnectRecoveryTests.cs:292
  — GetBattleState projection shape (must remain the same shape as the push)
client: the GameRuntime / BattleScene / SceneLifecycle test fixtures that build
  state payloads must gain the new members.
```

**Binary acceptance criteria for TASK-209**

```text
[ ] The join push, every resolved-Swap push, and the GetBattleState snapshot all
    carry the four authorized members, with identical member sets on both paths.
[ ] Each delivered value equals the authoritative state value it is read from,
    with no transformation (assert against the Domain state).
[ ] The members are always present, never null, and zero is sent as zero
    (a battle with power = 0 and a terminal hp = 0 case).
[ ] No member outside the approved sets appears anywhere in the payload, at any
    depth.
[ ] The client renders the four values without deriving any of them; a grep of
    the client battle presentation finds no arithmetic that produces HP, MaxHP,
    Power, or a cost/affordability/legality judgment.
[ ] No new SignalR method, event, subscription, store, key, column, or
    presentation-state object exists.
[ ] The amended exact-member-set tests pass; the PlayerId/PetId identity
    exclusions still pass.
[ ] No gameplay, balance, content, damage, healing, Power-generation, or
    progression file changed.
```

---

## M. Remaining Product Decisions (reported, not decided here)

```text
1. The docs amendment (§K) — prerequisite to TASK-209. Recommend TASK-208A
   (DOCUMENTATION) or TASK-209 step 1.
2. Pet Tier/Star/Level gameplay meaning — PET_RULES.md §5.7/§6 vs TASK-200 Q-6
   vs MVP_SCOPE.md §1 line 53 / ROADMAP.md vs the implementation (D-208-05).
   Recommend TASK-215. Nothing in TASK-208 depends on it.
3. Card cost / affordability presentation — needs a read-contract decision
   (API_CONTRACTS.md §5.3 excludes `powerCost`; CardCostModifiers[] stays
   undelivered and §4 item 15 forbids reconstructing it). Audit G2/U4;
   recommend TASK-212. Delivering `power` does NOT resolve it.
4. Boss telegraph / passive-skill / element / state presentation beyond the
   identity — NOT authorized by this decision. `bossState` remains identity +
   hp/maxHp; a telegraph would need its own contract decision.
5. Player-XP persistence ordering (§J-3) — verification required before it is
   called a defect. Recommend TASK-216.
6. Boss portraits / asset keys — no asset exists; adding them is new content
   (out of scope for this decision and for MVP scope as currently written).
7. `SIGNALR_PROTOCOL.md` §4 item 15's incorrect "the state push" claim (§C) —
   corrected as part of §K item 1f, not by a separate task.
8. Documentation drift unrelated to this decision (ARCHITECTURE.md's "No
   gameplay HUD exists yet", the several stale cross-references TASK-207 §J-4
   lists) — TASK-217. Reported, not touched.
```

---

## N. Stop-Condition Check and Validation

**Stop conditions (task §Stop Conditions)**

```text
1. No authoritative source exists for a requested HUD value.
   FIRED ONLY for fields that DO NOT EXIST — Pet Tier/Star/Level (not PetState
   members), Boss display name (not a state member; BOSS_RULES §6.4 forbids it
   as an identifier), Boss portrait/asset key (no source anywhere), Power max
   (no member; the range is an invariant). Each is marked NOT CURRENTLY
   AUTHORITATIVE / NOT REQUIRED and excluded from the projection rather than
   fabricated. NOT fired for HP, MaxHP, Power or BossId: each traces to a named
   authoritative state member.

2. GDD and implementation materially disagree about gameplay meaning.
   FIRED for the Pet progression question (PET_RULES §5.7/§6 vs TASK-200 Q-6 vs
   the implementation) — recorded as §J-2 / D-208-05 with the exact decision the
   Product Owner must make, and NOT resolved, NOT implemented. It does not block
   this contract: none of the four authorized facts depends on it. NOT fired for
   HP/Power/Boss identity.

3. Projection changes would require a new gameplay mechanic rather than exposing
   existing state. NOT FIRED. Every authorized fact is existing authoritative
   state; no rule, formula, value, or mechanic changes.

4. Existing protocol compatibility cannot be established. NOT FIRED — §F.

5. The minimum contract cannot be determined confidently. NOT FIRED — §A/§E/§I.

Consequence: TASK-208 resolves the projection contract and STOPS without
implementation, per its non-goals. The one genuinely open item (2) is reported
for a separate Product-Owner decision, exactly as AGENTS.md §4/§7 require.
```

**Validation checklist (task §Validation)**

```text
[x] 1. Re-read the relevant protocol sections: §4 (items 4, 6, 11, 13, 14, 15,
       16, 17), §4.2, §4.3, §4.4, §7 — and the GAME_STATE §2.3/§2.4 owning
       contracts, before recording the decision.
[x] 2. Every proposed field verified against its backend authoritative source:
       PetState.HP/MaxHP/Power (PetState.cs + GAME_STATE §2.3 + the write sites
       in ResourceGenerator/CardCastExecutor/RelicResolver/BattleStateService),
       BossState.BossId (BossState.cs + GAME_STATE §2.4 + BOSS_RULES §6.4).
[x] 3. Survival across reconnect/resync verified: both the §4 push and the §7
       snapshot are produced by the one BattleHub.ToPayload(BattleState), and the
       values round-trip through the Redis record (BattleStateJson /
       BattleStateSerializer). Connect-time recovery cannot report a different
       member set from the push.
[x] 4. No client-side derivation required: the values are read as sent; no
       event exists that carries them (DamageDealt/DamageTaken carry only
       `amount`; healing emits no event), and §7 forbids event replay.
[x] 5. The proposal is smaller than a new presentation architecture: members on
       two existing projections, via the existing method, trigger, and snapshot
       — versus Option B's new object, second member set, §7 parity statement,
       and ADR.
[x] 6. TASK-209 can implement the HUD using only the approved projection: its
       envelope, boundaries, required test updates, and acceptance criteria are
       recorded in §L, and no gap in it requires a further contract member.
       (One deliberate non-member remains: card cost — a separate decision, §M
       item 3 — and TASK-209 is required not to work around it client-side.)
[ ] Production tests added: NOT APPLICABLE — no production code changed. The
    tests that must change when the contract is implemented are listed in §L.
[x] 6b. No production code, test, asset, migration, ADR, or docs/ file was
       modified by this task; the only file created is this record.
```

---

## O. Final Report

```text
TASK-208 CONTRACT DECISION COMPLETE

Decision:
    Extend the two existing narrowed projections on the existing
    `BattleStateUpdated` carrier (Option A). Add the active Pet's current HP,
    MaxHP and Power to `petState`, and the Boss's canonical technical Identity
    to `bossState`. No new top-level member, object, method, event,
    subscription, store, key, column, or ADR. The authoritative wording is
    authored by the §K documentation amendment, which must land before
    TASK-209's code (AGENTS.md §7, §17; TASK-160 → TASK-161 precedent).

Approved Projection:
    `BattleStateUpdated`: `petState` gains three members read one-to-one from
    PetState; `bossState` gains one member read one-to-one from BossState. All
    four are always present, non-nullable, never omitted, never null, with zero
    sent as zero (the always-present convention of SIGNALR_PROTOCOL.md §4.2
    item 3 / §4.3 item 4 / §4.4 item 4). The same members are carried by the §7
    reconnect snapshot, because both paths use the one projection function.
    Nothing else in the payload changes.

Pet State:
    + current HP   ← PetState.HP      (GAME_STATE.md §2.3) — authoritative,
                                        implemented, Redis round-tripped
    + Max HP       ← PetState.MaxHP   (GAME_STATE.md §2.3) — same
    + Power        ← PetState.Power   (GAME_STATE.md §2.3, 0–100 invariant
                                        GAME_RULES.md §12 / COMBAT_RULES.md
                                        §1.1) — authoritative, persistent,
                                        Redis round-tripped
    NOT added: PetId, Element, Tier/Star/Level (not PetState members), ATK, DEF,
    Crit, EquippedRelics[], NextAttackCritModifiers[], CardCostModifiers[],
    ATKModifiers[], BurnDamageModifiers[], maxPower (does not exist).

Boss State:
    hp, maxHp    already delivered (§4.4) — unchanged
    + bossId     ← BossState.BossId (GAME_STATE.md §2.4, BOSS_RULES.md §6.4) —
                   the canonical technical Identity, the same value the client
                   already receives on BossSkillCast.sourceId and
                   PassiveCharged/PassiveTriggered.sourceId (§3.2.16–§3.2.18).
                   Display name and Element label are resolved from the client's
                   existing single catalog transcription; no display string,
                   portrait, asset key, or Element goes on the wire.

Power:
    ALREADY AUTHORITATIVE — so it is exposed, not deferred, and no gameplay
    decision is required. PetState.Power is the single authoritative resource
    with a single generation write site (ResourceGenerator.ApplyPower, clamped
    0–100), server-side consumption (CardCastExecutor effective cost; Boss
    PowerDrain), one post-resolution write-back, and a Redis round trip. Its only
    current client carrier is the transient PowerChanged event, which a resync
    discards without replay (§7 item 2) — so the state projection is the only
    reconstruction-safe carrier. Its exposure does NOT authorize client-side
    cost, affordability, or legality derivation (CardCostModifiers[] is not
    delivered and §4 item 15 forbids reconstructing a cost); card-cost display is
    a separate read-contract gap (API_CONTRACTS.md §5.3). Also recorded: §4 item
    15 currently claims the state push already carries Power, which is false
    today and is corrected by the amendment.

GDD / Progression Conflicts:
    §J-1 RESOLVED — GDD.md line 86 (Pet HP ends the battle), §9 (Power is the
    spend-or-save resource), §11 (HP/Max HP/Power are combat stats) and §17
    (comprehension), with MVP_SCOPE.md §1 listing HP and Power as MVP IN, are
    exactly what the four-field widening satisfies. GDD §17's remaining clauses
    (Card/Relic comprehension, Boss telegraph) are separate gaps, not part of
    this contract.
    §J-2 RECORDED, NOT RESOLVED (D-208-05) — PET_RULES.md §5.7 item 2 / §6
    (Level/Tier/Star scale stats) versus TASK-200 Q-6 (cosmetic for MVP) and the
    fixed-stat implementation, with MVP_SCOPE.md §1 line 53 / ROADMAP.md still
    promising "Tier, Star, Level progression" and GDD.md §14 naming no owner.
    Under AGENTS.md §2 a specific domain rule outranks a task record, so this is
    a real conflict that only the Product Owner may settle; no rule was changed.
    Tier/Star/Level are NOT PetState members and are NOT exposed. Recommend
    TASK-215.
    §J-3 VERIFY, DO NOT FIX — the Player-XP write can be skipped by
    PlayerRepository's `Unchanged`/`Local` early return and relies on
    PetRepository's later SaveChangesAsync to flush; if the Pet row is absent,
    the grant would be dropped while the persisted RewardSummary reports it.
    Real enough to require verification, unproven without a composed persistence
    test. Recommend TASK-216. Not touched here.

Protocol / ADR Changes:
    REQUIRED: SIGNALR_PROTOCOL.md version bump + revision entry, §4 (new item),
    §4.3 (member set + new item), §4.4 (member set + new item), §7 parity
    sentence, and §4 item 15's false "state push" claim corrected;
    GAME_STATE.md §2.3/§2.4 delivery-split reword; BOSS_RULES.md §6.2.6
    visibility reword (wording only — no gameplay value changes).
    NOT REQUIRED: any ADR (TASK-161 classified the TASK-160 widening as
    documentation, not architecture; the protocol treats a projection change as
    a protocol revision). NOT CHANGED: API_CONTRACTS.md, DATABASE.md,
    REDIS_STATE.md, GAME_EVENTS.md, ARCHITECTURE.md, GDD.md, MVP_SCOPE.md,
    ROADMAP.md, PET_RULES.md, COMBAT_RULES.md, CARD_RULES.md, PASSIVE_RULES.md,
    docs/03-decisions/.
    The amendment must land before implementation.

TASK-209 Can Now Implement:
    The projection widening and the player-facing battle HUD, entirely from the
    approved facts, with no contract question left open. Envelope in §L:
    (1) extend BattleHub's PetStatePayload/BossStatePayload and
    ToPetStatePayload/ToPayload as pure field mappings; (2) extend the client
    wire types and the two runtime readers; (3) render the HUD (Pet HP bar/
    numbers, Power, Boss identity + existing Boss HP, plus the already-delivered
    combo/matchCount, passive progress and status effects); (4) update the
    contract-pinning tests listed in §L to the amended exact member sets;
    (5) add delivery + §7-parity + no-derivation coverage. Prohibited: any
    gameplay/balance/content change, any new method/event/subscription/store/
    key/column, any additional exposed member, any client-side HP/Power/cost/
    affordability/matchup computation, and any second boss catalog.

Remaining Product Decisions:
    1. Author the §K amendment (recommend TASK-208A) — gates TASK-209.
    2. Pet Tier/Star/Level gameplay meaning (D-208-05; TASK-215).
    3. Card cost / affordability read contract (audit G2/U4; TASK-212) —
       deliberately NOT authorized by exposing Power.
    4. Boss telegraph / Element / State / passive-skill presentation beyond the
       identity — needs its own decision; not authorized here.
    5. Player-XP persistence ordering verification (J-3; TASK-216).
    6. Boss portraits/asset keys — no asset exists; new content, out of scope.
    7. Unrelated documentation drift (TASK-217).
```
