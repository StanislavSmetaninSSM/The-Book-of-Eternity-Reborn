using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsWoundCommonInputComposer
{
    internal sealed record JointCalculationInputCaptureResult(
        AcceptedMechanicsCalculationInputs? Inputs, IReadOnlyList<ValidationIssue> Issues);

    // Source inspection/data composition only. The private resource owner invokes
    // this with its own baseline, then validates current source again before
    // retaining C. No AcceptedMechanicsInput or common authority exists here.
    internal static Task<JointCalculationInputCaptureResult> CaptureJointRepairCalculationInputsAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        AcceptedMechanicsWoundStageBundle bundle, MortalWoundCanonicalAnchorPlan anchors,
        IReadOnlyDictionary<string, CanonicalBeforeImage> before,
        ResourceDefinitionCatalog definitions, ResourceStateLedger state, ResourceHistoryState history,
        ResourceOwnerAuthority owners, IResourceRegisteredSystemOutcomeDraft registeredOutcome,
        IReadOnlyList<ResourceOwnerKey> terminalOwners, AcceptedMechanicsIdentityFactory resourceFactory)
    {
        using var innerTiming1 = files.MeasureOperation("JointInput.CaptureCalculationInputs");
        try
        {
            var binding = bundle.Input.Binding;
            string? Read(string path) => before.TryGetValue(path, out var image)
                ? image.Existed ? CanonicalJsonUtf8.DecodeOneOptionalBom(image.Bytes!) : null
                : throw new InvalidDataException("Joint common input lacks the actual guarded before-image: " + path);
            var issues = new List<ValidationIssue>();
            ValidateTurnRequest(Read(LiveTurnPreparationService.TurnRequestPath), binding, issues);
            ValidateLiveWoundRoots(Read, bundle.Input, issues);
            ValidateLiveEffectRoots(Read, bundle.EffectBatchPlan.EffectPlan, issues);
            using var innerTiming2 = files.MeasureOperation("JointInput.CaptureReadSnapshot");
            var snapshot = PendingTurnSnapshotReader.ReadCurrent(files, lease,
                new[] { EffectAcceptedTurnInputComposer.WorldTimePath });
            innerTiming2?.Dispose();
            issues.AddRange(snapshot.Issues);
            if (!snapshot.Success || snapshot.Snapshot is not { } original || original.SessionId != binding.SessionId ||
                original.RequestId != binding.RequestId || original.SnapshotToken != binding.SnapshotToken ||
                original.TurnNumber != binding.Turn || original.Realm != binding.Realm ||
                EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(Read(EffectAcceptedTurnInputComposer.WorldTimePath)) != anchors.CurrentTimeInMinutes)
                throw new InvalidDataException("The current official original/clock does not match the complete joint frame.");
            var pending = ResourcePendingResolutionState.ParseCanonical(Read(ResourcePendingResolutionState.PendingPath),
                definitions, allowMissingPristine: true);
            issues.AddRange(pending.Issues);
            if (pending.State?.Requests.Count > 0)
                throw new InvalidDataException("Concurrent resource pending work is outside this joint owner.");
            var sources = ResourceMutationSourceCatalog.Create(registeredOutcome.SourceExports);
            issues.AddRange(sources.Issues);
            if (sources.Catalog is not { } sourceCatalog || issues.Count != 0)
                return Task.FromResult(new JointCalculationInputCaptureResult(null,
                    issues.Select(MortalWoundOpportunityAdapter.PrivateSourceIssue).ToArray()));
            var identity = bundle.Input.PreTurnEffectIdentityIndex?.DeepClone().AsObject() ??
                throw new InvalidDataException("The authentic joint preparation lacks its original effect index.");
            return Task.FromResult(new JointCalculationInputCaptureResult(new AcceptedMechanicsCalculationInputs(
                binding.Turn, binding.Realm, definitions, state, history, owners, sourceCatalog,
                ResourceAcceptedTurnInputComposer.Parse(null), identity, bundle.EffectBatchPlan.EffectPlan,
                Array.Empty<ResourceCapacityIntent>(), Array.Empty<ResourceOwnerCapacityDraft>(), terminalOwners,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal), Array.Empty<AcceptedMechanicsOwnerTransition>(),
                new[] { registeredOutcome }, pending.State, resourceFactory, null, 0, bundle, anchors),
                Array.Empty<ValidationIssue>()));
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
            System.Text.Json.JsonException or IOException)
        {
            var failed = PrivateFailed(Failed(WoundIdentityState.StatePath, "accepted_mechanics_joint_common_input_invalid",
                "the complete privately owned current joint input", error.Message).Issues);
            return Task.FromResult(new JointCalculationInputCaptureResult(null, failed.Issues));
        }
    }

    internal static async Task<AcceptedMechanicsWoundCommonInputCompositionResult> ComposeJointRepairAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority)
    {
        using var innerTiming3 = files.MeasureOperation("JointInput.ComposeAsync");
        try
        {
            await authority.ValidateCurrentAsync(files, lease);
            var bundle = authority.Bundle;
            var binding = bundle.Input.Binding;
            var before = authority.BeforeImages.ToDictionary(row => row.Key,
                row => new CanonicalBeforeImage(row.Value.Existed, row.Value.Bytes?.ToArray()), StringComparer.Ordinal);
            string? Read(string path) => before.TryGetValue(path, out var image)
                ? image.Existed ? CanonicalJsonUtf8.DecodeOneOptionalBom(image.Bytes!) : null
                : throw new InvalidDataException("Joint common input lacks the actual guarded before-image: " + path);
            var issues = new List<ValidationIssue>();
            ValidateTurnRequest(Read(LiveTurnPreparationService.TurnRequestPath), binding, issues);
            ValidateLiveWoundRoots(Read, bundle.Input, issues);
            ValidateLiveEffectRoots(Read, bundle.EffectBatchPlan.EffectPlan, issues);
            using var innerTiming4 = files.MeasureOperation("JointInput.ComposeReadSnapshot");
            var snapshot = PendingTurnSnapshotReader.ReadCurrent(files, lease, new[] { EffectAcceptedTurnInputComposer.WorldTimePath });
            innerTiming4?.Dispose();
            issues.AddRange(snapshot.Issues);
            if (!snapshot.Success || snapshot.Snapshot is not { } original || original.SessionId != binding.SessionId ||
                original.RequestId != binding.RequestId || original.SnapshotToken != binding.SnapshotToken ||
                original.TurnNumber != binding.Turn || original.Realm != binding.Realm ||
                EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(Read(EffectAcceptedTurnInputComposer.WorldTimePath)) != authority.Anchors.CurrentTimeInMinutes)
                throw new InvalidDataException("The current official original/clock does not match the complete joint frame.");
            var pending = ResourcePendingResolutionState.ParseCanonical(Read(ResourcePendingResolutionState.PendingPath),
                authority.Definitions, allowMissingPristine: true);
            issues.AddRange(pending.Issues);
            if (pending.State?.Requests.Count > 0)
                throw new InvalidDataException("Concurrent resource pending work is outside this joint owner.");
            if (issues.Count != 0) return PrivateFailed(issues);
            var sources = ResourceMutationSourceCatalog.Create(authority.RegisteredOutcome.SourceExports);
            issues.AddRange(sources.Issues);
            if (sources.Catalog is not { } sourceCatalog || issues.Count != 0) return PrivateFailed(issues);
            var commands = ResourceAcceptedTurnInputComposer.Parse(null);
            var acceptedEvents = new JsonObject { ["acceptedEvents"] = new JsonArray() };
            var woundCommands = new JsonObject { ["schemaVersion"] = 1,
                ["authorityKind"] = "accepted_mortal_joint_repair",
                ["inputFingerprint"] = bundle.InputFingerprint, ["bundleFingerprint"] = bundle.BundleFingerprint,
                ["anchorPlanFingerprint"] = authority.Anchors.Fingerprint,
                ["jointCommonAuthorityFingerprint"] = authority.Fingerprint };
            var definitionRoot = authority.Definitions.ToCanonicalRoot();
            var stateRoot = JsonNode.Parse(authority.State.ToCanonicalJson())!.AsObject();
            var historyRoot = JsonNode.Parse(authority.History.ToCanonicalJson())!.AsObject();
            var internalInputs = new JsonObject { ["authorityKind"] = "accepted_mortal_joint_repair",
                ["definitions"] = definitionRoot.DeepClone(), ["state"] = stateRoot.DeepClone(), ["history"] = historyRoot.DeepClone(),
                ["ownerFingerprint"] = authority.Owners.Fingerprint, ["sourceFingerprint"] = sourceCatalog.Fingerprint,
                ["woundInputFingerprint"] = bundle.InputFingerprint, ["woundStageBundleFingerprint"] = bundle.BundleFingerprint,
                ["jointCommonAuthorityFingerprint"] = authority.Fingerprint,
                ["woundAnchorPlanFingerprint"] = authority.Anchors.Fingerprint };
            internalInputs["jointProgressionBasisFingerprint"] = authority.Progression.Fingerprint;
            var effects = bundle.EffectBatchPlan.EffectPlan;
            var effectCommands = bundle.EffectBatchPlan.EffectInput.RawCommands;
            var effectIdentity = bundle.Input.PreTurnEffectIdentityIndex?.DeepClone().AsObject() ??
                throw new InvalidDataException("The authentic joint preparation lacks its original effect index.");
            var pendingRoot = pending.State?.ToCanonicalRoot() ?? new JsonObject();
            var fingerprints = new AcceptedMechanicsAuthorityFingerprints(
                Definitions: HashText("resource-definitions-v1", definitionRoot.ToJsonString()),
                Owners: authority.Owners.Fingerprint, ResourceState: authority.State.Fingerprint, ResourceHistory: authority.History.Fingerprint,
                EffectSources: HashText("accepted-mechanics-effect-sources-v1", effects.SourceAuthorityFingerprint),
                EffectTargets: HashText("accepted-mechanics-effect-targets-v1", effects.TargetAuthorityFingerprint),
                EffectCarriers: HashText("accepted-mechanics-effect-carriers-v1", effects.CarrierAuthorityFingerprint),
                EffectIdentityIndex: HashText("effect-index-v1", Read(EffectAcceptedTurnPlan.IdentityIndexPath) ?? "<missing>"),
                AcceptedEvents: HashNode("resource-events-v1", acceptedEvents),
                Commands: HashText("accepted-mechanics-commands-v1", commands.Root.ToJsonString() + "\n" + effectCommands.ToJsonString() + "\n" + woundCommands.ToJsonString()),
                Pending: HashNode("accepted-mechanics-pending-v1", pendingRoot),
                InternalInputs: HashNode("accepted-mechanics-internal-v1", internalInputs),
                WoundCarriers: HashText("accepted-mechanics-wound-carriers-v1", ComputeWoundCarrierAuthority(bundle.Input.PreTurnCarriers)),
                WoundIdentityIndex: HashText("accepted-mechanics-wound-index-v1", bundle.Input.PreTurnIdentityIndex.ToJsonString()),
                WoundHistory: HashText("accepted-mechanics-wound-history-v1", bundle.Input.PreTurnHistory.ToJsonString()));
            var context = new AcceptedMechanicsPlanningContext(definitionRoot, authority.Definitions, authority.State,
                authority.History, authority.Owners, sourceCatalog, commands, effectIdentity, effects,
                terminalOwners: authority.TerminalOwners, registeredSystemOutcomes: new[] { authority.RegisteredOutcome },
                pendingResolutionState: pending.State, resourceIdentityFactory: authority.RetainedCalculationInputs.ResourceIdentityFactory,
                woundStageBundle: bundle, woundAnchorPlan: authority.Anchors, jointRepairCommonAuthority: authority);
            var input = new AcceptedMechanicsInput(binding.SessionId, binding.RequestId, binding.SnapshotToken, binding.Realm,
                binding.Turn, acceptedEvents, commands.Root, effectCommands, pendingRoot, internalInputs, fingerprints,
                before, Array.Empty<ValidationIssue>(), context, woundCommands, bundle.Input);
            await authority.ValidateCurrentAsync(files, lease);
            return new(input, Array.Empty<ValidationIssue>());
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
            System.Text.Json.JsonException or IOException)
        {
            return PrivateFailed(Failed(WoundIdentityState.StatePath, "accepted_mechanics_joint_common_input_invalid",
                "the complete privately owned current joint input", error.Message).Issues);
        }
    }
    private static AcceptedMechanicsWoundCommonInputCompositionResult PrivateFailed(IReadOnlyList<ValidationIssue> issues) =>
        new(null, issues.Select(MortalWoundOpportunityAdapter.PrivateSourceIssue).ToArray());
}
