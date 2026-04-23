using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningStateValidationTests
{
    [Fact]
    public void ValidateShiningAbodeStateFile_InvalidEnumBackedFields_RaiseExplicitErrors()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 1 },
          "lightSparks": 40,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_invalid",
              "originType": "broken_origin",
              "hallId": "hall_invalid",
              "charter": {
                "factionName": "Испорченная фракция",
                "favoredArchetype": "broken_archetype",
                "patronEffectFamily": "broken_family",
                "summary": "Тест"
              },
              "leadership": {
                "headActorType": "guardian",
                "headActorId": "guardian_old",
                "leadershipState": "secure"
              },
              "baseStrength": 30,
              "factionStrength": 30,
              "investCountThisAscension": 0,
              "projects": [
                {
                  "projectId": "project_invalid",
                  "displayName": "Ломанный проект",
                  "summary": "Тест",
                  "toneTags": ["broken"],
                  "targetFactionIds": [],
                  "projectArchetype": "broken_project_archetype",
                  "outputEffectFamily": "broken_output_family",
                  "tier": 2,
                  "status": "broken_status",
                  "isSupported": false,
                  "strengthReward": 0
                }
              ],
              "tradeInventoryReceipts": [],
              "leadershipReceipts": [],
              "leadershipHistory": []
            }
          ],
          "shiningPoliticalActors": [],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": [],
          "coreActionReceipts": [],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_origin_type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_favored_archetype", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_patron_effect_family", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_project_archetype", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_output_effect_family", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_project_status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_InvalidPoliticalEnums_RaiseExplicitErrors()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 1 },
          "lightSparks": 40,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_invalid",
              "originType": "ascended_guardian",
              "hallId": "hall_invalid",
              "charter": {
                "factionName": "Испорченная фракция",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Тест"
              },
              "leadership": {
                "headActorType": "guardian",
                "headActorId": "guardian_old",
                "leadershipState": "broken_state"
              },
              "baseStrength": 30,
              "factionStrength": 30,
              "investCountThisAscension": 0,
              "projects": [],
              "tradeInventoryReceipts": [],
              "leadershipReceipts": [],
              "leadershipHistory": []
            }
          ],
          "shiningPoliticalActors": [
            {
              "actorId": "actor_invalid",
              "actorType": "broken_actor_type",
              "displayName": "Ломаный актор",
              "summary": "Тест",
              "originFactionId": "faction_invalid",
              "currentFactionId": "faction_invalid",
              "politicalStatus": "broken_status"
            }
          ],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": [],
          "coreActionReceipts": [],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_invalid_state", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_political_actor_invalid_type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_political_actor_invalid_status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_DuplicateReceiptRequestIds_RaiseExplicitErrors()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 1 },
          "lightSparks": 40,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_alpha",
              "originType": "ascended_guardian",
              "hallId": "hall_alpha",
              "charter": {
                "factionName": "Фракция Альфа",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Тест"
              },
              "leadership": {
                "headActorType": "guardian",
                "headActorId": "guardian_old",
                "leadershipState": "secure"
              },
              "baseStrength": 30,
              "factionStrength": 30,
              "investCountThisAscension": 0,
              "projects": [],
              "tradeInventoryReceipts": [
                { "requestId": "dup_trade", "factionId": "faction_alpha", "tradeCycleId": "cycle_1", "status": "ready", "itemCount": 1, "soldOutCount": 0, "resolvedAtTurn": 1, "resolvedAtUtc": "2026-04-20T00:00:00Z" },
                { "requestId": "dup_trade", "factionId": "faction_alpha", "tradeCycleId": "cycle_1", "status": "ready", "itemCount": 1, "soldOutCount": 0, "resolvedAtTurn": 2, "resolvedAtUtc": "2026-04-20T00:01:00Z" }
              ],
              "leadershipReceipts": [
                { "requestId": "dup_lead", "transitionMode": "peaceful_succession", "status": "accepted", "resolvedAtTurn": 1, "resolvedAtUtc": "2026-04-20T00:00:00Z", "previousHeadActorType": "guardian", "previousHeadActorId": "guardian_old", "newHeadActorType": "resident", "newHeadActorId": "resident_new" },
                { "requestId": "dup_lead", "transitionMode": "peaceful_succession", "status": "accepted", "resolvedAtTurn": 2, "resolvedAtUtc": "2026-04-20T00:01:00Z", "previousHeadActorType": "guardian", "previousHeadActorId": "guardian_old", "newHeadActorType": "resident", "newHeadActorId": "resident_new" }
              ],
              "leadershipHistory": [
                { "requestId": "dup_history", "eventType": "peaceful_succession", "turnNumber": 1, "summary": "old" },
                { "requestId": "dup_history", "eventType": "peaceful_succession", "turnNumber": 2, "summary": "new" }
              ]
            }
          ],
          "shiningPoliticalActors": [],
          "factionFoundingReceipts": [
            { "requestId": "dup_found", "proposedFactionId": "faction_beta", "proposedHallId": "hall_beta", "hallName": "Зал", "factionId": "faction_beta", "hallId": "hall_beta", "status": "accepted", "supportingResidentIds": [], "resolvedAtTurn": 1, "resolvedAtUtc": "2026-04-20T00:00:00Z" },
            { "requestId": "dup_found", "proposedFactionId": "faction_beta", "proposedHallId": "hall_beta", "hallName": "Зал", "factionId": "faction_beta", "hallId": "hall_beta", "status": "accepted", "supportingResidentIds": [], "resolvedAtTurn": 2, "resolvedAtUtc": "2026-04-20T00:01:00Z" }
          ],
          "factionRealignmentReceipts": [
            { "requestId": "dup_realign", "residentId": "resident_alpha", "residentName": "Резидент", "sourceFactionId": "faction_alpha", "targetFactionId": "faction_beta", "status": "accepted", "realignmentMode": "accepted_transfer", "resolvedAtTurn": 1, "resolvedAtUtc": "2026-04-20T00:00:00Z" },
            { "requestId": "dup_realign", "residentId": "resident_alpha", "residentName": "Резидент", "sourceFactionId": "faction_alpha", "targetFactionId": "faction_beta", "status": "accepted", "realignmentMode": "accepted_transfer", "resolvedAtTurn": 2, "resolvedAtUtc": "2026-04-20T00:01:00Z" }
          ],
          "coreActionReceipts": [
            { "requestId": "dup_core", "actionType": "open_gates", "status": "accepted", "selectedCardIds": [], "newResidentIds": [], "seededProjectIds": [], "generatedDraftVersion": 0, "resolvedAtTurn": 1, "resolvedAtUtc": "2026-04-20T00:00:00Z" },
            { "requestId": "dup_core", "actionType": "open_gates", "status": "accepted", "selectedCardIds": [], "newResidentIds": [], "seededProjectIds": [], "generatedDraftVersion": 0, "resolvedAtTurn": 2, "resolvedAtUtc": "2026-04-20T00:01:00Z" }
          ],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_duplicate_receipt_request_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_founding_duplicate_receipt_request_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_realignment_duplicate_receipt_request_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_trade_duplicate_receipt_request_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_duplicate_receipt_request_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_duplicate_history_request_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_CoreReceiptWithoutResolvedMarkers_RaisesExplicitErrors()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 1 },
          "lightSparks": 40,
          "halls": [],
          "factions": [],
          "shiningPoliticalActors": [],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": [],
          "coreActionReceipts": [
            {
              "requestId": "core_stub",
              "actionType": "open_gates",
              "status": "accepted",
              "selectedCardIds": [],
              "newResidentIds": [],
              "seededProjectIds": [],
              "generatedDraftVersion": 0,
              "resolvedAtTurn": 0,
              "resolvedAtUtc": ""
            }
          ],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_receipt_missing_resolved_at_turn", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_receipt_missing_resolved_at_utc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_PreparedPackageOrderMismatch_RaisesExplicitError()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 380, "tier": 3 },
          "lightSparks": 40,
          "halls": [],
          "factions": [],
          "shiningPoliticalActors": [],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": [],
          "coreActionReceipts": [],
          "preparedIncarnationPackage": {
            "generatedFromDraftVersion": 4,
            "preparedAtTurn": 155,
            "preparedAtUtc": "2026-04-19T10:00:00Z",
            "selectedCardIds": ["card_b", "card_a"],
            "selectedCards": [
              {
                "cardId": "card_a",
                "dedupeKey": "a",
                "sourceType": "head",
                "sourceFactionId": "faction_a",
                "effectFamily": "social",
                "rarity": "Rare",
                "displayName": "Карта А",
                "displaySummary": "Первая карта.",
                "effectPayload": {}
              },
              {
                "cardId": "card_b",
                "dedupeKey": "b",
                "sourceType": "project",
                "sourceFactionId": "faction_b",
                "effectFamily": "route",
                "rarity": "Epic",
                "displayName": "Карта Б",
                "displaySummary": "Вторая карта.",
                "effectPayload": {}
              }
            ]
          },
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_prepare_package_selected_card_sequence_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_PreparePackageReceiptWithStaleSelectedCards_RaisesExplicitError()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 380, "tier": 3 },
          "lightSparks": 40,
          "halls": [],
          "factions": [],
          "shiningPoliticalActors": [],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": [],
          "coreActionReceipts": [
            {
              "requestId": "package_receipt_1",
              "actionType": "prepare_incarnation_package",
              "status": "accepted",
              "selectedCardIds": ["card_b", "card_a"],
              "selectedCards": [
                {
                  "cardId": "card_a",
                  "dedupeKey": "a",
                  "sourceType": "head",
                  "sourceFactionId": "faction_a",
                  "effectFamily": "social",
                  "rarity": "Rare",
                  "displayName": "Карта А",
                  "displaySummary": "Первая карта.",
                  "effectPayload": {}
                },
                {
                  "cardId": "card_b",
                  "dedupeKey": "b",
                  "sourceType": "project",
                  "sourceFactionId": "faction_b",
                  "effectFamily": "route",
                  "rarity": "Epic",
                  "displayName": "Карта Б",
                  "displaySummary": "Вторая карта.",
                  "effectPayload": {}
                }
              ],
              "newResidentIds": [],
              "seededProjectIds": [],
              "generatedDraftVersion": 4,
              "resolvedAtTurn": 155,
              "resolvedAtUtc": "2026-04-19T10:00:00Z"
            }
          ],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_prepare_package_receipt_selected_cards_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateShiningLeadershipHeadReferencesAsync_MissingGuardianBinding_RaisesExplicitError()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
            {
              "availability": "active",
              "radiance": { "experience": 150, "tier": 1 },
              "lightSparks": 55,
              "halls": [],
              "factions": [
                {
                  "factionId": "faction_orphan",
                  "originType": "ascended_guardian",
                  "hallId": "hall_void",
                  "charter": {
                    "factionName": "Осиротевший хор",
                    "favoredArchetype": "accord",
                    "patronEffectFamily": "social",
                    "summary": "Тест"
                  },
                  "leadership": {
                    "headActorType": "guardian",
                    "headActorId": "guardian_missing",
                    "leadershipState": "secure"
                  },
                  "baseStrength": 30,
                  "factionStrength": 30,
                  "investCountThisAscension": 0,
                  "projects": [],
                  "tradeInventoryReceipts": [],
                  "leadershipReceipts": [],
                  "leadershipHistory": []
                }
              ],
              "shiningPoliticalActors": [],
              "factionFoundingReceipts": [],
              "factionRealignmentReceipts": [],
              "coreActionReceipts": [],
              "gates": {
                "draftVersion": 0,
                "hasOpenDraft": false,
                "isStale": false,
                "allCandidateBlessingCards": [],
                "availableBlessingCards": [],
                "shownBlessingCardIds": [],
                "selectedBlessingCardIds": [],
                "nextCandidateCursor": 0,
                "rerollsRemaining": 0
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "currentReturnCycleId": "return_1",
                "gachaHistory": []
              }
            }
            """);
            await fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
            {
              "guardians": [],
              "activeGuardian": {
                "guardianId": "guardian_live",
                "canonicalName": "Азалия"
              },
              "chaosSeaNavigation": {
                "currentAbodeId": "abode_live"
              }
            }
            """);
            await fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, """
            {
              "entries": [],
              "thoughtJournal": [],
              "interactionLog": [],
              "historyLog": [],
              "transferReceipts": [],
              "interactionReceipts": [],
              "rosterReceipts": []
            }
            """);

            var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
            var issues = new List<ValidationIssue>();
            var method = typeof(ValidationService).GetMethod(
                "ValidateShiningLeadershipHeadReferencesAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);

            var task = method!.Invoke(validator, new object[] { issues }) as Task;
            Assert.NotNull(task);
            await task!;

            Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_missing_head_actor_reference", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-shining-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
