# Complete Faction Materialization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce complete current-schema materialization for every Mortal and
Shining faction, narrow update authority, bounded repair, and fast development
feedback. Receipt-less canonical factions are invalid; the unreleased game has
no old-save compatibility requirement.

**Architecture:** Add a pure closed Faction Materialization kernel modeled after
Actor Materialization, then compose it with separate Mortal and Shining evidence
builders at the accepted-turn raw and canonical validation seams. Mortal full
carriers are creation-only and normalize atomically into existing sidecars;
existing factions use a closed `FactionCoreChanges` command. Shining route
validators remain authoritative, while raw semantic checks prevent
`ShiningAbodeState` defaults from manufacturing completeness.

**Tech Stack:** C#/.NET 8, `System.Text.Json`/`System.Text.Json.Nodes`, xUnit, file-backed canonical JSON state, PowerShell 7 bounded test lanes, existing `ValidationService`, `CanonicalStateNormalizer`, and `GameEngine` repair loop.

## Global Constraints

> **Authoritative amendment — 2026-08-10:** The detailed Tasks 0–11 below are
> retained as the historical execution record for the already implemented
> slice. Every instruction, example, classifier value, test, or code fragment
> that preserves receipt-less factions, introduces legacy promotion, or enables
> compatibility normalization is superseded and MUST NOT be implemented or
> restored. Remaining correction work is defined in the current
> `specs/1510-complete-faction-materialization/tasks.md` amendment and uses TDD
> to remove those paths, migrate positive fixtures, enforce exact Shining
> resident identity, and complete typed repair targets.

- Source issue is [#1510](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1510); every implementation commit includes `(#1510)`.
- The authoritative feature artifacts are
  `specs/1510-complete-faction-materialization/{spec,plan,research,data-model,quickstart,tasks}.md`
  and `contracts/*.md`.
- Support exactly `mortal_faction` and `shining_faction`; unknown envelope,
  capability, section, disposition, and command members fail closed.
- Every canonical faction has a complete receipt. Receipt-less state is rejected
  before normalization/rendering; repository fixtures migrate to current state.
- `factionStrength`, derived Shining tier, and service multiplier remain
  client-owned projections and preserve an existing immutable receipt.
- The normalizer and repair loop never invent narrative semantics or infer
  faction behavior from names, descriptions, IDs, tags, or genre vocabulary.
- Chaos Sea Guardian Politics stays outside #1510 and remains under #1500/#1368;
  #1222's worker and #1462's living-world behavior are not implemented.
- Ordinary console/browser views never expose `materialization`, schema/receipt
  IDs, disposition tokens, or private empty-state reasons.
- Use only `pwsh .\scripts\test-csharp.ps1`: exact Focused selections while
  iterating, `-FocusedProject Integration` for integration-project selections,
  one Fast checkpoint, one required FullValidation after afterlife docs
  stabilize, and one clean PreMerge before integration.
- Never combine fast-project and integration-project test classes in one
  Focused filter; one successful command must prove discovery in one explicit
  project.
- Do not widen test timeouts or concurrency and do not run an unbounded
  full-solution/full-suite `dotnet test`.
- Preserve unrelated worktree changes and never stage `.serena/`, `bin/`,
  `obj/`, test result, or unrelated generated files.

---

## File Responsibility Map

### New production files

- `BookOfEternityClient/Services/FactionMaterializationContract.cs` — pure
  closed envelope/disposition/capability validation and unique receipt IDs.
- `BookOfEternityClient/Services/FactionCoreChangesContract.cs` — closed Mortal
  ordinary-update evaluation and application.
- `BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs`
  — pre-turn classification, immutable receipt continuity, raw/canonical
  orchestration.
- `BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs`
  — Mortal semantic and cross-file bundle evidence.
- `BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs`
  — Shining semantic, route, story, hall, resident, and actor evidence.
- `BookOfEternityClient/Services/Validation/ValidationService.FactionCoreChanges.cs`
  — duplicate-sensitive raw command validation.

### New test files

- `BookOfEternityClient.Tests/FactionMaterializationContractTests.cs`
- `BookOfEternityClient.Tests/FactionCoreChangesContractTests.cs`
- `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
- `BookOfEternityClient.IntegrationTests/FactionCoreChangesTests.cs`
- `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Factions.cs`

### Existing integration seams

- `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs` —
  raw fence and exact repair packet routing.
- `BookOfEternityClient/Services/CanonicalStateNormalizer.cs` — ordered command,
  sidecar, chronicle, and core normalization.
- `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs`
  and `.FactionAndInventoryHelpers.cs` — Mortal extraction and command apply.
- `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.ShiningAbode.cs`
  plus `BookOfEternityClient/Services/ShiningAbodeState.cs` and
  `BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs` — strict
  strict current-schema Shining normalization and no semantic auto-creation or
  receipt-less fallback.
- `BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs`,
  `ValidationService.ValidationPhases.cs`, and
  `BookOfEternityClient.IntegrationTests/IntegrationValidationProfiles.cs` —
  selectable canonical phase plus bounded integration-test profiles.
- `BookOfEternityClient/Models/GameResponse.cs`,
  `BookOfEternityClient/Configuration/FileMapping.cs`, and
  `ValidationService.LifecycleControlAndStateFiles.cs` — new command surface.

### Documentation and guards

- Mortal: `Rules/Block_21.txt`, `Examples/E_Block_21.txt`,
  `TaskGuides/CLI_Step_Main.txt`, `Examples/E_CLI_Step_Main.txt`.
- Shared prompt/protocol: `CLI_API_Specification.md`,
  `CLI_Agent_Daemon_Specification.md`, `Rules/Block_CLI_Operations.txt`,
  `BookOfEternityClient/game_master_daemon.ps1`.
- Shining: `OtherGuides/Afterlife_Contract_Matrix.md`,
  `docs/audits/afterlife/shining-abode/Shining_Abode_Faction_Politics_Addendum.md`,
  `Examples/E_CLI_Afterlife_Turns.txt`,
  `Examples/example_validation_manifest.json`.
- Tests: `AfterlifeDocumentationCoverageTests.cs`,
  `PromptDocumentationCoverageTests.cs`,
  `ValidationSourceGuardTests.cs`,
  `ExplorerModeSourceGuardTests.cs`,
  `AfterlifeShiningPlayerFacingSourceGuardTests.cs`,
  `ShiningAbodeStateTests.cs`,
  `ExampleDocumentationValidationTests.cs`.

---

### Task 0: Add bounded Focused integration project selection

**Files:**

- Modify: `scripts/test-csharp.ps1`
- Modify: `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`
- Modify: `docs/testing.md`
- Reference:
  `docs/superpowers/specs/2026-08-03-focused-integration-selection-design.md`

- [ ] **Step 1: Add the failing closed-selector contract test**

Add this test to `FastTestBoundaryTests`:

```csharp
[Fact]
public void CSharpLaneRunner_FocusedProjectSelectorIsClosedScopedAndApplied()
{
    var runnerPath = Path.Combine(
        TestRepoPaths.RepoRoot,
        "scripts",
        "test-csharp.ps1");
    var source = File.ReadAllText(runnerPath);
    var normalized = Regex.Replace(source, @"\s+", " ");

    Assert.Contains(
        "[ValidateSet(\"Fast\", \"Integration\")] " +
        "[string]$FocusedProject = \"Fast\"",
        normalized,
        StringComparison.Ordinal);
    Assert.Contains(
        "$PSBoundParameters.ContainsKey(\"FocusedProject\")",
        source,
        StringComparison.Ordinal);
    Assert.Contains(
        "-FocusedProject is supported only with -Lane Focused.",
        source,
        StringComparison.Ordinal);
    Assert.Contains(
        "$selectedProject = if ($effectiveLane -eq \"Focused\") " +
        "{ $FocusedProject } else { $laneDefinition.Project }",
        normalized,
        StringComparison.Ordinal);
    Assert.Contains(
        "$projectPath = if ($selectedProject -eq \"Fast\")",
        normalized,
        StringComparison.Ordinal);
    Assert.Contains(
        "if ($selectedProject -eq \"Both\")",
        normalized,
        StringComparison.Ordinal);
    Assert.Contains(
        "elseif ($selectedProject -eq \"Fast\")",
        normalized,
        StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the contract test and the desired command to verify RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FastTestBoundaryTests.CSharpLaneRunner_FocusedProjectSelectorIsClosedScopedAndApplied"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName=BookOfEternityClient.Tests.IntegrationTestBoundaryTests.CSharpLaneRunner_SeparatesLifecycleIntegrationFromRoutinePreMerge"
```

Expected: the fast contract test fails because the selector is absent, and the
integration invocation fails parameter binding because `FocusedProject` is an
unknown parameter. The second failure is expected RED evidence, not a passing
test result.

- [ ] **Step 3: Add the closed parameter and derive an immutable selection**

Add the parameter after `Filter`:

```powershell
[Parameter(ParameterSetName = "Lane")]
[ValidateSet("Fast", "Integration")]
[string]$FocusedProject = "Fast",
```

Initialize `$selectedProject` next to the other effective lane values and set
it without mutating `$laneDefinitions`:

```powershell
$selectedProject = $null
if (-not $isSelfTest) {
    $effectiveLane = if ($Lane -eq "Complete") { "PreMerge" } else { $Lane }
    $laneDefinition = $laneDefinitions[$effectiveLane]
    $selectedProject = if ($effectiveLane -eq "Focused") {
        $FocusedProject
    }
    else {
        $laneDefinition.Project
    }
    $effectiveTimeoutMinutes = if ($TimeoutMinutes -gt 0) {
        $TimeoutMinutes
    }
    else {
        [int]$laneDefinition.TimeoutMinutes
    }
}
```

Reject explicit use on every non-Focused lane before running build or tests:

```powershell
if ($PSBoundParameters.ContainsKey("FocusedProject") -and
    $Lane -ne "Focused") {
    throw "-FocusedProject is supported only with -Lane Focused."
}
```

In `New-TestRuns` and the non-PreMerge build-selection block, replace only
project-routing reads of `$laneDefinition.Project` with `$selectedProject`:

```powershell
$projectPath = if ($selectedProject -eq "Fast") {
    $fastTestProject
}
else {
    $integrationTestProject
}
```

```powershell
$buildSelections = if ($selectedProject -eq "Both") {
    @(
        [pscustomobject]@{
            Name = "Build-Fast"
            ProjectPath = $fastTestProject
        },
        [pscustomobject]@{
            Name = "Build-Integration"
            ProjectPath = $integrationTestProject
        }
    )
}
elseif ($selectedProject -eq "Fast") {
    @(
        [pscustomobject]@{
            Name = "Build-Fast"
            ProjectPath = $fastTestProject
        }
    )
}
else {
    @(
        [pscustomobject]@{
            Name = "Build-Integration"
            ProjectPath = $integrationTestProject
        }
    )
}
```

Do not alter Focused's five-minute definition, parallelism, mandatory filter,
zero-result guard, process ownership, cleanup, or result merging.

- [ ] **Step 4: Document project-scoped Focused use**

In `docs/testing.md`, state that:

- Focused defaults to `BookOfEternityClient.Tests`;
- `-FocusedProject Integration` selects
  `BookOfEternityClient.IntegrationTests`;
- the selector accepts only `Fast` or `Integration` and is valid only with
  `-Lane Focused`;
- fast and integration class names must never be combined in one filter;
- both variants retain the five-minute hard limit and require `-Filter`.

Include these exact examples:

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~IntegrationTestBoundaryTests.CSharpLaneRunner_SeparatesLifecycleIntegrationFromRoutinePreMerge"
```

- [ ] **Step 5: Run exact GREEN and plan-evidence controls**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FastTestBoundaryTests.CSharpLaneRunner_FocusedProjectSelectorIsClosedScopedAndApplied"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName=BookOfEternityClient.Tests.IntegrationTestBoundaryTests.CSharpLaneRunner_SeparatesLifecycleIntegrationFromRoutinePreMerge"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FastTestBoundaryTests" -PlanOnly
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~IntegrationTestBoundaryTests" -PlanOnly
```

Expected: both exact tests pass; the first plan names
`BookOfEternityClient.Tests.csproj`, the second names
`BookOfEternityClient.IntegrationTests.csproj`; neither command widens the
Focused timeout or concurrency.

- [ ] **Step 6: Commit**

```powershell
git add -- scripts/test-csharp.ps1 BookOfEternityClient.Tests/FastTestBoundaryTests.cs docs/testing.md
git commit -m "test: allow focused integration selection (#1510)"
```

---

### Task 1: Add the closed common Faction Materialization kernel

**Files:**

- Create: `BookOfEternityClient/Services/FactionMaterializationContract.cs`
- Create: `BookOfEternityClient.Tests/FactionMaterializationContractTests.cs`
- Reference: `BookOfEternityClient/Services/ActorMaterializationContract.cs`
- Reference:
  `specs/1510-complete-faction-materialization/contracts/faction-materialization-envelope.md`

**Interfaces:**

- Consumes: `JsonElement`, `ValidationIssue`, exact section/capability evidence.
- Produces:
  `FactionMaterializationFamily`,
  `FactionMaterializationEvidence`,
  `FactionMaterializationContract.Validate(...)`, and
  `FactionMaterializationContract.ValidateUniqueMaterializationIds(...)`.

- [ ] **Step 1: Write the failing contract test and shared test builders**

Add this shape to
`BookOfEternityClient.Tests/FactionMaterializationContractTests.cs`:

```csharp
using System.Text.Json;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

public sealed class FactionMaterializationContractTests
{
    [Fact]
    public void Validate_MortalMissingRequiredSection_ReportsStableIssue()
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson(
            sectionsOverride: """
            {
              "hierarchy": { "state": "empty_by_design", "reason": "No ranks exist yet." },
              "resources": { "state": "empty_by_design", "reason": "Members contribute personally." },
              "relations": { "state": "empty_by_design", "reason": "No formal relations exist." },
              "projects": { "state": "empty_by_design", "reason": "No chartered projects exist." },
              "territoryAndInfluence": { "state": "empty_by_design", "reason": "No territory is claimed." },
              "playerMembership": { "state": "empty_by_design", "reason": "The player is not a member." }
            }
            """));

        var evidence = EmptyMortalEvidence("faction_watch");
        var issues = FactionMaterializationContract.Validate(
            document.RootElement,
            "faction_core.factions[0]",
            FactionMaterializationFamily.Mortal,
            evidence,
            requireEnvelope: true);

        var issue = Assert.Single(issues.Where(item =>
            item.Code == "faction_materialization_section_missing"));
        Assert.Equal("mortal_faction:faction_watch", issue.Actor);
        Assert.Contains("customStates", issue.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EmptyByDesignWithContent_ReportsDispositionMismatch()
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson());
        var evidence = EmptyMortalEvidence("faction_watch") with
        {
            SectionHasContent = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hierarchy"] = true,
                ["resources"] = false,
                ["relations"] = false,
                ["projects"] = false,
                ["territoryAndInfluence"] = false,
                ["playerMembership"] = false,
                ["customStates"] = false
            }
        };

        var issues = FactionMaterializationContract.Validate(
            document.RootElement,
            "faction_core.factions[0]",
            FactionMaterializationFamily.Mortal,
            evidence,
            requireEnvelope: true);

        Assert.Contains(issues, item =>
            item.Code == "faction_materialization_disposition_mismatch" &&
            item.Actor == "mortal_faction:faction_watch");
    }

    private static FactionMaterializationEvidence EmptyMortalEvidence(string factionId) =>
        new(
            "mortal_faction",
            factionId,
            MortalEvidence(false),
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hasFormalHierarchy"] = false,
                ["usesFactionResources"] = false,
                ["maintainsRelations"] = false,
                ["runsProjects"] = false,
                ["holdsTerritoryOrInfluence"] = false,
                ["supportsPlayerMembership"] = false,
                ["usesCustomMechanics"] = false
            },
            MortalEvidence(true));

    private static Dictionary<string, bool> MortalEvidence(bool value) =>
        new(StringComparer.Ordinal)
        {
            ["hierarchy"] = value,
            ["resources"] = value,
            ["relations"] = value,
            ["projects"] = value,
            ["territoryAndInfluence"] = value,
            ["playerMembership"] = value,
            ["customStates"] = value
        };

    private static string BuildMortalFactionJson(string? sectionsOverride = null) => $$"""
    {
      "factionId": "faction_watch",
      "materialization": {
        "schemaVersion": 1,
        "materializationId": "fmat_watch",
        "factionType": "mortal_faction",
        "factionId": "faction_watch",
        "materializedAtTurn": 12,
        "state": "complete",
        "capabilities": {
          "hasFormalHierarchy": false,
          "usesFactionResources": false,
          "maintainsRelations": false,
          "runsProjects": false,
          "holdsTerritoryOrInfluence": false,
          "supportsPlayerMembership": false,
          "usesCustomMechanics": false
        },
        "sections": {{sectionsOverride ?? """
        {
          "hierarchy": { "state": "empty_by_design", "reason": "No ranks exist yet." },
          "resources": { "state": "empty_by_design", "reason": "Members contribute personally." },
          "relations": { "state": "empty_by_design", "reason": "No formal relations exist." },
          "projects": { "state": "empty_by_design", "reason": "No chartered projects exist." },
          "territoryAndInfluence": { "state": "empty_by_design", "reason": "No territory is claimed." },
          "playerMembership": { "state": "empty_by_design", "reason": "The player is not a member." },
          "customStates": { "state": "empty_by_design", "reason": "No custom mechanic exists." }
        }
        """}}
      }
    }
    """;
}
```

Add tests in the same class for both profiles, duplicate envelope properties,
duplicate nested members, unknown members, invalid scalars, populated reasons,
whitespace empty reasons, exact-empty absence, capability mismatch, identity
mismatch, and duplicate `materializationId`.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FactionMaterializationContractTests"
```

Expected: build/test failure because `FactionMaterializationContract`,
`FactionMaterializationFamily`, and `FactionMaterializationEvidence` do not
exist.

- [ ] **Step 3: Add the exact public-internal contract surface**

Create `BookOfEternityClient/Services/FactionMaterializationContract.cs` with
these declarations and exact closed sets:

```csharp
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal enum FactionMaterializationFamily
{
    Mortal,
    Shining
}

internal sealed record FactionMaterializationEvidence(
    string FactionType,
    string FactionId,
    IReadOnlyDictionary<string, bool> SectionHasContent,
    IReadOnlyDictionary<string, bool> CapabilityEvidence,
    IReadOnlyDictionary<string, bool> SectionHasCanonicalEmptySurface);

internal static class FactionMaterializationContract
{
    internal const string PropertyName = "materialization";
    internal const int SchemaVersion = 1;

    private static readonly HashSet<string> EnvelopeFields = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "materializationId",
        "factionType",
        "factionId",
        "materializedAtTurn",
        "state",
        "capabilities",
        "sections"
    };

    private static readonly HashSet<string> DispositionFields =
        new(StringComparer.Ordinal) { "state", "reason" };

    private static readonly HashSet<string> MortalCapabilities = new(StringComparer.Ordinal)
    {
        "hasFormalHierarchy",
        "usesFactionResources",
        "maintainsRelations",
        "runsProjects",
        "holdsTerritoryOrInfluence",
        "supportsPlayerMembership",
        "usesCustomMechanics"
    };

    private static readonly HashSet<string> MortalSections = new(StringComparer.Ordinal)
    {
        "hierarchy",
        "resources",
        "relations",
        "projects",
        "territoryAndInfluence",
        "playerMembership",
        "customStates"
    };

    private static readonly HashSet<string> ShiningCapabilities = new(StringComparer.Ordinal)
    {
        "runsProjects",
        "holdsTerritorialInfluence",
        "usesResourceLedger",
        "hasResidentAffiliations",
        "canTrade",
        "hasLeadershipHistory",
        "usesStoryState"
    };

    private static readonly HashSet<string> ShiningSections = new(StringComparer.Ordinal)
    {
        "projects",
        "territorialInfluence",
        "resourceLedger",
        "residentAffiliations",
        "trade",
        "leadershipHistory",
        "storyState"
    };

    private static readonly IReadOnlyDictionary<string, string>
        MortalCapabilitySections =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["hasFormalHierarchy"] = "hierarchy",
                ["usesFactionResources"] = "resources",
                ["maintainsRelations"] = "relations",
                ["runsProjects"] = "projects",
                ["holdsTerritoryOrInfluence"] = "territoryAndInfluence",
                ["supportsPlayerMembership"] = "playerMembership",
                ["usesCustomMechanics"] = "customStates"
            };

    private static readonly IReadOnlyDictionary<string, string>
        ShiningDirectCapabilitySections =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["runsProjects"] = "projects",
                ["holdsTerritorialInfluence"] = "territorialInfluence",
                ["usesResourceLedger"] = "resourceLedger",
                ["hasResidentAffiliations"] = "residentAffiliations",
                ["hasLeadershipHistory"] = "leadershipHistory",
                ["usesStoryState"] = "storyState"
            };

    internal static IReadOnlyList<ValidationIssue> Validate(
        JsonElement faction,
        string context,
        FactionMaterializationFamily family,
        FactionMaterializationEvidence evidence,
        bool requireEnvelope,
        bool deferEvidenceConsistency = false)
    {
        var issues = new List<ValidationIssue>();
        if (faction.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Invalid(
                context,
                evidence,
                "object faction carrier",
                faction.ValueKind.ToString()));
            return issues;
        }

        ValidateDuplicateEnvelopeProperty(faction, context, evidence, issues);

        if (!faction.TryGetProperty(PropertyName, out var envelope))
        {
            if (requireEnvelope)
                issues.Add(Missing(context, evidence));
            return issues;
        }

        if (envelope.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Invalid($"{context}.{PropertyName}", evidence, "object", envelope.ValueKind.ToString()));
            return issues;
        }

        ValidateExactFields(envelope, EnvelopeFields, $"{context}.{PropertyName}", evidence, issues);
        ValidateScalars(envelope, context, evidence, issues);
        ValidateCapabilities(envelope, context, family, evidence, issues, deferEvidenceConsistency);
        ValidateSections(envelope, context, family, evidence, issues, deferEvidenceConsistency);
        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateUniqueMaterializationIds(
        IReadOnlyList<(JsonElement Faction, string Context, string FactionType, string FactionId)> factions)
    {
        var issues = new List<ValidationIssue>();
        var firstContextById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in factions)
        {
            if (item.Faction.ValueKind != JsonValueKind.Object ||
                !item.Faction.TryGetProperty(PropertyName, out var envelope) ||
                envelope.ValueKind != JsonValueKind.Object ||
                !envelope.TryGetProperty("materializationId", out var idNode) ||
                idNode.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(idNode.GetString()))
            {
                continue;
            }

            var id = idNode.GetString()!;
            if (firstContextById.TryAdd(id, item.Context))
                continue;

            issues.Add(new ValidationIssue(
                $"{item.Context}.{PropertyName}.materializationId",
                IssueSeverity.Error,
                "Faction materializationId must be unique.",
                code: "faction_materialization_duplicate_id",
                actor: $"{item.FactionType}:{item.FactionId}",
                section: "FactionMaterialization",
                expected: $"unique value; first declared at {firstContextById[id]}",
                actual: id,
                repairHint: "Assign a new stable ID only to the unaccepted materialization; preserve accepted historical receipts."));
        }

        return issues;
    }
}
```

Implement the private helpers in that same file with the exact behavior already
tested in `ActorMaterializationContract`: case-sensitive field names,
`schemaVersion == 1`, non-empty strings, `materializedAtTurn >= 0`,
`state == "complete"`, exact outer/envelope identity, boolean capabilities,
closed sections, and stable issue codes from the envelope contract. Iterate
`MortalCapabilitySections` and `ShiningDirectCapabilitySections` for the exact
capability-to-section comparisons; never infer a mapping from `HashSet`
enumeration order. Validate Shining `canTrade` separately against
`CapabilityEvidence["canTrade"]` and the `trade` section disposition against
`SectionEvidence["trade"]`.

- [ ] **Step 4: Run the contract tests to GREEN**

Run the Step 2 command.

Expected: every `FactionMaterializationContractTests` case passes; no other test
project is selected.

- [ ] **Step 5: Commit**

```powershell
git add -- 'BookOfEternityClient/Services/FactionMaterializationContract.cs' 'BookOfEternityClient.Tests/FactionMaterializationContractTests.cs'
git commit -m "feat: add faction materialization contract (#1510)"
```

---

### Task 2: Add duplicate-sensitive touch classification and validation phase

**Files:**

- Create:
  `BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs`
- Create:
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
- Modify:
  `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:365`
- Modify:
  `BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs:1`
- Modify:
  `BookOfEternityClient.IntegrationTests/IntegrationValidationProfiles.cs:1`
- Modify:
  `BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs:80`
- Test: `BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs`
- Test: `BookOfEternityClient.IntegrationTests/FullValidationEquivalenceTests.cs`
- Test: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`

**Interfaces:**

- Consumes: validated pending-turn snapshot lookup and current canonical/raw
  files.
- Produces:
  `FactionTouchKind`,
  `ValidateAcceptedTurnRawFactionMaterializationAsync()`, and
  `ValidateAcceptedTurnFactionMaterializationCompletenessAsync(List<ValidationIssue>)`,
  plus private
  `ValidateAcceptedTurnMortalFactionMaterializationContinuityAsync(bool,
  List<ValidationIssue>, List<(JsonElement Faction, string Context, string
  FactionType, string FactionId)>)` and
  `ValidateAcceptedTurnShiningFactionMaterializationContinuityAsync(bool,
  List<ValidationIssue>, List<(JsonElement Faction, string Context, string
  FactionType, string FactionId)>)`.

- [ ] **Step 1: Write classifier and continuity RED tests**

Start
`BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
with the existing integration filesystem fixture pattern and add these
assertions:

```csharp
[Theory]
[InlineData(false, false, true, false, FactionTouchKind.New)]
[InlineData(true, false, true, false, FactionTouchKind.LegacyPromotion)]
[InlineData(true, true, true, false, FactionTouchKind.AlreadyMaterialized)]
[InlineData(true, true, false, false, FactionTouchKind.AlreadyMaterialized)]
[InlineData(true, false, false, false, FactionTouchKind.UntouchedLegacy)]
[InlineData(true, true, false, true, FactionTouchKind.ClientDerivedOnly)]
public void Classify_ReturnsExpectedTouchKind(
    bool existedPreTurn,
    bool hadReceiptPreTurn,
    bool gmAuthoredTouch,
    bool derivedOnly,
    FactionTouchKind expected)
{
    Assert.Equal(
        expected,
        FactionTouchClassifier.Classify(
            existedPreTurn,
            hadReceiptPreTurn,
            gmAuthoredTouch,
            derivedOnly));
}

[Fact]
public async Task Validate_ChangedHistoricalEnvelope_ReportsImmutableFailure()
{
    await WriteValidatedMortalFactionAsync(
        preTurnMaterializationId: "fmat_original",
        currentMaterializationId: "fmat_changed");

    var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

    Assert.Contains(issues, issue =>
        issue.Code == "faction_materialization_immutable_receipt_changed" &&
        issue.Actor == "mortal_faction:faction_watch");
}

[Fact]
public async Task Validate_DuplicateMaterializationIdAcrossFamilies_ReportsDuplicate()
{
    await WriteMortalAndShiningCreationsAsync(
        mortalMaterializationId: "fmat_shared",
        shiningMaterializationId: "fmat_shared");

    var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

    Assert.Contains(issues, issue =>
        issue.Code == "faction_materialization_duplicate_id" &&
        issue.Actor == "shining_faction:order_dawn");
}
```

Add integration cases for duplicate pre-turn members, exact case-sensitive ID
maps, absent usable snapshot during a mutation, untouched legacy, and
derived-only Shining changes.

- [ ] **Step 2: Write phase-selection RED tests**

In the three existing phase/boundary test files, assert:

```csharp
Assert.True(
    GameStateValidationPhase.All.HasFlag(
        GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness));
Assert.True(
    GameStateValidationPhase.Selectable.HasFlag(
        GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness));
Assert.True(
    IntegrationValidationProfiles.FactionMaterialization.HasFlag(
        GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness));
```

Also assert that `FactionState` and `ShiningState` include the phase and that
FullValidation equivalence counts/selects it exactly once.

- [ ] **Step 3: Run the focused tests and verify RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FactionMaterializationValidationTests|FullyQualifiedName~FullValidationEquivalenceTests|FullyQualifiedName~IntegrationTestBoundaryTests"
```

Expected: build failures for the new classifier, validation methods, profile,
and phase flag.

- [ ] **Step 4: Implement the classifier and orchestration**

Add these exact types/methods to
`ValidationService.FactionMaterializationContinuity.cs`:

```csharp
namespace BookOfEternityClient.Services;

internal enum FactionTouchKind
{
    New,
    LegacyPromotion,
    AlreadyMaterialized,
    ClientDerivedOnly,
    UntouchedLegacy
}

internal static class FactionTouchClassifier
{
    internal static FactionTouchKind Classify(
        bool existedPreTurn,
        bool hadReceiptPreTurn,
        bool gmAuthoredTouch,
        bool clientDerivedOnly)
    {
        if (!existedPreTurn)
            return FactionTouchKind.New;
        if (clientDerivedOnly && !gmAuthoredTouch)
            return FactionTouchKind.ClientDerivedOnly;
        if (hadReceiptPreTurn)
            return FactionTouchKind.AlreadyMaterialized;
        return gmAuthoredTouch
            ? FactionTouchKind.LegacyPromotion
            : FactionTouchKind.UntouchedLegacy;
    }
}

public partial class ValidationService
{
    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawFactionMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        var materializedFactions =
            new List<(
                JsonElement Faction,
                string Context,
                string FactionType,
                string FactionId)>();
        await ValidateAcceptedTurnMortalFactionMaterializationContinuityAsync(
            rawBeforeNormalization: true,
            issues,
            materializedFactions);
        await ValidateAcceptedTurnShiningFactionMaterializationContinuityAsync(
            rawBeforeNormalization: true,
            issues,
            materializedFactions);
        issues.AddRange(
            FactionMaterializationContract.ValidateUniqueMaterializationIds(
                materializedFactions));
        return issues;
    }

    private async Task ValidateAcceptedTurnFactionMaterializationCompletenessAsync(
        List<ValidationIssue> issues)
    {
        var materializedFactions =
            new List<(
                JsonElement Faction,
                string Context,
                string FactionType,
                string FactionId)>();
        await ValidateAcceptedTurnMortalFactionMaterializationContinuityAsync(
            rawBeforeNormalization: false,
            issues,
            materializedFactions);
        await ValidateAcceptedTurnShiningFactionMaterializationContinuityAsync(
            rawBeforeNormalization: false,
            issues,
            materializedFactions);
        issues.AddRange(
            FactionMaterializationContract.ValidateUniqueMaterializationIds(
                materializedFactions));
    }
}
```

Use `TryFindDuplicateJsonProperty`, `LoadValidatedPendingTurnSnapshotLookupAsync`,
and `ReadValidatedPendingTurnSnapshotFileAsync` exactly as
`ValidationService.NpcCoreChanges.cs` does. Compare historical envelopes by
parsing both as `JsonNode` and `JsonNode.DeepEquals`; never compare raw text or
member order. The two continuity methods are implemented in this task; they
load/classify their domain, enforce receipt presence/closed shape and immutable
pre-turn equality, and add every current faction carrying a receipt—including
untouched already-materialized factions—exactly once per case-sensitive
`(factionType, factionId)` coordinate as
`(faction.Clone(), context, factionType, factionId)` to the shared list so the
`JsonElement` remains valid after its source `JsonDocument` is disposed. The
orchestrator calls `ValidateUniqueMaterializationIds` once, after both domains
have contributed, so a new receipt cannot collide with existing history and
duplicate IDs cannot cross the Mortal/Shining boundary. A duplicate logical
identity across canonical/full carriers is reported by the separate
duplicate-identity/full-resend rule and is not added twice to the uniqueness
list. Tasks 3 and 6 add
domain completeness calls to these same two public orchestrators; Task 2 does
not reference methods that do not exist yet.

The continuity methods apply the common contract with empty live-evidence
dictionaries and `deferEvidenceConsistency: true`; Tasks 3/6 perform the one
raw full-evidence check for new/promoted targets:

```csharp
var requireEnvelope = touchKind is
    FactionTouchKind.New or
    FactionTouchKind.LegacyPromotion or
    FactionTouchKind.AlreadyMaterialized;
if (touchKind == FactionTouchKind.ClientDerivedOnly &&
    hadReceiptPreTurn)
{
    requireEnvelope = true;
}

var structuralEvidence = new FactionMaterializationEvidence(
    factionType,
    factionId,
    new Dictionary<string, bool>(StringComparer.Ordinal),
    new Dictionary<string, bool>(StringComparer.Ordinal),
    new Dictionary<string, bool>(StringComparer.Ordinal));

issues.AddRange(FactionMaterializationContract.Validate(
    faction,
    context,
    family,
    structuralEvidence,
    requireEnvelope,
    deferEvidenceConsistency: true));
```

For `ClientDerivedOnly`, require a receipt only when the pre-turn faction
already had one; an unmaterialized legacy projection remains unpromoted.
`UntouchedLegacy` does not require an envelope.

- [ ] **Step 5: Wire the raw fence**

Change `CollectAcceptedTurnRawStateIssuesAsync` in
`GameEngine.ValidationAndRepair.cs` to:

```csharp
private async Task<List<ValidationIssue>> CollectAcceptedTurnRawStateIssuesAsync()
{
    var issues = await _criticalStateHealth.ValidateAcceptedTurnRawStateAsync();
    issues.AddRange(await _validator.ValidateNpcCoreChangesBeforeNormalizationAsync());
    issues.AddRange(await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync());
    issues.AddRange(await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync());
    return issues;
}
```

`ValidateFactionCoreChangesBeforeNormalizationAsync` is introduced in Task 5;
until Task 5 lands, add the line in that task rather than leaving uncompilable
code in this commit.

- [ ] **Step 6: Add and invoke the phase**

Add the next unused bit to `GameStateValidationPhase.cs`:

```csharp
AcceptedTurnFactionMaterializationCompleteness = 1u << 28,
All = ((1u << 26) - 1) |
      AcceptedTurnFactionMaterializationCompleteness,
Selectable = All |
             RivalAndResidentCrossReferences |
             GuardianProjectStateFiles,
```

This preserves the existing deliberate treatment of bits 26 and 27 while
including the new phase in `All` and therefore `Selectable`. Add:

```csharp
public const GameStateValidationPhase FactionMaterialization =
    GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness;
```

to `BookOfEternityClient.IntegrationTests/IntegrationValidationProfiles.cs`,
and OR it into `FactionState` and `ShiningState`. In
`ValidationService.ValidationPhases.cs`, add:

```csharp
if (phases.HasFlag(
        GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness))
{
    await ValidateAcceptedTurnFactionMaterializationCompletenessAsync(issues);
}
```

- [ ] **Step 7: Run focused tests to GREEN**

Run the Step 3 command.

Expected: classifier, continuity, phase selection, equivalence, and boundary
tests pass.

- [ ] **Step 8: Commit**

```powershell
git add -- 'BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs' 'BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs' 'BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs' 'BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs' 'BookOfEternityClient.IntegrationTests/IntegrationValidationProfiles.cs' 'BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs' 'BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs' 'BookOfEternityClient.IntegrationTests/FullValidationEquivalenceTests.cs' 'BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs'
git commit -m "feat: classify faction materialization turns (#1510)"
```

---

### Task 3: Enforce Mortal semantic core and atomic bundle evidence

**Files:**

- Create:
  `BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs`
- Modify:
  `BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs`
- Modify:
  `BookOfEternityClient/Services/Validation/ValidationService.FactionsAndProjects.cs:668`
- Modify:
  `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:7493`
- Test:
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
- Reference:
  `specs/1510-complete-faction-materialization/contracts/mortal-faction-authority.md`

**Interfaces:**

- Consumes: full/current/pre-turn Mortal core and five sidecars plus location/NPC
  authority.
- Produces:
  `ValidateAcceptedTurnMortalFactionMaterializationCompletenessAsync(bool,
  List<ValidationIssue>)`
  and exact `FactionMaterializationEvidence` per target.

- [ ] **Step 1: Add failing Mortal semantic and empty-surface tests**

Add table-driven mutation cases to
`FactionMaterializationValidationTests.cs`:

```csharp
public static TheoryData<string, string> MissingMortalSemantics => new()
{
    { "factionColor", "faction_materialization_mortal_color_missing" },
    { "purpose", "faction_materialization_mortal_purpose_missing" },
    { "currentAgenda", "faction_materialization_mortal_agenda_missing" },
    { "principles", "faction_materialization_mortal_principles_missing" },
    { "memory", "faction_materialization_mortal_memory_missing" },
    { "governance", "faction_materialization_mortal_governance_missing" },
    { "leadership", "faction_materialization_mortal_leadership_missing" },
    { "scribeChronicle", "faction_materialization_mortal_chronicle_missing" }
};

[Theory]
[MemberData(nameof(MissingMortalSemantics))]
public async Task NewMortalFaction_MissingSemanticField_FailsRaw(
    string propertyName,
    string expectedCode)
{
    var faction = BuildCompleteMortalCreation();
    faction.Remove(propertyName);
    await WriteMortalCreationAsync(faction);

    var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

    Assert.Contains(issues, issue =>
        issue.Code == expectedCode &&
        issue.Actor == "mortal_faction:temp-faction-watch");
}

[Fact]
public async Task NewMortalFaction_AllSevenExactEmptySurfaces_Passes()
{
    await WriteMortalCreationAsync(BuildCompleteMinimalMortalCreation());

    var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

    Assert.DoesNotContain(issues, issue =>
        issue.Severity == IssueSeverity.Error &&
        issue.Actor == "mortal_faction:temp-faction-watch");
}
```

`BuildCompleteMortalCreation` and
`BuildCompleteMinimalMortalCreation` use `factionId=null`,
`initialId="temp-faction-watch"`, `isNewFaction=true`, and the same temporary
ID in the raw envelope. The normalizer preserves that exact value as the
permanent canonical `factionId`; no display-name-derived remapping is allowed.

Add cases that remove the structure/resource/custom target entry after
normalization, add a project while disposition is empty, target an unknown
relation/NPC/location, use a colliding `initialId`, use non-empty leader IDs
with `vacant`, and omit an exact player non-member value.

- [ ] **Step 2: Run Mortal tests and verify RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FactionMaterializationValidationTests"
```

Expected: new Mortal cases fail because no Mortal evidence builder enforces the
semantic/bundle contract.

- [ ] **Step 3: Implement exact Mortal semantic validators**

In `ValidationService.MortalFactionMaterialization.cs`, define the closed
leadership set and semantic validators. Run these semantic/core/sidecar shape
checks for every current Mortal faction carrying a receipt, plus each
new/promotion candidate that is required to carry one; skip only exact
untouched legacy factions. Add `using System.Text.RegularExpressions;`:

```csharp
private static readonly HashSet<string> MortalLeadershipStates =
    new(StringComparer.Ordinal)
    {
        "headed",
        "collective",
        "vacant"
    };

private static readonly Regex MortalFactionColorRegex =
    new(
        "^#[0-9A-Fa-f]{6}$",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant);

private static void ValidateMortalSemanticCore(
    JsonElement faction,
    string context,
    string factionId,
    bool hasExistingChronicle,
    List<ValidationIssue> issues)
{
    var factionColor = RequireString(
        faction,
        context,
        issues,
        "factionColor");
    if (!string.IsNullOrWhiteSpace(factionColor) &&
        !MortalFactionColorRegex.IsMatch(factionColor))
    {
        issues.Add(FactionIssue(
            $"{context}.factionColor",
            "faction_materialization_mortal_color_invalid",
            factionId,
            "A materialized Mortal faction requires exact #RRGGBB visual identity."));
    }

    RequireString(faction, context, issues, "purpose");
    RequireString(faction, context, issues, "currentAgenda");
    RequireArrayOfStrings(faction, context, issues, "principles");
    ValidateMortalFactionMemory(faction, context, factionId, issues);
    ValidateMortalFactionGovernance(faction, context, factionId, issues);
    ValidateMortalFactionLeadership(faction, context, factionId, issues);

    var hasCreationChronicle =
        faction.TryGetProperty("scribeChronicle", out var chronicle) &&
        chronicle.ValueKind == JsonValueKind.Array &&
        chronicle.GetArrayLength() > 0;
    if (!hasExistingChronicle && !hasCreationChronicle)
    {
        issues.Add(FactionIssue(
            $"{context}.scribeChronicle",
            "faction_materialization_mortal_chronicle_missing",
            factionId,
            "A new/promoted Mortal faction requires existing history or at least one creation chronicle entry."));
    }
}
```

Implement `ValidateMortalFactionMemory` with exact members
`summary`, `lastUpdatedTurn`, `enduringFacts`, `openThreads`;
`ValidateMortalFactionGovernance` with `model`, `decisionProcess`; and
`ValidateMortalFactionLeadership` with exact members
`leadershipState`, `summary`, `leaderNpcIds`. Reject duplicate IDs, unknown
members, invalid states, a vacant non-empty list, and a headed empty list.
`principles` is non-empty, contains unique non-whitespace strings, and
`memory.lastUpdatedTurn` is a non-negative integer.

- [ ] **Step 4: Build exact bundle evidence**

For every classified target, build:

```csharp
var evidence = new FactionMaterializationEvidence(
    "mortal_faction",
    factionId,
    new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["hierarchy"] = HasFormalMortalHierarchy(structure),
        ["resources"] = HasMortalResources(resources),
        ["relations"] = HasObjectArrayEntries(faction, "relations"),
        ["projects"] = activeProjects.Count + completedProjects.Count > 0,
        ["territoryAndInfluence"] = HasMortalTerritoryOrInfluence(faction, locationAuthority),
        ["playerMembership"] = HasMortalPlayerMembership(faction),
        ["customStates"] = HasMortalCustomStates(custom)
    },
    new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["hasFormalHierarchy"] = HasFormalMortalHierarchy(structure),
        ["usesFactionResources"] = HasMortalResources(resources),
        ["maintainsRelations"] = HasObjectArrayEntries(faction, "relations"),
        ["runsProjects"] = activeProjects.Count + completedProjects.Count > 0,
        ["holdsTerritoryOrInfluence"] = HasMortalTerritoryOrInfluence(faction, locationAuthority),
        ["supportsPlayerMembership"] = HasMortalPlayerMembership(faction),
        ["usesCustomMechanics"] = HasMortalCustomStates(custom)
    },
    new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["hierarchy"] = HasExactEmptyMortalHierarchy(structure),
        ["resources"] = HasExactEmptyMortalResources(resources),
        ["relations"] = HasExactEmptyArray(faction, "relations"),
        ["projects"] = HasExplicitEmptyProjectCarrier(faction) &&
                       activeProjects.Count == 0 &&
                       completedProjects.Count == 0,
        ["territoryAndInfluence"] = HasExactEmptyMortalTerritory(faction, locationAuthority),
        ["playerMembership"] = HasExactEmptyMortalPlayerMembership(faction),
        ["customStates"] = HasExactEmptyMortalCustomStates(custom)
    });
```

When `rawBeforeNormalization=true`, build hierarchy/resource/custom/project
evidence from the complete `factionDataChanges[]` carrier and do not require
sidecar records that the normalizer has not created yet. When it is `false`,
require exact structure/resource/custom target records for new/promoted and
every receipt-bearing faction even when their live content is empty; project
emptiness is canonical absence of target rows plus the already-validated raw
carrier proof. Validate exact relation target IDs, location links, NPC
affiliations/leader IDs, and all same-turn references against one effective ID.
For the raw `New`/`LegacyPromotion` target, call
`FactionMaterializationContract.Validate` with full evidence,
`requireEnvelope: true`, and `deferEvidenceConsistency: false`. Canonical
post-normalization validation requires the complete live semantic/cross-file
shape but does not compare that evolved state back to the immutable snapshot;
Task 2 already validates the closed receipt and continuity with deferred live
evidence. Candidate collection and `ValidateUniqueMaterializationIds` remain
owned exclusively by Task 2.

Add the Mortal completeness call to both Task 2 orchestrators immediately
after Mortal continuity collection and before the shared uniqueness call:

```csharp
// ValidateAcceptedTurnRawFactionMaterializationAsync
await ValidateAcceptedTurnMortalFactionMaterializationCompletenessAsync(
    rawBeforeNormalization: true,
    issues);

// ValidateAcceptedTurnFactionMaterializationCompletenessAsync
await ValidateAcceptedTurnMortalFactionMaterializationCompletenessAsync(
    rawBeforeNormalization: false,
    issues);
```

- [ ] **Step 5: Tighten existing full and sidecar validators**

In `ValidationService.FactionsAndProjects.cs`, call the new semantic validators
only for full creation/promotion candidates and forbid a full carrier targeting
an already materialized pre-turn ID. In
`ValidationService.LifecycleControlAndStateFiles.cs`, require `governance` and
`leadership` alongside `ranks` and `structuredBonuses` for materialized
structure entries:

```csharp
var requiredStructureFields = new[]
{
    "governance",
    "leadership",
    "ranks",
    "structuredBonuses"
};
```

Preserve the legacy validator path when the exact faction has no materialization
receipt and is untouched.

- [ ] **Step 6: Run Mortal tests to GREEN**

Run the Step 2 command.

Expected: populated and minimal creations pass; every missing/contradictory
semantic, sidecar, and cross-reference case reports an exact faction coordinate.

- [ ] **Step 7: Commit**

```powershell
git add -- 'BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs' 'BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs' 'BookOfEternityClient/Services/Validation/ValidationService.FactionsAndProjects.cs' 'BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs' 'BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs'
git commit -m "feat: validate mortal faction bundles (#1510)"
```

---

### Task 4: Normalize Mortal sidecars and persist creation chronicles

**Files:**

- Create:
  `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Factions.cs`
- Modify:
  `BookOfEternityClient/Services/CanonicalStateNormalizer.cs:190`
- Modify:
  `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:7`
- Modify:
  `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.FactionAndInventoryHelpers.cs:9`

**Interfaces:**

- Consumes: validated `factionDataChanges[]` and backup state.
- Produces: target-bound core/structure/resource/project/custom/chronicle state
  with carrier fields consumed.

- [ ] **Step 1: Write failing extraction and chronicle tests**

Add:

```csharp
[Fact]
public async Task Normalize_MaterializedCreation_ExtractsEverySidecarAndChronicle()
{
    await WriteFactionCoreAsync(BuildCompleteMinimalMortalCreation());

    await _normalizer.NormalizeAccumulatedStateAsync(_backups);

    var core = await ReadObjectAsync("game_state/factions/faction_core.json");
    var faction = Assert.IsType<JsonObject>(
        Assert.Single(core["factions"]!.AsArray()));
    Assert.Equal(
        "temp-faction-watch",
        faction["factionId"]!.GetValue<string>());
    Assert.NotNull(faction["materialization"]);
    Assert.False(faction.ContainsKey("resources"));
    Assert.False(faction.ContainsKey("ranks"));
    Assert.False(faction.ContainsKey("activeProjects"));
    Assert.False(faction.ContainsKey("completedProjects"));
    Assert.False(faction.ContainsKey("customStates"));
    Assert.False(faction.ContainsKey("scribeChronicle"));

    AssertExactEmptyStructure(await ReadObjectAsync(
        "game_state/factions/faction_structure.json"));
    AssertExactEmptyResources(await ReadObjectAsync(
        "game_state/factions/faction_resources.json"));
    AssertExactEmptyCustom(await ReadObjectAsync(
        "game_state/factions/faction_custom.json"));

    var chronicles = await ReadObjectAsync(
        "game_state/factions/faction_chronicles.json");
    var entry = Assert.IsType<JsonObject>(
        Assert.Single(chronicles["entries"]!.AsArray()));
    Assert.Equal(
        "temp-faction-watch",
        entry["factionId"]!.GetValue<string>());
    Assert.Equal(
        "#12 - The Wayfarer Watch took responsibility for the western road.",
        entry["entry"]!.GetValue<string>());
    Assert.False(entry.ContainsKey("timestamp"));
}
```

Add tests for populated sidecars, stable same-turn ID binding, existing
chronicle preservation during promotion, explicit project emptiness, and
untouched legacy core preservation.

- [ ] **Step 2: Run the normalizer filter and verify RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~CanonicalStateNormalizerTests"
```

Expected: the new faction normalizer tests fail because `scribeChronicle` is
discarded and materialized core still contains carrier-owned fields.

- [ ] **Step 3: Add deterministic chronicle extraction**

In `CanonicalStateNormalizer.FactionAndInventoryHelpers.cs`, add:

```csharp
private static IEnumerable<JsonObject> CollectInitialFactionChronicleEntries(
    JsonNode? factionCoreRoot)
{
    foreach (var rawFaction in CollectFactionEntryObjects(
                 factionCoreRoot,
                 "factionDataChanges"))
    {
        var faction = NormalizeFactionCoreEntry(rawFaction);
        var factionId = GetNodeString(faction["factionId"]);
        var factionName =
            GetNodeString(faction["factionName"]) ??
            GetNodeString(faction["name"]);
        if (string.IsNullOrWhiteSpace(factionId) ||
            faction["scribeChronicle"] is not JsonArray entries)
        {
            continue;
        }

        foreach (var entryNode in entries)
        {
            var entry = GetNodeString(entryNode);
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            yield return new JsonObject
            {
                ["factionId"] = factionId,
                ["factionName"] = factionName,
                ["entry"] = entry
            };
        }
    }
}
```

Do not add `DateTime.UtcNow`; the accepted turn prefix is deterministic.

- [ ] **Step 4: Strip only materialized carrier-owned fields**

Add:

```csharp
private static void RemoveMaterializedFactionCarrierFields(JsonObject entry)
{
    if (entry[FactionMaterializationContract.PropertyName] is not JsonObject)
        return;

    foreach (var field in new[]
             {
                 "governance",
                 "leadership",
                 "ranks",
                 "structuredBonuses",
                 "resources",
                 "activeProjects",
                 "completedProjects",
                 "customStates",
                 "scribeChronicle"
             })
    {
        entry.Remove(field);
    }
}

private static JsonObject NormalizeFactionCoreForStorage(JsonObject rawEntry)
{
    var entry = NormalizeFactionCoreEntry(rawEntry);
    RemoveMaterializedFactionCarrierFields(entry);
    return entry;
}
```

Do not call the stripping helper from `NormalizeFactionCoreEntry`: all sidecar
collectors intentionally use that existing unstripped helper. Change only the
two `NormalizeFactionCoreAsync` upserts into final `factions[]` to call
`NormalizeFactionCoreForStorage`. This lets structure/resource/project/custom/
chronicle collectors see the raw carrier before final core storage removes it.
The materialization guard inside
`RemoveMaterializedFactionCarrierFields` preserves untouched legacy entries.

In both loops of `CollectFactionStructureEntriesFromCore`, extend the exact
presence and copy logic:

```csharp
var hasGovernance = entry["governance"] is JsonObject;
var hasLeadership = entry["leadership"] is JsonObject;
var hasRanks = entry["ranks"] is JsonObject;
var hasStructuredBonuses = entry["structuredBonuses"] is JsonArray;
if (!hasGovernance &&
    !hasLeadership &&
    !hasRanks &&
    !hasStructuredBonuses)
{
    continue;
}

var result = new JsonObject
{
    ["factionId"] = entry["factionId"]?.DeepClone(),
    ["factionName"] =
        entry["factionName"]?.DeepClone() ??
        entry["name"]?.DeepClone(),
    ["name"] = entry["name"]?.DeepClone()
};

if (hasGovernance)
    result["governance"] = entry["governance"]!.DeepClone();
if (hasLeadership)
    result["leadership"] = entry["leadership"]!.DeepClone();
if (hasRanks)
    result["ranks"] = entry["ranks"]!.DeepClone();
if (hasStructuredBonuses)
{
    result["structuredBonuses"] =
        entry["structuredBonuses"]!.DeepClone();
}
```

Do not synthesize `structuredBonuses=[]` when the raw candidate omitted it:
new/promoted materialization validation requires the complete surface, while a
governance-only `FactionCoreChanges` candidate must merge into the existing
structure entry without replacing ranks or bonuses.

- [ ] **Step 5: Reorder faction normalization to preserve the raw carrier**

In `CanonicalStateNormalizer.cs`, use this exact faction order:

```csharp
await NormalizeFactionCoreChangesAsync(backups);
await NormalizeFactionStructureAsync(backups);
await NormalizeFactionResourcesAsync(backups);
await NormalizeFactionProjectsAsync(backups);
await NormalizeFactionCustomAsync(backups);
await NormalizeFactionChroniclesAsync(backups);
await NormalizeFactionCoreAsync(backups);
```

Remove the old later duplicate faction-sidecar calls. Task 5 adds
`NormalizeFactionCoreChangesAsync`; until then, land the remaining six calls and
insert the command call in Task 5.

- [ ] **Step 6: Read creation chronicles from current raw core**

In `NormalizeFactionChroniclesAsync`, read current and backup core before the
core normalizer strips carrier fields:

```csharp
var factionCoreCurrent =
    await ReadNodeAsync("game_state/factions/faction_core.json");
var factionCorePrevious =
    await ReadBackupObjectAsync("game_state/factions/faction_core.json", backups);

foreach (var entry in CollectInitialFactionChronicleEntries(factionCorePrevious))
    AddUniqueNode(entries, entry);
foreach (var entry in CollectInitialFactionChronicleEntries(factionCoreCurrent))
    AddUniqueNode(entries, entry);
```

Keep ordinary `factionChronicleUpdates` collection and consumption unchanged.

- [ ] **Step 7: Run normalizer and Mortal validation tests to GREEN**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~CanonicalStateNormalizerTests|FullyQualifiedName~FactionMaterializationValidationTests"
```

Expected: all new Mortal normalizer/materialization cases pass and no legacy
fixture is eagerly rewritten.

- [ ] **Step 8: Commit**

```powershell
git add -- 'BookOfEternityClient/Services/CanonicalStateNormalizer.cs' 'BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs' 'BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.FactionAndInventoryHelpers.cs' 'BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Factions.cs'
git commit -m "feat: normalize materialized mortal factions (#1510)"
```

---

### Task 5: Add closed `FactionCoreChanges` and block full resends

**Files:**

- Create: `BookOfEternityClient/Services/FactionCoreChangesContract.cs`
- Create:
  `BookOfEternityClient/Services/Validation/ValidationService.FactionCoreChanges.cs`
- Create: `BookOfEternityClient.Tests/FactionCoreChangesContractTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/FactionCoreChangesTests.cs`
- Modify: `BookOfEternityClient/Models/GameResponse.cs:340`
- Modify: `BookOfEternityClient/Configuration/FileMapping.cs:116`
- Modify:
  `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer.cs:190`
- Modify:
  `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:365`
- Modify:
  `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:200`

**Interfaces:**

- Consumes: exact current/pre-turn Mortal core, complete command groups, exact
  faction/NPC relation authority.
- Produces:
  `FactionCoreChangesContract.Evaluate(...)`,
  `FactionCoreChangesContract.Apply(...)`,
  `ValidateFactionCoreChangesBeforeNormalizationAsync()`, and
  `NormalizeFactionCoreChangesAsync(...)`.

- [ ] **Step 1: Write failing closed-contract tests**

Use this representative test in
`FactionCoreChangesContractTests.cs`:

```csharp
[Fact]
public void Evaluate_RelationsGroup_ReplacesOnlyAbsoluteRelations()
{
    var preTurn = BuildMaterializedFactionCore();
    var current = preTurn.DeepClone().AsObject();
    current[FactionCoreChangesContract.PropertyName] = new JsonArray
    {
        new JsonObject
        {
            ["factionId"] = "faction_watch",
            ["reason"] = "The watch signed a bridge compact.",
            ["relations"] = new JsonObject
            {
                ["entries"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["targetFactionId"] = "faction_bridge_compact",
                        ["status"] = "allied",
                        ["description"] = "Both factions defend the bridge."
                    }
                }
            }
        }
    };

    var evaluation = FactionCoreChangesContract.Evaluate(
        current,
        preTurn,
        new FactionCoreChangesContract.Authority(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "faction_watch",
                "faction_bridge_compact"
            },
            new HashSet<string>(StringComparer.Ordinal)));

    Assert.True(evaluation.CanApply);
    var result = current.DeepClone().AsObject();
    FactionCoreChangesContract.Apply(result, evaluation);
    var faction = result["factions"]![0]!.AsObject();
    Assert.Single(faction["relations"]!.AsArray());
    Assert.Equal("fmat_watch", faction["materialization"]!["materializationId"]!.GetValue<string>());
    Assert.False(result.ContainsKey(FactionCoreChangesContract.PropertyName));
}
```

Add cases for each complete group, partial group objects, recursive unknown
members, protected fields, duplicate targets, missing reason, no groups, unknown
relation/faction IDs, legacy targets, promotion-plus-command, invalid command
preservation, and full existing-object bypass. Include a relation update that
turns an initially `empty_by_design` relation surface into populated live
state while preserving the old immutable receipt; it must pass because receipt
evidence consistency is deferred for `AlreadyMaterialized`.

- [ ] **Step 2: Run core-change tests and verify RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FactionCoreChangesContractTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FactionCoreChangesTests"
```

Expected: missing response/contract/validator/normalizer failures.

- [ ] **Step 3: Add response/file mapping**

In `GameResponse.cs`:

```csharp
[JsonPropertyName("factionCoreChanges")]
public JsonElement[]? FactionCoreChanges { get; set; }
```

In `FileMapping.cs`:

```csharp
["factionCoreChanges"] = "game_state/factions/faction_core.json",
```

Add `factionCoreChanges` to the exact allowed state-file command members in
`ValidationService.LifecycleControlAndStateFiles.cs`.

- [ ] **Step 4: Implement the closed contract surface**

Create `FactionCoreChangesContract.cs` with:

```csharp
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class FactionCoreChangesContract
{
    internal const string PropertyName = "factionCoreChanges";
    internal const string FactionCorePath =
        "game_state/factions/faction_core.json";

    private static readonly HashSet<string> CommandKeys = new(StringComparer.Ordinal)
    {
        "factionId",
        "reason",
        "profile",
        "purposeAndPrinciples",
        "progressionAndPower",
        "governanceAndLeadership",
        "playerMembership",
        "relations"
    };

    internal sealed record Authority(
        HashSet<string> KnownFactionIds,
        HashSet<string> KnownMortalNpcIds);

    internal sealed record ChangePlan(
        string FactionId,
        JsonObject Command);

    internal sealed class Evaluation
    {
        internal bool HasCommand { get; init; }
        internal List<ValidationIssue> Issues { get; } = [];
        internal List<ChangePlan> Plans { get; } = [];
        internal bool CanApply =>
            HasCommand && Issues.Count == 0 && Plans.Count > 0;
    }

    internal static Evaluation Evaluate(
        JsonObject currentRoot,
        JsonObject preTurnRoot,
        Authority authority)
    {
        var evaluation = new Evaluation
        {
            HasCommand = currentRoot.ContainsKey(PropertyName)
        };
        ValidateDirectFullResendBypass(currentRoot, preTurnRoot, evaluation.Issues);
        ValidateCommands(currentRoot, preTurnRoot, authority, evaluation);
        return evaluation;
    }

    internal static void Apply(JsonObject result, Evaluation evaluation)
    {
        foreach (var plan in evaluation.Plans)
            ApplyCommand(FindFaction(result, plan.FactionId)!, plan.Command);
        result.Remove(PropertyName);
    }
}
```

Use the exact group schemas in
`specs/1510-complete-faction-materialization/contracts/faction-core-changes.md`.
Every supplied group is complete; no delta member is accepted. Preserve
`materialization` by never including it in an allowed set and by applying only
named fields. `FindFaction` must search both canonical `factions[]` and
same-turn `factionDataChanges[]`; this is what makes a valid
promotion-plus-command target resolvable without broadening the accepted
command surface. `ValidateCommands` must resolve targets against that same
effective union, while still rejecting duplicate effective IDs.

`governanceAndLeadership` is applied to that effective in-core carrier only as
a transient normalization input. The immediately following
`NormalizeFactionStructureAsync` extracts the complete replacement into
`faction_structure.json.entries[]`; the final `NormalizeFactionCoreAsync`
removes `governance`/`leadership` from materialized core. The Task 4 order is
therefore a correctness requirement. The integration test must assert both the
updated structure entry and the absence of those fields from final
`faction_core.json`.

`ValidateDirectFullResendBypass` inspects only full objects supplied through
current `factionDataChanges[]`. It must not treat the ordinary unchanged
canonical `factions[]` baseline as a resend. A `factionDataChanges[]` target
whose exact pre-turn faction already has a receipt emits
`faction_existing_full_resend_forbidden`, even when the resent object is
otherwise identical.

- [ ] **Step 5: Implement raw validation and normalization**

`ValidationService.FactionCoreChanges.cs` must expose:

```csharp
public async Task<IReadOnlyList<ValidationIssue>>
    ValidateFactionCoreChangesBeforeNormalizationAsync()
```

Parse current/pre-turn files with duplicate detection, load exact known faction
and NPC IDs, call `Evaluate`, and return its issues. In
`CanonicalStateNormalizer.Factions.cs`, add:

```csharp
private async Task NormalizeFactionCoreChangesAsync(
    IReadOnlyDictionary<string, string>? backups)
{
    var currentNode =
        await ReadNodeAsync(FactionCoreChangesContract.FactionCorePath);
    if (currentNode is not JsonObject currentRoot ||
        !currentRoot.ContainsKey(FactionCoreChangesContract.PropertyName))
    {
        return;
    }

    var preTurnRoot = await ReadBackupObjectAsync(
        FactionCoreChangesContract.FactionCorePath,
        backups);
    if (preTurnRoot == null)
        return;

    var authority = await ReadFactionCoreChangesAuthorityAsync();
    var evaluation = FactionCoreChangesContract.Evaluate(
        currentRoot,
        preTurnRoot,
        authority);
    if (!evaluation.CanApply)
        return;

    var result = CloneObject(currentRoot);
    FactionCoreChangesContract.Apply(result, evaluation);
    await WriteIfChangedAsync(
        FactionCoreChangesContract.FactionCorePath,
        currentNode,
        result);
}
```

Place this method before sidecar extraction as specified in Task 4 so a
governance/leadership group reaches structure authority. Add its raw validator
call to the GameEngine method shown in Task 2.

- [ ] **Step 6: Run core-change and Mortal tests to GREEN**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FactionCoreChangesContractTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FactionCoreChangesTests|FullyQualifiedName~FactionMaterializationValidationTests|FullyQualifiedName~CanonicalStateNormalizerTests"
```

Expected: valid commands apply/consume; invalid commands remain; full existing
resends fail; promotion and unrelated history remain intact.

- [ ] **Step 7: Commit**

```powershell
git add -- 'BookOfEternityClient/Services/FactionCoreChangesContract.cs' 'BookOfEternityClient/Services/Validation/ValidationService.FactionCoreChanges.cs' 'BookOfEternityClient/Models/GameResponse.cs' 'BookOfEternityClient/Configuration/FileMapping.cs' 'BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs' 'BookOfEternityClient/Services/CanonicalStateNormalizer.cs' 'BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs' 'BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs' 'BookOfEternityClient.Tests/FactionCoreChangesContractTests.cs' 'BookOfEternityClient.IntegrationTests/FactionCoreChangesTests.cs'
git commit -m "feat: add narrow mortal faction updates (#1510)"
```

---

### Task 6: Add the Shining raw semantic fence and strict normalization mode

**Files:**

- Create:
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs`
- Modify:
  `BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs`
- Modify:
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:1093`
- Modify:
  `BookOfEternityClient/Services/ShiningAbodeState.cs:893`
- Modify:
  `BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:806`
- Modify:
  `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.ShiningAbode.cs:1`
- Test:
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
- Test:
  `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.ShiningAbode.cs`

**Interfaces:**

- Consumes: raw/current/pre-turn Shining state and current resident/actor
  authority.
- Produces:
  `ValidateAcceptedTurnShiningFactionMaterializationCompletenessAsync(bool,
  List<ValidationIssue>)`
  and `ShiningFactionNormalizationMode`.

- [ ] **Step 1: Add missing-semantic and laundering RED tests**

Add:

```csharp
public static TheoryData<string, string> MissingShiningSemantics => new()
{
    { "creationProvenance", "faction_materialization_shining_provenance_missing" },
    { "hallId", "faction_materialization_shining_hall_missing" },
    { "charter", "faction_materialization_shining_charter_missing" },
    { "currentAgenda", "faction_materialization_shining_agenda_missing" },
    { "factionLifecycle", "faction_materialization_shining_lifecycle_missing" },
    { "leadership", "faction_materialization_shining_leadership_missing" },
    { "strategicMemory", "faction_materialization_shining_memory_missing" },
    { "chronicle", "faction_materialization_shining_chronicle_missing" },
    { "visibility", "faction_materialization_shining_visibility_missing" },
    { "storyAuthority", "faction_materialization_shining_story_authority_missing" }
};

[Theory]
[MemberData(nameof(MissingShiningSemantics))]
public async Task NewShiningFaction_MissingAuthoredSemantic_FailsBeforeNormalization(
    string field,
    string code)
{
    var faction = BuildCompleteNativeShiningFaction();
    faction.Remove(field);
    await WriteNativeDiscoveryOutcomeAsync(faction);

    var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

    Assert.Contains(issues, issue => issue.Code == code);
}
```

In `CanonicalStateNormalizerTests.ShiningAbode.cs`, write a test that feeds a
new candidate missing charter/leadership/memory, calls raw validation first, and
asserts errors exist before `NormalizeStateRoot` can add values. Add
profile-evidence cases proving: an operational non-vacant tier-eligible faction
must declare `canTrade=true`; a vacant faction must declare `canTrade=false`;
trade content with an `empty` disposition fails; and explicit
`tradeInventory=null` plus `tradeInventoryReceipts=[]` is the only accepted
empty trade surface.

- [ ] **Step 2: Run Shining focused tests and verify RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FactionMaterializationValidationTests|FullyQualifiedName~CanonicalStateNormalizerTests"
```

Expected: Shining semantic cases fail because the normalizer still supplies
defaults and no profile evidence exists.

- [ ] **Step 3: Add exact Shining semantic field validation**

In `ValidationService.ShiningAbode.cs`, require the new fields on every current
faction carrying a receipt, plus each new/promotion candidate that is required
to carry one; skip only exact untouched legacy factions:

```csharp
private static readonly HashSet<string> ShiningCreationRoutes =
    new(StringComparer.Ordinal)
    {
        "native_discovery",
        "player_founding",
        "story"
    };

private static readonly HashSet<string> ShiningVisibilityStates =
    new(StringComparer.Ordinal)
    {
        "revealed",
        "rumored",
        "hidden"
    };
```

`creationProvenance` has exactly `route`, `authorityType`, `authorityId`.
`storyAuthority` is an explicit JSON null for non-story routes or exactly
`authorityType`, `authorityId`, `factionRole` for `story`. Require non-empty
`currentAgenda`, complete existing charter/lifecycle/leadership/strategic
memory, and at least one production-valid chronicle entry.

- [ ] **Step 4: Build Shining evidence**

In `ValidationService.ShiningFactionMaterialization.cs`, construct:

```csharp
var hasTradeContent =
    faction["tradeInventory"] is JsonObject ||
    HasObjectArrayEntries(faction, "tradeInventoryReceipts");
var canTrade = ShiningAbodeState.FactionHasAvailableTrade(faction);

var evidence = new FactionMaterializationEvidence(
    "shining_faction",
    factionId,
    new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["projects"] = HasObjectArrayEntries(faction, "projects"),
        ["territorialInfluence"] = HasObjectArrayEntries(faction, "territorialInfluence"),
        ["resourceLedger"] = HasObjectArrayEntries(faction, "resourceLedger"),
        ["residentAffiliations"] = affiliatedResidentIds.Count > 0,
        ["trade"] = hasTradeContent,
        ["leadershipHistory"] =
            HasObjectArrayEntries(faction, "leadershipHistory") ||
            HasObjectArrayEntries(faction, "leadershipReceipts"),
        ["storyState"] = faction["storyAuthority"] is JsonObject
    },
    new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["runsProjects"] = HasObjectArrayEntries(faction, "projects"),
        ["holdsTerritorialInfluence"] = HasObjectArrayEntries(faction, "territorialInfluence"),
        ["usesResourceLedger"] = HasObjectArrayEntries(faction, "resourceLedger"),
        ["hasResidentAffiliations"] = affiliatedResidentIds.Count > 0,
        ["canTrade"] = canTrade,
        ["hasLeadershipHistory"] =
            HasObjectArrayEntries(faction, "leadershipHistory") ||
            HasObjectArrayEntries(faction, "leadershipReceipts"),
        ["usesStoryState"] = faction["storyAuthority"] is JsonObject
    },
    new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["projects"] = HasExactEmptyArray(faction, "projects"),
        ["territorialInfluence"] = HasExactEmptyArray(faction, "territorialInfluence"),
        ["resourceLedger"] = HasExactEmptyArray(faction, "resourceLedger"),
        ["residentAffiliations"] = affiliatedResidentIds.Count == 0,
        ["trade"] =
            HasExplicitNull(faction, "tradeInventory") &&
            HasExactEmptyArray(faction, "tradeInventoryReceipts"),
        ["leadershipHistory"] =
            HasExactEmptyArray(faction, "leadershipHistory") &&
            HasExactEmptyArray(faction, "leadershipReceipts"),
        ["storyState"] = HasExplicitNull(faction, "storyAuthority")
    });
```

Define the referenced exact-shape helpers in the same partial validator:

```csharp
private static bool HasObjectArrayEntries(
    JsonObject owner,
    string propertyName) =>
    owner[propertyName] is JsonArray array &&
    array.OfType<JsonObject>().Any();

private static bool HasExactEmptyArray(
    JsonObject owner,
    string propertyName) =>
    owner.ContainsKey(propertyName) &&
    owner[propertyName] is JsonArray { Count: 0 };

private static bool HasExplicitNull(
    JsonObject owner,
    string propertyName) =>
    owner.ContainsKey(propertyName) &&
    owner[propertyName] is null;
```

For the raw `New`/`LegacyPromotion` target, call
`FactionMaterializationContract.Validate` with full evidence,
`requireEnvelope: true`, and `deferEvidenceConsistency: false`. Canonical
post-normalization validation requires complete live Shining semantics and
references while Task 2 owns deferred receipt-shape/continuity validation.
Candidate collection and `ValidateUniqueMaterializationIds` remain exclusively
in Task 2.

Add the Shining completeness call to both Task 2 orchestrators immediately
after Shining continuity collection and before the shared uniqueness call:

```csharp
// ValidateAcceptedTurnRawFactionMaterializationAsync
await ValidateAcceptedTurnShiningFactionMaterializationCompletenessAsync(
    rawBeforeNormalization: true,
    issues);

// ValidateAcceptedTurnFactionMaterializationCompletenessAsync
await ValidateAcceptedTurnShiningFactionMaterializationCompletenessAsync(
    rawBeforeNormalization: false,
    issues);
```

Update the existing `FactionHasAvailableTrade` predicate so the exact
mechanical capability also requires non-vacant leadership:

```csharp
public static bool FactionHasAvailableTrade(JsonObject? faction) =>
    faction != null &&
    IsFactionOperational(faction) &&
    faction["leadership"] is JsonObject leadership &&
    !string.Equals(
        GetNodeString(leadership["leadershipState"]),
        LeadershipStateVacant,
        StringComparison.OrdinalIgnoreCase) &&
    GetTradeTier(GetNodeInt(faction["factionStrength"], 0)) >= 1;
```

Keep all trade services calling this one predicate; do not reproduce a keyword
map in validation.

- [ ] **Step 5: Introduce strict versus legacy normalization**

In `ShiningAbodeState.cs`, add:

```csharp
internal enum ShiningFactionNormalizationMode
{
    LegacyCompatibility,
    AuthoredMaterialization
}
```

Pass the mode into `NormalizeFactionObject`. In
`AuthoredMaterialization`, do not create/replace `originType`, charter,
leadership, lifecycle semantics, agenda, visibility, story authority, strategic
memory summary, chronicle prose, or materialization. Continue to normalize
explicit arrays and compute `factionStrength`. In
`LegacyCompatibility`, retain current behavior only for exact untouched legacy
factions.

The normalizer chooses the mode by comparing exact IDs with the validated
backup: every current faction carrying a receipt—including new, promoted, and
already materialized factions—uses `AuthoredMaterialization`; only an exact
untouched pre-turn faction without a receipt uses `LegacyCompatibility`. A new
or GM-touched faction without a receipt is rejected by the raw fence and never
receives compatibility defaulting.

- [ ] **Step 6: Stop automatic semantic guardian-faction creation**

In `ShiningAbodeState.Gameplay.cs`
`EnsureActiveGuardianFactionMaterialized`, resolve the exact faction before
creating or updating its hall. If the faction is absent, return `false` and
create neither faction nor hall. If the existing faction has no materialization
receipt, retain the current legacy-compatibility projection. If it has a
receipt, do not rewrite `originType`, `hallId`, charter, leadership, agenda,
visibility, memory, chronicle, provenance, or hall semantics; numeric
client-owned projections remain normalized by their existing dedicated path.
Future creation must arrive through an authored `story` route with exact
Guardian Actor Materialization authority. Add focused regression tests proving
an absent faction/hall stay absent until authored, an existing legacy faction
remains readable, and an existing materialized faction's authored semantics
remain unchanged.

- [ ] **Step 7: Run common Shining tests to GREEN**

Run the Step 2 command.

Expected: raw missing-semantic cases fail for the intended reason; complete
common Shining candidates pass; untouched legacy fixtures still load.

- [ ] **Step 8: Commit**

```powershell
git add -- 'BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs' 'BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs' 'BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs' 'BookOfEternityClient/Services/ShiningAbodeState.cs' 'BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs' 'BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.ShiningAbode.cs' 'BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs' 'BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.ShiningAbode.cs'
git commit -m "feat: require authored shining faction semantics (#1510)"
```

---

### Task 7: Compose native discovery and player founding routes

**Files:**

- Modify:
  `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:1080,9544`
- Modify:
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs`
- Test:
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`

**Interfaces:**

- Consumes: existing native/founding request/receipt validators plus common
  Shining evidence.
- Produces: route-specific completeness without duplicating cost/count/diff
  authority.

- [ ] **Step 1: Write native route RED tests**

Add one valid case and mutations for wrong hall/faction count, resident count
outside 2–4, project count not 2, non-completed project, reused IDs, missing
actor envelope, wrong costs/receipt, and unrelated state rewrite:

```csharp
[Fact]
public async Task NativeDiscovery_CompleteMaterialization_Passes()
{
    await WriteCompleteNativeDiscoveryAsync(
        residentCount: 2,
        completedProjectCount: 2);

    var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

    Assert.DoesNotContain(issues, issue =>
        issue.Severity == IssueSeverity.Error &&
        issue.Actor == "shining_faction:shine_faction_native");
}
```

- [ ] **Step 2: Write player-founding RED tests**

Add a valid case and mutations for mismatched request ID, charter, supporters,
hall, player-soul leadership, quoted/reserved costs, missing founding receipt,
missing founding leadership history, and unrelated resident rewrite:

```csharp
[Fact]
public async Task PlayerFounding_CompleteMaterialization_Passes()
{
    await WriteCompletePlayerFoundingAsync();

    var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

    Assert.DoesNotContain(issues, issue =>
        issue.Severity == IssueSeverity.Error &&
        issue.Actor == "shining_faction:shine_faction_player");
}
```

- [ ] **Step 3: Run exact route tests and verify RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FactionMaterializationValidationTests"
```

Expected: route materialization cases report missing provenance/history/actor
composition.

- [ ] **Step 4: Compose with existing validators**

After existing native/founding outcome validation resolves exact request,
receipt, faction, hall, residents, and project IDs, call:

```csharp
ValidateShiningRouteMaterialization(
    route: "native_discovery",
    authorityType: "shining_core_action_request",
    authorityId: request.RequestId,
    faction: currentFaction,
    hallId: hallId,
    residentIds: residentIds,
    projectIds: projectIds,
    issues: issues);
```

and:

```csharp
ValidateShiningRouteMaterialization(
    route: "player_founding",
    authorityType: "shining_founding_request",
    authorityId: request.RequestId,
    faction: currentFaction,
    hallId: request.ProposedHallId,
    residentIds: request.SupportingResidentIds.ToHashSet(StringComparer.Ordinal),
    projectIds: new HashSet<string>(StringComparer.Ordinal),
    issues: issues);
```

The helper verifies exact `creationProvenance`, the envelope identity, complete
common semantics, route-specific affiliation/history content, and Actor
Materialization. Leave existing cost/count/reservation/constrained-diff issue
generation in its current methods.

- [ ] **Step 5: Run route tests to GREEN**

Run the Step 3 command.

Expected: both complete routes pass; each mutated route fails with its existing
mechanical issue and/or one exact materialization issue, never a generic
normalized-shape error.

- [ ] **Step 6: Commit**

```powershell
git add -- 'BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs' 'BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs' 'BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs'
git commit -m "feat: bind shining faction creation routes (#1510)"
```

---

### Task 8: Enforce story authority, Actor Materialization, and derived-only boundaries

**Files:**

- Modify:
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs`
- Modify:
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs`
- Test:
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
- Reference:
  `BookOfEternityClient/Services/Validation/ValidationService.ActorMaterializationContinuity.cs`
- Reference: `BookOfEternityClient/Services/ActorMaterializationContract.cs`

**Interfaces:**

- Consumes: exact canonical story state and afterlife actor profiles.
- Produces: story/hidden route validation and exact actor exceptions.

- [ ] **Step 1: Write story/hidden and actor RED tests**

Add:

```csharp
[Theory]
[InlineData("hidden")]
[InlineData("rumored")]
[InlineData("revealed")]
public async Task StoryFaction_ExactAuthorityAndVisibility_Passes(
    string visibility)
{
    await WriteCompleteStoryFactionAsync(
        authorityType: "saref_main_story",
        authorityId: "shine_faction_wings",
        factionRole: "wings_of_angels",
        visibility: visibility);

    var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

    Assert.DoesNotContain(issues, issue =>
        issue.Severity == IssueSeverity.Error &&
        issue.Actor == "shining_faction:shine_faction_wings");
}

[Fact]
public async Task StoryFaction_SecretiveNameWithoutAuthority_Fails()
{
    await WriteStoryFactionWithoutAuthorityAsync(
        factionName: "The Hidden Wings",
        charterSummary: "A secret order concealed from all.");

    var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

    Assert.Contains(issues, issue =>
        issue.Code == "faction_materialization_shining_story_authority_invalid");
}
```

Add cases for missing canonical story ID/role, wrong visibility, missing head
profile, incomplete head/resident/political actor envelope, vacant leadership,
`player_soul`, and a current state that differs only in
`factionStrength`/tier/service multiplier.

- [ ] **Step 2: Run story/actor tests and verify RED**

Run the Task 7 focused command.

Expected: missing exact story/actor binding checks.

- [ ] **Step 3: Implement exact story authority resolution**

Add a closed resolver:

```csharp
private bool TryResolveShiningStoryAuthority(
    JsonObject shiningFaction,
    JsonObject? sarefRoot,
    JsonObject? guardiansRoot,
    out string actual)
{
    actual = "missing or mismatched story authority";
    if (shiningFaction["storyAuthority"] is not JsonObject authority)
        return false;

    var authorityType = GetNodeString(authority["authorityType"]);
    var authorityId = GetNodeString(authority["authorityId"]);
    var factionRole = GetNodeString(authority["factionRole"]);
    var factionId = GetNodeString(shiningFaction["factionId"]);
    if (shiningFaction["creationProvenance"] is not JsonObject provenance ||
        !string.Equals(
            GetNodeString(provenance["route"]),
            "story",
            StringComparison.Ordinal) ||
        !string.Equals(
            GetNodeString(provenance["authorityType"]),
            authorityType,
            StringComparison.Ordinal) ||
        !string.Equals(
            GetNodeString(provenance["authorityId"]),
            authorityId,
            StringComparison.Ordinal))
    {
        return false;
    }

    if (authorityType == "saref_main_story")
    {
        var valid = SarefStoryAuthorizesFaction(
            sarefRoot,
            authorityId,
            factionId,
            factionRole,
            GetNodeString(shiningFaction["visibility"]),
            GetNodeString(shiningFaction["sarefFactionRole"]),
            GetNodeString(shiningFaction["sarefVisibility"]));
        actual = valid ? "matched saref_main_story" : actual;
        return valid;
    }

    if (authorityType == "guardian_ascension")
    {
        var valid = GuardianAscensionAuthorizesFaction(
            guardiansRoot,
            authorityId,
            factionId,
            factionRole,
            GetNodeString(shiningFaction["originType"]),
            GetNodeString(shiningFaction["visibility"]),
            shiningFaction["leadership"] as JsonObject);
        actual = valid ? "matched guardian_ascension" : actual;
        return valid;
    }

    return false;
}
```

`guardiansRoot` is the canonical `game_state/meta/guardians.json` root for
`guardian_ascension`.
`SarefStoryAuthorizesFaction` must require exact
`authorityId == factionLinks.wingsFactionId == factionId`,
`factionLinks.visibility == visibility`, and
`factionRole == sarefFactionRole == "wings_of_angels"` plus
`visibility == sarefVisibility`. This binding is valid before reveal and must
not depend on an optional/later `wingsInfiltration` request.
`GuardianAscensionAuthorizesFaction` must resolve exactly one Guardian from the
union of `activeGuardian` and `guardians[]` by `guardianId == authorityId`,
require `originType == "ascended_guardian"`,
`factionRole == "patron_guardian"`, `visibility == "revealed"`, and secure
leadership with `headActorType == "guardian"` and
`headActorId == authorityId`. Validate that Guardian with
`ActorMaterializationContract.ValidateAfterlifeProfile` and the exact current
trade-authority evidence described below. It must not inspect names,
descriptions, domains, archetypes, or keyword signatures, and must not
introduce another story state file.

- [ ] **Step 4: Reuse Actor Materialization validation**

Load the current afterlife actor trade-authority set once with
`LoadCurrentAfterlifeActorTradeAuthoritiesAsync`, resolve each non-player actor
to the exact afterlife profile, and then call the same evidence-aware path used
by accepted-turn Actor Materialization:

```csharp
issues.AddRange(ActorMaterializationContract.ValidateAfterlifeProfile(
    actorProfileElement,
    actorContext,
    requireEnvelope: true,
    canTradeEvidence: HasCurrentAfterlifeActorTradeAuthority(
        actorProfileElement,
        currentTradeAuthorities)));
```

Skip this call only when leadership is vacant with null head fields or when both
head type and ID equal `player_soul`. Newly significant residents and political
actors have no vacancy/player exception. Do not substitute
`ValidateCanonicalAfterlifeProfile` here: that helper intentionally supplies
`canTradeEvidence: false` and would reject a valid trading faction head.

- [ ] **Step 5: Whitelist only documented derived differences**

The touch comparator must ignore exact changes to:

```csharp
private static readonly HashSet<string> ShiningClientDerivedFactionFields =
    new(StringComparer.Ordinal)
    {
        "factionStrength",
        "derivedTier",
        "serviceMultiplier"
    };
```

All other semantic, section, link, receipt, or story changes are GM-authored
touches. Keep the actual repository field names if tier/service projections are
nested; whitelist their exact canonical paths, not fuzzy suffixes.

- [ ] **Step 6: Run story/actor/derived tests to GREEN**

Run the Task 7 focused command.

Expected: exact story routes and actor exceptions pass; inference and incomplete
actors fail; derived-only recomputation does not promote an untouched legacy
faction.

- [ ] **Step 7: Commit**

```powershell
git add -- 'BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs' 'BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs' 'BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs'
git commit -m "feat: validate shining story faction authority (#1510)"
```

---

### Task 9: Add bounded faction repair packet routing

**Files:**

- Modify:
  `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:2700-3150`
- Test:
  `BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs`
- Reference:
  `specs/1510-complete-faction-materialization/contracts/faction-repair-packet.md`

**Interfaces:**

- Consumes: `ValidationIssue` with stable faction coordinate and issue family.
- Produces: exact repair harness packet and exact target file list.

- [ ] **Step 1: Write repair-routing RED tests**

Add:

```csharp
[Fact]
public async Task WriteValidationRepairRequestAsync_FactionMaterializationError_AddsBoundedPacket()
{
    var engine = CreateGameEngine();
    var issues = new List<ValidationIssue>
    {
        new(
            "game_state/factions/faction_resources.json.entries",
            IssueSeverity.Error,
            "Missing resource sidecar.",
            code: "faction_materialization_bundle_incomplete",
            actor: "mortal_faction:faction_watch",
            section: "FactionMaterialization")
    };

    var method = typeof(GameEngine).GetMethod(
        "WriteValidationRepairRequestAsync",
        BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(method);
    var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
        engine,
        new object[] { "faction materialization repair", issues, 1 })!);
    await task;

    var json = await _fs.ReadFileAsync(
        "game_state/control/validation_repair_request.json");
    using var document = JsonDocument.Parse(json!);
    var packet = Assert.Single(document.RootElement
        .GetProperty("harnessRepairPackets")
        .EnumerateArray());
    Assert.Equal(
        "faction_materialization_repair",
        packet.GetProperty("kind").GetString());
    var targetFiles = packet.GetProperty("targetFiles")
        .EnumerateArray()
        .Select(item => item.GetString())
        .ToArray();
    Assert.Contains(
        "game_state/factions/faction_resources.json",
        targetFiles);
    Assert.DoesNotContain(
        "game_state/meta/shining_abode_state.json",
        targetFiles);
    Assert.Contains(
        packet.GetProperty("safeCorrectionRules").EnumerateArray(),
        item => item.GetString()!.Contains(
            "preserve",
            StringComparison.OrdinalIgnoreCase));
}
```

Add a Shining actor-link case, same-turn Mortal `initialId`, and a test proving
an unrelated faction/root is not included.

- [ ] **Step 2: Run repair tests and verify RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~GameEngineTurnLifecycleTests"
```

Expected: existing repair router falls through to generic identity/resource
packets or lacks exact faction materialization routing.

- [ ] **Step 3: Add issue recognition and exact target resolution**

In `GameEngine.ValidationAndRepair.cs`, add:

```csharp
private static bool IsFactionMaterializationRepairIssue(
    ValidationIssue issue) =>
    issue.Code?.StartsWith(
        "faction_materialization_",
        StringComparison.Ordinal) == true ||
    string.Equals(
        issue.Code,
        "faction_legacy_promotion_required",
        StringComparison.Ordinal) ||
    string.Equals(
        issue.Code,
        "faction_existing_full_resend_forbidden",
        StringComparison.Ordinal);
```

Route it before generic faction identity/resource routing. Resolve the domain
only from `issue.Actor` prefix, then group issues by exact coordinate so one
packet never repairs two factions. Mortal targets come from the exact issue
path plus the six allowed faction files and only exact linked NPC/location
files named by the issue. Shining targets start with
`game_state/meta/shining_abode_state.json` and add only the exact
resident/profile/story file named by the defect.

- [ ] **Step 4: Build the preservation-oriented packet**

Add:

```csharp
private static ValidationRepairHarnessPacket
    BuildFactionMaterializationRepairPacket(
    string coordinate,
    IReadOnlyList<ValidationIssue> factionIssues)
{
    var targetFiles = ResolveFactionMaterializationRepairTargetFiles(
        coordinate,
        factionIssues);
    var missingFields = factionIssues
        .Where(issue => issue.Code?.Contains(
            "missing",
            StringComparison.Ordinal) == true)
        .Select(issue => issue.Path)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToList();

    return new ValidationRepairHarnessPacket
    {
        Kind = "faction_materialization_repair",
        Priority = "critical",
        Title = $"Bounded faction materialization repair: {coordinate}",
        TargetFiles = targetFiles,
        TemplateRefs = coordinate.StartsWith(
            "mortal_faction:",
            StringComparison.Ordinal)
            ? new List<string> { "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md" }
            : new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
        CanonicalActorNames = new List<string> { coordinate },
        MissingFields = missingFields.Count == 0
            ? null
            : missingFields,
        ExactFieldCorrections = factionIssues
            .Select(BuildExactFieldCorrection)
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ToList(),
        ExpectedShape = new List<string>
        {
            "One exact faction coordinate owns one complete immutable materialization v1 receipt.",
            "Every governed section is populated with production-valid content or has its exact empty canonical surface and a meaningful reason.",
            "Every hall, actor, resident, location, NPC, receipt, and sidecar link resolves to the same exact faction ID."
        },
        SafeCorrectionRules = new List<string>
        {
            "Change only the listed faction and target files.",
            "Preserve every valid semantic section, historical receipt, chronicle, sidecar record, and unrelated faction.",
            "Use the accepted creation/promotion route and existing narrow commands; never infer content from names, tags, descriptions, or genre."
        },
        Steps = new List<string>
        {
            "Read every exact field correction and target selector.",
            "Repair only the missing or contradictory faction surfaces.",
            "Re-run raw materialization validation before normalization."
        },
        DoNotDo = new List<string>
        {
            "Do not change materializationId or another accepted historical receipt.",
            "Do not rewrite another faction or unrelated state root.",
            "Do not invent narrative content to silence validation."
        }
    };
}
```

Add the exact classification to `ExpectedShape` when the classifier emitted it
in an issue's `Actual`/`Expected`.

- [ ] **Step 5: Run repair tests to GREEN**

Run the Step 2 command.

Expected: each representative defect produces one exact coordinate and no
unrelated target.

- [ ] **Step 6: Commit**

```powershell
git add -- 'BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs' 'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs'
git commit -m "feat: route bounded faction repairs (#1510)"
```

---

### Task 10: Synchronize GM contracts, examples, manifests, and UI privacy

**Files:**

- Modify: `Rules/Block_21.txt`
- Modify: `Examples/E_Block_21.txt`
- Modify: `TaskGuides/CLI_Step_Main.txt`
- Modify: `Examples/E_CLI_Step_Main.txt`
- Modify: `CLI_API_Specification.md`
- Modify: `CLI_Agent_Daemon_Specification.md`
- Modify: `Rules/Block_CLI_Operations.txt`
- Modify: `BookOfEternityClient/game_master_daemon.ps1`
- Modify: `OtherGuides/Afterlife_Contract_Matrix.md`
- Modify:
  `docs/audits/afterlife/shining-abode/Shining_Abode_Faction_Politics_Addendum.md`
- Modify: `Examples/E_CLI_Afterlife_Turns.txt`
- Modify: `Examples/example_validation_manifest.json`
- Modify:
  `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`
- Modify:
  `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`
- Modify: `BookOfEternityClient.Tests/ValidationSourceGuardTests.cs`
- Modify: `BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs`
- Modify:
  `BookOfEternityClient.Tests/AfterlifeShiningPlayerFacingSourceGuardTests.cs`
- Modify:
  `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- Modify: `BookOfEternityClient/Services/SarefMainStoryState.cs`
- Modify:
  `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs`
- Modify:
  `BookOfEternityClient/UI/ExplorerShiningAbodeCommandResultBuilder.cs`
- Modify: `BookOfEternityClient.Tests/ShiningAbodeStateTests.cs`

**Interfaces:**

- Consumes: final runtime field names and route contracts from Tasks 1–9.
- Produces: executable GM guidance, eight worked-example families, manifest
  coverage, and private-metadata source guards.

- [ ] **Step 1: Write documentation and privacy RED assertions**

Add exact token assertions:

```csharp
[Fact]
public void FactionMaterializationContracts_AreCoveredByRequiredGuidance()
{
    AssertFileContains("Rules/Block_21.txt",
        "\"materialization\"",
        "\"purpose\"",
        "\"currentAgenda\"",
        "\"factionCoreChanges\"",
        "\"empty_by_design\"");
    AssertFileContains("OtherGuides/Afterlife_Contract_Matrix.md",
        "native_discovery",
        "player_founding",
        "story",
        "Actor Materialization",
        "shining_faction:");
}
```

Add source guards that enumerate ordinary Mortal/Shining console/browser reader
files and reject direct display of:

```csharp
private static readonly string[] PrivateFactionMaterializationTokens =
{
    "materializationId",
    "schemaVersion",
    "empty_by_design",
    "materializedAtTurn"
};
```

The guard may allow validator/debug/repair files through an explicit allow-list;
ordinary reader files are never allow-listed.

Add a visibility behavior test in `ShiningAbodeStateTests.cs`:

```csharp
[Theory]
[InlineData("hidden", false)]
[InlineData("rumored", false)]
[InlineData("revealed", true)]
public void GetPlayerVisibleShiningFactions_UsesCanonicalVisibility(
    string visibility,
    bool expectedVisible)
{
    var faction = new JsonObject
    {
        ["factionId"] = "shine_faction_story",
        ["originType"] = ShiningAbodeState.OriginTypeAscendedGuardian,
        ["hallId"] = "hall_story",
        ["visibility"] = visibility
    };
    var root = new JsonObject
    {
        ["factions"] = new JsonArray(faction)
    };

    Assert.Equal(
        expectedVisible,
        SarefMainStoryState
            .GetPlayerVisibleShiningFactions(root)
            .Any());
}
```

- [ ] **Step 2: Run doc/privacy tests and verify RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~ValidationSourceGuardTests|FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~AfterlifeShiningPlayerFacingSourceGuardTests|FullyQualifiedName~ShiningAbodeStateTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests"
```

Expected: missing contract/example/manifest tokens and privacy filtering.

- [ ] **Step 3: Update Mortal authoring guidance and examples**

`Rules/Block_21.txt` must define:

```json
{
  "purpose": "non-empty setting-authored purpose",
  "currentAgenda": "non-empty current agenda",
  "principles": ["one or more unique principles"],
  "memory": {
    "summary": "non-empty",
    "lastUpdatedTurn": "integer >= 0",
    "enduringFacts": [],
    "openThreads": []
  },
  "governance": {
    "model": "non-empty",
    "decisionProcess": "non-empty"
  },
  "leadership": {
    "leadershipState": "headed | collective | vacant",
    "summary": "non-empty",
    "leaderNpcIds": []
  }
}
```

Then document the exact envelope, seven capabilities/sections, full carrier
creation/promotion restriction, `scribeChronicle` extraction, all six
`factionCoreChanges` groups, and existing dedicated commands. Add four validated
Mortal examples: populated creation, seven-empty creation, legacy promotion,
and ordinary core/relations update, plus a bounded repair example.

- [ ] **Step 4: Update Shining authoring guidance and examples**

Document the exact common Shining fields, seven capabilities/sections, exact
empty surfaces, and these route bindings:

```text
native_discovery -> shining_core_action_request -> native_radiant
player_founding -> shining_founding_request -> player_founded
story -> exact story authority -> supported visibility
```

Add native, player-founded, story/hidden, and Shining repair examples. State
that an active Guardian does not cause the client to invent a faction charter;
the GM must author the supported story route with exact Actor Materialization.
Repeat the Chaos Sea Guardian Politics boundary explicitly.

- [ ] **Step 5: Update manifest and daemon prompt**

Add the modified guides/examples to
`Examples/example_validation_manifest.json` using the existing manifest schema.
Update the daemon's compact Mortal faction directive so any faction
creation/promotion/update opens the compact faction template and the exact
materialization guidance. Keep the large example file conditional as required by
the existing prompt-performance policy.

- [ ] **Step 6: Enforce generic Shining visibility and filter private metadata**

In `SarefMainStoryState.cs`, preserve legacy Wings behavior but make the new
canonical visibility authoritative:

```csharp
public static bool IsPlayerVisibleShiningFaction(JsonObject? faction)
{
    if (faction == null)
        return false;

    var visibility = GetNodeString(faction["visibility"]);
    if (!string.IsNullOrWhiteSpace(visibility))
    {
        return string.Equals(
            visibility,
            FactionVisibilityRevealed,
            StringComparison.OrdinalIgnoreCase);
    }

    return !IsHiddenWingsFaction(faction);
}

public static IEnumerable<JsonObject> GetPlayerVisibleShiningFactions(
    JsonObject? shiningRoot)
{
    if (shiningRoot?["factions"] is not JsonArray factions)
        yield break;

    foreach (var faction in factions.OfType<JsonObject>())
    {
        if (IsPlayerVisibleShiningFaction(faction))
            yield return faction;
    }
}
```

Route ordinary faction enumerations in the two Shining UI builders through
`GetPlayerVisibleShiningFactions`; pending-contract/GM-repair inspection may
retain canonical access but must not feed normal player blocks.

When cloning a faction for player output, use an explicit helper:

```csharp
private static void RemovePrivateFactionMaterialization(JsonNode? node)
{
    switch (node)
    {
        case JsonObject obj:
            obj.Remove(FactionMaterializationContract.PropertyName);
            foreach (var child in obj.Select(pair => pair.Value).ToList())
                RemovePrivateFactionMaterialization(child);
            break;
        case JsonArray array:
            foreach (var child in array)
                RemovePrivateFactionMaterialization(child);
            break;
    }
}
```

Call it from `CloneShiningJsonForPlayerFacingAudit` after cloning and before
rendering. Replace `RemoveHiddenSarefWingsFactions` with a generic faction
filter that uses `IsPlayerVisibleShiningFaction` for objects containing
`factionId`, `originType`, and `hallId`. Do not stringify or selectively render
the envelope. Existing visible gameplay fields remain unchanged.

- [ ] **Step 7: Run doc/privacy tests to GREEN**

Run the Step 2 command.

Expected: required tokens/examples/manifest entries are present and ordinary
reader guards find zero private materialization tokens.

- [ ] **Step 8: Commit**

```powershell
git add -- 'Rules/Block_21.txt' 'Examples/E_Block_21.txt' 'TaskGuides/CLI_Step_Main.txt' 'Examples/E_CLI_Step_Main.txt' 'CLI_API_Specification.md' 'CLI_Agent_Daemon_Specification.md' 'Rules/Block_CLI_Operations.txt' 'BookOfEternityClient/game_master_daemon.ps1' 'OtherGuides/Afterlife_Contract_Matrix.md' 'docs/audits/afterlife/shining-abode/Shining_Abode_Faction_Politics_Addendum.md' 'Examples/E_CLI_Afterlife_Turns.txt' 'Examples/example_validation_manifest.json' 'BookOfEternityClient/Services/SarefMainStoryState.cs' 'BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs' 'BookOfEternityClient/UI/ExplorerShiningAbodeCommandResultBuilder.cs' 'BookOfEternityClient.Tests/ShiningAbodeStateTests.cs' 'BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs' 'BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs' 'BookOfEternityClient.Tests/ValidationSourceGuardTests.cs' 'BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs' 'BookOfEternityClient.Tests/AfterlifeShiningPlayerFacingSourceGuardTests.cs' 'BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs'
git commit -m "docs: document faction materialization contracts (#1510)"
```

---

### Task 11: Run bounded cross-domain verification and address review findings

**Files:**

- Modify only files implicated by a focused failure or accepted review finding.
- Update: `specs/1510-complete-faction-materialization/tasks.md` checkboxes only
  after matching evidence exists.
- Update the PR description/issue notes, not production code, with command
  results and residual risk.

**Interfaces:**

- Consumes: final candidate implementation.
- Produces: fresh bounded evidence and a review-ready exact commit.

- [ ] **Step 1: Run all focused feature selections**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FactionMaterializationContractTests|FullyQualifiedName~FactionCoreChangesContractTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FactionMaterializationValidationTests|FullyQualifiedName~FactionCoreChangesTests|FullyQualifiedName~CanonicalStateNormalizerTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FullValidationEquivalenceTests|FullyQualifiedName~IntegrationTestBoundaryTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~ValidationSourceGuardTests|FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~AfterlifeShiningPlayerFacingSourceGuardTests|FullyQualifiedName~ShiningAbodeStateTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests"
```

Expected: all selected tests pass with no timeout, duplicate test ID, or
owned-process cleanup failure.

- [ ] **Step 2: Run one Fast checkpoint**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Fast
```

Expected: PASS under the existing five-minute hard limit. Record total tests,
duration, duplicates, timeout status, and cleanup status.

- [ ] **Step 3: Run the required afterlife contract control**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh .\scripts\test-csharp.ps1 -Lane FullValidation
```

Expected: both PASS. Do not run DeepValidation unless a related failure requires
diagnosis.

- [ ] **Step 4: Review the diff against every requirement**

Run:

```powershell
git diff --check
git status --short
git diff --stat
git diff -- 'BookOfEternityClient' 'BookOfEternityClient.Tests' 'BookOfEternityClient.IntegrationTests' 'Rules' 'TaskGuides' 'OtherGuides' 'Examples' 'docs'
```

Expected: no whitespace errors; no `.serena/`, `bin/`, `obj/`, test result, or
unrelated file staged. Check FR-001 through FR-035 and SC-001 through SC-010
against `spec.md`, then verify the #1222/#1368/#1462 boundaries.

- [ ] **Step 5: Request code review and fix only verified findings**

Use the `requesting-code-review` skill. For each Critical/Important finding:
reproduce it with a focused failing test, implement the bounded correction,
rerun that filter, and add one concrete correction subtask to
`specs/1510-complete-faction-materialization/tasks.md` naming the exact
production/test files before editing. Stage those explicitly named paths only,
then commit with
`fix: address faction materialization review (#1510)`. Never stage the
worktree broadly.

- [ ] **Step 6: Run clean-checkout PreMerge once**

Create/use a clean checkout of the exact final candidate commit, then:

```powershell
git status --short
pwsh .\scripts\test-csharp.ps1 -Lane PreMerge
```

Expected: clean status before the run and PASS under the existing 15-minute
limit, preferably under 10 minutes. Do not run a duplicate Fast immediately
before PreMerge.

- [ ] **Step 7: Reconcile tasks and prepare the PR**

Mark a Spec Kit task complete only when its diff and fresh evidence were
inspected. The PR description must link #1510, summarize common/Mortal/Shining
authority, list every verification command with counts/durations, state that
FullValidation was required by the afterlife docs boundary, confirm no timeout
or concurrency increase, and record any residual risk/follow-up issue.

Commit planning/task checkbox reconciliation separately only if it changes:

```powershell
git add -- 'specs/1510-complete-faction-materialization/tasks.md'
git commit -m "docs: reconcile faction materialization tasks (#1510)"
```
