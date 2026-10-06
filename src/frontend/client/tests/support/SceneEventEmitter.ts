/**
 * The scene's own event emitter — Phaser's `scene.events` / `scene.sys.events`.
 *
 * TASK-204's subject is *when* a scene's teardown runs, so the suites have to be
 * able to raise the events the engine actually raises. Phaser takes a scene down
 * through this emitter (`Phaser.Scenes.Systems#shutdown` emits
 * `Phaser.Scenes.Events.SHUTDOWN`; `#destroy` emits `DESTROY`) and never invokes
 * a scene method merely because the method exists, so a harness that lets a test
 * call `shutdown()` by name would model the wrong lifecycle — which is exactly
 * how the defect this module guards against went unnoticed.
 *
 * The `on` / `once` / `off` / `emit` semantics mirror Phaser's emitter
 * (`eventemitter3`): a `once` listener is dropped before it is invoked, and a
 * dispatch iterates the listener list it started with.
 */
export class SceneEventEmitter {
  private handlers = new Map<
    string,
    Array<{ fn: (...args: unknown[]) => void; context: unknown; once: boolean }>
  >();

  on(event: string, fn: (...args: unknown[]) => void, context?: unknown): this {
    return this.add(event, fn, context, false);
  }

  once(event: string, fn: (...args: unknown[]) => void, context?: unknown): this {
    return this.add(event, fn, context, true);
  }

  off(event: string, fn?: (...args: unknown[]) => void, context?: unknown): this {
    if (fn === undefined) {
      this.handlers.delete(event);
      return this;
    }

    this.handlers.set(
      event,
      (this.handlers.get(event) ?? []).filter(
        (entry) => !(entry.fn === fn && entry.context === context)
      )
    );
    return this;
  }

  emit(event: string, ...args: unknown[]): boolean {
    const entries = [...(this.handlers.get(event) ?? [])];
    for (const entry of entries) {
      if (entry.once) {
        this.off(event, entry.fn, entry.context);
      }
      entry.fn.apply(entry.context, args);
    }
    return entries.length > 0;
  }

  listenerCount(event: string): number {
    return (this.handlers.get(event) ?? []).length;
  }

  private add(
    event: string,
    fn: (...args: unknown[]) => void,
    context: unknown,
    once: boolean
  ): this {
    const list = this.handlers.get(event) ?? [];
    list.push({ fn, context, once });
    this.handlers.set(event, list);
    return this;
  }
}

/**
 * The `Phaser.Scenes.Events` constants a scene attaches teardown to, as the
 * string values Phaser 4.2.1's own `src/scene/events/*` modules define
 * (`SHUTDOWN_EVENT = 'shutdown'`, `DESTROY_EVENT = 'destroy'`).
 */
export const SCENE_EVENT_SHUTDOWN = 'shutdown';
export const SCENE_EVENT_DESTROY = 'destroy';
