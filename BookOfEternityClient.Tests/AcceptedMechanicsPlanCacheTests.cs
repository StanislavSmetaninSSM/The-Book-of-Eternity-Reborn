using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AcceptedMechanicsPlanCacheTests
{
    [Fact]
    public void GetOrBuildValidated_IdenticalCompleteInputReusesOnePlanAndIdentityAllocation()
    {
        var planner = new CountingPlanner();
        var cache = new AcceptedMechanicsPlanCache(planner.Build);
        var input = Input();

        var first = cache.GetOrBuildValidated(input);
        var second = cache.GetOrBuildValidated(input);

        Assert.True(first.Success);
        Assert.Same(first, second);
        Assert.Same(first.Plan, second.Plan);
        Assert.Equal(1, planner.Calls);
        Assert.Equal(1, planner.IdentityAllocations);
        Assert.Equal("mechanics_test_1", first.Plan!.EffectIdentityAfterImage["allocatedId"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("session")]
    [InlineData("request")]
    [InlineData("snapshot")]
    [InlineData("realm")]
    [InlineData("turn")]
    [InlineData("definitions")]
    [InlineData("owners")]
    [InlineData("resource_state")]
    [InlineData("resource_history")]
    [InlineData("effect_sources")]
    [InlineData("effect_targets")]
    [InlineData("effect_carriers")]
    [InlineData("effect_index")]
    [InlineData("accepted_events")]
    [InlineData("accepted_events_input")]
    [InlineData("commands")]
    [InlineData("pending")]
    [InlineData("internal_inputs")]
    [InlineData("resource_commands")]
    [InlineData("effect_commands")]
    [InlineData("pending_input")]
    [InlineData("internal_input")]
    [InlineData("before_bytes")]
    [InlineData("before_absence")]
    public void GetOrBuildValidated_EveryBoundInputChangeBuildsANewPlan(string mutation)
    {
        var planner = new CountingPlanner();
        var cache = new AcceptedMechanicsPlanCache(planner.Build);

        var first = cache.GetOrBuildValidated(Input());
        var second = cache.GetOrBuildValidated(Input(mutation));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.NotSame(first.Plan, second.Plan);
        Assert.NotEqual(first.Plan!.InputFingerprint, second.Plan!.InputFingerprint);
        Assert.Equal(2, planner.Calls);
        Assert.Equal(2, planner.IdentityAllocations);
    }

    [Fact]
    public void GetOrBuildValidated_ObjectAndDictionaryOrderDoNotChangeCanonicalFingerprint()
    {
        var planner = new CountingPlanner();
        var cache = new AcceptedMechanicsPlanCache(planner.Build);
        var firstBefore = BeforeImages(new byte[] { 4, 5, 6 });
        var secondBefore = firstBefore
            .Reverse()
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        var first = cache.GetOrBuildValidated(Input(
            resourceCommands: new JsonObject { ["b"] = 2, ["a"] = 1 },
            beforeImages: firstBefore));
        var second = cache.GetOrBuildValidated(Input(
            resourceCommands: new JsonObject { ["a"] = 1, ["b"] = 2 },
            beforeImages: secondBefore));

        Assert.Same(first, second);
        Assert.Equal(1, planner.Calls);
        Assert.Equal(1, planner.IdentityAllocations);
    }

    [Fact]
    public void FailedRevalidationClearsPriorValidatedHandoffAndDoesNotPublishPartialPlan()
    {
        var planner = new CountingPlanner();
        var cache = new AcceptedMechanicsPlanCache(planner.Build);
        var valid = Input();
        Assert.True(cache.GetOrBuildValidated(valid).Success);

        planner.FailNext = true;
        var failed = cache.GetOrBuildValidated(Input("request"));

        Assert.False(failed.Success);
        Assert.Null(failed.Plan);
        Assert.False(cache.HasValidated);
        Assert.False(cache.TryTakeValidated(valid.CreateBinding(), out _));
    }

    [Fact]
    public void UpstreamValidationIssueInvalidatesBeforePlannerAndCannotReusePriorResult()
    {
        var planner = new CountingPlanner();
        var cache = new AcceptedMechanicsPlanCache(planner.Build);
        var valid = Input();
        Assert.True(cache.GetOrBuildValidated(valid).Success);

        var invalid = Input(validationIssues: new[] { Issue("mechanics_input_invalid") });
        var result = cache.GetOrBuildValidated(invalid);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue => issue.Code == "mechanics_input_invalid");
        Assert.Equal(1, planner.Calls);
        Assert.False(cache.HasValidated);
    }

    [Fact]
    public void PlannerExceptionCannotLeaveAPriorValidatedHandoff()
    {
        var planner = new CountingPlanner();
        var cache = new AcceptedMechanicsPlanCache(planner.Build);
        Assert.True(cache.GetOrBuildValidated(Input()).Success);

        planner.ThrowNext = true;

        Assert.Throws<InvalidOperationException>(() =>
            cache.GetOrBuildValidated(Input("request")));
        Assert.False(cache.HasValidated);
    }

    [Fact]
    public void PlannerFailureWithoutIssuesIsConvertedToABoundedFailClosedResult()
    {
        var planner = new CountingPlanner { EmptyFailureNext = true };
        var cache = new AcceptedMechanicsPlanCache(planner.Build);

        var result = cache.GetOrBuildValidated(Input());

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "accepted_mechanics_plan_missing");
        Assert.False(cache.HasValidated);
    }

    [Fact]
    public void TryTakeValidated_ChangedLiveBindingInvalidatesTheHandoff()
    {
        var planner = new CountingPlanner();
        var cache = new AcceptedMechanicsPlanCache(planner.Build);
        var input = Input();
        cache.GetOrBuildValidated(input);

        Assert.False(cache.TryTakeValidated(Input("request").CreateBinding(), out _));
        Assert.False(cache.HasValidated);
        Assert.False(cache.TryTakeValidated(input.CreateBinding(), out _));
    }

    [Fact]
    public void TryTakeValidated_ExactLiveBindingConsumesExactlyOnce()
    {
        var planner = new CountingPlanner();
        var cache = new AcceptedMechanicsPlanCache(planner.Build);
        var input = Input();
        var validated = cache.GetOrBuildValidated(input);

        Assert.True(cache.TryTakeValidated(input.CreateBinding(), out var taken));
        Assert.Same(validated, taken);
        Assert.False(cache.HasValidated);
        Assert.False(cache.TryTakeValidated(input.CreateBinding(), out _));
    }

    [Fact]
    public void InputAndPlanDefensivelyCloneJsonCollectionsAndExactBeforeImageBytes()
    {
        var resourceCommands = new JsonObject { ["resourceChanges"] = new JsonArray() };
        var beforeBytes = new byte[] { 1, 2, 3 };
        var beforeImages = BeforeImages(beforeBytes);
        var input = Input(
            resourceCommands: resourceCommands,
            beforeImages: beforeImages);
        var planner = new CountingPlanner();
        var result = new AcceptedMechanicsPlanCache(planner.Build).GetOrBuildValidated(input);

        resourceCommands["forged"] = true;
        beforeBytes[0] = 9;
        beforeImages.Clear();
        var returnedBytes = result.Plan!.BeforeImages["game_state/resources/resource_state.json"].Bytes!;
        returnedBytes[1] = 9;
        var returnedState = result.Plan.StateAfterImage;
        returnedState["forged"] = true;
        var returnedCarriers = result.Plan.EffectCarrierAfterImages;
        returnedCarriers["game_state/effects/effects.json"]["forged"] = true;

        Assert.Null(input.ResourceCommands["forged"]);
        Assert.Equal(new byte[] { 1, 2, 3 },
            result.Plan.BeforeImages["game_state/resources/resource_state.json"].Bytes);
        Assert.Null(result.Plan.StateAfterImage["forged"]);
        Assert.Null(result.Plan.EffectCarrierAfterImages["game_state/effects/effects.json"]["forged"]);
        Assert.True(Assert.IsAssignableFrom<IDictionary<string, CanonicalBeforeImage>>(
            result.Plan.BeforeImages).IsReadOnly);
        Assert.Equal(new[]
        {
            "game_state/effects/effect_commands.json",
            "game_state/resources/resource_commands.json"
        }, result.Plan.ConsumedPaths);
        Assert.Equal(new[]
        {
            "game_state/effects/effect_commands.json",
            "game_state/effects/effect_identity_index.json",
            "game_state/effects/effects.json",
            "game_state/resources/resource_commands.json",
            "game_state/resources/resource_definitions.json",
            "game_state/resources/resource_history.json",
            "game_state/resources/resource_state.json"
        }, result.Plan.TouchedPaths);
    }

    [Fact]
    public void CanonicalBeforeImageDistinguishesMissingFromEveryPresentByteSequence()
    {
        Assert.Throws<ArgumentException>(() => new CanonicalBeforeImage(false, Array.Empty<byte>()));
        Assert.Throws<ArgumentException>(() => new CanonicalBeforeImage(true, null));

        var missing = new CanonicalBeforeImage(false, null);
        var empty = new CanonicalBeforeImage(true, Array.Empty<byte>());

        Assert.False(missing.Existed);
        Assert.Null(missing.Bytes);
        Assert.True(empty.Existed);
        Assert.Empty(empty.Bytes!);
        Assert.NotEqual(missing.Fingerprint, empty.Fingerprint);
    }

    [Theory]
    [InlineData("C:/outside.json")]
    [InlineData("/outside.json")]
    [InlineData("../outside.json")]
    [InlineData("game_state\\outside.json")]
    public void InputRejectsNonCanonicalOrRootedBeforeImagePaths(string path)
    {
        var beforeImages = BeforeImages(new byte[] { 4, 5, 6 });
        beforeImages.Remove("game_state/resources/resource_state.json");
        beforeImages[path] = new CanonicalBeforeImage(true, new byte[] { 7 });

        Assert.Throws<ArgumentException>(() => Input(beforeImages: beforeImages));
    }

    [Fact]
    public void PlanningResultDefensivelyCopiesIssueCollections()
    {
        var planner = new CountingPlanner();
        var result = new AcceptedMechanicsPlanCache(planner.Build)
            .GetOrBuildValidated(Input());

        planner.ResultIssues.Add(Issue("late_mutation"));

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
    }

    private static AcceptedMechanicsInput Input(
        string? mutation = null,
        JsonObject? resourceCommands = null,
        IReadOnlyDictionary<string, CanonicalBeforeImage>? beforeImages = null,
        IReadOnlyList<ValidationIssue>? validationIssues = null)
    {
        var fingerprints = Fingerprints(mutation);
        var canonicalBeforeImages = beforeImages ?? BeforeImages(
            mutation == "before_bytes" ? new byte[] { 4, 5, 7 } : new byte[] { 4, 5, 6 },
            stateAbsent: mutation == "before_absence");
        return new AcceptedMechanicsInput(
            SessionId: mutation == "session" ? "session_b" : "session_a",
            RequestId: mutation == "request" ? "request_b" : "request_a",
            SnapshotToken: mutation == "snapshot" ? "snapshot_b" : "snapshot_a",
            Realm: mutation == "realm" ? "chaos_sea" : "mortal_world",
            Turn: mutation == "turn" ? 43 : 42,
            AcceptedEvents: Object("event", mutation == "accepted_events_input" ? 2 : 1),
            ResourceCommands: resourceCommands ?? Object("resource", mutation == "resource_commands" ? 2 : 1),
            EffectCommands: Object("effect", mutation == "effect_commands" ? 2 : 1),
            PendingInput: Object("pendingInput", mutation == "pending_input" ? 2 : 1),
            InternalInputs: Object("internal", mutation == "internal_input" ? 2 : 1),
            AuthorityFingerprints: fingerprints,
            BeforeImages: canonicalBeforeImages,
            ValidationIssues: validationIssues ?? Array.Empty<ValidationIssue>());
    }

    private static AcceptedMechanicsAuthorityFingerprints Fingerprints(string? mutation) => new(
        Definitions: Hash(mutation == "definitions" ? 'b' : 'a'),
        Owners: Hash(mutation == "owners" ? 'b' : 'a'),
        ResourceState: Hash(mutation == "resource_state" ? 'b' : 'a'),
        ResourceHistory: Hash(mutation == "resource_history" ? 'b' : 'a'),
        EffectSources: Hash(mutation == "effect_sources" ? 'b' : 'a'),
        EffectTargets: Hash(mutation == "effect_targets" ? 'b' : 'a'),
        EffectCarriers: Hash(mutation == "effect_carriers" ? 'b' : 'a'),
        EffectIdentityIndex: Hash(mutation == "effect_index" ? 'b' : 'a'),
        AcceptedEvents: Hash(mutation == "accepted_events" ? 'b' : 'a'),
        Commands: Hash(mutation == "commands" ? 'b' : 'a'),
        Pending: Hash(mutation == "pending" ? 'b' : 'a'),
        InternalInputs: Hash(mutation == "internal_inputs" ? 'b' : 'a'));

    private static string Hash(char value) => "sha256:" + new string(value, 64);

    private static Dictionary<string, CanonicalBeforeImage> BeforeImages(
        byte[] stateBytes,
        bool stateAbsent = false) =>
        new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
        {
            ["game_state/effects/effect_commands.json"] = new(true, new byte[] { 1 }),
            ["game_state/effects/effect_identity_index.json"] = new(true, new byte[] { 2 }),
            ["game_state/effects/effects.json"] = new(true, new byte[] { 3 }),
            ["game_state/resources/resource_commands.json"] = new(true, new byte[] { 4 }),
            ["game_state/resources/resource_definitions.json"] = new(true, new byte[] { 5 }),
            ["game_state/resources/resource_history.json"] = new(true, new byte[] { 6 }),
            ["game_state/resources/resource_state.json"] = stateAbsent
                ? new CanonicalBeforeImage(false, null)
                : new CanonicalBeforeImage(true, stateBytes)
        };

    private static JsonObject Object(string property, int value) => new() { [property] = value };

    private static ValidationIssue Issue(string code) => new(
        "mechanics",
        IssueSeverity.Error,
        "Invalid accepted mechanics input.",
        code: code,
        section: "accepted_mechanics");

    private sealed class CountingPlanner
    {
        internal int Calls { get; private set; }

        internal int IdentityAllocations { get; private set; }

        internal bool FailNext { get; set; }

        internal bool ThrowNext { get; set; }

        internal bool EmptyFailureNext { get; set; }

        internal List<ValidationIssue> ResultIssues { get; } = new();

        internal AcceptedMechanicsPlanningResult Build(
            AcceptedMechanicsInput input,
            string inputFingerprint)
        {
            Calls++;
            if (ThrowNext)
            {
                ThrowNext = false;
                throw new InvalidOperationException("Injected planner failure.");
            }
            if (EmptyFailureNext)
            {
                EmptyFailureNext = false;
                return new AcceptedMechanicsPlanningResult(
                    null,
                    Array.Empty<ValidationIssue>());
            }
            if (FailNext)
            {
                FailNext = false;
                return new AcceptedMechanicsPlanningResult(
                    null,
                    new[] { Issue("mechanics_plan_invalid") });
            }

            IdentityAllocations++;
            var definitions = Object("schemaVersion", 1);
            var state = Object("schemaVersion", 1);
            var history = Object("schemaVersion", 1);
            var effectIndex = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["allocatedId"] = $"mechanics_test_{IdentityAllocations}"
            };
            var carriers = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
            {
                ["game_state/effects/effects.json"] = Object("schemaVersion", 1)
            };
            var touched = new[]
            {
                "game_state/resources/resource_state.json",
                "game_state/effects/effects.json",
                "game_state/resources/resource_definitions.json",
                "game_state/effects/effect_identity_index.json",
                "game_state/resources/resource_history.json",
                "game_state/resources/resource_commands.json",
                "game_state/effects/effect_commands.json"
            };
            var consumed = new[]
            {
                "game_state/resources/resource_commands.json",
                "game_state/effects/effect_commands.json"
            };
            return new AcceptedMechanicsPlanningResult(
                new AcceptedMechanicsPlan(
                    inputFingerprint,
                    definitions,
                    state,
                    history,
                    carriers,
                    effectIndex,
                    new Dictionary<string, JsonObject?>(),
                    new Dictionary<string, JsonObject>(),
                    input.BeforeImages,
                    touched,
                    consumed,
                    input.AuthorityFingerprints,
                    Array.Empty<ResourceAppliedEvent>(),
                    new ResourceProjectionInput(
                        definitions,
                        state,
                        history,
                        input.AuthorityFingerprints.Owners),
                    effectPlan: null),
                ResultIssues);
        }
    }
}
