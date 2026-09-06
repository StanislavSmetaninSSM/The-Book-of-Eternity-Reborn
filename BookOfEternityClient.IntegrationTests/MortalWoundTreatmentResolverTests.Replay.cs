using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// T061/T070 restart boundary. A replay row is never hand-authored here: the only
/// durable treatment attempt comes from the common accepted-mechanics publisher.
/// </summary>
public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void Replay_ValidEmptyHistoryReturnsNotFoundBeforeAnyFreshAuthorityExists()
    {
        var history = WoundHistoryState.Parse(
            WoundContractTestData.CreateHistory().ToJsonString(),
            WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));

        var probe = ProbeTreatment(
            history,
            "operation_t061_not_found",
            "attempt_t061_not_found",
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        AssertClosedProperties(probe, new[] { "Status", "Issues", "Request", "Receipt" });
        Assert.Equal("NotFound", Convert.ToString(ReadRequiredProperty(probe, "Status")));
        Assert.Empty(AsObjects(ReadRequiredProperty(probe, "Issues")));
        Assert.Null(ReadPropertyAllowingNull(probe, "Request"));
        Assert.Null(ReadPropertyAllowingNull(probe, "Receipt"));
    }

    [Fact]
    public void Replay_PublishedCommonPlanRestartsIntoDetachedExactRequestAndReceiptWithoutNewWork()
    {
        var scenario = PrepareProcedurePublicationScenario(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_replay",
                scenario.RouteId),
            "exact replay publication");
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(flow.Resolution, "OutcomeIntents")));
        ComposeAndPublishCoordinatedTreatment(fixture, flow);
        Assert.Equal(1, fixture.ReadNpcItemCount("sterile_thread"));
        fixture.AssertItemIdentityIndexValid();

        var originalRequest = flow.Request;
        var originalRequestJson = CanonicalValue(originalRequest);

        fixture.RestartForReplay();
        var history = fixture.ReadCurrentHistory();
        var acceptedTree = CaptureResolverFixtureTree(fixture.Root);
        var coordinates = ReadRequiredProperty(originalRequest, "Coordinates");
        var probe = ProbeTreatment(
            history,
            Convert.ToString(ReadRequiredProperty(coordinates, "OperationKey"))!,
            Convert.ToString(ReadRequiredProperty(coordinates, "AttemptId"))!,
            Convert.ToString(ReadRequiredProperty(originalRequest, "RequestFingerprint"))!);
        AssertClosedProperties(probe, new[] { "Status", "Issues", "Request", "Receipt" });
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(probe, "Status")));
        Assert.Empty(AsObjects(ReadRequiredProperty(probe, "Issues")));
        var restoredRequest = ReadRequiredProperty(probe, "Request");
        var restoredReceipt = ReadRequiredProperty(probe, "Receipt");
        Assert.NotSame(originalRequest, restoredRequest);
        Assert.Equal(originalRequestJson, CanonicalValue(restoredRequest));
        AssertReceiptOwnsNoActionableOutcomeSurface(restoredReceipt.GetType());
        AssertClosedTreatmentReceipt(restoredReceipt, restoredRequest, flow.Resolution);
        Assert.NotSame(
            ReadRequiredProperty(restoredRequest, "Coordinates"),
            ReadRequiredProperty(restoredReceipt, "Coordinates"));

        var planner = RequireOutcomeResolver();
        var replay = Invoke(
            ExactStaticMethod(planner, "CreateProcedureAttempt", 4),
            new object?[] { restoredRequest, history, null, null });
        AssertClosedProperties(replay, new[]
        {
            "Disposition", "Issues", "ReplayReceipt", "Resolution"
        });
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(replay, "Disposition")));
        Assert.Empty(AsObjects(ReadRequiredProperty(replay, "Issues")));
        Assert.Null(ReadPropertyAllowingNull(replay, "Resolution"));
        Assert.Equal(
            CanonicalValue(restoredReceipt),
            CanonicalValue(ReadRequiredProperty(replay, "ReplayReceipt")));
        Assert.DoesNotContain(
            replay.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public),
            static property => property.Name.Contains("Intent", StringComparison.Ordinal) ||
                               property.Name.Contains("Claim", StringComparison.Ordinal));

        AssertResolverFixtureTreeUnchanged(fixture.Root, acceptedTree);
    }

    [Fact]
    public void Replay_ChangedFingerprintConflictsButMalformedHistoryDominatesEveryCoordinate()
    {
        var scenario = PrepareProcedurePublicationScenario(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_precedence",
                scenario.RouteId),
            "conflicting fingerprint replay publication");
        ComposeAndPublishCoordinatedTreatment(fixture, flow);
        fixture.RestartForReplay();

        var coordinates = ReadRequiredProperty(flow.Request, "Coordinates");
        var operationKey = Convert.ToString(ReadRequiredProperty(coordinates, "OperationKey"))!;
        var attemptId = Convert.ToString(ReadRequiredProperty(coordinates, "AttemptId"))!;
        const string changedFingerprint =
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
        var conflict = ProbeTreatment(
            fixture.ReadCurrentHistory(),
            operationKey,
            attemptId,
            changedFingerprint);
        Assert.Equal("Conflict", Convert.ToString(ReadRequiredProperty(conflict, "Status")));
        Assert.Null(ReadPropertyAllowingNull(conflict, "Request"));
        Assert.Null(ReadPropertyAllowingNull(conflict, "Receipt"));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(conflict, "Issues"))
            .Select(Assert.IsType<ValidationIssue>));

        var historyPath = fixture.FileSystem.ResolvePath(WoundHistoryState.HistoryPath);
        var historyRoot = JsonNode.Parse(File.ReadAllText(historyPath))!.AsObject();
        var transitions = historyRoot["transitions"]!.AsArray();
        var transitionIndex = transitions.Count - 1;
        transitions[transitionIndex]!["transitionResult"]!["receiptFingerprint"] =
            "sha256:0000000000000000000000000000000000000000000000000000000000000000";
        File.WriteAllText(historyPath, historyRoot.ToJsonString());
        var malformed = WoundHistoryState.Parse(
            File.ReadAllText(historyPath),
            WoundHistoryState.HistoryPath);
        Assert.False(malformed.IsValid);
        Assert.NotEmpty(malformed.Issues);

        var invalid = ProbeTreatment(
            malformed,
            "operation_t061_completely_different",
            "attempt_t061_completely_different",
            changedFingerprint);
        Assert.Equal("InvalidHistory", Convert.ToString(ReadRequiredProperty(invalid, "Status")));
        Assert.Null(ReadPropertyAllowingNull(invalid, "Request"));
        Assert.Null(ReadPropertyAllowingNull(invalid, "Receipt"));
        var invalidIssues = AsObjects(ReadRequiredProperty(invalid, "Issues"))
            .Select(Assert.IsType<ValidationIssue>)
            .ToArray();
        Assert.Equal(
            malformed.Issues.Select(CanonicalValue),
            invalidIssues.Select(CanonicalValue));
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("attempt")]
    [InlineData("fingerprint")]
    public void Replay_ReusedSemanticCoordinateConflictsIndependently(string axis)
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_axis",
            scenario.RouteId);
        ComposeAndPublishTreatment(fixture, flow);
        fixture.RestartForReplay();

        var coordinates = ReadRequiredProperty(flow.Request, "Coordinates");
        var operationKey = Convert.ToString(ReadRequiredProperty(coordinates, "OperationKey"))!;
        var attemptId = Convert.ToString(ReadRequiredProperty(coordinates, "AttemptId"))!;
        var requestFingerprint = Convert.ToString(ReadRequiredProperty(
            flow.Request,
            "RequestFingerprint"))!;
        var probe = ProbeTreatment(
            fixture.ReadCurrentHistory(),
            axis == "attempt" ? operationKey + "_different" : operationKey,
            axis == "operation" ? attemptId + "_different" : attemptId,
            axis == "fingerprint"
                ? "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                : requestFingerprint);

        Assert.Equal("Conflict", Convert.ToString(ReadRequiredProperty(probe, "Status")));
        Assert.Null(ReadPropertyAllowingNull(probe, "Request"));
        Assert.Null(ReadPropertyAllowingNull(probe, "Receipt"));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(probe, "Issues")));
    }

    [Fact]
    public void Replay_PublicationAndProbeExposeOnlyTheExactSixArgumentAndThreeCoordinateSurfaces()
    {
        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.WoundAcceptedTurnPlanner",
            false,
            false);
        Assert.NotNull(planner);
        var compose = ExactStaticMethod(planner!, "ComposeMortalWoundTreatmentPublication", 6);
        Assert.Equal("FileSystemManager", compose.GetParameters()[0].ParameterType.Name);
        Assert.Equal("CanonicalWriteLease", compose.GetParameters()[1].ParameterType.Name);
        Assert.Equal("GameResponse", compose.GetParameters()[2].ParameterType.Name);
        Assert.Equal("MortalWoundTreatmentAcceptedStateAuthority", compose.GetParameters()[3].ParameterType.Name);
        Assert.Equal("MortalWoundTreatmentAttemptRequest", compose.GetParameters()[4].ParameterType.Name);
        Assert.Equal("MortalWoundTreatmentResolution", compose.GetParameters()[5].ParameterType.Name);
        Assert.DoesNotContain(compose.GetParameters(), static parameter =>
            typeof(JsonNode).IsAssignableFrom(parameter.ParameterType) ||
            parameter.ParameterType.Name.Contains("History", StringComparison.Ordinal) ||
            parameter.ParameterType.Name.Contains("Receipt", StringComparison.Ordinal));
        AssertClosedResultType(compose.ReturnType, "Plan");

        var probe = ExactInstanceMethod(typeof(WoundHistoryParseResult), "ProbeTreatmentAttempt", 3);
        Assert.Equal("MortalWoundTreatmentReplayProbeResult", probe.ReturnType.Name);
        Assert.All(probe.GetParameters(), static parameter => Assert.Equal(typeof(string), parameter.ParameterType));
        Assert.Equal(
            new[] { "Status", "Issues", "Request", "Receipt" }.OrderBy(static value => value),
            probe.ReturnType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static value => value));
    }

    private static object ProbeTreatment(
        WoundHistoryParseResult history,
        string operationKey,
        string attemptId,
        string requestFingerprint) =>
        InvokeInstance(
            ExactInstanceMethod(typeof(WoundHistoryParseResult), "ProbeTreatmentAttempt", 3),
            history,
            new object?[] { operationKey, attemptId, requestFingerprint });

    private static void AssertClosedTreatmentReceipt(
        object receipt,
        object request,
        object expectedResolution)
    {
        AssertReceiptOwnsNoActionableOutcomeSurface(receipt.GetType());
        Assert.Equal(
            CanonicalValue(ReadRequiredProperty(request, "Coordinates")),
            CanonicalValue(ReadRequiredProperty(receipt, "Coordinates")));
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(request, "RequestFingerprint")),
            Convert.ToString(ReadRequiredProperty(receipt, "RequestFingerprint")));
        Assert.Equal(
            ReadRequiredProperty(ReadRequiredProperty(request, "RequirementAuthority"), "AuthorityFingerprint"),
            ReadRequiredProperty(receipt, "RequirementAuthorityFingerprint"));
        Assert.Equal(
            ReadRequiredProperty(ReadRequiredProperty(request, "ResourceAuthority"), "AuthorityFingerprint"),
            ReadRequiredProperty(receipt, "ResourceAuthorityFingerprint"));
        foreach (var property in new[]
                 {
                     "Mode", "AttemptDisposition", "ResultCategory", "SelectedOutcomeIndex", "Interruption",
                     "DeclaredResult", "ConsumptionTrigger", "CourseId", "CourseMilestoneOrdinal",
                     "CourseDisposition", "ModeEvidence", "RouteFingerprint", "ResolutionAuthorityFingerprint",
                     "RequestFingerprint", "ResultFingerprint", "RouteCompletion"
                 })
        {
            Assert.Equal(
                CanonicalValue(ReadPropertyAllowingNull(expectedResolution, property)),
                CanonicalValue(ReadPropertyAllowingNull(receipt, property)));
        }
        foreach (var property in new[]
                 {
                     "RequirementAuthorityFingerprint", "ResourceAuthorityFingerprint",
                     "ResolutionAuthorityFingerprint", "ResultFingerprint", "ReceiptFingerprint"
                 })
            AssertAuthorityFingerprint(ReadRequiredProperty(receipt, property));
    }

    private static IReadOnlyDictionary<string, byte[]> CaptureResolverFixtureTree(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path)
                .StartsWith(".boe_runtime", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);

    private static void AssertResolverFixtureTreeUnchanged(
        string root,
        IReadOnlyDictionary<string, byte[]> before)
    {
        var after = CaptureResolverFixtureTree(root);
        Assert.Equal(
            before.Keys.OrderBy(static path => path, StringComparer.Ordinal),
            after.Keys.OrderBy(static path => path, StringComparer.Ordinal));
        foreach (var pair in before)
        {
            Assert.True(after.TryGetValue(pair.Key, out var bytes), pair.Key);
            Assert.True(pair.Value.AsSpan().SequenceEqual(bytes), pair.Key);
        }
    }
}
