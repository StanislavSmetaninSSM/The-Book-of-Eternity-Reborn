export interface BrowserUiAsset {
  readonly id: string;
  readonly url: string;
  readonly aspectRatio: string;
}

export const browserUiAssets = {
  sceneHeroFallback: {
    id: 'scene-hero-fallback',
    url: '/browser-ui-assets/scene-hero-fallback.png',
    aspectRatio: '16:9'
  },
  galleryEmptyArchive: {
    id: 'gallery-empty-archive',
    url: '/browser-ui-assets/gallery-empty-archive.png',
    aspectRatio: '4:3'
  },
  statusSoulVignette: {
    id: 'status-soul-vignette',
    url: '/browser-ui-assets/status-soul-vignette.png',
    aspectRatio: '1:1'
  }
} as const satisfies Record<string, BrowserUiAsset>;
