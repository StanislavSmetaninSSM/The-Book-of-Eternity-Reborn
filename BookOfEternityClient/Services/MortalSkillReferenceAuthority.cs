namespace BookOfEternityClient.Services;

/// <summary>
/// Describes whether a reference identifies an exact, confusable or unknown skill name.
/// </summary>
internal enum MortalSkillReferenceMatchKind
{
    Exact,
    Confusable,
    Unknown
}

/// <summary>
/// Carries an identity classification without binding or modifying a skill reference.
/// </summary>
internal readonly struct MortalSkillReferenceMatch
{
    /// <summary>
    /// Creates the result of a skill-reference identity lookup.
    /// </summary>
    /// <param name="kind">
    /// The reference classification.
    /// </param>
    /// <param name="canonicalName">
    /// The unique confusable target, or <see langword="null"/> when no target is provided.
    /// </param>
    /// <param name="isAmbiguous">
    /// Whether several canonical names share the non-exact reference's confusable key.
    /// </param>
    public MortalSkillReferenceMatch(
        MortalSkillReferenceMatchKind kind,
        string? canonicalName,
        bool isAmbiguous)
    {
        Kind = kind;
        CanonicalName = canonicalName;
        IsAmbiguous = isAmbiguous;
    }

    /// <summary>
    /// Gets the reference classification.
    /// </summary>
    public MortalSkillReferenceMatchKind Kind { get; }

    /// <summary>
    /// Gets the unique confusable target, or <see langword="null"/> for exact, unknown or ambiguous results.
    /// </summary>
    public string? CanonicalName { get; }

    /// <summary>
    /// Gets whether several exact-distinct names match this non-exact reference.
    /// </summary>
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

    /// <summary>
    /// Gets the number of distinct trimmed ordinal canonical names.
    /// </summary>
    public int Count => _exactNames.Count;

    /// <summary>
    /// Indexes canonical skill names without collapsing case or confusable distinctions.
    /// </summary>
    /// <param name="canonicalNames">
    /// The non-null sequence of names; null and whitespace entries are ignored and names are trimmed.
    /// </param>
    /// <returns>
    /// An authority preserving each distinct ordinal name and its shared confusable key.
    /// </returns>
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

    /// <summary>
    /// Classifies a trimmed reference without selecting a target for an ambiguous key.
    /// </summary>
    /// <param name="reference">
    /// The reference to classify; <see langword="null"/> or whitespace is unknown.
    /// </param>
    /// <returns>
    /// An exact or unknown result without a target, or a confusable result with a target only when unique.
    /// </returns>
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
