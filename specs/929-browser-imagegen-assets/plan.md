# Plan: Browser imagegen asset catalog and generated visuals

## Technical approach

1. Inspect current Browser frontend asset and launcher/surface structure.
2. Add/extend source guards before production work where practical:
   - asset catalog/provenance existence;
   - no runtime remote-generation dependency;
   - local asset paths are used by default Browser UI;
   - desktop/mobile visual-smoke artifact references committed assets and remains player-facing.
3. Create repository-local generated/development assets with source prompt notes next to the assets. Prefer safe local assets and provenance documentation over runtime image-generation hooks.
4. Wire the first asset batch into player-facing Browser UI surfaces with responsive crop/overlay/fallback behavior.
5. Verify frontend, focused browser smoke/source guards, diff-check, static/security scan, and player-facing copy/meta boundary.

## Constraints

- Keep C# game/runtime authority unchanged.
- Keep existing dynamic `ImageService` / `game_session/images` behavior intact.
- No hotlinks or runtime calls to generation services.
- No console behavior changes.
- No raw tooling/provenance copy on default player surfaces.

## Handoff

Hermes/Codex lifecycle after implementation: fresh verification, independent detached review, PR with `Closes #929` only, squash merge, post-merge verification, evidence comment, label hygiene, cleanup, then existing worker continuation.
