/**
 * Backwards-compatible re-export so callers that imported `MapBlock` (notably
 * BlockRenderer and the existing map render tests) keep working after the
 * unification. The single source of truth is now {@link MapAtlas}.
 *
 * `variant` defaults to `embedded` to preserve the in-app command-result
 * layout; the standalone viewer uses `MapAtlas` directly with `standalone`.
 */

import type { UiMapBlock } from '../api/contracts';
import { MapAtlas } from './map/MapAtlas';

export interface MapBlockProps {
  block: UiMapBlock;
  variant?: 'embedded' | 'standalone' | 'full' | 'compact';
}

export function MapBlock({ block, variant = 'embedded' }: MapBlockProps) {
  // Legacy callers passed 'full' / 'compact'. Only 'standalone' changes the
  // chrome, so anything else is treated as the embedded surface.
  const resolved = variant === 'standalone' ? 'standalone' : 'embedded';
  return <MapAtlas block={block} variant={resolved} />;
}
