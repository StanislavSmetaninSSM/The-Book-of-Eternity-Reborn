/// <reference types="vite/client" />
import type { WoundBrowserFixture } from './fixtures/woundBrowserHost';
// Compile the controlled DOM host as well as the production components; no runtime assertion claim.
export type WoundHostCase = Parameters<WoundBrowserFixture['setCase']>[0];
