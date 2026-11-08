using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    internal static readonly string[] NaturalStartDependencyPaths =
    [AfterlifeSpiritualConflictState.DifficultySettingsPath, "input/turn_request.json", SpiritualWoundOpportunityReceiptState.StatePath];

    internal sealed partial class Continuation
    {
        // Pending DATA is not permission to execute. Only the real ordinary composer
        // binds its fully validated conflict and owners before Prepare can succeed.
        private readonly JsonObject? _naturalConflictDraft;
        private JsonObject? _naturalConflictAfter;
        private string? _naturalConflictOwners;
        internal bool HasNaturalConflictStart => _naturalConflictDraft is not null;

        private static JsonObject ReadNaturalCleanStart(JsonObject? original, CanonicalBeforeImage image)
        {
            if (original is null || original["activeConflict"] is not null || image.Bytes is null)
                throw new InvalidDataException("Natural start requires its original inactive conflict root.");
            var current = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(image.Bytes),
                new JsonSerializerOptions(), "natural clean start")!;
            var parent = current.DeepClone().AsObject();
            parent.Remove(AfterlifeSpiritualConflictState.ResponseField);
            if (!JsonNode.DeepEquals(original, parent) || current[AfterlifeSpiritualConflictState.ResponseField] is not JsonObject update ||
                update["mode"]?.GetValue<string>() != AfterlifeSpiritualConflictState.ModeStart ||
                update.Any(pair => pair.Key is not ("mode" or "conflictState" or "activeConflict" or "conflictSeed" or "realm" or "dangerMode")))
                throw new InvalidDataException("Natural recovery permits only a clean start over its unchanged signed conflict parent.");
            var aliases = new[] { "conflictState", "activeConflict", "conflictSeed" }.Where(update.ContainsKey).ToArray();
            if (aliases.Length != 1 || update[aliases[0]] is not JsonObject seed ||
                seed["exchangeLog"] is not JsonArray { Count: 0 } ||
                seed["status"] is not null && seed["status"]!.GetValue<string>() != "active" ||
                seed["resolutionState"] is not null && seed["resolutionState"]!.GetValue<string>() != "active")
                throw new InvalidDataException("Natural recovery requires one new active start with an explicit empty exchange log.");
            if (update.ContainsKey("realm") &&
                (update["realm"] is not JsonValue wrapper || !wrapper.TryGetValue<string>(out var wrapperRealm) ||
                 !AfterlifeEntityProfileState.TryNormalizeEffectRealm(wrapperRealm, out var wrapperKey) ||
                 seed["realm"] is not null && (seed["realm"] is not JsonValue declared ||
                     !declared.TryGetValue<string>(out var declaredRealm) ||
                     !AfterlifeEntityProfileState.TryNormalizeEffectRealm(declaredRealm, out var declaredKey) || declaredKey != wrapperKey)))
                throw new InvalidDataException("Natural start wrapper realm must be a valid realm agreeing with its seed.");
            return current;
        }

        internal async Task<IReadOnlyList<ValidationIssue>> BindNaturalConflictCompositionAsync(
            ValidationService validator, ResourceDefinitionCatalog definitions, AfterlifeResourceOwnerRoots accepted, ResourceOwnerCompositionResult composition)
        {
            if (_naturalConflictDraft is null) return Array.Empty<ValidationIssue>();
            RequireCurrent(_files, _lease);
            if (composition.Authority is null || composition.Issues.Count != 0 ||
                !composition.OwnerCompanionAfterImages.TryGetValue(AfterlifeSpiritualConflictState.StatePath, out var projected) ||
                !JsonNode.DeepEquals(accepted.SpiritualConflict, AfterlifeSpiritualConflictState.ApplyUpdate(
                    _parsedSourceRoots[AfterlifeSpiritualConflictState.StatePath]!.AsObject(),
                    _naturalConflictDraft[AfterlifeSpiritualConflictState.ResponseField]!.AsObject())))
                throw new InvalidDataException("Natural start lost its actual ordinary conflict projection and owners.");
            if (!AfterlifeResourceOwnerComposer.CleanStartProjectionAgrees(definitions,
                _parsedSourceRoots[AfterlifeSpiritualConflictState.StatePath]!.AsObject(), accepted.SpiritualConflict, composition))
                throw new InvalidDataException("Natural start received a foreign conflict or side-owner composition.");
            var frame = await validator.CaptureSpiritualConflictValidationFrameAsync(
                new(true, _naturalConflictDraft.ToJsonString()), _lease);
            if (!frame.HasValidatedSnapshot || frame.SessionId != _snapshot.SessionId || frame.RequestId != _snapshot.RequestId ||
                frame.SnapshotToken != _snapshot.SnapshotToken || frame.SnapshotTurnNumber != _snapshot.TurnNumber)
                throw new InvalidDataException("Natural start conflict validation belongs to a different signed original.");
            var issues = validator.ValidateNaturalConflictStartFrame(_naturalConflictDraft, frame.WithCandidateImages(
                new(new(true, projected.ToJsonString()), frame.Candidate.Soul, frame.Candidate.Shining, frame.Candidate.Profiles)));
            RequireCurrent(_files, _lease);
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error)) return issues;
            if (_naturalConflictAfter is not null && (!JsonNode.DeepEquals(_naturalConflictAfter, projected) ||
                _naturalConflictOwners != composition.Authority.Fingerprint))
                throw new InvalidDataException("Natural start composition changed within its owning operation.");
            _naturalConflictAfter = projected.DeepClone().AsObject();
            _naturalConflictOwners = composition.Authority.Fingerprint;
            return issues;
        }

        internal void RequireNaturalConflictPlan(AcceptedMechanicsPlan plan)
        {
            if (_naturalConflictDraft is null) return;
            RequireExecutionCurrent();
            if (!ReferenceEquals(plan.SpiritualHealingContinuation, this) || plan.AuthorityFingerprints.Owners != _naturalConflictOwners ||
                !plan.BeforeImages.TryGetValue(AfterlifeSpiritualConflictState.StatePath, out var before) ||
                before.Fingerprint != _sources[AfterlifeSpiritualConflictState.StatePath].Fingerprint ||
                !plan.OwnerCompanionAfterImages.TryGetValue(AfterlifeSpiritualConflictState.StatePath, out var after) ||
                !JsonNode.DeepEquals(after, _naturalConflictAfter))
                throw new InvalidDataException("Natural start requires its exact common-plan conflict, owners and raw before-image.");
        }

        private void RequireNaturalConflictPublication(AcceptedMechanicsPlan? plan, IReadOnlyDictionary<string, JsonObject> writes)
        {
            if (_naturalConflictDraft is null) return;
            RequireNaturalConflictPlan(plan ?? throw new InvalidDataException("Natural start has no common publication plan."));
            if (!writes.TryGetValue(AfterlifeSpiritualConflictState.StatePath, out var conflict) ||
                !JsonNode.DeepEquals(conflict, _naturalConflictAfter))
                throw new InvalidDataException("Natural start cannot publish a separate or changed conflict root.");
        }
    }
}
