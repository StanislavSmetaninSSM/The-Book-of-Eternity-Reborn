using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const string MortalComponentAcquisition = "Край камня оставляет поверхностный порез на боку.";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task QtePhysicalInjuryConsumer_ComponentCommonPublicationReplaysOnceOrRejectsChangedDecision(bool materialize, bool changed)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var component = await PrepareMortalDecisionComponentAsync(materialize);
        Assert.Empty(await ValidateMortalDecisionComponentAsync(component.Engine));
        var first = await GetPrivateField<CanonicalStateNormalizer>(component.Engine, "_normalizer").NormalizeAcceptedMechanicsAsync(null);
        Assert.NotNull(first);
        Assert.Contains(MortalWoundOpportunityReceiptState.StatePath, first.OwnerCompanionAfterImages.Keys);
        Assert.Contains(MortalWoundOpportunityAdapter.DecisionPath, first.ConsumedPaths);
        Assert.Contains(AcceptedMechanicsPlan.WoundCommandPath, first.ConsumedPaths);
        Assert.Equal(materialize, first.WoundHistoryAfterImage is not null);
        var receipts = MortalWoundOpportunityReceiptState.Parse(await _fs.ReadFileAsync(MortalWoundOpportunityReceiptState.StatePath), MortalWoundOpportunityReceiptState.StatePath);
        Assert.True(receipts.IsValid, JsonSerializer.Serialize(receipts.Issues));
        var receipt = Assert.Single(receipts.State!.Receipts);
        Assert.Equal(materialize ? "materialize" : "none", receipt.Decision);
        Assert.Equal(component.Snapshot.RequestId, receipt.RequestId);
        Assert.Equal(component.Snapshot.SnapshotToken, receipt.SnapshotToken);
        Assert.Equal(43, receipt.Turn);
        Assert.Equal(42, receipt.SourceTurn);
        Assert.False(_fs.FileExists(MortalWoundOpportunityAdapter.DecisionPath));
        Assert.False(_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        var preserved = await CaptureMortalComponentImagesAsync();
        var original = (await _fs.ReadFileBytesAsync(PendingTurnSnapshotAuthority.AuthorityPath))!;
        var replayDraft = component.Decision.DeepClone().AsObject();
        if (changed)
        {
            if (materialize) replayDraft["proposal"]!["display"]!["description"] = "Иное описание для того же принятого события.";
            else replayDraft = new JsonObject { ["opportunityRef"] = replayDraft["opportunityRef"]!.DeepClone(),
                ["decision"] = "materialize", ["woundRef"] = "impact", ["proposal"] = CreateMortalComponentProposal() };
        }
        await _fs.WriteFileAtomicAsync(MortalWoundOpportunityAdapter.DecisionPath, replayDraft.ToJsonString());
        var issues = await ValidateMortalDecisionComponentAsync(component.Engine);
        if (changed)
        {
            Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease));
            Assert.False(_fs.FileExists(lease, AcceptedMechanicsPlan.WoundCommandPath));
        }
        else
        {
            Assert.Empty(issues);
            var replay = await GetPrivateField<CanonicalStateNormalizer>(component.Engine, "_normalizer").NormalizeAcceptedMechanicsAsync(null);
            Assert.NotNull(replay);
            Assert.Null(replay.WoundStageBundle);
            Assert.Null(replay.WoundHistoryAfterImage);
            Assert.Contains(MortalWoundOpportunityAdapter.DecisionPath, replay.ConsumedPaths);
            Assert.False(_fs.FileExists(MortalWoundOpportunityAdapter.DecisionPath));
        }
        foreach (var image in preserved) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        Assert.Equal(original, await _fs.ReadFileBytesAsync(PendingTurnSnapshotAuthority.AuthorityPath));
        WritePhysicalInjuryDiagnostic("qte_consumer_component_exact_or_changed_replay", new
        { materialize, changed, component.Snapshot.RequestId, component.Snapshot.SnapshotToken, receipt, issues, Draft = replayDraft,
            UnchangedCanonicalPaths = preserved.Keys });
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("scene")]
    [InlineData("foreign_command")]
    public async Task QtePhysicalInjuryConsumer_ComponentStagedInputDriftRevokesCacheAndPreservesForeignCommand(string mutation)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var component = await PrepareMortalDecisionComponentAsync(false);
        Assert.Empty(await ValidateMortalDecisionComponentAsync(component.Engine));
        AcceptedMechanicsPlan original;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(_fs, lease, out _, out var cached));
            original = cached.Plan!;
        }
        var preserved = await CaptureMortalComponentImagesAsync();
        var path = mutation switch
        { "draft" => MortalWoundOpportunityAdapter.DecisionPath, "scene" => "output/narrative_response.json", _ => AcceptedMechanicsPlan.WoundCommandPath };
        var changed = mutation switch
        { "draft" => "null", "scene" => "{\"response\":\"Герой меняет описание осмотра.\"}", _ => "{\"foreignCommand\":true}" };
        await _fs.WriteFileAtomicAsync(path, changed);
        var foreign = await _fs.ReadFileBytesAsync(path);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            var guarded = Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(await InvokePrivateTaskResultAsync(component.Engine,
                "SealMortalQteDecisionAsync", lease, original.BeforeImages));
            Assert.Contains(guarded, issue => issue.Code == "mortal_wound_decision_staged_input_changed");
        }
        var failure = await Record.ExceptionAsync(() => GetPrivateField<CanonicalStateNormalizer>(component.Engine, "_normalizer").NormalizeAcceptedMechanicsAsync(null));
        Assert.NotNull(failure);
        var issues = await ValidateMortalDecisionComponentAsync(component.Engine);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            if (mutation == "scene")
            {
                Assert.Empty(issues);
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(_fs, lease, out _, out var replacement));
                Assert.NotEqual(original.InputFingerprint, replacement.Plan!.InputFingerprint);
            }
            else
            {
                Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease));
            }
            if (mutation == "foreign_command") Assert.Equal(foreign, await _fs.ReadFileBytesAsync(lease, path));
            if (mutation == "draft") Assert.False(_fs.FileExists(lease, AcceptedMechanicsPlan.WoundCommandPath));
        }
        foreach (var image in preserved) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        WritePhysicalInjuryDiagnostic("qte_consumer_component_staged_drift", new
        { mutation, original.InputFingerprint, Failure = failure.ToString(), issues });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QtePhysicalInjuryConsumer_ComponentReceiptMemberFailureRollsBackAndExactResubmissionPublishesOnce(bool materialize)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var component = await PrepareMortalDecisionComponentAsync(materialize);
        Assert.Empty(await ValidateMortalDecisionComponentAsync(component.Engine));
        var before = BrowserRecoveryTree();
        var injected = false;
        _consolePublicationObserver = (phase, index) =>
        {
            if (injected || phase != TrustedLocalPublicationPhase.MemberPublished) return;
            var journalPath = Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(journalPath));
            if (journal.RootElement.GetProperty("Members")[index].GetProperty("Path").GetString() != _fs.ResolvePath(MortalWoundOpportunityReceiptState.StatePath)) return;
            Assert.False(journal.RootElement.GetProperty("Committed").GetBoolean());
            injected = true;
            throw new InvalidOperationException("Controlled Mortal decision receipt publication failure.");
        };
        Exception? failure;
        try { failure = await Record.ExceptionAsync(() => GetPrivateField<CanonicalStateNormalizer>(component.Engine, "_normalizer").NormalizeAcceptedMechanicsAsync(null)); }
        finally { _consolePublicationObserver = null; }
        Assert.True(injected);
        Assert.NotNull(failure);
        AssertBrowserRecoveryTree(before);
        Assert.False(_fs.FileExists(MortalWoundOpportunityReceiptState.StatePath));
        Assert.Empty(await ValidateMortalDecisionComponentAsync(component.Engine));
        var published = await GetPrivateField<CanonicalStateNormalizer>(component.Engine, "_normalizer").NormalizeAcceptedMechanicsAsync(null);
        Assert.NotNull(published);
        var receipts = MortalWoundOpportunityReceiptState.Parse(await _fs.ReadFileAsync(MortalWoundOpportunityReceiptState.StatePath), MortalWoundOpportunityReceiptState.StatePath);
        Assert.True(receipts.IsValid, JsonSerializer.Serialize(receipts.Issues));
        Assert.Single(receipts.State!.Receipts);
        Assert.False(_fs.FileExists(MortalWoundOpportunityAdapter.DecisionPath));
        Assert.False(_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        WritePhysicalInjuryDiagnostic("qte_consumer_component_atomic_receipt_retry", new
        { materialize, injected, Failure = failure.ToString(), published.InputFingerprint, Receipts = receipts.State.Receipts });
    }

    [Theory]
    [InlineData("player")]
    [InlineData("identity")]
    [InlineData("replay_player")]
    [InlineData("replay_duplicate")]
    public async Task QtePhysicalInjuryConsumer_ComponentPostAdmissionCanonicalMutationCannotDelegate(string mutation)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var component = await PrepareMortalDecisionComponentAsync(!mutation.StartsWith("replay_", StringComparison.Ordinal));
        var path = mutation != "identity" ? WoundCarrierCatalog.PlayerPath : WoundIdentityState.StatePath;
        Assert.False(_fs.FileExists(path));
        var injected = false;
        if (mutation.StartsWith("replay_", StringComparison.Ordinal))
        {
            Assert.Empty(await ValidateMortalDecisionComponentAsync(component.Engine));
            Assert.NotNull(await GetPrivateField<CanonicalStateNormalizer>(component.Engine, "_normalizer").NormalizeAcceptedMechanicsAsync(null));
            var receipts = MortalWoundOpportunityReceiptState.Parse(await _fs.ReadFileAsync(MortalWoundOpportunityReceiptState.StatePath), MortalWoundOpportunityReceiptState.StatePath);
            Assert.True(receipts.IsValid);
            Assert.Equal("none", Assert.Single(receipts.State!.Receipts).Decision);
            await _fs.WriteFileAtomicAsync(MortalWoundOpportunityAdapter.DecisionPath, component.Decision.ToJsonString());
            File.WriteAllText(_fs.ResolvePath(path), mutation == "replay_duplicate"
                ? "{\"schemaVersion\":1,\"schemaVersion\":1,\"activeWounds\":[]}"
                : "null");
            injected = true;
        }
        _consoleMutationObserver = observed =>
        {
            if (injected || observed != AcceptedMechanicsPlan.WoundCommandPath) return;
            injected = true;
            // Real command-only publication occurs after signed adapter admission.
            // Simulate unauthorized canonical mutation; this is deliberately invalid input.
            Directory.CreateDirectory(Path.GetDirectoryName(_fs.ResolvePath(path))!);
            File.WriteAllText(_fs.ResolvePath(path), "null");
        };
        IReadOnlyList<ValidationIssue> issues;
        try { issues = await ValidateMortalDecisionComponentAsync(component.Engine); }
        finally { _consoleMutationObserver = null; }
        Assert.True(injected);
        var expectedCode = mutation switch { "replay_player" => "wound_carrier_invalid_root",
            "replay_duplicate" => "wound_carrier_duplicate_property", _ => "wound_materialization_client_owned_root_mutated" };
        var actual = Assert.Single(issues.Where(issue => issue.Code == expectedCode &&
            (issue.FilePath == path || issue.FilePath.StartsWith(path + ".", StringComparison.Ordinal))));
        Assert.Equal(MortalWoundOpportunityAdapter.PrivateSourceIssueSection, actual.Section);
        Assert.Equal("Client", actual.Actor);
        Assert.False(_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        var profile = GmWorkerBridgeProfileTemplates.CreateValidationRepairCodexTemplate() with { Enabled = true, LaunchCommand = "false" };
        var source = new WorkerTurnReference { SessionId = component.Snapshot.SessionId,
            RequestId = component.Snapshot.RequestId, TurnNumber = component.Snapshot.TurnNumber };
        string generation;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync()) generation = _fs.GetOrCreateSessionGeneration(lease);
        Assert.Throws<ArgumentException>(() => GmWorkerTaskPacketBuilder.BuildValidationRepairTask(profile,
            "post_admission_private_source", source, [actual], new Dictionary<string, string>(), "2026-10-12T00:00:00Z", generation));
        var audit = new GmWorkerAuditLog(_fs);
        var delegator = new GmWorkerValidationRepairDelegator(_fs, new GmWorkerBridgePool(_fs, auditLog: audit),
            new GmWorkerApplyGate(GetPrivateField<ValidationService>(component.Engine, "_validator"), audit), audit);
        var dispatched = await delegator.TryRunAsync([profile], [actual], source, "2026-10-12T00:00:00Z", 1,
            expectedSessionGeneration: generation);
        Assert.Equal(GmWorkerValidationRepairOutcome.TaskBuildFailed, dispatched.Outcome);
        Assert.Contains("not worker proposal targets", dispatched.FallbackReason, StringComparison.Ordinal);
        Assert.Null(dispatched.Task);
        Assert.Null(dispatched.RunResult);
        Assert.False(dispatched.ReadySignalCreated);
        Assert.False(_fs.FileExists(GmWorkerValidationRepairDelegator.LatestValidationRepairTaskPath));
        Assert.Equal(mutation == "replay_duplicate" ? "{\"schemaVersion\":1,\"schemaVersion\":1,\"activeWounds\":[]}" : "null", await _fs.ReadFileAsync(path));
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync()) Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease));
        WritePhysicalInjuryDiagnostic("qte_consumer_component_downstream_private_refusal", new { mutation, injected, actual, dispatched });
    }

    [Theory]
    [InlineData("severity")]
    [InlineData("location")]
    public async Task QtePhysicalInjuryConsumer_ComponentProposalCannotExceedSealedMaximumOrLocation(string mutation)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var component = await PrepareMortalDecisionComponentAsync(true);
        var draft = component.Decision.DeepClone().AsObject();
        if (mutation == "severity") draft["proposal"]!["severity"] = "III";
        else draft["proposal"]!["classification"]!["locationProfile"]!["kind"] = "spiritual_axis";
        await _fs.WriteFileAtomicAsync(MortalWoundOpportunityAdapter.DecisionPath, draft.ToJsonString());
        var before = await CaptureMortalComponentImagesAsync();
        var issues = await ValidateMortalDecisionComponentAsync(component.Engine);
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
        var semantic = Assert.Single(issues.Where(issue => issue.FilePath.StartsWith("woundDecisions[0].proposal", StringComparison.Ordinal) &&
            issue.RepairTargetFiles.SequenceEqual(new[] { MortalWoundOpportunityAdapter.DecisionPath })));
        Assert.NotNull(semantic.Code);
        Assert.NotNull(semantic.RepairHint);
        var profile = GmWorkerBridgeProfileTemplates.CreateValidationRepairCodexTemplate();
        string generation;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync()) generation = _fs.GetOrCreateSessionGeneration(lease);
        var repair = GmWorkerTaskPacketBuilder.BuildValidationRepairTask(profile, "actual_semantic_decision_repair",
            new WorkerTurnReference { SessionId = component.Snapshot.SessionId, RequestId = component.Snapshot.RequestId,
                TurnNumber = component.Snapshot.TurnNumber }, [semantic],
            new Dictionary<string, string>
            {
                [MortalWoundOpportunityAdapter.DecisionPath] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    (await _fs.ReadFileBytesAsync(MortalWoundOpportunityAdapter.DecisionPath))!)),
                ["input/turn_request.json"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    (await _fs.ReadFileBytesAsync("input/turn_request.json"))!)),
                ["game_state/meta/soul_state.json"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    (await _fs.ReadFileBytesAsync("game_state/meta/soul_state.json"))!))
            }, "2026-10-12T00:00:00Z", generation);
        Assert.Equal(new[] { MortalWoundOpportunityAdapter.DecisionPath }, repair.AllowedProposalPaths);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync()) Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease));
        Assert.False(_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        foreach (var image in before) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        WritePhysicalInjuryDiagnostic("qte_consumer_component_proposal_bound", new { mutation, issues, Draft = draft });
    }

    [Theory]
    [InlineData("no_offer_absent")]
    [InlineData("no_offer_unsolicited")]
    [InlineData("multiple")]
    public async Task QtePhysicalInjuryConsumer_ComponentEmptyOfferAndSingleDraftBoundary(string mutation)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var component = await PrepareMortalDecisionComponentAsync(false, offered: mutation == "multiple");
        if (mutation == "no_offer_absent") _fs.DeleteFile(MortalWoundOpportunityAdapter.DecisionPath);
        if (mutation == "multiple") await _fs.WriteFileAtomicAsync(MortalWoundOpportunityAdapter.DecisionPath,
            new JsonArray(component.Decision.DeepClone(), component.Decision.DeepClone()).ToJsonString());
        var before = await CaptureMortalComponentImagesAsync();
        var issues = await ValidateMortalDecisionComponentAsync(component.Engine);
        if (mutation == "no_offer_absent") Assert.Empty(issues);
        else Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.False(_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.False(_fs.FileExists(MortalWoundOpportunityReceiptState.StatePath));
        foreach (var image in before) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        WritePhysicalInjuryDiagnostic("qte_consumer_component_empty_single_boundary", new { mutation, issues });
    }

    // Real QTE source followed by actual StagePlayerTurn and official Reader. The later
    // validation/common-normalizer calls are component controls, not a full ProcessPlayerTurn proof.
    private async Task<(GameEngine Engine, PendingTurnSnapshotReadAuthority Snapshot, JsonObject Decision)> PrepareMortalDecisionComponentAsync(bool materialize, bool offered = true)
    {
        var source = await RunActualQteStorySourceAsync(physicalDeclaration: offered, physicalMaximum: 2);
        Assert.Null(source.Failure);
        Assert.True(source.ActionObserved);
        await AssertActualQteStoryFinalizedAsync(_fs, source.Source!, source.StoryAtHandoff!, 155m);
        if (offered) await AssertActualPhysicalOccurrenceAsync(source, 2);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), settings =>
        { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; });
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(source.Source!.SessionId, PhysicalInjurySourceTurn);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, async () =>
        {
            Assert.NotNull(await InvokePrivateTaskNullableResultAsync(engine, "StagePlayerTurnAsync", "Осмотреть ушиб.", null, null));
            return true;
        });
        PendingTurnSnapshotReadAuthority snapshot;
        JsonObject decision;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            snapshot = MortalQteWoundOpportunityProjection.ReadSnapshot(_fs, lease);
            Assert.Equal(43, snapshot.TurnNumber);
            Assert.Equal(source.Source.SessionId, snapshot.SessionId);
            Assert.NotEqual(source.Source.RequestId, snapshot.RequestId);
            Assert.NotEqual(source.SnapshotToken, snapshot.SnapshotToken);
            var rows = MortalQteWoundOpportunityProjection.Project(snapshot);
            var reference = offered ? Assert.Single(rows)!["opportunityRef"]!.GetValue<string>() : "unsolicited_reference";
            if (!offered) Assert.Empty(rows);
            decision = new JsonObject { ["opportunityRef"] = reference, ["decision"] = materialize ? "materialize" : "none" };
            if (materialize) { decision["woundRef"] = "impact"; decision["proposal"] = CreateMortalComponentProposal(); }
            await _fs.WriteFileAtomicAsync(lease, "output/narrative_response.json", new JsonObject
                { ["response"] = materialize ? MortalComponentAcquisition : "Герой осматривает ушиб." }.ToJsonString());
            await _fs.WriteFileAtomicAsync(lease, MortalWoundOpportunityAdapter.DecisionPath, decision.ToJsonString());
            WritePhysicalInjuryDiagnostic("qte_consumer_component_original_staging", new
            { snapshot.SessionId, snapshot.RequestId, snapshot.SnapshotToken, snapshot.TurnNumber, snapshot.AbsentLogicalPaths, Rows = rows, Decision = decision });
        }
        return (engine, snapshot, decision);
    }

    private async Task<IReadOnlyList<ValidationIssue>> ValidateMortalDecisionComponentAsync(GameEngine engine) =>
        Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(await InvokePrivateTaskResultAsync(engine,
            "ValidateResourceAndMortalQteDecisionAsync", true));

    private async Task<Dictionary<string, byte[]?>> CaptureMortalComponentImagesAsync()
    {
        var paths = QtePhysicalInjurySource.WoundRootPaths.Concat(new[]
        {
            ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            MortalQteDecisionPublication.StatePath,
            EffectAcceptedTurnPlan.IdentityIndexPath, EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath,
            EffectCarrierCatalog.EnemiesPath, EffectCarrierCatalog.AlliesPath, EffectCarrierCatalog.AfterlifeProfilesPath,
            EffectCarrierCatalog.SpiritualConflictPath, "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
            PendingTurnSnapshotAuthority.AuthorityPath
        }).Concat(EffectAcceptedTurnInputComposer.SourceAuthorityPaths).Distinct(StringComparer.Ordinal);
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in paths) result.Add(path, await _fs.ReadFileBytesAsync(path));
        return result;
    }

    private static JsonObject CreateMortalComponentProposal()
    {
        // Public GM JSON only, copied from the existing creation grammar. No fixture authority.
        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
        definition["definitionKey"] = "wound_bleeding_root";
        definition["allowedRealms"] = new JsonArray("mortal_world");
        definition["allowedTargetKinds"] = new JsonArray("player");
        definition["parameterBounds"] = new JsonObject();
        definition["links"] = new JsonArray();
        definition["stacking"]!["stackKey"] = "stack_wound_bleeding_root";
        definition["stacking"]!["policy"] = "independent";
        definition["stacking"]!["maxStacks"] = 1;
        definition["stacking"]!["atMaximum"] = "no_change";
        definition["stacking"]!["refreshMode"] = null;
        definition["stacking"]!["mergeRule"] = null;
        return new JsonObject
        {
            ["classification"] = new JsonObject
            {
                ["woundType"] = "superficial_laceration",
                ["locationProfile"] = new JsonObject { ["kind"] = "other", ["readableLocus"] = "поверхностный порез на боку" }
            },
            ["display"] = new JsonObject
            {
                ["name"] = "Поверхностный порез", ["description"] = "Край камня оставил неглубокий порез.",
                ["visibleSymptoms"] = new JsonArray("полоска крови", "жжение"),
                ["prognosis"] = "Порез требует очистки и стабилизации.", ["visibility"] = "known_to_player",
                ["acquisitionNarration"] = MortalComponentAcquisition
            },
            ["severity"] = "I", ["complications"] = new JsonArray(),
            ["consequenceDefinitions"] = new JsonArray(new JsonObject
            {
                ["definitionRef"] = "bleeding_root", ["definition"] = definition,
                ["root"] = new JsonObject
                {
                    ["ownership"] = new JsonObject { ["kind"] = "base_wound", ["complicationRef"] = null },
                    ["slots"] = new JsonArray(new JsonObject
                    { ["profileKey"] = "periodic_damage", ["readableSummary"] = "Свежий порез продолжает кровоточить." })
                }
            }),
            ["treatment"] = new JsonObject
            {
                ["diagnosisPaths"] = new JsonArray(), ["routes"] = new JsonArray(),
                ["knownRouteIds"] = new JsonArray(), ["completedRouteIds"] = new JsonArray()
            },
            ["recovery"] = new JsonObject
            {
                ["mode"] = "requires_stabilization", ["clockKind"] = "mortal_world_time", ["cadence"] = 86400,
                ["currentStepProgress"] = 0, ["currentStepThreshold"] = 3, ["lastTickKey"] = null,
                ["blockers"] = new JsonArray("not_stabilized"), ["carryOverflow"] = true,
                ["deteriorationPolicy"] = new JsonObject
                {
                    ["policyRef"] = "untreated_cut", ["unmetConditions"] = new JsonArray("not_stabilized"),
                    ["graceMinutes"] = 30L, ["cadenceMinutes"] = 10L,
                    ["result"] = new JsonObject { ["kind"] = "increase_severity" }
                }
            }
        };
    }
}
