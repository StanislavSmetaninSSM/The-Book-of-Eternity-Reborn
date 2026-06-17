# Browser UI generated/development asset catalog (#929)

This catalog records repository-local development visuals used by the Browser UI.
The default app loads only committed local files from `public/browser-ui-assets/`.
No runtime dependency exists on Codex, image generation tools, Pollinations, or
remote services.

## Runtime boundary

- Assets are static files committed with the frontend.
- Source prompts and provenance notes are review documentation only.
- Default player UI must not expose this catalog, prompts, file paths, provider
  names, or generation-tool language.
- Assets intentionally contain no text, watermarks, logos, trademarks, or
  readable marks, and avoid high-contrast clutter behind player text.

## Provenance for this batch

The built-in image preview tool was available in this Codex session, but this
runtime did not expose retrievable generated image files under the documented
cache path. The committed first batch is therefore deterministic local
generated/development artwork created in-repo from fixed drawing instructions
using local raster drawing primitives. These files are placeholders/development
assets for the tracked Browser UI surfaces and may be replaced by future
development-time generated art under a tracked issue.

## Assets

### scene-hero-fallback

- Final path: `/browser-ui-assets/scene-hero-fallback.png`
- Repository path: `BookOfEternityClient.WebFrontend/public/browser-ui-assets/scene-hero-fallback.png`
- Target UI surface: scene hero background when dynamic scene/entity media is absent or disabled.
- Aspect ratio: 16:9.
- Crop/readability notes: center 32% cover crop; low-density lower third and dark overlays keep title/subtitle readable on desktop and mobile.
- Source prompt / art direction: dark-fantasy ruined archive threshold, obsidian hall, soft candle-gold and teal mist, no characters, no readable writing, restrained contrast for UI overlays.
- Generation/procedural method: deterministic local PNG, fixed seed `92901`, gradients, dust, soft arches, and low-contrast columns.
- Usage constraints / license caveat: repository-generated development artwork for this project only; replace if visual review finds text-like marks or third-party references.
- Fallback behavior: used only when no dynamic scene image URL exists; image load failure hides the decorative image and leaves the gradient hero intact.

### gallery-empty-archive

- Final path: `/browser-ui-assets/gallery-empty-archive.png`
- Repository path: `BookOfEternityClient.WebFrontend/public/browser-ui-assets/gallery-empty-archive.png`
- Target UI surface: command/media image block fallback when a gallery/image block has no usable URL.
- Aspect ratio: 4:3.
- Crop/readability notes: rendered inside a framed figure with captions outside the image, so no player copy is placed over the busy archive particles.
- Source prompt / art direction: quiet archive table, empty frames, sealed parchment mood, candlelit black wood and muted brass, no readable markings.
- Generation/procedural method: deterministic local PNG, fixed seed `92902`, gradients, empty frame geometry, table shadow, and dust.
- Usage constraints / license caveat: repository-generated development artwork for this project only; no external source image input.
- Fallback behavior: shown with player-facing unavailable copy when a media block has no local or dynamic URL; load failure falls back to text only.

### status-soul-vignette

- Final path: `/browser-ui-assets/status-soul-vignette.png`
- Repository path: `BookOfEternityClient.WebFrontend/public/browser-ui-assets/status-soul-vignette.png`
- Target UI surface: status route ambient art behind player/soul/world/afterlife cards.
- Aspect ratio: 1:1.
- Crop/readability notes: decorative background is low opacity, positioned away from card text, and disabled by normal image error hiding if unavailable.
- Source prompt / art direction: abstract soul sigil made of candle smoke and ink, dim halo, dark corners, no letters or runes that resemble writing.
- Generation/procedural method: deterministic local PNG, fixed seed `92903`, gradients, soft smoke ellipses, circular light contours, and vignette.
- Usage constraints / license caveat: repository-generated development artwork for this project only; no external source image input.
- Fallback behavior: decorative only; status data cards render normally if the image is absent.
