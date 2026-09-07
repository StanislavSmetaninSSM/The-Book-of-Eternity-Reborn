# Light Incarnate Historical Authority Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent a current spiritual exchange or resolution from waiving Light Incarnate through an unauthenticated pre-grant marker or reused historical payload.

**Architecture:** Keep the existing bonus and grant rules. Reuse the accepted active-exchange membership result; replace the separate pooled no-marker scan with a one-use exact recent-resolution tracker. Pass that private membership explicitly to capstone validation, so submitted dates classify only already-proved history.

**Tech Stack:** C#/.NET 8, System.Text.Json.Nodes, existing file-backed ValidationService, xUnit, PowerShell 7 bounded lanes.

## Global Constraints

- Tracked source: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), open T085 with bounded T089-T092 synchronization/verification; specs/1536-complete-wound-materialization/spec.md, plan.md and tasks.md are feature authority.
- **FR-031**: Ordinary spiritual wound eligibility MUST occur only on an accepted harmful strain transition and MUST reuse the accepted exchange evidence without a second injury roll.
- **FR-032**: The maximum spiritual severity MUST be calculated from harmful margin, applied-art tier, target Spiritual Resilience tier, destination strain rank, extra strain jumps, and the conflict-mode cap according to the approved design formula.
- Under a validated turn baseline, pre-grant Light Incarnate compatibility requires accepted pre-turn payload membership, not a submitted old turn marker. Missing authoritative dice does not remove that baseline.
- Active exchanges reuse the existing same-conflict, exact-first, one-use historical classifier, including its optional missing/null/string top-level-summary-only old-marker compatibility. Recent resolutions consume exact full payload occurrences from the pre-turn recent list only; the two surfaces never share a pool.
- Preserve all existing no-baseline offline history, accepted no-marker compatibility, current explicit-marker checks, full Source of Light closure checks and exact lead/support/coercion bonus arithmetic. Do not change marker precedence or add a new current-turn-equality policy.
- The resource outcome publisher still requires the exact pre-turn prefix. No history rewrite, migration, wound/healing producer, grant-authority redesign, art schema, pending receipt or public DTO is introduced.
- Work only in E:/Games/worktrees/boe-1536-wound-materialization on 1536-complete-wound-materialization. Preserve unrelated .serena/. No new branch, push, PR, merge or issue closure.
- Synchronize GM turn guidance, API/daemon guidance, afterlife matrix, worked example, manifest and source guard. Mortal contracts and player commands do not change. Existing daemon entrypoints already load the changed guide.
- C# has one owner, PowerShell 7 and scripts/test-csharp.ps1 only. Every returned session ID must drain fully before another lane, build, formatting or source edit. No overlapping lanes and no edits while a lane runs.
- Use Focused RED/GREEN/owners, one unchanged five-minute Fast, and one conditional FullValidation within the measured fifteen-minute cap. No PreMerge or full RegressionIntegration class run at this bounded checkpoint. Do not change filters, assertions, membership or concurrency to obtain green.

## Design and root-cause findings

The source at ValidationService.SourceOfLightCapstone.cs:773-890 reads a submitted
turn and permits zero bonus whenever it predates the grant, without consulting
the accepted active-exchange classifier. Its no-marker path uses an unconsumed
Any(DeepEquals) pool combining active exchanges and recent resolutions. Thus an
unmatched new backdated exchange, new backdated resolution, or duplicate no-marker
entry can waive the passive despite current-turn authority.

Three approaches were considered: retain marker trust and add GM warnings
(leaves the validator gap); build a new historical-turn registry (new durable
schema and migration/design burden); reuse the existing signed snapshot evidence
(recommended and selected within the approved T085 authority design). The latter
preserves old accepted data, makes the two consumers agree, and requires no new
player-facing mechanic. User has approved autonomous execution of the full #1536
specification; no new gameplay decision is requested here. The unrelated legacy
preparation question remains unanswered and is not decided by this plan.

The change proves only the Light Incarnate history-admission boundary. Full grant
authority still comes from the existing closure lookup; current-turn marker
consistency beyond pre-grant omission, recent-resolution ordinary dice authority,
cross-exchange dice ownership, strain/actor/resilience proofs, danger/seals and
actual spiritual wound/healing publication stay with their open tasks.

## File map

- Modify BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs: replace pooled no-marker lookup with a recent-only payload list, reuse the active classification, and pass one-use recent membership.
- Modify BookOfEternityClient/Services/Validation/ValidationService.SourceOfLightCapstone.cs: require trusted membership for pre-grant/no-marker exemptions during a validated turn.
- Create BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.LightIncarnateHistoricalAuthority.cs: twenty-two real file-backed cases in the existing partial/traits.
- Create BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.LightIncarnateHistoricalAuthority.cs: one deterministic documentation/manifest guard in the existing partial.
- Modify TaskGuides/CLI_Step_Main.txt, CLI_API_Specification.md, CLI_Agent_Daemon_Specification.md and OtherGuides/Afterlife_Contract_Matrix.md: qualify the existing capstone guidance with snapshot-backed history.
- Modify Examples/E_CLI_Afterlife_Turns.txt: worked accepted-versus-new capstone history contrast, without a new JSON fence.
- Modify Examples/example_validation_manifest.json: one precise production-validator coverage entry.
- Parent owns the feature spec/plan/tasks, this bounded plan and the metadata progress/review files; implementer owns only the ten listed runtime/test/GM paths plus its supplied report.

### Task 1: Bind capstone history to accepted occurrences

**Interfaces:**
- Consume existing PreTurnConflictPayloadTracker.TryConsume(JsonObject, bool),
  isPreTurnExchange and validated pending-turn snapshot readers.
- Keep ResolveLightIncarnateGrantTurnAsync and ResolveLightIncarnateAuditTurn unchanged.
- Replace private context member PreTurnNoTurnDicePayloads with
  IReadOnlyList<JsonObject>? PreTurnRecentConflictPayloads in the same argument position.
- Extend private ValidateRecentConflictProof with final bool isPreTurnProof and
  ValidateLightIncarnateDiceAuditModifier with final bool isPreTurnPayload.
  Both existing direct capstone callers must pass the applicable membership.
- No default true, public authority factory, cache or persisted result is added.

- [x] **Step 1: Add the complete Integration and documentation regression tests.**

Create the Integration partial exactly as follows. Its fixtures use the existing
complete capstone closure (grant7), actual snapshot/file helpers and current dice.
The tests assert only the named capstone boundary at the exact payload index,
plus validation non-mutation; unrelated minimal-fixture diagnostics do not make
these full-publication tests. The no-dice helper re-seals the real test manifest
using the existing test authority helper, instead of bypassing validation.

```csharp
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualConflictValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task T085_LightIncarnateHistory_BackdatedAppendRequiresCurrentAuthority(bool recent)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent, 6);
        await SnapshotEmptyLightIncarnateHistoryAsync(root, recent);
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent, 0, expectMismatch: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task T085_LightIncarnateHistory_ChangedOldPayloadIsNotPreGrantEvidence(bool recent)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent, 6);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Continue after the accepted old audit.");
        GetLightIncarnateHistoryLog(root, recent)[0]!["operationType"] = "pressure";
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent, 0, expectMismatch: true);
    }

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 6)]
    [InlineData(false, null)]
    [InlineData(true, null)]
    public async Task T085_LightIncarnateHistory_OneOccurrenceCannotExemptAnother(bool recent, int? turn)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent, turn);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Continue after one accepted occurrence.");
        var log = GetLightIncarnateHistoryLog(root, recent);
        log.Add(log[0]!.DeepClone());
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        var before = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var issues = await ValidateAfterlifeSpiritualConflictAsync();

        AssertLightIncarnateHistoryIssues(issues, recent, 0, expectMismatch: false);
        AssertLightIncarnateHistoryIssues(issues, recent, 1, expectMismatch: true);
        Assert.Equal(before, await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath));
    }

    [Fact]
    public async Task T085_LightIncarnateHistory_ForeignActiveConflictCannotBorrowNoTurnPayload()
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent: false, turn: null);
        await WriteValidatedConflictSnapshotFromCurrentAsync("The old active conflict is accepted.");
        root["activeConflict"]!["conflictId"] = "afterlife_conflict_replacement";
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent: false, 0, expectMismatch: true);
    }

    [Fact]
    public async Task T085_LightIncarnateHistory_RecentProofCannotBorrowActiveNoTurnPayload()
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent: false, turn: null);
        await WriteValidatedConflictSnapshotFromCurrentAsync("An exchange is not an accepted resolution.");
        var payload = GetLightIncarnateHistoryLog(root, recent: false)[0]!.DeepClone();
        root["activeConflict"] = null;
        root["recentConflicts"] = new JsonArray(payload);
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent: true, 0, expectMismatch: true);
    }

    [Theory]
    [InlineData("payload_resolved")]
    [InlineData("dice_turn")]
    [InlineData("nested_resolution")]
    public async Task T085_LightIncarnateHistory_OldMarkerPrecedenceCannotExemptCurrentExchange(string marker)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent: false, turn: 7);
        await SnapshotEmptyLightIncarnateHistoryAsync(root, recent: false);
        var payload = GetLightIncarnateHistoryLog(root, recent: false)[0]!.AsObject();
        switch (marker)
        {
            case "payload_resolved":
                payload["resolvedAtTurn"] = 6;
                break;
            case "dice_turn":
                payload.Remove("exchangeAtTurn");
                payload["diceAudit"]!["turnNumber"] = 6;
                break;
            case "nested_resolution":
                payload.Remove("exchangeAtTurn");
                payload["resolution"] = new JsonObject { ["resolvedAtTurn"] = 6 };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(marker), marker, null);
        }
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent: false, 0, expectMismatch: true);
    }

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 6)]
    [InlineData(false, null)]
    [InlineData(true, null)]
    public async Task T085_LightIncarnateHistory_ValidatedBaselineWithoutDiceStillRequiresCurrentAuthority(
        bool recent, int? turn)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent, turn);
        await SnapshotEmptyLightIncarnateHistoryAsync(root, recent);
        await RemoveLightIncarnateHistoryFixtureDiceAsync();
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent, 0, expectMismatch: true);
    }

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 6)]
    [InlineData(false, null)]
    [InlineData(true, null)]
    public async Task T085_LightIncarnateHistory_ExactAcceptedOccurrenceKeepsCompatibility(bool recent, int? turn)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent, turn);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Preserve the accepted old audit.");
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent, 0, expectMismatch: false);
    }

    [Fact]
    public async Task T085_LightIncarnateHistory_OldActiveReadableSummaryKeepsExistingCompatibility()
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent: false, turn: 6);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Preserve all accepted mechanics.");
        GetLightIncarnateHistoryLog(root, recent: false)[0]!["summary"] = "Only the readable wording differs.";
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent: false, 0, expectMismatch: false);
    }

    private async Task<JsonObject> CreateLightIncarnateHistoryRootAsync(bool recent, int? turn)
    {
        await WriteSoulStateWithLightIncarnateAsync();
        if (recent)
        {
            await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
            {
              "schemaVersion": 1,
              "activeConflict": null,
              "recentConflicts": [{
                "conflictId": "afterlife_conflict_light_history_006",
                "resolutionState": "resolved",
                "operationType": "guard",
                "playerOutcome": "won",
                "summary": "The accepted conflict ended before the grant.",
                "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
              }]
            }
            """);
        }
        else
        {
            await WriteConflictStateWithRawExchangeAsync($$"""
            {
              "exchangeId": "exchange_light_history_006",
              "operationType": "guard",
              "outcome": "success",
              "summary": "The accepted exchange predates the grant.",
              "before": { "conflictPosition": "contested" },
              "after": { "conflictPosition": "player_advantaged" },
              "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
            }
            """);
        }

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath))!)!.AsObject();
        if (turn.HasValue)
            GetLightIncarnateHistoryLog(root, recent)[0]![recent ? "resolvedAtTurn" : "exchangeAtTurn"] = turn.Value;
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        return root;
    }

    private async Task SnapshotEmptyLightIncarnateHistoryAsync(JsonObject candidate, bool recent)
    {
        var baseline = candidate.DeepClone().AsObject();
        GetLightIncarnateHistoryLog(baseline, recent).Clear();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, baseline.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Resolve a genuinely new current audit.");
    }

    private async Task RemoveLightIncarnateHistoryFixtureDiceAsync()
    {
        const string requestPath = "input/turn_request.json";
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        var request = JsonNode.Parse((await _fs.ReadFileAsync(requestPath))!)!.AsObject();
        var manifest = JsonNode.Parse((await _fs.ReadFileAsync(manifestPath))!)!.AsObject();
        request["preGeneratedDices1d20"] = new JsonArray();
        manifest["preGeneratedDices1d20"] = new JsonArray();
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync(requestPath, request.ToJsonString());
        await _fs.WriteFileAtomicAsync(manifestPath, manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static JsonArray GetLightIncarnateHistoryLog(JsonObject root, bool recent) =>
        (recent ? root["recentConflicts"] : root["activeConflict"]!["exchangeLog"])!.AsArray();

    private async Task WriteAndAssertLightIncarnateHistoryAsync(
        JsonObject root, bool recent, int index, bool expectMismatch)
    {
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        var before = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var issues = await ValidateAfterlifeSpiritualConflictAsync();
        AssertLightIncarnateHistoryIssues(issues, recent, index, expectMismatch);
        Assert.Equal(before, await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath));
    }

    private static void AssertLightIncarnateHistoryIssues(
        IReadOnlyList<ValidationIssue> issues, bool recent, int index, bool expectMismatch)
    {
        var path = recent ? $".recentConflicts[{index}].diceAudit" : $".activeConflict.exchangeLog[{index}].diceAudit";
        var selected = issues.Where(issue => issue.FilePath.Contains(path, StringComparison.Ordinal)).ToArray();
        var mismatches = selected.Where(issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.Ordinal)).ToArray();
        if (expectMismatch)
            Assert.Single(mismatches);
        else
            Assert.Empty(mismatches);
        Assert.DoesNotContain(selected, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_unauthorized", StringComparison.Ordinal));
    }
}
```

Create the documentation partial exactly as follows:

```csharp
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeDocumentationCoverageTests
{
    [Fact]
    public void LightIncarnateHistoryDocumentation_RequiresAcceptedPayloadNotOldMarker()
    {
        var guide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var api = ReadRepoFile("CLI_API_Specification.md");
        var daemon = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        foreach (var text in new[] { guide, matrix, api, daemon, examples })
        {
            foreach (var invariant in new[]
            {
                "Light Incarnate history requires accepted pre-turn payload evidence",
                "one occurrence once",
                "active exchanges and recent resolutions never share a history pool",
                "backdating alone cannot waive light_incarnate",
                "validated baseline without dice is still a current-turn boundary",
                "offline history without a validated baseline keeps its existing compatibility"
            })
            {
                Assert.Contains(invariant, text, StringComparison.Ordinal);
            }
        }

        foreach (var token in new[]
        {
            "afterlife_light_incarnate_history_authority_v1",
            "exchange_light_history_006",
            "grantedAtTurn=7",
            "resolvedAtTurn=6",
            "afterlife_conflict_light_incarnate_modifier_mismatch",
            "only the top-level summary",
            "exact pre-turn prefix",
            "not full wound or grant authority"
        })
        {
            Assert.Contains(token, examples, StringComparison.Ordinal);
        }

        using var manifest = JsonDocument.Parse(ReadRepoFile("Examples", "example_validation_manifest.json"));
        var entry = Assert.Single(
            manifest.RootElement.GetProperty("afterlifeEntityProfileCoverage").EnumerateArray(),
            item => item.GetProperty("contractId").GetString() == "afterlife_light_incarnate_history_authority_v1");
        Assert.Equal("E_CLI_Afterlife_Turns.txt", entry.GetProperty("file").GetString());
        Assert.Equal("production-validator", entry.GetProperty("validationKind").GetString());
        Assert.Contains("T085_LightIncarnateHistory_", entry.GetProperty("validationRoute").GetString()!, StringComparison.Ordinal);
        Assert.Contains("LightIncarnateHistoryDocumentation_", entry.GetProperty("validationRoute").GetString()!, StringComparison.Ordinal);
        Assert.Contains("not full wound or grant authority", entry.GetProperty("coverageLimit").GetString()!, StringComparison.Ordinal);
        var required = entry.GetProperty("requiredText").EnumerateArray().ToArray();
        Assert.NotEmpty(required);
        Assert.All(required, token => Assert.Contains(token.GetString()!, examples, StringComparison.Ordinal));
    }
}
```

- [x] **Step 2: Observe the focused semantic RED before runtime or GM changes.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 5 -Filter "FullyQualifiedName~T085_LightIncarnateHistory_"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~LightIncarnateHistoryDocumentation_"
```

Run sequentially, fully drain each session. Expected Integration22 executes:
17 rows fail on the missing capstone mismatch, five historical controls pass.
Documentation1 fails on absent new invariant. Compile/setup/snapshot exceptions
are not behavioral RED; investigate them before touching production code.

- [x] **Step 3: Replace the pooled lookup and pass explicit membership.**

In AfterlifeConflictDiceContext replace only the third parameter declaration:

```csharp
        IReadOnlyList<JsonObject>? PreTurnRecentConflictPayloads = null,
```

Remove the now-obsolete IsPreTurnNoTurnDicePayload method from that private record.
In ResolveAfterlifeConflictDiceContextAsync replace the local initialization with:

```csharp
        var preTurnRecentConflictPayloads = await ResolvePreTurnRecentConflictPayloadsAsync(manifest);
```

Replace each of its four positional preTurnNoTurnDicePayloads arguments with
preTurnRecentConflictPayloads, leaving every other argument and branch unchanged.
Replace the whole ResolvePreTurnNoTurnConflictDicePayloadsAsync method with the
following and remove its now-unused TryAddPreTurnNoTurnDicePayload helper.
The existing TryAddPreTurnConflictPayload already deep-clones and is reused.

```csharp
    private async Task<IReadOnlyList<JsonObject>> ResolvePreTurnRecentConflictPayloadsAsync(
        ValidationPendingTurnSnapshotManifest? manifest)
    {
        if (manifest == null)
            return Array.Empty<JsonObject>();

        var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(AfterlifeSpiritualConflictState.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return Array.Empty<JsonObject>();

        try
        {
            if (JsonNode.Parse(preTurnJson) is not JsonObject root)
                return Array.Empty<JsonObject>();

            var payloads = new List<JsonObject>();
            if (root["recentConflicts"] is JsonArray recentConflicts)
            {
                foreach (var entry in recentConflicts.OfType<JsonObject>())
                    TryAddPreTurnConflictPayload(payloads, entry);
            }

            return payloads;
        }
        catch
        {
            // Malformed conflict state is reported by the normal state validator.
            return Array.Empty<JsonObject>();
        }
    }
```

Replace the root recentConflicts branch body (retain the existing else diagnostic):

```csharp
            var rewardConflictIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var preTurnProofTracker = new PreTurnConflictPayloadTracker(
                diceContext.HasValidatedTurnBaseline ? diceContext.PreTurnRecentConflictPayloads : null);
            for (var index = 0; index < recentConflicts.Count; index++)
            {
                if (recentConflicts[index] is not JsonObject proof)
                    continue;

                var isPreTurnProof = preTurnProofTracker.TryConsume(
                    proof, allowHistoricalSummaryDrift: false);
                ValidateRecentConflictProof(
                    proof, $"{context}.recentConflicts[{index}]", issues, diceContext,
                    rewardContext, rewardConflictIds, soulDissipationContext, isPreTurnProof);
            }
```

Update the private recent-proof signature:

```csharp
    private void ValidateRecentConflictProof(
        JsonObject proof,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictDiceContext diceContext,
        AfterlifeConflictRewardContext rewardContext,
        HashSet<string> rewardConflictIds,
        AfterlifeSoulDissipationContext soulDissipationContext,
        bool isPreTurnProof)
```

Replace only its capstone call, retaining ordinary dice/reward/dissipation calls:

```csharp
            ValidateLightIncarnateDiceAuditModifier(
                proof, diceAudit, $"{context}.diceAudit", issues, diceContext, isPreTurnProof);
```

Replace only the active-exchange capstone call:

```csharp
            ValidateLightIncarnateDiceAuditModifier(
                exchange, diceAudit, $"{context}.diceAudit", issues, diceContext, isPreTurnExchange);
```

In SourceOfLightCapstone update the private signature:

```csharp
    private static void ValidateLightIncarnateDiceAuditModifier(
        JsonObject payload,
        JsonObject diceAudit,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictDiceContext diceContext,
        bool isPreTurnPayload)
```

Inside its existing missing-marker branch replace only the two compatibility
conditions. Keep the existing missing-marker diagnostic and return unchanged.

```csharp
            if (isPreTurnPayload)
                return;

            if (actual == 0 && !diceContext.HasAuthoritativeDice && !diceContext.HasValidatedTurnBaseline)
                return;
```

Immediately before the existing auditTurn < grant branch, insert:

```csharp
        if (auditTurn.Value < diceContext.LightIncarnateGrantTurn!.Value &&
            diceContext.HasValidatedTurnBaseline &&
            !isPreTurnPayload)
        {
            issues.Add(new ValidationIssue(
                $"{context}.turnNumber",
                IssueSeverity.Error,
                "Новый contested audit не может пропустить Воплощение Света, указав ход до получения искусства.",
                code: "afterlife_conflict_light_incarnate_modifier_mismatch",
                section: "AfterlifeSpiritualConflict",
                expected: $"accepted pre-turn payload or current audit with turn >= {diceContext.LightIncarnateGrantTurn!.Value} and explicit light_incarnate modifier",
                actual: $"auditTurn={auditTurn.Value}; modifier sum={actual}; accepted pre-turn payload=false",
                repairHint: "Исправь turn marker и явный light_incarnate modifier текущего exchange/resolution; не переписывай принятые старые записи."));
            return;
        }
```

The existing pre-grant nonzero unauthorized rejection, post-grant bonus calculation,
role/coercion arithmetic and no-closure rejection remain unchanged. Search all
references to the renamed private members and both extended methods; no stale
pool consumer or unupdated call may remain.

- [x] **Step 4: Synchronize the exact GM contract and worked contrast.**

The following qualification is the exact shared contract paragraph:

```text
Light Incarnate history requires accepted pre-turn payload evidence during a validated turn: consume one occurrence once, and active exchanges and recent resolutions never share a history pool. Active exchanges reuse the same-conflict exchange classifier; recent resolutions require an exact full payload match. Thus backdating alone cannot waive light_incarnate, and a validated baseline without dice is still a current-turn boundary. An accepted no-marker payload retains compatibility; an unmatched current payload needs explicit turn evidence and the applicable modifier. Historical pre-grant dice are preserved, while offline history without a validated baseline keeps its existing compatibility. This does not authorize editing the exact pre-turn prefix.
```

Add it once as a bullet immediately after the historical-exchange bullet in
TaskGuides/CLI_Step_Main.txt. Append it to the existing Source of Light paragraph
in the matrix (the paragraph containing "Historical conflict logs before"),
CLI_API_Specification.md (the /source_of_light bullet), and
CLI_Agent_Daemon_Specification.md (the paragraph beginning "If the soul has").
These paragraphs describe the bonus/closure semantics and must remain otherwise
unchanged. Unlike the prior marker-only classifier correction, this paragraph
qualifies a distinct capstone rule; do not duplicate or replace the accepted
general historical-exchange rule. Keep the full existing capstone worked JSON.

In Examples/E_CLI_Afterlife_Turns.txt, insert the following text immediately before
the existing "After this reward, any contested afterlife spiritual conflict" paragraph.
Do not add a JSON fence or remove any surrounding Source of Light example.

```text
Worked history boundary: afterlife_light_incarnate_history_authority_v1

Light Incarnate history requires accepted pre-turn payload evidence during a validated turn: consume one occurrence once, and active exchanges and recent resolutions never share a history pool. Active exchanges reuse the same-conflict exchange classifier; recent resolutions require an exact full payload match. Thus backdating alone cannot waive light_incarnate, and a validated baseline without dice is still a current-turn boundary. An accepted no-marker payload retains compatibility; an unmatched current payload needs explicit turn evidence and the applicable modifier. Historical pre-grant dice are preserved, while offline history without a validated baseline keeps its existing compatibility. This does not authorize editing the exact pre-turn prefix.

The complete Source of Light closure grants the passive at grantedAtTurn=7. A validated turn-7 snapshot contains exchange_light_history_006 with exchangeAtTurn=6 and no light_incarnate modifier. Keep that accepted occurrence unchanged: its old dice do not acquire a retroactive bonus. The existing active-exchange audit compatibility may differ in only the top-level summary (missing/null/string), never mechanical data; publication still preserves the exact pre-turn prefix.

By contrast, appending a new exchange with exchangeAtTurn=6, adding a new recent resolution with resolvedAtTurn=6, changing an old payload, or copying one accepted occurrence twice does not prove pre-grant history. The client reports afterlife_conflict_light_incarnate_modifier_mismatch for the current payload. Resolve that current audit with explicit current turn evidence and the applicable +8 lead / +4 support bonus, plus the existing +4 coercion bonus where applicable; do not repair it by changing accepted old dice. A recent resolution must match its own accepted recentConflicts[] occurrence exactly; it cannot borrow an active exchange or ignore a changed summary.

An accepted no-marker occurrence remains compatible, but an unmatched current no-marker payload must supply turn evidence even when the validated baseline has no dice. This example demonstrates history admission, not full wound or grant authority; it does not grant an art, create a wound or authorize a resource-prefix rewrite.
```

Add this single entry first in afterlifeEntityProfileCoverage (preserve all
existing entries and other arrays):

```json
{
  "contractId": "afterlife_light_incarnate_history_authority_v1",
  "file": "E_CLI_Afterlife_Turns.txt",
  "statePath": "game_state/meta/afterlife_spiritual_conflict_state.json",
  "responseSurface": "afterlifeSpiritualConflictUpdate exchange/resolution diceAudit",
  "realms": ["Chaos Sea", "Shining Abode"],
  "description": "Accepted pre-turn capstone history versus a new, changed, duplicate or cross-surface pre-grant claim.",
  "validationKind": "production-validator",
  "validationRoute": "AfterlifeSpiritualConflictValidationTests.T085_LightIncarnateHistory_ -> ValidationService.ValidateGameStateAsync(AfterlifeSpiritualConflictState); AfterlifeDocumentationCoverageTests.LightIncarnateHistoryDocumentation_RequiresAcceptedPayloadNotOldMarker",
  "coverageLimit": "Light Incarnate historical admission only; not full wound or grant authority, resource publication, actor progression or healing.",
  "requiredText": ["afterlife_light_incarnate_history_authority_v1", "exchange_light_history_006", "grantedAtTurn=7", "resolvedAtTurn=6", "backdating alone cannot waive light_incarnate", "afterlife_conflict_light_incarnate_modifier_mismatch", "not full wound or grant authority"]
}
```

- [x] **Step 5: Verify behavior, retained neighbors and the changed GM boundary.**

First run the Integration and documentation filters from Step2 again, expecting
22/22 and1/1 PASS. Then run these owner/checkpoint commands sequentially:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 5 -Filter "FullyQualifiedName~AfterlifeSpiritualConflictValidationTests&FullyQualifiedName~LightIncarnate|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests&FullyQualifiedName~HistoricalConflict|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests&FullyQualifiedName~PersistedExchangeWithoutTurnMarker|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests&FullyQualifiedName~PreTurnRecentConflictWithoutTurnMarker|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests&FullyQualifiedName~ChangedNoTurnConflict|FullyQualifiedName~T085_HistoricalExchange_"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
.\scripts\test-csharp.ps1 -Lane Fast
.\scripts\test-csharp.ps1 -Lane FullValidation -TimeoutMinutes 15
```

The owner filter retains the old offline pre-grant/no-marker tests, accepted
current-snapshot controls, current required/unauthorized/lead/support/coercion
checks and the previous historical classifier's sixteen cases. Record actual
owner count rather than guessing discovery expansion.

FullValidation is required by the changed afterlife docs/examples/manifest.
Its already measured10:09 to10:47 lower bound justifies15m; do not repeat it.
The existing full-spiritual diagnostic remains selectable, but this tightly
bounded task uses its exact affected owner selection; any branch-wide exhaustive
control belongs to the final integration checkpoint.

If the one Fast times out with no functional failure, preserve exit124, inspect
actual TRXs plus discovery and run only its exact uncompleted selection once in
Focused. Reconcile runtime theory expansions by method. This may prove bounded
case coverage, not a successful Fast or T177 completion. If a real test fails,
diagnose and report it before broadening scope or changing old assertions.
Do not change lane capacity, membership or concurrency. No PreMerge here.

- [x] **Step 6: Self-review, commit exact scope and obtain independent review.**

Inspect the complete ten-file diff and all actual summaries/TRXs/build/cleanup,
run git diff --check, and recursively check the manifest for duplicate keys.
Review that grant lookup, bonus math, old tests, resource exact-prefix and
Mortal contracts are unchanged. Stage only the ten owned paths and commit.
Append source/RED/GREEN/owner/bounded-lane evidence, commands, wall times, paths,
all warnings/failures and limitations to the supplied report. Return only status,
commits, a one-line test summary and concerns. Parent inspects source/artifacts
and requests independent Spec/Quality review over the recorded original BASE
through the final HEAD. Do not close any full #1536 task from this prerequisite.

## Parent self-review

- The plan changes the history evidence input, not the bonus, grant or gameplay
  rules. No new durable state, command, DTO, migration or schema is introduced.
- Active membership reuses the accepted classifier; recent membership is exact
  and one-use in a separate pool. No bool defaults can accidentally exempt a caller.
- Missing dice cannot remove a validated baseline. Existing no-baseline historical
  compatibility remains an explicit retained control, not a new publication path.
- The twenty-two tests comprise seventeen semantic rejection rows and five
  accepted-history controls. Existing capstone closure/role/bonus tests remain.
  Minimal fixtures assert the exact capstone diagnostic, not total game validity.
- Every named helper/signature is present in source or defined in this plan.
  Test manifest changes use existing hash/signed-authority helpers; no runtime
  validation bypass or synthetic wound authority is introduced.
- All afterlife GM entry guidance and the worked example/manifest/source guard
  receive the same precise qualification. The general historical classifier's
  resource-prefix warning remains intact. No Mortal prompt change is required.
- The separate grant-authority lookup, full wound/arts/healing integration,
  recovery receipts and legacy preparation decision remain explicitly open.

## Parent acceptance — 2026-09-07

Accepted only this bounded history-admission prerequisite. Original task BASE
`025fed7ea3eb1cebe3f343747812e26749f4c576`; implementation `cbbfa98f`; reviewed
correction `d9547c52b859b904da29a992613ded243a021383`. The independent reviewer
returned **Spec compliant / Task quality Approved**, with no Critical or
Important findings, after reviewing the complete original-BASE-to-final-HEAD
two-commit range. Parent inspected the final diffs, every listed run's actual
summaries/TRXs/build/cleanup, and manifest duplicate properties/contract IDs.
The ten-file source/GM scope is exact and `git diff --check` is clean.

Review added one complementary fact before `CreateLightIncarnateHistoryRootAsync`
in the supplied Integration block; all original constants and multiline source
were restored exactly. This brings the new boundary selection to 23 cases:

```csharp
    [Fact]
    public async Task T085_LightIncarnateHistory_ChangedRecentSummaryIsNotPreGrantEvidence()
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent: true, turn: 6);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Preserve the exact accepted resolution.");
        GetLightIncarnateHistoryLog(root, recent: true)[0]!["summary"] = "Changed recent resolution wording.";
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent: true, 0, expectMismatch: true);
    }
```

The new fact was proved by temporarily enabling recent summary drift, observing
its exact semantic failure, then restoring the runtime line before the final
owner run. The temporary mutation was never committed. The final runtime file's
SHA-256 is `02F88277B3956896D33149845B3D060153B8D81D6EF4494311DC0EC751E2BB87`,
identical to the successful Fast/Full candidate. No production, GM, example,
manifest or Fast-test change followed those controls; the final correction is
only the Integration partial, covered by the final owner run.

All paths in this table are relative to `TestResults/test-lanes/`:

| Run directory | Actual result | Wall time / cap |
| --- | --- | --- |
| `20260907-132357-976-43568-ba0fbced882a41d38bde91ad227684b9-focused` | Initial Integration RED: 17 semantic failures, 5 passing historical controls | 1:47.296 / 5m |
| `20260907-132549-853-42172-de2b82db4a5241189ec4c0af5e03af22-focused` | Documentation RED: 1 missing-invariant failure | 0:33.865 / 5m |
| `20260907-132940-794-3972-4a2ade7dff894ad2977d0c2984003ab1-focused` | Initial Integration GREEN: 22/22 | 1:45.403 / 5m |
| `20260907-133130-528-38052-8af84d3d708f419f8b4121289fb0c70f-focused` | Initial documentation GREEN: 1/1 | 0:32.495 / 5m |
| `20260907-133211-161-33512-5f075a0290c546779fef5a7af4ff5eec-focused` | Owner GREEN: 53/53, comprising 22 new + 16 prior + 15 existing | 1:07.839 / 5m |
| `20260907-133349-703-30352-fc3e9739feea482284b27dc60708ace6-focused` | Build-only failure: temporary doc reflow CS1026, zero tests, exit 1 | 0:06.376 / 5m |
| `20260907-133405-013-41764-dc5829dd0d604a3280e08d5fb3bef99f-focused` | Corrected documentation owners: 123/123 | 0:36.387 / 5m |
| `20260907-133446-109-46568-68b6f8609c854d66a67c10991b97a87a-fast` | One Fast: 7,631/7,631, exit 0 | 3:50.280 / unchanged 5m |
| `20260907-133841-490-35976-48df9de4f5f649c3af7712603922a42c-fullvalidation` | One conditional FullValidation: 1,857/1,857, exit 0 | 10:50.932 / measured 15m |
| `20260907-140804-947-45568-405286808769476f9be7bad843a7ad9e-focused` | Deliberate recent-summary mutation RED: 1 semantic failure | 1:20.216 / 5m |
| `20260907-140949-695-37248-2bc1f130ed9649428bd958c0507f6ae8-focused` | Restored final owners: 54/54, comprising 23 new + 16 prior + 15 existing | 1:52.908 / 5m |

Every completed lane has clean owned-tree cleanup and no timeout. Successful
builds have zero warnings/errors; the isolated CS1026 build-only failure remains
recorded and is not behavioral RED. The actual lanes are sequential; the small
13:35 runner self-test/discovery/hard-cap artifacts are not extra full controls.
Fast's 26 TRXs reconcile 7,577 discovery rows plus 54 runtime-expanded theory
rows across all 3,931 expected methods. Full's 11 TRXs reconcile 1,747 plus 110
across all 1,074 methods. Both have no missing/extra method, duplicate execution
ID, cross-TRX test ID, skipped or unexpected result.

The final owner command used bare second operands (`&LightIncarnate`, etc.)
instead of the fully qualified spelling above. Its exact command is preserved
in the run log and implementation report. Parent compared every actual case
name to the earlier full-form 53-case run: only the intended new fact was added,
with no missing or unexpected case. The reviewer records this as a non-blocking
command-form deviation, not a coverage gap; no duplicate run is warranted.

Retain two Minor review items for final whole-branch/T177 review: the command-form
deviation above, and the documentation guard's independent substrings rather
than exact paragraph/anchor adjacency. Direct source review verified the actual
GM paragraph placement. Alleged new loader duplication/broad catches were
withdrawn after original-BASE comparison proved they were pre-existing.

GM synchronization is complete for the turn guide, API, daemon guidance,
afterlife matrix, worked example, manifest and source guard. Existing daemon
entrypoints load those guides; no launcher change is needed. Mortal rules,
prompts and commands do not change. Full T084/T085/T089-T092/T177 and #1536 remain
open, as do actual wound/strain/actor/resilience provenance, grant authority,
arts/healing/recovery publication and the pending legacy decision. This
acceptance does not change the top-level 77/177 count or authorize merge/closure.
