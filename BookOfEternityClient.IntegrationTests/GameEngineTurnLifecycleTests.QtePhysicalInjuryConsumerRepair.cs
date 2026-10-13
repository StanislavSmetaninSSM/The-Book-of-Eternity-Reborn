using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("severity", false)]
    [InlineData("severity", true)]
    [InlineData("location", false)]
    [InlineData("location", true)]
    [InlineData("narration", false)]
    [InlineData("narration", true)]
    [InlineData("resource", false)]
    [InlineData("resource", true)]
    public async Task QtePhysicalInjuryConsumer_ActualMainGmRepairPreservesBoundedProposal(string rejection, bool siblingDrift)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var source = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2);
        Assert.Null(source.Failure);
        await AssertActualQteStoryFinalizedAsync(_fs, source.Source!, source.StoryAtHandoff!, 155m);
        var occurrence = await AssertActualPhysicalOccurrenceAsync(source, 2);
        var input = new QueuedConsoleInputSource([]);
        PendingTurnSnapshotReadAuthority? next = null;
        JsonObject? rejectedDraft = null, correctedDraft = null;
        byte[]? originalRequest = null, originalManifest = null, originalAuthority = null;
        string? rejectedScene = null, repairPacket = null, actualIssuedCommand = null, initialReady = null;
        var repairPublications = new List<object>();
        var repairCount = 0;
        var accepted = false;
        Exception? responderFailure = null;
        var engine = CreateGameEngine(input, settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            finalizationHooks: new GameEngineSessionFinalizationHooks { AtCheckpointAsync = AuthorInitial });
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(source.Source!.SessionId, PhysicalInjurySourceTurn);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        _consolePublicationObserver = RespondToMainRepair;
        Exception? failure;
        try { failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Осмотреть и обработать порез.", null)); }
        finally { _consolePublicationObserver = null; }
        var receiptJson = await _fs.ReadFileAsync(MortalWoundOpportunityReceiptState.StatePath);
        WritePhysicalInjuryDiagnostic("qte_consumer_actual_main_repair", new
        {
            rejection, siblingDrift, repairCount, accepted, EngineFailure = failure?.ToString(), ResponderFailure = responderFailure?.ToString(),
            Source = new { source.Source.SessionId, source.Source.RequestId, source.SnapshotToken, source.Source.TurnNumber },
            Next = next is null ? null : new { next.SessionId, next.RequestId, next.SnapshotToken, next.TurnNumber },
            RejectedDraft = rejectedDraft, CorrectedDraft = correctedDraft, rejectedScene, repairPacket, actualIssuedCommand, InitialReady = initialReady,
            RepairPublications = repairPublications,
            ActualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber, Receipt = receiptJson,
            DraftAfter = await _fs.ReadFileAsync(MortalWoundOpportunityAdapter.DecisionPath),
            CommandAfter = await _fs.ReadFileAsync(AcceptedMechanicsPlan.WoundCommandPath),
            MarkerAfter = await _fs.ReadFileAsync(MortalQteDecisionPublication.StatePath)
        });
        Assert.Null(responderFailure);
        Assert.Null(failure);
        Assert.NotNull(next);
        Assert.Equal(1, repairCount);
        Assert.Equal(rejection == "resource", actualIssuedCommand is not null);
        if (siblingDrift)
        {
            Assert.False(accepted);
            Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            Assert.Null(receiptJson);
            Assert.False(_fs.FileExists(WoundIdentityState.StatePath));
            Assert.False(_fs.FileExists(MortalQteDecisionPublication.StatePath));
        }
        else
        {
            Assert.True(accepted);
            Assert.Equal(43, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            var receipts = MortalWoundOpportunityReceiptState.Parse(receiptJson, MortalWoundOpportunityReceiptState.StatePath);
            Assert.True(receipts.IsValid, JsonSerializer.Serialize(receipts.Issues));
            var receipt = Assert.Single(receipts.State!.Receipts);
            Assert.Equal("materialize", receipt.Decision);
            Assert.Equal(next!.RequestId, receipt.RequestId);
            Assert.Equal(next.SnapshotToken, receipt.SnapshotToken);
            Assert.Equal(occurrence.OccurrenceId, receipt.OpportunityId);
            Assert.Equal(source.SnapshotToken, receipt.SourceSnapshotToken);
            Assert.NotNull(receipt.WoundId);
            Assert.NotNull(receipt.TransitionId);
            var history = WoundHistoryState.Parse(await _fs.ReadFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
            Assert.True(history.IsValid);
            Assert.Single(history.State!.Transitions);
            Assert.True(_fs.FileExists(MortalQteDecisionPublication.StatePath));
        }
        Assert.False(_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_ready.json"));

        async Task AuthorInitial(SessionFinalizationCheckpoint checkpoint)
        {
            if (checkpoint == SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization) { accepted = true; return; }
            if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            next = MortalQteWoundOpportunityProjection.ReadSnapshot(_fs, lease);
            Assert.Equal(43, next.TurnNumber);
            Assert.Equal(source.Source.SessionId, next.SessionId);
            var row = Assert.Single(MortalQteWoundOpportunityProjection.Project(next))!.AsObject();
            Assert.Equal(occurrence.OpportunityRef, row["opportunityRef"]!.GetValue<string>());
            Assert.Equal("II", row["maximumSeverity"]!.GetValue<string>());
            var proposal = CreateMortalComponentProposal();
            if (rejection == "severity") proposal["severity"] = "III";
            if (rejection == "location") proposal["classification"]!["locationProfile"]!["kind"] = "systemic";
            if (rejection == "resource") proposal["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["resource"] = "unbound_cut_resource";
            rejectedDraft = new JsonObject { ["opportunityRef"] = occurrence.OpportunityRef, ["decision"] = "materialize",
                ["woundRef"] = "local_repair_cut", ["proposal"] = proposal };
            rejectedScene = rejection == "narration" ? "Герой внимательно осматривает бок." : MortalComponentAcquisition;
            originalRequest = await _fs.ReadFileBytesAsync(lease, "input/turn_request.json");
            originalManifest = await _fs.ReadFileBytesAsync(lease, "game_state/control/pending_turn_snapshot.json");
            originalAuthority = await _fs.ReadFileBytesAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath);
            await WriteOutputs(lease, rejectedDraft, rejectedScene);
            await _fs.WriteFileAtomicAsync(lease, "ready/turn_complete.json", JsonSerializer.Serialize(new
            {
                sessionId = next.SessionId, requestId = next.RequestId, turnNumber = next.TurnNumber,
                status = "success", timestamp = DateTime.UtcNow.ToString("O"),
                filesModified = new[] { MortalWoundOpportunityAdapter.DecisionPath, "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json" }
            }));
            initialReady = await _fs.ReadFileAsync(lease, "ready/turn_complete.json");
        }

        void RespondToMainRepair(TrustedLocalPublicationPhase phase, int index)
        {
            if (phase != TrustedLocalPublicationPhase.Committed || next is null || responderFailure is not null) return;
            try
            {
                var path = Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
                using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(path));
                var members = journal.RootElement.GetProperty("Members").EnumerateArray().Select(row => row.GetProperty("Path").GetString()).ToArray();
                if (repairCount == 0 && members.Contains(_fs.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath)) && File.Exists(_fs.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath)))
                    actualIssuedCommand ??= File.ReadAllText(_fs.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath));
                var requestPath = _fs.ResolvePath("game_state/control/validation_repair_request.json");
                var requestMembers = journal.RootElement.GetProperty("Members").EnumerateArray()
                    .Where(row => row.GetProperty("Path").GetString() == requestPath).ToArray();
                if (requestMembers.Length == 0) return;
                var member = Assert.Single(requestMembers);
                var format = journal.RootElement.GetProperty("Format").GetInt32();
                Assert.Equal(1, format);
                var after = member.GetProperty("After");
                var afterExists = after.GetProperty("Exists").GetBoolean();
                var declaredBytes = after.GetProperty("Bytes").ValueKind == JsonValueKind.Null
                    ? null : after.GetProperty("Bytes").GetBytesFromBase64();
                var afterSha256 = after.GetProperty("Sha256").GetString();
                var committed = journal.RootElement.GetProperty("Committed").GetBoolean();
                repairPublications.Add(new
                {
                    Format = format, TransactionId = journal.RootElement.GetProperty("TransactionId").GetString(), Committed = committed,
                    MemberPath = member.GetProperty("Path").GetString(), DeclaredBytesLength = declaredBytes?.LongLength,
                    After = new { Exists = afterExists, Sha256 = afterSha256 }
                });
                Assert.True(committed);
                if (!afterExists)
                {
                    Assert.Null(declaredBytes);
                    Assert.Null(afterSha256);
                    return; // A declared deletion is not a newly published repair request.
                }
                Assert.NotNull(declaredBytes);
                var publishedBytes = File.ReadAllBytes(requestPath);
                Assert.Equal(declaredBytes!.LongLength, publishedBytes.LongLength);
                Assert.Equal(declaredBytes, publishedBytes);
                Assert.Equal(afterSha256, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(publishedBytes)));
                repairPacket = Encoding.UTF8.GetString(publishedBytes);
                using var packet = JsonDocument.Parse(repairPacket.TrimStart('\uFEFF'));
                var repair = packet.RootElement;
                Assert.Equal(next.SessionId, repair.GetProperty("sessionId").GetString());
                Assert.Equal(next.RequestId, repair.GetProperty("requestId").GetString());
                Assert.Equal(43, repair.GetProperty("turnNumber").GetInt32());
                Assert.True(repair.GetProperty("fullTurnResubmissionRequired").GetBoolean());
                Assert.False(repair.GetProperty("metadataDiagnosticOnly").GetBoolean());
                var woundPacket = Assert.Single(repair.GetProperty("harnessRepairPackets").EnumerateArray()
                    .Where(row => row.TryGetProperty("candidateKind", out _)));
                Assert.Equal(next.SnapshotToken, woundPacket.GetProperty("snapshotToken").GetString());
                Assert.Equal(rejection == "narration" ? "narrate_acquisition" : "construct_wound", woundPacket.GetProperty("candidateKind").GetString());
                Assert.Equal(originalRequest, File.ReadAllBytes(_fs.ResolvePath("input/turn_request.json")));
                Assert.Equal(originalManifest, File.ReadAllBytes(_fs.ResolvePath("game_state/control/pending_turn_snapshot.json")));
                Assert.Equal(originalAuthority, File.ReadAllBytes(_fs.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));
                Assert.False(File.Exists(_fs.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath)));
                Assert.Equal(0, repairCount++); // Unexpected extra repair is a prerequisite failure, never a timeout.
                correctedDraft = rejectedDraft!.DeepClone().AsObject();
                if (rejection == "severity") correctedDraft["proposal"]!["severity"] = "I";
                if (rejection == "location") correctedDraft["proposal"]!["classification"]!["locationProfile"]!["kind"] = "other";
                if (rejection == "resource") correctedDraft["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["resource"] =
                    CreateMortalComponentProposal()["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["resource"]!.DeepClone();
                if (siblingDrift) correctedDraft["proposal"]!["display"]!["description"] = "Несогласованная смена сохранённого описания.";
                // This existing publication observer acts as the offline MAIN-GM file author.
                // Only public outputs/ready are authored; no leases, commands or canonical roots.
                WritePhysicalOutput(MortalWoundOpportunityAdapter.DecisionPath, correctedDraft.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                var timestamp = DateTime.UtcNow.ToString("O");
                WritePhysicalOutput("output/narrative_response.json", JsonSerializer.Serialize(new { response = MortalComponentAcquisition, timestamp }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                WritePhysicalOutput("output/interface_updates.json", JsonSerializer.Serialize(new { dialogueOptions = Array.Empty<object>(), timestamp }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                WritePhysicalOutput("output/debug_logs.json", Debug(timestamp));
                WritePhysicalOutput("game_state/control/validation_repair_ready.json", JsonSerializer.Serialize(new
                { sessionId = next.SessionId, requestId = next.RequestId, turnNumber = 43, updatedAtUtc = timestamp, note = "Полный исходный ход представлен повторно с ограниченной правкой." }));
            }
            catch (Exception error) { responderFailure = error; input.Enqueue(Key(ConsoleKey.Escape)); }
        }

        async Task WriteOutputs(FileSystemManager.CanonicalWriteLease lease, JsonObject decision, string scene)
        {
            var timestamp = DateTime.UtcNow.ToString("O");
            await _fs.WriteFileAtomicAsync(lease, MortalWoundOpportunityAdapter.DecisionPath, decision.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await _fs.WriteFileAtomicAsync(lease, "output/narrative_response.json", JsonSerializer.Serialize(new { response = scene, timestamp }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await _fs.WriteFileAtomicAsync(lease, "output/interface_updates.json", JsonSerializer.Serialize(new { dialogueOptions = Array.Empty<object>(), timestamp }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await _fs.WriteFileAtomicAsync(lease, "output/debug_logs.json", Debug(timestamp));
        }
        string Debug(string timestamp) => JsonSerializer.Serialize(new { timestamp, gm_thoughts_markdown =
            "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: нет\n- Why relevant: Осмотр пореза без участия NPC.\n- Actors outside scope: нет\n- Why outside scope: В сцене нет других акторов.\n\n## Reasoning\n- Full original response is resubmitted; only the bounded rejected field or acquisition scene is corrected." }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        void WritePhysicalOutput(string path, string json) => File.WriteAllText(_fs.ResolvePath(path), json, new UTF8Encoding(false));
    }
}
