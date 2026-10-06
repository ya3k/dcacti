import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { CollectionViewerScene, COLLECTION_TABS } from '../src/game/scenes/CollectionViewerScene';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
import { INITIAL_RUNTIME_STATE } from '../src/state/GameRuntimeState';
import type { CardResponse, PetResponse, RelicResponse } from '../src/services/api/CollectionModels';
import { SceneEventEmitter } from './support/SceneEventEmitter';

/**
 * TASK-190 — CollectionViewerScene.
 *
 * Phaser's real `Scene` cannot run headlessly in jsdom, and its `add` / `scene` /
 * `registry` members are prototype getters that cannot be assigned onto an
 * instance. The harness therefore builds a `this` context whose prototype is the
 * real scene instance, so the scene's own private helpers resolve, and overrides
 * only the Phaser collaborators the scene calls — the same technique
 * `LobbyScene.test.ts` uses.
 *
 * The runtime double is strict about the boundary: everything the scene reads
 * goes through the fake port, and the port records every call, so a scene that
 * re-read on a tab switch or duplicated a request would be visible here.
 */
vi.mock('phaser', () => ({
  AUTO: 'AUTO',
  Scale: { FIT: 'FIT', CENTER_BOTH: 'CENTER_BOTH' },
  Game: vi.fn().mockImplementation(() => ({ destroy: vi.fn() })),
  Scene: class MockScene {},
  Structs: { Size: class MockSize {} },
  Loader: { Events: { COMPLETE: 'complete' } },
  Input: { Events: { GAMEOBJECT_POINTER_DOWN: 'gameobjectdown' } },
  // Phaser's scene lifecycle events (TASK-205). The engine emits SHUTDOWN /
  // DESTROY on `scene.events` (`Phaser.Scenes.Systems#shutdown` / `#destroy`)
  // and never calls a scene method merely because one exists, so this scene
  // attaches its teardown to these events and so does this suite.
  Scenes: {
    Events: {
      SHUTDOWN: 'shutdown',
      DESTROY: 'destroy',
    },
  },
}));

/** A `GET /api/pets` element (API_CONTRACTS.md §5.1) — the owned instance. */
function pet(
  petId: string,
  identity: string,
  overrides: Partial<PetResponse> = {}
): PetResponse {
  return { petId, identity, element: 'Fire', tier: 'Common', star: 1, level: 1, ...overrides };
}

/** A `GET /api/cards` element (API_CONTRACTS.md §5.3). */
function card(cardId: string, name: string, category: 'Basic' | 'PetSkill'): CardResponse {
  return { cardId, name, category };
}

/** A `GET /api/relics` element (API_CONTRACTS.md §5.4) — the owned instance. */
function relic(relicId: string, name: string): RelicResponse {
  return { relicId, name };
}

/** The starter ownership profile DATABASE.md §2 records the server grants. */
const STARTER_PETS = [pet('pet-instance-1', 'Xích Lang')];
const STARTER_CARDS = [
  card('card-heal', 'Heal', 'Basic'),
  card('card-shield', 'Shield', 'Basic'),
  card('card-power-charge', 'Power Charge', 'Basic'),
];
const STARTER_RELICS = [
  relic('relic-instance-1', 'Berserker Core'),
  relic('relic-instance-2', 'Mana Crystal'),
  relic('relic-instance-3', 'Assassin Eye'),
];

interface Clickable {
  readonly text: string;
  readonly interactive: boolean;
  readonly x?: number;
  readonly y?: number;
  readonly width?: number;
  readonly height?: number;
  click(): void;
}

interface SceneHarnessOptions {
  withRuntime?: boolean;
  pets?: PetResponse[];
  cards?: CardResponse[];
  relics?: RelicResponse[];
  /** When set, every collection read rejects with it. */
  collectionFailure?: Error | null;
  /**
   * When set, the first load rejects and every later load succeeds — used to
   * prove `RETRY` really re-reads.
   */
  failFirstLoadOnly?: boolean;
  /** Resolves the pending collection reads, so the loading state is observable. */
  deferLoad?: boolean;
}

function createViewerHarness(options: SceneHarnessOptions = {}) {
  const {
    withRuntime = true,
    pets = STARTER_PETS,
    cards = STARTER_CARDS,
    relics = STARTER_RELICS,
    collectionFailure = null,
    failFirstLoadOnly = false,
    deferLoad = false,
  } = options;

  const texts: string[] = [];
  /** Every clickable object the scene created, in creation order. */
  const clickables: Clickable[] = [];
  /**
   * The live objects of the latest render pass. A render pass destroys the
   * previous pass's objects, so only the live ones are readable — the same thing
   * a player would see.
   */
  const liveTexts = new Set<{ readonly text: string }>();
  const liveRectangles = new Set<{ readonly text: string }>();
  /** Every collection read the scene performed, in order. */
  const collectionReads: string[] = [];
  const sceneStarted: Array<{ key: string }> = [];

  /** How many loads have been attempted, 1-based once the first begins. */
  let loadAttempts = 0;
  /** Releases a deferred load, when `deferLoad` is set. */
  let releaseLoad: () => void = () => {};
  /** The promise a deferred load awaits. */
  const loadGate = deferLoad
    ? new Promise<void>((resolve) => {
        releaseLoad = resolve;
      })
    : Promise.resolve();

  const shouldFail = (): boolean => {
    if (collectionFailure === null) {
      return false;
    }
    if (failFirstLoadOnly) {
      return loadAttempts === 1;
    }
    return true;
  };

  const read = <T,>(name: string, value: T): Promise<T> => {
    collectionReads.push(name);
    return loadGate.then(() => {
      if (shouldFail()) {
        throw collectionFailure;
      }
      return value;
    });
  };

  const runtime = {
    getState: () => INITIAL_RUNTIME_STATE,
    getBattleState: () => null,
    onRuntimeEvent: () => () => {},
    onBattleEvents: () => () => {},
    onBattleState: () => () => {},
    requestAction: () => Promise.reject(new Error('not used by CollectionViewerScene')),
    startBattle: () => Promise.reject(new Error('not used by CollectionViewerScene')),
    getPets: () => {
      loadAttempts += 1;
      return read('getPets', pets);
    },
    getPet: (petId: string) => read(`getPet:${petId}`, pets[0]),
    getCards: () => read('getCards', cards),
    getRelics: () => read('getRelics', relics),
    getBattleResult: () => Promise.reject(new Error('not used by CollectionViewerScene')),
  };

  /** A text object with a registered pointer handler. */
  const makeText = (x: number, y: number, label: string): Clickable => {
    const handlers: Array<() => void> = [];
    let current = label;
    let interactive = false;

    texts.push(label);

    const obj = {
      x,
      y,
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

  /**
   * A rectangle drawn behind a label, mirroring how the scene layers a button:
   * the interactive hit area is the rectangle, and the visible caption is a
   * separate text object drawn over it.
   *
   * `labelAt` records the caption's own object once the scene creates it, so a
   * click routed to the rectangle is addressed by the caption the player reads —
   * which is how the smoke and unit tests both identify a button.
   */
  const makeRectangle = (x = 0, y = 0, width = 0, height = 0): Clickable => {
    const handlers: Array<() => void> = [];
    let interactive = false;

    const obj = {
      x,
      y,
      width,
      height,
      // The caption object drawn on top of this rectangle, when there is one.
      label: null as { readonly text: string } | null,
      get text() {
        return obj.label === null ? '' : obj.label.text;
      },
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

  /** The rectangle awaiting a caption, so `makeText` can pair the two. */
  let uncaptionedRectangle: { label: { readonly text: string } | null } | null = null;

  function context(scene: object): object {
    // The scene's own emitter (Phaser's `scene.events`). The engine raises its
    // lifecycle events here and never calls a scene method because one exists
    // (TASK-205), so the harness raises them here too.
    const events = new SceneEventEmitter();

    return Object.assign(Object.create(scene), {
      events,
      scene: { start: (key: string) => sceneStarted.push({ key }) },
      add: {
        rectangle: (x: number, y: number, width: number, height: number) => {
          const rect = makeRectangle(x, y, width, height);
          uncaptionedRectangle = rect as unknown as { label: { readonly text: string } | null };
          return rect;
        },
        text: (x: number, y: number, value: string) => {
          const text = makeText(x, y, value);
          // The scene draws a button's caption immediately after its rectangle,
          // at the same centre. Pairing them here lets a click be addressed by
          // the caption, exactly as a player would.
          if (uncaptionedRectangle !== null) {
            uncaptionedRectangle.label = text as unknown as { readonly text: string };
            uncaptionedRectangle = null;
          }
          return text;
        },
      },
      registry: {
        get: (key: string) => (withRuntime && key === RUNTIME_REGISTRY_KEY ? runtime : undefined),
      },
      sys: { settings: { key: 'CollectionViewerScene' } },
    });
  }

  /** Whether an object of the latest render pass is still live. */
  const isLive = (object: object): boolean =>
    liveTexts.has(object as { readonly text: string }) ||
    liveRectangles.has(object as { readonly text: string });

  return {
    runtime,
    texts,
    clickables,
    collectionReads,
    sceneStarted,
    context,
    releaseLoad,
    /** The live render pass's text, as the player would read it. */
    rendered: () => [...liveTexts].map((t) => t.text).join('\n'),
    /** The live interactive object whose caption contains `fragment`. */
    objectContaining: (fragment: string): Clickable => {
      const found = clickables.filter(
        (c) => c.interactive && c.text.includes(fragment) && isLive(c)
      );
      if (found.length === 0) {
        throw new Error(`No interactive object contains "${fragment}".`);
      }
      return found[found.length - 1];
    },
    /** Clicks the live interactive object whose caption is `label` exactly. */
    clickLabel: (label: string): void => {
      const found = clickables.filter((c) => c.interactive && c.text === label && isLive(c));
      if (found.length === 0) {
        throw new Error(`No interactive object is labelled "${label}".`);
      }
      found[found.length - 1].click();
    },
    /** The interactive objects that are still live, i.e. clickable right now. */
    liveInteractive: (): Clickable[] => clickables.filter((c) => c.interactive && isLive(c)),
    /**
     * The engine's own half of taking a scene down, on its own.
     *
     * Phaser's `DisplayList` destroys every child of a stopped scene whether or
     * not the scene registered a teardown of its own, so this is what the
     * scene's game objects look like after teardown even when no teardown of the
     * scene's own ran. It exists so a test can show what the defect looks like.
     */
    destroySceneDisplayList: (_ctx?: object) => {
      for (const object of [...liveTexts, ...liveRectangles]) {
        (object as { destroy?: () => void }).destroy?.();
      }
    },
    /**
     * Takes a scene down exactly as Phaser does (TASK-205):
     * `SceneManager` → `Systems#shutdown` → `scene.events.emit(SHUTDOWN)`, with
     * the engine's own `DisplayList` having destroyed every child first.
     * Nothing here calls a scene method by name.
     */
    shutdownScene: (ctx: object) => {
      for (const object of [...liveTexts, ...liveRectangles]) {
        // The engine's half of teardown: `DisplayList#shutdown` destroys the
        // scene's game objects whether or not the scene registered anything.
        (object as { destroy?: () => void }).destroy?.();
      }
      (ctx as { events?: SceneEventEmitter }).events?.emit('shutdown');
    },
    /** `Phaser.Scenes.Systems#destroy` → `Phaser.Scenes.Events.DESTROY`. */
    destroyScene: (ctx: object) => {
      (ctx as { events?: SceneEventEmitter }).events?.emit('destroy');
    },
    /** How many listeners the scene's own emitter holds for one lifecycle event. */
    sceneListenerCount: (ctx: object, event: string) =>
      (ctx as { events?: SceneEventEmitter }).events?.listenerCount(event) ?? 0,
  };
}

/** Invokes a scene method with the harness context, as Phaser itself would. */
function runScene(scene: object, ctx: object, method: string): void {
  const fn = Object.getPrototypeOf(scene)[method] as (this: object) => void;
  fn.call(ctx);
}

/** Runs `create()` and lets the collection reads settle. */
async function createViewer(options: SceneHarnessOptions = {}) {
  const harness = createViewerHarness(options);
  const scene = new CollectionViewerScene();
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

describe('CollectionViewerScene — lifecycle and collection load', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('exposes the documented scene class', () => {
    expect(new CollectionViewerScene()).toBeInstanceOf(CollectionViewerScene);
  });

  it('creates the viewer shell without throwing', async () => {
    const { harness } = await createViewer();

    const rendered = harness.rendered();
    expect(rendered).toContain('COLLECTION VIEWER');
    expect(rendered).toContain('< BACK');
  });

  it('requests Pets, Cards, and Relics through the runtime port', async () => {
    const { harness } = await createViewer();

    expect(harness.collectionReads).toEqual(['getPets', 'getCards', 'getRelics']);
  });

  it('defaults to the PETS category', async () => {
    const { ctx } = await createViewer();

    expect((ctx as { tab: string }).tab).toBe('pets');
    expect((ctx as { selectedItemId: string | null }).selectedItemId).toBeNull();
  });

  it('starts in the loading state while the reads are pending', async () => {
    const harness = createViewerHarness({ deferLoad: true });
    const scene = new CollectionViewerScene();
    const ctx = harness.context(scene);
    runScene(scene, ctx, 'create');
    await flush();

    // The reads are in flight, so the scene reports that rather than drawing
    // items it does not have yet.
    expect((ctx as { loading: boolean }).loading).toBe(true);
    expect(harness.rendered()).toContain('Loading collection items…');

    harness.releaseLoad();
    await flush();

    expect((ctx as { loading: boolean }).loading).toBe(false);
    expect(harness.rendered()).not.toContain('Loading collection items…');
  });

  it('shows the loaded collection once all three reads settle', async () => {
    const { harness } = await createViewer();

    const rendered = harness.rendered();
    expect(rendered).toContain('Xích Lang');
    expect(rendered).toContain('PETS (1)');
    expect(rendered).toContain('CARDS (3)');
    expect(rendered).toContain('RELICS (3)');
    expect(rendered).not.toContain('Loading');
  });

  it('presents a Pet row with the element, tier, star, and level the read returned', async () => {
    const { harness } = await createViewer({
      pets: [pet('pet-instance-7', 'Bạch Hổ', { element: 'Metal', tier: 'Rare', star: 3, level: 12 })],
    });

    // Every displayed value is the response's own value: nothing is recomputed,
    // and no level/star/tier is derived on the client.
    const rendered = harness.rendered();
    expect(rendered).toContain('Bạch Hổ');
    expect(rendered).toContain('Metal');
    expect(rendered).toContain('Rare');
    expect(rendered).toContain('★3');
    expect(rendered).toContain('Lv 12');
  });

  it('presents a Card row with the name and category the read returned', async () => {
    const { harness } = await createViewer({
      cards: [card('card-inferno', 'Inferno', 'PetSkill')],
    });

    harness.clickLabel('CARDS (1)');

    expect(harness.rendered()).toContain('Inferno   [PetSkill]');
  });

  it('presents a Relic row with the name the read returned', async () => {
    const { harness } = await createViewer({
      relics: [relic('relic-emergency-core', 'Emergency Core')],
    });

    harness.clickLabel('RELICS (1)');

    expect(harness.rendered()).toContain('Emergency Core');
  });

  it('renders one row per returned entry and no entry the read omitted', async () => {
    const { harness } = await createViewer();

    const rows = harness.rendered().split('\n').filter((line) => /^[○●] /.test(line));
    expect(rows).toHaveLength(1);
    expect(rows[0]).toContain('Xích Lang');

    // Nothing outside the PETS response is presented on the default tab.
    expect(harness.rendered()).not.toContain('Berserker Core');
    expect(harness.rendered()).not.toContain('Heal');
  });

  it('reports the no-runtime path without throwing', () => {
    const harness = createViewerHarness({ withRuntime: false });
    const scene = new CollectionViewerScene();
    const ctx = harness.context(scene);

    expect(() => runScene(scene, ctx, 'create')).not.toThrow();

    expect(harness.rendered()).toContain('No runtime is available');
    expect(harness.collectionReads).toEqual([]);
  });
});

describe('CollectionViewerScene — category tabs', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('offers exactly the three documented categories', () => {
    expect(COLLECTION_TABS.map((t) => t.label)).toEqual(['PETS', 'CARDS', 'RELICS']);
    expect(COLLECTION_TABS.map((t) => t.key)).toEqual(['pets', 'cards', 'relics']);
  });

  it('switches PETS → CARDS, showing the Card collection and its count', async () => {
    const { harness, ctx } = await createViewer();

    harness.clickLabel('CARDS (3)');

    expect((ctx as { tab: string }).tab).toBe('cards');
    const rendered = harness.rendered();
    expect(rendered).toContain('Heal   [Basic]');
    expect(rendered).toContain('Shield   [Basic]');
    expect(rendered).toContain('Power Charge   [Basic]');
    expect(rendered).not.toContain('Xích Lang');
  });

  it('switches CARDS → RELICS', async () => {
    const { harness, ctx } = await createViewer();

    harness.clickLabel('CARDS (3)');
    harness.clickLabel('RELICS (3)');

    expect((ctx as { tab: string }).tab).toBe('relics');
    const rendered = harness.rendered();
    expect(rendered).toContain('Berserker Core');
    expect(rendered).toContain('Mana Crystal');
    expect(rendered).toContain('Assassin Eye');
    expect(rendered).not.toContain('Heal   [Basic]');
  });

  it('switches RELICS → PETS', async () => {
    const { harness, ctx } = await createViewer();

    harness.clickLabel('CARDS (3)');
    harness.clickLabel('RELICS (3)');
    harness.clickLabel('PETS (1)');

    expect((ctx as { tab: string }).tab).toBe('pets');
    expect(harness.rendered()).toContain('Xích Lang');
    expect(harness.rendered()).not.toContain('Berserker Core');
  });

  it('shows the count of the collection the read returned for each category', async () => {
    const { harness } = await createViewer({
      pets: [pet('p1', 'A'), pet('p2', 'B')],
      cards: [card('c1', 'One', 'Basic')],
      relics: [],
    });

    const rendered = harness.rendered();
    expect(rendered).toContain('PETS (2)');
    expect(rendered).toContain('CARDS (1)');
    expect(rendered).toContain('RELICS (0)');
  });

  it('does not re-read the collection when the category changes', async () => {
    const { harness } = await createViewer();

    expect(harness.collectionReads).toHaveLength(3);

    harness.clickLabel('CARDS (3)');
    harness.clickLabel('RELICS (3)');
    harness.clickLabel('PETS (1)');
    await flush();

    // The single load already retrieved all three collections, so a tab switch
    // is a re-render and no request.
    expect(harness.collectionReads).toEqual(['getPets', 'getCards', 'getRelics']);
  });

  it('clears the selected item when the category changes', async () => {
    const { harness, ctx } = await createViewer();

    harness.objectContaining('Xích Lang').click();
    expect((ctx as { selectedItemId: string | null }).selectedItemId).toBe('pet-instance-1');
    expect(harness.rendered()).toContain('Instance ID: pet-instance-1');

    harness.clickLabel('CARDS (3)');

    expect((ctx as { selectedItemId: string | null }).selectedItemId).toBeNull();
    expect(harness.rendered()).toContain('Select an item to view details');
    expect(harness.rendered()).not.toContain('Instance ID:');
  });
});

describe('CollectionViewerScene — item detail panel', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('prompts for a selection before one is made', async () => {
    const { harness } = await createViewer();

    expect(harness.rendered()).toContain('Select an item to view details');
  });

  it('shows the selected Pet’s own fields verbatim', async () => {
    const { harness } = await createViewer({
      pets: [pet('pet-instance-9', 'Huyền Quy', { element: 'Water', tier: 'Epic', star: 4, level: 20 })],
    });

    harness.objectContaining('Huyền Quy').click();

    const rendered = harness.rendered();
    expect(rendered).toContain('Pet: Huyền Quy');
    expect(rendered).toContain('Element: Water');
    expect(rendered).toContain('Tier: Epic');
    expect(rendered).toContain('Star: 4');
    expect(rendered).toContain('Level: 20');
    expect(rendered).toContain('Instance ID: pet-instance-9');
  });

  it('shows the selected Card’s own fields verbatim', async () => {
    const { harness } = await createViewer();

    harness.clickLabel('CARDS (3)');
    harness.objectContaining('Shield').click();

    const rendered = harness.rendered();
    expect(rendered).toContain('Card: Shield');
    expect(rendered).toContain('Category: Basic');
    expect(rendered).toContain('Card ID: card-shield');
  });

  it('shows a PetSkill Card’s category as the wire value it received', async () => {
    const { harness } = await createViewer({
      cards: [card('card-inferno', 'Inferno', 'PetSkill')],
    });

    harness.clickLabel('CARDS (1)');
    harness.objectContaining('Inferno').click();

    expect(harness.rendered()).toContain('Category: PetSkill');
  });

  it('shows the selected Relic’s own fields verbatim', async () => {
    const { harness } = await createViewer();

    harness.clickLabel('RELICS (3)');
    harness.objectContaining('Mana Crystal').click();

    const rendered = harness.rendered();
    expect(rendered).toContain('Relic: Mana Crystal');
    expect(rendered).toContain('Relic ID: relic-instance-2');
  });

  it('replaces the current selection when another item is clicked', async () => {
    const { harness, ctx } = await createViewer({
      pets: [pet('pet-1', 'Xích Lang'), pet('pet-2', 'Bạch Hổ')],
    });

    harness.objectContaining('Xích Lang').click();
    expect(harness.rendered()).toContain('Pet: Xích Lang');

    harness.objectContaining('Bạch Hổ').click();

    expect((ctx as { selectedItemId: string | null }).selectedItemId).toBe('pet-2');
    const rendered = harness.rendered();
    expect(rendered).toContain('Pet: Bạch Hổ');
    expect(rendered).not.toContain('Pet: Xích Lang');
  });

  it('clears the selection when the selected item is clicked again', async () => {
    const { harness, ctx } = await createViewer();

    harness.objectContaining('Xích Lang').click();
    harness.objectContaining('Xích Lang').click();

    expect((ctx as { selectedItemId: string | null }).selectedItemId).toBeNull();
    expect(harness.rendered()).toContain('Select an item to view details');
  });

  it('does not modify the collection it is presenting', async () => {
    const pets = [pet('pet-instance-1', 'Xích Lang')];
    const before = JSON.parse(JSON.stringify(pets));
    const { harness } = await createViewer({ pets });

    harness.objectContaining('Xích Lang').click();
    harness.clickLabel('CARDS (3)');
    harness.clickLabel('PETS (1)');

    // Nothing about the loaded response changed: the viewer reads and renders.
    expect(pets).toEqual(before);
  });
});

describe('CollectionViewerScene — empty categories', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows the empty Pets notice and no invented entry', async () => {
    const { harness } = await createViewer({ pets: [], cards: STARTER_CARDS, relics: STARTER_RELICS });

    const rendered = harness.rendered();
    expect(rendered).toContain('No owned Pets returned.');
    expect(rendered).toContain('PETS (0)');
    // No starter Pet is assumed to fill the gap.
    expect(rendered).not.toContain('Xích Lang');
  });

  it('shows the empty Cards notice', async () => {
    const { harness } = await createViewer({ cards: [] });

    harness.clickLabel('CARDS (0)');

    const rendered = harness.rendered();
    expect(rendered).toContain('No unlocked Cards returned.');
    expect(rendered).not.toContain('Heal');
  });

  it('shows the empty Relics notice', async () => {
    const { harness } = await createViewer({ relics: [] });

    harness.clickLabel('RELICS (0)');

    const rendered = harness.rendered();
    expect(rendered).toContain('No owned Relics returned.');
    expect(rendered).not.toContain('Berserker Core');
  });

  it('renders every category safely when all three collections are empty', async () => {
    const { harness } = await createViewer({ pets: [], cards: [], relics: [] });

    expect(harness.rendered()).toContain('No owned Pets returned.');
    harness.clickLabel('CARDS (0)');
    expect(harness.rendered()).toContain('No unlocked Cards returned.');
    harness.clickLabel('RELICS (0)');
    expect(harness.rendered()).toContain('No owned Relics returned.');

    // No rows at all, and no crash.
    expect(harness.rendered().split('\n').filter((line) => /^[○●] /.test(line))).toHaveLength(0);
  });
});

describe('CollectionViewerScene — error state and retry', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows the failure and an interactive RETRY', async () => {
    const { harness } = await createViewer({
      collectionFailure: new Error('Request to /api/pets failed with status 401'),
    });

    const rendered = harness.rendered();
    expect(rendered).toContain('Collection load failed');
    expect(rendered).toContain('401');
    expect(rendered).toContain('RETRY');
    expect(harness.objectContaining('RETRY').interactive).toBe(true);
  });

  it('draws no items and no counts while the load has failed', async () => {
    const { harness } = await createViewer({
      collectionFailure: new Error('Request to /api/relics failed with status 500'),
    });

    const rendered = harness.rendered();
    // A failed load produced no collection, so no count is claimed and no item
    // from a partial result is presented.
    expect(rendered).toContain('PETS');
    expect(rendered).not.toContain('PETS (');
    expect(rendered).not.toContain('Xích Lang');
    expect(rendered.split('\n').filter((line) => /^[○●] /.test(line))).toHaveLength(0);
  });

  it('re-reads the collection when RETRY is activated', async () => {
    const { harness } = await createViewer({
      collectionFailure: new Error('Request to /api/pets failed with status 500'),
      failFirstLoadOnly: true,
    });

    expect(harness.collectionReads).toHaveLength(3);
    expect(harness.rendered()).toContain('Collection load failed');

    harness.clickLabel('RETRY');
    await flush();

    // The retry performed the real reload...
    expect(harness.collectionReads).toHaveLength(6);
    expect(harness.collectionReads.slice(3)).toEqual(['getPets', 'getCards', 'getRelics']);

    // ...and the viewer now presents the collection that load returned.
    const rendered = harness.rendered();
    expect(rendered).not.toContain('Collection load failed');
    expect(rendered).not.toContain('RETRY');
    expect(rendered).toContain('Xích Lang');
    expect(rendered).toContain('PETS (1)');
  });

  it('shows the RETRY again when the retried load also fails', async () => {
    const { harness } = await createViewer({
      collectionFailure: new Error('Request to /api/cards failed with status 503'),
    });

    harness.clickLabel('RETRY');
    await flush();

    expect(harness.collectionReads).toHaveLength(6);
    expect(harness.rendered()).toContain('Collection load failed');
    expect(harness.rendered()).toContain('RETRY');
  });

  it('accumulates no duplicate RETRY handlers across repeated retries', async () => {
    const { harness } = await createViewer({
      collectionFailure: new Error('Request to /api/pets failed with status 500'),
    });

    for (let attempt = 0; attempt < 3; attempt++) {
      harness.clickLabel('RETRY');
      await flush();
    }

    // Four loads in total: the initial one plus three retries. Each retry issued
    // exactly one read per collection — no handler was registered twice, so no
    // retry fanned out into extra requests.
    expect(harness.collectionReads).toHaveLength(12);

    // And exactly one RETRY button is live.
    expect(harness.liveInteractive().filter((c) => c.text === 'RETRY')).toHaveLength(1);
  });

  it('does not transition anywhere when the load fails', async () => {
    const { harness } = await createViewer({
      collectionFailure: new Error('Request to /api/pets failed with status 500'),
    });

    expect(harness.sceneStarted).toEqual([]);
  });
});

describe('CollectionViewerScene — back navigation', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('returns to MainMenuScene through the scene transition', async () => {
    const { harness } = await createViewer();

    harness.clickLabel('< BACK');

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
  });

  it('transitions at most once for repeated activation', async () => {
    const { harness } = await createViewer();

    harness.clickLabel('< BACK');
    harness.clickLabel('< BACK');
    harness.clickLabel('< BACK');

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
  });

  it('offers the back action while loading and while failed', async () => {
    const failed = await createViewer({
      collectionFailure: new Error('Request to /api/pets failed with status 500'),
    });
    expect(failed.harness.objectContaining('< BACK').interactive).toBe(true);

    const deferred = createViewerHarness({ deferLoad: true });
    const scene = new CollectionViewerScene();
    const ctx = deferred.context(scene);
    runScene(scene, ctx, 'create');
    await flush();

    expect(deferred.objectContaining('< BACK').interactive).toBe(true);
  });
});

describe('CollectionViewerScene — teardown on the engine lifecycle (TASK-205)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('attaches its teardown to the engine lifecycle events, not to a method name', async () => {
    // `Phaser.Scene` declares no `shutdown` method and the engine never calls
    // one: `Systems#shutdown` / `#destroy` emit SHUTDOWN / DESTROY on the
    // scene's own emitter, so a teardown that exists only as a method is dead
    // code in the browser.
    const { harness, ctx } = await createViewer();

    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);
    expect(harness.sceneListenerCount(ctx, 'destroy')).toBe(1);
  });

  it('is safe to be taken down before it created anything', () => {
    const harness = createViewerHarness();
    const scene = new CollectionViewerScene();
    const ctx = harness.context(scene);

    // No `create()` ran, so nothing was registered and there is nothing to
    // release: the engine's teardown is still safe.
    expect(() => harness.shutdownScene(ctx)).not.toThrow();
    expect(() => harness.destroyScene(ctx)).not.toThrow();
  });

  it('releases its own references when Phaser shuts the scene down', async () => {
    const { harness, ctx } = await createViewer();

    expect(harness.liveInteractive().length).toBeGreaterThan(0);

    // The engine's own teardown path — not a call to the scene's method.
    harness.shutdownScene(ctx);

    // No object survives the shutdown: the shell, the tabs, the rows, the back
    // button, and any retry have all been released, and the scene's own lists of
    // them are empty rather than full of the ended run's objects.
    expect(harness.liveInteractive()).toHaveLength(0);
    expect((ctx as { renderedTexts: unknown[] }).renderedTexts).toHaveLength(0);
    expect((ctx as { interactiveObjects: unknown[] }).interactiveObjects).toHaveLength(0);
    expect((ctx as { shellObjects: unknown[] }).shellObjects).toHaveLength(0);
  });

  it('can fail on the defect: with no attached teardown the scene keeps everything', async () => {
    // The harness's own guard, in the spirit of TASK-204's. Phaser destroys the
    // stopped scene's children whether or not the scene registered a teardown,
    // but only the scene can drop its own references and lists — so a scene
    // whose teardown is not attached keeps the ended run's objects in
    // `shellObjects` and would carry them into the next run.
    const { harness, ctx } = await createViewer();

    harness.destroySceneDisplayList(ctx);

    expect((ctx as { shellObjects: unknown[] }).shellObjects).not.toHaveLength(0);
    expect((ctx as { ownedPets: unknown[] }).ownedPets).not.toHaveLength(0);
  });

  it('detaches the handlers of the shut-down pass', async () => {
    const { harness, ctx } = await createViewer();

    // Snapshot the objects of the live pass, then take the scene down.
    const staleBack = harness.objectContaining('< BACK');
    const stalePet = harness.objectContaining('Xích Lang');

    harness.shutdownScene(ctx);

    // A click on a destroyed object reaches no handler — the leak the cleanup
    // exists to prevent.
    staleBack.click();
    stalePet.click();

    expect(harness.sceneStarted).toEqual([]);
  });

  it('discards the loaded collection and the selection when Phaser shuts the scene down', async () => {
    const { harness, ctx } = await createViewer();

    harness.objectContaining('Xích Lang').click();
    expect((ctx as { selectedItemId: string | null }).selectedItemId).toBe('pet-instance-1');

    harness.shutdownScene(ctx);

    expect((ctx as { ownedPets: unknown[] }).ownedPets).toHaveLength(0);
    expect((ctx as { ownedCards: unknown[] }).ownedCards).toHaveLength(0);
    expect((ctx as { ownedRelics: unknown[] }).ownedRelics).toHaveLength(0);
    expect((ctx as { selectedItemId: string | null }).selectedItemId).toBeNull();
    expect((ctx as { tab: string }).tab).toBe('pets');
    expect((ctx as { loadError: string | null }).loadError).toBeNull();
  });

  it('discards the loaded collection and the selection when Phaser destroys the scene', async () => {
    const { harness, ctx } = await createViewer();

    harness.objectContaining('Xích Lang').click();
    harness.destroyScene(ctx);

    expect((ctx as { ownedPets: unknown[] }).ownedPets).toHaveLength(0);
    expect((ctx as { selectedItemId: string | null }).selectedItemId).toBeNull();
    expect((ctx as { shellObjects: unknown[] }).shellObjects).toHaveLength(0);
  });

  it('re-reads the collection and starts clean when the scene is reopened', async () => {
    const { harness, scene, ctx } = await createViewer();

    expect(harness.collectionReads).toHaveLength(3);

    harness.shutdownScene(ctx);
    runScene(scene, ctx, 'create');
    await flush();

    // The reopened viewer performed its own load...
    expect(harness.collectionReads).toHaveLength(6);

    // ...and its own shell was rebuilt from an empty set of lists rather than
    // stacking a second one on the ended run's.
    expect((ctx as { shellObjects: unknown[] }).shellObjects).toHaveLength(5);

    // ...and presents a single, fresh pass: one back button, one tab per
    // category, one row per Pet — no duplicated UI from the previous run.
    const live = harness.liveInteractive();
    expect(live.filter((c) => c.text === '< BACK')).toHaveLength(1);
    expect(live.filter((c) => c.text.startsWith('PETS'))).toHaveLength(1);
    expect(live.filter((c) => c.text.startsWith('CARDS'))).toHaveLength(1);
    expect(live.filter((c) => c.text.startsWith('RELICS'))).toHaveLength(1);
    expect(live.filter((c) => /^[○●] /.test(c.text))).toHaveLength(1);
    expect(live.filter((c) => c.text === 'Xích Lang' || c.text.includes('Xích Lang'))).toHaveLength(1);
  });

  it('does not accumulate lifecycle listeners across a shutdown/start cycle', async () => {
    const { harness, scene, ctx } = await createViewer();

    harness.shutdownScene(ctx);
    runScene(scene, ctx, 'create');
    await flush();

    // One teardown handler per run — never the previous run's plus a new one —
    // and the handler that is registered is the live one.
    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);
    expect(harness.sceneListenerCount(ctx, 'destroy')).toBe(1);

    harness.shutdownScene(ctx);
    expect((ctx as { shellObjects: unknown[] }).shellObjects).toHaveLength(0);
  });

  it('transitions at most once even when reopened after a back navigation', async () => {
    const { harness, scene, ctx } = await createViewer();

    harness.clickLabel('< BACK');
    expect(harness.sceneStarted).toHaveLength(1);

    harness.shutdownScene(ctx);
    runScene(scene, ctx, 'create');
    await flush();

    harness.clickLabel('< BACK');

    // One transition per run, and no second one carried over from the first.
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene', 'MainMenuScene']);
  });
});

describe('CollectionViewerScene — architectural boundaries (AGENTS.md §10, §13)', () => {
  /** The scene source with comments stripped, so prose naming a forbidden import is not a violation. */
  function source(): string {
    return readFileSync(
      resolve(__dirname, '../src/game/scenes/CollectionViewerScene.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');
  }

  it('imports no transport client, no SignalR, and no Discord SDK', () => {
    const code = source();

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
      expect(code, `CollectionViewerScene must not reference "${forbidden}"`).not.toContain(
        forbidden
      );
    }

    // `fetch` is not used as an HTTP client.
    expect(code).not.toMatch(/\bfetch\b/);
  });

  it('creates no SignalR connection and subscribes to no battle stream', () => {
    const code = source();

    for (const forbidden of [
      'onBattleEvents',
      'onBattleState',
      'onRuntimeEvent',
      'startBattle',
      'requestAction',
      'getBattleState',
      'BattleHub',
    ]) {
      expect(code, `CollectionViewerScene must not reference "${forbidden}"`).not.toContain(
        forbidden
      );
    }
  });

  it('reaches the collection only through the runtime port', () => {
    const code = source();

    expect(code).toContain('readRuntime(this)');

    // The three list reads the viewer needs, and no single-Pet detail read: §5.2
    // is not used, because the list the viewer already loaded carries every
    // §5.1 member it presents.
    for (const capability of ['runtime.getPets()', 'runtime.getCards()', 'runtime.getRelics()']) {
      expect(code).toContain(capability);
    }
    expect(code).not.toContain('getPet(');
  });

  it('never mutates or equips the collection it presents', () => {
    const code = source();

    // No equip/loadout member (§5.6) and no mutation verb exists here: the
    // viewer reads and renders.
    for (const forbidden of [
      'isEquipped',
      'loadoutPosition',
      'cardLoadout',
      'relicLoadout',
      'equip',
      'upgrade',
      'levelUp',
      'sell',
      'gacha',
      'craft',
      'trade',
      'dismantle',
    ]) {
      expect(code.toLowerCase()).not.toContain(forbidden.toLowerCase());
    }
  });

  it('computes no progression or gameplay value', () => {
    const code = source();

    for (const forbidden of ['damage', 'cascade', 'combo', 'power', 'rarity', 'combatPower']) {
      expect(code, `CollectionViewerScene must not reference "${forbidden}"`).not.toContain(
        forbidden
      );
    }
  });

  it('contains no client-side randomness', () => {
    const code = readFileSync(
      resolve(__dirname, '../src/game/scenes/CollectionViewerScene.ts'),
      'utf8'
    );

    expect(code).not.toMatch(/Math\.random/);
    expect(code).not.toMatch(/crypto\./);
  });
});
