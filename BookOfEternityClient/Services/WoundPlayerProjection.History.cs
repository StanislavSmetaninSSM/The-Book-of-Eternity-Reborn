namespace BookOfEternityClient.Services;

internal static partial class WoundPlayerProjection
{
    private static IReadOnlyList<WoundPlayerHistoryEntry> ProjectHistory(WoundPlayerProjectionInput input)
    {
        var history = new List<WoundPlayerHistoryEntry>();
        foreach (var identity in input.Identities.Entries.Where(i => i.Status == "healed"))
        {
            var owner = new WoundOwnerCoordinate(identity.Realm, identity.OwnerKind, identity.OwnerId, identity.CarrierPath);
            if (!IsSelf(owner, input.Realm) || identity.TerminalTransitionId is null ||
                !input.History.TryResolveExactTransition(identity.TerminalTransitionId, out var terminal) ||
                terminal is null || !terminal.Terminal || terminal.WoundId != identity.WoundId)
                continue;
            // A readableSummary alone carries no historical visibility authority.
            // Use complete strictly reconstructed terminal before/after evidence only.
            var stages = input.History.Transitions.Where(t => t.WoundId == identity.WoundId)
                .Select(t => t.TransitionResult).OfType<MortalWoundRecoveryPersistedResult>()
                .SelectMany(r => r.Stages).Where(s => s.TransitionId == terminal.TransitionId).ToArray();
            if (stages.Length != 1) continue;
            var stage = stages[0];
            var before = stage.BeforeWound;
            var after = stage.AfterWound;
            if (!stage.Terminal || stage.Kind != "heal" || after.Lifecycle != "healed" ||
                before.Owner != owner || after.Owner != owner || before.WoundId != identity.WoundId ||
                after.WoundId != identity.WoundId || after.LastTransition.TransitionId != terminal.TransitionId ||
                stage.BeforeFingerprint != terminal.BeforeFingerprint || stage.AfterFingerprint != terminal.AfterFingerprint ||
                WoundIdentityState.ComputeSemanticFingerprint(before) != terminal.BeforeFingerprint ||
                WoundIdentityState.ComputeSemanticFingerprint(after) != terminal.AfterFingerprint ||
                !Visible(before.Display.Visibility) || !Visible(after.Display.Visibility)) continue;
            history.Add(new(Text(after.Display.Name), before.Severity.Value, "Рана зажила"));
        }
        return history;
    }
}
