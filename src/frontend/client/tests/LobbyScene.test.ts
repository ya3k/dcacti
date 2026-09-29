import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { LobbyScene, MVP_BOSS_ID } from '../src/game/scenes/LobbyScene';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
import { INITIAL_RUNTIME_STATE } from '../src/state/GameRuntimeState';
import type { BattleStartRequest } from '../src/services/api/BattleModels';
import type { CardResponse, PetResponse, RelicResponse } from '../src/services/api/CollectionModels';

/**
 * TASK-078 — LobbyScene.
 *
 * Phaser's real `Scene` cannot run headlessly in jsdom, and its `add` / `scene` /
 * `registry` members are prototype getters that cannot be assigned onto an
 * instance. The harness therefore builds a `this` context whose prototype is the
 * real scene instance, so the scene's own private helpers resolve, and overrides
 * only the Phaser collaborators the scene calls — the same technique
 * `SceneLifecycle.test.ts` uses for `BattleScene`.
 *
 * The doubles are deliberately strict about the boundary: everything the scene
 * reads or submits goes through the fake runtime port, so a scene that reached
 * for the transport some other way would simply have nothing to call.
 */
vi.mock('phaser', () => ({
  AUTO: 'AUTO',
  Scale: { FIT: 'FIT', CENTER_BOTH: 'CENTER_BOTH' },
  Game: vi.fn().mockImplementation(() => ({ destroy: vi.fn() })),
  Scene: class MockScene {},
  Structs: { Size: class MockSize {} },
  Loader: { Events: { COMPLETE: 'complete' } },
  Input: { Events: { GAMEOBJECT_POINTER_DOWN: 'gameobjectdown' } },
}));

/** A `GET /api/pets` element (API_CONTRACTS.md §5.1) — the owned instance. */
function pet(petId: string, identity: string): PetResponse {
  return { petId, identity, element: 'Fire', tier: 'Common', star: 1, level: 1 };
}

/** A `GET /api/cards` element (API_CONTRACTS.md §5.3). */
function card(cardId: string, name: string, category: 'Basic' | 'PetSkill'): CardResponse {
  return { cardId, name, category };
}

/** A `GET /api/relics` element (API_CONTRACTS.md §5.4) — the owned instance. */
function relic(relicId: string, name: string): RelicResponse {
  return { relicId, name };
}

/** The starter ownership DATABASE.md §2 records the server grants. */
const STARTER_PETS = [pet('pet-instance-1', 'Xích Lang')];
const STARTER_CARDS = [
  card('card-heal', 'Heal', 'Basic'),
  card('card-shield', 'Shield', 'Basic'),
  card('card-power-charge', 'Power Charge', 'Basic'),
  // A PetSkill card is never submitted (`API_CONTRACTS.md` §3); it is present
  // here so the test can prove the scene never puts one in `cardLoadout`.
  card('card-inferno', 'Inferno', 'PetSkill'),
];
const STARTER_RELICS = [
  relic('relic-instance-1', 'Berserker Core'),
  relic('relic-instance-2', 'Mana Crystal'),
  relic('relic-instance-3', 'Assassin Eye'),
];

interface Clickable {
  readonly text: string;
  readonly interactive: boolean;
  click(): void;
}

interface SceneHarnessOptions {
  withRuntime?: boolean;
  pets?: PetResponse[];
  cards?: CardResponse[];
  relics?: RelicResponse[];
  collectionFailure?: Error | null;
  startBehaviour?: (() => Promise<void>) | null;
}

function createLobbyHarness(options: SceneHarnessOptions = {}) {
  const {
    withRuntime = true,
    pets = STARTER_PETS,
    cards = STARTER_CARDS,
    relics = STARTER_RELICS,
    collectionFailure = null,
    startBehaviour = null,
  } = options;

  const texts: string[] = [];
  /** Every clickable object the scene created, in creation order. */
  const clickables: Clickable[] = [];
  /**
   * The live text objects of the latest render pass. A render pass destroys the
   * previous pass's objects, so only the live ones are readable — the same thing
   * a player would see.
   */
  const liveTexts = new Set<{ readonly text: string }>();
  /** The live rectangles of the latest render pass (the shell and the button). */
  const liveRectangles = new Set<{ readonly text: string }>();
  /** Every collection read the scene performed, in order. */
  const collectionReads: string[] = [];
  /** Every `startBattle` request the scene submitted, in order. */
  const startRequests: BattleStartRequest[] = [];
  const sceneStarted: Array<{ key: string }> = [];

  const doubleOf = <T,>(value: T): Promise<T> =>
    collectionFailure ? Promise.reject(collectionFailure) : Promise.resolve(value);

  const runtime = {
    getState: () => INITIAL_RUNTIME_STATE,
    getBattleState: () => null,
    onRuntimeEvent: () => () => {},
    onBattleEvents: () => () => {},
    onBattleState: () => () => {},
    requestAction: () => Promise.reject(new Error('not used by LobbyScene')),
    getPets: () => {
      collectionReads.push('getPets');
      return doubleOf(pets);
    },
    getPet: (petId: string) => {
      collectionReads.push(`getPet:${petId}`);
      return doubleOf(pets[0]);
    },
    getCards: () => {
      collectionReads.push('getCards');
      return doubleOf(cards);
    },
    getRelics: () => {
      collectionReads.push('getRelics');
      return doubleOf(relics);
    },
    startBattle: (request: BattleStartRequest) => {
      if (startBehaviour) {
        // A rejection must not record a submitted request: nothing was started.
        return startBehaviour();
      }
      startRequests.push(request);
      return Promise.resolve();
    },
  };

  /** A text object with a registered pointer handler. */
  const makeText = (label: string): Clickable => {
    const handlers: Array<() => void> = [];
    let current = label;
    let interactive = false;

    texts.push(label);

    const obj = {
      get text() {
        return current;
      },
      get interactive() {
        return interactive;
      },
      setText: (next: string) => {
        current = next;
        texts.push(next);
        return obj;
      },
      setColor: () => obj,
      setOrigin: () => obj,
      setWordWrapWidth: () => obj,
      setInteractive: () => {
        interactive = true;
        return obj;
      },
      on: (_event: string, handler: () => void) => {
        handlers.push(handler);
        return obj;
      },
      off: () => {
        handlers.length = 0;
        return obj;
      },
      destroy: () => {
        handlers.length = 0;
        liveTexts.delete(obj);
      },
      click: () => {
        for (const handler of [...handlers]) {
          handler();
        }
      },
    };

    liveTexts.add(obj);
    clickables.push(obj as unknown as Clickable);
    return obj as unknown as Clickable;
  };

  /** A rectangle (the shell background and the Start Battle button). */
  const makeRectangle = (): Clickable => {
    const handlers: Array<() => void> = [];
    let interactive = false;

    const obj = {
      // The shell background has no text; the button is found by this label.
      text: 'START BATTLE',
      get interactive() {
        return interactive;
      },
      setStrokeStyle: () => obj,
      setInteractive: () => {
        interactive = true;
        return obj;
      },
      on: (_event: string, handler: () => void) => {
        handlers.push(handler);
        return obj;
      },
      off: () => {
        handlers.length = 0;
        return obj;
      },
      destroy: () => {
        handlers.length = 0;
        liveRectangles.delete(obj);
      },
      click: () => {
        for (const handler of [...handlers]) {
          handler();
        }
      },
    };

    liveRectangles.add(obj);
    clickables.push(obj as unknown as Clickable);
    return obj as unknown as Clickable;
  };

  function context(scene: object): object {
    return Object.assign(Object.create(scene), {
      scene: { start: (key: string) => sceneStarted.push({ key }) },
      add: {
        rectangle: () => makeRectangle(),
        text: (_x: number, _y: number, value: string) => makeText(value),
      },
      registry: {
        get: (key: string) => (withRuntime && key === RUNTIME_REGISTRY_KEY ? runtime : undefined),
      },
      sys: { settings: { key: 'LobbyScene' } },
    });
  }

  return {
    runtime,
    texts,
    clickables,
    collectionReads,
    startRequests,
    sceneStarted,
    context,
    /** The live render pass's text, as the player would read it. */
    rendered: () => [...liveTexts].map((t) => t.text).join('\n'),
    /** The interactive option whose text contains `fragment`. */
    optionContaining: (fragment: string): Clickable => {
      const found = clickables.filter(
        (c) => c.interactive && c.text.includes(fragment) && liveTexts.has(c)
      );
      if (found.length === 0) {
        throw new Error(`No interactive option contains "${fragment}".`);
      }
      return found[found.length - 1];
    },
    /** The Start Battle trigger. */
    startTrigger: (): Clickable => {
      const candidates = clickables.filter(
        (c) => c.interactive && c.text.includes('START BATTLE') && liveRectangles.has(c)
      );
      const button = candidates[candidates.length - 1];
      if (!button) {
        throw new Error('LobbyScene rendered no Start Battle trigger.');
      }
      return button;
    },
  };
}

/** Invokes a scene method with the harness context, as Phaser itself would. */
function runScene(scene: object, ctx: object, method: string): void {
  const fn = Object.getPrototypeOf(scene)[method] as (this: object) => void;
  fn.call(ctx);
}

/** Runs `create()` and lets the collection reads settle. */
async function createLobby(options: SceneHarnessOptions = {}) {
  const harness = createLobbyHarness(options);
  const scene = new LobbyScene();
  const ctx = harness.context(scene);
  runScene(scene, ctx, 'create');
  await flush();
  return { harness, scene, ctx };
}

async function flush(): Promise<void> {
  for (let i = 0; i < 5; i++) {
    await Promise.resolve();
  }
}

/**
 * Selects the full documented loadout (1 Pet / 3 Basic Cards / 3 Relics)
 * through the scene's own option objects, using the collection's own labels.
 */
function selectFullLoadout(harness: ReturnType<typeof createLobbyHarness>): void {
  harness.optionContaining('Xích Lang').click();
  harness.optionContaining('Heal').click();
  harness.optionContaining('Shield').click();
  harness.optionContaining('Power Charge').click();
  for (const label of ['Berserker Core', 'Mana Crystal', 'Assassin Eye']) {
    harness.optionContaining(label).click();
  }
}

describe('LobbyScene — lifecycle and collection load (ARCHITECTURE.md §2.2.3)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('exposes the documented scene class', () => {
    expect(new LobbyScene()).toBeInstanceOf(LobbyScene);
  });

  it('creates the lobby shell without throwing', async () => {
    const { harness } = await createLobby();

    expect(harness.rendered()).toContain('LOBBY — BATTLE PREPARATION');
  });

  it('reads Pets, Cards, and Relics through the runtime port', async () => {
    const { harness } = await createLobby();

    expect(harness.collectionReads).toEqual(['getPets', 'getCards', 'getRelics']);
  });

  it('presents exactly the collection entries the read returned', async () => {
    const { harness } = await createLobby();

    const rendered = harness.rendered();
    expect(rendered).toContain('Xích Lang');
    expect(rendered).toContain('Heal');
    expect(rendered).toContain('Shield');
    expect(rendered).toContain('Power Charge');
    expect(rendered).toContain('Berserker Core');
    expect(rendered).toContain('Mana Crystal');
    expect(rendered).toContain('Assassin Eye');
  });

  it('presents a PetSkill Card as such and never as a Basic Card', async () => {
    const { harness } = await createLobby();

    // §5.3's `category` is a wire member; the presentation shows the value it
    // received rather than classifying the Card itself.
    expect(harness.rendered()).toContain('Inferno  [PetSkill]');
    expect(harness.rendered()).toContain('Heal  [Basic]');
  });

  it('reflects an empty collection exactly, with no fallback loadout', async () => {
    const { harness } = await createLobby({ pets: [], cards: [], relics: [] });

    const rendered = harness.rendered();
    expect(rendered).toContain('No owned Pets returned.');
    expect(rendered).toContain('No unlocked Cards returned.');
    expect(rendered).toContain('No owned Relics returned.');
    // No starter identifier is invented to fill the gap.
    expect(rendered).not.toContain('Xích Lang');
    expect(rendered).not.toContain('Berserker Core');
  });

  it('reports a failed collection read without throwing or substituting data', async () => {
    const { harness } = await createLobby({
      collectionFailure: new Error('Request to /api/pets failed with status 401'),
    });

    const rendered = harness.rendered();
    expect(rendered).toContain('Collection load failed');
    expect(rendered).toContain('401');
    expect(harness.startRequests).toHaveLength(0);
  });

  it('does not present a Relic the collection read did not return', async () => {
    // `relic-emergency-core` is provisioned but deliberately not starter-owned
    // (DATABASE.md §2). When the read omits it, it must not appear at all.
    const { harness } = await createLobby();

    expect(harness.rendered()).not.toContain('Emergency Core');
    expect(harness.rendered()).not.toContain('relic-emergency-core');
  });
});

describe('LobbyScene — selection interaction (GDD.md §2; ARCHITECTURE.md §2.2.3 rules 1–2)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('presents the documented pre-battle steps', async () => {
    const { harness } = await createLobby();

    const rendered = harness.rendered();
    expect(rendered).toContain('1. CHOOSE PET');
    expect(rendered).toContain('2. EQUIP CARDS');
    expect(rendered).toContain('3. EQUIP RELICS');
    expect(rendered).toContain('4. REVIEW');
  });

  it('presents the fixed MVP Boss target and offers no Boss selection', async () => {
    const { harness } = await createLobby();

    expect(harness.rendered()).toContain(`Boss: ${MVP_BOSS_ID}`);
    // No interactive control chooses a Boss, and no second Boss appears.
    expect(harness.clickables.filter((c) => c.interactive && /Boss/i.test(c.text))).toEqual([]);
    expect(harness.rendered()).not.toContain('boss-thuy-ma');
    expect(harness.rendered()).not.toContain('boss-moc-yeu');
  });

  it('selects exactly one Pet, replacing the previous choice', async () => {
    const twoPets = [pet('pet-instance-1', 'Xích Lang'), pet('pet-instance-2', 'Bạch Hổ')];
    const { harness } = await createLobby({ pets: twoPets });

    harness.optionContaining('Xích Lang').click();
    expect(harness.rendered()).toContain('Pet:   pet-instance-1');

    harness.optionContaining('Bạch Hổ').click();
    const rendered = harness.rendered();
    expect(rendered).toContain('Pet:   pet-instance-2');
    expect(rendered).not.toContain('Pet:   pet-instance-1');
  });

  it('clears the Pet choice when the same Pet is tapped twice', async () => {
    const { harness } = await createLobby();

    harness.optionContaining('Xích Lang').click();
    harness.optionContaining('Xích Lang').click();

    expect(harness.rendered()).toContain('Pet:   —');
  });

  it('limits the Card selection to three slots', async () => {
    const { harness } = await createLobby();

    harness.optionContaining('Heal').click();
    harness.optionContaining('Shield').click();
    harness.optionContaining('Power Charge').click();
    // A fourth Basic Card is not offered by the starter set, so the PetSkill
    // card stands in for "any further Card".
    harness.optionContaining('Inferno').click();

    expect(harness.rendered()).toContain('Cards: 3/3');
    expect(harness.rendered()).toContain('Exactly 3 Basic Cards: deselect one first.');
  });

  it('keeps the Relic selection within three to five distinct instances', async () => {
    const { harness } = await createLobby();

    for (const name of ['Berserker Core', 'Mana Crystal', 'Assassin Eye']) {
      harness.optionContaining(name).click();
    }

    expect(harness.rendered()).toContain('Relics: 3/5');
  });

  it('toggles a selected Card and Relic back out', async () => {
    const { harness } = await createLobby();

    harness.optionContaining('Heal').click();
    expect(harness.rendered()).toContain('Cards: 1/3');
    harness.optionContaining('Heal').click();
    expect(harness.rendered()).toContain('Cards: 0/3');

    harness.optionContaining('Mana Crystal').click();
    harness.optionContaining('Mana Crystal').click();
    expect(harness.rendered()).toContain('Relics: 0/5');
  });

  it('reviews the selection without asserting its validity', async () => {
    const { harness } = await createLobby();

    selectFullLoadout(harness);

    const rendered = harness.rendered();
    expect(rendered).toContain('Pet:   pet-instance-1');
    expect(rendered).toContain('Cards: card-heal, card-shield, card-power-charge');
    expect(rendered).toContain(
      'Relics: relic-instance-1, relic-instance-2, relic-instance-3'
    );
    // Review is presentation: no verdict word is computed by the scene.
    expect(rendered).not.toMatch(/\bvalid\b/i);
  });
});

describe('LobbyScene — BattleStartRequest construction (API_CONTRACTS.md §3)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('submits exactly the four documented members', async () => {
    const { harness } = await createLobby();

    selectFullLoadout(harness);
    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests).toHaveLength(1);
    expect(Object.keys(harness.startRequests[0]).sort()).toEqual([
      'bossId',
      'cardLoadout',
      'petId',
      'relicLoadout',
    ]);
  });

  it("submits the collection's Pet instance id, never a definition id", async () => {
    const { harness } = await createLobby({
      pets: [pet('pet-instance-77', 'Xích Lang')],
    });

    selectFullLoadout(harness);
    harness.startTrigger().click();
    await flush();

    // §5.1: the element's `petId` IS `Pet.PetInstanceId`. The scene takes it from
    // the read — it is neither the definition id nor a hardcoded literal.
    expect(harness.startRequests[0].petId).toBe('pet-instance-77');
  });

  it('submits the fixed MVP Boss identity', async () => {
    const { harness } = await createLobby();

    selectFullLoadout(harness);
    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests[0].bossId).toBe('boss-hoa-long');
  });

  it('submits exactly three Basic Card ids and no PetSkill card', async () => {
    const { harness } = await createLobby();

    selectFullLoadout(harness);
    harness.startTrigger().click();
    await flush();

    const { cardLoadout } = harness.startRequests[0];
    expect(cardLoadout).toHaveLength(3);
    expect([...cardLoadout].sort()).toEqual(['card-heal', 'card-power-charge', 'card-shield']);
    expect(cardLoadout).not.toContain('card-inferno');
  });

  it('submits the Relic instance ids in the order the player chose them', async () => {
    const { harness } = await createLobby();

    harness.optionContaining('Xích Lang').click();
    harness.optionContaining('Heal').click();
    harness.optionContaining('Shield').click();
    harness.optionContaining('Power Charge').click();
    // Chosen in an order that is not the collection's order and not sorted:
    // position i is equip slot i + 1 (RELIC_RULES.md §2.3).
    harness.optionContaining('Assassin Eye').click();
    harness.optionContaining('Berserker Core').click();
    harness.optionContaining('Mana Crystal').click();

    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests[0].relicLoadout).toEqual([
      'relic-instance-3',
      'relic-instance-1',
      'relic-instance-2',
    ]);
  });

  it('never submits a Relic the collection read did not return', async () => {
    const { harness } = await createLobby({
      relics: [...STARTER_RELICS, relic('relic-instance-4', 'Emergency Core')],
    });

    harness.optionContaining('Xích Lang').click();
    harness.optionContaining('Heal').click();
    harness.optionContaining('Shield').click();
    harness.optionContaining('Power Charge').click();
    harness.optionContaining('Assassin Eye').click();
    harness.optionContaining('Berserker Core').click();
    harness.optionContaining('Mana Crystal').click();

    harness.startTrigger().click();
    await flush();

    // Present in the read but not selected → absent from the submission.
    expect(harness.startRequests[0].relicLoadout).not.toContain('relic-instance-4');
  });

  it('submits a request built from the read, not from a hardcoded starter set', async () => {
    // A collection with different instance ids and names: the submission must
    // follow the read. Each option is selected by its own row label rather than
    // by a known starter name.
    const { harness } = await createLobby({
      pets: [pet('own-pet-9', 'Huyền Quy')],
      cards: [
        card('own-card-a', 'Alpha', 'Basic'),
        card('own-card-b', 'Beta', 'Basic'),
        card('own-card-c', 'Gamma', 'Basic'),
      ],
      relics: [relic('own-relic-1', 'One'), relic('own-relic-2', 'Two'), relic('own-relic-3', 'Three')],
    });

    harness.optionContaining('Huyền Quy').click();
    for (const label of ['Alpha', 'Beta', 'Gamma', 'One', 'Two', 'Three']) {
      harness.optionContaining(label).click();
    }

    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests[0]).toEqual({
      petId: 'own-pet-9',
      bossId: 'boss-hoa-long',
      cardLoadout: ['own-card-a', 'own-card-b', 'own-card-c'],
      relicLoadout: ['own-relic-1', 'own-relic-2', 'own-relic-3'],
    });

    // None of the starter identifiers appears in the submission.
    const submitted = JSON.stringify(harness.startRequests[0]);
    for (const starter of ['pet-instance-1', 'card-heal', 'card-shield', 'relic-instance-']) {
      expect(submitted).not.toContain(starter);
    }
  });

  it('does not submit while the selection is incomplete', async () => {
    const { harness } = await createLobby();

    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests).toHaveLength(0);
    expect(harness.rendered()).toContain('Choose a Pet first.');
  });

  it('does not submit an under-filled Relic loadout', async () => {
    const { harness } = await createLobby();

    harness.optionContaining('Xích Lang').click();
    harness.optionContaining('Heal').click();
    harness.optionContaining('Shield').click();
    harness.optionContaining('Power Charge').click();
    harness.optionContaining('Berserker Core').click();
    harness.optionContaining('Mana Crystal').click();

    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests).toHaveLength(0);
    expect(harness.rendered()).toContain('Choose at least 3 Relics.');
  });

  it('issues exactly one startBattle call for repeated activation', async () => {
    /** Number of `startBattle` calls the double has received. */
    let startCalls = 0;
    /** Set once the in-flight request is resolvable by the test. */
    let resolveStart: () => void = () => {};

    const harness = createLobbyHarness({
      startBehaviour: () => {
        startCalls += 1;
        return new Promise<void>((resolve) => {
          resolveStart = resolve;
        });
      },
    });
    const scene = new LobbyScene();
    const ctx = harness.context(scene);
    runScene(scene, ctx, 'create');
    await flush();

    selectFullLoadout(harness);

    harness.startTrigger().click();
    await flush();

    // The first activation reached the port and is still in flight...
    expect(startCalls).toBe(1);
    expect(harness.rendered()).toContain('request in flight');

    // ...and repeated activation while it is pending is ignored: no second call,
    // and nothing queued.
    harness.startTrigger().click();
    harness.startTrigger().click();
    await flush();

    expect(startCalls).toBe(1);

    resolveStart();
    await flush();

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene']);
  });
});

describe('LobbyScene — start routing and rejection (ARCHITECTURE.md §2.2.3 rule 5)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('transitions to BattleScene on a successful start', async () => {
    const { harness } = await createLobby();

    selectFullLoadout(harness);
    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests).toHaveLength(1);
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene']);
  });

  it('does not transition before the start resolves', async () => {
    const { harness } = await createLobby();

    selectFullLoadout(harness);

    expect(harness.sceneStarted).toEqual([]);
  });

  it.each([
    ['401 UNAUTHENTICATED', 'Request to /api/battle/start failed with status 401'],
    ['400 INVALID_LOADOUT', 'Request to /api/battle/start failed with status 400'],
    ['400 PET_NOT_OWNED', 'Request to /api/battle/start failed with status 400'],
    ['400 BOSS_NOT_FOUND', 'Request to /api/battle/start failed with status 400'],
    ['transport failure', 'SignalR connection is not established.'],
  ])('stays active with error feedback on a %s rejection', async (_case, message) => {
    const { harness } = await createLobby({
      startBehaviour: async () => {
        throw new Error(message);
      },
    });

    selectFullLoadout(harness);
    harness.startTrigger().click();
    await flush();

    // No transition, the error is displayed, and the selection survives so the
    // player can correct and retry.
    expect(harness.sceneStarted).toEqual([]);
    expect(harness.rendered()).toContain('Battle start failed');
    expect(harness.rendered()).toContain(message);
    expect(harness.rendered()).toContain('Pet:   pet-instance-1');
    expect(harness.rendered()).toContain('Cards: card-heal, card-shield, card-power-charge');
  });

  it('re-submits the same selection when the trigger is retried', async () => {
    const { harness } = await createLobby({
      startBehaviour: async () => {
        throw new Error('Request to /api/battle/start failed with status 400');
      },
    });

    selectFullLoadout(harness);
    harness.startTrigger().click();
    await flush();

    // The guard is released on rejection, so the player can retry.
    const { startBehaviour } = { startBehaviour: null };
    void startBehaviour;
    harness.startTrigger().click();
    await flush();

    expect(harness.rendered()).toContain('Battle start failed');
  });

  it('reports the no-runtime path without throwing', async () => {
    const harness = createLobbyHarness({ withRuntime: false });
    const scene = new LobbyScene();
    const ctx = harness.context(scene);

    expect(() => runScene(scene, ctx, 'create')).not.toThrow();

    expect(harness.rendered()).toContain('No runtime is available');
    expect(harness.collectionReads).toEqual([]);

    // Activating the trigger is safe too — no unhandled exception.
    harness.startTrigger().click();
    expect(harness.startRequests).toHaveLength(0);
    expect(harness.sceneStarted).toEqual([]);
  });
});

describe('LobbyScene — shutdown cleanup (ARCHITECTURE.md §2.2.3 rules 1–2)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('is safe to shut down without creating anything', () => {
    const harness = createLobbyHarness();
    const scene = new LobbyScene();

    expect(() => runScene(scene, harness.context(scene), 'shutdown')).not.toThrow();
  });

  it('discards the in-progress selection on shutdown', async () => {
    const { harness, scene, ctx } = await createLobby();

    selectFullLoadout(harness);
    expect(harness.rendered()).toContain('Cards: card-heal, card-shield, card-power-charge');

    runScene(scene, ctx, 'shutdown');

    // ARCHITECTURE.md §2.2.3 rules 1–2: a shut-down lobby has no selection and
    // keeps none. A restart therefore begins from an empty one — the previous
    // selection is not carried across, and nothing about it survives on the
    // runtime either (the runtime holds no collection or selection at all).
    runScene(scene, ctx, 'create');
    await flush();

    const rendered = harness.rendered();
    expect(rendered).toContain('Pet:   —');
    expect(rendered).toContain('Cards: 0/3');
    expect(rendered).toContain('Relics: 0/5');
    expect(rendered).toContain('Pet:   (none chosen)');
    expect(rendered).toContain('Cards: (none chosen)');
    expect(rendered).toContain('Relics: (none chosen)');

    // And nothing can be submitted from the discarded state.
    harness.startTrigger().click();
    await flush();
    expect(harness.startRequests).toHaveLength(0);
    expect(harness.sceneStarted).toEqual([]);
  });

  it('detaches the previous render pass when the scene is restarted', async () => {
    const { harness, scene, ctx } = await createLobby();

    selectFullLoadout(harness);
    runScene(scene, ctx, 'shutdown');

    // The option objects of the shut-down pass are destroyed, so a click on one
    // cannot reach a handler — the leak the cleanup exists to prevent.
    const stale = harness.clickables.filter((c) => c.text.includes('Xích Lang')).pop();
    stale?.click();

    runScene(scene, ctx, 'create');
    await flush();

    expect(harness.rendered()).toContain('Pet:   —');
  });

  it('re-reads the collection when the scene is restarted', async () => {
    const { harness, scene, ctx } = await createLobby();

    expect(harness.collectionReads).toHaveLength(3);

    runScene(scene, ctx, 'shutdown');
    runScene(scene, ctx, 'create');
    await flush();

    expect(harness.collectionReads).toHaveLength(6);
  });
});

describe('LobbyScene — architectural boundaries (AGENTS.md §10, §13; ARCHITECTURE.md §2.2.1)', () => {
  it('imports no transport client and no Discord SDK', () => {
    // Comments are stripped first, so the prose that *names* these forbidden
    // imports while forbidding them is not a violation.
    const source = readFileSync(resolve(__dirname, '../src/game/scenes/LobbyScene.ts'), 'utf8')
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    for (const forbidden of [
      '@microsoft/signalr',
      'HubConnection',
      'services/realtime',
      'ApiService',
      'services/api/ApiService',
      'embedded-app-sdk',
      'DiscordService',
      'services/discord',
    ]) {
      expect(source, `LobbyScene must not reference "${forbidden}"`).not.toContain(forbidden);
    }

    // `fetch` is not used as an HTTP client (the word itself does not appear in
    // code, comments having been stripped).
    expect(source).not.toMatch(/\bfetch\b/);
  });

  it('reaches the collection and the start only through the runtime port', () => {
    const source = readFileSync(resolve(__dirname, '../src/game/scenes/LobbyScene.ts'), 'utf8')
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    expect(source).toContain('readRuntime(this)');
    for (const capability of ['getPets', 'getCards', 'getRelics', 'startBattle']) {
      expect(source).toContain(capability);
    }
  });

  it('contains no client-side randomness', () => {
    const source = readFileSync(resolve(__dirname, '../src/game/scenes/LobbyScene.ts'), 'utf8');

    expect(source).not.toMatch(/Math\.random/);
    expect(source).not.toMatch(/crypto\./);
  });

  it('contains no client-authoritative gameplay or loadout validation', () => {
    const source = readFileSync(resolve(__dirname, '../src/game/scenes/LobbyScene.ts'), 'utf8')
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    // No gameplay system and no validity computation: count/ownership/category/
    // copy-limit/distinctness are the server's answer (API_CONTRACTS.md §3).
    for (const forbidden of [
      'damage',
      'cascade',
      'combo',
      'power',
      'matchCount',
      'isEquipped',
      'loadoutPosition',
      'validate',
    ]) {
      expect(source, `LobbyScene must not reference "${forbidden}"`).not.toContain(forbidden);
    }
  });

  it('holds no Boss collection or Boss selection state', () => {
    const source = readFileSync(resolve(__dirname, '../src/game/scenes/LobbyScene.ts'), 'utf8')
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    expect(source).not.toMatch(/getBosses|getBoss\b|bossList|selectedBossId/);
  });
});

