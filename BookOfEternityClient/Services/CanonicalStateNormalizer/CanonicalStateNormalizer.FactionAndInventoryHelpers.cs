using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;
public partial class CanonicalStateNormalizer
{
    private static IEnumerable<JsonObject> CollectFactionCoreEntries(JsonNode? root)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray.OfType<JsonObject>())
                yield return CloneObject(item);
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        foreach (var propName in new[] { "factions", "factionDataChanges" })
        {
            if (obj[propName] is not JsonArray arr)
                continue;

            foreach (var item in arr.OfType<JsonObject>())
                yield return CloneObject(item);
        }
    }

    private static JsonObject NormalizeFactionCoreEntry(JsonObject source)
    {
        var entry = CloneObject(source);
        var factionId = GetNodeString(entry["factionId"]);
        var initialId = GetNodeString(entry["initialId"]);
        var hasExplicitNullFactionId = entry.ContainsKey("factionId") && entry["factionId"] == null;

        if (string.IsNullOrWhiteSpace(factionId) &&
            !string.IsNullOrWhiteSpace(initialId) &&
            hasExplicitNullFactionId &&
            entry["isNewFaction"] is JsonValue isNewFactionValue &&
            isNewFactionValue.TryGetValue<bool>(out var isNewFaction) &&
            isNewFaction &&
            LooksLikeCanonicalNewFactionEntry(entry))
        {
            entry["factionId"] = initialId;
            entry.Remove("initialId");
            entry.Remove("isNewFaction");
        }

        return entry;
    }

    private static void RemoveMaterializedFactionCarrierFields(JsonObject entry)
    {
        if (entry[FactionMaterializationContract.PropertyName] is not JsonObject)
            return;

        foreach (var field in new[]
                 {
                     "governance",
                     "leadership",
                     "ranks",
                     "structuredBonuses",
                     "resources",
                     "activeProjects",
                     "completedProjects",
                     "customStates",
                     "scribeChronicle"
                 })
        {
            entry.Remove(field);
        }
    }

    private static JsonObject NormalizeFactionCoreForStorage(JsonObject rawEntry)
    {
        var entry = NormalizeFactionCoreEntry(rawEntry);
        RemoveMaterializedFactionCarrierFields(entry);
        return entry;
    }

    private static bool LooksLikeCanonicalNewFactionEntry(JsonObject entry)
    {
        return !string.IsNullOrWhiteSpace(GetNodeString(entry["name"])) &&
               !string.IsNullOrWhiteSpace(GetNodeString(entry["description"])) &&
               !string.IsNullOrWhiteSpace(GetNodeString(entry["developmentArchetype"])) &&
               entry["powerProfile"] is JsonObject &&
               entry["resources"] is JsonObject &&
               entry["ranks"] is JsonObject &&
               entry.ContainsKey("isPlayerFaction") &&
               entry.ContainsKey("isPlayerMember") &&
               entry["reputation"] != null &&
               entry["level"] != null &&
               entry["experience"] != null &&
               entry["experienceForNextLevel"] != null;
    }

    private static IEnumerable<JsonObject> CollectInitialFactionChronicleEntries(
        JsonNode? factionCoreRoot)
    {
        foreach (var rawFaction in CollectFactionEntryObjects(
                     factionCoreRoot,
                     "factionDataChanges"))
        {
            var faction = NormalizeFactionCoreEntry(rawFaction);
            var factionId = GetNodeString(faction["factionId"]);
            var factionName =
                GetNodeString(faction["factionName"]) ??
                GetNodeString(faction["name"]);
            if (string.IsNullOrWhiteSpace(factionId) ||
                faction["scribeChronicle"] is not JsonArray entries)
            {
                continue;
            }

            foreach (var entryNode in entries)
            {
                var entry = GetNodeString(entryNode);
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                yield return new JsonObject
                {
                    ["factionId"] = factionId,
                    ["factionName"] = factionName,
                    ["entry"] = entry
                };
            }
        }
    }

    private static void ApplyInventoryResourceCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateInventoryItemEntry(entries, command);
            if (command["resource"] != null)
                entry["resource"] = command["resource"]?.DeepClone();
            if (command["maximumResource"] != null)
                entry["maximumResource"] = command["maximumResource"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(command["resourceType"])))
                entry["resourceType"] = GetNodeString(command["resourceType"]);
            if (command["contentsPath"] != null)
                entry["contentsPath"] = command["contentsPath"]?.DeepClone();
            if (command["isEmpty"] != null)
                entry["isEmpty"] = command["isEmpty"]?.DeepClone();
        }
    }

    private static void ApplyInventoryBondCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateInventoryItemEntry(entries, command);
            if (command["newBondLevel"] != null)
                entry["ownerBondLevelCurrent"] = command["newBondLevel"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(command["changeReason"])))
                entry["lastBondChangeReason"] = GetNodeString(command["changeReason"]);
        }
    }

    private static void ApplyInventoryFateCardUnlockCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateInventoryItemEntry(entries, command);
            var fateCards = EnsureArray(entry, "fateCards");

            var card = new JsonObject
            {
                ["cardId"] = GetNodeString(command["cardId"]) ?? "",
                ["name"] = GetNodeString(command["cardName"]) ?? GetNodeString(command["cardId"]) ?? "card",
                ["isUnlocked"] = true
            };

            UpsertByIdentity(fateCards, card, "cardId", "name");
        }
    }

    private static void ApplyItemJournalCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var appendedEntry = GetNodeString(command["entryToAppend"]);
            if (string.IsNullOrWhiteSpace(appendedEntry))
                continue;

            var entry = GetOrCreateInventoryItemEntry(entries, command);
            var journalEntries = EnsureArray(entry, "journalEntries");
            AddUniqueNode(journalEntries, JsonValue.Create(StripPlayerFacingTurnAnchor(appendedEntry))!);
        }
    }

    private static void ApplyInventoryTextCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var textToAppend = GetNodeString(command["textToAppend"]);
            if (string.IsNullOrWhiteSpace(textToAppend))
                continue;

            var entry = GetOrCreateInventoryItemEntry(entries, command);
            var textContent = EnsureArray(entry, "textContent");
            textContent.Add(textToAppend);
        }
    }

    private static IEnumerable<JsonObject> CollectFactionEntryObjects(JsonNode? root, string propName)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray.OfType<JsonObject>())
            {
                var clone = CloneObject(item);
                NormalizeStoredFactionReference(clone);
                yield return clone;
            }
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        if (obj[propName] is JsonArray arr)
        {
            foreach (var item in arr.OfType<JsonObject>())
            {
                var clone = CloneObject(item);
                NormalizeStoredFactionReference(clone);
                yield return clone;
            }
        }
    }

    private static IEnumerable<JsonObject> CollectFactionStructureEntriesFromCore(JsonNode? root)
    {
        foreach (var rawEntry in CollectFactionEntryObjects(root, "factions"))
        {
            var entry = NormalizeFactionCoreEntry(rawEntry);
            var hasGovernance = entry["governance"] is JsonObject;
            var hasLeadership = entry["leadership"] is JsonObject;
            var hasRanks = entry["ranks"] is JsonObject;
            var hasStructuredBonuses = entry["structuredBonuses"] is JsonArray;
            if (!hasGovernance &&
                !hasLeadership &&
                !hasRanks &&
                !hasStructuredBonuses)
            {
                continue;
            }

            var result = new JsonObject
            {
                ["factionId"] = entry["factionId"]?.DeepClone(),
                ["factionName"] = entry["factionName"]?.DeepClone() ?? entry["name"]?.DeepClone(),
                ["name"] = entry["name"]?.DeepClone()
            };

            if (hasGovernance)
                result["governance"] = entry["governance"]!.DeepClone();
            if (hasLeadership)
                result["leadership"] = entry["leadership"]!.DeepClone();
            if (hasRanks)
                result["ranks"] = entry["ranks"]!.DeepClone();
            if (hasStructuredBonuses)
            {
                result["structuredBonuses"] =
                    entry["structuredBonuses"]!.DeepClone();
            }

            yield return result;
        }

        foreach (var rawEntry in CollectFactionEntryObjects(root, "factionDataChanges"))
        {
            var entry = NormalizeFactionCoreEntry(rawEntry);
            var hasGovernance = entry["governance"] is JsonObject;
            var hasLeadership = entry["leadership"] is JsonObject;
            var hasRanks = entry["ranks"] is JsonObject;
            var hasStructuredBonuses = entry["structuredBonuses"] is JsonArray;
            if (!hasGovernance &&
                !hasLeadership &&
                !hasRanks &&
                !hasStructuredBonuses)
            {
                continue;
            }

            var result = new JsonObject
            {
                ["factionId"] = entry["factionId"]?.DeepClone(),
                ["factionName"] = entry["factionName"]?.DeepClone() ?? entry["name"]?.DeepClone(),
                ["name"] = entry["name"]?.DeepClone()
            };

            if (hasGovernance)
                result["governance"] = entry["governance"]!.DeepClone();
            if (hasLeadership)
                result["leadership"] = entry["leadership"]!.DeepClone();
            if (hasRanks)
                result["ranks"] = entry["ranks"]!.DeepClone();
            if (hasStructuredBonuses)
            {
                result["structuredBonuses"] =
                    entry["structuredBonuses"]!.DeepClone();
            }

            yield return result;
        }
    }

    private static IEnumerable<JsonObject> CollectFactionResourceEntriesFromCore(JsonNode? root)
    {
        foreach (var propName in new[] { "factions", "factionDataChanges" })
        {
            foreach (var rawEntry in CollectFactionEntryObjects(root, propName))
            {
                var entry = NormalizeFactionCoreEntry(rawEntry);
                if (entry["resources"] is not JsonObject resources)
                    continue;

                var result = new JsonObject
                {
                    ["factionId"] = entry["factionId"]?.DeepClone(),
                    ["factionName"] = entry["factionName"]?.DeepClone() ?? entry["name"]?.DeepClone(),
                    ["name"] = entry["name"]?.DeepClone(),
                    ["metaResources"] = new JsonArray(),
                    ["strategicGoods"] = new JsonArray()
                };

                if (resources["metaResources"] is JsonArray metaResources)
                    result["metaResources"] = metaResources.DeepClone();
                if (resources["strategicGoods"] is JsonArray strategicGoods)
                    result["strategicGoods"] = strategicGoods.DeepClone();

                if (result["metaResources"] != null || result["strategicGoods"] != null)
                    yield return result;
            }
        }
    }

    private static void CollectFactionProjectsFromCore(JsonNode? root, List<JsonObject> activeProjects, List<JsonObject> completedProjects)
    {
        foreach (var propName in new[] { "factions", "factionDataChanges" })
        {
            foreach (var rawEntry in CollectFactionEntryObjects(root, propName))
            {
                var entry = NormalizeFactionCoreEntry(rawEntry);
                var factionId = GetNodeString(entry["factionId"]);
                var factionName = GetNodeString(entry["factionName"]) ?? GetNodeString(entry["name"]);

                if (entry["activeProjects"] is JsonArray activeArray)
                {
                    foreach (var project in activeArray.OfType<JsonObject>())
                    {
                        var projectClone = CloneObject(project);
                        projectClone["factionId"] = factionId;
                        projectClone["factionName"] = factionName;
                        UpsertProjectByIdentity(activeProjects, projectClone);
                    }
                }

                if (entry["completedProjects"] is JsonArray completedArray)
                {
                    foreach (var project in completedArray.OfType<JsonObject>())
                    {
                        var projectClone = CloneObject(project);
                        projectClone["factionId"] = factionId;
                        projectClone["factionName"] = factionName;
                        UpsertProjectByIdentity(completedProjects, projectClone);
                    }
                }
            }
        }
    }

    private static bool HasExplicitMaterializedFactionProjectSurface(JsonNode? root)
    {
        foreach (var propName in new[] { "factions", "factionDataChanges" })
        {
            foreach (var rawEntry in CollectFactionEntryObjects(root, propName))
            {
                var entry = NormalizeFactionCoreEntry(rawEntry);
                if (entry[FactionMaterializationContract.PropertyName] is JsonObject &&
                    entry["activeProjects"] is JsonArray &&
                    entry["completedProjects"] is JsonArray)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<JsonObject> CollectFactionCustomEntriesFromCore(JsonNode? root)
    {
        foreach (var propName in new[] { "factions", "factionDataChanges" })
        {
            foreach (var rawEntry in CollectFactionEntryObjects(root, propName))
            {
                var entry = NormalizeFactionCoreEntry(rawEntry);
                if (entry["customStates"] is not JsonArray customStates)
                    continue;

                yield return new JsonObject
                {
                    ["factionId"] = entry["factionId"]?.DeepClone(),
                    ["factionName"] = entry["factionName"]?.DeepClone() ?? entry["name"]?.DeepClone(),
                    ["name"] = entry["name"]?.DeepClone(),
                    ["customStates"] = customStates.DeepClone()
                };
            }
        }
    }

    private static void ApplyFactionRankChangeCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateFactionEntry(entries, command);
            var ranksRoot = entry["ranks"] as JsonObject ?? new JsonObject();
            var branches = EnsureArray(ranksRoot, "branches");

            if (command["branchesToRemove"] is JsonArray branchesToRemove)
            {
                foreach (var branchIdNode in branchesToRemove)
                {
                    var branchId = GetNodeString(branchIdNode);
                    if (string.IsNullOrWhiteSpace(branchId))
                        continue;

                    RemoveBranchById(branches, branchId);
                }
            }

            if (command["ranksToRemove"] is JsonArray ranksToRemove)
            {
                foreach (var rankRemoval in ranksToRemove.OfType<JsonObject>())
                    RemoveRankByIdentifier(branches, GetNodeString(rankRemoval["targetBranchId"]), GetNodeString(rankRemoval["rankIdentifier"]));
            }

            if (command["branchesToAdd"] is JsonArray branchesToAdd)
            {
                foreach (var branch in branchesToAdd.OfType<JsonObject>())
                {
                    var branchClone = CloneObject(branch);
                    EnsureArray(branchClone, "ranks");
                    UpsertByIdentity(branches, branchClone, "branchId", "displayName");
                }
            }

            if (command["ranksToAdd"] is JsonArray ranksToAdd)
            {
                foreach (var rankAdd in ranksToAdd.OfType<JsonObject>())
                {
                    var branchId = GetNodeString(rankAdd["targetBranchId"]);
                    if (string.IsNullOrWhiteSpace(branchId) || rankAdd["rank"] is not JsonObject rank)
                        continue;

                    var branch = GetOrCreateBranch(branches, branchId);
                    var rankArray = EnsureArray(branch, "ranks");
                    UpsertByIdentity(rankArray, CloneObject(rank), "rankNameMale", "rankNameFemale", "name");
                }
            }

            if (command["branchesToUpdate"] is JsonArray branchesToUpdate)
            {
                foreach (var branchUpdate in branchesToUpdate.OfType<JsonObject>())
                {
                    var branchId = GetNodeString(branchUpdate["branchId"]);
                    if (string.IsNullOrWhiteSpace(branchId))
                        continue;

                    var branch = GetOrCreateBranch(branches, branchId);
                    if (!string.IsNullOrWhiteSpace(GetNodeString(branchUpdate["newDisplayName"])))
                        branch["displayName"] = GetNodeString(branchUpdate["newDisplayName"]);
                }
            }

            if (command["ranksToUpdate"] is JsonArray ranksToUpdate)
            {
                foreach (var rankUpdate in ranksToUpdate.OfType<JsonObject>())
                {
                    var branchId = GetNodeString(rankUpdate["targetBranchId"]);
                    var rankIdentifier = GetNodeString(rankUpdate["rankIdentifier"]);
                    if (string.IsNullOrWhiteSpace(branchId) || string.IsNullOrWhiteSpace(rankIdentifier) || rankUpdate["update"] is not JsonObject update)
                        continue;

                    var branch = GetOrCreateBranch(branches, branchId);
                    var rankArray = EnsureArray(branch, "ranks");
                    var rank = rankArray
                        .OfType<JsonObject>()
                        .FirstOrDefault(item =>
                            string.Equals(GetNodeString(item["rankNameMale"]), rankIdentifier, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(GetNodeString(item["rankNameFemale"]), rankIdentifier, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(GetNodeString(item["name"]), rankIdentifier, StringComparison.OrdinalIgnoreCase));
                    if (rank == null)
                        continue;

                    ApplyRankUpdate(rank, update);
                }
            }

            entry["ranks"] = ranksRoot;
        }
    }

    private static void ApplyFactionBonusChangeCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateFactionEntry(entries, command);
            var bonuses = EnsureArray(entry, "structuredBonuses");

            if (command["bonusesToRemove"] is JsonArray bonusesToRemove)
            {
                foreach (var bonusIdNode in bonusesToRemove)
                {
                    var bonusId = GetNodeString(bonusIdNode);
                    if (string.IsNullOrWhiteSpace(bonusId))
                        continue;

                    RemoveByIdentity(bonuses, "bonusId", bonusId);
                }
            }

            if (command["bonusesToAddOrUpdate"] is JsonArray bonusesToAddOrUpdate)
            {
                foreach (var bonus in bonusesToAddOrUpdate.OfType<JsonObject>())
                    UpsertByIdentity(bonuses, CloneObject(bonus), "bonusId", "description", "bonusType", "target");
            }
        }
    }

    private static void ApplyFactionResourceChangeCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateFactionEntry(entries, command);
            var metaResources = EnsureArray(entry, "metaResources");
            var strategicGoods = EnsureArray(entry, "strategicGoods");

            if (command["resourceChanges"] is not JsonArray resourceChanges)
                continue;

            foreach (var resourceChange in resourceChanges.OfType<JsonObject>())
            {
                var resourceName = GetNodeString(resourceChange["resourceName"]);
                if (string.IsNullOrWhiteSpace(resourceName))
                    continue;

                var targetArray = IsMetaResource(resourceName) ? metaResources : strategicGoods;
                var resource = targetArray
                    .OfType<JsonObject>()
                    .FirstOrDefault(item => string.Equals(GetNodeString(item["resourceName"]), resourceName, StringComparison.OrdinalIgnoreCase));
                if (resource == null)
                {
                    resource = new JsonObject
                    {
                        ["resourceName"] = resourceName,
                        ["currentStockpile"] = 0,
                        ["incomePerCycle"] = 0
                    };
                    if (IsMetaResource(resourceName))
                        resource["upkeepPerCycle"] = 0;
                    targetArray.Add(resource);
                }

                var currentStockpile = GetNodeInt(resource["currentStockpile"]);
                resource["currentStockpile"] = currentStockpile + GetNodeInt(resourceChange["changeAmount"]);
            }
        }
    }

    private static void CollectFactionProjectObjects(JsonObject? root, string propName, List<JsonObject> target)
    {
        if (root?[propName] is not JsonArray arr)
            return;

        foreach (var item in arr.OfType<JsonObject>())
            UpsertProjectByIdentity(target, CloneObject(item));
    }

    private static void CollectFactionProjectObjects(JsonNode? root, string propName, List<JsonObject> target)
    {
        if (root is JsonObject obj)
            CollectFactionProjectObjects(obj, propName, target);
    }

    private static void ApplyFactionProjectUpdateCommands(List<JsonObject> activeProjects, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            if (command["projectUpdate"] is not JsonObject projectUpdate)
                continue;

            var project = new JsonObject
            {
                ["factionId"] = command["factionId"]?.DeepClone() ?? command["initialFactionId"]?.DeepClone(),
                ["factionName"] = command["factionName"]?.DeepClone() ?? command["name"]?.DeepClone(),
                ["projectId"] = projectUpdate["projectId"]?.DeepClone()
            };

            MergeObject(project, projectUpdate);
            NormalizeStoredFactionReference(project);
            if (string.IsNullOrWhiteSpace(GetNodeString(project["projectName"])) &&
                !string.IsNullOrWhiteSpace(GetNodeString(project["name"])))
            {
                project["projectName"] = GetNodeString(project["name"]);
            }
            if (string.IsNullOrWhiteSpace(GetNodeString(project["projectName"])))
                project["projectName"] = GetNodeString(project["projectId"]) ?? "project";

            UpsertProjectByIdentity(activeProjects, project);
        }
    }

    private static void ApplyFactionProjectCompletionCommands(List<JsonObject> activeProjects, List<JsonObject> completedProjects, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var factionId = GetNodeString(command["factionId"]);
            var initialFactionId = GetNodeString(command["initialFactionId"]);
            var effectiveFactionId = ResolveFactionIdentity(factionId, initialFactionId);
            var factionName = GetNodeString(command["factionName"]) ?? GetNodeString(command["name"]);
            var projectId = GetNodeString(command["projectId"]);
            if (string.IsNullOrWhiteSpace(projectId))
                continue;

            var existing = activeProjects.FirstOrDefault(project =>
                string.Equals(GetNodeString(project["projectId"]), projectId, StringComparison.OrdinalIgnoreCase) &&
                FactionIdentityMatches(project, effectiveFactionId));

            var completed = existing != null ? CloneObject(existing) : new JsonObject();
            completed["factionId"] = effectiveFactionId;
            completed["factionName"] = factionName;
            completed["projectId"] = projectId;
            completed["projectName"] = GetNodeString(command["projectName"]) ?? GetNodeString(completed["projectName"]) ?? projectId;
            completed["finalState"] = GetNodeString(command["finalState"]) ?? "Completed";
            completed["completionTurn"] = GetNodeString(command["completionTurn"]) ?? GetNodeString(completed["completionTurn"]) ?? "";
            completed.Remove("activeState");
            completed.Remove("initialFactionId");

            if (existing != null)
                activeProjects.Remove(existing);

            UpsertProjectByIdentity(completedProjects, completed);
        }
    }

    private static void ApplyFactionCustomStateCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateFactionEntry(entries, command);
            var customStates = EnsureArray(entry, "customStates");

            if (command["statesToRemove"] is JsonArray statesToRemove)
            {
                foreach (var stateIdNode in statesToRemove)
                {
                    var stateId = GetNodeString(stateIdNode);
                    if (string.IsNullOrWhiteSpace(stateId))
                        continue;

                    RemoveByIdentity(customStates, "stateId", stateId);
                }
            }

            if (command["statesToAddOrUpdate"] is JsonArray statesToAddOrUpdate)
            {
                foreach (var state in statesToAddOrUpdate.OfType<JsonObject>())
                    UpsertByIdentity(customStates, CloneObject(state), "stateId", "name", "title");
            }
        }
    }

    private static JsonObject GetOrCreateFactionEntry(JsonArray entries, JsonObject source)
    {
        var factionId = ResolveFactionIdentity(GetNodeString(source["factionId"]), GetNodeString(source["initialFactionId"]));
        var existing = entries
            .OfType<JsonObject>()
            .FirstOrDefault(item => FactionIdentityMatches(item, factionId));
        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(factionId))
                existing["factionId"] = factionId;
            existing.Remove("initialFactionId");
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["factionName"])))
                existing["factionName"] = source["factionName"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["name"])))
                existing["name"] = source["name"]?.DeepClone();
            return existing;
        }

        var created = new JsonObject
        {
            ["factionId"] = factionId,
            ["factionName"] = source["factionName"]?.DeepClone() ?? source["name"]?.DeepClone() ?? source["initialFactionId"]?.DeepClone(),
            ["name"] = source["name"]?.DeepClone() ?? source["factionName"]?.DeepClone()
        };
        entries.Add(created);
        return created;
    }

    private static JsonObject GetOrCreateInventoryItemEntry(JsonArray entries, JsonObject source)
    {
        var existing = entries
            .OfType<JsonObject>()
            .FirstOrDefault(item =>
                MatchesByAnyIdentity(item, source, "existedId", "itemId", "id", "itemName", "name"));
        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["existedId"])))
                existing["existedId"] = source["existedId"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["itemId"])))
                existing["itemId"] = source["itemId"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["id"])))
                existing["id"] = source["id"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["itemName"])))
                existing["itemName"] = source["itemName"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["name"])))
                existing["name"] = source["name"]?.DeepClone();
            return existing;
        }

        var created = new JsonObject
        {
            ["existedId"] = source["existedId"]?.DeepClone() ?? source["itemId"]?.DeepClone() ?? source["id"]?.DeepClone(),
            ["itemId"] = source["itemId"]?.DeepClone() ?? source["existedId"]?.DeepClone() ?? source["id"]?.DeepClone(),
            ["id"] = source["id"]?.DeepClone() ?? source["itemId"]?.DeepClone() ?? source["existedId"]?.DeepClone(),
            ["itemName"] = source["itemName"]?.DeepClone() ?? source["name"]?.DeepClone(),
            ["name"] = source["name"]?.DeepClone() ?? source["itemName"]?.DeepClone()
        };
        entries.Add(created);
        return created;
    }

    private static JsonObject GetOrCreateBranch(JsonArray branches, string branchId)
    {
        var branch = branches
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["branchId"]), branchId, StringComparison.OrdinalIgnoreCase));
        if (branch != null)
            return branch;

        branch = new JsonObject
        {
            ["branchId"] = branchId,
            ["displayName"] = branchId,
            ["isCoreBranch"] = false,
            ["ranks"] = new JsonArray()
        };
        branches.Add(branch);
        return branch;
    }

    private static void ApplyRankUpdate(JsonObject rank, JsonObject update)
    {
        if (!string.IsNullOrWhiteSpace(GetNodeString(update["newRankNameMale"])))
            rank["rankNameMale"] = GetNodeString(update["newRankNameMale"]);
        if (!string.IsNullOrWhiteSpace(GetNodeString(update["newRankNameFemale"])))
            rank["rankNameFemale"] = GetNodeString(update["newRankNameFemale"]);
        if (update["newRequiredReputation"] != null)
            rank["requiredReputation"] = update["newRequiredReputation"]?.DeepClone();
        if (!string.IsNullOrWhiteSpace(GetNodeString(update["newUnlockCondition"])))
            rank["unlockCondition"] = GetNodeString(update["newUnlockCondition"]);
        if (update["newBenefits"] is JsonArray newBenefits)
            rank["benefits"] = newBenefits.DeepClone();
        if (update["newIsJunctionPoint"] != null)
            rank["isJunctionPoint"] = update["newIsJunctionPoint"]?.DeepClone();
        if (update["newAvailableBranches"] is JsonArray newAvailableBranches)
            rank["availableBranches"] = newAvailableBranches.DeepClone();
    }

    private static void RemoveBranchById(JsonArray branches, string? branchId)
    {
        if (string.IsNullOrWhiteSpace(branchId))
            return;

        for (var i = branches.Count - 1; i >= 0; i--)
        {
            if (branches[i] is not JsonObject branch)
                continue;
            if (string.Equals(GetNodeString(branch["branchId"]), branchId, StringComparison.OrdinalIgnoreCase))
            {
                branches.RemoveAt(i);
                return;
            }
        }
    }

    private static void RemoveRankByIdentifier(JsonArray branches, string? targetBranchId, string? rankIdentifier)
    {
        if (string.IsNullOrWhiteSpace(targetBranchId) || string.IsNullOrWhiteSpace(rankIdentifier))
            return;

        var branch = branches
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["branchId"]), targetBranchId, StringComparison.OrdinalIgnoreCase));
        if (branch?["ranks"] is not JsonArray ranks)
            return;

        for (var i = ranks.Count - 1; i >= 0; i--)
        {
            if (ranks[i] is not JsonObject rank)
                continue;
            if (string.Equals(GetNodeString(rank["rankNameMale"]), rankIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetNodeString(rank["rankNameFemale"]), rankIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetNodeString(rank["name"]), rankIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                ranks.RemoveAt(i);
                return;
            }
        }
    }

    private static bool IsMetaResource(string? resourceName)
    {
        return string.Equals(resourceName, "Wealth", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(resourceName, "Influence", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(resourceName, "Manpower", StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveByIdentity(JsonArray items, string keyName, string expectedValue)
    {
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] is not JsonObject item)
                continue;
            if (string.Equals(GetNodeString(item[keyName]), expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                items.RemoveAt(i);
                return;
            }
        }
    }

    private static void UpsertProjectByIdentity(List<JsonObject> projects, JsonObject candidate)
    {
        NormalizeStoredFactionReference(candidate);
        var candidateFactionId = GetNodeString(candidate["factionId"]);
        var existing = projects.FirstOrDefault(project =>
            MatchesByAnyIdentity(project, candidate, "projectId") &&
            FactionIdentityMatches(project, candidateFactionId));

        if (existing != null)
        {
            MergeObject(existing, candidate);
            NormalizeStoredFactionReference(existing);
            return;
        }

        projects.Add(candidate.DeepClone()!.AsObject());
    }

}

