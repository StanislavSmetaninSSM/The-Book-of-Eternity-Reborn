export {};

import { createBrowserApiClient, browserApiEndpointDocs } from '../src/api/client.js';
import type { BrowserMediaGenerateRequest } from '../src/api/contracts.js';

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { readFileSync } = await import(fsSpecifier);
const { basename, join } = await import(pathSpecifier);
const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, 'src', ...relativePath), 'utf-8');
}

function assert(condition: unknown, message: string) {
  if (!condition) {
    throw new Error(message);
  }
}

const mediaEndpoint = browserApiEndpointDocs.find((endpoint) => String(endpoint.id) === 'media-generate');
assert(mediaEndpoint, 'Browser API docs should include the media-generate endpoint.');
if (!mediaEndpoint) {
  throw new Error('Browser API docs should include the media-generate endpoint.');
}
assert(mediaEndpoint.method === 'POST', 'media-generate endpoint should be POST.');
assert(String(mediaEndpoint.path) === '/api/media/generate', 'media-generate endpoint should target /api/media/generate.');
assert(mediaEndpoint.playerSurface === 'player-default', 'media-generate endpoint should be player-default.');
assert(String(mediaEndpoint.response) === 'BrowserMediaGenerateResult', 'media-generate endpoint should document BrowserMediaGenerateResult.');

const mediaRequest: BrowserMediaGenerateRequest = {
  prompt: 'Moonlit ruins under crimson rain',
  entityType: 'scene',
  entityKey: 'scene_browser_123'
};

let requestUrl = '';
let requestInit: RequestInit | undefined;
const mediaClient = createBrowserApiClient({
  baseUrl: 'https://example.test',
  fetcher: async (input, init) => {
    requestUrl = String(input);
    requestInit = init;
    return new Response(JSON.stringify({
      success: true,
      mediaId: 'media-001',
      url: 'https://example.test/scenes/media-001.jpg',
      errorMessage: null
    }), {
      status: 200,
      headers: {
        'Content-Type': 'application/json'
      }
    });
  }
});

const mediaResult = await mediaClient.generateMedia(mediaRequest);
assert(mediaResult.ok, 'generateMedia should resolve successful payloads.');
if (!mediaResult.ok) {
  throw new Error('generateMedia unexpectedly returned failure.');
}
assert(requestUrl === 'https://example.test/api/media/generate', `generateMedia should call the media endpoint, got ${requestUrl}`);
assert(requestInit?.method === 'POST', 'generateMedia should POST JSON.');
if (!requestInit) {
  throw new Error('generateMedia should capture request init.');
}
const requestHeaders = new Headers(requestInit.headers);
assert(requestHeaders.get('Content-Type') === 'application/json', 'generateMedia should send JSON content type.');
assert(requestHeaders.get('Accept') === 'application/json', 'generateMedia should accept JSON.');
assert(requestInit.body === JSON.stringify(mediaRequest), 'generateMedia should serialize the request body exactly.');
assert(mediaResult.data.url === 'https://example.test/scenes/media-001.jpg', 'generateMedia should surface the returned media url.');

const contractsSource = readSource('api', 'contracts.ts');
assert(contractsSource.includes('export interface BrowserMediaGenerateRequest'), 'contracts.ts should define BrowserMediaGenerateRequest.');
assert(contractsSource.includes('export interface BrowserMediaGenerateResult'), 'contracts.ts should define BrowserMediaGenerateResult.');

const hookSource = readSource('hooks', 'useSceneImage.ts');
assert(hookSource.includes('browserApi.generateMedia({'), 'useSceneImage should request generated media when gallery is missing a scene image.');
assert(hookSource.includes("imageKind: 'scene' | 'location' = 'scene'"), 'useSceneImage should default to scene image generation while supporting location images.');
assert(hookSource.includes('entityIdentity?: string | null'), 'useSceneImage should accept a stable entity identity for media reuse.');
assert(hookSource.includes('const entityKey = sanitizeEntityKey(entityIdentity ?? sceneImagePrompt ?? imageKind);'), 'useSceneImage should derive a stable entity key from the current scene or location identity.');
assert(hookSource.includes('return stem === entityKey || stem.startsWith(`${entityKey}__img_`);'), 'useSceneImage should reuse only media files for the same entity key.');
assert(!hookSource.includes("item.fileName.includes('location')"), 'useSceneImage should rely on entity keys instead of dead location-name fallbacks.');
assert(!hookSource.includes("item.fileName.includes('scene')"), 'useSceneImage should rely on entity keys instead of dead scene-name fallbacks.');
assert(hookSource.includes('lastPromptRef.current === entityKey'), 'useSceneImage should deduplicate generation per entity key, not just per prompt text.');
assert(hookSource.includes('lastPromptRef.current = entityKey;'), 'useSceneImage should remember the last generated entity key.');
assert(hookSource.includes('const generatingRef = useRef<string | null>(null);'), 'useSceneImage should track which entity is currently generating.');
assert(hookSource.includes('generatingRef.current === entityKey'), 'useSceneImage should only block duplicate generation for the same entity key.');
assert(hookSource.includes('generatingRef.current = entityKey;'), 'useSceneImage should record the in-flight entity key.');
assert(hookSource.includes('setState({ url: null, loading: true, error: null });'), 'useSceneImage should clear stale imagery while generating a new entity image.');
assert(hookSource.includes('if (generatingRef.current !== entityKey) {'), 'useSceneImage should ignore stale async results for superseded entity keys.');
assert(hookSource.includes('entityType: imageKind'), 'useSceneImage should generate media for the requested image kind.');
assert(hookSource.includes('entityKey'), 'useSceneImage should send the stable entity key to the media API.');
assert(hookSource.includes('result.ok && result.data.success && result.data.url'), 'useSceneImage should accept successful API responses via result.ok.');
