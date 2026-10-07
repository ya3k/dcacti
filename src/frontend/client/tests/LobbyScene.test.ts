import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import {
  LobbyScene,
  LOBBY_BACK_BUTTON,
  LOBBY_RETRY_BUTTON,
  LOBBY_RELIC_PREV_BUTTON,
  LOBBY_RELIC_NEXT_BUTTON,
  MVP_BOSSES,
  SELECTION_TEXT_BOTTOM_OFFSET,
  REVIEW_TEXT_BOTTOM_OFFSET,
  MESSAGE_TEXT_BOTTOM_OFFSET,
  ERROR_TEXT_BOTTOM_OFFSET,
} from '../src/game/scenes/LobbyScene';
import type { LobbySceneData } from '../src/game/scenes/LobbyScene';
import { SAFE_AREA } from '../src/game/GameViewport';
import {
  clearPreservedLoadout,
  preserveLoadout,
  readPreservedLoadout,
} from '../src/game/state/PreservedLoadout';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
import { ApiRequestError } from '../src/services/api/ApiService';
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
  return {
    petId,
    identity,
    element: 'Fire',
    tier: 'Common',
    star: 1,
    level: 1,
    // §5.1: the Pet's derived Signature Skill reference — always present, and
    // never a member of the Card collection the Lobby selects from (§5.3).
    signatureSkill: { cardId: 'card-inferno', name: 'Inferno', category: 'PetSkill' },
  };
}

/** A `GET /api/cards` element (API_CONTRACTS.md §5.3). */
function card(
  cardId: string,
  name: string,
  category: 'Basic' | 'PetSkill',
  effectDefinition: CardResponse['effectDefinition'] = []
): CardResponse {
  return { cardId, name, category, effectDefinition };
}

/** A `GET /api/relics` element (API_CONTRACTS.md §5.4) — the owned instance. */
function relic(
  relicId: string,
  name: string,
  content: Partial<Pick<RelicResponse, 'trigger' | 'condition' | 'effectDefinition'>> = {}
): RelicResponse {
  return {
    relicId,
    name,
    trigger: content.trigger ?? 'OnMatchCount',
    condition: content.condition ?? null,
    effectDefinition: content.effectDefinition ?? [],
  };
}

/**
 * The starter consequences the server ships — the three Basic Cards'
 * `effectDefinition` (`CARD_RULES.md` §2) and the three starter-owned Relics'
 * §5.4 content (`RELIC_RULES.md` §6/§8.5).
 *
 * These are the **responses' own members**, transcribed so a rendering can be
 * asserted value by value. They are not a content source: `LobbyScene` holds no
 * per-id table, and every assertion over them is over text the scene produced
 * from the response it was handed.
 */
const STARTER_CARD_EFFECTS: Readonly<Record<string, CardResponse['effectDefinition']>> = {
  'card-heal': [{ effectType: 'Heal', valueType: 'PercentMaxHp', value: 20 }],
  'card-shield': [{ effectType: 'Shield', valueType: 'PercentMaxHp', value: 20 }],
  'card-power-charge': [{ effectType: 'Power', valueType: 'Flat', value: 25 }],
};

const STARTER_RELIC_CONTENT: Readonly<
  Record<string, Pick<RelicResponse, 'trigger' | 'condition' | 'effectDefinition'>>
> = {
  'relic-instance-1': {
    trigger: 'OnMatchCount',
    condition: { conditionType: 'MatchCountAtLeast', threshold: 3 },
    effectDefinition: [
      { effectType: 'ATK', valueType: 'Percentage', value: 5, target: 'Pet', lifetime: 'Battle' },
    ],
  },
  'relic-instance-2': {
    trigger: 'OnMatchCount',
    condition: { conditionType: 'MatchCountAtLeast', threshold: 4 },
    effectDefinition: [
      { effectType: 'Power', valueType: 'Flat', value: 10, target: 'Pet', lifetime: 'Immediate' },
    ],
  },
  'relic-instance-3': {
    trigger: 'OnCombo',
    condition: { conditionType: 'ComboAtLeast', threshold: 3 },
    effectDefinition: [
      {
        effectType: 'Crit',
        valueType: 'PercentagePoints',
        value: 10,
        target: 'Pet',
        lifetime: 'NextAttack',
      },
    ],
  },
};

/** The starter ownership DATABASE.md §2 records the server grants. */
const STARTER_PETS = [pet('pet-instance-1', 'Xích Lang')];
const STARTER_CARDS = [
  card('card-heal', 'Heal', 'Basic', STARTER_CARD_EFFECTS['card-heal']),
  card('card-shield', 'Shield', 'Basic', STARTER_CARD_EFFECTS['card-shield']),
  card('card-power-charge', 'Power Charge', 'Basic', STARTER_CARD_EFFECTS['card-power-charge']),
  // A PetSkill card is never submitted (`API_CONTRACTS.md` §3); it is present
  // here so the test can prove the scene never puts one in `cardLoadout`.
  card('card-inferno', 'Inferno', 'PetSkill'),
];
const STARTER_RELICS = [
  relic('relic-instance-1', 'Berserker Core', STARTER_RELIC_CONTENT['relic-instance-1']),
  relic('relic-instance-2', 'Mana Crystal', STARTER_RELIC_CONTENT['relic-instance-2']),
  relic('relic-instance-3', 'Assassin Eye', STARTER_RELIC_CONTENT['relic-instance-3']),
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
  /**
   * When given, the three collection reads stay in flight until it resolves —
   * the "loading" state, which several TASK-211 scenarios occupy deliberately.
   */
  collectionGate?: Promise<void>;
  startBehaviour?: (() => Promise<void>) | null;
}

function createLobbyHarness(options: SceneHarnessOptions = {}) {
  const {
    withRuntime = true,
    pets = STARTER_PETS,
    cards = STARTER_CARDS,
    relics = STARTER_RELICS,
    collectionGate,
    startBehaviour = null,
  } = options;

  /**
   * The failure the next collection read rejects with, or `null`.
   *
   * It is mutable because recovery is what is being tested: a read that fails
   * once and succeeds on `RETRY` is the documented recoverable failure, and a
   * fixed failure could only ever prove the first half of it.
   */
  let collectionFailure: Error | null = options.collectionFailure ?? null;

  const texts: string[] = [];
  /** Every clickable object the scene created, in creation order. */
  const clickables: Clickable[] = [];
  /**
   * The live text objects of the latest render pass. A render pass destroys the
   * previous pass's objects, so only the live ones are readable — the same thing
   * a player would see.
   */
  const liveTexts = new Set<{
    readonly text: string;
    readonly x?: number;
    readonly y?: number;
  }>();
  /** The live rectangles of the latest render pass (the shell and the buttons). */
  const liveRectangles = new Set<{ readonly text: string }>();
  /**
   * Every game object this harness created, so the engine's half of a scene
   * teardown can be modelled: Phaser's `DisplayList` handles the same SHUTDOWN
   * event by destroying each child, whether or not the scene released its own
   * references to them.
   */
  const createdObjects: Array<{ destroy: () => void }> = [];
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

  const doubleOf = <T,>(value: T): Promise<T> => {
    if (collectionFailure) {
      return Promise.reject(collectionFailure);
    }
    return collectionGate ? collectionGate.then(() => value) : Promise.resolve(value);
  };

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
    createdObjects.push(obj);
    return obj as unknown as Clickable;
  };

  /** A rectangle (the shell background and the controls). */
  const makeRectangle = (x = 0, y = 0, width = 0, height = 0): Clickable => {
    const handlers: Array<() => void> = [];
    let interactive = false;

    const obj = {
      x,
      y,
      width,
      height,
      /**
       * The caption a player reads on this control.
       *
       * The scene draws a control the way Phaser controls are conventionally
       * drawn: an interactive rectangle with its caption as a separate `Text` at
       * the same centre. The lookup is lazy, so the caption created after the
       * rectangle is found — and a control is identified by what it says rather
       * than by assuming which rectangle is which.
       */
      get text() {
        const caption = [...liveTexts].find((t) => t.x === x && t.y === y);
        return caption ? caption.text : '';
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
    createdObjects.push(obj);
    return obj as unknown as Clickable;
  };

  /**
   * The live interactive control a player reads as `label`, or a failure naming
   * the label that is missing.
   */
  const controlLabelled = (label: string): Clickable => {
    const found = clickables.filter(
      (c) => c.interactive && liveRectangles.has(c) && c.text === label
    );
    const control = found[found.length - 1];
    if (!control) {
      throw new Error(`LobbyScene rendered no "${label}" control.`);
    }
    return control;
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
     * `SceneManager` → `Systems#shutdown` → the engine's own `DisplayList`
     * destroys every child → `scene.events.emit(SHUTDOWN)`. Nothing here calls a
     * scene method by name.
     *
     * Modelling the display-list half is what makes "a reused scene accumulates
     * nothing" observable: Phaser destroys a stopped scene's game objects
     * whether or not the scene dropped its references, so a control that is live
     * after a restart can only be one the new run created.
     */
    shutdownScene: (ctx: object) => {
      for (const object of [...createdObjects]) {
        object.destroy();
      }
      (ctx as { events?: SceneEventEmitter }).events?.emit('shutdown');
    },
    /** `Phaser.Scenes.Systems#destroy` → `Phaser.Scenes.Events.DESTROY`. */
    destroyScene: (ctx: object) => {
      for (const object of [...createdObjects]) {
        object.destroy();
      }
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
    /** The live interactive control a player reads as `label`. */
    controlLabelled,
    /** The Start Battle trigger. */
    startTrigger: (): Clickable => controlLabelled('START BATTLE'),
    /** The `< BACK` control. */
    backControl: (): Clickable => controlLabelled('< BACK'),
    /** The `RETRY` control behind a failed, repeatable operation. */
    retryControl: (): Clickable => controlLabelled('RETRY'),
    /** Whether a control a player could read right now carries this label. */
    rendersControl: (label: string): boolean =>
      clickables.some((c) => c.interactive && liveRectangles.has(c) && c.text === label),
    /**
     * The captions of the live interactive controls, in creation order.
     *
     * A render pass destroys the previous pass's controls, so this is the set a
     * player could actually activate — which is what "a re-render accumulates no
     * control and no listener" has to be asserted against.
     */
    liveControlLabels: (): string[] =>
      clickables.filter((c) => c.interactive && liveRectangles.has(c)).map((c) => c.text),
    /** Sets (or clears) the failure the next collection read rejects with. */
    setCollectionFailure: (failure: Error | null) => {
      collectionFailure = failure;
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

  it('states what each Card changes (API_CONTRACTS.md §5.3)', async () => {
    const { harness } = await createLobby();

    const rendered = harness.rendered();

    // Each Card's own structured `effectDefinition`, rendered from the response
    // the read returned: identity, magnitude, and interpretation. Before this
    // existed the row communicated a name and a category only.
    expect(rendered).toContain('Heal 20% Max HP');
    expect(rendered).toContain('Shield 20% Max HP');
    expect(rendered).toContain('Power 25');

    // The identity line is unchanged, so the content is additive.
    expect(rendered).toContain('○ Heal  [Basic]');
  });

  it('states what each Relic changes and when (API_CONTRACTS.md §5.4)', async () => {
    const { harness } = await createLobby();

    const rendered = harness.rendered();

    // trigger + condition + effect, read from the response alone.
    expect(rendered).toContain('OnMatchCount MatchCountAtLeast 3 | ATK 5% (Pet, Battle)');
    expect(rendered).toContain('OnMatchCount MatchCountAtLeast 4 | Power 10 (Pet, Immediate)');
    expect(rendered).toContain('OnCombo ComboAtLeast 3 | Crit 10 percentage points (Pet, NextAttack)');
  });

  it('omits the condition segment for a Relic that declares none', async () => {
    const { harness } = await createLobby({
      relics: [
        relic('relic-instance-9', 'Burning Curse', {
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
        }),
      ],
    });

    const rendered = harness.rendered();

    // RELIC_RULES.md §8.1 item 4's nullable condition renders as no condition
    // segment — never a guessed form and never a fabricated threshold.
    expect(rendered).toContain('OnBattleStart | BurnDamage 30% (Pet, Battle)');
    expect(rendered).not.toContain('null');
    expect(rendered).not.toContain('undefined');
  });

  it('renders unknown content tokens verbatim rather than guessing', async () => {
    const { harness } = await createLobby({
      cards: [
        card('card-future', 'Future Card', 'Basic', [
          { effectType: 'FutureEffect', valueType: 'FutureUnit', value: 7 },
        ]),
      ],
      relics: [
        relic('relic-future', 'Future Relic', {
          trigger: 'OnFuture',
          condition: { conditionType: 'FutureForm', threshold: 9 },
          effectDefinition: [
            {
              effectType: 'FutureEffect',
              valueType: 'FutureUnit',
              value: 4,
              target: 'FutureTarget',
              lifetime: 'FutureLifetime',
            },
          ],
        }),
      ],
    });

    const rendered = harness.rendered();

    expect(rendered).toContain('FutureEffect 7 (FutureUnit)');
    expect(rendered).toContain('OnFuture FutureForm 9 | FutureEffect 4 (FutureUnit) (FutureTarget, FutureLifetime)');
    expect(rendered).not.toContain('undefined');
  });

  it('draws no content line when the response carries no usable rule', async () => {
    const { harness } = await createLobby({
      cards: [card('card-empty', 'Empty Card', 'Basic')],
      relics: [relic('relic-empty', 'Empty Relic', { trigger: '', condition: null })],
    });

    const rendered = harness.rendered();

    expect(rendered).toContain('○ Empty Card  [Basic]');
    expect(rendered).toContain('○ Empty Relic');
    // Nothing was invented to fill either row's empty rule.
    expect(rendered).not.toMatch(/\(Undetermined\)|\(\s*\)/);
  });

  it('treats a row’s content line as part of that option', async () => {
    const { harness, ctx } = await createLobby();

    const cardContent = harness.optionContaining('Heal 20% Max HP');
    expect(cardContent.interactive).toBe(true);
    cardContent.click();
    expect((ctx as { selectedCardIds: string[] }).selectedCardIds).toEqual(['card-heal']);

    const relicContent = harness.optionContaining('OnCombo ComboAtLeast 3');
    relicContent.click();
    expect((ctx as { selectedRelicIds: string[] }).selectedRelicIds).toEqual(['relic-instance-3']);
  });

  it('keeps every content line inside its own row', async () => {
    const { harness } = await createLobby();

    // Cards: line 1 at the row's own y, content at y + 15, at a 12 px font
    // (≈13 px line box). The pair must clear the next row's own line by at least
    // the documented minimum gap, so a 30 px pitch can never be overrun.
    const rows = [
      ['Heal  [Basic]', 'Heal 20% Max HP', 'Shield  [Basic]'],
      ['Shield  [Basic]', 'Shield 20% Max HP', 'Power Charge  [Basic]'],
      ['Berserker Core', 'OnMatchCount MatchCountAtLeast 3', 'Mana Crystal'],
      ['Mana Crystal', 'OnMatchCount MatchCountAtLeast 4', 'Assassin Eye'],
    ] as const;

    for (const [rowLabel, contentLabel, nextRowLabel] of rows) {
      const row = harness.optionContaining(rowLabel);
      const content = harness.optionContaining(contentLabel);
      const next = harness.optionContaining(nextRowLabel);

      expect(content.y).toBe((row.y ?? 0) + 15);
      // 13 px is the line box of the 12 px monospace content line; the next
      // row starts 30 px below this one's own line, so nothing can touch.
      expect(next.y!).toBeGreaterThanOrEqual((content.y ?? 0) + 13);
    }
  });

  it('keeps the last collection row clear of the Boss band', async () => {
    const { harness } = await createLobby();

    // Eight rows is the documented maximum the columns render; the Boss band
    // begins below `LIST_TOP + 8 * 30`, so even a full column's content lines
    // stay inside the list block.
    const lastRelic = harness.optionContaining('Assassin Eye');
    expect(lastRelic.y).toBeLessThan(108 + 8 * 30);
    expect(harness.optionContaining('OnCombo ComboAtLeast 3').y).toBe(
      (lastRelic.y ?? 0) + 15
    );
    expect(harness.rendered()).toContain('4. CHOOSE BOSS');
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
      collectionFailure: new ApiRequestError(
        401,
        'UNAUTHENTICATED',
        'An authenticated session is required.'
      ),
    });

    const rendered = harness.rendered();
    expect(rendered).toContain('Collection load failed: An authenticated session is required.');
    expect(harness.startRequests).toHaveLength(0);
    // The player is told what the server said, and never this client's HTTP
    // internals: no status, no request path (TASK-211 §4).
    expect(rendered).not.toContain('401');
    expect(rendered).not.toContain('/api/');
    expect(rendered).not.toContain('failed with status');
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

  /**
   * The documented rejections of `POST /api/battle/start`, each as the shared
   * transport now reports it: the §6 envelope's `message` is the statement a
   * player may be shown, and the machine code and status stay on the error
   * (`API_CONTRACTS.md` §3, §6, §2.8).
   */
  const documentedRejections: Array<[string, unknown]> = [
    [
      '401 UNAUTHENTICATED',
      new ApiRequestError(
        401,
        'UNAUTHENTICATED',
        'An authenticated session is required to start a battle.'
      ),
    ],
    ['400 INVALID_LOADOUT', new ApiRequestError(400, 'INVALID_LOADOUT', 'The Card loadout is invalid.')],
    [
      '400 PET_NOT_OWNED',
      new ApiRequestError(400, 'PET_NOT_OWNED', 'A Pet must be selected and it must be owned by the player.'),
    ],
    [
      '400 BOSS_NOT_FOUND',
      new ApiRequestError(400, 'BOSS_NOT_FOUND', 'The selected Boss is not a valid MVP Boss.'),
    ],
    ['transport failure', new Error('The connection to the game server was lost.')],
  ];

  it.each(documentedRejections)(
    'stays active with readable error feedback on a %s rejection',
    async (_case, failure) => {
      const { harness } = await createLobby({
        startBehaviour: async () => {
          throw failure;
        },
      });

      selectFullLoadout(harness);
      harness.startTrigger().click();
      await flush();

      // No transition, the error is displayed, and the selection survives so the
      // player can correct and retry.
      const rendered = harness.rendered();
      expect(harness.sceneStarted).toEqual([]);
      expect(rendered).toContain('Battle start failed');
      expect(rendered).toContain((failure as Error).message);
      expect(rendered).toContain('Pet:   pet-instance-1');
      expect(rendered).toContain('Cards: card-heal, card-shield, card-power-charge');

      // The player-facing line never leads with an HTTP status, a request path,
      // or a machine code (TASK-211 §4).
      expect(rendered).not.toContain('failed with status');
      expect(rendered).not.toContain('/api/');
      expect(rendered).not.toMatch(/\bUNAUTHENTICATED\b|\bINVALID_LOADOUT\b|\bPET_NOT_OWNED\b|\bBOSS_NOT_FOUND\b/);
    }
  );

  it('re-submits the same selection when the trigger is retried', async () => {
    const { harness } = await createLobby({
      startBehaviour: async () => {
        throw new ApiRequestError(400, 'INVALID_LOADOUT', 'The Card loadout is invalid.');
      },
    });

    selectFullLoadout(harness);
    harness.startTrigger().click();
    await flush();

    // The guard is released on rejection, so the player can retry.
    harness.startTrigger().click();
    await flush();

    expect(harness.rendered()).toContain('Battle start failed: The Card loadout is invalid.');
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

  it('holds no client-side Card or Relic content catalog', () => {
    const source = readFileSync(resolve(__dirname, '../src/game/scenes/LobbyScene.ts'), 'utf8')
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    // The loadout row text is a function of the response alone. A per-content
    // identity table here — or a hardcoded magnitude beside one — would be a
    // second source of truth (RELIC_RULES.md §8.2 item 1, GAME_STATE.md §0 item
    // 5, AGENTS.md §7/§9), so no provisioned identity may appear in the scene.
    for (const forbidden of [
      'card-heal',
      'card-shield',
      'card-power-charge',
      'relic-berserker-core',
      'relic-mana-crystal',
      'relic-assassin-eye',
    ]) {
      expect(source, `LobbyScene must not reference "${forbidden}"`).not.toContain(forbidden);
    }

    // The content vocabulary lives in the shared presentation module, which is
    // the one formatter both this scene and the Collection viewer use.
    expect(source).toContain('presentation/ContentEffectFormat');
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

describe('LobbyScene — exit, retry and asynchronous lifecycle (TASK-211 §1–§3)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** A deferred the test releases by hand. */
  function deferred(): { readonly promise: Promise<void>; readonly release: () => void } {
    let release: () => void = () => {};
    const promise = new Promise<void>((resolve) => {
      release = resolve;
    });
    return { promise, release };
  }

  /**
   * The loadout `ResultScene → PLAY AGAIN` hands back (ADR-022): a complete
   * selection, which is what makes a "click START during the read" scenario
   * meaningful rather than merely incomplete.
   */
  const PRESERVED: BattleStartRequest = {
    petId: 'pet-instance-1',
    bossId: 'boss-kim-loi-vuong',
    cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'],
    relicLoadout: ['relic-instance-1', 'relic-instance-2', 'relic-instance-3'],
  };

  it('renders a < BACK control in every state, and START BATTLE beside it', async () => {
    const ready = await createLobby();
    expect(ready.harness.rendersControl('< BACK')).toBe(true);
    expect(ready.harness.rendersControl('START BATTLE')).toBe(true);

    // Loading: the collection read has not settled.
    const gate = deferred();
    const loading = await createLobby({ collectionGate: gate.promise });
    expect(loading.harness.rendered()).toContain('Loading the collection…');
    expect(loading.harness.rendersControl('< BACK')).toBe(true);

    // Error: the start was rejected.
    const failed = await createLobby({
      startBehaviour: async () => {
        throw new ApiRequestError(400, 'INVALID_LOADOUT', 'The Card loadout is invalid.');
      },
    });
    selectFullLoadout(failed.harness);
    failed.harness.startTrigger().click();
    await flush();

    expect(failed.harness.rendersControl('< BACK')).toBe(true);
    expect(failed.harness.rendersControl('RETRY')).toBe(true);
  });

  it('returns to MainMenuScene from the ready state, and nowhere else', async () => {
    const { harness } = await createLobby();

    harness.backControl().click();

    // The one documented exit: not the Collection, not the Battle History, and
    // not a battle (`GDD.md` §2.1, `D-202-01 = C`).
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
    expect(harness.startRequests).toHaveLength(0);
  });

  it('returns to MainMenuScene while the collection read is still in flight', async () => {
    const gate = deferred();
    const { harness } = await createLobby({ collectionGate: gate.promise });

    expect(harness.rendered()).toContain('Loading the collection…');

    harness.backControl().click();

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
  });

  it('returns to MainMenuScene after a rejected start', async () => {
    const { harness } = await createLobby({
      startBehaviour: () => Promise.reject(new Error('The connection was lost.')),
    });

    selectFullLoadout(harness);
    harness.startTrigger().click();
    await flush();

    expect(harness.rendersControl('RETRY')).toBe(true);

    harness.backControl().click();

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
  });

  it('BACK invalidates an in-flight start attempt — a stale completion cannot navigate', async () => {
    /** Released by the test once the attempt is in flight. */
    let resolveStart: () => void = () => {};
    let attempts = 0;

    const harness = createLobbyHarness({
      startBehaviour: () => {
        attempts += 1;
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

    expect(attempts).toBe(1);

    // The player leaves while the request is outstanding.
    harness.backControl().click();
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);

    // The attempt the Lobby abandoned now succeeds. It must not preserve a
    // loadout and must not open a battle from a scene the player has left.
    resolveStart();
    await flush();

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
    expect(preservedIn(ctx)).toBeNull();
  });

  it('discards a collection read that settles after BACK', async () => {
    const gate = deferred();
    const { harness, ctx } = await createLobby({ collectionGate: gate.promise });

    expect((ctx as { ownedPets: unknown[] }).ownedPets).toHaveLength(0);

    harness.backControl().click();
    gate.release();
    await flush();

    // The read's own answer never reached the presentation that left.
    expect((ctx as { ownedPets: unknown[] }).ownedPets).toHaveLength(0);
    expect(harness.rendered()).not.toContain('Xích Lang');
  });

  it('RETRY behind a rejected start repeats the start operation, and can succeed', async () => {
    let attempts = 0;

    const harness = createLobbyHarness({
      startBehaviour: async () => {
        attempts += 1;
        if (attempts === 1) {
          throw new ApiRequestError(400, 'PET_NOT_OWNED', 'The selected Pet is not owned by the player.');
        }
      },
    });
    const scene = new LobbyScene();
    const ctx = harness.context(scene);
    runScene(scene, ctx, 'create');
    await flush();

    selectFullLoadout(harness);
    harness.startTrigger().click();
    await flush();

    expect(attempts).toBe(1);
    expect(harness.sceneStarted).toEqual([]);
    expect(harness.retryControl().interactive).toBe(true);

    harness.retryControl().click();
    await flush();

    // The retry submitted a second, complete attempt — the same selection — and
    // its success is what enters the battle.
    expect(attempts).toBe(2);
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene']);
    // The failed attempt preserved nothing; the successful one preserved the
    // loadout it actually submitted.
    expect(preservedIn(ctx)).toEqual({
      petId: 'pet-instance-1',
      bossId: 'boss-hoa-long',
      cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'],
      relicLoadout: ['relic-instance-1', 'relic-instance-2', 'relic-instance-3'],
    });
  });

  it('RETRY behind a failed collection read re-invokes that read exactly once', async () => {
    const { harness } = await createLobby({
      collectionFailure: new ApiRequestError(401, 'UNAUTHENTICATED', 'An authenticated session is required.'),
    });

    expect(harness.collectionReads).toEqual(['getPets', 'getCards', 'getRelics']);
    expect(harness.rendered()).toContain('Collection load failed');

    // The read recovers: the same control now completes the load and clears the
    // error, without a second concurrent read being issued.
    harness.setCollectionFailure(null);
    harness.retryControl().click();
    await flush();

    expect(harness.collectionReads).toEqual([
      'getPets',
      'getCards',
      'getRelics',
      'getPets',
      'getCards',
      'getRelics',
    ]);
    const rendered = harness.rendered();
    expect(rendered).not.toContain('Collection load failed');
    expect(rendered).not.toContain('401');
    expect(rendered).toContain('Xích Lang');
    expect(harness.rendersControl('RETRY')).toBe(false);
  });

  it('ignores repeated activation while the repeated start attempt is in flight', async () => {
    /** Released by the test once the retried attempt is in flight. */
    let resolveRetry: () => void = () => {};
    let attempts = 0;

    const harness = createLobbyHarness({
      startBehaviour: () => {
        attempts += 1;
        if (attempts === 1) {
          return Promise.reject(new Error('The connection was lost.'));
        }
        return new Promise<void>((resolve) => {
          resolveRetry = resolve;
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

    expect(attempts).toBe(1);
    expect(harness.rendersControl('RETRY')).toBe(true);

    // The retried attempt is now outstanding: the failure state — and with it
    // the RETRY control — is gone, and every further activation is inert.
    harness.retryControl().click();
    await flush();

    expect(attempts).toBe(2);
    expect(harness.rendersControl('RETRY')).toBe(false);

    harness.startTrigger().click();
    harness.startTrigger().click();
    await flush();

    expect(attempts).toBe(2);

    resolveRetry();
    await flush();

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene']);
  });

  it('cannot submit a start while the collection read is in flight', async () => {
    const gate = deferred();
    const harness = createLobbyHarness({ collectionGate: gate.promise });
    const scene = new LobbyScene();
    const ctx = harness.context(scene);

    // The `PLAY AGAIN` entry: a complete selection is already restored before
    // the read settles, so only the loading gate stops the submission.
    preserveLoadout(ctx as never, PRESERVED);
    runScene(scene, ctx, 'init', { restorePreservedLoadout: true } satisfies LobbySceneData);
    runScene(scene, ctx, 'create');

    expect(harness.rendered()).toContain('Pet:   pet-instance-1');
    expect(harness.rendered()).toContain('Loading the collection…');

    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests).toHaveLength(0);
    expect(harness.sceneStarted).toEqual([]);

    // Once the read settles the same control submits normally.
    gate.release();
    await flush();
    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests).toHaveLength(1);
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene']);
  });

  it('transitions once however often START is pressed', async () => {
    const { harness } = await createLobby();

    selectFullLoadout(harness);
    harness.startTrigger().click();
    harness.startTrigger().click();
    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests).toHaveLength(1);
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene']);
  });

  it('keeps the hint line informative on the error path', async () => {
    const { harness } = await createLobby({
      startBehaviour: async () => {
        throw new ApiRequestError(400, 'INVALID_LOADOUT', 'The Card loadout is invalid.');
      },
    });

    selectFullLoadout(harness);
    harness.startTrigger().click();
    await flush();

    const rendered = harness.rendered();
    // The guidance line does not go blank when the error line fills: it states
    // what the next attempt needs (TASK-211 NG-06).
    expect(rendered).toContain('START BATTLE — ready to retry.');
    expect(rendered).toContain('Battle start failed: The Card loadout is invalid.');
  });

  it('does not accumulate controls across a shutdown/start cycle', async () => {
    const { harness, scene, ctx } = await createLobby();

    expect(harness.liveControlLabels().sort()).toEqual(['< BACK', 'START BATTLE']);

    harness.shutdownScene(ctx);
    runScene(scene, ctx, 'create');
    await flush();

    // A reused instance holds exactly one of each control: the previous run's
    // objects were released by the render pass, not stacked on top of.
    expect(harness.liveControlLabels().sort()).toEqual(['< BACK', 'START BATTLE']);
    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);
    expect(harness.sceneListenerCount(ctx, 'destroy')).toBe(1);
  });

  it('leaves the preserved loadout untouched on Lobby → BACK → MainMenu', async () => {
    const harness = createLobbyHarness();
    const scene = new LobbyScene();
    const ctx = harness.context(scene);

    preserveLoadout(ctx as never, PRESERVED);
    runScene(scene, ctx, 'init', { restorePreservedLoadout: true } satisfies LobbySceneData);
    runScene(scene, ctx, 'create');
    await flush();

    harness.backControl().click();

    // The exit is not the post-result active-battle cleanup and does not own the
    // carrier: leaving the Lobby must not discard the loadout PLAY AGAIN
    // preserved (ADR-022, `D-202-04 = A`).
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
    expect(preservedIn(ctx)).toEqual(PRESERVED);
  });

  describe('control layout (TASK-211 §1)', () => {
    const SAFE_LEFT = SAFE_AREA.x;
    const SAFE_TOP = SAFE_AREA.y;
    const SAFE_RIGHT = SAFE_AREA.x + SAFE_AREA.width;
    const SAFE_BOTTOM = SAFE_AREA.y + SAFE_AREA.height;

    it('places both controls inside the safe area and clear of the trigger', () => {
      for (const control of [LOBBY_BACK_BUTTON, LOBBY_RETRY_BUTTON]) {
        expect(control.x - control.width / 2).toBeGreaterThanOrEqual(SAFE_LEFT);
        expect(control.x + control.width / 2).toBeLessThanOrEqual(SAFE_RIGHT);
        expect(control.y - control.height / 2).toBeGreaterThanOrEqual(SAFE_TOP);
        expect(control.y + control.height / 2).toBeLessThanOrEqual(SAFE_BOTTOM);
      }

      // The two controls do not overlap each other...
      const backLeft = LOBBY_BACK_BUTTON.x - LOBBY_BACK_BUTTON.width / 2;
      const retryRight = LOBBY_RETRY_BUTTON.x + LOBBY_RETRY_BUTTON.width / 2;
      expect(retryRight).toBeLessThan(backLeft);

      // ...and neither reaches the columns' title band or the START BATTLE row.
      const controlBottom = LOBBY_BACK_BUTTON.y + LOBBY_BACK_BUTTON.height / 2;
      expect(controlBottom).toBeLessThan(SAFE_AREA.y + 58);
      expect(controlBottom).toBeLessThan(578);
    });

    it('draws the controls where the exported layout says they are', async () => {
      const { harness } = await createLobby({
        startBehaviour: () => Promise.reject(new Error('The connection was lost.')),
      });

      selectFullLoadout(harness);
      harness.startTrigger().click();
      await flush();

      const back = harness.backControl();
      const retry = harness.retryControl();

      expect({ x: back.x, y: back.y, width: back.width, height: back.height }).toEqual({
        x: LOBBY_BACK_BUTTON.x,
        y: LOBBY_BACK_BUTTON.y,
        width: LOBBY_BACK_BUTTON.width,
        height: LOBBY_BACK_BUTTON.height,
      });
      expect({ x: retry.x, y: retry.y, width: retry.width, height: retry.height }).toEqual({
        x: LOBBY_RETRY_BUTTON.x,
        y: LOBBY_RETRY_BUTTON.y,
        width: LOBBY_RETRY_BUTTON.width,
        height: LOBBY_RETRY_BUTTON.height,
      });
    });
  });
});

/**
 * TASK-221 — the Relic column's capacity.
 *
 * `MVP_SCOPE.md` §1 grants a new account all ten MVP Relics, and the column grid
 * has room for eight rows before the Boss band begins. These tests are about the
 * consequence TASK-213 §3.2 C-2 recorded: every owned Relic must be reachable and
 * selectable, and the selection must remain the loadout the request carries —
 * with no client-side ownership or legality decision anywhere in it.
 */
describe('LobbyScene — the Relic column presents every owned instance (TASK-221)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** The five Pets the grant owns (`MVP_SCOPE.md` §1, `PET_RULES.md` §8). */
  const GRANTED_PETS: PetResponse[] = [
    pet('pet-instance-1', 'Xích Lang'),
    pet('pet-instance-2', 'Bạch Hổ'),
    pet('pet-instance-3', 'Huyền Quy'),
    pet('pet-instance-4', 'Thanh Xà'),
    pet('pet-instance-5', 'Sơn Hùng'),
  ];

  /** The ten Relic names in the order the grant declares them (`RELIC_RULES.md` §6). */
  const GRANTED_RELIC_NAMES = [
    'Berserker Core',
    'Mana Crystal',
    'Assassin Eye',
    'Emergency Core',
    'Burning Curse',
    'Combo Fang',
    'Arcane Battery',
    'Execution Mark',
    'Cascade Core',
    'Battle Instinct',
  ] as const;

  /**
   * The ten Relics the grant owns, each with its own delivered §5.4 content, so
   * a row is a realistic one: a second line whose text comes from the response
   * and never from this fixture's shape.
   */
  const GRANTED_RELICS: RelicResponse[] = GRANTED_RELIC_NAMES.map((name, index) =>
    relic(`relic-instance-${index + 1}`, name, {
      trigger: 'OnMatchCount',
      condition: { conditionType: 'MatchCountAtLeast', threshold: index + 1 },
      effectDefinition: [
        { effectType: 'ATK', valueType: 'Percentage', value: 5, target: 'Pet', lifetime: 'Battle' },
      ],
    })
  );

  /** A Lobby whose collection read returns the whole granted ownership set. */
  const createGrantedLobby = (options: SceneHarnessOptions = {}) =>
    createLobby({ pets: GRANTED_PETS, cards: STARTER_CARDS, relics: GRANTED_RELICS, ...options });

  /** The Relic rows the current page is showing, as their owned names. */
  const shownRelicNames = (harness: ReturnType<typeof createLobbyHarness>): string[] =>
    GRANTED_RELIC_NAMES.filter((name) =>
      harness
        .rendered()
        .split('\n')
        .some((line) => /^[○●] /.test(line) && line.includes(name))
    );

  it('shows one page of the owned Relics and offers the rest through its own controls', async () => {
    const { harness } = await createGrantedLobby();

    // Eight rows is the column grid's own capacity (the Boss band begins below
    // it), so the first page is the first eight owned instances.
    const shown = shownRelicNames(harness);
    expect(shown).toHaveLength(8);
    expect(shown).toEqual(GRANTED_RELIC_NAMES.slice(0, 8));

    // The two that do not fit are not silently dropped: they are one control
    // away, and the indicator says so.
    expect(harness.rendersControl('NEXT')).toBe(true);
    expect(harness.rendersControl('PREV')).toBe(true);
    expect(harness.rendered()).toContain('1-8 of 10');
  });

  it('reaches every one of the ten owned Relics by paging', async () => {
    const { harness } = await createGrantedLobby();

    const visited = new Set(shownRelicNames(harness));

    harness.controlLabelled('NEXT').click();

    expect(harness.rendered()).toContain('9-10 of 10');
    for (const name of shownRelicNames(harness)) {
      visited.add(name);
    }

    expect(shownRelicNames(harness)).toEqual(['Cascade Core', 'Battle Instinct']);

    // Every owned Relic was presented on some page — the requirement TASK-213
    // §3.2 C-2 records, and the whole point of the page.
    expect([...visited].sort()).toEqual([...GRANTED_RELIC_NAMES].sort());

    // And the way back is offered too.
    harness.controlLabelled('PREV').click();
    expect(harness.rendered()).toContain('1-8 of 10');
    expect(shownRelicNames(harness)).toEqual(GRANTED_RELIC_NAMES.slice(0, 8));
  });

  it('does not page past either end', async () => {
    const { harness } = await createGrantedLobby();

    // `PREV` on the first page and `NEXT` on the last are no-ops rather than a
    // wrap-around, so a player can never be shown an instance as if it were on
    // the far side of the column.
    harness.controlLabelled('PREV').click();
    expect(harness.rendered()).toContain('1-8 of 10');

    harness.controlLabelled('NEXT').click();
    harness.controlLabelled('NEXT').click();
    expect(harness.rendered()).toContain('9-10 of 10');
  });

  it('offers no paging at all when the whole column fits one page', async () => {
    // The starter fixture owns three Relics, so the column fits: a collection
    // that fits must render exactly as it did before paging existed, and no
    // control a player cannot use may be drawn.
    const { harness } = await createLobby();

    expect(harness.rendersControl('NEXT')).toBe(false);
    expect(harness.rendersControl('PREV')).toBe(false);
    expect(harness.rendered()).not.toContain('of 3');
  });

  it('keeps the selection when the page changes, in selection order', async () => {
    const { harness } = await createGrantedLobby();

    harness.optionContaining('Berserker Core').click();
    harness.controlLabelled('NEXT').click();
    harness.optionContaining('Battle Instinct').click();
    harness.optionContaining('Cascade Core').click();
    harness.controlLabelled('PREV').click();

    // Paging presents a different slice of the same owned set; it never edits
    // the selection, and the selection's own order stays the equip-slot order
    // (RELIC_RULES.md §2.3) even when one of its entries is off-page.
    expect(harness.rendered()).toContain(
      'Relics: relic-instance-1, relic-instance-10, relic-instance-9'
    );

    // The on-page selected row shows its own slot, and the off-page ones keep
    // the slots they were given.
    expect(harness.optionContaining('Berserker Core').text).toContain('slot 1');
    harness.controlLabelled('NEXT').click();
    expect(harness.optionContaining('Battle Instinct').text).toContain('slot 2');
    expect(harness.optionContaining('Cascade Core').text).toContain('slot 3');
  });

  it('submits exactly the three selected Relics of the ten owned', async () => {
    const { harness } = await createGrantedLobby();

    harness.optionContaining('Xích Lang').click();
    harness.optionContaining('Heal').click();
    harness.optionContaining('Shield').click();
    harness.optionContaining('Power Charge').click();
    harness.optionContaining('Hỏa Long').click();

    // One from the first page, two from the second: the smallest documented
    // Relic loadout (RELIC_RULES.md §2.1 item 1 — 3–5) built from instances that
    // are only reachable because the column pages.
    harness.optionContaining('Berserker Core').click();
    harness.controlLabelled('NEXT').click();
    harness.optionContaining('Cascade Core').click();
    harness.optionContaining('Battle Instinct').click();

    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests).toHaveLength(1);
    expect(harness.startRequests[0].relicLoadout).toEqual([
      'relic-instance-1',
      'relic-instance-9',
      'relic-instance-10',
    ]);
    // Exactly the four documented members, and nothing carrying the page.
    expect(Object.keys(harness.startRequests[0]).sort()).toEqual([
      'bossId',
      'cardLoadout',
      'petId',
      'relicLoadout',
    ]);
  });

  it('restores a ten-Relic loadout through PLAY AGAIN and keeps every entry editable', async () => {
    // The loadout the battle that just ended was fought with, including two
    // instances that live on the column's second page.
    const preserved: BattleStartRequest = {
      petId: 'pet-instance-1',
      bossId: 'boss-kim-loi-vuong',
      cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'],
      relicLoadout: ['relic-instance-9', 'relic-instance-10', 'relic-instance-8'],
    };

    const harness = createLobbyHarness({
      pets: GRANTED_PETS,
      cards: STARTER_CARDS,
      relics: GRANTED_RELICS,
    });
    const scene = new LobbyScene();
    const ctx = harness.context(scene);

    preserveLoadout(ctx as never, preserved);
    runScene(scene, ctx, 'init', { restorePreservedLoadout: true } satisfies LobbySceneData);
    runScene(scene, ctx, 'create');
    await flush();

    // Restoring is a starting point, not a lock: the review restates all three,
    // including the two the first page does not show.
    expect(harness.rendered()).toContain(
      'Relics: relic-instance-9, relic-instance-10, relic-instance-8'
    );
    expect(harness.rendered()).toContain('Relics: 3/5');

    // Paging to them shows them marked as the selected rows they are.
    harness.controlLabelled('NEXT').click();
    expect(harness.optionContaining('Cascade Core').text).toContain('●');
    expect(harness.optionContaining('Battle Instinct').text).toContain('●');

    // Editing the restored selection changes the outgoing loadout: deselect one
    // off-page instance, then pick a first-page one.
    harness.optionContaining('Cascade Core').click();
    harness.controlLabelled('PREV').click();
    harness.optionContaining('Arcane Battery').click();

    harness.startTrigger().click();
    await flush();

    expect(harness.startRequests).toEqual([
      {
        petId: 'pet-instance-1',
        bossId: 'boss-kim-loi-vuong',
        cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'],
        relicLoadout: ['relic-instance-10', 'relic-instance-8', 'relic-instance-7'],
      },
    ]);
  });

  it('discards the page with the rest of the in-progress state on teardown', async () => {
    const { harness, ctx } = await createGrantedLobby();

    harness.controlLabelled('NEXT').click();
    expect(harness.rendered()).toContain('9-10 of 10');

    harness.shutdownScene(ctx);

    // The page is this run's own presentation state, like the selection: a
    // shut-down Lobby keeps neither (ARCHITECTURE.md §2.2.3 rule 1).
    expect((ctx as { relicPage: number }).relicPage).toBe(0);
  });

  describe('page layout (TASK-221 §4 — inside the 1280×720 safe area)', () => {
    const SAFE_LEFT = SAFE_AREA.x;
    const SAFE_TOP = SAFE_AREA.y;
    const SAFE_RIGHT = SAFE_AREA.x + SAFE_AREA.width;
    const SAFE_BOTTOM = SAFE_AREA.y + SAFE_AREA.height;

    /** The columns' list block: the band the pager must stay clear of. */
    const LIST_TOP = SAFE_AREA.y + 84;

    it('places both page controls inside the safe area and clear of every row band', () => {
      for (const control of [LOBBY_RELIC_PREV_BUTTON, LOBBY_RELIC_NEXT_BUTTON]) {
        expect(control.x - control.width / 2).toBeGreaterThanOrEqual(SAFE_LEFT);
        expect(control.x + control.width / 2).toBeLessThanOrEqual(SAFE_RIGHT);
        expect(control.y - control.height / 2).toBeGreaterThanOrEqual(SAFE_TOP);
        expect(control.y + control.height / 2).toBeLessThanOrEqual(SAFE_BOTTOM);

        // The pager lives on the Relic column's header line, above the first
        // collection row, so it can never cover an option — on any page.
        expect(control.y + control.height / 2).toBeLessThan(LIST_TOP);
      }

      // The two controls do not overlap each other...
      expect(LOBBY_RELIC_PREV_BUTTON.x + LOBBY_RELIC_PREV_BUTTON.width / 2).toBeLessThan(
        LOBBY_RELIC_NEXT_BUTTON.x - LOBBY_RELIC_NEXT_BUTTON.width / 2
      );

      // ...and neither reaches the `< BACK` / `RETRY` band above them.
      const topControlsBottom = LOBBY_BACK_BUTTON.y + LOBBY_BACK_BUTTON.height / 2;
      expect(LOBBY_RELIC_PREV_BUTTON.y - LOBBY_RELIC_PREV_BUTTON.height / 2).toBeGreaterThan(
        topControlsBottom
      );

      // Nor the Relic column's own header literal, which is left-aligned at the
      // column origin: the pager sits in the free room to its right.
      const relicColumnX = SAFE_AREA.x + 26 + 372 * 2;
      expect(LOBBY_RELIC_PREV_BUTTON.x - LOBBY_RELIC_PREV_BUTTON.width / 2).toBeGreaterThan(
        relicColumnX + 200
      );
    });

    it('draws the page controls where the exported layout says they are', async () => {
      const { harness } = await createGrantedLobby();

      const prev = harness.controlLabelled('PREV');
      const next = harness.controlLabelled('NEXT');

      expect({ x: prev.x, y: prev.y, width: prev.width, height: prev.height }).toEqual({
        x: LOBBY_RELIC_PREV_BUTTON.x,
        y: LOBBY_RELIC_PREV_BUTTON.y,
        width: LOBBY_RELIC_PREV_BUTTON.width,
        height: LOBBY_RELIC_PREV_BUTTON.height,
      });
      expect({ x: next.x, y: next.y, width: next.width, height: next.height }).toEqual({
        x: LOBBY_RELIC_NEXT_BUTTON.x,
        y: LOBBY_RELIC_NEXT_BUTTON.y,
        width: LOBBY_RELIC_NEXT_BUTTON.width,
        height: LOBBY_RELIC_NEXT_BUTTON.height,
      });
    });

    it('keeps every rendered Relic row and its content line inside the list block', async () => {
      const { harness } = await createGrantedLobby();

      // The Boss band begins below `LIST_TOP + 8 * 30`, so the eighth row and its
      // content line stay inside the list block on every page.
      for (const page of [0, 1]) {
        if (page === 1) {
          harness.controlLabelled('NEXT').click();
        }

        const rows = GRANTED_RELIC_NAMES.filter((name) =>
          harness
            .rendered()
            .split('\n')
            .some((line) => /^[○●] /.test(line) && line.includes(name))
        );

        rows.forEach((name, index) => {
          const row = harness.optionContaining(name);
          expect(row.y).toBe(LIST_TOP + index * 30);
          expect((row.y ?? 0) + 15).toBeLessThan(LIST_TOP + 8 * 30);
        });
      }
    });
  });
});
