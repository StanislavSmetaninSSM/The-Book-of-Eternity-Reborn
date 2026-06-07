# Research: Browser Shining Abode Politics

## Decision 1: Add three dedicated browser mutation commands

**Decision**: Keep `/shining_politics` as the read-only filtered overview and add dedicated local-turn browser commands for founding, realignment, and leadership transition.

**Rationale**: The existing command is already a safe player-facing overview. Dedicated commands keep browser action metadata clear, avoid overloading a read-only command with mutation modes, and match the browser's current minimalist guided-form direction.

**Alternatives considered**:
- Add subcommands under `/shining_politics`: rejected because existing browser action metadata and player menu are descriptor-based and do not naturally expose subcommands as first-class player actions.
- Add React-only buttons with custom handlers: rejected because C# must remain authoritative and React must not invent gameplay rules.

## Decision 2: Reuse `ShiningFactionRequestState` as the sole write authority

**Decision**: Browser submit handlers will construct the existing pending request DTOs, call existing validators, and write through `WriteFoundingRequestAsync`, `WriteRealignmentRequestAsync`, and `WriteLeadershipTransitionRequestAsync`.

**Rationale**: Console already uses these helpers. Reuse preserves pending/control shape and avoids contract drift.

**Alternatives considered**:
- Write JSON directly in the browser service: rejected because it bypasses existing conflict handling and makes console/browser parity fragile.
- Add new browser-specific pending files: rejected as a new runtime contract outside #810.

## Decision 3: Use existing prompt input types

**Decision**: Browser forms will use current prompt DTO types: selection, text input, long text input, and confirmation. Multi-supporter fields may use a comma-separated text input unless an existing multi-select prompt type is already available.

**Rationale**: The issue is browser parity, not a frontend prompt-control expansion. Avoiding new prompt DTOs reduces fixture/frontend blast radius.

**Alternatives considered**:
- Add a multi-select prompt control: deferred because it changes frontend rendering/contracts and is not required for parity if the form labels are clear.

## Decision 4: No GM-facing contract changes planned

**Decision**: Do not modify afterlife contract matrix/examples/manifests unless implementation changes pending/control shape, response fields, validation rules, normalizer side effects, or GM-authored guidance.

**Rationale**: Console flows already create these pending requests. Browser parity should reuse the same contracts rather than adding GM burden.

**Alternatives considered**:
- Refresh docs preemptively: rejected because it creates unrelated documentation churn without a contract change.

## Decision 5: Guard both direct open and stale submit

**Decision**: Direct command result builders will block outside ordinary active Shining Abode context before returning `RequiresInput`; write handlers will re-check realm and blockers at submit time.

**Rationale**: Prompt sessions can become stale after a realm switch. The console's realm and local-write guards must hold even when the browser submits an old session.
