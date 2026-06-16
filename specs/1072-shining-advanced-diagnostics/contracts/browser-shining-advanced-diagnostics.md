# Contract: Browser Shining Advanced Diagnostics Boundary

Source issue: #1072 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1072

## Default Player Mode

Default browser command-result output for `/shining_treasury` and `/source_of_light` must be safe to show to a player without advanced/debug opt-in.

Default output may include:

- Russian/in-world summaries of treasury, Source of Light, resource, readiness, pending, or unavailable states.
- Player-facing empty/sparse/malformed-state explanations such as data being unavailable, damaged, or requiring GM attention.
- Existing safe command actions and prompt/write affordances routed through shared C# services.

Default output must not include:

- `UiRawJsonBlock` or equivalent raw diagnostic blocks.
- Raw JSON dumps or serialized DTO/API payloads.
- `game_state/` paths, drive-letter paths, local filesystem paths, or file names presented as diagnostics.
- API, DTO, endpoint, protocol, debug, stack trace, parser exception, or implementation-language copy.
- Malformed JSON warning path text.
- React-side gameplay filtering as the only protection.

## Advanced / Debug Mode

Advanced/debug diagnostics may include raw or technical data only when an explicit advanced/debug context is active. The implementation should prefer existing C# command-result metadata and advanced-mode mechanisms over new frontend-only state.

Advanced output must still avoid leaking secrets or credentials. It may expose local state file names/paths only when that is already the explicit advanced/debug contract and tests/source guards prove default mode is clean.

## Authority Boundary

This feature is presentation/read-only unless implementation proves otherwise. It must not introduce or modify:

- afterlife pending/control files;
- Shining treasury/source write contracts;
- local-turn write services;
- validation, normalizer, or canonical state schemas;
- GM prompts, examples, manifests, or afterlife contract matrix rows;
- React-side gameplay mutation rules.

If one of those changes is required to satisfy #1072, stop and update the Spec Kit artifacts plus docs/tests, or create a focused follow-up before proceeding.

## Required Evidence

- Focused tests for `/shining_treasury` default no-leak behavior.
- Focused tests for `/source_of_light` default no-leak behavior.
- Coverage for malformed/sparse-state diagnostic copy when reproducible.
- Coverage or source guard showing advanced diagnostics are explicit and do not bleed into default player mode.
- Broad Shining/browser command slice remains green.
