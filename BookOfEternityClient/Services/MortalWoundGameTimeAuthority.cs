using System.Collections.ObjectModel;
using System.Globalization;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundGameTimeAuthorityResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundGameTimeAuthority? Authority);

internal sealed class MortalWoundGameTimeAuthority
{
    internal const string CanonicalClockKind = "world_time.currentTimeInMinutes";
    internal const string CanonicalSourcePath = "game_state/world/world_time.json";
    private const string AuthorityDomain =
        "book_of_eternity.mortal_wound_treatment.game_time_authority";

    private MortalWoundGameTimeAuthority(
        long currentTimeInMinutes,
        string coordinatesFingerprint,
        string acceptedStateFingerprint,
        string authorityFingerprint)
    {
        CurrentTimeInMinutes = currentTimeInMinutes;
        CoordinatesFingerprint = coordinatesFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        AuthorityFingerprint = authorityFingerprint;
    }

    public string ClockKind => CanonicalClockKind;
    public string SourcePath => CanonicalSourcePath;
    public long CurrentTimeInMinutes { get; }
    public string CoordinatesFingerprint { get; }
    public string AcceptedStateFingerprint { get; }
    public string AuthorityFingerprint { get; }

    internal static MortalWoundGameTimeAuthorityResult Create(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates)
    {
        var issues = new List<ValidationIssue>();
        if (acceptedState is null ||
            coordinates is null ||
            !coordinates.MatchesAcceptedState(acceptedState))
        {
            Add(
                issues,
                "treatmentAttempt.gameTime",
                "mortal_wound_treatment_game_time_coordinates_invalid",
                "current accepted state and its exact sealed attempt coordinates",
                acceptedState is null
                    ? "missing accepted state"
                    : coordinates is null
                        ? "missing coordinates"
                        : "foreign, stale, or malformed coordinates");
            return Failure(issues);
        }

        var minute = acceptedState.CurrentGameMinute;
        var expectedClockFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "mortal_wound_treatment_clock",
                "1",
                CanonicalSourcePath,
                minute.ToString(CultureInfo.InvariantCulture)
            });
        if (minute < 0 || !string.Equals(
                expectedClockFingerprint,
                acceptedState.ClockFingerprint,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                CanonicalSourcePath,
                "mortal_wound_treatment_game_time_clock_invalid",
                "the exact non-negative accepted canonical world minute",
                minute.ToString(CultureInfo.InvariantCulture));
            return Failure(issues);
        }

        var authorityFingerprint = ComputeFingerprint(
            minute,
            coordinates.CoordinatesFingerprint,
            acceptedState.AcceptedStateFingerprint);
        return new MortalWoundGameTimeAuthorityResult(
            true,
            Array.Empty<ValidationIssue>(),
            new MortalWoundGameTimeAuthority(
                minute,
                coordinates.CoordinatesFingerprint,
                acceptedState.AcceptedStateFingerprint,
                authorityFingerprint));
    }

    internal bool Matches(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates) =>
        acceptedState is not null &&
        coordinates is not null &&
        coordinates.MatchesAcceptedState(acceptedState) &&
        CurrentTimeInMinutes == acceptedState.CurrentGameMinute &&
        string.Equals(
            CoordinatesFingerprint,
            coordinates.CoordinatesFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            AcceptedStateFingerprint,
            acceptedState.AcceptedStateFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            AuthorityFingerprint,
            ComputeFingerprint(
                CurrentTimeInMinutes,
                CoordinatesFingerprint,
                AcceptedStateFingerprint),
            StringComparison.Ordinal);

    private static string ComputeFingerprint(
        long minute,
        string coordinatesFingerprint,
        string acceptedStateFingerprint) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            AuthorityDomain,
            "1",
            CanonicalClockKind,
            CanonicalSourcePath,
            minute.ToString(CultureInfo.InvariantCulture),
            coordinatesFingerprint,
            acceptedStateFingerprint
        });

    private static MortalWoundGameTimeAuthorityResult Failure(
        IEnumerable<ValidationIssue> issues) => new(
        false,
        new ReadOnlyCollection<ValidationIssue>(issues.ToArray()),
        null);

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "The Mortal wound-treatment game-time authority cannot be trusted.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
        "Recreate game-time authority from the current accepted state and its sealed attempt coordinates."));
}
