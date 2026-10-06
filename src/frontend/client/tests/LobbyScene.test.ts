import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import {
  LobbyScene,
  MVP_BOSSES,
  SELECTION_TEXT_BOTTOM_OFFSET,
  REVIEW_TEXT_BOTTOM_OFFSET,
  MESSAGE_TEXT_BOTTOM_OFFSET,
  ERROR_TEXT_BOTTOM_OFFSET,
} from '../src/game/scenes/LobbyScene';
import type { LobbySceneData } from '../src/game/scenes/LobbyScene';
import {
  clearPreservedLoadout,
  preserveLoadout,
  readPreservedLoadout,
} from '../src/game/state/PreservedLoadout';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
import { INITIAL_RUNTIME_STATE } from '../src/state/GameRuntimeState';
import type { BattleStartRequest } from '../src/services/api/BattleModels';
import type { CardResponse, PetResponse, RelicResponse } from '../src/services/api/CollectionModels';
import { SceneEventEmitter } from './support/SceneEventEmitter';

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
  /**
   * The Phaser game-wide registry's contents.
   *
   * It is one store per game instance — shared by every scene context this
   * harness builds, exactly as `game.registry` is in Phaser — which is what
   * makes the preserved-loadout carrier (ADR-022) reachable across scenes and
   * across a scene restart.
   */
  const registryValues = new Map<string, unknown>();

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

  /** A rectangle (the shell background and the Start Battle button). */
  const makeRectangle = (x = 0, y = 0, width = 0, height = 0): Clickable => {
    const handlers: Array<() => void> = [];
    let interactive = false;

    const obj = {
      x,
      y,
      width,
      height,
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
    // The scene's own emitter (Phaser's `scene.events`). The engine raises its
    // lifecycle events here and never calls a scene method because one exists
    // (TASK-205), so the harness raises them here too.
    const events = new SceneEventEmitter();

    return Object.assign(Object.create(scene), {
      events,
      scene: { start: (key: string) => sceneStarted.push({ key }) },
      add: {
        rectangle: (x: number, y: number, width: number, height: number) =>
          makeRectangle(x, y, width, height),
        text: (x: number, y: number, value: string) => makeText(x, y, value),
      },
      registry: {
        get: (key: string) => {
          if (withRuntime && key === RUNTIME_REGISTRY_KEY) {
            return runtime;
          }
          return registryValues.get(key);
        },
        set: (key: string, value: unknown) => {
          registryValues.set(key, value);
        },
        remove: (key: string) => {
          registryValues.delete(key);
        },
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
    /**
     * Takes a scene down exactly as Phaser does (TASK-205):
     * `SceneManager` → `Systems#shutdown` → `scene.events.emit(SHUTDOWN)`.
     * Nothing here calls a scene method by name.
     */
    shutdownScene: (ctx: object) => {
      (ctx as { events?: SceneEventEmitter }).events?.emit('shutdown');
    },
    /** `Phaser.Scenes.Systems#destroy` → `Phaser.Scenes.Events.DESTROY`. */
    destroyScene: (ctx: object) => {
      (ctx as { events?: SceneEventEmitter }).events?.emit('destroy');
    },
    /** How many listeners the scene's own emitter holds for one lifecycle event. */
    sceneListenerCount: (ctx: object, event: string) =>
      (ctx as { events?: SceneEventEmitter }).events?.listenerCount(event) ?? 0,
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
function runScene(scene: object, ctx: object, method: string, ...args: unknown[]): void {
  const fn = Object.getPrototypeOf(scene)[method] as
    | ((this: object, ...args: unknown[]) => void)
    | undefined;
  fn?.call(ctx, ...args);
}

/**
 * The preserved-loadout carrier as the scene reads and writes it (ADR-022).
 *
 * The accessors take the scene because that is where Phaser's game-wide
 * registry lives; the harness context carries the same registry.
 */
function preservedIn(ctx: object): BattleStartRequest | null {
  return readPreservedLoadout(ctx as never);
}

/** Runs `create()` and lets the collection reads settle. */
async function createLobby(options: SceneHarnessOptions = {}, startData?: LobbySceneData) {
  const harness = createLobbyHarness(options);
  const scene = new LobbyScene();
  const ctx = harness.context(scene);
  runScene(scene, ctx, 'init', startData);
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
 * Selects the full documented loadout (1 Pet / 3 Basic Cards / 3 Relics / 1 Boss)
 * through the scene's own option objects, using the collection's own labels.
 *
 * The Boss is chosen explicitly — there is no default selection to fall back on
 * (`MVP_BOSSES` starts unselected) — and the caller names which one, so a test
 * that cares about the Boss identity cannot be satisfied by a fixed value.
 */
function selectFullLoadout(
  harness: ReturnType<typeof createLobbyHarness>,
  bossDisplayName: string | null = 'Hỏa Long'
): void {
  harness.optionContaining('Xích Lang').click();
  harness.optionContaining('Heal').click();
  harness.optionContaining('Shield').click();
  harness.optionContaining('Power Charge').click();
  for (const label of ['Berserker Core', 'Mana Crystal', 'Assassin Eye']) {
    harness.optionContaining(label).click();
  }
  if (bossDisplayName !== null) {
    harness.optionContaining(bossDisplayName).click();
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
    expect(rendered).toContain('4. CHOOSE BOSS');
    expect(rendered).toContain('5. REVIEW');
  });

  it('offers exactly the five canonical MVP Bosses and nothing else', async () => {
    const { harness } = await createLobby();

    // BOSS_RULES.md §6 defines exactly five content-defined Bosses and §6.4 fixes
    // these identities; the presentation shows the §6 display names.
    expect(MVP_BOSSES.map((boss) => boss.bossId)).toEqual([
      'boss-hoa-long',
      'boss-thuy-ma',
      'boss-moc-yeu',
      'boss-son-thach-ve',
      'boss-kim-loi-vuong',
    ]);

    for (const boss of MVP_BOSSES) {
      expect(harness.rendered()).toContain(`${boss.displayName}  ${boss.element}`);
      // Each Boss is a real, interactive option.
      expect(harness.optionContaining(boss.displayName).interactive).toBe(true);
    }

    // No sixth entry, placeholder, or invented Boss: the rendered pass contains
    // exactly one option row per canonical Boss and no other Boss row.
    const bossLines = harness
      .rendered()
      .split('\n')
      .filter((line) => /^[○●] /.test(line));
    expect(bossLines.filter((line) => MVP_BOSSES.some((b) => line.includes(b.displayName)))).toHaveLength(5);
    expect(harness.rendered()).not.toContain('boss-def-');
  });

  it('selects no Boss until the player chooses one', async () => {
    const { harness } = await createLobby();

    // The initial state is `selectedBossId = null`: the review line and every
    // Boss option show an unfilled selection.
    expect(harness.rendered()).toContain('5. REVIEW — Boss: (none chosen)');
    for (const boss of MVP_BOSSES) {
      expect(harness.rendered()).toContain(`○ ${boss.displayName}  ${boss.element}`);
    }
  });

  it('selects each of the five Bosses, replacing the previous choice', async () => {
    const { harness } = await createLobby();

    for (const boss of MVP_BOSSES) {
      harness.optionContaining(boss.displayName).click();

      const rendered = harness.rendered();
      expect(rendered).toContain(`5. REVIEW — Boss: ${boss.bossId}`);
      // Exactly one Boss is marked selected — the one just chosen.
      expect(rendered).toContain(`● ${boss.displayName}  ${boss.element}`);
      for (const other of MVP_BOSSES.filter((entry) => entry.bossId !== boss.bossId)) {
        expect(rendered).toContain(`○ ${other.displayName}  ${other.element}`);
      }
    }
  });

  it('clears the Boss choice when the same Boss is tapped twice', async () => {
    const { harness } = await createLobby();

    harness.optionContaining('Kim Lôi Vương').click();
    expect(harness.rendered()).toContain('5. REVIEW — Boss: boss-kim-loi-vuong');

    harness.optionContaining('Kim Lôi Vương').click();
    expect(harness.rendered()).toContain('5. REVIEW — Boss: (none chosen)');
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

  it("submits the Boss the player selected, never a fixed identity", async () => {
    // The central case: a NON-default Boss must reach the request, so this is
    // not satisfied by the flow merely still succeeding.
    const { harness } = await createLobby();

    selectFullLoadout(harness);
    harness.optionContaining('Kim Lôi Vương').click();
    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests[0].bossId).toBe('boss-kim-loi-vuong');
  });

  it('submits each canonical Boss identity exactly, including the diacritic spellings', async () => {
    for (const boss of MVP_BOSSES) {
      const { harness } = await createLobby();

      // Everything but the Boss, then the Boss itself — chosen once, through its
      // own row, so re-selection cannot clear it.
      selectFullLoadout(harness, null);
      harness.optionContaining(boss.displayName).click();
      harness.startTrigger().click();
      await flush();

      // §6.4's exact values: no runtime slugification of the display name, no
      // dropped `boss-` prefix, and no `BossDefinitionId`.
      expect(harness.startRequests[0].bossId).toBe(boss.bossId);
    }
  });

  it('submits only the last-selected Boss when the selection is replaced', async () => {
    const { harness } = await createLobby();

    selectFullLoadout(harness);
    harness.optionContaining('Hỏa Long').click();
    harness.optionContaining('Thủy Ma').click();
    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests[0].bossId).toBe('boss-thuy-ma');
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
    harness.optionContaining('Sơn Thạch Vệ').click();

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
    harness.optionContaining('Thủy Ma').click();

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
    // A non-default Boss, so the submission proves the Boss member follows the
    // selection rather than a fixed value.
    harness.optionContaining('Mộc Yêu').click();

    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests[0]).toEqual({
      petId: 'own-pet-9',
      bossId: 'boss-moc-yeu',
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

  it('does not submit a complete loadout that has no Boss selected', async () => {
    const { harness } = await createLobby();

    // Everything except the Boss: no Boss is defaulted, so the request has no
    // documented `bossId` and must not be sent.
    harness.optionContaining('Xích Lang').click();
    harness.optionContaining('Heal').click();
    harness.optionContaining('Shield').click();
    harness.optionContaining('Power Charge').click();
    for (const label of ['Berserker Core', 'Mana Crystal', 'Assassin Eye']) {
      harness.optionContaining(label).click();
    }

    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests).toHaveLength(0);
    expect(harness.sceneStarted).toEqual([]);
    expect(harness.rendered()).toContain('Choose a Boss.');
    expect(harness.rendered()).toContain('5. REVIEW — Boss: (none chosen)');
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

describe('LobbyScene — teardown on the engine lifecycle (ARCHITECTURE.md §2.2.3 rules 1–2, TASK-205)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('attaches its teardown to the engine lifecycle events, not to a method name', async () => {
    // `Phaser.Scene` declares no `shutdown` method and the engine never calls
    // one: `Systems#shutdown` / `#destroy` emit SHUTDOWN / DESTROY on the
    // scene's own emitter, so a teardown that exists only as a method is dead
    // code in the browser — which is why the documented "discarded on shutdown"
    // was not true before this task.
    const { harness, ctx } = await createLobby();

    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);
    expect(harness.sceneListenerCount(ctx, 'destroy')).toBe(1);
  });

  it('is safe to be taken down before it created anything', () => {
    const harness = createLobbyHarness();
    const scene = new LobbyScene();
    const ctx = harness.context(scene);

    // No `create()` ran, so nothing was registered and there is nothing to
    // release: the engine's teardown is still safe.
    expect(() => harness.shutdownScene(ctx)).not.toThrow();
    expect(() => harness.destroyScene(ctx)).not.toThrow();
  });

  it('discards the in-progress selection when Phaser shuts the scene down', async () => {
    const { harness, scene, ctx } = await createLobby();

    selectFullLoadout(harness);
    expect(harness.rendered()).toContain('Cards: card-heal, card-shield, card-power-charge');

    // The engine's own teardown path — not a call to the scene's method.
    harness.shutdownScene(ctx);

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
    expect(rendered).toContain('5. REVIEW — Boss: (none chosen)');
    expect(rendered).toContain('Pet:   (none chosen)');
    expect(rendered).toContain('Cards: (none chosen)');
    expect(rendered).toContain('Relics: (none chosen)');

    // And nothing can be submitted from the discarded state.
    harness.startTrigger().click();
    await flush();
    expect(harness.startRequests).toHaveLength(0);
    expect(harness.sceneStarted).toEqual([]);
  });

  it('discards the in-progress selection when Phaser destroys the scene', async () => {
    const { harness, scene, ctx } = await createLobby();

    selectFullLoadout(harness);
    harness.destroyScene(ctx);

    expect((ctx as { selectedPetId: string | null }).selectedPetId).toBeNull();
    expect((ctx as { selectedBossId: string | null }).selectedBossId).toBeNull();
    expect((ctx as { selectedCardIds: string[] }).selectedCardIds).toEqual([]);
    expect((ctx as { selectedRelicIds: string[] }).selectedRelicIds).toEqual([]);

    runScene(scene, ctx, 'create');
    await flush();
    expect(harness.rendered()).toContain('Pet:   —');
  });

  it('detaches the previous render pass when the scene is restarted', async () => {
    const { harness, scene, ctx } = await createLobby();

    selectFullLoadout(harness);
    harness.shutdownScene(ctx);

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

    harness.shutdownScene(ctx);
    runScene(scene, ctx, 'create');
    await flush();

    expect(harness.collectionReads).toHaveLength(6);
  });

  it('does not accumulate lifecycle listeners across a shutdown/start cycle', async () => {
    const { harness, scene, ctx } = await createLobby();

    harness.shutdownScene(ctx);
    runScene(scene, ctx, 'create');
    await flush();

    // One teardown handler per run — never the previous run's plus a new one.
    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);

    // And the handler that is registered is the live one: taking the reused
    // scene down still discards its selection.
    selectFullLoadout(harness);
    harness.shutdownScene(ctx);
    expect((ctx as { selectedPetId: string | null }).selectedPetId).toBeNull();
  });
});

describe('LobbyScene — preserved loadout (TASK-203, D-202-03 = D, ADR-022)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /**
   * The loadout the battle that just ended was fought with, exactly as
   * `POST /api/battle/start` carried it (`API_CONTRACTS.md` §3): the owned Pet
   * instance, the Boss's canonical Identity, three Basic Cards, and three owned
   * Relic instances in equip-slot order.
   */
  const PRESERVED: BattleStartRequest = {
    petId: 'pet-instance-1',
    bossId: 'boss-kim-loi-vuong',
    cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'],
    relicLoadout: ['relic-instance-1', 'relic-instance-2', 'relic-instance-3'],
  };

  /**
   * Enters the Lobby the way the approved `ResultScene → PLAY AGAIN`
   * continuation does: the completed battle already published its loadout to the
   * carrier, and this scene instance is started with the restore flag.
   */
  async function enterThroughPlayAgain(preserved: BattleStartRequest = PRESERVED) {
    const harness = createLobbyHarness();
    const scene = new LobbyScene();
    const ctx = harness.context(scene);

    preserveLoadout(ctx as never, preserved);
    runScene(scene, ctx, 'init', { restorePreservedLoadout: true } satisfies LobbySceneData);
    runScene(scene, ctx, 'create');
    await flush();

    return { harness, scene, ctx };
  }

  it('restores the previous loadout and shows it through the Lobby UI', async () => {
    const { harness } = await enterThroughPlayAgain();

    const rendered = harness.rendered();
    expect(rendered).toContain('Pet:   pet-instance-1');
    expect(rendered).toContain('Cards: 3/3');
    expect(rendered).toContain('Relics: 3/5');
    expect(rendered).toContain('5. REVIEW — Boss: boss-kim-loi-vuong');
    expect(rendered).toContain('Cards: card-heal, card-shield, card-power-charge');
    expect(rendered).toContain('Relics: relic-instance-1, relic-instance-2, relic-instance-3');

    // The restored entries are the selected rows of the loaded collection, so
    // the player sees the loadout rather than a summary of it.
    expect(harness.optionContaining('Xích Lang').text).toContain('●');
    expect(harness.optionContaining('Kim Lôi Vương').text).toContain('●');
  });

  it('submits the restored loadout when the next battle is started', async () => {
    const { harness } = await enterThroughPlayAgain();

    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests).toEqual([PRESERVED]);
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene']);
  });

  it('keeps the restored loadout editable — the next battle uses the edited values', async () => {
    const { harness } = await enterThroughPlayAgain();

    // A different Boss, and a Relic re-picked last so the equip-slot order
    // changes: `RELIC_RULES.md` §2.3 makes position i slot i + 1, so the order
    // is part of the submitted loadout.
    harness.optionContaining('Hỏa Long').click();
    harness.optionContaining('Berserker Core').click();
    harness.optionContaining('Berserker Core').click();

    harness.startTrigger().click();
    await flush();

    // The preserved selection was a starting point, not a lock: the request
    // carries exactly the player's current selection and no validation of it
    // happened on the client (ARCHITECTURE.md §2.2.3 rule 5).
    expect(harness.startRequests).toEqual([
      {
        petId: 'pet-instance-1',
        bossId: 'boss-hoa-long',
        cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'],
        relicLoadout: ['relic-instance-2', 'relic-instance-3', 'relic-instance-1'],
      },
    ]);
  });

  it('does not restore on a normal entry — MainMenu → Lobby keeps today’s behavior', async () => {
    const harness = createLobbyHarness();
    const scene = new LobbyScene();
    const ctx = harness.context(scene);

    // A loadout is preserved (a battle happened), but this entry is not the
    // approved PLAY AGAIN return: MainMenuScene starts LobbyScene with no start
    // data, so the scene must not consult the carrier.
    preserveLoadout(ctx as never, PRESERVED);
    runScene(scene, ctx, 'init', undefined);
    runScene(scene, ctx, 'create');
    await flush();

    const rendered = harness.rendered();
    expect(rendered).toContain('Pet:   —');
    expect(rendered).toContain('Cards: 0/3');
    expect(rendered).toContain('5. REVIEW — Boss: (none chosen)');

    // And the carrier is left alone rather than consumed by the entry.
    expect(preservedIn(ctx)).toEqual(PRESERVED);
  });

  it('restores nothing when the carrier is empty', async () => {
    const harness = createLobbyHarness();
    const scene = new LobbyScene();
    const ctx = harness.context(scene);
    clearPreservedLoadout(ctx as never);

    runScene(scene, ctx, 'init', { restorePreservedLoadout: true } satisfies LobbySceneData);
    runScene(scene, ctx, 'create');
    await flush();

    // An empty carrier is not an error and is not a fallback loadout: the scene
    // simply opens unselected, exactly as a first-ever entry does.
    const rendered = harness.rendered();
    expect(rendered).toContain('Pet:   —');
    expect(rendered).toContain('Cards: 0/3');
    expect(rendered).toContain('5. REVIEW — Boss: (none chosen)');
    expect(preservedIn(ctx)).toBeNull();
  });

  it('preserves exactly the loadout a successful start submitted', async () => {
    const { harness, ctx } = await createLobby();
    expect(preservedIn(ctx)).toBeNull();

    selectFullLoadout(harness);
    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests).toHaveLength(1);
    // `D-202-03 = D`: the battle that just started is fought with this loadout,
    // so it is what PLAY AGAIN must restore (ADR-022).
    expect(preservedIn(ctx)).toEqual(harness.startRequests[0]);
    // And the copy is the carrier's own: the scene's later edits cannot rewrite
    // the preserved loadout behind its back.
    harness.optionContaining('Xích Lang').click();
    expect(preservedIn(ctx)).toEqual(harness.startRequests[0]);
  });

  it('preserves nothing when the start is rejected', async () => {
    const harness = createLobbyHarness({
      startBehaviour: () => Promise.reject(new Error('400 INVALID_LOADOUT')),
    });
    const scene = new LobbyScene();
    const ctx = harness.context(scene);

    // The loadout of the battle that actually happened, still preserved.
    preserveLoadout(ctx as never, PRESERVED);
    runScene(scene, ctx, 'init', { restorePreservedLoadout: true } satisfies LobbySceneData);
    runScene(scene, ctx, 'create');
    await flush();

    harness.optionContaining('Hỏa Long').click();
    harness.startTrigger().click();
    await flush();

    // No battle was created, so nothing was preserved for one: the carrier still
    // holds the battle that really ended (ARCHITECTURE.md §2.2.3 rule 5).
    expect(harness.sceneStarted).toEqual([]);
    expect(preservedIn(ctx)).toEqual(PRESERVED);
  });

  it('leaves the preserved loadout untouched on shutdown', async () => {
    const { harness, scene, ctx } = await enterThroughPlayAgain();

    harness.shutdownScene(ctx);

    // The scene's own copy of the selection is dropped...
    runScene(scene, ctx, 'init', undefined);
    runScene(scene, ctx, 'create');
    await flush();
    expect(harness.rendered()).toContain('Pet:   —');

    // ...but the carrier is not the scene's copy, and the scene's teardown is not
    // the post-result active-battle cleanup: neither clears it (`D-202-04 = A`'s
    // cleanup clears ACTIVE BATTLE state only).
    expect(preservedIn(ctx)).toEqual(PRESERVED);
  });

  it('reaches the carrier without any transport access', () => {
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/LobbyScene.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    // The carrier is client presentation state, read through its own accessor
    // (ADR-022, ARCHITECTURE.md §2.2.3) — never through the runtime or a
    // transport, and never through a persistent store.
    expect(source).toContain('readPreservedLoadout');
    expect(source).toContain('preserveLoadout');
    expect(source).not.toMatch(/localStorage|sessionStorage/);
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

  it('holds no Boss collection read and no Boss definition system', () => {
    const source = readFileSync(resolve(__dirname, '../src/game/scenes/LobbyScene.ts'), 'utf8')
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    // TASK-185: the Boss choice is scene-local selection state built from the
    // static `BOSS_RULES.md` §6.4 catalog. What stays forbidden is a Boss data
    // source of the client's own — a Boss read capability, a Boss response
    // model, or a Boss-definition/passive/combat subsystem
    // (ARCHITECTURE.md §2.2.3 rules 3–6, AGENTS.md §9/§10).
    expect(source).not.toMatch(/getBosses|getBoss\b|BossResponse|bossList|BossDefinition\b/);

    // The selection is one scene field holding one canonical identity, and it is
    // the only source of the submitted `bossId`.
    expect(source).toMatch(/private selectedBossId: string \| null = null;/);
    expect(source).toContain('bossId: this.selectedBossId as string');
  });

  it('carries no Boss gameplay value in its selection catalog', () => {
    const source = readFileSync(resolve(__dirname, '../src/game/scenes/LobbyScene.ts'), 'utf8')
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    // The catalog is presentation/selection metadata only (BOSS_RULES.md §6
    // owns stats, Passives, Skills, and magnitudes; the server is authoritative
    // for all of them). A stat, threshold, or passive value appearing here would
    // be a second Boss-definition system.
    for (const forbidden of [
      'maxHp',
      'enrage',
      'passive',
      'baseDamage',
      'chargeRequirement',
      'cooldownTurns',
    ]) {
      expect(source.toLowerCase()).not.toContain(forbidden.toLowerCase());
    }
  });

  describe('bottom text band layout (TASK-186)', () => {
    const LINE_HEIGHT = 15;
    const MIN_GAP = 4;
    const SAFE_AREA_BOTTOM = 696;
    const LAST_BOSS_ROW_BOTTOM = 531;

    it('spaces the bottom-left text blocks so they never overlap (AC-01..AC-03, AC-07, AC-09)', async () => {
      const { ctx } = await createLobby();

      const selection = (ctx as any).selectionText;
      const review = (ctx as any).reviewText;
      const message = (ctx as any).messageText;
      const error = (ctx as any).errorText;

      const selectionLines = selection.text.split('\n').length;
      const reviewLines = review.text.split('\n').length;
      const messageLines = Math.max(1, message.text.split('\n').length);
      const errorLines = Math.max(1, (error.text || '').split('\n').filter(Boolean).length);

      expect(selectionLines).toBe(3);
      expect(reviewLines).toBe(4);
      expect(messageLines).toBe(1);

      const selectionHeight = selectionLines * LINE_HEIGHT;
      const reviewHeight = reviewLines * LINE_HEIGHT;
      const messageHeight = messageLines * LINE_HEIGHT;
      const errorHeight = errorLines * LINE_HEIGHT;

      // AC-01: selectionText.bottom < reviewText.top with at least MIN_GAP
      expect(selection.y).toBeGreaterThanOrEqual(LAST_BOSS_ROW_BOTTOM + MIN_GAP);
      expect(review.y - selection.y).toBeGreaterThanOrEqual(selectionHeight + MIN_GAP);

      // AC-02: reviewText.bottom < messageText.top with at least MIN_GAP
      expect(message.y - review.y).toBeGreaterThanOrEqual(reviewHeight + MIN_GAP);

      // AC-03: messageText.bottom < errorText.top with at least MIN_GAP
      expect(error.y - message.y).toBeGreaterThanOrEqual(messageHeight + MIN_GAP);

      // AC-09: All blocks within safe area and errorText.top <= 670
      expect(error.y).toBeLessThanOrEqual(670);
      expect(error.y + errorHeight).toBeLessThanOrEqual(SAFE_AREA_BOTTOM);
      expect(message.y + messageHeight).toBeLessThanOrEqual(SAFE_AREA_BOTTOM);
      expect(review.y + reviewHeight).toBeLessThanOrEqual(SAFE_AREA_BOTTOM);
      expect(selection.y + selectionHeight).toBeLessThanOrEqual(SAFE_AREA_BOTTOM);

      // Direct offset verification against exported named constants
      expect(selection.y).toBe(SAFE_AREA_BOTTOM - SELECTION_TEXT_BOTTOM_OFFSET);
      expect(review.y).toBe(SAFE_AREA_BOTTOM - REVIEW_TEXT_BOTTOM_OFFSET);
      expect(message.y).toBe(SAFE_AREA_BOTTOM - MESSAGE_TEXT_BOTTOM_OFFSET);
      expect(error.y).toBe(SAFE_AREA_BOTTOM - ERROR_TEXT_BOTTOM_OFFSET);
    });

    it('proves that pre-fix offsets violate the non-overlapping contract', () => {
      // Historical offsets from safe area bottom (696):
      // selectionText: 696 - 132 = 564
      // reviewText:    696 - 96  = 600
      // messageText:   696 - 58  = 638
      // errorText:     696 - 26  = 670
      const oldSelectionY = SAFE_AREA_BOTTOM - 132;
      const oldReviewY = SAFE_AREA_BOTTOM - 96;
      const oldMessageY = SAFE_AREA_BOTTOM - 58;

      const selectionHeight = 3 * LINE_HEIGHT; // 45
      const reviewHeight = 4 * LINE_HEIGHT; // 60

      // Reverting selection/review gap produces 36 px < 45 px + 4 px (9 px overlap)
      expect(oldReviewY - oldSelectionY).toBeLessThan(selectionHeight + MIN_GAP);
      expect(oldReviewY - oldSelectionY).toBeLessThan(selectionHeight);

      // Reverting review/message gap produces 38 px < 60 px + 4 px (22 px overlap)
      expect(oldMessageY - oldReviewY).toBeLessThan(reviewHeight + MIN_GAP);
      expect(oldMessageY - oldReviewY).toBeLessThan(reviewHeight);
    });

    it('maintains non-overlapping geometry across dynamic selection updates and error states', async () => {
      const { harness, scene, ctx } = await createLobby();

      selectFullLoadout(harness);

      const selection = (ctx as any).selectionText;
      const review = (ctx as any).reviewText;
      const message = (ctx as any).messageText;
      const error = (ctx as any).errorText;

      const selectionLines = selection.text.split('\n').length;
      const reviewLines = review.text.split('\n').length;
      const messageLines = Math.max(1, message.text.split('\n').length);

      expect(selectionLines).toBe(3);
      expect(reviewLines).toBe(4);
      expect(messageLines).toBe(1);

      expect(review.y - selection.y).toBeGreaterThanOrEqual(selectionLines * LINE_HEIGHT + MIN_GAP);
      expect(message.y - review.y).toBeGreaterThanOrEqual(reviewLines * LINE_HEIGHT + MIN_GAP);

      // Now populate errorText
      (ctx as any).startError = 'Server connection failed.';
      runScene(scene, ctx, 'render');

      const errorLines = Math.max(1, (error.text || '').split('\n').filter(Boolean).length);
      expect(error.text).toBe('Server connection failed.');
      expect(error.y - message.y).toBeGreaterThanOrEqual(messageLines * LINE_HEIGHT + MIN_GAP);
      expect(error.y + errorLines * LINE_HEIGHT).toBeLessThanOrEqual(SAFE_AREA_BOTTOM);
    });

    it('guarantees that no bottom text block intersects the START BATTLE trigger rectangle (AC-05, AC-09)', async () => {
      const { harness, ctx } = await createLobby();
      const trigger = harness.startTrigger();

      expect(trigger.x).toBeDefined();
      expect(trigger.y).toBeDefined();
      expect(trigger.width).toBe(300);
      expect(trigger.height).toBe(44);

      // Trigger center (1080, 600) -> [930..1230, 578..622]
      const triggerLeft = (trigger.x as number) - (trigger.width as number) / 2;
      const triggerRight = (trigger.x as number) + (trigger.width as number) / 2;
      const triggerTop = (trigger.y as number) - (trigger.height as number) / 2;
      const triggerBottom = (trigger.y as number) + (trigger.height as number) / 2;

      expect(triggerLeft).toBe(930);
      expect(triggerRight).toBe(1230);
      expect(triggerTop).toBe(578);
      expect(triggerBottom).toBe(622);

      const review = (ctx as any).reviewText;
      const message = (ctx as any).messageText;
      const error = (ctx as any).errorText;

      // Lines of reviewText that could extend horizontally towards the button:
      // Line 4 (Relics) starts at review.y + 3 * LINE_HEIGHT = 585 + 45 = 630.
      const reviewLine4Top = review.y + 3 * LINE_HEIGHT;
      expect(reviewLine4Top).toBeGreaterThan(triggerBottom);

      // Message and error blocks are strictly below the trigger
      expect(message.y).toBeGreaterThan(triggerBottom);
      expect(error.y).toBeGreaterThan(triggerBottom);
    });
  });
});

