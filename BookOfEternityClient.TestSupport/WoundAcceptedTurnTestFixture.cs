using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Shared deterministic player wound inputs used by Fast and Integration tests.
/// </summary>
internal static class WoundAcceptedTurnTestFixture
{
    private const string SessionId = "session_wound_batch_test";
    private const string RequestId = "request_wound_batch_test";
    private const string SnapshotToken = "snapshot_wound_batch_test";
    private const string Realm = "mortal_world";
    private const int Turn = 42;

    internal static WoundAcceptedTurnInput CreateDefaultInput() =>
        CreateInput(includeMechanics: true);

    internal static WoundAcceptedTurnInput CreateNoMechanicsInput() =>
        CreateInput(includeMechanics: false);

    internal static EffectAcceptedTurnInput CreateEffectInput(
        WoundPreparedAcceptedTurnPlan prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);

        var woundExports = prepared.EffectOperationBatches.Select(batch =>
            new EffectSourceExport(
                batch.SourceExport.Realm,
                batch.SourceExport.Kind,
                batch.SourceExport.SourceId,
                new JsonArray(batch.SourceExport.Definitions.Select(static value =>
                    (JsonNode)value.Definition.DeepClone()).ToArray()),
                Materializable: false,
                Active: true,
                SameTurn: true,
                SourceRef: batch.SourceExport.SourceRef)).ToArray();
        var woundGroups = prepared.EffectOperationBatches.Select(batch =>
        {
            if (!WoundEffectCarrierAdapter.TryCreateTargetKey(
                    batch.SourceExport.Owner,
                    out var target))
            {
                throw new InvalidOperationException(
                    "The standard wound fixture could not create its effect target.");
            }

            return new WoundSourceGroupAuthority(
                new EffectIdentitySourceGroup(
                    batch.SourceExport.Realm,
                    batch.SourceExport.Kind,
                    batch.SourceExport.SourceId),
                batch.SourceExport.Owner,
                target,
                sameTurn: true,
                batch.SourceExport.SourceRef,
                batch.SourceExportFingerprint,
                batch.SourceExport.Definitions.Select(static definition =>
                    new WoundEffectSourceDefinition(
                        definition.DefinitionKey,
                        definition.Definition)).ToArray(),
                batch.RootLineageAuthority
                    .Where(static row => row.ApplicationRef is not null)
                    .ToArray(),
                batch.RootLineageAuthority
                    .Where(static row => row.EffectId is not null)
                    .ToArray());
        }).ToArray();
        var sourceAuthority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            Array.Empty<EffectSourceExport>(),
            woundExports,
            new HashSet<string>(StringComparer.Ordinal),
            WoundGroups: woundGroups));
        var targets = prepared.EffectOperationBatches
            .SelectMany(static batch => batch.RootApplications)
            .Select(static application => application.ExpectedTargetKey)
            .Distinct()
            .Select(static key => new EffectTargetExport(
                key.Realm,
                key.Kind,
                key.TargetId,
                SameTurn: false))
            .ToArray();
        var targetAuthority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            targets,
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            CombatantIdentities: null));
        var eventInput = new JsonObject
        {
            ["turn"] = prepared.Binding.Turn,
            ["events"] = new JsonArray(prepared.Binding.AcceptedEvents.Select(static value =>
                (JsonNode)new JsonObject
                {
                    ["eventRef"] = value.EventRef,
                    ["kind"] = value.Kind,
                    ["authorityId"] = value.AuthorityId
                }).ToArray())
        };

        return new EffectAcceptedTurnInput(
            prepared.Binding.SessionId,
            prepared.Binding.SnapshotToken,
            EffectMaterializationTestFixture.CreateCommandRoot(),
            sourceAuthority,
            targetAuthority,
            eventInput,
            prepared.Binding.Realm,
            PreTurnCarriers: null,
            PreTurnIdentityIndex: new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            });
    }

    private static WoundAcceptedTurnInput CreateInput(bool includeMechanics)
    {
        var owner = new WoundOwnerCoordinate(
            Realm,
            "player",
            "player_current",
            WoundCarrierCatalog.PlayerPath);
        var acceptedEvent = new WoundAcceptedEventAuthority(
            $"turn_{Turn}:wound:001",
            "accepted_turn",
            "authority_A_001",
            Fingerprint("accepted-event:001"));
        var acceptedEvents = new[] { acceptedEvent };
        var binding = new WoundAcceptedTurnBinding(
            SessionId,
            RequestId,
            SnapshotToken,
            Realm,
            Turn,
            acceptedEvents,
            WoundAcceptedEventSetFingerprint.Compute(acceptedEvents));
        var opportunity = new WoundOpportunityAuthority(
            SessionId,
            RequestId,
            SnapshotToken,
            "opportunity_wound_001",
            "wound_opportunity_001",
            acceptedEvent.EventRef,
            acceptedEvent.Kind,
            acceptedEvent.AuthorityId,
            binding.AcceptedEventsFingerprint,
            owner,
            "physical",
            "mortal_formal_injury_v1",
            "combat_action",
            "combat_action_001",
            "active",
            null,
            2,
            null,
            new WoundOpportunitySafeContext(
                "Проверяемая цель",
                "Причина ранения 001.",
                ["anatomical", "systemic", "other"]),
            acceptedEvent.SemanticFingerprint,
            Fingerprint($"opportunity-placeholder:001:{owner}"));
        opportunity = opportunity with
        {
            AuthorityFingerprint =
                WoundOpportunityAuthority.RecomputeAuthorityFingerprint(opportunity)
        };
        var proposed = CreateProposedWound(
            "draft_wound_001",
            "draft_transition_001",
            opportunity.OpportunityId,
            acceptedEvent.EventRef);
        var definitions = includeMechanics
            ? new[]
            {
                new WoundAcceptedEffectDefinitionDraft(
                    "draft_effect_001_001",
                    CreatePeriodicDamageDefinition())
            }
            : Array.Empty<WoundAcceptedEffectDefinitionDraft>();
        var applications = includeMechanics
            ? new[]
            {
                new WoundAcceptedRootApplicationDraft(
                    "draft_application_001_001",
                    "draft_effect_001_001",
                    "root_application_001_001",
                    WoundRootOwnershipDomain.BaseWound)
            }
            : Array.Empty<WoundAcceptedRootApplicationDraft>();
        var slots = includeMechanics
            ? new[]
            {
                new WoundAcceptedConsequenceSlotBinding(
                    1,
                    "periodic_damage",
                    "draft_application_001_001",
                    "The wound applies periodic_damage.")
            }
            : Array.Empty<WoundAcceptedConsequenceSlotBinding>();

        return new WoundAcceptedTurnInput(
            binding,
            new[] { opportunity },
            new[]
            {
                new WoundAcceptedTransitionDraft(
                    "create",
                    "operation_wound_001",
                    "draft_wound_001",
                    "draft_transition_001",
                    opportunity.OpportunityId,
                    "Materialize wound proposal 001.",
                    proposed,
                    definitions,
                    applications,
                    slots)
            },
            new WoundCarrierCatalogInput(
                WoundContractTestData.CreatePlayerCarrier(),
                null,
                null,
                null,
                null),
            WoundContractTestData.CreateIdentityIndex(),
            WoundContractTestData.CreateHistory());
    }

    private static JsonObject CreatePeriodicDamageDefinition()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
        definition["definitionKey"] = "wound_definition_001_root_001";
        definition["allowedRealms"] = new JsonArray(Realm);
        definition["allowedTargetKinds"] = new JsonArray("player");
        definition["parameterBounds"] = new JsonObject();
        definition["links"] = new JsonArray();
        definition["stacking"]!["stackKey"] = "stack_wound_definition_001_root_001";
        definition["stacking"]!["policy"] = "independent";
        definition["stacking"]!["maxStacks"] = 1;
        definition["stacking"]!["atMaximum"] = "no_change";
        definition["stacking"]!["refreshMode"] = null;
        definition["stacking"]!["mergeRule"] = null;
        definition["components"]![0]!["componentId"] = "component_001_001_001";
        definition["triggers"]![0]!["componentIds"] = new JsonArray(
            "component_001_001_001");
        return definition;
    }

    private static WoundMaterializationEnvelope CreateProposedWound(
        string localWoundRef,
        string localTransitionRef,
        string opportunityId,
        string eventRef)
    {
        var json = WoundContractTestData.CreateActiveWound(
            localWoundRef,
            Realm,
            "player",
            "player_current",
            WoundCarrierCatalog.PlayerPath,
            domain: "physical");
        json["origin"]!["eventRef"] = eventRef;
        json["origin"]!["sourceId"] = "combat_action_001";
        json["origin"]!["createdAtTurn"] = Turn;
        json["origin"]!["opportunityId"] = opportunityId;
        json["severity"]!["lastChangeEventRef"] = eventRef;
        json["lastTransition"]!["transitionId"] = localTransitionRef;
        json["lastTransition"]!["turn"] = Turn;
        json["consequences"] = new JsonObject
        {
            ["slotBudget"] = 2,
            ["slotsUsed"] = 0,
            ["entries"] = new JsonArray(),
            ["ownedEffectSources"] = new JsonObject
            {
                ["definitions"] = new JsonArray(),
                ["rootBindings"] = new JsonArray()
            }
        };
        var parsed = WoundMaterializationContract.Parse(
            json.ToJsonString(),
            "acceptedTurn.transitions[0].proposedAfter");
        if (!parsed.IsValid || parsed.Wound is null)
        {
            throw new InvalidOperationException(
                "The standard wound fixture produced an invalid wound: " +
                string.Join(Environment.NewLine, parsed.Issues.Select(static issue =>
                    $"{issue.Code}: {issue.Message}")));
        }

        return parsed.Wound;
    }

    private static string Fingerprint(string value) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
