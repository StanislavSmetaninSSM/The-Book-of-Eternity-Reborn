namespace BookOfEternityClient.Services;

internal static partial class MortalQteDecisionPublication
{
    // Detached calculation data only. These types issue no source or publication authority.
    internal sealed class JointAutomaticInputs
    {
        internal const string CharacteristicsPath = "game_state/misc/characteristics.json";
        internal const string StatusChangesPath = "game_state/player/status_changes.json";
        internal const string StatPointsPath = "game_state/player/stat_points.json";
        internal JointAutomaticInputs(byte[] characteristics, byte[]? changes, byte[] points,
            byte[]? experience, byte[]? player, byte[]? soul) { }
        internal string Fingerprint => throw new NotImplementedException();
        internal byte[]? ReadSourceBytes(string path) => throw new NotImplementedException();
    }

    internal sealed class JointAutomaticPlan
    {
        internal int TrainingLevel => throw new NotImplementedException();
        internal int AwardLevel => throw new NotImplementedException();
        internal int PointsAwarded => throw new NotImplementedException();
        internal IReadOnlyList<string> OrderedStats => throw new NotImplementedException();
        internal IReadOnlyList<string> AppliedStats => throw new NotImplementedException();
        internal IReadOnlyList<string> BlockedStats => throw new NotImplementedException();
        internal IReadOnlyList<string> IgnoredStats => throw new NotImplementedException();
        internal string MutationFingerprint => throw new NotImplementedException();
        internal IReadOnlyDictionary<string, CanonicalBeforeImage> AfterImages => throw new NotImplementedException();
    }
}
