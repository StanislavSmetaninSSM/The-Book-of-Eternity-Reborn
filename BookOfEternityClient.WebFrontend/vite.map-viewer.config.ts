import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

/**
 * Separate Vite config that builds ONLY the standalone map viewer as a
 * self-contained IIFE bundle (React + MapAtlas + map-atlas.css, inlined).
 *
 * Output is a single file written to
 *   ../BookOfEternityClient/Assets/MapViewer/map-viewer-bundle.js
 * which the C# project embeds as a resource (see BookOfEternityClient.csproj)
 * and LocalMapViewerRenderer.BuildStandaloneHtml inlines into map_viewer.html.
 *
 * Run via:  npm run build:map-viewer   (scripts/build-map-viewer.mjs)
 *
 * This is the unification pivot: the same MapAtlas React component that the
 * embedded browser client uses is the one that renders the console /map HTML,
 * so the two surfaces can never drift. A source-guard test asserts that the
 * standalone entry imports MapAtlas, preventing a second renderer from
 * reappearing.
 */
export default defineConfig({
  plugins: [react()],
  build: {
    // Output is consumed from a tracked path; do not empty the parent dir.
    emptyOutDir: false,
    cssCodeSplit: false,
    assetsInlineLimit: Number.MAX_SAFE_INTEGER,
    // Vite 8 ships the oxc-based minifier by default; 'esbuild' would require
    // the (here-uninstalled) esbuild package. Keep the default so the bundle
    // build works with the project's existing dependency set.
    minify: true,
    lib: {
      entry: 'src/mapViewerStandalone.tsx',
      formats: ['iife'],
      name: 'BookOfEternityMapBundle',
      fileName: () => 'map-viewer-bundle.js'
    },
    rollupOptions: {
      output: {
        dir: '../BookOfEternityClient/Assets/MapViewer',
        inlineDynamicImports: true,
        entryFileNames: 'map-viewer-bundle.js',
        assetFileNames: 'map-viewer-bundle.[ext]'
      }
    }
  }
});
