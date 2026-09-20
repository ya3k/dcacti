import React, { useEffect, useState } from 'react';
import { GAME_WIDTH, GAME_HEIGHT, fitWithinViewport } from '../../game/GameViewport';

interface ViewportMetrics {
  viewportWidth: number;
  viewportHeight: number;
  canvasWidth: number;
  canvasHeight: number;
  scale: number;
  aspect: string;
}

function readMetrics(): ViewportMetrics {
  const viewportWidth = window.innerWidth;
  const viewportHeight = window.innerHeight;
  const { width, height, scale } = fitWithinViewport(viewportWidth, viewportHeight);

  return {
    viewportWidth,
    viewportHeight,
    canvasWidth: width,
    canvasHeight: height,
    scale,
    aspect: (viewportWidth / viewportHeight).toFixed(2),
  };
}

/**
 * Development-only viewport diagnostics.
 *
 * Reports the physical viewport and the logical game space so responsive
 * behavior can be verified by hand. This is intentionally debug tooling, not
 * production gameplay UI, and is only mounted when `import.meta.env.DEV` is true
 * (see App.tsx). Its own resize listener only updates this readout — it never
 * drives Phaser, which owns its own scaling.
 */
export const ViewportDebugOverlay: React.FC = () => {
  const [metrics, setMetrics] = useState<ViewportMetrics>(() => readMetrics());

  useEffect(() => {
    const onResize = () => setMetrics(readMetrics());
    window.addEventListener('resize', onResize);
    return () => window.removeEventListener('resize', onResize);
  }, []);

  return (
    <div className="viewport-debug" data-testid="viewport-debug">
      {`Viewport: ${metrics.viewportWidth} x ${metrics.viewportHeight}
Game:     ${GAME_WIDTH} x ${GAME_HEIGHT}
Canvas:   ${Math.round(metrics.canvasWidth)} x ${Math.round(metrics.canvasHeight)}
Scale:    ${metrics.scale.toFixed(2)}
Aspect:   ${metrics.aspect}`}
    </div>
  );
};