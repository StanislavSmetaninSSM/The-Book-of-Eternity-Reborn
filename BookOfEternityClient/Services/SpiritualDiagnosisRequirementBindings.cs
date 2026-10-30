using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Closed historical proof DATA for the three supported requirements; never current authority.</summary>
internal static class SpiritualDiagnosisRequirementBindings
{
    internal static string Fingerprint(JsonArray rows) => WoundAcceptedTurnFingerprintWriter.Compute(new[]
    { "spiritual_diagnosis.requirement_bindings", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(rows) });

    internal static string StatementFingerprint(JsonElement statement) => WoundAcceptedTurnFingerprintWriter.Compute(new[]
    { "spiritual_diagnosis.requirement_statement", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonNode.Parse(statement.GetRawText())) });

    internal static void Validate(SpiritualDiagnosisAdmission admission)
    {
        try
        {
            var path = admission.Wound.Treatment.DiagnosisPaths.Single(p => p.DiagnosisPathId == admission.Selection.Diagnosis!.DiagnosisPathId);
            var parsed = MortalWoundTreatmentContract.ParseRequirementProjection(path.Requirements, "diagnosis.requirementBindings");
            var rows = admission.RequirementBindings;
            if (!parsed.IsValid || rows.Count != path.Requirements.Count) throw new InvalidDataException("Diagnosis requirement proof is incomplete.");
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index]!.AsObject();
                Closed(row, "ordinal", "statementFingerprint", "kind", "actor", "capabilityRef", "tier", "mirrorTier", "sourceDependencies");
                var statement = path.Requirements[index];
                var kind = statement.GetProperty("kind").GetString();
                if (row["ordinal"]!.GetValue<int>() != index || row["statementFingerprint"]!.GetValue<string>() != StatementFingerprint(statement) ||
                    row["kind"]!.GetValue<string>() != kind || kind is not ("source_capability" or "skill_tier" or "provider"))
                    throw new InvalidDataException("Diagnosis requirement proof differs from its exact declaration/order.");
                var role = kind == "provider" ? "provider" : statement.GetProperty("actorRole").GetString();
                var actor = role == "provider" ? new WoundOwnerCoordinate(admission.Wound.Owner.Realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath)
                    : admission.Wound.Owner;
                if (!JsonNode.DeepEquals(row["actor"], SpiritualHealingRequestProjection.OwnerJson(actor)))
                    throw new InvalidDataException("Diagnosis requirement proof has a foreign typed actor.");
                if (kind == "provider")
                {
                    if (statement.GetProperty("providerRef").GetString() != "player_soul" ||
                        row["capabilityRef"] is not null || row["tier"] is not null || row["mirrorTier"] is not null)
                        throw new InvalidDataException("Diagnosis provider proof is not its current player identity.");
                }
                else
                {
                    var art = statement.GetProperty("capabilityRef").GetString();
                    var tier = row["tier"]!.GetValue<int>();
                    if (art is not ("spiritual_healing" or "spiritual_stabilization") || row["capabilityRef"]!.GetValue<string>() != art || tier is < 0 or > 5 ||
                        actor.OwnerKind == "player_soul" && (row["mirrorTier"]?.GetValue<int>() != tier || art == "spiritual_healing" && tier != admission.HealingTier) ||
                        actor.OwnerKind != "player_soul" && row["mirrorTier"] is not null ||
                        kind == "skill_tier" && (statement.GetProperty("minimumTier").GetInt32() is < 0 or > 5 || tier < statement.GetProperty("minimumTier").GetInt32()))
                        throw new InvalidDataException("Diagnosis standard-art proof is absent, unbounded or insufficient.");
                }
                var dependencies = row["sourceDependencies"]!.AsArray();
                var expected = actor.OwnerKind == "player_soul" ? new[] { WoundCarrierCatalog.AfterlifeProfilesPath, "game_state/meta/soul_state.json" }
                    : new[] { WoundCarrierCatalog.AfterlifeProfilesPath };
                if (dependencies.Count != expected.Length) throw new InvalidDataException("Diagnosis source dependency set differs.");
                for (var sourceIndex = 0; sourceIndex < expected.Length; sourceIndex++)
                {
                    var source = dependencies[sourceIndex]!.AsObject(); Closed(source, "path", "fingerprint");
                    if (source["path"]!.GetValue<string>() != expected[sourceIndex] ||
                        !ResourceMaterializationContract.IsAuthorityFingerprint(source["fingerprint"]!.GetValue<string>()))
                        throw new InvalidDataException("Diagnosis source dependency is foreign or malformed.");
                }
            }
        }
        catch (Exception failure) when (failure is not InvalidDataException && failure is
            (InvalidOperationException or ArgumentException or JsonException or KeyNotFoundException or NullReferenceException))
        { throw new InvalidDataException("Malformed closed diagnosis requirement proof.", failure); }
    }

    private static void Closed(JsonObject row, params string[] fields)
    {
        if (row.Count != fields.Length || !fields.All(row.ContainsKey))
            throw new InvalidDataException("Diagnosis proof object has missing or unknown fields.");
    }
}
