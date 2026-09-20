import React, { useEffect, useRef } from 'react';
import * as Phaser from 'phaser';
import { createGameConfig } from './GameConfig';

interface PhaserGameProps {
  onInitialized?: () => void;
}

/**
 * Mounts the Phaser 4 game into the game shell.
 *
 * The game instance is created exactly once and destroyed on unmount. Viewport
 * changes are handled by Phaser's Scale Manager, which observes this element —
 * the component installs no window resize listener and never resizes the canvas
 * or the document manually.
 */
export const PhaserGame: React.FC<PhaserGameProps> = ({ onInitialized }) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const gameRef = useRef<Phaser.Game | null>(null);

  useEffect(() => {
    if (containerRef.current && !gameRef.current) {
      const config = createGameConfig(containerRef.current);
      const game = new Phaser.Game(config);
      gameRef.current = game;

      if (onInitialized) {
        onInitialized();
      }
    }

    return () => {
      if (gameRef.current) {
        gameRef.current.destroy(true);
        gameRef.current = null;
      }
    };
  }, [onInitialized]);

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