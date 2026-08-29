using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundAcceptedEventAuthorityComposerTests
{
    [Fact]
    public void Api_IsOneClosedTypedTwoArgumentCompositionSurface()
    {
        var type = RequiredType("WoundAcceptedEventAuthorityComposer");
        var compose = Assert.Single(
            type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name == "Compose");
        Assert.Collection(
            compose.GetParameters(),
            parameter => Assert.Equal(
                RequiredType("WoundAcceptedResponseEventProjection"),
                parameter.ParameterType),
            parameter => Assert.Equal(
                typeof(IReadOnlyList<WoundSelectedEventEvidence>),
                parameter.ParameterType));
        Assert.Equal(
            RequiredType("WoundAcceptedEventAuthorityCompositionResult"),
            compose.ReturnType);
        Assert.Equal(
            new[] { "Events", "EventsFingerprint", "Issues", "Success" },
            compose.ReturnType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .OrderBy(name => name));
        Assert.DoesNotContain(
            compose.GetParameters(),
            parameter => parameter.ParameterType.Name.Contains("Json", StringComparison.Ordinal));
        Assert.DoesNotContain(
            type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name.Contains("File", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("Write", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("Lease", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Compose_PreservesCompleteOrderAndDerivesSelectedSemanticsByOrdinal()
    {
        var projection = Projection(
            Event("event_72_0", "accepted_turn", "turn_72"),
            Event("event_72_1", "formal_retrauma", "source_72_1"),
            Event("event_72_2", "accepted_turn", "turn_72_effect_3"));
        var evidence = Evidence("formal", "formal_retrauma", "source_72_1");

        var result = WoundAcceptedEventAuthorityComposer.Compose(
            projection,
            new[] { new WoundSelectedEventEvidence(1, evidence) });

        Assert.True(result.Success, Describe(result.Issues));
        Assert.Empty(result.Issues);
        Assert.Equal(
            new[] { "event_72_0", "event_72_1", "event_72_2" },
            result.Events.Select(value => value.EventRef));
        Assert.Equal(
            WoundOpportunityEventEvidenceFingerprint.Compute(evidence),
            result.Events[1].SemanticFingerprint);
        Assert.Matches("^sha256:[0-9a-f]{64}$", result.Events[0].SemanticFingerprint);
        Assert.Matches("^sha256:[0-9a-f]{64}$", result.Events[2].SemanticFingerprint);
        Assert.Equal(
            WoundAcceptedEventSetFingerprint.Compute(result.Events),
            result.EventsFingerprint);

        var replay = WoundAcceptedEventAuthorityComposer.Compose(
            Projection(projection.Events.ToArray()),
            new[] { new WoundSelectedEventEvidence(1, evidence with { }) });
        Assert.True(replay.Success, Describe(replay.Issues));
        Assert.Equal(result.Events, replay.Events);
        Assert.Equal(result.EventsFingerprint, replay.EventsFingerprint);
    }

    [Fact]
    public void Compose_GenericSemanticFingerprintMatchesTheExistingProductionVector()
    {
        var projection = Projection(Event("event_72_0", "accepted_turn", "turn_72"));

        var result = WoundAcceptedEventAuthorityComposer.Compose(
            projection,
            Array.Empty<WoundSelectedEventEvidence>());

        Assert.True(result.Success, Describe(result.Issues));
        const string rawProductionRow =
            "{\"kind\":\"accepted_turn\",\"authorityId\":\"turn_72\",\"eventRef\":\"event_72_0\"}";
        var expectedPayload =
            "session_72\nrequest_72\n" + Fingerprint('b') + "\n" + rawProductionRow;
        var expected = "sha256:" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(
                "accepted-wound-event-authority-v1\0" + expectedPayload)))
            .ToLowerInvariant();
        Assert.Equal(expected, Assert.Single(result.Events).SemanticFingerprint);
    }

    [Fact]
    public void Compose_GenericSemanticsBindProjectionCoordinatesAndOrder()
    {
        var baseline = WoundAcceptedEventAuthorityComposer.Compose(
            Projection(Event("event_74_0", "accepted_turn", "turn_74")),
            Array.Empty<WoundSelectedEventEvidence>());
        Assert.True(baseline.Success, Describe(baseline.Issues));
        var baselineFingerprint = Assert.Single(baseline.Events).SemanticFingerprint;

        foreach (var changedProjection in new[]
                 {
                     ProjectionWith("session_changed", "request_72", Fingerprint('b'), 72,
                         Event("event_74_0", "accepted_turn", "turn_74")),
                     ProjectionWith("session_72", "request_changed", Fingerprint('b'), 72,
                         Event("event_74_0", "accepted_turn", "turn_74")),
                     ProjectionWith("session_72", "request_72", Fingerprint('c'), 72,
                         Event("event_74_0", "accepted_turn", "turn_74")),
                     Projection(Event("event_74_changed", "accepted_turn", "turn_74")),
                     Projection(Event("event_74_0", "changed_kind", "turn_74")),
                     Projection(Event("event_74_0", "accepted_turn", "changed_authority"))
                 })
        {
            var changed = WoundAcceptedEventAuthorityComposer.Compose(
                changedProjection,
                Array.Empty<WoundSelectedEventEvidence>());
            Assert.True(changed.Success, Describe(changed.Issues));
            Assert.NotEqual(
                baselineFingerprint,
                Assert.Single(changed.Events).SemanticFingerprint);
        }

        var original = WoundAcceptedEventAuthorityComposer.Compose(
            Projection(
                Event("event_74_0", "accepted_turn", "turn_74_0"),
                Event("event_74_1", "accepted_turn", "turn_74_1")),
            Array.Empty<WoundSelectedEventEvidence>());
        var reordered = WoundAcceptedEventAuthorityComposer.Compose(
            Projection(
                Event("event_74_1", "accepted_turn", "turn_74_1"),
                Event("event_74_0", "accepted_turn", "turn_74_0")),
            Array.Empty<WoundSelectedEventEvidence>());
        Assert.True(original.Success, Describe(original.Issues));
        Assert.True(reordered.Success, Describe(reordered.Issues));
        Assert.NotEqual(original.EventsFingerprint, reordered.EventsFingerprint);
    }

    [Fact]
    public void Compose_RejectsMalformedLimitsAndExactOrConfusableAuthorityCollisions()
    {
        foreach (var projection in new[]
                 {
                     Projection(),
                     Projection((WoundAcceptedResponseEventCoordinate)null!),
                     Projection(Event(" event_1", "accepted_turn", "turn_1")),
                     Projection(Event("event_1", "accepted_turn", "turn_1"),
                         Event("event_1", "accepted_turn", "turn_2")),
                     Projection(Event("event_o", "accepted_turn", "turn_1"),
                         Event("event_о", "accepted_turn", "turn_2")),
                     Projection(Event("event_1", "combat", "authority_1"),
                         Event("event_2", "combat", "authority_1")),
                     Projection(Event("event_1", "combat_o", "authority_1"),
                         Event("event_2", "combat_о", "authority_1"))
                 })
        {
            AssertInvalid(WoundAcceptedEventAuthorityComposer.Compose(
                projection,
                Array.Empty<WoundSelectedEventEvidence>()));
        }

        var overLimit = Projection(Enumerable.Range(0, 161)
            .Select(index => Event($"event_{index}", "accepted_turn", $"turn_{index}"))
            .ToArray());
        AssertInvalid(WoundAcceptedEventAuthorityComposer.Compose(
            overLimit,
            Array.Empty<WoundSelectedEventEvidence>()));
    }

    [Fact]
    public void Compose_RejectsOutOfRangeDuplicateMismatchedOrHarmlessSelections()
    {
        var projection = Projection(Event("event_76", "combat", "combat_76"));
        foreach (var selected in new IReadOnlyList<WoundSelectedEventEvidence>[]
                 {
                     new[] { new WoundSelectedEventEvidence(1, Evidence("combat", "combat", "combat_76")) },
                     new[] { new WoundSelectedEventEvidence(0, Evidence("combat", "trap", "combat_76")) },
                     new[] { new WoundSelectedEventEvidence(0, Evidence("combat", "combat", "other_authority")) },
                     new[] { new WoundSelectedEventEvidence(0, Evidence("unknown", "combat", "combat_76")) },
                     new[] { new WoundSelectedEventEvidence(0, Evidence("combat", "combat", "combat_76") with { OutcomeKind = "harmless", MaximumSeverityRank = 0 }) },
                     new[]
                     {
                         new WoundSelectedEventEvidence(0, Evidence("combat", "combat", "combat_76")),
                         new WoundSelectedEventEvidence(0, Evidence("combat", "combat", "combat_76"))
                     }
                 })
        {
            AssertInvalid(WoundAcceptedEventAuthorityComposer.Compose(projection, selected));
        }
    }

    [Fact]
    public void ProjectionAndResultDetachMutableInputCollections()
    {
        var mutable = new[] { Event("event_77", "formal", "formal_77") };
        var projection = Projection(mutable);
        mutable[0] = Event("event_changed", "changed", "changed");

        var result = WoundAcceptedEventAuthorityComposer.Compose(
            projection,
            Array.Empty<WoundSelectedEventEvidence>());

        Assert.True(result.Success, Describe(result.Issues));
        Assert.Equal("event_77", Assert.Single(result.Events).EventRef);
        Assert.Equal("event_77", Assert.Single(projection.Events).EventRef);
    }

    private static WoundAcceptedResponseEventProjection Projection(
        params WoundAcceptedResponseEventCoordinate[] events) =>
        ProjectionWith("session_72", "request_72", Fingerprint('b'), 72, events);

    private static WoundAcceptedResponseEventProjection ProjectionWith(
        string sessionId,
        string requestId,
        string snapshotToken,
        int turn,
        params WoundAcceptedResponseEventCoordinate[] events) =>
        new(sessionId, requestId, snapshotToken, turn, events);

    private static WoundAcceptedResponseEventCoordinate Event(
        string eventRef,
        string kind,
        string authorityId) => new(eventRef, kind, authorityId);

    private static WoundOpportunityEventEvidence Evidence(
        string adapterKind,
        string authorityKind,
        string authorityId) => new(
        adapterKind,
        authorityKind,
        authorityId,
        "harmful",
        2,
        "Проверяемая причина ранения.");

    private static string Fingerprint(char value) => "sha256:" + new string(value, 64);

    private static void AssertInvalid(WoundAcceptedEventAuthorityCompositionResult result)
    {
        Assert.False(result.Success);
        Assert.Empty(result.Events);
        Assert.Equal(string.Empty, result.EventsFingerprint);
        Assert.NotEmpty(result.Issues);
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(issue =>
            $"{issue.Code}: {issue.FilePath}: {issue.Actual}"));

    private static Type RequiredType(string name) =>
        typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services." + name,
            throwOnError: false,
            ignoreCase: false) ??
        throw new Xunit.Sdk.XunitException($"T064 requires {name}.");
}
