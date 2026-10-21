using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Observes ordinary existence probes on tiny independent physical fixtures without game initialization.
/// </summary>
public sealed class OrdinaryExistenceTimingTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Selects the destination for every received fixture timing event.
    /// </summary>
    /// <param name="output">
    /// The current test's complete diagnostic output receiver.
    /// </param>
    public OrdinaryExistenceTimingTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Preserves physical presence, original refusals and fixture bytes when timing observers fail.
    /// </summary>
    /// <param name="entry">
    /// A regular file, an absent file or a directory at the requested file boundary.
    /// </param>
    /// <param name="throwingObserver">
    /// Whether every received timing boundary throws after retaining its complete observation.
    /// </param>
    [Theory]
    [InlineData("regular", false)]
    [InlineData("missing", false)]
    [InlineData("directory", false)]
    [InlineData("regular", true)]
    [InlineData("missing", true)]
    [InlineData("directory", true)]
    public void FileExists_PreservesOutcomeAndDataWithCompleteOrFailingObserver(string entry, bool throwingObserver)
    {
        var parent = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "ordinary-existence-timing");
        Directory.CreateDirectory(parent);
        var root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(root));
        Directory.CreateDirectory(root);
        var events = new List<(string Operation, string State, TimeSpan Elapsed)>();
        var observerFailure = new IOException("injected passive existence observer failure");
        Exception? refusal = null;
        bool? result = null;
        try
        {
            var outputDirectory = Path.Combine(root, "game_session", "output");
            Directory.CreateDirectory(outputDirectory);
            var target = Path.Combine(outputDirectory, "subject.json");
            if (entry == "regular") File.WriteAllText(target, "{\"subject\":true}");
            else if (entry == "directory") Directory.CreateDirectory(target);
            else Assert.Equal("missing", entry);
            var before = CaptureFiles(root);
            var directoriesBefore = CaptureDirectories(root);
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    OperationTimingObserver = (name, state, elapsed) =>
                    {
                        events.Add((name, state, elapsed));
                        if (throwingObserver) throw observerFailure;
                    }
                });

            refusal = Record.Exception(() => { result = files.FileExists("output/subject.json"); });
            if (entry == "directory")
            {
                Assert.IsType<InvalidDataException>(refusal);
                Assert.Null(result);
                Assert.DoesNotContain(observerFailure.Message, refusal!.ToString(), StringComparison.Ordinal);
            }
            else
            {
                Assert.Null(refusal);
                Assert.Equal(entry == "regular", result);
            }
            Assert.Equal(before, CaptureFiles(root));
            Assert.Equal(directoriesBefore, CaptureDirectories(root));
            Assert.False(Directory.Exists(Path.Combine(root, ".boe_runtime")));
            Assert.NotEmpty(events);
            var stack = new Stack<string>();
            foreach (var observation in events)
            {
                Assert.True(observation.Elapsed >= TimeSpan.Zero);
                if (observation.State == "begin") stack.Push(observation.Operation);
                else
                {
                    Assert.Equal("end", observation.State);
                    Assert.NotEmpty(stack);
                    Assert.Equal(stack.Pop(), observation.Operation);
                }
            }
            Assert.Empty(stack);
            foreach (var operation in new[]
            {
                "CanonicalExists.FileExists", "OrdinaryExists.Total", "OrdinaryExists.ResolvePath",
                "OrdinaryExists.CanonicalSafety", "OrdinaryExists.PrepareScope",
                "OrdinaryExists.ValidateFileAndPresence", "ReadPublication.RecoverBeforeReadSync",
                "ReadPublication.HasPending", "StorageEvidence.ProbeAndEnumerate"
            })
            {
                Assert.Contains(events, row => row.Operation == operation && row.State == "begin");
                Assert.Contains(events, row => row.Operation == operation && row.State == "end");
            }
        }
        finally
        {
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Entry = entry, ThrowingObserver = throwingObserver, Result = result,
                Refusal = refusal?.ToString(), Events = events.Select(row => new
                { row.Operation, row.State, Milliseconds = row.Elapsed.TotalMilliseconds }).ToArray()
            }));
            var resolvedRoot = Path.GetFullPath(root);
            var expectedParent = Path.GetFullPath(parent) + Path.DirectorySeparatorChar;
            if (!resolvedRoot.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Fixture cleanup target escaped its declared parent.");
            Directory.Delete(resolvedRoot, recursive: true);
        }
    }

    /// <summary>
    /// Captures every ordinary fixture file without requiring a game or generation.
    /// </summary>
    /// <param name="root">
    /// This case's existing independent ordinary directory.
    /// </param>
    /// <returns>
    /// Ordered relative paths with exact length and SHA256 for all files.
    /// </returns>
    private static KeyValuePair<string, string>[] CaptureFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => new KeyValuePair<string, string>(Path.GetRelativePath(root, path),
                new FileInfo(path).Length + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))))
            .OrderBy(row => row.Key, StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Captures every fixture directory including empty ones.
    /// </summary>
    /// <param name="root">
    /// This case's existing independent ordinary directory.
    /// </param>
    /// <returns>
    /// Ordered relative directory paths beneath the fixture root.
    /// </returns>
    private static string[] CaptureDirectories(string root) =>
        Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path)).Order(StringComparer.Ordinal).ToArray();
}
