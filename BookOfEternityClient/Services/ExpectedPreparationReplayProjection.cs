using System.Globalization;

namespace BookOfEternityClient.Services;

/// <summary>Retains immutable expected replay DATA, never an actual plan or authority verdict.</summary>
internal sealed class ExpectedPreparationReplayProjection
{
    private readonly (string Source, string Seal)[] _batches;

    /// <summary>Copies the complete ordered replay comparison vector.</summary>
    /// <param name="binding">Expected binding fingerprint.</param>
    /// <param name="input">Expected input fingerprint.</param>
    /// <param name="preparation">Expected preparation fingerprint.</param>
    /// <param name="baselineInput">Expected baseline input fingerprint.</param>
    /// <param name="baselineSeal">Expected baseline authority seal.</param>
    /// <param name="batches">Every ordered source-export and transition-seal pair.</param>
    internal ExpectedPreparationReplayProjection(string binding, string input, string preparation,
        string baselineInput, string baselineSeal, IEnumerable<(string Source, string Seal)> batches)
    {
        BindingFingerprint = binding; InputFingerprint = input; WoundPreparationFingerprint = preparation;
        BaselinePreparedInputFingerprint = baselineInput; BaselineAuthoritySeal = baselineSeal;
        _batches = batches.ToArray();
    }

    /// <summary>Gets expected binding DATA.</summary>
    internal string BindingFingerprint { get; }
    /// <summary>Gets expected input DATA.</summary>
    internal string InputFingerprint { get; }
    /// <summary>Gets expected preparation DATA shared with the scalar consumer.</summary>
    internal string WoundPreparationFingerprint { get; }
    /// <summary>Gets expected baseline input DATA.</summary>
    internal string BaselinePreparedInputFingerprint { get; }
    /// <summary>Gets expected baseline seal DATA.</summary>
    internal string BaselineAuthoritySeal { get; }

    /// <summary>Captures only a successful original source result.</summary>
    /// <param name="result">The original detached source envelope.</param>
    /// <returns>The immutable vector, or null for the original refusal.</returns>
    internal static ExpectedPreparationReplayProjection? Capture(WoundAcceptedTurnPreparationResult result)
    {
        if (!result.Success || result.Plan is not { } expected) return null;
        var baseline = expected.BaselineAuthority;
        return new(expected.BindingFingerprint, expected.InputFingerprint, expected.WoundPreparationFingerprint,
            baseline.PreparedInputFingerprint, baseline.AuthoritySeal,
            expected.EffectOperationBatches.Select(batch => (batch.SourceExportFingerprint, batch.TransitionAuthority.AuthoritySeal)));
    }

    /// <summary>Repeats the original full ordinal comparison against the current actual plan.</summary>
    /// <param name="plan">The actual plan freshly validated by its caller.</param>
    /// <returns>True only for all five scalar values, count and ordered pairs.</returns>
    internal bool AgreesWith(WoundPreparedAcceptedTurnPlan plan)
    {
        if (!string.Equals(BindingFingerprint, plan.BindingFingerprint, StringComparison.Ordinal) ||
            !string.Equals(InputFingerprint, plan.InputFingerprint, StringComparison.Ordinal) ||
            !string.Equals(WoundPreparationFingerprint, plan.WoundPreparationFingerprint, StringComparison.Ordinal) ||
            !string.Equals(BaselinePreparedInputFingerprint, plan.BaselineAuthority.PreparedInputFingerprint, StringComparison.Ordinal) ||
            !string.Equals(BaselineAuthoritySeal, plan.BaselineAuthority.AuthoritySeal, StringComparison.Ordinal)) return false;
        var actual = plan.EffectOperationBatches;
        if (_batches.Length != actual.Count) return false;
        for (var i = 0; i < _batches.Length; i++)
            if (!string.Equals(_batches[i].Source, actual[i].SourceExportFingerprint, StringComparison.Ordinal) ||
                !string.Equals(_batches[i].Seal, actual[i].TransitionAuthority.AuthoritySeal, StringComparison.Ordinal)) return false;
        return true;
    }

    /// <summary>Exports independent complete DATA for typed diagnostics and comparisons.</summary>
    /// <returns>A fresh array; changing it cannot affect retained DATA.</returns>
    internal string[] ExportValues() => new[] { BindingFingerprint, InputFingerprint, WoundPreparationFingerprint,
        BaselinePreparedInputFingerprint, BaselineAuthoritySeal, _batches.Length.ToString(CultureInfo.InvariantCulture) }
        .Concat(_batches.SelectMany(pair => new[] { pair.Source, pair.Seal })).ToArray();
}
