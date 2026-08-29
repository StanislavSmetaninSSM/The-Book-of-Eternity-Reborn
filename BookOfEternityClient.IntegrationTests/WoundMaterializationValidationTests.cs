using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundMaterializationValidationTests
{
    internal static readonly string[] SnapshotWoundPaths =
        WoundAcceptedTurnSnapshotContract.RequiredPaths.ToArray();

    [Fact]
    public async Task EmptyStrictCommand_ComposesOneSealedWoundStageIntoCommonPlan()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        var snapshotToken = await ReadSnapshotTokenAsync(context);
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            EmptyCommands(
                sessionId: "session_resource_materialization",
                requestId: "request_resource_materialization",
                snapshotToken: snapshotToken).ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);

        var errors = issues
            .Where(static issue => issue.Severity == IssueSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            string.Join(
                Environment.NewLine,
                errors.Select(static issue =>
                    $"{issue.Code}: {issue.FilePath}: {issue.Actual}")));
        var hasPrepared = WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
            context.FileSystem,
            lease,
            out _);
        var hasCommon = AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            lease,
            out var binding,
            out var result);
        Assert.True(
            hasPrepared,
            "The raw wound command did not reach the prepared wound stage; " +
            $"common={hasCommon}." + Environment.NewLine +
            string.Join(Environment.NewLine, issues.Select(static issue =>
                $"{issue.Code}: {issue.FilePath}: {issue.Actual}")));
        Assert.True(
            hasCommon,
            string.Join(
                Environment.NewLine,
                issues.Select(static issue => issue.ToString())));
        Assert.True(result.Success);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
        var stages = Assert.IsType<AcceptedMechanicsWoundStageBundle>(
            plan.WoundStageBundle);
        Assert.Empty(stages.Input.Transitions);
        Assert.Empty(stages.PreparedPlan.EffectOperationBatches);
        Assert.Equal(snapshotToken, stages.Input.Binding.SnapshotToken);
        Assert.NotNull(binding.WoundCommands);
        Assert.NotNull(binding.WoundInput);
        Assert.Contains(AcceptedMechanicsPlan.WoundCommandPath, plan.ConsumedPaths);
    }

    [Fact]
    public async Task EmptyWoundStage_PreservesOrdinaryEffectEventAndPublishesEffect()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerSkillSourceAsync();
        await SeedEmptyWoundFoundationsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var snapshotToken = await ReadSnapshotTokenAsync(context);

        var effectCommand = EffectMaterializationTestFixture.CreateApplyCommand();
        effectCommand["source"] = new JsonObject
        {
            ["kind"] = "skill",
            ["sourceId"] = EffectMaterializationTestContext.MaterializableSkillId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(effectCommand));
        await context.WriteJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            EmptyCommands(
                sessionId: "session_effect_materialization",
                requestId: "request_effect_materialization",
                snapshotToken: snapshotToken));

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
        var errors = issues
            .Where(static issue => issue.Severity == IssueSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            string.Join(Environment.NewLine, errors.Select(static issue =>
                $"{issue.Code}: {issue.FilePath}: {issue.Actual}")));

        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            lease,
            out _,
            out var result));
        Assert.True(result.Success);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
        var woundStages = Assert.IsType<AcceptedMechanicsWoundStageBundle>(
            plan.WoundStageBundle);
        Assert.Empty(woundStages.Input.Transitions);
        Assert.Empty(woundStages.PreparedPlan.EffectOperationBatches);

        var effectPlan = Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan);
        var acceptedEvent = Assert.IsType<JsonObject>(
            Assert.Single(effectPlan.EventInput["events"]!.AsArray()));
        Assert.Equal("accepted_turn", acceptedEvent["kind"]!.GetValue<string>());
        Assert.Equal("turn_42", acceptedEvent["authorityId"]!.GetValue<string>());
        Assert.Equal(
            "turn_42:accepted_effect",
            acceptedEvent["eventRef"]!.GetValue<string>());
        Assert.Single(effectPlan.ActiveEffects);

        var published = await context.Normalizer.BindTo(lease)
            .NormalizeAcceptedMechanicsAsync(backups);
        Assert.NotNull(published);
        var playerEffects = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath));
        var publishedEffect = Assert.IsType<JsonObject>(
            Assert.Single(playerEffects["activeEffects"]!.AsArray()));
        Assert.Equal("skill", publishedEffect["source"]!["kind"]!.GetValue<string>());
        Assert.Equal(
            EffectMaterializationTestContext.MaterializableSkillId,
            publishedEffect["source"]!["sourceId"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(
            EffectMaterializationTestContext.CommandPath));
        Assert.Null(await context.ReadJsonAsync(AcceptedMechanicsPlan.WoundCommandPath));
    }

    [Fact]
    public async Task StrictCommand_RejectsUnknownRootFieldBeforeAnyCommonHandoff()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        var command = EmptyCommands(
            sessionId: "session_resource_materialization",
            requestId: "request_resource_materialization",
            snapshotToken: await ReadSnapshotTokenAsync(context));
        command["gmOwnedShortcut"] = true;
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            command.ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);

        Assert.Contains(issues, issue =>
            issue.FilePath == AcceptedMechanicsPlan.WoundCommandPath + ".gmOwnedShortcut" &&
            issue.Code == "wound_command_unknown_field");
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            lease,
            out _,
            out _));
    }

    [Theory]
    [InlineData("duplicate_schema", "wound_command_duplicate_field")]
    [InlineData("missing_request", "wound_command_missing_field")]
    [InlineData("wrong_snapshot", "wound_command_binding_mismatch")]
    [InlineData("unadapted_command", "wound_command_transition_adapter_unavailable")]
    public async Task StrictCommand_RejectsMalformedOrUnadaptedEnvelopeBeforePlanning(
        string mutation,
        string expectedCode)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        var command = EmptyCommands(
            sessionId: "session_resource_materialization",
            requestId: "request_resource_materialization",
            snapshotToken: await ReadSnapshotTokenAsync(context));
        var commandJson = mutation switch
        {
            "duplicate_schema" => command.ToJsonString().Replace(
                "\"schemaVersion\":1",
                "\"schemaVersion\":1,\"schemaVersion\":1",
                StringComparison.Ordinal),
            "missing_request" => RemoveCommandField(command, "requestId"),
            "wrong_snapshot" => SetCommandField(
                command,
                "snapshotToken",
                "snapshot_wrong"),
            "unadapted_command" => AddUnadaptedCommand(command),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            commandJson);

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);

        Assert.Contains(issues, issue => issue.Code == expectedCode);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            lease,
            out _,
            out _));
    }

    [Fact]
    public async Task RawValidation_RejectsChangedClientOwnedIdentityAgainstSnapshot()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        var snapshotToken = await ReadSnapshotTokenAsync(context);
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            EmptyCommands(
                sessionId: "session_resource_materialization",
                requestId: "request_resource_materialization",
                snapshotToken: snapshotToken).ToJsonString());
        var changedIdentity = EmptyIdentity();
        changedIdentity["entries"]!.AsArray().Add(
            new JsonObject
            {
                ["woundId"] = "wound_test_torn_side",
                ["realm"] = "mortal_world",
                ["ownerKind"] = "player",
                ["ownerId"] = "player_current",
                ["carrierPath"] = WoundCarrierCatalog.PlayerPath,
                ["domain"] = "physical",
                ["status"] = "active",
                ["createdAtTurn"] = 42,
                ["createdEventRef"] = "turn_42:wound_opened",
                ["lastTransitionOrdinal"] = 1,
                ["terminalTransitionId"] = null,
                ["semanticFingerprint"] =
                    "sha256:0000000000000000000000000000000000000000000000000000000000000000"
            });
        await context.WriteExactJsonAsync(
            WoundIdentityState.StatePath,
            changedIdentity.ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);

        Assert.True(
            issues.Any(issue =>
                issue.FilePath == WoundIdentityState.StatePath &&
                issue.Code == "wound_materialization_client_owned_root_mutated" &&
                issue.Category == IssueCategory.ClientOwnedSurface),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code}: {issue.FilePath}: {issue.Actual}")));
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            lease,
            out _,
            out _));
    }

    [Fact]
    public async Task RawValidation_RejectsEncodingOnlyClientOwnedMutationAgainstExactSnapshotBytes()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        var snapshotToken = await ReadSnapshotTokenAsync(context);
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            EmptyCommands(
                sessionId: "session_resource_materialization",
                requestId: "request_resource_materialization",
                snapshotToken: snapshotToken).ToJsonString());
        var identityBytes = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(WoundIdentityState.StatePath));
        await context.WriteExactBytesAsync(
            WoundIdentityState.StatePath,
            Encoding.UTF8.GetPreamble().Concat(identityBytes).ToArray());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);

        Assert.True(
            issues.Any(issue =>
                issue.FilePath == WoundIdentityState.StatePath &&
                issue.Code == "wound_materialization_client_owned_root_mutated" &&
                issue.Category == IssueCategory.ClientOwnedSurface),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code}: {issue.FilePath}: {issue.Actual}")));
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            lease,
            out _,
            out _));
    }

    [Theory]
    [InlineData(WoundCarrierCatalog.EnemiesPath)]
    [InlineData(WoundCarrierCatalog.AlliesPath)]
    [InlineData(WoundCarrierCatalog.AfterlifeProfilesPath)]
    public async Task RawValidation_RejectsLiveSharedCarrierActiveWoundsMutation(
        string carrierPath)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        var carrier = CreateSharedCarrier(carrierPath, includeWound: true);
        await context.WriteExactJsonAsync(carrierPath, carrier.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            EmptyCommands(
                sessionId: "session_resource_materialization",
                requestId: "request_resource_materialization",
                snapshotToken: await ReadSnapshotTokenAsync(context)).ToJsonString());
        ResolveFirstSharedActiveWounds(carrierPath, carrier)[0]!
            ["display"]!["description"] =
                "Текущая допустимая форма раны подменена после подписанного снимка.";
        await context.WriteExactJsonAsync(carrierPath, carrier.ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync(lease);

        Assert.Contains(issues, issue =>
            issue.FilePath == carrierPath &&
            issue.Code == "wound_materialization_client_owned_root_mutated" &&
            issue.Category == IssueCategory.ClientOwnedSurface);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            lease,
            out _,
            out _));
    }

    [Fact]
    public async Task RawValidation_AllowsOrdinarySharedCarrierFieldsToChangeWhenWoundsDoNot()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        var carrier = CreateSharedCarrier(
            WoundCarrierCatalog.EnemiesPath,
            includeWound: false);
        await context.WriteExactJsonAsync(
            WoundCarrierCatalog.EnemiesPath,
            carrier.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            EmptyCommands(
                sessionId: "session_resource_materialization",
                requestId: "request_resource_materialization",
                snapshotToken: await ReadSnapshotTokenAsync(context)).ToJsonString());
        carrier["enemiesData"]![0]!["initiative"] = 17;
        await context.WriteExactJsonAsync(
            WoundCarrierCatalog.EnemiesPath,
            carrier.ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);

        Assert.DoesNotContain(issues, issue =>
            issue.FilePath == WoundCarrierCatalog.EnemiesPath &&
            issue.Code == "wound_materialization_client_owned_root_mutated");
    }

    [Fact]
    public async Task RawValidation_RejectsConflictingLifecycleContextBeforeWoundPlanning()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        var snapshotToken = await ReadSnapshotTokenAsync(context);
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            EmptyCommands(
                sessionId: "session_resource_materialization",
                requestId: "request_resource_materialization",
                snapshotToken: snapshotToken).ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/control/validation_repair_request.json",
            new JsonObject
            {
                ["sessionId"] = "session_resource_materialization",
                ["requestId"] = "request_conflicting_repair",
                ["turnNumber"] = 43
            }.ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);

        Assert.Contains(issues, issue =>
            issue.Code == "pending_turn_snapshot_reader_context_conflict");
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            lease,
            out _,
            out _));
    }

    [Fact]
    public async Task RawValidation_RejectsDuplicateNestedPropertyInSignedWoundCarrier()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        await context.WriteExactJsonAsync(
            WoundCarrierCatalog.PlayerPath,
            """
            {"schemaVersion":1,"owner":{"realm":"mortal_world","ownerKind":"player","ownerId":"player_current","ownerId":"player_current"},"activeWounds":[]}
            """);
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            EmptyCommands(
                sessionId: "session_resource_materialization",
                requestId: "request_resource_materialization",
                snapshotToken: await ReadSnapshotTokenAsync(context)).ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);

        Assert.Contains(issues, issue =>
            issue.FilePath == WoundCarrierCatalog.PlayerPath + ".owner.ownerId" &&
            issue.Code == "wound_carrier_duplicate_property");
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            lease,
            out _,
            out _));
    }

    internal static async Task SeedEmptyFoundationsAsync(
        ResourceMaterializationTestContext context)
    {
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.WriteExactJsonAsync(
            WoundCarrierCatalog.PlayerPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["owner"] = new JsonObject
                {
                    ["realm"] = "mortal_world",
                    ["ownerKind"] = "player",
                    ["ownerId"] = "player_current"
                },
                ["activeWounds"] = new JsonArray()
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            WoundCarrierCatalog.NpcPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            WoundCarrierCatalog.EnemiesPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray()
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            WoundCarrierCatalog.AlliesPath,
            new JsonObject
            {
                ["alliesData"] = new JsonArray()
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            WoundCarrierCatalog.AfterlifeProfilesPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["profiles"] = new JsonArray()
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            WoundIdentityState.StatePath,
            EmptyIdentity().ToJsonString());
        await context.WriteExactJsonAsync(
            WoundHistoryState.HistoryPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["nextOrdinal"] = 1,
                ["transitions"] = new JsonArray()
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            MortalWoundOccurrenceState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["occurrences"] = new JsonArray()
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            MortalWoundOpportunityReceiptState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["nextOrdinal"] = 1,
                ["receipts"] = new JsonArray()
            }.ToJsonString());
    }

    private static async Task SeedEmptyWoundFoundationsAsync(
        EffectMaterializationTestContext context)
    {
        await context.WriteJsonAsync(
            WoundCarrierCatalog.PlayerPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["owner"] = new JsonObject
                {
                    ["realm"] = "mortal_world",
                    ["ownerKind"] = "player",
                    ["ownerId"] = "player_current"
                },
                ["activeWounds"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            WoundCarrierCatalog.NpcPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            WoundCarrierCatalog.EnemiesPath,
            new JsonObject { ["enemiesData"] = new JsonArray() });
        await context.WriteJsonAsync(
            WoundCarrierCatalog.AlliesPath,
            new JsonObject { ["alliesData"] = new JsonArray() });
        await context.WriteJsonAsync(
            WoundCarrierCatalog.AfterlifeProfilesPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["profiles"] = new JsonArray()
            });
        await context.WriteJsonAsync(WoundIdentityState.StatePath, EmptyIdentity());
        await context.WriteJsonAsync(
            WoundHistoryState.HistoryPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["nextOrdinal"] = 1,
                ["transitions"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            MortalWoundOccurrenceState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["occurrences"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            MortalWoundOpportunityReceiptState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["nextOrdinal"] = 1,
                ["receipts"] = new JsonArray()
            });
    }

    internal static JsonObject EmptyIdentity() => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray()
    };

    internal static JsonObject EmptyCommands(
        string sessionId,
        string requestId,
        string snapshotToken) => new()
    {
        ["schemaVersion"] = 1,
        ["sessionId"] = sessionId,
        ["requestId"] = requestId,
        ["snapshotToken"] = snapshotToken,
        ["commands"] = new JsonArray()
    };

    private static JsonObject CreateSharedCarrier(
        string path,
        bool includeWound)
    {
        if (path is WoundCarrierCatalog.EnemiesPath or WoundCarrierCatalog.AlliesPath)
        {
            var collection = path == WoundCarrierCatalog.EnemiesPath
                ? "enemiesData"
                : "alliesData";
            var wounds = includeWound
                ? new JsonArray(WoundContractTestData.CreateActiveWound(
                    woundId: "wound_shared_carrier_baseline",
                    ownerKind: "combatant",
                    ownerId: "combatant_wound_baseline",
                    carrierPath: path))
                : new JsonArray();
            return new JsonObject
            {
                [collection] = new JsonArray(new JsonObject
                {
                    ["combatantId"] = "combatant_wound_baseline",
                    ["initiative"] = 12,
                    ["activeBuffs"] = new JsonArray(),
                    ["activeDebuffs"] = new JsonArray(),
                    ["activeWounds"] = wounds
                })
            };
        }

        if (path == WoundCarrierCatalog.AfterlifeProfilesPath)
        {
            return new JsonObject
            {
                ["schemaVersion"] = 1,
                [AfterlifeEntityProfileState.ProfilesProperty] =
                    new JsonArray(new JsonObject
                    {
                        ["actorType"] = "guardian",
                        ["actorId"] = "guardian_wound_baseline",
                        ["displayName"] = "Хранитель тестовой раны",
                        ["realm"] = "Chaos Sea",
                        ["resourceOwnerBindings"] = new JsonArray(new JsonObject
                        {
                            ["realm"] = "chaos_sea",
                            ["resourceOwnerId"] = "guardian_wound_baseline",
                            ["state"] = "active"
                        }),
                        ["activeWounds"] = includeWound
                            ? new JsonArray(WoundContractTestData.CreateActiveWound(
                                woundId: "wound_shared_afterlife_baseline",
                                realm: "chaos_sea",
                                ownerKind: "guardian",
                                ownerId: "guardian_wound_baseline",
                                carrierPath: path,
                                domain: "spiritual"))
                            : new JsonArray()
                    })
            };
        }

        throw new ArgumentOutOfRangeException(nameof(path), path, null);
    }

    private static JsonArray ResolveFirstSharedActiveWounds(
        string path,
        JsonObject root) => path switch
        {
            WoundCarrierCatalog.EnemiesPath =>
                root["enemiesData"]![0]!["activeWounds"]!.AsArray(),
            WoundCarrierCatalog.AlliesPath =>
                root["alliesData"]![0]!["activeWounds"]!.AsArray(),
            WoundCarrierCatalog.AfterlifeProfilesPath =>
                root[AfterlifeEntityProfileState.ProfilesProperty]![0]!
                    ["activeWounds"]!.AsArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
        };

    private static string RemoveCommandField(JsonObject command, string field)
    {
        Assert.True(command.Remove(field));
        return command.ToJsonString();
    }

    private static string SetCommandField(
        JsonObject command,
        string field,
        JsonNode value)
    {
        command[field] = value;
        return command.ToJsonString();
    }

    private static string AddUnadaptedCommand(JsonObject command)
    {
        command["commands"]!.AsArray().Add(new JsonObject());
        return command.ToJsonString();
    }

    internal static async Task<string> ReadSnapshotTokenAsync(
        ResourceMaterializationTestContext context)
    {
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"));
        return manifest["manifestPayloadHash"]!.GetValue<string>();
    }

    private static async Task<string> ReadSnapshotTokenAsync(
        EffectMaterializationTestContext context)
    {
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"));
        return manifest["manifestPayloadHash"]!.GetValue<string>();
    }
}
