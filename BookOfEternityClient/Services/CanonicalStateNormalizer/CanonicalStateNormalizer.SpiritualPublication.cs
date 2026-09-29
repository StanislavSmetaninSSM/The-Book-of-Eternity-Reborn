using System.Text.Json.Nodes;
using SpiritualPublicationAuthority = BookOfEternityClient.Services.ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority;
using SpiritualPublicationReceipt = BookOfEternityClient.Services.ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationReceipt;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    /// <summary>
    /// Retains the exact spiritual publication and item normalization authority before transaction ownership is taken.
    /// </summary>
    /// <param name="Authority">
    /// Capture-issued completed publication whose current physical inputs have been checked.
    /// </param>
    /// <param name="Validated">
    /// Original common binding and immutable snapshot witnesses.
    /// </param>
    /// <param name="ItemSnapshot">
    /// Existing item normalization authority matched to the completed common owner catalog.
    /// </param>
    internal sealed record SpiritualPublicationPreflight(
        SpiritualPublicationAuthority Authority,
        AcceptedMechanicsNormalizationPreflight.Validated Validated,
        MortalItemAcceptedTurnNormalizationSnapshot ItemSnapshot);

    /// <summary>
    /// Checks completed spiritual authority before any generic normalizer may write canonical state.
    /// </summary>
    /// <returns>
    /// The exact preflight, or <see langword="null"/> when the common slot is not a spiritual publication.
    /// </returns>
    internal async Task<SpiritualPublicationPreflight?> PrevalidateSpiritualPublicationTransactionAsync()
    {
        var lease = _writeLease ?? throw new InvalidOperationException("Spiritual publication requires a canonical lease.");
        if (!AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(_fs, lease, out var authority))
            return null;
        try
        {
            var issues = await authority.ValidateCurrentInputsAsync(_fs, lease);
            if (issues.Count != 0)
                throw new InvalidDataException(string.Join("; ", issues.Select(issue =>
                    $"{issue.Code}: {issue.FilePath}")));
            var witnesses = CaptureAcceptedMechanicsSnapshotBeforeImages(authority.Plan.BeforeImages);
            ValidateAcceptedMechanicsSnapshotBinding(authority.Binding, witnesses);
            var validated = new AcceptedMechanicsNormalizationPreflight.Validated(
                authority.Plan, authority.Binding, witnesses);
            var items = authority.MortalItemSnapshot;
            if (!items.MatchesAcceptedOwnerAuthority(authority.Plan.OwnerAuthority))
                throw new InvalidDataException("Spiritual publication requires its original item normalization authority.");
            return new(authority, validated, items);
        }
        catch
        {
            InvalidateAcceptedMechanicsHandoffs();
            throw;
        }
    }

    /// <summary>
    /// Normalizes through the existing accepted-turn writer under one already-taken spiritual transaction.
    /// </summary>
    /// <param name="backups">
    /// Existing signed backup mapping used by unrelated normalizers.
    /// </param>
    /// <param name="preflight">
    /// Exact pre-write validation and item authority retained by the transaction coordinator.
    /// </param>
    /// <param name="receipt">
    /// One-shot registry receipt for this completed plan and canonical filesystem.
    /// </param>
    /// <returns>
    /// The same completed common plan after publication and read-back.
    /// </returns>
    internal Task<AcceptedMechanicsPlan?> NormalizeAccumulatedStateWithSpiritualPublicationTransactionAsync(
        IReadOnlyDictionary<string, string> backups, SpiritualPublicationPreflight preflight,
        SpiritualPublicationReceipt receipt)
    {
        RequireCurrentSpiritualPublication(receipt, preflight.Validated.Plan);
        if (!ReferenceEquals(preflight.Authority, receipt.Authority))
            throw new InvalidDataException("Spiritual preflight belongs to another publication.");
        return NormalizeAccumulatedStateCoreAsync(backups,
            new MortalItemAcceptedTurnNormalizationMode.Validated(preflight.ItemSnapshot),
            preflight.Validated, spiritualPublicationReceipt: receipt);
    }

    /// <summary>
    /// Rejects missing, replaced or foreign transaction ownership before using a spiritual plan.
    /// </summary>
    /// <param name="receipt">
    /// Taken publication receipt held by the transaction coordinator.
    /// </param>
    /// <param name="plan">
    /// Exact plan whose retained payload is about to be used.
    /// </param>
    private void RequireCurrentSpiritualPublication(SpiritualPublicationReceipt receipt, AcceptedMechanicsPlan plan)
    {
        if (_writeLease is null || !ReferenceEquals(receipt.Plan, plan) ||
            !AcceptedMechanicsPlanAuthority.IsTakenSpiritualPublicationCurrent(_fs, _writeLease, receipt))
            throw new InvalidDataException("The completed spiritual publication no longer owns this exact plan.");
    }

    /// <summary>
    /// Determines whether the common writer already owns the complete final image of a normalization root.
    /// </summary>
    /// <param name="plan">
    /// Exact completed common plan.
    /// </param>
    /// <param name="path">
    /// Canonical root under consideration.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a whole-root writer; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool SpiritualPlanOwnsRoot(AcceptedMechanicsPlan plan, string path) =>
        plan.OwnerCompanionAfterImages.ContainsKey(path) || plan.EffectCarrierAfterImages.ContainsKey(path) ||
        plan.WoundCarrierAfterImages.ContainsKey(path);

    /// <summary>
    /// Rechecks private inputs, commands and immutable witnesses after unrelated normalizers have run.
    /// </summary>
    /// <param name="receipt">
    /// Exact taken authority whose committed inputs remain immutable until common publication.
    /// </param>
    private async Task ValidateSpiritualRetainedPublicationInputsAsync(SpiritualPublicationReceipt receipt)
    {
        RequireCurrentSpiritualPublication(receipt, receipt.Plan);
        var normalizedPaths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.ToHashSet(StringComparer.Ordinal);
        foreach (var command in new[]
        {
            AcceptedMechanicsPlan.WoundCommandPath, EffectAcceptedTurnPlan.CommandPath,
            ResourceMaterializationContract.CommandPath, SpiritualWoundCaptureCheckpointState.StatePath,
            SpiritualWoundDecisionPendingState.StatePath
        })
            normalizedPaths.Remove(command);
        foreach (var pair in receipt.Authority.PublicationInputs.Where(pair => !normalizedPaths.Contains(pair.Key)))
        {
            var bytes = await _fs.ReadFileBytesAsync(_writeLease!, pair.Key);
            if (new CanonicalBeforeImage(bytes is not null, bytes).Fingerprint != pair.Value.Fingerprint)
                throw new InvalidDataException($"Spiritual publication retained input '{pair.Key}' changed during normalization.");
        }
    }

    /// <summary>
    /// Checks untouched whole-root inputs and independently normalized carrier baselines before the common writer runs.
    /// </summary>
    /// <param name="receipt">
    /// Exact taken publication with authenticated committed physical inputs.
    /// </param>
    /// <param name="live">
    /// Current physical carrier roots read under the transaction lease.
    /// </param>
    private async Task ValidateSpiritualPublicationCarriersAsync(
        SpiritualPublicationReceipt receipt, EffectCarrierCatalogInput live)
    {
        var plan = receipt.Plan;
        foreach (var path in new[]
        {
            EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath,
            EffectCarrierCatalog.EnemiesPath, EffectCarrierCatalog.AlliesPath,
            EffectCarrierCatalog.AfterlifeProfilesPath, EffectCarrierCatalog.SpiritualConflictPath
        })
        {
            if ((path == AfterlifeSpiritualConflictState.StatePath || path == AfterlifeEntityProfileState.StatePath) &&
                SpiritualPlanOwnsRoot(plan, path))
            {
                var current = await _fs.ReadFileBytesAsync(_writeLease!, path);
                if (!receipt.Authority.PublicationInputs.TryGetValue(path, out var expected) ||
                    expected.Fingerprint != new CanonicalBeforeImage(current is not null, current).Fingerprint)
                    throw StaleEffectPlan(path);
            }
            else if (!JsonNode.DeepEquals(GetEffectCarrierRoot(live, path),
                         GetEffectCarrierRoot(plan.EffectPlan!.AcceptedCarrierBaselines, path)))
                throw StaleEffectPlan(path);
        }
    }

    /// <summary>
    /// Checks final wound envelopes and history before substituting accepted application lineage for allocated roots.
    /// Shared effect and resource siblings remain outside the wound projection.
    /// </summary>
    /// <param name="receipt">
    /// Exact taken publication retaining the immutable completed insertion chain.
    /// </param>
    /// <param name="roots">
    /// Independently composed final canonical source roots.
    /// </param>
    private void ValidateSpiritualWoundPublicationSources(SpiritualPublicationReceipt receipt,
        IReadOnlyDictionary<string, JsonNode?> roots)
    {
        RequireCurrentSpiritualPublication(receipt, receipt.Plan);
        if (receipt.Authority.LiveWoundCompletion is not { } completion)
            return;
        var final = completion.FinalState;
        var actual = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            Read(WoundCarrierCatalog.PlayerPath), Read(WoundCarrierCatalog.NpcPath),
            Read(WoundCarrierCatalog.EnemiesPath), Read(WoundCarrierCatalog.AlliesPath),
            Read(WoundCarrierCatalog.AfterlifeProfilesPath)));
        var expected = WoundCarrierCatalog.Build(final.Carriers);
        if (actual.Issues.Count != 0 || expected.Issues.Count != 0 ||
            !JsonNode.DeepEquals(receipt.Plan.WoundIdentityAfterImage, final.Identity) ||
            !JsonNode.DeepEquals(receipt.Plan.WoundHistoryAfterImage, final.History))
            throw StaleEffectPlan("spiritual completed wound state");
        foreach (var woundId in completion.Insertions.Select(insertion => insertion.Wound.WoundId).Distinct(StringComparer.Ordinal))
        {
            if (!actual.TryResolveOne(woundId, out var published) || !expected.TryResolveOne(woundId, out var retained) ||
                WoundMaterializationContract.SerializeCanonical(published.Wound) !=
                WoundMaterializationContract.SerializeCanonical(retained.Wound))
                throw StaleEffectPlan("spiritual completed wound envelope");
        }
        var identity = WoundIdentityState.Parse(final.Identity.ToJsonString(), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(final.History.ToJsonString(), WoundHistoryState.HistoryPath);
        if (identity.State is null || identity.Issues.Count != 0 || history.State is null || history.Issues.Count != 0 ||
            history.State.ValidateAgreement(identity.State, actual).Count != 0)
            throw StaleEffectPlan("spiritual completed wound history agreement");

        JsonObject? Read(string path) => roots.TryGetValue(path, out var root) ? root as JsonObject : null;
    }

    /// <summary>
    /// Checks the exact C3 ledger, its source-to-decision bijection and newly published wound transitions.
    /// </summary>
    /// <param name="plan">
    /// Completed plan whose receipt and canonical images have already been written and read back.
    /// </param>
    private async Task ValidatePublishedSpiritualReceiptAsync(AcceptedMechanicsPlan plan)
    {
        const string path = SpiritualWoundOpportunityReceiptState.StatePath;
        if (!plan.OwnerCompanionAfterImages.TryGetValue(path, out var expected))
            throw new InvalidDataException("Spiritual publication has no authenticated C3 receipt.");
        var json = await ReadExactPublishedAfterImageAsync(path, expected);
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(json, path);
        if (!parsed.IsValid)
            throw new InvalidDataException("Spiritual receipt failed canonical instance/source/decision agreement.");
        var materializations = expected["decisions"]!.AsArray().OfType<JsonObject>()
            .Where(row => row["decision"]?.GetValue<string>() == "materialize").ToArray();
        if (materializations.Length != 0)
        {
            var history = WoundHistoryState.Parse(
                await ReadCanonicalFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
            if (history.State is null || history.Issues.Count != 0)
                throw new InvalidDataException("Spiritual publication has no valid wound history.");
            foreach (var row in materializations)
            {
                if (!history.State.TryResolveExactTransition(row["transitionId"]!.GetValue<string>(), out var transition) ||
                    transition!.WoundId != row["woundId"]!.GetValue<string>() ||
                    transition.Turn != row["binding"]!["turn"]!.GetValue<int>())
                    throw new InvalidDataException("Spiritual receipt does not match its published wound transition.");
            }
        }
        foreach (var consumed in new[]
        {
            SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
            AcceptedMechanicsPlan.WoundCommandPath
        })
        {
            if (!plan.ConsumedPaths.Contains(consumed, StringComparer.Ordinal) ||
                await _fs.ReadFileBytesAsync(_writeLease!, consumed) is not null)
                throw new InvalidDataException($"Spiritual publication did not consume '{consumed}'.");
        }
    }

    /// <summary>
    /// Reads one source from the final common composition, falling back to the current unrelated normalized source.
    /// </summary>
    /// <param name="plan">
    /// Exact capture-issued completed plan whose whole-root producers do not overlap.
    /// </param>
    /// <param name="path">
    /// Source root required by the ordinary canonical effect catalog builder.
    /// </param>
    /// <returns>
    /// Detached final source root, or <see langword="null"/> for an absent retained source.
    /// </returns>
    private async Task<JsonNode?> ReadSpiritualFinalSourceAsync(AcceptedMechanicsPlan plan, string path)
    {
        if (plan.WoundCarrierAfterImages.TryGetValue(path, out var wound))
            return wound;
        if (plan.EffectCarrierAfterImages.TryGetValue(path, out var effect))
            return effect;
        if (plan.OwnerCompanionAfterImages.TryGetValue(path, out var owner))
            return owner;
        if (path == WoundIdentityState.StatePath && plan.WoundIdentityAfterImage is { } identity)
            return identity;
        if (path == WoundHistoryState.HistoryPath && plan.WoundHistoryAfterImage is { } history)
            return history;
        return await ReadEffectPublicationNodeAsync(path);
    }
}
