using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualDiagnosisStagingTests
{
    [Fact]
    public async Task CallerControlIsDetachedBeforeTheFirstActualSourceRead()
    {
        ProgressionControl? borrowed = null; var armed = false; var cut = 0;
        var hooks = new FileSystemManagerHooks { BeforeCanonicalReadOpenAsync = _ =>
        {
            if (armed)
            {
                armed = false; cut++;
                borrowed!.CurrentRealm = "Mortal World"; borrowed.NextChaosSeaTurnOrdinal = 99;
                borrowed.ChaosSeaCyclesExpectedThisTurn = 99;
            }
            return Task.CompletedTask;
        } };
        await using var fixture = await SpiritualSelfTreatmentRequestTests.Fixture.CreateAsync(tier: 0, hooks: hooks);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var selection = await InstallDiagnosisAsync(fixture.Files, lease);
        borrowed = fixture.Context.Progression;
        var before = JsonSerializer.Serialize(borrowed);
        armed = true;
        var stage = await AfterlifeWoundTreatmentAuthority.StageDiagnosisAsync(fixture.Files, lease, fixture.Context, selection);
        Assert.Equal(1, cut); Assert.True(stage.IsStaged, stage.IssueCode);
        Assert.Equal(99, borrowed.NextChaosSeaTurnOrdinal);
        var request = await fixture.ReadRequestAsync(lease);
        Assert.Equal(before, JsonSerializer.Serialize(request["control"]!.Deserialize<ProgressionControl>()));
    }

    [Fact]
    public async Task ActualRequestReadbackUncertaintyIsNeverAnOrdinaryStagingRefusal()
    {
        SpiritualSelfTreatmentRequestTests.Fixture? owned = null; var armed = false; var cut = 0;
        var hooks = new FileSystemManagerHooks { BeforeCanonicalReadOpenAsync = path =>
        {
            if (armed && path == AcceptedMechanicsPlan.WoundCommandPath && File.Exists(owned!.Files.ResolvePath(path)))
            {
                armed = false; cut++;
                throw new CoordinatedStatePublicationUncertainException(new IOException("Injected actual request readback uncertainty."));
            }
            return Task.CompletedTask;
        } };
        await using var fixture = await SpiritualSelfTreatmentRequestTests.Fixture.CreateAsync(tier: 0, hooks: hooks);
        owned = fixture;
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var selection = await InstallDiagnosisAsync(fixture.Files, lease);
        armed = true;
        try
        {
            await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() =>
                AfterlifeWoundTreatmentAuthority.StageDiagnosisAsync(fixture.Files, lease, fixture.Context, selection));
        }
        finally { armed = false; }
        Assert.Equal(1, cut);
        Assert.True(File.Exists(fixture.Files.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath)));
    }

    [Theory]
    [InlineData("chaos_sea")][InlineData("shining_abode")]
    public async Task ActualPreparedH0StageRetainsExactNoDrawRequestAndDoesNotHealOrAdvance(string realm)
    {
        await using var fixture = await SpiritualSelfTreatmentRequestTests.Fixture.CreateAsync(tier: 0, realm: realm);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var selection = await InstallDiagnosisAsync(fixture.Files, lease);
        var before = fixture.CanonicalBytes();
        var stage = await AfterlifeWoundTreatmentAuthority.StageDiagnosisAsync(fixture.Files, lease, fixture.Context, selection);
        Assert.True(stage.IsStaged, stage.IssueCode);
        Assert.False(stage.AlreadyStaged);
        Assert.Equal(before, fixture.CanonicalBytes());
        var request = await fixture.ReadRequestAsync(lease);
        Assert.Equal("spiritual_diagnosis_request", request["kind"]!.GetValue<string>());
        Assert.Equal(0, request["diagnosis"]!["healingTier"]!.GetValue<int>());
        foreach (var field in new[] { "roll", "payment", "cost", "snapshotToken" }) Assert.False(request.ContainsKey(field));
        Assert.Equal(selection, SpiritualDiagnosisRequestCodec.ReadSelection(request));
        Assert.Equal("diagnose", stage.GmContext!["mode"]!.GetValue<string>());
        Assert.NotEmpty(stage.GmContext["naturalRecovery"]!["cohort"]!.AsArray());
        var bytes = await fixture.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        var retry = await AfterlifeWoundTreatmentAuthority.StageDiagnosisAsync(fixture.Files, lease, fixture.Context, selection);
        Assert.True(retry.IsStaged, retry.IssueCode); Assert.True(retry.AlreadyStaged);
        Assert.Equal(bytes, await fixture.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Equal(before, fixture.CanonicalBytes());
    }

    [Theory]
    [InlineData("missing_art")][InlineData("unknown_requirement")][InlineData("unsupported_check")]
    [InlineData("foreign_target")][InlineData("stale_wound")]
    public async Task ActualStageRefusesUnprovedCurrentInputsWithoutRequestOrCanonicalMutation(string fault)
    {
        await using var fixture = await SpiritualSelfTreatmentRequestTests.Fixture.CreateAsync(tier: 0);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var selection = await InstallDiagnosisAsync(fixture.Files, lease, fault);
        if (fault == "foreign_target") selection = selection with { Diagnosis = selection.Diagnosis! with
            { Target = new("chaos_sea", "resident", "unavailable_resident", WoundCarrierCatalog.AfterlifeProfilesPath) } };
        if (fault == "stale_wound") selection = selection with { ExpectedWoundFingerprint = "sha256:" + new string('f', 64) };
        var before = fixture.CanonicalBytes();
        var stage = await AfterlifeWoundTreatmentAuthority.StageDiagnosisAsync(fixture.Files, lease, fixture.Context, selection);
        Assert.False(stage.IsStaged); Assert.NotNull(stage.IssueCode);
        Assert.Equal(before, fixture.CanonicalBytes());
        Assert.Null(await fixture.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
    }

    internal static async Task<SpiritualSelfTreatmentSelection> InstallDiagnosisAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, string? fault = null)
    {
        var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
        var profile = profiles["profiles"]![0]!.AsObject();
        var node = profile["activeWounds"]![0]!.DeepClone().AsObject();
        node["complications"] = new JsonArray(new JsonObject { ["complicationId"] = "diagnosis_hidden", ["kind"] = "spiritual_instability",
            ["state"] = "active", ["displayName"] = "Скрытая неустойчивость", ["treatmentDifficultyModifier"] = 0,
            ["ownedEffectIds"] = new JsonArray(), ["visibility"] = "hidden" });
        node["treatment"]!["diagnosisPaths"] = new JsonArray(new JsonObject
        {
            ["diagnosisPathId"] = "stage_diagnosis", ["displayName"] = "Осмотреть духовную рану", ["visibility"] = "public",
            ["requiresKnownFacts"] = new JsonArray(), ["requirements"] = new JsonArray(new JsonObject
            { ["kind"] = fault == "unknown_requirement" ? "unproved_condition" : "source_capability", ["capabilityRef"] = "spiritual_healing", ["actorRole"] = "provider" }),
            ["check"] = fault == "unsupported_check" ? new JsonObject { ["formula"] = "unowned" } : new JsonObject(),
            ["reveals"] = new JsonArray("complication:diagnosis_hidden"), ["failurePolicy"] = "no_reveal"
        });
        Assert.True(AfterlifeEntityProfileState.TryNormalizeEffectRealm(profile["realm"]!.GetValue<string>(), out var realm));
        if (realm == "shining_abode")
        {
            // Explicit initial realm DATA for diagnosis, not an accepted-turn receipt.
            profile["locationId"] = "hall_diagnosis_source";
            profile["locationName"] = "Зал осмотра";
            var shining = ShiningAbodeState.CreateDefaultState();
            shining["currentHallId"] = profile["locationId"]!.DeepClone();
            shining["halls"] = new JsonArray(new JsonObject
            {
                ["hallId"] = profile["locationId"]!.DeepClone(), ["hallName"] = profile["locationName"]!.DeepClone(),
                ["description"] = "Тихий зал для духовного осмотра.", ["serviceTags"] = new JsonArray()
            });
            ShiningAbodeState.NormalizeStateRoot(shining, residentRoot: null);
            await files.WriteFileAtomicAsync(lease, ShiningAbodeState.StatePath, shining.ToJsonString());
        }
        var input = WoundPlayerProjectionTestData.InputInRealm(realm, node);
        profile["activeWounds"] = input.Carriers.AfterlifeProfiles!["profiles"]![0]!["activeWounds"]!.DeepClone();
        if (fault == "missing_art") profile["standardArts"]!.AsObject().Remove("spiritual_healing");
        await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, WoundIdentityState.SerializeCanonical(input.Identities));
        await files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, WoundHistoryState.SerializeCanonical(input.History));
        var parsed = WoundMaterializationContract.Parse(profile["activeWounds"]![0]!.ToJsonString(), "fixture.diagnosis");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues));
        var wound = parsed.Wound!;
        return new(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
        { Diagnosis = new(wound.Owner, "stage_diagnosis", WoundDiagnosisEvidence.ComputePathFingerprint(wound, "stage_diagnosis")) };
    }
}
