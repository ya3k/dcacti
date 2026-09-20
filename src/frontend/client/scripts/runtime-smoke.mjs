/**
 * Integration smoke test for the game runtime foundation.
 *
 * Exercises the real transport path against a running backend:
 *
 *   Node SignalR client -> BattleHub -> RuntimeService (Application)
 *
 * It verifies the connection lifecycle the runtime depends on: connection
 * acknowledgement (the technical handshake), a stable connection id, clean
 * disconnect, reconnect, and the absence of gameplay hub methods.
 *
 * Run with the backend listening on http://localhost:5000.
 */
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';

const HUB_URL = process.env.SMOKE_HUB_URL ?? 'http://localhost:5000/hubs/battle';

const results = [];

function record(name, passed, detail = '') {
  results.push({ name, passed, detail });
  console.log(`${passed ? 'PASS' : 'FAIL'}  ${name}${detail ? ` — ${detail}` : ''}`);
}

function buildConnection() {
  return new HubConnectionBuilder()
    .withUrl(HUB_URL)
    .configureLogging(LogLevel.Error)
    .build();
}

async function main() {
  // 1. Connect.
  const connection = buildConnection();
  await connection.start();
  record('signalr.connect', connection.state === HubConnectionState.Connected, connection.state);

  // 2. Connection identity assigned by the server.
  record(
    'signalr.connectionId',
    typeof connection.connectionId === 'string' && connection.connectionId.length > 0,
    connection.connectionId ?? 'none'
  );

  // 3. Technical handshake: runtime status acknowledgement.
  const acknowledged = new Promise((resolve) => {
    connection.on('RuntimeStatusChanged', (status) => resolve(status));
  });

  // A fresh connection re-triggers the handshake; subscribe before connecting
  // again to observe it deterministically.
  const handshakeConnection = buildConnection();
  const handshake = new Promise((resolve) => {
    handshakeConnection.on('RuntimeStatusChanged', (status) => resolve(status));
  });
  await handshakeConnection.start();

  const status = await Promise.race([
    handshake,
    new Promise((_, reject) =>
      setTimeout(() => reject(new Error('handshake timeout')), 10000)
    ),
  ]).catch((error) => ({ error: error.message }));

  if (status && !status.error) {
    record('battlehub.handshake.connectionId', typeof status.connectionId === 'string', status.connectionId);
    record('battlehub.handshake.status', status.status === 0, `status=${status.status}`);
    record('battlehub.handshake.sequence', typeof status.sequence === 'number' && status.sequence > 0, `sequence=${status.sequence}`);
  } else {
    record('battlehub.handshake', false, status?.error ?? 'no acknowledgement');
  }
  await handshakeConnection.stop();

  // 4. No gameplay hub methods exist yet (out of scope for this foundation).
  const gameplayMethods = ['Swap', 'CardCast', 'PetSkillCast', 'GetBattleState'];
  for (const method of gameplayMethods) {
    let rejected = false;
    try {
      await connection.invoke(method, 'battle-x');
    } catch {
      rejected = true;
    }
    record(`battlehub.no-gameplay-method.${method}`, rejected, rejected ? 'not registered' : 'UNEXPECTEDLY PRESENT');
  }

  // 5. Clean disconnect.
  await connection.stop();
  record('signalr.disconnect', connection.state === HubConnectionState.Disconnected, connection.state);

  // 6. Reconnect on a fresh connection.
  const reconnected = buildConnection();
  await reconnected.start();
  record(
    'signalr.reconnect',
    reconnected.state === HubConnectionState.Connected && Boolean(reconnected.connectionId),
    reconnected.connectionId ?? 'none'
  );
  await reconnected.stop();

  // 7. Unreachable backend fails gracefully rather than hanging.
  const bad = new HubConnectionBuilder()
    .withUrl('http://localhost:59999/hubs/battle')
    .configureLogging(LogLevel.None)
    .build();
  let failedGracefully = false;
  try {
    await bad.start();
  } catch {
    failedGracefully = true;
  }
  record('signalr.failure-handled', failedGracefully, failedGracefully ? 'rejected' : 'did not reject');

  // Avoid an unhandled rejection from the unused promise.
  void acknowledged;

  const failed = results.filter((r) => !r.passed);
  console.log(`\n${results.length - failed.length}/${results.length} checks passed`);

  // Sets the exit code without force-terminating the process, so the Node event
  // loop can drain cleanly (an abrupt process.exit() asserts in libuv teardown
  // on Windows).
  process.exitCode = failed.length === 0 ? 0 : 1;
}

main().catch((error) => {
  console.error('SMOKE TEST ERROR:', error);
  process.exitCode = 1;
});