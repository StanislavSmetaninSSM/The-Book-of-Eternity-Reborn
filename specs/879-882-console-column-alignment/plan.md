# Console Column Alignment Implementation Plan

**Source issues:**
- #879 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/879
- #882 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/882

**Spec:** `specs/879-882-console-column-alignment/spec.md`

## Technical Context

- Runtime/client: .NET 8 C# Console Client using Spectre.Console.
- Main presentation files:
  - `BookOfEternityClient/UI/ConsoleLayout.cs`
  - `BookOfEternityClient/UI/GameInterface.cs`
  - representative `BookOfEternityClient/UI/ExplorerMode/*.cs` surfaces that use `ConsoleLayout`.
- Test/guard files:
  - `BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs`
  - add focused tests only where they verify real source/helper behavior.
- Baseline observed before implementation in the fresh worktree after restore:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter 'FullyQualifiedName~ExplorerModeSourceGuardTests' --logger 'console;verbosity=minimal'`
  - Result: passed 40, failed 0, skipped 0.

## Architecture

`ConsoleLayout` remains the shared boundary for console row/table alignment. The HUD should use a fixed-width metric table (label, bar, value, expansion spacer) or an equivalent shared helper that constrains the label/bar/value group before any flexible column absorbs terminal width. Dynamic player text remains escaped through existing `GameInterface.Safe*` helpers before reaching Spectre markup.

If a new helper is introduced, it should make the aligned-column contract explicit and be difficult to misuse at call sites. If existing `CreateBarMetricTable` is sufficient after correcting widths/cells/rendering, prefer improving that rather than adding unnecessary API surface.

## Data Flow

1. `GameInterface.RenderStatusBar(PlayerStatusState)` receives canonical player status values.
2. Percent values are normalized and rendered into internal bar markup via `ConsoleLayout.CreateBarFromPercent` / `CreateBar`.
3. Status rows are added to the shared metric table with fixed label/bar/value columns and an empty spacer column.
4. Spectre.Console receives a single table/renderable whose flexible expansion, if any, happens outside the label/bar/value group.
5. Visual verification captures the real Console Client/terminal result after the fix.

## Error Handling and Safety

- Keep percent clamping behavior in `ConsoleLayout.CreateBarFromPercent` / `CreateBar`.
- Do not pass user/GM-authored text into `Markup` without central escaping.
- Do not change gameplay state, command routing, runtime contracts, or Browser Client behavior.
- If a real screenshot cannot be captured in this environment, stop and report the exact blocker instead of closing #879/#882.

## Verification Plan

1. TDD RED: add or tighten a focused guard that fails on the current implementation for the missing shared aligned-row invariant or the actual HUD drift risk. Run it and capture the expected failure.
2. GREEN: implement the smallest `ConsoleLayout`/HUD change that makes the guard pass while preserving existing source guards.
3. Run focused guards/tests:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter 'FullyQualifiedName~ExplorerModeSourceGuardTests' --logger 'console;verbosity=minimal'
```

4. Run build:

```bash
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
```

5. Run hygiene:

```bash
git diff --check origin/main...HEAD
git diff origin/main...HEAD -- . ':!docs/superpowers/plans' | grep '^+' | grep -iE '(api_key|secret|password|token|passwd)\s*=\s*['"'"'][^'"'"']{6,}['"'"']|os\.system\(|subprocess.*shell=True|\beval\(|\bexec\(|pickle\.loads?\(|execute\(f"|\.format\(.*SELECT|\.format\(.*INSERT' || true
```

6. Visual evidence: run the real Console Client and capture a screenshot/terminal capture showing the aligned HUD. Include the artifact path in PR/issue comments and final report.

## Spec Kit Consistency Notes

- `spec.md`, `plan.md`, and `tasks.md` all link #879 and #882.
- No GM prompt/docs update is expected because the work is presentation-only.
- If implementation expands into command behavior or runtime-state changes, update the spec and stop for reconciliation.
