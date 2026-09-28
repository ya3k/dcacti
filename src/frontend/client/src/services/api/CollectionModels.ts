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
 * `effectDefinition`, … — must not be added here, and neither may §5.6's
 * equip/loadout members (`isEquipped`, `equipped`, `slot`, `loadoutPosition`,
 * `active`): §5.6 states that equip state is battle-scoped and unpersisted, so
 * no §5 response carries it and a client must not read it from these endpoints.
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
 *   "level": 1
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
}

/**
 * One `GET /api/cards` array element (`API_CONTRACTS.md` §5.3).
 *
 * ```json
 * {
 *   "cardId": "card-001",
 *   "name": "Example Card",
 *   "category": "Basic"
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
 *   "relicId": "relic-001",
 *   "name": "Example Relic"
 * }
 * ```
 *
 * `relicId` is the owned **instance** identity — the value submitted in
 * `relicLoadout` at battle start and snapshotted into
 * `PetState.EquippedRelics[]` (`RELIC_RULES.md` §2.2, `GAME_STATE.md` §2.3) —
 * not a definition id.
 */
export interface RelicResponse {
  /** `Relic.RelicInstanceId` (§5.4). */
  readonly relicId: string;
  /** `RelicDefinition.Name` (§5.4). */
  readonly name: string;
}
