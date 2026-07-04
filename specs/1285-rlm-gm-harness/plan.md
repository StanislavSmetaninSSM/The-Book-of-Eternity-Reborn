# Implementation Plan: RLM-Inspired GM Harness

**Branch**: `1266-universal-command-audit` | **Date**: 2026-06-26 | **Spec**: `specs/1285-rlm-gm-harness/spec.md`

**Input**: Feature specification from `specs/1285-rlm-gm-harness/spec.md`

## Summary

Implement an RLM-inspired GM harness layer without adding unsafe arbitrary REPL access. The first usable slice records structured live-turn trajectories and harness reward signals. Later slices retrieve compact lessons, expose safe session-local context probes, wire worker delegation as bounded proposal-only subcalls, and run the next live test with a harness-friction rubric.

## Technical Context

**Language/Version**: C#/.NET 8, PowerShell, JSON file-backed game session state.

**Primary Dependencies**: Existing game client runtime, GM bridge, daemon helper scripts, validation repair flow, worker proposal flow.

**Storage**: Session-owned JSON/JSONL files under `game_state/control/` or another context-pack/session-owned control/audit folder.

**Testing**: xUnit tests in `BookOfEternityClient.Tests`, focused daemon/bridge contract tests, documentation coverage tests, manual live GM bridge test.

**Target Platform**: Local Windows game client and local loopback GM bridge.

**Project Type**: Local desktop/console game client with local GM bridge and file-backed state.

**Performance Goals**: Compact ledger and lesson generation must not materially slow turn startup; live-turn quality is measured by reduced repair loops and less context-pack spelunking.

**Constraints**:
- No remote/cloud dependency.
- No arbitrary REPL/shell authority for normal GM gameplay.
- Workers remain hidden/background and proposal-only unless accepted through existing gates.
- GM-facing docs/examples must stay synchronized for Mortal World and afterlife surfaces.

**Scale/Scope**: One active local game session, one main GM bridge, optional hidden worker tasks, bounded context-pack artifacts.

**Source Issue(s)**:
- #1249 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1249
- #1285 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1285
- #1281 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1281
- #1282 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1282
- #1283 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1283
- #1286 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1286
- #1287 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1287
- #1288 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1288
- #1289 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1289
- #1290 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1290
- #1316 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1316
- #1340 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1340
- #1341 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1341
- #1342 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1342
- #1343 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1343
- #1344 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1344
- #1345 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1345
- #1349 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1349
- #1350 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1350
- #1351 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1351
- #1352 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1352
- #1353 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1353
- #1354 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1354
- #1356 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1356
- #1396 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1396
- #1419 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1419
- #1420 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1420

**Contract Scope**: GM-facing prompts, runtime-state, validation, docs, examples, agent-console, e2e.

**Verification Commands**:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmTurnHelperContractTests|GmBridgeDiagnosticsContractTests|LiveTurnPreparationServiceTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

Manual:

- Run a short live GM bridge test with `codex -m gpt-5.5 -c model_reasoning_effort="high" --dangerously-bypass-approvals-and-sandbox`.
- Inspect the generated ledger, lesson artifact, context-pack references, worker/repair events, and rubric notes.

## Constitution Check

- **GitHub traceability**: PASS. Source issues #1285-#1290 are linked.
- **Spec Kit fit**: PASS. This is epic, cross-contract, GM-facing, validation/runtime, and multi-session work.
- **Player-facing integrity**: PASS. The initial feature is GM harness only; no player-facing UI change is intended. Any player-facing behavior found during implementation requires a follow-up issue/spec update.
- **Contract/state authority**: PASS. Existing validators, repair packets, rollback, and worker proposal/apply gates remain authoritative. GM prompts/docs/examples must be updated if new workflow surfaces are added.
- **Test-first path**: PASS. Each implementation slice starts with focused contract tests.
- **Verification evidence**: PASS. Focused daemon/bridge tests and docs coverage are listed.
- **Agent orchestration**: PASS. Worker delegation remains proposal-only and hidden/background; Codex/Hermes task packets must include this spec and verification commands.

## Project Structure

### Documentation (this feature)

```text
specs/1285-rlm-gm-harness/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── gm-trajectory-ledger.md
│   ├── experience-memory.md
│   ├── safe-gm-probes.md
│   └── worker-delegation.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/
├── game_master_daemon.ps1
├── Launcher/
└── ... existing runtime/GM harness code

BookOfEternityClient.Tests/
├── GmTurnHelperContractTests.cs
├── GmBridgeDiagnosticsContractTests.cs
└── documentation/source guard tests

OtherGuides/
├── GM_Worker_Bridges.md
└── afterlife and GM contract guidance

Examples/
└── worked validation/GM examples and validation manifest
```

**Structure Decision**: Put durable requirements in `specs/1285-rlm-gm-harness/`. Runtime artifacts must be session-owned and generated outside the repository source tree during gameplay. Implementation should extend existing daemon/bridge/context-pack code rather than introducing a separate Python RLM runtime.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
