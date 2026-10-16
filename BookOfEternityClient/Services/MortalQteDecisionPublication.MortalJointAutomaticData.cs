using System.Collections.ObjectModel;
using System.Globalization;

namespace BookOfEternityClient.Services;

internal static partial class MortalQteDecisionPublication
{
    // Detached calculation data only. These types issue no source or publication authority.
    internal sealed class JointAutomaticInputs
    {
        internal const string CharacteristicsPath = "game_state/misc/characteristics.json";
        internal const string StatusChangesPath = "game_state/player/status_changes.json";
        internal const string StatPointsPath = "game_state/player/stat_points.json";
        internal const string ExperiencePath = "game_state/player/experience.json";
        internal const string PlayerStatusPath = "game_state/core/player_status.json";
        internal const string SoulPath = "game_state/meta/soul_state.json";
        private readonly IReadOnlyDictionary<string, CanonicalBeforeImage> _images;
        internal JointAutomaticInputs(byte[] characteristics, byte[]? changes, byte[] points,
            byte[]? experience, byte[]? player, byte[]? soul)
        {
            ArgumentNullException.ThrowIfNull(characteristics);
            ArgumentNullException.ThrowIfNull(points);
            var images = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
            foreach (var (path, bytes) in new (string, byte[]?)[]
            {
                (CharacteristicsPath, characteristics), (StatusChangesPath, changes), (StatPointsPath, points),
                (ExperiencePath, experience), (PlayerStatusPath, player), (SoulPath, soul)
            }) images.Add(path, new(bytes is not null, bytes));
            _images = new ReadOnlyDictionary<string, CanonicalBeforeImage>(images);
            var fields = new List<string?> { "book_of_eternity.mortal_joint_automatic.inputs.v1" };
            foreach (var row in images.OrderBy(row => row.Key, StringComparer.Ordinal))
            { fields.Add(row.Key); fields.Add(row.Value.Fingerprint); }
            Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }
        internal string Fingerprint { get; }
        internal byte[]? ReadSourceBytes(string path) => _images.TryGetValue(path, out var image)
            ? image.Bytes : throw new ArgumentException("Not an automatic calculation source.", nameof(path));
    }

    internal sealed class JointAutomaticPlan
    {
        internal JointAutomaticPlan(JointAutomaticInputs inputs, int trainingLevel, int awardLevel,
            int processedBefore, int unspentBefore, int pointsAwarded, IEnumerable<string> ordered,
            IEnumerable<string> applied, IEnumerable<string> blocked, IEnumerable<string> ignored,
            byte[] characteristicsAfter, byte[]? changesAfter, byte[] pointsAfter)
        {
            TrainingLevel = trainingLevel; AwardLevel = awardLevel; ProcessedBefore = processedBefore;
            UnspentBefore = unspentBefore; PointsAwarded = pointsAwarded;
            OrderedStats = Array.AsReadOnly(ordered.ToArray()); AppliedStats = Array.AsReadOnly(applied.ToArray());
            BlockedStats = Array.AsReadOnly(blocked.ToArray()); IgnoredStats = Array.AsReadOnly(ignored.ToArray());
            AfterImages = new ReadOnlyDictionary<string, CanonicalBeforeImage>(new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
            {
                [JointAutomaticInputs.CharacteristicsPath] = new(true, characteristicsAfter),
                [JointAutomaticInputs.StatusChangesPath] = new(changesAfter is not null, changesAfter),
                [JointAutomaticInputs.StatPointsPath] = new(true, pointsAfter)
            });
            var fields = new List<string?> { "book_of_eternity.mortal_joint_automatic.mutation.v1", inputs.Fingerprint };
            foreach (var n in new[] { trainingLevel, awardLevel, processedBefore, unspentBefore, pointsAwarded })
                fields.Add(n.ToString(CultureInfo.InvariantCulture));
            foreach (var list in new[] { OrderedStats, AppliedStats, BlockedStats, IgnoredStats })
            { fields.Add(list.Count.ToString(CultureInfo.InvariantCulture)); fields.AddRange(list); }
            foreach (var row in AfterImages.OrderBy(row => row.Key, StringComparer.Ordinal))
            { fields.Add(row.Key); fields.Add(row.Value.Fingerprint); }
            MutationFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }
        internal int TrainingLevel { get; }
        internal int AwardLevel { get; }
        internal int ProcessedBefore { get; }
        internal int UnspentBefore { get; }
        internal int PointsAwarded { get; }
        internal IReadOnlyList<string> OrderedStats { get; }
        internal IReadOnlyList<string> AppliedStats { get; }
        internal IReadOnlyList<string> BlockedStats { get; }
        internal IReadOnlyList<string> IgnoredStats { get; }
        internal string MutationFingerprint { get; }
        internal IReadOnlyDictionary<string, CanonicalBeforeImage> AfterImages { get; }
    }
}
