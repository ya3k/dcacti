/**
 * TASK-206: Battle History & Account Progression — end-to-end browser smoke test.
 *
 * Verifies the read-only Battle History surface against the frozen endpoint
 * (`API_CONTRACTS.md` §4.5), on a **freshly registered account**, through real
 * browser pointer input and **real, server-resolved battles**:
 *
 *   Clean browser profile
 *     ↓
 *   AuthScreen (register a unique account — the repository's own isolation
 *     mechanism: no database rows are deleted and no test-only endpoint exists)
 *     ↓
 *   MainMenuScene — the third entry point exists and the two existing entries
 *     keep their documented coordinates
 *     ↓
 *   BattleHistoryScene — `GET /api/battle/history` → `200 []` on the fresh
 *     account, so the explicit empty state is shown, no Level/XP is fabricated,
 *     and BACK returns to the menu
 *     ↓
 *   Lobby → START BATTLE → BattleScene (a real battle)
 *     ↓
 *   The battle is played to its own terminal resolution by real cell taps, so
 *   the server persists a durable BattleResult row (§4.5 note 10) — a
 *   client-side outcome hand-off would not (no such row would exist)
 *     ↓
 *   ResultScene — the server's own outcome and the persisted `rewards` read back
 *     through `GET /api/battle/{battleId}/result` (§4)
 *     ↓
 *   MAIN MENU → BattleHistoryScene — the completed battle is visible, with every
 *     delivered value rendered verbatim, and the latest delivered Player
 *     Level / XP presented
 *     ↓
 *   Second real battle → Result → MAIN MENU → BattleHistoryScene — both
 *     completed battles are visible, in the delivered (newest-first) order
 *     ↓
 *   Zero uncaught exceptions, zero fatal console errors, zero unexpected API
 *   responses, and the history endpoint is only ever read with a bare
 *   authenticated `GET`
 *
 * Usage:
 *   node scripts/battle-history-smoke.mjs [url]
 *   npm run verify:e2e:history
 *
 * Two clean-context runs by default (the convention the other harnesses use).
 * Harness-only overrides, for iterating on this script:
 *
 *   HISTORY_RUNS=1        run once instead of twice
 *   HISTORY_DEBUG=1       log every Swap attempt, the committed board, and the
 *                         page's focus/loop state
 *   HISTORY_MAX_SWAPS=n   cap the Swaps one battle may take (default 120)
 *
 * The transport/harness helpers below are the repository's existing CDP smoke
 * harness (see `standalone-web-smoke.mjs` and `collection-viewer-smoke.mjs`), not
 * a second browser framework.
 */

import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const FRONTEND_URL = process.argv[2] || process.env.URL_UNDER_TEST || 'http://localhost:5173/';
const BACKEND_URL = process.env.BACKEND_URL || 'http://localhost:5000';
const DEBUG_PORT = Number(process.env.DEBUG_PORT) || 9292;
const OUT_DIR = 'battle-history-shots';

/** The canonical Boss the two battles are fought against (BOSS_RULES.md §6.4). */
const BATTLE_BOSS = {
  displayName: 'Kim Lôi Vương',
  bossId: 'boss-kim-loi-vuong',
  element: 'Kim',
};

/** The menu's documented button centres — the battle entry must not move. */
const START_BATTLE_BUTTON = { x: 640, y: 324, width: 280, height: 50 };
const COLLECTION_BUTTON = { x: 640, y: 394, width: 280, height: 50 };
const BATTLE_HISTORY_BUTTON = { x: 640, y: 464, width: 280, height: 50 };

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

/** Board constants (MATCH3_RULES.md §1.0 and BattleScene.ts). */
const BOARD_SIZE = 8;
const CELL_SIZE = 56;
const CELL_GAP = 6;
const BOARD_WIDTH = BOARD_SIZE * CELL_SIZE + (BOARD_SIZE - 1) * CELL_GAP; // 490
const BOARD_ORIGIN_X = (1280 - BOARD_WIDTH) / 2; // 395
const BOARD_ORIGIN_Y = 24 + 96; // 120

/**
 * The largest number of committed Swaps a single battle is allowed to take
 * before the run reports a failure. The MVP Bosses end a battle in well under
 * this (the starter Pet's 1000 HP against a 140-ATK Boss), so the bound is a
 * safety net rather than an expected budget.
 */
const MAX_SWAPS_PER_BATTLE = Number(process.env.HISTORY_MAX_SWAPS) || 120;

/** Verbose per-Swap diagnostics for iterating on this harness itself. */
const DEBUG = process.env.HISTORY_DEBUG === '1';

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
  await delay(60);
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mousePressed', buttons: 1 });
  await delay(50);
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mouseReleased', buttons: 0 });
  await delay(120);
}

/** Clicks `pointFn()` until `predicate()` holds, or gives up after `attempts`. */
async function clickUntil(cdp, pointFn, predicate, description, attempts = 8) {
  for (let i = 0; i < attempts; i++) {
    if (await predicate()) return true;
    await realClick(cdp, await pointFn());
    await delay(700);
  }
  if (await predicate()) return true;
  throw new Error(`Timeout clicking until: ${description}`);
}

/** Fills a controlled React input with real pointer focus + native value dispatch. */
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
  for (const key of ['MainMenuScene', 'BattleHistoryScene', 'LobbyScene', 'BattleScene', 'ResultScene']) {
    const s = window.__game.scene.getScene(key);
    if (s && s.scene && s.scene.isActive && s.scene.isActive()) return key;
  }
  return 'none';
})()
`;

/** The MainMenu's live interactive hit areas, in logical game coordinates. */
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

const LOBBY_SNAPSHOT = `
(() => {
  const s = window.__game && window.__game.scene.getScene('LobbyScene');
  if (!s || !s.scene.isActive()) return { active: false };
  const canvas = document.querySelector('canvas');
  if (!canvas) return { active: true, reason: 'no canvas' };
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
    controls,
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
    outcomeHandled: s.outcomeHandled === true,
    bossHpText: typeof s.battleText === 'object' && s.battleText ? s.battleText.text : null,
    boardChildCount: s.boardLayer && s.boardLayer.list ? s.boardLayer.list.length : 0,
    boardLabels: s.boardLayer && s.boardLayer.list
      ? s.boardLayer.list.filter((o) => o.type === 'Text').map((o) => o.text)
      : [],
    swapText: s.swapText && typeof s.swapText.text === 'string' ? s.swapText.text : '',
    viewport: { left: rect.left, top: rect.top, scaleX: rect.width / gameWidth, scaleY: rect.height / gameHeight },
  };
})()
`;

const RESULT_SNAPSHOT = `
(() => {
  const s = window.__game && window.__game.scene.getScene('ResultScene');
  if (!s || !s.scene.isActive()) return { active: false, controls: [] };
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
    battleId: s.resultData && typeof s.resultData.battleId === 'string' ? s.resultData.battleId : null,
    controls,
  };
})()
`;

/**
 * The BattleHistoryScene's observable presentation state (TASK-206).
 *
 * `entries` are the scene's own rendered history blocks — one per delivered
 * `GET /api/battle/history` element, in the delivered order — `progression` is
 * the account-progression line it drew from the newest delivered element, and
 * `controls` are its interactive rectangles with the captions drawn over them.
 */
const HISTORY_SNAPSHOT = `
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
    teardownShutdownHandlers: (s.events.listeners('shutdown') || []).filter((fn) => fn === s.shutdown).length,
    teardownDestroyHandlers: (s.events.listeners('destroy') || []).filter((fn) => fn === s.shutdown).length,
    shellObjectCount: (s.shellObjects || []).length,
    renderedTextCount: (s.renderedTexts || []).length,
    interactiveObjectCount: (s.interactiveObjects || []).length,
    viewport: { left: rect.left, top: rect.top, scaleX: rect.width / gameWidth, scaleY: rect.height / gameHeight },
  };
})()
`;

/** The page's own focus/loop state, so a paused game loop is visible in debug. */
const LOOP_STATE = `
(() => ({
  hasFocus: document.hasFocus(),
  visibility: document.visibilityState,
  loopRunning: window.__game && window.__game.loop ? !!window.__game.loop.running : null,
  loopInFocus: window.__game && window.__game.loop ? !!window.__game.loop.inFocus : null,
}))()
`;

/**
 * Finds an adjacent cell pair whose swap produces a Match-3 on the delivered
 * board.
 *
 * The client owns no board rule: this is the smoke test's own input helper, and
 * whether the swap is legal is decided by the server
 * (`MATCH3_RULES.md` §2.1.2) — a rejected candidate simply commits no Turn and
 * the next candidate is tried.
 */
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

/** The screen point of one board cell, from a live battle snapshot. */
function cellCentre(snapshot, index) {
  return {
    x:
      snapshot.viewport.left +
      (BOARD_ORIGIN_X + (index % BOARD_SIZE) * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2) *
        snapshot.viewport.scaleX,
    y:
      snapshot.viewport.top +
      (BOARD_ORIGIN_Y + Math.floor(index / BOARD_SIZE) * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2) *
        snapshot.viewport.scaleY,
  };
}

/**
 * Plays the active battle to its own terminal resolution with real pointer
 * input.
 *
 * This is what makes the history assertion meaningful: a `BattleResult` row
 * exists only after the server resolves a terminal outcome
 * (`DATABASE.md` §1, `API_CONTRACTS.md` §4.5 note 10), so the battle is fought —
 * not hand-off simulated — and the run waits for the engine to move the
 * presentation to `ResultScene` on the server's own `BattleWon` / `BattleLost`.
 *
 * A candidate whose swap the server rejects commits nothing, so the pair is
 * remembered for the board it was rejected on and the next candidate is tried:
 * the documented `STALE_ACTION` answer — "the action's unordered pair is the
 * pair most recently committed" (`MATCH3_RULES.md` §2.1.4) — would otherwise be
 * resubmitted forever, because a rejected swap leaves the board unchanged. The
 * record is cleared as soon as the authoritative board changes.
 */
async function driveBattleToTerminal(cdp, maxSwaps = MAX_SWAPS_PER_BATTLE) {
  let swaps = 0;
  const started = Date.now();
  /** Pairs the server rejected on the board currently on screen. */
  let rejectedPairs = new Set();
  let lastBoardKey = null;
  let lastFeedback = '';

  const pairKey = (a, b) => (a < b ? `${a}-${b}` : `${b}-${a}`);

  while (swaps < maxSwaps) {
    if ((await evaluate(cdp, ACTIVE_SCENE)) !== 'BattleScene') break;

    const battle = await evaluate(cdp, BATTLE_SNAPSHOT);
    if (!battle.active) break;
    if (battle.boardLabels.length !== BOARD_SIZE * BOARD_SIZE) {
      await delay(400);
      continue;
    }

    const boardKey = JSON.stringify(battle.boardLabels);
    if (boardKey !== lastBoardKey) {
      lastBoardKey = boardKey;
      rejectedPairs = new Set();
    }

    const candidates = findMatchingSwaps(battle.boardLabels).filter(
      ([from, to]) => !rejectedPairs.has(pairKey(from, to))
    );
    const [from, to] = candidates.length > 0 ? candidates[0] : [0, 1];
    const beforeFeedback = lastFeedback;

    await realClick(cdp, cellCentre(battle, from));
    await realClick(cdp, cellCentre(battle, to));
    swaps += 1;

    // Wait for either the authoritative push that follows a committed Swap (the
    // board changes) or the acknowledgement feedback a rejection produces. Both
    // are bounded: a rejection is an answer, not a hang.
    try {
      await waitForCondition(
        async () => {
          if ((await evaluate(cdp, ACTIVE_SCENE)) !== 'BattleScene') return true;
          const current = await evaluate(cdp, BATTLE_SNAPSHOT);
          if (JSON.stringify(current.boardLabels) !== boardKey) return true;
          return Boolean(current.swapText) && current.swapText !== beforeFeedback;
        },
        'Swap acknowledgement or committed board',
        6000,
        120
      );
    } catch {
      /* no acknowledgement: treat the pair as rejected and move on */
    }

    const after = await evaluate(cdp, BATTLE_SNAPSHOT);
    if (JSON.stringify(after.boardLabels) === boardKey) {
      rejectedPairs.add(pairKey(from, to));
    }
    lastFeedback = after.swapText;

    if (DEBUG) {
      const committed = JSON.stringify(after.boardLabels) !== boardKey;
      const loop = await evaluate(cdp, LOOP_STATE);
      console.log(
        `    [swap ${swaps}] candidate ${from}->${to} candidates=${candidates.length} ` +
          `committed=${committed} swapText="${after.swapText}" loop=${JSON.stringify(loop)}\n` +
          `      ${String(after.bossHpText).split('\n').join(' | ')}`
      );
    }
  }

  const activeScene = await evaluate(cdp, ACTIVE_SCENE);
  return {
    ended: activeScene === 'ResultScene',
    swaps,
    activeScene,
    seconds: Math.round((Date.now() - started) / 1000),
  };
}

/**
 * Reads the frozen history endpoint from the page itself — the same origin, the
 * same session the app established. This is the authoritative comparison the
 * rendered surface is checked against.
 */
const READ_HISTORY = `
(async () => {
  const token = localStorage.getItem('dcacti_session_token');
  const response = await fetch('/api/battle/history', {
    method: 'GET',
    headers: token ? { Authorization: 'Bearer ' + token } : {},
  });
  let body = null;
  try { body = await response.json(); } catch { body = null; }
  return { status: response.status, body };
})()
`;

/** Reads one persisted battle result from the page for the same reason. */
const readResultExpression = (battleId) => `
(async () => {
  const token = localStorage.getItem('dcacti_session_token');
  const response = await fetch('/api/battle/${battleId}/result', {
    method: 'GET',
    headers: token ? { Authorization: 'Bearer ' + token } : {},
  });
  let body = null;
  try { body = await response.json(); } catch { body = null; }
  return { status: response.status, body };
})()
`;

/*
 * The rendering the scene performs for one delivered element, spelled here so
 * the browser assertions can compare against the delivered values. The unit
 * suite pins the rendering itself; this pins that the *delivered* values are the
 * ones that reach the screen.
 */
const formatDelivered = (value) => (value === null || value === undefined ? '—' : `${value}`);
const formatLeveledUp = (value) =>
  value === null || value === undefined ? '  ·  —' : value ? '  ·  LEVEL UP' : '';
const playerRewardLine = (rewards) =>
  `Player: +${rewards.playerXpGained} XP  ·  XP ${formatDelivered(rewards.newPlayerXp)}  ·  Level ${formatDelivered(rewards.newPlayerLevel)}${formatLeveledUp(rewards.playerLeveledUp)}`;
const petRewardLine = (rewards) =>
  `Pet: +${rewards.petXpGained} XP  ·  XP ${formatDelivered(rewards.newPetXp)}  ·  Level ${formatDelivered(rewards.newPetLevel)}${formatLeveledUp(rewards.petLeveledUp)}`;
const progressionLine = (rewards) =>
  `Level ${formatDelivered(rewards.newPlayerLevel)}   ·   XP ${formatDelivered(rewards.newPlayerXp)}${formatLeveledUp(rewards.playerLeveledUp)}`;

export async function runBattleHistorySmokeTest(runNumber = 1) {
  console.log(`\n============================================================`);
  console.log(`=== RUN ${runNumber}: BATTLE HISTORY BROWSER SMOKE TEST ===`);
  console.log(`============================================================`);

  await checkPreflightHealth();

  const browserBin = resolveBrowserPath();
  const profileDir = join(
    tmpdir(),
    `dcacti-history-${Date.now()}-${Math.random().toString(16).slice(2, 6)}`
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
    /** Every `/api/battle/history` request the page made, with its method. */
    const historyRequests = [];

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
      if (url.includes('/api/battle/history')) {
        historyRequests.push({ url, method: p.request?.method ?? null });
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
    // A headless target is not focused by default, and Phaser's `TimeStep` skips
    // its updates while the window is blurred (`this.inFocus`) — which stops
    // input processing and tween completion, so a battle would freeze mid-fight.
    // Focus emulation is what keeps the page's own focus state true for the whole
    // run; the fallback keeps the harness usable on a browser that lacks it.
    try {
      await cdp.send('Emulation.setFocusEmulationEnabled', { enabled: true });
    } catch {
      /* older browser: continue without focus emulation */
    }
    try {
      await cdp.send('Page.bringToFront');
    } catch {
      /* ignore */
    }
    await cdp.send('Emulation.setDeviceMetricsOverride', {
      width: 1280,
      height: 720,
      deviceScaleFactor: 1,
      mobile: false,
    });

    // -------------------------------------------------------------
    // PHASE 1: FRESH ACCOUNT
    // -------------------------------------------------------------
    console.log('\n--- Phase 1: Fresh Account Registration ---');
    await cdp.send('Page.navigate', { url: FRONTEND_URL });

    await waitForCondition(
      async () => evaluate(cdp, `document.querySelector('[data-testid="auth-screen"]') !== null`),
      'AuthScreen container mounted'
    );

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

    // The repository's own isolation mechanism: a brand-new account per run, so
    // the empty-history case is a genuinely empty account. No database rows are
    // deleted and no test-only backend behavior exists.
    const testUsername = `history_${Date.now()}_${Math.random().toString(16).slice(2, 6)}`;
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
    // PHASE 2: MAIN MENU ENTRY POINT
    // -------------------------------------------------------------
    console.log('\n--- Phase 2: MainMenu BATTLE HISTORY Entry Point ---');
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
    await captureScreenshot(cdp, `run${runNumber}-01-mainmenu`);

    record('phase2.battleHistoryButtonPresent', Boolean(menuButton('BATTLE HISTORY')));
    record(
      'phase2.battleHistoryButtonAtDocumentedCentre',
      Boolean(
        menuButton('BATTLE HISTORY') &&
          menuButton('BATTLE HISTORY').x === BATTLE_HISTORY_BUTTON.x &&
          menuButton('BATTLE HISTORY').y === BATTLE_HISTORY_BUTTON.y
      ),
      menuButton('BATTLE HISTORY')
    );
    // The two existing entries keep their own centres: the third button must not
    // move — or overlap — the battle or collection targets.
    record(
      'phase2.startBattleNotMoved',
      Boolean(
        menuButton('START BATTLE') &&
          menuButton('START BATTLE').x === START_BATTLE_BUTTON.x &&
          menuButton('START BATTLE').y === START_BATTLE_BUTTON.y
      ),
      menuButton('START BATTLE')
    );
    record(
      'phase2.collectionNotMoved',
      Boolean(
        menuButton('COLLECTION') &&
          menuButton('COLLECTION').x === COLLECTION_BUTTON.x &&
          menuButton('COLLECTION').y === COLLECTION_BUTTON.y
      ),
      menuButton('COLLECTION')
    );
    record(
      'phase2.menuButtonsDoNotOverlap',
      [START_BATTLE_BUTTON, COLLECTION_BUTTON, BATTLE_HISTORY_BUTTON].every(
        (button, index, all) =>
          index === all.length - 1 ||
          button.y + button.height / 2 < all[index + 1].y - all[index + 1].height / 2
      )
    );

    const menuPoint = async (button) => {
      const live = await evaluate(cdp, MAIN_MENU_SNAPSHOT);
      return {
        x: live.viewport.left + button.x * live.viewport.scaleX,
        y: live.viewport.top + button.y * live.viewport.scaleY,
      };
    };

    // -------------------------------------------------------------
    // PHASE 3: EMPTY HISTORY ON THE FRESH ACCOUNT
    // -------------------------------------------------------------
    console.log('\n--- Phase 3: Empty Battle History on a fresh account ---');
    await clickUntil(
      cdp,
      () => menuPoint(BATTLE_HISTORY_BUTTON),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'BattleHistoryScene',
      'BattleHistoryScene active after the BATTLE HISTORY click'
    );
    record('phase3.battleHistoryOpened', true);

    const emptyHistory = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, HISTORY_SNAPSHOT);
        return snap.active && !snap.loading ? snap : false;
      },
      'Battle History read settled',
      12000
    );
    await captureScreenshot(cdp, `run${runNumber}-02-history-empty`);

    record(
      'phase3.explicitEmptyState',
      emptyHistory.loadError === null &&
        emptyHistory.history.length === 0 &&
        emptyHistory.status.includes('No battles yet') &&
        emptyHistory.entries.length === 0,
      { status: emptyHistory.status, entries: emptyHistory.entries, error: emptyHistory.loadError }
    );
    record(
      'phase3.noFabricatedLevelOrXp',
      !/Level \d/.test(emptyHistory.progression) &&
        !/XP \d/.test(emptyHistory.progression) &&
        !emptyHistory.texts.some((t) => /Level \d/.test(t) || /XP \d/.test(t)),
      emptyHistory.progression
    );
    record(
      'phase3.emptyIsNotAnErrorState',
      !(emptyHistory.controls || []).some((c) => c.label === 'RETRY') &&
        (emptyHistory.controls || []).some((c) => c.label === '< BACK'),
      (emptyHistory.controls || []).map((c) => c.label)
    );
    record(
      'phase3.teardownRegisteredOnEngineLifecycleEvents',
      emptyHistory.teardownShutdownHandlers === 1 && emptyHistory.teardownDestroyHandlers === 1,
      {
        shutdownHandlers: emptyHistory.teardownShutdownHandlers,
        destroyHandlers: emptyHistory.teardownDestroyHandlers,
      }
    );

    /** The live click point of the history scene's control captioned `label`. */
    const historyControlPoint = async (label) => {
      const snap = await evaluate(cdp, HISTORY_SNAPSHOT);
      const control = (snap.controls || []).find((c) => c.label === label);
      if (!control) throw new Error(`BattleHistoryScene rendered no "${label}" control.`);
      const live = await evaluate(cdp, MAIN_MENU_SNAPSHOT);
      const viewport = snap.viewport ?? {
        left: live.viewport.left,
        top: live.viewport.top,
        scaleX: live.viewport.scaleX,
        scaleY: live.viewport.scaleY,
      };
      return {
        x: viewport.left + control.x * viewport.scaleX,
        y: viewport.top + control.y * viewport.scaleY,
      };
    };

    await clickUntil(
      cdp,
      () => historyControlPoint('< BACK'),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene',
      '< BACK returned to MainMenuScene'
    );
    record('phase3.backReturnedToMainMenu', true);

    // -------------------------------------------------------------
    // PHASE 4: FIRST REAL BATTLE
    // -------------------------------------------------------------
    console.log('\n--- Phase 4: First real battle ---');
    await clickUntil(
      cdp,
      () => menuPoint(START_BATTLE_BUTTON),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene',
      'LobbyScene active after START BATTLE'
    );

    let lobby = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, LOBBY_SNAPSHOT);
        return snap.active && snap.ownedPetsCount > 0 && snap.ownedCardsCount >= 3 && snap.ownedRelicsCount >= 3
          ? snap
          : false;
      },
      'Lobby collections loaded from the starter grant',
      12000
    );
    record('phase4.starterGrantLoaded', true, {
      pets: lobby.ownedPetsCount,
      cards: lobby.ownedCardsCount,
      relics: lobby.ownedRelicsCount,
    });

    const toVp = (viewport, gx, gy) => ({
      x: viewport.left + gx * viewport.scaleX,
      y: viewport.top + gy * viewport.scaleY,
    });
    const centreOf = (snapshot, o) =>
      toVp(snapshot.viewport, o.x + o.width / 2, o.y + o.height / 2);

    const lobbyControlsOf = (snap) => snap.controls || [];
    const lobbyControlOf = (snap, label) => {
      const control = lobbyControlsOf(snap).find((c) => c.label === label);
      if (!control) {
        throw new Error(`LobbyScene rendered no "${label}" control.`);
      }
      return control;
    };

    const bossOptionsOf = (snapshot) =>
      snapshot.interactive.filter(
        (o) => o.text !== null && o.text.includes(BATTLE_BOSS.displayName)
      );

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
          .filter(
            (o) =>
              o.x > 700 && !(o.text || '').includes('[Basic]') && (o.text || '').startsWith('○')
          )
          .slice(0, 3 - lobby.selectedRelicIds.length);

        for (const row of [petRow, ...cardRows, ...relicRows].filter(Boolean)) {
          await realClick(cdp, centreOf(lobby, row));
        }

        lobby = await evaluate(cdp, LOBBY_SNAPSHOT);
        if (
          lobby.selectedPetId !== null &&
          lobby.selectedCardIds.length === 3 &&
          lobby.selectedRelicIds.length === 3
        ) {
          return true;
        }
      }
      return false;
    };

    const loadoutSelected = await selectLoadout();
    record('phase4.loadoutSelected', loadoutSelected, {
      petId: lobby.selectedPetId,
      cards: lobby.selectedCardIds.length,
      relics: lobby.selectedRelicIds.length,
    });

    for (let attempt = 1; attempt <= 3 && lobby.selectedBossId !== BATTLE_BOSS.bossId; attempt++) {
      const option = bossOptionsOf(lobby)[0];
      if (option) {
        await realClick(cdp, centreOf(lobby, option));
      }
      lobby = await evaluate(cdp, LOBBY_SNAPSHOT);
    }
    record('phase4.bossSelected', lobby.selectedBossId === BATTLE_BOSS.bossId, lobby.selectedBossId);

    const startButton = lobbyControlOf(lobby, 'START BATTLE');
    const startButtonPoint = toVp(lobby.viewport, startButton.x, startButton.y);
    await realClick(cdp, startButtonPoint);

    await waitForCondition(
      async () => {
        const scene = await evaluate(cdp, ACTIVE_SCENE);
        if (scene === 'MainMenuScene') {
          throw new Error('Lobby activated < BACK and returned to MainMenuScene instead of starting battle.');
        }
        return scene === 'BattleScene';
      },
      'BattleScene active after START BATTLE',
      15000
    );

    const battle1 = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
        return snap.active && snap.battleId && snap.boardChildCount === 128 ? snap : false;
      },
      'Battle 1 board rendered from its own authoritative push',
      15000
    );
    const battle1Id = battle1.battleId;
    record('phase4.battle1BoardRendered', battle1.boardChildCount === 128 && Boolean(battle1Id), battle1Id);
    await captureScreenshot(cdp, `run${runNumber}-03-battle1`);

    // -------------------------------------------------------------
    // PHASE 5: REAL TERMINAL RESOLUTION (SERVER-PERSISTED RESULT)
    // -------------------------------------------------------------
    console.log('\n--- Phase 5: Battle 1 played to its own terminal resolution ---');
    const battle1Run = await driveBattleToTerminal(cdp);
    record(
      'phase5.realBattleReachedItsOwnResult',
      battle1Run.ended === true,
      `${battle1Run.swaps} swaps in ${battle1Run.seconds}s (scene: ${battle1Run.activeScene})`
    );

    const result1 = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, RESULT_SNAPSHOT);
        return snap.active && snap.outcomeText.length > 0 ? snap : false;
      },
      'ResultScene rendered',
      10000
    );
    await captureScreenshot(cdp, `run${runNumber}-04-result1`);

    record(
      'phase5.serverOutcomeRendered',
      ['VICTORY', 'DEFEAT'].includes(result1.outcomeText),
      result1.outcomeText
    );
    record(
      'phase5.resultCarriesBattle1Identity',
      result1.battleId === battle1Id,
      { expected: battle1Id, rendered: result1.battleId }
    );

    // `ResultScene` reads `GET /api/battle/{battleId}/result`; a reward block is
    // therefore proof that a durable result row exists for this battle — which
    // is exactly what the history endpoint reads (API_CONTRACTS.md §4.5 notes
    // 2, 10).
    const result1WithRewards = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, RESULT_SNAPSHOT);
        return snap.rewardText.includes('REWARDS') ? snap : false;
      },
      'ResultScene rendered the persisted reward summary',
      12000
    );
    record(
      'phase5.persistedRewardsRendered',
      result1WithRewards.rewardText.includes('Player:') &&
        result1WithRewards.rewardText.includes('Pet:'),
      result1WithRewards.rewardText
    );

    const storedResult1 = await evaluate(cdp, readResultExpression(battle1Id));
    const historyAfterBattle1 = await evaluate(cdp, READ_HISTORY);

    record(
      'phase5.resultRouteServesBattle1',
      storedResult1.status === 200 && storedResult1.body?.battleId === battle1Id,
      { status: storedResult1.status, battleId: storedResult1.body?.battleId }
    );
    record(
      'phase5.historyHasExactlyBattle1',
      historyAfterBattle1.status === 200 &&
        Array.isArray(historyAfterBattle1.body) &&
        historyAfterBattle1.body.length === 1 &&
        historyAfterBattle1.body[0].battleId === battle1Id,
      { status: historyAfterBattle1.status, ids: (historyAfterBattle1.body || []).map((e) => e.battleId) }
    );

    const delivered1 = historyAfterBattle1.body?.[0];
    record(
      'phase5.historyElementIsResultShapePlusCompletedAt',
      Boolean(delivered1) &&
        JSON.stringify(Object.keys(delivered1).sort()) ===
          JSON.stringify([...Object.keys(storedResult1.body || {}), 'completedAt'].sort()),
      { history: Object.keys(delivered1 || {}).sort(), result: Object.keys(storedResult1.body || {}).sort() }
    );
    record(
      'phase5.historyValuesMatchTheResultRoute',
      Boolean(delivered1) &&
        delivered1.outcome === storedResult1.body?.outcome &&
        delivered1.durationTurns === storedResult1.body?.durationTurns &&
        JSON.stringify(delivered1.rewards) === JSON.stringify(storedResult1.body?.rewards) &&
        typeof delivered1.completedAt === 'string' &&
        delivered1.completedAt.length > 0,
      { history: delivered1, result: storedResult1.body }
    );
    record(
      'phase5.renderedOutcomeMatchesTheDeliveredOutcome',
      Boolean(delivered1) && result1.outcomeText === String(delivered1.outcome).toUpperCase(),
      { rendered: result1.outcomeText, delivered: delivered1?.outcome }
    );

    // Play again is not used: the approved MAIN MENU exit returns to the menu,
    // which is where the Battle History entry lives.
    const resultControlPoint = async (label) => {
      const snap = await evaluate(cdp, RESULT_SNAPSHOT);
      const control = (snap.controls || []).find((c) => c.label === label);
      if (!control) throw new Error(`ResultScene rendered no "${label}" control.`);
      const canvas = await evaluate(cdp, `(() => {
        const c = document.querySelector('canvas');
        const r = c.getBoundingClientRect();
        return { left: r.left, top: r.top, width: r.width, height: r.height };
      })()`);
      return {
        x: canvas.left + control.x * (canvas.width / 1280),
        y: canvas.top + control.y * (canvas.height / 720),
      };
    };

    await clickUntil(
      cdp,
      () => resultControlPoint('MAIN MENU'),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene',
      'MAIN MENU returned to MainMenuScene'
    );
    record('phase5.mainMenuReachedAfterBattle1', true);

    // -------------------------------------------------------------
    // PHASE 6: COMPLETED BATTLE VISIBLE
    // -------------------------------------------------------------
    console.log('\n--- Phase 6: Completed battle visible in Battle History ---');
    await clickUntil(
      cdp,
      () => menuPoint(BATTLE_HISTORY_BUTTON),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'BattleHistoryScene',
      'BattleHistoryScene active after the completed battle'
    );

    const history1 = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, HISTORY_SNAPSHOT);
        return snap.active && !snap.loading && snap.history.length > 0 ? snap : false;
      },
      'Battle History loaded the completed battle',
      12000
    );
    await captureScreenshot(cdp, `run${runNumber}-05-history-one`);

    record(
      'phase6.completedBattleVisible',
      history1.entries.length === 1 && history1.entries[0].includes(battle1Id),
      history1.entries
    );
    record(
      'phase6.deliveredMembersRenderedVerbatim',
      history1.entries.length === 1 &&
        history1.entries[0].includes(`Outcome: ${delivered1.outcome}`) &&
        history1.entries[0].includes(`Duration: ${delivered1.durationTurns} turns`) &&
        history1.entries[0].includes(`Completed At: ${delivered1.completedAt}`),
      history1.entries[0]
    );
    record(
      'phase6.deliveredRewardsRendered',
      history1.entries.length === 1 &&
        history1.entries[0].includes(playerRewardLine(delivered1.rewards)) &&
        history1.entries[0].includes(petRewardLine(delivered1.rewards)),
      history1.entries[0]
    );
    record(
      'phase6.latestDeliveredProgressionDisplayed',
      history1.progression === progressionLine(delivered1.rewards),
      { rendered: history1.progression, expected: progressionLine(delivered1.rewards) }
    );
    record(
      'phase6.renderedOrderMatchesDeliveredOrder',
      JSON.stringify(history1.history.map((e) => e.battleId)) ===
        JSON.stringify(historyAfterBattle1.body.map((e) => e.battleId)),
      { rendered: history1.history.map((e) => e.battleId), delivered: historyAfterBattle1.body.map((e) => e.battleId) }
    );

    await clickUntil(
      cdp,
      () => historyControlPoint('< BACK'),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene',
      '< BACK returned to MainMenuScene'
    );
    record('phase6.backReturnedToMainMenu', true);

    // -------------------------------------------------------------
    // PHASE 7: SECOND REAL BATTLE
    // -------------------------------------------------------------
    console.log('\n--- Phase 7: Second real battle ---');
    await clickUntil(
      cdp,
      () => menuPoint(START_BATTLE_BUTTON),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene',
      'LobbyScene active for battle 2'
    );

    lobby = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, LOBBY_SNAPSHOT);
        return snap.active && snap.ownedPetsCount > 0 && snap.ownedCardsCount >= 3 && snap.ownedRelicsCount >= 3
          ? snap
          : false;
      },
      'Lobby collections loaded for battle 2',
      12000
    );
    record('phase7.secondLoadoutSelected', await selectLoadout());
    for (let attempt = 1; attempt <= 3 && lobby.selectedBossId !== BATTLE_BOSS.bossId; attempt++) {
      const option = bossOptionsOf(lobby)[0];
      if (option) {
        await realClick(cdp, centreOf(lobby, option));
      }
      lobby = await evaluate(cdp, LOBBY_SNAPSHOT);
    }
    record('phase7.secondBossSelected', lobby.selectedBossId === BATTLE_BOSS.bossId, lobby.selectedBossId);

    const startButton2 = lobbyControlOf(lobby, 'START BATTLE');
    const startButtonPoint2 = toVp(lobby.viewport, startButton2.x, startButton2.y);
    await realClick(cdp, startButtonPoint2);

    await waitForCondition(
      async () => {
        const scene = await evaluate(cdp, ACTIVE_SCENE);
        if (scene === 'MainMenuScene') {
          throw new Error('Lobby activated < BACK and returned to MainMenuScene instead of starting battle.');
        }
        return scene === 'BattleScene';
      },
      'BattleScene active for battle 2',
      15000
    );
    const battle2 = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
        return snap.active && snap.battleId && snap.boardChildCount === 128 ? snap : false;
      },
      'Battle 2 board rendered from its own authoritative push',
      15000
    );
    const battle2Id = battle2.battleId;
    record(
      'phase7.battle2IsANewBattle',
      Boolean(battle2Id) && battle2Id !== battle1Id,
      { battle1Id, battle2Id }
    );

    const battle2Run = await driveBattleToTerminal(cdp);
    record(
      'phase7.secondRealBattleReachedItsOwnResult',
      battle2Run.ended === true,
      `${battle2Run.swaps} swaps in ${battle2Run.seconds}s (scene: ${battle2Run.activeScene})`
    );

    await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, RESULT_SNAPSHOT);
        return snap.active && snap.rewardText.includes('REWARDS') ? snap : false;
      },
      'Battle 2 ResultScene rendered the persisted reward summary',
      15000
    );
    await captureScreenshot(cdp, `run${runNumber}-06-result2`);

    const historyAfterBattle2 = await evaluate(cdp, READ_HISTORY);
    record(
      'phase7.historyHasBothBattlesNewestFirst',
      historyAfterBattle2.status === 200 &&
        Array.isArray(historyAfterBattle2.body) &&
        historyAfterBattle2.body.length === 2 &&
        historyAfterBattle2.body[0].battleId === battle2Id &&
        historyAfterBattle2.body[1].battleId === battle1Id,
      { status: historyAfterBattle2.status, ids: (historyAfterBattle2.body || []).map((e) => e.battleId) }
    );

    await clickUntil(
      cdp,
      () => resultControlPoint('MAIN MENU'),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene',
      'MAIN MENU returned to MainMenuScene after battle 2'
    );

    // -------------------------------------------------------------
    // PHASE 8: BOTH BATTLES VISIBLE, NEWEST FIRST
    // -------------------------------------------------------------
    console.log('\n--- Phase 8: Both completed battles visible ---');
    await clickUntil(
      cdp,
      () => menuPoint(BATTLE_HISTORY_BUTTON),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'BattleHistoryScene',
      'BattleHistoryScene active after battle 2'
    );

    const history2 = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, HISTORY_SNAPSHOT);
        return snap.active && !snap.loading && snap.history.length === 2 ? snap : false;
      },
      'Battle History loaded both completed battles',
      12000
    );
    await captureScreenshot(cdp, `run${runNumber}-07-history-two`);

    const delivered2 = historyAfterBattle2.body;
    record(
      'phase8.bothBattlesVisible',
      history2.entries.length === 2 &&
        history2.entries[0].includes(battle2Id) &&
        history2.entries[1].includes(battle1Id),
      history2.entries.map((e) => e.split('\n')[0])
    );
    record(
      'phase8.newestBattleFirst',
      history2.history[0].battleId === battle2Id && history2.history[1].battleId === battle1Id,
      history2.history.map((e) => e.battleId)
    );
    record(
      'phase8.renderedOrderMatchesDeliveredOrder',
      JSON.stringify(history2.history.map((e) => e.battleId)) ===
        JSON.stringify(delivered2.map((e) => e.battleId)),
      { rendered: history2.history.map((e) => e.battleId), delivered: delivered2.map((e) => e.battleId) }
    );
    record(
      'phase8.everyDeliveredBattleRenderedVerbatim',
      history2.entries.length === 2 &&
        history2.entries.every((entry, index) => {
          const delivered = delivered2[index];
          return (
            entry.includes(`Outcome: ${delivered.outcome}`) &&
            entry.includes(`Duration: ${delivered.durationTurns} turns`) &&
            entry.includes(`Completed At: ${delivered.completedAt}`) &&
            entry.includes(playerRewardLine(delivered.rewards)) &&
            entry.includes(petRewardLine(delivered.rewards))
          );
        }),
      history2.entries
    );
    record(
      'phase8.latestDeliveredProgressionDisplayed',
      history2.progression === progressionLine(delivered2[0].rewards),
      { rendered: history2.progression, expected: progressionLine(delivered2[0].rewards) }
    );
    record(
      'phase8.progressionIsTheNewestBattleNotTheOlder',
      // The two defeats deliver the same Player track here, so the property is
      // asserted structurally: the panel shows the newest element's values.
      history2.progression === progressionLine(history2.history[0].rewards),
      { newest: history2.history[0].rewards, rendered: history2.progression }
    );

    await clickUntil(
      cdp,
      () => historyControlPoint('< BACK'),
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene',
      'final < BACK returned to MainMenuScene'
    );
    record('phase8.finalBackReturnedToMainMenu', true);

    // -------------------------------------------------------------
    // PHASE 9: HEALTH & SAFETY GATES
    // -------------------------------------------------------------
    console.log('\n--- Phase 9: Error & Health Safety Gates ---');
    record('safety.zeroDiscordDependencies', discordEvents.length === 0, `${discordEvents.length} Discord events`);
    record('safety.zeroUncaughtExceptions', uncaughtExceptions.length === 0, uncaughtExceptions);
    record('safety.zeroFatalConsoleErrors', consoleErrors.length === 0, consoleErrors);

    const unexpectedApiResponses = Array.from(apiResponses.values()).filter(
      (resp) => !(resp.status >= 200 && resp.status < 300)
    );
    record('safety.zeroUnexpectedApiResponses', unexpectedApiResponses.length === 0, unexpectedApiResponses);

    // §4.5 notes 5–6: the endpoint has no query parameters at all, and it is a
    // read — so every request the page made for it is a bare authenticated GET.
    record(
      'safety.historyEndpointReadWithBareGet',
      historyRequests.length >= 3 &&
        historyRequests.every(
          (request) => request.method === 'GET' && !new URL(request.url).search
        ),
      historyRequests.map((request) => `${request.method} ${request.url}`)
    );
  } finally {
    if (cdp) cdp.close();
    cleanup();
  }

  console.log(`\n--- RUN ${runNumber} SUMMARY ---`);
  console.log(`Total checks: ${checks.length}, Failures: ${failures}`);

  if (failures > 0) {
    throw new Error(`Battle history smoke RUN ${runNumber} failed with ${failures} failure(s).`);
  }

  return { runNumber, checks, failures };
}

// Direct execution
if (process.argv[1] && process.argv[1].endsWith('battle-history-smoke.mjs')) {
  (async () => {
    try {
      // Two clean-context runs by default, the convention the other smoke
      // harnesses use. A real battle is negotiated turn by turn, so a single-run
      // mode is available for iterating on the harness itself.
      const runs = Number(process.env.HISTORY_RUNS) || 2;
      for (let run = 1; run <= runs; run++) {
        await runBattleHistorySmokeTest(run);
      }

      console.log('\n============================================================');
      console.log('=== ALL BATTLE HISTORY RUNS PASSED CLEANLY (NO FLAKINESS) ===');
      console.log('============================================================\n');
      process.exit(0);
    } catch (err) {
      console.error('\n[BATTLE HISTORY SMOKE FATAL ERROR]', err.message);
      process.exit(1);
    }
  })();
}
