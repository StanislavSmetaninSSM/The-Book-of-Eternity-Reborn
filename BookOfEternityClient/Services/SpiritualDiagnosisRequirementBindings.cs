using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Closed historical requirement proof DATA; never current authority.</summary>
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
            var actualPath = WoundDiagnosisEvidence.ComputePathFingerprint(admission.Wound, path.DiagnosisPathId);
            var actualRequirements = WoundAcceptedTurnFingerprintWriter.Compute(new[] { "spiritual_diagnosis.requirements", "1",
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(new JsonArray(path.Requirements.Select(r => JsonNode.Parse(r.GetRawText())).ToArray())) });
            if (admission.PathFingerprint != actualPath || admission.Selection.Diagnosis!.ExpectedDiagnosisPathFingerprint != actualPath ||
                admission.RequirementFingerprint != actualRequirements || path.Check.ValueKind != JsonValueKind.Object || path.Check.EnumerateObject().Any())
                throw new InvalidDataException("Diagnosis admission metadata differs from its actual selected path/requirements/check.");
            var parsed = MortalWoundTreatmentContract.ParseRequirementProjection(path.Requirements, "diagnosis.requirementBindings");
            var rows = admission.RequirementBindings;
            if (!parsed.IsValid || rows.Count != path.Requirements.Count) throw new InvalidDataException("Diagnosis requirement proof is incomplete.");
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index]!.AsObject();
                var statement = path.Requirements[index];
                var kind = statement.GetProperty("kind").GetString();
                if (row["ordinal"]!.GetValue<int>() != index || row["statementFingerprint"]!.GetValue<string>() != StatementFingerprint(statement) ||
                    row["kind"]!.GetValue<string>() != kind)
                    throw new InvalidDataException("Diagnosis requirement proof differs from its exact declaration/order.");
                if (kind is not null && SpiritualDiagnosisCanonicalSources.IsCanonicalKind(kind))
                {
                    ValidateCanonical(row, parsed.Requirements[index], admission);
                    continue;
                }
                Closed(row, "ordinal", "statementFingerprint", "kind", "actor", "capabilityRef", "tier", "mirrorTier", "sourceDependencies");
                if (kind is not ("source_capability" or "skill_tier" or "provider"))
                    throw new InvalidDataException("Unknown diagnosis proof kind.");
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
            (InvalidOperationException or ArgumentException or JsonException or KeyNotFoundException or NullReferenceException or FormatException))
        { throw new InvalidDataException("Malformed closed diagnosis requirement proof.", failure); }
    }

    private static void ValidateCanonical(JsonObject row, MortalWoundTreatmentRequirement requirement, SpiritualDiagnosisAdmission admission)
    {
        Closed(row, "ordinal", "statementFingerprint", "kind", "actor", "evidence", "sourceDependencies");
        var provider = new WoundOwnerCoordinate(admission.Wound.Owner.Realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
        var role = requirement switch { MortalWoundItemQuantityRequirement r => r.OwnerRole, MortalWoundResourceQuantityRequirement r => r.OwnerRole,
            MortalWoundLocationRequirement r => r.TargetRole, MortalWoundEffectStateRequirement r => r.TargetRole, _ => "provider" };
        var actor = role == "provider" ? provider : admission.Wound.Owner;
        void Require(bool condition) { if (!condition) throw new InvalidDataException("Diagnosis canonical proof differs from its exact requirement/evidence shape."); }
        Require(JsonNode.DeepEquals(row["actor"], SpiritualHealingRequestProjection.OwnerJson(actor)));
        var evidence = row["evidence"]!.AsObject();
        string Text(string key) => evidence[key]!.GetValue<string>();
        switch (requirement)
        {
            case MortalWoundItemQuantityRequirement item:
                Closed(evidence, "itemRef", "quantity", "availableQuantity");
                Require(actor.OwnerKind == "player_soul" && actor.OwnerId == "player_soul" && Text("itemRef") == item.ItemRef &&
                    item.Quantity == 1 && evidence["quantity"]!.GetValue<int>() == 1 && evidence["availableQuantity"]!.GetValue<int>() == 1);
                break;
            case MortalWoundResourceQuantityRequirement resource:
                Closed(evidence, "resourceRef", "quantity", "resourceOwnerId", "current", "maximum", "resourceStateFingerprint", "resourceHistoryFingerprint", "ownerAuthorityFingerprint");
                Require(Text("resourceRef") == resource.ResourceRef && evidence["quantity"]!.GetValue<int>() == resource.Quantity &&
                    ResourceMaterializationContract.IsExactIdentifier(Text("resourceOwnerId")) &&
                    evidence["current"]!.GetValue<decimal>() >= resource.Quantity && evidence["maximum"]!.GetValue<decimal>() >= evidence["current"]!.GetValue<decimal>() &&
                    new[] { "resourceStateFingerprint", "resourceHistoryFingerprint", "ownerAuthorityFingerprint" }.All(key => ResourceMaterializationContract.IsAuthorityFingerprint(Text(key))));
                break;
            case MortalWoundConsentRequirement consent:
                Closed(evidence, "consentRef", "provider", "target", "locationId", "status");
                Require(Text("consentRef") == consent.ConsentRef && Text("status") == "granted" && consent.ProviderRef == provider.OwnerId && consent.TargetRef == admission.Wound.Owner.OwnerId &&
                    JsonNode.DeepEquals(evidence["provider"], SpiritualHealingRequestProjection.OwnerJson(provider)) &&
                    JsonNode.DeepEquals(evidence["target"], SpiritualHealingRequestProjection.OwnerJson(admission.Wound.Owner)));
                Require(ResourceMaterializationContract.IsExactIdentifier(Text("locationId")));
                break;
            case MortalWoundFacilityRequirement facility:
                Closed(evidence, "facilityRef", "locationId", "available");
                Require(Text("facilityRef") == facility.FacilityRef && evidence["available"]!.GetValue<bool>() && ResourceMaterializationContract.IsExactIdentifier(Text("locationId")));
                break;
            case MortalWoundLocationRequirement location:
                Closed(evidence, "locationRef", "locationId", "actor");
                Require(Text("locationRef") == location.LocationRef && Text("locationId") == location.LocationRef &&
                    JsonNode.DeepEquals(evidence["actor"], SpiritualHealingRequestProjection.OwnerJson(actor)));
                break;
            case MortalWoundQuestStateRequirement quest:
                Closed(evidence, "questRef", "state"); Require(Text("questRef") == quest.QuestRef && Text("state") == quest.RequiredState &&
                    quest.RequiredState is "active" or "completed" or "failed" or "abandoned");
                break;
            case MortalWoundEffectStateRequirement effect:
                Closed(evidence, "effectRef", "state", "actor", "effectFingerprint");
                Require(Text("effectRef") == effect.EffectRef && Text("state") == effect.RequiredState &&
                    JsonNode.DeepEquals(evidence["actor"], SpiritualHealingRequestProjection.OwnerJson(actor)) &&
                    ResourceMaterializationContract.IsAuthorityFingerprint(Text("effectFingerprint")));
                break;
            case MortalWoundEnvironmentRequirement environment:
                Closed(evidence, "environmentRef", "state", "locationId"); Require(Text("environmentRef") == environment.EnvironmentRef &&
                    Text("state") == environment.RequiredState && ResourceMaterializationContract.IsExactIdentifier(Text("locationId")));
                break;
            default: throw new InvalidDataException("Unknown canonical diagnosis proof.");
        }
        var expectedPaths = SpiritualDiagnosisCanonicalSources.Paths(admission.Wound.Owner.Realm, new[] { requirement.Kind });
        var dependencies = row["sourceDependencies"]!.AsArray();
        Require(dependencies.Count == expectedPaths.Count);
        for (var index = 0; index < dependencies.Count; index++)
        {
            var dependency = dependencies[index]!.AsObject(); Closed(dependency, "path", "fingerprint");
            Require(dependency["path"]!.GetValue<string>() == expectedPaths[index] && ResourceMaterializationContract.IsAuthorityFingerprint(dependency["fingerprint"]!.GetValue<string>()));
        }
    }

    private static void Closed(JsonObject row, params string[] fields)
    {
        if (row.Count != fields.Length || !fields.All(row.ContainsKey))
            throw new InvalidDataException("Diagnosis proof object has missing or unknown fields.");
    }
}
