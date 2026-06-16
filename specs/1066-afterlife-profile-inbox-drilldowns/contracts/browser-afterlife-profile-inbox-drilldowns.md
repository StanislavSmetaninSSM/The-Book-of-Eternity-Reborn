# Contract: Browser Afterlife Profile and Inbox Follow-Through Drill-Downs

Source issue: #1066 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1066
Origin audit: #949 AFD-005 — `docs/audits/afterlife-drilldown-audit.md`

## Contract boundary

This feature is a presentation/read-only Browser Client command-result enhancement over existing afterlife state. It must not introduce new afterlife runtime schemas, pending/control files, validation or normalizer rules, local write contracts, GM prompt requirements, GM examples, or React-side gameplay authority unless the implementation explicitly expands scope and updates required docs/tests in the same PR.

## Default player-mode requirements

Default command-result output for `/afterlife_profiles`, `/afterlife_threats`, `/afterlife_chronicles`, and `/afterlife_inbox` must remain Russian/in-world and player-facing.

Default output must not include:

- raw JSON blocks or raw serialized state;
- `game_state/` paths, drive paths, local filenames, parser exception text, or stack traces;
- API, DTO, endpoint, protocol, debug, Spec Kit, agent, or implementation-meta wording;
- hidden/gm-only evidence, GM thoughts, hidden success conditions, or hidden threat/profile/chronicle rows;
- raw target IDs as the primary player-facing explanation when a friendly display name/reason is available.

## Overview preservation

Every scoped overview command must keep its existing overview summary/list behavior. Detail/follow-through actions are additive; they must not replace the first useful summary with only action buttons or generic completion copy.

## Selected-detail action contract

A selected-detail action may be exposed when all are true:

1. The overview row represents a concrete visible profile, threat, chronicle/event, notification, or supported inbox/support target.
2. A stable identifier or existing command argument can resolve that row through canonical state or an existing safe detail/context renderer.
3. The default detail can be rendered without revealing hidden/gm-only state.
4. The action is read-only and does not write pending/control state, acknowledge messages, mutate inbox read status, or commit a local turn.

If any condition fails, the row should either omit the follow-through action or route to a safe unavailable result.

## Inbox follow-through contract

`/afterlife_inbox` follow-through actions must be read-only. They may open existing safe context for supported targets such as profile, threat, chronicle, Guardian, archive, Shining, resident, project, or trade views when current state or stored snapshots provide enough authority.

Read-only follow-through must not auto-mark a notification read. Existing explicit read/acknowledge actions may continue to mutate through existing C# prompt/write services.

## Missing, stale, unsupported, or hidden targets

When a selected target cannot be safely resolved, the result must say so in player-facing terms, for example that the memory is no longer visible, the trace has faded, or the relevant context is unavailable until the GM updates the state. It must not dump raw IDs, JSON, file paths, or implementation diagnostics in default mode.

## Advanced/debug mode

Existing advanced/debug pathways may continue to show raw identifiers or diagnostics when explicitly enabled. This feature does not require new advanced-mode infrastructure. If advanced details are adjusted, default-mode no-leak behavior remains mandatory.

## Sibling boundary

Spiritual conflict exchange/log/art selected details belong to #1067. #1066 may mention those commands only as out-of-scope sibling context and must not close #1067 unless separate verified work fully satisfies it.
