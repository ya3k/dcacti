import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ApplicationSession } from '../src/services/api/ApplicationSession';
import { ApiRequestError, ApiService } from '../src/services/api/ApiService';
import type { CardResponse, PetResponse, RelicResponse } from '../src/services/api/CollectionModels';
import type { Element } from '../src/services/api/CollectionModels';

/**
 * Client consumption of the four collection read endpoints
 * (`API_CONTRACTS.md` §5.1–§5.6).
 *
 * ```text
 * ApplicationSession  →  ApiService  →  GET /api/pets
 *                                    →  GET /api/pets/{petId}
 *                                    →  GET /api/cards
 *                                    →  GET /api/relics
 * ```
 *
 * Every expected value below is taken from §5's own examples and member
 * tables: the fixtures carry exactly the documented members and nothing else.
 */

/** A §5.1/§5.2 Pet, with exactly the seven documented members. */
const PET: PetResponse = {
  petId: 'pet-001',
  identity: 'pet-fire-001',
  element: 'Fire',
  tier: 'Common',
  star: 1,
  level: 1,
  signatureSkill: { cardId: 'card-inferno', name: 'Inferno', category: 'PetSkill' },
};

/** A §5.3 Card, with exactly the four documented members. */
const CARD: CardResponse = {
  cardId: 'card-heal',
  name: 'Heal',
  category: 'Basic',
  effectDefinition: [{ effectType: 'Heal', valueType: 'PercentMaxHp', value: 20 }],
};

/** A §5.4 Relic, with exactly the five documented members. */
const RELIC: RelicResponse = {
  relicId: 'relic-instance-1',
  name: 'Berserker Core',
  trigger: 'OnMatchCount',
  condition: { conditionType: 'MatchCountAtLeast', threshold: 3 },
  effectDefinition: [
    { effectType: 'ATK', valueType: 'Percentage', value: 5, target: 'Pet', lifetime: 'Battle' },
  ],
};

/**
 * The canonical §5.1 Element value set — the REST surface's whole Element
 * vocabulary. The Vietnamese display names of `ELEMENT_RULES.md` §1 are
 * deliberately absent: they are presentation text, never wire values.
 */
const ELEMENTS: Element[] = ['Fire', 'Water', 'Earth', 'Wood', 'Metal'];

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

/** Queues a successful response carrying `body` as its JSON payload. */
function respondWith(body: unknown): void {
  fetchMock.mockResolvedValue({ ok: true, status: 200, json: async () => body });
}

/** Queues the documented §6 error envelope at `status`. */
function respondWithError(status: number, envelope: ErrorEnvelope): void {
  fetchMock.mockResolvedValue({ ok: false, status, json: async () => envelope });
}

/**
 * Awaits a rejected request and returns the rejection.
 *
 * `API_CONTRACTS.md` §6 defines the error response as a machine-readable `error`
 * plus a human-readable `message`, and the shared transport reads both: the
 * message is what a caller may show a player, and the code and the HTTP status
 * stay on the error for diagnostics. Asserting all three is what makes these
 * cases pin the envelope rather than merely "something failed".
 */
async function captureRejection(promise: Promise<unknown>): Promise<ApiRequestError> {
  try {
    await promise;
  } catch (error) {
    expect(error).toBeInstanceOf(ApiRequestError);
    return error as ApiRequestError;
  }

  throw new Error('Expected the request to be rejected, but it resolved.');
}

/**
 * Establishes the session the §5 endpoints require, so every request below is
 * an authenticated one and the header under test is the session's own.
 */
function establishSession(sessionToken = 'issued.token.value'): void {
  ApplicationSession.getInstance().establish({ sessionToken, playerId: 'player_9' });
}

beforeEach(() => {
  ApplicationSession.getInstance().clear();
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
});

describe('ApiService.getPets (API_CONTRACTS.md §5.1, §5.5)', () => {
  it('should GET /api/pets with the documented method and path', async () => {
    establishSession();
    respondWith([PET]);

    await ApiService.getInstance().getPets();

    // §1/§5: the endpoint is `GET /api/pets` — no query parameter, no
    // pagination, no sort, no filter (§5.5).
    expect(requestedUrl()).toBe('/api/pets');
    expect(requestedInit().method).toBe('GET');
  });

  it('should carry the session as Authorization: Bearer', async () => {
    establishSession('header.payload.signature');
    respondWith([PET]);

    await ApiService.getInstance().getPets();

    // §2.3 "Transport" / ADR-015 D4: REST carries the application session as
    // `Authorization: Bearer <sessionToken>`.
    expect(requestedInit().headers).toEqual({
      Authorization: 'Bearer header.payload.signature',
    });
  });

  it('should return the typed response array', async () => {
    establishSession();
    respondWith([PET]);

    const pets = await ApiService.getInstance().getPets();

    expect(pets).toEqual([PET]);

    // §5.1's member list is binding and exhaustive: the seven members, and no
    // eighth. `name` is absent because §5.1 spells the member `identity`, and
    // the persisted-but-not-exposed `xp`, `acquiredAt`, `playerId`, and
    // `petDefinitionId` must not appear. `signatureSkill` is §5.1's derived
    // Signature Skill reference — the member that identifies the Pet's Skill
    // without the owned Card collection (§5.3).
    expect(Object.keys(pets[0]).sort()).toEqual([
      'element',
      'identity',
      'level',
      'petId',
      'signatureSkill',
      'star',
      'tier',
    ]);
    expect(Object.keys(pets[0].signatureSkill).sort()).toEqual([
      'cardId',
      'category',
      'name',
    ]);
    expect(pets[0]).not.toHaveProperty('name');
    expect(pets[0]).not.toHaveProperty('xp');
    expect(pets[0]).not.toHaveProperty('acquiredAt');
    expect(pets[0]).not.toHaveProperty('playerId');
    expect(pets[0]).not.toHaveProperty('petDefinitionId');
  });

  it('should accept every canonical Element wire value', async () => {
    establishSession();

    // §5.1: element is "Fire" | "Water" | "Earth" | "Wood" | "Metal" — the
    // English form is the wire value for the whole REST surface, and no
    // Vietnamese display name (`Mộc`, `Hỏa`, `Thổ`, `Kim`, `Thủy`) may appear.
    const pets = ELEMENTS.map((element, index) => ({
      ...PET,
      petId: `pet-${index}`,
      element,
    }));
    respondWith(pets);

    const received = await ApiService.getInstance().getPets();

    expect(received.map((pet) => pet.element)).toEqual(ELEMENTS);
  });

  it('should deserialize an empty collection as []', async () => {
    establishSession();

    // §5.5: "empty collection → 200 with []". A Player owning nothing has an
    // empty collection — not a null, not an undefined, and not an error.
    respondWith([]);

    const pets = await ApiService.getInstance().getPets();

    expect(pets).toEqual([]);
    expect(Array.isArray(pets)).toBe(true);
    expect(pets).toHaveLength(0);
  });

  it('should propagate 401 UNAUTHENTICATED', async () => {
    // §2.3 "Failure behavior": a missing, invalid/tampered, or expired session
    // is one public response — 401 with the §6 envelope.
    respondWithError(401, { error: 'UNAUTHENTICATED', message: 'An authenticated session is required.' });

    const error = await captureRejection(ApiService.getInstance().getPets());

    // §6: the server's human-readable detail is the statement; the code and the
    // status remain available for diagnostics.
    expect(error.message).toBe('An authenticated session is required.');
    expect(error.code).toBe('UNAUTHENTICATED');
    expect(error.status).toBe(401);
  });

  it('should send no Authorization header when no session is established', async () => {
    // §2.3 "Transport": with no session there is no bearer credential to
    // present — the request is sent unauthenticated and the server answers 401,
    // rather than the client inventing a placeholder token.
    respondWith([]);

    await ApiService.getInstance().getPets();

    expect(requestedInit().headers).toEqual({});
  });
});

describe('ApiService.getPet (API_CONTRACTS.md §5.2)', () => {
  it('should GET /api/pets/{petId} with the petId as a path segment', async () => {
    establishSession();
    respondWith(PET);

    await ApiService.getInstance().getPet('pet-001');

    // §5.2's route: the id is the `{petId}` segment, not a query parameter.
    expect(requestedUrl()).toBe('/api/pets/pet-001');
    expect(requestedInit().method).toBe('GET');
  });

  it('should safely encode a petId that is not URL-safe', async () => {
    establishSession();
    respondWith(PET);

    // The id is a route segment, so it is percent-encoded rather than
    // concatenated raw. Encoding must not turn it into a query string: the
    // separators stay escaped, which is what keeps one path segment one
    // segment.
    await ApiService.getInstance().getPet('pet/../admin?id=1&x=2');

    const url = requestedUrl();
    expect(url).toBe('/api/pets/pet%2F..%2Fadmin%3Fid%3D1%26x%3D2');
    expect(url.startsWith('/api/pets/')).toBe(true);
    expect(url).not.toContain('?');
  });

  it('should carry the session as Authorization: Bearer', async () => {
    establishSession('header.payload.signature');
    respondWith(PET);

    await ApiService.getInstance().getPet('pet-001');

    expect(requestedInit().headers).toEqual({
      Authorization: 'Bearer header.payload.signature',
    });
  });

  it('should return the typed response — the same object as one list element', async () => {
    establishSession();
    respondWith(PET);

    const pet = await ApiService.getInstance().getPet('pet-001');

    // §5.2: "200 is the same object as one /api/pets array element — no
    // wrapper." Nothing is unwrapped, renamed, or re-keyed on the way in.
    expect(pet).toEqual(PET);
    expect(Object.keys(pet).sort()).toEqual([
      'element',
      'identity',
      'level',
      'petId',
      'signatureSkill',
      'star',
      'tier',
    ]);
  });

  it('should propagate 404 PET_NOT_FOUND', async () => {
    establishSession();
    respondWithError(404, { error: 'PET_NOT_FOUND', message: 'The requested Pet was not found.' });

    // §5.2: a petId that does not exist and one owned by another Player are the
    // identical response. The client surfaces the documented §6 envelope as the
    // server sent it and invents no distinct code for either case.
    const error = await captureRejection(ApiService.getInstance().getPet('pet-404'));

    expect(error.message).toBe('The requested Pet was not found.');
    expect(error.code).toBe('PET_NOT_FOUND');
    expect(error.status).toBe(404);
  });

  it('should propagate 401 UNAUTHENTICATED', async () => {
    respondWithError(401, { error: 'UNAUTHENTICATED', message: 'An authenticated session is required.' });

    const error = await captureRejection(ApiService.getInstance().getPet('pet-001'));

    expect(error.message).toBe('An authenticated session is required.');
    expect(error.code).toBe('UNAUTHENTICATED');
    expect(error.status).toBe(401);
  });
});

describe('ApiService.getCards (API_CONTRACTS.md §5.3, §5.5)', () => {
  it('should GET /api/cards with the documented method and path', async () => {
    establishSession();
    respondWith([CARD]);

    await ApiService.getInstance().getCards();

    expect(requestedUrl()).toBe('/api/cards');
    expect(requestedInit().method).toBe('GET');
  });

  it('should carry the session as Authorization: Bearer', async () => {
    establishSession('header.payload.signature');
    respondWith([CARD]);

    await ApiService.getInstance().getCards();

    expect(requestedInit().headers).toEqual({
      Authorization: 'Bearer header.payload.signature',
    });
  });

  it('should return the typed response array', async () => {
    establishSession();
    respondWith([CARD, { cardId: 'card-002', name: 'Pet Skill Card', category: 'PetSkill', effectDefinition: [] }]);

    const cards = await ApiService.getInstance().getCards();

    // §5.3: exactly `cardId`, `name`, `category`, `effectDefinition`. There is
    // deliberately no `unlocked` member — membership of the array *is* the
    // unlocked state — and `playerId`, `powerCost`, and `loadoutCopyLimit` are
    // not exposed. `effectDefinition` IS exposed: it is the Card's own
    // structured effect rule, carried element for element.
    expect(cards).toEqual([
      {
        cardId: 'card-heal',
        name: 'Heal',
        category: 'Basic',
        effectDefinition: [{ effectType: 'Heal', valueType: 'PercentMaxHp', value: 20 }],
      },
      { cardId: 'card-002', name: 'Pet Skill Card', category: 'PetSkill', effectDefinition: [] },
    ]);
    expect(Object.keys(cards[0]).sort()).toEqual([
      'cardId',
      'category',
      'effectDefinition',
      'name',
    ]);
    expect(cards[0]).not.toHaveProperty('unlocked');
    expect(cards[0]).not.toHaveProperty('playerId');
    expect(cards[0]).not.toHaveProperty('powerCost');
    expect(cards[0]).not.toHaveProperty('loadoutCopyLimit');

    // The element keeps the definition's own member names and present-iff
    // shape (DATABASE.md §1/§3): a plain effect carries exactly the triple.
    expect(Object.keys(cards[0].effectDefinition[0]).sort()).toEqual([
      'effectType',
      'value',
      'valueType',
    ]);
  });

  it('should accept the effect-specific extra members and their absence', async () => {
    establishSession();

    // DATABASE.md §3: `duration` is present iff effectType = Burn and `scope`
    // iff effectType = Crit. An element that defines neither omits both, so the
    // model must represent "absent" rather than "null".
    respondWith([
      {
        cardId: 'card-inferno',
        name: 'Inferno',
        category: 'PetSkill',
        effectDefinition: [
          { effectType: 'Damage', valueType: 'Flat', value: 100 },
          { effectType: 'Burn', valueType: 'Flat', value: 50, duration: 2 },
        ],
      },
      {
        cardId: 'card-iron-fang',
        name: 'Iron Fang',
        category: 'PetSkill',
        effectDefinition: [
          { effectType: 'Crit', valueType: 'PercentagePoints', value: 10, scope: 'NextAttack' },
        ],
      },
    ]);

    const cards = await ApiService.getInstance().getCards();

    const burn = cards[0].effectDefinition[1];
    expect(burn).toEqual({ effectType: 'Burn', valueType: 'Flat', value: 50, duration: 2 });
    expect(burn).not.toHaveProperty('scope');
    expect(cards[0].effectDefinition[0]).not.toHaveProperty('duration');

    const crit = cards[1].effectDefinition[0];
    expect(crit).toEqual({
      effectType: 'Crit',
      valueType: 'PercentagePoints',
      value: 10,
      scope: 'NextAttack',
    });
    expect(crit).not.toHaveProperty('duration');
  });

  it('should carry no cost, affordability, or legality member', async () => {
    establishSession();
    respondWith([CARD]);

    const cards = await ApiService.getInstance().getCards();
    const serialized = JSON.stringify(cards);

    // The content/cost boundary: §5.3 answers "what does this Card do?" and
    // never "can I afford to cast it right now" (CARD_RULES.md §3.6,
    // SIGNALR_PROTOCOL.md §4 item 15).
    for (const forbidden of [
      'powerCost',
      'effectiveCost',
      'cardCostModifier',
      'affordable',
      'canCast',
      'legality',
    ]) {
      expect(serialized).not.toContain(forbidden);
    }
  });

  it('should deserialize an empty collection as []', async () => {
    establishSession();

    // §5.5: a Player with no unlocks gets 200 with []. An empty unlock set is
    // an empty collection, not an absent one (ADR-012).
    respondWith([]);

    const cards = await ApiService.getInstance().getCards();

    expect(cards).toEqual([]);
    expect(Array.isArray(cards)).toBe(true);
  });

  it('should propagate 401 UNAUTHENTICATED', async () => {
    respondWithError(401, { error: 'UNAUTHENTICATED', message: 'An authenticated session is required.' });

    const error = await captureRejection(ApiService.getInstance().getCards());

    expect(error.message).toBe('An authenticated session is required.');
    expect(error.code).toBe('UNAUTHENTICATED');
    expect(error.status).toBe(401);
  });
});

describe('ApiService.getRelics (API_CONTRACTS.md §5.4, §5.5, §5.6)', () => {
  it('should GET /api/relics with the documented method and path', async () => {
    establishSession();
    respondWith([RELIC]);

    await ApiService.getInstance().getRelics();

    expect(requestedUrl()).toBe('/api/relics');
    expect(requestedInit().method).toBe('GET');
  });

  it('should carry the session as Authorization: Bearer', async () => {
    establishSession('header.payload.signature');
    respondWith([RELIC]);

    await ApiService.getInstance().getRelics();

    expect(requestedInit().headers).toEqual({
      Authorization: 'Bearer header.payload.signature',
    });
  });

  it('should return the typed response array', async () => {
    establishSession();
    respondWith([RELIC]);

    const relics = await ApiService.getInstance().getRelics();

    // §5.4: exactly `relicId`, `name`, `trigger`, `condition`, and
    // `effectDefinition`. `playerId`, `acquiredAt`, and `definitionId` are not
    // exposed — the content is delivered inline per owned instance.
    expect(relics).toEqual([RELIC]);
    expect(Object.keys(relics[0]).sort()).toEqual([
      'condition',
      'effectDefinition',
      'name',
      'relicId',
      'trigger',
    ]);
    expect(relics[0]).not.toHaveProperty('playerId');
    expect(relics[0]).not.toHaveProperty('acquiredAt');
    expect(relics[0]).not.toHaveProperty('definitionId');
    expect(relics[0]).not.toHaveProperty('relicDefinitionId');

    // The condition object carries its own two members, and the effect element
    // the definition's own five (RELIC_RULES.md §8.2–§8.3).
    expect(Object.keys(relics[0].condition!).sort()).toEqual(['conditionType', 'threshold']);
    expect(Object.keys(relics[0].effectDefinition[0]).sort()).toEqual([
      'effectType',
      'lifetime',
      'target',
      'value',
      'valueType',
    ]);
  });

  it('should accept a null condition as the contract spelling of "no condition"', async () => {
    establishSession();

    // RELIC_RULES.md §8.1 item 4: a Relic whose Trigger alone is its complete
    // condition carries none. §5.4 emits the member as an explicit `null` so the
    // member set is fixed and exactly assertable — it is not an unsupported
    // value and not an absent member.
    respondWith([
      {
        relicId: 'relic-instance-4',
        name: 'Burning Curse',
        trigger: 'OnBattleStart',
        condition: null,
        effectDefinition: [
          {
            effectType: 'BurnDamage',
            valueType: 'Percentage',
            value: 30,
            target: 'Pet',
            lifetime: 'Battle',
          },
        ],
      },
    ]);

    const relics = await ApiService.getInstance().getRelics();

    expect(relics[0].condition).toBeNull();
    expect(relics[0]).toHaveProperty('condition');
    expect(relics[0].trigger).toBe('OnBattleStart');
  });

  it('should accept an Undetermined effect with no value member', async () => {
    establishSession();

    // RELIC_RULES.md §8.2 item 3: an Undetermined element carries no `value`
    // member at all — never 0 and never an explicit null.
    respondWith([
      {
        relicId: 'relic-instance-5',
        name: 'Unquantified',
        trigger: 'OnCascade',
        condition: null,
        effectDefinition: [
          { effectType: 'Power', valueType: 'Undetermined', target: 'Pet', lifetime: 'Immediate' },
        ],
      },
    ]);

    const relics = await ApiService.getInstance().getRelics();

    expect(relics[0].effectDefinition[0]).toEqual({
      effectType: 'Power',
      valueType: 'Undetermined',
      target: 'Pet',
      lifetime: 'Immediate',
    });
    expect(relics[0].effectDefinition[0]).not.toHaveProperty('value');
  });

  it('should read no equip state from the response', async () => {
    establishSession();
    respondWith([RELIC]);

    const relics = await ApiService.getInstance().getRelics();

    // §5.6: no §5 response carries `isEquipped`, `equipped`, `slot`,
    // `loadoutPosition`, or `active` — equip state is battle-scoped and
    // unpersisted, so it is not read from a collection endpoint.
    for (const forbidden of [
      'isEquipped',
      'equipped',
      'slot',
      'loadoutPosition',
      'active',
    ]) {
      expect(relics[0]).not.toHaveProperty(forbidden);
    }
  });

  it('should deserialize an empty collection as []', async () => {
    establishSession();

    // §5.5: a Player owning no Relic gets 200 with [].
    respondWith([]);

    const relics = await ApiService.getInstance().getRelics();

    expect(relics).toEqual([]);
    expect(Array.isArray(relics)).toBe(true);
  });

  it('should propagate 401 UNAUTHENTICATED', async () => {
    respondWithError(401, { error: 'UNAUTHENTICATED', message: 'An authenticated session is required.' });

    const error = await captureRejection(ApiService.getInstance().getRelics());

    expect(error.message).toBe('An authenticated session is required.');
    expect(error.code).toBe('UNAUTHENTICATED');
    expect(error.status).toBe(401);
  });
});

describe('Collection read scope (AGENTS.md §10, ADR-001, §5.6)', () => {
  it('should expose no collection write, equip, or ownership method', () => {
    // The client reads server-owned collection data. Ownership is established
    // by the session alone (§5), and equip state is battle-scoped and
    // unpersisted (§5.6, ADR-011), so no local mutation surface may exist —
    // adding one would be client-authoritative collection state.
    const surface = Object.getOwnPropertyNames(ApiService.prototype);

    for (const forbidden of [
      'addPet',
      'grantPet',
      'unlockCard',
      'addRelic',
      'equipPet',
      'equipCard',
      'equipRelic',
      'setLoadout',
      'setEquippedRelics',
      'removePet',
      'removeRelic',
    ]) {
      expect(surface).not.toContain(forbidden);
    }
  });

  it('should send no playerId or gameplay value in a collection request', async () => {
    establishSession();
    respondWith([PET]);

    await ApiService.getInstance().getPets();

    // §5: "a caller reads only their own collection, and no request member,
    // query parameter, or header selects a playerId." ADR-015 D3: the server
    // derives the identity from the session's player_id claim, so the client
    // has no playerId to send — and §5.5 adds no page/limit/cursor/sort/filter
    // parameter for it to send either.
    const serialized = JSON.stringify(fetchMock.mock.calls[0]);
    expect(serialized).not.toContain('playerId');
    expect(serialized).not.toContain('X-Player-Id');
    expect(serialized).not.toContain('page');
    expect(serialized).not.toContain('limit');
    expect(serialized).not.toContain('cursor');
    expect(serialized).not.toContain('sort');
    expect(serialized).not.toContain('filter');
    expect(serialized).not.toContain('search');
  });

  it('should return the server array unchanged, in the order the server sent', async () => {
    establishSession();

    // §5.5: "ordering — none defined — clients must not rely on any order."
    // The service must therefore not sort, group, or otherwise reorder the
    // collection it was given.
    const sent = [
      { ...PET, petId: 'pet-c' },
      { ...PET, petId: 'pet-a' },
      { ...PET, petId: 'pet-b' },
    ];
    respondWith(sent);

    const pets = await ApiService.getInstance().getPets();

    expect(pets.map((pet) => pet.petId)).toEqual(['pet-c', 'pet-a', 'pet-b']);
  });
});
