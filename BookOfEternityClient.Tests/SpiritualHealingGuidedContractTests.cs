using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualHealingGuidedContractTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-healing-guided-contract-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private static readonly SpiritualSelfTreatmentSelection Selection = new("wound_selected", "sha256:" + new string('a', 64));

    public SpiritualHealingGuidedContractTests()
    {
        _files = new(_root, NullLogger<FileSystemManager>.Instance);
        _files.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task TypedSelectionAndOriginalBytesRemainBoundAcrossEveryRetainedPhase()
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var queued = PendingPlayerActionService.PrepareQueued(_files, lease, WoundApplicationService.ActionText,
            "browser-wound-action", DateTime.UtcNow.ToString("o"), Selection);
        var generation = _files.GetOrCreateSessionGeneration(lease);
        var state = PendingPlayerActionService.Parse(queued.ToJsonString(), generation);
        Assert.Equal(Selection, state.Binding.SpiritualSelfTreatment);
        state = PendingPlayerActionService.Parse(PendingPlayerActionService.CreatePhase(state, "preparing"), generation);
        var blob = Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"));
        var staged = new PendingPlayerActionService.Staged(state.Binding, "{}", "{}", "{}", "", "{}", blob);
        foreach (var phase in new[] { "staged", "terminalProcessing", "accepted", "settled" })
        {
            var json = PendingPlayerActionService.CreatePhase(state, phase, PendingPlayerActionService.StagingProof(staged));
            state = PendingPlayerActionService.Parse(json, generation);
            var retained = PendingPlayerActionService.ReadStaged(state);
            Assert.Equal(Selection, retained.Binding.SpiritualSelfTreatment);
            Assert.Equal(blob, retained.SpiritualWoundRequestBytesBase64);
            var changed = JsonNode.Parse(json)!.AsObject();
            changed["spiritualSelfTreatment"]!["woundId"] = "wound_replaced";
            Assert.Throws<InvalidDataException>(() => PendingPlayerActionService.Parse(changed.ToJsonString(), generation));
        }
        // Parse retains DATA only: the fake tuple above cannot authenticate a detached original.
        Assert.Throws<InvalidDataException>(() => GameEngine.ValidateDetachedBrowserBinding(PendingPlayerActionService.ReadStaged(state)));
    }

    [Fact]
    public async Task ProseAndPreparingCannotMasqueradeAsStagedHealingEvidence()
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var generation = _files.GetOrCreateSessionGeneration(lease);
        var plain = PendingPlayerActionService.PrepareQueued(_files, lease, "Осмотреть берег.", "browser-composer", DateTime.UtcNow.ToString("o"));
        Assert.False(plain.ContainsKey("spiritualSelfTreatment"));
        var prose = PendingPlayerActionService.Parse(plain.ToJsonString(), generation);
        var blob = new JsonObject { ["spiritualWoundRequestBytesBase64"] = "e30=" };
        Assert.Throws<InvalidDataException>(() => PendingPlayerActionService.CreatePhase(prose, "staged", blob));
        var typed = PendingPlayerActionService.Parse(PendingPlayerActionService.PrepareQueued(_files, lease,
            WoundApplicationService.ActionText, "browser-wound-action", DateTime.UtcNow.ToString("o"), Selection).ToJsonString(), generation);
        Assert.Throws<InvalidDataException>(() => PendingPlayerActionService.Parse(
            PendingPlayerActionService.CreatePhase(typed, "preparing", blob), generation));
        Assert.Throws<InvalidDataException>(() => PendingPlayerActionService.Parse(
            PendingPlayerActionService.CreatePhase(typed, "staged"), generation));
        Assert.Throws<InvalidDataException>(() => PendingPlayerActionService.PrepareQueued(_files, lease,
            WoundApplicationService.ActionText, "browser-wound-action", DateTime.UtcNow.ToString("o")));
    }

    [Fact]
    public async Task TypedTransportPreservesGraphsAndOrdinaryOutputKeepsFieldAbsent()
    {
        const string graphJson = "[{\"stageRef\":\"requested_graph\",\"consequenceDefinitions\":[]}]";
        var response = JsonSerializer.Deserialize<GameResponse>("{\"spiritualHealingGraphs\":" + graphJson + "}")!;
        var distributor = new StateDistributor(_files, NullLogger<StateDistributor>.Instance);
        await distributor.DistributeAsync(response);
        var interfaceJson = await _files.ReadFileAsync("output/interface_updates.json");
        Assert.NotNull(interfaceJson);
        var root = JsonNode.Parse(interfaceJson)!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(graphJson), root["spiritualHealingGraphs"]));
        Assert.Contains("spiritualHealingGraphs", FileMapping.OutputOnlyResponseFields);
        Assert.False(FileMapping.FieldToFile.ContainsKey("spiritualHealingGraphs"));
        var refreshed = GameResponseRefreshMerger.Merge(response, new GameResponse());
        Assert.Single(refreshed.SpiritualHealingGraphs!);
        Assert.Empty(GameResponseRefreshMerger.Merge(response, new GameResponse { SpiritualHealingGraphs = [] }).SpiritualHealingGraphs!);
        await distributor.DistributeAsync(new GameResponse { DialogueOptions = [] });
        Assert.False(JsonNode.Parse((await _files.ReadFileAsync("output/interface_updates.json"))!)!.AsObject().ContainsKey("spiritualHealingGraphs"));
    }

    [Fact]
    public async Task UnboundGraphFieldCannotGainInterfacePermissionOrMutateItsOutput()
    {
        const string path = "output/interface_updates.json";
        var json = new JsonObject { ["spiritualHealingGraphs"] = new JsonArray(), ["timestamp"] = DateTime.UtcNow.ToString("o") }.ToJsonString();
        await _files.WriteFileAtomicAsync(path, json);
        var before = await _files.ReadFileBytesAsync(path);
        var issues = await new ValidationService(_files, NullLogger<ValidationService>.Instance).ValidateAcceptedTurnInterfacePayloadAsync();
        Assert.Contains(issues, i => i.Code == "spiritual_healing_interface_unbound");
        Assert.Contains(issues, i => i.Code == "interface_updates_unknown_field");
        Assert.Equal(before, await _files.ReadFileBytesAsync(path));
    }

    [Fact]
    public void CombatHealingHasAClosedWorkedExampleAndNoSafeAttemptOrCycle()
    {
        const string marker = "spiritual_combat_healing_v1";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "TaskGuides/CLI_Step_Main.txt", "BookOfEternityClient/game_master_daemon.ps1" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, c => c.ContractId == marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example));
        var start = example.IndexOf(marker + " — worked", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var fence = example.IndexOf("```json", start, StringComparison.Ordinal) + "```json".Length;
        var end = example.IndexOf("```", fence, StringComparison.Ordinal);
        var root = JsonNode.Parse(example[fence..end])!.AsObject();
        var selection = SpiritualSelfTreatmentSelection.Parse(root);
        Assert.NotNull(selection.Combat); Assert.Null(selection.Aid);
        Assert.Equal(WoundApplicationService.CombatActionText, WoundApplicationService.ActionFor(selection));
        Assert.Equal(selection, SpiritualSelfTreatmentSelection.Parse(selection.ToJson()));
        root["roll"] = 20;
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(root));
        Assert.True(AfterlifeContractRegistry.Find(AcceptedMechanicsPlan.WoundCommandPath)!.IsPrivateMechanicsSurface);
    }

    [Fact]
    public void GuidedSelfContractIsSynchronizedWithGmWorkedExampleAndPrivateRequestRegistry()
    {
        const string marker = "spiritual_self_treatment_v2";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "TaskGuides/CLI_Step_Main.txt", "BookOfEternityClient/game_master_daemon.ps1" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, c => c.ContractId == marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example));
        Assert.True(AfterlifeContractRegistry.Find(AcceptedMechanicsPlan.WoundCommandPath)!.IsPrivateMechanicsSurface);
    }

    [Fact]
    public void NaturalModeHasOneClosedResultAndSynchronizedOrdinaryGmExample()
    {
        const string marker = "spiritual_natural_recovery_v2";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "TaskGuides/CLI_Step_Main.txt", "BookOfEternityClient/game_master_daemon.ps1" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, c => c.ContractId == marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example));
    }

    [Fact]
    public void GuardianParticipationProposalHasAClosedWorkedExampleAndSynchronizedGmGuidance()
    {
        const string marker = "spiritual_treatment_participation_v1";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "TaskGuides/CLI_Step_Main.txt", "BookOfEternityClient/game_master_daemon.ps1" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, c => c.ContractId == marker);
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, text));
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        var fence = text.IndexOf("```json", start, StringComparison.Ordinal) + "```json".Length;
        var end = text.IndexOf("```", fence, StringComparison.Ordinal);
        var proposal = JsonNode.Parse(text[fence..end])!.AsObject();
        var previous = new JsonObject { ["profiles"] = new JsonArray(new JsonObject
        { ["actorType"] = "guardian", ["actorId"] = "guardian_example", ["realm"] = "Chaos Sea" }) };
        var response = previous.DeepClone().AsObject();
        response["profiles"]![0]![SpiritualTreatmentParticipation.ChangeProperty] = proposal;
        var parsed = Assert.Single(SpiritualTreatmentParticipation.ReadChanges(response, previous));
        Assert.Equal("grant", parsed.Proposal["operation"]!.GetValue<string>());
        Assert.Equal("player_soul", parsed.Proposal["healerId"]!.GetValue<string>());
    }

    [Fact]
    public void GuardianAidUsesClosedSelectionAndTheDocumentedTwoTurnGmContract()
    {
        const string marker = "spiritual_guardian_aid_v3";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "TaskGuides/CLI_Step_Main.txt", "BookOfEternityClient/game_master_daemon.ps1" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, c => c.ContractId == marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md" })
            Assert.Contains("complete profile projection", File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        Assert.Contains("complete profile projection", example);
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example));
        var start = example.IndexOf(marker, StringComparison.Ordinal);
        var fence = example.IndexOf("```json", start, StringComparison.Ordinal) + "```json".Length;
        var end = example.IndexOf("```", fence, StringComparison.Ordinal);
        var root = JsonNode.Parse(example[fence..end])!.AsObject();
        var selection = SpiritualSelfTreatmentSelection.Parse(root);
        Assert.Equal("player_soul", selection.Aid!.HealerId);
        Assert.Equal("guardian_example", selection.Aid.TargetId);
        Assert.Equal(WoundApplicationService.AidActionText, WoundApplicationService.ActionFor(selection));
        Assert.Equal(WoundApplicationService.ActionText, WoundApplicationService.ActionFor(Selection));
        Assert.Equal(selection, SpiritualSelfTreatmentSelection.Parse(selection.ToJson()));
        root["schemaVersion"] = 2;
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(root));
        root["schemaVersion"] = 3; root["healerId"] = "guardian_example";
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(root));
        root["healerId"] = "player_soul"; root["permission"] = true;
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(root));
    }

    [Fact]
    public void GuardianHelpHasAClosedOfferExampleAndAnExplicitPlayerConfirmedSelection()
    {
        const string marker = "spiritual_guardian_help_v4";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "TaskGuides/CLI_Step_Main.txt", "BookOfEternityClient/game_master_daemon.ps1" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, c => c.ContractId == marker);
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, text));
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        var fence = text.IndexOf("```json", start, StringComparison.Ordinal) + "```json".Length;
        var end = text.IndexOf("```", fence, StringComparison.Ordinal);
        var proposal = JsonNode.Parse(text[fence..end])!.AsObject();
        var previous = new JsonObject { ["profiles"] = new JsonArray(new JsonObject
        { ["actorType"] = "guardian", ["actorId"] = "guardian_example", ["realm"] = "Chaos Sea" }) };
        var response = previous.DeepClone().AsObject();
        response["profiles"]![0]![SpiritualTreatmentParticipation.ChangeProperty] = proposal;
        var parsed = Assert.Single(SpiritualTreatmentParticipation.ReadChanges(response, previous));
        Assert.Equal("offer", parsed.Proposal["operation"]!.GetValue<string>());
        Assert.Equal("player_soul", parsed.Proposal["targetId"]!.GetValue<string>());
        Assert.False(parsed.Proposal.ContainsKey("playerConsent"));
        var candidate = Selection with { Aid = new("guardian_example", "player_soul", "sha256:" + new string('b', 64)) { ReceiveAid = true } };
        Assert.Equal(candidate, SpiritualSelfTreatmentSelection.Parse(candidate.ToJson()));
        Assert.False(candidate.Aid!.PlayerConfirmed); // These are menu comparison DATA, not an issued event.
        Assert.Equal(WoundApplicationService.ReceiveAidActionText, WoundApplicationService.ActionFor(candidate));
        var unknown = candidate.ToJson(); unknown["playerConsent"] = true;
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(unknown));
        var crossVersion = candidate.ToJson(); crossVersion["schemaVersion"] = 3;
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(crossVersion));
        // A detached marker can be round-tripped as DATA; only actual ConfirmAsync
        // plus one-shot/queued Engine binding issues the real player action.
        var confirmed = candidate with { Aid = candidate.Aid with { PlayerConfirmed = true } };
        Assert.Equal(confirmed, SpiritualSelfTreatmentSelection.Parse(confirmed.ToJson()));
        Assert.NotEqual(candidate, confirmed);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
