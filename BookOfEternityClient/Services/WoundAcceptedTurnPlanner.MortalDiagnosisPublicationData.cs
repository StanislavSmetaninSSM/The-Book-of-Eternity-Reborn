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

        private MortalDiagnosisPublicationWitness(TreatmentSkillCommandEnvelope skills, MortalTreatmentItemCommandEnvelope items)
        { _skills = skills; _items = items.Clone(); }

        /// <summary>
        /// Captures inputs only from the actual frozen projection and its matching reservation.
        /// </summary>
        internal static MortalDiagnosisPublicationWitness Capture(MortalDiagnosisPublicationBinding publication)
        {
            if (!TryReadTreatmentSkillProjectionForEnvelopeSemantics(publication.SkillProjectionAuthority,
                    publication.ReservationAuthority, out var projection))
                throw new InvalidDataException("Diagnosis publication lost its actual frozen skill projection.");
            return Parse(new JsonObject
            {
                ["schemaVersion"] = 1, ["skillCommands"] = projection.CommandEnvelope.CommandRoot.DeepClone(),
                ["itemCommands"] = publication.ItemCommands.ToWitnessRoot()
            });
        }

        /// <summary>
        /// Copies the actual private continuation's frozen envelopes. These are
        /// deterministic comparison inputs, never a publication permission.
        /// </summary>
        internal static MortalDiagnosisPublicationWitness CaptureFrozen(TreatmentSkillCommandEnvelope skills,
            MortalTreatmentItemCommandEnvelope items) => Parse(new JsonObject
            {
                ["schemaVersion"] = 1, ["skillCommands"] = skills.CommandRoot.DeepClone(),
                ["itemCommands"] = items.ToWitnessRoot()
            });

        /// <summary>
        /// Rederives ordinary physical publication semantics from the shared
        /// original binding and pure outcome preparation calculation.
        /// </summary>
        internal string ComputeTreatmentSemantic(string acceptedFingerprint, string bindingFingerprint,
            MortalWoundTreatmentAttemptRequest request, MortalWoundTreatmentResolution resolution,
            string preparationFingerprint) => ComputeTreatmentPublicationEnvelopeFingerprint(
                ComputeTreatmentPublicationBaseFingerprint(acceptedFingerprint, bindingFingerprint,
                    request, resolution, preparationFingerprint), _skills, _items);

        /// <summary>
        /// Parses closed historical DATA through the same envelope freezers as live publication.
        /// </summary>
        internal static MortalDiagnosisPublicationWitness Parse(JsonObject root)
        {
            if (root.Count != 3 || root["schemaVersion"]?.GetValue<int>() != 1 ||
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
            return new(frozenSkills.Envelope, frozenItems.Envelope);
        }

        /// <summary>
        /// Copies the complete versioned inputs, preserving omission, null, empty arrays and order.
        /// </summary>
        internal JsonObject ToCanonicalJson() => new()
        {
            ["schemaVersion"] = 1, ["skillCommands"] = _skills.CommandRoot.DeepClone(),
            ["itemCommands"] = _items.ToWitnessRoot()
        };

        /// <summary>
        /// Rederives the publication semantic from terminal outcome DATA, never the outer result seal.
        /// </summary>
        internal string ComputeSemantic(string acceptedFingerprint, MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentResolution resolution, string workingFingerprint, string progressionFingerprint)
        {
            var critical = MortalWoundCriticalReactionPublicationPlanner.RecomposeDetached(request, resolution);
            return ComputeTreatmentPublicationEnvelopeFingerprint(ComputeMortalDiagnosisPublicationBaseFingerprint(
                acceptedFingerprint, request, resolution, workingFingerprint, progressionFingerprint, critical.Fingerprint!), _skills, _items);
        }
    }

    private static string ComputeMortalDiagnosisPublicationBaseFingerprint(string acceptedFingerprint,
        MortalWoundTreatmentAttemptRequest request, MortalWoundTreatmentResolution resolution,
        string workingFingerprint, string progressionFingerprint, string criticalFingerprint) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.mortal_diagnosis.publication", "1", acceptedFingerprint,
            request.RequestFingerprint, resolution.ResultFingerprint, workingFingerprint, progressionFingerprint, criticalFingerprint
        });
}
