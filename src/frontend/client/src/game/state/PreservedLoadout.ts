import type * as Phaser from 'phaser';
import type { BattleStartRequest } from '../../services/api/BattleModels';

/**
 * Registry key under which the preserved pre-battle loadout is published to the
 * Phaser game instance (ADR-022, `ARCHITECTURE.md` §2.2.3).
 *
 * It is a second key in the game-wide registry (`game.registry`, a
 * `Phaser.Data.DataManager`) — the same mechanism `RuntimeRegistry.ts` uses to
 * publish the runtime — and it is deliberately distinct from
 * `RUNTIME_REGISTRY_KEY`: the preserved loadout is not the runtime and the
 * runtime must not hold it (`ARCHITECTURE.md` §2.2.3 rule 2).
 */
export const PRESERVED_LOADOUT_REGISTRY_KEY = 'preservedLoadout';

/**
 * The post-result preserved-loadout carrier (ADR-022).
 *
 * `D-202-03 = D` requires the loadout used in the battle that just ended to
 * still be selected when `LobbyScene` is entered again through
 * `ResultScene → PLAY AGAIN`, while remaining fully editable. The in-progress
 * selection is otherwise ephemeral `LobbyScene`-local state
 * (`ARCHITECTURE.md` §2.2.3 rule 1) that is discarded when the scene shuts
 * down, so the approved decision needs exactly one carrier that outlives a
 * `LobbyScene` instance. This is it.
 *
 * ```text
 * LobbyScene start succeeds   → preserveLoadout(scene, submittedRequest)
 * ResultScene PLAY AGAIN      → LobbyScene starts with the restore flag
 *                             → readPreservedLoadout(scene)
 * explicit                    → clearPreservedLoadout(scene)
 * ```
 *
 * **What it is.** The player's not-yet-submitted choice of one Pet, one Boss,
 * and the Card and Relic sets for the UPCOMING battle — exactly the four
 * documented `BattleStartRequest` members (`API_CONTRACTS.md` §3) of the
 * loadout most recently submitted. Preserving it changes that selection's
 * **lifetime**, never its nature: it stays client presentation state, is never
 * authoritative, and becomes authoritative only when the server validates it
 * inside `POST /api/battle/start` (`GAME_RULES.md` §18, ADR-001). No loadout,
 * selection, or collection type is declared here — it holds the wire request
 * the client already builds, so it is no second spelling of a loadout shape
 * (`ARCHITECTURE.md` §2.2.3 rule 6).
 *
 * **Owner and lifetime.** The client game-presentation layer owns it, through
 * the Phaser game instance that owns the registry and the scene manager. It
 * survives every scene `shutdown()`/`start()` within one running game and ends
 * with that game instance; nothing about it is persisted to `localStorage`,
 * `sessionStorage`, the URL, the backend, or a database. That lifetime is also
 * a boundary: the game instance exists only for an authenticated session and is
 * destroyed on unmount (`GameShell` → `PhaserGame`), so a preserved loadout
 * cannot cross a session boundary.
 *
 * **What it must never be.** Not gameplay state (not `BattleState`, `PetState`,
 * or any server-owned value), not technical runtime state (never on
 * `state/GameRuntimeState.ts`), not a second source of truth for the owned
 * collection, and not a store or state-management framework
 * (`ARCHITECTURE.md` §2.2.3, §5.5, `AGENTS.md` §9).
 *
 * **Cleanup.** The `D-202-04 = A` active-battle cleanup does **not** clear this
 * carrier: the preserved loadout is not active battle state, and clearing it
 * would make `D-202-03 = D` impossible. It is overwritten by the next
 * successful battle start and can be dropped explicitly with
 * {@link clearPreservedLoadout}. The converse also holds: preserving the
 * loadout licenses retaining no stale battle state.
 */

/**
 * Publishes `loadout` as the preserved pre-battle selection of this game
 * instance, replacing any previous one.
 *
 * Called by `LobbyScene` only when a battle start has succeeded, so the
 * preserved loadout is exactly the one the battle that just ended is fought
 * with. A rejected start preserves nothing (`ARCHITECTURE.md` §2.2.3 rule 5).
 *
 * The carrier owns its own copy: the caller's arrays are copied in, so a later
 * edit in the scene can never rewrite the preserved selection behind its back.
 */
export function preserveLoadout(scene: Phaser.Scene, loadout: BattleStartRequest): void {
  scene.registry.set(PRESERVED_LOADOUT_REGISTRY_KEY, {
    petId: loadout.petId,
    bossId: loadout.bossId,
    cardLoadout: [...loadout.cardLoadout],
    relicLoadout: [...loadout.relicLoadout],
  });
}

/**
 * The preserved pre-battle selection of this game instance, or `null` when
 * none has been preserved.
 *
 * A fresh copy is returned each call, so the caller can edit its restored
 * selection freely without mutating what the carrier holds. Reading is not
 * restoring: only `LobbyScene` on the approved `ResultScene → PLAY AGAIN`
 * entry consults this (`ARCHITECTURE.md` §2.2.3, ADR-022). Restoring is a
 * starting point, never a lock and never a legality decision — the scene
 * restores the ids verbatim and the server validates the submitted request.
 */
export function readPreservedLoadout(scene: Phaser.Scene): BattleStartRequest | null {
  const preserved = scene.registry.get(PRESERVED_LOADOUT_REGISTRY_KEY) as
    | BattleStartRequest
    | undefined;

  if (preserved === undefined) {
    return null;
  }

  return {
    petId: preserved.petId,
    bossId: preserved.bossId,
    cardLoadout: [...preserved.cardLoadout],
    relicLoadout: [...preserved.relicLoadout],
  };
}

/**
 * Drops the preserved pre-battle selection.
 *
 * This is the carrier's only explicit clearing rule, and it is deliberately
 * **not** part of the post-result active-battle cleanup: `clearActiveBattleState`
 * must leave the preserved loadout available (`ARCHITECTURE.md` §2.2.3,
 * ADR-022).
 */
export function clearPreservedLoadout(scene: Phaser.Scene): void {
  scene.registry.remove(PRESERVED_LOADOUT_REGISTRY_KEY);
}
