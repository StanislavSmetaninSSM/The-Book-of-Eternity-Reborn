# Contract: Browser generated/development UI assets

## Runtime boundary

- Browser Client must use committed local assets at runtime.
- No runtime dependency on Codex, imagegen, Pollinations, remote image services, or developer-only generation tools.
- Source prompts/provenance notes are documentation for development and review, not player-facing UI text.

## Catalog fields

Each accepted asset should record:

- stable asset id;
- target UI surface;
- final repository path;
- aspect ratio / intended crop zones;
- source prompt or art direction;
- generation/procedural method and provenance note;
- usage constraints / license caveat;
- fallback behavior when the asset is missing or disabled.

## Player-facing constraints

- No embedded text, watermarks, logos, or trademarked source references.
- Must remain readable under UI overlays at desktop and mobile widths.
- Default UI must not expose file paths, raw prompts, provider names, debug metadata, DTO/JSON/API wording, or generation-tool names.
