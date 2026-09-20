import React, { createContext, useContext, useEffect, useState } from 'react';
import { GameRuntime } from './GameRuntime';
import type { GameRuntimeState } from '../../state/GameRuntimeState';
import { INITIAL_RUNTIME_STATE } from '../../state/GameRuntimeState';

/**
 * React bridge to the game runtime.
 *
 * Boundary (task §17): React reads runtime status and renders UI; it does not
 * reach into Phaser internals. Phaser reads the runtime through this same port
 * and does not touch React. The single shared `GameRuntime` instance is the
 * integration boundary between them.
 */
const GameRuntimeContext = createContext<GameRuntime | null>(null);

/** Returns the runtime, or throws when used outside the provider. */
export function useGameRuntime(): GameRuntime {
  const runtime = useContext(GameRuntimeContext);

  if (runtime === null) {
    throw new Error('useGameRuntime must be used within a GameRuntimeProvider.');
  }

  return runtime;
}

/**
 * Subscribes to technical runtime state for presentation.
 *
 * React consumes runtime status through this hook rather than a state store:
 * `ARCHITECTURE.md` §5.5 keeps client state minimal and excludes heavy state
 * libraries. This is technical/session status only — never gameplay state.
 */
export function useGameRuntimeState(): GameRuntimeState {
  const runtime = useGameRuntime();
  const [state, setState] = useState<GameRuntimeState>(() => runtime.getState());

  useEffect(() => {
    // Sync immediately: the runtime may have transitioned before this
    // component mounted.
    setState(runtime.getState());

    return runtime.onRuntimeEvent((event) => {
      setState(event.state);
    });
  }, [runtime]);

  return state;
}

interface GameRuntimeProviderProps {
  runtime: GameRuntime;
  children: React.ReactNode;
}

export const GameRuntimeProvider: React.FC<GameRuntimeProviderProps> = ({
  runtime,
  children,
}) => {
  return (
    <GameRuntimeContext.Provider value={runtime}>{children}</GameRuntimeContext.Provider>
  );
};

/** Exported for tests that need to assert provider behaviour. */
export { GameRuntimeContext, INITIAL_RUNTIME_STATE };
export type { GameRuntimeState };