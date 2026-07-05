// Builds the standalone map viewer IIFE bundle.
//
// This script runs `vite build` with the dedicated config
// (vite.map-viewer.config.ts) and then concatenates the inlined JS + CSS into
// a single self-contained file at
//   ../BookOfEternityClient/Assets/MapViewer/map-viewer-bundle.js
// which is committed and embedded as a C# resource (see
// BookOfEternityClient.csproj). LocalMapViewerRenderer.BuildStandaloneHtml
// inlines it into map_viewer.html.
//
// The committed artifact keeps `dotnet build` / `dotnet test` independent of
// Node; regenerate it whenever the React MapAtlas or map-atlas.css changes.
import { readFile, writeFile, mkdir, readdir, rm } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { build } from 'vite';

const here = dirname(fileURLToPath(import.meta.url));
const frontendRoot = resolve(here, '..');
const outputDir = resolve(frontendRoot, '..', 'BookOfEternityClient', 'Assets', 'MapViewer');
const bundlePath = resolve(outputDir, 'map-viewer-bundle.js');

console.log('[build-map-viewer] building standalone IIFE via vite.map-viewer.config.ts');
await build({
  configFile: resolve(frontendRoot, 'vite.map-viewer.config.ts'),
  logLevel: 'info'
});

// Vite (cssCodeSplit:false, assetsInlineLimit huge) emits one .js with the CSS
// inlined as a base64 asset import. Some Vite versions still emit a sibling
// .css; if present, inline it into the JS so the standalone HTML only needs
// one <script> tag.
const files = await readdir(outputDir);
const cssFile = files.find((name) => name.endsWith('.css'));
if (cssFile) {
  const cssPath = resolve(outputDir, cssFile);
  const css = await readFile(cssPath, 'utf8');
  const js = await readFile(bundlePath, 'utf8');
  const injected = `;(function(){var s=document.createElement('style');s.textContent=${JSON.stringify(css)};document.head.appendChild(s);})();\n${js}`;
  await writeFile(bundlePath, injected, 'utf8');
  await rm(cssPath, { force: true });
  console.log(`[build-map-viewer] inlined ${cssFile} into bundle`);
}

// Remove any auxiliary chunks Vite may have emitted (e.g. polyfills); the
// viewer is a single self-contained file.
for (const name of files) {
  if (name === 'map-viewer-bundle.js') continue;
  if (name.endsWith('.js')) {
    await rm(resolve(outputDir, name), { force: true });
  }
}

await mkdir(dirname(bundlePath), { recursive: true });
const stat = await readFile(bundlePath);
console.log(`[build-map-viewer] wrote ${bundlePath} (${(stat.length / 1024).toFixed(1)} KB)`);
console.log('[build-map-viewer] done. Commit this file; LocalMapViewerAssets.BundleResourceName points at it.');
