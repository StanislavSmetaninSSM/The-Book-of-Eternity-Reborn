using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public async Task GuidedPhysicalHeldRequestReconstructsInActualFreshProcessAndNewRoot(string mode)
    {
        await using var source = await MortalWoundTreatmentResolverTests.DiagnosisOwnershipFixture.CreateAsync(
            20, signedInput: true, physicalMode: mode);
        var input = source.Authority.ReadGuidedTreatmentOriginalInput();
        var frame = MortalWoundGuidedTreatmentInputCodec.Read(input).Frame;
        var held = await WoundApplicationService.ResolveMortalTreatmentInputAsync(source.Files, source.Lease, frame);
        var request = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(held.Request)!.ToJsonString();
        var generation = source.Files.ReadLocalGenerationSnapshot(source.Lease);
        var generationBytes = File.ReadAllBytes(source.Files.SessionGenerationPath);
        var sourceBefore = CaptureGuidedColdCanonical(source.Files);
        var package = sourceBefore.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
        await source.ReleaseLeaseAsync();
        var loadedRoot = Path.Combine(Path.GetTempPath(), "boe-guided-cold-loaded-" + Guid.NewGuid().ToString("N"));
        var loaded = new FileSystemManager(loadedRoot, NullLogger<FileSystemManager>.Instance);
        loaded.EnsureDirectoryStructure();
        using var owned = new CleanupOwnedFixture(loadedRoot, line => _directGachaOutput?.WriteLine(line));
        try
        {
            // Copy the actual task-owned durable session and its exact existing
            // generation metadata. Nothing is re-signed, synthesized or rebased.
            foreach (var (path, bytes) in package)
            {
                var destination = loaded.ResolvePath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await File.WriteAllBytesAsync(destination, bytes);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(loaded.SessionGenerationPath)!);
            await File.WriteAllBytesAsync(loaded.SessionGenerationPath, generationBytes);
            Assert.NotEqual(source.Files.BasePath, loaded.BasePath);
            Assert.Equal(generation.Binding.Id, held.Request.Coordinates.SessionGeneration);
            var context = new JsonObject
            {
                ["parentProcessId"] = Environment.ProcessId, ["sourceRoot"] = source.Files.BasePath,
                ["mode"] = mode, ["generation"] = generation.Binding.Id,
                ["request"] = request,
                ["digests"] = new JsonObject(package.Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key,
                    JsonValue.Create(PendingTurnSnapshotAuthority.ComputeSha256(pair.Value)))))
            };
            var contextPath = Path.Combine(loadedRoot, "guided-cold-context.json");
            var outputPath = Path.Combine(loadedRoot, "guided-cold-result.json");
            await File.WriteAllTextAsync(contextPath, context.ToJsonString());
            var failure = await Record.ExceptionAsync(() => RunMortalArchiveChildAsync("engine-mortal-guided-treatment-cold",
                loadedRoot, outputPath, 0, contextPath));
            var resultText = File.Exists(outputPath) ? await File.ReadAllTextAsync(outputPath) : null;
            var log = File.Exists(outputPath + ".log") ? await File.ReadAllTextAsync(outputPath + ".log") : null;
            WritePhysicalInjuryDiagnostic("physical_guided_cold_result", new { Mode = mode, Context = context,
                Result = resultText, Log = log, Failure = failure?.ToString() });
            Assert.Null(failure); Assert.NotNull(resultText);
            var result = JsonNode.Parse(resultText!)!.AsObject();
            Assert.True(result["success"]!.GetValue<bool>());
            Assert.NotEqual(Environment.ProcessId, result["processId"]!.GetValue<int>());
            Assert.Equal(request, result["request"]!.GetValue<string>());
            Assert.Equal(generation.Binding.Id, result["generation"]!.GetValue<string>());
            AssertGuidedColdImages(package, loaded);
            AssertGuidedColdImages(sourceBefore, source.Files);
        }
        finally { Assert.False(source.Files.BasePath == loaded.BasePath); }
    }

    /// <summary>
    /// Existing native-host public entrypoint. The child starts with fresh static
    /// registries and acquires the actual MainAdmission/current lease itself.
    /// </summary>
#pragma warning disable xUnit1013 // Required public child-host entrypoint, not a discovered test; preserve the existing host protocol.
    public static async Task WriteMortalGuidedTreatmentColdProbeAsync(string root, string output, int scenario, string contextPath)
    {
        var context = JsonNode.Parse(await File.ReadAllTextAsync(contextPath))!.AsObject();
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        using var main = files.BeginMainAdmission(); await main.AcquireAsync();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var before = CaptureGuidedColdCanonical(files);
        Exception? failure = null; string? serialized = null;
        try
        {
            Assert.NotEqual(Environment.ProcessId, context["parentProcessId"]!.GetValue<int>());
            Assert.NotEqual(files.BasePath, context["sourceRoot"]!.GetValue<string>());
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(files, lease));
            foreach (var pair in context["digests"]!.AsObject())
                Assert.Equal(pair.Value!.GetValue<string>(), PendingTurnSnapshotAuthority.ComputeSha256(before[pair.Key]));
            var input = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath)))!.AsObject();
            var frame = MortalWoundGuidedTreatmentInputCodec.Read(input).Frame;
            var admitted = await WoundApplicationService.ResolveMortalTreatmentInputAsync(files, lease, frame, requireRetained: true);
            serialized = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(admitted.Request)!.ToJsonString();
            Assert.Equal(context["request"]!.GetValue<string>(), serialized);
            Assert.Equal(context["mode"]!.GetValue<string>(), admitted.Request.Mode);
            Assert.Equal(context["generation"]!.GetValue<string>(), admitted.Request.Coordinates.SessionGeneration);
            Assert.True(MortalWoundTreatmentDetachedSealValidator.IsValid(admitted.Request));
            if (admitted.Request.ModeAuthority is MortalWoundProcedureCheckAuthority check)
                Assert.True(admitted.Owner.HasLiveProcedureReservationAgreement(check));
            var repeated = await WoundApplicationService.ResolveMortalTreatmentInputAsync(files, lease, frame, requireRetained: true);
            Assert.Equal(serialized, WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(repeated.Request)!.ToJsonString());
            AssertGuidedColdImages(before, files);
        }
        catch (Exception error) { failure = error; }
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { success = failure is null,
            processId = Environment.ProcessId, generation = files.ReadLocalGenerationSnapshot(lease).Binding.Id,
            request = serialized, failure = failure?.ToString() }));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
#pragma warning restore xUnit1013

    private static Dictionary<string, byte[]> CaptureGuidedColdCanonical(FileSystemManager files) =>
        Directory.EnumerateFiles(files.GameSessionPath, "*", new EnumerationOptions
        { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }).ToDictionary(
            path => Path.GetRelativePath(files.GameSessionPath, path).Replace('\\', '/'), File.ReadAllBytes, StringComparer.Ordinal);

    private static void AssertGuidedColdImages(IReadOnlyDictionary<string, byte[]> before, FileSystemManager files)
    {
        var after = CaptureGuidedColdCanonical(files);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var pair in before) Assert.True(pair.Value.AsSpan().SequenceEqual(after[pair.Key]), pair.Key);
    }
}
