/**
 * TASK-185 browser verification harness (development-only tooling).
 *
 * Proves the Lobby's Boss-selection step end to end in a real browser, with real
 * pointer input, against the running development host:
 *
 *   Lobby
 *     → all five canonical MVP Bosses rendered, none selected
 *     → complete the Pet/Card/Relic loadout and activate START BATTLE with no
 *       Boss chosen: no start request may leave the browser
 *     → select a NON-default Boss with a real pointer event
 *     → click START BATTLE (its own centre) with a real pointer event
 *     → exactly one POST /api/battle/start leaves the browser
 *     → BattleScene opens
 *     → the response's initialState.bossState.bossId IS the selected Boss,
 *       corroborated by element + maxHP
 *     → board input still works (TASK-182) and START BATTLE stays clickable
 *       under the Runtime Status overlay (TASK-183)
 *
 * Nothing here is invoked from JavaScript: the harness never calls
 * `startBattle()`, `requestStart()`, a scene handler, or a React callback. Every
 * selection and the start itself are trusted `Input.dispatchMouseEvent` presses.
 * The only JavaScript that runs is observation:
 *
 *   - it reads the live LobbyScene's own selection fields and hit-area geometry
 *     (to know where to aim the real pointer),
 *   - it wraps `runtime.startBattle` / `runtime.requestAction` to COUNT what the
 *     real gesture produced (the wrappers delegate; they never submit), and
 *   - it reads the POST /api/battle/start request and response bodies off the
 *     network through the DevTools Protocol, so the authoritative Boss identity
 *     comes from the server's own response and not from the client.
 *
 * Usage: node scripts/boss-selection-smoke.mjs [url]
 */

import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { setTimeout as delay } from 'node:timers/promises';

const URL_UNDER_TEST = process.argv[2] || 'http://localhost:5173/';
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const OUT_DIR = 'boss-selection-shots';
const DEBUG_PORT = 9255;

/**
 * The five canonical MVP Bosses as `BOSS_RULES.md` §6/§6.4 and §6.1 define them.
 *
 * Deliberately transcribed here rather than read from the client catalog: the
 * harness is the independent expectation, so a mis-spelled or mis-transcribed
 * identity in the implementation cannot satisfy it. `wireElement`/`maxHp` are
 * §6.1's values, which the server restates in the start response.
 */
const CANONICAL_BOSSES = [
  { displayName: 'Hỏa Long', bossId: 'boss-hoa-long', element: 'Hỏa', wireElement: 'Fire', maxHp: 5000 },
  { displayName: 'Thủy Ma', bossId: 'boss-thuy-ma', element: 'Thủy', wireElement: 'Water', maxHp: 5000 },
  { displayName: 'Mộc Yêu', bossId: 'boss-moc-yeu', element: 'Mộc', wireElement: 'Wood', maxHp: 5000 },
  { displayName: 'Sơn Thạch Vệ', bossId: 'boss-son-thach-ve', element: 'Thổ', wireElement: 'Earth', maxHp: 3000 },
  { displayName: 'Kim Lôi Vương', bossId: 'boss-kim-loi-vuong', element: 'Kim', wireElement: 'Metal', maxHp: 2800 },
];

/** Case A / Case B: a NON-default Boss each, in a fresh session. */
const CASES = ['boss-thuy-ma', 'boss-kim-loi-vuong'];

/** Board geometry the BattleScene draws with (`BattleScene.ts` constants). */
const CELL_SIZE = 56;
const CELL_GAP = 6;
const BOARD_SIZE = 8;

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
        msg.error ? reject(new Error(JSON.stringify(msg.error))) : resolve(msg.result);
      } else if (msg.method) {
        const fn = this.listeners.get(msg.method);
        if (fn) fn(msg.params);
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
    this.ws.close();
  }
}

async function waitForDevTools() {
  for (let i = 0; i < 80; i++) {
    try {
      const res = await fetch(`http://127.0.0.1:${DEBUG_PORT}/json/version`);
      if (res.ok) return await res.json();
    } catch {
      /* not up yet */
    }
    await delay(250);
  }
  throw new Error('DevTools endpoint did not become available');
}

async function evaluate(cdp, expression) {
  const result = await cdp.send('Runtime.evaluate', {
    expression,
    returnByValue: true,
    awaitPromise: true,
  });
  if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails));
  return result.result.value;
}

/** A real left-button click: move, press, release — three trusted events. */
async function realClick(cdp, point) {
  const base = { x: point.x, y: point.y, button: 'left', clickCount: 1 };
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mouseMoved', buttons: 0 });
  await delay(120);
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mousePressed', buttons: 1 });
  await delay(80);
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mouseReleased', buttons: 0 });
  await delay(300);
}

async function shot(cdp, name) {
  const png = await cdp.send('Page.captureScreenshot', { format: 'png' });
  writeFileSync(`${OUT_DIR}/${name}.png`, Buffer.from(png.data, 'base64'));
}

/** Captures the live Phaser game via the React root's fiber tree. */
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
  for (const key of ['MainMenuScene', 'LobbyScene', 'BattleScene', 'ResultScene']) {
    const s = window.__game.scene.getScene(key);
    if (s && s.scene && s.scene.isActive && s.scene.isActive()) return key;
  }
  return 'none';
})()
`;

/**
 * The Lobby's own state and hit-area geometry, read (never driven) so the
 * harness knows where to aim real pointer events. Game pixels are converted to
 * viewport pixels with the canvas's own bounding box, because Phaser's Scale
 * Manager letterboxes the logical 1280x720 space.
 */
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
    reviewText: s.reviewText && typeof s.reviewText.text === 'string' ? s.reviewText.text : null,
    messageText: s.messageText && typeof s.messageText.text === 'string' ? s.messageText.text : null,
    errorText: s.errorText && typeof s.errorText.text === 'string' ? s.errorText.text : null,
    selectionBounds: s.selectionText ? {
      x: s.selectionText.x,
      y: s.selectionText.y,
      width: s.selectionText.width,
      height: s.selectionText.height,
      bottom: s.selectionText.y + s.selectionText.height,
      text: s.selectionText.text,
    } : null,
    reviewBounds: s.reviewText ? {
      x: s.reviewText.x,
      y: s.reviewText.y,
      width: s.reviewText.width,
      height: s.reviewText.height,
      bottom: s.reviewText.y + s.reviewText.height,
      text: s.reviewText.text,
    } : null,
    messageBounds: s.messageText ? {
      x: s.messageText.x,
      y: s.messageText.y,
      width: s.messageText.width,
      height: s.messageText.height,
      bottom: s.messageText.y + s.messageText.height,
      text: s.messageText.text,
    } : null,
    errorBounds: s.errorText ? {
      x: s.errorText.x,
      y: s.errorText.y,
      width: s.errorText.width,
      height: s.errorText.height,
      bottom: s.errorText.y + s.errorText.height,
      text: s.errorText.text,
    } : null,
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

/** The Runtime Status overlay's own hit-testing properties (TASK-183 regression). */
const OVERLAY_STATE = `
(() => {
  const el = document.querySelector('[data-testid="runtime-status-overlay"]');
  if (!el) return null;
  const cs = getComputedStyle(el);
  return { present: true, pointerEvents: cs.pointerEvents, display: cs.display, visibility: cs.visibility, zIndex: cs.zIndex };
})()
`;

/**
 * Counts what the runtime port was asked to submit. The wrapper only counts and
 * delegates, so the counted request is always the one the real pointer gesture
 * caused — the harness never submits anything itself.
 */
const HOOK_START_BATTLE = `
(() => {
  const s = window.__game && window.__game.scene.getScene('LobbyScene');
  if (!s || !s.runtime || s.runtime.__startObserved) return 'no runtime';
  const runtime = s.runtime;
  runtime.__startObserved = true;
  window.__starts = [];
  const original = runtime.startBattle.bind(runtime);
  runtime.startBattle = (request) => { window.__starts.push(request); return original(request); };
  return 'hooked';
})()
`;

const HOOK_BATTLE_INPUT = `
(() => {
  const s = window.__game && window.__game.scene.getScene('BattleScene');
  if (!s || !s.runtime) return 'no runtime';
  const runtime = s.runtime;
  if (!runtime.__observed) {
    runtime.__observed = true;
    window.__requests = [];
    const original = runtime.requestAction.bind(runtime);
    runtime.requestAction = (action) => { window.__requests.push(action); return original(action); };
  }
  if (!s.__cellObserved) {
    s.__cellObserved = true;
    window.__taps = [];
    const proto = Object.getPrototypeOf(s);
    const original = proto.onCellTapped;
    proto.onCellTapped = function (cellIndex) { window.__taps.push(cellIndex); return original.call(this, cellIndex); };
  }
  return 'hooked';
})()
`;

const BATTLE_STATE = `
(() => {
  const s = window.__game && window.__game.scene.getScene('BattleScene');
  if (!s || !s.scene.isActive()) return { active: false };
  const canvas = document.querySelector('canvas');
  const rect = canvas.getBoundingClientRect();
  const gameWidth = window.__game.scale.gameSize.width;
  const gameHeight = window.__game.scale.gameSize.height;
  return {
    active: true,
    boardChildCount: s.boardLayer && s.boardLayer.list ? s.boardLayer.list.length : null,
    // One Text label per delivered cell, in cell order — the authoritative Gem
    // types the server sent, as the scene received them (used only to CHOOSE a
    // gesture that a player could see would match).
    boardLabels: s.boardLayer && s.boardLayer.list
      ? s.boardLayer.list.filter((o) => o.type === 'Text').map((o) => o.text)
      : null,
    boardText: s.boardText && typeof s.boardText.text === 'string' ? s.boardText.text : null,
    swapText: s.swapText && typeof s.swapText.text === 'string' ? s.swapText.text : null,
    viewport: { left: rect.left, top: rect.top, scaleX: rect.width / gameWidth, scaleY: rect.height / gameHeight },
  };
})()
`;

/** Adjacent swaps that would produce a Match on the delivered board. */
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

async function main() {
  mkdirSync(OUT_DIR, { recursive: true });
  const edge = spawn(EDGE, [
    '--headless=new', `--remote-debugging-port=${DEBUG_PORT}`, '--remote-allow-origins=*',
    '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--hide-scrollbars',
    '--window-size=1280,720',
    '--user-data-dir=' + process.env.TEMP + '\\task185-boss-selection-profile', 'about:blank',
  ], { stdio: 'ignore' });

  const report = { url: URL_UNDER_TEST, cases: [] };
  let failures = 0;

  const check = (bucket, name, ok, detail) => {
    bucket.checks.push({ name, ok, detail });
    if (!ok) failures += 1;
    console.log(`    ${ok ? 'PASS' : 'FAIL'}  ${name}: ${typeof detail === 'string' ? detail : JSON.stringify(detail)}`);
  };

  try {
    await waitForDevTools();

    for (const targetBossId of CASES) {
      const target = CANONICAL_BOSSES.find((boss) => boss.bossId === targetBossId);
      const bucket = { targetBossId, targetDisplayName: target.displayName, checks: [] };
      report.cases.push(bucket);
      console.log(`\n=== CASE ${target.bossId} (${target.displayName}) — fresh session ===`);

      // --- fresh page: a new session, so this case cannot inherit case A state
      const page = await (await fetch(`http://127.0.0.1:${DEBUG_PORT}/json/new?about:blank`, { method: 'PUT' })).json();
      const cdp = await Cdp.connect(page.webSocketDebuggerUrl);

      // Network observation: the POST's own request/response bodies, plus the
      // count of start requests (one activation must produce exactly one).
      const startRequests = [];
      const responseByRequestId = new Map();
      const socketFrames = [];
      cdp.on('Network.requestWillBeSent', (p) => {
        if (p.request && typeof p.request.url === 'string' && p.request.url.includes('/api/battle/start')) {
          startRequests.push({ url: p.request.url, method: p.request.method, postData: p.request.postData ?? null });
        }
      });
      cdp.on('Network.responseReceived', (p) => {
        if (p.response && typeof p.response.url === 'string' && p.response.url.includes('/api/battle/start')) {
          responseByRequestId.set(p.requestId, { url: p.response.url, status: p.response.status, body: null });
        }
      });
      cdp.on('Network.loadingFinished', async (p) => {
        const entry = responseByRequestId.get(p.requestId);
        if (entry && entry.body === null) {
          try {
            const body = await cdp.send('Network.getResponseBody', { requestId: p.requestId });
            entry.body = body.base64Encoded ? Buffer.from(body.body, 'base64').toString('utf8') : body.body;
          } catch (error) {
            entry.body = `<unreadable: ${error.message}>`;
          }
        }
      });
      cdp.on('Network.webSocketFrameSent', (p) => {
        if (p.response && typeof p.response.payloadData === 'string') {
          socketFrames.push({ opcode: p.response.opcode ?? null, payloadData: p.response.payloadData });
        }
      });

      try {
        await cdp.send('Page.enable');
        await cdp.send('Runtime.enable');
        await cdp.send('Network.enable');
        await cdp.send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 720, deviceScaleFactor: 1, mobile: false });
        await cdp.send('Page.navigate', { url: URL_UNDER_TEST });
        await delay(5000);

        const boot = await evaluate(cdp, `(() => ({ url: location.href, text: document.body.innerText.slice(0, 200) }))()`);
        check(bucket, 'devAuth.authenticated', boot.text.includes('Runtime Status'), boot.text.replace(/\n/g, ' | ').slice(0, 120));

        // --- reach the Lobby with a real click on the menu trigger
        await evaluate(cdp, CAPTURE_GAME);
        for (let i = 0; i < 8; i++) {
          await evaluate(cdp, CAPTURE_GAME);
          if ((await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene') break;
          await realClick(cdp, { x: 640, y: 324 });
          await delay(1200);
        }
        check(bucket, 'lobby.reached', (await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene', await evaluate(cdp, ACTIVE_SCENE));
        await shot(cdp, `case-${targetBossId}-01-lobby`);

        // --- the Lobby's own state and hit areas
        let lobby = await evaluate(cdp, LOBBY_SNAPSHOT);
        const toViewport = (gx, gy) => ({
          x: lobby.viewport.left + gx * lobby.viewport.scaleX,
          y: lobby.viewport.top + gy * lobby.viewport.scaleY,
        });
        const centreOf = (o) => toViewport(o.x + o.width / 2, o.y + o.height / 2);
        const topmostAt = (point) =>
          evaluate(cdp, `(() => { const el = document.elementFromPoint(${point.x}, ${point.y}); return el ? el.tagName : null; })()`);

        const bossOptionsOf = (snapshot) =>
          snapshot.interactive.filter(
            (o) => o.text !== null && CANONICAL_BOSSES.some((boss) => o.text.endsWith(`  ${boss.element}`))
          );

        const bossOptions = bossOptionsOf(lobby);
        check(bucket, 'ac01.fiveBossesRendered', bossOptions.length === 5, bossOptions.map((o) => o.text));
        check(
          bucket,
          'ac01.exactlyTheCanonicalFive',
          CANONICAL_BOSSES.every((boss) => bossOptions.filter((o) => o.text.includes(boss.displayName)).length === 1),
          CANONICAL_BOSSES.map((boss) => `${boss.displayName}=${bossOptions.filter((o) => o.text.includes(boss.displayName)).length}`)
        );
        check(bucket, 'ac02.noBossSelectedInitially', lobby.selectedBossId === null && bossOptions.every((o) => o.text.startsWith('○')), {
          selectedBossId: lobby.selectedBossId,
          options: bossOptions.map((o) => o.text),
          review: (lobby.reviewText || '').split('\n')[0],
        });
        check(bucket, 'ac12.overlayDoesNotBlockPointer', (await evaluate(cdp, OVERLAY_STATE))?.pointerEvents === 'none', await evaluate(cdp, OVERLAY_STATE));

        // --- loadout first (Pet + 3 Basic Cards + 3 Relics), still no Boss. Each
        // click is a real pointer event; a row is only clicked while it is
        // unselected, so a lost or duplicated click cannot deselect a filled slot.
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
              return attempt;
            }
          }
          return -1;
        };

        const loadoutAttempts = await selectLoadout();
        check(bucket, 'ac10.loadoutSelectedUnchanged', loadoutAttempts > 0, {
          attempts: loadoutAttempts,
          petId: lobby.selectedPetId,
          cards: lobby.selectedCardIds,
          relics: lobby.selectedRelicIds,
        });

        // --- AC-05: a complete loadout with NO Boss must not submit anything
        await evaluate(cdp, HOOK_START_BATTLE);
        let startButton = lobby.interactive.find((o) => o.type === 'Rectangle' && o.enabled);
        let buttonPoint = toViewport(startButton.x, startButton.y);
        await realClick(cdp, buttonPoint);
        await delay(1500);

        const startsWithoutBoss = await evaluate(cdp, `window.__starts || []`);
        check(bucket, 'ac05.noSubmitWithoutABoss', startsWithoutBoss.length === 0 && startRequests.length === 0, {
          runtimePortCalls: startsWithoutBoss.length,
          httpRequests: startRequests.length,
          message: (await evaluate(cdp, LOBBY_SNAPSHOT)).messageText,
          scene: await evaluate(cdp, ACTIVE_SCENE),
        });
        check(
          bucket,
          'ac05.validationMessageIsTheDocumentedOne',
          ((await evaluate(cdp, LOBBY_SNAPSHOT)).messageText || '').includes('Choose a Boss.'),
          (await evaluate(cdp, LOBBY_SNAPSHOT)).messageText
        );

        // --- AC-03/AC-04: select the target Boss with a REAL pointer event
        lobby = await evaluate(cdp, LOBBY_SNAPSHOT);
        for (let attempt = 1; attempt <= 3 && lobby.selectedBossId !== target.bossId; attempt++) {
          const option = bossOptionsOf(lobby).find((o) => o.text.includes(target.displayName));
          const point = centreOf(option);
          check(bucket, `ac03.bossOptionHitTestable(attempt ${attempt})`, (await topmostAt(point)) === 'CANVAS', {
            point,
            topmost: await topmostAt(point),
          });
          await realClick(cdp, point);
          lobby = await evaluate(cdp, LOBBY_SNAPSHOT);
        }

        const selectedOptions = bossOptionsOf(lobby).filter((o) => o.text.startsWith('●'));
        check(bucket, 'ac03.selectedBossIdIsTheClick', lobby.selectedBossId === target.bossId, {
          selectedBossId: lobby.selectedBossId,
          expected: target.bossId,
        });
        check(bucket, 'ac03.exactlyOneBossMarkedSelected', selectedOptions.length === 1 && selectedOptions[0].text.includes(target.displayName), selectedOptions.map((o) => o.text));
        check(bucket, 'ac04.reviewShowsSelectedBoss', (lobby.reviewText || '').startsWith(`5. REVIEW — Boss: ${target.bossId}`), (lobby.reviewText || '').split('\n')[0]);
        check(bucket, 'ac04.bossSelectionSurvivesLoadout', lobby.selectedPetId !== null && lobby.selectedCardIds.length === 3 && lobby.selectedRelicIds.length === 3, {
          petId: lobby.selectedPetId,
          cards: lobby.selectedCardIds,
          relics: lobby.selectedRelicIds,
        });
        check(bucket, 'ac07.bossOptionsAreTheOnlyBossSource', await evaluate(cdp, `(() => {
          const s = window.__game.scene.getScene('LobbyScene');
          return typeof s.runtime.getBosses !== 'function' && typeof s.runtime.getBoss !== 'function';
        })()`), 'no Boss read capability on the runtime port');

        // --- TASK-186: Live geometry measurements and non-overlapping assertions
        const sel = lobby.selectionBounds;
        const rev = lobby.reviewBounds;
        const msg = lobby.messageBounds;
        const err = lobby.errorBounds;

        console.log(`\n--- TASK-186 Live Geometry (1280x720, ${targetBossId}) ---`);
        console.table([
          { block: 'selectionText', x: sel?.x, y: sel?.y, width: sel?.width, height: sel?.height, bottom: sel?.bottom },
          { block: 'reviewText',    x: rev?.x, y: rev?.y, width: rev?.width, height: rev?.height, bottom: rev?.bottom },
          { block: 'messageText',   x: msg?.x, y: msg?.y, width: msg?.width, height: msg?.height, bottom: msg?.bottom },
          { block: 'errorText',     x: err?.x, y: err?.y, width: err?.width, height: err?.height, bottom: err?.bottom },
          { block: 'startTrigger',  x: startButton.x - startButton.width / 2, y: startButton.y - startButton.height / 2, width: startButton.width, height: startButton.height, bottom: startButton.y + startButton.height / 2 },
        ]);

        const selBottomToRevTop = rev.y - sel.bottom;
        const revBottomToMsgTop = msg.y - rev.bottom;
        const msgBottomToErrTop = err.y - msg.bottom;

        console.log(`selection.bottom -> review.top gap  : ${selBottomToRevTop} px (AC-01, min 4)`);
        console.log(`review.bottom    -> message.top gap : ${revBottomToMsgTop} px (AC-02, min 4)`);
        console.log(`message.bottom   -> error.top gap   : ${msgBottomToErrTop} px (AC-03, min 0)`);

        check(bucket, 'task186.ac01.selectionReviewSeparation', selBottomToRevTop >= 4, {
          selectionBottom: sel.bottom,
          reviewTop: rev.y,
          gap: selBottomToRevTop,
        });
        check(bucket, 'task186.ac02.reviewMessageSeparation', revBottomToMsgTop >= 4, {
          reviewBottom: rev.bottom,
          messageTop: msg.y,
          gap: revBottomToMsgTop,
        });
        check(bucket, 'task186.ac03.messageErrorSeparation', msgBottomToErrTop >= 0, {
          messageBottom: msg.bottom,
          errorTop: err.y,
          gap: msgBottomToErrTop,
        });
        check(bucket, 'task186.ac09.allBlocksInsideSafeArea',
          sel.bottom <= 696 && rev.bottom <= 696 && msg.bottom <= 696 && err.bottom <= 696,
          { selBottom: sel.bottom, revBottom: rev.bottom, msgBottom: msg.bottom, errBottom: err.bottom }
        );
        check(bucket, 'task186.ac09.errorTextTopBounded', err.y <= 670, { errorY: err.y });

        const triggerLeft = startButton.x - startButton.width / 2;
        const triggerRight = startButton.x + startButton.width / 2;
        const triggerTop = startButton.y - startButton.height / 2;
        const triggerBottom = startButton.y + startButton.height / 2;

        check(bucket, 'task186.ac05.selectionClearOfTrigger', sel.x + sel.width < triggerLeft, { selRight: sel.x + sel.width, triggerLeft });
        check(bucket, 'task186.ac05.messageClearOfTrigger', msg.y > triggerBottom, { msgTop: msg.y, triggerBottom });
        check(bucket, 'task186.ac05.errorClearOfTrigger', err.y > triggerBottom, { errTop: err.y, triggerBottom });

        // Line 4 (Relics) of reviewText is the only line reaching x >= 930; its top must be below triggerBottom
        const reviewLine4Top = rev.y + 3 * 15;
        check(bucket, 'task186.ac05.reviewRelicsLineBelowTrigger', reviewLine4Top >= triggerBottom, { reviewLine4Top, triggerBottom });

        await shot(cdp, `case-${targetBossId}-02-lobby-selected`);

        // --- one real activation of START BATTLE
        startButton = lobby.interactive.find((o) => o.type === 'Rectangle' && o.enabled);
        buttonPoint = toViewport(startButton.x, startButton.y);
        check(bucket, 'ac12.startBattleCentreHitTestable', (await topmostAt(buttonPoint)) === 'CANVAS', {
          point: buttonPoint,
          geometry: { x: startButton.x, y: startButton.y, width: startButton.width, height: startButton.height },
          topmost: await topmostAt(buttonPoint),
        });

        await realClick(cdp, buttonPoint);

        let scene = await evaluate(cdp, ACTIVE_SCENE);
        for (let i = 0; i < 20 && scene !== 'BattleScene'; i++) {
          await delay(1000);
          scene = await evaluate(cdp, ACTIVE_SCENE);
        }
        await shot(cdp, scene === 'BattleScene' ? `case-${targetBossId}-03-battle` : `case-${targetBossId}-03-after-click`);

        const portStarts = await evaluate(cdp, `window.__starts || []`);
        check(
          bucket,
          'ac13.oneStartPerActivation',
          portStarts.length === 1 && !!portStarts[0] && portStarts[0].bossId === target.bossId,
          { portCalls: portStarts.length, submitted: portStarts[0] || null }
        );
        check(bucket, 'ac13.exactlyOneHttpStartRequest', startRequests.length === 1, {
          count: startRequests.length,
          requests: startRequests.map((r) => ({ method: r.method, url: r.url, postData: r.postData })),
        });
        check(
          bucket,
          'ac06.submittedBossIdIsTheSelection',
          !!startRequests[0] && JSON.parse(startRequests[0].postData).bossId === target.bossId,
          { postData: startRequests[0] ? startRequests[0].postData : null, expected: target.bossId }
        );
        check(bucket, 'ac09.battleReached', scene === 'BattleScene', scene);

        // --- AC-09: authoritative Boss identity, from the server's own response
        let bossState = null;
        for (let i = 0; i < 20 && bossState === null; i++) {
          for (const entry of responseByRequestId.values()) {
            if (entry.body !== null && !entry.body.startsWith('<unreadable')) {
              try {
                bossState = JSON.parse(entry.body).initialState.bossState;
              } catch {
                /* not the start response yet */
              }
            }
          }
          if (bossState === null) await delay(500);
        }
        check(bucket, 'ac09.authoritativeBossId', bossState !== null && bossState.bossId === target.bossId, bossState);
        check(
          bucket,
          'ac09.authoritativeBossCorroborated',
          bossState !== null && bossState.element === target.wireElement && bossState.maxHP === target.maxHp,
          bossState === null
            ? null
            : { element: bossState.element, expectedElement: target.wireElement, maxHP: bossState.maxHP, expectedMaxHp: target.maxHp }
        );

        // --- TASK-182 regression: 64 cells, real pointer input, one Swap per gesture
        await evaluate(cdp, HOOK_BATTLE_INPUT);
        let battle = await evaluate(cdp, BATTLE_STATE);
        for (let i = 0; i < 15 && (battle.boardChildCount ?? 0) !== 128; i++) {
          await delay(1000);
          battle = await evaluate(cdp, BATTLE_STATE);
        }
        check(bucket, 'task182.sixtyFourCellsRendered', battle.boardChildCount === 128, {
          boardChildren: battle.boardChildCount,
          cells: (battle.boardChildCount ?? 0) / 2,
        });

        const boardOriginX = (1280 - (BOARD_SIZE * CELL_SIZE + (BOARD_SIZE - 1) * CELL_GAP)) / 2;
        const boardOriginY = 24 + 96;
        const cellCentre = (index) =>
          toViewport(
            boardOriginX + (index % BOARD_SIZE) * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2,
            boardOriginY + Math.floor(index / BOARD_SIZE) * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2
          );

        // A player looks at the board and swaps two Gems that will match. The
        // labels read here are the ones the server delivered; the search only
        // chooses which real gesture to make.
        const matchingSwaps = findMatchingSwaps(battle.boardLabels);
        const boardLabelsBefore = battle.boardLabels;
        let committed = null;
        for (const [first, second] of matchingSwaps.slice(0, 3)) {
          await realClick(cdp, cellCentre(first));
          await realClick(cdp, cellCentre(second));
          await delay(3000);
          battle = await evaluate(cdp, BATTLE_STATE);
          if ((battle.swapText || '').includes('accepted')) {
            committed = { first, second, swapText: battle.swapText };
            break;
          }
          // Reset the selection so the next gesture starts clean.
          await realClick(cdp, cellCentre(first));
          await delay(300);
        }

        if (committed === null) {
          // No match-producing swap was found on the delivered board: fall back to
          // a real gesture anyway, so the transport path is still exercised.
          await realClick(cdp, cellCentre(12));
          await realClick(cdp, cellCentre(13));
          await delay(3000);
          battle = await evaluate(cdp, BATTLE_STATE);
        }

        // An accepted Swap is resolved by the server and pushed back as
        // `BattleStateUpdated`, so wait for the delivered board to change.
        let boardChanged = JSON.stringify(boardLabelsBefore) !== JSON.stringify(battle.boardLabels);
        for (let i = 0; i < 10 && !boardChanged; i++) {
          await delay(1000);
          battle = await evaluate(cdp, BATTLE_STATE);
          boardChanged = JSON.stringify(boardLabelsBefore) !== JSON.stringify(battle.boardLabels);
        }

        const taps = await evaluate(cdp, `window.__taps || []`);
        const actions = await evaluate(cdp, `window.__requests || []`);
        const swapFrames = socketFrames.filter((frame) => frame.payloadData.includes('Swap'));
        check(bucket, 'task182.realPointerInputReachedTheBoard', taps.length >= 2, { taps });
        check(
          bucket,
          'task182.exactlyOneSwapPerGesture',
          actions.filter((a) => a && a.kind === 'Swap').length === taps.length / 2,
          { swaps: actions.filter((a) => a && a.kind === 'Swap').length, gestures: taps.length / 2 }
        );
        // The server's own Swap acknowledgement — an accepted Swap, or one of its
        // documented rejection codes (SIGNALR_PROTOCOL.md §5) — is the proof the
        // Swap reached the server; a client-side value cannot produce either.
        check(bucket, 'task182.swapReachedTheServer', /accepted|rejected \(/.test(battle.swapText || ''), {
          swapText: battle.swapText,
          committedSwap: committed,
          observedSocketFrames: { total: socketFrames.length, opcodes: [...new Set(socketFrames.map((f) => f.opcode))], withSwap: swapFrames.length },
        });
        check(
          bucket,
          'task182.committedSwapAccepted',
          committed !== null,
          committed === null ? { note: 'no match-producing swap on this delivered board', swapText: battle.swapText } : committed
        );
        check(bucket, 'task182.authoritativeBoardChanged', boardChanged, {
          changed: boardChanged,
          labelsBefore: boardLabelsBefore ? boardLabelsBefore.slice(0, 8) : null,
          labelsAfter: battle.boardLabels ? battle.boardLabels.slice(0, 8) : null,
        });
        await shot(cdp, `case-${targetBossId}-04-board-after-input`);

        bucket.networkStartRequests = startRequests;
        bucket.bossState = bossState;
        bucket.runtimePortStarts = portStarts;
        bucket.startsWithoutBoss = startsWithoutBoss;
        bucket.swapText = battle.swapText;
        bucket.boardChanged = boardChanged;
      } finally {
        cdp.close();
        await fetch(`http://127.0.0.1:${DEBUG_PORT}/json/close/${page.id}`);
      }
    }

    writeFileSync(`${OUT_DIR}/report.json`, JSON.stringify(report, null, 2));
    console.log(`\nreport: ${OUT_DIR}/report.json`);
    if (failures > 0) {
      console.error(`\n${failures} check(s) FAILED`);
      process.exitCode = 1;
    }
  } catch (err) {
    console.error('HARNESS ERROR:', err.message);
    writeFileSync(`${OUT_DIR}/report.json`, JSON.stringify(report, null, 2));
    process.exitCode = 1;
  } finally {
    edge.kill();
  }
}

main().catch((err) => {
  console.error('HARNESS ERROR:', err);
  process.exit(1);
});
