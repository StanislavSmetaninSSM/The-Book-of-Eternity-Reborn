using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData("missing_location")]
    [InlineData("changed_wound")]
    public Task GuidedMortalDiagnosisReadsCancelConfirmsAndStagesOnlyFreshDeclaredMethod(string mode) =>
        VerifyGuidedMortalDiagnosisStagingAsync(mode);

    [Fact]
    public Task GuidedMortalDiagnosisStagesDeclaredInputWithOnlySchedulerBookkeeping() =>
        VerifyGuidedMortalDiagnosisStagingAsync("valid");

    private static async Task VerifyGuidedMortalDiagnosisStagingAsync(string mode)
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 30,
            originalProducer: async (files, request) =>
            {
                foreach (var pair in new Dictionary<string, JsonObject>
                {
                    [WoundCarrierCatalog.NpcPath] = new() { ["schemaVersion"] = 1, ["entries"] = new JsonArray() },
                    [WoundCarrierCatalog.EnemiesPath] = new() { ["enemiesData"] = new JsonArray() },
                    [WoundCarrierCatalog.AlliesPath] = new() { ["alliesData"] = new JsonArray() },
                    [WoundCarrierCatalog.AfterlifeProfilesPath] = AfterlifeEntityProfileState.CreateDefaultRoot()
                })
                    if (await files.ReadFileBytesAsync(pair.Key) is null) await files.WriteFileAtomicAsync(pair.Key, pair.Value.ToJsonString());
                var initial = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
                files.DeleteFile(AcceptedMechanicsPlan.WoundCommandPath); // Initial fixture input, before any original exists.
                var original = CaptureDiagnosisMenuJson(files);
                var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
                var menu = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
                if (menu.IsTargetSelection) menu = menu.ForTarget(Assert.Single(menu.Targets, target => target.Owner.OwnerKind == "player"));
                var choice = Assert.Single(menu.Choices, row => row.Available && row.Selection.MortalDiagnosis?.Provider.OwnerId == "field_medic_01");
                Assert.Contains("1d20", choice.Reason); Assert.Contains("30 мин.", choice.Reason); Assert.Contains("Расход", choice.Reason);
                AssertDiagnosisMenuJson(original, files); // Read and abandoning the form are inert.
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                {
                    var confirmed = await WoundApplicationService.ConfirmAsync(files, lease, menu, choice.Value);
                    Assert.Equal(choice.Selection, confirmed); AssertDiagnosisMenuJson(original, files);
                    var changedPath = mode == "missing_location" ? MortalLocationMaterializationContract.CurrentLocationPath : WoundCarrierCatalog.PlayerPath;
                    var before = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, changedPath));
                    if (mode == "missing_location") files.DeleteFile(lease, changedPath);
                    if (mode == "changed_wound")
                    {
                        var root = JsonNode.Parse(FileSystemManager.DecodeFileBytes(before))!.AsObject();
                        root["activeWounds"]![0]!["recovery"]!["currentStepProgress"] = 1;
                        await files.WriteFileAtomicAsync(lease, changedPath, root.ToJsonString());
                    }
                    var faultImage = CaptureDiagnosisMenuJson(files);
                    if (mode != "valid") await Assert.ThrowsAsync<InvalidDataException>(() =>
                        WoundApplicationService.ConfirmAsync(files, lease, menu, choice.Value));
                    var stageStartedUtc = DateTime.UtcNow;
                    var staged = await WoundApplicationService.StageMortalDiagnosisInputAsync(files, lease,
                        new(request.SessionId, request.RequestId, request.TurnNumber, request.ProgressionControl!), confirmed);
                    var stageFinishedUtc = DateTime.UtcNow;
                    Assert.Equal(mode == "valid", staged.IsStaged);
                    Assert.Null(await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
                    if (mode == "valid")
                    {
                        var text = Assert.IsType<string>(await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                        var input = JsonNode.Parse(text)!.AsObject();
                        Assert.True(MortalWoundDiagnosisInputCodec.IsBound(input, new(request.SessionId, request.RequestId, request.TurnNumber, request.ProgressionControl!)));
                        Assert.Equal(confirmed, MortalWoundDiagnosisInputCodec.Read(input).Selection);
                        Assert.NotNull(staged.GmContext);
                        Assert.Equal(30, staged.GmContext["durationMinutes"]!.GetValue<int>());
                        var after = CaptureDiagnosisMenuJson(files); after.Remove(files.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath));
                        Assert.Equal(original.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
                        foreach (var pair in original)
                        {
                            if (pair.Key == files.ResolvePath(ProgressionScheduleService.SchedulePath))
                            {
                                var beforeSchedule = JsonNode.Parse(FileSystemManager.DecodeFileBytes(pair.Value))!.AsObject();
                                var afterSchedule = JsonNode.Parse(FileSystemManager.DecodeFileBytes(after[pair.Key]))!.AsObject();
                                Assert.True(DateTime.TryParseExact(beforeSchedule["lastUpdatedUtc"]!.GetValue<string>(), "O",
                                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var previousUtc));
                                Assert.Equal(DateTimeKind.Utc, previousUtc.Kind);
                                Assert.True(DateTime.TryParseExact(afterSchedule["lastUpdatedUtc"]!.GetValue<string>(), "O",
                                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var updatedUtc));
                                Assert.Equal(DateTimeKind.Utc, updatedUtc.Kind);
                                Assert.InRange(updatedUtc, stageStartedUtc, stageFinishedUtc);
                                Assert.True(updatedUtc >= previousUtc);
                                afterSchedule["lastUpdatedUtc"] = beforeSchedule["lastUpdatedUtc"]!.DeepClone();
                                Assert.True(JsonNode.DeepEquals(beforeSchedule, afterSchedule),
                                    "Staging changed schedule fields beyond lastUpdatedUtc.");
                            }
                            else Assert.True(pair.Value.AsSpan().SequenceEqual(after[pair.Key]),
                                "Unexpected staging mutation: " + Path.GetRelativePath(files.GameSessionPath, pair.Key));
                        }
                    }
                    else { AssertDiagnosisMenuJson(faultImage, files); await files.WriteFileAtomicBytesAsync(lease, changedPath, before); }
                    // Complete the independent fixture's initial preparation only.
                    // No rejected post-accepted input is patched into live authority.
                    if (mode != "valid") await files.WriteFileAtomicBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath, initial);
                }
                await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
                {
                    SessionId = request.SessionId, RequestId = request.RequestId, TurnNumber = request.TurnNumber,
                    CurrentRealm = "Mortal World", ProgressionControl = request.ProgressionControl,
                    PlayerAction = request.PlayerAction, PreGeneratedDices1d20 = request.PreGeneratedDices1d20
                });
            });
    }

    private static Dictionary<string, byte[]> CaptureDiagnosisMenuJson(FileSystemManager files) =>
        FileSystemManager.EnumerateFilesWithoutFollowingReparsePoints(files.GameSessionPath, "*.json")
            .Where(path => !Path.GetRelativePath(files.GameSessionPath, path).Replace(Path.DirectorySeparatorChar, '/')
                .StartsWith(".boe_runtime", StringComparison.Ordinal))
            .ToDictionary(static path => path, File.ReadAllBytes, StringComparer.Ordinal);
    private static void AssertDiagnosisMenuJson(IReadOnlyDictionary<string, byte[]> expected, FileSystemManager files)
    {
        var actual = CaptureDiagnosisMenuJson(files);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
}
