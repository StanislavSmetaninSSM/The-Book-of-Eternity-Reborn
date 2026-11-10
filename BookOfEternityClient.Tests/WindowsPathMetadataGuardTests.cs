using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the production walker with detached metadata DATA and explicit error sequences.
/// </summary>
public sealed class WindowsPathMetadataGuardTests
{
    /// <summary>
    /// Preserves root directory-only handling and segment directory-first short circuiting.
    /// </summary>
    [Fact]
    public void RootFalseStillWalksSegmentsAndBothFalseStops()
    {
        var root = Path.GetFullPath("metadata-detached-root");
        var child = Path.Combine(root, "child");
        var provider = new ScriptedProvider();
        provider.Steps.Enqueue(("directory", root, new(false, null)));
        provider.Steps.Enqueue(("directory", child, new(false, null)));
        provider.Steps.Enqueue(("file", child, new(false, null)));

        WindowsPathMetadataGuard.Ensure(root, Path.Combine(child, "unvisited"), "Detached", provider);

        Assert.Empty(provider.Steps);
        Assert.Equal(new[] { "directory", "directory", "file" }, provider.Calls);
    }

    /// <summary>
    /// Restricts root-equals-path to a single directory observation, even for <see langword="false"/> presence.
    /// </summary>
    [Fact]
    public void RootEqualsPathNeverQueriesRootAsFile()
    {
        var root = Path.GetFullPath("metadata-detached-root");
        var provider = new ScriptedProvider();
        provider.Steps.Enqueue(("directory", root, new(false, FileAttributes.ReparsePoint)));
        WindowsPathMetadataGuard.Ensure(root, root, "Detached", provider);
        Assert.Equal(new[] { "directory" }, provider.Calls);
        Assert.Empty(provider.Steps);
    }

    /// <summary>
    /// Uses ordinary matching-kind cached attributes without adding a final attributes query.
    /// </summary>
    [Fact]
    public void OrdinaryCachedDirectoryAndFileSkipOnlyFinalReparseQuery()
    {
        var root = Path.GetFullPath("metadata-detached-root");
        var child = Path.Combine(root, "file");
        var provider = new ScriptedProvider();
        provider.Steps.Enqueue(("directory", root, new(true, FileAttributes.Directory)));
        provider.Steps.Enqueue(("directory", child, new(false, null)));
        provider.Steps.Enqueue(("file", child, new(true, FileAttributes.Normal)));
        WindowsPathMetadataGuard.Ensure(root, child, "Detached", provider);
        Assert.Equal(new[] { "directory", "directory", "file" }, provider.Calls);
        Assert.Empty(provider.Steps);
    }

    /// <summary>
    /// Lets the original reparse query decide after reparse or uncertain positive DATA.
    /// </summary>
    /// <param name="attributes">
    /// Cached attributes; <see langword="null"/> represents unavailable attributes and zero a mismatched directory kind.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData((int)(FileAttributes.Directory | FileAttributes.ReparsePoint))]
    public void ReparseOrUncertainPositiveUsesOriginalQuery(int? attributes)
    {
        var root = Path.GetFullPath("metadata-detached-root");
        var provider = new ScriptedProvider { Reparse = false };
        provider.Steps.Enqueue(("directory", root,
            new(true, attributes.HasValue ? (FileAttributes)attributes.Value : null)));
        WindowsPathMetadataGuard.Ensure(root, root, "Detached", provider);
        Assert.Equal(new[] { "directory", "reparse" }, provider.Calls);
    }

    /// <summary>
    /// Preserves the original root and segment refusal messages when that query observes a link.
    /// </summary>
    /// <param name="rootLink">
    /// <see langword="true"/> selects the root refusal; <see langword="false"/> selects the segment refusal.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OriginalReparseDecisionPreservesAuthorityAndPath(bool rootLink)
    {
        var root = Path.GetFullPath("metadata-detached-root");
        var child = Path.Combine(root, "child");
        var provider = new ScriptedProvider { Reparse = true };
        provider.Steps.Enqueue(("directory", root,
            new(true, rootLink ? null : FileAttributes.Directory)));
        if (!rootLink)
            provider.Steps.Enqueue(("directory", child, new(true, null)));
        var error = Assert.Throws<InvalidDataException>(() =>
            WindowsPathMetadataGuard.Ensure(root, child, "Detached", provider));
        Assert.Equal(rootLink ? "Detached root cannot be a reparse point."
            : $"Detached path traverses reparse point '{child}'.", error.Message);
    }

    /// <summary>
    /// Executes the legacy component decision after expected observation errors and preserves final error identity.
    /// </summary>
    /// <param name="failureKind">
    /// Selects the expected new-observation exception: argument, IO or access failure.
    /// </param>
    [Theory]
    [InlineData("argument")]
    [InlineData("io")]
    [InlineData("access")]
    public void ObservationFailureFallsBackWithoutCatchingOriginalFailure(string failureKind)
    {
        var root = Path.GetFullPath("metadata-detached-root");
        Exception observationError = failureKind switch
        {
            "argument" => new ArgumentException("new observation"),
            "io" => new IOException("new observation"),
            _ => new UnauthorizedAccessException("new observation")
        };
        var originalError = new IOException("original final attributes failure");
        var provider = new ScriptedProvider
        {
            ObservationError = observationError,
            LegacyDirectory = true,
            ReparseError = originalError
        };
        var actual = Assert.Throws<IOException>(() =>
            WindowsPathMetadataGuard.Ensure(root, root, "Detached", provider));
        Assert.Same(originalError, actual);
        Assert.Equal(new[] { "directory", "legacy-directory", "reparse" }, provider.Calls);
    }

    /// <summary>
    /// Preserves directory-first legacy segment fallback, including the original file-positive branch.
    /// </summary>
    [Fact]
    public void SegmentFallbackRepeatsExactLegacyDecisionOutsideObservationCatch()
    {
        var root = Path.GetFullPath("metadata-detached-root");
        var child = Path.Combine(root, "child");
        var provider = new ScriptedProvider { LegacyFile = true };
        provider.Steps.Enqueue(("directory", root, new(true, FileAttributes.Directory)));
        provider.ObservationErrorPath = child;
        provider.ObservationError = new IOException("new observation");
        WindowsPathMetadataGuard.Ensure(root, child, "Detached", provider);
        Assert.Equal(new[] { "directory", "directory", "legacy-directory", "legacy-file", "reparse" },
            provider.Calls);
    }

    /// <summary>
    /// Propagates unexpected managed cancellation without turning it into <see langword="false"/> presence or fallback.
    /// </summary>
    [Fact]
    public void ManagedCancellationPropagatesByIdentity()
    {
        var root = Path.GetFullPath("metadata-detached-root");
        var expected = new OperationCanceledException("managed failure, not stored native status");
        var provider = new ScriptedProvider { ObservationError = expected };
        Assert.Same(expected, Assert.Throws<OperationCanceledException>(() =>
            WindowsPathMetadataGuard.Ensure(root, root, "Detached", provider)));
        Assert.Equal(new[] { "directory" }, provider.Calls);
    }

    /// <summary>
    /// Requires independent guard calls to consume fresh observations instead of reusing earlier DATA.
    /// </summary>
    [Fact]
    public void IndependentCallsObserveReplacementAndPreserveFinalFailureIdentity()
    {
        var root = Path.GetFullPath("metadata-detached-root");
        var originalError = new UnauthorizedAccessException("fresh original query");
        var provider = new ScriptedProvider { ReparseError = originalError };
        provider.Steps.Enqueue(("directory", root, new(true, FileAttributes.Directory)));
        provider.Steps.Enqueue(("directory", root, new(true, FileAttributes.Directory | FileAttributes.ReparsePoint)));
        WindowsPathMetadataGuard.Ensure(root, root, "Detached", provider);
        Assert.Same(originalError, Assert.Throws<UnauthorizedAccessException>(() =>
            WindowsPathMetadataGuard.Ensure(root, root, "Detached", provider)));
        Assert.Equal(new[] { "directory", "directory", "reparse" }, provider.Calls);
    }

    /// <summary>
    /// Supplies detached DATA and errors while checking the walker's physical-query order and path arguments.
    /// </summary>
    private sealed class ScriptedProvider : IWindowsPathMetadataProvider
    {
        /// <summary>
        /// Queues exact query kinds, component spellings and detached observation DATA.
        /// </summary>
        internal readonly Queue<(string Kind, string Path, PathMetadataObservation Observation)> Steps = new();

        /// <summary>
        /// Retains every query in order for the walker's branch assertions.
        /// </summary>
        internal readonly List<string> Calls = new();

        /// <summary>
        /// Supplies a new-observation failure; <see langword="null"/> permits queued observations.
        /// </summary>
        internal Exception? ObservationError;

        /// <summary>
        /// Restricts the observation failure to one spelling; <see langword="null"/> applies it to every observation.
        /// </summary>
        internal string? ObservationErrorPath;

        /// <summary>
        /// Supplies the original final-query failure; <see langword="null"/> permits the configured boolean result.
        /// </summary>
        internal Exception? ReparseError;

        /// <summary>
        /// Returns <see langword="true"/> from the original final query when selected; the default <see langword="false"/> reports an ordinary entry.
        /// </summary>
        internal bool Reparse;

        /// <summary>
        /// Returns the selected legacy directory presence; the default <see langword="false"/> permits the segment file query.
        /// </summary>
        internal bool LegacyDirectory;

        /// <summary>
        /// Returns the selected legacy file presence; the default <see langword="false"/> reports absence.
        /// </summary>
        internal bool LegacyFile;

        /// <inheritdoc />
        public PathMetadataObservation ObserveDirectory(string path, PathMetadataRole role) => Observe("directory", path);

        /// <inheritdoc />
        public PathMetadataObservation ObserveFile(string path, PathMetadataRole role) => Observe("file", path);

        /// <inheritdoc />
        public bool LegacyDirectoryExists(string path, PathMetadataRole role)
        {
            Calls.Add("legacy-directory");
            return LegacyDirectory;
        }

        /// <inheritdoc />
        public bool LegacyFileExists(string path, PathMetadataRole role)
        {
            Calls.Add("legacy-file");
            return LegacyFile;
        }

        /// <inheritdoc />
        public bool IsReparsePoint(string path, PathMetadataRole role)
        {
            Calls.Add("reparse");
            if (ReparseError != null)
                throw ReparseError;
            return Reparse;
        }

        /// <summary>
        /// Returns the next explicitly matched observation or throws its selected observation failure.
        /// </summary>
        /// <param name="kind">
        /// The directory or file query selected by the walker.
        /// </param>
        /// <param name="path">
        /// The component path, which must match the queued expectation.
        /// </param>
        /// <returns>
        /// The queued immutable presence and optional attributes DATA.
        /// </returns>
        private PathMetadataObservation Observe(string kind, string path)
        {
            Calls.Add(kind);
            if (ObservationError != null && (ObservationErrorPath == null || ObservationErrorPath == path))
                throw ObservationError;
            var expected = Steps.Dequeue();
            Assert.Equal(expected.Kind, kind);
            Assert.Equal(expected.Path, path);
            return expected.Observation;
        }
    }
}
