# Afterlife Chaos Sea / Shining Abode Audit Findings

Tracking issues: #470, #471, #472, #473, #474, #475, #476, #477, #478, #479.

## Rules For This File

- Record every finding before implementing the fix.
- Include source files, affected realm, severity, reproduction or mental experiment, expected behavior, and proposed fix.
- If a finding is too large for the current task, create a separate GitHub issue and link it here.
- Mark status as `open`, `fixed`, `split`, or `wontfix`.

## Findings

| ID | Status | Issue | Realm | Severity | Summary | Source / Evidence | Proposed Fix |
| --- | --- | --- | --- | --- | --- | --- | --- |
| AFT-001 | fixed | #477 | Chaos Sea / Shining Abode | P2 | Afterlife example coverage ignored examples 24-26 because the test only parsed `N. VALID` headings; `EXAMPLE 24-26` could drift without runtime coverage or exemption. | `Examples/E_CLI_Afterlife_Turns.txt` has `EXAMPLE 24`, `EXAMPLE 25`, `EXAMPLE 26`; the previous test only matched `^(\\d+)\\. VALID` and expected 1..23; `example_validation_manifest.json.afterlifeExampleCoverage` ended at 23. | Fixed by parsing both heading styles, expecting examples 1..26, and adding manifest coverage/exemptions for 24-26. Verification: `dotnet test ... --filter "FullyQualifiedName~AfterlifeWorkedExamplesHaveRuntimeScenarioOrExplicitCoverageExemption"` passed 1/1. |
| AFT-002 | fixed | #477 | Chaos Sea / Shining Abode | P3 | The afterlife examples index and daemon coverage wording did not route readers to all newer examples: the index skipped example 24 and example 26, while daemon docs/tests still mentioned `examples 14-25` as the range. | `Examples/E_CLI_Afterlife_Turns.txt` index listed 23 then 25; `CLI_Agent_Daemon_Specification.md` said `examples 14-25` and separately mentioned 26; coverage tests asserted the stale range. | Fixed by adding index bullets for examples 24 and 26 and updating daemon wording/tests to `examples 14-26`. Verification: `dotnet test ... --filter "AfterlifeDocumentationCoverageTests"` passed 63/63. |
