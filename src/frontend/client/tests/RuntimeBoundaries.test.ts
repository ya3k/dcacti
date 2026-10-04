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
  describe('Phaser is not coupled to SignalR or to any other transport', () => {
    // TASK-078: `LobbyScene` is a scene like any other and is covered by the same
    // rules. The list is the registered scene set (GameConfig.ts), so a new
    // scene is covered as soon as it exists.
    const sceneFiles = [
      'BootScene.ts',
      'PreloaderScene.ts',
      'MainMenuScene.ts',
      'LobbyScene.ts',
      'BattleScene.ts',
      'ResultScene.ts',
    ].map((f) => join('game', 'scenes', f));

    it.each(sceneFiles)('%s imports no transport client', (file) => {
      const code = stripComments(readSource(file));

      // ARCHITECTURE.md §2.2.1 rule 1 (restated transport-general by §2.2.3
      // rule 3): a scene depends on the runtime port, never on the transport —
      // SignalR, `fetch`, or `ApiService`.
      expect(code).not.toMatch(/@microsoft\/signalr/);
      expect(code).not.toMatch(/HubConnection/);
      expect(code).not.toMatch(/services\/realtime/);

      // A scene names the wire MODEL types it moves through the port (they are
      // owned by `services/api/` and are not redefined for it — §2.2.3 rule 6),
      // but it imports no transport implementation, so the only `services/api/`
      // imports it may contain are the type-only model files.
      const apiImports = [...code.matchAll(/from '[^']*services\/api\/([^']+)'/g)].map(
        (match) => match[1]
      );
      for (const imported of apiImports) {
        expect(['BattleModels', 'CollectionModels']).toContain(imported);
      }

      // No HTTP client and no Discord SDK in a scene.
      expect(code).not.toMatch(/\bfetch\s*\(/);
      expect(code).not.toMatch(/embedded-app-sdk/);
      expect(code).not.toMatch(/services\/discord/);
    });

    it('the scene list under test is the registered scene list', () => {
      // Guards against a scene being added to GameConfig.ts without being
      // covered by the boundary assertions above.
      const config = stripComments(readSource(join('game', 'GameConfig.ts')));
      const registered = [...config.matchAll(/import \{ (\w+Scene) \} from '\.\/scenes\/(\w+Scene)'/g)]
        .map((match) => `${match[2]}.ts`)
        .sort();

      expect(registered).toEqual([...sceneFiles].map((f) => f.split(/[\\/]/).pop()).sort());
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
      // (GAME_STATE.md §2.0.5), `playerState`'s `matchCount`/`combo` from the
      // Match / Combo accounting stage (§2.2), and `petState`'s delivered members
      // from the Pet / Passive stage (§2.3) — so those names are part of the
      // documented contract rather than violations. What remains forbidden is
      // every *gameplay system* the stage does not implement — resolution,
      // combat, and the later-stage systems (§2.0.5.3, §2.2, §2.3).
      //
      // `matchcount` is deliberately NOT in this list, and `combo` is deliberately
      // NOT either: both are documented GAME_STATE.md §2.2 fields, and the client
      // carries them because they are `BattleState` fields. `MATCH3_RULES.md` §6.6
      // item 3 makes rendering them the client's own job while computing them
      // remains the server's (`GAME_RULES.md` §18). `passiveid`, `passiveprogress`,
      // and `passiveresetoverride` are likewise NOT in it: SIGNALR_PROTOCOL.md
      // §4.3 makes exactly those members part of the delivered `petState`, and
      // PASSIVE_RULES.md §6 item 1 requires the progress pair to reach the client
      // as a UI-facing value. `statuseffects` and `bossstate` are NOT in it
      // either, for the same reason: §4.3 item 14 delivers the active Pet's active
      // instances (TASK-160 D-1A) and §4.4 delivers the Boss's `hp`/`maxHp`
      // (TASK-160 D-2A), so both are documented contract members — and the
      // dedicated assertions below pin that the runtime renders them without
      // computing them. The tests below assert the stronger property that
      // matters: the runtime never *derives* any of these values.
      //
      // `boss` is likewise NOT in it any more, for the same reason as
      // `statuseffects`: §4.4's `bossState` is a delivered projection, so the
      // runtime legitimately names it. What stays forbidden is Boss *gameplay* —
      // selection, and any Boss value beyond the two delivered numbers — which the
      // assertion at "carries no Boss source of its own" pins.
      //
      // TASK-078 stage advance. `boss` and `relic` were in this list because the
      // runtime carried no Boss or Relic concept at all; ARCHITECTURE.md §2.2.3
      // rules 3–6 now make the *collection read* a documented port capability, so
      // `getRelics()` and the `RelicResponse` wire type it transports are part of
      // the contract rather than a violation. What remains forbidden is what
      // always was: a gameplay system. The runtime carries no Boss selection —
      // since TASK-185 the *scene* holds the player's Boss choice and submits the
      // selected identity as the request's `bossId`, which is transport data the
      // runtime forwards — and it does not
      // equip, trigger, or evaluate a Relic — it transports the owned-instance
      // list the server returned. The dedicated assertions below pin exactly
      // that, so widening this list cannot hide a failure to the generic term
      // scan.
      const forbidden = [
        'damage',
        'match3',
        'cascade',
        'crit',
        'gravity',
        'detonate',
        'power',
      ];

      for (const term of forbidden) {
        // The term may appear in prose-adjacent identifiers only if it is part
        // of a negative assertion comment; comments are already stripped, so any
        // occurrence here is real code.
        expect(code, `${file} must not reference "${term}"`).not.toContain(term);
      }
    });

    it.each(runtimeFiles)('%s carries no Boss source of its own', (file) => {
      const code = stripComments(readSource(file));

      // ARCHITECTURE.md §2.2.3 rule 6 + BOSS_RULES.md §6.4: the client holds no
      // Boss source in the runtime — no Boss collection read and no Boss
      // selection state anywhere in it, so no Boss identity can reach the wire
      // through the runtime. Since TASK-185 the Lobby's own static selection
      // catalog supplies the submitted `bossId`; that is scene state, and the
      // runtime only transports the request it is handed.
      for (const term of [
        'getBoss',
        'getBosses',
        'BossResponse',
        'bossList',
        'bossDefinition',
        'BossDefinition',
        'boss-hoa-long',
        'boss-thuy-ma',
        'boss-moc-yeu',
      ]) {
        expect(code, `${file} must not reference "${term}"`).not.toContain(term);
      }
    });

    it.each(runtimeFiles)('%s equips, triggers, and evaluates no Relic', (file) => {
      const code = stripComments(readSource(file));

      // RELIC_RULES.md §2–§5 / GAME_RULES.md §18: the Relic trigger engine and the
      // battle-scoped equip snapshot are the server's. The runtime transports the
      // owned-instance read and has no equip model, no slot ordering, and no
      // trigger evaluation of its own.
      for (const term of [
        'EquippedRelics',
        'equippedRelics',
        'RelicTrigger',
        'TriggerRelic',
        'triggerRelic',
        'EvaluateRelic',
        'equipRelic',
        'loadoutPosition',
        'isEquipped',
      ]) {
        expect(code, `${file} must not reference "${term}"`).not.toContain(term);
      }
    });

    it.each(runtimeFiles)('%s applies, expires, and evaluates no Status Effect', (file) => {
      const code = stripComments(readSource(file));

      // SIGNALR_PROTOCOL.md §4.3 item 14 / GAME_STATE.md §5.1.1 / COMBAT_RULES.md
      // §5: the Status Effect lifecycle — apply, refresh, consume at step 19a,
      // expire, and remove — is the server's. The runtime stores and exposes the
      // delivered instances and never mutates or derives them
      // (GAME_RULES.md §18, ADR-001).
      //
      // `statusEffects` itself is deliberately NOT in this list: §4.3 item 14
      // makes it a delivered contract member, so naming it is required rather
      // than forbidden. What stays forbidden is the gameplay: the runtime must
      // not decrement a countdown, evaluate an expiry condition, or build a
      // second, client-side Status Effect system.
      for (const term of [
        'StatusEffectLifecycle',
        'ApplyStatusEffect',
        'applyStatusEffect',
        'RefreshStatusEffect',
        'refreshStatusEffect',
        'ExpireStatusEffect',
        'expireStatusEffect',
        'RemoveStatusEffect',
        'removeStatusEffect',
        'TickStatusEffect',
        'tickStatusEffect',
        'ConsumeStatusEffect',
        'remainingTurns--',
        'remainingTurns -=',
        'remainingTurns++',
        'remainingTurns +=',
      ]) {
        expect(code, `${file} must not reference "${term}"`).not.toContain(term);
      }

      // The delivered countdown and magnitude are only ever read and copied, never
      // computed with: no arithmetic operator may touch them (`GAME_STATE.md`
      // §5.1.1 item 2's single step-19a decrement is the server's). A plain copy
      // assignment inside the reader is the read being stored, not a derivation.
      expect(code, `${file} must not compute with the delivered remainingTurns`).not.toMatch(
        /\.remainingTurns\s*(\+\+|--|\+=|-=|\*=|\/=)/
      );
      expect(code, `${file} must not compute with the delivered magnitude`).not.toMatch(
        /\.magnitude\s*(\+\+|--|\+=|-=|\*=|\/=)/
      );
    });

    it.each(runtimeFiles)('%s damages, clamps, and derives no Boss HP', (file) => {
      const code = stripComments(readSource(file));

      // SIGNALR_PROTOCOL.md §4.4 item 7 / GAME_STATE.md §2.4 / GAME_RULES.md §18:
      // `bossState`'s `hp`/`maxHp` are authoritative server state made
      // renderable, not client-side Boss logic. The runtime renders the two
      // numbers it was sent.
      //
      // `bossState`, `hp`, and `maxHp` themselves are NOT in this list: §4.4
      // makes them delivered contract members, so naming them is required. What
      // stays forbidden is the computation — damaging the Boss, clamping `hp` to
      // `maxHp`, inferring either from the other, or re-deriving them from the
      // events or `finalBossHp`.
      for (const term of [
        'ApplyDamageToBoss',
        'DamageBoss',
        'damageBoss',
        'BossHp --',
        'bossHp--',
        'ComputedBossHp',
        'derivedBossHp',
        'PredictedBossHp',
        'RemainingBossHpPercentage',
      ]) {
        expect(code, `${file} must not reference "${term}"`).not.toContain(term);
      }

      // The delivered Boss values are copied through, never computed with: no
      // arithmetic may touch `hp` or `maxHp`, so neither can be damaged, clamped,
      // or re-derived from the other (§4.4 items 5 and 7).
      expect(code, `${file} must not compute with the delivered Boss hp`).not.toMatch(
        /\.hp\s*(\+\+|--|\+=|-=|\*=|\/=)/
      );
      expect(code, `${file} must not compute with the delivered Boss maxHp`).not.toMatch(
        /\.maxHp\s*(\+\+|--|\+=|-=|\*=|\/=)/
      );
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

    it.each(runtimeFiles)('%s charges, evaluates, and resets no Passive', (file) => {
      const code = stripComments(readSource(file));

      // SIGNALR_PROTOCOL.md §4.3 item 9 / PASSIVE_RULES.md §2–§5: the Passive's
      // charge, threshold, trigger, and reset rules are the server's. The runtime
      // stores and exposes the delivered `current / threshold` pair and the
      // identity; it never charges a Passive, evaluates a Threshold, resets
      // progress, or applies an overflow — that would be a second, client-side
      // Passive system (GAME_RULES.md §18, ADR-001).
      for (const term of [
        'PassiveTracker',
        'ChargePassive',
        'chargePassive',
        'passiveProgress.current++',
        'passiveProgress.current =',
        'current >= threshold',
        'current > threshold',
        'ResetPassive',
        'resetPassive',
        'ApplyOverflow',
        'applyOverflow',
        'PassiveTriggered(',
      ]) {
        expect(code, `${file} must not reference "${term}"`).not.toContain(term);
      }

      // The progress pair is only ever read, never written: no assignment to
      // either member exists outside the reader that copies the delivered value.
      expect(code, `${file} must not assign the delivered current`).not.toMatch(
        /\.current\s*(\+\+|--|\+=|-=|=)/
      );
      expect(code, `${file} must not assign the delivered threshold`).not.toMatch(
        /\.threshold\s*(\+\+|--|\+=|-=|=)/
      );
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

    it('the client invokes exactly the documented gameplay Hub methods', () => {
      const code = stripComments(readSource(join('services', 'realtime', 'SignalRService.ts')));

      // TASK-069 stage advance. This assertion previously read "the client
      // exposes no gameplay Hub methods", because the runtime foundation stage
      // implemented only `JoinBattle`. The Swap contract already existed in
      // SIGNALR_PROTOCOL.md §2.1 and on the server (BattleHub.Swap) and had no
      // client half; TASK-069 implements that half, so `'Swap'` now IS invoked.
      //
      // TASK-144 stage advance. This assertion previously also required
      // `GetBattleState` to be absent, because §7's snapshot request had no
      // client half. TASK-143 implemented the server method and TASK-144
      // implements the client invocation (§7.1), so `'GetBattleState'` now IS
      // invoked — the historical TASK-120 *assertion* is superseded by the
      // current authoritative protocol, not TASK-120 itself (completed tasks
      // are immutable, TASK_LIFECYCLE.md §3).
      //
      // Every property that still holds is preserved: the other documented
      // gameplay methods stay absent, and `JoinBattle` stays present.
      const invoked = [...code.matchAll(/invoke(?:<[^>]*>)?\(\s*'([^']+)'/g)].map((m) => m[1]);
      expect(invoked.sort()).toEqual([
        'CardCast',
        'GetBattleState',
        'JoinBattle',
        'PetSkillCast',
        'Ping',
        'Swap',
      ]);

      // `Swap`, `CardCast`, and `PetSkillCast` are the documented client → server
      // gameplay methods (SIGNALR_PROTOCOL.md §2, §2.1).
      expect(code).toMatch(/invoke<[^>]*>\(\s*'Swap'/);
      expect(code).toMatch(/invoke<[^>]*>\(\s*'CardCast'/);
      expect(code).toMatch(/invoke<[^>]*>\(\s*'PetSkillCast'/);

      // `GetBattleState` (§7.1) is the documented reconnect/resync snapshot
      // request. It is a request/response read, not a gameplay action, and it
      // is the runtime — not a scene — that issues it.
      expect(code).toMatch(/invoke<[^>]*>\(\s*'GetBattleState'/);

      // `JoinBattle` (§1.2) is still the documented group join, and it is not a
      // gameplay action.
      expect(code).toMatch(/'JoinBattle'/);
    });

    it('BattleScene stays independent of the transport, GetBattleState included', () => {
      // SIGNALR_PROTOCOL.md §7 does not move the boundary: the recovered
      // snapshot still reaches the scene through `runtime.onBattleState(...)`,
      // so the scene names no hub method and imports no transport. The scene
      // coverage in "Phaser is not coupled to SignalR or to any other
      // transport" asserts the imports; this pins the method-level half.
      const code = stripComments(readSource(join('game', 'scenes', 'BattleScene.ts')));

      expect(code).not.toMatch(/'GetBattleState'/);
      expect(code).not.toMatch(/SignalRService/);
      expect(code).toMatch(/onBattleState/);
    });

    it('the runtime port carries capabilities, not models', () => {
      // ARCHITECTURE.md §2.2.3 rule 6: the port exposes the start and collection
      // read capabilities; it does not define, re-export, or own the collection
      // read models or any loadout/selection type. Those wire shapes stay in
      // `services/api/`, and the in-progress selection belongs to the scene.
      const code = stripComments(readSource(join('game', 'runtime', 'GameRuntimeEvents.ts')));

      // It DECLARES no such type...
      expect(code).not.toMatch(/export (interface|type) \w*(Loadout|Selection|Collection)\w*/);

      // ...and it carries only the documented capabilities.
      const port = code.slice(
        code.indexOf('export interface GameRuntimePort'),
        code.indexOf('RuntimeActionNotImplementedError')
      );
      const members = [...port.matchAll(/^ {2}(\w+)\(/gm)].map((match) => match[1]).sort();
      expect(members).toEqual([
        // TASK-149 stage advance: `getBattleResult` is the documented result
        // read (`API_CONTRACTS.md` §4) `ResultScene` renders the persisted
        // reward summary from. It is a capability, like the collection reads —
        // it declares no model and re-exports nothing, which the assertions
        // below this list still enforce.
        'getBattleResult',
        'getBattleState',
        'getCards',
        'getPet',
        'getPets',
        'getRelics',
        'getState',
        'onBattleEvents',
        'onBattleState',
        'onRuntimeEvent',
        'requestAction',
        'startBattle',
      ]);

      // The model types it names are imported as types from their owner, never
      // re-declared or re-exported. A re-export would make the port a second
      // definition of a wire shape `services/api/` already owns.
      expect(code).not.toMatch(/export type \{/);
      expect(code).toMatch(
        /import type \{ BattleResultResponse, BattleStartRequest \} from '\.\.\/\.\.\/services\/api\/BattleModels'/
      );
      expect(code).toMatch(
        /import type \{ CardResponse, PetResponse, RelicResponse \} from '\.\.\/\.\.\/services\/api\/CollectionModels'/
      );
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