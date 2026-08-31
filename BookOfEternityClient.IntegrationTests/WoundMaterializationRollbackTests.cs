using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundMaterializationLifecycleTests
{
    public static TheoryData<string, string, string> WoundRepairRoundtripCases => new()
    {
        {
            "owner",
            "woundDecisions[0].proposal.owner",
            "wound_response_unknown_field"
        },
        {
            "severity",
            "woundDecisions[0].proposal.severity",
            "wound_severity_above_opportunity"
        },
        {
            "slot",
            "woundDecisions[0].proposal.consequenceDefinitions[0].root.slots",
            "wound_consequence_slot_budget_exceeded"
        },
        {
            "effect",
            "woundDecisions[0].proposal.consequenceDefinitions[0].definition.links",
            "wound_materialization_effect_binding_invalid"
        },
        {
            "treatment",
            "woundDecisions[0].proposal.treatment.routes[0].displayName",
            "wound_materialization_missing_field"
        },
        {
            "resource",
            "woundDecisions[0].proposal.consequenceDefinitions[0].definition.components[0].payload.resource",
            "wound_consequence_resource_bound_missing"
        },
        {
            "narration",
            "output/narrative_response.json.response",
            "wound_acquisition_narration_missing"
        }
    };

    public static TheoryData<string, string, string> WoundResponseRepairContextCases => new()
    {
        {
            "owner",
            "proposal.owner",
            "wound_response_unknown_field"
        },
        {
            "severity",
            "proposal.severity",
            "wound_severity_above_opportunity"
        },
        {
            "slot",
            "proposal.consequenceDefinitions[0].root.slots",
            "wound_consequence_slot_budget_exceeded"
        },
        {
            "effect",
            "proposal.consequenceDefinitions[0].definition.links",
            "wound_materialization_effect_binding_invalid"
        },
        {
            "treatment",
            "proposal.treatment.routes[0].displayName",
            "wound_materialization_missing_field"
        },
        {
            "narration",
            "response",
            "wound_acquisition_narration_missing"
        }
    };

    [Theory]
    [MemberData(nameof(WoundResponseRepairContextCases))]
    public async Task WoundMaterializationRollbackTests_RejectedResponseCarriesSafeRepairAuthority(
        string category,
        string issuePath,
        string issueCode)
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var rejectedProposal = CreateRepairRoundtripProposal(category);
        ApplyRejectedRepairMutation(category, rejectedProposal);
        var rejectedResponse = Response(Decision("materialize", rejectedProposal));
        if (string.Equals(category, "narration", StringComparison.Ordinal))
        {
            rejectedResponse.Response =
                "Вы вовремя отступаете от обвала и продолжаете путь без происшествий.";
        }

        var rejected = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            rejectedResponse.WoundDecisions,
            rejectedResponse.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(rejected.Success);
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(rejected.Issues));
        Assert.False(WoundRepairPacketBuilder.RequiresFailClosedRollback(
            rejected.Issues));
        Assert.Contains(packet.Issues, issue =>
            string.Equals(issue.Code, issueCode, StringComparison.Ordinal) &&
            string.Equals(issue.Path, issuePath, StringComparison.Ordinal));
        Assert.True(packet.MatchesOpportunity(authority.Opportunity));
        Assert.DoesNotContain(
            authority.Opportunity.AuthorityFingerprint,
            packet.ToJsonObject().ToJsonString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task WoundMaterializationRollbackTests_UnregisteredResourceCarriesCanonicalRepairAuthority()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var rejectedProposal = CreateRepairRoundtripProposal("resource");
        ApplyRejectedRepairMutation("resource", rejectedProposal);
        var rejectedResponse = Response(Decision("materialize", rejectedProposal));
        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            rejectedResponse.WoundDecisions,
            rejectedResponse.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));

        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            Assert.IsType<JsonObject>(composed.CommandRoot).ToJsonString());
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        var issue = Assert.Single(issues, static issue => string.Equals(
            issue.Code,
            "wound_consequence_resource_bound_missing",
            StringComparison.Ordinal));
        Assert.Equal(
            "woundDecisions[0].proposal.consequenceDefinitions[0].definition.components[0].payload.resource",
            issue.FilePath);
        Assert.NotNull(issue.WoundRepairContext);
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(issues));
        Assert.True(packet.MatchesOpportunity(authority.Opportunity));
        Assert.DoesNotContain(
            authority.Opportunity.AuthorityFingerprint,
            packet.ToJsonObject().ToJsonString(),
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(WoundRepairRoundtripCases))]
    public async Task WoundMaterializationRollbackTests_CorrectedRepairRoundtripPublishesOneAtomicResult(
        string category,
        string issuePath,
        string issueCode)
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var validProposal = CreateRepairRoundtripProposal(category);
        var rejectedProposal = validProposal.DeepClone().AsObject();
        ApplyRejectedRepairMutation(category, rejectedProposal);
        var rejectedResponse = Response(Decision("materialize", rejectedProposal));
        if (string.Equals(category, "narration", StringComparison.Ordinal))
        {
            rejectedResponse.Response =
                "Вы вовремя отступаете от обвала и продолжаете путь без происшествий.";
        }

        var rejected = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            rejectedResponse.WoundDecisions,
            rejectedResponse.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        IReadOnlyList<ValidationIssue> rejectedIssues;
        if (string.Equals(category, "resource", StringComparison.Ordinal))
        {
            Assert.True(rejected.Success, Describe(rejected.Issues));
            await context.WriteExactJsonAsync(
                AcceptedMechanicsPlan.WoundCommandPath,
                Assert.IsType<JsonObject>(rejected.CommandRoot).ToJsonString());
            rejectedIssues = await context.Validator
                .ValidateAcceptedTurnRawResourceMaterializationAsync();
        }
        else
        {
            Assert.False(rejected.Success);
            Assert.Null(rejected.CommandRoot);
            rejectedIssues = rejected.Issues;
        }

        Assert.Contains(rejectedIssues, issue =>
            string.Equals(issue.Code, issueCode, StringComparison.Ordinal) &&
            string.Equals(issue.FilePath, issuePath, StringComparison.Ordinal));
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(rejectedIssues));

        var repairAuthority = new WoundRepairPacketAuthority(
            authority.Binding.SessionId,
            authority.Binding.RequestId,
            authority.Binding.SnapshotToken,
            "generation_repair_" + category,
            authority.Opportunity.InputEvidenceFingerprint,
            Fingerprint("target-" + category),
            Fingerprint("roll-" + category));
        var repairCache = new AcceptedMechanicsPlanCache(
            AcceptedMechanicsPlanner.BuildAcceptedPlan);
        Assert.True(repairCache.TryRegisterWoundRepairWave(
            repairAuthority,
            new[] { packet }));
        Assert.True(repairCache.TryTakeWoundRepairPacket(
            repairAuthority,
            packet.CreateReceipt(),
            out var takenPacket));
        Assert.False(repairCache.HasWoundRepairWave);

        var correctedProposal = takenPacket.PreservedProposal.DeepClone().AsObject();
        ApplyRepairCorrection(category, correctedProposal, validProposal);
        Assert.True(
            JsonNode.DeepEquals(validProposal, correctedProposal),
            $"The '{category}' repair changed content outside the rejected semantic leaf.");

        var correctedDecision = Decision("materialize", correctedProposal);
        var correctedResponse = Response(correctedDecision);
        Assert.True(takenPacket.MatchesCorrectedDecision(
            correctedDecision,
            correctedResponse.Response));
        var corrected = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            correctedResponse.WoundDecisions,
            correctedResponse.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(corrected.Success, Describe(corrected.Issues));
        var plan = await PublishAsync(
            context,
            Assert.IsType<JsonObject>(corrected.CommandRoot));
        var output = GameEngine.BindAcceptedWoundOutput(
            plan,
            correctedResponse.Response!);
        Assert.True(output.Success, Describe(output.Issues));

        var wounds = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundCarrierCatalog.PlayerPath));
        Assert.Single(wounds["activeWounds"]!.AsArray());
        var effects = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.PlayerPath));
        Assert.Single(effects["activeEffects"]!.AsArray());
        var identities = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundIdentityState.StatePath));
        Assert.Single(identities["entries"]!.AsArray());
        var history = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundHistoryState.HistoryPath));
        Assert.Single(history["transitions"]!.AsArray());
        Assert.Single(output.Notifications);
        Assert.False(context.FileSystem.FileExists(
            AcceptedMechanicsPlan.WoundCommandPath));
    }

    internal static JsonObject CreateRepairRoundtripProposal(string category)
    {
        var proposal = CreatePhysicalProposal(
            severity: "II",
            includeMechanicalRoot: true);
        if (!string.Equals(category, "treatment", StringComparison.Ordinal))
            return proposal;

        proposal["treatment"]!["routes"] = new JsonArray(
            CreateRepairRoundtripTreatmentRoute());
        proposal["treatment"]!["knownRouteIds"] =
            new JsonArray("clean_and_suture");
        return proposal;
    }

    private static JsonObject CreateRepairRoundtripTreatmentRoute() => new()
    {
        ["routeId"] = "clean_and_suture",
        ["displayName"] = "Очистить и наложить швы",
        ["visibility"] = "known_to_player",
        ["mode"] = "procedure",
        ["requirements"] = new JsonArray(),
        ["resourcePolicy"] = new JsonObject
        {
            ["reserveBeforeResolution"] = true,
            ["consumeOn"] = new JsonArray("success", "partial_success"),
            ["refundOn"] = new JsonArray(
                "cancelled",
                "validation_failed",
                "rolled_back"),
            ["mutations"] = new JsonArray()
        },
        ["resolution"] = new JsonObject
        {
            ["formulaKey"] = "mortal_wound_procedure_v1",
            ["difficulty"] = 12,
            ["rollSource"] = "accepted_d20",
            ["criticalPolicy"] = "natural_20_first_natural_1_last",
            ["modifierSource"] = new JsonObject
            {
                ["kind"] = "fixed_zero"
            }
        },
        ["outcomes"] = new JsonArray(
            new JsonObject
            {
                ["bandId"] = "clean_success",
                ["minimumMargin"] = 0,
                ["maximumMargin"] = null,
                ["category"] = "success",
                ["result"] = new JsonArray(
                    new JsonObject { ["kind"] = "stabilize" })
            },
            new JsonObject
            {
                ["bandId"] = "clean_no_improvement",
                ["minimumMargin"] = null,
                ["maximumMargin"] = -1,
                ["category"] = "failed_attempt",
                ["result"] = new JsonArray(
                    new JsonObject { ["kind"] = "no_improvement" })
            }),
        ["interruption"] = null
    };

    private static void ApplyRejectedRepairMutation(
        string category,
        JsonObject proposal)
    {
        switch (category)
        {
            case "owner":
                proposal["owner"] = new JsonObject
                {
                    ["ownerId"] = "owner_rejected_secret",
                    ["carrierPath"] = WoundCarrierCatalog.PlayerPath
                };
                break;
            case "severity":
                proposal["severity"] = "III";
                break;
            case "slot":
            {
                var slots = proposal["consequenceDefinitions"]![0]!["root"]!["slots"]!
                    .AsArray();
                slots.Add(slots[0]!.DeepClone());
                slots.Add(slots[0]!.DeepClone());
                break;
            }
            case "effect":
                proposal["consequenceDefinitions"]![0]!["definition"]!["links"] =
                    new JsonArray(new JsonObject
                    {
                        ["kind"] = "wound",
                        ["targetId"] = "wound_rejected_secret",
                        ["role"] = "source"
                    });
                break;
            case "treatment":
                proposal["treatment"]!["routes"]![0]!.AsObject()
                    .Remove("displayName");
                break;
            case "resource":
                proposal["consequenceDefinitions"]![0]!["definition"]!
                    ["components"]![0]!["payload"]!["resource"] =
                    "unregistered_resource";
                break;
            case "narration":
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(category),
                    category,
                    "Unknown wound repair roundtrip category.");
        }
    }

    private static void ApplyRepairCorrection(
        string category,
        JsonObject corrected,
        JsonObject valid)
    {
        switch (category)
        {
            case "owner":
                corrected.Remove("owner");
                break;
            case "severity":
                corrected["severity"] = valid["severity"]!.DeepClone();
                break;
            case "slot":
                corrected["consequenceDefinitions"]![0]!["root"]!["slots"] =
                    valid["consequenceDefinitions"]![0]!["root"]!["slots"]!
                        .DeepClone();
                break;
            case "effect":
                corrected["consequenceDefinitions"]![0]!["definition"]!["links"] =
                    valid["consequenceDefinitions"]![0]!["definition"]!["links"]!
                        .DeepClone();
                break;
            case "treatment":
                corrected["treatment"]!["routes"]![0]!["displayName"] =
                    valid["treatment"]!["routes"]![0]!["displayName"]!.DeepClone();
                break;
            case "resource":
                corrected["consequenceDefinitions"]![0]!["definition"]!
                    ["components"]![0]!["payload"]!["resource"] =
                    valid["consequenceDefinitions"]![0]!["definition"]!
                        ["components"]![0]!["payload"]!["resource"]!.DeepClone();
                break;
            case "narration":
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(category),
                    category,
                    "Unknown wound repair roundtrip category.");
        }
    }

}

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task WoundMaterializationRollbackTests_ExactRetryPreservesUnrelatedWoundCommands()
    {
        var (binding, opportunities) = CreateWoundRepairRetryAuthorities();
        var firstProposal = WoundMaterializationLifecycleTests
            .CreateRepairRoundtripProposal("severity");
        var secondProposal = WoundMaterializationLifecycleTests
            .CreateRepairRoundtripProposal("severity");
        secondProposal["display"]!["name"] = "Ушиб правого плеча";
        secondProposal["display"]!["acquisitionNarration"] =
            "Тяжёлый камень ударяет вас в правое плечо.";

        var firstDecision = CreateWoundRepairRetryDecision(
            opportunities[0].PublicRef,
            "local_wound_retry_first",
            firstProposal);
        var secondDecision = CreateWoundRepairRetryDecision(
            opportunities[1].PublicRef,
            "local_wound_retry_second",
            secondProposal);
        var finalSceneText = string.Join(
            ' ',
            firstProposal["display"]!["acquisitionNarration"]!.GetValue<string>(),
            secondProposal["display"]!["acquisitionNarration"]!.GetValue<string>());
        var valid = WoundResponseInputComposer.Compose(
            binding,
            opportunities,
            new[] { ToWoundRepairRetryElement(firstDecision),
                ToWoundRepairRetryElement(secondDecision) },
            finalSceneText,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(valid.Success, DescribeWoundRepairRetryIssues(valid.Issues));

        var rejectedFirstDecision = firstDecision.DeepClone().AsObject();
        rejectedFirstDecision["proposal"]!["severity"] = "III";
        var rejected = WoundResponseInputComposer.Compose(
            binding,
            opportunities,
            new[] { ToWoundRepairRetryElement(rejectedFirstDecision),
                ToWoundRepairRetryElement(secondDecision) },
            finalSceneText,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.False(rejected.Success);
        var packets = WoundRepairPacketBuilder.Build(rejected.Issues);
        Assert.Single(packets);

        var rejectedRoot = Assert.IsType<JsonObject>(valid.CommandRoot);
        rejectedRoot["commands"]![0]!["decision"] =
            rejectedFirstDecision.DeepClone();
        await _fs.WriteFileAtomicAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            rejectedRoot.ToJsonString());
        var engine = CreateGameEngine();
        var obligations = await InvokePrivateTaskResultAsync(
            engine,
            "CaptureWoundRepairRetryObligationsAsync",
            packets);

        var correctedRoot = rejectedRoot.DeepClone().AsObject();
        correctedRoot["commands"]![0]!["decision"] = firstDecision.DeepClone();
        await _fs.WriteFileAtomicAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            correctedRoot.ToJsonString());
        Assert.True(await InvokePrivateAsync<bool>(
            engine,
            "HasExactWoundRepairResubmissionAsync",
            obligations));

        var unrelatedMutation = correctedRoot.DeepClone().AsObject();
        unrelatedMutation["commands"]![1]!["decision"]!["proposal"]!
            ["display"]!["name"] = "Подменённое соседнее ранение";
        await _fs.WriteFileAtomicAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            unrelatedMutation.ToJsonString());
        Assert.False(await InvokePrivateAsync<bool>(
            engine,
            "HasExactWoundRepairResubmissionAsync",
            obligations));
    }

    [Fact]
    public void WoundMaterializationRollbackTests_MissingRetryAuthorityForActionablePacketRequiresFailClosed()
    {
        var repairIssue = CreateActionableWoundRepairIssue();
        Assert.Single(WoundRepairPacketBuilder.Build(new[] { repairIssue }));
        Assert.False(GameEngine.RequiresWoundRepairFailClosed(
            new[] { repairIssue },
            rollbackAvailable: true));
        Assert.True(GameEngine.RequiresWoundRepairFailClosed(
            new[] { repairIssue },
            rollbackAvailable: false));

        var retryAuthorityIssue = new ValidationIssue(
            AcceptedMechanicsPlan.WoundCommandPath,
            IssueSeverity.Error,
            "Wound repair could not bind the rejected proposal to one exact retry authority.",
            code: "wound_repair_retry_authority_unavailable",
            actor: "Client",
            section: "wound_materialization",
            expected: "one strict rejected wound command",
            actual: "missing");
        Assert.True(GameEngine.RequiresWoundRepairFailClosed(
            new[] { repairIssue, retryAuthorityIssue },
            rollbackAvailable: true));
    }

    private static readonly string[] WoundRollbackPublicationPaths =
    [
        WoundCarrierCatalog.PlayerPath,
        WoundIdentityState.StatePath,
        EffectCarrierCatalog.PlayerPath,
        EffectAcceptedTurnPlan.IdentityIndexPath,
        ResourceMaterializationContract.StatePath,
        EffectCarrierCatalog.AfterlifeProfilesPath,
        ProgressionScheduleService.SchedulePath,
        ProgressionScheduleService.ReportPath,
        WoundHistoryState.HistoryPath,
        "output/narrative_response.json",
        "output/interface_updates.json",
        "output/debug_logs.json"
    ];

    public static TheoryData<string, string> WoundPublicationFailureBoundaries
    {
        get
        {
            var result = new TheoryData<string, string>();
            foreach (var path in WoundRollbackPublicationPaths)
            {
                result.Add("before", path);
                result.Add("after", path);
            }

            return result;
        }
    }

    [Theory]
    [MemberData(nameof(WoundPublicationFailureBoundaries))]
    public async Task WoundMaterializationRollbackTests_FailureBeforeOrAfterEveryPublicationSurfaceRestoresExactSnapshot(
        string boundary,
        string failurePath)
    {
        var probe = new WoundRollbackFailureProbe();
        await using var context = await ResourceMaterializationTestContext.CreateAsync(
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationAsync = probe.BeforeCanonicalMutationAsync,
                AfterPhysicalFilePublishedAsync = probe.AfterPhysicalFilePublishedAsync
            });
        var expected = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        for (var index = 0; index < WoundRollbackPublicationPaths.Length; index++)
        {
            var path = WoundRollbackPublicationPaths[index];
            if (index % 2 == 0)
            {
                var bytes = Encoding.UTF8.GetBytes(
                    $"{{\"baselineOrdinal\":{index},\"state\":\"accepted\"}}");
                await context.FileSystem.WriteFileAtomicBytesAsync(path, bytes);
            }
            else if (context.FileSystem.FileExists(path))
            {
                context.FileSystem.DeleteFile(path);
            }

            expected[path] = await context.FileSystem.ReadFileBytesAsync(path);
        }

        var engine = CreateGameEngine(fileSystem: context.FileSystem);
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "wound_publication_" + boundary + "_" + SanitizeWoundFailurePath(failurePath));
        probe.Arm(context.FileSystem, boundary, failurePath);

        var exception = await Record.ExceptionAsync(async () =>
        {
            for (var index = 0; index < WoundRollbackPublicationPaths.Length; index++)
            {
                var path = WoundRollbackPublicationPaths[index];
                var rejectedBytes = Encoding.UTF8.GetBytes(
                    $"{{\"rejectedOrdinal\":{index},\"state\":\"partial\"}}");
                await context.FileSystem.WriteFileAtomicBytesAsync(path, rejectedBytes);
            }
        });

        Assert.NotNull(exception);
        Assert.True(
            probe.Triggered,
            $"The '{boundary}' failure hook was not reached for '{failurePath}'.");
        probe.Disarm();
        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);

        foreach (var path in WoundRollbackPublicationPaths)
        {
            var actual = await context.FileSystem.ReadFileBytesAsync(path);
            Assert.Equal(expected[path] is not null, actual is not null);
            if (expected[path] is not null)
                Assert.Equal(expected[path], actual);
        }
    }

    private static string SanitizeWoundFailurePath(string path) =>
        new(path.Select(static character =>
            char.IsLetterOrDigit(character) ? character : '_').ToArray());

    private static (WoundAcceptedTurnBinding Binding,
        IReadOnlyList<WoundOpportunityAuthority> Opportunities)
        CreateWoundRepairRetryAuthorities()
    {
        var owner = new WoundOwnerCoordinate(
            "mortal_world",
            "player",
            "player_current",
            WoundCarrierCatalog.PlayerPath);
        var evidence = new[]
        {
            new WoundOpportunityEventEvidence(
                "formal",
                "accepted_turn",
                "turn_42_first_harm",
                "harmful",
                2,
                "Острый край рассекает левое предплечье."),
            new WoundOpportunityEventEvidence(
                "formal",
                "accepted_turn",
                "turn_42_second_harm",
                "harmful",
                2,
                "Тяжёлый камень ударяет правое плечо.")
        };
        var events = evidence.Select((value, index) =>
            new WoundAcceptedEventAuthority(
                $"event_wound_retry_{index}",
                value.AuthorityKind,
                value.AuthorityId,
                WoundOpportunityEventEvidenceFingerprint.Compute(value)))
            .ToArray();
        var binding = new WoundAcceptedTurnBinding(
            "session_wound_retry_exact",
            "request_wound_retry_exact",
            "snapshot_wound_retry_exact",
            "mortal_world",
            42,
            events,
            WoundAcceptedEventSetFingerprint.Compute(events));
        var opportunities = evidence.Select((value, index) =>
        {
            var result = WoundOpportunityAuthority.Compose(
                new WoundOpportunityBuildRequest(
                    binding,
                    $"opportunity_id_wound_retry_{index}",
                    $"opportunity_wound_retry_{index}",
                    events[index].EventRef,
                    owner,
                    "physical",
                    "mortal_formal_injury_v1",
                    "combat_action",
                    $"combat_action_wound_retry_{index}",
                    "active",
                    value,
                    HardMaximumSeverityRank: 4,
                    GuaranteedTrigger: null,
                    new WoundOpportunitySafeContext(
                        "вы",
                        index == 0
                            ? "острый край"
                            : "тяжёлый камень",
                        new[] { "anatomical", "systemic", "other" })));
            Assert.True(
                result.Success,
                DescribeWoundRepairRetryIssues(result.Issues));
            return Assert.IsType<WoundOpportunityAuthority>(result.Opportunity);
        }).ToArray();
        return (binding, opportunities);
    }

    private static JsonObject CreateWoundRepairRetryDecision(
        string opportunityRef,
        string woundRef,
        JsonObject proposal) =>
        new()
        {
            ["opportunityRef"] = opportunityRef,
            ["decision"] = "materialize",
            ["woundRef"] = woundRef,
            ["proposal"] = proposal.DeepClone()
        };

    private static JsonElement ToWoundRepairRetryElement(JsonNode value) =>
        JsonSerializer.SerializeToElement(value);

    private static string DescribeWoundRepairRetryIssues(
        IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(static issue =>
            $"{issue} code={issue.Code}; expected={issue.Expected}; actual={issue.Actual}"));

    private static ValidationIssue CreateActionableWoundRepairIssue()
    {
        var issue = new ValidationIssue(
            "woundDecisions[0].proposal.severity",
            IssueSeverity.Error,
            "Severity exceeds the sealed opportunity.",
            code: "wound_severity_above_opportunity",
            section: "wound_materialization",
            expected: "validator-internal range",
            actual: "III");
        issue.WoundRepairContext = new WoundRepairContext(
            "session_wound_retry_guard",
            "request_wound_retry_guard",
            "snapshot_wound_retry_guard",
            "construct_wound",
            "candidate_wound_retry_guard",
            WoundAcceptedTurnFingerprintWriter.Compute(
                new[] { "candidate-wound-retry-guard" }),
            "opportunity_wound_retry_guard",
            new JsonObject
            {
                ["event"] = "осколок после обвала",
                ["target"] = "игрок",
                ["realm"] = "Смертный мир"
            },
            new[] { "none", "materialize" },
            "I",
            "II",
            new JsonObject
            {
                ["opportunityRef"] = "opportunity_wound_retry_guard",
                ["decision"] = "materialize",
                ["woundRef"] = "local_wound_retry_guard",
                ["proposal"] = new JsonObject
                {
                    ["classification"] = new JsonObject(),
                    ["display"] = new JsonObject
                    {
                        ["name"] = "Рваная рана",
                        ["acquisitionNarration"] =
                            "Осколок рассекает предплечье."
                    },
                    ["severity"] = "III",
                    ["complications"] = new JsonArray(),
                    ["consequenceDefinitions"] = new JsonArray(),
                    ["treatment"] = new JsonObject(),
                    ["recovery"] = new JsonObject()
                }
            },
            WoundAcceptedTurnFingerprintWriter.Compute(
                new[] { "opportunity-wound-retry-guard" }));
        return issue;
    }

    private sealed class WoundRollbackFailureProbe
    {
        private string? _boundary;
        private string? _relativePath;
        private string? _resolvedPath;

        internal bool Triggered { get; private set; }

        internal void Arm(
            FileSystemManager fileSystem,
            string boundary,
            string relativePath)
        {
            _boundary = boundary;
            _relativePath = relativePath;
            _resolvedPath = fileSystem.ResolvePath(relativePath);
            Triggered = false;
        }

        internal void Disarm()
        {
            _boundary = null;
            _relativePath = null;
            _resolvedPath = null;
        }

        internal Task BeforeCanonicalMutationAsync(string path)
        {
            if (!string.Equals(_boundary, "before", StringComparison.Ordinal) ||
                !string.Equals(path, _relativePath, StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            Triggered = true;
            Disarm();
            return Task.FromException(new InvalidDataException(
                $"Injected failure before wound publication '{path}'."));
        }

        internal Task AfterPhysicalFilePublishedAsync(string path)
        {
            if (!string.Equals(_boundary, "after", StringComparison.Ordinal) ||
                !string.Equals(path, _resolvedPath, StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            Triggered = true;
            Disarm();
            return Task.FromException(new InvalidDataException(
                $"Injected failure after wound publication '{path}'."));
        }
    }
}
