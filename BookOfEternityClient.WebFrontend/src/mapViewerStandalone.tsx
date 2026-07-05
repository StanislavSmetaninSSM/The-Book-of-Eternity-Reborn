/**
 * Standalone map viewer entry point.
 *
 * This is built to a self-contained IIFE bundle (see vite.map-viewer.config.ts
 * and scripts/build-map-viewer.mjs) and embedded into map_viewer.html by the
 * console `/map` command (LocalMapViewerRenderer.BuildStandaloneHtml in
 * LocalMapViewService.cs).
 *
 * It mounts the SAME MapAtlas React component used by the embedded browser
 * client, guaranteeing one renderer for both surfaces. The MapViewDto is
 * injected as a JSON script tag by BuildStandaloneHtml.
 */

import { createElement } from 'react';
import { createRoot } from 'react-dom/client';
import type { MapViewDto, UiMapBlock } from './api/contracts';
import { MapAtlas } from './components/map/MapAtlas';

declare global {
  interface Window {
    BookOfEternityMap?: {
      mount: (root: HTMLElement, map: MapViewDto, options?: { title?: string }) => void;
    };
  }
}

function readInjectedMap(): MapViewDto | null {
  const node = document.getElementById('map-viewer-data');
  if (!node) return null;
  try {
    const raw = node.textContent || '';
    return JSON.parse(raw) as MapViewDto;
  } catch {
    return null;
  }
}

function mount(root: HTMLElement, map: MapViewDto, options?: { title?: string }) {
  const block: UiMapBlock = {
    kind: 'map',
    title: options?.title || map.title || 'Карта',
    map
  };
  const container = document.createElement('div');
  root.appendChild(container);
  createRoot(container).render(createElement(MapAtlas, { block, variant: 'standalone' }));
}

window.BookOfEternityMap = { mount };

// Auto-mount when the page carries the injected data + a root element.
// BuildStandaloneHtml writes both; this keeps the standalone file a no-op
// until the data tag is present.
function autoMount() {
  const root = document.getElementById('map-viewer-root');
  if (!root) return;
  const map = readInjectedMap();
  if (!map) return;
  mount(root, map, { title: document.title });
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', autoMount, { once: true });
} else {
  autoMount();
}
