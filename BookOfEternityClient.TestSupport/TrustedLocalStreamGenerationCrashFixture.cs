using System.Buffers.Binary;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises generation-member ordering and empty-image decisions in isolated v2 publication processes.
/// </summary>
public static class TrustedLocalStreamGenerationCrashFixture
{
    /// <summary>
    /// Runs one actual publication cut or ordinary cold canonical acquisition.
    /// </summary>
    /// <param name="args">
    /// The owned root, declared heap bytes, generation mode and requested publication cut.
    /// </param>
    /// <returns>
    /// Zero on cold acquisition, 73 at the exact requested abrupt cut, or 90 on a recorded failure.
    /// </returns>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || args[2] is not ("generation-fresh-publish" or "generation-transition-publish" or "generation-recover")) return 64;
        var heap = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (heap != long.Parse(args[1])) throw new InvalidOperationException("The owned generation child did not inherit its declared heap.");
        try
        {
            var report = new Dictionary<string, object?> { ["HeapBytes"] = heap, ["Mode"] = args[2] };
            FileSystemManager? files = null;
            void Observe(TrustedLocalPublicationPhase phase, int index)
            {
                if (phase == TrustedLocalPublicationPhase.IntentPublished)
                {
                    DescribeGenerationRegion(files!, report);
                    Directory.Delete(Path.Combine(args[0], "inputs"), recursive: true);
                }
                var reached = args[3] switch
                {
                    "before-generation" => phase == TrustedLocalPublicationPhase.MemberPublished && index == 2,
                    "generation-staged" => phase == TrustedLocalPublicationPhase.MemberStaged && index == 3,
                    "generation-published" => phase == TrustedLocalPublicationPhase.MemberPublished && index == 3,
                    "Committed" => phase == TrustedLocalPublicationPhase.Committed && index == -1,
                    _ => false
                };
                if (!reached) return;
                report["Phase"] = "AbruptExit"; report["Cut"] = phase.ToString(); report["Index"] = index;
                Console.WriteLine(JsonSerializer.Serialize(report)); Console.Out.Flush(); Environment.Exit(73);
            }
            files = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks { LocalPublicationObserver = Observe });
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            if (args[2] == "generation-recover")
            {
                Console.WriteLine(JsonSerializer.Serialize(new { Phase = "CanonicalAcquisitionReturned", HeapBytes = heap,
                    GenerationId = files.ReadExistingSessionGeneration(lease),
                    GenerationBytes = File.Exists(files.SessionGenerationPath) ? Convert.ToBase64String(File.ReadAllBytes(files.SessionGenerationPath)) : null }));
                return 0;
            }
            var scope = new TrustedLocalFileScope([args[0]]);
            var content = files.ResolvePath("game_state/core/generation-content.bin");
            var created = files.ResolvePath("game_state/core/generation-created-empty.bin");
            var deleted = files.ResolvePath("game_state/core/generation-deleted-empty.bin");
            var after = TrustedLocalFileImage.CaptureFile(scope, Path.Combine(args[0], "inputs/after.bin"));
            var empty = TrustedLocalFileImage.CaptureFile(scope, Path.Combine(args[0], "inputs/empty.bin"));
            if (args[2] == "generation-fresh-publish")
            {
                if (files.ReadExistingSessionGeneration(lease) != null) throw new InvalidDataException("The fresh fixture already has a generation.");
                var outcome = await files.PublishLocalImageFilesAsync(lease,
                [
                    new("game_state/core/generation-content.bin", TrustedLocalFileImage.CaptureFile(scope, content), after),
                    new("game_state/core/generation-created-empty.bin", TrustedLocalFileImage.FromBytes(null), empty),
                    new("game_state/core/generation-deleted-empty.bin", TrustedLocalFileImage.CaptureFile(scope, deleted), TrustedLocalFileImage.FromBytes(null))
                ]);
                throw new InvalidOperationException("The fresh image adapter returned without its requested cut: " + outcome.Disposition);
            }
            var binding = files.ReadLocalGenerationSnapshot(lease).Binding;
            new TrustedLocalFilePublication(files, scope).PublishImagesWithOutcome(lease, binding,
            [
                new(content, TrustedLocalFileImage.CaptureFile(scope, content), after),
                new(created, TrustedLocalFileImage.FromBytes(null), empty),
                new(deleted, TrustedLocalFileImage.CaptureFile(scope, deleted), TrustedLocalFileImage.FromBytes(null)),
                new(files.SessionGenerationPath, TrustedLocalFileImage.CaptureFile(scope, files.SessionGenerationPath),
                    TrustedLocalFileImage.CaptureFile(scope, Path.Combine(args[0], "inputs/generation-after.json")))
            ], Observe);
            throw new InvalidOperationException("The generation transition returned without its requested cut.");
        }
        catch (Exception failure)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { Phase = "GenerationScenarioFailed", HeapBytes = heap,
                FailureType = failure.GetType().Name, failure.Message, Stack = failure.StackTrace }));
            return 90;
        }
    }

    /// <summary>
    /// Records the producer's exact generation region before disposable inputs disappear.
    /// </summary>
    /// <param name="files">
    /// Resolves the owned active journal and generation member.
    /// </param>
    /// <param name="report">
    /// Receives independently retained generation bytes, binding and member ordering.
    /// </param>
    private static void DescribeGenerationRegion(FileSystemManager files, Dictionary<string, object?> report)
    {
        using var stream = File.OpenRead(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json"));
        var prefix = new byte[16]; stream.ReadExactly(prefix);
        if (!prefix.AsSpan(0, 8).SequenceEqual("BOELP2\r\n"u8)) throw new InvalidDataException("The generation scenario did not produce v2.");
        var headerLength = BinaryPrimitives.ReadInt64LittleEndian(prefix.AsSpan(8));
        if (headerLength is <= 0 or > 1024 * 1024 || headerLength > stream.Length - 16)
            throw new InvalidDataException("The generation frame header length is invalid.");
        var header = new byte[(int)headerLength]; stream.ReadExactly(header);
        using var document = JsonDocument.Parse(header);
        var members = document.RootElement.GetProperty("Members").EnumerateArray().ToArray();
        if (members.Length != 4) throw new InvalidDataException("The generation scenario did not declare exactly four members.");
        var generationMember = members[3];
        if (!string.Equals(Path.GetFullPath(generationMember.GetProperty("Path").GetString()!), Path.GetFullPath(files.SessionGenerationPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("The generation was not the final declared member.");
        var region = generationMember.GetProperty("After");
        var length = region.GetProperty("Length").GetInt64();
        var offset = region.GetProperty("Offset").GetInt64();
        if (length is <= 0 or > 4096 || offset < 0 || offset > stream.Length - 16 - headerLength - length)
            throw new InvalidDataException("The fixture's small generation region is invalid.");
        stream.Position = checked(16 + headerLength + offset);
        var bytes = new byte[(int)length]; stream.ReadExactly(bytes);
        report["GenerationAfterBytes"] = Convert.ToBase64String(bytes);
        report["GenerationAfterId"] = document.RootElement.GetProperty("GenerationAfter").GetProperty("Id").GetString();
        report["GenerationBeforeExists"] = document.RootElement.GetProperty("GenerationBefore").GetProperty("Exists").GetBoolean();
        report["GenerationBeforeId"] = document.RootElement.GetProperty("GenerationBefore").GetProperty("Id").GetString();
        report["MemberCount"] = members.Length;
        report["CreatedEmptyExists"] = members[1].GetProperty("After").GetProperty("Exists").GetBoolean();
        report["CreatedEmptyLength"] = members[1].GetProperty("After").GetProperty("Length").GetInt64();
        report["DeletedEmptyBeforeExists"] = members[2].GetProperty("Before").GetProperty("Exists").GetBoolean();
        report["DeletedEmptyBeforeLength"] = members[2].GetProperty("Before").GetProperty("Length").GetInt64();
        report["DeletedEmptyAfterExists"] = members[2].GetProperty("After").GetProperty("Exists").GetBoolean();
    }
}
