using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalLocationAcceptedTurnPlanner
{
    /// <summary>Pure reference checks after successful Mortal owner composition. The supplied
    /// roots must belong to that same fenced operation. No allocation, registry or publication authority.</summary>
    internal static IReadOnlyList<ValidationIssue> ValidateVehicleBirthActorReferences(
        JsonObject vehicleRoot, JsonObject npcRoot, JsonObject? factionRoot,
        ResourceOwnerAuthority acceptedMortalOwners)
    {
        var issues = new List<ValidationIssue>();
        if (vehicleRoot["UpdateVehicles"] is not JsonArray updates) return issues;
        var factions = BuildFactionControlAuthority(factionRoot);
        var npcIndex = new ExactIdentityIndex();
        var acceptedNpcs = new Dictionary<string, ResourceOwnerAuthorityEntry>(StringComparer.Ordinal);
        foreach (var entry in acceptedMortalOwners.Entries.Values)
        {
            if (entry.Key.Realm != "mortal_world" || entry.Key.OwnerKind != ResourceOwnerKind.Npc) continue;
            acceptedNpcs.Add(entry.Key.ResourceOwnerId, entry);
            npcIndex.Add(entry.Key.ResourceOwnerId);
        }
        // Only selected IDs acquire envelope-validation work. This dictionary is operation-local
        // detached DATA, not a permission token or a persistent cache.
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var update in updates.OfType<JsonObject>())
            if (ReadExactString(update, "vehicleRef") is not null && update["controller"] is JsonObject controller &&
                ReadExactString(controller, "actorType") == "npc" && ReadExactString(controller, "actorId") is { } id)
                selected.Add(id);
        var actors = new Dictionary<string, (JsonObject Actor, string Path)>(StringComparer.Ordinal);
        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (npcRoot[section] is not JsonArray rows) continue;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i] is not JsonObject actor) continue;
                var id = ReadExactString(actor, "NPCId") ?? ReadExactString(actor, "initialId");
                if (id is null || !selected.Contains(id) || !acceptedNpcs.ContainsKey(id)) continue;
                // Successful composer already proved exact same-section uniqueness and the
                // semantic equality of legitimate cross-section mirrors. Keep one representative.
                actors.TryAdd(id, (actor, $"game_state/npcs/npc_core.json.{section}[{i}]"));
            }
        }
        var evidenceValid = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var id in selected)
        {
            if (!acceptedNpcs.TryGetValue(id, out var entry) || !actors.TryGetValue(id, out var row)) continue;
            var element = JsonSerializer.SerializeToElement(row.Actor);
            var diagnostics = entry.SameTurn
                ? ActorMaterializationContract.ValidateMortalNpc(element, row.Path, "NPCs")
                : ActorMaterializationContract.ValidateHistoricalMortalNpc(element, row.Path);
            var envelope = row.Actor[ActorMaterializationContract.PropertyName] as JsonObject;
            evidenceValid[id] = ReadExactString(envelope, "actorType") == "mortal_npc" &&
                ReadExactString(envelope, "actorId") == id &&
                !diagnostics.Any(issue => issue.Severity == IssueSeverity.Error);
        }
        for (var i = 0; i < updates.Count; i++)
        {
            if (updates[i] is not JsonObject vehicle || ReadExactString(vehicle, "vehicleRef") is null) continue;
            var path = $"game_state/misc/vehicles.json.UpdateVehicles[{i}]";
            if (vehicle["owner"] is JsonObject owner && ReadExactString(owner, "ownerType") == "Faction")
            {
                var id = ReadExactString(owner, "ownerId");
                string? name = null;
                var resolution = id is null ? MortalLocationReferenceResolution.Missing :
                    factions.ResolveOwner(id, allowTemporary: true, out name);
                if (resolution != MortalLocationReferenceResolution.Exact)
                    Add(path + ".owner.ownerId", Suffix(resolution), "one exact accepted Mortal faction");
                else
                {
                    if (!string.Equals(ReadExactString(owner, "ownerName"), name, StringComparison.Ordinal))
                        Add(path + ".owner.ownerName", "name_mismatch", "the exact accepted faction name");
                }
            }
            if (vehicle["controller"] is JsonObject controller && ReadExactString(controller, "actorType") == "npc")
            {
                var id = ReadExactString(controller, "actorId");
                var resolution = id is null ? MortalLocationReferenceResolution.Missing : npcIndex.Resolve(id);
                if (resolution != MortalLocationReferenceResolution.Exact)
                    Add(path + ".controller.actorId", Suffix(resolution), "one exact accepted Mortal NPC owner");
                else if (!evidenceValid.GetValueOrDefault(id!))
                    Add(path + ".controller.actorId", "realm_invalid", "matching complete Mortal NPC materialization evidence");
            }
        }
        return issues;

        void Add(string path, string suffix, string expected) => issues.Add(new ValidationIssue(path,
            IssueSeverity.Error, "Vehicle birth references unavailable or inconsistent actor authority.",
            code: "mortal_vehicle_birth_reference_" + suffix, section: "Vehicles", expected: expected,
            actual: "unresolved or invalid birth reference",
            repairHint: "Correct only this new vehicle's indexed owner/controller reference or name using the accepted Mortal actor/faction. Do not invent identities, alter canonical actors, or repair client-owned evidence."));
        static string Suffix(MortalLocationReferenceResolution resolution) => resolution switch
        {
            MortalLocationReferenceResolution.Confusable => "confusable",
            MortalLocationReferenceResolution.Ambiguous => "ambiguous",
            _ => "unknown"
        };
    }
}
