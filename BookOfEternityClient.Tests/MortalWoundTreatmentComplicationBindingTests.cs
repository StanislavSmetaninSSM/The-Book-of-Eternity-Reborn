using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundTreatmentComplicationBindingTests
{
    [Fact]
    public void BindingPreparation_RootKeysUseExistingResponseCoordinateWriter()
    {
        var definition = WoundContractTestData.CreateActiveWound()["consequences"]!["ownedEffectSources"]!["definitions"]![1]!.DeepClone().AsObject();
        definition["links"] = new JsonArray();
        var draft = new MortalWoundComplicationProposalDraft(new("edge", "pain", "active", "Edge pain", 1, "known_to_player"),
            ImmutableArray.Create(new WoundConsequenceDefinitionProposalDraft("z_root", JsonSerializer.SerializeToElement(definition),
                new("complication", "edge", ImmutableArray.Create(new WoundConsequenceSlotProposalDraft("action_control", "Restricted.")))),
                new WoundConsequenceDefinitionProposalDraft("a_child", JsonSerializer.SerializeToElement(definition), null),
                new WoundConsequenceDefinitionProposalDraft("b_root", JsonSerializer.SerializeToElement(definition),
                    new("complication", "edge", ImmutableArray.Create(new WoundConsequenceSlotProposalDraft("action_control", "Second restriction."))))));
        var request = WoundAcceptedTurnFingerprintWriter.Compute(new[] { "binding-test" });
        var declared = MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(0, new MortalWoundAddComplicationOperation(draft));
        var binding = MortalWoundTreatmentOutcomeIntentComposer.PrepareComplicationBindings(request, 0, draft, declared);
        Assert.Equal(new[] { "0/edge/z_root", "0/edge/a_child", "0/edge/b_root" }, binding.DefinitionReferenceBindings.Select(row => row.NamespacedRef));
        Assert.Equal(new[] { "0/edge/z_root_application", "0/edge/b_root_application" }, binding.ApplicationReferenceBindings.Select(row => row.NamespacedRef));
        Assert.Equal(new[] { "z_root", "b_root" }, binding.Roots.Select(row => row.LocalDefinitionRef));
        var root = binding.Roots[0];
        Assert.Equal("z_root", root.LocalDefinitionRef);
        Assert.Equal("0/edge/z_root", root.DefinitionRef);
        Assert.Equal("0/edge/z_root_application", root.ApplicationRef);
        var expectedKey = "wound_root_operation_" + WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.wound.response_local_coordinate", "1", "wound_root_operation", request, "0/edge/z_root"
        })["sha256:".Length..];
        Assert.Equal(expectedKey, root.OperationKey);
        Assert.Equal(expectedKey, WoundResponseInputComposer.CreateLocalIdentifier("wound_root_operation", request, "0/edge/z_root"));
        var fields = new List<string?> { "book_of_eternity.mortal_wound_treatment.complication_preparation", "1", request,
            "0", "edge", binding.ComplicationId, declared, "z_root", "0/edge/z_root", "a_child", "0/edge/a_child",
            "b_root", "0/edge/b_root", "z_root_application", "0/edge/z_root_application", "b_root_application", "0/edge/b_root_application" };
        Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(fields), binding.PreparationFingerprint);
        var reversed = fields.Take(13).Concat(fields.Skip(15)).Concat(fields.Skip(13).Take(2));
        Assert.NotEqual(WoundAcceptedTurnFingerprintWriter.Compute(reversed), binding.PreparationFingerprint);
        var next = MortalWoundTreatmentOutcomeIntentComposer.PrepareComplicationBindings(request, 1, draft,
            MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(1, new MortalWoundAddComplicationOperation(draft)));
        Assert.NotEqual(binding.ComplicationId, next.ComplicationId);
        Assert.NotEqual(root.OperationKey, next.Roots[0].OperationKey);
        Assert.NotEqual(root.OperationKey, binding.Roots[1].OperationKey);
        definition["definitionKey"] = "mutated";
        Assert.Equal("0/edge/z_root", binding.DefinitionReferenceBindings[0].NamespacedRef);
    }
    [Fact]
    public void BindingPreparation_PreservesExactT067CoordinatesAndFingerprint()
    {
        var draft = new MortalWoundComplicationProposalDraft(
            new WoundComplicationProposalDraft("edge", "pain", "active", "Edge pain", 1,
                "known_to_player"), ImmutableArray<WoundConsequenceDefinitionProposalDraft>.Empty);
        var operation = new MortalWoundAddComplicationOperation(draft);
        var request = WoundAcceptedTurnFingerprintWriter.Compute(new[] { "binding-test" });
        var declared = MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(0, operation);
        var method = typeof(MortalWoundTreatmentOutcomeIntentComposer).GetMethod(
            "PrepareComplicationBindings", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var binding = method.Invoke(null, new object[] { request, 0, draft, declared })!;
        object Read(string name) => binding.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(binding)!;
        var complicationId = "mortal_wound_complication_" +
            MortalWoundTreatmentIdentityWriter.Digest("complication", request, "0", "edge");
        Assert.Equal(complicationId, Read("ComplicationId"));
        Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.complication_preparation", "1",
            request, "0", "edge", complicationId, declared
        }), Read("PreparationFingerprint"));
        Assert.Empty((System.Collections.IEnumerable)Read("Roots"));
        Assert.Empty((System.Collections.IEnumerable)Read("DefinitionReferenceBindings"));
        Assert.Empty((System.Collections.IEnumerable)Read("ApplicationReferenceBindings"));
    }
}
