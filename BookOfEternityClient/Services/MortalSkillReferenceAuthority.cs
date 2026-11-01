namespace BookOfEternityClient.Services;

internal enum MortalSkillReferenceMatchKind
{
    Exact,
    Confusable,
    Unknown
}

internal readonly struct MortalSkillReferenceMatch
{
    public MortalSkillReferenceMatch(
        MortalSkillReferenceMatchKind kind,
        string? canonicalName,
        bool isAmbiguous)
    {
        Kind = kind;
        CanonicalName = canonicalName;
        IsAmbiguous = isAmbiguous;
    }

    public MortalSkillReferenceMatchKind Kind { get; }

    public string? CanonicalName { get; }

    public bool IsAmbiguous { get; }
}

/// <summary>
/// Fail-closed identity authority for skill references on mastery validation surfaces.
/// Exact ordinal names are the only accepted binding; case-variant or Unicode-confusable
/// references are classified (never silently bound) so validation can reject them with
/// the canonical name to write.
/// </summary>
internal sealed class MortalSkillReferenceAuthority
{
    private static readonly MortalSkillReferenceMatch UnknownMatch =
        new(MortalSkillReferenceMatchKind.Unknown, null, false);

    private readonly HashSet<string> _exactNames;
    private readonly Dictionary<string, List<string>> _confusableIndex;

    private MortalSkillReferenceAuthority(
        HashSet<string> exactNames,
        Dictionary<string, List<string>> confusableIndex)
    {
        _exactNames = exactNames;
        _confusableIndex = confusableIndex;
    }

    public int Count => _exactNames.Count;

    public static MortalSkillReferenceAuthority Build(IEnumerable<string?> canonicalNames)
    {
        ArgumentNullException.ThrowIfNull(canonicalNames);

        var exactNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in canonicalNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            exactNames.Add(name.Trim());
        }

        var confusableIndex = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var name in exactNames)
        {
            var key = MortalLocationIdentityState.BuildConfusableKey(name);
            if (!confusableIndex.TryGetValue(key, out var bucket))
            {
                bucket = new List<string>();
                confusableIndex[key] = bucket;
            }

            bucket.Add(name);
        }

        return new MortalSkillReferenceAuthority(exactNames, confusableIndex);
    }

    public MortalSkillReferenceMatch Classify(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return UnknownMatch;

        var trimmed = reference.Trim();
        if (_exactNames.Contains(trimmed))
            return new MortalSkillReferenceMatch(MortalSkillReferenceMatchKind.Exact, null, false);

        var key = MortalLocationIdentityState.BuildConfusableKey(trimmed);
        if (!_confusableIndex.TryGetValue(key, out var bucket) || bucket.Count == 0)
            return UnknownMatch;

        return bucket.Count == 1
            ? new MortalSkillReferenceMatch(MortalSkillReferenceMatchKind.Confusable, bucket[0], false)
            : new MortalSkillReferenceMatch(MortalSkillReferenceMatchKind.Confusable, null, true);
    }
}
