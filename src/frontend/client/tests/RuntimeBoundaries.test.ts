import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { resolve, join } from 'node:path';

/**
 * Architectural boundary tests (task §16, §29; AGENTS.md §10, §13).
 *
 * These assert the runtime foundation's structural rules directly against the
 * source, because the boundaries are the deliverable of this task:
 *
 *   - Phaser scenes must not depend on the SignalR transport.
 *   - The client must not implement authoritative gameplay.
 *   - The runtime must contain no gameplay calculations or domain entities.
 *
 * They are static source checks: a boundary violation is a structural defect
 * that a behavioural test would not catch.
 */
const SRC = resolve(__dirname, '../src');

function readSource(relativePath: string): string {
  return readFileSync(join(SRC, relativePath), 'utf8');
}

/** Strips comments so documentation prose never triggers a false positive. */
function stripComments(source: string): string {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/(^|[^:])\/\/.*$/gm, '$1');
}

function walk(dir: string): string[] {
  const out: string[] = [];
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      out.push(...walk(full));
    } else if (/\.(ts|tsx)$/.test(entry)) {
      out.push(full);
    }
  }
  return out;
}

describe('Frontend architectural boundaries', () => {
  describe('Phaser is not coupled to SignalR', () => {
    const sceneFiles = ['BootScene.ts', 'PreloaderScene.ts', 'BattleScene.ts'].map(
      (f) => join('game', 'scenes', f)
    );

    it.each(sceneFiles)('%s does not import the SignalR transport', (file) => {
      const code = stripComments(readSource(file));

      expect(code).not.toMatch(/@microsoft\/signalr/);
      expect(code).not.toMatch(/HubConnection/);
      expect(code).not.toMatch(/services\/realtime/);
    });

    it('GameConfig does not import the SignalR transport', () => {
      const code = stripComments(readSource(join('game', 'GameConfig.ts')));

      expect(code).not.toMatch(/@microsoft\/signalr/);
      expect(code).not.toMatch(/HubConnection/);
    });

    it('GameRuntime contains no SignalR implementation details beyond the service port', () => {
      const code = stripComments(readSource(join('game', 'runtime', 'GameRuntime.ts')));

      // It may use SignalRService (the documented existing service)...
      expect(code).toMatch(/SignalRService/);
      // ...but must not talk to the HubConnection itself.
      expect(code).not.toMatch(/@microsoft\/signalr/);
      expect(code).not.toMatch(/HubConnectionBuilder/);
    });

    it('exactly one SignalRService implementation exists', () => {
      const realtimeFiles = walk(join(SRC, 'services', 'realtime')).filter((f) =>
        /SignalR/i.test(f)
      );

      expect(realtimeFiles).toHaveLength(1);
    });
  });

  describe('the client implements no authoritative gameplay', () => {
    const runtimeFiles = [
      join('state', 'GameRuntimeState.ts'),
      join('game', 'runtime', 'GameRuntime.ts'),
      join('game', 'runtime', 'GameRuntimeEvents.ts'),
    ];

    it.each(runtimeFiles)('%s declares no gameplay state or calculations', (file) => {
      const code = stripComments(readSource(file)).toLowerCase();

      // The runtime carries the implemented stage's fields as a synchronized
      // presentation copy — `board` from the Board Foundation stage
      // (GAME_STATE.md §2.0.5) and `playerState`'s `matchCount`/`combo` from the
      // Match / Combo accounting stage (§2.2) — so those names are part of the
      // documented contract rather than violations. What remains forbidden is
      // every *gameplay system* the stage does not implement — resolution,
      // combat, and the later-stage systems (§2.0.5.3, §2.2).
      //
      // `matchcount` is deliberately NOT in this list, and `combo` is deliberately
      // NOT either: both are documented GAME_STATE.md §2.2 fields, and the client
      // carries them because they are `BattleState` fields. `MATCH3_RULES.md` §6.6
      // item 3 makes rendering them the client's own job while computing them
      // remains the server's (`GAME_RULES.md` §18). The tests below assert the
      // stronger property that matters: the runtime never *derives* either value.
      const forbidden = [
        'damage',
        'match3',
        'cascade',
        'passive',
        'boss',
        'relic',
        'crit',
        'gravity',
        'detonate',
        'hp',
        'power',
      ];

      for (const term of forbidden) {
        // The term may appear in prose-adjacent identifiers only if it is part
        // of a negative assertion comment; comments are already stripped, so any
        // occurrence here is real code.
        expect(code, `${file} must not reference "${term}"`).not.toContain(term);
      }
    });

    it.each(runtimeFiles)('%s derives no Match or Combo value from anything', (file) => {
      const code = stripComments(readSource(file));

      // GAME_STATE.md §2.2 / GAME_RULES.md §18: `MatchCount` and `Combo` are
      // authoritative server state. The client carries the delivered values and
      // renders them; it never counts a Match, advances a Combo, resets one, or
      // re-derives either from the board, `turn`, or `sequence`.
      for (const term of [
        'MatchCount++',
        'matchCount++',
        'combo++',
        'combo = 0',
        'Combo = 0',
        'Passes',
        'ClearCells',
      ]) {
        expect(code, `${file} must not reference "${term}"`).not.toContain(term);
      }
    });

    it.each(runtimeFiles)('%s resolves no matches and generates no board', (file) => {
      const code = stripComments(readSource(file));

      // The runtime transports and stores the authoritative board; it never
      // produces one and never evaluates a match over one
      // (SIGNALR_PROTOCOL.md §4 item 10, GAME_RULES.md §18).
      for (const term of ['Math.random', 'HasMatch', 'FindMatch', 'ResolveSwap', 'HasValidSwap']) {
        expect(code, `${file} must not reference "${term}"`).not.toContain(term);
      }
    });

    it('the runtime does not invent battle event names', () => {
      const code = stripComments(readSource(join('game', 'runtime', 'GameRuntimeEvents.ts')));

      // GAME_EVENTS.md owns event names; the runtime must not enumerate them.
      const battleEventNames = [
        'BattleStarted',
        'SwapStarted',
        'SwapResolved',
        'MatchCreated',
        'MatchResolved',
        'CascadeCreated',
        'ComboChanged',
        'GemMatched',
        'PowerChanged',
        'PassiveCharged',
        'PassiveTriggered',
        'RelicTriggered',
        'DamageCalculated',
        'DamageDealt',
        'DamageTaken',
        'BossSkillCast',
        'TurnStarted',
        'TurnEnded',
        'BattleWon',
        'BattleLost',
      ];

      for (const name of battleEventNames) {
        expect(code, `must not declare "${name}"`).not.toContain(name);
      }
    });

    it('the client exposes no gameplay Hub methods', () => {
      const code = stripComments(readSource(join('services', 'realtime', 'SignalRService.ts')));

      // SIGNALR_PROTOCOL.md §2/§7 own these; implementing them is gameplay.
      // `JoinBattle` (§1.2) is the only client → server method the foundation
      // adds, and it is a group join, not a gameplay action.
      expect(code).not.toMatch(/invoke<[^>]*>\('Swap'/);
      expect(code).not.toMatch(/'CardCast'/);
      expect(code).not.toMatch(/'PetSkillCast'/);
      expect(code).not.toMatch(/'GetBattleState'/);
      expect(code).toMatch(/'JoinBattle'/);
    });

    it('the runtime declares no undocumented state-sync method', () => {
      // SIGNALR_PROTOCOL.md §4: `BattleStateUpdated` is the only state-push
      // method. No second or parallel state-sync method exists.
      const code = stripComments(readSource(join('game', 'runtime', 'GameRuntime.ts')));

      for (const invented of ['GameStateSync', 'SyncEverything', 'GameStateChanged', 'BattleReady']) {
        expect(code, `must not reference "${invented}"`).not.toContain(invented);
      }
    });

    it('the client models no battle Status or lifecycle value', () => {
      // GAME_STATE.md §2.0.3, SIGNALR_PROTOCOL.md §8.3: no Status field and no
      // READY/STARTING/ACTIVE/PAUSED/FINISHED/WON/LOST lifecycle exists.
      const files = [
        join('game', 'runtime', 'GameRuntime.ts'),
        join('game', 'runtime', 'GameRuntimeEvents.ts'),
        join('services', 'realtime', 'SignalRService.ts'),
      ];

      for (const file of files) {
        const code = stripComments(readSource(file));
        for (const invented of ['READY', 'STARTING', 'PAUSED', 'FINISHED']) {
          expect(code, `${file} must not declare "${invented}"`).not.toContain(invented);
        }
      }
    });
  });

  describe('React and Phaser stay on their side of the boundary', () => {
    it('React UI does not import Phaser', () => {
      const uiFiles = walk(join(SRC, 'ui'));

      for (const file of uiFiles) {
        const code = stripComments(readFileSync(file, 'utf8'));
        expect(code, `${file} must not import phaser`).not.toMatch(/from 'phaser'/);
      }
    });

    it('Phaser scenes do not import React', () => {
      const sceneFiles = walk(join(SRC, 'game', 'scenes'));

      for (const file of sceneFiles) {
        const code = stripComments(readFileSync(file, 'utf8'));
        expect(code, `${file} must not import react`).not.toMatch(/from 'react'/);
      }
    });

    it('the runtime is engine-agnostic and does not import Phaser', () => {
      const code = stripComments(readSource(join('game', 'runtime', 'GameRuntime.ts')));
      expect(code).not.toMatch(/from 'phaser'/);
    });
  });

  describe('the frontend stays on the approved stack', () => {
    it('adds no unauthorized state or game libraries', () => {
      const pkg = JSON.parse(
        readFileSync(resolve(__dirname, '../package.json'), 'utf8')
      ) as { dependencies: Record<string, string>; devDependencies: Record<string, string> };

      const installed = [
        ...Object.keys(pkg.dependencies),
        ...Object.keys(pkg.devDependencies),
      ];

      const forbidden = ['redux', 'zustand', 'mobx', 'colyseus', 'xstate', 'pixi.js', 'phaser3'];

      for (const name of forbidden) {
        expect(installed).not.toContain(name);
      }
    });

    it('keeps phaser pinned to the documented major version', () => {
      const pkg = JSON.parse(
        readFileSync(resolve(__dirname, '../package.json'), 'utf8')
      ) as { dependencies: Record<string, string> };

      expect(pkg.dependencies.phaser).toMatch(/^4\./);
    });
  });
});