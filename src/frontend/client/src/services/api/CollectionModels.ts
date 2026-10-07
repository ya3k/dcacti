/**
 * The client-side collection read models — the wire shapes of the four
 * collection read endpoints (`API_CONTRACTS.md` §5.1–§5.4).
 *
 * ```text
 * GET /api/pets          → PetResponse[]
 * GET /api/pets/{petId}  → PetResponse      | 404 PET_NOT_FOUND
 * GET /api/cards         → CardResponse[]
 * GET /api/relics        → RelicResponse[]
 * ```
 *
 * These are **transport shapes only**. The server owns every value in them:
 * ownership comes solely from the authenticated session (§5: "a caller reads
 * only their own collection, and no request member, query parameter, or header
 * selects a `playerId`"), and the client reads the collection — it never
 * derives, defaults, clamps, or mutates a member (`GAME_RULES.md` §18,
 * ADR-001). Nothing here is gameplay state and nothing here is cached as
 * authoritative.
 *
 * Each member list is **binding and exhaustive**, as §5.1 states in its own
 * words. A member that §5.1/§5.3/§5.4 mark persisted-but-not-exposed — `xp`,
 * `acquiredAt`, `playerId`, `petDefinitionId`, `powerCost`, `loadoutCopyLimit`,
 * … — must not be added here, and neither may §5.6's equip/loadout members
 * (`isEquipped`, `equipped`, `slot`, `loadoutPosition`, `active`): §5.6 states
 * that equip state is battle-scoped and unpersisted, so no §5 response carries
 * it and a client must not read it from these endpoints.
 *
 * The structured Card/Relic content members §5.3/§5.4 now expose
 * (`effectDefinition`, `trigger`, `condition`) are represented here as their own
 * **transport shapes**, not as a second copy of the contract: every token stays
 * `string` and is read exactly as delivered, because the closed vocabularies are
 * owned by their domain documents (`DATABASE.md` §1/§3, `CARD_RULES.md`
 * §2/§4.1, `RELIC_RULES.md` §3/§8.1–§8.3) — the same reason `PetResponse.tier`
 * is a `string` rather than a locally re-listed union that could drift.
 */

/**
 * The canonical REST Element value set (`API_CONTRACTS.md` §5.1;
 * `ELEMENT_RULES.md` §1).
 *
 * §5.1 states the set once, for this document's whole REST surface: an Element
 * is spelled `Fire`, `Water`, `Earth`, `Wood`, or `Metal` wherever it is
 * serialized into a REST request or response body. The Vietnamese names
 * (`Mộc`, `Hỏa`, `Thổ`, `Kim`, `Thủy` — `ELEMENT_RULES.md` §1) are display
 * values only and are never wire values, so they do not appear here.
 *
 * This alias therefore preserves the wire values exactly rather than
 * translating them: it exists so a reader of this type cannot mistake a display
 * name for the contract, and it deliberately admits no sixth value.
 */
export type Element = 'Fire' | 'Water' | 'Earth' | 'Wood' | 'Metal';

/**
 * One `GET /api/pets` array element, and the whole body of
 * `GET /api/pets/{petId}` (`API_CONTRACTS.md` §5.1, §5.2).
 *
 * ```json
 * {
 *   "petId": "pet-001",
 *   "identity": "pet-fire-001",
 *   "element": "Fire",
 *   "tier": "Common",
 *   "star": 1,
 *   "level": 1,
 *   "signatureSkill": {
 *     "cardId": "card-inferno",
 *     "name": "Inferno",
 *     "category": "PetSkill"
 *   }
 * }
 * ```
 *
 * §5.2 makes the detail body the same object as one list element — "no
 * wrapper" — so one type covers both.
 */
export interface PetResponse {
  /**
   * `Pet.PetInstanceId` — the **owned instance**, and the same id submitted as
   * `petId` to `POST /api/battle/start` (§5.1, §3). It is not the definition
   * id: several owned instances may share one definition.
   */
  readonly petId: string;
  /**
   * `PetDefinition.Identity` (§5.1) — the Pet's identity, delivered under
   * exactly this member name. It is deliberately not called `name`: §5.1's
   * member list is exhaustive, so a `name` member would be an invented one.
   */
  readonly identity: string;
  /**
   * `PetDefinition.Element` as its canonical wire value (§5.1) — one of
   * `Fire` | `Water` | `Earth` | `Wood` | `Metal`, never a Vietnamese display
   * name and never the domain enum's own member name.
   */
  readonly element: Element;
  /**
   * `Pet.Tier` as its documented name (§5.1) — a string, not a number. The
   * permitted names are owned by `PET_RULES.md` §3; this contract states the
   * member, not a second copy of the tier vocabulary, so the type stays
   * `string` rather than a locally re-listed union that could drift.
   */
  readonly tier: string;
  /** `Pet.Star` (§5.1). */
  readonly star: number;
  /** `Pet.Level` (§5.1). */
  readonly level: number;
  /**
   * The Pet's **derived Signature Skill reference** (§5.1) —
   * `PetDefinition.SignatureSkillCardId` and the `CardDefinition` it names.
   *
   * Always present, never `null`, and never omitted: the FK is required and
   * every Pet has exactly one Signature Skill (`CARD_RULES.md` §4 item 1), so
   * there is no absent form to represent. It is **not** an owned Card and not a
   * §5.3 member — a `PetSkill` `CardDefinition` is never an unlock row
   * (`CARD_RULES.md` §1 item 4), which is exactly why `GET /api/cards` cannot
   * be the identification source for it (`SIGNALR_PROTOCOL.md` §4.3 item 13).
   */
  readonly signatureSkill: PetSignatureSkillResponse;
}

/**
 * One Pet's `signatureSkill` object (`API_CONTRACTS.md` §5.1) — the Pet's
 * **derived** Signature Skill reference.
 *
 * ```json
 * {
 *   "cardId": "card-inferno",
 *   "name": "Inferno",
 *   "category": "PetSkill"
 * }
 * ```
 *
 * The member set is exactly these three. It carries no cost, no affordability
 * state, and no cast-legality judgment — the composed cast value belongs to the
 * realtime projection (`CARD_RULES.md` §3.6, `SIGNALR_PROTOCOL.md` §4 item 15)
 * — and no `effectDefinition`, which stays §5.3's content member.
 */
export interface PetSignatureSkillResponse {
  /**
   * `PetDefinition.SignatureSkillCardId` (§5.1) — the `CardDefinitionId` the
   * Pet's Signature Skill is expressed as, and the definition the battle loadout
   * derives its 4th equipped entry from (`CARD_RULES.md` §4 item 1,
   * `API_CONTRACTS.md` §3).
   */
  readonly cardId: string;
  /** That `CardDefinition`'s `Name` (§5.1). */
  readonly name: string;
  /**
   * That `CardDefinition`'s `Category` as its canonical wire value (§5.1) —
   * `"PetSkill"` for a Pet's Signature Skill (`CARD_RULES.md` §4 item 1). It is
   * the same closed two-member set §5.3 binds, read as delivered.
   */
  readonly category: CardCategory;
}

/**
 * One `GET /api/cards` array element (`API_CONTRACTS.md` §5.3).
 *
 * ```json
 * {
 *   "cardId": "card-heal",
 *   "name": "Heal",
 *   "category": "Basic",
 *   "effectDefinition": [
 *     { "effectType": "Heal", "valueType": "PercentMaxHp", "value": 20 }
 *   ]
 * }
 * ```
 *
 * MVP Cards have no progression (`DATABASE.md` §2, ADR-012): presence in the
 * array **is** the unlocked state, which is why there is no `unlocked` member
 * here — the server does not send one and this contract must not add one.
 */
export interface CardResponse {
  /**
   * `CardDefinition.CardDefinitionId` (§5.3) — the id submitted in
   * `cardLoadout` at battle start (§3).
   */
  readonly cardId: string;
  /** `CardDefinition.Name` (§5.3). */
  readonly name: string;
  /**
   * `CardDefinition.Category` as its canonical wire value (§5.3) — `"Basic"`
   * or `"PetSkill"` (`CARD_RULES.md` §1, `DATABASE.md` §3). The two are the
   * whole set: a category is never invented here.
   */
  readonly category: CardCategory;
  /**
   * `CardDefinition.EffectDefinition` (§5.3) — the Card's own structured effect
   * rule, one element per effect, in the order the server sent. Always present,
   * never `null`, and never empty (`DATABASE.md` §1 stores the column NOT NULL
   * and every Card states at least one effect).
   *
   * It states **what the Card changes**, not what a cast costs: `powerCost`,
   * affordability, and cast legality are deliberately not exposed by §5.3
   * (`CARD_RULES.md` §3.6, `SIGNALR_PROTOCOL.md` §4 item 15).
   */
  readonly effectDefinition: readonly CardEffectResponse[];
}

/**
 * One element of a Card's `effectDefinition` (`API_CONTRACTS.md` §5.3).
 *
 * The member names are the definition's own storage names (`DATABASE.md` §1
 * item 2), and each optional member follows `DATABASE.md` §3's present-iff
 * rule: `value` is present iff the `valueType` interprets one, `duration` iff
 * `effectType` is `Burn`, and `scope` iff it is `Crit`. An absent member is
 * **absent on the wire**, never an explicit `null` — so it is optional here and
 * a reader must fail closed rather than substitute a value.
 *
 * `effectType` and `valueType` stay `string`: their closed sets are owned by
 * `DATABASE.md` §1 item 1 / §3 and their identities and magnitudes by
 * `CARD_RULES.md` §2/§4.1, so re-listing them here would be a second copy of a
 * vocabulary this contract does not own.
 */
export interface CardEffectResponse {
  /**
   * The stored `effectType` token (`DATABASE.md` §1 item 1) — the effect
   * identity (`CARD_RULES.md` §2/§4.1 own its values).
   */
  readonly effectType: string;
  /**
   * The stored `valueType` token (`DATABASE.md` §1 item 1 / §3) — how `value`
   * is interpreted.
   */
  readonly valueType: string;
  /**
   * The stored magnitude, present iff `valueType` interprets one
   * (`DATABASE.md` §3). Never `0`; absent — not `null` — for `Undetermined`.
   */
  readonly value?: number;
  /**
   * The stored duration in Turns, present iff `effectType` is `Burn`
   * (`DATABASE.md` §3; `CARD_RULES.md` §4.1).
   */
  readonly duration?: number;
  /**
   * The stored scope, present iff `effectType` is `Crit` (`DATABASE.md` §3;
   * `CARD_RULES.md` §4.1).
   */
  readonly scope?: string;
}

/**
 * The canonical REST Card category value set (`API_CONTRACTS.md` §5.3).
 *
 * `category` is a wire member wherever a Card category is serialized into a
 * REST response body, and §5.3 binds it to `"Basic" | "PetSkill"`.
 */
export type CardCategory = 'Basic' | 'PetSkill';

/**
 * One `GET /api/relics` array element (`API_CONTRACTS.md` §5.4).
 *
 * ```json
 * {
 *   "relicId": "relic-instance-1",
 *   "name": "Berserker Core",
 *   "trigger": "OnMatchCount",
 *   "condition": { "conditionType": "MatchCountAtLeast", "threshold": 3 },
 *   "effectDefinition": [
 *     { "effectType": "ATK", "valueType": "Percentage", "value": 5,
 *       "target": "Pet", "lifetime": "Battle" }
 *   ]
 * }
 * ```
 *
 * `relicId` is the owned **instance** identity — the value submitted in
 * `relicLoadout` at battle start and snapshotted into
 * `PetState.EquippedRelics[]` (`RELIC_RULES.md` §2.2, `GAME_STATE.md` §2.3) —
 * not a definition id. The content is resolved through the instance's own
 * definition reference, so two owned instances of one definition carry the same
 * content and remain two elements.
 */
export interface RelicResponse {
  /** `Relic.RelicInstanceId` (§5.4). */
  readonly relicId: string;
  /** `RelicDefinition.Name` (§5.4). */
  readonly name: string;
  /**
   * `RelicDefinition.Trigger` (§5.4) — the Relic's one primary Trigger
   * identity. Always present and never `null`. Its closed value set is
   * `RELIC_RULES.md` §3's, so it stays a `string` here.
   */
  readonly trigger: string;
  /**
   * `RelicDefinition.Condition` (§5.4) — the Trigger's optional extra condition
   * as the structured form-plus-threshold object of `RELIC_RULES.md` §8.1, or
   * `null` when the Relic declares none (§8.1 item 4). The member is always
   * present: `null` is the contract's own spelling of "no extra condition", so
   * it must never be read as an unsupported or missing value.
   */
  readonly condition: RelicConditionResponse | null;
  /**
   * `RelicDefinition.EffectDefinition` (§5.4) — the Relic's own structured
   * effect rule, one element per effect, in the order the server sent. Always
   * present, never `null`, and never empty (`DATABASE.md` §1 stores the column
   * NOT NULL and every Relic states at least one effect).
   */
  readonly effectDefinition: readonly RelicEffectResponse[];
}

/**
 * A Relic's `condition` object (`API_CONTRACTS.md` §5.4).
 *
 * The member names are the definition's own storage names (`DATABASE.md` §1),
 * and `conditionType` stays a `string` because its closed three-form set is
 * `RELIC_RULES.md` §8.1's.
 */
export interface RelicConditionResponse {
  /**
   * The stored `conditionType` token (`RELIC_RULES.md` §8.1) — which
   * comparison form applies.
   */
  readonly conditionType: string;
  /** The stored `threshold` — the form's `N`, a positive integer (§8.1 item 1). */
  readonly threshold: number;
}

/**
 * One element of a Relic's `effectDefinition` (`API_CONTRACTS.md` §5.4).
 *
 * The member names are the definition's own (`DATABASE.md` §1 for
 * `effectType`/`valueType`/`value`, `RELIC_RULES.md` §8.3 for
 * `target`/`lifetime`). `value` is present iff the `valueType` interprets one
 * (`RELIC_RULES.md` §8.2 item 3): it is absent — never `0` and never an
 * explicit `null` — for `Undetermined`, so it is optional here.
 *
 * `effectType`, `valueType`, `target`, and `lifetime` stay `string` for the
 * same reason: their closed sets and the allowed per-`effectType` combination
 * are `RELIC_RULES.md` §8.2–§8.4's.
 */
export interface RelicEffectResponse {
  /** The stored `effectType` token (`RELIC_RULES.md` §8.2 item 1). */
  readonly effectType: string;
  /** The stored `valueType` token (`RELIC_RULES.md` §8.2 item 2). */
  readonly valueType: string;
  /**
   * The stored magnitude, present iff `valueType` interprets one
   * (`RELIC_RULES.md` §8.2 item 3). Never `0`.
   */
  readonly value?: number;
  /** The stored `target` token (`RELIC_RULES.md` §8.3 item 1). */
  readonly target: string;
  /** The stored `lifetime` token (`RELIC_RULES.md` §8.3 item 2). */
  readonly lifetime: string;
}
