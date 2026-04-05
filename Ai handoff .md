# AI Handoff

## Purpose
This file is the starting point for the next AI session on this repo.
The current long dialogue focused on hardening the Guardian politics system.
Use this file first, then inspect the referenced code and rerun the focused tests.

## Repo
- Root: `E:\Games\The Book of Eternity Reborn`
- Main app: `BookOfEternityClient`
- Tests: `BookOfEternityClient.Tests`

## Current status
- Guardian-focused validation/normalization work has been heavily refactored.
- Latest focused verification passed:
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-build --filter "FullyQualifiedName~CanonicalStateNormalizerTests|FullyQualifiedName~GuardianCorrectionServiceTests|FullyQualifiedName~GuardianSystemRegressionTests"`
- Last reported result in this session: `111/111` in the guardian-focused subset.
- Full 500+ test suite was **not** run.

## Most relevant files
- `BookOfEternityClient/Services/Validation/ValidationService.GuardiansAndAfterlife.cs`
- `BookOfEternityClient/Services/Validation/ValidationService.GuardianProjectsAndGacha.cs`
- `BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs`
- `BookOfEternityClient/Services/GuardianPowerEventState.cs`
- `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`
- `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs`
- `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs`
- `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardianProjectHelpers.cs`
- `BookOfEternityClient.Tests/GuardianSystemRegressionTests.cs`

## What was just changed
- `UpdateGuardians` validation no longer pre-authorizes all `create` commands from the whole array.
- Guardian identity for project/power/journal validation was tightened to current guardian state.
- `validation_repair_request.json` no longer wins purely by existence or parseability; manifest matching now checks repair context first, then falls back to `input/turn_request.json`.
- Added regression tests for:
  - future/invalid `create` not authorizing commands
  - snapshot-only guardian rejected on guardian project validation
  - raw create-only guardian rejected on power event/journal validation
  - stale repair request falling back to valid turn request

## Outstanding findings from the latest review

### 1. Same-turn successful `UpdateGuardians.create` is still rejected on guardian-political surfaces
Validation for guardian projects / raw guardian power events / journal uses only current `guardians.json`.
But runtime normalization applies `UpdateGuardians` first and can then process same-turn `guardianPowerEvents` and guardian project commands for the newly created guardian.

Relevant code:
- `BookOfEternityClient/Services/Validation/ValidationService.GuardianProjectsAndGacha.cs`
- `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`
- `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs`
- `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs`

Recommended direction:
- Decide on one contract and keep it consistent.
- Recommended: allow only **earlier successfully validated** same-turn `create` to authorize later guardian-political commands/events, mirroring `UpdateGuardians` sequential semantics.

### 2. Snapshot-context matching is still too permissive when request context has only `turnNumber`
Current request-context helpers treat missing `sessionId` / `requestId` as empty strings.
That means a parseable request file with only `turnNumber` can match any same-turn manifest.
This leaves stale same-turn snapshot fallback possible.

Relevant code:
- `BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs`
- `BookOfEternityClient/Services/GuardianPowerEventState.cs`
- `BookOfEternityClient/Services/Validation/ValidationService.BootstrapAndProtocol.cs`

Recommended direction:
- Make snapshot fallback fail closed unless current context is specific enough.
- Recommended default: if the manifest has `sessionId` / `requestId`, require the active repair/turn context to provide matching values; do not treat empty current ids as wildcard.

### 3. Validator still accepts `activeGuardian` as identity, but apply paths operate on `guardians[]`
Validator/sequential-state collection still includes `activeGuardian`.
Runtime mutation and power-event apply look up guardians inside `guardians[]` first-class structures.
So a malformed root with `activeGuardian` not mirrored in `guardians[]` can validate and then no-op at apply time.

Relevant code:
- `BookOfEternityClient/Services/Validation/ValidationService.GuardiansAndAfterlife.cs`
- `BookOfEternityClient/Services/Validation/ValidationService.GuardianProjectsAndGacha.cs`
- `BookOfEternityClient/Services/GuardianPowerEventState.cs`
- `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs`
- `BookOfEternityClient/Services/Validation/ValidationService.GuardianAndRivalCrossRefs.cs`

Recommended direction:
- Remove `activeGuardian` as an identity-authorizing source for command/project/power validation and rely on `guardians[]` only.
- Keep `activeGuardian` as a consistency mirror checked against `guardians[]`, not as an alternative canonical source.

## Missing tests worth adding next
- Positive regression: earlier valid `UpdateGuardians.create` followed by same-turn `guardianPowerEvents` for the new guardian.
- Positive regression: earlier valid `UpdateGuardians.create` followed by same-turn `startGuardianProjects` for the new guardian.
- Negative regression: request-context with only `turnNumber` should not authorize a manifest that has a different `sessionId` / `requestId`.
- Negative regression: `activeGuardian` present without matching entry in `guardians[]` should not authorize guardian commands or power events.

## Suggested next-session workflow
1. Read this file.
2. Re-read the latest review findings in the current chat if available.
3. Inspect the files listed in "Most relevant files".
4. Implement the three outstanding findings above.
5. Rerun the guardian-focused build/test commands.
6. Only after that, decide whether the full test suite is worth running.

## Notes
- The guardian area has gone through many iterative fixes in this session. Do not assume older chat conclusions are still accurate without checking current code.
- Prefer code-first verification over memory. The window that produced this handoff became too long to trust narratively.
