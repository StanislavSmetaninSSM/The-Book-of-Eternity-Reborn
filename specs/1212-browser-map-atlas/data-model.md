# Data Model: Browser Map Atlas Drilldown

## Map Block

- Source: command result block of kind `map`.
- Contains one authoritative map view.
- Browser renders it visually; it does not mutate game state.

## Map Node

- Fields used by the browser: id, label, coordinates, z-level, layer, current flag, placeholder flag, owner/influence data, details, and image metadata.
- A node with `isPlaceholder=true` represents an unresolved exit rather than a full known location.

## Location Media

- Trusted renderable URL for a known non-placeholder node.
- Browser renderer accepts local `/api/media/...` URLs, local asset URLs, http(s), and image data URLs.
- Non-image or unsafe URL schemes are ignored rather than rendered.

## Selected-Location Panel

- Derived from the selected map node.
- Shows node details, fallback state, z-level, faction control when present, and optional image thumbnail.
