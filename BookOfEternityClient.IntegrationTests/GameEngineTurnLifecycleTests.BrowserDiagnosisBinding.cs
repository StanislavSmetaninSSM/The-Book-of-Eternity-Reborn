using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("diagnosis")]
    [InlineData("selection")]
    [InlineData("control")]
    [InlineData("bytes")]
    [InlineData("active")]
    [InlineData("combat")]
    public async Task DetachedBrowserOriginalBindsExactDiagnosisPurposeAndPreservesHealingSiblings(string mode)
    {
        await using var fixture = await HealingFixture.CreateAsync(2, 3, 20, stageRequest: false,
            beforeResourceBootstrap: SeedBrowserDiagnosisPathsAsync);
        var files = fixture.Files;
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(files, async () =>
        {
            var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            var baseSelection = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound));
            var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
            var menu = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
            var diagnosisChoice = Assert.Single(menu.Choices, choice => choice.Available && choice.Selection.Diagnosis?.DiagnosisPathId == "native_diagnosis");
            var alternativeChoice = Assert.Single(menu.Choices, choice => choice.Available && choice.Selection.Diagnosis?.DiagnosisPathId == "native_diagnosis_alternative");
            var selection = mode == "active" ? Assert.Single(menu.Choices, choice => choice.Available && choice.Selection.Diagnosis is null &&
                choice.Selection.Aid is null && choice.Selection.Service is null && choice.Selection.Combat is null).Selection : mode == "combat" ? baseSelection with
            {
                Combat = new("browser_binding_conflict", WoundAcceptedTurnFingerprintWriter.Compute(new[] { "browser-binding-conflict" }), 1)
            } : diagnosisChoice.Selection;
            Assert.Equal(selection, SpiritualSelfTreatmentSelection.Parse(selection.ToJson()));
            if (selection.Diagnosis is not null) Assert.Equal(wound.Owner, selection.Diagnosis.Target);
            var control = await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance)
                .BuildControlForNextTurnAsync();
            PendingPlayerActionService.Binding binding;
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                var queued = PendingPlayerActionService.PrepareQueued(files, lease,
                    WoundApplicationService.ActionFor(selection), "browser-wound-action", DateTime.UtcNow.ToString("O"), selection);
                var json = queued.ToJsonString();
                await files.WriteFileAtomicAsync(lease, PendingPlayerActionService.PendingPath, json);
                binding = PendingPlayerActionService.Parse(json, files.GetOrCreateSessionGeneration(lease)).Binding;
            }
            var request = new TurnRequest
            {
                SessionId = "session_browser_diagnosis", RequestId = binding.ActionId, TurnNumber = 43,
                Timestamp = DateTime.UtcNow.ToString("O"), PlayerAction = binding.Action, ProgressionControl = control
            };
            var context = new SpiritualSelfTreatmentStageContext(request.SessionId, request.RequestId, request.TurnNumber, control);
            var runtime = CreateGameEngineRuntime(files, configureSettings: settings =>
            { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; });
            try
            {
                runtime.GameLoop.SetSession(request.SessionId, 42);
                using var originalOperation = files.BeginBrowserOriginalOperation(binding);
                var backup = await runtime.Engine.CaptureOriginalRollbackAsync(binding.ActionId);
                await runtime.Explorer.StagePendingLocalTurnRollbackSnapshotAsync(AcceptedMechanicsPlan.WoundCommandPath);
                SpiritualDiagnosisAdmission? admission = null;
                SpiritualDiagnosisAdmission? alternativeAdmission = null;
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                {
                    if (mode == "combat")
                    {
                        // This sibling proves the existing detached comparison
                        // contract, not live combat admission or a check receipt.
                        var sources = await ReadSourcesAsync(lease, AfterlifeWoundTreatmentAuthority.CombatSourcePaths);
                        var intent = SpiritualCombatHealingIntent.Create(request.SessionId, request.RequestId,
                            request.TurnNumber, selection, 3, JsonSerializer.SerializeToNode(control)!.AsObject(), sources);
                        Assert.True(intent.IsBound(context, selection));
                        await files.WriteFileAtomicAsync(lease, AcceptedMechanicsPlan.WoundCommandPath, intent.Root.ToJsonString());
                    }
                    else
                    {
                        if (mode is "selection" or "control")
                        {
                            var sources = await ReadSourcesAsync(lease, AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths("chaos_sea"));
                            admission = AfterlifeWoundTreatmentAuthority.PrepareDiagnosisCandidate(selection, sources, request.TurnNumber).Admission;
                            if (mode == "selection")
                            {
                                var alternative = alternativeChoice.Selection;
                                alternativeAdmission = AfterlifeWoundTreatmentAuthority.PrepareDiagnosisCandidate(alternative, sources, request.TurnNumber).Admission;
                                var alternativeRequest = SpiritualDiagnosisRequestCodec.Create(context, alternative, alternativeAdmission);
                                Assert.True(SpiritualDiagnosisRequestCodec.IsBound(alternativeRequest, context));
                                Assert.Equal(alternative, SpiritualDiagnosisRequestCodec.ReadSelection(alternativeRequest));
                                Assert.NotEqual(selection, alternative);
                            }
                        }
                        var staged = mode == "active"
                            ? await AfterlifeWoundTreatmentAuthority.StageSelfTreatmentAsync(files, lease, context, selection, _ => new[] { 20 })
                            : await AfterlifeWoundTreatmentAuthority.StageDiagnosisAsync(files, lease, context, selection);
                        Assert.True(staged.IsStaged, staged.IssueCode);
                        request.SpiritualHealing = Assert.IsType<JsonObject>(staged.GmContext);
                    }
                }
                runtime.Explorer.MarkExistingPendingLocalTurnValidationSnapshotFiles(AcceptedMechanicsPlan.WoundCommandPath);
                runtime.Engine.OverlayExplorerLocalRollbackSnapshot(backup, runtime.Explorer.ConsumePendingLocalTurnRollbackSnapshot());
                var requestJson = JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
                await files.WriteFileAtomicAsync(LiveTurnPreparationService.TurnRequestPath, requestJson);
                await runtime.Engine.CaptureOriginalValidationSnapshotAsync(request, backup, "обработки хода", binding);
                await using var readLease = await files.AcquireCanonicalWriteLeaseAsync();
                var manifestJson = (await files.ReadFileAsync(readLease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!;
                var authorityJson = (await files.ReadFileAsync(readLease, PendingTurnSnapshotAuthority.AuthorityPath))!;
                var manifest = JsonSerializer.Deserialize<GameEngine.PendingTurnSnapshotManifest>(manifestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                var bytes = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(readLease, manifest.Files[AcceptedMechanicsPlan.WoundCommandPath]));
                Assert.Equal(bytes, await files.ReadFileBytesAsync(readLease, AcceptedMechanicsPlan.WoundCommandPath));
                var source = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes))!.AsObject();
                if (mode == "active") Assert.True(SpiritualSelfTreatmentRequestCodec.IsBoundActive(source, context, selection, "chaos_sea", control.NextChaosSeaTurnOrdinal));
                else if (mode == "combat") Assert.True(SpiritualCombatHealingIntent.Parse(source).IsBound(context, selection));
                else
                {
                    Assert.True(SpiritualDiagnosisRequestCodec.IsBound(source, context));
                    Assert.Equal(selection, SpiritualDiagnosisRequestCodec.ReadSelection(source));
                }
                var detached = new PendingPlayerActionService.Staged(binding, requestJson, manifestJson, authorityJson,
                    binding.Json, (await files.ReadFileAsync(readLease, WoundHistoryState.HistoryPath))!, Convert.ToBase64String(bytes));
                AssertBrowserOriginalHeader(detached, checkBytes: true);
                if (mode == "selection")
                {
                    var queued = JsonNode.Parse(binding.Json)!.AsObject();
                    queued["spiritualSelfTreatment"] = alternativeAdmission!.Selection.ToJson();
                    var otherBinding = PendingPlayerActionService.Parse(queued.ToJsonString(), binding.Generation).Binding;
                    Assert.Equal(binding.ActionId, otherBinding.ActionId);
                    Assert.Equal(alternativeAdmission.Selection, otherBinding.SpiritualSelfTreatment);
                    detached = detached with { Binding = otherBinding };
                }
                if (mode == "control")
                {
                    var otherControl = JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(control))!;
                    otherControl.WorldCycleMinutes++;
                    Assert.Equal(control.CurrentRealm, otherControl.CurrentRealm);
                    Assert.Equal(control.NextChaosSeaTurnOrdinal, otherControl.NextChaosSeaTurnOrdinal);
                    var otherContext = context with { Progression = otherControl };
                    var otherSource = SpiritualDiagnosisRequestCodec.Create(otherContext, selection, admission!);
                    Assert.True(SpiritualDiagnosisRequestCodec.IsBound(otherSource, otherContext));
                    Assert.Equal(selection, SpiritualDiagnosisRequestCodec.ReadSelection(otherSource));
                    Assert.False(SpiritualDiagnosisRequestCodec.IsBound(source, otherContext));
                    var otherRequest = JsonSerializer.Deserialize<TurnRequest>(requestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                    otherRequest.ProgressionControl = otherControl;
                    detached = detached with { RequestJson = JsonSerializer.Serialize(otherRequest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) };
                }
                if (mode == "bytes")
                {
                    var changedBytes = bytes.Concat(new byte[] { 32 }).ToArray();
                    Assert.NotEqual(manifest.SnapshotFileHashes[AcceptedMechanicsPlan.WoundCommandPath], PendingTurnSnapshotAuthority.ComputeSha256(changedBytes));
                    Assert.NotNull(JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(changedBytes)));
                    detached = detached with { SpiritualWoundRequestBytesBase64 = Convert.ToBase64String(changedBytes) };
                }
                AssertBrowserOriginalHeader(detached, checkBytes: mode != "bytes");
                var before = fixture.CaptureMutationRoots(manifest.Files.Values.Concat(manifest.RollbackBackups.Values)
                    .Concat(new[] { PendingPlayerActionService.PendingPath, LiveTurnPreparationService.TurnRequestPath,
                        LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath }));
                var failure = Record.Exception(() => GameEngine.ValidateDetachedBrowserBinding(detached));
                fixture.AssertMutationRootsUnchanged(before);
                if (mode is "selection" or "control" or "bytes") Assert.IsType<InvalidDataException>(failure);
                else Assert.Null(failure); // Intended RED: only lawful diagnosis lacks its dispatch.
            }
            finally { await runtime.AudioService.DisposeAsync(); }

            async Task<IReadOnlyList<AfterlifeWoundTreatmentAuthority.SourceFile>> ReadSourcesAsync(
                FileSystemManager.CanonicalWriteLease lease, IEnumerable<string> paths)
            {
                var result = new List<AfterlifeWoundTreatmentAuthority.SourceFile>();
                foreach (var path in paths)
                {
                    var bytes = await files.ReadFileBytesAsync(lease, path);
                    result.Add(new(path, new(bytes is not null, bytes), bytes is null ? null :
                        JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes))!.AsObject()));
                }
                return result;
            }
            return true;
        });
    }

    private static void AssertBrowserOriginalHeader(PendingPlayerActionService.Staged staged, bool checkBytes)
    {
        var manifest = JsonSerializer.Deserialize<GameEngine.PendingTurnSnapshotManifest>(staged.ManifestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        var request = JsonSerializer.Deserialize<TurnRequest>(staged.RequestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        Assert.True(PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(staged.AuthorityJson, out var authority));
        Assert.NotNull(authority);
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        Assert.Equal(manifest.ManifestPayloadHash, PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(manifest, options,
            value => value.ManifestPayloadHash, (value, hash) => value.ManifestPayloadHash = hash));
        Assert.Equal(manifest.ManifestPayloadHash, authority.ManifestPayloadHash);
        Assert.Equal(manifest.SessionId, authority.SessionId); Assert.Equal(manifest.RequestId, authority.RequestId);
        Assert.Equal(manifest.TurnNumber, authority.TurnNumber);
        Assert.Equal(manifest.SessionId, request.SessionId); Assert.Equal(manifest.RequestId, request.RequestId);
        Assert.Equal(manifest.TurnNumber, request.TurnNumber); Assert.Equal(manifest.RequestTimestamp, request.Timestamp);
        Assert.Equal(staged.Binding.ActionId, manifest.RequestId); Assert.Equal(staged.Binding.ActionId, manifest.BrowserActionId);
        Assert.Equal(staged.Binding.Generation, manifest.BrowserSessionGeneration);
        Assert.Equal(staged.Binding.Action, request.PlayerAction); Assert.Equal(staged.Binding.Action, manifest.PlayerAction);
        Assert.Equal("обработки хода", manifest.SourceLabel); Assert.NotNull(manifest.BrowserOriginalMainCondition);
        Assert.NotNull(request.ProgressionControl);
        var path = AcceptedMechanicsPlan.WoundCommandPath;
        Assert.Equal(PendingTurnSnapshotAuthority.ExactSnapshotHashMode, authority.SnapshotHashMode);
        Assert.Equal(manifest.Files[path], authority.Files[path]); Assert.True(PendingTurnSnapshotAuthority.IsSafeRelativePath(authority.Files[path]));
        Assert.DoesNotContain('\\', authority.Files[path]);
        Assert.Equal(manifest.SnapshotFileHashes[path], authority.SnapshotFileHashes[path]);
        if (checkBytes) Assert.Equal(authority.SnapshotFileHashes[path], PendingTurnSnapshotAuthority.ComputeSnapshotFileHash(authority,
            Convert.FromBase64String(staged.SpiritualWoundRequestBytesBase64!)));
    }

    private static async Task SeedBrowserDiagnosisPathsAsync(FileSystemManager files, JsonObject profiles)
    {
        await AfterlifeSpiritualNaturalRecoveryTests.SeedDiagnosisBeforeBootstrapAsync(files, profiles);
        var profile = Assert.Single(profiles["profiles"]!.AsArray().OfType<JsonObject>(), p => p["actorType"]!.GetValue<string>() == "player_soul");
        var node = Assert.IsType<JsonObject>(Assert.Single(profile["activeWounds"]!.AsArray())).DeepClone().AsObject();
        var paths = node["treatment"]!["diagnosisPaths"]!.AsArray();
        var alternative = Assert.IsType<JsonObject>(Assert.Single(paths)).DeepClone().AsObject();
        alternative["diagnosisPathId"] = "native_diagnosis_alternative";
        paths.Add(alternative);
        var seed = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", node);
        profile["activeWounds"] = seed.Carriers.AfterlifeProfiles!["profiles"]![0]!["activeWounds"]!.DeepClone();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, WoundIdentityState.SerializeCanonical(seed.Identities));
        await files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, WoundHistoryState.SerializeCanonical(seed.History));
    }
}
