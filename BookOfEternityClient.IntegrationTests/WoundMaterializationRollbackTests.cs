using System.Text;
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
        Assert.False(rejected.Success);
        Assert.Null(rejected.CommandRoot);

        var semanticFingerprint = Fingerprint("repair-" + category);
        var issue = new ValidationIssue(
            issuePath,
            IssueSeverity.Error,
            "The rejected wound response violates one bounded GM-owned field.",
            code: issueCode,
            section: "wound_materialization",
            expected: "validator-internal authority evidence",
            actual: DescribeRejectedRepairValue(category),
            repairHint: "validator-internal repair implementation detail");
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(
            new WoundRepairBuildRequest(
                authority.Binding.SessionId,
                authority.Binding.RequestId,
                authority.Binding.SnapshotToken,
                new[]
                {
                    new WoundRepairCandidateInput(
                        "repair_wound",
                        "candidate_repair_" + category,
                        semanticFingerprint,
                        authority.Opportunity.PublicRef,
                        new JsonObject
                        {
                            ["event"] = "острый край во время обвала",
                            ["target"] = "игрок",
                            ["realm"] = "Смертный мир"
                        },
                        new[] { "none", "materialize" },
                        "I",
                        "II",
                        Decision("materialize", rejectedProposal),
                        new[] { issue })
                })));

        var repairAuthority = new WoundRepairPacketAuthority(
            authority.Binding.SessionId,
            authority.Binding.RequestId,
            authority.Binding.SnapshotToken,
            "generation_repair_" + category,
            authority.Opportunity.InputEvidenceFingerprint,
            Fingerprint("target-" + category),
            Fingerprint("roll-" + category));
        var repairCache = new WoundAcceptedTurnPlanCache();
        Assert.True(repairCache.TryRegisterRepairWave(
            repairAuthority,
            new[] { packet }));
        Assert.True(repairCache.TryTakeRepairPacket(
            repairAuthority,
            packet.CreateReceipt(),
            out var takenPacket));
        Assert.False(repairCache.HasRepairWave);

        var correctedProposal = takenPacket.PreservedProposal.DeepClone().AsObject();
        ApplyRepairCorrection(category, correctedProposal, validProposal);
        Assert.True(
            JsonNode.DeepEquals(validProposal, correctedProposal),
            $"The '{category}' repair changed content outside the rejected semantic leaf.");

        var correctedResponse = Response(Decision("materialize", correctedProposal));
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

    private static JsonObject CreateRepairRoundtripProposal(string category)
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
            ["refundOn"] = new JsonArray("validation_failed", "rolled_back"),
            ["mutations"] = new JsonArray()
        },
        ["resolution"] = new JsonObject
        {
            ["formulaKey"] = "mortal_wound_procedure_v1",
            ["difficulty"] = 12,
            ["rollSource"] = "accepted_d20"
        },
        ["outcomes"] = new JsonArray(),
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

    private static string DescribeRejectedRepairValue(string category) => category switch
    {
        "owner" => "forbidden owner field",
        "severity" => "III",
        "slot" => "3",
        "effect" => "missing reciprocal link",
        "treatment" => "missing",
        "resource" => "unregistered resource",
        "narration" => "missing",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };
}

public sealed partial class GameEngineTurnLifecycleTests
{
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
