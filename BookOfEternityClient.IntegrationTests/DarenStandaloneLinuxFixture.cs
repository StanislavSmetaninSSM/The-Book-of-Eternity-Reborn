using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

// Each case owns its root, current-schema bootstrap, original lease and consumers.
internal sealed class DarenStandaloneLinuxFixture(ITestOutputHelper output) : IDisposable
{
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "boe-daren-standalone-linux-" + Guid.NewGuid().ToString("N"));
    internal FileSystemManager Files { get; private set; } = null!;
    internal StateManager State { get; private set; } = null!;
    internal DarenQteRewardProfileService Profile => new(Files);
    internal string ProfilePath => Path.Combine(Root, DarenQteRewardProfileService.ProfileRelativePath);
    internal string JournalRoot => Path.Combine(Files.RuntimeRootPath, "trusted-local-publication-v1");
    internal string ActiveJournal => Path.Combine(JournalRoot, "active.json");
    internal Func<string, Task>? Mutation { get; set; }
    internal Action<TrustedLocalPublicationPhase, int>? Publication { get; set; }
    internal int ProfileMutationBoundaries { get; private set; }

    internal async Task InitializeAsync(bool bootstrap = true)
    {
        Assert.True(OperatingSystem.IsLinux(), "Linux body execution required");
        Directory.CreateDirectory(Root);
        Files = new(Root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (path == "@daren_reward_profile") ProfileMutationBoundaries++;
                    return Mutation?.Invoke(path) ?? Task.CompletedTask;
                },
                LocalPublicationObserver = (phase, index) => Publication?.Invoke(phase, index)
            });
        State = new(Files, new GameSettings(), NullLogger<StateManager>.Instance);
        if (bootstrap) await State.BootstrapLocalStorageAsync();
    }

    internal FileSystemManager Fresh() => new(Root, NullLogger<FileSystemManager>.Instance);

    internal void AssertPublisherClean()
    {
        Assert.False(File.Exists(ActiveJournal));
        if (Directory.Exists(JournalRoot)) Assert.Empty(Directory.EnumerateFileSystemEntries(JournalRoot));
        Assert.Empty(Directory.GetFiles(Root, ".boe-local-*", SearchOption.AllDirectories));
    }

    public void Dispose()
    {
        Mutation = null; Publication = null;
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        output.WriteLine("FixtureCleanup root=" + Root + " rootRemoved=" + !Directory.Exists(Root));
    }
}
