/**
 * TASK-182 browser verification harness (development-only tooling).
 *
 * Drives the real application in a real Chromium through the DevTools Protocol
 * and produces the swap with REAL canvas pointer input
 * (`Input.dispatchMouseEvent` => trusted DOM events), never by calling an
 * internal method:
 *
 *   real browser pointer
 *     -> Phaser input picking (the board layer as an input target)
 *     -> BattleScene board handler
 *     -> BattleScene.onCellTapped
 *     -> GameRuntime.requestAction(Swap)
 *     -> SignalRService.swap -> BattleHub.Swap -> authoritative server
 *
 * Every navigation step waits for the app's own state to change rather than
 * sleeping a fixed interval, so a slow start cannot silently skip a scene.
 *
 * JavaScript is evaluated only to observe: read the scene's rendered readouts,
 * read Phaser's input registration, record hub frames, and watch which cells the
 * scene's own handler received. Nothing submits an action.
 *
 * Usage: node scripts/board-input-smoke.mjs
 */

import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { setTimeout as delay } from 'node:timers/promises';

const URL_UNDER_TEST = process.argv[2] || 'http://localhost:5173/';
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const OUT_DIR = 'board-input-shots';
const DEBUG_PORT = 9231;

// Presentation geometry, mirrored from the scenes' layout constants (logical
// game pixels). The board's only coordinate convention stays MATCH3_RULES.md
// §1.0 row-major `index = row * 8 + column`; these only aim a pointer at it.
const GAME_WIDTH = 1280;
const GAME_HEIGHT = 720;
const SAFE_AREA_X = 24;
const SAFE_AREA_Y = 24;
const SAFE_AREA_WIDTH = GAME_WIDTH - SAFE_AREA_X * 2;
const SAFE_AREA_HEIGHT = GAME_HEIGHT - SAFE_AREA_Y * 2;

const CELL_SIZE = 56;
const CELL_GAP = 6;
const CELL_PITCH = CELL_SIZE + CELL_GAP;
const BOARD_COLUMNS = 8;
const BOARD_WIDTH = BOARD_COLUMNS * CELL_SIZE + (BOARD_COLUMNS - 1) * CELL_GAP;
const BOARD_ORIGIN_X = (GAME_WIDTH - BOARD_WIDTH) / 2;
const BOARD_ORIGIN_Y = SAFE_AREA_Y + 96;

const MENU_BUTTON = { x: GAME_WIDTH / 2, y: SAFE_AREA_Y + 300 };
const LOBBY_START_BUTTON = {
  x: GAME_WIDTH - SAFE_AREA_X - 26 - 150,
  y: SAFE_AREA_Y + SAFE_AREA_HEIGHT - 96,
};
const LOBBY_COLUMN_PITCH = 372;
const LOBBY_COLUMN_ORIGIN_X = SAFE_AREA_X + 26;
const LOBBY_LIST_TOP = SAFE_AREA_Y + 84;
const LOBBY_ROW_HEIGHT = 30;

function cellCentre(index) {
  const row = Math.floor(index / BOARD_COLUMNS);
  const column = index % BOARD_COLUMNS;
  return {
    x: BOARD_ORIGIN_X + column * CELL_PITCH + CELL_SIZE / 2,
    y: BOARD_ORIGIN_Y + row * CELL_PITCH + CELL_SIZE / 2,
  };
}
function gapCentre(row) {
  return {
    x: BOARD_ORIGIN_X + CELL_SIZE + CELL_GAP / 2,
    y: BOARD_ORIGIN_Y + row * CELL_PITCH + CELL_SIZE / 2,
  };
}
function offBoard() {
  return {
    x: BOARD_ORIGIN_X + BOARD_WIDTH + 30,
    y: BOARD_ORIGIN_Y + BOARD_WIDTH + 30,
  };
}
function lobbyRow(column, index) {
  return {
    x: LOBBY_COLUMN_ORIGIN_X + column * LOBBY_COLUMN_PITCH + 40,
    y: LOBBY_LIST_TOP + index * LOBBY_ROW_HEIGHT + 7,
  };
}

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

/**
 * Observation-only instrumentation installed before any app script runs.
 *
 * Records every hub WebSocket frame the page sends/receives, so an outbound Swap
 * invocation is observed on the wire rather than inferred from the scene's
 * intent. It reads nothing and submits nothing.
 */
const INSTRUMENTATION = `
(() => {
  window.__t = { ws: [], errors: [] };
  const NativeWS = window.WebSocket;
  function Rec(...args) {
    const socket = new NativeWS(...args);
    const rec = { url: String(args[0]), sent: [], received: [] };
    window.__t.ws.push(rec);
    socket.addEventListener('message', (e) => {
      const d = e.data;
      try {
        if (typeof d === 'string') rec.received.push(d);
        else if (d instanceof ArrayBuffer) rec.received.push(new TextDecoder().decode(new Uint8Array(d)));
        else if (d && typeof d.text === 'function') d.text().then((t) => rec.received.push(t)).catch(() => {});
      } catch {}
    });
    const nativeSend = socket.send.bind(socket);
    socket.send = (d) => {
      try {
        if (typeof d === 'string') rec.sent.push(d);
        else if (d instanceof ArrayBuffer) rec.sent.push(new TextDecoder().decode(new Uint8Array(d)));
      } catch {}
      return nativeSend(d);
    };
    return socket;
  }
  Rec.prototype = NativeWS.prototype;
  Object.assign(Rec, { CONNECTING: 0, OPEN: 1, CLOSING: 2, CLOSED: 3 });
  window.WebSocket = Rec;
  window.addEventListener('error', (e) => window.__t.errors.push(String(e.message)));
  window.addEventListener('unhandledrejection', (e) => window.__t.errors.push('rej: ' + String(e.reason)));
})();
`;

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

/** Which scene is active right now. */
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

/** Records which cell indices the scene's own handler receives. */
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

/**
 * Counts Swap requests submitted through the runtime port.
 *
 * `GameRuntime.requestAction` is the single funnel every Swap goes through
 * (`ARCHITECTURE.md` §2.2 rule 3), so counting its invocations measures how many
 * requests one gesture produced — which is what AC-08's "no duplicate handler"
 * requirement is about. The wrapper only counts and delegates; it never
 * substitutes a call of its own.
 */
const HOOK_REQUEST_ACTION = `
(() => {
  const s = window.__game && window.__game.scene.getScene('BattleScene');
  if (!s || !s.runtime) return 'no runtime';
  const runtime = s.runtime;
  if (!runtime.__observed) {
    runtime.__observed = true;
    window.__requests = [];
    const original = runtime.requestAction.bind(runtime);
    runtime.requestAction = (action) => {
      window.__requests.push(action);
      return original(action);
    };
  }
  return 'hooked';
})()
`;

/**
 * The board the client is currently presenting, read from the scene's rendered
 * cell labels in draw order. This is what the server pushed — the scene renders
 * the payload verbatim and computes no cell value of its own.
 */
const BOARD_CELLS = `
(() => {
  const s = window.__game && window.__game.scene.getScene('BattleScene');
  if (!s || !s.scene.isActive() || !s.boardLayer) return null;
  return s.boardLayer.list.filter((o) => o.type === 'Text').map((o) => o.text);
})()
`;

/** The scene's live readouts plus Phaser's own input registration. */
const SCENE_STATE = `
(() => {
  const s = window.__game && window.__game.scene.getScene('BattleScene');
  if (!s || !s.scene.isActive()) return { sceneActive: false };
  const g = (o) => (o && typeof o.text === 'string' ? o.text : null);
  const layer = s.boardLayer;
  const list = s.input && s.input._list ? s.input._list : [];
  return {
    sceneActive: true,
    swapText: g(s.swapText),
    boardText: g(s.boardText),
    battleText: g(s.battleText),
    selectedCell: s.selectedCell,
    boardChildCount: layer && layer.list ? layer.list.length : null,
    boardLayer: layer ? {
      type: layer.type,
      width: layer.width,
      height: layer.height,
      inputEnabled: !!(layer.input && layer.input.enabled),
      hitAreaType: layer.input && layer.input.hitArea ? layer.input.hitArea.constructor.name : null,
      hitArea: layer.input && layer.input.hitArea ? {
        x: layer.input.hitArea.x, y: layer.input.hitArea.y,
        width: layer.input.hitArea.width, height: layer.input.hitArea.height,
      } : null,
    } : null,
    inputTrackedTotal: list.length,
    inputTrackedContainers: list.filter((o) => o.type === 'Container').map((o) => ({
      inputEnabled: !!(o.input && o.input.enabled),
      hitArea: o.input && o.input.hitArea ? {
        width: o.input.hitArea.width, height: o.input.hitArea.height,
      } : null,
    })),
  };
})()
`;

/**
 * The hub's outbound frames, read at the browser network layer.
 *
 * Instrumenting `window.WebSocket` alone does not see SignalR's frames (it keeps
 * its own socket reference), so the frames are collected by CDP's Network domain
 * in `main` and merged here.
 */
const HUB_SENT = `
(() => { const o = []; for (const s of window.__t.ws) { if (!s.url.includes('/hubs/battle')) continue; for (const f of s.sent) o.push(f); } return o; })()
`;
const HUB_RECEIVED = `
(() => { const o = []; for (const s of window.__t.ws) { if (!s.url.includes('/hubs/battle')) continue; for (const f of s.received) o.push(f); } return o; })()
`;

function splitRecords(frames) {
  const out = [];
  for (const frame of frames) {
    for (const record of frame.split('\u001e')) {
      if (!record.trim()) continue;
      try {
        out.push(JSON.parse(record));
      } catch {
        /* SignalR handshake / non-JSON record */
      }
    }
  }
  return out;
}
const swapInvocations = (frames) =>
  splitRecords(frames).filter((r) => r && r.type === 1 && r.target === 'Swap');
const statePushes = (frames) =>
  splitRecords(frames).filter((r) => r && r.type === 1 && r.target === 'BattleStateUpdated');

async function main() {
  mkdirSync(OUT_DIR, { recursive: true });
  const edge = spawn(EDGE, [
    '--headless=new', `--remote-debugging-port=${DEBUG_PORT}`, '--remote-allow-origins=*',
    '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--hide-scrollbars',
    '--window-size=1280,720',
    '--user-data-dir=' + process.env.TEMP + '\\task182-smoke-profile', 'about:blank',
  ], { stdio: 'ignore' });

  let cdp;
  const report = { checks: [] };
  /** Hub frames captured at the browser network layer (independent of JS). */
  const netFrames = [];
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
    await cdp.send('Network.enable');
    // Capture the real WebSocket wire frames: SignalR holds its own socket
    // reference, so the network layer is the only reliable observation point for
    // what the client actually sent.
    cdp.on('Network.webSocketFrameSent', (p) => {
      netFrames.push({ dir: 'sent', payload: String(p.response.payloadData) });
    });
    cdp.on('Network.webSocketFrameReceived', (p) => {
      netFrames.push({ dir: 'recv', payload: String(p.response.payloadData) });
    });
    await cdp.send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 720, deviceScaleFactor: 1, mobile: false });
    await cdp.send('Page.addScriptToEvaluateOnNewDocument', { source: INSTRUMENTATION });
    await cdp.send('Page.navigate', { url: URL_UNDER_TEST });

    /** Waits for the app to reach a scene, clicking `driver` until it does. */
    const reachScene = async (wanted, driver, attempts = 8) => {
      for (let i = 0; i < attempts; i++) {
        await evaluate(cdp, CAPTURE_GAME);
        const active = await evaluate(cdp, ACTIVE_SCENE);
        if (active === wanted) return { reached: true, attempts: i + 1, active };
        await driver(i);
        await delay(1200);
      }
      await evaluate(cdp, CAPTURE_GAME);
      return { reached: false, attempts, active: await evaluate(cdp, ACTIVE_SCENE) };
    };

    console.log('STEP 1-4  load the app (development authentication is the app\'s own)');
    await delay(5000);
    const boot = await evaluate(cdp, `(() => ({ url: location.href, text: document.body.innerText.slice(0, 260) }))()`);
    ok('app.boot', {
      url: boot.url,
      authenticated: boot.text.includes('Runtime Status'),
      signalrConnected: boot.text.includes('Connected'),
    });

    console.log('STEP 5  MainMenu -> Lobby (real clicks until LobbyScene is active)');
    const lobby = await reachScene('LobbyScene', () => realClick(cdp, MENU_BUTTON));
    ok('lobby.reached', lobby);
    if (!lobby.reached) throw new Error('LobbyScene was never reached');
    await shot(cdp, '01-lobby');

    console.log('STEP 6  Lobby loadout selection (real clicks)');
    const selectStart = async () => {
      for (const [column, index] of [[0, 0], [1, 0], [1, 1], [1, 2], [2, 0], [2, 1], [2, 2]]) {
        await realClick(cdp, lobbyRow(column, index));
      }
      await realClick(cdp, LOBBY_START_BUTTON);
    };
    const battle = await reachScene('BattleScene', selectStart, 6);
    ok('battle.reached', battle);
    if (!battle.reached) throw new Error('BattleScene was never reached');
    await shot(cdp, '02-battle');

    console.log('STEP 7  SignalR + board render');
    let state = await evaluate(cdp, SCENE_STATE);
    for (let i = 0; i < 15 && (state.boardChildCount ?? 0) !== 128; i++) {
      await delay(1000);
      state = await evaluate(cdp, SCENE_STATE);
    }
    ok('board.rendered', {
      boardText: state.boardText,
      childCount: state.boardChildCount,
      cells: (state.boardChildCount ?? 0) / 2,
    });
    if (state.boardChildCount !== 128) {
      throw new Error(`board did not render 64 cells (children=${state.boardChildCount})`);
    }

    console.log('STEP 8  board input target (Phaser\'s own input registration)');
    ok('boardLayer.registration', state.boardLayer);
    ok('phaser.input.tracked', {
      total: state.inputTrackedTotal,
      containers: state.inputTrackedContainers,
    });

    console.log('STEP 9  REAL canvas pointer gesture on the canvas');
    await evaluate(cdp, HOOK_BOARD_HANDLER);
    ok('runtime.requestAction.hooked', await evaluate(cdp, HOOK_REQUEST_ACTION));

    /** Swap requests the client submitted through the runtime port. */
    const clientSwaps = async () =>
      (await evaluate(cdp, `window.__requests || []`)).filter((a) => a && a.kind === 'Swap');

    /** Swap invocations seen on the wire so far. */
    const wireSwaps = () => swapInvocations(netFrames.map((f) => f.payload));
    /** Server pushes seen on the wire so far. */
    const wirePushes = () =>
      statePushes(netFrames.filter((f) => f.dir === 'recv').map((f) => f.payload));

    const swapsBefore = wireSwaps().length;
    const pushesBefore = wirePushes().length;
    const boardBefore = await evaluate(cdp, BOARD_CELLS);
    const stateBefore = await evaluate(cdp, SCENE_STATE);

    /**
     * Performs one REAL two-tap gesture on two drawn cells, exactly as
     * `MATCH3_RULES.md` §2 item 1 documents: first tap selects, second submits.
     * Between attempts the prior selection is cleared by tapping the same cell
     * twice, which the scene treats as "clear the selection".
     */
    const gesture = async (first, second) => {
      await realClick(cdp, cellCentre(first));
      const selected = await evaluate(cdp, SCENE_STATE);
      await realClick(cdp, cellCentre(second));
      await delay(2500);
      return selected;
    };

    // Attempt gestures until the server accepts one. A rejected swap is the
    // server's authoritative answer and changes nothing (MATCH3_RULES.md §2.1.5),
    // so retrying with a different pair exercises the real path repeatedly rather
    // than fabricating a success.
    const candidatePairs = [
      [12, 13], [13, 12], [18, 19], [19, 18], [20, 21], [21, 20],
      [25, 26], [26, 25], [34, 35], [35, 34], [40, 41], [41, 40],
      [2, 3], [3, 2], [8, 9], [9, 8], [48, 49], [49, 48],
    ];

    let acceptedPair = null;
    let lastSwapText = null;
    for (const [first, second] of candidatePairs) {
      const selected = await gesture(first, second);
      const state = await evaluate(cdp, SCENE_STATE);
      lastSwapText = state.swapText;
      if (state.swapText && state.swapText.includes('accepted')) {
        acceptedPair = [first, second];
        ok('gesture.acceptedPair', {
          first,
          second,
          firstTapSelection: selected.selectedCell,
          swapText: state.swapText,
        });
        break;
      }
      // Clear any lingering selection before the next attempt.
      await realClick(cdp, cellCentre(first));
      await delay(300);
    }

    const taps = await evaluate(cdp, `window.__taps || []`);
    const requestedSwaps = await clientSwaps();

    ok('gesture.cellsReachingOnCellTapped', taps);
    ok('swap.requestsSubmittedThroughRuntime', requestedSwaps);
    ok('swap.requestsSubmittedCount', requestedSwaps.length);
    ok('swap.acceptedByServer', acceptedPair !== null);
    if (!acceptedPair) {
      ok('swap.lastServerAnswer', lastSwapText);
    }
    await shot(cdp, '03-after-gesture');

    console.log('STEP 10  authoritative state push / board change');
    const boardAfter = await evaluate(cdp, BOARD_CELLS);
    const stateAfter = await evaluate(cdp, SCENE_STATE);

    ok('authoritative.boardChanged', {
      before: boardBefore ? boardBefore.slice(0, 8) : null,
      after: boardAfter ? boardAfter.slice(0, 8) : null,
      changed: JSON.stringify(boardBefore) !== JSON.stringify(boardAfter),
    });
    ok('authoritative.battleStateAdvanced', {
      before: {
        turnSequence: (stateBefore.battleText || '').split('\n')[1] ?? null,
        bossHp: (stateBefore.battleText || '').split('\n').find((l) => l.startsWith('Boss HP')) ?? null,
      },
      after: {
        turnSequence: (stateAfter.battleText || '').split('\n')[1] ?? null,
        bossHp: (stateAfter.battleText || '').split('\n').find((l) => l.startsWith('Boss HP')) ?? null,
      },
    });

    const afterSwap = stateAfter;
    ok('battleScene.afterGestures', {
      swapText: afterSwap.swapText,
      boardText: afterSwap.boardText,
    });
    await shot(cdp, '04-after-authoritative-push');

    console.log('STEP 11  invalid input is still ignored (real pointer events)');
    const beforeInvalid = wireSwaps().length;
    await realClick(cdp, offBoard());
    await realClick(cdp, gapCentre(1));
    await delay(800);
    ok('invalidInput.noNewRequest', {
      before: beforeInvalid,
      after: wireSwaps().length,
    });

    ok('pageErrors', await evaluate(cdp, `window.__t.errors`));
    ok('duplicateCheck.oneRequestPerGesture', {
      swapsSubmitted: (await clientSwaps()).length,
      gesturesPerformed: taps.length / 2,
      note: 'one Swap request per completed two-tap gesture',
    });
    writeFileSync(`${OUT_DIR}/report.json`, JSON.stringify(report, null, 2));
    writeFileSync(`${OUT_DIR}/client-swaps.json`, JSON.stringify(await clientSwaps(), null, 2));
    writeFileSync(`${OUT_DIR}/net-frames.json`, JSON.stringify(netFrames, null, 2));
    console.log('\nreport: board-input-shots/report.json');
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
