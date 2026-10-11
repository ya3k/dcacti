import { describe, it, expect, vi, afterEach } from 'vitest';
import { createActionId } from '../src/game/runtime/ActionIdentity';

/**
 * `ADR-025` §2 makes the action identity the key the server deduplicates a retry
 * on, so its uniqueness — across logical actions *and* across runtime resets — is
 * a correctness property, not a nicety. These tests assert it directly, including
 * the fallback paths a runtime without `crypto.randomUUID()` takes.
 */
describe('createActionId', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('generates a distinct identity per call', () => {
    const ids = new Set(Array.from({ length: 64 }, () => createActionId('swap')));

    expect(ids.size).toBe(64);
  });

  it('produces an identity the action-token grammar can carry', () => {
    // SIGNALR_PROTOCOL.md §2 item 1: the token is "{expectedSequence}:{actionId}",
    // so the identity must not contain the delimiter, and it must not be a bare
    // integer (the ambiguous form the hub refuses).
    for (let index = 0; index < 32; index += 1) {
      const actionId = createActionId('swap');

      expect(actionId).not.toContain(':');
      expect(actionId).not.toMatch(/^[0-9]+$/);
      expect(actionId.length).toBeGreaterThan(0);
    }
  });

  it('uses crypto.randomUUID when the runtime provides it', () => {
    const randomUUID = vi.fn(() => '11111111-2222-3333-4444-555555555555');
    vi.stubGlobal('crypto', { randomUUID });

    expect(createActionId('swap')).toBe('swap-11111111-2222-3333-4444-555555555555');
    expect(randomUUID).toHaveBeenCalledTimes(1);
  });

  it('falls back to a random crypto value when randomUUID is unavailable', () => {
    // A runtime that has `crypto` but no `randomUUID` (the older secure-context
    // shape) still gets a random identity rather than a counter. The stub varies
    // the bytes per call the way a real random source does.
    let nextByte = 0x01;
    const getRandomValues = vi.fn((bytes: Uint8Array) => {
      bytes.fill(nextByte);
      nextByte += 1;

      return bytes;
    });
    vi.stubGlobal('crypto', { getRandomValues });

    const actionId = createActionId('swap');

    expect(getRandomValues).toHaveBeenCalledTimes(1);
    expect(actionId).toBe(`swap-${'01'.repeat(16)}`);
    expect(createActionId('swap')).not.toBe(actionId);
  });

  it('falls back to time plus a random component when no crypto exists at all', () => {
    vi.stubGlobal('crypto', undefined);

    const first = createActionId('swap');
    const second = createActionId('swap');

    expect(first).not.toBe(second);
    expect(first).toMatch(/^swap-[0-9a-z]+-[0-9a-z]+-[0-9a-z]+$/);
  });

  it('does not reproduce an identity after a fresh module evaluation', async () => {
    // A page reload is a fresh module evaluation. A module-scoped counter would
    // restart and hand the first action of the new runtime the identity the
    // earlier runtime used, which the server reads as a retry of that action
    // (ADR-025 §2) — the swap the player just made would never execute.
    vi.stubGlobal('crypto', undefined);

    const first = createActionId('swap');

    vi.resetModules();
    const reloaded = await import('../src/game/runtime/ActionIdentity');

    expect(reloaded.createActionId('swap')).not.toBe(first);
  });
});
