using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Reads the signed wound baseline and accepted effect base, optionally applying the initial wound stage to a detached copy.
        /// This seed allocates no identities and does not authorize an insertion or publication.
        /// </summary>
        /// <param name="issues">
        /// Receives invalid baseline, stage binding or carrier contribution failures.
        /// </param>
        /// <param name="applyInitialStage">
        /// Includes the current turn's initial accepted wound stage when <see langword="true"/>.
        /// <see langword="false"/> reads only the signed pre-turn wound baseline.
        /// </param>
        /// <returns>
        /// Detached initial wound and effect state, or <see langword="null"/> if provenance or consistency fails.
        /// </returns>
        private WoundOperationBeforeData? ReadInitialWoundState(List<ValidationIssue> issues,
            bool applyInitialStage = true)
        {
            var stage = _input.PlanningContext?.WoundStageBundle;
            var original = stage?.Input ?? _input.WoundInput;
            var carriers = original?.PreTurnCarriers ?? new WoundCarrierCatalogInput(
                ReadCarrier(WoundCarrierCatalog.PlayerPath), ReadCarrier(WoundCarrierCatalog.NpcPath),
                ReadCarrier(WoundCarrierCatalog.EnemiesPath), ReadCarrier(WoundCarrierCatalog.AlliesPath),
                ReadCarrier(WoundCarrierCatalog.AfterlifeProfilesPath));
            if (original == null)
            {
                // Reuse the ordinary accepted-owner projection: persistent owners
                // may not have an activeWounds array before their first wound.
                // Only signed wound collections survive this projection.
                var projected = WoundAcceptedOwnerCarrierAuthority.Compose(
                    carriers, _input.PlanningContext!.EffectPlan!.ResourceTriggerCarriers);
                issues.AddRange(projected.Issues);
                if (!projected.Success || projected.Carriers is not { } acceptedCarriers)
                    return null;
                carriers = acceptedCarriers;
            }
            var catalog = WoundCarrierCatalog.Build(carriers);
            var identityJson = original?.PreTurnIdentityIndex.ToJsonString() ??
                _source.ReadOriginal(WoundIdentityState.StatePath);
            var historyJson = original?.PreTurnHistory.ToJsonString() ??
                _source.ReadOriginal(WoundHistoryState.HistoryPath);
            // Signed absence is an empty initial state only when both client-owned
            // indexes are absent and there are no wound occurrences to account for.
            // Present malformed or one-sided state must still fail normal parsing.
            if (original == null && identityJson == null && historyJson == null &&
                _source.IsOriginalAbsent(WoundIdentityState.StatePath) &&
                _source.IsOriginalAbsent(WoundHistoryState.HistoryPath) &&
                catalog.Issues.Count == 0 && catalog.Occurrences.Count == 0)
            {
                identityJson = "{\"schemaVersion\":1,\"entries\":[]}";
                historyJson = "{\"schemaVersion\":1,\"nextOrdinal\":1,\"transitions\":[]}";
            }
            var identity = WoundIdentityState.Parse(identityJson, WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(historyJson, WoundHistoryState.HistoryPath);
            issues.AddRange(identity.Issues);
            issues.AddRange(history.Issues);
            issues.AddRange(catalog.Issues);
            if (identity.State == null || history.State == null || issues.Count != 0)
                return null;
            issues.AddRange(history.State.ValidateAgreement(identity.State, catalog));
            if (issues.Count != 0)
                return null;
            var identityRoot = JsonNode.Parse(WoundIdentityState.SerializeCanonical(identity.State))!.AsObject();
            var historyRoot = JsonNode.Parse(WoundHistoryState.SerializeCanonical(history.State))!.AsObject();
            if (applyInitialStage && stage != null)
            {
                issues.AddRange(AcceptedMechanicsCarrierAssembler.ValidateInitialEffectPlan(
                    _input.PlanningContext!.EffectPlan, stage));
                if (issues.Count != 0)
                    return null;
                var owners = new HashSet<WoundOwnerCoordinate>();
                foreach (var contribution in stage.FinalPlan.CarrierContributions)
                {
                    var owner = contribution.Owner;
                    var root = WoundCarrierCollectionAuthority.GetRoot(carriers, owner.CarrierPath);
                    if (!owners.Add(owner) || root == null ||
                        !WoundCarrierCollectionAuthority.TryResolve(root, owner, out var collection, out _) ||
                        WoundCarrierCollectionAuthority.ComputeFingerprint(owner, collection) !=
                        contribution.ExpectedWoundCollectionFingerprint)
                    {
                        issues.Add(SourceIssue(owner.CarrierPath, "spiritual_wound_initial_stage_mismatch",
                            "one accepted initial contribution bound to its exact original owner collection"));
                        return null;
                    }
                    foreach (var mutation in contribution.Mutations)
                    {
                        var failure = AcceptedMechanicsCarrierAssembler.ApplyMutation(collection, owner, mutation);
                        if (failure != null)
                        {
                            issues.Add(failure);
                            return null;
                        }
                    }
                }
                identityRoot = stage.FinalPlan.IdentityIndexAfterImage;
                historyRoot = stage.FinalPlan.HistoryAfterImage;
                var resultingIdentity = WoundIdentityState.Parse(identityRoot.ToJsonString(), WoundIdentityState.StatePath);
                var resultingHistory = WoundHistoryState.Parse(historyRoot.ToJsonString(), WoundHistoryState.HistoryPath);
                var resultingCatalog = WoundCarrierCatalog.Build(carriers);
                issues.AddRange(resultingIdentity.Issues);
                issues.AddRange(resultingHistory.Issues);
                issues.AddRange(resultingCatalog.Issues);
                if (issues.Count != 0 || resultingIdentity.State == null || resultingHistory.State == null)
                    return null;
                issues.AddRange(resultingHistory.State.ValidateAgreement(resultingIdentity.State, resultingCatalog));
                if (issues.Count != 0)
                    return null;
            }
            var effectPlan = _input.PlanningContext!.EffectPlan!;
            return new WoundOperationBeforeData(carriers, identityRoot, historyRoot,
                effectPlan.ResourceTriggerCarriers, effectPlan.IdentityIndexAfterImage);

            JsonObject? ReadCarrier(string path) => ParseWoundCarrierRoot(_source.ReadOriginal(path), path, issues);
        }
    }
}
