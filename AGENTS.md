# Repository Agent Instructions

## Task tracking guardrail

Do not implement project changes without a tracked task.

Before editing code, tests, prompts, documentation, examples, or game contracts, first ensure there is an explicit task for the work. If the user asks to implement something and no task exists, create or request a task record before making repository changes. Small exploratory reads, reviews, and planning may happen without a task, but implementation work must be tied to a task.

## Afterlife contract documentation guardrail

The GM does not read client implementation code during normal play. If you change any `Chaos Sea` / `Shining Abode` runtime contract, update the GM-facing documentation in the same change.

This applies when adding, renaming, removing, or changing any afterlife:
- pending/control file in `game_state/control/`
- `pending_shining_abode_actions.json` `actionType`
- response field, receipt, report, or canonical state surface
- validation rule, scheduler contour, lifecycle mode, normalizer side effect, or authority path
- player-visible command behavior that the GM must resolve through prompts

Before finishing that change, check whether these files also need updates:
- `OtherGuides/Afterlife_Contract_Matrix.md`
- `Examples/E_CLI_Afterlife_Turns.txt`
- `Examples/example_validation_manifest.json`
- `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`
- `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs`
- daemon/launcher prompt entrypoints if the GM must be forced to read new guidance

If an explicit afterlife contract registry is added later, update it together with the matrix, examples, and manifest. Do not leave a code-only afterlife contract unless it is intentionally client-owned and documented as not GM-authored.

Minimum verification for documentation-sensitive afterlife changes:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```
