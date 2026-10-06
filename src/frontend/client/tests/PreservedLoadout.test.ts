import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import {
  PRESERVED_LOADOUT_REGISTRY_KEY,
  clearPreservedLoadout,
  preserveLoadout,
  readPreservedLoadout,
} from '../src/game/state/PreservedLoadout';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
import type { BattleStartRequest } from '../src/services/api/BattleModels';

/**
 * The post-result preserved-loadout carrier (TASK-203, `D-202-03 = D`, ADR-022).
 *
 * The carrier is a second key in Phaser's game-wide registry, so these tests use
 * the registry surface a scene actually has. The scene-level behavior — who
 * writes it, who reads it, and which entries do neither — is covered by
 * `LobbyScene.test.ts` and `SceneLifecycle.test.ts`.
 */
function gameRegistry(values = new Map<string, unknown>()) {
  const scene = {
    registry: {
      get: (key: string) => values.get(key),
      set: (key: string, value: unknown) => {
        values.set(key, value);
      },
      remove: (key: string) => {
        values.delete(key);
      },
    },
  };

  return { scene, values };
}

/** The loadout the battle that just ended was fought with (`API_CONTRACTS.md` §3). */
function loadout(overrides: Partial<BattleStartRequest> = {}): BattleStartRequest {
  return {
    petId: 'pet-instance-1',
    bossId: 'boss-kim-loi-vuong',
    cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'],
    relicLoadout: ['relic-instance-1', 'relic-instance-2', 'relic-instance-3'],
    ...overrides,
  };
}

describe('preserved loadout carrier (TASK-203, ADR-022)', () => {
  it('holds nothing until a loadout is preserved', () => {
    const { scene } = gameRegistry();

    expect(readPreservedLoadout(scene as never)).toBeNull();
  });

  it('round-trips the four documented request members verbatim', () => {
    const { scene } = gameRegistry();
    const submitted = loadout();

    preserveLoadout(scene as never, submitted);

    // Exactly the submitted selection: nothing defaulted, reordered, filtered,
    // or derived (API_CONTRACTS.md §3).
    expect(readPreservedLoadout(scene as never)).toEqual(submitted);
  });

  it('is keyed separately from the runtime it shares the registry with', () => {
    const { scene, values } = gameRegistry();
    const runtime = { getState: () => 'runtime-double' };

    values.set(RUNTIME_REGISTRY_KEY, runtime);
    preserveLoadout(scene as never, loadout());

    expect(PRESERVED_LOADOUT_REGISTRY_KEY).not.toBe(RUNTIME_REGISTRY_KEY);
    // The preserved loadout is not the runtime and the runtime does not hold it
    // (ARCHITECTURE.md §2.2.3 rule 2).
    expect(readPreservedLoadout(scene as never)).toEqual(loadout());
    expect(values.get(RUNTIME_REGISTRY_KEY)).toBe(runtime);
  });

  it('owns its own copy, so neither side can rewrite the other', () => {
    const { scene } = gameRegistry();
    const submitted = loadout({ cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'] });

    preserveLoadout(scene as never, submitted);

    // A later edit of the submitted request does not rewrite what was preserved:
    // preserving is a lifetime extension of one selection, not a live view of
    // the scene's arrays.
    (submitted.cardLoadout as string[]).push('card-inferno');
    (submitted.relicLoadout as string[]).shift();

    const preserved = readPreservedLoadout(scene as never);
    expect(preserved).toEqual(loadout({ cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'] }));

    // And editing the value handed out does not rewrite the carrier either.
    (preserved!.cardLoadout as string[]).length = 0;
    expect(readPreservedLoadout(scene as never)).toEqual(
      loadout({ cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'] })
    );
  });

  it('is replaced by the next preserved loadout and cleared only explicitly', () => {
    const { scene } = gameRegistry();
    const second = loadout({ petId: 'pet-instance-2', bossId: 'boss-hoa-long' });

    preserveLoadout(scene as never, loadout());
    preserveLoadout(scene as never, second);
    expect(readPreservedLoadout(scene as never)).toEqual(second);

    clearPreservedLoadout(scene as never);
    expect(readPreservedLoadout(scene as never)).toBeNull();

    // Safe to clear twice, and safe to clear what was never there.
    expect(() => clearPreservedLoadout(scene as never)).not.toThrow();
  });

  it('is client presentation state: no transport, no persistence, no runtime', () => {
    const source = readFileSync(
      resolve(__dirname, '../src/game/state/PreservedLoadout.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    // A scene reaches the runtime through `RuntimeRegistry`, and this carrier is
    // not the runtime: it imports no runtime, no scene, and no transport.
    expect(source).not.toContain('GameRuntime');
    expect(source).not.toContain('Scene.ts');
    expect(source).not.toMatch(/@microsoft\/signalr|HubConnection|services\/realtime/);
    expect(source).not.toMatch(/\bfetch\s*\(/);

    // The only `services/api/` import is the type-only wire model whose four
    // members the carrier holds — no transport implementation, and no second
    // loadout/selection type (`ARCHITECTURE.md` §2.2.3 rule 6).
    const apiImports = [...source.matchAll(/import type \{([^}]+)\} from '([^']*services\/api\/[^']+)'/g)];
    expect(apiImports.map((m) => m[2])).toEqual(['../../services/api/BattleModels']);
    expect(source).not.toMatch(/export (interface|type) \w*(Loadout|Selection|Collection)\w*/);

    // Nothing is persisted and nothing touches the browser's storage or the
    // document (D3 of ADR-022): the carrier lives and dies with the game
    // instance.
    expect(source).not.toMatch(/localStorage|sessionStorage|indexedDB/);
    expect(source).not.toMatch(/\bwindow\b|\bdocument\b/);
  });
});
