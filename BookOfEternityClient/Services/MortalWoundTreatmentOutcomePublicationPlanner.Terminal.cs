namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentOutcomePublicationPlanner
{
    /// <summary>
    /// Projects existing typed immutable legacy seeds into the reducer's closed
    /// declaration protocol. This is DATA shared by actual and detached stages.
    /// </summary>
    /// <param name="heal">The immutable request-derived heal intent and ordered seeds.</param>
    /// <param name="declared">The same declared heal program retained by the resolution.</param>
    /// <param name="woundId">The exact original selected wound identifier.</param>
    /// <returns>Detached ordered reducer declarations, without allocating identities.</returns>
    internal static IReadOnlyList<WoundLegacyDeclaration> CreateTerminalDeclarations(MortalWoundHealOutcomeIntent heal,
        MortalWoundHealOperation declared, string woundId)
    {
        if (heal.LegacySeedBindings.Count != declared.Legacies.Length) throw new InvalidDataException("Terminal legacy declaration count changed.");
        return declared.Legacies.Select((draft, ordinal) =>
        {
            var seed = heal.LegacySeedBindings[ordinal];
            if (seed.LegacyOrdinal != ordinal || seed.LocalLegacyRef != draft.LocalLegacyRef || seed.Kind != draft.Kind)
                throw new InvalidDataException("Terminal legacy declaration order changed.");
            return new WoundLegacyDeclaration(seed.LegacyId,
                draft is MortalWoundMechanicalEffectLegacyDraft ? "independent_mechanical" : "cosmetic",
                draft is MortalWoundMechanicalEffectLegacyDraft ? "effect" : null, woundId, draft.ReadableSummary);
        }).ToArray();
    }

    /// <summary>
    /// Applies the existing heal reducer to an exact active primary treatment
    /// snapshot. No effect allocation, recovery tick, live permission or IO occurs.
    /// </summary>
    /// <param name="before">The actual or independently reconstructed active treatment snapshot.</param>
    /// <param name="heal">Its immutable final child coordinates and preparation fingerprint.</param>
    /// <param name="declarations">The complete ordered verified declaration DATA.</param>
    /// <param name="turn">The original positive accepted turn.</param>
    /// <returns>The ordinary reducer result, preserving every validation issue and linked ordinal.</returns>
    internal static WoundTransitionReductionResult ReduceTerminalHeal(WoundMaterializationEnvelope before,
        MortalWoundHealOutcomeIntent heal, IReadOnlyList<WoundLegacyDeclaration> declarations, int turn)
    {
        var after = before with
        {
            Lifecycle = "healed", Care = before.Care with { State = "healed", ActiveCourseId = null },
            Relations = before.Relations with
            {
                LegacyRefs = before.Relations.LegacyRefs.Concat(declarations.Where(row => row.Kind == "cosmetic").Select(row => row.LegacyId)).ToArray(),
                IndependentEffectRefs = before.Relations.IndependentEffectRefs.Concat(declarations.Where(row => row.Kind == "independent_mechanical").Select(row => row.LegacyId)).ToArray()
            },
            LastTransition = new(heal.HealChildCoordinates.TransitionId, checked(before.LastTransition.Ordinal + 1), turn, "heal")
        };
        return WoundTransitionReducer.Reduce(new WoundTransitionRequest("heal", after.LastTransition.TransitionId,
            heal.HealChildCoordinates.OperationKey, heal.HealChildCoordinates.EventRef, turn, before, after,
            new WoundHealingEvidence(heal.IntentFingerprint, WoundIdentityState.ComputeSemanticFingerprint(before),
                WoundIdentityState.ComputeSemanticFingerprint(after), heal.PreparationFingerprint, null, declarations)));
    }
}
