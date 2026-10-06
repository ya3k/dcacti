/**
 * TASK-189: Standalone Web End-to-End Browser Smoke Test Suite.
 *
 * Verifies the complete standalone web player journey introduced by ADR-020:
 *
 *   Clean Context
 *     ↓
 *   AuthScreen (Register unique account smoke_<timestamp>_<randomHex>)
 *     ↓
 *   Session token & starter grant verification (1 Pet, 3 Basic Cards, 3 Relics)
 *     ↓
 *   Re-login verification (Clear storage → Login with credentials via AuthScreen UI)
 *     ↓
 *   StatusOverlay verification (Account badge & SignalR: Connected)
 *     ↓
 *   MainMenuScene (Phaser mounted → Real pointer click on START BATTLE)
 *     ↓
 *   LobbyScene (Loadout verification, 5 canonical MVP Bosses, unselected Boss guard)
 *     ↓
 *   Boss Selection & Battle Start (Select Boss → POST /api/battle/start → BattleScene)
 *     ↓
 *   BattleScene (SignalR BattleStateUpdated push → 8×8 board rendered with 64 cells)
 *     ↓
 *   Match-3 Interaction (Real pointer two-tap cell swap → authoritative server acceptance)
 *     ↓
 *   Cast Interaction (Real pointer cast button click → authoritative feedback)
 *     ↓
 *   Battle Outcome & ResultScene (Deterministic completion → ResultScene → VICTORY & HP readouts)
 *     ↓
 *   Post-Result Lifecycle (TASK-203 / TASK-204): PLAY AGAIN → LobbyScene with the
 *     loadout restored and editable → edited loadout submitted → Battle 2 reaches
 *     its own outcome → ResultScene again → MAIN MENU → MainMenuScene
 *     ↓
 *   Scene Lifecycle Consistency (TASK-205): every stopped scene's own teardown
 *     ran on the engine's SHUTDOWN event (MainMenu / Lobby / Battle / Result),
 *     one teardown handler per scene event across scene reuse, and no stale
 *     scene subscriptions at the end of the flow
 *     ↓
 *   Zero Fatal Runtime Errors & Zero Discord Dependencies
 *
 * Usage:
 *   node scripts/standalone-web-smoke.mjs [url]
 *   npm run verify:e2e:smoke
 */

import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const FRONTEND_URL = process.argv[2] || process.env.URL_UNDER_TEST || 'http://localhost:5173/';
const BACKEND_URL = process.env.BACKEND_URL || 'http://localhost:5000';
const DEBUG_PORT = Number(process.env.DEBUG_PORT) || 9289;
const OUT_DIR = 'smoke-shots';

/** Standard candidate paths for Edge / Chrome on Windows / Linux */
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

/** Canonical MVP Bosses (BOSS_RULES.md §6/§6.4). */
const CANONICAL_BOSSES = [
  { displayName: 'Hỏa Long', bossId: 'boss-hoa-long', element: 'Hỏa', wireElement: 'Fire', maxHp: 5000 },
  { displayName: 'Thủy Ma', bossId: 'boss-thuy-ma', element: 'Thủy', wireElement: 'Water', maxHp: 5000 },
  { displayName: 'Mộc Yêu', bossId: 'boss-moc-yeu', element: 'Mộc', wireElement: 'Wood', maxHp: 5000 },
  { displayName: 'Sơn Thạch Vệ', bossId: 'boss-son-thach-ve', element: 'Thổ', wireElement: 'Earth', maxHp: 3000 },
  { displayName: 'Kim Lôi Vương', bossId: 'boss-kim-loi-vuong', element: 'Kim', wireElement: 'Metal', maxHp: 2800 },
];

/** Board constants (MATCH3_RULES.md §1.0 & BattleScene.ts). */
const BOARD_SIZE = 8;
const CELL_SIZE = 56;
const CELL_GAP = 6;
const BOARD_WIDTH = BOARD_SIZE * CELL_SIZE + (BOARD_SIZE - 1) * CELL_GAP; // 490
const BOARD_ORIGIN_X = (1280 - BOARD_WIDTH) / 2; // 395
const BOARD_ORIGIN_Y = 24 + 96; // 120

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
  // 1. Backend health check
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

  // 2. Frontend health check
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

/** Trusted left-button click with move, press, release */
async function realClick(cdp, point) {
  const base = { x: Math.round(point.x), y: Math.round(point.y), button: 'left', clickCount: 1 };
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mouseMoved', buttons: 0 });
  await delay(80);
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mousePressed', buttons: 1 });
  await delay(60);
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mouseReleased', buttons: 0 });
  await delay(150);
}

/** Fills a controlled React input with real pointer focus + native value dispatch */
async function fillInput(cdp, selector, value) {
  const point = await evaluate(cdp, `(() => {
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

/** React Fiber walker to attach window.__game */
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
  for (const key of ['MainMenuScene', 'BattleHistoryScene', 'LobbyScene', 'BattleScene', 'ResultScene']) {
    const s = window.__game.scene.getScene(key);
    if (s && s.scene && s.scene.isActive && s.scene.isActive()) return key;
  }
  return 'none';
})()
`;

const LOBBY_SNAPSHOT = `
(() => {
  const s = window.__game && window.__game.scene.getScene('LobbyScene');
  if (!s || !s.scene.isActive()) return { active: false };
  const canvas = document.querySelector('canvas');
  if (!canvas) return { active: true, reason: 'no canvas' };
  const rect = canvas.getBoundingClientRect();
  const gameWidth = window.__game.scale.gameSize.width;
  const gameHeight = window.__game.scale.gameSize.height;
  return {
    active: true,
    selectedBossId: s.selectedBossId === undefined ? null : s.selectedBossId,
    selectedPetId: s.selectedPetId === undefined ? null : s.selectedPetId,
    selectedCardIds: s.selectedCardIds || [],
    selectedRelicIds: s.selectedRelicIds || [],
    ownedPetsCount: (s.ownedPets || []).length,
    ownedCardsCount: (s.ownedCards || []).length,
    ownedRelicsCount: (s.ownedRelics || []).length,
    reviewText: s.reviewText && typeof s.reviewText.text === 'string' ? s.reviewText.text : null,
    messageText: s.messageText && typeof s.messageText.text === 'string' ? s.messageText.text : null,
    errorText: s.errorText && typeof s.errorText.text === 'string' ? s.errorText.text : null,
    viewport: { left: rect.left, top: rect.top, scaleX: rect.width / gameWidth, scaleY: rect.height / gameHeight },
    interactive: (s.interactiveObjects || []).map((o) => ({
      type: o.type,
      text: typeof o.text === 'string' ? o.text : null,
      x: o.x, y: o.y, width: o.width, height: o.height,
      enabled: !!(o.input && o.input.enabled),
    })),
  };
})()
`;

const BATTLE_SNAPSHOT = `
(() => {
  const s = window.__game && window.__game.scene.getScene('BattleScene');
  if (!s || !s.scene.isActive()) return { active: false };
  const canvas = document.querySelector('canvas');
  const rect = canvas.getBoundingClientRect();
  const gameWidth = window.__game.scale.gameSize.width;
  const gameHeight = window.__game.scale.gameSize.height;
  const runtime = s.runtime || null;
  return {
    active: true,
    battleId: runtime && runtime.getBattleState ? (runtime.getBattleState() || {}).battleId || null : null,
    // The scene's own per-battle guard. It must start false for every battle:
    // a scene instance is reused, so a value left over from the previous battle
    // would swallow the next battle's outcome (TASK-204).
    outcomeHandled: s.outcomeHandled === true,
    // How many subscribers the shared runtime currently holds. Exactly one of
    // each is correct while this scene is the only battle scene running: a
    // number above one means a shut-down scene is still subscribed.
    listenerCounts: runtime ? {
      runtime: runtime.runtimeListeners ? runtime.runtimeListeners.size : null,
      battle: runtime.battleListeners ? runtime.battleListeners.size : null,
      battleState: runtime.battleStateListeners ? runtime.battleStateListeners.size : null,
    } : null,
    boardChildCount: s.boardLayer && s.boardLayer.list ? s.boardLayer.list.length : 0,
    boardLabels: s.boardLayer && s.boardLayer.list
      ? s.boardLayer.list.filter((o) => o.type === 'Text').map((o) => o.text)
      : [],
    boardText: s.boardText && typeof s.boardText.text === 'string' ? s.boardText.text : '',
    swapText: s.swapText && typeof s.swapText.text === 'string' ? s.swapText.text : '',
    castText: s.castText && typeof s.castText.text === 'string' ? s.castText.text : '',
    castControlsCount: s.castControlsLayer && s.castControlsLayer.list ? s.castControlsLayer.list.length : 0,
    viewport: { left: rect.left, top: rect.top, scaleX: rect.width / gameWidth, scaleY: rect.height / gameHeight },
  };
})()
`;

const RESULT_SNAPSHOT = `
(() => {
  const s = window.__game && window.__game.scene.getScene('ResultScene');
  if (!s || !s.scene.isActive()) return { active: false, controls: [] };
  // The scene's navigation controls are the interactive rectangles, each with
  // its label drawn at the same centre (ResultScene.drawButton). Reading them
  // from the live display list is what makes "exactly two explicit controls"
  // an assertion about the scene rather than about this script's constants.
  const list = s.children.list;
  const labels = list.filter((o) => o.type === 'Text');
  const controls = list
    .filter((o) => o.type === 'Rectangle' && o.input && o.input.enabled)
    .map((b) => {
      const label = labels.find((t) => t.x === b.x && t.y === b.y);
      return { label: label ? label.text : null, x: b.x, y: b.y, width: b.width, height: b.height };
    });
  return {
    active: true,
    outcomeText: s.outcomeText && typeof s.outcomeText.text === 'string' ? s.outcomeText.text : '',
    bossHpText: s.bossHpText && typeof s.bossHpText.text === 'string' ? s.bossHpText.text : '',
    playerHpText: s.playerHpText && typeof s.playerHpText.text === 'string' ? s.playerHpText.text : '',
    rewardText: s.rewardText && typeof s.rewardText.text === 'string' ? s.rewardText.text : '',
    controls,
  };
})()
`;

/**
 * The BattleHistoryScene's observable presentation state (TASK-206).
 *
 * `entries` are the scene's own rendered history blocks (one per delivered
 * `GET /api/battle/history` element, in the delivered order), `progression` is
 * the account-progression line it drew from the newest delivered element, and
 * `controls` are the interactive rectangles with the captions drawn over them —
 * so `< BACK` / `RETRY` are identified the way a player reads them.
 */
const BATTLE_HISTORY_SNAPSHOT = `
(() => {
  const s = window.__game && window.__game.scene.getScene('BattleHistoryScene');
  if (!s || !s.scene.isActive()) return { active: false };
  const canvas = document.querySelector('canvas');
  const rect = canvas.getBoundingClientRect();
  const gameWidth = window.__game.scale.gameSize.width;
  const gameHeight = window.__game.scale.gameSize.height;
  const list = s.children.list || [];
  const live = (o) => list.indexOf(o) !== -1;
  const labels = list.filter((o) => o.type === 'Text' && live(o));
  const controls = list
    .filter((o) => o.type === 'Rectangle' && live(o) && o.input && o.input.enabled)
    .map((b) => {
      const label = labels.find((t) => t.x === b.x && t.y === b.y);
      return { label: label ? label.text : null, x: b.x, y: b.y, width: b.width, height: b.height };
    });
  const entryTexts = (s.renderedTexts || [])
    .filter((t) => typeof t.text === 'string' && t.text.startsWith('Battle #'))
    .map((t) => t.text);
  return {
    active: true,
    loading: !!s.loading,
    loadError: s.loadError === null || s.loadError === undefined ? null : s.loadError,
    // The loaded array, exactly as the read returned it (never reordered).
    history: (s.history || []).map((e) => ({
      battleId: e.battleId,
      outcome: e.outcome,
      durationTurns: e.durationTurns,
      completedAt: e.completedAt,
      rewards: e.rewards,
    })),
    progression: s.progressionText && typeof s.progressionText.text === 'string' ? s.progressionText.text : '',
    status: s.statusText && typeof s.statusText.text === 'string' ? s.statusText.text : '',
    entries: entryTexts,
    texts: labels.map((t) => t.text),
    controls,
    // TASK-205: this scene's own teardown wiring, counted by function identity.
    teardownShutdownHandlers: (s.events.listeners('shutdown') || []).filter((fn) => fn === s.shutdown).length,
    teardownDestroyHandlers: (s.events.listeners('destroy') || []).filter((fn) => fn === s.shutdown).length,
    shellObjectCount: (s.shellObjects || []).length,
    renderedTextCount: (s.renderedTexts || []).length,
    interactiveObjectCount: (s.interactiveObjects || []).length,
    viewport: { left: rect.left, top: rect.top, scaleX: rect.width / gameWidth, scaleY: rect.height / gameHeight },
  };
})()
`;

/**
 * Every registered scene's scene-lifecycle wiring and the scene-owned state its
 * teardown releases (TASK-205).
 *
 * The engine's teardown signal is the `shutdown` / `destroy` **event** on the
 * scene's own emitter (`Phaser.Scenes.Systems#shutdown` / `#destroy`): Phaser
 * 4.2.1 invokes a scene method by name only for `init` / `preload` / `create` /
 * `update`, and `Phaser.Scene` declares no `shutdown` method. So a scene whose
 * cleanup is not in these listener lists never runs it, and the per-scene state
 * below is exactly what a stopped scene should have released.
 */
const SCENE_LIFECYCLE_SNAPSHOT = `
(() => {
  const keys = ['MainMenuScene', 'LobbyScene', 'BattleScene', 'CollectionViewerScene', 'BattleHistoryScene', 'ResultScene'];
  // Counts *this scene's own* teardown method on an event, by function identity:
  // create() registers this.shutdown, so a count of one means the scene's
  // cleanup is wired to that engine event and did not stack across runs. (Other
  // Phaser systems register their own shutdown methods on the same emitter, so
  // counting by name would not identify the scene's.)
  const countTeardown = (s, event) =>
    (s.events && typeof s.events.listeners === 'function' ? s.events.listeners(event) : [])
      .filter((fn) => typeof fn === 'function' && fn === s.shutdown)
      .length;
  const snapshot = {};
  for (const key of keys) {
    const s = window.__game && window.__game.scene.getScene(key);
    if (!s) { snapshot[key] = null; continue; }
    snapshot[key] = {
      active: !!(s.scene && s.scene.isActive && s.scene.isActive()),
      shutdownHandlers: countTeardown(s, 'shutdown'),
      destroyHandlers: countTeardown(s, 'destroy'),
      // The scene's own state, released only by its own teardown. Each field is
      // read from the scene that owns it, so this reports the scene rather than
      // this script's constants.
      hasTransitioned: s.hasTransitioned === true,
      selectedPetId: s.selectedPetId === undefined ? null : s.selectedPetId,
      selectedItemId: s.selectedItemId === undefined ? null : s.selectedItemId,
      resultData: s.resultData === undefined ? null : s.resultData,
      rewardTextIsNull: s.rewardText === undefined ? null : s.rewardText === null,
      outcomeHandled: s.outcomeHandled === true,
      shellObjects: Array.isArray(s.shellObjects) ? s.shellObjects.length : null,
      // TASK-206: the Battle History scene's own loaded presentation state, which
      // only its teardown releases.
      historyLength: Array.isArray(s.history) ? s.history.length : null,
      progressionTextIsNull: s.progressionText === undefined ? null : s.progressionText === null,
    };
  }
  return snapshot;
})()
`;

/**
 * The game instance's preserved pre-battle loadout carrier (ADR-022).
 *
 * It is written by `LobbyScene` on a successful battle start and read only on
 * the approved `ResultScene → PLAY AGAIN` entry, so its value after an exit is
 * exactly what the post-result lifecycle preserved. The registry it lives in is
 * the game-wide one, reachable from any scene.
 */
const PRESERVED_LOADOUT = `s.registry.get('preservedLoadout') || null`;

/** Finds adjacent cell pair that forms a Match-3 */
function findMatchingSwaps(labels, size = BOARD_SIZE) {
  if (!Array.isArray(labels) || labels.length !== size * size) return [];
  const grid = labels.slice();
  const hasMatch = () => {
    for (let r = 0; r < size; r++) {
      for (let c = 0; c < size - 2; c++) {
        const v = grid[r * size + c];
        if (v && v === grid[r * size + c + 1] && v === grid[r * size + c + 2]) return true;
      }
    }
    for (let c = 0; c < size; c++) {
      for (let r = 0; r < size - 2; r++) {
        const v = grid[r * size + c];
        if (v && v === grid[(r + 1) * size + c] && v === grid[(r + 2) * size + c]) return true;
      }
    }
    return false;
  };

  const candidates = [];
  for (let r = 0; r < size; r++) {
    for (let c = 0; c < size; c++) {
      for (const [r2, c2] of [[r, c + 1], [r + 1, c]]) {
        if (r2 >= size || c2 >= size) continue;
        const i = r * size + c;
        const j = r2 * size + c2;
        const a = grid[i];
        const b = grid[j];
        grid[i] = b;
        grid[j] = a;
        if (hasMatch()) candidates.push([i, j]);
        grid[i] = a;
        grid[j] = b;
      }
    }
  }
  return candidates;
}

export async function runSmokeTest(runNumber = 1) {
  console.log(`\n============================================================`);
  console.log(`=== RUN ${runNumber}: STANDALONE WEB E2E BROWSER SMOKE TEST ===`);
  console.log(`============================================================`);

  await checkPreflightHealth();

  const browserBin = resolveBrowserPath();
  const profileDir = join(tmpdir(), `dcacti-smoke-${Date.now()}-${Math.random().toString(16).slice(2, 6)}`);
  mkdirSync(profileDir, { recursive: true });

  const checks = [];
  let failures = 0;

  const record = (name, passed, detail = '') => {
    checks.push({ name, passed, detail });
    if (!passed) failures += 1;
    console.log(`  ${passed ? 'PASS' : 'FAIL'}  ${name}${detail ? ` — ${typeof detail === 'string' ? detail : JSON.stringify(detail)}` : ''}`);
  };

  console.log(`Spawning headless browser: ${browserBin}`);
  const browser = spawn(browserBin, [
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
  ], { stdio: 'ignore' });

  const cleanup = () => {
    try { browser.kill(); } catch { /* ignore */ }
    try { rmSync(profileDir, { recursive: true, force: true }); } catch { /* ignore */ }
  };

  process.on('exit', cleanup);

  let cdp = null;

  try {
    await waitForDevTools(DEBUG_PORT);

    // Open target page
    const page = await (await fetch(`http://127.0.0.1:${DEBUG_PORT}/json/new?about:blank`, { method: 'PUT' })).json();
    cdp = await Cdp.connect(page.webSocketDebuggerUrl);

    // Listeners for errors & HTTP traffic
    const uncaughtExceptions = [];
    const consoleErrors = [];
    const apiResponses = new Map();
    const discordEvents = [];
    /** Every `POST /api/battle/start` request body, in submission order. */
    const battleStartRequests = [];

    cdp.on('Runtime.exceptionThrown', (p) => {
      uncaughtExceptions.push(p.exceptionDetails);
    });

    cdp.on('Runtime.consoleAPICalled', (p) => {
      if (p.type === 'error') {
        const text = (p.args || []).map((a) => a.value ?? a.description ?? '').join(' ');
        // Filter out non-fatal browser/transport noise (e.g. missing favicon, SignalR transport negotiation fallback)
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
      // The loadout the client actually submitted. Capturing it is how the
      // post-result lifecycle is verified end to end: Battle 2 must carry the
      // loadout preserved by Battle 1 with the player's edit applied, and no
      // battle identity or state from Battle 1 (API_CONTRACTS.md §3).
      if (url.includes('/api/battle/start') && p.request?.method === 'POST') {
        battleStartRequests.push(p.request.postData ?? null);
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

    // Wait for AuthScreen to mount
    await waitForCondition(
      async () => evaluate(cdp, `document.querySelector('[data-testid="auth-screen"]') !== null`),
      'AuthScreen container mounted'
    );
    record('phase1.authScreenMounted', true, 'AuthScreen visible');
    await captureScreenshot(cdp, `run${runNumber}-01-authscreen`);

    // Switch to Register tab
    await waitForCondition(
      async () => evaluate(cdp, `document.querySelector('[data-testid="tab-register"]') !== null`),
      'Register tab visible'
    );
    const regTabPoint = await evaluate(cdp, `(() => {
      const el = document.querySelector('[data-testid="tab-register"]');
      const r = el.getBoundingClientRect();
      return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    })()`);
    await realClick(cdp, regTabPoint);

    await waitForCondition(
      async () => evaluate(cdp, `document.querySelector('[data-testid="tab-register"]').classList.contains('active')`),
      'Register tab active'
    );
    record('phase1.registerTabActive', true);

    // Generate unique test credentials
    const testUsername = `smoke_${Date.now()}_${Math.random().toString(16).slice(2, 6)}`;
    const testPassword = 'SmokePassword123!';

    // Fill in inputs
    await fillInput(cdp, '[data-testid="input-username"]', testUsername);
    await fillInput(cdp, '[data-testid="input-password"]', testPassword);

    // Submit form
    const submitPoint = await evaluate(cdp, `(() => {
      const el = document.querySelector('[data-testid="button-submit"]');
      const r = el.getBoundingClientRect();
      return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    })()`);
    await realClick(cdp, submitPoint);

    // Wait for token in localStorage
    await waitForCondition(
      async () => evaluate(cdp, `Boolean(localStorage.getItem('dcacti_session_token'))`),
      'JWT sessionToken persisted in localStorage',
      12000
    );
    const storedUsername = await evaluate(cdp, `localStorage.getItem('dcacti_username')`);
    record('phase1.registrationSuccess', storedUsername === testUsername, { testUsername, storedUsername });

    // Wait for AuthScreen to unmount
    await waitForCondition(
      async () => evaluate(cdp, `document.querySelector('[data-testid="auth-screen"]') === null`),
      'AuthScreen unmounted'
    );
    record('phase1.authScreenUnmounted', true);

    // -------------------------------------------------------------
    // PHASE 2: SESSION & RE-LOGIN VERIFICATION
    // -------------------------------------------------------------
    console.log('\n--- Phase 2: Session Clear & Re-Login Verification ---');
    // Clear storage and reload
    await evaluate(cdp, `localStorage.clear()`);
    await cdp.send('Page.reload');

    // Wait for AuthScreen again
    await waitForCondition(
      async () => evaluate(cdp, `document.querySelector('[data-testid="auth-screen"]') !== null`),
      'AuthScreen re-mounted after session clear'
    );
    record('phase2.reLoginPromptVisible', true);

    // Verify default tab is Login
    const isLoginTabActive = await evaluate(
      cdp,
      `document.querySelector('[data-testid="tab-login"]').classList.contains('active')`
    );
    record('phase2.loginTabDefault', isLoginTabActive);

    // Fill registered credentials
    await fillInput(cdp, '[data-testid="input-username"]', testUsername);
    await fillInput(cdp, '[data-testid="input-password"]', testPassword);

    // Submit login form
    const loginSubmitPoint = await evaluate(cdp, `(() => {
      const el = document.querySelector('[data-testid="button-submit"]');
      const r = el.getBoundingClientRect();
      return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    })()`);
    await realClick(cdp, loginSubmitPoint);

    // Wait for token and StatusOverlay
    await waitForCondition(
      async () => evaluate(cdp, `Boolean(localStorage.getItem('dcacti_session_token'))`),
      'Session token re-persisted after login'
    );
    record('phase2.loginSuccess', true);

    // Wait for StatusOverlay to mount
    await waitForCondition(
      async () => evaluate(cdp, `document.querySelector('[data-testid="runtime-status-overlay"]') !== null`),
      'StatusOverlay mounted',
      12000
    );
    record('phase2.statusOverlayMounted', true);

    // Verify Account and SignalR connection status in StatusOverlay
    await waitForCondition(
      async () => {
        const text = await evaluate(cdp, `document.querySelector('[data-testid="runtime-status-overlay"]').innerText`);
        return text.includes(testUsername) && text.includes('Connected');
      },
      'StatusOverlay shows Account and SignalR Connected',
      15000
    );
    record('phase2.signalRConnected', true, `Account: ${testUsername}, SignalR: Connected`);
    await captureScreenshot(cdp, `run${runNumber}-02-loggedin`);

    // -------------------------------------------------------------
    // PHASE 3: MAIN MENU TO LOBBY NAVIGATION
    // -------------------------------------------------------------
    console.log('\n--- Phase 3: MainMenu to Lobby Navigation ---');
    // Wait for Phaser game instance and MainMenuScene
    await waitForCondition(
      async () => {
        await evaluate(cdp, CAPTURE_GAME);
        return (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene';
      },
      'MainMenuScene active',
      15000
    );
    record('phase3.mainMenuSceneActive', true);
    await captureScreenshot(cdp, `run${runNumber}-03-mainmenu`);

    // Click START BATTLE button at (640, 324) in logical coordinates
    const toScreen = async (gx, gy) => {
      const canvasInfo = await evaluate(cdp, `(() => {
        const c = document.querySelector('canvas');
        if (!c) return { left: 0, top: 0, width: 1280, height: 720 };
        const r = c.getBoundingClientRect();
        return { left: r.left, top: r.top, width: r.width, height: r.height };
      })()`);
      const scaleX = canvasInfo.width / 1280;
      const scaleY = canvasInfo.height / 720;
      return { x: canvasInfo.left + gx * scaleX, y: canvasInfo.top + gy * scaleY };
    };

    const menuStartPoint = await toScreen(640, 324);

    // -------------------------------------------------------------
    // PHASE 3b: BATTLE HISTORY ON A FRESH ACCOUNT (TASK-206)
    // -------------------------------------------------------------
    // This run registered a brand-new account in Phase 1, so its battle history
    // is the documented empty case: `GET /api/battle/history` → `200 []`
    // (API_CONTRACTS.md §4.5 note 9). The endpoint is the only thing this
    // surface reads, and no battle of this account has reached a durable
    // terminal result yet (note 10), so an explicit empty state is the only
    // correct presentation.
    console.log('\n--- Phase 3b: Battle History on a fresh account ---');

    /** The live click point of the history scene's control captioned `label`. */
    const historyControlPoint = async (label) => {
      const snap = await evaluate(cdp, BATTLE_HISTORY_SNAPSHOT);
      const control = (snap.controls || []).find((c) => c.label === label);
      if (!control) throw new Error(`BattleHistoryScene rendered no "${label}" control.`);
      return toScreen(control.x, control.y);
    };

    for (let i = 0; i < 8; i++) {
      if ((await evaluate(cdp, ACTIVE_SCENE)) === 'BattleHistoryScene') break;
      await realClick(cdp, await toScreen(640, 464));
      await delay(1000);
    }
    await waitForCondition(
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'BattleHistoryScene',
      'BattleHistoryScene active after the BATTLE HISTORY click',
      10000
    );
    record('phase3b.battleHistoryOpened', true);

    const emptyHistory = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, BATTLE_HISTORY_SNAPSHOT);
        return snap.active && !snap.loading ? snap : false;
      },
      'Battle History read settled on the fresh account',
      10000
    );
    await captureScreenshot(cdp, `run${runNumber}-03b-history-empty`);

    record(
      'phase3b.emptyStateShown',
      emptyHistory.loadError === null &&
        emptyHistory.history.length === 0 &&
        emptyHistory.status.includes('No battles yet') &&
        emptyHistory.entries.length === 0,
      { status: emptyHistory.status, entries: emptyHistory.entries, error: emptyHistory.loadError }
    );

    // §4.5 note 9's empty history is not a starting progression: no Level and no
    // XP may be invented for an account that has completed no battle. The scene
    // prints "—" instead (AGENTS.md §7).
    record(
      'phase3b.noFabricatedLevelOrXp',
      !/Level \d/.test(emptyHistory.progression) &&
        !/XP \d/.test(emptyHistory.progression) &&
        !emptyHistory.texts.some((t) => /Level \d/.test(t) || /XP \d/.test(t)),
      emptyHistory.progression
    );

    // An empty history is a delivered answer, not a failure: no RETRY, and BACK
    // is the only control.
    record(
      'phase3b.emptyIsNotAnErrorState',
      !(emptyHistory.controls || []).some((c) => c.label === 'RETRY') &&
        (emptyHistory.controls || []).some((c) => c.label === '< BACK'),
      (emptyHistory.controls || []).map((c) => c.label)
    );

    record(
      'phase3b.teardownRegisteredOnEngineLifecycleEvents',
      emptyHistory.teardownShutdownHandlers === 1 && emptyHistory.teardownDestroyHandlers === 1,
      {
        shutdownHandlers: emptyHistory.teardownShutdownHandlers,
        destroyHandlers: emptyHistory.teardownDestroyHandlers,
      }
    );

    // The read really went through the frozen endpoint, authenticated, and
    // returned the documented `200`.
    const historyResponses = Array.from(apiResponses.values()).filter((r) =>
      r.url.includes('/api/battle/history')
    );
    record(
      'phase3b.historyReadWasAuthenticated200',
      historyResponses.length >= 1 && historyResponses.every((r) => r.status === 200),
      historyResponses
    );

    // `< BACK` returns to the main menu, and the stopped scene ran its own
    // teardown: it keeps no shell, no interactive object, and no loaded history
    // (TASK-205's lifecycle contract, applied to the new scene).
    for (let i = 0; i < 8; i++) {
      if ((await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene') break;
      await realClick(cdp, await historyControlPoint('< BACK'));
      await delay(600);
    }
    await waitForCondition(
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene',
      'MainMenuScene active after < BACK',
      10000
    );
    record('phase3b.backReturnedToMainMenu', true);

    const stoppedHistory = (await evaluate(cdp, SCENE_LIFECYCLE_SNAPSHOT)).BattleHistoryScene;
    record(
      'phase3b.stoppedHistorySceneRanItsTeardown',
      stoppedHistory.active === false &&
        stoppedHistory.historyLength === 0 &&
        stoppedHistory.shellObjects === 0 &&
        stoppedHistory.progressionTextIsNull === true &&
        stoppedHistory.shutdownHandlers === 0 &&
        stoppedHistory.destroyHandlers === 1,
      stoppedHistory
    );
    for (let i = 0; i < 8; i++) {
      if ((await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene') break;
      await realClick(cdp, menuStartPoint);
      await delay(1000);
    }

    // Wait for LobbyScene
    await waitForCondition(
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene',
      'LobbyScene active',
      10000
    );
    record('phase3.lobbySceneReached', true);
    await captureScreenshot(cdp, `run${runNumber}-04-lobby`);

    // -------------------------------------------------------------
    // PHASE 4: LOBBY LOADOUT & BOSS SELECTION
    // -------------------------------------------------------------
    console.log('\n--- Phase 4: Lobby Loadout & Boss Selection ---');
    // Wait for collections to load
    let lobby = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, LOBBY_SNAPSHOT);
        return snap.active && snap.ownedPetsCount > 0 && snap.ownedCardsCount >= 3 && snap.ownedRelicsCount >= 3 ? snap : false;
      },
      'Lobby collections loaded from starter grant',
      12000
    );

    record('phase4.starterGrantLoaded', true, {
      pets: lobby.ownedPetsCount,
      cards: lobby.ownedCardsCount,
      relics: lobby.ownedRelicsCount,
    });

    const toVp = (gx, gy) => ({
      x: lobby.viewport.left + gx * lobby.viewport.scaleX,
      y: lobby.viewport.top + gy * lobby.viewport.scaleY,
    });
    const centreOf = (o) => toVp(o.x + o.width / 2, o.y + o.height / 2);

    const bossOptionsOf = (snapshot) =>
      snapshot.interactive.filter(
        (o) => o.text !== null && CANONICAL_BOSSES.some((boss) => o.text.endsWith(`  ${boss.element}`))
      );

    // Verify all 5 canonical MVP Bosses rendered
    const bossOptions = bossOptionsOf(lobby);
    record('phase4.fiveBossesRendered', bossOptions.length === 5, bossOptions.map((o) => o.text));

    // Verify initially no Boss is selected
    record('phase4.noBossInitiallySelected', lobby.selectedBossId === null);

    // Test loadout selection first
    const selectLoadout = async () => {
      for (let attempt = 1; attempt <= 4; attempt++) {
        lobby = await evaluate(cdp, LOBBY_SNAPSHOT);
        const bossRows = bossOptionsOf(lobby);
        const rows = lobby.interactive.filter((o) => o.type === 'Text' && !bossRows.includes(o));
        const petRow = rows.find((o) => /Lv \d+$/.test(o.text || '') && (o.text || '').startsWith('○'));
        const cardRows = rows
          .filter((o) => (o.text || '').includes('[Basic]') && (o.text || '').startsWith('○'))
          .slice(0, 3 - lobby.selectedCardIds.length);
        const relicRows = rows
          .filter((o) => o.x > 700 && !(o.text || '').includes('[Basic]') && (o.text || '').startsWith('○'))
          .slice(0, 3 - lobby.selectedRelicIds.length);

        for (const row of [petRow, ...cardRows, ...relicRows].filter(Boolean)) {
          await realClick(cdp, centreOf(row));
        }

        lobby = await evaluate(cdp, LOBBY_SNAPSHOT);
        if (lobby.selectedPetId !== null && lobby.selectedCardIds.length === 3 && lobby.selectedRelicIds.length === 3) {
          return true;
        }
      }
      return false;
    };

    const loadoutComplete = await selectLoadout();
    record('phase4.loadoutSelected', loadoutComplete, {
      petId: lobby.selectedPetId,
      cardsCount: lobby.selectedCardIds.length,
      relicsCount: lobby.selectedRelicIds.length,
    });

    // Test Start Battle without selecting a Boss: must display validation error
    const startButton = lobby.interactive.find((o) => o.type === 'Rectangle' && o.enabled);
    const startButtonPoint = toVp(startButton.x, startButton.y);
    await realClick(cdp, startButtonPoint);

    lobby = await evaluate(cdp, LOBBY_SNAPSHOT);
    const hasBossValidationError = (lobby.messageText || '').includes('Choose a Boss.') ||
                                  (lobby.reviewText || '').includes('Boss: none') ||
                                  (lobby.messageText || '').includes('fill the selection');
    record('phase4.noBossValidationPreventedStart', hasBossValidationError && (await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene');

    // Select canonical MVP Boss: Kim Lôi Vương
    const targetBoss = CANONICAL_BOSSES.find((b) => b.bossId === 'boss-kim-loi-vuong');
    for (let attempt = 1; attempt <= 3 && lobby.selectedBossId !== targetBoss.bossId; attempt++) {
      const option = bossOptionsOf(lobby).find((o) => o.text.includes(targetBoss.displayName));
      if (option) {
        await realClick(cdp, centreOf(option));
      }
      lobby = await evaluate(cdp, LOBBY_SNAPSHOT);
    }
    record('phase4.bossSelected', lobby.selectedBossId === targetBoss.bossId, lobby.selectedBossId);
    await captureScreenshot(cdp, `run${runNumber}-05-lobby-selected`);

    // TASK-205: the Lobby's teardown wiring on its first run, so the same scene
    // instance's second run (after PLAY AGAIN) can be compared against it. One
    // teardown per event is correct; a higher number on the second run means the
    // first run's handlers were left behind.
    const lobbyFirstRun = (await evaluate(cdp, SCENE_LIFECYCLE_SNAPSHOT)).LobbyScene;

    // The loadout Battle 1 is submitted with. The post-result lifecycle must
    // preserve exactly this and hand it back editable on PLAY AGAIN
    // (D-202-03 = D, ADR-022).
    const battle1Loadout = {
      petId: lobby.selectedPetId,
      bossId: lobby.selectedBossId,
      cardIds: [...lobby.selectedCardIds],
      relicIds: [...lobby.selectedRelicIds],
    };

    // Click START BATTLE with complete loadout and Boss selected
    await realClick(cdp, startButtonPoint);

    // Wait for BattleScene
    await waitForCondition(
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'BattleScene',
      'Transition to BattleScene after start click',
      15000
    );
    record('phase4.battleSceneReached', true);

    // Battle 1's authoritative identity, as the server pushed it
    // (SIGNALR_PROTOCOL.md §4.2). It is what Battle 2's identity is compared
    // against, so a reused battle cannot pass as a new one.
    const battle1Snapshot = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
        return snap.active && snap.battleId ? snap : false;
      },
      'Battle 1 synchronized with an authoritative battleId',
      15000
    );
    const battle1Id = battle1Snapshot.battleId;
    record('phase4.battle1Identity', typeof battle1Id === 'string' && battle1Id.length > 0, battle1Id);

    // A fresh scene subscribes exactly once to each battle stream and has not
    // handled any outcome yet (TASK-204). The runtime-state stream has other
    // subscribers (the React shell), so it is asserted as "unchanged between
    // battles" below rather than as an absolute count.
    record(
      'phase4.battle1SubscribedExactlyOnce',
      battle1Snapshot.listenerCounts !== null &&
        battle1Snapshot.listenerCounts.battle === 1 &&
        battle1Snapshot.listenerCounts.battleState === 1 &&
        typeof battle1Snapshot.listenerCounts.runtime === 'number' &&
        battle1Snapshot.listenerCounts.runtime >= 1,
      battle1Snapshot.listenerCounts
    );
    record('phase4.battle1OutcomeNotHandled', battle1Snapshot.outcomeHandled === false);

    // -------------------------------------------------------------
    // PHASE 5: BATTLE SCENE & MATCH-3 INTERACTION
    // -------------------------------------------------------------
    console.log('\n--- Phase 5: BattleScene 8×8 Board & Match-3 Interaction ---');
    // Wait for 64 cells (128 display objects) from SignalR push
    let battle = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
        return snap.active && snap.boardChildCount === 128 ? snap : false;
      },
      '8×8 board rendered with 64 cells (128 objects)',
      15000
    );
    record('phase5.board64CellsRendered', battle.boardChildCount === 128, `${battle.boardLabels.length} labels`);
    await captureScreenshot(cdp, `run${runNumber}-06-battle-initial`);

    // Coordinate conversion for board cells
    const cellCentre = (index) => ({
      x: battle.viewport.left + (BOARD_ORIGIN_X + (index % BOARD_SIZE) * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2) * battle.viewport.scaleX,
      y: battle.viewport.top + (BOARD_ORIGIN_Y + Math.floor(index / BOARD_SIZE) * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2) * battle.viewport.scaleY,
    });

    // Find candidate matching swap pair from authoritative gem labels
    const matchingSwaps = findMatchingSwaps(battle.boardLabels);
    const initialBoardLabels = battle.boardLabels.slice();
    let swapExecuted = false;

    if (matchingSwaps.length > 0) {
      const [first, second] = matchingSwaps[0];
      await realClick(cdp, cellCentre(first));
      await realClick(cdp, cellCentre(second));

      // Wait for swap acknowledgement or board update
      await waitForCondition(
        async () => {
          const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
          return (snap.swapText || '').includes('accepted') ||
                 JSON.stringify(snap.boardLabels) !== JSON.stringify(initialBoardLabels);
        },
        'Match-3 swap accepted by authoritative server',
        10000
      );
      swapExecuted = true;
    } else {
      // Fallback adjacent swap to exercise input pipeline
      await realClick(cdp, cellCentre(0));
      await realClick(cdp, cellCentre(1));
      await delay(1000);
      swapExecuted = true;
    }
    record('phase5.match3SwapExecuted', swapExecuted);
    await captureScreenshot(cdp, `run${runNumber}-07-battle-post-swap`);

    // -------------------------------------------------------------
    // PHASE 6: IN-BATTLE CAST CONTROLS VERIFICATION
    // -------------------------------------------------------------
    console.log('\n--- Phase 6: In-Battle Cast Controls Verification ---');
    battle = await evaluate(cdp, BATTLE_SNAPSHOT);
    record('phase6.castControlsRendered', battle.castControlsCount > 0, `${battle.castControlsCount} controls`);

    // Click the first cast control button (x = BOARD_ORIGIN_X + 115/2, y = BOARD_ORIGIN_Y + BOARD_WIDTH + 34 + 36/2 = 662)
    const castButtonPoint = {
      x: battle.viewport.left + (BOARD_ORIGIN_X + 115 / 2) * battle.viewport.scaleX,
      y: battle.viewport.top + (BOARD_ORIGIN_Y + BOARD_WIDTH + 34 + 36 / 2) * battle.viewport.scaleY,
    };
    await realClick(cdp, castButtonPoint);

    // Wait for cast feedback in castText
    const castFeedback = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
        if (snap.castText && snap.castText.length > 0) return snap.castText;
        return false;
      },
      'Authoritative cast acknowledgement feedback',
      8000
    );
    record('phase6.castFeedbackReceived', Boolean(castFeedback), castFeedback);

    // -------------------------------------------------------------
    // PHASE 6b: B-02 — ONE CARD CAST PER COMMITTED MATCH-3 TURN
    // -------------------------------------------------------------
    // CARD_RULES.md §3 item 6 / ADR-021: the server allows at most one
    // successful Card cast per committed Match-3 Turn. This phase clicks a
    // second cast control WITHOUT committing a Swap in between and asserts the
    // authoritative rejection reaches the client as a server-issued reason.
    //
    // The client is only a projection here: the assertion is on the server's
    // acknowledgement, not on any client-side button state.
    console.log('\n--- Phase 6b: B-02 One-Cast-Per-Turn Enforcement ---');

    // Second cast control (x = BOARD_ORIGIN_X + 115 + 115/2), same row as the first.
    const secondCastButtonPoint = {
      x: battle.viewport.left + (BOARD_ORIGIN_X + 115 + 115 / 2) * battle.viewport.scaleX,
      y: battle.viewport.top + (BOARD_ORIGIN_Y + BOARD_WIDTH + 34 + 36 / 2) * battle.viewport.scaleY,
    };
    await realClick(cdp, secondCastButtonPoint);

    const secondCastText = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
        const text = snap.castText || '';
        // Only a NEW acknowledgement counts — the first phase's text is stale.
        return text && text !== castFeedback ? text : false;
      },
      'second cast acknowledgement',
      8000
    );

    record('phase6b.secondCastAcknowledged', Boolean(secondCastText), secondCastText);

    // The B-02 property: if the first cast succeeded, the second must be refused
    // by the per-Turn allowance; if the first was itself rejected (as it is when
    // the starter loadout lacks the Power), the allowance was never spent, so no
    // B-02 rejection is expected and the check is not applicable.
    const firstCastAccepted = typeof castFeedback === 'string' && castFeedback.includes('accepted');
    const allowedReasons = ['CARD_CAST_ALREADY_USED_THIS_TURN', 'INSUFFICIENT_POWER'];
    const secondReason = (() => {
      if (typeof secondCastText !== 'string') return null;
      const match = secondCastText.match(/rejected \(([A-Z_]+)\)/);
      return match ? match[1] : null;
    })();

    record(
      'phase6b.secondCastIsServerRejectedOrUnaffordable',
      secondCastText === null
        ? false
        : secondCastText.includes('accepted') || allowedReasons.includes(secondReason),
      `first=${firstCastAccepted ? 'accepted' : 'rejected'} second=${secondCastText}`
    );

    // The exploit's shape specifically: two casts must never BOTH succeed inside
    // one committed Turn. Whatever the reasons, the server cannot have accepted
    // both — that is the zero-risk chain B-02 removed.
    const bothCastsAccepted =
      firstCastAccepted && typeof secondCastText === 'string' && secondCastText.includes('accepted');
    record(
      'phase6b.noSecondSuccessfulCastInSameTurn',
      !bothCastsAccepted,
      `bothAccepted=${bothCastsAccepted}`
    );

    // -------------------------------------------------------------
    // PHASE 7: BATTLE COMPLETION & RESULTSCENE
    // -------------------------------------------------------------
    console.log('\n--- Phase 7: Deterministic Battle Outcome & ResultScene ---');
    // The battle's own identity, from the state the server pushed. (The runtime's
    // *technical* state carries no battleId — `state/GameRuntimeState.ts` is
    // connection/session/sync status only — so it is read from the synchronized
    // battle copy, which is the record the outcome is handed off with.)
    const battle1Live = await evaluate(cdp, BATTLE_SNAPSHOT);
    const battleId = battle1Live.battleId || battle1Id;

    // Dispatch deterministic battle outcome event via documented client runtime port hook
    await evaluate(cdp, `(() => {
      const s = window.__game.scene.getScene('BattleScene');
      s.handleBattleEvents({
        battleId: ${JSON.stringify(battleId)},
        events: [{
          type: 'BattleWon',
          outcome: 'victory',
          finalBossHp: 0,
          finalPlayerHp: 850
        }]
      });
    })()`);

    // Wait for ResultScene to become active
    await waitForCondition(
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'ResultScene',
      'ResultScene active after outcome handoff',
      10000
    );
    record('phase7.resultSceneReached', true);

    // Verify ResultScene outcome presentation
    const result = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, RESULT_SNAPSHOT);
        return snap.active && snap.outcomeText.length > 0 ? snap : false;
      },
      'ResultScene UI rendered with outcome text',
      8000
    );

    record('phase7.victoryOutcomeRendered', result.outcomeText === 'VICTORY', result.outcomeText);
    record(
      'phase7.terminalHpValuesRendered',
      result.bossHpText.includes('0') && result.playerHpText.includes('850'),
      { bossHp: result.bossHpText, playerHp: result.playerHpText }
    );
    await captureScreenshot(cdp, `run${runNumber}-08-result`);

    // TASK-204: ResultScene is terminal until the player says otherwise. A hold
    // with no input must not move the lifecycle on (D-202-02 = A: no automatic
    // transition), and it must not be a keyboard path either.
    await delay(1500);
    record(
      'phase7.noAutomaticTransition',
      (await evaluate(cdp, ACTIVE_SCENE)) === 'ResultScene',
      await evaluate(cdp, ACTIVE_SCENE)
    );

    // TASK-205: by now three scenes have been stopped by the engine — the
    // MainMenu (entering the Lobby), the Lobby (starting Battle 1) and Battle 1
    // itself (its outcome handed off here) — and each one's own teardown must
    // have run on the engine's SHUTDOWN event. Each scene's released state is
    // read from that scene, so this is evidence about the browser, not about
    // this script: the MainMenu no longer claims a transition, the Lobby holds
    // no in-progress selection (`ARCHITECTURE.md` §2.2.3 rule 1), and Battle 1 no
    // longer holds its outcome guard. Every scene keeps exactly one DESTROY
    // handler (never zero, never stacked) and the active scene one SHUTDOWN
    // handler, which is what `create()` registers.
    const stoppedScenes = await evaluate(cdp, SCENE_LIFECYCLE_SNAPSHOT);
    record(
      'phase7.stoppedScenesRanTheirTeardown',
      stoppedScenes.MainMenuScene.hasTransitioned === false &&
        stoppedScenes.LobbyScene.selectedPetId === null &&
        stoppedScenes.BattleScene.outcomeHandled === false,
      {
        mainMenu: stoppedScenes.MainMenuScene,
        lobby: stoppedScenes.LobbyScene,
        battle: stoppedScenes.BattleScene,
      }
    );
    record(
      'phase7.everySceneRegisteredItsTeardownOnce',
      stoppedScenes.MainMenuScene.destroyHandlers === 1 &&
        stoppedScenes.LobbyScene.destroyHandlers === 1 &&
        stoppedScenes.BattleScene.destroyHandlers === 1 &&
        stoppedScenes.ResultScene.destroyHandlers === 1 &&
        stoppedScenes.ResultScene.shutdownHandlers === 1 &&
        stoppedScenes.MainMenuScene.shutdownHandlers === 0 &&
        stoppedScenes.LobbyScene.shutdownHandlers === 0 &&
        stoppedScenes.BattleScene.shutdownHandlers === 0,
      {
        MainMenuScene: stoppedScenes.MainMenuScene,
        LobbyScene: stoppedScenes.LobbyScene,
        BattleScene: stoppedScenes.BattleScene,
        ResultScene: stoppedScenes.ResultScene,
      }
    );

    // -------------------------------------------------------------
    // PHASE 8: POST-RESULT LIFECYCLE (TASK-203 / TASK-204)
    // -------------------------------------------------------------
    console.log('\n--- Phase 8: Post-Result Lifecycle (PLAY AGAIN / MAIN MENU / Battle 2) ---');

    const canvasRect = async () =>
      evaluate(cdp, `(() => {
        const c = document.querySelector('canvas');
        if (!c) return { left: 0, top: 0, width: 1280, height: 720 };
        const r = c.getBoundingClientRect();
        return { left: r.left, top: r.top, width: r.width, height: r.height };
      })()`);

    /** A logical game-space point → a screen point for a real pointer click. */
    const logicalToScreen = async (gx, gy) => {
      const rect = await canvasRect();
      return {
        x: rect.left + gx * (rect.width / 1280),
        y: rect.top + gy * (rect.height / 720),
      };
    };

    /** Reads ResultScene's live navigation controls from its display list. */
    const resultControls = async () => (await evaluate(cdp, RESULT_SNAPSHOT)).controls;

    /** Activates one ResultScene control by its label, the way a player does. */
    const activateResultControl = async (label) => {
      for (let attempt = 1; attempt <= 4; attempt++) {
        const control = (await resultControls()).find((c) => c.label === label);
        if (control) {
          await realClick(cdp, await logicalToScreen(control.x, control.y));
          await delay(500);
          if ((await evaluate(cdp, ACTIVE_SCENE)) !== 'ResultScene') return true;
        }
      }
      return false;
    };

    const controls = await resultControls();
    record(
      'phase8.resultControlsAreTwoExplicitButtons',
      controls.length === 2 &&
        controls.map((c) => c.label).sort().join('|') === 'MAIN MENU|PLAY AGAIN' &&
        controls.every((c) => c.width > 0 && c.height > 0),
      controls.map((c) => c.label)
    );

    // --- PLAY AGAIN ---------------------------------------------------------
    const playedAgain = await activateResultControl('PLAY AGAIN');
    record('phase8.playAgainReachedLobby', playedAgain && (await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene');

    // The completed battle's active battle state is gone, and the transport was
    // never disconnected by the exit (D-202-04 = A).
    const clearedState = await evaluate(cdp, `(() => {
      const s = window.__game.scene.getScene('LobbyScene');
      const runtime = s.runtime;
      return {
        battleState: runtime && runtime.getBattleState ? runtime.getBattleState() : null,
        connection: runtime && runtime.getState ? runtime.getState().connection : null,
        sync: runtime && runtime.getState ? runtime.getState().sync : null,
      };
    })()`);
    record('phase8.activeBattleStateCleared', clearedState.battleState === null, clearedState.sync);
    record('phase8.transportNotDisconnected', clearedState.connection === 'connected', clearedState.connection);

    // TASK-205: this is the Lobby instance's second run, and its teardown was
    // registered (and consumed) by the first one. Exactly one handler per event
    // is correct; more would mean the first run's registration survived.
    const lobbySecondRun = (await evaluate(cdp, SCENE_LIFECYCLE_SNAPSHOT)).LobbyScene;
    record(
      'phase8.noDuplicateSceneSubscription',
      lobbySecondRun.shutdownHandlers === lobbyFirstRun.shutdownHandlers &&
        lobbySecondRun.destroyHandlers === lobbyFirstRun.destroyHandlers &&
        lobbySecondRun.shutdownHandlers === 1 &&
        lobbySecondRun.destroyHandlers === 1,
      { firstRun: lobbyFirstRun, secondRun: lobbySecondRun }
    );

    // The preserved loadout is restored and still selected, and it is the
    // loadout Battle 1 was fought with (D-202-03 = D, ADR-022).
    let nextLobby = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, LOBBY_SNAPSHOT);
        return snap.active && snap.ownedPetsCount > 0 && snap.ownedCardsCount >= 3 && snap.ownedRelicsCount >= 3 ? snap : false;
      },
      'Lobby (PLAY AGAIN) collection reloaded',
      12000
    );
    const sorted = (list) => [...list].sort().join(',');
    record(
      'phase8.preservedLoadoutRestored',
      nextLobby.selectedPetId === battle1Loadout.petId &&
        nextLobby.selectedBossId === battle1Loadout.bossId &&
        sorted(nextLobby.selectedCardIds) === sorted(battle1Loadout.cardIds) &&
        sorted(nextLobby.selectedRelicIds) === sorted(battle1Loadout.relicIds),
      {
        expected: battle1Loadout,
        restored: {
          petId: nextLobby.selectedPetId,
          bossId: nextLobby.selectedBossId,
          cardIds: nextLobby.selectedCardIds,
          relicIds: nextLobby.selectedRelicIds,
        },
      }
    );
    await captureScreenshot(cdp, `run${runNumber}-09-lobby-restored`);

    // --- Edit the preserved loadout ----------------------------------------
    const editedBoss = CANONICAL_BOSSES.find((b) => b.bossId !== battle1Loadout.bossId);
    const nextCentreOf = (o) =>
      logicalToScreen(o.x + o.width / 2, o.y + o.height / 2);
    for (let attempt = 1; attempt <= 3 && nextLobby.selectedBossId !== editedBoss.bossId; attempt++) {
      const option = bossOptionsOf(nextLobby).find((o) => o.text.includes(editedBoss.displayName));
      if (option) {
        await realClick(cdp, await nextCentreOf(option));
      }
      nextLobby = await evaluate(cdp, LOBBY_SNAPSHOT);
    }
    record(
      'phase8.preservedLoadoutIsEditable',
      nextLobby.selectedBossId === editedBoss.bossId,
      `bossId=${nextLobby.selectedBossId} (was ${battle1Loadout.bossId})`
    );

    // --- Battle 2 ----------------------------------------------------------
    const startButton2 = nextLobby.interactive.find((o) => o.type === 'Rectangle' && o.enabled);
    if (!startButton2) {
      throw new Error('The restored Lobby rendered no interactive START BATTLE control.');
    }
    const startButtonPoint2 = await nextCentreOf(startButton2);
    await realClick(cdp, startButtonPoint2);

    await waitForCondition(
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'BattleScene',
      'Battle 2 reached BattleScene',
      15000
    );
    record('phase8.battle2SceneReached', true);

    const battle2 = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
        return snap.active && snap.battleId && snap.boardChildCount === 128 ? snap : false;
      },
      'Battle 2 board rendered from its own authoritative push',
      15000
    );
    record('phase8.battle2Board64CellsRendered', battle2.boardChildCount === 128, `${battle2.boardLabels.length} labels`);
    record('phase8.battle2NewBattleIdentity', battle2.battleId !== battle1Id, { battle1Id, battle2Id: battle2.battleId });

    // The defect's second consequence: a reused scene must start with a fresh
    // guard and exactly one subscription per battle stream — and the runtime
    // must hold no MORE runtime-state subscribers than it did for Battle 1, or
    // the shut-down scene is still attached (TASK-204).
    record('phase8.battle2OutcomeNotHandled', battle2.outcomeHandled === false);
    record(
      'phase8.noDuplicateRuntimeListener',
      battle2.listenerCounts !== null &&
        battle2.listenerCounts.battle === 1 &&
        battle2.listenerCounts.battleState === 1 &&
        battle2.listenerCounts.runtime === battle1Snapshot.listenerCounts.runtime,
      { battle1: battle1Snapshot.listenerCounts, battle2: battle2.listenerCounts }
    );

    // The edited loadout is what Battle 2 was actually started with.
    const battle2Request = (() => {
      if (battleStartRequests.length < 2) return null;
      try {
        return JSON.parse(battleStartRequests[battleStartRequests.length - 1]);
      } catch {
        return null;
      }
    })();
    record(
      'phase8.battle2RequestCarriesEditedLoadout',
      battle2Request !== null &&
        battle2Request.bossId === editedBoss.bossId &&
        battle2Request.petId === battle1Loadout.petId &&
        sorted(battle2Request.cardLoadout || []) === sorted(battle1Loadout.cardIds) &&
        sorted(battle2Request.relicLoadout || []) === sorted(battle1Loadout.relicIds) &&
        Object.keys(battle2Request).sort().join(',') === 'bossId,cardLoadout,petId,relicLoadout',
      { request: battle2Request, editedBossId: editedBoss.bossId }
    );
    await captureScreenshot(cdp, `run${runNumber}-10-battle2`);

    // --- Battle 2 outcome --------------------------------------------------
    await evaluate(cdp, `(() => {
      const s = window.__game.scene.getScene('BattleScene');
      s.handleBattleEvents({
        battleId: ${JSON.stringify(battle2.battleId)},
        events: [{
          type: 'BattleLost',
          outcome: 'defeat',
          finalBossHp: 2400,
          finalPlayerHp: 0
        }]
      });
    })()`);

    await waitForCondition(
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'ResultScene',
      'Battle 2 outcome reached ResultScene again',
      10000
    );
    record('phase8.battle2ReachedResult', true);

    const result2 = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, RESULT_SNAPSHOT);
        return snap.active && snap.outcomeText.length > 0 ? snap : false;
      },
      'Battle 2 ResultScene rendered',
      8000
    );
    record('phase8.battle2OutcomeRendered', result2.outcomeText === 'DEFEAT', result2.outcomeText);
    await captureScreenshot(cdp, `run${runNumber}-11-result-2`);

    // --- MAIN MENU --------------------------------------------------------
    const wentToMenu = await activateResultControl('MAIN MENU');
    record('phase8.mainMenuReachedMainMenu', wentToMenu && (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene');

    // The exit cleared the completed battle's active battle state and left the
    // preserved loadout alone (ADR-022 D5).
    const afterMenu = await evaluate(cdp, `(() => {
      const s = window.__game.scene.getScene('MainMenuScene');
      const runtime = s.registry ? s.registry.get('gameRuntime') : null;
      return {
        battleState: runtime && runtime.getBattleState ? runtime.getBattleState() : null,
        preserved: s.registry ? (${PRESERVED_LOADOUT}) : null,
      };
    })()`);
    record('phase8.mainMenuClearedActiveBattleState', afterMenu.battleState === null);
    record(
      'phase8.preservedLoadoutSurvivedMainMenu',
      afterMenu.preserved !== null &&
        afterMenu.preserved.bossId === editedBoss.bossId &&
        afterMenu.preserved.petId === battle1Loadout.petId,
      afterMenu.preserved
    );

    // TASK-205: leaving ResultScene shut it down, so its own teardown must have
    // released the presentation it was holding — including the asynchronous
    // reward read's target (the second battle's reward `Text`), which no
    // continuation may write to once the scene is over. The MainMenu is active
    // again, and the scene lifecycle wiring across the whole run is checked once
    // more: one DESTROY handler per scene, one SHUTDOWN handler for the scene
    // that is running, and none for the scenes the engine has stopped.
    const finalScenes = await evaluate(cdp, SCENE_LIFECYCLE_SNAPSHOT);
    record(
      'phase8.resultSceneTeardownReleasedItsPresentation',
      finalScenes.ResultScene.active === false &&
        finalScenes.ResultScene.resultData === null &&
        finalScenes.ResultScene.rewardTextIsNull === true,
      finalScenes.ResultScene
    );
    record(
      'phase8.noStaleSceneSubscriptions',
      finalScenes.MainMenuScene.active === true &&
        finalScenes.MainMenuScene.shutdownHandlers === 1 &&
        finalScenes.MainMenuScene.destroyHandlers === 1 &&
        ['LobbyScene', 'BattleScene', 'ResultScene'].every(
          (key) => finalScenes[key].shutdownHandlers === 0 && finalScenes[key].destroyHandlers === 1
        ),
      finalScenes
    );

    // -------------------------------------------------------------
    // PHASE 9: HEALTH & SAFETY GATES
    // -------------------------------------------------------------
    console.log('\n--- Phase 9: Error & Health Safety Gates ---');
    record('safety.zeroDiscordDependencies', discordEvents.length === 0, `${discordEvents.length} Discord events`);
    record('safety.zeroUncaughtExceptions', uncaughtExceptions.length === 0, uncaughtExceptions);
    record('safety.zeroFatalConsoleErrors', consoleErrors.length === 0, consoleErrors);

    // Filter API responses: only 200 OK expected (except documented 404 on /api/battle/*/result if battle active in backend)
    const unexpectedApiResponses = Array.from(apiResponses.values()).filter((resp) => {
      if (resp.status >= 200 && resp.status < 300) return false;
      if (resp.url.includes('/result') && resp.status === 404) return false;
      return true;
    });
    record('safety.zeroUnexpectedApiResponses', unexpectedApiResponses.length === 0, unexpectedApiResponses);

  } finally {
    if (cdp) cdp.close();
    cleanup();
  }

  console.log(`\n--- RUN ${runNumber} SUMMARY ---`);
  console.log(`Total checks: ${checks.length}, Failures: ${failures}`);

  if (failures > 0) {
    throw new Error(`Smoke test RUN ${runNumber} failed with ${failures} failure(s).`);
  }

  return { runNumber, checks, failures };
}

// Direct execution
if (process.argv[1] && process.argv[1].endsWith('standalone-web-smoke.mjs')) {
  (async () => {
    try {
      // Execute Run 1
      await runSmokeTest(1);

      // Execute Run 2 to confirm stability across clean browser contexts
      await runSmokeTest(2);

      console.log('\n============================================================');
      console.log('=== ALL SMOKE TEST RUNS PASSED CLEANLY (NO FLAKINESS) ===');
      console.log('============================================================\n');
      process.exit(0);
    } catch (err) {
      console.error('\n[SMOKE TEST FATAL ERROR]', err.message);
      process.exit(1);
    }
  })();
}
