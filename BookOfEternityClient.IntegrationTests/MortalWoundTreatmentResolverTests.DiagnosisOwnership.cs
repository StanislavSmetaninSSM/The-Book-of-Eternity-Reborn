using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public async Task DiagnosisPurposeRetainsActualAcceptedClaimsAndRejectsSameIdTreatmentCollision()
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync();
        var owner = fixture.Authority;
        var before = owner.CurrentWound;
        var history = WoundHistoryState.Parse(await fixture.Files.ReadFileAsync(fixture.Lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid);
        var unchanged = fixture.CaptureJson();
        var ordinary = MortalWoundTreatmentPlanner.PrepareProcedureRequest(owner, history, before,
            "diagnosis_baseline_control", "inspect_explicit_method", fixture.EventRef);
        Assert.True(ordinary.IsValid, JsonSerializer.Serialize(ordinary.Issues));
        Assert.True(ordinary.Request!.RollbackNewProvisionalClaims(owner));
        var prepared = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, before,
            "diagnosis_same_operation", "inspect_explicit_method", fixture.EventRef);
        Assert.True(prepared.IsValid, JsonSerializer.Serialize(prepared.Issues));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(prepared.Request);
        Assert.Equal("diagnosis", request.Mode); Assert.Equal("diagnosis", request.Coordinates.Purpose);
        Assert.Equal("inspect_explicit_method", request.Coordinates.DiagnosisPathId); Assert.Empty(request.Coordinates.RouteId);
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority);
        Assert.Equal(new[] { 0 }, check.SourceIndices);
        var retry = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, before,
            "diagnosis_same_operation", "inspect_explicit_method", fixture.EventRef);
        Assert.True(retry.IsValid, JsonSerializer.Serialize(retry.Issues));
        Assert.Equal(request.RequestFingerprint, retry.Request!.RequestFingerprint);
        Assert.Equal(check.SourceIndices, Assert.IsType<MortalWoundProcedureCheckAuthority>(retry.Request.ModeAuthority).SourceIndices);
        var conflicting = MortalWoundTreatmentPlanner.PrepareProcedureRequest(owner, history, before,
            "diagnosis_same_operation", "inspect_explicit_method", fixture.EventRef);
        Assert.False(conflicting.IsValid); Assert.Null(conflicting.Request);
        var competitor = MortalWoundTreatmentPlanner.PrepareProcedureRequest(owner, history, before,
            "diagnosis_competing_operation", "inspect_explicit_method", fixture.EventRef);
        Assert.True(competitor.IsValid, JsonSerializer.Serialize(competitor.Issues));
        var competing = Assert.IsType<MortalWoundProcedureCheckAuthority>(competitor.Request!.ModeAuthority);
        Assert.Empty(check.SourceIndices.Intersect(competing.SourceIndices));
        Assert.Equal(new[] { 1 }, competing.SourceIndices);
        fixture.AssertJson(unchanged);
        Assert.DoesNotContain(history.State!.Transitions, transition => transition.Kind == "diagnose");
    }

    private sealed class DiagnosisOwnershipFixture : IAsyncDisposable
    {
        private DiagnosisOwnershipFixture(string root, FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalWoundTreatmentAcceptedStateAuthority authority, string eventRef)
        { Root = root; Files = files; Lease = lease; Authority = authority; EventRef = eventRef; }
        private string Root { get; }
        internal FileSystemManager Files { get; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; }
        internal MortalWoundTreatmentAcceptedStateAuthority Authority { get; }
        internal string EventRef { get; }

        internal static async Task<DiagnosisOwnershipFixture> CreateAsync()
        {
            // Initial canonical scenario DATA only. The real live preparation and
            // exporter below issue ownership; these JSON values are not receipts.
            var scenario = CreateScenario("procedure_normal_uses_lowest_free_die", "procedure");
            var method = WoundContractTestData.CreateMortalDiagnosisPath(true, 30);
            var route = Assert.IsType<JsonObject>(Assert.Single(scenario.Before["treatment"]!["routes"]!.AsArray())).DeepClone().AsObject();
            route["routeId"] = "inspect_explicit_method";
            scenario.Before["treatment"]!["routes"]!.AsArray().Add(route);
            scenario.Before["treatment"]!["knownRouteIds"]!.AsArray().Add("inspect_explicit_method");
            scenario.Before["treatment"]!["diagnosisPaths"] = new JsonArray(method);
            scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before), SeedCanonicalWoundEffects = true };
            var root = Path.Combine(Path.GetTempPath(), "boe-mortal-diagnosis-owner-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            files.EnsureDirectoryStructure();
            FileSystemManager.CanonicalWriteLease? lease = null;
            try
            {
                _ = AcceptedStateFixture.SeedCanonicalComponentData(files, scenario, "player", "player_current", WoundCarrierCatalog.PlayerPath);
                await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
                {
                    SessionId = scenario.AcceptedState["sessionId"]!.GetValue<string>(),
                    RequestId = scenario.AcceptedState["requestId"]!.GetValue<string>(),
                    TurnNumber = scenario.AcceptedState["turn"]!.GetValue<int>(),
                    CurrentRealm = "Mortal World", PlayerAction = "Осмотреть рану по отдельно подтверждённому способу.",
                    PreGeneratedDices1d20 = scenario.AcceptedState["acceptedDice"]!.AsArray().Select(value => value!.GetValue<int>()).ToArray()
                });
                lease = await files.AcquireCanonicalWriteLeaseAsync();
                var context = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
                {
                    ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = "player", ["targetId"] = "player_current",
                    ["providerKind"] = "npc", ["providerId"] = "field_medic_01", ["currentLocationId"] = "loc_field_clinic_001"
                }.ToJsonString(), "diagnosis.context");
                Assert.True(context.IsValid, JsonSerializer.Serialize(context.Issues));
                var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(files, lease,
                    Assert.IsType<MortalWoundTreatmentAuthority.Context>(context.Context), scenario.Before["woundId"]!.GetValue<string>());
                Assert.True(exported.IsValid, JsonSerializer.Serialize(exported.Issues));
                return new(root, files, lease, Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(exported.Authority), scenario.EventRef);
            }
            catch
            {
                if (lease is not null) await lease.DisposeAsync();
                Directory.Delete(root, true); throw;
            }
        }

        internal Dictionary<string, byte[]> CaptureJson() => Directory.EnumerateFiles(Root, "*.json", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(Root, path).StartsWith(".boe_runtime", StringComparison.Ordinal))
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        internal void AssertJson(Dictionary<string, byte[]> before)
        {
            var after = CaptureJson(); Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
            foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
        }
        public async ValueTask DisposeAsync()
        {
            await Lease.DisposeAsync();
            Directory.Delete(Root, true);
        }
    }
}
