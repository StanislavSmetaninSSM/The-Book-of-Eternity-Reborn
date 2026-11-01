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

    private static ValidationRepairHarnessPacket BuildMortalNpcInventoryUpdateRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcInventoryUpdateErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcInventoryUpdateErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcInventoryUpdateErrors, includeNpcCoreWhenMissing: true);

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_inventory_update_repair",
            Priority = "high",
            Title = "Mortal NPC existing-inventory update repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "A genuinely new NPC with NPCId = JSON null and a non-empty initialId may carry its initial inventory inside the full UpdateNPCs/NPCsInScene object.",
                "An ordinary existing NPC must remove the whole ordinary-existing full-object resend from UpdateNPCs. Every legitimate supported change uses its dedicated delta/command surface.",
                "A true legacy promotion must retain schema-required inventory and restore the exact semantically unchanged validated pre-turn inventory snapshot carried by validationIssues[].expected; real mutations still use dedicated inventory commands."
            },
            SafeCorrectionRules = new List<string>
            {
                "Patch only the listed NPC entries and inventory command surfaces.",
                "For an ordinary existing NPC, remove the whole ordinary-existing full-object resend; never retain a schema-invalid UpdateNPCs object with inventory omitted.",
                "Express every legitimate skill, inventory, relationship, journal, activity, equipment/resource, or other supported change through its dedicated delta/command surface.",
                "If a required delta surface does not exist, use the main-GM rollback/repair path instead of constructing a partial full object.",
                "For a true legacy promotion, copy the exact inventory JSON from validationIssues[].expected back into the full promotion object without semantic changes.",
                "Keep initial inventory only for a genuinely new NPC that was already new in validated continuity authority; never change an existing identity into NPCId = JSON null plus initialId to evade this error."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md before repairing NPC inventory update validation errors.",
                "For each named actor, classify the object as a genuinely new NPC, an ordinary existing NPC, or a true legacy promotion using validated identity/materialization continuity and the issue metadata.",
                "For an ordinary existing NPC, remove the whole ordinary-existing full-object resend from UpdateNPCs. Re-author every legitimate supported change through NPCInventoryAdds, NPCInventoryUpdates, NPCInventoryRemovals, skill, relationship, journal, activity, equipment/resource, or another documented dedicated command surface.",
                "If any required ordinary-existing change has no dedicated delta/command surface, stop and use the main-GM rollback/repair path; do not retain a partial full object.",
                "For a true legacy promotion, keep inventory present and restore the exact semantically unchanged validated pre-turn inventory snapshot from validationIssues[].expected.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not delete a meaningful NPC to silence an inventory resend error.",
                "Do not keep an ordinary existing full UpdateNPCs object after removing only its inventory property; that partial object is schema-invalid.",
                "Do not remove the schema-required inventory from a full true legacy promotion.",
                "Do not mutate or reconstruct the validated pre-turn promotion snapshot; copy validationIssues[].expected exactly.",
                "Do not reclassify an existing NPC as genuinely new by replacing its permanent NPCId with initialId.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer NPC inventory rules."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcFullObjectRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcFullObjectErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcFullObjectErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcFullObjectErrors, includeNpcCoreWhenMissing: true);
        var missingFields = CollectRepairMissingFields(mortalNpcFullObjectErrors);
        var hasMalformedSkillArrays = mortalNpcFullObjectErrors.Any(issue => IsMortalNpcSkillObjectRepairPath(issue.FilePath));
        var expectedShape = new List<string>
        {
            "Every meaningful Mortal World NPC update must materialize a full NPC object in game_state/npcs/npc_core.json.",
            "Required profile/social fields include display identity, rarity, worldview, personalityArchetype, culturalStance, race, class, appearanceDescription, history, progressionType, relationshipLevel, attitude, relationshipLock, goals, inventory, and personalityTraits.",
            "Collections such as relationshipLock, goals, inventory, personalityTraits, customProperties, journalEntries, and related arrays stay JSON arrays/objects even when they contain one item."
        };
        var steps = new List<string>
        {
            "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md before editing game_state/npcs/npc_core.json.",
            "Find each NPC named by the validation errors and expand it to the canonical full NPC object shape instead of leaving a partial row.",
            "Ensure relationshipLock is an object/array in the expected canonical shape, goals is a collection of concrete goals, and personalityTraits is a collection of concrete traits.",
            "Ensure attitude is synchronized with relationshipLevel and culturalStance uses Conformist, Pragmatist, or Dissident."
        };

        if (missingFields.Count > 0)
        {
            expectedShape.Add($"Validator reported these exact missing fields: {string.Join(", ", missingFields)}.");
            steps.Add($"Patch the exact missing fields first: {string.Join(", ", missingFields)}.");
        }

        if (missingFields.Contains("inventory", StringComparer.OrdinalIgnoreCase))
            steps.Add("For a newly created NPC without carried items, add inventory: [] rather than omitting the inventory field.");

        if (hasMalformedSkillArrays)
        {
            expectedShape.Add("NPC activeSkills/passiveSkills arrays must contain full skill objects, not string names. Put names in skillName/displayName fields inside each object.");
            steps.Add("Replace activeSkills/passiveSkills string names with full skill objects: active skills need skillName, skillDescription, rarity, actionCost, and combatEffect; passive skills need skillName, skillDescription, rarity, type/group, bonuses, and structuredBonuses/playerStatBonus where applicable.");
        }

        steps.Add("After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json.");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_full_object_repair",
            Priority = "high",
            Title = "Mortal NPC full object shape repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            MissingFields = missingFields.Count == 0 ? null : missingFields,
            ExpectedShape = expectedShape,
            SafeCorrectionRules = new List<string>
            {
                "Complete the existing NPC object instead of replacing the file with a minimal skeleton.",
                "For background-only names that should not persist, remove them from structured actor/NPC updates instead of creating a partial NPC.",
                "Keep all user-visible NPC prose meaningful; required fields should not be filled with placeholders like unknown/TBD unless the story explicitly supports uncertainty."
            },
            Steps = steps,
            DoNotDo = new List<string>
            {
                "Do not delete meaningful NPCs or story hooks to avoid filling required fields.",
                "Do not use raw nulls for required strings or collapse single-item arrays into scalars.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer NPC object rules."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildActorMaterializationRepairPacket(
        IReadOnlyList<ValidationIssue> materializationErrors)
    {
        var actorIdentities = CollectRepairActorNames(materializationErrors)
            .OrderBy(actor => actor, StringComparer.Ordinal)
            .ToList();
        var targetFiles = materializationErrors
            .SelectMany(ResolveActorMaterializationRepairTargetFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        var missingTargets = materializationErrors
            .Select(DescribeMissingActorMaterializationTarget)
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Select(target => target!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(target => target, StringComparer.Ordinal)
            .ToList();
        var hasMortalTargets = targetFiles.Contains(
            "game_state/npcs/npc_core.json",
            StringComparer.OrdinalIgnoreCase);
        var hasAfterlifeTargets = targetFiles.Contains(
            AfterlifeEntityProfileState.StatePath,
            StringComparer.OrdinalIgnoreCase);
        var templateRefs = new List<string>();
        if (hasMortalTargets)
            templateRefs.Add("Templates/MORTAL_NPC_UPDATE_TEMPLATE.md");
        if (hasAfterlifeTargets)
        {
            templateRefs.Add("OtherGuides/Afterlife_Contract_Matrix.md");
            templateRefs.Add("Examples/E_CLI_Afterlife_Turns.txt");
        }

        return new ValidationRepairHarnessPacket
        {
            Kind = "actor_materialization_repair",
            Priority = "critical",
            Title = "Bounded actor materialization repair",
            TargetFiles = targetFiles,
            TemplateRefs = templateRefs,
            CanonicalActorNames = actorIdentities,
            MissingFields = missingTargets.Count == 0 ? null : missingTargets,
            ExactFieldCorrections = materializationErrors
                .Select(BuildExactFieldCorrection)
                .OrderBy(correction => correction.Path, StringComparer.Ordinal)
                .ThenBy(correction => correction.Code, StringComparer.Ordinal)
                .ToList(),
            ExpectedShape = new List<string>
            {
                "Each listed actor uses one exact canonical actorType:actorId identity; display names, prose, and setting genre are never identity authority.",
                "A new or promoted significant actor has one complete actor-bound materialization v1 envelope with exact capabilities and every required section disposition.",
                "A section with canonical content uses state=populated; state=empty_by_design requires a non-empty in-world reason and keeps every governed canonical empty array, object, or null field physically present.",
                "A new afterlife actor explicitly carries appearanceDescription, profileSummary, personalityProfile.archetype, motivation, personalityProfile.worldview, realm, locationId, goals with a non-empty plan, and exact actor-owned memory.",
                "A new Mortal UpdateNPCs actor carries exactly one non-empty location authority: a known currentLocationId or a valid same-turn initialLocationId, never neither or both.",
                "An existing materialized afterlife profile is never resent through the full afterlifeEntityProfileUpdates carrier; later changes use the exact dedicated delta, while a legacy first-envelope migration preserves every historical field.",
                "Existing valid actor fields, valid sections, and accepted historical materialization envelopes remain unchanged unless an exact correction explicitly targets them."
            },
            SafeCorrectionRules = new List<string>
            {
                "Repair only the actors and materialization sections listed in canonicalActorNames, missingFields, and exactFieldCorrections.",
                "Preserve every valid actor field and valid materialization section that is not named by an exact validation error.",
                "For afterlife_actor_materialization_profile_missing, add exactly one common profile for the listed exact actorType:actorId in game_state/meta/afterlife_entity_profiles.json.",
                "For profile ambiguity, keep one canonical exact type-and-ID profile and preserve its valid sections; do not merge records by displayName.",
                "For actor-owned memory errors, initialize memory from facts of the current accepted turn in the exact actor profile or documented type-specific journal.",
                "For an empty-surface error, restore only the named governed field in its exact canonical empty array, object, or null shape; omission is not empty_by_design.",
                "For afterlife presentation, personality, realm/location, or goals/plan errors, author only the exact missing structured field from current canonical evidence.",
                "For a new Mortal location error, select exactly one known currentLocationId or valid same-turn initialLocationId and preserve every unrelated NPC field."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and only the packet-listed targetFiles/templates first.",
                "Locate each actor by the exact actorType:actorId value in canonicalActorNames; do not resolve an actor by displayName or narrative description.",
                "Apply missingFields and exactFieldCorrections one by one, changing only the named envelope/profile/section target.",
                "Remove any historical afterlife full-carrier resend and express its intended change through the documented exact dedicated delta surface.",
                "Recheck that all unrelated actor data, already valid materialization sections, and historical envelopes are byte-for-byte or semantically preserved as required by the listed error.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer Actor Materialization rules; use this packet, validation_repair_request.json, templates, docs, examples, and canonical state.",
                "Do not rewrite the whole actor, entire profile collection, or unrelated materialization sections to fix one listed target.",
                "Do not delete an actor, profile, item, memory record, or valid section merely to silence a materialization error.",
                "Do not infer identity, capabilities, skills, inventory, equipment slots, trade, or section content from actor names, archetype prose, item types, genre keywords, or narrative descriptions.",
                "Do not ask the client to invent GM-authored actor content or fabricate missing narrative facts; the GM must author only the exact bounded repair from current canonical evidence.",
                "Do not create a new turn or write ready/turn_complete.json during validation repair."
            }
        };
    }

    private static IEnumerable<string> ResolveActorMaterializationRepairTargetFiles(ValidationIssue issue)
    {
        var authorityPath = GmWorkerTaskPacketBuilder.ResolveActorMaterializationAuthorityPath(issue);
        if (authorityPath != null)
        {
            yield return authorityPath;
            yield break;
        }

        var actor = NormalizeRepairActorName(issue.Actor);
        if (!string.IsNullOrWhiteSpace(actor) &&
            actor.StartsWith("mortal_npc:", StringComparison.Ordinal))
        {
            yield return "game_state/npcs/npc_core.json";
            yield break;
        }

        if ((issue.Code ?? string.Empty).StartsWith("afterlife_actor_materialization_", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(actor) && actor.Contains(':', StringComparison.Ordinal)))
        {
            yield return AfterlifeEntityProfileState.StatePath;
            yield break;
        }

        var normalizedPath = NormalizeRepairTargetPath(issue.FilePath);
        if (normalizedPath.StartsWith("game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase))
        {
            yield return "game_state/npcs/npc_core.json";
            yield break;
        }

        if (normalizedPath.StartsWith(AfterlifeEntityProfileState.StatePath, StringComparison.OrdinalIgnoreCase))
            yield return AfterlifeEntityProfileState.StatePath;
    }

    private static string? DescribeMissingActorMaterializationTarget(ValidationIssue issue)
    {
        var actor = NormalizeRepairActorName(issue.Actor);
        if (string.IsNullOrWhiteSpace(actor))
            actor = "exact actor from errors[]";

        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "actor_materialization_missing" => $"{actor} / materialization",
            "actor_materialization_section_missing" => $"{actor} / {issue.Section ?? "required section"}",
            "actor_materialization_section_empty_surface_invalid" => $"{actor} / {issue.Section ?? "required section"} canonical empty surface",
            "actor_materialization_afterlife_missing_appearance" => $"{actor} / appearanceDescription",
            "actor_materialization_afterlife_missing_profile_summary" => $"{actor} / profileSummary",
            "actor_materialization_afterlife_missing_personality" => $"{actor} / personalityProfile.archetype",
            "actor_materialization_afterlife_missing_motivation" => $"{actor} / motivation",
            "actor_materialization_afterlife_missing_worldview" => $"{actor} / personalityProfile.worldview",
            "actor_materialization_afterlife_missing_realm" => $"{actor} / realm",
            "actor_materialization_afterlife_missing_location" => $"{actor} / locationId",
            "actor_materialization_afterlife_missing_goals_plan" => $"{actor} / goals plan",
            "afterlife_actor_materialization_profile_missing" => $"{actor} / common profile",
            "afterlife_actor_materialization_memory_missing" => $"{actor} / actor-owned memory",
            "npc_new_update_location_authority_not_exactly_one" => $"{actor} / exactly one currentLocationId or initialLocationId",
            "npc_new_update_current_location_unknown" => $"{actor} / known currentLocationId",
            _ => null
        };
    }

    private static List<string> CollectRepairMissingFields(IReadOnlyList<ValidationIssue> issues)
    {
        var fields = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in issues)
        {
            foreach (var part in (issue.Actual ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var field = part;
                const string MissingPrefix = "missing ";
                if (field.StartsWith(MissingPrefix, StringComparison.OrdinalIgnoreCase))
                    field = field[MissingPrefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(field))
                    fields.Add(field);
            }
        }

        return fields.ToList();
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcRelationshipEnumRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcRelationshipEnumErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcRelationshipEnumErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcRelationshipEnumErrors, includeNpcCoreWhenMissing: true);

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_relationship_enum_repair",
            Priority = "high",
            Title = "Mortal NPC relationship tier and cultural stance repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "NPC attitude is a canonical Russian relationship tier derived from relationshipLevel.",
                "Allowed attitude tiers: Непримиримый Враг, Противник, Неприязнь, Нейтралитет, Доверие и Расположение, Глубокая Связь, Легендарная Преданность.",
                "NPC culturalStance is one of the canonical enum values: Conformist, Pragmatist, Dissident."
            },
            SafeCorrectionRules = new List<string>
            {
                "Change attitude and/or relationshipLevel together so the tier and score agree.",
                "Use culturalStance enum values exactly as written; put localized prose in separate display/description fields, not in culturalStance.",
                "Preserve the NPC's existing story relationship meaning; do not reset relationshipLevel to zero unless neutral is the intended repair."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md and patch the listed NPC entries in game_state/npcs/npc_core.json.",
                "Map relationshipLevel to one canonical attitude tier: Непримиримый Враг / Противник / Неприязнь / Нейтралитет / Доверие и Расположение / Глубокая Связь / Легендарная Преданность.",
                "Set culturalStance only to Conformist, Pragmatist, or Dissident.",
                "If the relationship meaning in prose conflicts with the numeric level, adjust the smallest necessary field pair and keep a coherent repair note in debug output.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not write English attitude labels such as Friendly/Hostile into NPC attitude.",
                "Do not localize culturalStance into Russian inside the enum field.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer relationship enum rules."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcReferenceRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcReferenceErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcReferenceErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcReferenceErrors, includeNpcCoreWhenMissing: true);

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_reference_repair",
            Priority = "high",
            Title = "Mortal NPC reference repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "NPCJournals and other NPC sidecar updates must reference an existing NPCId or NPCName from canonical game_state/npcs/npc_core.json.",
                "If the actor became meaningful this turn, materialize a full NPC object first; if the actor was only background color, remove the orphan sidecar/journal update."
            },
            SafeCorrectionRules = new List<string>
            {
                "Do not create orphan NPCJournals entries for names that are absent from npc_core.json.",
                "Preserve journal prose only after it is attached to an existing or newly materialized full NPC.",
                "Prefer correcting the reference to an existing NPC over inventing a new NPC when the name clearly points to a current character."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md before repairing NPCJournals or other NPC references.",
                "Compare the referenced NPCId/NPCName with game_state/npcs/npc_core.json.",
                "If the NPC exists, correct the journal/reference to the exact existing NPCId or NPCName.",
                "If the NPC is genuinely introduced by this turn, add/complete the full NPC object in game_state/npcs/npc_core.json before keeping NPCJournals.",
                "If the reference was only background-only color, remove the orphan NPCJournals/reference update instead of creating a partial NPC.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not leave NPCJournals pointing at unknown names or ids.",
                "Do not create a partial NPC object just to satisfy the reference.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer NPC reference rules."
            }
        };
    }
}
