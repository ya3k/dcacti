/**
 * The client-side battle read/write models — the wire shapes of the three
 * battle endpoints (`API_CONTRACTS.md` §3, §4, §4.5).
 *
 * ```text
 * POST /api/battle/start              → BattleStartResponse
 * GET  /api/battle/{battleId}/result  → BattleResultResponse
 * GET  /api/battle/history            → BattleHistoryItemResponse[]
 * ```
 *
 * These are **transport shapes only**. Every value in a response is authored by
 * the server (`GAME_RULES.md` §18, `ADR-001`); the client reads what it was
 * sent and never derives, defaults, clamps, recomputes, or mutates a member.
 * Nothing here is cached as authoritative, and nothing here is gameplay state
 * the client owns — `BattleState` lives in the server's active-state record
 * (`GAME_STATE.md` §2, `REDIS_STATE.md` §1), and a `BattleStartResponse` is the
 * server's summary of it.
 *
 * The member lists are **binding**: `API_CONTRACTS.md` §3 fixes
 * `initialState` as the summary of `GAME_STATE.md` §2's `BattleState`, so the
 * nested types below mirror that document's members rather than a subset the
 * client happens to read today. A member the contract does not define must not
 * be added here, and a documented one must not be dropped merely because the
 * client has no consumer for it yet.
 */

import type { Element } from './CollectionModels';

/**
 * Re-exported so the Element wire value set has one definition on the client.
 * `API_CONTRACTS.md` §5.1 owns the set — `"Fire" | "Water" | "Earth" | "Wood" |
 * "Metal"` — and §3 binds `initialState.petState.element` and
 * `initialState.bossState.element` to it by reference, so the battle-start
 * response carries the same English values as the §5 collection responses. The
 * Vietnamese design names (`Mộc`, `Hỏa`, `Thổ`, `Kim`, `Thủy` —
 * `ELEMENT_RULES.md` §1) are display text and are never wire values.
 */
export type { Element };

/**
 * The `POST /api/battle/start` request body (`API_CONTRACTS.md` §3).
 *
 * ```json
 * {
 *   "petId": "pet-001",
 *   "bossId": "boss-hoa-long",
 *   "cardLoadout": ["heal", "shield", "power_charge"],
 *   "relicLoadout": ["relic_id_1", "relic_id_2", "relic_id_3"]
 * }
 * ```
 *
 * **Exactly the four documented members, and they are a selection.**
 *
 * ```text
 * petId          the owned Pet instance (Pet.PetInstanceId) — §5.1's `petId`
 * bossId         the Boss's canonical technical Identity, e.g. "boss-hoa-long"
 *                (BOSS_RULES.md §6/§6.4) — never a display name
 * cardLoadout    exactly 3 Basic Card CardDefinitionIds (CARD_RULES.md §1);
 *                the Signature Skill Card is derived by the server, not sent
 * relicLoadout   3–5 owned RelicInstanceIds (RELIC_RULES.md §2), ordered —
 *                position i is equip slot i + 1 (§2.3)
 * ```
 *
 * No member here identifies the caller: the requesting Player is resolved
 * server-side from the session's `player_id` claim (§2.3 "Identity", ADR-015
 * D3), so there is deliberately no `playerId`, no `discordUserId`, and no
 * `sessionToken` — §4 note 7 forbids any request member from selecting,
 * overriding, or standing in for the caller's identity.
 *
 * No member here is server-owned battle state either: there is no `battleId`,
 * `turn`, `sequence`, `rngSeed`, HP, or Power. The server authors the whole
 * resulting `BattleState` (`GAME_RULES.md` §18, ADR-001), and the four members
 * above are the documented complete request surface (`API_CONTRACTS.md` §1:
 * "No endpoint accepts Damage, HP, Power, Match, or Combo values from the
 * client").
 *
 * The 3-relic/3-card lower bounds and 5-relic upper bound are stated because
 * they are the contract's own cardinalities, not because this type enforces
 * them: `cardLoadout` and `relicLoadout` are `string[]`, and count, ownership,
 * category, copy-limit, and duplicate rules are validated **server-side** (§3).
 * The client transports the selection; it never pre-validates it into a
 * locally-decided accept or reject.
 */
export interface BattleStartRequest {
  /**
   * The active Pet's owned instance identity (`Pet.PetInstanceId`, §5.1) — the
   * same value `POST /api/battle/start` receives as `petId` and
   * `GAME_STATE.md` §2.3 records as `PetState.PetId`. It is not a Pet
   * definition id.
   */
  readonly petId: string;
  /**
   * The Boss's canonical technical Identity (`BOSS_RULES.md` §6.4, e.g.
   * `boss-hoa-long`) — not a display name and not a `BossDefinitionId`
   * (`GAME_STATE.md` §2.4). A value that is not a valid MVP Boss is rejected
   * server-side with `400 BOSS_NOT_FOUND`.
   */
  readonly bossId: string;
  /**
   * Exactly 3 Basic Card `CardDefinitionId` values (`CARD_RULES.md` §1). The
   * active Pet's Signature Skill Card is derived server-side and is never part
   * of this array (§3).
   */
  readonly cardLoadout: readonly string[];
  /**
   * 3–5 owned Relic **instance** identities (`RELIC_RULES.md` §2), whose array
   * order **is** the equip slot order: position *i* is slot *i + 1* (§2.3).
   * The server snapshots this order into `PetState.EquippedRelics[]`, so the
   * array is sent as selected and is never re-sorted or de-duplicated here.
   */
  readonly relicLoadout: readonly string[];
}

/**
 * The `POST /api/battle/start` success response (`API_CONTRACTS.md` §3).
 *
 * ```json
 * {
 *   "battleId": "string",
 *   "signalrHub": "string (hub URL/path)",
 *   "initialState": { "...": "BattleState summary, GAME_STATE.md §2" }
 * }
 * ```
 *
 * Exactly the three documented members. None of the created battle's values is
 * promoted to a top-level field: `turn`, `sequence`, `rngSeed`/`rngState`, the
 * board, the Match/Combo accounting, the Pet, and the Boss all travel inside
 * `initialState`, which is what §3 defines it to be.
 */
export interface BattleStartResponse {
  /**
   * The created battle's identity (`GAME_STATE.md` §2.0.1) — the id the client
   * joins the battle group with (`SIGNALR_PROTOCOL.md` §1 items 1–2) and the id
   * `getBattleResult` later reads.
   */
  readonly battleId: string;
  /**
   * The hub URL/path the client connects to (`SIGNALR_PROTOCOL.md` §1 item 2).
   * It is a transport address, not battle state: it selects no Pet, Boss, or
   * loadout, and this task consumes it as a value only — the SignalR connection
   * itself is a separate concern (`ARCHITECTURE.md` §2.2.1).
   */
  readonly signalrHub: string;
  /**
   * The summary of the created `BattleState` (`GAME_STATE.md` §2) — the whole
   * authoritative state at battle creation, which is the "at creation" values
   * §2.0.2 documents (`turn`/`sequence` = 0) and §6.5 item 4's publishable
   * `combo` = 0.
   */
  readonly initialState: BattleStartInitialState;
}

/**
 * The `initialState` summary of a created battle (`API_CONTRACTS.md` §3:
 * "BattleState summary, `GAME_STATE.md` §2").
 *
 * ```text
 * BattleStartInitialState
 * ├── battleId
 * ├── turn
 * ├── sequence
 * ├── rngSeed
 * ├── rngState: { state, increment }
 * ├── board:    { cells: [ { gemType, specialGem } ] }
 * ├── combo
 * ├── matchCount
 * ├── petState
 * └── bossState
 * ```
 *
 * **This mirrors the `BattleState` members §2 defines, not a convenient
 * subset.** It is not derived from the Domain classes — the REST wire contract
 * is what this type follows, and where the two could differ the contract wins
 * (`AGENTS.md` §2). That is why, for example, `playerId` is absent: it is a §2
 * member but explicitly **not** a wire member (`GAME_STATE.md` §2.8 item 3,
 * ADR-014 item 3).
 *
 * `turn` and `sequence` are `0` at creation and `combo`/`matchCount` are `0`
 * because no Match has occurred, but zero here is a **value, not an absence**:
 * every member is written, and none may be modelled as optional
 * (`GAME_STATE.md` §2.2.1 item 1, `MATCH3_RULES.md` §6.5 item 4).
 */
export interface BattleStartInitialState {
  /** The battle's identity (`GAME_STATE.md` §2.0.1) — equal to the outer `battleId`. */
  readonly battleId: string;
  /** The current Turn number — `0` at creation (`GAME_STATE.md` §2.0.2, `GAME_RULES.md` §2). */
  readonly turn: number;
  /** The monotonic resolution counter — `0` at creation (`GAME_STATE.md` §2.0.2, §5). */
  readonly sequence: number;
  /**
   * The battle's server-chosen PRNG seed (`GAME_STATE.md` §2.6.1) — an unsigned
   * 64-bit integer. The client never supplies, influences, or draws from it
   * (`TDD.md` §6 item 3, `AGENTS.md` §11).
   */
  readonly rngSeed: number;
  /**
   * The PRNG state after initial board generation (`GAME_STATE.md` §2.6.2) —
   * one logical field carried as a state + increment pair. The client never
   * advances it.
   */
  readonly rngState: BattleStartRngState;
  /** The authoritative board (`GAME_STATE.md` §2.1). */
  readonly board: BattleStartBoard;
  /**
   * The current Combo for the Swap that just resolved, reset to `0` when a new
   * Swap begins (`GAME_STATE.md` §2.2, `GAME_RULES.md` §5) — `0` at creation.
   */
  readonly combo: number;
  /** The cumulative Match count this battle (`GAME_STATE.md` §2.2) — `0` at creation. */
  readonly matchCount: number;
  /** The one active Pet for this battle (`GAME_STATE.md` §2.3, ADR-011). */
  readonly petState: BattleStartPetState;
  /** The Boss the battle is fought against (`GAME_STATE.md` §2.4). */
  readonly bossState: BattleStartBossState;
}

/**
 * The PRNG state summary (`GAME_STATE.md` §2.6.2).
 *
 * The two components are **one logical field** and are never split across
 * separate members (§2.6.2 item 1), so they stay together here.
 */
export interface BattleStartRngState {
  /** The PRNG's current internal state (`GAME_STATE.md` §2.6.2). */
  readonly state: number;
  /** The PRNG's stream selector (`GAME_STATE.md` §2.6.2). */
  readonly increment: number;
}

/**
 * The board summary (`GAME_STATE.md` §2.1) — `Cells[64]`, its only field.
 */
export interface BattleStartBoard {
  /**
   * Exactly 64 cell entries in ascending cell-index order, row-major, where
   * the array position **is** the cell index (`MATCH3_RULES.md` §1.0,
   * `GAME_STATE.md` §2.1.7 item 2). No index member is carried per element for
   * the same reason.
   */
  readonly cells: readonly BattleStartCell[];
}

/**
 * One board cell entry (`GAME_STATE.md` §2.1.1).
 *
 * A cell's `gemType` is **always** present, including under a Special Gem: a
 * Special Gem is metadata on the cell's Gem, not a substitute for it
 * (§2.1.3 item 1), and the Gem type is what Match Detection compares.
 */
export interface BattleStartCell {
  /**
   * The cell's Gem type as its contract name (`MATCH3_RULES.md` §1.1) — one of
   * the four functional types `ATK` | `DEF` | `HP` | `POWER`. Gems are
   * functional, not elemental: a Gem type is not an `Element` (§2.1.1 item 2).
   */
  readonly gemType: string;
  /**
   * The Special Gem occupying this cell, or `null`/absent for an ordinary Gem
   * (`GAME_STATE.md` §2.1.7 item 3). Absence is the representation of "no
   * Special Gem" — never a sentinel type, and never one of the three real
   * types standing in for it.
   *
   * It is typed as nullable rather than optional because §2.1.7 item 3 states
   * that omitting the member and writing an explicit `null` are the same
   * statement, so a reader must handle both spellings.
   */
  readonly specialGem?: BattleStartSpecialGem | null;
}

/**
 * A Special Gem's metadata (`GAME_STATE.md` §2.1.4).
 *
 * ```text
 * SpecialGem
 * ├── type            LineClear | Burst | Area   (MATCH3_RULES.md §5.2–§5.4)
 * └── orientation     Horizontal | Vertical       (Line Clear only)
 * ```
 *
 * `orientation` is present **if and only if** `type` is `LineClear`
 * (§2.1.4 item 2): `Burst` and `Area` are orientation-free, so an orientation
 * on those types would be a value no rule reads. Both members are typed
 * `string` rather than as locally re-listed unions for the same reason §5.1's
 * `tier` is — the permitted values are owned by `MATCH3_RULES.md` §5.2–§5.4,
 * and a second copy here could drift from it.
 */
export interface BattleStartSpecialGem {
  /** `LineClear`, `Burst`, or `Area` (`GAME_STATE.md` §2.1.4 item 1). */
  readonly type: string;
  /** `Horizontal` or `Vertical`, for a `LineClear` gem only (§2.1.4 item 2). */
  readonly orientation?: string | null;
}

/**
 * The created Pet state summary (`GAME_STATE.md` §2.3) — the active Pet is the
 * **combat character**, so the combat stats and the battle-scoped loadout live
 * here and not under a Player state (ADR-011).
 *
 * ```text
 * PetState
 * ├── PetId / Identity
 * ├── Element
 * ├── HP / MaxHP
 * ├── ATK / DEF / Crit
 * ├── Power
 * ├── EquippedCards[]
 * ├── EquippedRelics[]
 * └── PassiveId / PassiveProgress
 * ```
 *
 * **`StatusEffects[]` is deliberately not here.** It is a §2.3 member but is
 * **not yet implemented** (`GAME_STATE.md` §2.3: "The remaining collection
 * member `StatusEffects[]` is not yet implemented"), so it is not on this
 * wire, and modelling it would be inventing a payload member the contract does
 * not yet define. `GAME_STATE.md` §2.0.5.3 is explicit that this is a staging
 * position, not a scope reduction: the member arrives with its owning Combat
 * system and is added here then.
 *
 * The same document's §2.6.1-style "implemented so far" list confirms the
 * members below are the whole of what a created battle's `PetState` carries.
 */
export interface BattleStartPetState {
  /**
   * Current health — `MaxHP` at creation (`GAME_STATE.md` §2.3,
   * `COMBAT_RULES.md` §1.1).
   */
  readonly hp: number;
  /** Maximum health (`COMBAT_RULES.md` §1.1). */
  readonly maxHP: number;
  /** Attack power (`COMBAT_RULES.md` §1.1). */
  readonly atk: number;
  /** Defense (`COMBAT_RULES.md` §1.1). */
  readonly def: number;
  /**
   * Critical hit chance as a **percentage** (`COMBAT_RULES.md` §1.1) — the
   * value is `5`, not `0.05` (`GAME_STATE.md` §2.3).
   */
  readonly crit: number;
  /**
   * Card/Skill resource, 0–100 (`GAME_RULES.md` §12) — `0` at creation. Zero
   * is a real publishable value, not an absence (`GAME_STATE.md` §2.3).
   */
  readonly power: number;
  /**
   * The active Pet's one Element (`PET_RULES.md` §1), as the canonical REST
   * wire value bound by §3 → §5.1 — never a Vietnamese display name and never
   * a Domain enum member name.
   */
  readonly element: Element;
  /**
   * The active Pet's one Passive identity (`PASSIVE_RULES.md` §1). A Pet has
   * exactly one, so this is a value, not a selection among several, and there
   * is no collection or slot for it (`GAME_STATE.md` §2.3).
   */
  readonly passiveId: string;
  /**
   * The Passive's threshold (`PASSIVE_RULES.md` §1). The threshold is carried
   * as its own member: `PassiveProgress` is delivered here as the current
   * count beside it rather than as a nested progress object
   * (`GAME_STATE.md` §2.3's `PassiveProgress` — current count vs. threshold).
   */
  readonly passiveThreshold: number;
  /** Progress toward the threshold — `0` at creation (`PASSIVE_RULES.md` §2). */
  readonly passiveCurrent: number;
  /**
   * The battle-scoped Card snapshot: exactly 4 `CardDefinitionId` entries —
   * the 3 submitted Basics plus the active Pet's **derived** Signature Skill
   * Card (`CARD_RULES.md` §1, `GAME_STATE.md` §2.3). Elements are **definition**
   * identities, so a repeated `CardDefinitionId` is that same definition
   * repeated (up to its loadout copy limit), never a second instance. Element
   * order carries no gameplay significance.
   */
  readonly equippedCards: readonly string[];
  /**
   * The battle-scoped Relic snapshot: the validated 3–5 owned Relic
   * **instance** identities, in the submitted loadout order, which **is** the
   * equip slot order (`RELIC_RULES.md` §2.2–§2.5, `GAME_STATE.md` §2.3). The
   * order is preserved exactly as submitted and is never re-sorted here.
   */
  readonly equippedRelics: readonly string[];
}

/**
 * The created Boss state summary (`GAME_STATE.md` §2.4) — the Boss the battle
 * is fought against, at full health in its Initial State.
 *
 * ```text
 * BossState
 * ├── BossId
 * ├── Element
 * ├── HP / MaxHP / ATK / DEF
 * └── State
 * ```
 *
 * The Boss's Passive/Skill charging members (`PassiveId`, `PassiveProgress`,
 * `SkillCharge`, `SkillCooldown`) are §2.4 members but resolution state, and
 * starting a battle resolves no action — so they are not part of this summary
 * and are not modelled here. `StatusEffects[]` is likewise not yet implemented
 * (§2.4.1). This is the "at creation" projection §3 defines, not a reduced
 * `BossState`.
 *
 * `bossId` denotes the canonical technical Boss **Identity** (`boss-hoa-long`)
 * — the same value submitted as the request's `bossId` — not the display name
 * and not `BossDefinitionId` (`GAME_STATE.md` §2.4).
 */
export interface BattleStartBossState {
  /** The Boss's canonical technical Identity (`BOSS_RULES.md` §6.4, `GAME_STATE.md` §2.4). */
  readonly bossId: string;
  /**
   * The Boss's one Element (`BOSS_RULES.md` §6.1), as the canonical REST wire
   * value bound by §3 → §5.1.
   */
  readonly element: Element;
  /** Current health — `MaxHP` at creation (`BOSS_RULES.md` §6.1). */
  readonly hp: number;
  /** Maximum health (`BOSS_RULES.md` §6.1). */
  readonly maxHp: number;
  /** Attack power (`BOSS_RULES.md` §6.1). */
  readonly atk: number;
  /** Defense (`BOSS_RULES.md` §6.1). */
  readonly def: number;
  /**
   * The Boss's state kind (`BOSS_RULES.md` §1) — `Idle` at creation
   * (`GAME_STATE.md` §2.4.1).
   *
   * Typed `string` rather than a locally re-listed union: `Idle`, `Charging`,
   * `Enraged`, and `Stunned` are owned by `BOSS_RULES.md` §1/§5, and §2.4.5
   * records that no MVP Boss currently applies `Stunned`. Re-listing the
   * vocabulary here would be a second copy that could drift.
   */
  readonly state: string;
}

/**
 * The `GET /api/battle/{battleId}/result` success response
 * (`API_CONTRACTS.md` §4).
 *
 * ```json
 * {
 *   "battleId": "string",
 *   "outcome": "victory" | "defeat",
 *   "rewards": {},
 *   "durationTurns": 0
 * }
 * ```
 *
 * Exactly the four documented members. No Redis key, no `sequence`, no RNG
 * value, no persistence metadata, and no authentication internals are added:
 * §4's shape is a representation of the `BattleResult` row, not the row itself,
 * so `DATABASE.md` §1's `PlayerId`, `PetInstanceId`, `BossDefinitionId`, and
 * `CompletedAt` are not read here.
 *
 * The endpoint returns data only for a battle that has already ended
 * (`GAME_EVENTS.md`'s `BattleWon`/`BattleLost`). While a battle is active its
 * state is available through the SignalR connection, not this endpoint — an
 * active or unknown battle both answer `404 BATTLE_NOT_FOUND` (§4).
 */
export interface BattleResultResponse {
  /**
   * The battle's id — the result row's own key, since `BattleResultId` **is**
   * the battle's `BattleId`, one row per battle (§4 note 2, `DATABASE.md` §1).
   */
  readonly battleId: string;
  /**
   * The battle's outcome (`§4` note 4), whose value set and semantics are owned
   * by `GAME_EVENTS.md` §2 (`BattleWon`/`BattleLost`) and shared with
   * `DATABASE.md` §1's persisted `BattleResult.Outcome` and
   * `SIGNALR_PROTOCOL.md` §3.2.19.
   *
   * The vocabulary is exactly those two values — there is no `won`, `lost`,
   * `success`, or `failed` alternative, and the client must not introduce one.
   */
  readonly outcome: BattleOutcome;
  /**
   * The persisted `BattleResult.RewardSummary` (§4 note 1) — **always
   * present**, for both outcomes, never absent or optional.
   */
  readonly rewards: RewardSummaryResponse;
  /**
   * `BattleResult.DurationTurns` — the battle's Turn count at terminal
   * resolution (§4 note 5, `DATABASE.md` §1 "Duration and completion sourcing"
   * item 1). A battle reaching a terminal state before any committed Swap
   * records `0`, which is a value, not an absence.
   */
  readonly durationTurns: number;
}

/**
 * One `GET /api/battle/history` element (`API_CONTRACTS.md` §4.5).
 *
 * ```json
 * [
 *   {
 *     "battleId": "string",
 *     "outcome": "victory" | "defeat",
 *     "rewards": {},
 *     "durationTurns": 0,
 *     "completedAt": "string"
 *   }
 * ]
 * ```
 *
 * **It is the §4 result object plus exactly one further member** — §4.5 note 2
 * makes the four shared members carry "exactly the same meaning, source, type,
 * and value vocabulary" as `GET /api/battle/{battleId}/result`, so this type
 * extends {@link BattleResultResponse} rather than restating them, and reuses
 * `BattleOutcome` and `RewardSummaryResponse` unchanged. There is deliberately
 * no history-specific reward or outcome representation: §4.5 note 12 fixes the
 * element's member set at exactly these five, and note 2 states that "no reduced
 * history-summary member list exists".
 *
 * **`completedAt` is the one additive member** (note 3): `BattleResult.CompletedAt`,
 * the server clock reading captured on the battle-end path
 * (`DATABASE.md` §1). It is server-authoritative and is never derived from a
 * battle id, from any other member, or from the client's own clock — §4.5 notes
 * 3–4 make it the member that makes the delivered ordering observable, and the
 * ordering it expresses is read, never recomputed.
 *
 * **The array is the whole history, in the delivered order.** §4.5 note 1 makes
 * the bare array the response body (no wrapper), note 5 makes it complete (no
 * pagination), note 6 makes it unfiltered and unsorted on the request side, and
 * note 4 fixes its order as `CompletedAt` descending with a `BattleResultId`
 * descending tie-break that "clients MAY rely on". A client therefore renders
 * the array as sent: it does not sort, reverse, re-number by timestamp, or
 * infer an order from `battleId`.
 */
export interface BattleHistoryItemResponse extends BattleResultResponse {
  /**
   * `BattleResult.CompletedAt` (§4.5 note 3, `DATABASE.md` §1) — the
   * server-authoritative completion reading, as serialized. §4.5 note 3 leaves
   * the exact serialization and any timezone to the persisted column, so this is
   * a `string` read as delivered: it is never parsed into a locale, a timezone,
   * or a locally formatted date, and never re-derived.
   */
  readonly completedAt: string;
}

/**
 * The battle-outcome value set (`API_CONTRACTS.md` §4 note 4).
 *
 * `GAME_EVENTS.md` §2 owns these two values, and the persisted
 * `BattleResult.Outcome`, the SignalR wire member
 * (`SIGNALR_PROTOCOL.md` §3.2.19), and this REST response all use the same two
 * for the same battle — so this alias states the contract's set once instead of
 * letting each reader re-spell it.
 */
export type BattleOutcome = 'victory' | 'defeat';

/**
 * The `rewards` member of the battle-result response (`API_CONTRACTS.md` §4
 * note 1), whose member list is owned by `DATABASE.md` §1 "Reward semantics for
 * `RewardSummary`".
 *
 * **The Player track is frozen; the Pet track is implemented.**
 *
 * ```text
 * playerXpGained     int   — Player XP granted by this battle:
 *                            100 on a win, 0 on a loss (COMBAT_RULES.md §7)
 * newPlayerXp        int   — Player.XP after applying the grant
 * playerLeveledUp    bool  — whether Player.Level changed
 * newPlayerLevel     int   — Player.Level after applying the grant
 *
 * petXpGained        int   — Pet XP granted to the active combat Pet:
 *                            100 on a win, 0 on a loss (PET_RULES.md §5.3)
 * newPetXp           int   — that Pet's XP after applying the grant
 * petLeveledUp       bool  — whether that Pet's Level changed
 * newPetLevel        int   — that Pet's Level after applying the grant
 * ```
 *
 * The Player-track member set applies to **both** outcomes
 * (`DATABASE.md` §1 item 5): a `"defeat"` serializes `playerXpGained = 0`,
 * `playerLeveledUp = false`, and the XP/Level members at their unchanged
 * current values. There is therefore no outcome-specific member arity, and
 * `rewards` is never `{}` on the landed path (§4 note 1 keeps `{}` only as the
 * pre-implementation staging value).
 *
 * **All eight members are required.** The Pet-track *member list* was the one
 * part of this contract left open (`DATABASE.md` §1 item 2 deferred it to the
 * implementation, which has landed), so a `petXpGained?: number` optional
 * member — the shape an earlier reading might suggest — is not the contract in
 * force and is not used here. No member is optional for convenience: §4 note 1
 * makes `rewards` always present, and `TASK-076`'s acceptance gates require
 * this type to match the **current** authoritative shape.
 *
 * The four "resulting value" members are nullable **by contract**, not by
 * convenience: `DATABASE.md` §1 records that a battle whose owning Player or
 * Pet row does not exist has no progression to record, so the serialized value
 * there is JSON `null` rather than an invented number. The client therefore
 * reads `null` as "this track had no row", not as "absent field", and never
 * substitutes a zero for it.
 *
 * The client only transports this value. It never computes a reward, applies a
 * grant, updates Player XP, or updates Pet XP — reward amounts and the
 * resulting progression are server-authored (`AGENTS.md` §10, ADR-001).
 */
export interface RewardSummaryResponse {
  /** Player XP granted by this battle — `100` on a win, `0` on a loss (`COMBAT_RULES.md` §7). */
  readonly playerXpGained: number;
  /** `Player.XP` after applying the grant, or `null` when the battle had no owning Player row. */
  readonly newPlayerXp: number | null;
  /** Whether the grant changed `Player.Level`. */
  readonly playerLeveledUp: boolean | null;
  /** `Player.Level` after applying the grant, or `null` when the battle had no owning Player row. */
  readonly newPlayerLevel: number | null;
  /**
   * Pet XP granted to the active combat Pet — `100` on a win, `0` on a loss
   * (`PET_RULES.md` §5.3). Only the active combat Pet receives battle Pet XP.
   */
  readonly petXpGained: number;
  /** That Pet's XP after applying the grant, or `null` when the battle had no such row. */
  readonly newPetXp: number | null;
  /** Whether the grant changed that Pet's `Level`. */
  readonly petLeveledUp: boolean | null;
  /** That Pet's `Level` after applying the grant, or `null` when the battle had no such row. */
  readonly newPetLevel: number | null;
}
