using System.Globalization;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectResourceTriggerRoutingScaleTests
{
    private const string ScenarioOneLegacyResourceState = """{"schemaVersion":1,"entries":[{"realm":"mortal_world","ownerKind":"npc","resourceOwnerId":"npc_accepted_event_budget","resourceKey":"health","current":8,"maximum":10,"capacityBinding":{"kind":"registered_formula","authorityKey":"mortal_health_capacity_v1","authorityFingerprint":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"state":"active","chronology":{"createdAtTurn":1,"createdEventRef":"turn_1:budget:initialize","lastTransitionId":"resource_transition_00000002000000000000000000000000","lastEventRef":"turn_43:budget:two_consuming_replacements","lastTransitionTurn":43}}]}""";

    private const string ScenarioOneLegacyResourceHistory = """{"schemaVersion":1,"entries":[{"transitionId":"transition_budget_initialize","operationId":"operation_budget_initialize","eventRef":"turn_1:budget:initialize","originKind":"owner_materialization","originId":"npc_accepted_event_budget","phase":"registered_system_outcome","priority":50,"executionSequence":0,"coordinate":{"realm":"mortal_world","ownerKind":"npc","resourceOwnerId":"npc_accepted_event_budget","resourceKey":"health"},"operation":"initialize","requestedAmount":0,"appliedAmount":0,"outcome":"applied","capacityDisposition":"initialize_from_definition","beforeState":null,"afterState":{"current":10,"maximum":10,"capacityBinding":{"kind":"registered_formula","authorityKey":"mortal_health_capacity_v1","authorityFingerprint":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"state":"active"},"sourceEvidence":{"sourceKind":"owner_materialization","sourceId":"npc_accepted_event_budget","authorityFingerprint":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"policyFingerprint":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","receiptId":null,"turn":1},{"transitionId":"resource_transition_00000002000000000000000000000000","operationId":"resource_operation_00000001000000000000000000000000","eventRef":"turn_43:budget:two_consuming_replacements","originKind":"registered_system_outcome","originId":"budget_root_source","phase":"registered_system_outcome","priority":100,"executionSequence":0,"coordinate":{"realm":"mortal_world","ownerKind":"npc","resourceOwnerId":"npc_accepted_event_budget","resourceKey":"health"},"operation":"damage","requestedAmount":2,"appliedAmount":2,"outcome":"applied","capacityDisposition":null,"beforeState":{"current":10,"maximum":10,"capacityBinding":{"kind":"registered_formula","authorityKey":"mortal_health_capacity_v1","authorityFingerprint":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"state":"active"},"afterState":{"current":8,"maximum":10,"capacityBinding":{"kind":"registered_formula","authorityKey":"mortal_health_capacity_v1","authorityFingerprint":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"state":"active"},"sourceEvidence":{"sourceKind":"registered_system_outcome","sourceId":"budget_root_source","authorityFingerprint":"sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"},"policyFingerprint":"sha256:9d37bb8f79eecc71b9a900c91fb9162418ac8276f05b430cde7b92124d23fd55","receiptId":null,"turn":43}]}""";

    private static readonly string[] ScenarioOneLegacyResourceFactoryCalls =
    {
        "operation:resource_operation_00000001000000000000000000000000",
        "transition:resource_transition_00000002000000000000000000000000"
    };

    private static readonly string[] ScenarioOneLegacyAllocatedEffectIds =
    {
        "effect_accepted_event_budget",
        "effect_scenario_one_1",
        "effect_scenario_one_2"
    };

    private static readonly string[] ScenarioOneLegacyAllocatedTransitionIds =
    {
        "effect_transition_scenario_one_1",
        "effect_transition_scenario_one_2",
        "effect_transition_scenario_one_3",
        "effect_transition_scenario_one_4",
        "effect_transition_scenario_one_5",
        "effect_transition_scenario_one_6"
    };

    private static readonly string[] ScenarioOneLegacyEffectFactoryCalls =
    {
        "effect:effect_scenario_one_1",
        "transition:effect_transition_scenario_one_1",
        "transition:effect_transition_scenario_one_2",
        "effect:effect_scenario_one_2",
        "transition:effect_transition_scenario_one_3",
        "transition:effect_transition_scenario_one_4",
        "transition:effect_transition_scenario_one_5",
        "transition:effect_transition_scenario_one_6"
    };

    /// <summary>
    /// Compares the current ordinary adapter result with the frozen pre-draft-owner scenario-one golden.
    /// </summary>
    /// <param name="result">
    /// The current resource and completed effect result.
    /// </param>
    /// <param name="finalizedPlan">
    /// The current completed effect plan.
    /// </param>
    /// <param name="resourceIdentityFactory">
    /// The deterministic resource factory that recorded the current allocation stream.
    /// </param>
    /// <param name="effectIdentityFactory">
    /// The deterministic effect factory that recorded the current allocation stream.
    /// </param>
    private static void AssertScenarioOneLegacyGolden(
        AcceptedEventBudgetResult result,
        EffectAcceptedTurnPlan finalizedPlan,
        ScenarioOneRecordingResourceIdentityFactory resourceIdentityFactory,
        ScenarioOneRecordingEffectIdentityFactory effectIdentityFactory)
    {
        Assert.Equal(
            ScenarioOneLegacyResourceState,
            result.Resources.StateAfterImage!.ToCanonicalJson());
        Assert.Equal(
            ScenarioOneLegacyResourceHistory,
            result.Resources.HistoryAfterImage!.ToCanonicalJson());
        Assert.Equal(
            ScenarioOneLegacyResourceFactoryCalls,
            resourceIdentityFactory.Calls);
        Assert.Equal(
            "sha256:629b62fe6bc385cfcd0b756085702904eefe27766eca55360f4d1bdb222d082e",
            WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(
                finalizedPlan));
        Assert.Equal(
            "sha256:629b62fe6bc385cfcd0b756085702904eefe27766eca55360f4d1bdb222d082e",
            finalizedPlan.AcceptedBoundaryFinalPlanFingerprint);
        Assert.Equal(
            ScenarioOneLegacyAllocatedEffectIds,
            finalizedPlan.AllocatedEffectIds);
        Assert.Equal(
            ScenarioOneLegacyAllocatedTransitionIds,
            finalizedPlan.AllocatedTransitionIds);
        Assert.Equal(
            ScenarioOneLegacyEffectFactoryCalls,
            effectIdentityFactory.Calls);
        Assert.Equal(
            "639BC2299504D448F7F81CBCE9F8D55D55B64D22C8B95ED346A4A07FFD508721",
            finalizedPlan.SourceAuthorityFingerprint);
        Assert.Equal(
            "E4E22BFB4A9AB7ADF3206665B9C7448782671FA169B33EB7632FDDD9A6EFB67B",
            finalizedPlan.TargetAuthorityFingerprint);
        Assert.Null(finalizedPlan.SkillScopeAuthority);
    }

    /// <summary>
    /// Allocates deterministic resource identities and records their exact call order.
    /// </summary>
    private sealed class ScenarioOneRecordingResourceIdentityFactory :
        AcceptedMechanicsIdentityFactory
    {
        private readonly List<string> _calls = new();
        private int _next = 1;

        /// <summary>
        /// Gets the ordered identity kind and value pairs allocated by this factory.
        /// </summary>
        internal IReadOnlyList<string> Calls => _calls;

        /// <summary>
        /// Allocates and records the next deterministic resource operation identity.
        /// </summary>
        /// <returns>
        /// The allocated resource operation identity.
        /// </returns>
        internal override string CreateOperationId()
        {
            var id = "resource_operation_" + NextGuid();
            _calls.Add("operation:" + id);
            return id;
        }

        /// <summary>
        /// Allocates and records the next deterministic resource transition identity.
        /// </summary>
        /// <returns>
        /// The allocated resource transition identity.
        /// </returns>
        internal override string CreateTransitionId()
        {
            var id = "resource_transition_" + NextGuid();
            _calls.Add("transition:" + id);
            return id;
        }

        /// <summary>
        /// Creates the next deterministic GUID image used by the historical fixture.
        /// </summary>
        /// <returns>
        /// The 32-character lowercase GUID image for the next allocation.
        /// </returns>
        private string NextGuid() =>
            new Guid(_next++, 0, 0, new byte[8]).ToString("N");
    }

    /// <summary>
    /// Allocates deterministic effect identities and records their exact call order.
    /// </summary>
    private sealed class ScenarioOneRecordingEffectIdentityFactory :
        EffectIdentityFactory
    {
        private readonly List<string> _calls = new();
        private int _effects;
        private int _transitions;

        /// <summary>
        /// Gets the ordered identity kind and value pairs allocated by this factory.
        /// </summary>
        internal IReadOnlyList<string> Calls => _calls;

        /// <summary>
        /// Allocates and records the next deterministic effect identity.
        /// </summary>
        /// <returns>
        /// The allocated effect identity.
        /// </returns>
        internal override string CreateEffectId()
        {
            var id = "effect_scenario_one_" + (++_effects).ToString(
                CultureInfo.InvariantCulture);
            _calls.Add("effect:" + id);
            return id;
        }

        /// <summary>
        /// Allocates and records the next deterministic effect transition identity.
        /// </summary>
        /// <returns>
        /// The allocated effect transition identity.
        /// </returns>
        internal override string CreateTransitionId()
        {
            var id = "effect_transition_scenario_one_" +
                     (++_transitions).ToString(CultureInfo.InvariantCulture);
            _calls.Add("transition:" + id);
            return id;
        }
    }
}
