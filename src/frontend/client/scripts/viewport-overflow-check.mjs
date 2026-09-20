/**
 * Focused check: does any overlay element escape the viewport, and does the
 * diagnostics panel stay inside it at small sizes?
 *
 * Development-only tooling for this task.
 */

import { spawn } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';

const URL_UNDER_TEST = process.argv[2] || 'http://localhost:5173/';
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const DEBUG_PORT = 9224;

const VIEWPORTS = [
  [1280, 720],
  [1024, 768],
  [900, 700],
  [800, 600],
  [600, 900],
  [400, 300],
  [320, 240],
  [280, 200],
];

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
  close() { this.ws.close(); }
}

const EXPR = `(() => {
  const de = document.documentElement;
  const vw = window.innerWidth, vh = window.innerHeight;

  const escapes = [];
  for (const el of document.querySelectorAll('.game-shell, .game-shell *')) {
    const r = el.getBoundingClientRect();
    if (r.right > vw + 0.5 || r.bottom > vh + 0.5 || r.left < -0.5 || r.top < -0.5) {
      escapes.push({
        tag: el.tagName + '.' + (typeof el.className === 'string' ? el.className : ''),
        x: Math.round(r.x), y: Math.round(r.y),
        right: Math.round(r.right), bottom: Math.round(r.bottom),
      });
    }
  }

  const panel = document.querySelector('.status-overlay-card');
  const canvas = document.querySelector('#phaser-container canvas');
  const pr = panel ? panel.getBoundingClientRect() : null;
  const cr = canvas ? canvas.getBoundingClientRect() : null;

  // Does the panel cover the center of the game canvas?
  let coversCenter = null;
  if (pr && cr) {
    const cx = cr.x + cr.width / 2;
    const cy = cr.y + cr.height / 2;
    coversCenter = cx >= pr.x && cx <= pr.right && cy >= pr.y && cy <= pr.bottom;
  }

  return {
    viewport: { w: vw, h: vh },
    canScrollX: de.scrollWidth > de.clientWidth,
    canScrollY: de.scrollHeight > de.clientHeight,
    scrollW: de.scrollWidth, clientW: de.clientWidth,
    scrollH: de.scrollHeight, clientH: de.clientHeight,
    escapingElements: escapes,
    panel: pr ? { x: Math.round(pr.x), y: Math.round(pr.y), right: Math.round(pr.right), bottom: Math.round(pr.bottom) } : null,
    panelInsideViewport: pr ? (pr.right <= vw + 0.5 && pr.bottom <= vh + 0.5 && pr.x >= -0.5 && pr.y >= -0.5) : null,
    canvas: cr ? { w: Math.round(cr.width), h: Math.round(cr.height) } : null,
    panelCoversCanvasCenter: coversCenter,
  };
})()`;

async function waitForDevTools() {
  for (let i = 0; i < 60; i++) {
    try {
      const res = await fetch(`http://127.0.0.1:${DEBUG_PORT}/json/version`);
      if (res.ok) return await res.json();
    } catch { /* not up */ }
    await delay(250);
  }
  throw new Error('DevTools endpoint did not become available');
}

async function main() {
  const edge = spawn(EDGE, [
    '--headless=new',
    `--remote-debugging-port=${DEBUG_PORT}`,
    '--remote-allow-origins=*',
    '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--hide-scrollbars',
    '--user-data-dir=' + process.env.TEMP + '\\viewport-overflow-profile',
    'about:blank',
  ], { stdio: 'ignore' });

  let cdp;
  const out = [];
  try {
    await waitForDevTools();
    const res = await fetch(`http://127.0.0.1:${DEBUG_PORT}/json/new?about:blank`, { method: 'PUT' });
    const target = await res.json();
    cdp = await Cdp.connect(target.webSocketDebuggerUrl);
    await cdp.send('Page.enable');
    await cdp.send('Runtime.enable');

    for (const [w, h] of VIEWPORTS) {
      await cdp.send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
      await cdp.send('Page.navigate', { url: URL_UNDER_TEST });
      await delay(2500);
      const r = await cdp.send('Runtime.evaluate', { expression: EXPR, returnByValue: true });
      out.push({ requested: { w, h }, ...r.result.value });
    }

    console.log(JSON.stringify(out, null, 2));
  } finally {
    if (cdp) cdp.close();
    edge.kill();
  }
}

main().catch((e) => { console.error('HARNESS ERROR:', e); process.exit(1); });