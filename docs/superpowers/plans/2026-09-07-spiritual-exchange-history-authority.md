# Spiritual Exchange Historical Authority Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Require same-conflict, one-use accepted snapshot evidence before a spiritual exchange bypasses current dice, matchup and action-cost checks.

**Architecture:** Keep the existing snapshot tracker and all current audit validators. Remove marker-alone historical authority; retain its existing readable-summary compatibility only when every other member matches an unconsumed accepted occurrence. This is a prerequisite for accepted spiritual wound evidence, not the spiritual wound producer.

**Tech Stack:** C#/.NET, System.Text.Json.Nodes, xUnit, PowerShell 7 bounded test lanes, Markdown/CDATA GM documentation and JSON manifest.

## Global Constraints

- Tracked source: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), open T085 with bounded T089-T092 documentation/verification; `specs/1536-complete-wound-materialization/spec.md`, `plan.md`, and `tasks.md` remain the durable feature authority.
- **FR-031**: Ordinary spiritual wound eligibility MUST occur only on an accepted harmful strain transition and MUST reuse the accepted exchange evidence without a second injury roll.
- **FR-032**: The maximum spiritual severity MUST be calculated from harmful margin, applied-art tier, target Spiritual Resilience tier, destination strain rank, extra strain jumps, and the conflict-mode cap according to the approved design formula.
- Exact `JsonNode.DeepEquals` matches are consumed first. Historical payloads must belong to the same validated pre-turn active `conflictId`, using the existing `OrdinalIgnoreCase` conflict-identity convention, and each snapshot occurrence may be consumed once.
- A lower `exchangeAtTurn` is never sufficient authority. It permits only a fallback match where every other member except the exact top-level `summary` key is equal, and both summaries are missing/null/string. Nested summary, number/object/array summary, added/removed mechanical members, changed turn/identity/dice/art/strain, foreign conflict and duplicate consumption receive no such exemption.
- Preserve the existing old-marker/readable-summary integration regression and exact no-marker historical compatibility. No migration, no mandatory new turn field and no change to validation without an active validated baseline.
- Summary tolerance is exchange-audit validation compatibility only. `AfterlifeSpiritualConflictResourceOutcome` still requires the exact entire pre-turn prefix and fingerprints full new exchange JSON. Do not widen that publication contract, permit history rewriting, or treat mutable historical prose as new wound evidence.
- Source of Light's separate `light_incarnate` turn-marker path, cross-exchange dice consumption, strain continuity, participant/resilience authority, art progression schemas, danger modes, wound seals, healing, recovery receipts and publishers are outside this bounded task. T084/T085/T089-T092/T177/#1536 remain open.
- Work only in `E:/Games/worktrees/boe-1536-wound-materialization`, branch `1536-complete-wound-materialization`; preserve unrelated `.serena/`. No new branch, push, PR, merge or issue closure.
- GM-facing synchronization must include turn guidance, afterlife matrix, a worked example, manifest and source guard. Mortal contracts do not change. Existing daemon entrypoint already loads `TaskGuides/CLI_Step_Main.txt`; do not add a redundant entrypoint or prompt file.
- C# execution has one owner and runs sequentially through PowerShell 7 `scripts/test-csharp.ps1`. A returned session ID means the command is STILL RUNNING: drain it fully before another lane, source edit, formatting or build. Never overlap lanes or change a source while a lane is active.
- Use smallest Focused RED/GREEN/owners, one unchanged five-minute Fast at the meaningful checkpoint, and one conditional FullValidation with the already measured fifteen-minute diagnostic limit. No PreMerge or whole RegressionIntegration class run here. Do not change lane membership, assertions, timeout caps or concurrency.

## File map

- `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`: scoped one-use historical classification and narrow summary comparison.
- `BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.cs`: add only `partial` to the class declaration; all existing tests stay unchanged.
- `BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.HistoricalExchangeAuthority.cs`: sixteen focused file-backed behavior cases using existing real snapshot helpers.
- `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`: add only `partial` to the class declaration.
- `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.HistoricalExchangeAuthority.cs`: one deterministic guide/example/manifest coverage test.
- `TaskGuides/CLI_Step_Main.txt`: remove marker-alone ambiguity and explain the bounded exchange-audit rule.
- `OtherGuides/Afterlife_Contract_Matrix.md`: synchronize authority and preserved stricter publication fence.
- `Examples/E_CLI_Afterlife_Turns.txt`: worked historical/current/replay contrast beside the existing dice example, without a new JSON fence.
- `Examples/example_validation_manifest.json`: one truthful afterlife coverage entry with runtime/source-guard routes and explicit limits.

### Task 1: Preserve accepted history without accepting backdated mechanics

**Files:** Exactly the nine files in the file map. Parent owns feature/spec/plan/task and progress acceptance bookkeeping; implementer does not edit them.

**Interfaces:**

- Existing private `AfterlifeConflictDiceContext` carries `HasValidatedTurnBaseline`, `CurrentTurn`, `PreTurnActiveConflictId` and `PreTurnConflictPayloads`. No field or public signature changes.
- Existing `TryReadConflictId`, `TryGetJsonNodeInt`, current dice/matchup/action-cost validators and resource publication remain unchanged.
- Existing real integration fixture methods `WriteSoulStateWithAfterlifeCombatProfileAsync`, `WriteConflictStateWithRawExchangeAsync`, `WritePreTurnActiveConflictSnapshotWithAuthorityAsync`, `WriteValidatedConflictSnapshotFromCurrentAsync`, `BuildPriorTurnDiceAuditJson` and `ValidateAfterlifeSpiritualConflictAsync` remain unchanged. Snapshot helper seals turn 7 and current d20 values 5/18 at indices 0/1; prior dice helper has 9/7.
- New private tracker overload `TryConsume(JsonObject payload, bool allowHistoricalSummaryDrift)` replaces the sole one-argument caller. No new canonical state, command, receipt or authority factory.

- [x] **Step 1: Add complete behavior and documentation RED tests.**

Change the two existing class declarations only:

```csharp
public sealed partial class AfterlifeSpiritualConflictValidationTests : IDisposable
public sealed partial class AfterlifeDocumentationCoverageTests
```

Create the Integration partial with this complete code:

```csharp
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualConflictValidationTests
{
    [Fact]
    public async Task T085_HistoricalExchange_BackdatedAppendRequiresAllCurrentAudits()
    {
        await WriteHistoricalExchangeAuthoritySoulAsync();
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();
        await WriteConflictStateWithRawExchangeAsync(
            BuildHistoricalExchangeAuthorityPayload().ToJsonString(),
            addDefaultMatchupAudit: false, addDefaultActionCostAudit: false);

        await AssertHistoricalExchangeAuthorityAsync(0, expectCurrent: true);
    }

    [Theory]
    [InlineData("art_tier")]
    [InlineData("dice_value")]
    [InlineData("nested_summary")]
    [InlineData("added_member")]
    [InlineData("removed_member")]
    [InlineData("turn_marker")]
    [InlineData("invalid_summary_number")]
    [InlineData("invalid_summary_object")]
    public async Task T085_HistoricalExchange_ChangedEvidenceRequiresAllCurrentAudits(string mutation)
    {
        var root = await WriteHistoricalExchangeAuthoritySnapshotAsync();
        var exchange = root["activeConflict"]!["exchangeLog"]![0]!.AsObject();
        switch (mutation)
        {
            case "art_tier":
                exchange["actionCostAudit"]!["player"]!["artTier"] = 4;
                break;
            case "dice_value":
                exchange["diceAudit"]!["diceUsed"]![0]!["value"] = 10;
                break;
            case "nested_summary":
                exchange["diceAudit"]!["summary"] = "Not the readable exchange summary.";
                break;
            case "added_member":
                exchange["unacceptedEvidence"] = true;
                break;
            case "removed_member":
                exchange.Remove("exchangeId");
                break;
            case "turn_marker":
                exchange["exchangeAtTurn"] = 5;
                break;
            case "invalid_summary_number":
                exchange["summary"] = 42;
                break;
            case "invalid_summary_object":
                exchange["summary"] = new JsonObject { ["text"] = "Not a string." };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());

        await AssertHistoricalExchangeAuthorityAsync(0, expectCurrent: true);
    }

    [Fact]
    public async Task T085_HistoricalExchange_ForeignConflictCannotBorrowAcceptedOccurrence()
    {
        var root = await WriteHistoricalExchangeAuthoritySnapshotAsync();
        root["activeConflict"]!["conflictId"] = "afterlife_conflict_unaccepted_replacement";
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());

        await AssertHistoricalExchangeAuthorityAsync(0, expectCurrent: true);
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("reworded")]
    [InlineData("null")]
    [InlineData("missing")]
    public async Task T085_HistoricalExchange_ExactOrReadableSummaryOnlyKeepsAuditCompatibility(string summary)
    {
        var root = await WriteHistoricalExchangeAuthoritySnapshotAsync();
        var exchange = root["activeConflict"]!["exchangeLog"]![0]!.AsObject();
        switch (summary)
        {
            case "unchanged":
                break;
            case "reworded":
                exchange["summary"] = "The old exchange remains old; only its readable wording differs.";
                break;
            case "null":
                exchange["summary"] = null;
                break;
            case "missing":
                exchange.Remove("summary");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(summary), summary, null);
        }
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());

        await AssertHistoricalExchangeAuthorityAsync(0, expectCurrent: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task T085_HistoricalExchange_OneOccurrenceCannotExemptSecondCopy(bool changeSecondSummary)
    {
        var root = await WriteHistoricalExchangeAuthoritySnapshotAsync();
        var log = root["activeConflict"]!["exchangeLog"]!.AsArray();
        var second = log[0]!.DeepClone().AsObject();
        if (changeSecondSummary)
            second["summary"] = "A second copy is not another accepted occurrence.";
        log.Add(second);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());

        var before = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var issues = await ValidateAfterlifeSpiritualConflictAsync();
        AssertHistoricalExchangeAuthorityIssues(issues, 0, expectCurrent: false);
        AssertHistoricalExchangeAuthorityIssues(issues, 1, expectCurrent: true);
        Assert.Equal(before, await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath));
    }

    private Task WriteHistoricalExchangeAuthoritySoulAsync() =>
        WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "pressure": 0 }
        }
        """);

    private async Task<JsonObject> WriteHistoricalExchangeAuthoritySnapshotAsync()
    {
        await WriteHistoricalExchangeAuthoritySoulAsync();
        await WriteConflictStateWithRawExchangeAsync(
            BuildHistoricalExchangeAuthorityPayload().ToJsonString(),
            addDefaultMatchupAudit: false, addDefaultActionCostAudit: false);
        await WriteValidatedConflictSnapshotFromCurrentAsync("I continue the same accepted conflict.");
        return JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath))!)!.AsObject();
    }

    private static JsonObject BuildHistoricalExchangeAuthorityPayload() => JsonNode.Parse($$"""
    {
      "exchangeId": "exchange_accepted_history_006",
      "exchangeAtTurn": 6,
      "operationType": "pressure",
      "outcome": "no_effect",
      "summary": "The old pressure exchange left no new strain.",
      "before": {
        "playerSideStrain": "clear",
        "oppositionSideStrain": "clear",
        "conflictPosition": "contested"
      },
      "after": {
        "playerSideStrain": "clear",
        "oppositionSideStrain": "clear",
        "conflictPosition": "contested"
      },
      "diceAudit": {{BuildPriorTurnDiceAuditJson()}},
      "actionCostAudit": {
        "player": {
          "operationType": "pressure",
          "baseCost": 3,
          "minCost": 1,
          "artTier": 5,
          "effectiveCost": 1,
          "before": 6,
          "after": 5
        }
      }
    }
    """)!.AsObject();

    private async Task AssertHistoricalExchangeAuthorityAsync(int index, bool expectCurrent)
    {
        var before = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var issues = await ValidateAfterlifeSpiritualConflictAsync();
        AssertHistoricalExchangeAuthorityIssues(issues, index, expectCurrent);
        Assert.Equal(before, await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath));
    }

    private static void AssertHistoricalExchangeAuthorityIssues(
        IReadOnlyList<ValidationIssue> issues, int index, bool expectCurrent)
    {
        foreach (var code in new[]
        {
            "afterlife_conflict_dice_value_not_authorized",
            "afterlife_conflict_matchup_audit_missing",
            "afterlife_conflict_action_cost_art_tier_authority_mismatch"
        })
        {
            var matches = issues.Where(issue =>
                string.Equals(issue.Code, code, StringComparison.Ordinal) &&
                issue.FilePath.Contains($".activeConflict.exchangeLog[{index}]", StringComparison.Ordinal));
            if (expectCurrent)
                Assert.NotEmpty(matches);
            else
                Assert.Empty(matches);
        }
    }
}
```

The three audit assertions intentionally do not assert that the entire fixture is
publishable. Its historical cost may differ from today's art profile; the existing
resource prefix and other state validators still apply. These tests prove the
classifier through real validation, not a mock or reflection call.

Create the documentation partial with this complete code:

```csharp
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeDocumentationCoverageTests
{
    [Fact]
    public void HistoricalExchangeAuthorityDocumentation_RequiresSnapshotNotBackdating()
    {
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        foreach (var text in new[]
        {
            ReadRepoFile("TaskGuides", "CLI_Step_Main.txt"),
            ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md"),
            examples
        })
        {
            foreach (var invariant in new[]
            {
                "same validated pre-turn active conflictId",
                "each snapshot occurrence once",
                "exchangeAtTurn alone is not authority",
                "only the top-level summary",
                "missing/null/string",
                "current dice, matchup and action-cost checks",
                "validation compatibility only",
                "exact pre-turn prefix"
            })
                Assert.Contains(invariant, text, StringComparison.Ordinal);
        }

        foreach (var token in new[]
        {
            "afterlife_spiritual_exchange_history_authority_v1",
            "exchange_accepted_history_006",
            "afterlife_conflict_dice_value_not_authorized",
            "afterlife_conflict_matchup_audit_missing",
            "afterlife_conflict_action_cost_art_tier_authority_mismatch",
            "afterlife_conflict_resource_log_prefix_mutated"
        })
            Assert.Contains(token, examples, StringComparison.Ordinal);

        using var manifest = JsonDocument.Parse(ReadRepoFile("Examples", "example_validation_manifest.json"));
        var entry = Assert.Single(manifest.RootElement.GetProperty("afterlifeEntityProfileCoverage").EnumerateArray(),
            item => item.GetProperty("contractId").GetString() == "afterlife_spiritual_exchange_history_authority_v1");
        Assert.Equal("E_CLI_Afterlife_Turns.txt", entry.GetProperty("file").GetString());
        Assert.Equal("production-validator", entry.GetProperty("validationKind").GetString());
        Assert.Contains("T085_HistoricalExchange_", entry.GetProperty("validationRoute").GetString()!, StringComparison.Ordinal);
        Assert.Contains("HistoricalExchangeAuthorityDocumentation_", entry.GetProperty("validationRoute").GetString()!, StringComparison.Ordinal);
        Assert.Contains("not full wound or publication authority", entry.GetProperty("coverageLimit").GetString()!, StringComparison.Ordinal);
        var required = entry.GetProperty("requiredText").EnumerateArray().ToArray();
        Assert.NotEmpty(required);
        Assert.All(required, token => Assert.Contains(token.GetString()!, examples, StringComparison.Ordinal));
    }
}
```

- [x] **Step 2: Observe semantic RED, sequentially, before production or GM edits.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 5 -Filter "FullyQualifiedName~T085_HistoricalExchange_"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~HistoricalExchangeAuthorityDocumentation_"
```

First run should execute sixteen cases: twelve semantic failures because the old
classifier exempts unaccepted/changed/duplicate payloads, four historical controls
PASS. Second run should fail on missing documentation. Wait for each returned
session to actually finish; these are not parallel commands. If there is a compile
or fixture error instead, report it and fix only the test setup before claiming RED.

- [x] **Step 3: Implement the narrow classifier without changing validators.**

Replace `PreTurnConflictPayloadTracker` with:

```csharp
    private sealed class PreTurnConflictPayloadTracker
    {
        private readonly IReadOnlyList<JsonObject> _payloads;
        private readonly bool[] _consumed;

        public PreTurnConflictPayloadTracker(IReadOnlyList<JsonObject>? payloads)
        {
            _payloads = payloads ?? Array.Empty<JsonObject>();
            _consumed = new bool[_payloads.Count];
        }

        public bool TryConsume(JsonObject payload, bool allowHistoricalSummaryDrift) =>
            TryConsumeMatching(payload, ignoreSummary: false) ||
            (allowHistoricalSummaryDrift && TryConsumeMatching(payload, ignoreSummary: true));

        private bool TryConsumeMatching(JsonObject payload, bool ignoreSummary)
        {
            for (var index = 0; index < _payloads.Count; index++)
            {
                if (_consumed[index])
                    continue;

                var matches = ignoreSummary
                    ? MatchesExceptReadableSummary(_payloads[index], payload)
                    : JsonNode.DeepEquals(_payloads[index], payload);
                if (!matches)
                    continue;

                _consumed[index] = true;
                return true;
            }

            return false;
        }

        private static bool MatchesExceptReadableSummary(JsonObject accepted, JsonObject current)
        {
            if (!IsOptionalSummaryText(accepted["summary"]) || !IsOptionalSummaryText(current["summary"]))
                return false;

            foreach (var member in accepted)
            {
                if (member.Key == "summary")
                    continue;
                if (!current.TryGetPropertyValue(member.Key, out var value) ||
                    !JsonNode.DeepEquals(member.Value, value))
                    return false;
            }

            foreach (var member in current)
            {
                if (member.Key != "summary" && !accepted.ContainsKey(member.Key))
                    return false;
            }

            return true;
        }

        private static bool IsOptionalSummaryText(JsonNode? node) =>
            node is null || node is JsonValue value && value.TryGetValue<string>(out _);
    }
```

Inside `ValidateActiveAfterlifeConflict`, replace the tracker initialization and
the `isPreTurnExchange` expression only:

```csharp
            var scopedPreTurnPayloads = diceContext.HasValidatedTurnBaseline &&
                !string.IsNullOrWhiteSpace(diceContext.PreTurnActiveConflictId) &&
                string.Equals(TryReadConflictId(conflict), diceContext.PreTurnActiveConflictId, StringComparison.OrdinalIgnoreCase)
                    ? diceContext.PreTurnConflictPayloads
                    : null;
            var preTurnExchangePayloads = new PreTurnConflictPayloadTracker(scopedPreTurnPayloads);
```

```csharp
                    var isPreTurnExchange = preTurnExchangePayloads.TryConsume(
                        exchange, allowHistoricalSummaryDrift: HasPriorTurnMarker(exchange, diceContext));
```

Rename the sole private `IsExchangeFromPriorTurn` helper to reflect its weaker role;
its arithmetic is unchanged:

```csharp
    private static bool HasPriorTurnMarker(JsonObject exchange, AfterlifeConflictDiceContext diceContext)
    {
        if (!diceContext.HasValidatedTurnBaseline ||
            diceContext.CurrentTurn is not int currentTurn ||
            !TryGetJsonNodeInt(exchange["exchangeAtTurn"], out var exchangeTurn))
        {
            return false;
        }

        return exchangeTurn < currentTurn;
    }
```

No change to `WithoutCurrentTurnDiceAuthority`, Light Incarnate, action-cost or
matchup validation, resource outcome/publisher, snapshot readers or public models.

- [x] **Step 4: Synchronize the actual GM boundary and worked contrast.**

In `TaskGuides/CLI_Step_Main.txt`, replace the single historical-dice bullet with
this exact text at its existing indentation:

```text
- Current `input/turn_request.json.preGeneratedDices1d20` is dice authority only for current/new contested exchanges or resolutions. Preserve accepted historical exchange dice rather than rewriting them to the current pool. Historical exchange-audit exemption requires the same validated pre-turn active conflictId and consumes each snapshot occurrence once; exchangeAtTurn alone is not authority. An old turn marker permits a match differing in only the top-level summary, restricted to missing/null/string, with every other member exact. New, mechanically changed, foreign-conflict or duplicate exchanges receive current dice, matchup and action-cost checks. This is validation compatibility only: resource publication still preserves the exact pre-turn prefix, including summary. Restore an altered accepted prefix when its dedicated diagnostic requests it; do not reroll old dice.
```

In the matrix, replace just the sentence beginning `Historical exchange logs from
earlier accepted turns` with the following, preserving the preceding runtime dice
sentence and all following natural-critical rules:

```text
Historical exchange-audit exemption requires the same validated pre-turn active conflictId and consumes each snapshot occurrence once; exchangeAtTurn alone is not authority. An old turn marker permits a match differing in only the top-level summary, restricted to missing/null/string, with every other member exact. Accepted historical dice stay unchanged; new, mechanically changed, foreign-conflict or duplicate exchanges receive current dice, matchup and action-cost checks. This is validation compatibility only: resource publication still preserves the exact pre-turn prefix, including summary, and a dedicated prefix diagnostic requires restoring that old entry rather than rewriting old dice.
```

In the example, replace just the sentence beginning `Accepted historical
exchangeLog[] entries from earlier turns` with:

```text
Accepted historical dice stay unchanged only with the same validated pre-turn active conflictId and each snapshot occurrence once; exchangeAtTurn alone is not authority. An old turn marker permits a match differing in only the top-level summary, restricted to missing/null/string, with every other member exact. New, mechanically changed, foreign-conflict or duplicate exchanges receive current dice, matchup and action-cost checks. This is validation compatibility only: resource publication still preserves the exact pre-turn prefix, including summary.
```

Immediately after that existing example paragraph, insert this worked prose
continuation (no JSON fence, no snippet extraction exemptions):

```text
HISTORICAL EXCHANGE AUTHORITY — afterlife_spiritual_exchange_history_authority_v1

The accepted pre-turn snapshot for conflict afterlife_conflict_test_001 contains one exchange_accepted_history_006 with exchangeAtTurn=6. Its old player/opposition dice at sourceIndex=0/1 were 9/7. The current accepted request is turn 7, whose same indices contain 5/18. The GM preserves the old entry and authors any new exchange against the current request's dice and art authority; old dice are not rewritten to 5/18.

If only the old readable summary differs, its exchange audit keeps historical compatibility after matching every other member against that one snapshot occurrence. This does not authorize publishing the edit: afterlife_conflict_resource_log_prefix_mutated still requires restoring the exact accepted prefix. New artistic narration belongs in the current response, not a rewritten accepted history row.

A newly appended entry claiming exchangeAtTurn=6 has no matching accepted occurrence. Neither copying the accepted entry twice, changing its artTier or nested dice data, nor placing it under a different conflictId creates historical authority. The unmatched entry receives afterlife_conflict_dice_value_not_authorized for old values 9/7 instead of current 5/18, afterlife_conflict_matchup_audit_missing if its current tactical audit is absent, and afterlife_conflict_action_cost_art_tier_authority_mismatch if artTier=5 disagrees with the current accepted pressure tier 0. Repair the current exchange from current authority; do not backdate it to suppress these diagnostics.

This contrast covers exchange dice/matchup/action-cost classification, not a new wound, healing operation, accepted resource publication or Source of Light capstone decision. A historical description is never a new harmful strain event by itself.
```

Insert this complete new object once into `afterlifeEntityProfileCoverage` beside
the existing spiritual conflict entries; keep other manifest objects unchanged:

```json
{
  "contractId": "afterlife_spiritual_exchange_history_authority_v1",
  "file": "E_CLI_Afterlife_Turns.txt",
  "statePath": "game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict.exchangeLog",
  "responseSurface": "afterlifeSpiritualConflictUpdate.exchange and retained accepted exchange history",
  "realms": ["Chaos Sea", "Shining Abode"],
  "description": "Same-conflict one-use snapshot authority for historical exchange dice, matchup and action-cost audit exemption; marker-alone backdating rejected, narrow readable-summary validation compatibility retained.",
  "validationKind": "production-validator",
  "validationRoute": "AfterlifeSpiritualConflictValidationTests.T085_HistoricalExchange_ -> ValidationService.ValidateGameStateAsync(AfterlifeSpiritualConflictState); AfterlifeDocumentationCoverageTests.HistoricalExchangeAuthorityDocumentation_RequiresSnapshotNotBackdating",
  "coverageLimit": "Behavioral exchange-audit classification and worked text coverage, not full wound or publication authority. The resource publisher still requires the exact pre-turn prefix. Source of Light has a separate turn-marker contract outside this prerequisite.",
  "requiredText": ["afterlife_spiritual_exchange_history_authority_v1", "exchange_accepted_history_006", "exchangeAtTurn alone is not authority", "validation compatibility only", "exact pre-turn prefix", "afterlife_conflict_resource_log_prefix_mutated"]
}
```

- [x] **Step 5: Verify the changed behavior and its exact neighboring boundaries.**

Run the new Integration filter from Step 2 first, then these commands sequentially:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 5 -Filter "FullyQualifiedName~AfterlifeSpiritualConflictValidationTests&FullyQualifiedName~PreTurn|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests&FullyQualifiedName~HistoricalConflict|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests&FullyQualifiedName~CurrentContestedExchange|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests&FullyQualifiedName~CurrentNoEffectExchange|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests&FullyQualifiedName~CurrentTerminalExchange|FullyQualifiedName~DuplicateCurrentExchangeCannotReusePreTurnMatchupExemption|FullyQualifiedName~CurrentExchangeRequiresActionCostAudit|FullyQualifiedName~CurrentIncomingActionRequiresOppositionActionCostAudit|FullyQualifiedName~RejectsActionCostArtTierAboveAuthorityProfile|FullyQualifiedName~ContestedExchange_RejectsDiceNotFromAuthoritativePool"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
.\scripts\test-csharp.ps1 -Lane Fast
.\scripts\test-csharp.ps1 -Lane FullValidation -TimeoutMinutes 15
```

Expected all new and selected existing rows PASS, including the unchanged summary
drift regression. The focused owner set checks pre-turn identity/control/realm,
historical capstone controls and current dice/matchup/action-cost diagnostics
without the entire 350-plus-case exhaustive class. FullValidation is conditional
and required because afterlife docs/example/manifest changed; its already measured
ten-to-eleven-minute lower bound justifies fifteen minutes. No new JSON snippet
is introduced and its parse/runtime consumers must remain green.

One Fast is a bounded development control, not a promise of lane capacity. If it
times out without a failing test, stop further whole-Fast attempts, inspect all
completed TRXs/discovery and run only the exact uncompleted selection once through
bounded Focused. Reconcile runtime-expanded theory rows by method, not raw count
subtraction. Retain exit124 and T177/performance/final PreMerge as open; this task's
case-coverage evidence does not relabel Fast GREEN. If any executed test fails,
diagnose its exact cause and report before expanding scope or altering old tests.
Do not run a diagnostic FullValidation again after an unrelated timing issue.

Inspect actual summaries, every TRX, build output, test identities and cleanup.
Record discovered versus executed counts separately, all warnings and timeouts.
When the tool yields a running session ID, poll that SAME session until completion.
There must never be another test/build/source-edit owner in parallel.

- [x] **Step 6: Commit exact paths and obtain independent acceptance.**

Self-review the nine-file change, run `git diff --check`, recursively check the
final manifest for duplicate keys and preserve the existing old-marker test byte
for byte. Stage only the nine owned paths, check the staged diff and commit.
Write the full report to the supplied metadata report path with source changes,
RED/GREEN commands, actual artifact directories/counts/warnings, constraints and
remaining caveats. Return status, commit, one-line evidence and concerns only.
Parent inspects actual source/artifacts and generates a review package for the
entire recorded BASE..HEAD range, then obtains fresh independent Spec/Quality
review. No top-level #1536 task closes from this bounded prerequisite alone.

## Parent self-review

- The existing older-marker regression changes only readable summary. It is
  retained, not inverted or weakened. The new fallback needs real same-conflict
  snapshot membership, consumes once and cannot cover new mechanics.
- The resource outcome builder separately rejects any pre-turn prefix mutation;
  docs/tests explicitly distinguish exchange-audit compatibility from publication.
- Exact matching remains first, optional-summary comparison does not strip nested
  fields or coerce numeric/object values, and neither path mutates the payloads.
- All test helper signatures and imports were checked against current source.
  Sixteen rows cover twelve current-authority rejections and four positive controls;
  existing no-marker and summary-addition regressions remain in owner verification.
- New partials preserve existing traits and avoid growing the 15,000-line owning
  Integration file or the 4,000-line documentation file beyond one class modifier.
- No art schema, wound creation, progression, pending receipt, danger/escalation,
  natural recovery or capstone design is inferred. Those full-spec sections remain
  assigned to their existing open tasks. This plan owns T085's classifier prerequisite
  and bounded T089-T092 documentation/verification only.

## Bounded acceptance — 2026-09-07

Accepted implementation range is `a8e551c7869dc51b6765c0d44f96f21ee9fd9d9c`
through `27f6804c9ac6cb43603766f66f141c1d5350c706` (runtime `921feff0`,
four-file review correction `27f6804c`). Parent inspected the actual runtime,
test and GM diffs, all reported summaries/TRXs/build logs/cleanup and the final
source hashes. Independent original-base re-review is Spec Compliant / Quality
Approved, zero remaining Critical/Important/Minor findings. Its original GM-rule
duplication and test-readability findings are preserved in the review record and
resolved by the correction. Only this prerequisite is complete; full
T084/T085/T089-T092/T177 and #1536 remain open, with top-level progress 77/177.

Actual artifact directories below are under `TestResults/test-lanes/`:

| Run | Actual result | Bounded wall time |
| --- | --- | --- |
| `20260907-121257-736-41636-b60ae9b882944154ac584356804d5807-focused` | Integration RED: 4 PASS, 12 intended current-audit failures | 2:10.586 / 5m |
| `20260907-121512-865-2204-f259eba57cfd428683ce4f991dba820f-focused` | Documentation RED: 1 intended missing-invariant failure | 0:44.859 / 5m |
| `20260907-121824-002-43400-6194d89e82f04dc6b7512d53925e9c4f-focused` | New Integration 16/16 PASS | 1:56.772 / 5m |
| `20260907-122024-863-48048-f81312b7bd3b47edb77ebe2cc1000975-focused` | New documentation 1/1 PASS | 0:42.800 / 5m |
| `20260907-122115-914-35872-39b1964ac8c74d028f03b0b172a149ea-focused` | Existing neighboring boundaries 37/37 PASS | 1:03.268 / 5m |
| `20260907-122223-134-41332-77c177e4b554435981231e7e845cde6e-fast` | One Fast 7,630/7,630 PASS, exit 0 | 4:35.585 / 5m |
| `20260907-122708-141-36448-42afc8ba06874314b800acf42f2e5c4d-focused` | Example-preservation RED: 1 intended missing-sentence failure | 0:31.708 / 5m |
| `20260907-122755-839-46920-ba175f7254ac401a8d25781a5d18a0fc-focused` | Restored example, documentation owners 122/122 PASS | 0:19.523 / 5m |
| `20260907-122820-559-37212-74acb40a6fe4481094926a1818f01fbf-fullvalidation` | One conditional FullValidation 1,857/1,857 PASS | 10:09.537 / 15m |
| `20260907-124846-440-48692-5da5cf745205432aaec8ae9fb337794b-focused` | Initial corrective selection 1/1 PASS; not RED evidence | 1:11.745 / 5m |
| `20260907-125219-661-46948-feef01d5767049649f1816c22aa4dfd0-focused` | Documentation owners 122/122 PASS before final reflow | 0:35.082 / 5m |
| `20260907-125258-413-43076-9e0e765540d7467f8d425a32761953f6-focused` | Final behavior partial 16/16 PASS | 1:07.148 / 5m |
| `20260907-125443-995-28668-caff79ea1c0b45c5a1b2b84e259a7517-focused` | Corrective semantic RED: 1 obsolete-guide assertion failure | 0:14.776 / 5m |
| `20260907-125532-514-46588-ccd3ce4dcc644bca9a72d9b184dc75ec-focused` | Final documentation owners 122/122 PASS | 0:35.570 / 5m |

The initial corrective GREEN ran before the new negative source assertions,
according to the correction report's command/edit chronology. It is not credited
as regression proof. The actual corrective RED kept the new assertions and
temporarily restored only the two obsolete guide/matrix statements; the final
GREEN restored the consolidated wording. No lane overlapped another; all builds
reported zero warnings/errors, cleanup succeeded, and no test was skipped,
aborted, errored or executed twice within a run. No timeout occurred.

Parent reconciled Fast discovery 7,576 with 7,630 actual rows through seven
existing runtime theories (+54), with all 3,930 method identities accounted for.
FullValidation discovery 1,747 expands to 1,857 through five existing runtime
theories (+110), with all 1,074 method identities accounted for. Neither run has
missing/unexpected methods, cross-TRX test identities or duplicate execution IDs.
Extra zero-test directories belong to runner self-tests, not duplicate Fast runs.

The final production validator, worked example and manifest are byte-identical
to the artifacts used by the passing Fast/FullValidation controls. The correction
changes only new test layout/negative guards and consolidates the prescribed GM
wording; the final covering checks prove those edits without another broad run.
Mortal contracts do not change. The afterlife turn guide, matrix, worked example,
manifest and source guard are synchronized; the existing daemon entrypoint already
loads the changed guide. Resource exact-prefix publication and the separate
Source of Light authority path remain unchanged. No PreMerge, push, PR, merge,
migration or issue closure was performed.
