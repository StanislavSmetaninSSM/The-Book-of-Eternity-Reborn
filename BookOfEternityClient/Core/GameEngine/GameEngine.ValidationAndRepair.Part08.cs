using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{

    private static List<JsonObject> BuildValidationRepairHarnessPackets(
        IReadOnlyList<ValidationIssue> errors,
        IReadOnlyCollection<string>? guardianActorNameHints = null,
        IReadOnlyList<EffectRepairPacket>? effectRepairPackets = null)
    {
        var packets = new List<ValidationRepairHarnessPacket>();
        var guardianPendingCreationMaterializationErrors = errors.Where(IsGuardianPendingCreationMaterializationRepairIssue).ToList();
        var guardianScopeErrors = errors.Where(IsGuardianScopeRepairIssue).ToList();
        var guardianScopeActorNames = CollectRepairActorNames(guardianScopeErrors, guardianActorNameHints);
        var actorReasoningSubpointErrors = errors
            .Where(IsActorReasoningSubpointRepairIssue)
            .Where(issue => !guardianScopeActorNames.Contains(NormalizeRepairActorName(issue.Actor)))
            .ToList();
        var actorMemoryPersistenceErrors = errors.Where(IsActorMemoryPersistenceRepairIssue).ToList();
        var actorMaterializationErrors = errors.Where(IsActorMaterializationRepairIssue).ToList();
        var factionMaterializationGroups = errors
            .Where(IsFactionMaterializationRepairIssue)
            .Select(issue => (
                Issue: issue,
                Coordinate: ResolveFactionMaterializationRepairCoordinate(issue)))
            .Where(candidate => candidate.Coordinate != null)
            .GroupBy(
                candidate => candidate.Coordinate!,
                candidate => candidate.Issue,
                StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();
        var factionIdentityErrors = errors
            .Where(issue => !IsRoutableFactionMaterializationRepairIssue(issue))
            .Where(IsFactionIdentityRepairIssue)
            .ToList();
        var mortalFactionResourceErrors = errors
            .Where(issue => !IsRoutableFactionMaterializationRepairIssue(issue))
            .Where(IsMortalFactionResourceRepairIssue)
            .ToList();
        var mortalBootstrapMaterializationErrors = errors.Where(IsMortalBootstrapMaterializationRepairIssue).ToList();
        var mortalNpcScopeErrors = errors.Where(IsMortalNpcScopeRepairIssue).ToList();
        var mortalNpcLocationErrors = errors.Where(IsMortalNpcLocationRepairIssue).ToList();
        var mortalNpcInventoryUpdateErrors = errors.Where(IsMortalNpcInventoryUpdateRepairIssue).ToList();
        var mortalNpcFullObjectErrors = errors.Where(IsMortalNpcFullObjectRepairIssue).ToList();
        var mortalNpcRelationshipEnumErrors = errors.Where(IsMortalNpcRelationshipEnumRepairIssue).ToList();
        var mortalNpcReferenceErrors = errors.Where(IsMortalNpcReferenceRepairIssue).ToList();
        var mortalCombatStateErrors = errors.Where(IsMortalCombatStateRepairIssue).ToList();
        var mortalSkillProgressionShapeErrors = errors.Where(IsMortalSkillProgressionShapeRepairIssue).ToList();
        var afterlifeChronicleStringArrayErrors = errors.Where(IsAfterlifeChronicleStringArrayRepairIssue).ToList();
        var guardianTradeInventoryResolutionErrors = errors.Where(IsGuardianTradeInventoryResolutionRepairIssue).ToList();
        var afterlifeActionCostErrors = errors.Where(IsAfterlifeSpiritualConflictActionCostRepairIssue).ToList();
        var afterlifeConflictRewardErrors = errors.Where(IsAfterlifeSpiritualConflictRewardRepairIssue).ToList();
        var afterlifeEntityProfileScaffoldErrors = errors.Where(IsAfterlifeEntityProfileScaffoldRepairIssue).ToList();
        var mortalTrainingSkillEvolutionErrors = errors.Where(IsMortalTrainingSkillEvolutionRepairIssue).ToList();
        var mortalSkillReferenceIdentityErrors = errors.Where(IsMortalSkillReferenceIdentityRepairIssue).ToList();
        var trainingShowcaseSnapshotErrors = errors.Where(IsTrainingShowcaseSnapshotRepairIssue).ToList();
        var npcScopeDeclarationErrors = errors.Where(IsNpcScopeDeclarationRepairIssue).ToList();
        var acceptedTurnOutputArtifactErrors = errors.Where(IsAcceptedTurnOutputArtifactRepairIssue).ToList();
        var mortalItemRepairPackets = MortalItemRepairPacketBuilder.Build(errors);
        var mortalLocationRepairPackets = MortalLocationRepairPacketBuilder.Build(errors);
        var resourceRepairPackets = ResourceRepairPacketBuilder.Build(errors);

        if (guardianPendingCreationMaterializationErrors.Count > 0)
            packets.Add(BuildGuardianPendingCreationMaterializationRepairPacket(guardianPendingCreationMaterializationErrors));

        if (guardianScopeErrors.Count > 0)
            packets.Add(BuildGuardianScopeRepairPacket(guardianScopeErrors, guardianActorNameHints));

        if (npcScopeDeclarationErrors.Count > 0)
            packets.Add(BuildNpcScopeDeclarationRepairPacket(npcScopeDeclarationErrors));

        if (acceptedTurnOutputArtifactErrors.Count > 0)
        {
            var outputArtifactPacket = BuildAcceptedTurnOutputArtifactRepairPacket(acceptedTurnOutputArtifactErrors);
            if (outputArtifactPacket.TargetFiles.Count > 0)
                packets.Add(outputArtifactPacket);
        }

        if (actorReasoningSubpointErrors.Count > 0)
            packets.Add(BuildActorReasoningSubpointRepairPacket(actorReasoningSubpointErrors));

        if (actorMemoryPersistenceErrors.Count > 0)
            packets.Add(BuildActorMemoryPersistenceRepairPacket(actorMemoryPersistenceErrors, guardianActorNameHints));

        if (actorMaterializationErrors.Count > 0)
            packets.Add(BuildActorMaterializationRepairPacket(actorMaterializationErrors));

        packets.AddRange(mortalItemRepairPackets.Select(BuildMortalItemRepairHarnessPacket));
        packets.AddRange(mortalLocationRepairPackets.Select(BuildMortalLocationRepairHarnessPacket));
        packets.AddRange(resourceRepairPackets.Select(BuildResourceRepairHarnessPacket));
        if (effectRepairPackets is { Count: > 0 })
        {
            packets.AddRange(effectRepairPackets.Select(
                BuildEffectRepairHarnessPacket));
        }

        foreach (var factionMaterializationGroup in factionMaterializationGroups)
        {
            packets.Add(BuildFactionMaterializationRepairPacket(
                factionMaterializationGroup.Key,
                factionMaterializationGroup.ToList()));
        }

        if (factionIdentityErrors.Count > 0)
            packets.Add(BuildFactionIdentityRepairPacket(factionIdentityErrors));

        if (mortalFactionResourceErrors.Count > 0)
            packets.Add(BuildMortalFactionResourceRepairPacket(mortalFactionResourceErrors));

        if (mortalBootstrapMaterializationErrors.Count > 0)
            packets.Add(BuildMortalBootstrapMaterializationRepairPacket(mortalBootstrapMaterializationErrors));

        if (mortalNpcScopeErrors.Count > 0)
            packets.Add(BuildMortalNpcScopeRepairPacket(mortalNpcScopeErrors));

        if (mortalNpcLocationErrors.Count > 0)
            packets.Add(BuildMortalNpcLocationRepairPacket(mortalNpcLocationErrors));

        if (mortalNpcInventoryUpdateErrors.Count > 0)
            packets.Add(BuildMortalNpcInventoryUpdateRepairPacket(mortalNpcInventoryUpdateErrors));

        if (mortalNpcFullObjectErrors.Count > 0)
            packets.Add(BuildMortalNpcFullObjectRepairPacket(mortalNpcFullObjectErrors));

        if (mortalNpcRelationshipEnumErrors.Count > 0)
            packets.Add(BuildMortalNpcRelationshipEnumRepairPacket(mortalNpcRelationshipEnumErrors));

        if (mortalNpcReferenceErrors.Count > 0)
            packets.Add(BuildMortalNpcReferenceRepairPacket(mortalNpcReferenceErrors));

        if (mortalCombatStateErrors.Count > 0)
            packets.Add(BuildMortalCombatStateRepairPacket(mortalCombatStateErrors));

        if (mortalSkillProgressionShapeErrors.Count > 0)
            packets.Add(BuildMortalSkillProgressionShapeRepairPacket(mortalSkillProgressionShapeErrors));

        if (mortalTrainingSkillEvolutionErrors.Count > 0)
            packets.Add(BuildMortalTrainingSkillEvolutionRepairPacket(mortalTrainingSkillEvolutionErrors));

        if (mortalSkillReferenceIdentityErrors.Count > 0)
            packets.Add(BuildMortalSkillReferenceIdentityRepairPacket(mortalSkillReferenceIdentityErrors));

        if (trainingShowcaseSnapshotErrors.Count > 0)
            packets.Add(BuildTrainingShowcaseSnapshotHashRepairPacket(trainingShowcaseSnapshotErrors));

        if (afterlifeChronicleStringArrayErrors.Count > 0)
            packets.Add(BuildAfterlifeChronicleStringArrayRepairPacket(afterlifeChronicleStringArrayErrors));

        if (guardianTradeInventoryResolutionErrors.Count > 0)
            packets.Add(BuildGuardianTradeInventoryResolutionRepairPacket(guardianTradeInventoryResolutionErrors));

        if (afterlifeActionCostErrors.Count > 0)
            packets.Add(BuildAfterlifeSpiritualConflictActionCostRepairPacket(afterlifeActionCostErrors));

        if (afterlifeConflictRewardErrors.Count > 0)
            packets.Add(BuildAfterlifeSpiritualConflictRewardRepairPacket(afterlifeConflictRewardErrors));

        if (afterlifeEntityProfileScaffoldErrors.Count > 0)
            packets.Add(BuildAfterlifeEntityProfileScaffoldRepairPacket(afterlifeEntityProfileScaffoldErrors));

        var serializedPackets = packets
            .Select(SerializeValidationRepairHarnessPacket)
            .ToList();
        serializedPackets.AddRange(WoundRepairPacketBuilder.Build(errors)
            .Select(static packet => packet.ToJsonObject()));
        return serializedPackets;
    }

    private static JsonObject SerializeValidationRepairHarnessPacket(
        ValidationRepairHarnessPacket packet) =>
        JsonSerializer.SerializeToNode(packet, JsonOpts)?.AsObject() ??
        throw new InvalidDataException(
            "Validation repair harness packet could not be serialized.");

    private static string? ReadValidationRepairPacketKind(JsonObject packet) =>
        packet["kind"] is JsonValue value &&
        value.TryGetValue<string>(out var kind)
            ? kind
            : null;

    private static ValidationRepairHarnessPacket BuildMortalItemRepairHarnessPacket(
        MortalItemRepairPacket source)
    {
        return new ValidationRepairHarnessPacket
        {
            Kind = source.Kind,
            Priority = source.Priority,
            Title = source.Title,
            TargetFiles = source.TargetFiles.ToList(),
            TemplateRefs = source.TemplateRefs.ToList(),
            CanonicalActorNames = source.CanonicalActorNames.ToList(),
            TransitionClass = source.TransitionClass,
            Route = source.Route,
            SourceCarrier = CreateMortalItemRepairCarrierElement(source.SourceCarrier),
            DestinationCarrier = CreateMortalItemRepairCarrierElement(source.DestinationCarrier),
            MissingFields = source.MissingFields.ToList(),
            ExactFieldCorrections = source.ExactFieldCorrections
                .Select(correction => new ValidationRepairExactFieldCorrection
                {
                    Path = correction.Path,
                    Expected = correction.Expected,
                    Actual = correction.Actual,
                    Code = correction.Code,
                    RepairHint = correction.RepairHint
                })
                .ToList(),
            RequiredCompanionTargets = source.RequiredCompanionTargets.ToList(),
            ExpectedAuthority = source.ExpectedAuthority.ToList(),
            ActualEvidence = source.ActualEvidence.ToList(),
            ExpectedShape = source.ExpectedShape.ToList(),
            SafeCorrectionRules = source.SafeCorrectionRules.ToList(),
            Steps = source.Steps.ToList(),
            DebugLogTemplate = string.Empty,
            DoNotDo = source.DoNotDo.ToList()
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalLocationRepairHarnessPacket(
        MortalLocationRepairPacket source)
    {
        return new ValidationRepairHarnessPacket
        {
            Kind = source.Kind,
            Priority = source.Priority,
            Title = source.Title,
            TargetFiles = source.TargetFiles.ToList(),
            TemplateRefs = source.TemplateRefs.ToList(),
            CanonicalActorNames = source.CanonicalActorNames.ToList(),
            TransitionClass = source.TransitionClass,
            Route = source.Route,
            RawCarrier = source.RawCarrier,
            RawCoordinate = source.RawCoordinate,
            MissingFields = source.MissingFields.ToList(),
            InvalidFields = source.InvalidFields.ToList(),
            Conflicts = source.Conflicts.ToList(),
            ExactFieldCorrections = source.ExactFieldCorrections
                .Select(correction => new ValidationRepairExactFieldCorrection
                {
                    Path = correction.Path,
                    Expected = correction.Expected,
                    Actual = correction.Actual,
                    Code = correction.Code,
                    RepairHint = correction.RepairHint
                })
                .ToList(),
            RequiredCompanionTargets = source.RequiredCompanionTargets.ToList(),
            ExpectedAuthority = source.ExpectedAuthority.ToList(),
            ActualEvidence = source.ActualEvidence.ToList(),
            ExpectedShape = source.ExpectedShape.ToList(),
            SafeCorrectionRules = source.SafeCorrectionRules.ToList(),
            Steps = source.Steps.ToList(),
            DebugLogTemplate = string.Empty,
            DoNotDo = source.DoNotDo.ToList()
        };
    }

    private static ValidationRepairHarnessPacket BuildResourceRepairHarnessPacket(
        ResourceRepairPacket source)
    {
        return new ValidationRepairHarnessPacket
        {
            Kind = source.Kind,
            Priority = source.Priority,
            Title = source.Title,
            TargetFiles = source.TargetFiles.ToList(),
            TransitionClass = source.CommandSemantic,
            Route = source.Route,
            MissingFields = source.MissingSemantics.ToList(),
            FullTurnResubmissionRequired = source.FullTurnResubmissionRequired,
            ResubmissionObligations = source.ResubmissionObligations.ToList(),
            ExpectedShape = source.ExpectedShape.ToList(),
            SafeCorrectionRules = source.SafeCorrectionRules.ToList(),
            Steps = source.Steps.ToList(),
            DebugLogTemplate = string.Empty,
            DoNotDo = source.DoNotDo.ToList()
        };
    }

    private static ValidationRepairHarnessPacket BuildEffectRepairHarnessPacket(
        EffectRepairPacket source)
    {
        return new ValidationRepairHarnessPacket
        {
            Kind = source.Kind,
            Priority = "blocking",
            Title = "Repair one exact source-bounded effect parameter",
            TargetFiles = source.TargetFiles.ToList(),
            TemplateRefs = new List<string>
            {
                "specs/1535-complete-effect-materialization/contracts/effect-command-and-envelope.md",
                "specs/1535-complete-effect-materialization/contracts/effect-atomic-repair-and-rollback.md"
            },
            CanonicalActorNames = new List<string> { source.Actor },
            Actor = source.Actor,
            TransitionClass = "apply",
            Route = source.Route,
            RawCarrier = EffectAcceptedTurnPlan.CommandPath,
            RawCoordinate = source.RawCoordinate,
            ExpectedSource = source.ExpectedSource.DeepClone().AsObject(),
            ExpectedTarget = source.ExpectedTarget.DeepClone().AsObject(),
            ExpectedDefinitionKey = source.ExpectedDefinitionKey,
            ExpectedEventRef = source.ExpectedEventRef.DeepClone().AsObject(),
            ExactFieldCorrections = source.ExactFieldCorrections
                .Select(correction => new ValidationRepairExactFieldCorrection
                {
                    Path = correction.Path,
                    Expected = correction.ExpectedValueJson,
                    Actual = "missing",
                    Code = correction.Code,
                    RepairHint = correction.RepairHint
                })
                .ToList(),
            FullTurnResubmissionRequired = source.FullTurnResubmissionRequired,
            ResubmissionObligations = source.ResubmissionObligations.ToList(),
            ExpectedShape = new List<string>
            {
                "One complete rejected-turn response with the exact effect operation restored.",
                "The listed scalar parameter equals the one source-owned value.",
                "All unrelated accepted-turn changes and player-facing output are resubmitted coherently."
            },
            SafeCorrectionRules = new List<string>
            {
                "Edit only the listed rawCoordinate in effect_commands.json.",
                "Preserve exact source, target, definitionKey, eventRef, and every unrelated response path.",
                "Signal ready only after the complete response has been rewritten."
            },
            Steps = new List<string>
            {
                "Restore the full rejected response against the already restored pre-turn baseline.",
                "Apply the one exactFieldCorrection without changing any selector or client-owned state.",
                "Resubmit every required path, then signal validation repair ready."
            },
            DoNotDo = new List<string>
            {
                "Do not author effectId, stack/lifetime state, transition history, receipt, index, pending, carrier, resource, or wound state.",
                "Do not retarget the source, target, realm, event, wound, or owner.",
                "Do not submit a ready marker or effect-only fragment without the complete rejected response."
            }
        };
    }

    private static JsonElement CreateMortalItemRepairCarrierElement(
        MortalItemCarrierCoordinate? carrier)
    {
        if (carrier == null)
            return JsonSerializer.SerializeToElement<object?>(null, JsonOpts);

        return JsonSerializer.SerializeToElement(
            new
            {
                kind = carrier.Kind,
                ownerId = carrier.OwnerId,
                containerId = carrier.ContainerId,
                containerPath = carrier.ContainerPath
            },
            JsonOpts);
    }

    private static bool IsGuardianScopeRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "active_guardian_missing_from_scope", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGuardianPendingCreationMaterializationRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "guardian_materialized_without_create_surface", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "stale_pending_guardian_creation_after_materialization", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "pending_guardian_creation_missing_materialized_guardian", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "pending_guardian_creation_unresolved_after_startup_turn", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsActorReasoningSubpointRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        return IsGuardianReasoningSection(issue.Section) &&
               (string.Equals(code, "missing_actor_block", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, "missing_actor_situation", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, "missing_actor_thoughts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, "missing_actor_actions", StringComparison.OrdinalIgnoreCase) ||
                code.StartsWith("actor_brain_", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsActorMemoryPersistenceRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "guardian_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "mortal_npc_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "afterlife_resident_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "afterlife_entity_relevant_actor_missing_memory_ledger_delta", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "shining_faction_relevant_actor_missing_strategic_memory_delta", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "actor_thought_journal_not_first_person", StringComparison.OrdinalIgnoreCase) ||
               IsGuardianThoughtJournalShapeRepairIssue(issue);
    }

    private static bool IsActorMaterializationRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "actor_materialization_missing" => true,
            "actor_materialization_invalid_envelope" => true,
            "actor_materialization_actor_binding_mismatch" => true,
            "actor_materialization_duplicate_id" => true,
            "actor_materialization_duplicate_property" => true,
            "actor_materialization_invalid_actor_type" => true,
            "actor_materialization_inventory_reference_mismatch" => true,
            "actor_materialization_section_missing" => true,
            "actor_materialization_section_content_mismatch" => true,
            "actor_materialization_section_empty_surface_invalid" => true,
            "actor_materialization_capability_mismatch" => true,
            "actor_materialization_existing_resend_forbidden" => true,
            "actor_materialization_historical_envelope_changed" => true,
            "actor_materialization_afterlife_missing_appearance" => true,
            "actor_materialization_afterlife_missing_profile_summary" => true,
            "actor_materialization_afterlife_missing_personality" => true,
            "actor_materialization_afterlife_missing_motivation" => true,
            "actor_materialization_afterlife_missing_worldview" => true,
            "actor_materialization_afterlife_missing_realm" => true,
            "actor_materialization_afterlife_missing_location" => true,
            "actor_materialization_afterlife_missing_goals_plan" => true,
            "afterlife_actor_materialization_profile_missing" => true,
            "afterlife_actor_materialization_profile_ambiguous" => true,
            "afterlife_actor_materialization_memory_missing" => true,
            "npc_new_update_location_authority_not_exactly_one" => true,
            "npc_new_update_current_location_unknown" => true,
            _ => false
        };
    }

    private static bool IsFactionMaterializationRepairIssue(ValidationIssue issue) =>
        issue.Code?.StartsWith(
            "faction_materialization_",
            StringComparison.Ordinal) == true ||
        string.Equals(
            issue.Code,
            "faction_existing_full_resend_forbidden",
            StringComparison.Ordinal);

    private static bool IsRoutableFactionMaterializationRepairIssue(ValidationIssue issue) =>
        IsFactionMaterializationRepairIssue(issue) &&
        ResolveFactionMaterializationRepairCoordinate(issue) != null;

    private static string? ResolveFactionMaterializationRepairCoordinate(ValidationIssue issue)
    {
        var actor = issue.Actor;
        if (string.IsNullOrEmpty(actor))
            return null;

        var prefix = actor.StartsWith("mortal_faction:", StringComparison.Ordinal)
            ? "mortal_faction:"
            : actor.StartsWith("shining_faction:", StringComparison.Ordinal)
                ? "shining_faction:"
                : null;
        if (prefix == null || actor.Length == prefix.Length)
            return null;

        for (var index = prefix.Length; index < actor.Length; index++)
        {
            if (char.IsWhiteSpace(actor[index]) || actor[index] == ':')
                return null;
        }

        return actor;
    }

    private static bool IsGuardianThoughtJournalShapeRepairIssue(ValidationIssue issue)
    {
        var path = NormalizeRepairTargetPath(issue.FilePath);
        if (!path.StartsWith(GuardianThoughtJournalState.StatePath, StringComparison.OrdinalIgnoreCase))
            return false;

        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "strict_state_missing_allowed_top_level_key" => true,
            "missing_allowed_top_level_key" => true,
            "flexible_state_unknown_top_level_key" => true,
            "expected_array" => true,
            _ => false
        };
    }

    private static bool IsAfterlifeChronicleStringArrayRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Section, "AfterlifeChronicles", StringComparison.OrdinalIgnoreCase) &&
               (string.Equals(issue.Code, "afterlife_chronicle_persistent_consequences_entry_invalid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "afterlife_chronicle_open_threads_entry_invalid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "afterlife_chronicle_persistent_consequences_not_array", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "afterlife_chronicle_open_threads_not_array", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFactionIdentityRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "faction_full_object_existing_requires_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_unknown_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_conflicting_identity", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_requires_explicit_null_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_requires_explicit_create_flag", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_command_unknown_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "canonical_faction_sidecar_requires_permanent_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "canonical_faction_sidecar_unknown_faction_id", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalNpcLocationRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "missing_actor_current_location", StringComparison.OrdinalIgnoreCase) ||
               (IsMortalNpcRepairPath(issue.FilePath) &&
                (string.Equals(issue.Code, "current_location_new_scene_missing_initial_id_for_npc_scene", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "missing_actor_current_location", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_scene_missing_current_location_id", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_scene_location_mismatch", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_scene_missing_initial_location_id", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_scene_initial_location_mismatch", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_initial_location_same_turn_target_unknown", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_same_turn_initial_location_requires_null_current_location", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsMortalFactionResourceRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "canonical_faction_resource_entry_missing_required_fields", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_missing_meta_resources", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_missing_strategic_goods", StringComparison.OrdinalIgnoreCase);
    }
}
