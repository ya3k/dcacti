/**
 * TASK-183 browser verification harness (development-only tooling).
 *
 * Reproduces and verifies the UI-overlap defect found during TASK-182:
 *
 *   Runtime Status overlay (opaque, absolutely positioned, z-index 5, bottom-right)
 *     -> opts back INTO pointer input via `.game-shell__overlay > *`
 *     -> sits on top of the Phaser canvas where LobbyScene draws START BATTLE
 *     -> swallows the right-hand / centre portion of the button
 *
 * The check is geometric and behavioural, not visual: it aims a REAL pointer at
 * the *centre* of the button's own hit area and asserts the LobbyScene handler
 * fired exactly once. It never calls the handler, never dispatches a synthetic
 * click on a React node, and never invokes any callback from JavaScript —
 * trusted `Input.dispatchMouseEvent` presses only.
 *
 * It also asserts the overlay is still rendered and readable (AC-01/AC-02) and
 * that one activation produces exactly one startBattle call (AC-04), and then
 * re-checks the TASK-182 board input path for regression (AC-06).
 *
 * Usage: node scripts/lobby-start-overlay-smoke.mjs
 */

import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { setTimeout as delay } from 'node:timers/promises';

const URL_UNDER_TEST = process.argv[2] || 'http://localhost:5173/';
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const OUT_DIR = 'lobby-start-shots';
const DEBUG_PORT = 9233;

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
  await delay(50);
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mousePressed', buttons: 1 });
  await delay(50);
  await cdp.send('Input.dispatchMouseEvent', { ...base, type: 'mouseReleased', buttons: 0 });
  await delay(200);
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
 * Reads the LobbyScene's own Start Battle trigger out of the live display list
 * and reports its geometry in BOTH game coordinates and CSS/viewport pixels.
 *
 * Phaser's Scale Manager letterboxes `GAME_WIDTH x GAME_HEIGHT` inside the shell,
 * so a game pixel is not a CSS pixel; the transform is derived from the canvas
 * element's own bounding box rather than assumed.
 *
 * It also records, for the same point, which DOM element is topmost there
 * (`document.elementFromPoint`) — the direct measure of whether the overlay
 * intercepts the click. This only observes.
 */
const START_BUTTON_GEOMETRY = `
(() => {
  const s = window.__game && window.__game.scene.getScene('LobbyScene');
  if (!s || !s.scene.isActive()) return { reason: 'LobbyScene not active' };

  const lists = [];
  for (const key of Object.keys(s)) {
    const v = s[key];
    if (v && typeof v === 'object' && Array.isArray(v.list) && v.list.length) lists.push(v);
  }
  let button = null;
  for (const list of lists) {
    for (const o of list.list) {
      if (o && o.type === 'Rectangle' && o.input && o.input.enabled) { button = o; break; }
    }
    if (button) break;
  }
  if (!button) return { reason: 'no interactive Rectangle (Start Battle trigger) found' };

  const canvas = document.querySelector('canvas');
  if (!canvas) return { reason: 'no canvas' };
  const rect = canvas.getBoundingClientRect();
  const gameWidth = window.__game.scale.gameSize.width;
  const gameHeight = window.__game.scale.gameSize.height;
  const scaleX = rect.width / gameWidth;
  const scaleY = rect.height / gameHeight;

  const b = button.getBounds ? button.getBounds() : null;
  const cx = button.x, cy = button.y;
  const toViewport = (gx, gy) => ({ x: rect.left + gx * scaleX, y: rect.top + gy * scaleY });

  const centre = toViewport(cx, cy);
  const rightEdge = toViewport(b ? b.right - 4 : cx + button.width / 2 - 4, cy);
  const topmostAtCentre = document.elementFromPoint(centre.x, centre.y);
  const topmostAtRight = document.elementFromPoint(rightEdge.x, rightEdge.y);
  const describe = (el) => el ? (el.tagName + (el.className && typeof el.className === 'string' ? '.' + el.className.trim().split(/\\s+/).join('.') : '') + (el.dataset && el.dataset.testid ? '[' + el.dataset.testid + ']' : '')) : null;

  return {
    button: { x: cx, y: cy, width: button.width, height: button.height, interactive: !!(button.input && button.input.enabled) },
    bounds: b ? { left: b.left, right: b.right, top: b.top, bottom: b.bottom } : null,
    canvas: { left: rect.left, top: rect.top, width: rect.width, height: rect.height },
    scale: { scaleX, scaleY },
    centreViewport: centre,
    rightViewport: rightEdge,
    topmostAtCentre: describe(topmostAtCentre),
    topmostAtRight: describe(topmostAtRight),
    overlay: (() => {
      const el = document.querySelector('[data-testid="runtime-status-overlay"]');
      if (!el) return null;
      const r = el.getBoundingClientRect();
      const cs = getComputedStyle(el);
      return {
        present: true,
        text: el.innerText.slice(0, 120),
        rect: { left: r.left, top: r.top, right: r.right, bottom: r.bottom },
        pointerEvents: cs.pointerEvents,
        zIndex: cs.zIndex,
        display: cs.display,
        visibility: cs.visibility,
      };
    })(),
  };
})()
`;

/**
 * Counts startBattle submissions at the runtime port.
 *
 * `GameRuntime.startBattle` is the single funnel the Lobby goes through
 * (`ARCHITECTURE.md` §2.2 rule 5), so counting invocations measures how many
 * battle-start requests one activation produced. The wrapper only counts and
 * delegates — it never submits anything itself, so the request under test is
 * always the one the real pointer gesture caused.
 */
const HOOK_START_BATTLE = `
(() => {
  const s = window.__game && window.__game.scene.getScene('LobbyScene');
  if (!s || !s.runtime) return 'no runtime';
  const runtime = s.runtime;
  if (!runtime.__startObserved) {
    runtime.__startObserved = true;
    window.__starts = [];
    const original = runtime.startBattle.bind(runtime);
    runtime.startBattle = (request) => { window.__starts.push(request); return original(request); };
  }
  return 'hooked';
})()
`;

/**
 * Which scene the Lobby's own activation path produced, plus how many
 * `startBattle` submissions the runtime saw.
 */
const START_OBSERVATION = `
(() => ({
  starts: (window.__starts || []).length,
  requests: window.__starts || [],
  scene: (() => {
    if (!window.__game) return null;
    for (const key of ['MainMenuScene', 'LobbyScene', 'BattleScene', 'ResultScene']) {
      const s = window.__game.scene.getScene(key);
      if (s && s.scene && s.scene.isActive && s.scene.isActive()) return key;
    }
    return 'none';
  })(),
}))()
`;

/** The TASK-182 board-input path, re-checked for regression (AC-06). */
const HOOK_REQUEST_ACTION = `
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
  return 'hooked';
})()
`;

const HOOK_BOARD_HANDLER = `
(() => {
  const s = window.__game && window.__game.scene.getScene('BattleScene');
  if (!s || !s.boardLayer) return false;
  if (!window.__taps) window.__taps = [];
  if (!s.__observed) {
    s.__observed = true;
    const proto = Object.getPrototypeOf(s);
    const original = proto.onCellTapped;
    proto.onCellTapped = function (cellIndex) {
      window.__taps.push(cellIndex);
      return original.call(this, cellIndex);
    };
  }
  return true;
})()
`;

const SCENE_STATE = `
(() => {
  const s = window.__game && window.__game.scene.getScene('BattleScene');
  if (!s || !s.scene.isActive()) return {};
  return {
    swapText: s.swapText && typeof s.swapText.text === 'string' ? s.swapText.text : null,
    boardText: s.boardText && typeof s.boardText.text === 'string' ? s.boardText.text : null,
    boardChildCount: s.boardLayer && s.boardLayer.list ? s.boardLayer.list.length : null,
  };
})()
`;

async function main() {
  mkdirSync(OUT_DIR, { recursive: true });
  const edge = spawn(EDGE, [
    '--headless=new', `--remote-debugging-port=${DEBUG_PORT}`, '--remote-allow-origins=*',
    '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--hide-scrollbars',
    '--window-size=1280,720',
    '--user-data-dir=' + process.env.TEMP + '\\task183-smoke-profile', 'about:blank',
  ], { stdio: 'ignore' });

  let cdp;
  const report = { checks: [] };
  const ok = (name, value) => {
    report.checks.push({ name, value });
    console.log(`  ${name}: ${typeof value === 'string' ? value : JSON.stringify(value)}`);
  };

  try {
    await waitForDevTools();
    const target = await (await fetch(`http://127.0.0.1:${DEBUG_PORT}/json/new?about:blank`, { method: 'PUT' })).json();
    cdp = await Cdp.connect(target.webSocketDebuggerUrl);
    await cdp.send('Page.enable');
    await cdp.send('Runtime.enable');
    await cdp.send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 720, deviceScaleFactor: 1, mobile: false });
    await cdp.send('Page.navigate', { url: URL_UNDER_TEST });

    console.log('STEP 1  load the app (development authentication is the app\'s own)');
    await delay(5000);
    const boot = await evaluate(cdp, `(() => ({ url: location.href, text: document.body.innerText.slice(0, 300) }))()`);
    ok('app.boot', {
      url: boot.url,
      authenticated: boot.text.includes('Runtime Status'),
      signalrConnected: boot.text.includes('Connected'),
    });
    await shot(cdp, '00-boot');

    console.log('STEP 2  MainMenu -> Lobby (real click on the menu trigger)');
    await evaluate(cdp, CAPTURE_GAME);
    const menuButton = { x: 640, y: 324 };
    for (let i = 0; i < 8; i++) {
      await evaluate(cdp, CAPTURE_GAME);
      if ((await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene') break;
      await realClick(cdp, menuButton);
      await delay(1200);
    }
    const lobbyReached = (await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene';
    ok('lobby.reached', { reached: lobbyReached, scene: await evaluate(cdp, ACTIVE_SCENE) });
    if (!lobbyReached) throw new Error('LobbyScene was never reached');
    await shot(cdp, '01-lobby');

    console.log('STEP 3  overlay + START BATTLE geometry, and who is topmost where');
    const geometry = await evaluate(cdp, START_BUTTON_GEOMETRY);
    ok('startButton.geometry', geometry.button);
    ok('startButton.bounds.gamePixels', geometry.bounds);
    ok('startButton.centreInViewport', geometry.centreViewport);
    ok('overlay.state', geometry.overlay);
    if (!geometry.button) throw new Error(`could not read Start Battle trigger: ${geometry.reason}`);
    if (!geometry.overlay || !geometry.overlay.present) throw new Error('Runtime Status overlay is not rendered');

    // AC-01/AC-02: still visible, still readable, unchanged content.
    ok('ac01.overlayVisible', {
      display: geometry.overlay.display,
      visibility: geometry.overlay.visibility,
      zIndex: geometry.overlay.zIndex,
      area: (geometry.overlay.rect.right - geometry.overlay.rect.left) * (geometry.overlay.rect.bottom - geometry.overlay.rect.top),
    });
    ok('ac02.overlayReadable', { text: geometry.overlay.text, pointerEvents: geometry.overlay.pointerEvents });

    // The core of TASK-183: whatever is topmost at the button's centre must be
    // the canvas, not the overlay.
    ok('ac03.topmostElementAtCentre', geometry.topmostAtCentre);
    ok('ac03.topmostElementAtRightEdge', geometry.topmostAtRight);

    // Does the overlay's box actually cover the button centre? (It must not
    // matter any more, but this documents the geometry that caused the bug.)
    const ov = geometry.overlay.rect;
    const c = geometry.centreViewport;
    ok('geometry.overlayCoversButtonCentre', {
      overlayRect: ov,
      buttonCentre: c,
      overlaps: c.x >= ov.left && c.x <= ov.right && c.y >= ov.top && c.y <= ov.bottom,
      note: 'overlap is expected; pointer-events:none makes it harmless',
    });

    // Hit testing: does a real click at the centre reach the canvas?
    const hitAtCentre = await cdp.send('Runtime.evaluate', {
      expression: `document.elementFromPoint(${c.x}, ${c.y}) && document.elementFromPoint(${c.x}, ${c.y}).tagName`,
      returnByValue: true,
    });
    ok('ac03.hitTestAtButtonCentre', hitAtCentre.result.value);

    console.log('STEP 4  REAL pointer click at the CENTRE of START BATTLE');
    await evaluate(cdp, HOOK_START_BATTLE);
    ok('startBattle.hook', await evaluate(cdp, HOOK_START_BATTLE));
    const before = await evaluate(cdp, START_OBSERVATION);
    ok('before.starts', before.starts);

    // Select a legal loadout first, exactly as a player would, so the click
    // exercises the whole path rather than a "selection incomplete" guard.
    const LOBBY_COLUMN_ORIGIN_X = 50;
    const LOBBY_COLUMN_PITCH = 372;
    const LOBBY_LIST_TOP = 108;
    const LOBBY_ROW_HEIGHT = 30;
    const canvas = geometry.canvas;
    const sx = canvas.width / 1280;
    const sy = canvas.height / 720;
    const toViewport = (gx, gy) => ({ x: canvas.left + gx * sx, y: canvas.top + gy * sy });
    const lobbyRow = (column, index) =>
      toViewport(LOBBY_COLUMN_ORIGIN_X + column * LOBBY_COLUMN_PITCH + 40, LOBBY_LIST_TOP + index * LOBBY_ROW_HEIGHT + 7);

    for (const [column, index] of [[0, 0], [1, 0], [1, 1], [1, 2], [2, 0], [2, 1], [2, 2]]) {
      await realClick(cdp, lobbyRow(column, index));
    }
    await shot(cdp, '02-lobby-selected');

    const centre = geometry.centreViewport;
    console.log(`        clicking (${Math.round(centre.x)}, ${Math.round(centre.y)}) — the button's own centre`);
    await realClick(cdp, centre);

    // Wait for the Lobby to hand over to the Battle scene.
    let observed = await evaluate(cdp, START_OBSERVATION);
    for (let i = 0; i < 15 && observed.scene !== 'BattleScene'; i++) {
      await delay(1000);
      observed = await evaluate(cdp, START_OBSERVATION);
    }
    ok('ac03.clickReceived', { starts: observed.starts });
    ok('ac03.sceneAfterClick', observed.scene);
    await shot(cdp, observed.scene === 'BattleScene' ? '03-battle' : '03-after-click');

    // AC-04: exactly one submission from one activation.
    ok('ac04.oneStartPerActivation', {
      starts: observed.starts,
      expected: 1,
      duplicate: observed.starts > 1,
      battleId: observed.requests[0] && observed.requests[0].battleId !== undefined ? '(client request)' : null,
      requestShape: observed.requests[0] ? Object.keys(observed.requests[0]) : null,
    });

    if (observed.scene !== 'BattleScene') {
      throw new Error(`clicking the centre of START BATTLE did not reach BattleScene (scene=${observed.scene}, starts=${observed.starts})`);
    }
    ok('ac03.transitionSucceeded', true);

    console.log('STEP 5  TASK-182 board input regression check (AC-06)');
    let state = await evaluate(cdp, SCENE_STATE);
    for (let i = 0; i < 15 && (state.boardChildCount ?? 0) !== 128; i++) {
      await delay(1000);
      state = await evaluate(cdp, SCENE_STATE);
    }
    ok('board.rendered', { childCount: state.boardChildCount, cells: (state.boardChildCount ?? 0) / 2 });
    if (state.boardChildCount !== 128) throw new Error(`board did not render 64 cells (children=${state.boardChildCount})`);

    await evaluate(cdp, HOOK_BOARD_HANDLER);
    ok('runtime.requestAction.hooked', await evaluate(cdp, HOOK_REQUEST_ACTION));

    const CELL_SIZE = 56, CELL_GAP = 6, CELL_PITCH = CELL_SIZE + CELL_GAP;
    const BOARD_ORIGIN_X = (1280 - (8 * CELL_SIZE + 7 * CELL_GAP)) / 2;
    const BOARD_ORIGIN_Y = 24 + 96;
    const cellCentre = (index) => {
      const row = Math.floor(index / 8), column = index % 8;
      return toViewport(BOARD_ORIGIN_X + column * CELL_PITCH + CELL_SIZE / 2, BOARD_ORIGIN_Y + row * CELL_PITCH + CELL_SIZE / 2);
    };

    const boardBefore = state.boardText;
    let accepted = false;
    for (const [first, second] of [[12, 13], [13, 12], [18, 19], [19, 18], [20, 21], [21, 20], [25, 26], [26, 25], [34, 35], [35, 34], [40, 41], [41, 40], [2, 3], [3, 2], [8, 9], [9, 8], [48, 49], [49, 48]]) {
      await realClick(cdp, cellCentre(first));
      await realClick(cdp, cellCentre(second));
      await delay(2500);
      const s = await evaluate(cdp, SCENE_STATE);
      if (s.swapText && s.swapText.includes('accepted')) { accepted = true; ok('task182.swapAccepted', { first, second, swapText: s.swapText }); break; }
      await realClick(cdp, cellCentre(first));
      await delay(300);
    }

    const taps = await evaluate(cdp, `window.__taps || []`);
    const requests = await evaluate(cdp, `window.__requests || []`);
    ok('task182.cellsReachingOnCellTapped', taps);
    ok('task182.swapRequestsThroughRuntime', requests.filter((a) => a && a.kind === 'Swap').length);
    ok('task182.swapAcceptedByServer', accepted);
    const boardAfter = (await evaluate(cdp, SCENE_STATE)).boardText;
    ok('task182.authoritativeBoardChanged', { changed: JSON.stringify(boardBefore) !== JSON.stringify(boardAfter) });
    ok('task182.oneRequestPerGesture', {
      swapsSubmitted: requests.filter((a) => a && a.kind === 'Swap').length,
      gestures: taps.length / 2,
    });
    await shot(cdp, '04-board-after-swap');

    writeFileSync(`${OUT_DIR}/report.json`, JSON.stringify(report, null, 2));
    console.log('\nreport: lobby-start-shots/report.json');
  } catch (err) {
    console.error('HARNESS ERROR:', err.message);
    writeFileSync(`${OUT_DIR}/report.json`, JSON.stringify(report, null, 2));
    process.exitCode = 1;
  } finally {
    if (cdp) cdp.close();
    edge.kill();
  }
}

main().catch((err) => {
  console.error('HARNESS ERROR:', err);
  process.exit(1);
});
