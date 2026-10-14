using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public Task GuaranteedTreatment_MissingGenerationRefusesActualOuterAdmissionWithoutCreatingGeneration() =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true, refusalScenario: "generation_missing");

    [Fact]
    public Task GuaranteedTreatment_ForeignDonorGenerationRefusesActualOuterAdmissionAndRetainsEvidence() =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true, refusalScenario: "generation_foreign");

    private sealed record JointGenerationDonor(string Root, string Generation, byte[] Bytes, Dictionary<string, byte[]?> Before);

    private static async Task<JointGenerationDonor> InitializeJointGenerationDonorAsync(string root)
    {
        Assert.False(Directory.Exists(root)); Assert.False(File.Exists(root));
        var donor = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        donor.EnsureDirectoryStructure();
        string generation;
        byte[] bytes;
        await using (var lease = await donor.AcquireCanonicalWriteLeaseAsync())
        {
            generation = donor.GetOrCreateSessionGeneration(lease);
            var observed = donor.ReadLocalGenerationSnapshot(lease);
            Assert.Equal(generation, observed.Binding.Id);
            bytes = Assert.IsType<byte[]>(observed.Bytes);
            lease.EnsureNoPendingLocalDecision();
        }
        // A real otherwise independent storage root issues this negative data.
        // It conveys no authority for the retained target original/cohort.
        return new(root, generation, bytes, await CaptureJointGenerationRefusalRawImagesAsync(donor));
    }

    // Diagnostic raw capture only: the damaged root cannot acquire a canonical
    // lease. Never parse/mint G, call an inspector, or run storage recovery here.
    private static async Task<Dictionary<string, byte[]?>> CaptureJointGenerationRefusalRawImagesAsync(FileSystemManager files)
    {
        var scope = new TrustedLocalFileScope([files.BasePath]);
        var session = EnumerateJointOrdinaryNames(files.GameSessionPath);
        var runtime = EnumerateJointOrdinaryNames(files.RuntimeRootPath);
        var images = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var relative in session.Files)
            images.Add(relative, await File.ReadAllBytesAsync(scope.ValidateFile(Path.Combine(files.GameSessionPath,
                relative.Replace('/', Path.DirectorySeparatorChar)), allowMissing: false)));
        var excludedLockStreams = new HashSet<string>(StringComparer.Ordinal)
        { "locks/canonical-write.lock", "locks/session-lifecycle.lock", "locks/gm-main-owner.lock" };
        foreach (var relative in runtime.Files.Where(path => !excludedLockStreams.Contains(path)))
            images.Add("$runtime/" + relative, await File.ReadAllBytesAsync(scope.ValidateFile(Path.Combine(files.RuntimeRootPath,
                relative.Replace('/', Path.DirectorySeparatorChar)), allowMissing: false)));
        var generationPath = scope.ValidateFile(files.SessionGenerationPath);
        images.Add("$sessionGeneration", File.Exists(generationPath) ? await File.ReadAllBytesAsync(generationPath) : null);
        // Complete names retain unknown/new files and empty directories too.
        // Only the contents of the three inert lock streams are excluded.
        images.Add("$gameSessionDirectories", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(session.Directories)));
        images.Add("$runtimeDirectories", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(runtime.Directories)));
        images.Add("$runtimeFiles", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(runtime.Files)));
        return images;
    }

    private static void AssertJointGenerationRefusalRawImagesEqual(IReadOnlyDictionary<string, byte[]?> before,
        IReadOnlyDictionary<string, byte[]?> after)
    {
        Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal), after.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
    }
}
