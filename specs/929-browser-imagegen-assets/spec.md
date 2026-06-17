# Feature Spec: Browser imagegen asset catalog and first generated visuals

**Feature Branch**: `work/929-browser-imagegen-assets`
**Source GitHub issue**: [#929 Add imagegen asset catalog and generated visuals for browser UI](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/929)
**Parent epic**: [#680](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680)

## Goal

Create a controlled development-time generated-asset workflow and first browser UI asset set so default Browser Client surfaces feel like a polished dark-fantasy game UI without depending on runtime image generation or remote image services.

## Scope

In scope:

- Audit browser UI surfaces that need static illustrative assets:
  - launcher/main menu;
  - scene hero fallback when no dynamic scene image exists;
  - world/location/media/gallery empty states;
  - inventory/books/journal/status sections where illustration improves readability;
  - afterlife / Chaos Sea / Shining Abode route surfaces;
  - QTE/training/Daren surfaces only if current routes already exist.
- Create a tracked asset catalog listing generated asset path, target surface, aspect ratio, source prompt/art direction, provenance, and runtime boundary.
- Commit a first batch of local generated/development assets under the frontend/public asset tree or another explicit repository-local asset location.
- Wire those assets into Browser UI using responsive cropping, overlays, fallbacks, and no broken-image states.
- Keep default UI player-facing and readable on desktop and mobile.

Out of scope:

- Runtime calls to Codex/imagegen/Pollinations/remote generation services.
- Changes to console client behavior.
- Changes to existing runtime `ImageService` or `game_session/images` dynamic scene/entity image authority unless strictly needed for browser fallback display.
- New GM contracts, afterlife/mortal runtime contract changes, or gameplay logic.

## Acceptance criteria

- Asset catalog documents each generated asset, target UI surface, aspect ratio, prompt/art direction, provenance note, and final path.
- First batch of local assets is committed and used by Browser UI.
- Browser UI still works when dynamic scene/entity images are absent or disabled.
- Generated assets contain no embedded text, watermarks, logos, or high-contrast clutter that harms text readability.
- Desktop and mobile visual smoke artifacts show clean crop/framing and readable text overlays.
- Existing console behavior remains unchanged.
- Default Browser UI exposes no raw API, endpoint, DTO, JSON, debug, imagegen/Codex tooling, file path, or agent meta-language.

## Verification expectations

- Frontend verification: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- Focused C# browser/local web smoke/source guards for asset path/provenance/default UI expectations.
- `git diff --check origin/main...HEAD`.
- Static/security scan over added non-Spec/non-doc code.
- Player-facing copy/meta scan for default Browser UI.
- Visual smoke artifact under `TestResults/browser-smoke/`, explicitly described as local/offline and not an automated screenshot unless real screenshots are produced.
