using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    private sealed partial class Fixture
    {
        /// <summary>
        /// Checks heavy setup coverage over the complete already recorded interval before the first direct copy.
        /// Runs after all existing semantic, refusal, restoration, abort and admission-tree controls.
        /// Requires an original QTE calculation and fresh source proofs around every reused contribution.
        /// </summary>
        /// <param name="directCopy">
        /// The existing direct-copy offsets; only its start selects the preceding setup observations.
        /// No new preparation, copy or authority operation is performed.
        /// </param>
        internal void AssertJointHeavyPreparationTimingTree((int Start, int End) directCopy)
        {
            var rows = ReadAdmissionTimingRange((0, directCopy.Start));
            string[] required =
            [
                "JointOriginal.DetachRequest",
                "JointOriginal.CreateBackup",
                "JointOriginal.CreateSnapshot",
                "JointOriginal.LoadManifest",
                "JointOriginal.AuthenticateSnapshot",
                "OriginalBackup.Total",
                "OriginalBackup.Read:",
                "OriginalSnapshot.Total",
                "OriginalSnapshot.CaptureMembers",
                "OriginalSnapshot.CreateAuthorityJson",
                "JointCommonAuthority.Construct",
                "JointInput.Compose.DetachBundle",
                "JointCommonAuthority.BindInput.ComputePlanningFingerprint",
                "JointBaseline.HasValidSeal",
                "JointFinal.ReplayAgreement",
                "JointQte.PrepareAsync"
            ];
            var missing = required.Where(pattern => !rows.Any(row =>
                row.Boundary == "begin" && MatchesHeavyTimingName(row.Name, pattern))).ToArray();
            Assert.True(missing.Length == 0, "Missing required heavy preparation timing names: " + string.Join(", ", missing));
            var inventory = rows.GroupBy(row => row.Name, StringComparer.Ordinal).Select(group => new
            {
                Name = group.Key,
                Begin = group.Count(row => row.Boundary == "begin"),
                End = group.Count(row => row.Boundary == "end")
            }).ToArray();
            foreach (var entry in inventory)
            {
                Assert.True(entry.Begin > 0, entry.Name);
                Assert.Equal(entry.Begin, entry.End);
            }
            var nodes = ReadStrictAdmissionTree(rows);
            string[][] chains =
            [
                ["JointOriginal.CreateBackup", "OriginalBackup.Total"],
                ["JointOriginal.CreateSnapshot", "OriginalSnapshot.Total"],
                ["OriginalSnapshot.Total", "OriginalSnapshot.CaptureMembers"],
                ["OriginalSnapshot.Total", "OriginalSnapshot.CreateAuthorityJson"],
                ["JointBundleFixture.BindCommonInput", "JointCommon.CreateAuthority", "JointCommonAuthority.Construct"],
                ["JointCommon.ComposeInputAsync", "JointInput.ComposeAsync", "JointInput.Compose.DetachBundle"],
                ["JointCommon.BindInput", "JointCommonAuthority.BindInput.ComputePlanningFingerprint"],
                ["JointBaseline.ValidateSeal", "JointBaseline.HasValidSeal", "JointFinal.ReplayAgreement"],
                ["JointResources.RecomputeQteAsync", "JointQte.ValidateCachedSources", "JointQte.CalculationReused"]
            ];
            foreach (var chain in chains)
                Assert.True(nodes.Any(node => HasAdmissionTimingChain(node, chain, 0)),
                    "Missing heavy preparation timing chain: " + string.Join(" > ", chain));
            var qteRecomputations = nodes.Where(node => node.Name == "JointResources.RecomputeQteAsync").ToArray();
            Assert.NotEmpty(qteRecomputations);
            foreach (var node in qteRecomputations)
            {
                Assert.Equal(2, node.Children.Count(child => child.Name == "JointBaseline.ValidateCurrentAsync"));
                Assert.Single(node.Children, child => child.Name == "ValidationReadSet.ExitProof");
                Assert.True(HasAdmissionTimingChain(node, chains[^1], 0));
                Assert.False(HasHeavyTimingDescendant(node, "JointQte.PrepareAsync"));
                Assert.False(HasHeavyTimingDescendant(node, "JointQte.ChangedCacheSource"));
            }
            Assert.True(nodes.Any(node => node.Name == "OriginalBackup.Total" &&
                HasHeavyTimingDescendant(node, "OriginalBackup.Read:")),
                "Actual per-path backup reads must occur inside an original backup.");
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Kind = "JointHeavyPreparationTimingTree", Start = 0, End = directCopy.Start,
                Events = rows.Length, RequiredPatterns = required, Inventory = inventory,
                StrictLifo = true, DeclaredChains = chains, BackupReadInsideBackup = true,
                QteRecomputations = qteRecomputations.Length, QteBaselineChecksPerReuse = 2,
                QteExitProofsPerReuse = 1, RepeatedQteCalculationAbsent = true,
                OptionalItemMarkers = inventory.Where(entry =>
                    entry.Name == "JointResources.ReplayItemConsumption" ||
                    entry.Name.StartsWith("ItemBaseline.", StringComparison.Ordinal)).ToArray()
            }));
        }

        /// <summary>
        /// Matches an exact required name or the explicitly declared per-path backup-read prefix.
        /// </summary>
        /// <param name="name">
        /// The actual uncapped observer name.
        /// </param>
        /// <param name="pattern">
        /// An exact required name, or the literal <c>OriginalBackup.Read:</c> prefix.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the name matches the declared requirement; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool MatchesHeavyTimingName(string name, string pattern) =>
            pattern == "OriginalBackup.Read:"
                ? name.StartsWith(pattern, StringComparison.Ordinal)
                : name == pattern;

        /// <summary>
        /// Finds a real per-path timing below an already observed parent without assuming intermediate member names.
        /// </summary>
        /// <param name="node">
        /// The observed parent whose descendants are searched.
        /// </param>
        /// <param name="pattern">
        /// The exact name or explicit backup-read prefix required below this parent.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when a descendant matches; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool HasHeavyTimingDescendant(AdmissionTimingNode node, string pattern) =>
            node.Children.Any(child => MatchesHeavyTimingName(child.Name, pattern) ||
                HasHeavyTimingDescendant(child, pattern));
    }
}
