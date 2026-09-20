/**
 * Captures screenshots of the running app at each required viewport size.
 *
 * Development-only tooling for this task; not part of the application build.
 *
 * Usage: node scripts/viewport-shots.mjs [url]
 */

import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { setTimeout as delay } from 'node:timers/promises';

const URL_UNDER_TEST = process.argv[2] || 'http://localhost:5173/';
const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const OUT_DIR = 'viewport-shots';
const DEBUG_PORT = 9223;

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

async function main() {
  mkdirSync(OUT_DIR, { recursive: true });

  const edge = spawn(EDGE, [
    '--headless=new',
    `--remote-debugging-port=${DEBUG_PORT}`,
    '--remote-allow-origins=*',
    '--no-first-run',
    '--no-default-browser-check',
    '--disable-gpu',
    '--hide-scrollbars',
    '--user-data-dir=' + process.env.TEMP + '\\viewport-shots-profile',
    'about:blank',
  ], { stdio: 'ignore' });

  let cdp;
  try {
    await waitForDevTools();
    const res = await fetch(`http://127.0.0.1:${DEBUG_PORT}/json/new?about:blank`, {
      method: 'PUT',
    });
    const target = await res.json();
    cdp = await Cdp.connect(target.webSocketDebuggerUrl);

    await cdp.send('Page.enable');
    await cdp.send('Runtime.enable');

    for (const [w, h] of VIEWPORTS) {
      await cdp.send('Emulation.setDeviceMetricsOverride', {
        width: w,
        height: h,
        deviceScaleFactor: 1,
        mobile: false,
      });
      await cdp.send('Page.navigate', { url: URL_UNDER_TEST });
      await delay(3000);

      const shot = await cdp.send('Page.captureScreenshot', { format: 'png' });
      writeFileSync(`${OUT_DIR}/${w}x${h}.png`, Buffer.from(shot.data, 'base64'));
      console.log(`captured ${w}x${h}`);
    }
  } finally {
    if (cdp) cdp.close();
    edge.kill();
  }
}

main().catch((err) => {
  console.error('HARNESS ERROR:', err);
  process.exit(1);
});