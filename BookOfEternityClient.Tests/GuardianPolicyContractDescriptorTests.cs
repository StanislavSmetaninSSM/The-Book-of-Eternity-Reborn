using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Reflection;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public class GuardianPolicyContractDescriptorTests
{
    [Fact]
    public void SoulStateDescriptor_ExposesCanonicalWritePatchLifecycleAndStrictAuthorityTopLevelKeys()
    {
        var lifecycleKeys = GuardianPolicyContracts.SoulStateLifecycleTopLevelKeys.OrderBy(x => x).ToArray();
        var patchWriteKeys = GuardianPolicyContracts.SoulStatePatchWriteTopLevelKeys.OrderBy(x => x).ToArray();
        var canonicalWriteKeys = GuardianPolicyContracts.SoulStateCanonicalWriteTopLevelKeys.OrderBy(x => x).ToArray();
        var strictKeys = GuardianPolicyContracts.SoulStateStrictAuthorityTopLevelKeys.OrderBy(x => x).ToArray();
        var expectedLifecycleKeys = new[]
        {
            "afterlifeArchive",
            "afterlifeArchiveUpdates",
            "archiveActionResolutions",
            "crossIncarnationData",
            "currentIncarnation",
            "currentRealm",
            "enlightenment",
            "inkFeathers",
            "livesHistory",
            "metaStateUpdates",
            "pendingMemoryLegacy",
            "pendingShiningBlessingEffects",
            "playerFoundedGuardianId",
            "playerGuardianFoundationStatus",
            "previousSoulNames",
            "soulImprint",
            "soulName",
            "soulProgression",
            "soulRelics"
        };
        var expectedStrictKeys = expectedLifecycleKeys
            .Where(key => !string.Equals(key, "crossIncarnationData", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var expectedPatchWriteKeys = expectedStrictKeys;
        var expectedCanonicalWriteKeys = expectedStrictKeys
            .Where(key =>
                !string.Equals(key, "metaStateUpdates", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, "afterlifeArchiveUpdates", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, "archiveActionResolutions", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(expectedLifecycleKeys, lifecycleKeys);
        Assert.Equal(expectedPatchWriteKeys, patchWriteKeys);
        Assert.Equal(expectedCanonicalWriteKeys, canonicalWriteKeys);
        Assert.Equal(expectedStrictKeys, strictKeys);
    }

    [Fact]
    public void SoulStateDescriptor_CoversAllCanonicalSoulStateMappedFields()
    {
        var mappedSoulFields = FileMapping.FieldToFile
            .Where(entry => string.Equals(entry.Value, "game_state/meta/soul_state.json", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Key)
            .OrderBy(entry => entry)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "afterlifeArchiveUpdates",
                "archiveActionResolutions",
                "metaStateUpdates"
            },
            mappedSoulFields);

        Assert.All(mappedSoulFields, field => Assert.Contains(field, GuardianPolicyContracts.SoulStateLifecycleTopLevelKeys));
    }

    [Fact]
    public void SoulStateDescriptor_CrossIncarnationDataRemainsLifecycleCompatibleButPatchCanonicalAndStrictlyUnsupported()
    {
        using var crossIncarnationDataDoc = JsonDocument.Parse("""
        {
          "currentIncarnation": 3,
          "currentRealm": "Chaos Sea",
          "crossIncarnationData": {
            "legacyThreadId": "thread_alpha"
          }
        }
        """);

        Assert.Contains("crossIncarnationData", GuardianPolicyContracts.SoulStateLifecycleTopLevelKeys);
        Assert.DoesNotContain("crossIncarnationData", GuardianPolicyContracts.SoulStatePatchWriteTopLevelKeys);
        Assert.DoesNotContain("crossIncarnationData", GuardianPolicyContracts.SoulStateCanonicalWriteTopLevelKeys);
        Assert.DoesNotContain("crossIncarnationData", GuardianPolicyContracts.SoulStateStrictAuthorityTopLevelKeys);

        Assert.True(
            GuardianPolicyContracts.TryDescribeUnsupportedGuardianPolicySoulStateTopLevelKeys(
                crossIncarnationDataDoc.RootElement,
                out var failureDescription));
        Assert.Contains("crossIncarnationData", failureDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SoulStateDescriptor_TransientCommandRootsRemainLifecyclePatchEligibleAndStrictValidButCanonicalWriteUnsupported()
    {
        using var commandRootDoc = JsonDocument.Parse("""
        {
          "currentIncarnation": 3,
          "currentRealm": "Chaos Sea",
          "metaStateUpdates": {},
          "afterlifeArchiveUpdates": [],
          "archiveActionResolutions": []
        }
        """);

        Assert.Contains("metaStateUpdates", GuardianPolicyContracts.SoulStateLifecycleTopLevelKeys);
        Assert.Contains("afterlifeArchiveUpdates", GuardianPolicyContracts.SoulStateLifecycleTopLevelKeys);
        Assert.Contains("archiveActionResolutions", GuardianPolicyContracts.SoulStateLifecycleTopLevelKeys);
        Assert.Contains("metaStateUpdates", GuardianPolicyContracts.SoulStatePatchWriteTopLevelKeys);
        Assert.Contains("afterlifeArchiveUpdates", GuardianPolicyContracts.SoulStatePatchWriteTopLevelKeys);
        Assert.Contains("archiveActionResolutions", GuardianPolicyContracts.SoulStatePatchWriteTopLevelKeys);

        Assert.DoesNotContain("metaStateUpdates", GuardianPolicyContracts.SoulStateCanonicalWriteTopLevelKeys);
        Assert.DoesNotContain("afterlifeArchiveUpdates", GuardianPolicyContracts.SoulStateCanonicalWriteTopLevelKeys);
        Assert.DoesNotContain("archiveActionResolutions", GuardianPolicyContracts.SoulStateCanonicalWriteTopLevelKeys);

        Assert.True(
            GuardianPolicyContracts.TryDescribeUnsupportedCanonicalSoulStateTopLevelKeys(
                commandRootDoc.RootElement,
                out var failureDescription));
        Assert.Contains("metaStateUpdates", failureDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("afterlifeArchiveUpdates", failureDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("archiveActionResolutions", failureDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SoulStateDescriptor_TopLevelCurrentTierRemainsUnsupported()
    {
        using var currentTierDoc = JsonDocument.Parse("""
        {
          "currentIncarnation": 3,
          "currentRealm": "Chaos Sea",
          "currentTier": "Transcendent"
        }
        """);

        Assert.True(
            GuardianPolicyContracts.TryDescribeUnsupportedGuardianPolicySoulStateTopLevelKeys(
                currentTierDoc.RootElement,
                out var failureDescription));
        Assert.Contains("currentTier", failureDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BaseSessionSoulState_UsesCanonicalWriteRootContract()
    {
        AssertSoulStateFileUsesCanonicalWriteContract(
            Path.Combine(TestRepoPaths.BaseSessionRoot, "game_state", "meta", "soul_state.json"));
    }

    [Fact]
    public void SharedGuardianPolicySoulFixtures_UseCanonicalWriteRootContract()
    {
        foreach (var fixturePath in Directory.EnumerateFiles(
                     TestRepoPaths.ValidatorFixturesRoot,
                     "*soul_state.json",
                     SearchOption.AllDirectories))
        {
            var normalizedPath = fixturePath.Replace('\\', '/');
            if (!normalizedPath.Contains("/shared/", StringComparison.OrdinalIgnoreCase))
                continue;

            var fileName = Path.GetFileName(normalizedPath);
            if (!string.Equals(fileName, "current_soul_state.json", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fileName, "pre_turn_soul_state.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AssertSoulStateFileUsesCanonicalWriteContract(fixturePath);
        }
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_PreservesTransientTopLevelRootsWhenTouchedDomainsAreUnrelated()
    {
        var root = JsonNode.Parse("""
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "pendingMemoryLegacy": null,
          "crossIncarnationData": {
            "legacyThreadId": "thread_alpha"
          },
          "metaStateUpdates": {},
          "afterlifeArchiveUpdates": [],
          "archiveActionResolutions": []
        }
        """)!.AsObject();

        GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
            root,
            GuardianPolicyContracts.SoulStatePatchConflictContext.None);

        Assert.Equal("Пепельная Искра", root["soulName"]!.GetValue<string>());
        Assert.False(root.ContainsKey("crossIncarnationData"));
        Assert.True(root.ContainsKey("metaStateUpdates"));
        Assert.True(root.ContainsKey("afterlifeArchiveUpdates"));
        Assert.True(root.ContainsKey("archiveActionResolutions"));
        Assert.True(root.ContainsKey("pendingMemoryLegacy"));
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_StripsWholeInkFeatherChangesButPreservesUnrelatedMetaStateWork()
    {
        var root = JsonNode.Parse("""
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "metaStateUpdates": {
            "inkFeatherChanges": {
              "add": 2,
              "spend": 5
            },
            "soulRelicOperations": {
              "addRelic": {
                "relicId": "relic_field_conflict"
              },
              "removeRelic": {
                "relicId": "relic_remove_conflict"
              },
              "equipRelic": {
                "relicId": "relic_keep",
                "slot": "Neck"
              },
              "updateRelicField": {
                "relicId": "relic_field_conflict",
                "field": "companionManifestationStatus",
                "newValue": "pending"
              }
            },
            "memoryLegacyGrant": {
              "legacyId": "legacy_keep",
              "legacyType": "startingCharacteristicBonus",
              "sourceLifeHint": "life_001",
              "characteristic": "strength",
              "bonus": 2
            },
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": {},
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          },
          "afterlifeArchiveUpdates": [],
          "archiveActionResolutions": [],
          "soulRelics": { "equipped": [], "stored": [] },
          "inkFeathers": { "current": 5 }
        }
        """)!.AsObject();

        GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
            root,
            new GuardianPolicyContracts.SoulStatePatchConflictContext(
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.InkFeathers |
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.SoulRelics,
                unsafeToReplayAddedSoulRelicIds: new[] { "relic_field_conflict" },
                removedSoulRelicIds: new[] { "relic_remove_conflict" },
                updatedSoulRelicFieldsById: new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["relic_field_conflict"] = new[] { "companionManifestationStatus" }
                }));

        Assert.True(root.ContainsKey("metaStateUpdates"));
        var metaStateUpdates = Assert.IsType<JsonObject>(root["metaStateUpdates"]);
        Assert.False(metaStateUpdates.ContainsKey("inkFeatherChanges"));

        var soulRelicOperations = Assert.IsType<JsonObject>(metaStateUpdates["soulRelicOperations"]);
        Assert.False(soulRelicOperations.ContainsKey("addRelic"));
        Assert.False(soulRelicOperations.ContainsKey("removeRelic"));
        Assert.False(soulRelicOperations.ContainsKey("updateRelicField"));
        Assert.True(soulRelicOperations.ContainsKey("equipRelic"));
        Assert.True(metaStateUpdates.ContainsKey("memoryLegacyGrant"));
        Assert.True(metaStateUpdates.ContainsKey("lifeTransitions"));
        Assert.True(root.ContainsKey("afterlifeArchiveUpdates"));
        Assert.True(root.ContainsKey("archiveActionResolutions"));
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_StripsWholeInkFeatherChangesOnOverlappingLocalFeatherWrite()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "metaStateUpdates": {
            "inkFeatherChanges": {
              "add": 4,
              "spend": 2
            }
          },
          "inkFeathers": { "current": 7 }
        }
        """)!.AsObject();

        GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
            root,
            new GuardianPolicyContracts.SoulStatePatchConflictContext(
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.InkFeathers));

        Assert.False(root.ContainsKey("metaStateUpdates"));
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_MalformedInkFeatherChanges_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "metaStateUpdates": {
            "inkFeatherChanges": {
              "add": "5"
            }
          },
          "inkFeathers": { "current": 7 }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                new GuardianPolicyContracts.SoulStatePatchConflictContext(
                    GuardianPolicyContracts.SoulStatePatchTouchedDomains.InkFeathers)));

        Assert.Contains("metaStateUpdates.inkFeatherChanges", exception.Message, StringComparison.Ordinal);
        var metaStateUpdates = Assert.IsType<JsonObject>(root["metaStateUpdates"]);
        var inkFeatherChanges = Assert.IsType<JsonObject>(metaStateUpdates["inkFeatherChanges"]);
        Assert.Equal("5", inkFeatherChanges["add"]!.GetValue<string>());
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_MalformedEnlightenmentProgression_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "metaStateUpdates": {
            "enlightenmentProgression": {
              "foo": 1
            }
          }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                GuardianPolicyContracts.SoulStatePatchConflictContext.None));

        Assert.Contains("metaStateUpdates.enlightenmentProgression", exception.Message, StringComparison.Ordinal);
        var metaStateUpdates = Assert.IsType<JsonObject>(root["metaStateUpdates"]);
        Assert.True(metaStateUpdates.ContainsKey("enlightenmentProgression"));
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_PrunesExactRelicFieldUpdateButPreservesOtherFieldsAndRelics()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "metaStateUpdates": {
            "soulRelicOperations": {
              "addRelic": {
                "relicId": "relic_keep"
              },
              "updateRelicField": {
                "relicId": "relic_manifested",
                "field": "companionManifestationStatus",
                "newValue": "pending"
              }
            }
          },
          "soulRelics": { "equipped": [], "stored": [] }
        }
        """)!.AsObject();

        GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
            root,
            new GuardianPolicyContracts.SoulStatePatchConflictContext(
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.SoulRelics,
                updatedSoulRelicFieldsById: new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["relic_manifested"] = new[] { "companionManifestationStatus" }
                }));

        var metaStateUpdates = Assert.IsType<JsonObject>(root["metaStateUpdates"]);
        var soulRelicOperations = Assert.IsType<JsonObject>(metaStateUpdates["soulRelicOperations"]);
        Assert.True(soulRelicOperations.ContainsKey("addRelic"));
        Assert.False(soulRelicOperations.ContainsKey("updateRelicField"));
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_MalformedSoulRelicOperations_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "metaStateUpdates": {
            "soulRelicOperations": {
              "unknownRelicOp": {
                "relicId": "relic_keep"
              }
            }
          },
          "soulRelics": { "equipped": [], "stored": [] }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                new GuardianPolicyContracts.SoulStatePatchConflictContext(
                    GuardianPolicyContracts.SoulStatePatchTouchedDomains.SoulRelics)));

        Assert.Contains("metaStateUpdates.soulRelicOperations", exception.Message, StringComparison.Ordinal);
        var metaStateUpdates = Assert.IsType<JsonObject>(root["metaStateUpdates"]);
        var soulRelicOperations = Assert.IsType<JsonObject>(metaStateUpdates["soulRelicOperations"]);
        Assert.True(soulRelicOperations.ContainsKey("unknownRelicOp"));
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_MalformedCanonicalInkFeathersRoot_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "inkFeathers": {
            "current": 7,
            "foo": 99
          }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                GuardianPolicyContracts.SoulStatePatchConflictContext.None));

        Assert.Contains("current inkFeathers", exception.Message, StringComparison.Ordinal);
        var inkFeathers = Assert.IsType<JsonObject>(root["inkFeathers"]);
        Assert.Equal(99, inkFeathers["foo"]!.GetValue<int>());
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_MalformedCanonicalSoulRelicsRoot_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "soulRelics": {
            "equipped": [
              {}
            ],
            "stored": []
          }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                GuardianPolicyContracts.SoulStatePatchConflictContext.None));

        Assert.Contains("current soulRelics", exception.Message, StringComparison.Ordinal);
        var equipped = root["soulRelics"]!["equipped"]!.AsArray();
        Assert.Single(equipped);
        Assert.Empty(equipped[0]!.AsObject());
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_CompanionEchoSoulRelicMissingCanonicalCompanionSeed_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_echo_alpha",
                "name": "Эхо Альфы",
                "rarity": "Rare",
                "relicType": "companion_echo"
              }
            ]
          }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                GuardianPolicyContracts.SoulStatePatchConflictContext.None));

        Assert.Contains("current soulRelics", exception.Message, StringComparison.Ordinal);
        var stored = root["soulRelics"]!["stored"]!.AsArray();
        Assert.Single(stored);
        Assert.False(stored[0]!.AsObject().ContainsKey("companionSeed"));
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_MalformedTopLevelMetaStateUpdates_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "metaStateUpdates": [],
          "inkFeathers": { "current": 7 }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                new GuardianPolicyContracts.SoulStatePatchConflictContext(
                    GuardianPolicyContracts.SoulStatePatchTouchedDomains.InkFeathers)));

        Assert.Contains("current metaStateUpdates", exception.Message, StringComparison.Ordinal);
        Assert.True(root.ContainsKey("metaStateUpdates"));
        Assert.IsType<JsonArray>(root["metaStateUpdates"]);
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_UnknownMetaStateUpdatesCommand_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "metaStateUpdates": {
            "unknownCommand": {
              "value": 1
            }
          },
          "inkFeathers": { "current": 7 }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                GuardianPolicyContracts.SoulStatePatchConflictContext.None));

        Assert.Contains("unknownCommand", exception.Message, StringComparison.Ordinal);
        var metaStateUpdates = Assert.IsType<JsonObject>(root["metaStateUpdates"]);
        Assert.True(metaStateUpdates.ContainsKey("unknownCommand"));
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_MalformedAfterlifeArchiveUpdatesRoot_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "afterlifeArchiveUpdates": {},
          "afterlifeArchive": { "stored": [] }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                new GuardianPolicyContracts.SoulStatePatchConflictContext(
                    GuardianPolicyContracts.SoulStatePatchTouchedDomains.AfterlifeArchive,
                    affectedArchiveIds: new[] { "archive_001" })));

        Assert.Contains("current afterlifeArchiveUpdates", exception.Message, StringComparison.Ordinal);
        Assert.True(root.ContainsKey("afterlifeArchiveUpdates"));
        Assert.IsType<JsonObject>(root["afterlifeArchiveUpdates"]);
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_MalformedArchiveActionResolutionsRoot_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "archiveActionResolutions": {},
          "afterlifeArchive": { "stored": [] }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                new GuardianPolicyContracts.SoulStatePatchConflictContext(
                    GuardianPolicyContracts.SoulStatePatchTouchedDomains.AfterlifeArchive,
                    affectedArchiveRequestIds: new[] { "request_001" })));

        Assert.Contains("current archiveActionResolutions", exception.Message, StringComparison.Ordinal);
        Assert.True(root.ContainsKey("archiveActionResolutions"));
        Assert.IsType<JsonObject>(root["archiveActionResolutions"]);
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_NullAfterlifeArchiveUpdatesRoot_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "afterlifeArchiveUpdates": null,
          "afterlifeArchive": { "stored": [] }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                GuardianPolicyContracts.SoulStatePatchConflictContext.None));

        Assert.Contains("current afterlifeArchiveUpdates", exception.Message, StringComparison.Ordinal);
        Assert.True(root.ContainsKey("afterlifeArchiveUpdates"));
        Assert.Null(root["afterlifeArchiveUpdates"]);
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_NullArchiveActionResolutionsRoot_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "archiveActionResolutions": null,
          "afterlifeArchive": { "stored": [] }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                GuardianPolicyContracts.SoulStatePatchConflictContext.None));

        Assert.Contains("current archiveActionResolutions", exception.Message, StringComparison.Ordinal);
        Assert.True(root.ContainsKey("archiveActionResolutions"));
        Assert.Null(root["archiveActionResolutions"]);
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_MalformedAfterlifeArchiveUpdateItem_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "afterlifeArchiveUpdates": [
            {
              "command": "unknown",
              "archiveId": "archive_001"
            }
          ],
          "afterlifeArchive": { "stored": [] }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                new GuardianPolicyContracts.SoulStatePatchConflictContext(
                    GuardianPolicyContracts.SoulStatePatchTouchedDomains.AfterlifeArchive,
                    affectedArchiveIds: new[] { "archive_001" })));

        Assert.Contains("afterlifeArchiveUpdates", exception.Message, StringComparison.Ordinal);
        Assert.True(root.ContainsKey("afterlifeArchiveUpdates"));
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_MalformedArchiveActionResolutionItem_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "archiveActionResolutions": [
            {
              "requestId": "request_001",
              "archiveId": "archive_001",
              "requestedMode": "consultation"
            }
          ],
          "afterlifeArchive": { "stored": [] }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                new GuardianPolicyContracts.SoulStatePatchConflictContext(
                    GuardianPolicyContracts.SoulStatePatchTouchedDomains.AfterlifeArchive,
                    affectedArchiveRequestIds: new[] { "request_001" })));

        Assert.Contains("archiveActionResolutions", exception.Message, StringComparison.Ordinal);
        Assert.True(root.ContainsKey("archiveActionResolutions"));
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_MalformedLifeTransitions_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": {},
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": []
              }
            }
          }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                GuardianPolicyContracts.SoulStatePatchConflictContext.None));

        Assert.Contains("metaStateUpdates.lifeTransitions", exception.Message, StringComparison.Ordinal);
        var metaStateUpdates = Assert.IsType<JsonObject>(root["metaStateUpdates"]);
        Assert.True(metaStateUpdates.ContainsKey("lifeTransitions"));
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_MalformedMemoryLegacyGrant_FailClosed()
    {
        var root = JsonNode.Parse("""
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "metaStateUpdates": {
            "memoryLegacyGrant": {
              "legacyId": "legacy_alpha",
              "legacyType": "startingCharacteristicBonus",
              "sourceLifeHint": "life_001",
              "characteristic": "strength",
              "bonus": 1
            }
          }
        }
        """)!.AsObject();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
                root,
                GuardianPolicyContracts.SoulStatePatchConflictContext.None));

        Assert.Contains("metaStateUpdates.memoryLegacyGrant", exception.Message, StringComparison.Ordinal);
        var metaStateUpdates = Assert.IsType<JsonObject>(root["metaStateUpdates"]);
        Assert.True(metaStateUpdates.ContainsKey("memoryLegacyGrant"));
    }

    [Fact]
    public void SoulStatePatchWriteSanitizer_PrunesOnlyConflictingArchiveTransientEntries()
    {
        var root = JsonNode.Parse("""
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "metaStateUpdates": {},
          "afterlifeArchiveUpdates": [
            {
              "command": "remove",
              "archiveId": "archive_conflict"
            },
            {
              "command": "remove",
              "archiveId": "archive_keep"
            }
          ],
          "archiveActionResolutions": [
            {
              "requestId": "request_conflict",
              "archiveId": "archive_conflict",
              "requestedMode": "consultation",
              "status": "cancelled"
            },
            {
              "requestId": "request_keep",
              "archiveId": "archive_keep",
              "requestedMode": "project_fuel",
              "status": "rejected"
            }
          ],
          "afterlifeArchive": { "stored": [] }
        }
        """)!.AsObject();

        GuardianPolicyContracts.SanitizeSoulStateForPatchWrite(
            root,
            new GuardianPolicyContracts.SoulStatePatchConflictContext(
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.AfterlifeArchive,
                affectedArchiveIds: new[] { "archive_conflict" },
                affectedArchiveRequestIds: new[] { "request_conflict" }));

        Assert.True(root.ContainsKey("metaStateUpdates"));
        var archiveUpdates = Assert.IsType<JsonArray>(root["afterlifeArchiveUpdates"]);
        Assert.Single(archiveUpdates);
        Assert.Equal("archive_keep", archiveUpdates[0]!["archiveId"]!.GetValue<string>());

        var archiveResolutions = Assert.IsType<JsonArray>(root["archiveActionResolutions"]);
        Assert.Single(archiveResolutions);
        Assert.Equal("request_keep", archiveResolutions[0]!["requestId"]!.GetValue<string>());
        Assert.Equal("archive_keep", archiveResolutions[0]!["archiveId"]!.GetValue<string>());
    }

    [Fact]
    public void SoulStateCanonicalWriteSanitizer_StripsLegacyAndTransientTopLevelRoots()
    {
        var root = JsonNode.Parse("""
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "pendingMemoryLegacy": null,
          "crossIncarnationData": {
            "legacyThreadId": "thread_alpha"
          },
          "metaStateUpdates": {},
          "afterlifeArchiveUpdates": [],
          "archiveActionResolutions": []
        }
        """)!.AsObject();

        GuardianPolicyContracts.SanitizeSoulStateForCanonicalWrite(root);

        Assert.Equal("Пепельная Искра", root["soulName"]!.GetValue<string>());
        Assert.False(root.ContainsKey("crossIncarnationData"));
        Assert.False(root.ContainsKey("metaStateUpdates"));
        Assert.False(root.ContainsKey("afterlifeArchiveUpdates"));
        Assert.False(root.ContainsKey("archiveActionResolutions"));
        Assert.True(root.ContainsKey("pendingMemoryLegacy"));
    }

    [Fact]
    public void SoulStatePatchWriters_MustUsePatchedWriteRootHelper()
    {
        var patchWriterPaths = new[]
        {
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "SoulIdentityService.cs"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "AfterlifeArchiveActionState.cs"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "AfterlifeArchiveConsultationService.cs"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "AfterlifeArchiveProjectFuelService.cs"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "AfterlifeArchiveCandidateService.cs"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "GuardianAbodeResidentRequestState.cs"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "GuardianTradeService.cs")
        };

        foreach (var path in patchWriterPaths)
        {
            var source = File.ReadAllText(path);
            Assert.Contains("CreatePatchedSoulStateWriteRoot", source, StringComparison.Ordinal);
            Assert.Contains("SoulStatePatchConflictContext", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SoulStateWriters_MustNotPerformRawDirectWritesWithoutSharedHelper()
    {
        var rawWriteMatches = Directory
            .EnumerateFiles(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient"), "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return Regex.IsMatch(
                    source,
                    "WriteFileAtomicAsync\\s*\\(\\s*(?:SoulStatePath|\"game_state/meta/soul_state\\\\.json\")\\s*,",
                    RegexOptions.Singleline);
            })
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);
                var matches = Regex.Matches(
                    source,
                    "WriteFileAtomicAsync\\s*\\(\\s*(?:SoulStatePath|\"game_state/meta/soul_state\\\\.json\")\\s*,",
                    RegexOptions.Singleline);
                return matches
                    .Select(match => new
                    {
                        Path = path,
                        Snippet = source.Substring(match.Index, Math.Min(600, source.Length - match.Index))
                    })
                    .Where(match =>
                        !match.Snippet.Contains("CreatePatchedSoulStateWriteRoot", StringComparison.Ordinal) &&
                        !match.Snippet.Contains("CreateCanonicalSoulStateWriteRoot", StringComparison.Ordinal))
                    .Select(match => match.Path);
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(rawWriteMatches);
    }

    [Fact]
    public void NpcCoreDescriptor_SeparatesLifecycleSectionsFromCanonicalAndLegacySections()
    {
        var lifecycleKeys = GuardianPolicyContracts.NpcCoreLifecycleTopLevelSections.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var canonicalSections = GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var carrierSections = GuardianPolicyContracts.ManifestedCompanionNpcCarrierSections.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var legacyAliasSections = GuardianPolicyContracts.NpcCoreLegacyAliasSections.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

        Assert.Equal(
            new[]
            {
                "NPCsInScene",
                "NPCsRenameData",
                "UpdateNPCs",
                "UpdateNpcTradeInventoryReceipts"
            },
            lifecycleKeys);
        Assert.Equal(
            new[]
            {
                "NPCsInScene",
                "UpdateNPCs"
            },
            canonicalSections);
        Assert.Equal(
            new[]
            {
                "NPCsInScene",
                "UpdateNPCs"
            },
            carrierSections);
        Assert.Equal(
            new[]
            {
                "NPCsRenameData",
                "UpdateNpcTradeInventoryReceipts"
            },
            GuardianPolicyContracts.NpcCoreLifecycleNonCarrierTopLevelSections.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
        Assert.Equal(
            new[]
            {
                "npcDataChanges",
                "NPCs",
                "npcs"
            },
            legacyAliasSections);
    }

    [Fact]
    public void NpcCoreDescriptor_CoversAllMappedNpcCoreFieldsAndKeepsReceiptUpdatesNonCarrier()
    {
        var mappedNpcCoreFields = FileMapping.FieldToFile
            .Where(entry => string.Equals(entry.Value, "game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Key)
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "NPCsInScene",
                "NPCsRenameData",
                "UpdateNPCs",
                "UpdateNpcTradeInventoryReceipts"
            },
            mappedNpcCoreFields);

        Assert.All(mappedNpcCoreFields, field => Assert.Contains(field, GuardianPolicyContracts.NpcCoreLifecycleTopLevelSections));
        Assert.DoesNotContain(NpcTradeRequestState.UpdateReceiptsProperty, GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections);
        Assert.DoesNotContain(NpcTradeRequestState.UpdateReceiptsProperty, GuardianPolicyContracts.ManifestedCompanionNpcCarrierSections);
    }

    [Fact]
    public void NpcCoreDescriptor_StaysInSyncWithNpcWorldValidationRegistration()
    {
        var structuredSingleActorSections = ReadPrivateStringSet(typeof(ValidationService), "NpcStructuredSingleActorSections");
        var structuredSpecialSections = ReadPrivateStringSet(typeof(ValidationService), "NpcStructuredSpecialSections");

        var registeredNpcCoreSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            GuardianPolicyContracts.NpcCoreSceneSectionName
        };
        registeredNpcCoreSections.UnionWith(structuredSingleActorSections.Where(GuardianPolicyContracts.NpcCoreLifecycleTopLevelSections.Contains));
        registeredNpcCoreSections.UnionWith(structuredSpecialSections.Where(GuardianPolicyContracts.NpcCoreLifecycleTopLevelSections.Contains));

        Assert.Equal(
            GuardianPolicyContracts.NpcCoreLifecycleTopLevelSections.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            registeredNpcCoreSections.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    [Fact]
    public void CanonicalNpcObjectEnumerator_IgnoresLegacyAliasSections()
    {
        using var doc = JsonDocument.Parse("""
        {
          "UpdateNPCs": [
            { "npcId": "npc_update" }
          ],
          "NPCsInScene": [
            { "npcId": "npc_scene" }
          ],
          "NPCs": [
            { "npcId": "npc_alias_upper" }
          ],
          "npcs": [
            { "npcId": "npc_alias_lower" }
          ],
          "npcDataChanges": [
            { "npcId": "npc_alias_changes" }
          ]
        }
        """);

        var npcIds = GuardianPolicyContracts.EnumerateCanonicalNpcObjects(doc.RootElement)
            .Select(npc => npc.GetProperty("npcId").GetString())
            .ToArray();

        Assert.Equal(new[] { "npc_update", "npc_scene" }, npcIds);
    }

    [Fact]
    public void ManifestedCompanionNpcProbe_IgnoresRenameAndAliasSectionsEvenWhenMalformed()
    {
        const string renameOnlyJson = """
        {
          "NPCsRenameData": [
            {
              "oldName": "Странник",
              "newName": "Переименованный странник",
              "sourceAfterlifeResidentId": "resident_alpha"
            }
          ]
        }
        """;
        const string aliasOnlyJson = """
        {
          "NPCs": [
            {
              "npcId": "npc_companion_alpha",
              "sourceAfterlifeResidentId": "resident_alpha",
              "sourceCompanionRelicId": "relic_alpha"
            }
          ]
        }
        """;
        const string malformedRenameJson = """
        {
          "UpdateNPCs": [],
          "NPCsRenameData": [
            {
              "oldName": "Странник",
              "newName": "Переименованный странник",
              "sourceAfterlifeResidentId": "resident_alpha"
            }
          ],
          "broken":
        """;
        const string malformedAliasJson = """
        {
          "UpdateNPCs": [],
          "NPCs": [
            {
              "npcId": "npc_companion_alpha",
              "sourceAfterlifeResidentId": "resident_alpha",
              "sourceCompanionRelicId": "relic_alpha"
            }
          ],
          "broken":
        """;
        const string carrierJson = """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_companion_alpha",
              "sourceAfterlifeResidentId": "resident_alpha",
              "sourceCompanionRelicId": "relic_alpha"
            }
          ]
        }
        """;
        var longPadding = new string('x', 768);
        var malformedCarrierJson = $$"""
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_companion_alpha",
              "padding": "{{longPadding}}",
              "sourceAfterlifeResidentId": "resident_alpha",
              "sourceCompanionRelicId": "relic_alpha"
            }
          ],
          "broken":
        """;
        const string malformedCarrierFollowedByRenameDependencyJson = """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_ordinary",
              "name": "Обычный прохожий"
            ,
          "NPCsRenameData": [
            {
              "oldName": "Обычный прохожий",
              "newName": "Переименованный прохожий",
              "sourceAfterlifeResidentId": "resident_alpha",
              "sourceCompanionRelicId": "relic_alpha"
            }
          ]
        }
        """;
        const string malformedCarrierFollowedByAliasDependencyJson = """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_ordinary",
              "name": "Обычный прохожий"
            ,
          "NPCs": [
            {
              "npcId": "npc_companion_alpha",
              "sourceAfterlifeResidentId": "resident_alpha",
              "sourceCompanionRelicId": "relic_alpha"
            }
          ]
        }
        """;
        const string malformedCarrierStringLiteralMentionJson = """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_ordinary",
              "name": "Обычный прохожий",
              "notes": "В тексте упомянут sourceAfterlifeResidentId, но это не object key."
            }
          ],
          "broken":
        """;

        Assert.False(GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(renameOnlyJson));
        Assert.False(GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(aliasOnlyJson));
        Assert.False(GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(malformedRenameJson));
        Assert.False(GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(malformedAliasJson));
        Assert.False(GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(malformedCarrierFollowedByRenameDependencyJson));
        Assert.False(GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(malformedCarrierFollowedByAliasDependencyJson));
        Assert.False(GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(malformedCarrierStringLiteralMentionJson));
        Assert.True(GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(carrierJson));
        Assert.True(GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(malformedCarrierJson));
    }

    private static HashSet<string> ReadPrivateStringSet(Type declaringType, string fieldName)
    {
        var field = declaringType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field!.GetValue(null) as IEnumerable<string>;
        Assert.NotNull(value);

        return new HashSet<string>(value!, StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertSoulStateFileUsesCanonicalWriteContract(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        Assert.False(
            GuardianPolicyContracts.TryDescribeUnsupportedCanonicalSoulStateTopLevelKeys(
                doc.RootElement,
                out var failureDescription),
            $"{path} drifted from canonical soul_state write contract: {failureDescription}");
    }
}
