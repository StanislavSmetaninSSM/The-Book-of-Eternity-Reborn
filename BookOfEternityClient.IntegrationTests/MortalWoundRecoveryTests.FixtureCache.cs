using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundRecoveryTests
{
    /// <summary>
    /// Reuses genuine signed preparation while proving independent roots, fresh
    /// admission generations and rejection of local snapshot tampering.
    /// </summary>
    [Fact]
    public void FixtureCache_ReusesSignedBytesWithFreshAuthorityAndIsolatedTamperRejection()
    {
        var scenario = Scenario.MultiCadenceJump();
        var authored = scenario.Wound.DeepClone().AsObject();
        authored["display"]!["name"] = "recovery_cache_contract_" + Guid.NewGuid().ToString("N");
        scenario = scenario with { Wound = authored };
        var first = Fixture.Create(scenario);
        Fixture? firstToDispose = first;
        try
        {
            using var second = Fixture.Create(scenario);
            Assert.Equal(1, Fixture.GetPreparationCount(scenario));
            Assert.NotEqual(first.FileSystem.BasePath, second.FileSystem.BasePath);
            Assert.NotSame(first.FileSystem, second.FileSystem);
            Assert.NotSame(first.Lease, second.Lease);
            Assert.NotSame(first.FileSystem.CanonicalRootAuthorityIdentity,
                second.FileSystem.CanonicalRootAuthorityIdentity);
            var firstAuthority = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                Required(first.ExportCurrentResult(), "Authority"));
            var secondAuthority = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                Required(second.ExportCurrentResult(), "Authority"));
            Assert.NotSame(firstAuthority, secondAuthority);
            Assert.NotEqual(firstAuthority.SessionGeneration, secondAuthority.SessionGeneration);
            Assert.NotEqual(firstAuthority.AcceptedStateFingerprint, secondAuthority.AcceptedStateFingerprint);
            Assert.True(firstAuthority.HasCurrentAdmissionAuthority());
            Assert.True(secondAuthority.HasCurrentAdmissionAuthority());
            Assert.False(firstAuthority.IsLeaseBoundTo(second.FileSystem, second.Lease));
            Assert.False(secondAuthority.IsLeaseBoundTo(first.FileSystem, first.Lease));
            first.AssertCarrierIdentityHistoryAgreement();
            second.AssertCarrierIdentityHistoryAgreement();
            var snapshotPath = LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/" +
                WoundCarrierCatalog.PlayerPath;
            var originalSnapshot = File.ReadAllBytes(second.FileSystem.ResolvePath(snapshotPath));
            File.WriteAllBytes(first.FileSystem.ResolvePath(snapshotPath), Encoding.UTF8.GetBytes("{}"));
            var rejected = first.ExportCurrentResult();
            Assert.False(Assert.IsType<bool>(Required(rejected, "IsValid")));
            Assert.Null(Optional(rejected, "Authority"));
            Assert.NotEmpty(Values(rejected, "Issues"));
            Assert.True(Assert.IsType<bool>(Required(second.ExportCurrentResult(), "IsValid")));
            Assert.Equal(originalSnapshot, File.ReadAllBytes(second.FileSystem.ResolvePath(snapshotPath)));
            var retiredRoot = first.FileSystem.BasePath;
            first.Dispose();
            firstToDispose = null;
            Assert.False(Directory.Exists(retiredRoot));
            Assert.False(firstAuthority.HasCurrentAdmissionAuthority());
            using var third = Fixture.Create(scenario);
            Assert.Equal(1, Fixture.GetPreparationCount(scenario));
            Assert.Equal(originalSnapshot, File.ReadAllBytes(third.FileSystem.ResolvePath(snapshotPath)));
            var thirdAuthority = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                Required(third.ExportCurrentResult(), "Authority"));
            Assert.NotEqual(secondAuthority.SessionGeneration, thirdAuthority.SessionGeneration);
            Assert.True(thirdAuthority.HasCurrentAdmissionAuthority());
            third.AssertCarrierIdentityHistoryAgreement();
        }
        finally
        {
            firstToDispose?.Dispose();
        }
    }

    private sealed partial class Fixture
    {
        private static readonly ConcurrentDictionary<string, Lazy<PreparedRecoveryFixture>> PreparedRecoveryTemplates =
            new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, int> PreparationCounts = new(StringComparer.Ordinal);

        /// <summary>
        /// Stores only portable completed session bytes and detached comparison data.
        /// </summary>
        /// <param name="Tree">
        /// The genuine signed game-session corpus, excluding runtime locks and session generation.
        /// </param>
        /// <param name="WoundId">
        /// The exact wound allocated by genuine creation.
        /// </param>
        /// <param name="InitialDeteriorationAnchorBytes">
        /// Private creation-anchor comparison bytes, or null when creation had no condition epoch.
        /// </param>
        /// <param name="PostStabilizationDeteriorationAnchorBytes">
        /// Private settled-anchor comparison bytes, or null when no condition epoch remains.
        /// </param>
        private sealed record PreparedRecoveryFixture(PreparedFixtureTree Tree, string WoundId,
            byte[]? InitialDeteriorationAnchorBytes, byte[]? PostStabilizationDeteriorationAnchorBytes);

        /// <summary>
        /// Keys every input read by completed fixture preparation using its exact serialized bytes.
        /// Expected outcomes and filesystem hooks do not describe shared preparation.
        /// </summary>
        /// <param name="scenario">
        /// The complete authored setup and creation, stabilization and evaluation clocks.
        /// </param>
        /// <returns>
        /// A deterministic private cache key preserving JSON array order and exact source text.
        /// </returns>
        private static string GetPreparationKey(Scenario scenario) => WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "mortal_wound_recovery_test_preparation", "1", scenario.Wound.ToJsonString(),
            scenario.WorldTime.ToJsonString(), scenario.CreationMinute.ToString(CultureInfo.InvariantCulture),
            scenario.StabilizationMinute?.ToString(CultureInfo.InvariantCulture),
            scenario.Minute.ToString(CultureInfo.InvariantCulture),
            scenario.StartsStabilized ? "true" : "false", scenario.NoMechanics ? "true" : "false",
            scenario.IncludeSecondCreationRoot ? "true" : "false"
        });

        /// <summary>
        /// Reads the number of genuine preparation factories invoked for this exact setup.
        /// </summary>
        /// <param name="scenario">
        /// The exact setup whose isolated cache profile is counted.
        /// </param>
        /// <returns>
        /// Zero before preparation, otherwise the number of invoked factories for that profile.
        /// </returns>
        internal static int GetPreparationCount(Scenario scenario) =>
            PreparationCounts.TryGetValue(GetPreparationKey(scenario), out var count) ? count : 0;

        /// <summary>
        /// Captures a completed genuine preparation and disposes every source root and live owner.
        /// </summary>
        /// <param name="scenario">
        /// Detached setup inputs fixed when the profile was selected.
        /// </param>
        /// <param name="key">
        /// The exact setup key used only for deterministic preparation accounting.
        /// </param>
        /// <returns>
        /// Portable signed session bytes and private comparison copies without runtime capabilities.
        /// </returns>
        private static PreparedRecoveryFixture PrepareRecoveryTemplate(Scenario scenario, string key)
        {
            PreparationCounts.AddOrUpdate(key, 1, static (_, count) => checked(count + 1));
            using var original = CreateUncached(scenario);
            return new(PreparedFixtureTree.Capture(original.FileSystem.GameSessionPath), original.WoundId,
                original.InitialDeteriorationAnchorBytes?.ToArray(),
                original.PostStabilizationDeteriorationAnchorBytes?.ToArray());
        }

        /// <summary>
        /// Copies exact signed preparation into fresh physical ownership and authenticates
        /// new recovery authority without importing any cached lease, registry or plan.
        /// </summary>
        /// <param name="scenario">
        /// The caller's complete setup and expected-result metadata.
        /// </param>
        /// <param name="hooks">
        /// Optional hooks attached only to the fresh materialized filesystem; null uses ordinary I/O.
        /// </param>
        /// <returns>
        /// An independently disposable fixture with its own canonical lease and accepted binding.
        /// </returns>
        private static Fixture CreateFromPreparedTemplate(Scenario scenario, FileSystemManagerHooks? hooks)
        {
            var key = GetPreparationKey(scenario);
            var detached = scenario with
            {
                Wound = scenario.Wound.DeepClone().AsObject(),
                WorldTime = scenario.WorldTime.DeepClone().AsObject(),
                IntentTypes = scenario.IntentTypes.ToArray()
            };
            var prepared = PreparedRecoveryTemplates.GetOrAdd(key, _ =>
                new Lazy<PreparedRecoveryFixture>(() => PrepareRecoveryTemplate(detached, key),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value;
            var root = Path.Combine(Path.GetTempPath(), "boe-t062-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, hooks);
            FileSystemManager.CanonicalWriteLease? lease = null;
            try
            {
                fs.EnsureDirectoryStructure();
                prepared.Tree.Materialize(fs.GameSessionPath);
                lease = fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
                var binding = ExportRecoveryBinding(fs, lease, prepared.WoundId);
                AssertLiveTurnCorrelation(fs, binding, input: null);
                return new(root, fs, lease, binding, prepared.WoundId, scenario,
                    prepared.InitialDeteriorationAnchorBytes?.ToArray(),
                    prepared.PostStabilizationDeteriorationAnchorBytes?.ToArray());
            }
            catch
            {
                if (lease is not null) lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
                Directory.Delete(root, recursive: true);
                throw;
            }
        }
    }
}
