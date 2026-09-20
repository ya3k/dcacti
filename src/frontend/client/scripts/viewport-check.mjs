/**
 * Manual responsive viewport verification harness.
 *
 * Drives a headless Chromium (Edge) over the DevTools Protocol using Node's
 * built-in WebSocket, loads the running Vite dev server, and measures the real
 * layout at each required viewport size.
 *
 * Development-only tooling for this task; not part of the application build.
 *
 * Usage: node scripts/viewport-check.mjs [url]
 */

import { spawn } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';

const URL_UNDER_TEST = process.argv[2] || 'http://localhost:5173/';
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const DEBUG_PORT = 9222;

const VIEWPORTS = [
  [1920, 1080],
  [1600, 900],
  [1366, 768],
  [1280, 720],
  [1024, 768],
  [900, 700],
  [800, 600],
  [600, 900],
];

/** Minimal CDP client over the built-in WebSocket. */
class Cdp {
  constructor(ws) {
    this.ws = ws;
    this.id = 0;
    this.pending = new Map();
    ws.addEventListener('message', (event) => {
      const msg = JSON.parse(event.data);
      if (msg.id && this.pending.has(msg.id)) {
        const { resolve, reject } = this.pending.get(msg.id);
        this.pending.delete(msg.id);
        msg.error ? reject(new Error(JSON.stringify(msg.error))) : resolve(msg.result);
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
  for (let i = 0; i < 60; i++) {
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

async function openTarget() {
  const res = await fetch(`http://127.0.0.1:${DEBUG_PORT}/json/new?about:blank`, {
    method: 'PUT',
  });
  if (!res.ok) throw new Error(`Failed to open target: ${res.status}`);
  return await res.json();
}

/** Collects the layout facts required by the task's verification checklist. */
const MEASURE = `(() => {
  const de = document.documentElement;
  const body = document.body;
  const shell = document.querySelector('.game-shell');
  const canvasHost = document.querySelector('#phaser-container');
  const canvas = document.querySelector('#phaser-container canvas');
  const overlay = document.querySelector('.game-shell__overlay');
  const rect = (el) => {
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return { x: r.x, y: r.y, w: r.width, h: r.height, right: r.right, bottom: r.bottom };
  };

  // Widest element in the document, to detect anything escaping the viewport.
  let widest = null;
  for (const el of document.querySelectorAll('.game-shell, .game-shell *')) {
    const r = el.getBoundingClientRect();
    if (!widest || r.right > widest.right) {
      widest = { right: r.right, tag: el.tagName + '.' + el.className };
    }
  }

  return {
    viewport: { w: window.innerWidth, h: window.innerHeight },
    docScroll: {
      scrollWidth: de.scrollWidth,
      scrollHeight: de.scrollHeight,
      clientWidth: de.clientWidth,
      clientHeight: de.clientHeight,
      bodyScrollWidth: body.scrollWidth,
      bodyScrollHeight: body.scrollHeight,
      canScrollX: de.scrollWidth > de.clientWidth,
      canScrollY: de.scrollHeight > de.clientHeight,
    },
    shell: rect(shell),
    canvasHost: rect(canvasHost),
    canvas: rect(canvas),
    overlay: rect(overlay),
    canvasAttrs: canvas ? { width: canvas.width, height: canvas.height } : null,
    canvasCss: canvas ? { w: canvas.style.width, h: canvas.style.height } : null,
    canvasParentIsHost: canvas ? canvas.parentElement === canvasHost : null,
    shellContainsCanvas: shell && canvas ? shell.contains(canvas) : null,
    overlayInsideShell: shell && overlay ? shell.contains(overlay) : null,
    widest,
  };
})()`;

function evaluate(cdp, expression) {
  return cdp.send('Runtime.evaluate', { expression, returnByValue: true }).then((r) => {
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.text);
    return r.result.value;
  });
}

async function main() {
  const edge = spawn(EDGE, [
    '--headless=new',
    `--remote-debugging-port=${DEBUG_PORT}`,
    '--remote-allow-origins=*',
    '--no-first-run',
    '--no-default-browser-check',
    '--disable-gpu',
    '--hide-scrollbars',
    '--user-data-dir=' + process.env.TEMP + '\\viewport-check-profile',
    'about:blank',
  ], { stdio: 'ignore' });

  let cdp;
  const results = [];

  try {
    await waitForDevTools();
    const target = await openTarget();
    cdp = await Cdp.connect(target.webSocketDebuggerUrl);

    await cdp.send('Page.enable');
    await cdp.send('Runtime.enable');

    // Collect console errors so a broken Phaser boot cannot pass silently.
    const consoleErrors = [];
    cdp.ws.addEventListener('message', (event) => {
      const msg = JSON.parse(event.data);
      if (msg.method === 'Runtime.exceptionThrown') {
        consoleErrors.push(msg.params.exceptionDetails.text || 'exception');
      }
    });

    for (const [w, h] of VIEWPORTS) {
      await cdp.send('Emulation.setDeviceMetricsOverride', {
        width: w,
        height: h,
        deviceScaleFactor: 1,
        mobile: false,
      });

      await cdp.send('Page.navigate', { url: URL_UNDER_TEST });
      await delay(2500);

      // Let Phaser finish booting, then trigger a real resize transition.
      await evaluate(cdp, 'new Promise(r => setTimeout(r, 500))');
      const data = await evaluate(cdp, MEASURE);
      results.push({ requested: { w, h }, ...data });
    }

    // Resize-transition test: one loaded page, many viewport changes.
    await cdp.send('Emulation.setDeviceMetricsOverride', {
      width: 1920,
      height: 1080,
      deviceScaleFactor: 1,
      mobile: false,
    });
    await delay(500);

    const transitions = [];
    for (const [w, h] of VIEWPORTS) {
      await cdp.send('Emulation.setDeviceMetricsOverride', {
        width: w,
        height: h,
        deviceScaleFactor: 1,
        mobile: false,
      });
      await delay(600);
      const d = await evaluate(cdp, MEASURE);
      transitions.push({ requested: { w, h }, ...d });
    }

    // High-DPI check.
    await cdp.send('Emulation.setDeviceMetricsOverride', {
      width: 1280,
      height: 720,
      deviceScaleFactor: 2,
      mobile: false,
    });
    await delay(800);
    const hidpi = await evaluate(cdp, MEASURE);

    console.log(JSON.stringify({ results, transitions, hidpi, consoleErrors }, null, 2));
  } finally {
    if (cdp) cdp.close();
    edge.kill();
  }
}

main().catch((err) => {
  console.error('HARNESS ERROR:', err);
  process.exit(1);
});