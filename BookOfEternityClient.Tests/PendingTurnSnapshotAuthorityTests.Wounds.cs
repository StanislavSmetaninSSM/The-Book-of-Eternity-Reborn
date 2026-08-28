using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PendingTurnSnapshotAuthorityTests
{
    public static IEnumerable<object[]> FoundationalWoundSnapshotPaths =>
        WoundAcceptedTurnSnapshotContract.RequiredPaths
            .Select(static path => new object[] { path });

    [Fact]
    public void WoundSnapshotContract_ContainsExactFoundationalAuthorityPaths()
    {
        var expected = new[]
        {
            WoundCarrierCatalog.PlayerPath,
            WoundCarrierCatalog.NpcPath,
            WoundCarrierCatalog.EnemiesPath,
            WoundCarrierCatalog.AlliesPath,
            WoundCarrierCatalog.AfterlifeProfilesPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            AcceptedMechanicsPlan.WoundCommandPath,
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
            ProgressionScheduleService.SchedulePath,
            ProgressionScheduleService.ReportPath,
            "output/narrative_response.json",
            "output/interface_updates.json",
            "output/debug_logs.json"
        };

        Assert.Equal(
            expected.OrderBy(static path => path, StringComparer.Ordinal),
            WoundAcceptedTurnSnapshotContract.RequiredPaths);
        Assert.Equal(
            expected.Length,
            WoundAcceptedTurnSnapshotContract.RequiredPaths
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void HasValidatedRollbackSnapshotCoverage_AllPresentWoundPaths_Accepts()
    {
        var manifest = CreateWoundCoverageManifest();

        var covered = PendingTurnSnapshotAuthority
            .HasValidatedRollbackSnapshotCoverage(
                manifest,
                static value => value.Files,
                static value => value.SnapshotFileHashes,
                static value => value.RollbackBaselineFiles,
                WoundAcceptedTurnSnapshotContract.RequiredPaths,
                out var missingPath);

        Assert.True(covered);
        Assert.Null(missingPath);
    }

    [Theory]
    [MemberData(nameof(FoundationalWoundSnapshotPaths))]
    public void HasValidatedRollbackSnapshotCoverage_PresentPathWithoutSnapshot_FailsClosed(
        string path)
    {
        var manifest = CreateWoundCoverageManifest();
        manifest.Files.Remove(path);
        manifest.SnapshotFileHashes.Remove(path);

        var covered = PendingTurnSnapshotAuthority
            .HasValidatedRollbackSnapshotCoverage(
                manifest,
                static value => value.Files,
                static value => value.SnapshotFileHashes,
                static value => value.RollbackBaselineFiles,
                WoundAcceptedTurnSnapshotContract.RequiredPaths,
                out var missingPath);

        Assert.False(covered);
        Assert.Equal(path, missingPath);
    }

    [Fact]
    public void HasValidatedRollbackSnapshotCoverage_SignedAbsenceOfNewSurfaces_Accepts()
    {
        var manifest = CreateWoundCoverageManifest();
        var absentPaths = new[]
        {
            AcceptedMechanicsPlan.WoundCommandPath,
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
            "output/narrative_response.json",
            "output/interface_updates.json",
            "output/debug_logs.json"
        };
        foreach (var path in absentPaths)
        {
            manifest.RollbackBaselineFiles.Remove(path);
            manifest.Files.Remove(path);
            manifest.SnapshotFileHashes.Remove(path);
        }

        var covered = PendingTurnSnapshotAuthority
            .HasValidatedRollbackSnapshotCoverage(
                manifest,
                static value => value.Files,
                static value => value.SnapshotFileHashes,
                static value => value.RollbackBaselineFiles,
                WoundAcceptedTurnSnapshotContract.RequiredPaths,
                out var missingPath);

        Assert.True(covered);
        Assert.Null(missingPath);
    }

    [Theory]
    [MemberData(nameof(FoundationalWoundSnapshotPaths))]
    public void WoundSnapshotContract_MissingPlanBeforeImage_FailsClosed(string path)
    {
        var beforeImages = WoundAcceptedTurnSnapshotContract.RequiredPaths
            .ToDictionary(
                static value => value,
                static _ => new CanonicalBeforeImage(existed: false, bytes: null),
                StringComparer.Ordinal);
        beforeImages.Remove(path);

        var complete = WoundAcceptedTurnSnapshotContract.HasCompleteBeforeImages(
            beforeImages,
            out var missingPath);

        Assert.False(complete);
        Assert.Equal(path, missingPath);
    }

    [Fact]
    public void WoundSnapshotContract_ConfusableBeforeImagePath_FailsClosed()
    {
        var beforeImages = WoundAcceptedTurnSnapshotContract.RequiredPaths
            .ToDictionary(
                static value => value,
                static _ => new CanonicalBeforeImage(existed: false, bytes: null),
                StringComparer.OrdinalIgnoreCase);
        beforeImages.Remove(WoundIdentityState.StatePath);
        beforeImages[WoundIdentityState.StatePath.ToUpperInvariant()] =
            new CanonicalBeforeImage(existed: false, bytes: null);

        var complete = WoundAcceptedTurnSnapshotContract.HasCompleteBeforeImages(
            beforeImages,
            out var missingPath);

        Assert.False(complete);
        Assert.Equal(WoundIdentityState.StatePath, missingPath);
    }

    [Fact]
    public void WoundSnapshotContract_BuildRequiredPaths_UnionsDynamicPlanPaths()
    {
        const string dynamicQuestPath = "game_state/quests/regular_quests.json";

        var paths = WoundAcceptedTurnSnapshotContract.BuildRequiredPaths(
            new[] { dynamicQuestPath, WoundCarrierCatalog.PlayerPath });

        Assert.Contains(dynamicQuestPath, paths);
        Assert.Equal(
            WoundAcceptedTurnSnapshotContract.RequiredPaths.Count + 1,
            paths.Count);
        Assert.Equal(
            paths.OrderBy(static path => path, StringComparer.Ordinal),
            paths);
    }

    [Theory]
    [InlineData("input/turn_request.json")]
    [InlineData("../output/narrative_response.json")]
    [InlineData("game_state\\wounds\\wound_history.json")]
    public void WoundSnapshotContract_BuildRequiredPaths_RejectsArbitraryPath(
        string path)
    {
        Assert.Throws<ArgumentException>(() =>
            WoundAcceptedTurnSnapshotContract.BuildRequiredPaths(new[] { path }));
    }

    private static TestManifest CreateWoundCoverageManifest()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in WoundAcceptedTurnSnapshotContract.RequiredPaths)
        {
            files[path] = "game_state/control/pending_turn_snapshot/" + path;
            hashes[path] = "ABC123";
        }

        return new TestManifest
        {
            SessionId = "session-wound-snapshot",
            RequestId = "request-wound-snapshot",
            TurnNumber = 42,
            RequestTimestamp = "2026-08-28T00:00:00Z",
            PlayerAction = "wound snapshot authority test",
            Files = files,
            SnapshotFileHashes = hashes,
            RollbackBaselineFiles = WoundAcceptedTurnSnapshotContract.RequiredPaths.ToList(),
            SourceLabel = "wound-snapshot-authority-tests"
        };
    }
}
