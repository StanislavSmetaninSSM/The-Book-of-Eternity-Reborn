using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Projects the entire bounded completed effect payload in the production field order, including both retained authority fingerprints.</summary>
    /// <param name="data">Detached full effect plan DATA whose closed absent branches are asserted before their empty framing is appended.</param>
    /// <returns>Every ordered nullable string consumed by the effect payload writer for this exact no-injury lane.</returns>
    private static IReadOnlyList<string?> ReadGuardianCappedRetryEffectPayloadHashFields(JsonObject data)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.effect.accepted_plan_payload", "1",
            data["InputFingerprint"]!.GetValue<string>(), data["CarrierAuthorityFingerprint"]!.GetValue<string>(),
            data["SourceAuthorityFingerprint"]!.GetValue<string>(), data["TargetAuthorityFingerprint"]!.GetValue<string>(),
            data["SkillScopeFingerprint"]?.GetValue<string>() ?? "none"
        };
        foreach (var name in new[] { "MortalRecoveryExecutionFingerprint", "SpiritualHealingExecutionFingerprint",
            "MortalDiagnosisExecutionFingerprint", "MortalTreatmentExecutionFingerprint" }) Assert.Null(data[name]);
        Assert.Empty(data["AllocatedCombatantIds"]!.AsArray()); Assert.Empty(data["AllocatedEffectIds"]!.AsArray());
        Assert.Single(data["AllocatedTransitionIds"]!.AsArray());
        foreach (var name in new[] { "AllocatedCombatantIds", "AllocatedEffectIds", "AllocatedTransitionIds" })
            AppendGuardianCappedRetryHashEffectOrderedStrings(fields, data[name]!.AsArray());
        foreach (var name in new[] { "WoundApplicationRootEffectBindings", "Sources", "Targets", "SourceBindings", "DeferredReactions" })
        {
            var rows = data[name]!.AsArray(); Assert.Empty(rows); fields.Add(GuardianCappedRetryHashNumber(rows.Count));
        }
        var expansionCount = data["ReactionExpansionCount"]!.GetValue<int>(); Assert.Equal(0, expansionCount);
        fields.Add(GuardianCappedRetryHashNumber(expansionCount));
        var expansion = data["ReactionExpansionUsage"]!.AsArray(); Assert.Empty(expansion); fields.Add(GuardianCappedRetryHashNumber(expansion.Count));
        var active = data["ActiveEffects"]!.AsArray(); Assert.Single(active); fields.Add(GuardianCappedRetryHashNumber(active.Count));
        for (var index = 0; index < active.Count; index++)
        { fields.Add(GuardianCappedRetryHashNumber(index)); fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(active[index])); }
        AppendGuardianCappedRetryHashEffectCarriers(fields, data["ResourceTriggerCarriers"]);
        fields.Add(data["RetainedSourceAuthorityFingerprint"]!.GetValue<string>());
        fields.Add(data["RetainedTargetAuthorityFingerprint"]!.GetValue<string>());
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(data["EventInput"]));
        AppendGuardianCappedRetryHashEffectCarrierMap(fields, data["CarrierBeforeImages"]!.AsObject());
        AppendGuardianCappedRetryHashEffectCarrierMap(fields, data["CarrierAfterImages"]!.AsObject());
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(data["IdentityIndexBeforeImage"]));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(data["IdentityIndexAfterImage"]));
        AppendGuardianCappedRetryHashEffectOrderedStrings(fields, data["TouchedPaths"]!.AsArray());
        AppendGuardianCappedRetryHashEffectOrderedStrings(fields, data["DeletedPaths"]!.AsArray());
        AppendGuardianCappedRetryHashEffectCarriers(fields, data["AcceptedCarrierBaselines"]);
        return fields;
    }

    /// <summary>Projects every common prepared-plan operand in production order after proving all absent stage/capability branches.</summary>
    /// <param name="envelope">Full existing images plus complete detached missing operands; none are substituted or omitted.</param>
    /// <param name="effectHash">Independently verified full completed effect payload, not an arbitrary digest binding.</param>
    /// <returns>The complete ordered nullable strings for this bounded common plan, including exact null and empty-map framing.</returns>
    private static IReadOnlyList<string?> ReadGuardianCappedRetryPreparedPlanHashFields(JsonObject envelope, string effectHash)
    {
        var data = envelope["Plan"]!.AsObject();
        envelope = data;
        var fields = new List<string?>
        {
            "book_of_eternity.accepted_mechanics.prepared_plan", "1", envelope["InputFingerprint"]!.GetValue<string>(),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(data["DefinitionAfterImage"]),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(envelope["StateAfterImage"]),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(envelope["HistoryAfterImage"])
        };
        var authorities = ReadGuardianCappedExact<AcceptedMechanicsAuthorityFingerprints>(data["AuthorityFingerprints"]).Enumerate()
            .OrderBy(row => row.Key, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "acceptedEvents", "commands", "definitions", "effectCarriers", "effectIdentityIndex", "effectSources", "effectTargets",
            "internalInputs", "owners", "pending", "resourceHistory", "resourceState", "woundCarriers", "woundHistory", "woundIdentityIndex" },
            authorities.Select(row => row.Key));
        fields.Add(GuardianCappedRetryHashNumber(authorities.Length));
        foreach (var row in authorities) { fields.Add(row.Key); fields.Add(row.Value); }
        AppendGuardianCappedRetryHashPreparedObjectMap(fields, envelope["EffectCarrierAfterImages"]!.AsObject());
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(envelope["EffectIdentityAfterImage"]));
        var pending = data["PendingAfterImages"]!.AsObject(); Assert.Empty(pending); fields.Add(GuardianCappedRetryHashNumber(pending.Count));
        Assert.Null(data["PendingGmPacket"]); fields.Add(null);
        Assert.Null(data["PendingPublicationAuthority"]); fields.Add(null);
        AppendGuardianCappedRetryHashPreparedObjectMap(fields, envelope["OwnerCompanionAfterImages"]!.AsObject());
        var owners = data["OwnerTransitions"]!.AsArray(); Assert.Empty(owners); fields.Add(GuardianCappedRetryHashNumber(owners.Count));
        var events = envelope["ResourceEvents"]!.AsArray(); fields.Add(GuardianCappedRetryHashNumber(events.Count));
        foreach (var node in events)
        {
            var row = node!.AsObject(); var coordinate = row["Coordinate"]!.AsObject();
            fields.Add(row["EventKind"]!.GetValue<string>()); fields.Add(row["OperationId"]!.GetValue<string>());
            fields.Add(row["EventRef"]!.GetValue<string>()); fields.Add(coordinate["Realm"]!.GetValue<string>());
            fields.Add(((ResourceOwnerKind)coordinate["OwnerKind"]!.GetValue<int>()).ToString());
            fields.Add(coordinate["ResourceOwnerId"]!.GetValue<string>()); fields.Add(coordinate["ResourceKey"]!.GetValue<string>());
            fields.Add(row["Before"]!.GetValue<decimal>().ToString("G29", CultureInfo.InvariantCulture));
            fields.Add(row["After"]!.GetValue<decimal>().ToString("G29", CultureInfo.InvariantCulture));
            fields.Add(row["AppliedAmount"]!.GetValue<decimal>().ToString("G29", CultureInfo.InvariantCulture));
            fields.Add(GuardianCappedRetryHashNumber(row["Turn"]!.GetValue<int>())); fields.Add(GuardianCappedRetryHashNumber(row["ExecutionSequence"]!.GetValue<int>()));
            fields.Add(row["SourceFingerprint"]!.GetValue<string>());
        }
        var projection = data["ProjectionInput"]!.AsObject();
        Assert.True(JsonNode.DeepEquals(data["DefinitionAfterImage"], projection["Definitions"]));
        Assert.True(JsonNode.DeepEquals(envelope["StateAfterImage"], projection["State"]));
        Assert.True(JsonNode.DeepEquals(envelope["HistoryAfterImage"], projection["History"]));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(projection["Definitions"]));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(projection["State"]));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(projection["History"]));
        fields.Add(projection["OwnerAuthorityFingerprint"]!.GetValue<string>()); fields.Add(data["OwnerAuthorityFingerprint"]!.GetValue<string>());
        fields.Add("present"); fields.Add(effectHash);
        Assert.Null(data["WoundStageBundle"]); fields.Add(null);
        foreach (var name in new[] { "LiveWoundProofFingerprint", "WoundAnchorPlanFingerprint", "DirectWoundPublicationAuthority", "TreatmentResourcePublicationAuthority" })
        { Assert.Null(data[name]); fields.Add(null); }
        // ComputePrepared adds no field at all for the absent joint-repair branch.
        Assert.Null(data["JointRepairCommonAuthority"]);
        Assert.Empty(envelope["WoundCarrierAfterImages"]!.AsObject());
        AppendGuardianCappedRetryHashPreparedObjectMap(fields, envelope["WoundCarrierAfterImages"]!.AsObject());
        Assert.Null(data["WoundIdentityAfterImage"]); fields.Add(null);
        Assert.Null(data["WoundHistoryAfterImage"]); fields.Add(null);
        var before = envelope["BeforeImages"]!.AsArray().Select(row => row!.AsObject()).OrderBy(row => row["path"]!.GetValue<string>(), StringComparer.Ordinal).ToArray();
        fields.Add(GuardianCappedRetryHashNumber(before.Length));
        foreach (var row in before)
        {
            fields.Add(row["path"]!.GetValue<string>()); fields.Add(row["Existed"]!.GetValue<bool>() ? "present" : "missing");
            fields.Add(row["bytesBase64"]?.GetValue<string>());
        }
        AppendGuardianCappedRetryHashOrderedStrings(fields, envelope["TouchedPaths"]!.AsArray());
        AppendGuardianCappedRetryHashOrderedStrings(fields, envelope["ConsumedPaths"]!.AsArray());
        return fields;
    }

    /// <summary>Appends every retained effect carrier in its declared production order, preserving explicit null carriers.</summary>
    /// <param name="fields">Independent ordered hash fields being constructed.</param>
    /// <param name="carriers">Full detached carrier catalog, or an explicitly absent catalog.</param>
    private static void AppendGuardianCappedRetryHashEffectCarriers(ICollection<string?> fields, JsonNode? carriers)
    {
        if (carriers is null) { fields.Add(null); return; }
        fields.Add("present");
        foreach (var name in new[] { "PlayerEffects", "NpcEffects", "EnemyCombatants", "AllyCombatants", "AfterlifeProfiles", "SpiritualConflict" })
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(carriers[name]));
    }

    /// <summary>Appends a complete effect before/after carrier map, with the count and sorted per-entry index required by its payload hash.</summary>
    /// <param name="fields">Independent ordered hash fields being constructed.</param>
    /// <param name="images">Every detached carrier image, including explicitly absent before-images.</param>
    private static void AppendGuardianCappedRetryHashEffectCarrierMap(ICollection<string?> fields, JsonObject images)
    {
        var rows = images.OrderBy(row => row.Key, StringComparer.Ordinal).ToArray(); fields.Add(GuardianCappedRetryHashNumber(rows.Length));
        for (var index = 0; index < rows.Length; index++)
        { fields.Add(GuardianCappedRetryHashNumber(index)); fields.Add(rows[index].Key); fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(rows[index].Value)); }
    }

    /// <summary>Appends a complete common-plan object map in its production count/key/value order, without effect-map indexes.</summary>
    /// <param name="fields">Independent ordered hash fields being constructed.</param>
    /// <param name="images">All detached images, never a subset selected to make fingerprints agree.</param>
    private static void AppendGuardianCappedRetryHashPreparedObjectMap(ICollection<string?> fields, JsonObject images)
    {
        var rows = images.OrderBy(row => row.Key, StringComparer.Ordinal).ToArray(); fields.Add(GuardianCappedRetryHashNumber(rows.Length));
        foreach (var row in rows) { fields.Add(row.Key); fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(row.Value)); }
    }

    /// <summary>Appends the effect payload's actual count followed by each index and ordered value, including empty arrays.</summary>
    /// <param name="fields">Independent effect payload hash fields being constructed.</param>
    /// <param name="values">Exact detached effect list whose indices are part of its producer's hash framing.</param>
    private static void AppendGuardianCappedRetryHashEffectOrderedStrings(ICollection<string?> fields, JsonArray values)
    {
        fields.Add(GuardianCappedRetryHashNumber(values.Count));
        for (var index = 0; index < values.Count; index++)
        { fields.Add(GuardianCappedRetryHashNumber(index)); fields.Add(values[index]!.GetValue<string>()); }
    }

    /// <summary>Appends the actual count and every ordered string, preserving array order and all empty arrays.</summary>
    /// <param name="fields">Independent ordered hash fields being constructed.</param>
    /// <param name="values">Exact detached ordered values.</param>
    private static void AppendGuardianCappedRetryHashOrderedStrings(ICollection<string?> fields, JsonArray values)
    {
        fields.Add(GuardianCappedRetryHashNumber(values.Count)); foreach (var value in values) fields.Add(value!.GetValue<string>());
    }

    /// <summary>Formats one actual count or index using the production invariant decimal representation.</summary>
    /// <param name="value">Actual retained count, ordinal or index.</param>
    /// <returns>Invariant decimal string, without a guessed diagnostic count.</returns>
    private static string GuardianCappedRetryHashNumber(int value) => value.ToString(CultureInfo.InvariantCulture);
    /// <summary>One exact producer-derived value at its structural output path, retained for validation and coherent detached resealing.</summary>
    private sealed record GuardianCappedRetryHash(string Path, string Value, string Calculated);

    /// <summary>Derives every retained byte/image, packet, source, decision, lifecycle and common-plan digest from complete operands.</summary>
    private static List<GuardianCappedRetryHash> ReadGuardianCappedRetryHashes(GuardianCappedRetryDocument document)
    {
        var result = new List<GuardianCappedRetryHash>();
        foreach (var surface in document.Surfaces()) foreach (var (path, row) in surface.Objects)
        {
            if (row.ContainsKey("bytesBase64") && row.ContainsKey("Existed") && row.ContainsKey("Fingerprint"))
            {
                GuardianCappedClosed(row, "path Existed bytesBase64 Fingerprint");
                var existed = row["Existed"]!.GetValue<bool>(); var bytes = row["bytesBase64"] is null ? null : Convert.FromBase64String(row["bytesBase64"]!.GetValue<string>());
                Assert.Equal(existed, bytes is not null); Add(path, row, "Fingerprint", new CanonicalBeforeImage(existed, bytes).Fingerprint);
            }
            if (row.ContainsKey("contentBase64") && row.ContainsKey("contentFingerprint"))
            {
                var existed = row["existed"]?.GetValue<bool>() ?? true;
                var bytes = row["contentBase64"] is null ? null : Convert.FromBase64String(row["contentBase64"]!.GetValue<string>());
                Assert.Equal(existed, bytes is not null); Add(path, row, "contentFingerprint", row.ContainsKey("existed") ? new CanonicalBeforeImage(existed, bytes).Fingerprint : "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes!)).ToLowerInvariant());
            }
            if (row.ContainsKey("sourceFingerprint") && row.ContainsKey("strainPreimage") && row.ContainsKey("turnEvidence"))
            {
                var hash = SpiritualWoundStateJson.Hash(row, "source", "sourceId", "sourceFingerprint");
                Add(path, row, "sourceFingerprint", hash);
                Add(path, row, "sourceId", "spiritual_source_" + hash[7..]);
            }
            if (row.ContainsKey("retainedPrefixFingerprint") && row.ContainsKey("packetFingerprint"))
            {
                Add(path, row, "retainedPrefixFingerprint", SpiritualWoundDecisionPendingState.ComputePrefixFingerprint(row));
                Add(path, row, "packetFingerprint", SpiritualWoundStateJson.Hash(row, "pending_packet", "packetFingerprint"));
            }
            if (row.ContainsKey("checkpointFingerprint"))
                Add(path, row, "checkpointFingerprint", SpiritualWoundStateJson.Hash(row, "spiritual_capture_checkpoint_v1", "checkpointFingerprint"));
            if (row.ContainsKey("decisionFingerprint") && row.ContainsKey("decision") && row.ContainsKey("opportunityRef"))
            {
                var domain = row.ContainsKey("binding") ? "decision" : "staged_decision";
                Add(path, row, "decisionFingerprint", SpiritualWoundStateJson.Hash(row, domain, "decisionFingerprint"));
                if (row.ContainsKey("decisionId"))
                {
                    var decision = document.Root["Completed"]!["NoneDecision"]!;
                    // Only the newly appended injury-only none is this attempt's staged decision. Old retained decisions stay invariant.
                    if (row["sourceId"]!.GetValue<string>() == document.Root["First"]!["Witness"]!["sourceId"]!.GetValue<string>())
                        Add(path, row, "decisionId", "spiritual_decision_" + decision["decisionFingerprint"]!.GetValue<string>()[7..]);
                }
            }
            foreach (var (field, input, kind) in new[] { ("beforeImageRef", "beforeImage", "effect_image"), ("afterImageRef", "afterImage", "effect_image"), ("initialRosterRef", "initialRoster", "carrier_roster"), ("rosterRef", "roster", "carrier_roster") })
                if (row[field] is not null) Add(path, row, field, GuardianCappedLifecycleArtifact(kind, row[input]));
        }
        var root = document.Root.AsObject(); var first = root["First"]!.AsObject(); var interval = first["Interval"]!.AsObject();
        var plan = root["Plan"]!.AsObject(); var effects = plan["EffectPlan"]!.AsObject(); var witness = first["Witness"]!.AsObject();
        var transitions = interval["AppliedTransitions"]!.AsArray().Select(ReadGuardianCappedExact<ResourceTransition>).ToArray();
        var events = interval["Events"]!.AsArray().Select(ReadGuardianCappedExact<ResourceAppliedEvent>).ToArray();
        var boundaries = RecomputeGuardianCappedBoundaryHashes(root);
        result.Add(new("/First/Interval/EffectAfter/Fingerprint", interval["EffectAfter"]!["Fingerprint"]!.GetValue<string>(), boundaries.After));
        result.Add(new("/Completed/Reduction/Resources/FullEffectBoundary/Fingerprint", root["Completed"]!["Reduction"]!["Resources"]!["FullEffectBoundary"]!["Fingerprint"]!.GetValue<string>(), boundaries.Terminal));
        var resourceHash = WoundAcceptedTurnFingerprintWriter.Compute(new string?[] { "book_of_eternity.spiritual_closed_resource_result", "1", interval["ConflictId"]!.GetValue<string>(), interval["ExchangeId"]!.GetValue<string>(), "0", JsonSerializer.Serialize(transitions), JsonSerializer.Serialize(Array.Empty<ResourceTransition>()), JsonSerializer.Serialize(events) });
        Add("/First/Witness", witness, "resourceResultFingerprint", resourceHash);
        var effectPrefix = WoundAcceptedTurnFingerprintWriter.Compute(new[] { "book_of_eternity.spiritual_closed_effect_prefix", "1", effects["AcceptedBoundaryBasePlanFingerprint"]!.GetValue<string>(), interval["EffectAfter"]!["Fingerprint"]!.GetValue<string>() });
        Add("/First/Witness", witness, "effectPlanFingerprint", effectPrefix);
        var effectFields = ReadGuardianCappedRetryEffectPayloadHashFields(effects);
        Add("/Plan/EffectPlan", effects, "AcceptedBoundaryFinalPlanFingerprint", WoundAcceptedTurnFingerprintWriter.Compute(effectFields));
        var ledger = effects["LifecycleExecutionLedger"]!.AsObject();
        Add("/Plan/EffectPlan", effects, "LifecycleExecutionLedgerFingerprint", WoundAcceptedTurnFingerprintWriter.Compute(new[] { "book_of_eternity.effect.lifecycle_execution_ledger", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(ledger) }));
        var binding = ReadGuardianCappedBindingData(root["PublicationBinding"]!.AsObject());
        Add("/Plan", plan, "InputFingerprint", AcceptedMechanicsPlanFingerprints.ComputeInput(binding));
        Add("/Plan", plan, "PreparedPlanFingerprint", WoundAcceptedTurnFingerprintWriter.Compute(ReadGuardianCappedRetryPreparedPlanHashFields(root, effects["AcceptedBoundaryFinalPlanFingerprint"]!.GetValue<string>())));
        return result;

        void Add(string path, JsonObject row, string field, string computed)
        {
            result.Add(new(path + "/" + GuardianCappedEscape(field), row[field]!.GetValue<string>(), computed));
        }
    }

    /// <summary>Uses the lifecycle producer's explicit artifact domain and complete canonical operand.</summary>
    private static string GuardianCappedLifecycleArtifact(string kind, JsonNode? image) => WoundAcceptedTurnFingerprintWriter.Compute(new[] { "book_of_eternity.effect.lifecycle_execution_ledger.artifact", "1", kind, WoundAcceptedTurnFingerprintWriter.CanonicalJson(image) });

    /// <summary>Recreates only detached ordinary binding DATA through the real input-fingerprint contract.</summary>
    private static AcceptedMechanicsPlanBinding ReadGuardianCappedBindingData(JsonObject row)
    {
        GuardianCappedClosed(row, "SessionId RequestId SnapshotToken Realm Turn AcceptedEvents ResourceCommands EffectCommands PendingInput InternalInputs WoundCommands WoundInput AuthorityFingerprints BeforeImages");
        Assert.Null(row["WoundInput"]);
        return new(row["SessionId"]!.GetValue<string>(), row["RequestId"]!.GetValue<string>(), row["SnapshotToken"]!.GetValue<string>(), row["Realm"]!.GetValue<string>(), row["Turn"]!.GetValue<int>(),
            row["AcceptedEvents"]!.AsObject(), row["ResourceCommands"]!.AsObject(), row["EffectCommands"]!.AsObject(), row["PendingInput"]!.AsObject(), row["InternalInputs"]!.AsObject(),
            ReadGuardianCappedExact<AcceptedMechanicsAuthorityFingerprints>(row["AuthorityFingerprints"]),
            row["BeforeImages"]!.AsArray().ToDictionary(node => node!["path"]!.GetValue<string>(), node => new CanonicalBeforeImage(node!["Existed"]!.GetValue<bool>(), node["bytesBase64"] is null ? null : Convert.FromBase64String(node["bytesBase64"]!.GetValue<string>())), StringComparer.Ordinal), row["WoundCommands"]?.AsObject());
    }

    /// <summary>Rebuilds actual prefix/terminal fingerprints using the real typed transcript builder, without issuing execution authority.</summary>
    private static (string After, string Terminal) RecomputeGuardianCappedBoundaryHashes(JsonObject root)
    {
        var interval = root["First"]!["Interval"]!; var before = interval["EffectBefore"]!;
        var stamp = ReadGuardianCappedExact<EffectAcceptedPlanAuthorityStamp>(before["PlanAuthority"]);
        var boundary = ReadGuardianCappedExact<EffectEventBoundaryStamp>(Assert.Single(before["Boundaries"]!.AsArray()));
        var accepted = Assert.Single(before["AcceptedActivations"]!.AsArray())!;
        var proposed = ReadGuardianCappedExact<EffectActivationCandidate>(accepted["Candidate"]!["Activation"]);
        var candidate = new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(proposed, null, null, [], [], new Dictionary<ResourceOperationKey, string>(), [], [], EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin.Empty);
        var activation = ReadGuardianCappedExact<AcceptedEffectActivation>(accepted["Activation"]);
        var builder = new AcceptedEffectBoundaryTranscript.Builder(stamp);
        var opened = builder.OpenBoundary(boundary.ParentBoundaryOrdinal, boundary.Producer, boundary.EventKind, boundary.ProducerEventRef, boundary.ProducerTransitionId, boundary.ProducerExecutionSequence, boundary.ProducerMechanicsOrdinal, [candidate]);
        builder.RecordAccepted(opened, activation, candidate); Assert.Null(builder.BindCausalClosure(opened, [], new Dictionary<string, string>(StringComparer.Ordinal))); Assert.Null(builder.CloseBoundary(opened));
        foreach (var node in interval["EffectAfter"]!["ResourceMutations"]!.AsArray())
        {
            var evidence = ReadGuardianCappedExact<AcceptedResourceMutationEvidence>(node); var ordinal = builder.RecordMutationExecution();
            Assert.Equal(evidence.MechanicsOrdinal, ordinal); builder.RecordResourceMutation(evidence.Mutation, evidence.Transition, evidence.ExecutionKind, ordinal);
        }
        var after = builder.CaptureClosedPrefix(); Assert.True(after.IsValid); Assert.Empty(after.Issues);
        _ = builder.SealUseProjection(); var terminal = builder.Freeze(); Assert.True(terminal.IsValid); Assert.Empty(terminal.Issues);
        return (after.Prefix!.Fingerprint, terminal.Transcript!.Fingerprint);
    }
}
