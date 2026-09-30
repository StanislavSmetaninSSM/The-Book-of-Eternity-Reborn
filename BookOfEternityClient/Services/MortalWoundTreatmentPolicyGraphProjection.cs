namespace BookOfEternityClient.Services;

internal static class MortalWoundTreatmentPolicyGraphProjection
{
    internal static MortalWoundTreatmentPreparedGraphOperationResult Project(
        MortalWoundTreatmentWorkingGraphProjection before,
        WoundWorkingOperationAddress address,
        MortalWoundDeteriorationResultKind resultKind,
        MortalWoundComplicationProposalDraft? draft)
    {
        if (resultKind == MortalWoundDeteriorationResultKind.AddComplication)
        {
            if (draft is null) return new(false, false, null);
            var applicable = before.TryAppendComplication(draft,
                WoundWorkingReferenceOrigin.PolicyAddition, address, out var appended);
            return new(applicable, false, appended);
        }
        if (resultKind != MortalWoundDeteriorationResultKind.IncreaseSeverity || draft is not null)
            return new(false, false, null);
        var rank = checked(before.Scalars.Severity.Rank + 1);
        var value = rank switch { 2 => "II", 3 => "III", 4 => "IV", _ => string.Empty };
        if (value.Length == 0) return new(false, false, null);
        var after = before.WithScalars(before.Scalars with
        {
            Severity = before.Scalars.Severity with { Rank = rank, Value = value }
        });
        return after.ValidateGraph("mortalWoundTreatment.workingGraph").IsEmpty
            ? new(true, false, after) : new(false, false, null);
    }
}
