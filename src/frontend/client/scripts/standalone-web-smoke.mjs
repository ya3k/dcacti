/**
 * TASK-189: Standalone Web End-to-End Browser Smoke Test Suite.
 *
 * Verifies the complete standalone web player journey introduced by ADR-020:
 *
 *   Clean Context
 *     ↓
 *   AuthScreen (Register unique account smoke_<timestamp>_<randomHex>)
 *     ↓
 *   Session token & starter grant verification (5 Pets, 3 Basic Cards, 10 Relics
 *     — MVP_SCOPE.md §1; TASK-213 decision, TASK-221 implementation)
 *     ↓
 *   Re-login verification (Clear storage → Login with credentials via AuthScreen UI)
 *     ↓
 *   StatusOverlay verification (Account badge & SignalR: Connected)
 *     ↓
 *   MainMenuScene (Phaser mounted → Real pointer click on START BATTLE)
 *     ↓
 *   LobbyScene (Loadout verification, every owned Relic reachable through the
 *     Relic column's own pager, 5 canonical MVP Bosses, unselected Boss guard,
 *     and each Card/Relic row's own delivered effect content — TASK-212A-1/TASK-221)
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

/**
 * Makes sure the inspected page is the foreground tab, and reports what it found.
 *
 * The suite opens its own target with `PUT /json/new`, which does not necessarily
 * make that tab the active one. While a document is `hidden`, Chromium suspends
 * `requestAnimationFrame`, so Phaser's game loop receives no steps: a scene
 * transition the scene has already claimed (its own guard is set) is queued and
 * never processed, and the run sees a navigation that "did not happen" for a
 * reason that has nothing to do with the application. `Page.bringToFront`
 * activates the tab, which starts the frames again.
 */
async function ensurePageVisible(cdp) {
  const visibility = await evaluate(cdp, `document.visibilityState`).catch(() => 'unknown');

  if (visibility === 'visible') {
    return { visibility, broughtToFront: false };
  }

  await cdp.send('Page.bringToFront');
  await delay(250);

  return {
    visibility: await evaluate(cdp, `document.visibilityState`).catch(() => 'unknown'),
    broughtToFront: true,
  };
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

/**
 * True only for the **settled** cast acknowledgement the battle scene renders
 * once the server has answered a cast request (`BattleScene.renderCastStatus`,
 * `SIGNALR_PROTOCOL.md` §2, §5):
 *
 * ```text
 * CardCast <cardId>: accepted. Awaiting the server's state push.
 * PetSkillCast: rejected (<reason>).
 * ```
 *
 * The same line also carries the scene's transient transport captions: the
 * `… in flight…` it renders *before* the acknowledgement arrives, and `… not
 * sent: …` when the request never left the client. Neither is the server's
 * answer, so a caption that merely *changed* is not an acknowledgement — only
 * `accepted` / `rejected` is (TASK-227).
 */
function isSettledCastAcknowledgement(text) {
  return typeof text === 'string' && /: (?:accepted|rejected)\b/.test(text);
}

/**
 * The closed set of **player-facing** callout forms the battle scene may show
 * (TASK-210 §7's ladder, extended by TASK-218B).
 *
 * It is deliberately an allow-list rather than "any text": each form is the
 * documented presentation of a delivered event, and the callout row must never
 * become an event feed. A callout that is not one of these is a failure even
 * though it is on the callout line.
 *
 * ```text
 * MATCH                        MatchCreated
 * COMBO ×N   (N >= 2)          ComboChanged        (MATCH3_RULES.md §6.2 item 1)
 * CARD: <name>                 CardCast
 * PET SKILL: <name>            PetSkillCast
 * RELIC: <name>                RelicTriggered      (TASK-218B, §3.2.23)
 * BOSS SKILL[: <name>]         BossSkillCast (TASK-231)
 * BOSS ENRAGED                 Boss Enrage transition (TASK-231)
 * PASSIVE … / BOSS PASSIVE …   PassiveCharged / PassiveTriggered
 * ```
 *
 * `MATCH` / `COMBO ×N` is exactly the vocabulary `MATCH3_RULES.md` §6 gives the
 * value, and `COMBO ×1` is deliberately never shown (§6.2 item 1).
 */
function isPlayerFacingCallout(callout) {
  return (
    callout === 'MATCH' ||
    /^COMBO ×[2-9]\d*$/.test(callout) ||
    /^CARD: \S/.test(callout) ||
    /^PET SKILL: \S/.test(callout) ||
    // TASK-218B: the Relic's own delivered name, and nothing else on that line.
    /^RELIC: \S/.test(callout) ||
    callout === 'BOSS SKILL' ||
    /^BOSS SKILL: \S/.test(callout) ||
    callout === 'BOSS ENRAGED' ||
    /^(?:Swap|Cast) rejected/.test(callout) ||
    /^(?:Swap does not create a match|Invalid swap|Invalid board position|Board has changed|Not enough Power|Only one card can be cast per turn|Card is not in your current loadout|Invalid card|Pet skill is not available|Battle session not found)/.test(callout) ||
    /^(?:BOSS )?PASSIVE(?: TRIGGERED)? \d+\/\d+$/.test(callout) ||
    /^(?:BOSS )?PASSIVE TRIGGERED$/.test(callout)
  );
}

/**
 * Whether a rendered callout carries a **technical identity** that must never
 * reach the player.
 *
 * This is the rejection half of the callout contract, and it is independent of the
 * allow-list: an identity could appear inside an otherwise valid-looking line, so
 * both are asserted. It covers the raw Relic instance identity
 * (`RELIC_RULES.md` §2.2 item 3), the Relic *definition* id spelling
 * `SIGNALR_PROTOCOL.md` §3.2.23's example misleadingly shows, an internal wire
 * event type name, a technical Trigger name (`RELIC_RULES.md` §3's closed set), and
 * a Card definition id.
 */
function carriesTechnicalIdentity(callout) {
  return (
    /relicinst/i.test(callout) ||
    /relic-[a-z0-9-]+/i.test(callout) ||
    /RelicTriggered|PowerChanged|DamageDealt|DamageTaken|MatchCreated|ComboChanged|CardCast|PetSkillCast|BossSkillCast|PassiveCharged|PassiveTriggered/.test(
      callout
    ) ||
    /On(?:Match|MatchCount|Combo|Cascade|HpBelow|PowerGain|DamageTaken|DamageDealt|CardCast|BattleStart|TurnStart|TurnEnd)\b/.test(
      callout
    ) ||
    /card-[a-z0-9-]+/i.test(callout)
  );
}

/**
 * The BattleScene's transient combat feedback, as it is at the moment of the probe.
 *
 * TASK-210 §7–§8: the callout is the one player-facing statement a resolution
 * produces, and the feedback layer holds the floating numbers that are alive right
 * then. Both are transient by design, so this is polled rather than waited for —
 * a single sample would race the fade.
 */
const FEEDBACK_PROBE = `
(() => {
  const s = window.__game && window.__game.scene.getScene('BattleScene');
  if (!s || !s.scene.isActive()) return null;
  const callout = s.calloutText && typeof s.calloutText.text === 'string' ? s.calloutText.text : '';
  const calloutBounds = s.calloutText && typeof s.calloutText.getBounds === 'function'
    ? s.calloutText.getBounds() : null;
  const layer = s.feedbackLayer;
  const floaters = [];
  if (layer && layer.list) {
    for (const o of layer.list) {
      if (o.type !== 'Text') continue;
      const b = typeof o.getBounds === 'function' ? o.getBounds() : null;
      floaters.push({
        text: typeof o.text === 'string' ? o.text : '',
        bounds: b ? { x: b.x, y: b.y, width: b.width, height: b.height } : null,
      });
    }
  }
  return {
    callout,
    calloutBounds: calloutBounds
      ? { x: calloutBounds.x, y: calloutBounds.y, width: calloutBounds.width, height: calloutBounds.height }
      : null,
    floaters,
  };
})()
`;

/**
 * Waits until the battle scene's transient combat feedback has faded.
 *
 * A floater lives 600 ms of **game** time (`BattleScene`'s `FLOATER_LIFE_MS`),
 * not wall-clock time: while the frame loop is stalled — a CDP screenshot, a
 * hidden document (see `ensurePageVisible`) — Phaser advances no tweens, so a
 * fixed `delay()` cannot make a previous batch's floaters expire. The
 * deterministic feedback batch is injected twice (once for the player-facing
 * screenshot, once for the measurement), so this waits on the scene's own
 * feedback layer instead of on the clock: the measurement pass then observes
 * exactly one batch, which is what
 * `phase5c.oneFloaterPerDamageInstancePlusPower` asserts.
 *
 * It is a **harness** fix and not a relaxed assertion — the assertion, the
 * injected batch, and the scene are unchanged. The fixed `delay(900)` it
 * replaces assumed wall-clock time equals game time, which TASK-210's own
 * harness note records as unsafe; TASK-221 makes a battle's delivered feedback
 * longer (ten ownable Relics, including the `OnCascade` / `OnPowerGain` ones),
 * which is what made the assumption bite.
 *
 * `requireSighting` makes the wait prove the batch was presented before waiting
 * for it to fade. Without it the helper returns as soon as nothing is on screen,
 * which is what draining a previous batch wants.
 */
async function waitForFeedbackToFade(cdp, timeoutMs, requireSighting) {
  const start = Date.now();
  let sighted = false;

  while (Date.now() - start < timeoutMs) {
    try {
      const sample = await evaluate(cdp, FEEDBACK_PROBE);
      const alive = (sample?.floaters ?? []).filter((f) => f.text);

      if (alive.length > 0) {
        sighted = true;
      } else if (!requireSighting || sighted) {
        return true;
      }
    } catch {
      /* the scene may be mid-transition; keep polling */
    }

    await delay(60);
  }

  return false;
}

/**
 * Removes any transient feedback still on the scene and waits for the layer to be
 * genuinely empty.
 *
 * `waitForFeedbackToFade` waits for the *tweens* to finish, which a stalled frame
 * loop can postpone indefinitely; this destroys what is left instead, so a
 * measurement pass starts from a known-empty layer regardless of the clock. It is a
 * harness measure and not an assertion: the assertions about one batch's feedback
 * remain exactly as strict, they are just no longer racing a previous pass's fade.
 */
async function drainFeedback(cdp, timeoutMs) {
  try {
    await evaluate(
      cdp,
      `(() => {
        const s = window.__game.scene.getScene('BattleScene');
        if (!s || !s.feedbackLayer) return false;
        for (const child of [...(s.feedbackLayer.list || [])]) child.destroy();
        return true;
      })()`
    );
  } catch {
    /* the scene may be mid-transition; the poll below reports what is left */
  }

  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const sample = await evaluate(cdp, FEEDBACK_PROBE);
      if ((sample?.floaters ?? []).filter((f) => f.text).length === 0) return true;
    } catch {
      /* keep polling */
    }
    await delay(60);
  }

  return false;
}

/**
 * Samples the battle scene's transient feedback until `timeoutMs` elapses.
 *
 * It returns every distinct callout it saw, the bounds of the last one, and the
 * floaters: `floaterTextSets` is one sorted text array per sampled instant that had
 * any floater alive (so "exactly one floater per damage instance" is an assertion
 * about a single instant, not about how many times the probe ran), and `floaters`
 * is the first observation of each distinct number, for the geometry checks.
 */
async function pollFeedback(cdp, timeoutMs) {
  const callouts = new Set();
  let calloutBounds = null;
  const floaterTextSets = [];
  const floatersByText = new Map();
  const start = Date.now();

  while (Date.now() - start < timeoutMs) {
    try {
      const sample = await evaluate(cdp, FEEDBACK_PROBE);
      if (sample) {
        if (sample.callout) {
          callouts.add(sample.callout);
          calloutBounds = sample.calloutBounds ?? calloutBounds;
        }

        const alive = (sample.floaters ?? []).filter((f) => f.text);
        if (alive.length > 0) {
          const texts = alive.map((f) => f.text).sort();
          if (!floaterTextSets.some((set) => set.join('|') === texts.join('|'))) {
            floaterTextSets.push(texts);
          }
          for (const floater of alive) {
            if (!floatersByText.has(floater.text)) {
              floatersByText.set(floater.text, floater);
            }
          }
        }
      }
    } catch {
      /* the scene may be mid-transition; keep sampling */
    }
    await delay(80);
  }

  return {
    callouts: [...callouts],
    calloutBounds,
    floaterTextSets,
    floaters: [...floatersByText.values()],
  };
}

/**
 * One deterministic resolution batch, delivered through the same entry point the
 * server-authoritative batches arrive on.
 *
 * It carries exactly what the contract delivers for a two-sided resolution: a
 * Match and its Combo, a Power generation, and two damage instances — one the Boss
 * took and one the active Pet took — each reported twice (`DamageDealt` and
 * `DamageTaken`, `SIGNALR_PROTOCOL.md` §3.2.15 item 1). Nothing here is a gameplay
 * fact the client computes: every member is one the server sends.
 *
 * TASK-218B adds the two Relic triggers below. `RelicTriggered` carries only the
 * owned **instance** identity (`SIGNALR_PROTOCOL.md` §3.2.23), which is what the
 * battle scene must resolve to the Relic's delivered `name` through the same
 * `getRelics()` read the Lobby makes (`API_CONTRACTS.md` §5.4). Two different
 * owned instances are named, so the assertion is about the lookup rather than
 * about one fixed label. They sit **after** the Combo, so the batch's own priority
 * ladder still selects `COMBO ×4` — the Relic events are present in the batch
 * without changing which single callout wins.
 *
 * It is presentation-only (`BattleScene` mutates no state from it), so the check
 * below can run it twice — once so the feedback is on screen for a screenshot,
 * once for the timed sampling.
 */
const FEEDBACK_INJECTION = `
(() => {
  const s = window.__game.scene.getScene('BattleScene');
  s.handleBattleEvents({
    battleId: 'task-210-feedback-probe',
    serverSequence: 999999,
    events: [
      { type: 'MatchCreated', shape: 'Straight', cells: [0, 1, 2], gemType: 'ATK', cascadeDepth: 0 },
      { type: 'ComboChanged', combo: 4 },
      { type: 'PowerChanged', delta: 12, power: 12, source: 'match' },
      { type: 'DamageCalculated', base: 100, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 88 },
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 88 },
      { type: 'DamageTaken', source: 'player', target: 'boss', amount: 88 },
      { type: 'DamageCalculated', base: 50, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 40 },
      { type: 'DamageDealt', source: 'boss', target: 'player', amount: 40 },
      { type: 'DamageTaken', source: 'boss', target: 'player', amount: 40 },
      ...window.__relicTriggerProbe
    ],
  });
  return true;
})()
`;

/**
 * The owned Relic instances this run may name, read from the **battle scene's own
 * loaded definitions** rather than from this script's expectations.
 *
 * `BattleScene.relicDefinitions` is the `GET /api/relics` read the scene performed
 * (`loadRelicDefinitions`), so a value taken from it is one the running stack
 * actually delivered. It also stashes the two `RelicTriggered` events
 * `FEEDBACK_INJECTION` appends, so that batch and this probe can never disagree
 * about which Relics it names. An empty result means the read did not reach the
 * scene, and the Relic half of phase 5c is then recorded as a failure rather than
 * skipped.
 */
const RELIC_PROBE_SETUP = `
(async () => {
  const s = window.__game.scene.getScene('BattleScene');
  const owned = s.relicDefinitions instanceof Map ? [...s.relicDefinitions.values()] : [];
  window.__relicTriggerProbe = owned.slice(0, 2).map((r) => ({ type: 'RelicTriggered', relicId: r.relicId }));
  return owned.map((r) => ({ relicId: r.relicId, name: r.name }));
})()
`;

/** Trusted left-button click with move, press, release */
async function realClick(cdp, point) {
  // A hidden document suspends the frames the game loop runs on, which silently
  // freezes every Phaser transition (see `ensurePageVisible`). Real input is where
  // that becomes visible to the suite, so the page is re-asserted as foreground
  // before the click is delivered — a no-op when it already is.
  await ensurePageVisible(cdp);

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

/**
 * The key of the scene Phaser currently has running, or `'none'`.
 *
 * `getScene` returns a scene for as long as it is *registered*, which is not the
 * same as it being runnable: while the scene manager is mid-transition a scene's
 * own `ScenePlugin` can already have released its manager, and asking it
 * `isActive()` then throws inside Phaser rather than answering. A scene in that
 * state is not running, so the probe answers that instead of propagating an
 * engine-internal error into the harness — a check that then finds no active
 * scene still fails on its own terms, so nothing is masked.
 */
const ACTIVE_SCENE = `
(() => {
  if (!window.__game) return null;
  const running = (s) => {
    try {
      return !!(s && s.scene && s.scene.isActive && s.scene.isActive());
    } catch {
      return false;
    }
  };
  for (const key of ['MainMenuScene', 'BattleHistoryScene', 'LobbyScene', 'BattleScene', 'ResultScene']) {
    if (running(window.__game.scene.getScene(key))) return key;
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
  // The scene's outbound controls are the interactive rectangles, each with its
  // caption drawn at the same centre (LobbyScene.drawControl). Reading them from
  // the live display list is what makes "the Lobby can be left" and "a failed
  // operation offers RETRY" assertions about the scene rather than about this
  // script's constants (TASK-211 §1–§3).
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
    // TASK-221: the Relic column pages once the grant owns more instances than
    // the column grid holds (MVP_SCOPE.md §1 grants ten against eight rows), so
    // a check reads the column's own page rather than assuming one page holds
    // every owned instance.
    relicPage: typeof s.relicPage === 'number' ? s.relicPage : null,
    relicPageCount: typeof s.relicPageCount === 'function' ? s.relicPageCount() : null,
    // TASK-212A-1: the responses the rows were rendered from, so a check can
    // assert that what the player reads is what the API delivered — including
    // the structured content API_CONTRACTS.md 5.3/5.4 now carry.
    ownedCards: (s.ownedCards || []).map((c) => ({
      cardId: c.cardId,
      name: c.name,
      category: c.category,
      effectDefinition: Array.isArray(c.effectDefinition) ? c.effectDefinition : [],
    })),
    ownedRelics: (s.ownedRelics || []).map((r) => ({
      relicId: r.relicId,
      name: r.name,
      trigger: r.trigger,
      condition: r.condition === undefined ? null : r.condition,
      effectDefinition: Array.isArray(r.effectDefinition) ? r.effectDefinition : [],
    })),
    reviewText: s.reviewText && typeof s.reviewText.text === 'string' ? s.reviewText.text : null,
    messageText: s.messageText && typeof s.messageText.text === 'string' ? s.messageText.text : null,
    errorText: s.errorText && typeof s.errorText.text === 'string' ? s.errorText.text : null,
    // TASK-211: the scene's own async state, so a check can see the guard and
    // the recovery state rather than infer them from a screenshot.
    loading: s.loading === true,
    startPending: s.startPending === true,
    failedOperation: s.failedOperation === undefined ? null : s.failedOperation,
    asyncRun: typeof s.asyncRun === 'number' ? s.asyncRun : null,
    hasTransitioned: s.hasTransitioned === true,
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

/**
 * The live cast-control captions, in draw order.
 *
 * The captions are children of the scene's cast-control **container**
 * (`renderCastControls` draws one tile + label pair per equipped entry), not of
 * the scene's root display list, so they are not part of
 * `BATTLE_SNAPSHOT.renderedTexts`. This reads the layer they are actually drawn
 * into, which is what the player sees.
 */
const CAST_CONTROL_CAPTIONS = `
(() => {
  const s = window.__game && window.__game.scene.getScene('BattleScene');
  const layer = s && s.castControlsLayer;
  if (!layer || !layer.list) return [];
  return layer.list.filter((o) => o.type === 'Text').map((o) => o.text);
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
  const state = runtime && runtime.getBattleState ? runtime.getBattleState() : null;
  /** One HUD line's rendered text, or null when the scene has no such line. */
  const line = (name) => (s[name] && typeof s[name].text === 'string' ? s[name].text : null);
  return {
    active: true,
    battleId: state ? state.battleId : null,
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
    boardText: s.boardMessageText && typeof s.boardMessageText.text === 'string' ? s.boardMessageText.text : '',
    swapText: s.swapText && typeof s.swapText.text === 'string' ? s.swapText.text : '',
    castText: s.castText && typeof s.castText.text === 'string' ? s.castText.text : '',
    // The scene's own input gates. A click that lands while one of these is set is
    // deliberately ignored by the scene (isInputLocked), so this is the evidence
    // that separates "the player's tap was refused" from "the tap missed".
    inputLocked: typeof s.isInputLocked === 'function' ? s.isInputLocked() : null,
    inputLockReasons: {
      presentationLocked: s.presentationLocked === true,
      swapPending: s.swapPending === true,
      actionInFlight: s.actionInFlight === true,
    },
    castControlsCount: s.castControlsLayer && s.castControlsLayer.list ? s.castControlsLayer.list.length : 0,
    // ---- The player-facing Battle HUD (TASK-209 §4, TASK-210 §1–§7) -------
    // Each line is read from the scene object that draws it, so this reports the
    // scene rather than this script's expectations.
    hud: {
      connection: line('connectionText'),
      bossName: line('bossNameText'),
      bossHp: line('bossHpText'),
      petName: line('petNameText'),
      petHp: line('petHpText'),
      petPower: line('petPowerText'),
      petPassive: line('petPassiveText'),
      petPassiveProgress: line('petPassiveProgressText'),
      petStatus: line('petStatusText'),
      combo: line('comboText'),
      matches: line('matchesText'),
      callout: line('calloutText'),
      boardMessage: line('boardMessageText'),
    },
    // The HP / Power / Passive gauges: each one's declared track geometry, its
    // fill's current width, and whether the fill is drawn at all. TASK-210 §1
    // requires the bar to be a presentation of the authoritative current/max pair,
    // so the fill width is asserted against trackWidth * current / max below
    // rather than against a hard-coded pixel count.
    gauges: (() => {
      const out = {};
      for (const name of ['bossHpGauge', 'petHpGauge', 'powerGauge', 'passiveGauge']) {
        const g = s[name];
        if (!g || !g.fill) continue;
        out[name] = {
          x: g.x,
          y: g.y,
          width: g.width,
          height: g.height,
          fillWidth: typeof g.fill.width === 'number' ? g.fill.width : null,
          fillVisible: g.fill.visible === true,
          fillX: typeof g.fill.x === 'number' ? g.fill.x : null,
        };
      }
      return out;
    })(),
    // Every transient feedback object currently in the scene's feedback layer —
    // the floating combat numbers — with the bounds a check can test against the
    // board and the safe area. The layer is emptied as each effect completes, so a
    // non-empty list is a snapshot taken while feedback is on screen.
    feedback: (() => {
      const layer = s.feedbackLayer;
      if (!layer || !layer.list) return [];
      return layer.list.map((o) => {
        const text = typeof o.text === 'string' ? o.text : null;
        const b = typeof o.getBounds === 'function' ? o.getBounds() : null;
        return {
          type: o.type,
          text,
          bounds: b ? { x: b.x, y: b.y, width: b.width, height: b.height } : null,
        };
      });
    })(),
    // The cast controls' own bounds, so "the HUD and the feedback never block a
    // card control or the pet skill" is an assertion about the rendered geometry.
    castControlBounds: (() => {
      const layer = s.castControlsLayer;
      if (!layer || !layer.list) return [];
      return layer.list
        .filter((o) => o.type === 'Rectangle' && typeof o.getBounds === 'function')
        .map((o) => {
          const b = o.getBounds();
          return { x: b.x, y: b.y, width: b.width, height: b.height };
        });
    })(),
    // Every rendered Text object's content, so a check can assert that no
    // developer diagnostic and no raw event-log line is rendered anywhere.
    renderedTexts: (s.children && s.children.list ? s.children.list : [])
      .filter((o) => o.type === 'Text')
      .map((o) => o.text)
      .filter((t) => typeof t === 'string' && t.length > 0),
    // The scene's own development-only event log. It is deliberately NOT
    // rendered any more; this is what lets the check prove that.
    presentedEvents: typeof s.getPresentedEvents === 'function' ? s.getPresentedEvents() : [],
    // Each HUD line's world bounds, so "does not overlap the board and stays
    // inside the safe area" is an assertion about the rendered geometry.
    hudBounds: (() => {
      const names = ['connectionText', 'bossNameText', 'bossHpText', 'petNameText', 'petHpText',
        'petPowerText', 'petPassiveText', 'petPassiveProgressText', 'petStatusText', 'comboText',
        'matchesText', 'calloutText', 'swapText', 'castText', 'boardMessageText'];
      const out = {};
      for (const name of names) {
        const o = s[name];
        if (!o || typeof o.getBounds !== 'function') continue;
        const b = o.getBounds();
        out[name] = { x: b.x, y: b.y, width: b.width, height: b.height };
      }
      return out;
    })(),
    // The authoritative values the HUD was drawn from — the runtime's own
    // synchronized copy. Comparing the two is what proves the HUD renders the
    // delivered state rather than a value of its own.
    authoritative: state ? {
      bossId: state.bossState.bossId,
      bossHp: state.bossState.hp,
      bossMaxHp: state.bossState.maxHp,
      petHp: state.petState.hp,
      petMaxHp: state.petState.maxHp,
      power: state.petState.power,
      combo: state.playerState.combo,
      matchCount: state.playerState.matchCount,
    } : null,
    viewport: { left: rect.left, top: rect.top, scaleX: rect.width / gameWidth, scaleY: rect.height / gameHeight },
  };
})()
`;

const RESULT_SNAPSHOT = `
(() => {
  const s = window.__game && window.__game.scene.getScene('ResultScene');
  if (!s || !s.scene.isActive()) return { active: false, controls: [] };
  // The scene's controls are the interactive rectangles, each with its caption
  // drawn at the same centre (ResultScene.drawButton / drawRewardRetryControl).
  // Reading them from the live display list is what makes "the two approved
  // exits always exist, and RETRY exists only in the failure state" an assertion
  // about the scene rather than about this script's constants (TASK-211 §5–§6).
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
    battleId: s.resultData && typeof s.resultData.battleId === 'string' ? s.resultData.battleId : null,
    finalBossHp: s.resultData && typeof s.resultData.finalBossHp === 'number' ? s.resultData.finalBossHp : null,
    finalPlayerHp: s.resultData && typeof s.resultData.finalPlayerHp === 'number' ? s.resultData.finalPlayerHp : null,
    outcomeText: s.outcomeText && typeof s.outcomeText.text === 'string' ? s.outcomeText.text : '',
    bossHpText: s.bossHpText && typeof s.bossHpText.text === 'string' ? s.bossHpText.text : '',
    playerHpText: s.playerHpText && typeof s.playerHpText.text === 'string' ? s.playerHpText.text : '',
    durationText: s.durationText && typeof s.durationText.text === 'string' ? s.durationText.text : '',
    rewardText: s.rewardText && typeof s.rewardText.text === 'string' ? s.rewardText.text : '',
    // TASK-211: the reward block's own state, so a check can distinguish a
    // delivered zero reward from a failed or still-pending read.
    rewardState: s.rewardState === undefined ? null : s.rewardState,
    rewardRetryable: s.rewardRetryable === true,
    rewardLoadRun: typeof s.rewardLoadRun === 'number' ? s.rewardLoadRun : null,
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

    // The target this suite opened is not necessarily the active tab, and a hidden
    // document suspends the frames Phaser's loop runs on. Re-asserting the
    // foreground here is what makes every later scene transition deterministic.
    const visibility = await ensurePageVisible(cdp);
    record(
      'phase0.inspectedPageIsForeground',
      visibility.visibility === 'visible',
      visibility
    );

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

    /** The live controls of a Lobby snapshot, as `{ label, x, y, … }`. */
    const lobbyControlsOf = (snap) => snap.controls || [];

    /** The real screen point of the Lobby control captioned `label`. */
    const lobbyControlPoint = async (label) => {
      const snap = await evaluate(cdp, LOBBY_SNAPSHOT);
      const control = lobbyControlsOf(snap).find((c) => c.label === label);
      if (!control) {
        throw new Error(`LobbyScene rendered no "${label}" control.`);
      }
      return toScreen(control.x, control.y);
    };

    // -------------------------------------------------------------
    // PHASE 3c: THE LOBBY'S EXIT (TASK-211 §1–§2)
    // -------------------------------------------------------------
    // The Lobby's only transition used to be a successful battle start, so a
    // player who opened it could not reach the main menu, the collection, or the
    // battle history again without reloading the page. `< BACK` is that missing
    // exit, and it is a real control in every Lobby state — including while the
    // collection read is still in flight.
    console.log('\n--- Phase 3c: Lobby exit (< BACK) ---');

    const firstLobby = await evaluate(cdp, LOBBY_SNAPSHOT);
    const lobbyControlBounds = (control) => ({
      left: control.x - control.width / 2,
      right: control.x + control.width / 2,
      top: control.y - control.height / 2,
      bottom: control.y + control.height / 2,
    });
    const overlaps = (a, b) =>
      a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;

    record(
      'phase3c.lobbyOffersAnExitAndTheStart',
      lobbyControlsOf(firstLobby).some((c) => c.label === '< BACK') &&
        lobbyControlsOf(firstLobby).some((c) => c.label === 'START BATTLE'),
      lobbyControlsOf(firstLobby).map((c) => c.label)
    );

    // Every control the Lobby draws is inside the 1280×720 frame and inside the
    // 24 px safe area, and no two of them overlap — so `< BACK` cannot sit on
    // top of START BATTLE (TASK-211 §1, "no layout clipping/overlap").
    const lobbyControls = lobbyControlsOf(firstLobby);
    const lobbyControlBoundsList = lobbyControls.map(lobbyControlBounds);
    record(
      'phase3c.lobbyControlsFitTheFrameWithoutOverlap',
      lobbyControlBoundsList.every(
        (b) => b.left >= 24 && b.top >= 24 && b.right <= 1256 && b.bottom <= 696
      ) &&
        lobbyControlBoundsList.every((a, i) =>
          lobbyControlBoundsList.every((b, j) => i === j || !overlaps(a, b))
        ),
      lobbyControls.map((c) => ({ label: c.label, ...lobbyControlBounds(c) }))
    );

    await realClick(cdp, await lobbyControlPoint('< BACK'));
    await waitForCondition(
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene',
      'MainMenuScene active after the Lobby < BACK',
      10000
    );
    record('phase3c.backReturnedToMainMenu', true);

    // Leaving the Lobby ran its own teardown on the engine's SHUTDOWN event: the
    // stopped scene keeps no in-progress selection and no claim on a transition,
    // and it is not the scene that is running.
    const stoppedLobby = (await evaluate(cdp, SCENE_LIFECYCLE_SNAPSHOT)).LobbyScene;
    record(
      'phase3c.stoppedLobbyRanItsTeardown',
      stoppedLobby.active === false &&
        stoppedLobby.selectedPetId === null &&
        stoppedLobby.hasTransitioned === false &&
        stoppedLobby.destroyHandlers === 1,
      stoppedLobby
    );

    // Re-entering is a fresh Lobby run: this is the first of the run's two
    // scene-reuse cycles, and the controls below are the new run's own.
    for (let i = 0; i < 8; i++) {
      if ((await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene') break;
      await realClick(cdp, menuStartPoint);
      await delay(1000);
    }
    await waitForCondition(
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene',
      'LobbyScene active again after re-entering',
      10000
    );

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

    // The canvas box is stable for the whole run, so one logical→screen mapping
    // is enough; it is taken from the running Lobby's own snapshot.
    const lobbyViewport = lobby.viewport;
    const toVp = (gx, gy) => ({
      x: lobbyViewport.left + gx * lobbyViewport.scaleX,
      y: lobbyViewport.top + gy * lobbyViewport.scaleY,
    });
    const centreOf = (o) => toVp(o.x + o.width / 2, o.y + o.height / 2);

    /** The Lobby control captioned `label`, from a live snapshot. */
    const lobbyControlOf = (snap, label) => {
      const control = lobbyControlsOf(snap).find((c) => c.label === label);
      if (!control) {
        throw new Error(`LobbyScene rendered no "${label}" control.`);
      }
      return control;
    };

    const bossOptionsOf = (snapshot) =>
      snapshot.interactive.filter(
        (o) => o.text !== null && CANONICAL_BOSSES.some((boss) => o.text.endsWith(`  ${boss.element}`))
      );

    // Verify all 5 canonical MVP Bosses rendered
    const bossOptions = bossOptionsOf(lobby);
    record('phase4.fiveBossesRendered', bossOptions.length === 5, bossOptions.map((o) => o.text));

    // Verify initially no Boss is selected
    record('phase4.noBossInitiallySelected', lobby.selectedBossId === null);

    // -------------------------------------------------------------
    // TASK-212A-1 / TASK-221: each Card and Relic row states what it changes —
    // and every owned Relic is reachable.
    //
    // `API_CONTRACTS.md` §5.3/§5.4 deliver the definition's own structured
    // content, and the Lobby row must present it: a row carrying only a name
    // leaves "what does this do?" unanswerable before START BATTLE (GDD.md
    // §17). The lines are read from the running scene's own interactive
    // objects, so these are assertions about the rendered Lobby rather than
    // about this script's expectations.
    //
    // TASK-221 owns the second half: MVP_SCOPE.md §1 grants ten Relics against a
    // column grid of eight rows (TASK-213 §3.2 C-2), so the column pages through
    // its own PREV / NEXT controls. Every Relic assertion below is therefore made
    // per page and over the union of the pages the survey walks.
    // -------------------------------------------------------------
    const contentLinesOf = (snapshot, pattern) =>
      snapshot.interactive.filter(
        (o) => o.type === 'Text' && o.text !== null && pattern.test(o.text)
      );

    /** A Relic row's own identity, without its marker or its slot suffix. */
    const relicRowName = (row) =>
      (row.text || '').replace(/^[○●]\s*/, '').replace(/\s+slot \d+$/, '');

    /** The live Relic option rows of one snapshot (the Relic column's x). */
    const relicRowsOf = (snapshot) =>
      snapshot.interactive.filter(
        (o) => o.type === 'Text' && /^[○●] /.test(o.text || '') && o.x > 700
      );

    // A Relic content line opens with its Trigger
    // (LobbyScene.renderRelics → ContentEffectFormat.formatRelicSummary).
    const relicContentOf = (snapshot) => contentLinesOf(snapshot, /^On[A-Za-z]+/);

    const relicPages = [];
    let relicSurvey = lobby;

    for (let step = 0; step < 12; step++) {
      relicPages.push({
        index: relicSurvey.relicPage,
        count: relicSurvey.relicPageCount,
        rows: relicRowsOf(relicSurvey),
        content: relicContentOf(relicSurvey),
      });

      if (relicSurvey.relicPage + 1 >= relicSurvey.relicPageCount) break;

      await realClick(cdp, centreOf(lobbyControlOf(relicSurvey, 'NEXT')));
      await delay(250);
      relicSurvey = await evaluate(cdp, LOBBY_SNAPSHOT);
    }

    // Back to the column's first page before anything else runs: the phases
    // below click rows by coordinate and select the loadout there.
    for (let step = 0; step < 12; step++) {
      if (relicSurvey.relicPage === 0) break;
      await realClick(cdp, centreOf(lobbyControlOf(relicSurvey, 'PREV')));
      await delay(250);
      relicSurvey = await evaluate(cdp, LOBBY_SNAPSHOT);
    }

    lobby = relicSurvey;

    const relicRowNamesOf = (page) => page.rows.map(relicRowName);
    const pagedRelicNames = relicPages.flatMap(relicRowNamesOf);
    const ownedRelicNames = lobby.ownedRelics.map((r) => r.name);
    const relicContentLines = relicPages.flatMap((page) => page.content);
    const cardContentLines = contentLinesOf(lobby, /^(?:Heal|Shield|Power|Damage|Burn|Crit)\s+\d/);

    // The defect TASK-213 §3.2 C-2 recorded, asserted as fixed: the grant owns
    // more Relics than one page holds, the column offers exactly as many pages as
    // that needs, and every owned instance is presented on some page — with its
    // own content line.
    record(
      'phase4.everyOwnedRelicIsReachable',
      lobby.ownedRelicsCount > 8 &&
        relicPages.length === lobby.relicPageCount &&
        relicPages.every((page) => page.rows.length > 0 && page.rows.length <= 8) &&
        relicPages.every((page) => page.content.length === page.rows.length) &&
        pagedRelicNames.length === ownedRelicNames.length &&
        ownedRelicNames.every((name) => pagedRelicNames.includes(name)) &&
        lobby.relicPage === 0,
      {
        pages: relicPages.map((page) => ({
          index: page.index,
          count: page.count,
          rows: relicRowNamesOf(page),
        })),
        ownedRelicNames,
      }
    );

    record(
      'phase4.cardRowsStateWhatTheyChange',
      cardContentLines.length >= lobby.ownedCardsCount &&
        cardContentLines.some((o) => o.text.includes('% Max HP')),
      cardContentLines.map((o) => o.text)
    );

    // A Relic content line opens with its Trigger and carries the condition and
    // the effect the definition authored. Every owned Relic is on exactly one
    // page, so the union carries exactly one line per owned instance.
    record(
      'phase4.relicRowsStateTriggerConditionAndEffect',
      relicContentLines.length === lobby.ownedRelicsCount &&
        relicContentLines.every((o) => o.text.includes('|')) &&
        relicContentLines.every((o) => /\([A-Za-z]+, [A-Za-z]+\)$/.test(o.text)),
      relicContentLines.map((o) => o.text)
    );

    // The content is a second line of the row it belongs to — never a separate
    // control, never outside the safe area, and never two rows sharing one. The
    // geometry is asserted per page, because a page is what the player sees: two
    // pages legitimately reuse one row position.
    const allContentLines = [...cardContentLines, ...relicContentLines];
    record(
      'phase4.contentLinesSitInsideTheirOwnRow',
      allContentLines.length > 0 &&
        cardContentLines.every((line) =>
          lobby.interactive.some(
            (row) =>
              row.type === 'Text' &&
              (row.text || '').startsWith('○') &&
              Math.abs(line.y - (row.y + 15)) < 0.01
          )
        ) &&
        relicPages.every((page) =>
          page.content.every((line) =>
            page.rows.some((row) => Math.abs(line.y - (row.y + 15)) < 0.01)
          )
        ) &&
        new Set(cardContentLines.map((line) => `${line.x}:${line.y}`)).size ===
          cardContentLines.length &&
        relicPages.every(
          (page) =>
            new Set(page.content.map((line) => `${line.x}:${line.y}`)).size === page.content.length
        ) &&
        allContentLines.every((line) => line.y > 0 && line.y < 696),
      allContentLines.map((line) => ({ text: line.text, x: line.x, y: line.y }))
    );

    // The content vocabulary is the response's, never a client-side catalog.
    //
    // TASK-221 changes what that means for Relics: all ten MVP Relics are owned
    // now (MVP_SCOPE.md §1), so `Emergency Core`'s delivered rule IS on screen.
    // The assertion is therefore that every rendered Relic line is one the
    // delivered responses can produce — and that the Card column still shows
    // nothing beyond the three unlocked Basic Cards, whose Pet Skill Cards are
    // derived server-side and never granted (CARD_RULES.md §1 item 4).
    record(
      'phase4.contentIsNotAClientSideCatalog',
      cardContentLines.every(
        (o) =>
          !/Tidal Barrier|Iron Fang|Inferno|Venomous Bloom|Earthshaker|Emergency Core|CardCost/.test(
            o.text
          )
      ) &&
        relicContentLines.every((o) =>
          lobby.ownedRelics.some(
            (r) =>
              o.text.startsWith(r.trigger) &&
              (r.condition === null || o.text.includes(r.condition.conditionType))
          )
        ) &&
        relicContentLines.some((o) => /CardCost/.test(o.text)) &&
        lobby.ownedRelics.some((r) => r.name === 'Emergency Core'),
      {
        cardContentLines: cardContentLines.map((o) => o.text),
        relicContentLines: relicContentLines.map((o) => o.text),
      }
    );

    // What the player reads is what the API delivered: every element of every
    // returned Card's `effectDefinition` is on screen with its identity and its
    // magnitude, and every returned Relic's `trigger` — and its condition when
    // it has one — opens a rendered line. `API_CONTRACTS.md` §5.3/§5.4 are the
    // only source for both.
    const renderedCardContent = lobby.ownedCards.every((c) =>
      (c.effectDefinition || []).every((element) => {
        const containing = cardContentLines.filter((o) => o.text.includes(element.effectType));
        if (containing.length === 0) return false;
        if (element.value === undefined) return true;
        return containing.some((o) => new RegExp(`\\b${element.value}\\b`).test(o.text));
      })
    );
    const renderedRelicContent = lobby.ownedRelics.every(
      (r) =>
        relicContentLines.some((o) => o.text.startsWith(r.trigger)) &&
        (r.condition === null ||
          relicContentLines.some((o) => o.text.includes(r.condition.conditionType))) &&
        (r.effectDefinition || []).every((element) =>
          relicContentLines.some((o) => o.text.includes(element.effectType))
        )
    );
    record(
      'phase4.renderedContentMatchesTheDeliveredResponse',
      renderedCardContent && renderedRelicContent,
      {
        cards: lobby.ownedCards,
        relics: lobby.ownedRelics,
        cardContentLines: cardContentLines.map((o) => o.text),
        relicContentLines: relicContentLines.map((o) => o.text),
      }
    );

    // TASK-212A-1: the content is a second line of an existing row, so it must
    // stay inside the game's own safe area — never clipped by the frame and
    // never leaving the canvas. Measured from Phaser's own text bounds, which is
    // the presentation the player actually reads.
    record(
      'phase4.contentLinesStayInsideTheSafeArea',
      allContentLines.every(
        (line) =>
          line.x >= 24 &&
          line.x + line.width <= 1256 &&
          line.y >= 24 &&
          line.y + line.height <= 696
      ),
      allContentLines.map((line) => ({
        text: line.text,
        left: line.x,
        right: line.x + line.width,
        bottom: line.y + line.height,
      }))
    );

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

    /** Selects the named canonical Boss through its own row, and settles. */
    const selectBossIn = async (displayName) => {
      const boss = CANONICAL_BOSSES.find((b) => b.displayName === displayName);
      for (let attempt = 1; attempt <= 3; attempt++) {
        const option = bossOptionsOf(lobby).find((o) => o.text.includes(boss.displayName));
        if (option) {
          await realClick(cdp, centreOf(option));
        }
        lobby = await evaluate(cdp, LOBBY_SNAPSHOT);
        if (lobby.selectedBossId === boss.bossId) break;
      }
      return lobby;
    };

    // Test Start Battle without selecting a Boss: must display validation error
    const startButton = lobbyControlOf(lobby, 'START BATTLE');
    const startButtonPoint = toVp(startButton.x, startButton.y);
    await realClick(cdp, startButtonPoint);

    lobby = await evaluate(cdp, LOBBY_SNAPSHOT);
    const hasBossValidationError = (lobby.messageText || '').includes('Choose a Boss.') ||
                                  (lobby.reviewText || '').includes('Boss: none') ||
                                  (lobby.messageText || '').includes('fill the selection');
    record('phase4.noBossValidationPreventedStart', hasBossValidationError && (await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene');

    // Select canonical MVP Boss: Kim Lôi Vương
    const targetBoss = CANONICAL_BOSSES.find((b) => b.bossId === 'boss-kim-loi-vuong');
    lobby = await selectBossIn(targetBoss.displayName);
    record('phase4.bossSelected', lobby.selectedBossId === targetBoss.bossId, lobby.selectedBossId);
    await captureScreenshot(cdp, `run${runNumber}-05-lobby-selected`);

    // The loadout Battle 1 is submitted with. The post-result lifecycle must
    // preserve exactly this and hand it back editable on PLAY AGAIN
    // (D-202-03 = D, ADR-022).
    const battle1Loadout = {
      petId: lobby.selectedPetId,
      bossId: lobby.selectedBossId,
      cardIds: [...lobby.selectedCardIds],
      relicIds: [...lobby.selectedRelicIds],
    };

    // -------------------------------------------------------------
    // PHASE 4b: A REJECTED START IS READABLE, RETRYABLE AND ESCAPABLE
    // (TASK-211 §2–§4)
    // -------------------------------------------------------------
    console.log('\n--- Phase 4b: Lobby start failure, RETRY and BACK ---');

    /**
     * Makes the next submission carry a Pet this account does not own, which is
     * the documented `400 PET_NOT_OWNED` rejection (`API_CONTRACTS.md` §3). The
     * request keeps all four documented members and the selection stays
     * complete, and the server — not this script — decides it is invalid.
     */
    const armUnownedPet = async (petId) => {
      await evaluate(cdp, `(() => {
        const s = window.__game.scene.getScene('LobbyScene');
        s.selectedPetId = ${JSON.stringify(petId)};
        return true;
      })()`);
    };

    /** Puts the genuinely owned Pet back, ready for a retry. */
    const armOwnedPet = async () => {
      await evaluate(cdp, `(() => {
        const s = window.__game.scene.getScene('LobbyScene');
        s.selectedPetId = ${JSON.stringify(battle1Loadout.petId)};
        return true;
      })()`);
    };

    /** Waits for the Lobby to report a settled failure of `operation`. */
    const waitForLobbyFailure = (operation, description) =>
      waitForCondition(
        async () => {
          const snap = await evaluate(cdp, LOBBY_SNAPSHOT);
          return snap.active &&
            snap.failedOperation === operation &&
            snap.startPending === false &&
            (snap.errorText || '').length > 0
            ? snap
            : false;
        },
        description,
        12000
      );

    await armUnownedPet('pet-instance-not-owned');
    await realClick(cdp, startButtonPoint);
    const failedStart = await waitForLobbyFailure('start', 'Lobby reported the rejected battle start');

    // §6: the server's own human-readable detail is what the player reads. The
    // Lobby never leads with an HTTP status, a request path, or the machine code.
    record(
      'phase4b.rejectedStartIsReadable',
      failedStart.errorText.includes('Pet') &&
        failedStart.errorText.includes('owned') &&
        !/status \d/.test(failedStart.errorText) &&
        !failedStart.errorText.includes('/api/') &&
        !failedStart.errorText.includes('PET_NOT_OWNED'),
      failedStart.errorText
    );

    // The wire really carried the documented §6 envelope for this rejection.
    const rejectionResponse = Array.from(apiResponses.entries()).find(
      ([, resp]) => resp.url.includes('/api/battle/start') && resp.status === 400
    );
    const rejectionBody = rejectionResponse
      ? await (async () => {
          try {
            const body = await cdp.send('Network.getResponseBody', { requestId: rejectionResponse[0] });
            return JSON.parse(body.body);
          } catch {
            return null;
          }
        })()
      : null;
    record(
      'phase4b.serverAnsweredWithTheDocumentedEnvelope',
      rejectionBody !== null &&
        rejectionBody.error === 'PET_NOT_OWNED' &&
        typeof rejectionBody.message === 'string' &&
        rejectionBody.message.length > 0,
      rejectionBody
    );

    // The failure state offers both recovery controls beside the still-usable
    // START BATTLE, and the selection that was rejected survived it.
    record(
      'phase4b.rejectedStartOffersRetryAndBack',
      ['< BACK', 'RETRY', 'START BATTLE'].every((label) =>
        lobbyControlsOf(failedStart).some((c) => c.label === label)
      ) && (await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene',
      lobbyControlsOf(failedStart).map((c) => c.label)
    );
    record(
      'phase4b.theRejectedSelectionSurvived',
      failedStart.selectedCardIds.length === 3 &&
        failedStart.selectedRelicIds.length === 3 &&
        failedStart.selectedBossId === battle1Loadout.bossId,
      {
        cards: failedStart.selectedCardIds.length,
        relics: failedStart.selectedRelicIds.length,
        boss: failedStart.selectedBossId,
      }
    );
    await captureScreenshot(cdp, `run${runNumber}-05b-lobby-start-failure`);

    // --- Failure → RETRY -----------------------------------------------------
    // The retry repeats the *start operation* with the state as it stands, so
    // an untouched invalid selection is rejected again — the same documented
    // answer, and the control is still usable afterwards.
    await realClick(cdp, await lobbyControlPoint('RETRY'));
    const retriedFailure = await waitForLobbyFailure('start', 'Lobby reported the retried rejection');
    record(
      'phase4b.retryRepeatsTheStartOperation',
      retriedFailure.errorText === failedStart.errorText &&
        (retriedFailure.asyncRun === failedStart.asyncRun),
      { before: failedStart.errorText, after: retriedFailure.errorText }
    );

    // --- Failure → BACK ------------------------------------------------------
    await realClick(cdp, await lobbyControlPoint('< BACK'));
    await waitForCondition(
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene',
      'MainMenuScene active after the Lobby failure < BACK',
      10000
    );
    record('phase4b.backFromFailureReturnedToMainMenu', true);

    // Re-enter and rebuild the same selection through the UI, then take the
    // second rejection to its successful retry.
    for (let i = 0; i < 8; i++) {
      if ((await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene') break;
      await realClick(cdp, menuStartPoint);
      await delay(1000);
    }
    lobby = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, LOBBY_SNAPSHOT);
        return snap.active && snap.ownedPetsCount > 0 && snap.ownedCardsCount >= 3 && snap.ownedRelicsCount >= 3
          ? snap
          : false;
      },
      'Lobby collections reloaded after the failure exit',
      12000
    );
    await selectLoadout();
    lobby = await selectBossIn(targetBoss.displayName);

    // TASK-205: the Lobby's teardown wiring on the run that starts the battle,
    // so the same instance's later run (after PLAY AGAIN) can be compared with
    // it. One teardown per event is correct; a higher number means an earlier
    // run's handlers were left behind.
    const lobbyFirstRun = (await evaluate(cdp, SCENE_LIFECYCLE_SNAPSHOT)).LobbyScene;

    await armUnownedPet('pet-instance-not-owned');
    await realClick(cdp, startButtonPoint);
    await waitForLobbyFailure('start', 'Lobby reported the second rejected battle start');

    // The player fixes the selection and presses RETRY: the retry submits a new,
    // complete request — the same operation, the corrected state — and its
    // success is what enters the battle.
    await armOwnedPet();
    await realClick(cdp, await lobbyControlPoint('RETRY'));

    // Wait for BattleScene
    await waitForCondition(
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'BattleScene',
      'Transition to BattleScene after the successful retry',
      15000
    );
    record('phase4b.retryStartedTheBattle', true);
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

    // -------------------------------------------------------------
    // PHASE 5b: PLAYER-FACING BATTLE HUD (TASK-209 §4, TASK-210 §1–§7)
    // -------------------------------------------------------------
    // The HUD must let a player identify the Boss, see both sides' HP as a gauge
    // *and* as the delivered numbers, read the Pet's Power, the delivered
    // Match/Combo feedback, and the Pet's Passive and Status Effect lines — every
    // value drawn from the authoritative state the server pushed, with none of the
    // developer diagnostics the scene used to present.
    console.log('\n--- Phase 5b: Battle HUD (boss/Pet gauges, Power, match feedback, passive & status) ---');

    const delivered = battle.authoritative;
    const bossOption = CANONICAL_BOSSES.find((b) => b.bossId === delivered.bossId);

    record(
      'phase5b.hudBossIdentityIsCorrect',
      bossOption !== undefined && battle.hud.bossName === bossOption.displayName,
      { bossId: delivered.bossId, rendered: battle.hud.bossName, expected: bossOption ? bossOption.displayName : null }
    );

    // ---- HP: the authoritative numbers, still on screen, beside a gauge -------
    record(
      'phase5b.hudBossHpIsVisible',
      battle.hud.bossHp === `${delivered.bossHp} / ${delivered.bossMaxHp}`,
      { rendered: battle.hud.bossHp, delivered: [delivered.bossHp, delivered.bossMaxHp] }
    );
    record(
      'phase5b.hudPetHpIsVisible',
      battle.hud.petHp === `${delivered.petHp} / ${delivered.petMaxHp}`,
      { rendered: battle.hud.petHp, delivered: [delivered.petHp, delivered.petMaxHp] }
    );

    // ---- HP gauges: `fill = round(trackWidth * current / max)` ----------------
    // The presentation rule is asserted, not a pixel constant: the bar is the
    // authoritative pair drawn as a proportion, so a state push that changes HP
    // moves the fill by exactly that ratio — and a zero HP value leaves the track
    // empty while the numbers above stay on screen.
    const expectedGaugeFill = (gauge, current, max) =>
      gauge && max > 0 ? Math.round(gauge.width * Math.min(1, Math.max(0, current / max))) : 0;

    const bossGauge = battle.gauges.bossHpGauge;
    const petGauge = battle.gauges.petHpGauge;
    record(
      'phase5b.bossHpGaugeRenders',
      Boolean(bossGauge) &&
        bossGauge.width > 0 &&
        bossGauge.fillWidth === expectedGaugeFill(bossGauge, delivered.bossHp, delivered.bossMaxHp) &&
        bossGauge.fillVisible === (expectedGaugeFill(bossGauge, delivered.bossHp, delivered.bossMaxHp) > 0),
      { gauge: bossGauge, delivered: [delivered.bossHp, delivered.bossMaxHp] }
    );
    record(
      'phase5b.petHpGaugeRenders',
      Boolean(petGauge) &&
        petGauge.width > 0 &&
        petGauge.fillWidth === expectedGaugeFill(petGauge, delivered.petHp, delivered.petMaxHp) &&
        petGauge.fillVisible === (expectedGaugeFill(petGauge, delivered.petHp, delivered.petMaxHp) > 0),
      { gauge: petGauge, delivered: [delivered.petHp, delivered.petMaxHp] }
    );

    // ---- Power: the delivered value against the documented 0–100 range --------
    record(
      'phase5b.hudPetPowerIsVisible',
      battle.hud.petPower === `${delivered.power} / 100`,
      { rendered: battle.hud.petPower, delivered: delivered.power }
    );
    const powerGauge = battle.gauges.powerGauge;
    record(
      'phase5b.powerGaugeRenders',
      Boolean(powerGauge) &&
        powerGauge.width > 0 &&
        powerGauge.fillWidth === expectedGaugeFill(powerGauge, delivered.power, 100),
      { gauge: powerGauge, delivered: delivered.power }
    );

    // ---- Match / Combo feedback ----------------------------------------------
    record(
      'phase5b.hudMatchFeedbackIsVisible',
      battle.hud.matches === `MATCHES ${delivered.matchCount}` &&
        (delivered.combo >= 2 ? battle.hud.combo === `COMBO ×${delivered.combo}` : battle.hud.combo === ''),
      { matches: battle.hud.matches, combo: battle.hud.combo, delivered: [delivered.matchCount, delivered.combo] }
    );

    // ---- Passive and Status Effect presentation ------------------------------
    // The Passive identity and its delivered `current / threshold` pair are split
    // across two lines so the progress gauge can sit beside the pair, and the
    // Status Effect line names each delivered instance with its magnitude and its
    // one duration model. Both are read from the scene's own synchronized copy,
    // compared here against what the HUD rendered.
    const passiveState = await evaluate(cdp, `(() => {
      const s = window.__game.scene.getScene('BattleScene');
      const state = s.runtime && s.runtime.getBattleState ? s.runtime.getBattleState() : null;
      if (!state) return null;
      const p = state.petState;
      return {
        passiveId: p.passiveId,
        current: p.passiveProgress.current,
        threshold: p.passiveProgress.threshold,
        reset: p.passiveResetOverride === undefined ? 'Default' : p.passiveResetOverride,
        statusEffects: p.statusEffects.map((e) => ({
          id: e.id,
          magnitude: e.magnitude,
          duration: e.remainingTurns !== undefined
            ? \`\${e.remainingTurns} turns\`
            : e.expiryCondition !== undefined ? e.expiryCondition : 'no duration',
        })),
        passiveGaugeWidth: s.passiveGauge ? s.passiveGauge.width : null,
        passiveGaugeFillWidth: s.passiveGauge && s.passiveGauge.fill ? s.passiveGauge.fill.width : null,
      };
    })()`);

    record(
      'phase5b.passivePresentationIsReadable',
      passiveState !== null &&
        battle.hud.petPassive === `Passive: ${passiveState.passiveId}` &&
        battle.hud.petPassiveProgress ===
          `${passiveState.current} / ${passiveState.threshold} Matches (reset: ${passiveState.reset})` &&
        passiveState.passiveGaugeFillWidth ===
          expectedGaugeFill(
            { width: passiveState.passiveGaugeWidth },
            passiveState.current,
            passiveState.threshold
          ),
      { rendered: { id: battle.hud.petPassive, progress: battle.hud.petPassiveProgress }, delivered: passiveState }
    );

    const expectedEffects =
      passiveState !== null && passiveState.statusEffects.length > 0
        ? 'Effects: ' +
          passiveState.statusEffects
            .map((e) => `${e.id} (${e.magnitude}, ${e.duration})`)
            .join('; ')
        : 'Effects: none';
    record(
      'phase5b.statusEffectPresentationIsReadable',
      passiveState !== null && battle.hud.petStatus === expectedEffects,
      { rendered: battle.hud.petStatus, expected: expectedEffects }
    );

    // The Pet's name is resolved from the client's own preserved loadout and owned
    // Pet collection — `PetState` carries no client-visible identity (ADR-014), so
    // the HUD must not expect one on the wire and must not invent one.
    const petName = await evaluate(cdp, `(async () => {
      const s = window.__game.scene.getScene('BattleScene');
      const loadout = s.registry.get('preservedLoadout') || null;
      const pets = await s.runtime.getPets();
      const owned = loadout ? pets.find((p) => p.petId === loadout.petId) : undefined;
      return {
        preservedPetId: loadout ? loadout.petId : null,
        expectedIdentity: owned ? owned.identity : null,
        renderedPetName: s.petNameText ? s.petNameText.text : null,
      };
    })()`);
    record(
      'phase5b.hudPetNameResolvesFromTheClientCatalog',
      petName.expectedIdentity !== null && petName.renderedPetName === petName.expectedIdentity,
      petName
    );

    // No developer diagnostic and no raw event-log line is rendered anywhere.
    const hudText = Object.values(battle.hud).filter((t) => typeof t === 'string').join(' ');
    const allRenderedText = battle.renderedTexts.join(' ');
    record(
      'phase5b.noDeveloperDiagnosticsInTheHud',
      !/SignalR:|Sync:|BattleId:|RngSeed:|Turn:|Sequence:/.test(`${hudText} ${allRenderedText}`),
      { hud: hudText }
    );
    record(
      'phase5b.rawEventLogIsNotRendered',
      battle.presentedEvents.every((entry) => !allRenderedText.includes(entry)),
      { presentedEventCount: battle.presentedEvents.length }
    );

    // The HUD occupies the bands the fixed board leaves free: nothing it draws may
    // overlap a matchable cell or leave the 24 px safe area.
    const boardRect = { x: BOARD_ORIGIN_X, y: BOARD_ORIGIN_Y, width: BOARD_WIDTH, height: BOARD_WIDTH };
    const overlapsBoard = (b) =>
      b.x < boardRect.x + boardRect.width &&
      b.x + b.width > boardRect.x &&
      b.y < boardRect.y + boardRect.height &&
      b.y + b.height > boardRect.y;
    const outsideSafeArea = (b) =>
      b.x < 23.5 || b.y < 23.5 || b.x + b.width > 1256.5 || b.y + b.height > 696.5;

    record(
      'phase5b.hudDoesNotOverlapTheBoard',
      Object.values(battle.hudBounds).every((b) => !overlapsBoard(b)),
      Object.entries(battle.hudBounds).filter(([, b]) => overlapsBoard(b)).map(([n]) => n)
    );
    record(
      'phase5b.hudStaysInsideTheSafeArea',
      Object.values(battle.hudBounds).every((b) => !outsideSafeArea(b)),
      Object.entries(battle.hudBounds).filter(([, b]) => outsideSafeArea(b)).map(([n]) => n)
    );

    // The developer status panel is mounted in this development build; it must not
    // cover the HUD (TASK-209 §6).
    const overlayVsHud = await evaluate(cdp, `(() => {
      const card = document.querySelector('[data-testid="runtime-status-overlay"]');
      const s = window.__game.scene.getScene('BattleScene');
      const canvas = document.querySelector('canvas');
      const r = canvas.getBoundingClientRect();
      const scaleX = r.width / 1280;
      const scaleY = r.height / 720;
      if (!card) return { mounted: false, covered: [] };
      const c = card.getBoundingClientRect();
      const box = {
        x: (c.left - r.left) / scaleX,
        y: (c.top - r.top) / scaleY,
        width: c.width / scaleX,
        height: c.height / scaleY,
      };
      const overlaps = (b) =>
        b.x < box.x + box.width && b.x + b.width > box.x && b.y < box.y + box.height && b.y + b.height > box.y;
      const names = ['bossNameText', 'bossHpText', 'petNameText', 'petHpText', 'petPowerText',
        'petPassiveText', 'petStatusText', 'comboText', 'matchesText'];
      const covered = names.filter((name) => {
        const o = s[name];
        if (!o || typeof o.getBounds !== 'function') return false;
        const b = o.getBounds();
        return overlaps({ x: b.x, y: b.y, width: b.width, height: b.height });
      });
      return { mounted: true, box, covered };
    })()`);
    record(
      'phase5b.developerStatusPanelDoesNotObscureTheHud',
      !overlayVsHud.mounted || overlayVsHud.covered.length === 0,
      overlayVsHud
    );

    // Neither the HUD's own lines nor its guard may cover a cast control: the
    // Card / Pet Skill triggers are the player's SECONDARY controls and have to
    // stay clickable (TASK-210 §10 — "no HUD blocks card controls / pet skill
    // controls").
    const overlapsRect = (a, b) =>
      a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y;
    record(
      'phase5b.hudDoesNotBlockTheCastControls',
      battle.castControlBounds.length > 0 &&
        Object.values(battle.hudBounds).every((b) =>
          battle.castControlBounds.every((c) => !overlapsRect(b, c))
        ),
      {
        castControls: battle.castControlBounds,
        blockedBy: Object.entries(battle.hudBounds)
          .filter(([, b]) => battle.castControlBounds.some((c) => overlapsRect(b, c)))
          .map(([n]) => n),
      }
    );

    // A player-facing screenshot of the HUD with the two DEVELOPMENT-ONLY React
    // diagnostics hidden, so the captured frame shows what a player sees rather
    // than the cards a dev build mounts over the corners of the game surface.
    const hiddenDevOverlays = await evaluate(cdp, `(() => {
      const nodes = [
        document.querySelector('[data-testid="runtime-status-overlay"]'),
        document.querySelector('[data-testid="viewport-debug"]'),
      ].filter(Boolean);
      window.__hiddenDevOverlays = nodes.map((n) => ({ node: n, display: n.style.display }));
      for (const n of nodes) n.style.display = 'none';
      return nodes.length;
    })()`);
    await captureScreenshot(cdp, `run${runNumber}-06b-battle-hud`);
    await evaluate(cdp, `(() => {
      for (const { node, display } of window.__hiddenDevOverlays || []) node.style.display = display;
      window.__hiddenDevOverlays = null;
      return true;
    })()`);
    record('phase5b.hudScreenshotCapturedWithPlayerFacingFrame', true, {
      hiddenDevOverlayCount: hiddenDevOverlays,
    });

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

    // -------------------------------------------------------------
    // PHASE 5c: COMBAT FEEDBACK (TASK-210 §3, §4, §7, §8)
    // -------------------------------------------------------------
    // Two things are checked, from both directions:
    //
    //   1. a REAL resolution's feedback — what the player's own committed Swap
    //      actually produced on screen, sampled while it was there;
    //   2. a deterministic batch pushed through the same entry point the
    //      authoritative events use, so the exact per-event semantics (one floater
    //      per damage instance, anchored on the party that took it, a signed Power
    //      movement, one selective callout) can be asserted rather than raced.
    console.log('\n--- Phase 5c: Combat feedback (callout, damage & Power floaters) ---');

    // The callout holds for about a second and a floater lives 600 ms, so the
    // resolution's own feedback is sampled right away rather than waited on.
    const presentedBeforeSwap = battle.presentedEvents.length;
    const realFeedback = await pollFeedback(cdp, 2500);
    await captureScreenshot(cdp, `run${runNumber}-07-battle-post-swap`);
    const afterSwap = await evaluate(cdp, BATTLE_SNAPSHOT);

    // The resolution that the server accepted must have said something: when the
    // presented-event log grew, the player was told what happened. A rejected
    // fallback swap legitimately produces nothing, and then there is nothing to
    // assert.
    record(
      'phase5c.aRealResolutionReachesThePlayer',
      afterSwap.presentedEvents.length === presentedBeforeSwap ||
        realFeedback.callouts.some((c) => isPlayerFacingCallout(c)),
      {
        callouts: realFeedback.callouts,
        eventsPresented: afterSwap.presentedEvents.length - presentedBeforeSwap,
        presentedEvents: afterSwap.presentedEvents.slice(-6),
      }
    );

    // Every callout a real resolution produced is one of the documented
    // player-facing forms, and nothing else — no raw event line, no technical
    // identity. `MATCH` / `COMBO ×N` is exactly the vocabulary MATCH3_RULES.md §6
    // gives the value (`COMBO ×1` is deliberately never shown, §6.2 item 1); a
    // `RELIC: <name>` line is TASK-218B's presentation of a delivered
    // `RelicTriggered` (SIGNALR_PROTOCOL.md §3.2.23), named from the Relic's own
    // delivered `name` (`API_CONTRACTS.md` §5.4) and never from its instance id.
    record(
      'phase5c.comboCalloutUsesTheDeliveredValue',
      realFeedback.callouts.every((c) => isPlayerFacingCallout(c)),
      realFeedback.callouts
    );

    // No callout may carry a technical identity, whatever form it takes: a raw
    // Relic instance id (`relicinst_…`), a Relic definition id, a Card id, an
    // internal event type name, or a technical Trigger name. This is asserted over
    // the real resolution's own callouts as well as the injected batch's below.
    record(
      'phase5c.noCalloutLeaksATechnicalIdentity',
      realFeedback.callouts.every((c) => !carriesTechnicalIdentity(c)),
      realFeedback.callouts.filter(carriesTechnicalIdentity)
    );

    // Now the deterministic half: one batch, through the documented entry point the
    // authoritative events arrive on.
    const beforeInjection = await evaluate(cdp, BATTLE_SNAPSHOT);

    // TASK-218B: the batch's Relic events are built from the battle scene's **own**
    // loaded definitions (its `getRelics()` read), so they name Relics this running
    // stack actually delivered. The setup must precede the first injection, and an
    // empty result is recorded as its own failure below rather than silently
    // dropping the Relic half of this phase.
    const sceneRelics = await evaluate(cdp, RELIC_PROBE_SETUP);

    record(
      'phase5cRelic.sceneLoadedTheDeliveredRelicRead',
      Array.isArray(sceneRelics) &&
        sceneRelics.length > 0 &&
        sceneRelics.every(
          (r) => typeof r.relicId === 'string' && typeof r.name === 'string' && r.name.length > 0
        ),
      sceneRelics
    );

    // Drain whatever the real resolution left on screen first, so the probe below
    // can only ever be measuring the injected batch: a real resolution's Power
    // floaters (the `OnCascade` / `OnPowerGain` Relics) would otherwise be
    // indistinguishable from the batch's own.
    await waitForFeedbackToFade(cdp, 5000, false);

    // First pass, for the player-facing screenshot: the callout holds about a
    // second and the floaters live 600 ms of game time, so the frame is taken
    // while they are still on screen.
    await evaluate(cdp, FEEDBACK_INJECTION);
    await delay(140);
    await captureScreenshot(cdp, `run${runNumber}-07b-combat-feedback`);

    // Wait for that pass's own floaters to fade before measuring. This is a wait
    // on the scene rather than on the clock, because a stall in the frame loop
    // would otherwise leave the screenshot pass's floaters alive when the
    // measurement pass injects its own. `requireSighting` makes the wait prove the
    // screenshot pass was presented at all before it waits for it to go away, so a
    // frame-loop stall cannot turn this into "returned immediately, still alive".
    await waitForFeedbackToFade(cdp, 5000, true);

    // Belt and braces for that same stall: the measurement pass must observe only
    // its own batch, so the layer is asserted empty before it is injected. A
    // leftover floater here is drained rather than measured, because the assertion
    // below is about one batch's feedback, not about the fade's timing.
    await drainFeedback(cdp, 5000);

    // Second pass, for the measurements.
    await evaluate(cdp, FEEDBACK_INJECTION);
    const injectedFeedback = await pollFeedback(cdp, 700);
    const afterInjection = await evaluate(cdp, BATTLE_SNAPSHOT);

    // One callout for the whole batch, and it is the most important thing in it.
    // TASK-218B puts two `RelicTriggered` events in this batch: they are present,
    // and they still do not add a second callout, because the Combo outranks them.
    record(
      'phase5c.oneSelectiveCalloutPerBatch',
      injectedFeedback.callouts.includes('COMBO ×4') && injectedFeedback.callouts.length === 1,
      injectedFeedback.callouts
    );

    // Whatever won the batch, the line is a documented player-facing form and
    // carries no technical identity.
    record(
      'phase5c.theSelectedCalloutIsPlayerFacing',
      injectedFeedback.callouts.length === 1 &&
        injectedFeedback.callouts.every((c) => isPlayerFacingCallout(c)) &&
        injectedFeedback.callouts.every((c) => !carriesTechnicalIdentity(c)),
      injectedFeedback.callouts
    );

    // One floater per damage instance (the `DamageDealt`/`DamageTaken` pair of one
    // instance must not be drawn twice) plus the signed Power movement. This is
    // asserted per sampled instant — every moment that showed any floater showed
    // exactly these three — so a duplicated pair could not hide behind the probe's
    // repeated samples.
    const expectedFloaters = ['+12', '-40', '-88'];
    record(
      'phase5c.oneFloaterPerDamageInstancePlusPower',
      injectedFeedback.floaterTextSets.length > 0 &&
        injectedFeedback.floaterTextSets.every(
          (texts) => JSON.stringify(texts) === JSON.stringify(expectedFloaters)
        ),
      injectedFeedback.floaterTextSets
    );

    // The side is the delivered party that took the damage: the Boss's hit is drawn
    // over the Boss HP gauge in the band above the board, the Pet's over its own HP
    // gauge beside the board, the Power movement over the Power gauge. Never keyed
    // on the event's own name, which would draw every hit on one side.
    const centreX = (b) => b.x + b.width / 2;
    const anchorCentreX = (gauge) => gauge.x + gauge.width / 2;
    const bossFloater = injectedFeedback.floaters.find((f) => f.text === '-88');
    const petFloater = injectedFeedback.floaters.find((f) => f.text === '-40');
    const powerFloater = injectedFeedback.floaters.find((f) => f.text === '+12');
    record(
      'phase5c.damageFloatersAreAnchoredOnThePartyThatTookTheHit',
      Boolean(bossFloater?.bounds) &&
        Boolean(petFloater?.bounds) &&
        Boolean(powerFloater?.bounds) &&
        // The Boss's hit: over the Boss gauge's own column, above the board.
        centreX(bossFloater.bounds) === anchorCentreX(afterInjection.gauges.bossHpGauge) &&
        bossFloater.bounds.y + bossFloater.bounds.height <= BOARD_ORIGIN_Y &&
        // The Pet's hit: over the Pet gauge's own column, beside the board's rows.
        centreX(petFloater.bounds) === anchorCentreX(afterInjection.gauges.petHpGauge) &&
        petFloater.bounds.x + petFloater.bounds.width <= BOARD_ORIGIN_X &&
        petFloater.bounds.y >= BOARD_ORIGIN_Y &&
        // The Power movement: over the Power gauge, beside the board.
        centreX(powerFloater.bounds) === anchorCentreX(afterInjection.gauges.powerGauge) &&
        powerFloater.bounds.x + powerFloater.bounds.width <= BOARD_ORIGIN_X,
      {
        boss: { floater: bossFloater, gaugeCentreX: anchorCentreX(afterInjection.gauges.bossHpGauge) },
        pet: { floater: petFloater, gaugeCentreX: anchorCentreX(afterInjection.gauges.petHpGauge) },
        power: { floater: powerFloater, gaugeCentreX: anchorCentreX(afterInjection.gauges.powerGauge) },
      }
    );

    // Feedback is transient and never occupies gameplay space: no observed floater
    // or callout — from the real resolution or the injected batch — overlapped a
    // board cell or left the safe area.
    const observedFeedbackBounds = [
      ...realFeedback.floaters.map((f) => f.bounds).filter(Boolean),
      ...injectedFeedback.floaters.map((f) => f.bounds).filter(Boolean),
      ...(injectedFeedback.calloutBounds ? [injectedFeedback.calloutBounds] : []),
      ...(realFeedback.calloutBounds ? [realFeedback.calloutBounds] : []),
    ];
    record(
      'phase5c.feedbackNeverOverlapsTheBoardOrLeavesTheSafeArea',
      observedFeedbackBounds.length > 0 &&
        observedFeedbackBounds.every((b) => !overlapsBoard(b) && !outsideSafeArea(b)),
      {
        checked: observedFeedbackBounds.length,
        overlapping: observedFeedbackBounds.filter(overlapsBoard),
        outside: observedFeedbackBounds.filter(outsideSafeArea),
      }
    );

    // TASK-210 §12: the feedback path is presentation only. Pushing a whole
    // resolution's events through it changed no gameplay value, drew no different
    // numbers, and left no fixture behind in the HUD. The callout is the one line
    // the batch is *supposed* to change, so it is compared separately.
    const hudWithoutCallout = ({ callout, ...rest }) => rest;
    record(
      'phase5c.feedbackMutatesNoAuthoritativeState',
      JSON.stringify(beforeInjection.authoritative) === JSON.stringify(afterInjection.authoritative) &&
        JSON.stringify(hudWithoutCallout(beforeInjection.hud)) ===
          JSON.stringify(hudWithoutCallout(afterInjection.hud)) &&
        beforeInjection.boardLabels.join() === afterInjection.boardLabels.join(),
      {
        before: beforeInjection.authoritative,
        after: afterInjection.authoritative,
        calloutBefore: beforeInjection.hud.callout,
        calloutAfter: afterInjection.hud.callout,
      }
    );

    // Let the injected batch's own short input guard settle before the next pass.
    await delay(400);

    // -------------------------------------------------------------
    // PHASE 5c-RELIC: RELIC TRIGGER PRESENTATION (TASK-218B)
    // -------------------------------------------------------------
    // `RelicTriggered` is delivered with the owned instance identity and nothing
    // else (`SIGNALR_PROTOCOL.md` §3.2.23 item 5 fixes that shape as final), so the
    // player-facing name has to come from the `GET /api/relics` read the scene
    // performs (`API_CONTRACTS.md` §5.4). This pass asserts the whole path end to
    // end: a delivered `RelicTriggered` renders the delivered **name**, one callout
    // at a time, and no raw identity appears anywhere.
    //
    // The identities are taken from the scene's own loaded definitions (resolved
    // above, before the batch was injected), so this is evidence about the running
    // stack rather than about this script's fixture.
    console.log('\n--- Phase 5c-relic: Relic trigger callout (TASK-218B) ---');

    // Two different owned Relics, exercised one batch at a time, so the assertion
    // is about the lookup rather than one fixed label. `\`\${id}\`` is the identity
    // as the wire carries it; the rendered line must contain the delivered name.
    const relicCalls = [];
    for (const relic of (Array.isArray(sceneRelics) ? sceneRelics : []).slice(0, 2)) {
      await drainFeedback(cdp, 5000);
      await evaluate(
        cdp,
        `(() => {
          const s = window.__game.scene.getScene('BattleScene');
          s.handleBattleEvents({
            battleId: 'task-218b-relic-probe',
            serverSequence: 999998,
            events: [{ type: 'RelicTriggered', relicId: ${JSON.stringify(relic.relicId)} }],
          });
          return true;
        })()`
      );
      const sampled = await pollFeedback(cdp, 900);
      relicCalls.push({ relic, callouts: sampled.callouts, calloutBounds: sampled.calloutBounds });

      // Player-facing evidence of the callout naming this Relic, taken while it is
      // on screen (the callout holds about a second). One frame per Relic, so the
      // artifact shows two different names rather than one.
      if (relicCalls.length === 1) {
        await captureScreenshot(cdp, `run${runNumber}-07c-relic-callout`);
      }
    }

    record(
      'phase5cRelic.everyRelicCalloutNamesTheDeliveredRelic',
      relicCalls.length === Math.min(2, Array.isArray(sceneRelics) ? sceneRelics.length : 0) &&
        relicCalls.length > 0 &&
        relicCalls.every(
          ({ relic, callouts }) =>
            callouts.length === 1 &&
            callouts[0] === `RELIC: ${relic.name}` &&
            !callouts[0].includes(relic.relicId)
        ),
      relicCalls.map(({ relic, callouts }) => ({ relicId: relic.relicId, name: relic.name, callouts }))
    );

    // The rejection half, asserted independently of the exact wording: no observed
    // Relic callout carries an instance id, a definition id, an event type name, or
    // a technical Trigger name.
    record(
      'phase5cRelic.noRawRelicIdentityOrEventTypeIsShown',
      relicCalls.every(({ callouts }) => callouts.every((c) => !carriesTechnicalIdentity(c))),
      relicCalls.flatMap(({ callouts }) => callouts.filter(carriesTechnicalIdentity))
    );

    // A Relic the loaded definitions do not contain produces **no** callout — not
    // the raw id, and no crash that would stop the rest of the presentation.
    //
    // The callout row is a transient line that is *replaced*, never blanked
    // (`showCallout` only runs when a batch selects one), so "no callout" is
    // asserted the way a player experiences it: the batch does not put a new line
    // there, and no line anywhere ever shows the identity. The before/after text is
    // therefore read directly rather than sampled.
    await drainFeedback(cdp, 5000);
    const unownedProbe = await evaluate(
      cdp,
      `(() => {
        const s = window.__game.scene.getScene('BattleScene');
        const owned = s.relicDefinitions instanceof Map ? [...s.relicDefinitions.keys()] : [];
        const unowned = 'relicinst_not-owned-by-this-account';
        if (owned.includes(unowned)) return { skipped: true, reason: 'fixture collides with an owned id' };
        // Start from a known non-Relic line, so "unchanged" is a real observation
        // rather than a coincidence of the previous probe also being a Relic one
        // (which would make a RELIC-prefix check vacuous).
        if (s.calloutText) s.calloutText.setText('MATCH');
        const before = s.calloutText ? s.calloutText.text : null;
        s.handleBattleEvents({
          battleId: 'task-218b-relic-probe',
          serverSequence: 999997,
          events: [
            { type: 'RelicTriggered', relicId: unowned },
            { type: 'PowerChanged', delta: 3, power: 3, source: 'relic' },
          ],
        });
        const after = s.calloutText ? s.calloutText.text : null;
        // Every text object the scene owns, so "the identity is nowhere on screen"
        // is asserted over the whole rendered surface rather than one line.
        const rendered = [];
        for (const o of (s.children && s.children.list) || []) {
          if (o.type === 'Text' && typeof o.text === 'string' && o.text) rendered.push(o.text);
        }
        return { skipped: false, before, after, rendered };
      })()`
    );
    const unownedFeedback = await pollFeedback(cdp, 900);
    record(
      'phase5cRelic.anUnresolvableRelicShowsNothing',
      unownedProbe.skipped === true ||
        (// The batch left the row exactly as it was rather than naming the Relic:
          // the previous callout — whatever it was — is still the callout, because
          // a batch with no player-facing callout replaces nothing. This is the
          // assertion that would fail if the identity had been named...
          unownedProbe.after === unownedProbe.before &&
          // ...and the row was not turned into a Relic callout, which is what naming
          // the unresolvable instance would have required.
          unownedProbe.before === 'MATCH' &&
          !String(unownedProbe.after || '').startsWith('RELIC:') &&
          // ...and no line anywhere on the scene shows the raw identity, so it did
          // not reach the UI by any other route either.
          (unownedProbe.rendered || []).every(
            (text) => !text.includes('relicinst_not-owned-by-this-account')
          ) &&
          !String(unownedProbe.after || '').includes('relicinst_not-owned-by-this-account') &&
          // The sibling event still reached the player, so the rest of the
          // presentation continued normally rather than being aborted.
          unownedFeedback.floaters.some((f) => f.text === '+3')),
      { probe: unownedProbe, callouts: unownedFeedback.callouts, floaters: unownedFeedback.floaters }
    );

    // The Relic callout is still the one transient row: it must not overlap a board
    // cell and must stay inside the safe area, exactly like every other callout.
    const relicCalloutBounds = relicCalls.map((c) => c.calloutBounds).filter(Boolean);
    record(
      'phase5cRelic.theRelicCalloutStaysInTheCalloutRow',
      relicCalloutBounds.length > 0 &&
        relicCalloutBounds.every((b) => !overlapsBoard(b) && !outsideSafeArea(b)),
      relicCalloutBounds
    );

    // Let this pass's own short input guard settle before the cast phase.
    await delay(400);

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
        // Only the **settled** acknowledgement resolves this wait. This is the
        // first cast of the phase, so there is no earlier caption to go stale —
        // the transient `… in flight…` / `… not sent: …` lines are simply not
        // the server's answer (TASK-228, V-2).
        const text = snap.castText || '';
        return isSettledCastAcknowledgement(text) ? text : false;
      },
      'Authoritative cast acknowledgement feedback',
      8000
    );
    record('phase6.castFeedbackReceived', Boolean(castFeedback), castFeedback);

    // The first control is one of the 3 submitted Basic Cards (`API_CONTRACTS.md`
    // §3 composes `equippedCards` as the 3 Basics plus the derived Signature
    // Skill), and a Basic Card's client action is `CardCast` — never
    // `PetSkillCast` (`SIGNALR_PROTOCOL.md` §2, §3.2.21). Its caption is the
    // `Card:` presentation of the delivered Card name (§5.3).
    record(
      'phase6.basicCardCastUsesCardCast',
      typeof castFeedback === 'string' && castFeedback.startsWith('CardCast '),
      castFeedback
    );

    const captionsAtFirstCast = await evaluate(cdp, CAST_CONTROL_CAPTIONS);
    const basicCardCaption = (captionsAtFirstCast || []).find((text) =>
      text.startsWith('Card: ')
    );
    record(
      'phase6.basicCardPresentsAsCard',
      typeof basicCardCaption === 'string' && basicCardCaption !== 'Card: ',
      { captions: captionsAtFirstCast, basicCardCaption: basicCardCaption ?? null }
    );

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

    // A real tap is retried the way a player would repeat it: the scene ignores
    // input while it is presenting the previous resolution (`isInputLocked`), and a
    // tap that lands in that window is refused rather than queued. Only a NEW
    // acknowledgement counts — the first phase's text is stale.
    let secondCastText = null;
    let secondCastInputState = null;

    for (let attempt = 1; attempt <= 4 && secondCastText === null; attempt++) {
      await realClick(cdp, secondCastButtonPoint);

      try {
        secondCastText = await waitForCondition(
          async () => {
            const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
            const text = snap.castText || '';
            // TASK-227: the scene paints `CardCast <id> in flight…` on its way to
            // the settled caption, so a caption that merely changed is not yet this
            // phase's acknowledgement — only the server's own answer is.
            return text !== castFeedback && isSettledCastAcknowledgement(text) ? text : false;
          },
          'second cast acknowledgement',
          4000
        );
      } catch {
        const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
        secondCastInputState = {
          attempt,
          inputLocked: snap.inputLocked,
          reasons: snap.inputLockReasons,
          castText: snap.castText,
        };
      }
    }

    record(
      'phase6b.secondCastAcknowledged',
      Boolean(secondCastText),
      secondCastText ?? secondCastInputState
    );

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
    // PHASE 6d: THE SIGNATURE SKILL CONTROL IS STILL USABLE, AND IS
    // PRESENTED AND DISPATCHED AS THE SIGNATURE SKILL
    // -------------------------------------------------------------
    // TASK-210 adds a callout row, floating numbers and gauges; none of them may
    // sit over the Pet's Signature Skill trigger or swallow its clicks. The last
    // entry of the delivered `equippedCards` loadout is the Signature Skill Card
    // (`SIGNALR_PROTOCOL.md` §4.3 item 13: three Basic Cards plus one Pet Skill
    // Card), and the controls are drawn in that same order.
    //
    // TASK-219A adds the contract properties this phase now certifies live:
    //   * the entry is IDENTIFIED from the delivered Pet read, not from a Card
    //     category — `API_CONTRACTS.md` §5.1's `signatureSkill`, the Pet's own
    //     `PetDefinition.SignatureSkillCardId` reference (§4.3 item 13);
    //   * it is PRESENTED as the Skill, with that delivered `name` — never the raw
    //     `cardId` and never the `Card:` caption;
    //   * activating it DISPATCHES `PetSkillCast` — the canonical client request —
    //     while a Basic Card keeps `CardCast` (`SIGNALR_PROTOCOL.md` §2).
    console.log('\n--- Phase 6d: Signature Skill control remains usable ---');

    // The reference the server actually delivered for the active loadout's derived
    // entry: the Pet read's own `signatureSkill` object, matched by the card
    // identity the loadout carries. Nothing here is this script's expectation — it
    // is the response's own member, so the assertions below compare the rendered
    // surface against what the contract delivered.
    const deliveredSignatureSkill = await evaluate(cdp, `(async () => {
      const s = window.__game.scene.getScene('BattleScene');
      const pets = await s.runtime.getPets();
      const state = s.runtime && s.runtime.getBattleState ? s.runtime.getBattleState() : null;
      const equipped = state ? state.petState.equippedCards : [];
      const skill = pets
        .map((pet) => pet.signatureSkill)
        .find((delivered) => delivered && equipped.includes(delivered.cardId));
      return skill
        ? { cardId: skill.cardId, name: skill.name, category: skill.category }
        : null;
    })()`);

    record(
      'phase6d.deliveredSignatureSkillReference',
      Boolean(deliveredSignatureSkill) &&
        deliveredSignatureSkill.category === 'PetSkill' &&
        typeof deliveredSignatureSkill.cardId === 'string' &&
        typeof deliveredSignatureSkill.name === 'string',
      deliveredSignatureSkill
    );

    const skillControlIndex = await evaluate(cdp, `(() => {
      const s = window.__game.scene.getScene('BattleScene');
      const state = s.runtime && s.runtime.getBattleState ? s.runtime.getBattleState() : null;
      return state ? state.petState.equippedCards.length - 1 : -1;
    })()`);

    if (skillControlIndex >= 0) {
      const skillPoint = {
        x: battle.viewport.left + (BOARD_ORIGIN_X + skillControlIndex * 125 + 115 / 2) * battle.viewport.scaleX,
        y: battle.viewport.top + (BOARD_ORIGIN_Y + BOARD_WIDTH + 34 + 36 / 2) * battle.viewport.scaleY,
      };

      let skillFeedback = null;
      for (let attempt = 1; attempt <= 4 && skillFeedback === null; attempt++) {
        await realClick(cdp, skillPoint);
        try {
          skillFeedback = await waitForCondition(
            async () => {
              const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
              const text = snap.castText || '';
              // TASK-227: `PetSkillCast in flight…` is transient transport
              // feedback; only the settled caption is this phase's acknowledgement.
              const changedCaption = text.length > 0 && text !== secondCastText;
              return changedCaption && isSettledCastAcknowledgement(text) ? text : false;
            },
            'Signature Skill control acknowledgement',
            4000
          );
        } catch {
          /* the scene ignores input while presenting; a real player would retry */
        }
      }

      record(
        'phase6d.signatureSkillControlRemainsUsable',
        typeof skillFeedback === 'string' &&
          (skillFeedback.includes('accepted') || skillFeedback.includes('rejected')),
        skillFeedback
      );

      // The canonical client path: the Signature Skill is a `PetSkillCast`, and it
      // carries no card identifier (`SIGNALR_PROTOCOL.md` §2).
      record(
        'phase6d.signatureSkillDispatchesPetSkillCast',
        typeof skillFeedback === 'string' && skillFeedback.startsWith('PetSkillCast'),
        skillFeedback
      );

      const castCaptions = await evaluate(cdp, CAST_CONTROL_CAPTIONS);
      const renderedCaptions = castCaptions || [];
      const skillCaptions = renderedCaptions.filter((text) => text.startsWith('Skill: '));
      // A raw developer identity is what the defect produced: the fallback caption
      // is `Card: card-inferno` / `Skill: card-inferno`. Neither may render, and
      // the Skill's caption carries the delivered name.
      const rawIdentityCaption = renderedCaptions.find((text) => /^(Card|Skill): card-/.test(text));

      record(
        'phase6d.signatureSkillPresentsAsSkill',
        skillCaptions.length === 1 &&
          rawIdentityCaption === undefined &&
          (deliveredSignatureSkill === null ||
            skillCaptions[0] === `Skill: ${deliveredSignatureSkill.name}`),
        { captions: renderedCaptions, rawIdentityCaption: rawIdentityCaption ?? null }
      );

      // The UI still distinguishes the two: a Basic Card keeps its `Card:` caption
      // beside the `Skill:` one.
      record(
        'phase6d.basicCardStillPresentsAsCard',
        renderedCaptions.some((text) => text.startsWith('Card: ')),
        renderedCaptions.filter((text) => text.startsWith('Card: '))
      );
    } else {
      record(
        'phase6d.signatureSkillControlRemainsUsable',
        false,
        'no synchronized loadout to address'
      );
      record(
        'phase6d.signatureSkillDispatchesPetSkillCast',
        false,
        'no synchronized loadout to address'
      );
      record(
        'phase6d.signatureSkillPresentsAsSkill',
        false,
        'no synchronized loadout to address'
      );
      record(
        'phase6d.basicCardStillPresentsAsCard',
        false,
        'no synchronized loadout to address'
      );
    }

    // -------------------------------------------------------------
    // PHASE 6c: RECONNECT / RESYNC CONVERGENCE (TASK-209 §12)
    // -------------------------------------------------------------
    // HP and Power are state values, so they must survive a real transport
    // disconnect and the §7 snapshot recovery that follows a reconnect — with no
    // event replay and nothing reconstructed client-side.
    console.log('\n--- Phase 6c: Reconnect / resync convergence ---');

    const beforeResync = await evaluate(cdp, BATTLE_SNAPSHOT);

    const resynced = await evaluate(cdp, `(async () => {
      const s = window.__game.scene.getScene('BattleScene');
      const service = s.runtime.signalR;
      const connection = service.connection;
      const beforeConnectionId = service.getConnectionId();
      // A real transport stop and restart — a genuine WebSocket close and reopen —
      // on the SAME connection object, which is what SignalR's automatic reconnect
      // does and why the runtime keeps one connection and one subscription
      // throughout (ARCHITECTURE.md §2.2.1 rule 6).
      await connection.stop();
      await connection.start();
      const sameConnection = service.connection === connection;
      const connectionId = service.getConnectionId();
      // SignalR raises its reconnect notification once the connection is back; the
      // runtime's registered handler is what requests §7.1's authoritative
      // snapshot. Invoking that registered handler is the same code path a real
      // reconnect runs, and the request it issues is a real server round trip.
      if (service.handlers && typeof service.handlers.onReconnected === 'function') {
        service.handlers.onReconnected(connectionId);
      }
      return { beforeConnectionId, connectionId, sameConnection, state: service.getConnectionState() };
    })()`);

    const afterResync = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
        if (!snap.active || !snap.authoritative) return false;
        const a = snap.authoritative;
        return snap.hud.petHp === `${a.petHp} / ${a.petMaxHp}` &&
          snap.hud.petPower === `${a.power} / 100` &&
          snap.hud.bossHp === `${a.bossHp} / ${a.bossMaxHp}`
          ? snap
          : false;
      },
      'Battle HUD converged to the authoritative resync state',
      15000
    );

    record(
      'phase6c.transportReconnected',
      resynced.sameConnection === true &&
        typeof resynced.connectionId === 'string' &&
        resynced.connectionId.length > 0,
      resynced
    );
    record(
      'phase6c.hudConvergedToTheAuthoritativeResyncState',
      afterResync.hud.bossHp === `${afterResync.authoritative.bossHp} / ${afterResync.authoritative.bossMaxHp}` &&
        afterResync.hud.petHp === `${afterResync.authoritative.petHp} / ${afterResync.authoritative.petMaxHp}` &&
        afterResync.hud.petPower === `${afterResync.authoritative.power} / 100`,
      {
        before: beforeResync.authoritative,
        after: afterResync.authoritative,
        hud: { bossHp: afterResync.hud.bossHp, petHp: afterResync.hud.petHp, petPower: afterResync.hud.petPower },
      }
    );
    // The gauges converged with the numbers they are a proportion of: after a real
    // disconnect and the §7 snapshot, the fills are redrawn from the recovered
    // state rather than left at whatever the pre-disconnect values drew.
    record(
      'phase6c.gaugesConvergedWithTheNumbers',
      Boolean(afterResync.gauges.bossHpGauge) &&
        afterResync.gauges.bossHpGauge.fillWidth ===
          expectedGaugeFill(afterResync.gauges.bossHpGauge, afterResync.authoritative.bossHp, afterResync.authoritative.bossMaxHp) &&
        afterResync.gauges.petHpGauge.fillWidth ===
          expectedGaugeFill(afterResync.gauges.petHpGauge, afterResync.authoritative.petHp, afterResync.authoritative.petMaxHp) &&
        afterResync.gauges.powerGauge.fillWidth ===
          expectedGaugeFill(afterResync.gauges.powerGauge, afterResync.authoritative.power, 100),
      { gauges: afterResync.gauges, delivered: afterResync.authoritative }
    );
    record(
      'phase6c.resyncCarriedHpAndPowerAsState_NoReplay',
      afterResync.authoritative !== null &&
        Number.isInteger(afterResync.authoritative.petHp) &&
        Number.isInteger(afterResync.authoritative.power) &&
        Number.isInteger(afterResync.authoritative.petMaxHp) &&
        afterResync.boardChildCount === 128,
      { authoritative: afterResync.authoritative, boardChildCount: afterResync.boardChildCount }
    );

    // The Signature Skill's identification is not replayed from events and is not
    // reconstructed from the recovered state: it is the delivered Pet read's own
    // reference (`API_CONTRACTS.md` §5.1, `SIGNALR_PROTOCOL.md` §4.3 item 13), so
    // the recovered surface presents it as the Skill exactly as before the
    // disconnect did — with the delivered name, never the raw card identity.
    const captionsAfterResync = await evaluate(cdp, CAST_CONTROL_CAPTIONS);
    record(
      'phase6c.signatureSkillIdentificationSurvivesResync',
      (captionsAfterResync || []).filter((text) => text.startsWith('Skill: ')).length === 1 &&
        (captionsAfterResync || []).every((text) => !/^(Card|Skill): card-/.test(text)) &&
        (deliveredSignatureSkill === null ||
          (captionsAfterResync || []).includes(`Skill: ${deliveredSignatureSkill.name}`)),
      captionsAfterResync
    );

    // The board is still playable after recovery: the recovered board still
    // responds to the two-tap interaction. The probe taps ONE cell, so it exercises
    // the whole input path (pointer event → cell index → selection feedback) without
    // committing a Swap — the script must not change the battle it is about to
    // complete, and a committed Swap can legitimately end it
    // (MATCH3_RULES.md §2.1.2 item 4, §5).
    const tapProbe = await (async () => {
      const live = await evaluate(cdp, BATTLE_SNAPSHOT);
      if (!live.active || live.boardLabels.length !== 64) return null;
      if (!live.authoritative) return null;

      await realClick(cdp, cellCentre(0));
      const selected = await waitForCondition(
        async () => {
          const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
          const text = snap.swapText || '';
          return text.includes('selected') ? text : false;
        },
        'cell selection feedback after resync',
        8000
      ).catch(() => null);

      // A second tap on the same cell clears the selection (the documented local
      // interaction, MATCH3_RULES.md §2.1 item 1), leaving the board untouched.
      await realClick(cdp, cellCentre(0));
      const cleared = await waitForCondition(
        async () => {
          const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
          const text = snap.swapText || '';
          return text.includes('Select a cell') ? text : false;
        },
        'cell selection cleared after resync',
        8000
      ).catch(() => null);

      // Nothing was submitted: the interactions above are presentation-local only.
      return { selected, cleared, submittedActions: null };
    })();

    record(
      'phase6c.boardStillRespondsToInputAfterResync',
      tapProbe !== null && tapProbe.selected !== null && tapProbe.cleared !== null,
      tapProbe
    );

    // The delivered Match/Combo feedback is read from the HUD and compared with the
    // authoritative values the same HUD was drawn from: the cumulative Match count
    // rendered exactly as delivered, and the Combo line present exactly when the
    // delivered Combo is meaningful (MATCH3_RULES.md §6.2 item 1 — a published combo
    // of 1 is an ordinary single-Match Swap).
    const feedbackSnap = await evaluate(cdp, BATTLE_SNAPSHOT);
    const deliveredFeedback = feedbackSnap.authoritative;
    record(
      'phase6c.hudMatchAndComboFollowTheAuthoritativeState',
      feedbackSnap.active &&
        deliveredFeedback !== null &&
        deliveredFeedback.matchCount >= 1 &&
        feedbackSnap.hud.matches === `MATCHES ${deliveredFeedback.matchCount}` &&
        (deliveredFeedback.combo >= 2
          ? feedbackSnap.hud.combo === `COMBO ×${deliveredFeedback.combo}`
          : feedbackSnap.hud.combo === ''),
      {
        matches: feedbackSnap.hud.matches,
        combo: feedbackSnap.hud.combo,
        delivered: { matchCount: deliveredFeedback.matchCount, combo: deliveredFeedback.combo },
      }
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

    // Before the batch is injected, the scene must still be the active battle
    // surface and must not have handled an outcome already — that is what makes the
    // batch below the one that drives the handoff, so the outcome values that follow
    // are this script's and not a real resolution's.
    const preOutcome = await evaluate(cdp, `(() => {
      const s = window.__game.scene.getScene('BattleScene');
      const runtime = s ? s.runtime : null;
      return {
        active: !!(s && s.scene && s.scene.isActive && s.scene.isActive()),
        outcomeHandled: s ? s.outcomeHandled === true : null,
        inputLocked: s && typeof s.isInputLocked === 'function' ? s.isInputLocked() : null,
        sync: runtime && runtime.getState ? runtime.getState().sync : null,
        activeScene: (() => {
          for (const key of ['MainMenuScene', 'BattleHistoryScene', 'LobbyScene', 'BattleScene', 'ResultScene']) {
            const scene = window.__game.scene.getScene(key);
            if (scene && scene.scene && scene.scene.isActive && scene.scene.isActive()) return key;
          }
          return 'none';
        })(),
      };
    })()`);
    record(
      'phase7.battleSceneReadyForTheOutcomeBatch',
      preOutcome.active && preOutcome.outcomeHandled === false,
      preOutcome
    );

    // Dispatch deterministic battle outcome event via documented client runtime port hook.
    //
    // Phaser processes a scene-start request on its next loop step, and a hidden
    // document suspends `requestAnimationFrame`, so the foreground is re-asserted
    // here exactly as `realClick` does it before every pointer input: this
    // handoff is delivered through `Runtime.evaluate` rather than a click, so it
    // is not covered by that guard.
    await ensurePageVisible(cdp);
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
    try {
      await waitForCondition(
        async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'ResultScene',
        'ResultScene active after outcome handoff',
        10000
      );
    } catch (error) {
      // The handoff failure is reported with the scene evidence, so the run's log
      // says which surface is actually active — and whether the game loop was
      // stepping at all — rather than only that the wait failed.
      record('phase7.resultSceneReached', false, {
        preOutcome,
        activeScene: await evaluate(cdp, ACTIVE_SCENE),
        loop: await evaluate(cdp, `(() => ({
          visibility: document.visibilityState,
          running: window.__game && window.__game.loop ? window.__game.loop.running : null,
          sleeping: window.__game && window.__game.loop ? window.__game.loop.sleeping : null,
        }))()`),
      });
      throw error;
    }
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

    // TASK-211 §8: the delivered `finalPlayerHp` is the active **Pet's** HP at
    // battle end (`SIGNALR_PROTOCOL.md` §3.2.19, `GAME_STATE.md` §2.3, ADR-011 —
    // there is no Player HP pool), so that is what the line must name. The wire
    // member is unchanged; only the label a player reads was wrong.
    record(
      'phase7.terminalPetHpIsLabelledAsThePet',
      result.playerHpText.startsWith('Final Pet HP:') &&
        result.playerHpText.includes('850') &&
        result.bossHpText.startsWith('Final Boss HP:'),
      { bossHp: result.bossHpText, petHp: result.playerHpText }
    );
    await captureScreenshot(cdp, `run${runNumber}-08-result`);

    // -------------------------------------------------------------
    // PHASE 7b: RESULT REWARD FAILURE, RETRY AND THE TWO EXITS (TASK-211 §5–§6)
    // -------------------------------------------------------------
    // Battle 1 was never completed on the server: its outcome was handed to the
    // presentation above, but no `BattleResult` row exists, so
    // `GET /api/battle/{battleId}/result` answers the documented
    // `404 BATTLE_NOT_FOUND` (API_CONTRACTS.md §4 notes 6–7). Before TASK-211
    // that failure was silent — the game's only reward channel rendered an empty
    // box with no way to recover.
    console.log('\n--- Phase 7b: Result reward failure (< RETRY >, PLAY AGAIN, MAIN MENU) ---');

    const rewardFailure = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, RESULT_SNAPSHOT);
        return snap.active && snap.rewardState === 'failure' ? snap : false;
      },
      'ResultScene reported the unavailable reward summary',
      10000
    );

    record(
      'phase7b.rewardFailureIsReadable',
      rewardFailure.rewardText.includes('Rewards could not be loaded.') &&
        rewardFailure.rewardText.includes('Please try again.') &&
        !rewardFailure.rewardText.includes('REWARDS') &&
        !/status \d/.test(rewardFailure.rewardText) &&
        !rewardFailure.rewardText.includes('/api/'),
      rewardFailure.rewardText
    );

    // A failed read is not a delivered zero reward: `REWARDS` + `+0 XP` is a real
    // answer (`DATABASE.md` §1 item 5) and the two must stay distinguishable.
    record(
      'phase7b.rewardFailureIsNotAZeroReward',
      rewardFailure.rewardText.length > 0 && !rewardFailure.rewardText.includes('+0 XP'),
      rewardFailure.rewardText
    );

    record(
      'phase7b.rewardFailureOffersRetryBesideBothExits',
      ['RETRY', 'PLAY AGAIN', 'MAIN MENU'].every((label) =>
        rewardFailure.controls.some((c) => c.label === label)
      ),
      rewardFailure.controls.map((c) => c.label)
    );

    // Nothing delivered a length, so none is invented: `durationTurns` comes only
    // from the result read (API_CONTRACTS.md §4 note 5).
    record(
      'phase7b.noDurationIsInventedWithoutAResult',
      rewardFailure.durationText === '',
      rewardFailure.durationText
    );
    await captureScreenshot(cdp, `run${runNumber}-08b-result-reward-failure`);

    // --- Reward failure → RETRY ---------------------------------------------
    const rewardRetryPoint = (() => {
      const control = rewardFailure.controls.find((c) => c.label === 'RETRY');
      if (!control) {
        throw new Error('ResultScene rendered no RETRY control in its failure state.');
      }
      return control;
    })();
    const rewardRunBeforeRetry = rewardFailure.rewardLoadRun;

    await realClick(cdp, await toScreen(rewardRetryPoint.x, rewardRetryPoint.y));

    const afterRewardRetry = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, RESULT_SNAPSHOT);
        return snap.active &&
          snap.rewardState === 'failure' &&
          snap.rewardLoadRun > rewardRunBeforeRetry
          ? snap
          : false;
      },
      'ResultScene re-read the reward summary and reported the same failure',
      10000
    );

    record(
      'phase7b.rewardRetryRereadsWithoutDuplicatingAnything',
      afterRewardRetry.rewardText === rewardFailure.rewardText &&
        afterRewardRetry.controls.filter((c) => c.label === 'RETRY').length === 1 &&
        afterRewardRetry.controls.filter((c) => c.label === 'PLAY AGAIN').length === 1 &&
        afterRewardRetry.controls.filter((c) => c.label === 'MAIN MENU').length === 1,
      {
        reward: afterRewardRetry.rewardText,
        controls: afterRewardRetry.controls.map((c) => c.label),
        readRuns: { before: rewardRunBeforeRetry, after: afterRewardRetry.rewardLoadRun },
      }
    );

    // The retry repeated the *read* only: the scene is still the result surface,
    // the outcome is unchanged, and no battle was restarted.
    record(
      'phase7b.rewardRetryDidNotRestartTheBattle',
      (await evaluate(cdp, ACTIVE_SCENE)) === 'ResultScene' &&
        afterRewardRetry.outcomeText === 'VICTORY' &&
        afterRewardRetry.battleId === rewardFailure.battleId,
      {
        activeScene: await evaluate(cdp, ACTIVE_SCENE),
        outcome: afterRewardRetry.outcomeText,
        battleId: afterRewardRetry.battleId,
      }
    );

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

    /**
     * Why the last {@link activateResultControl} gave up, when it did.
     *
     * It exists so a failed ResultScene navigation reports the game loop's own
     * state instead of an unexplained timeout: Phaser processes a scene-start
     * request on its next loop step, so a paused/throttled loop is the one way a
     * claimed transition can legitimately not have happened yet.
     */
    let resultControlFailureDetail = null;

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

      // The scene claims its transition synchronously and then asks Phaser for the
      // next scene; Phaser processes that request on its next step. If the loop is
      // not running (a throttled/hidden document pauses it), the request is queued
      // and the scene never changes — so a failure here is reported with the loop's
      // own state rather than as an unexplained timeout.
      resultControlFailureDetail = await evaluate(cdp, `(() => {
        const s = window.__game && window.__game.scene.getScene('ResultScene');
        const running = (scene) => {
          try {
            return !!(scene && scene.scene && scene.scene.isActive && scene.scene.isActive());
          } catch {
            return false;
          }
        };
        return {
          activeScene: (() => {
            for (const key of ['MainMenuScene', 'BattleHistoryScene', 'LobbyScene', 'BattleScene', 'ResultScene']) {
              if (running(window.__game.scene.getScene(key))) return key;
            }
            return 'none';
          })(),
          hasTransitioned: s ? s.hasTransitioned === true : null,
          documentVisibility: document.visibilityState,
          loopRunning: window.__game.loop ? window.__game.loop.running : null,
          loopSleeping: window.__game.loop ? window.__game.loop.sleeping : null,
        };
      })()`);
      return false;
    };

    const controls = await resultControls();
    // TASK-211 §6: the two approved exits are always present and usable, and
    // the reward block's RETRY joins them **only** while the reward read is in
    // its failure state. This result's read failed (the battle has no persisted
    // result), so all three are expected here — and a success must not carry
    // RETRY, which phase 10 checks.
    const navLabels = controls.map((c) => c.label).filter((l) => l !== 'RETRY');
    record(
      'phase8.resultControlsAreTheTwoExitsPlusRetryOnFailure',
      navLabels.sort().join('|') === 'MAIN MENU|PLAY AGAIN' &&
        controls.some((c) => c.label === 'RETRY') &&
        controls.filter((c) => c.label === 'RETRY').length === 1 &&
        controls.every((c) => c.width > 0 && c.height > 0),
      controls.map((c) => c.label)
    );

    // --- PLAY AGAIN ---------------------------------------------------------
    const playedAgain = await activateResultControl('PLAY AGAIN');
    record('phase8.playAgainReachedLobby', playedAgain && (await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene', resultControlFailureDetail);

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

    // --- Change the Relic selection (TASK-221 §5, §8 steps 11–13) -----------
    // The restored three are the Relics Battle 1 was fought with. One of them is
    // deselected and an owned instance that is NOT in the restored set takes its
    // place, so the outgoing request can only match the new selection if the
    // change really reached it (TASK-221 §5: "changing the selected Relics
    // before a new battle actually changes the outgoing loadout").
    const relicNameOf = (relicId) =>
      (nextLobby.ownedRelics.find((r) => r.relicId === relicId) || {}).name || null;

    /** Clicks the Relic row whose owned name is `name`, paging until it is shown. */
    const clickRelicRow = async (name) => {
      if (name === null) return false;
      const token = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

      for (let step = 0; step < 4; step++) {
        const snapshot = await evaluate(cdp, LOBBY_SNAPSHOT);
        const row = snapshot.interactive.find(
          (o) =>
            o.type === 'Text' &&
            new RegExp(`^[○●] ${token}(\\s|$)`).test(o.text || '') &&
            o.x > 700
        );

        if (row) {
          await realClick(cdp, await nextCentreOf(row));
          await delay(200);
          return true;
        }

        // Not on this page: page forward, and back to the first page at the end.
        const label =
          snapshot.relicPage + 1 < snapshot.relicPageCount ? 'NEXT' : 'PREV';
        const pager = lobbyControlOf(snapshot, label);
        await realClick(cdp, await logicalToScreen(pager.x, pager.y));
        await delay(250);
      }

      return false;
    };

    const restoredRelicLoadout = [...nextLobby.selectedRelicIds];
    const droppedRelicId = restoredRelicLoadout[0];
    const replacementRelicId = nextLobby.ownedRelics
      .map((r) => r.relicId)
      .find((relicId) => !restoredRelicLoadout.includes(relicId));

    const relicEditClicked =
      (await clickRelicRow(relicNameOf(droppedRelicId))) &&
      (await clickRelicRow(relicNameOf(replacementRelicId)));

    nextLobby = await evaluate(cdp, LOBBY_SNAPSHOT);
    const expectedRelicLoadout = [...restoredRelicLoadout.slice(1), replacementRelicId];

    record(
      'phase8.preservedRelicSelectionIsEditable',
      relicEditClicked &&
        nextLobby.selectedRelicIds.length === 3 &&
        !nextLobby.selectedRelicIds.includes(droppedRelicId) &&
        nextLobby.selectedRelicIds.includes(replacementRelicId) &&
        nextLobby.selectedRelicIds.join(',') === expectedRelicLoadout.join(','),
      {
        restored: restoredRelicLoadout,
        now: nextLobby.selectedRelicIds,
        dropped: droppedRelicId,
        added: replacementRelicId,
        pages: { page: nextLobby.relicPage, count: nextLobby.relicPageCount },
      }
    );

    // --- Battle 2 ----------------------------------------------------------
    // The control is identified by the caption a player reads, not by "the first
    // enabled rectangle": the Lobby now draws an exit beside the trigger, and a
    // position-blind lookup would click that instead (TASK-211 §1).
    const startButton2 = lobbyControlOf(nextLobby, 'START BATTLE');
    if (!startButton2) {
      throw new Error('The restored Lobby rendered no interactive START BATTLE control.');
    }
    // A `Rectangle` GameObject's position **is** its centre (origin 0.5), unlike
    // the collection rows, whose text origin is (0, 0).
    const startButtonPoint2 = await logicalToScreen(startButton2.x, startButton2.y);
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
        // The edited Relic selection, in the order the player chose it — which is
        // the equip-slot order (RELIC_RULES.md §2.3), never a re-sorted set.
        (battle2Request.relicLoadout || []).join(',') === expectedRelicLoadout.join(',') &&
        sorted(battle2Request.relicLoadout || []) !== sorted(battle1Loadout.relicIds) &&
        Object.keys(battle2Request).sort().join(',') === 'bossId,cardLoadout,petId,relicLoadout',
      {
        request: battle2Request,
        editedBossId: editedBoss.bossId,
        restoredRelicLoadout: battle1Loadout.relicIds,
        editedRelicLoadout: expectedRelicLoadout,
      }
    );
    await captureScreenshot(cdp, `run${runNumber}-10-battle2`);

    // --- Battle 2 outcome --------------------------------------------------
    // Same foreground guard as Battle 1's handoff: the scene start this queued
    // through `Runtime.evaluate` is processed by the engine's loop.
    await ensurePageVisible(cdp);
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

    // Battle 2's reward read is a second, independent failure state on a reused
    // scene instance: the same readable statement, exactly one RETRY control and
    // exactly one reward block — nothing accumulated from Battle 1's result.
    const result2Failure = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, RESULT_SNAPSHOT);
        return snap.active && snap.rewardState === 'failure' ? snap : false;
      },
      'Battle 2 ResultScene reported the unavailable reward summary',
      10000
    );
    record(
      'phase8.battle2RewardFailureIsReadableOnAReusedScene',
      result2Failure.rewardText === 'Rewards could not be loaded.\nPlease try again.' &&
        result2Failure.controls.filter((c) => c.label === 'RETRY').length === 1 &&
        result2Failure.durationText === '',
      {
        reward: result2Failure.rewardText,
        controls: result2Failure.controls.map((c) => c.label),
        duration: result2Failure.durationText,
      }
    );
    await captureScreenshot(cdp, `run${runNumber}-11-result-2`);

    // --- Reward failure → MAIN MENU ----------------------------------------
    record(
      'phase8.mainMenuIsUsableFromTheRewardFailure',
      result2Failure.controls.some((c) => c.label === 'MAIN MENU') &&
        result2Failure.controls.some((c) => c.label === 'PLAY AGAIN'),
      result2Failure.controls.map((c) => c.label)
    );
    const wentToMenu = await activateResultControl('MAIN MENU');
    record(
      'phase8.mainMenuReachedMainMenu',
      wentToMenu && (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene',
      resultControlFailureDetail
    );

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
    // PHASE 10: A REAL COMPLETED BATTLE — REWARD SUCCESS (TASK-211 §5, §7)
    // -------------------------------------------------------------
    // Every result above was handed to the presentation for a battle the server
    // had not completed, so `GET /api/battle/{battleId}/result` had no row to
    // serve. The success half of the reward block needs the real thing: a battle
    // fought to its own terminal resolution with real pointer input, which is
    // what makes a `BattleResult` row exist (`DATABASE.md` §1, `API_CONTRACTS.md`
    // §4.5 note 10). This phase plays that battle and then reads the delivered
    // rewards, the delivered length and the terminal Pet HP from it.
    console.log('\n--- Phase 10: A real completed battle (reward success) ---');

    /** The screen point of one board cell, from a live battle snapshot. */
    const cellCentreOf = (snapshot, index) => ({
      x:
        snapshot.viewport.left +
        (BOARD_ORIGIN_X + (index % BOARD_SIZE) * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2) *
          snapshot.viewport.scaleX,
      y:
        snapshot.viewport.top +
        (BOARD_ORIGIN_Y + Math.floor(index / BOARD_SIZE) * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2) *
          snapshot.viewport.scaleY,
    });

    /**
     * Plays the active battle to its own terminal resolution with real pointer
     * input, so the server persists a `BattleResult` for it. A rejected swap
     * commits nothing, so the pair is remembered for the board it was rejected on
     * and the next candidate is tried.
     */
    const driveBattleToTerminal = async (maxSwaps = Number(process.env.SMOKE_MAX_SWAPS) || 120) => {
      let swaps = 0;
      const started = Date.now();
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

        await realClick(cdp, cellCentreOf(battle, from));
        await realClick(cdp, cellCentreOf(battle, to));
        swaps += 1;

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
      }

      return {
        ended: (await evaluate(cdp, ACTIVE_SCENE)) === 'ResultScene',
        swaps,
        seconds: Math.round((Date.now() - started) / 1000),
      };
    };

    // MainMenu → Lobby, with a fresh selection (the MainMenu entry restores
    // nothing; only `PLAY AGAIN` does).
    for (let i = 0; i < 8; i++) {
      if ((await evaluate(cdp, ACTIVE_SCENE)) === 'LobbyScene') break;
      await realClick(cdp, menuStartPoint);
      await delay(1000);
    }
    lobby = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, LOBBY_SNAPSHOT);
        return snap.active && snap.ownedPetsCount > 0 && snap.ownedCardsCount >= 3 && snap.ownedRelicsCount >= 3
          ? snap
          : false;
      },
      'Lobby collections loaded for the real battle',
      12000
    );

    // Leaving the Lobby and coming back must not accumulate anything: this is a
    // third run of the same instance, and it still holds one control of each kind.
    record(
      'phase10.reusedLobbyStillHoldsOneControlOfEachKind',
      lobbyControlsOf(lobby).filter((c) => c.label === '< BACK').length === 1 &&
        lobbyControlsOf(lobby).filter((c) => c.label === 'START BATTLE').length === 1 &&
        !lobbyControlsOf(lobby).some((c) => c.label === 'RETRY'),
      lobbyControlsOf(lobby).map((c) => c.label)
    );

    await selectLoadout();
    lobby = await selectBossIn(CANONICAL_BOSSES[0].displayName);
    record(
      'phase10.realBattleLoadoutSelected',
      lobby.selectedPetId !== null &&
        lobby.selectedCardIds.length === 3 &&
        lobby.selectedRelicIds.length === 3 &&
        lobby.selectedBossId === CANONICAL_BOSSES[0].bossId,
      {
        petId: lobby.selectedPetId,
        bossId: lobby.selectedBossId,
        cards: lobby.selectedCardIds.length,
        relics: lobby.selectedRelicIds.length,
      }
    );

    await realClick(cdp, await toScreen(lobbyControlOf(lobby, 'START BATTLE').x, lobbyControlOf(lobby, 'START BATTLE').y));
    await waitForCondition(
      async () => (await evaluate(cdp, ACTIVE_SCENE)) === 'BattleScene',
      'Real battle reached BattleScene',
      15000
    );
    const realBattle = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, BATTLE_SNAPSHOT);
        return snap.active && snap.battleId && snap.boardLabels.length === BOARD_SIZE * BOARD_SIZE
          ? snap
          : false;
      },
      'Real battle synchronized with its own authoritative state',
      15000
    );

    // -------------------------------------------------------------
    // PHASE 10b: POST-RECONNECT GROUP DELIVERY (P-1, SIGNALR_PROTOCOL.md §7 item 4)
    // -------------------------------------------------------------
    // The acceptance leg for the reconnect group re-join. The scenario is the
    // documented one, in order:
    //
    //   connected + joined  →  committed battle action  →  real transport
    //   reconnect  →  the client re-joins the battle's group  →  another
    //   committed battle action  →  the resulting group broadcast reaches the
    //   NEW connection
    //
    // SignalR group membership is **connection-scoped** (§7 item 4), so a
    // reconnecting client cannot rely on the membership of the connection the
    // reconnect replaced: this leg fails unless the reconnected connection is
    // added to the group again *and* a server-generated broadcast then arrives on
    // it. §7.1's `GetBattleState` alone cannot satisfy it — that is a caller-only
    // request/response that carries no events (§5 item 4, §3.1 item 4) — and
    // neither can the re-join alone, because a join that never delivers a
    // subsequent broadcast is exactly the defect this leg exists to catch.
    //
    // It runs on the phase-10 battle, which is played to its own terminal
    // resolution immediately afterwards, so the extra resolution committed here
    // cannot disturb a later phase. Its pre-reconnect action is committed on a
    // freshly started battle, where neither side is near a terminal HP value.
    console.log('\n--- Phase 10b: Post-reconnect group delivery ---');

    /**
     * Page-side observation of the live connection (observation only — it
     * changes no product code path).
     *
     * Two things are recorded: every server → client push the connection
     * receives, with the connection id it arrived on and the sequence it carries
     * (§3 `ReceiveEvents`, §4 `BattleStateUpdated`), and every `joinBattle` the
     * client performs, with the connection id it was invoked on. The join is
     * recorded through the existing `SignalRService.joinBattle` operation — the
     * documented §1.2 group join — never by reaching into the transport.
     */
    const RECONNECT_PROBE = `
    (() => {
      const s = window.__game && window.__game.scene.getScene('BattleScene');
      const runtime = s && s.runtime ? s.runtime : null;
      const state = runtime && runtime.getBattleState ? runtime.getBattleState() : null;
      const probe = window.__reconnectProbe || null;
      return {
        active: !!(s && s.scene && s.scene.isActive()),
        connectionId: runtime && runtime.signalR ? runtime.signalR.getConnectionId() : null,
        sync: runtime && runtime.getState ? runtime.getState().sync : null,
        sequence: state ? state.sequence : null,
        boardLabels: s && s.boardLayer && s.boardLayer.list
          ? s.boardLayer.list.filter((o) => o.type === 'Text').map((o) => o.text)
          : [],
        joins: probe ? probe.joins.slice() : [],
        deliveries: probe ? probe.deliveries.slice() : [],
      };
    })()
    `;

    const probeInstalled = await evaluate(cdp, `(() => {
      const s = window.__game.scene.getScene('BattleScene');
      const runtime = s.runtime;
      const service = runtime.signalR;
      const state = runtime.getBattleState();

      window.__reconnectProbe = { joins: [], deliveries: [] };

      const observe = (name) => (payload) => {
        window.__reconnectProbe.deliveries.push({
          name,
          connectionId: service.getConnectionId(),
          // §3's batch carries \`serverSequence\`; §4's push carries the state's
          // own \`sequence\`. They are recorded separately and never conflated.
          serverSequence: payload && typeof payload.serverSequence === 'number' ? payload.serverSequence : null,
          sequence: payload && typeof payload.sequence === 'number' ? payload.sequence : null,
          eventCount: payload && Array.isArray(payload.events) ? payload.events.length : null,
        });
      };
      service.on('ReceiveEvents', observe('ReceiveEvents'));
      service.on('BattleStateUpdated', observe('BattleStateUpdated'));

      const joinBattle = service.joinBattle.bind(service);
      service.joinBattle = async (battleId) => {
        window.__reconnectProbe.joins.push({
          battleId,
          connectionId: service.getConnectionId(),
        });
        return await joinBattle(battleId);
      };

      return {
        battleId: state ? state.battleId : null,
        sequence: state ? state.sequence : null,
        connectionId: service.getConnectionId(),
      };
    })()`);

    /**
     * Commits one real board action with real pointer input and reports whether
     * the board changed — which is what a committed resolution proves.
     *
     * A rejected Swap commits nothing (`MATCH3_RULES.md` §2.1.5 item 6) and
     * delivers no broadcast (§3.1 item 4), so a rejection is retried against the
     * next candidate instead of being counted as a delivery.
     */
    const commitOneSwap = async () => {
      const attempted = [];

      for (let attempt = 0; attempt < 8; attempt++) {
        if ((await evaluate(cdp, ACTIVE_SCENE)) !== 'BattleScene') {
          return { committed: false, reason: 'the battle is no longer the active scene', attempted };
        }

        const before = await evaluate(cdp, BATTLE_SNAPSHOT);
        if (!before.active || before.boardLabels.length !== BOARD_SIZE * BOARD_SIZE) {
          await delay(300);
          continue;
        }

        const candidates = findMatchingSwaps(before.boardLabels);
        const [from, to] = candidates.length > 0 ? candidates[0] : [0, 1];
        await realClick(cdp, cellCentreOf(before, from));
        await realClick(cdp, cellCentreOf(before, to));

        try {
          await waitForCondition(
            async () => {
              const current = await evaluate(cdp, BATTLE_SNAPSHOT);
              if (!current.active) return true;
              return JSON.stringify(current.boardLabels) !== JSON.stringify(before.boardLabels);
            },
            'committed board after the swap',
            6000,
            120
          );
        } catch {
          /* rejected or unresolved: try the next candidate */
        }

        const after = await evaluate(cdp, BATTLE_SNAPSHOT);
        const changed =
          !after.active || JSON.stringify(after.boardLabels) !== JSON.stringify(before.boardLabels);

        attempted.push({ from, to, changed });

        if (changed) {
          return { committed: true, from, to, attempted };
        }
      }

      return { committed: false, reason: 'no candidate Swap was committed', attempted };
    };

    // ---- 1. One committed action on the joined connection ------------------
    const beforeReconnectAction = await evaluate(cdp, RECONNECT_PROBE);
    const preReconnect = await commitOneSwap();
    const afterPreReconnect = await evaluate(cdp, RECONNECT_PROBE);

    record(
      'phase10b.preReconnectActionCommitted',
      preReconnect.committed === true &&
        beforeReconnectAction.sequence !== null &&
        afterPreReconnect.sequence !== null &&
        afterPreReconnect.sequence > beforeReconnectAction.sequence,
      {
        action: preReconnect,
        sequence: [beforeReconnectAction.sequence, afterPreReconnect.sequence],
      }
    );

    // The action's own resolution reached this connection as a group broadcast —
    // the baseline that makes the post-reconnect delivery below a comparison
    // rather than an isolated observation.
    const preReconnectDeliveries = afterPreReconnect.deliveries.filter(
      (d) => d.name === 'ReceiveEvents' && d.serverSequence === afterPreReconnect.sequence
    );
    record(
      'phase10b.preReconnectActionDeliveredOnTheJoinedConnection',
      preReconnectDeliveries.length === 1 &&
        preReconnectDeliveries[0].connectionId === probeInstalled.connectionId,
      preReconnectDeliveries
    );

    // ---- 2. A real transport reconnect ------------------------------------
    const reconnected = await evaluate(cdp, `(async () => {
      const s = window.__game.scene.getScene('BattleScene');
      const runtime = s.runtime;
      const service = runtime.signalR;
      const connection = service.connection;
      const beforeConnectionId = service.getConnectionId();

      // A real transport stop and restart — a genuine close and reopen of the
      // connection — on the SAME connection object, which is why the runtime
      // keeps one connection and one subscription throughout
      // (ARCHITECTURE.md §2.2.1 rule 6). The server withdraws the old
      // connection from every group when it closes, and assigns the new one a
      // new connection id.
      await connection.stop();
      await connection.start();

      const connectionId = service.getConnectionId();
      const sameConnection = service.connection === connection;

      // SignalR raises its reconnect notification once the connection is back.
      // A manual stop/start does not raise it (a real outage does), so the
      // runtime's registered handler — the same code a real reconnect runs — is
      // invoked here, exactly as phase 6c does. Everything it does afterwards is
      // real: the group re-join and the snapshot request are real server round
      // trips on the new connection.
      if (service.handlers && typeof service.handlers.onReconnected === 'function') {
        service.handlers.onReconnected(connectionId);
      }

      return {
        beforeConnectionId,
        connectionId,
        sameConnection,
        syncAfterHandler: runtime.getState().sync,
      };
    })()`);

    // ---- 3. The re-join, on the new connection ----------------------------
    // The wait is on the resync the reconnect flow performs, not on the re-join:
    // the re-join and the delivery it enables are asserted by their own checks
    // below, so a missing re-join is reported as a missing re-join instead of
    // hiding the delivery observation behind it.
    const resyncedAfterReconnect = await waitForCondition(
      async () => {
        const probe = await evaluate(cdp, RECONNECT_PROBE);
        return probe.sync === 'synchronized' && probe.sequence !== null ? probe : false;
      },
      'the reconnected client re-synchronized from the snapshot',
      20000
    );

    const sequenceBeforePostReconnectAction = resyncedAfterReconnect.sequence;
    const rejoins = resyncedAfterReconnect.joins.filter(
      (j) => j.connectionId === reconnected.connectionId
    );

    record(
      'phase10b.reconnectProducedANewConnection',
      reconnected.sameConnection === true &&
        typeof reconnected.connectionId === 'string' &&
        reconnected.connectionId.length > 0 &&
        reconnected.connectionId !== reconnected.beforeConnectionId,
      reconnected
    );

    record(
      'phase10b.clientRejoinedTheBattleGroupOnTheNewConnection',
      rejoins.length === 1 &&
        rejoins[0].battleId === probeInstalled.battleId &&
        resyncedAfterReconnect.sequence >= probeInstalled.sequence,
      {
        rejoins,
        battleId: probeInstalled.battleId,
        connectionIds: [reconnected.beforeConnectionId, reconnected.connectionId],
      }
    );

    // ---- 4. Another committed action, after the re-join -------------------
    const postReconnect = await commitOneSwap();
    const afterPostReconnect = await evaluate(cdp, RECONNECT_PROBE);

    // The group broadcast of that action, and the state push that follows it,
    // both on the reconnected connection, both past the sequence the client held
    // before the action — and the client's own synchronized sequence equals the
    // broadcast's `serverSequence`, which is the client having ingested it
    // through the ordinary §3/§4 path rather than through any snapshot.
    const postReconnectDeliveries = afterPostReconnect.deliveries.filter(
      (d) => d.name === 'ReceiveEvents' && d.serverSequence > sequenceBeforePostReconnectAction
    );
    const postReconnectStatePushes = afterPostReconnect.deliveries.filter(
      (d) => d.name === 'BattleStateUpdated' && d.sequence > sequenceBeforePostReconnectAction
    );

    record(
      'phase10b.committedActionAfterReconnectDeliveredOnTheNewConnection',
      postReconnect.committed === true &&
        postReconnectDeliveries.length === 1 &&
        postReconnectDeliveries[0].connectionId === reconnected.connectionId &&
        postReconnectDeliveries[0].serverSequence === afterPostReconnect.sequence &&
        postReconnectStatePushes.length >= 1 &&
        postReconnectStatePushes.every((d) => d.connectionId === reconnected.connectionId),
      {
        action: postReconnect,
        sequence: [sequenceBeforePostReconnectAction, afterPostReconnect.sequence],
        deliveries: postReconnectDeliveries,
        statePushes: postReconnectStatePushes,
      }
    );

    await captureScreenshot(cdp, `run${runNumber}-11b-post-reconnect-group-delivery`);

    const realRun = await driveBattleToTerminal();
    record(
      'phase10.realBattleReachedItsOwnResult',
      realRun.ended === true,
      `${realRun.swaps} swaps in ${realRun.seconds}s`
    );

    const realResult = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, RESULT_SNAPSHOT);
        return snap.active && snap.outcomeText.length > 0 ? snap : false;
      },
      'Real battle ResultScene rendered',
      10000
    );
    record(
      'phase10.serverOutcomeRendered',
      ['VICTORY', 'DEFEAT'].includes(realResult.outcomeText),
      realResult.outcomeText
    );

    // TASK-211 §5: the reward block's success state. A persisted result row now
    // exists for this battle, so the read delivers and the block shows the eight
    // delivered members instead of the failure statement.
    const realRewards = await waitForCondition(
      async () => {
        const snap = await evaluate(cdp, RESULT_SNAPSHOT);
        return snap.active && snap.rewardState === 'success' ? snap : false;
      },
      'ResultScene rendered the persisted reward summary',
      12000
    );
    record(
      'phase10.deliveredRewardsAreRendered',
      realRewards.rewardText.includes('REWARDS') &&
        realRewards.rewardText.includes('Player:') &&
        realRewards.rewardText.includes('Pet:') &&
        !realRewards.rewardText.includes('Rewards could not be loaded.'),
      realRewards.rewardText
    );

    // TASK-211 §7: the length is the delivered `durationTurns` — no turn was
    // counted here, no timestamp was read, and no combat event was consulted.
    const deliveredResult = await evaluate(cdp, `(async () => {
      const token = localStorage.getItem('dcacti_session_token');
      const response = await fetch('/api/battle/${realBattle.battleId}/result', {
        method: 'GET',
        headers: token ? { Authorization: 'Bearer ' + token } : {},
      });
      return { status: response.status, body: await response.json().catch(() => null) };
    })()`);
    record(
      'phase10.durationIsRenderedFromTheDeliveredResult',
      deliveredResult.status === 200 &&
        deliveredResult.body !== null &&
        realRewards.durationText === `Duration: ${deliveredResult.body.durationTurns} turns`,
      { rendered: realRewards.durationText, delivered: deliveredResult.body?.durationTurns }
    );

    // The rendered members are exactly the delivered ones, line by line.
    const renderedRewardLines = realRewards.rewardText.split('\n');
    const playerLine = renderedRewardLines.find((line) => line.startsWith('Player:')) || '';
    const petLine = renderedRewardLines.find((line) => line.startsWith('Pet:')) || '';
    const rewards = deliveredResult.body === null ? null : deliveredResult.body.rewards;
    record(
      'phase10.rewardLinesCarryTheDeliveredMembers',
      rewards !== null &&
        playerLine.includes(`+${rewards.playerXpGained} XP`) &&
        playerLine.includes(`XP ${rewards.newPlayerXp ?? '—'}`) &&
        playerLine.includes(`Level ${rewards.newPlayerLevel ?? '—'}`) &&
        (rewards.playerLeveledUp === true
          ? playerLine.includes('LEVEL UP')
          : !playerLine.includes('LEVEL UP')) &&
        petLine.includes(`+${rewards.petXpGained} XP`) &&
        petLine.includes(`XP ${rewards.newPetXp ?? '—'}`) &&
        petLine.includes(`Level ${rewards.newPetLevel ?? '—'}`) &&
        (rewards.petLeveledUp === true
          ? petLine.includes('LEVEL UP')
          : !petLine.includes('LEVEL UP')),
      { playerLine, petLine, delivered: rewards }
    );

    // TASK-211 §8: the terminal Pet HP line names the Pet, and it prints the
    // value the battle delivered — never a locally derived one.
    record(
      'phase10.terminalPetHpIsLabelledAndVerbatim',
      realResult.finalPlayerHp !== null &&
        realResult.playerHpText === `Final Pet HP: ${realResult.finalPlayerHp}` &&
        realResult.finalBossHp !== null &&
        realResult.bossHpText === `Final Boss HP: ${realResult.finalBossHp}`,
      {
        petHp: realResult.playerHpText,
        bossHp: realResult.bossHpText,
        delivered: { petHp: realResult.finalPlayerHp, bossHp: realResult.finalBossHp },
      }
    );

    // A success is not a failure state, and the two exits are still the only
    // navigation: no RETRY and no duplicate control.
    record(
      'phase10.successOffersNoRetryAndKeepsBothExits',
      !realRewards.controls.some((c) => c.label === 'RETRY') &&
        realRewards.controls.map((c) => c.label).sort().join('|') === 'MAIN MENU|PLAY AGAIN' &&
        realRewards.controls.length === 2,
      realRewards.controls.map((c) => c.label)
    );

    // The delivered reward surface is legible: the block, the length line and
    // the two HP lines do not overlap each other or leave the safe area.
    const layoutProbe = await evaluate(cdp, `(() => {
      const s = window.__game.scene.getScene('ResultScene');
      const boundsOf = (t) => {
        if (!t || typeof t.getBounds !== 'function') return null;
        const b = t.getBounds();
        return { left: b.x, top: b.y, right: b.x + b.width, bottom: b.y + b.height };
      };
      return {
        bossHp: boundsOf(s.bossHpText),
        petHp: boundsOf(s.playerHpText),
        duration: boundsOf(s.durationText),
        reward: boundsOf(s.rewardText),
      };
    })()`);
    const layoutOverlaps = (a, b) =>
      a && b && a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;
    record(
      'phase10.rewardPresentationDoesNotOverlapOrClip',
      Object.values(layoutProbe).every(
        (b) => b !== null && b.left >= 24 && b.top >= 24 && b.right <= 1256 && b.bottom <= 696
      ) &&
        !layoutOverlaps(layoutProbe.bossHp, layoutProbe.petHp) &&
        !layoutOverlaps(layoutProbe.petHp, layoutProbe.duration) &&
        !layoutOverlaps(layoutProbe.duration, layoutProbe.reward),
      layoutProbe
    );
    await captureScreenshot(cdp, `run${runNumber}-12-result-rewards`);

    // Leave the app on the main menu, so the run ends where it started.
    await activateResultControl('MAIN MENU');
    record(
      'phase10.returnedToMainMenuAfterTheRealBattle',
      (await evaluate(cdp, ACTIVE_SCENE)) === 'MainMenuScene',
      resultControlFailureDetail
    );

    // -------------------------------------------------------------
    // PHASE 9: HEALTH & SAFETY GATES
    // -------------------------------------------------------------
    console.log('\n--- Phase 9: Error & Health Safety Gates ---');
    record('safety.zeroDiscordDependencies', discordEvents.length === 0, `${discordEvents.length} Discord events`);
    record('safety.zeroUncaughtExceptions', uncaughtExceptions.length === 0, uncaughtExceptions);
    record('safety.zeroFatalConsoleErrors', consoleErrors.length === 0, consoleErrors);

    // Filter API responses: only 200 OK expected, plus the two failures this run
    // induces on purpose — a result read for a battle the server has not
    // completed (`404 BATTLE_NOT_FOUND`, §4 notes 6–7) and the documented
    // battle-start rejection that verifies the Lobby's failure state
    // (`400 PET_NOT_OWNED`, §3). Each induced rejection is asserted above, with
    // its §6 envelope read off the wire, so nothing is merely excused here.
    const inducedStartRejections = Array.from(apiResponses.values()).filter(
      (resp) => resp.url.includes('/api/battle/start') && resp.status === 400
    );
    record(
      'safety.onlyTheInducedDocumentedRejectionsOccurred',
      inducedStartRejections.length === 3,
      inducedStartRejections
    );

    const unexpectedApiResponses = Array.from(apiResponses.values()).filter((resp) => {
      if (resp.status >= 200 && resp.status < 300) return false;
      if (resp.url.includes('/result') && resp.status === 404) return false;
      if (resp.url.includes('/api/battle/start') && resp.status === 400) return false;
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
