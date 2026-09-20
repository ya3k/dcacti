import React, { useEffect, useRef } from 'react';
import * as Phaser from 'phaser';
import { createGameConfig } from './GameConfig';
import { GameRuntime } from './runtime/GameRuntime';

interface PhaserGameProps {
  /**
   * The game runtime the scenes coordinate through. Created and owned by the
   * application shell so React and Phaser share exactly one instance.
   */
  runtime: GameRuntime;
  onInitialized?: () => void;
}

/**
 * Mounts the Phaser 4 game into the game shell.
 *
 * The game instance is created exactly once and destroyed on unmount. Viewport
 * changes are handled by Phaser's Scale Manager, which observes this element —
 * the component installs no window resize listener and never resizes the canvas
 * or the document manually.
 *
 * React never reaches into Phaser internals: it mounts the canvas and hands the
 * game the shared runtime. Phaser never touches React components. The
 * `GameRuntime` is that integration boundary (task §17).
 *
 * React StrictMode intentionally double-invokes effects in development. The
 * cleanup below destroys the game and clears the ref, so the second run creates
 * exactly one new instance and no duplicate canvas is left behind (task §20).
 */
export const PhaserGame: React.FC<PhaserGameProps> = ({ runtime, onInitialized }) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const gameRef = useRef<Phaser.Game | null>(null);

  // Kept in a ref so a changing callback identity never re-runs the effect and
  // therefore never recreates the Phaser instance.
  const onInitializedRef = useRef(onInitialized);
  useEffect(() => {
    onInitializedRef.current = onInitialized;
  }, [onInitialized]);

  useEffect(() => {
    if (containerRef.current && !gameRef.current) {
      const config = createGameConfig(containerRef.current, runtime);
      const game = new Phaser.Game(config);
      gameRef.current = game;

      onInitializedRef.current?.();
    }

    return () => {
      if (gameRef.current) {
        gameRef.current.destroy(true);
        gameRef.current = null;
      }
    };
  }, [runtime]);

  return (
    <div
      ref={containerRef}
      id="phaser-container"
      data-testid="phaser-container"
      style={{
        width: '100%',
        height: '100%',
        overflow: 'hidden',
      }}
    />
  );
};