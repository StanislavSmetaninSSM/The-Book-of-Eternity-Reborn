using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    /// <summary>
    /// Closed copied publication inputs. These values rederive comparison
    /// identities after closure and never carry reservation or execution permission.
    /// </summary>
    internal sealed class MortalDiagnosisPublicationWitness
    {
        private readonly TreatmentSkillCommandEnvelope _skills;
        private readonly MortalTreatmentItemCommandEnvelope _items;
        private readonly JsonObject? _livingWorld;
        internal JsonObject? LivingWorldComparison => _livingWorld?.DeepClone().AsObject();

        private MortalDiagnosisPublicationWitness(TreatmentSkillCommandEnvelope skills, MortalTreatmentItemCommandEnvelope items, JsonObject? livingWorld)
        { _skills = skills; _items = items.Clone(); _livingWorld = livingWorld?.DeepClone().AsObject(); }

        /// <summary>
        /// Captures inputs only from the actual frozen projection and its matching reservation.
        /// </summary>
        internal static MortalDiagnosisPublicationWitness Capture(MortalDiagnosisPublicationBinding publication,
            MortalLivingWorldDiagnosisContribution? livingWorld = null)
        {
            if (!TryReadTreatmentSkillProjectionForEnvelopeSemantics(publication.SkillProjectionAuthority,
                    publication.ReservationAuthority, out var projection))
                throw new InvalidDataException("Diagnosis publication lost its actual frozen skill projection.");
            var root = new JsonObject
            {
                ["schemaVersion"] = 1, ["skillCommands"] = projection.CommandEnvelope.CommandRoot.DeepClone(),
                ["itemCommands"] = publication.ItemCommands.ToWitnessRoot()
            };
            if (livingWorld is not null) { root["schemaVersion"] = 2; root["livingWorld"] = livingWorld.ToComparisonData(); }
            return Parse(root);
        }

        /// <summary>
        /// Rederives ordinary physical publication semantics from the shared
        /// original binding and pure outcome preparation calculation.
        /// </summary>
        internal string ComputeTreatmentSemantic(string acceptedFingerprint, string bindingFingerprint,
            MortalWoundTreatmentAttemptRequest request, MortalWoundTreatmentResolution resolution,
            string preparationFingerprint) => _livingWorld is not null
                ? throw new InvalidDataException("A diagnosis living-world witness cannot substitute for physical treatment inputs.")
                : ComputeTreatmentPublicationEnvelopeFingerprint(
                ComputeTreatmentPublicationBaseFingerprint(acceptedFingerprint, bindingFingerprint,
                    request, resolution, preparationFingerprint), _skills, _items);

        /// <summary>
        /// Parses closed historical DATA through the same envelope freezers as live publication.
        /// </summary>
        internal static MortalDiagnosisPublicationWitness Parse(JsonObject root)
        {
            var version = root["schemaVersion"]?.GetValue<int>();
            if (!(version == 1 && root.Count == 3 || version == 2 && root.Count == 4 && root["livingWorld"] is JsonObject) ||
                root["skillCommands"] is not JsonObject skills || root["itemCommands"] is not JsonObject items)
                throw new InvalidDataException("Missing or unknown diagnosis publication witness fields.");
            var itemNames = new[] { "UpdateInventory", "moveInventoryItems", "removeInventoryItems", "NPCInventoryAdds",
                "NPCInventoryUpdates", "NPCInventoryRemovals", "NPCEquipmentChanges" };
            if (items.Count != itemNames.Length || itemNames.Any(name => !items.ContainsKey(name) ||
                    items[name] is not null and not JsonArray))
                throw new InvalidDataException("Diagnosis publication requires its exact seven nullable item inputs.");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var skillResponse = skills.Deserialize<GameResponse>(options) ?? throw new InvalidDataException("Missing frozen skill input.");
            var itemResponse = items.Deserialize<GameResponse>(options) ?? throw new InvalidDataException("Missing frozen item input.");
            var frozenSkills = FreezeTreatmentSkillCommands(skillResponse);
            var frozenItems = FreezeTreatmentItemCommands(itemResponse);
            if (frozenSkills.Envelope is null || frozenItems.Envelope is null ||
                !JsonNode.DeepEquals(skills, frozenSkills.Envelope.CommandRoot) ||
                !JsonNode.DeepEquals(items, frozenItems.Envelope.ToWitnessRoot()))
                throw new InvalidDataException("Diagnosis publication inputs do not re-freeze exactly.");
            if (root["livingWorld"] is JsonObject living) _ = MortalLivingWorldDiagnosisContribution.ParseComparison(living);
            return new(frozenSkills.Envelope, frozenItems.Envelope, root["livingWorld"] as JsonObject);
        }

        /// <summary>
        /// Copies the complete versioned inputs, preserving omission, null, empty arrays and order.
        /// </summary>
        internal JsonObject ToCanonicalJson()
        {
            var root = new JsonObject { ["schemaVersion"] = 1, ["skillCommands"] = _skills.CommandRoot.DeepClone(),
                ["itemCommands"] = _items.ToWitnessRoot() };
            if (_livingWorld is not null) { root["schemaVersion"] = 2; root["livingWorld"] = _livingWorld.DeepClone(); }
            return root;
        }

        /// <summary>
        /// Rederives the publication semantic from terminal outcome DATA, never the outer result seal.
        /// </summary>
        internal string ComputeSemantic(string acceptedFingerprint, MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentResolution resolution, string workingFingerprint, string progressionFingerprint)
        {
            var critical = MortalWoundCriticalReactionPublicationPlanner.RecomposeDetached(request, resolution);
            var contribution = _livingWorld is null ? null : MortalLivingWorldDiagnosisContribution.ParseComparison(_livingWorld);
            var livingFingerprint = contribution is null ? null : MortalLivingWorldDiagnosisContribution.ComputeFingerprint(
                workingFingerprint, progressionFingerprint, contribution.Receipt.Receipt["receiptFingerprint"]!.GetValue<string>());
            return ComputeTreatmentPublicationEnvelopeFingerprint(ComputeMortalDiagnosisPublicationBaseFingerprint(
                acceptedFingerprint, request, resolution, workingFingerprint, progressionFingerprint, critical.Fingerprint!, livingFingerprint), _skills, _items);
        }
    }

    /// <summary>
    /// Copies the actual private continuation's frozen envelopes without exposing
    /// their private type. These comparison inputs grant no publication permission.
    /// </summary>
    private static MortalDiagnosisPublicationWitness CaptureFrozenTreatmentPublicationInputs(
        TreatmentSkillCommandEnvelope skills, MortalTreatmentItemCommandEnvelope items) =>
        MortalDiagnosisPublicationWitness.Parse(new JsonObject
        {
            ["schemaVersion"] = 1, ["skillCommands"] = skills.CommandRoot.DeepClone(),
            ["itemCommands"] = items.ToWitnessRoot()
        });

    private static string ComputeMortalDiagnosisPublicationBaseFingerprint(string acceptedFingerprint,
        MortalWoundTreatmentAttemptRequest request, MortalWoundTreatmentResolution resolution,
        string workingFingerprint, string progressionFingerprint, string criticalFingerprint, string? livingFingerprint = null)
    {
        var baseline = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.mortal_diagnosis.publication", "1", acceptedFingerprint,
            request.RequestFingerprint, resolution.ResultFingerprint, workingFingerprint, progressionFingerprint, criticalFingerprint
        });
        return livingFingerprint is null ? baseline : WoundAcceptedTurnFingerprintWriter.Compute(new[] { baseline, livingFingerprint });
    }
}
