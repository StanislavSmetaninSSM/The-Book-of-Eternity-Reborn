# Implementation Plan: Network Pass-The-Turn Multiplayer

**Branch**: `1266-universal-command-audit` | **Date**: 2026-06-24 |
**Spec**: [`spec.md`](spec.md)

**Input**: Feature specification from
`specs/1251-network-pass-turn-multiplayer/spec.md`

## Summary

Implement shared-protagonist pass-the-turn multiplayer by adding an explicit
campaign/seat/handoff contract, a Mortal persona/guise ledger prerequisite, a
local/reference relay for invite-code join and automatic handoff, and
player-facing console/browser flows. The relay transports opaque accepted
handoff packages and does not become GM or game-state authority.

## Technical Context

**Language/Version**: C#/.NET 8, TypeScript/React/Vite.

**Primary Dependencies**: Existing file-backed game state, validation services,
accepted-turn lifecycle, local web host/browser UI, GM bridge runtime.

**Storage**: File-backed JSON for local client state; local/reference relay may
use file-backed campaign records for MVP.

**Testing**: `dotnet test`, frontend `npm run verify`, browser/manual smoke for
multi-client relay flow.

**Target Platform**: Local Windows development/play first, loopback relay for
MVP; central relay UX remains the product target.

**Project Type**: Local game client with console, browser, runtime services, and
GM bridge.

**Performance Goals**: Handoff sync should feel like a save/load operation, not
a long maintenance task; local relay tests should complete in normal integration
test time.

**Constraints**: No manual save transfer as primary UX; no peer-to-peer setup;
single canonical GM authority; relay cannot mutate game semantics; no automatic
in-fiction time advancement during dormancy.

**Scale/Scope**: MVP private campaigns with a small number of known seats.

**Source Issue(s)**:
<https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1251>

**Contract Scope**: player-facing, GM-facing prompts, runtime-state,
validation, docs, examples, console, browser.

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "NetworkMultiplayer|Handoff|PersonaLedger|Relay"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"`
- `npm run verify` from `BookOfEternityClient.WebFrontend/` when browser UI is
  touched.

## Constitution Check

- **GitHub traceability**: Pass. Source issue #1251 is linked in every artifact.
- **Spec Kit fit**: Pass. This is an epic affecting runtime authority,
  validation, GM bridge, docs/examples, console, and browser.
- **Player-facing integrity**: Pass. Console/browser status and blocked reasons
  are explicitly required to be Russian and non-debug.
- **Contract/state authority**: Pass. Canonical GM authority, relay limits,
  handoff gate, persona ledger, and documentation/example requirements are
  explicit.
- **Test-first path**: Pass. Tasks require failing contract/integration tests
  before implementation.
- **Verification evidence**: Pass. Focused commands are listed.
- **Agent orchestration**: Pass. This directory is the active design packet for
  future Codex/Hermes delegation.

## Project Structure

### Documentation (this feature)

```text
specs/1251-network-pass-turn-multiplayer/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── relay-protocol.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (planned implementation areas)

```text
BookOfEternityClient/
├── Services/                  # campaign, relay, handoff, validation services
├── Core/GameEngine/           # lifecycle and handoff gate integration
├── UI/                        # console multiplayer commands/status
├── WebUi/                     # browser API/menu/action surfaces
└── game_session/              # local test session data only

BookOfEternityClient.Tests/    # contract, validation, local relay E2E tests
BookOfEternityClient.WebFrontend/ # browser multiplayer UI when implemented
BookOfEternityGMBridge/        # host GM authority routing/integration
OtherGuides/                   # GM/player guidance
Examples/                      # worked multiplayer examples
docs/                          # technical and player-facing docs
```

**Structure Decision**: Keep the relay model separate from game-state semantic
authority. Runtime services may produce and consume handoff packages, but relay
storage should remain an opaque package coordinator.

## Phase 0 Research Summary

See [`research.md`](research.md). Key decisions:

- shared protagonist only;
- one canonical GM authority;
- relay-based join and handoff;
- opaque handoff packages with hashes;
- dormancy resumes from latest accepted state;
- Mortal persona/guise ledger is a prerequisite;
- hidden GM-only data privacy is documented as an MVP trust limitation.

## Phase 1 Design Summary

See:

- [`data-model.md`](data-model.md)
- [`contracts/relay-protocol.md`](contracts/relay-protocol.md)
- [`quickstart.md`](quickstart.md)

## Risk Plan

- **Privacy**: Document MVP trusted-campaign limitation; create a future
  hardening task for per-seat encrypted/private payloads before public relay.
- **State corruption**: Require hash chain, turn ordinal, and validation status
  before relay accepts a handoff.
- **GM authority outage**: Block GM-resolved actions with clear player-facing
  reason instead of silently using another GM.
- **Persona confusion**: Enforce persona/guise ledger before Mortal handoff.
- **Scope creep**: Defer public matchmaking, peer-to-peer, full host migration,
  and multi-character play to separate tracked issues.

## Post-Design Constitution Check

Pass. The design stays tied to #1251, separates runtime authority from relay
transport, requires prompt/docs/examples for GM-authored contracts, and defines
test-first implementation slices.
