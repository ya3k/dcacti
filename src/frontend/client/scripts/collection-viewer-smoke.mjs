/**
 * TASK-190: Collection Viewer Browser Smoke Test.
 *
 * Verifies the read-only meta-progression viewer journey end to end, over the
 * repository's existing native CDP browser automation (the harness TASK-189
 * established in `standalone-web-smoke.mjs` — no Playwright/Puppeteer):
 *
 *   Clean Context
 *     ↓
 *   AuthScreen (Register unique account)
 *     ↓
 *   MainMenuScene (real pointer click on COLLECTION at (640, 394))
 *     ↓
 *   CollectionViewerScene (PETs default tab → starter Pet rendered)
 *     ↓
 *   CARDS tab (real pointer click → 3 starter Cards rendered)
 *     ↓
 *   RELICS tab (real pointer click → 3 starter Relics rendered)
 *     ↓
 *   Item Detail (real pointer click on an item → its wire fields rendered)
 *     ↓
 *   < BACK (real pointer click → MainMenuScene active again)
 *     ↓
 *   Zero Fatal Runtime Errors & Zero Discord Dependencies
 *
 * Scope: this is the collection journey only. It deliberately does not repeat
 * the TASK-189 battle flow — `standalone-web-smoke.mjs` owns that, and it is run
 * separately as the regression safety net.
 *
 * Usage:
 *   node scripts/collection-viewer-smoke.mjs [url]
 */

import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const FRONTEND_URL = process.argv[2] || process.env.URL_UNDER_TEST || 'http://localhost:5173/';
const BACKEND_URL = process.env.BACKEND_URL || 'http://localhost:5000';
const DEBUG_PORT = Number(process.env.DEBUG_PORT) || 9291;
const OUT_DIR = 'collection-viewer-shots';

/**
 * The starter grant DATABASE.md §2 records the server makes (TASK-083, TASK-187):
 * 1 Pet, 3 Basic Cards, 3 Relics. These are the deterministic expectations the
 * assertions below are written against.
 */
const STARTER_PET_IDENTITY = 'Xích Lang';
const STARTER_CARD_NAMES = ['Heal', 'Shield', 'Power Charge'];
const STARTER_RELIC_NAMES = ['Berserker Core', 'Mana Crystal', 'Assassin Eye'];

/** The MainMenu button centres TASK-190 fixes, in logical game pixels. */
const START_BATTLE_BUTTON = { x: 640, y: 324, width: 280, height: 50 };
const COLLECTION_BUTTON = { x: 640, y: 394, width: 280, height: 50 };

/** Standard candidate paths for Edge / Chrome on Windows / Linux. */
const BROWSER_CANDIDATES = [
  process.env.EDGE_BIN,
  process.env.CHROME_BIN,
  'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
  'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
  '/usr/bin/google-chrome',
  '/usr/bin/microsoft-edge',
  '/usr/bin/chromium-browser',
  '/usr/bin/chromium',
].filter(Boolean);

class Cdp {
  constructor(ws) {
    this.ws = ws;
    this.id = 0;
    this.pending = new Map();
    this.listeners = new Map();

    ws.addEventListener('message', (event) => {
      const msg = JSON.parse(event.data);
      if (msg.id && this.pending.has(msg.id)) {
        const { resolve, reject } = this.pending.get(msg.id);
        this.pending.delete(msg.id);
        if (msg.error) {
          reject(new Error(JSON.stringify(msg.error)));
        } else {
          resolve(msg.result);
        }
      } else if (msg.method) {
        const handler = this.listeners.get(msg.method);
        if (handler) handler(msg.params);
      }
    });
  }

  static async connect(wsUrl) {
    const ws = new WebSocket(wsUrl);
    await new Promise((resolve, reject) => {
      ws.addEventListener('open', resolve, { once: true });
      ws.addEventListener('error', reject, { once: true });
    });
    return new Cdp(ws);
  }

  on(method, fn) {
    this.listeners.set(method, fn);
  }

  send(method, params = {}) {
    const id = ++this.id;
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.ws.send(JSON.stringify({ id, method, params }));
    });
  }

  close() {
    try {
      this.ws.close();
    } catch {
      /* ignore */
    }
  }
}

function resolveBrowserPath() {
  for (const candidate of BROWSER_CANDIDATES) {
    if (existsSync(candidate)) {
      return candidate;
    }
  }
  throw new Error(
    `No supported Chromium browser found. Checked:\n  ${BROWSER_CANDIDATES.join('\n  ')}\nSet EDGE_BIN or CHROME_BIN environment variable.`
  );
}

async function checkPreflightHealth() {
  console.log('--- Preflight Health Checks ---');
  try {
    const res = await fetch(`${BACKEND_URL}/health`, { signal: AbortSignal.timeout(3000) });
    if (!res.ok) throw new Error(`Status ${res.status}`);
    console.log(`  [OK] Backend healthy at ${BACKEND_URL}/health`);
  } catch (err) {
    throw new Error(
      `Backend service not reachable at ${BACKEND_URL}/health (${err.message}).\n` +
        `Ensure backend is running:\n` +
        `  dotnet run --project src/backend/GameServer.Api/GameServer.Api.csproj`
    );
  }

  try {
    const res = await fetch(FRONTEND_URL, { signal: AbortSignal.timeout(3000) });
    if (!res.ok) throw new Error(`Status ${res.status}`);
    console.log(`  [OK] Frontend reachable at ${FRONTEND_URL}`);
  } catch (err) {
    throw new Error(
      `Frontend dev server not reachable at ${FRONTEND_URL} (${err.message}).\n` +
        `Ensure Vite is running in src/frontend/client:\n` +
        `  npm run dev`
    );
  }
}

async function waitForDevTools(port) {
  for (let i = 0; i < 80; i++) {
    try {
      const res = await fetch(`http://127.0.0.1:${port}/json/version`);
      if (res.ok) return await res.json();
    } catch {
      /* not ready yet */
    }
    await delay(250);
  }
  throw new Error(`DevTools endpoint http://127.0.0.1:${port} did not become available`);
}

async function evaluate(cdp, expression) {
  const result = await cdp.send('Runtime.evaluate', {
    expression,
    returnByValue: true,
    awaitPromise: true,
  });
  if (result.exceptionDetails) {
    throw new Error(JSON.stringify(result.exceptionDetails));
  }
  return result.result.value;
}

async function waitForCondition(predicateFn, description, timeoutMs = 15000, intervalMs = 200) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const res = await predicateFn();
      if (res) return res;
    } catch {
      /* continue polling */
    }
    await delay(intervalMs);
  }
  throw new Error(`Timeout waiting for condition: ${description} (${timeoutMs}ms)`);
}

/** Trusted left-button click with move, press, release. */
async function realClick(cdp, point) {
  const base = { x: Math.round(point.x), y: Math.round(point.y), button: 'left', clickCount: 1 };
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mouseMoved', buttons: 0 });
  await delay(80);
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mousePressed', buttons: 1 });
  await delay(60);
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mouseReleased', buttons: 0 });
  await delay(150);
}

/**
 * Clicks a point and waits for the click to take effect.
 *
 * Phaser maps a pointer into game space from the canvas bounds the Scale Manager
 * last measured, and that measurement settles a frame or two after the parent is
 * laid out. A click dispatched inside that window is mapped against stale bounds
 * and lands nowhere, so this retries the same point until `predicate` holds —
 * the same retry-until-observed convention `standalone-web-smoke.mjs` uses for
 * its own MainMenu click.
 *
 * The retry re-reads the point from `pointFn` each time, so a control that moved
 * (because the layout settled) is still addressed where it now is.
 */
async function clickUntil(cdp, pointFn, predicate, description, attempts = 6) {
  for (let attempt = 1; attempt <= attempts; attempt++) {
    await realClick(cdp, await pointFn());
    try {
      const result = await waitForCondition(predicate, description, 1200, 150);
      return result;
    } catch {
      /* the click did not take effect yet — re-address and try again */
    }
  }
  // Final attempt, so the caller reports the underlying timeout rather than a
  // swallowed failure.
  await realClick(cdp, await pointFn());
  return waitForCondition(predicate, description, 8000, 200);
}

/** Fills a controlled React input with real pointer focus + native value dispatch. */
async function fillInput(cdp, selector, value) {  const point = await evaluate(cdp, `(() => {
    const el = document.querySelector('${selector}');
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
  })()`);
  if (!point) {
    throw new Error(`Input element not found: ${selector}`);
  }

  await realClick(cdp, point);

  await evaluate(cdp, `(() => {
    const el = document.querySelector('${selector}');
    if (!el) return;
    el.focus();
    const set = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
    set.call(el, ${JSON.stringify(value)});
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  })()`);
}

async function captureScreenshot(cdp, name) {
  try {
    mkdirSync(OUT_DIR, { recursive: true });
    const png = await cdp.send('Page.captureScreenshot', { format: 'png' });
    writeFileSync(join(OUT_DIR, `${name}.png`), Buffer.from(png.data, 'base64'));
  } catch {
    /* screenshot failure is non-fatal */
  }
}

/** React Fiber walker to attach window.__game. */
const CAPTURE_GAME = `
(() => {
  if (window.__game) return true;
  const root = document.getElementById('root');
  const key = root && Object.getOwnPropertyNames(root).find((k) => k.startsWith('__reactContainer$'));
  if (!key) return false;
  const isGame = (v) => !!v && typeof v === 'object' && v.scene && typeof v.scene.getScene === 'function';
  const visited = new Set(); const stack = [root[key]]; let seen = 0;
  while (stack.length && seen < 20000 && !window.__game) {
    const f = stack.pop(); if (!f || visited.has(f)) continue; visited.add(f); seen++;
    let h = f.memoizedState, d = 0;
    while (h && d < 60) { const s = h.memoizedState;
      if (isGame(s)) { window.__game = s; break; }
      if (s && typeof s === 'object' && isGame(s.current)) { window.__game = s.current; break; }
      h = h.next; d++; }
    if (f.child) stack.push(f.child); if (f.sibling) stack.push(f.sibling);
  }
  return !!window.__game;
})()
`;

const ACTIVE_SCENE = `
(() => {
  if (!window.__game) return null;
  for (const key of ['MainMenuScene', 'CollectionViewerScene', 'LobbyScene', 'BattleScene', 'ResultScene']) {
    const s = window.__game.scene.getScene(key);
    if (s && s.scene && s.scene.isActive && s.scene.isActive()) return key;
  }
  return 'none';
})()
`;

/**
 * The MainMenu's interactive hit areas, in logical game coordinates.
 *
 * Read from the live scene rather than assumed, so the test clicks the button
 * the game actually drew. The caption of each button is the text object drawn at
 * the same centre.
 */
const MAIN_MENU_SNAPSHOT = `
(() => {
  const s = window.__game && window.__game.scene.getScene('MainMenuScene');
  if (!s || !s.scene.isActive()) return { active: false };
  const canvas = document.querySelector('canvas');
  const rect = canvas.getBoundingClientRect();
  const gameWidth = window.__game.scale.gameSize.width;
  const gameHeight = window.__game.scale.gameSize.height;
  const texts = s.children.list.filter((o) => o.type === 'Text').map((o) => ({ text: o.text, x: o.x, y: o.y }));
  return {
    active: true,
    buttons: s.children.list
      .filter((o) => o.type === 'Rectangle' && o.input && o.input.enabled)
      .map((o) => ({ x: o.x, y: o.y, width: o.width, height: o.height,
        label: (texts.find((t) => Math.abs(t.x - o.x) < 1 && Math.abs(t.y - o.y) < 1) || {}).text || null })),
    viewport: { left: rect.left, top: rect.top, scaleX: rect.width / gameWidth, scaleY: rect.height / gameHeight },
  };
})()
`;

/**
 * The CollectionViewerScene's observable presentation state.
 *
 * `rows` are the item list's own text objects (each starts with the selected/
 * unselected marker, exactly as the scene draws them), `tabs` are the three
 * category tabs, and `detail` is the detail panel's text. Reading the scene's
 * own objects keeps the assertions on what is rendered rather than on a
 * parallel copy of it.
 */
const COLLECTION_SNAPSHOT = `
(() => {
  const s = window.__game && window.__game.scene.getScene('CollectionViewerScene');
  if (!s || !s.scene.isActive()) return { active: false };
  const canvas = document.querySelector('canvas');
  const rect = canvas.getBoundingClientRect();
  const gameWidth = window.__game.scale.gameSize.width;
  const gameHeight = window.__game.scale.gameSize.height;
  const list = s.children.list || [];
  const live = (o) => list.indexOf(o) !== -1;
  return {
    active: true,
    tab: s.tab,
    selectedItemId: s.selectedItemId === undefined ? null : s.selectedItemId,
    loading: !!s.loading,
    loadError: s.loadError === null || s.loadError === undefined ? null : s.loadError,
    ownedPets: (s.ownedPets || []).map((p) => ({ petId: p.petId, identity: p.identity, element: p.element, tier: p.tier, star: p.star, level: p.level })),
    ownedCards: (s.ownedCards || []).map((c) => ({ cardId: c.cardId, name: c.name, category: c.category })),
    ownedRelics: (s.ownedRelics || []).map((r) => ({ relicId: r.relicId, name: r.name })),
    rows: list.filter((o) => o.type === 'Text' && live(o) && /^[○●] /.test(o.text)).map((o) => o.text),
    tabs: list.filter((o) => o.type === 'Text' && live(o) && /^(PETS|CARDS|RELICS)( \\(\\d+\\))?$/.test(o.text)).map((o) => o.text),
    detail: s.detailText && typeof s.detailText.text === 'string' ? s.detailText.text : '',
    status: s.statusText && typeof s.statusText.text === 'string' ? s.statusText.text : '',
    backButton: list.some((o) => o.type === 'Rectangle' && live(o) && o.input && o.input.enabled &&
      Math.abs(o.x - (24 + 20 + 70)) < 1 && Math.abs(o.y - (24 + 20 + 18)) < 1),
    interactive: list.filter((o) => o.input && o.input.enabled).map((o) => ({ type: o.type, text: typeof o.text === 'string' ? o.text : null, x: o.x, y: o.y, width: o.width, height: o.height })),
    /**
     * Every live text object with its position. A control's caption is a plain
     * Text drawn over its interactive rectangle, so captions are located here
     * rather than in the interactive list (a Text has no hit area of its own).
     */
    allTexts: list.filter((o) => o.type === 'Text' && live(o)).map((o) => ({ text: o.text, x: o.x, y: o.y })),
    texts: list.filter((o) => o.type === 'Text' && live(o)).map((o) => o.text),
    /**
     * TASK-205: the scene's own lifecycle wiring and the references its teardown
     * owns. create() registers this.shutdown on the engine's SHUTDOWN / DESTROY
     * events, so counting by function identity tells whether this scene's cleanup
     * is wired and whether a run left a handler behind (other Phaser systems
     * register their own shutdown methods on the same emitter, so counting by
     * name would not identify the scene's).
     */
    teardownShutdownHandlers: (s.events.listeners('shutdown') || []).filter((fn) => fn === s.shutdown).length,
    teardownDestroyHandlers: (s.events.listeners('destroy') || []).filter((fn) => fn === s.shutdown).length,
    shellObjectCount: (s.shellObjects || []).length,
    renderedTextCount: (s.renderedTexts || []).length,
    interactiveObjectCount: (s.interactiveObjects || []).length,
    viewport: { left: rect.left, top: rect.top, scaleX: rect.width / gameWidth, scaleY: rect.height / gameHeight },
  };
})()
`;

export async function runCollectionViewerSmokeTest(runNumber = 1) {
  console.log(`\n============================================================`);
  console.log(`=== RUN ${runNumber}: COLLECTION VIEWER BROWSER SMOKE TEST ===`);
  console.log(`============================================================`);

  await checkPreflightHealth();

  const browserBin = resolveBrowserPath();
  // A fresh profile per run: this is what makes the run a clean browser context
  // rather than a continuation of the previous one.
  const profileDir = join(
    tmpdir(),
    `dcacti-collection-${Date.now()}-${Math.random().toString(16).slice(2, 6)}`
  );
  mkdirSync(profileDir, { recursive: true });

  const checks = [];
  let failures = 0;

  const record = (name, passed, detail = '') => {
    checks.push({ name, passed, detail });
    if (!passed) failures += 1;
    console.log(
      `  ${passed ? 'PASS' : 'FAIL'}  ${name}${detail ? ` — ${typeof detail === 'string' ? detail : JSON.stringify(detail)}` : ''}`
    );
  };

  console.log(`Spawning headless browser: ${browserBin}`);
  const browser = spawn(
    browserBin,
    [
      '--headless=new',
      `--remote-debugging-port=${DEBUG_PORT}`,
      '--remote-allow-origins=*',
      '--no-first-run',
      '--no-default-browser-check',
      '--disable-gpu',
      '--hide-scrollbars',
      '--window-size=1280,720',
      `--user-data-dir=${profileDir}`,
      'about:blank',
    ],
    { stdio: 'ignore' }
  );

  const cleanup = () => {
    try {
      browser.kill();
    } catch {
      /* ignore */
    }
    try {
      rmSync(profileDir, { recursive: true, force: true });
    } catch {
      /* ignore */
    }
  };

  process.on('exit', cleanup);

  let cdp = null;

  try {
    await waitForDevTools(DEBUG_PORT);

    const page = await (
      await fetch(`http://127.0.0.1:${DEBUG_PORT}/json/new?about:blank`, { method: 'PUT' })
    ).json();
    cdp = await Cdp.connect(page.webSocketDebuggerUrl);

    const uncaughtExceptions = [];
    const consoleErrors = [];
    const apiResponses = new Map();
    const discordEvents = [];

    cdp.on('Runtime.exceptionThrown', (p) => {
      uncaughtExceptions.push(p.exceptionDetails);
    });

    cdp.on('Runtime.consoleAPICalled', (p) => {
      if (p.type === 'error') {
        const text = (p.args || []).map((a) => a.value ?? a.description ?? '').join(' ');
        const isBenignNoise =
          text.includes('favicon.ico') ||
          text.includes('Failed to start the transport') ||
          text.includes('The connection could not be found on the server');
        if (!isBenignNoise) {
          consoleErrors.push(text);
        }
      }
    });

    cdp.on('Network.requestWillBeSent', (p) => {
      const url = p.request?.url || '';
      if (url.includes('discord.com') || url.includes('@discord')) {
        discordEvents.push({ type: 'request', url });
      }
    });

    cdp.on('Network.responseReceived', (p) => {
      const url = p.response?.url || '';
      if (url.includes('/api/')) {
        apiResponses.set(p.requestId, {
          url,
          status: p.response.status,
          statusText: p.response.statusText,
        });
      }
    });

    await cdp.send('Page.enable');
    await cdp.send('Runtime.enable');
    await cdp.send('Network.enable');
    await cdp.send('Emulation.setDeviceMetricsOverride', {
      width: 1280,
      height: 720,
      deviceScaleFactor: 1,
      mobile: false,
    });

    // -------------------------------------------------------------
    // PHASE 1: ACCOUNT REGISTRATION
    // -------------------------------------------------------------
    console.log('\n--- Phase 1: Account Registration via AuthScreen ---');
    await cdp.send('Page.navigate', { url: FRONTEND_URL });

    await waitForCondition(
      async () => evaluate(cdp, `document.querySelector('[data-testid="auth-screen"]') !== null`),
      'AuthScreen container mounted'
    );
    record('phase1.authScreenMounted', true);

    const regTabPoint = await evaluate(cdp, `(() => {
      const el = document.querySelector('[data-testid="tab-register"]');
      const r = el.getBoundingClientRect();
      return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    })()`);
    await realClick(cdp, regTabPoint);

    await waitForCondition(
      async () =>
        evaluate(
          cdp,
          `document.querySelector('[data-testid="tab-register"]').classList.contains('active')`
        ),
      'Register tab active'
    );

    const testUsername = `collect_${Date.now()}_${Math.random().toString(16).slice(2, 6)}`;
    const testPassword = 'SmokePassword123!';

    await fillInput(cdp, '[data-testid="input-username"]', testUsername);
    await fillInput(cdp, '[data-testid="input-password"]', testPassword);

    const submitPoint = await evaluate(cdp, `(() => {
      const el = document.querySelector('[data-testid="button-submit"]');
      const r = el.getBoundingClientRect();
      return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    })()`);
    await realClick(cdp, submitPoint);

    await waitForCondition(
      async () => evaluate(cdp, `Boolean(localStorage.getItem('dcacti_session_token'))`),
      'JWT session token persisted',
      12000
    );
    record('phase1.registrationSuccess', true, testUsername);

    await waitForCondition(
      async () => evaluate(cdp, `document.querySelector('[data-testid="auth-screen"]') === null`),
      'AuthScreen unmounted'
    );
    record('phase1.authScreenUnmounted', true);

    // -------------------------------------------------------------
    // PHASE 2: MAIN MENU — THE COLLECTION ENTRY POINT
    // -------------------------------------------------------------
    console.log('\n--- Phase 2: MainMenu COLLECTION Entry Point ---');
    await waitForCondition(
      async () => {
        await evaluate(cdp, CAPTURE_GAME);
        return (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene';
      },
      'MainMenuScene active',
      15000
    );
    record('phase2.mainMenuSceneActive', true);

    const menu = await evaluate(cdp, MAIN_MENU_SNAPSHOT);
    const menuButton = (label) => (menu.buttons || []).find((b) => b.label === label);

    // The two documented entry points exist and are at the fixed coordinates.
    record('phase2.startBattleButtonPresent', Boolean(menuButton('START BATTLE')));
    record('phase2.collectionButtonPresent', Boolean(menuButton('COLLECTION')));
    record(
      'phase2.collectionButtonAtDocumentedCentre',
      Boolean(
        menuButton('COLLECTION') &&
          menuButton('COLLECTION').x === COLLECTION_BUTTON.x &&
          menuButton('COLLECTION').y === COLLECTION_BUTTON.y
      ),
      menuButton('COLLECTION')
    );
    record(
      'phase2.startBattleNotMoved',
      Boolean(
        menuButton('START BATTLE') &&
          menuButton('START BATTLE').x === START_BATTLE_BUTTON.x &&
          menuButton('START BATTLE').y === START_BATTLE_BUTTON.y
      ),
      menuButton('START BATTLE')
    );
    await captureScreenshot(cdp, `run${runNumber}-01-mainmenu`);

    // The canvas-to-CSS mapping, taken from the live canvas so the click lands
    // where Phaser expects it regardless of the emulated viewport.
    const menuPoint = async (button) => {
      const live = await evaluate(cdp, MAIN_MENU_SNAPSHOT);
      return {
        x: live.viewport.left + button.x * live.viewport.scaleX,
        y: live.viewport.top + button.y * live.viewport.scaleY,
      };
    };

    // A real pointer click on the COLLECTION button's documented centre.
    await clickUntil(
      cdp,
      () => menuPoint(COLLECTION_BUTTON),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'CollectionViewerScene',
      'CollectionViewerScene active after COLLECTION click'
    );
    record('phase2.collectionSceneOpened', true);

    // -------------------------------------------------------------
    // PHASE 3: DEFAULT PETS TAB
    // -------------------------------------------------------------
    console.log('\n--- Phase 3: CollectionViewerScene PETS Tab ---');
    let viewer = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, COLLECTION_SNAPSHOT);
        return snap.active && !snap.loading && snap.ownedPets.length > 0 ? snap : false;
      },
      'CollectionViewerScene loaded the collection',
      12000
    );

    record('phase3.defaultTabIsPets', viewer.tab === 'pets', viewer.tab);
    record('phase3.noLoadError', viewer.loadError === null, viewer.loadError);

    // TASK-205: this first run's own lifecycle wiring, so the reopened run below
    // can be compared against it. A scene method named `shutdown` is not a
    // Phaser hook, so the teardown must be registered on the engine's events —
    // and exactly once per event, however many times the scene is reopened.
    const viewerFirstRun = viewer;
    record(
      'phase3.teardownRegisteredOnEngineLifecycleEvents',
      viewer.teardownShutdownHandlers === 1 && viewer.teardownDestroyHandlers === 1,
      {
        shutdownHandlers: viewer.teardownShutdownHandlers,
        destroyHandlers: viewer.teardownDestroyHandlers,
      }
    );

    // Each tab shows the count of the collection the read returned.
    record('phase3.petCountBadge', viewer.tabs.includes(`PETS (${viewer.ownedPets.length})`), viewer.tabs);
    record('phase3.cardCountBadge', viewer.tabs.includes(`CARDS (${viewer.ownedCards.length})`), viewer.tabs);
    record('phase3.relicCountBadge', viewer.tabs.includes(`RELICS (${viewer.ownedRelics.length})`), viewer.tabs);

    // The starter Pet is rendered with the element/tier/star/level the API sent.
    const pet = viewer.ownedPets[0];
    record(
      'phase3.starterPetRendered',
      viewer.rows.some((row) => row.includes(pet.identity)) &&
        viewer.rows.some((row) => row.includes(pet.element)) &&
        viewer.rows.some((row) => row.includes(pet.tier)) &&
        viewer.rows.some((row) => row.includes(`\u2605${pet.star}`)) &&
        viewer.rows.some((row) => row.includes(`Lv ${pet.level}`)),
      viewer.rows
    );
    record(
      'phase3.starterPetIsXichLang',
      viewer.ownedPets.some((p) => p.identity === STARTER_PET_IDENTITY),
      viewer.ownedPets.map((p) => p.identity)
    );
    record('phase3.backButtonRendered', viewer.backButton === true);
    await captureScreenshot(cdp, `run${runNumber}-02-collection-pets`);

    const toViewerScreen = (gx, gy) => ({
      x: viewer.viewport.left + gx * viewer.viewport.scaleX,
      y: viewer.viewport.top + gy * viewer.viewport.scaleY,
    });

    /**
     * The live click point of the control captioned `caption`.
     *
     * The scene layers every control as an interactive rectangle with a separate
     * caption text drawn over its centre, so the caption locates the control and
     * the click is dispatched at that caption's own position — the point a player
     * would aim at.
     */
    const captionPoint = async (caption) => {
      const live = await evaluate(cdp, COLLECTION_SNAPSHOT);
      const target = (live.allTexts || []).find((o) => o.text === caption);
      if (!target) {
        throw new Error(
          `No control is captioned "${caption}". Live texts: ${JSON.stringify(live.texts)}`
        );
      }
      return toViewerScreen(target.x, target.y);
    };

    /**
     * Clicks the control whose caption is `caption`, waiting until `predicate`
     * observes the effect.
     */
    const clickCaption = async (caption, predicate, description) => {
      await clickUntil(cdp, () => captionPoint(caption), predicate, description);
    };

    /** The tab caption whose text starts with `prefix` (e.g. `CARDS (3)`). */
    const tabCaption = (prefix) => viewer.tabs.find((t) => t.startsWith(prefix));

    /**
     * Clicks a tab and waits until the viewer's active tab is `key`.
     */
    const clickTab = async (prefix, key) => {
      await clickCaption(
        tabCaption(prefix),
        async () => {
          const snap = await evaluate(cdp, COLLECTION_SNAPSHOT);
          return snap.active && snap.tab === key ? snap : false;
        },
        `${key} tab active`
      );
      viewer = await evaluate(cdp, COLLECTION_SNAPSHOT);
    };

    /**
     * Clicks the item row containing `fragment` and waits until an item is
     * selected, returning the refreshed snapshot.
     */
    const clickRow = async (fragment) => {
      const point = async () => {
        const live = await evaluate(cdp, COLLECTION_SNAPSHOT);
        // Item rows are themselves interactive text, found by the row caption.
        const target = (live.allTexts || []).find((o) => o.text.includes(fragment));
        if (!target) {
          throw new Error(`No item row contains "${fragment}".`);
        }
        return toViewerScreen(target.x, target.y);
      };
      return clickUntil(
        cdp,
        point,
        async () => {
          const snap = await evaluate(cdp, COLLECTION_SNAPSHOT);
          return snap.active && snap.selectedItemId !== null ? snap : false;
        },
        `Item row "${fragment}" selected`
      );
    };

    // -------------------------------------------------------------
    // PHASE 4: CARDS TAB
    // -------------------------------------------------------------
    console.log('\n--- Phase 4: CARDS Tab ---');
    await clickTab('CARDS', 'cards');
    record('phase4.cardsTabActive', viewer.tab === 'cards', viewer.tab);

    const cardRowsRendered = STARTER_CARD_NAMES.filter((name) =>
      viewer.rows.some((row) => row.includes(name))
    );
    record(
      'phase4.starterCardsRendered',
      cardRowsRendered.length === STARTER_CARD_NAMES.length,
      cardRowsRendered
    );
    // Each Card row carries the category value the API returned.
    record(
      'phase4.cardCategoriesRendered',
      viewer.ownedCards.every((c) => viewer.rows.some((row) => row.includes(`[${c.category}]`))),
      viewer.ownedCards.map((c) => c.category)
    );
    // Switching tabs cleared the selection and rendered only the Card category.
    record('phase4.selectionClearedOnTabChange', viewer.selectedItemId === null, viewer.selectedItemId);
    record('phase4.petRowNoLongerRendered', !viewer.rows.some((row) => row.includes(STARTER_PET_IDENTITY)));
    await captureScreenshot(cdp, `run${runNumber}-03-collection-cards`);

    // -------------------------------------------------------------
    // PHASE 5: RELICS TAB
    // -------------------------------------------------------------
    console.log('\n--- Phase 5: RELICS Tab ---');
    await clickTab('RELICS', 'relics');
    record('phase5.relicsTabActive', viewer.tab === 'relics', viewer.tab);

    const relicRowsRendered = STARTER_RELIC_NAMES.filter((name) =>
      viewer.rows.some((row) => row.includes(name))
    );
    record(
      'phase5.starterRelicsRendered',
      relicRowsRendered.length === STARTER_RELIC_NAMES.length,
      relicRowsRendered
    );
    record('phase5.cardRowNoLongerRendered', !viewer.rows.some((row) => row.includes('Heal')));
    await captureScreenshot(cdp, `run${runNumber}-04-collection-relics`);

    // -------------------------------------------------------------
    // PHASE 6: ITEM DETAIL
    // -------------------------------------------------------------
    console.log('\n--- Phase 6: Item Detail Panel ---');
    // Click the first Relic row, addressed by its rendered row text.
    const firstRelic = viewer.ownedRelics[0];
    viewer = await clickRow(firstRelic.name);

    record('phase6.relicSelected', viewer.selectedItemId === firstRelic.relicId, viewer.selectedItemId);
    record(
      'phase6.relicDetailRendered',
      viewer.detail.includes(`Relic: ${firstRelic.name}`) &&
        viewer.detail.includes(`Relic ID: ${firstRelic.relicId}`),
      viewer.detail
    );
    await captureScreenshot(cdp, `run${runNumber}-05-relic-detail`);

    // Now inspect a Pet: switch to PETS and click its row.
    await clickTab('PETS', 'pets');

    // Switching category cleared the Relic selection.
    record('phase6.selectionClearedOnReturnToPets', viewer.selectedItemId === null, viewer.selectedItemId);

    viewer = await clickRow(pet.identity);

    record(
      'phase6.petDetailRendersWireFields',
      viewer.detail.includes(`Pet: ${pet.identity}`) &&
        viewer.detail.includes(`Element: ${pet.element}`) &&
        viewer.detail.includes(`Tier: ${pet.tier}`) &&
        viewer.detail.includes(`Star: ${pet.star}`) &&
        viewer.detail.includes(`Level: ${pet.level}`) &&
        viewer.detail.includes(`Instance ID: ${pet.petId}`),
      viewer.detail
    );
    // The detail panel adds nothing that the wire does not carry, and the viewer
    // read the Pet straight from the loaded list without a second API call.
    record(
      'phase6.detailInventsNoFields',
      !/(Description|Lore|Rarity|Bonus|Power|Upgrade)/i.test(viewer.detail),
      viewer.detail
    );
    await captureScreenshot(cdp, `run${runNumber}-06-pet-detail`);

    // -------------------------------------------------------------
    // PHASE 7: BACK TO MAIN MENU
    // -------------------------------------------------------------
    console.log('\n--- Phase 7: Back Navigation ---');
    // The `< BACK` control is a rectangle captioned `< BACK`; clicking the
    // caption's own centre is the point a player aims at.
    await clickUntil(
      cdp,
      () => captionPoint('< BACK'),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene',
      '< BACK returned to MainMenuScene'
    );
    record('phase7.backReturnedToMainMenu', true);

    // The menu is rebuilt, not stacked: exactly one COLLECTION button is live.
    const menuAfter = await evaluate(cdp, MAIN_MENU_SNAPSHOT);
    record(
      'phase7.menuRebuiltWithoutDuplicates',
      (menuAfter.buttons || []).filter((b) => b.label === 'COLLECTION').length === 1,
      (menuAfter.buttons || []).map((b) => b.label)
    );
    await captureScreenshot(cdp, `run${runNumber}-07-back-at-menu`);

    // Re-entering the viewer proves the scene rebuilds cleanly after a shutdown:
    // no duplicated UI, no stale listeners, no scene-transition failure.
    await clickUntil(
      cdp,
      () => menuPoint(COLLECTION_BUTTON),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'CollectionViewerScene',
      'CollectionViewerScene reopened'
    );
    const reopened = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, COLLECTION_SNAPSHOT);
        return snap.active && !snap.loading && snap.ownedPets.length > 0 ? snap : false;
      },
      'Reopened viewer loaded its collection',
      12000
    );
    record(
      'phase7.reopenRendersNoDuplicates',
      (reopened.interactive || []).filter(
        (o) => o.type === 'Text' && o.text.includes(pet.identity)
      ).length === 1 &&
        reopened.tabs.filter((t) => t.startsWith('PETS')).length === 1 &&
        reopened.backButton === true,
      { rows: reopened.rows, tabs: reopened.tabs }
    );

    // TASK-205: this is the viewer instance's second run, and `< BACK` shut the
    // first one down — so the engine's SHUTDOWN event must have run the scene's
    // own teardown. Its own object lists are the evidence: the shell was rebuilt
    // from empty lists (no second shell stacked on the ended run's), and the
    // registration did not accumulate (one teardown handler per event, exactly as
    // on the first run).
    record(
      'phase7.reopenRanTheSceneTeardown',
      reopened.shellObjectCount === viewerFirstRun.shellObjectCount &&
        reopened.teardownShutdownHandlers === viewerFirstRun.teardownShutdownHandlers &&
        reopened.teardownDestroyHandlers === viewerFirstRun.teardownDestroyHandlers &&
        reopened.teardownShutdownHandlers === 1 &&
        reopened.teardownDestroyHandlers === 1,
      {
        firstRun: {
          shellObjects: viewerFirstRun.shellObjectCount,
          shutdownHandlers: viewerFirstRun.teardownShutdownHandlers,
          destroyHandlers: viewerFirstRun.teardownDestroyHandlers,
        },
        secondRun: {
          shellObjects: reopened.shellObjectCount,
          shutdownHandlers: reopened.teardownShutdownHandlers,
          destroyHandlers: reopened.teardownDestroyHandlers,
        },
      }
    );

    // Return once more, so the run ends on the menu as it began.
    await clickUntil(
      cdp,
      () => captionPoint('< BACK'),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene',
      'Second < BACK returned to MainMenuScene'
    );
    record('phase7.secondBackReturnedToMainMenu', true);

    // -------------------------------------------------------------
    // PHASE 8: HEALTH & SAFETY GATES
    // -------------------------------------------------------------
    console.log('\n--- Phase 8: Error & Health Safety Gates ---');
    record('safety.zeroDiscordDependencies', discordEvents.length === 0, `${discordEvents.length} Discord events`);
    record('safety.zeroUncaughtExceptions', uncaughtExceptions.length === 0, uncaughtExceptions);
    record('safety.zeroFatalConsoleErrors', consoleErrors.length === 0, consoleErrors);

    const unexpectedApiResponses = Array.from(apiResponses.values()).filter(
      (resp) => !(resp.status >= 200 && resp.status < 300)
    );
    record('safety.zeroUnexpectedApiResponses', unexpectedApiResponses.length === 0, unexpectedApiResponses);

    // The viewer performed no battle action: only the collection reads and the
    // two auth routes were exercised on this journey.
    const calledPaths = Array.from(apiResponses.values()).map((r) => new URL(r.url).pathname);
    record(
      'safety.noBattleApiCalls',
      !calledPaths.some((path) => path.includes('/api/battle')),
      calledPaths
    );
    // The three collection reads were each performed exactly once per viewer
    // entry — the viewer opens twice in this run (the initial open and the
    // reopen after `< BACK`), so six reads in total, and switching tabs added
    // none.
    const collectionReadCounts = calledPaths.filter((path) =>
      ['/api/pets', '/api/cards', '/api/relics'].includes(path)
    ).length;
    record(
      'safety.collectionReadsAreBatched',
      collectionReadCounts === 6,
      `${collectionReadCounts} collection reads across 2 viewer entries`
    );
  } finally {
    if (cdp) cdp.close();
    cleanup();
  }

  console.log(`\n--- RUN ${runNumber} SUMMARY ---`);
  console.log(`Total checks: ${checks.length}, Failures: ${failures}`);

  if (failures > 0) {
    throw new Error(`Collection viewer smoke RUN ${runNumber} failed with ${failures} failure(s).`);
  }

  return { runNumber, checks, failures };
}

// Direct execution
if (process.argv[1] && process.argv[1].endsWith('collection-viewer-smoke.mjs')) {
  (async () => {
    try {
      await runCollectionViewerSmokeTest(1);
      await runCollectionViewerSmokeTest(2);

      console.log('\n============================================================');
      console.log('=== ALL COLLECTION VIEWER RUNS PASSED CLEANLY (NO FLAKINESS) ===');
      console.log('============================================================\n');
      process.exit(0);
    } catch (err) {
      console.error('\n[COLLECTION VIEWER SMOKE FATAL ERROR]', err.message);
      process.exit(1);
    }
  })();
}
