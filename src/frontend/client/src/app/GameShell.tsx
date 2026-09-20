import React from 'react';
import { PhaserGame } from '../game/PhaserGame';

interface GameShellProps {
  /** Called once the Phaser game instance has been created. */
  onGameInitialized?: () => void;
  /**
   * React UI overlay rendered on top of the Phaser canvas. Gameplay HUD is not
   * implemented in this task — the shell only establishes the overlay layer.
   */
  children?: React.ReactNode;
}

/**
 * GameShell owns the available viewport.
 *
 * Hierarchy (see the task's viewport architecture):
 *
 *   GameShell
 *     ├── Phaser canvas   (absolutely positioned, fills the shell)
 *     └── React overlay   (absolutely positioned, must not affect document size)
 *
 * The shell is exactly 100% x 100% of the application root, never larger, and
 * clips anything that would otherwise overflow. It performs no resize handling
 * of its own: layout is driven by CSS, and Phaser's Scale Manager observes the
 * shell element directly.
 */
export const GameShell: React.FC<GameShellProps> = ({ onGameInitialized, children }) => {
  return (
    <div className="game-shell" data-testid="game-shell">
      <div className="game-shell__canvas" data-testid="game-shell-canvas">
        <PhaserGame onInitialized={onGameInitialized} />
      </div>

      {children ? (
        <div className="game-shell__overlay" data-testid="game-shell-overlay">
          {children}
        </div>
      ) : null}
    </div>
  );
};