/**
 * Action identity generation for the client runtime (`ADR-025` §2).
 *
 * The identity is what the authoritative server deduplicates a retry on: a Swap
 * carries it as the second half of its action token
 * (`SIGNALR_PROTOCOL.md` §2 item 1, `"{expectedSequence}:{actionId}"`), and
 * `BattleStateService` answers a request whose identity matches the most recently
 * committed action as an idempotent replay instead of executing it again.
 *
 * That contract gives the identity two required properties:
 *
 * ```text
 * unique across logical actions   two different actions must never share one, or
 *                                 the second is answered as a replay of the
 *                                 first and silently does nothing
 * unique across runtime resets    a page reload is a fresh runtime; if it
 *                                 reproduced an identity an earlier runtime used,
 *                                 its first swap of that pair would be read as a
 *                                 retry of that earlier action
 * ```
 *
 * A counter satisfies neither across a reset — a fresh process restarts it — so
 * no branch here is a counter on its own. `crypto.randomUUID()` is used where the
 * runtime provides it; the fallbacks are a random `crypto` value, or time plus a
 * random component where no `crypto` exists at all.
 *
 * This is **not** gameplay randomness. Nothing here is a game value, nothing here
 * is drawn from or fed to the RNG the server owns (`TDD.md` §6, `GAME_RULES.md`
 * §18), and the result is never read as one: it identifies a request and nothing
 * else (`AGENTS.md` §11).
 */

/**
 * Counter behind the last-resort identity only.
 *
 * It is a tie-breaker inside the final branch, never a uniqueness mechanism of
 * its own: its value is combined with the current time and a random component, so
 * an identity it contributes is not reproducible by a fresh process that starts
 * the counter again.
 */
let fallbackCounter = 0;

/**
 * Creates the unique identity of one logical action (`ADR-025` §2).
 *
 * @param prefix A short, non-numeric label for the action kind, e.g. `swap`. It
 * is presentational (it makes a logged identity readable) and adds no uniqueness
 * of its own.
 * @returns An identifier that contains no `:` — the action token's delimiter — and
 * is never a bare integer, so it is expressible in both documented token forms.
 */
export function createActionId(prefix: string): string {
  const webCrypto = (globalThis as { crypto?: Crypto }).crypto;

  if (webCrypto && typeof webCrypto.randomUUID === 'function') {
    return `${prefix}-${webCrypto.randomUUID()}`;
  }

  if (webCrypto && typeof webCrypto.getRandomValues === 'function') {
    const bytes = new Uint8Array(16);
    webCrypto.getRandomValues(bytes);

    const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');

    return `${prefix}-${hex}`;
  }

  fallbackCounter += 1;

  return `${prefix}-${Date.now().toString(36)}-${fallbackCounter.toString(36)}-${Math.random()
    .toString(36)
    .slice(2, 10)}`;
}
