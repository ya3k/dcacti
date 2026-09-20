/**
 * Battle State Foundation — end-to-end pipeline verification.
 *
 * Verifies the documented transport path against a running backend that
 * contains this task's implementation, using the real client transport
 * (`SignalRService`) and the real client coordination layer (`GameRuntime`) on
 * top of the real SignalR client:
 *
 *   ASP.NET Core -> Authoritative Battle State -> SignalR -> SignalRService
 *                -> GameRuntime -> (BattleScene readout)
 *
 * It asserts the foundation contract only (GAME_STATE.md §2.0,
 * SIGNALR_PROTOCOL.md §4): BattleId, Turn = 0, Sequence = 0 — no gameplay.
 *
 * Usage: node scripts/battle-state-smoke.mjs
 *   SMOKE_HUB_URL  default http://localhost:5000/hubs/battle
 *   SMOKE_BATTLE_ID default a battle id the verification backend pre-creates
 */
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { readFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const HUB_URL = process.env.SMOKE_HUB_URL ?? 'http://localhost:5000/hubs/battle';
const BATTLE_ID = process.env.SMOKE_BATTLE_ID ?? 'battle-foundation-smoke';

const results = [];

function record(name, passed, detail = '') {
  results.push({ name, passed, detail });
  console.log(`${passed ? 'PASS' : 'FAIL'}  ${name}${detail ? ` — ${detail}` : ''}`);
}

/**
 * Loads the client's real runtime and transport sources.
 *
 * The client modules are TypeScript, so they are transpiled in-memory rather
 * than duplicated here — the point of this check is to exercise the actual
 * shipped pipeline, not a re-implementation of it.
 */
async function loadClientRuntime() {
  const here = dirname(fileURLToPath(import.meta.url));
  const clientRoot = resolve(here, '..');

  const { createServer } = await import('vite');
  const server = await createServer({
    root: clientRoot,
    logLevel: 'error',
    server: { middlewareMode: true },
    appType: 'custom',
  });

  try {
    const signals = await server.ssrLoadModule('/src/services/realtime/SignalRService.ts');
    const runtimeModule = await server.ssrLoadModule('/src/game/runtime/GameRuntime.ts');
    return {
      SignalRService: signals.SignalRService,
      GameRuntime: runtimeModule.GameRuntime,
      close: () => server.close(),
    };
  } catch (error) {
    await server.close();
    throw error;
  }
}

async function main() {
  // ---------------------------------------------------------------------
  // 1. Server side: confirm the authoritative foundation state exists and is
  //    created by the server, then read it through the documented hub path.
  // ---------------------------------------------------------------------

  // The verification backend pre-creates its battle session. This probe only
  // confirms whether that id is known; it never creates state itself.
  const connection = new HubConnectionBuilder()
    .withUrl(HUB_URL)
    .configureLogging(LogLevel.Error)
    .build();

  await connection.start();
  record('signalr.connect', true, HUB_URL);

  const pushed = [];
  // The handler must not return a value: SignalR treats a handler's return
  // value as an invocation result and logs an error when the server expects
  // none. `push()` returns a number, so the body needs braces.
  connection.on('BattleStateUpdated', (payload) => {
    pushed.push(payload);
  });

  await connection.invoke('JoinBattle', BATTLE_ID);

  // The push is server-initiated on join (§4.1) — allow it to arrive.
  const deadline = Date.now() + 10000;
  while (pushed.length === 0 && Date.now() < deadline) {
    await new Promise((r) => setTimeout(r, 100));
  }

  record(
    'battlehub.BattleStateUpdated.pushed-on-join',
    pushed.length === 1,
    `${pushed.length} push(es)`
  );

  if (pushed.length > 0) {
    const state = pushed[0];

    record(
      'state.battleId',
      state.battleId === BATTLE_ID,
      String(state.battleId)
    );
    record('state.turn.initial', state.turn === 0, `turn=${state.turn}`);
    record('state.sequence.initial', state.sequence === 0, `sequence=${state.sequence}`);

    // §4.4: battleId, turn, sequence — no other field.
    const fields = Object.keys(state).sort();
    record(
      'state.fields.documented-only',
      JSON.stringify(fields) === JSON.stringify(['battleId', 'sequence', 'turn']),
      fields.join(',')
    );

    // §8.3 / GAME_STATE.md §2.0.3: no Status and no lifecycle value.
    const serialized = JSON.stringify(state);
    record(
      'state.no-status-field',
      !/status|ready|starting|active|paused|finished|won|lost/i.test(serialized),
      serialized
    );
  }

  // A second join must not mutate the authoritative state (§2.0.2).
  pushed.length = 0;
  await connection.invoke('JoinBattle', BATTLE_ID);
  const secondDeadline = Date.now() + 5000;
  while (pushed.length === 0 && Date.now() < secondDeadline) {
    await new Promise((r) => setTimeout(r, 100));
  }
  record(
    'state.immutable-across-joins',
    pushed.length > 0 && pushed[0].turn === 0 && pushed[0].sequence === 0,
    pushed.length > 0 ? `turn=${pushed[0].turn} sequence=${pushed[0].sequence}` : 'no push'
  );

  await connection.stop();

  // ---------------------------------------------------------------------
  // 2. Client side: the real GameRuntime + SignalRService pipeline.
  // ---------------------------------------------------------------------

  const client = await loadClientRuntime();

  try {
    // A fresh transport instance is needed because the service is a
    // process-wide singleton that the probe above did not use.
    const signalR = client.SignalRService.getInstance();
    await signalR.disconnect();

    const runtime = new client.GameRuntime(signalR);

    const observed = [];
    runtime.onBattleState((state) => observed.push(state));

    await runtime.initialize(HUB_URL);

    record('runtime.connected', runtime.getState().connection === 'connected');
    record(
      'runtime.awaiting-battle-before-join',
      runtime.getBattleState() === null && runtime.getState().sync === 'awaiting_battle'
    );

    await signalR.joinBattle(BATTLE_ID);

    const runtimeDeadline = Date.now() + 10000;
    while (observed.length === 0 && Date.now() < runtimeDeadline) {
      await new Promise((r) => setTimeout(r, 100));
    }

    record('runtime.received-state', observed.length > 0, `${observed.length} update(s)`);

    const state = runtime.getBattleState();
    record('runtime.stores-battleId', state?.battleId === BATTLE_ID, String(state?.battleId));
    record('runtime.stores-turn', state?.turn === 0, `turn=${state?.turn}`);
    record('runtime.stores-sequence', state?.sequence === 0, `sequence=${state?.sequence}`);
    record('runtime.sync-synchronized', runtime.getState().sync === 'synchronized');
    record(
      'runtime.state-fields.documented-only',
      JSON.stringify(Object.keys(state ?? {}).sort()) ===
        JSON.stringify(['battleId', 'sequence', 'turn']),
      Object.keys(state ?? {}).join(',')
    );
    record('runtime.no-error', runtime.getState().lastError === null, String(runtime.getState().lastError));

    // The runtime must not expose gameplay state on its technical contract.
    const runtimeKeys = Object.keys(runtime.getState());
    const forbiddenState = ['hp', 'board', 'gems', 'power', 'combo', 'damage', 'boss', 'pet'];
    record(
      'runtime.technical-state-has-no-gameplay',
      forbiddenState.every((k) => !runtimeKeys.includes(k)),
      runtimeKeys.join(',')
    );

    await runtime.dispose();
  } finally {
    await client.close();
  }

  // ---------------------------------------------------------------------
  // 3. Source-level scope guard: no gameplay was introduced.
  // ---------------------------------------------------------------------
  const here = dirname(fileURLToPath(import.meta.url));
  const sceneSource = readFileSync(
    resolve(here, '../src/game/scenes/BattleScene.ts'),
    'utf8'
  ).replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|[^:])\/\/.*$/gm, '');

  const gameplayTerms = ['gem', 'board', 'swap', 'cascade', 'combo', 'match3', 'damage'];
  record(
    'battlescene.contains-no-gameplay-code',
    gameplayTerms.every((term) => !sceneSource.toLowerCase().includes(term)),
    ''
  );

  const failed = results.filter((r) => !r.passed);
  console.log(`\n${results.length - failed.length}/${results.length} checks passed`);
  if (failed.length) {
    console.log('\nFailures:');
    for (const f of failed) console.log(`  - ${f.name} (${f.detail})`);
  }
  process.exitCode = failed.length === 0 ? 0 : 1;
}

main().catch((error) => {
  console.error('SMOKE TEST ERROR:', error);
  process.exitCode = 1;
});