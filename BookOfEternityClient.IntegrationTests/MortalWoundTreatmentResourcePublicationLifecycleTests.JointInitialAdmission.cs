using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("initial_manifest_missing")]
    [InlineData("initial_manifest_malformed")]
    [InlineData("initial_input_missing")]
    [InlineData("initial_input_malformed")]
    [InlineData("initial_authority_missing")]
    [InlineData("initial_authority_malformed")]
    public Task GuaranteedTreatment_InitiallyBadHeaderBeforeChildPidPreservesFirstOwnerNamespace(string corruption) =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true, refusalScenario: corruption);

    [Theory]
    [InlineData("initial_generation_missing")]
    [InlineData("initial_generation_malformed")]
    [InlineData("initial_generation_foreign")]
    public Task GuaranteedTreatment_InitiallyBadGenerationBeforeChildPidPreservesFirstOwnerNamespace(string corruption) =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true, refusalScenario: corruption);

    private static bool IsInitialJointAuthorityRefusal(string? corruption) => corruption is
        "initial_manifest_missing" or "initial_manifest_malformed" or "initial_input_missing" or "initial_input_malformed" or
        "initial_authority_missing" or "initial_authority_malformed" or "initial_generation_missing" or
        "initial_generation_malformed" or "initial_generation_foreign";

    private static bool IsInitialJointEarlyAdmissionRefusal(string? corruption) => corruption is
        "initial_manifest_malformed" or "initial_generation_missing" or "initial_generation_malformed" or "initial_generation_foreign";

    private sealed record JointInitialAuthorityDamage(string PhysicalLeaf, Dictionary<string, byte[]?> Before,
        Dictionary<string, byte[]?> After, JointGenerationDonor? Donor);

    private static JsonObject JointInitialRawInventory(IReadOnlyDictionary<string, byte[]?> images)
    {
        var inventory = new JsonObject();
        foreach (var pair in images.OrderBy(row => row.Key, StringComparer.Ordinal))
            inventory.Add(pair.Key, pair.Value is null ? null : new JsonObject
            { ["length"] = pair.Value.Length, ["sha256"] = MortalArchiveHash(pair.Value) });
        return inventory;
    }

    private static void AssertJointInitialRawInventory(JsonObject expected, IReadOnlyDictionary<string, byte[]?> actual)
    {
        // Exact keys distinguish an absent physical entry from a present null alias.
        Assert.Equal(expected.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal),
            actual.Keys.OrderBy(key => key, StringComparer.Ordinal));
        foreach (var pair in expected)
        {
            if (pair.Value is null) Assert.Null(actual[pair.Key]);
            else
            {
                var image = Assert.IsType<byte[]>(actual[pair.Key]);
                Assert.Equal(pair.Value["length"]!.GetValue<int>(), image.Length);
                Assert.Equal(pair.Value["sha256"]!.GetValue<string>(), MortalArchiveHash(image));
            }
        }
    }

    private static async Task<JointInitialAuthorityDamage> DamageJointInitialAuthorityBeforeChildLaunchAsync(
        FileSystemManager files, string corruption, string? donorRoot)
    {
        Assert.True(IsInitialJointAuthorityRefusal(corruption));
        Assert.True(Directory.Exists(files.GameSessionPath));
        var before = await CaptureJointGenerationRefusalRawImagesAsync(files);
        var pendingPublicationNames = before.Keys.Where(path =>
            path.StartsWith("$runtime/trusted-local-publication-v1/", StringComparison.Ordinal)).ToArray();
        Assert.Empty(pendingPublicationNames); // Observe this genuine producer, not an arbitrary damaged root.
        var isGeneration = corruption.StartsWith("initial_generation_", StringComparison.Ordinal);
        var relative = corruption switch
        {
            "initial_manifest_missing" or "initial_manifest_malformed" => LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            "initial_input_missing" or "initial_input_malformed" => "input/turn_request.json",
            "initial_authority_missing" or "initial_authority_malformed" => PendingTurnSnapshotAuthority.AuthorityPath,
            _ => "$runtime/session-generation/current.json"
        };
        var physical = new TrustedLocalFileScope([files.BasePath]).ValidateFile(isGeneration ? files.SessionGenerationPath :
            Path.Combine(files.GameSessionPath, relative), allowMissing: false);
        Assert.Equal(Assert.IsType<byte[]>(before[relative]), await File.ReadAllBytesAsync(physical));
        JointGenerationDonor? donor = null;
        byte[]? replacement = null;
        if (corruption == "initial_generation_foreign")
        {
            donor = await InitializeJointGenerationDonorAsync(Assert.IsType<string>(donorRoot));
            Assert.NotEqual(files.BasePath, donor.Root);
            Assert.False(before["$sessionGeneration"]!.AsSpan().SequenceEqual(donor.Bytes));
            replacement = donor.Bytes;
        }
        else if (corruption.EndsWith("_malformed", StringComparison.Ordinal)) replacement = Encoding.UTF8.GetBytes("{");
        // All issuing leases have closed. This single physical leaf is negative
        // data only; no resigning, draft reconstruction, target admission or repair.
        if (replacement is null) File.Delete(physical);
        else await File.WriteAllBytesAsync(physical, replacement);
        var after = await CaptureJointGenerationRefusalRawImagesAsync(files);
        var expected = new Dictionary<string, byte[]?>(before, StringComparer.Ordinal);
        if (replacement is null) expected.Remove(relative);
        else expected[relative] = replacement;
        if (isGeneration)
        {
            expected["$sessionGeneration"] = replacement;
            if (replacement is null)
            {
                var names = JsonSerializer.Deserialize<string[]>(before["$runtimeFiles"]!)!;
                Assert.Single(names, name => name == "session-generation/current.json");
                expected["$runtimeFiles"] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                    names.Where(name => name != "session-generation/current.json").ToArray()));
            }
        }
        AssertJointGenerationRefusalRawImagesEqual(expected, after);
        if (donor is not null)
            AssertJointGenerationRefusalRawImagesEqual(donor.Before,
                await CaptureJointGenerationRefusalRawImagesAsync(new FileSystemManager(donor.Root, NullLogger<FileSystemManager>.Instance)));
        return new(relative, before, after, donor);
    }

    private static void AssertJointInitialFirstOwnerOutcome(string corruption, Exception? ownerFailure,
        bool actualCallerEntered, bool? handled, bool inGameBefore, GameEngine engine,
        IReadOnlyDictionary<string, byte[]?> before, IReadOnlyDictionary<string, byte[]?> after)
    {
        Assert.True(IsInitialJointAuthorityRefusal(corruption));
        AssertJointGenerationRefusalRawImagesEqual(before, after);
        Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
        Assert.Equal(inGameBefore, GetPrivateFieldValue<bool>(engine, "_inGame"));
        if (corruption == "initial_manifest_malformed")
        {
            var refused = Assert.IsAssignableFrom<JsonException>(ownerFailure);
            Assert.Contains("FileSystemManager.PreflightBrowserOriginalAdmission", refused.StackTrace ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(Encoding.UTF8.GetBytes("{"), before[LiveTurnPreparationService.PendingTurnSnapshotManifestPath]);
        }
        else if (corruption.StartsWith("initial_generation_", StringComparison.Ordinal))
        {
            var refused = Assert.IsType<InvalidDataException>(ownerFailure);
            Assert.Equal(corruption switch
            {
                "initial_generation_missing" => "Active joint continuation generation is missing; evidence retained.",
                "initial_generation_malformed" => "Session generation authority is invalid.",
                "initial_generation_foreign" => "Active joint continuation generation is invalid or differs; evidence retained.",
                _ => throw new InvalidOperationException(corruption)
            }, refused.Message);
            if (corruption == "initial_generation_missing") Assert.Null(after["$sessionGeneration"]);
            if (corruption == "initial_generation_malformed") Assert.Equal(Encoding.UTF8.GetBytes("{"), after["$sessionGeneration"]);
        }
        else
        {
            Assert.Null(ownerFailure); Assert.True(handled);
            Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
        }
        Assert.Equal(!IsInitialJointEarlyAdmissionRefusal(corruption), actualCallerEntered);
        if (IsInitialJointEarlyAdmissionRefusal(corruption)) Assert.Null(handled);
    }
}
