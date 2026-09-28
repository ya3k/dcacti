import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ApplicationSession } from '../src/services/api/ApplicationSession';
import { ApiService } from '../src/services/api/ApiService';
import type {
  BattleResultResponse,
  BattleStartRequest,
  BattleStartResponse,
  Element,
  RewardSummaryResponse,
} from '../src/services/api/ApiService';

/**
 * Client consumption of the two battle REST endpoints
 * (`API_CONTRACTS.md` §3, §4).
 *
 * ```text
 * ApplicationSession  →  ApiService  →  POST /api/battle/start
 *                                    →  GET  /api/battle/{battleId}/result
 * ```
 *
 * Every expected value below is taken from the contract's own text: §3's
 * request/response blocks and `GAME_STATE.md` §2's `BattleState` members for
 * the start response, and §4 plus `DATABASE.md` §1's "Reward semantics for
 * `RewardSummary`" for the result response. The fixtures carry exactly the
 * documented members and nothing else — they are contract fixtures, not
 * minimal ones, because a payload that omitted a required member would let a
 * model drift from the document without failing a test.
 */

/** The §5.1 Element value set, which §3 binds the battle-start members to. */
const ELEMENTS: Element[] = ['Fire', 'Water', 'Earth', 'Wood', 'Metal'];

/**
 * A §3 request body: `petId`, `bossId`, exactly 3 Basic Cards, and 3 Relics.
 * `bossId` is the canonical technical Boss Identity (`BOSS_RULES.md` §6.4),
 * never a display name.
 */
const START_REQUEST: BattleStartRequest = {
  petId: 'pet-001',
  bossId: 'boss-hoa-long',
  cardLoadout: ['heal', 'shield', 'power_charge'],
  relicLoadout: ['relic_id_1', 'relic_id_2', 'relic_id_3'],
};

/**
 * One §2.1.1 cell entry: a Gem type (`MATCH3_RULES.md` §1.1's four functional
 * types) and, when present, the Special Gem occupying the cell.
 */
interface CellFixture {
  gemType: string;
  specialGem?: { type: string; orientation?: string | null } | null;
}

/**
 * A board of exactly 64 cells, which is what `GAME_STATE.md` §2.1.1 requires —
 * `Cells[64]`, row-major, one entry per cell. The array position **is** the
 * cell index, so no index member is written per element (§2.1.7 item 2).
 *
 * Cell 63 carries a `LineClear` Special Gem (with its required orientation,
 * §2.1.4 item 2) and cell 62 a `Burst` Special Gem (orientation-free, same
 * item), so the fixtures exercise both the present and the absent case rather
 * than only the ordinary-Gem path. Every other cell is an ordinary Gem with no
 * Special Gem member.
 */
function boardCells(): CellFixture[] {
  const gemTypes = ['ATK', 'DEF', 'HP', 'POWER'];

  return Array.from({ length: 64 }, (_, index) => {
    const cell: CellFixture = { gemType: gemTypes[index % gemTypes.length] };

    if (index === 63) cell.specialGem = { type: 'LineClear', orientation: 'Horizontal' };
    if (index === 62) cell.specialGem = { type: 'Burst' };

    return cell;
  });
}

/**
 * A §3 `initialState` fixture carrying every member `GAME_STATE.md` §2 defines
 * for a created battle, at its documented creation values.
 *
 * ```text
 * §2.0.2   Turn = 0, Sequence = 0
 * §2.2     Combo = 0, MatchCount = 0
 * §2.3     4 EquippedCards (3 submitted Basics + the derived Signature Skill)
 * §2.3     3-5 EquippedRelics in equip-slot order
 * §2.4     Boss at full HP, State = Idle
 * §2.6.1   RngSeed - server-chosen, unsigned 64-bit
 * §2.6.2   RngState - the state + increment pair
 * ```
 *
 * `playerId` is deliberately **not** here: it is a §2 member, but §2.8 item 3
 * and ADR-014 item 3 exclude it from every wire projection.
 */
function initialStateFixture() {
  return {
    battleId: 'battle-001',
    turn: 0,
    sequence: 0,
    rngSeed: 18446744073709551615,
    rngState: { state: 12345678901234567890, increment: 1442695040888963407 },
    board: { cells: boardCells() },
    combo: 0,
    matchCount: 0,
    petState: {
      hp: 1000,
      maxHP: 1000,
      atk: 50,
      def: 25,
      crit: 5,
      power: 0,
      element: 'Fire' as Element,
      passiveId: 'passive-001',
      passiveThreshold: 10,
      passiveCurrent: 0,
      equippedCards: ['heal', 'shield', 'power_charge', 'pet-skill-001'],
      equippedRelics: ['relic_id_1', 'relic_id_2', 'relic_id_3'],
    },
    bossState: {
      bossId: 'boss-hoa-long',
      element: 'Fire' as Element,
      hp: 5000,
      maxHp: 5000,
      atk: 60,
      def: 30,
      state: 'Idle',
    },
  };
}

/** A §3 success response. */
function startResponseFixture(): BattleStartResponse {
  return {
    battleId: 'battle-001',
    signalrHub: '/hubs/battle',
    initialState: initialStateFixture(),
  };
}

/**
 * The complete `RewardSummary` a landed battle carries (`DATABASE.md` §1
 * "Reward semantics for `RewardSummary`" items 1, 2 and 5).
 *
 * The four Player-track members are the frozen contract; the four Pet-track
 * members are the implemented projection of `PET_RULES.md` §5.3–§5.5. Both
 * tracks are present for **both** outcomes — a `"defeat"` serializes
 * `playerXpGained = 0` / `petXpGained = 0` rather than omitting line items.
 */
function victoryRewards(): RewardSummaryResponse {
  return {
    playerXpGained: 100,
    newPlayerXp: 400,
    playerLeveledUp: true,
    newPlayerLevel: 5,
    petXpGained: 100,
    newPetXp: 900,
    petLeveledUp: true,
    newPetLevel: 10,
  };
}

function defeatRewards(): RewardSummaryResponse {
  return {
    playerXpGained: 0,
    newPlayerXp: 300,
    playerLeveledUp: false,
    newPlayerLevel: 4,
    petXpGained: 0,
    newPetXp: 800,
    petLeveledUp: false,
    newPetLevel: 9,
  };
}

/** The documented §6 error envelope. */
interface ErrorEnvelope {
  error: string;
  message: string;
}

let fetchMock: ReturnType<typeof vi.fn>;

/** The URL `fetch` was called with. */
function requestedUrl(): string {
  return fetchMock.mock.calls[0][0] as string;
}

/** The `RequestInit` `fetch` was called with. */
function requestedInit(): RequestInit {
  return fetchMock.mock.calls[0][1] as RequestInit;
}

/** The request body `fetch` was called with, parsed back from its JSON text. */
function requestedBody(): unknown {
  return JSON.parse(requestedInit().body as string);
}

/** Queues a successful response carrying `body` as its JSON payload. */
function respondWith(body: unknown): void {
  fetchMock.mockResolvedValue({ ok: true, status: 200, json: async () => body });
}

/** Queues the documented §6 error envelope at `status`. */
function respondWithError(status: number, envelope: ErrorEnvelope): void {
  fetchMock.mockResolvedValue({ ok: false, status, json: async () => envelope });
}

/**
 * Establishes the session both battle endpoints require, so every request
 * below is an authenticated one and the header under test is the session's
 * own.
 */
function establishSession(sessionToken = 'issued.token.value'): void {
  ApplicationSession.getInstance().establish({ sessionToken, playerId: 'player_9' });
}

beforeEach(() => {
  ApplicationSession.getInstance().clear();
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
});

describe('ApiService.startBattle (API_CONTRACTS.md §3)', () => {
  it('should POST /api/battle/start with the documented method and path', async () => {
    establishSession();
    respondWith(startResponseFixture());

    await ApiService.getInstance().startBattle(START_REQUEST);

    // §1/§3: starting a battle is `POST /api/battle/start` — the only
    // documented way to create a battle, with no query parameter, no variant,
    // and no gameplay-free alternative (§3).
    expect(requestedUrl()).toBe('/api/battle/start');
    expect(requestedInit().method).toBe('POST');
  });

  it('should serialize exactly the four documented request members', async () => {
    establishSession();
    respondWith(startResponseFixture());

    await ApiService.getInstance().startBattle(START_REQUEST);

    // §3's request block is exactly `petId`, `bossId`, `cardLoadout`, and
    // `relicLoadout`. The member list is closed: nothing server-owned and
    // nothing identity-bearing is added.
    expect(requestedBody()).toEqual({
      petId: 'pet-001',
      bossId: 'boss-hoa-long',
      cardLoadout: ['heal', 'shield', 'power_charge'],
      relicLoadout: ['relic_id_1', 'relic_id_2', 'relic_id_3'],
    });
    expect(Object.keys(requestedBody() as object).sort()).toEqual([
      'bossId',
      'cardLoadout',
      'petId',
      'relicLoadout',
    ]);
  });

  it('should send no identity, session, or server-owned battle member', async () => {
    establishSession();
    respondWith(startResponseFixture());

    await ApiService.getInstance().startBattle(START_REQUEST);

    // §2.8 "Identity" / ADR-015 D3: the requesting Player is resolved
    // server-side from the session's `player_id` claim, so there is no
    // playerId to send. §4 note 7 forbids any request member, query parameter,
    // or header from selecting or standing in for the caller's identity, and
    // ADR-014 item 3 keeps `playerId` off every wire projection.
    //
    // §1/§3: the server authors the whole resulting BattleState, so the client
    // submits no battleId, turn, sequence, RNG value, HP, or Power.
    const body = requestedBody() as Record<string, unknown>;

    for (const forbidden of [
      'playerId',
      'discordUserId',
      'sessionToken',
      'battleId',
      'turn',
      'sequence',
      'rngSeed',
      'rngState',
      'board',
      'combo',
      'matchCount',
      'petState',
      'bossState',
      'hp',
      'maxHP',
      'atk',
      'def',
      'power',
    ]) {
      // Asserted as a *member*, not as a substring: a Card id such as
      // `power_charge` legitimately contains "power", and a substring guard
      // would forbid a documented loadout value.
      expect(body).not.toHaveProperty(forbidden);
    }

    // §3: the whole request surface is the four members, so the member list
    // itself is the strongest statement that nothing else was added.
    expect(Object.keys(body)).toHaveLength(4);
  });

  it('should JSON-serialize the body with the request content type', async () => {
    establishSession();
    respondWith(startResponseFixture());

    await ApiService.getInstance().startBattle(START_REQUEST);

    // The §3 request block is JSON. The body is the serialized request — not a
    // form encoding, not a query string — and the shared transport sets the
    // matching content type.
    const headers = requestedInit().headers as Record<string, string>;
    expect(headers['Content-Type']).toBe('application/json');
    expect(typeof requestedInit().body).toBe('string');
    expect(requestedBody()).toEqual(START_REQUEST);
  });

  it('should carry the session as Authorization: Bearer', async () => {
    establishSession('header.payload.signature');
    respondWith(startResponseFixture());

    await ApiService.getInstance().startBattle(START_REQUEST);

    // §2.8 "Transport" / ADR-015 D4: REST carries the application session as
    // `Authorization: Bearer <sessionToken>` — the same mechanism the
    // collection reads use, not a second one.
    expect(requestedInit().headers).toEqual({
      'Content-Type': 'application/json',
      Authorization: 'Bearer header.payload.signature',
    });
  });

  it('should return the typed response with every documented member', async () => {
    establishSession();
    const response = startResponseFixture();
    respondWith(response);

    const started = await ApiService.getInstance().startBattle(START_REQUEST);

    expect(started).toEqual(response);
    expect(Object.keys(started).sort()).toEqual(['battleId', 'initialState', 'signalrHub']);
  });

  it('should deserialize the nested initialState, board, and RNG state', async () => {
    establishSession();
    respondWith(startResponseFixture());

    const { initialState } = await ApiService.getInstance().startBattle(START_REQUEST);

    // §3: `initialState` is the §2 BattleState summary — the state's own
    // members, not a reduced projection. Turn and Sequence are 0 at creation
    // (§2.0.2) and Combo/MatchCount are 0 because no Match has occurred
    // (§2.2) — zero is a value, not an absence (§2.2.1 item 1).
    expect(Object.keys(initialState).sort()).toEqual([
      'battleId',
      'board',
      'bossState',
      'combo',
      'matchCount',
      'petState',
      'rngSeed',
      'rngState',
      'sequence',
      'turn',
    ]);
    expect(initialState.battleId).toBe('battle-001');
    expect(initialState.turn).toBe(0);
    expect(initialState.sequence).toBe(0);
    expect(initialState.combo).toBe(0);
    expect(initialState.matchCount).toBe(0);

    // §2.8 item 3 / ADR-014 item 3: the battle's owning PlayerId is server
    // state only and is never a wire member.
    expect(initialState).not.toHaveProperty('playerId');

    // §2.6.1: the seed is an unsigned 64-bit value the client only reads; the
    // full-range value round-trips rather than being clamped or truncated.
    expect(initialState.rngSeed).toBe(18446744073709551615);

    // §2.6.2 item 1: RngState is one logical field carried as a state +
    // increment pair, and the pair is not split.
    expect(Object.keys(initialState.rngState).sort()).toEqual(['increment', 'state']);
    expect(initialState.rngState.state).toBe(12345678901234567890);
    expect(initialState.rngState.increment).toBe(1442695040888963407);
  });

  it('should deserialize all 64 board cells in index order with Special Gem metadata', async () => {
    establishSession();
    respondWith(startResponseFixture());

    const { initialState } = await ApiService.getInstance().startBattle(START_REQUEST);
    const { cells } = initialState.board;

    // §2.1.1 item 1: `Cells` is a fixed-length row-major array of exactly 64
    // entries, and the array position is the cell index (§2.1.7 item 2) — the
    // order is the server's and is never re-enumerated.
    expect(cells).toHaveLength(64);
    expect(cells[0]).toEqual({ gemType: 'ATK' });
    expect(cells[1]).toEqual({ gemType: 'DEF' });
    expect(cells[3]).toEqual({ gemType: 'POWER' });
    expect(cells[4]).toEqual({ gemType: 'ATK' });

    // §2.1.3 item 1: a cell's gemType is present whether or not the cell holds
    // a Special Gem — the Special Gem is metadata on the Gem, not a substitute
    // for it.
    expect(cells[63].gemType).toBe('POWER');
    expect(cells[63].specialGem).toEqual({ type: 'LineClear', orientation: 'Horizontal' });
    expect(cells[62].gemType).toBe('HP');
    expect(cells[62].specialGem).toEqual({ type: 'Burst' });
  });

  it('should read an absent Special Gem as absence rather than as an ordinary type', async () => {
    establishSession();
    respondWith(startResponseFixture());

    const { initialState } = await ApiService.getInstance().startBattle(START_REQUEST);
    const ordinary = initialState.board.cells[0];

    // §2.1.7 item 3: absence and an explicit `null` are the same statement —
    // "this cell holds no Special Gem" — and no third spelling exists. What
    // must never happen is an absent Special Gem being spelled as one of the
    // three real types, so the absent member is read as absent/null and is
    // never the string `LineClear`, `Burst`, or `Area`.
    expect(ordinary.specialGem ?? null).toBeNull();
    expect(ordinary.specialGem).not.toBe('LineClear');
    expect(ordinary.specialGem).not.toBe('Burst');
    expect(ordinary.specialGem).not.toBe('Area');
  });

  it('should deserialize petState with the combat stats, passive, and loadouts', async () => {
    establishSession();
    respondWith(startResponseFixture());

    const { petState } = (await ApiService.getInstance().startBattle(START_REQUEST)).initialState;

    // §2.3: the combat stats are the active Pet's, initialized to their
    // COMBAT_RULES.md §1.1 MVP defaults. Crit is the percentage §1.1 defines,
    // so the value is 5 and not 0.05.
    expect(petState.hp).toBe(1000);
    expect(petState.maxHP).toBe(1000);
    expect(petState.atk).toBe(50);
    expect(petState.def).toBe(25);
    expect(petState.crit).toBe(5);
    expect(petState.power).toBe(0);

    // §3 → §5.1: the Element is a wire member, so it carries the canonical
    // English value — never a Vietnamese display name and never a Domain enum
    // member name.
    expect(ELEMENTS).toContain(petState.element);
    expect(petState.element).toBe('Fire');

    // §2.3: the Passive identity is set at battle creation and is a value, not
    // a definition — the threshold is not copied into a second concept.
    expect(petState.passiveId).toBe('passive-001');
    expect(petState.passiveThreshold).toBe(10);
    expect(petState.passiveCurrent).toBe(0);

    // §2.3: EquippedCards is the 4 battle-scoped entries — the 3 submitted
    // Basics plus the derived Signature Skill — and EquippedRelics is the
    // validated 3-5 in equip-slot order.
    expect(petState.equippedCards).toEqual(['heal', 'shield', 'power_charge', 'pet-skill-001']);
    expect(petState.equippedRelics).toEqual(['relic_id_1', 'relic_id_2', 'relic_id_3']);

    // §2.3: StatusEffects[] is a §2.3 member but is not yet implemented, so it
    // is not on this payload — and an unimplemented member must not be
    // invented here either.
    expect(petState).not.toHaveProperty('statusEffects');
  });

  it('should deserialize bossState at its creation values', async () => {
    establishSession();
    respondWith(startResponseFixture());

    const { bossState } = (await ApiService.getInstance().startBattle(START_REQUEST)).initialState;

    // §2.4: `bossId` is the canonical technical Boss Identity — the same value
    // the request submitted — not a display name and not a BossDefinitionId.
    expect(bossState.bossId).toBe('boss-hoa-long');
    expect(bossState.element).toBe('Fire');
    expect(bossState.hp).toBe(5000);
    expect(bossState.maxHp).toBe(5000);
    expect(bossState.atk).toBe(60);
    expect(bossState.def).toBe(30);

    // §2.4.1 / §2.4.5: the Boss starts in its Initial State, and no MVP Boss
    // applies Stunned.
    expect(bossState.state).toBe('Idle');
  });

  it('should accept every canonical Element wire value on both element members', async () => {
    establishSession();

    const seen: Record<string, string[]> = { pet: [], boss: [] };

    for (const element of ELEMENTS) {
      const fixture = startResponseFixture();
      fetchMock.mockReset();
      respondWith({
        ...fixture,
        initialState: {
          ...fixture.initialState,
          petState: { ...fixture.initialState.petState, element },
          bossState: { ...fixture.initialState.bossState, element },
        },
      });

      const { initialState } = await ApiService.getInstance().startBattle(START_REQUEST);

      // §5.1 binds the set once for the document's whole REST surface, and §3
      // names `petState.element` and `bossState.element` explicitly, so both
      // members carry the same English vocabulary as the §5 collection
      // responses.
      expect(initialState.petState.element).toBe(element);
      expect(initialState.bossState.element).toBe(element);

      seen.pet.push(initialState.petState.element);
      seen.boss.push(initialState.bossState.element);
    }

    // All five wire values are decodable on both members, and the observed
    // vocabularies are exactly the canonical set — never a Vietnamese display
    // name (`Mộc`, `Hỏa`, `Thổ`, `Kim`, `Thủy` — ELEMENT_RULES.md §1, display
    // text only) and never a Domain enum member name (`Moc`, `Hoa`, `Tho`,
    // `Thuy`, `Kim`), which is the value TASK-074 removed from this endpoint.
    for (const observed of [seen.pet, seen.boss]) {
      expect(observed).toEqual(ELEMENTS);

      for (const forbidden of ['Mộc', 'Hỏa', 'Thổ', 'Kim', 'Thủy']) {
        expect(observed).not.toContain(forbidden);
      }

      for (const enumName of ['Moc', 'Hoa', 'Tho', 'Thuy', 'Kim']) {
        expect(observed).not.toContain(enumName);
      }
    }
  });

  it('should transport a 3-Relic loadout as the documented minimum', async () => {
    establishSession();
    respondWith(startResponseFixture());

    // §3 / RELIC_RULES.md §2.1: `relicLoadout` is 3-5 elements, so 3 is valid.
    // The client does not validate the count — it transports the selection, and
    // this asserts the boundary arrives at the server unmodified rather than
    // being padded or rejected locally.
    await ApiService.getInstance().startBattle({
      ...START_REQUEST,
      relicLoadout: ['relic_a', 'relic_b', 'relic_c'],
    });

    expect((requestedBody() as BattleStartRequest).relicLoadout).toEqual([
      'relic_a',
      'relic_b',
      'relic_c',
    ]);
  });

  it('should transport a 5-Relic loadout as the documented maximum', async () => {
    establishSession();
    respondWith(startResponseFixture());

    // §3 / RELIC_RULES.md §2.1: 5 is the documented upper bound. The client
    // neither truncates it nor decides validity.
    await ApiService.getInstance().startBattle({
      ...START_REQUEST,
      relicLoadout: ['relic_a', 'relic_b', 'relic_c', 'relic_d', 'relic_e'],
    });

    expect((requestedBody() as BattleStartRequest).relicLoadout).toEqual([
      'relic_a',
      'relic_b',
      'relic_c',
      'relic_d',
      'relic_e',
    ]);
  });

  it('should preserve the relicLoadout order as the equip-slot order', async () => {
    establishSession();
    respondWith(startResponseFixture());

    // §3 / RELIC_RULES.md §2.3: the array order **is** the equip slot order —
    // position i is slot i + 1 — and the server must not re-sort it. The client
    // must therefore not reorder it either: these ids sort differently from
    // their slot order on purpose.
    await ApiService.getInstance().startBattle({
      ...START_REQUEST,
      relicLoadout: ['relic_c', 'relic_a', 'relic_b'],
    });

    expect((requestedBody() as BattleStartRequest).relicLoadout).toEqual([
      'relic_c',
      'relic_a',
      'relic_b',
    ]);
  });

  it('should propagate 400 INVALID_LOADOUT', async () => {
    establishSession();
    respondWithError(400, {
      error: 'INVALID_LOADOUT',
      message: 'The Card or Relic selection is not valid.',
    });

    // §3: a Card or Relic selection failing count, ownership, category,
    // copy-limit, or distinctness is rejected with this one documented code. A
    // rejected request equips nothing and writes no battle state.
    await expect(ApiService.getInstance().startBattle(START_REQUEST)).rejects.toThrow('400');
  });

  it('should propagate 400 PET_NOT_OWNED', async () => {
    establishSession();
    respondWithError(400, {
      error: 'PET_NOT_OWNED',
      message: 'The requested Pet is not owned by the requesting Player.',
    });

    // §3: `petId` must be a Pet owned by the requesting Player (PET_RULES.md
    // §2); the foreign and unknown cases share this one code.
    await expect(ApiService.getInstance().startBattle(START_REQUEST)).rejects.toThrow('400');
  });

  it('should propagate 400 BOSS_NOT_FOUND', async () => {
    establishSession();
    respondWithError(400, {
      error: 'BOSS_NOT_FOUND',
      message: 'The requested Boss is not a valid MVP Boss.',
    });

    // §3: `bossId` must be a valid MVP Boss's canonical technical Identity
    // (BOSS_RULES.md §6/§6.4), never a display name.
    await expect(ApiService.getInstance().startBattle(START_REQUEST)).rejects.toThrow('400');
  });

  it('should propagate 401 UNAUTHENTICATED', async () => {
    respondWithError(401, {
      error: 'UNAUTHENTICATED',
      message: 'An authenticated session is required to start a battle.',
    });

    // §2.8 "Failure behavior": a missing, invalid/tampered, or expired session
    // is one public response — 401 with the §6 envelope.
    await expect(ApiService.getInstance().startBattle(START_REQUEST)).rejects.toThrow('401');
  });

  it('should send no Authorization header when no session is established', async () => {
    respondWith(startResponseFixture());

    // §2.8 "Transport": with no session there is no bearer credential to
    // present — the request is sent unauthenticated and the server answers 401,
    // rather than the client inventing a placeholder token.
    await ApiService.getInstance().startBattle(START_REQUEST);

    expect(requestedInit().headers).toEqual({ 'Content-Type': 'application/json' });
  });

  it('should not mutate the request it was given', async () => {
    establishSession();
    respondWith(startResponseFixture());

    const request: BattleStartRequest = {
      ...START_REQUEST,
      cardLoadout: ['heal', 'shield', 'power_charge'],
      relicLoadout: ['relic_c', 'relic_a', 'relic_b'],
    };
    const before = JSON.stringify(request);

    await ApiService.getInstance().startBattle(request);

    // The service transports the selection; it does not sort, pad, drop,
    // de-duplicate, or otherwise author it. A mutation here would be the client
    // deciding part of the loadout the server owns.
    expect(JSON.stringify(request)).toBe(before);
  });
});

describe('ApiService.getBattleResult (API_CONTRACTS.md §4)', () => {
  it('should GET /api/battle/{battleId}/result with the documented method and path', async () => {
    establishSession();
    respondWith({
      battleId: 'battle-001',
      outcome: 'victory',
      rewards: victoryRewards(),
      durationTurns: 12,
    });

    await ApiService.getInstance().getBattleResult('battle-001');

    // §1/§4's route: the id is the `{battleId}` segment, not a query parameter.
    expect(requestedUrl()).toBe('/api/battle/battle-001/result');
    expect(requestedInit().method).toBe('GET');
  });

  it('should safely encode a battleId that is not URL-safe', async () => {
    establishSession();
    respondWith({
      battleId: 'battle-001',
      outcome: 'victory',
      rewards: victoryRewards(),
      durationTurns: 12,
    });

    // The id is a route segment, so it is percent-encoded rather than
    // concatenated raw. Encoding must not turn it into a query string: the
    // separators stay escaped, which is what keeps one path segment one
    // segment — and §4 defines no query parameter that could be smuggled in.
    await ApiService.getInstance().getBattleResult('battle/../admin?id=1&x=2');

    const url = requestedUrl();
    expect(url).toBe('/api/battle/battle%2F..%2Fadmin%3Fid%3D1%26x%3D2/result');
    expect(url.startsWith('/api/battle/')).toBe(true);
    expect(url.endsWith('/result')).toBe(true);
    expect(url).not.toContain('?');
  });

  it('should carry the session as Authorization: Bearer', async () => {
    establishSession('header.payload.signature');
    respondWith({
      battleId: 'battle-001',
      outcome: 'victory',
      rewards: victoryRewards(),
      durationTurns: 12,
    });

    await ApiService.getInstance().getBattleResult('battle-001');

    expect(requestedInit().headers).toEqual({
      Authorization: 'Bearer header.payload.signature',
    });
  });

  it('should send no playerId or identity input', async () => {
    establishSession();
    respondWith({
      battleId: 'battle-001',
      outcome: 'victory',
      rewards: victoryRewards(),
      durationTurns: 12,
    });

    await ApiService.getInstance().getBattleResult('battle-001');

    // §4 note 7: only the authenticated owner may read a result, ownership is
    // derived server-side from the session, and no request member, query
    // parameter, header, or body field may select, override, or stand in for
    // the caller's identity — so the battleId route segment is the only input.
    const serialized = JSON.stringify(fetchMock.mock.calls[0]);
    expect(serialized).not.toContain('playerId');
    expect(serialized).not.toContain('discordUserId');
    expect(serialized).not.toContain('X-Player-Id');
  });

  it('should return exactly the four documented members', async () => {
    establishSession();
    const response: BattleResultResponse = {
      battleId: 'battle-001',
      outcome: 'victory',
      rewards: victoryRewards(),
      durationTurns: 12,
    };
    respondWith(response);

    const result = await ApiService.getInstance().getBattleResult('battle-001');

    expect(result).toEqual(response);
    expect(Object.keys(result).sort()).toEqual([
      'battleId',
      'durationTurns',
      'outcome',
      'rewards',
    ]);

    // §4's shape is a representation of the BattleResult row, not the row
    // itself: DATABASE.md §1's PlayerId, PetInstanceId, BossDefinitionId, and
    // CompletedAt are persistence values the endpoint does not expose, and no
    // Redis key, Sequence, or RNG value appears either.
    for (const forbidden of [
      'playerId',
      'petInstanceId',
      'bossDefinitionId',
      'completedAt',
      'sequence',
      'rngSeed',
      'rngState',
    ]) {
      expect(result).not.toHaveProperty(forbidden);
    }
  });

  it('should deserialize a "victory" outcome with the full reward payload', async () => {
    establishSession();
    respondWith({
      battleId: 'battle-001',
      outcome: 'victory',
      rewards: victoryRewards(),
      durationTurns: 12,
    });

    const result = await ApiService.getInstance().getBattleResult('battle-001');

    // §4 note 4: the outcome vocabulary is exactly `"victory" | "defeat"`, with
    // its value set owned by GAME_EVENTS.md §2 (BattleWon). `won`, `success`,
    // and `failed` are not part of it and must never be produced or accepted as
    // alternatives.
    expect(result.outcome).toBe('victory');
    for (const invented of ['won', 'lost', 'success', 'failed']) {
      expect(result.outcome).not.toBe(invented);
    }

    // §4 note 5 / DATABASE.md §1: `durationTurns` is BattleResult.DurationTurns
    // — the battle's Turn count at terminal resolution.
    expect(result.durationTurns).toBe(12);

    // §4 note 1: `rewards` is BattleResult.RewardSummary (DATABASE.md §1). The
    // Player track is the four frozen members; both tracks are present.
    expect(Object.keys(result.rewards).sort()).toEqual([
      'newPetLevel',
      'newPetXp',
      'newPlayerLevel',
      'newPlayerXp',
      'petLeveledUp',
      'petXpGained',
      'playerLeveledUp',
      'playerXpGained',
    ]);
    expect(result.rewards.playerXpGained).toBe(100);
    expect(result.rewards.playerLeveledUp).toBe(true);
    expect(result.rewards.petXpGained).toBe(100);
    expect(result.rewards.petLeveledUp).toBe(true);
  });

  it('should deserialize a "defeat" outcome with the same reward member set', async () => {
    establishSession();
    respondWith({
      battleId: 'battle-002',
      outcome: 'defeat',
      rewards: defeatRewards(),
      durationTurns: 4,
    });

    const result = await ApiService.getInstance().getBattleResult('battle-002');

    expect(result.outcome).toBe('defeat');
    for (const invented of ['won', 'lost', 'success', 'failed']) {
      expect(result.outcome).not.toBe(invented);
    }
    expect(result.durationTurns).toBe(4);

    // DATABASE.md §1 item 5: the Player-track member set applies to **both**
    // outcomes. A defeat serializes `playerXpGained = 0`,
    // `playerLeveledUp = false`, and the XP/Level members at their unchanged
    // current values — it does not carry an empty rewards object or drop the
    // Pet track.
    expect(Object.keys(result.rewards).sort()).toEqual(Object.keys(victoryRewards()).sort());
    expect(result.rewards.playerXpGained).toBe(0);
    expect(result.rewards.playerLeveledUp).toBe(false);
    expect(result.rewards.newPlayerXp).toBe(300);
    expect(result.rewards.petXpGained).toBe(0);
    expect(result.rewards.petLeveledUp).toBe(false);
  });

  it('should read a reward member with no progression row as null, not as zero', async () => {
    establishSession();
    respondWith({
      battleId: 'battle-003',
      outcome: 'victory',
      rewards: {
        playerXpGained: 100,
        newPlayerXp: null,
        playerLeveledUp: null,
        newPlayerLevel: null,
        petXpGained: 100,
        newPetXp: null,
        petLeveledUp: null,
        newPetLevel: null,
      },
      durationTurns: 1,
    });

    const result = await ApiService.getInstance().getBattleResult('battle-003');

    // DATABASE.md §1 item 2: a battle whose owning Player or Pet row does not
    // exist has no progression to record, so the resulting-value members are
    // JSON `null` rather than an invented number. The client reads that as "no
    // row" and never substitutes a zero or a default for it.
    expect(result.rewards.newPlayerXp).toBeNull();
    expect(result.rewards.newPlayerLevel).toBeNull();
    expect(result.rewards.playerLeveledUp).toBeNull();
    expect(result.rewards.newPetXp).toBeNull();
    expect(result.rewards.newPetLevel).toBeNull();
    expect(result.rewards.petLeveledUp).toBeNull();

    // The grant amounts are still the outcome's documented amounts, because
    // that is what the battle awarded.
    expect(result.rewards.playerXpGained).toBe(100);
    expect(result.rewards.petXpGained).toBe(100);
  });

  it('should read a battle that ended before any committed Swap as durationTurns 0', async () => {
    establishSession();
    respondWith({
      battleId: 'battle-004',
      outcome: 'defeat',
      rewards: defeatRewards(),
      durationTurns: 0,
    });

    const result = await ApiService.getInstance().getBattleResult('battle-004');

    // DATABASE.md §1 "Duration and completion sourcing" item 1: a battle
    // reaching a terminal state before any committed Swap records 0, and zero
    // here is a value rather than an absent member.
    expect(result.durationTurns).toBe(0);
  });

  it('should propagate 404 BATTLE_NOT_FOUND', async () => {
    establishSession();
    respondWithError(404, { error: 'BATTLE_NOT_FOUND', message: 'The requested battle was not found.' });

    // §4: an authenticated caller asking for a result that does not exist or is
    // not theirs receives this one answer for both cases, so the endpoint never
    // discloses another Player's battle. The client surfaces the documented
    // failure as the transport reports it and invents no distinct code.
    await expect(ApiService.getInstance().getBattleResult('battle-404')).rejects.toThrow('404');
  });

  it('should propagate 401 UNAUTHENTICATED', async () => {
    respondWithError(401, {
      error: 'UNAUTHENTICATED',
      message: 'An authenticated session is required to read a battle result.',
    });

    // §4 note 6: an unauthenticated caller receives 401 UNAUTHENTICATED — not
    // 404 BATTLE_NOT_FOUND, which would make an authorization failure
    // indistinguishable from a missing row.
    await expect(ApiService.getInstance().getBattleResult('battle-001')).rejects.toThrow('401');
  });

  it('should send no Authorization header when no session is established', async () => {
    respondWith({
      battleId: 'battle-001',
      outcome: 'victory',
      rewards: victoryRewards(),
      durationTurns: 12,
    });

    await ApiService.getInstance().getBattleResult('battle-001');

    expect(requestedInit().headers).toEqual({});
  });
});

describe('Battle API scope (AGENTS.md §10, ADR-001, §3, §4)', () => {
  it('should expose no skill-cast, swap, or gameplay-action method', () => {
    // TASK-076 is the REST transport only. In-battle actions are SignalR Hub
    // methods, not REST endpoints (API_CONTRACTS.md §1: "There is no REST
    // endpoint for a player Swap"; §7 item 1; TDD.md §5), so a REST method
    // named after one would be an invented endpoint.
    const surface = Object.getOwnPropertyNames(ApiService.prototype);

    for (const forbidden of [
      'swap',
      'sendSwap',
      'castCard',
      'castPetSkill',
      'getBattleState',
      'joinBattle',
      'resolveAction',
    ]) {
      expect(surface).not.toContain(forbidden);
    }
  });

  it('should expose no method that mutates battle, player, or pet progression', () => {
    // The reward path is server-owned: BattleWon/BattleLost grants and the
    // Player/Pet XP they produce are persisted server-side (COMBAT_RULES.md §7,
    // PET_RULES.md §5.3, ADR-001). §4 note 1 makes `rewards` an output to read,
    // never an input to apply, so no local grant surface may exist.
    const surface = Object.getOwnPropertyNames(ApiService.prototype);

    for (const forbidden of [
      'applyRewards',
      'grantRewards',
      'grantPlayerXp',
      'grantPetXp',
      'updatePlayerXp',
      'updatePetXp',
      'setBattleState',
      'storeBattleState',
      'cacheBattleState',
      'setPlayerState',
      'setPetState',
      'setBossState',
    ]) {
      expect(surface).not.toContain(forbidden);
    }
  });

  it('should keep no battle state on the service instance', async () => {
    establishSession();
    respondWith(startResponseFixture());

    const service = ApiService.getInstance();
    const before = JSON.stringify(Object.keys(service));

    await service.startBattle(START_REQUEST);

    // The service is transport-focused: it hands back the typed response and
    // keeps nothing. A cached BattleState on the client would be a second,
    // client-side copy of server-authoritative state (GAME_STATE.md §2.0.4
    // item 1, ARCHITECTURE.md §2.2.1 item 5).
    expect(JSON.stringify(Object.keys(service))).toBe(before);
    expect(Object.keys(service)).not.toContain('battleState');
    expect(Object.keys(service)).not.toContain('currentBattle');
    expect(Object.keys(service)).not.toContain('lastResult');
  });

  it('should not recompute or default any server-authored response value', async () => {
    establishSession();

    // A payload whose values are deliberately not the creation defaults is
    // returned exactly as sent: the service derives nothing, clamps nothing,
    // and substitutes no zero for a missing or null value.
    const sent: BattleResultResponse = {
      battleId: 'battle-777',
      outcome: 'victory',
      rewards: {
        playerXpGained: 100,
        newPlayerXp: 4900,
        playerLeveledUp: true,
        newPlayerLevel: 50,
        petXpGained: 100,
        newPetXp: 4900,
        petLeveledUp: false,
        newPetLevel: 50,
      },
      durationTurns: 37,
    };
    respondWith(sent);

    const result = await ApiService.getInstance().getBattleResult('battle-777');

    expect(result).toEqual(sent);
  });
});
