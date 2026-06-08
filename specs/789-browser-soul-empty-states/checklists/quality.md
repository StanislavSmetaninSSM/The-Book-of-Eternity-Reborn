# Quality Checklist: Browser Soul page empty states and player copy

**Feature**: `specs/789-browser-soul-empty-states`

**Source issue**: #789 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/789

## Product and scope

- [x] Source issue #789 is linked in `spec.md`, `plan.md`, and `tasks.md`.
- [x] Implementation stays on the current `StatusView`/Browser Client architecture and does not recreate obsolete `SoulRoute`/`DetailSurfaceCard` patterns.
- [x] Sidebar (#790) and Home launcher (#791) work remains out of scope.
- [x] No C# runtime, GM prompt, example, validation, afterlife/mortal contract, or console behavior change is introduced unless the spec is updated first.

## Player-facing UI

- [x] Missing player/soul identity fields render intentional in-world empty-state treatment, not blank cells.
- [x] Meaningful player/soul values remain visible by default.
- [x] Status meters retain semantic good/warning/danger styling and accessibility metadata.
- [x] Default copy is Russian/player-facing and has no raw `/api/`, endpoint, DTO, debug, raw JSON, validation-dashboard, or agent terminology.

## Verification

- [x] Focused #789 guard/test shows RED or mutation evidence, then GREEN.
- [x] `npm run verify --prefix BookOfEternityClient.WebFrontend` passes with non-zero counts.
- [x] Focused `.NET` browser source-smoke guard passes if source expectations or built smoke assets changed.
- [x] `git diff --check origin/main...HEAD` passes.
- [x] Added-line static scan has no real secret/injection blockers.
- [x] Visual smoke evidence is produced and accurately described as screenshot only if it is a real screenshot.
