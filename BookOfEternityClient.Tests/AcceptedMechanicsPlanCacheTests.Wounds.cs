using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedMechanicsPlanCacheTests
{
    [Fact]
    public void WoundPreparedCache_IdenticalCompleteInputPreparesOnce()
    {
        var preparer = new CountingWoundPreparer();
        var cache = new WoundAcceptedTurnPlanCache(
            preparer.Build,
            WoundAcceptedTurnPlanner.Finalize);
        var input = WoundInput();

        var first = cache.GetOrBuildPrepared(input);
        var second = cache.GetOrBuildPrepared(input);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(
            first.Plan!.WoundPreparationFingerprint,
            second.Plan!.WoundPreparationFingerprint);
        Assert.Equal(1, preparer.Calls);
        Assert.True(cache.TryPeekPrepared(out var peeked));
        Assert.Equal(
            first.Plan.WoundPreparationFingerprint,
            peeked.Plan!.WoundPreparationFingerprint);
    }

    [Fact]
    public void WoundPreparedCache_ChangedBindingPreparesFreshAndClearsPriorFinal()
    {
        var preparer = new CountingWoundPreparer();
        var finalizer = new CountingWoundFinalizer();
        var cache = new WoundAcceptedTurnPlanCache(
            preparer.Build,
            finalizer.Build);
        var firstPrepared = AssertPrepared(cache.GetOrBuildPrepared(WoundInput()));
        var firstEffect = BuildEmptyEffectStage(firstPrepared);
        Assert.True(cache.GetOrBuildFinal(firstPrepared, firstEffect).Success);
        Assert.True(cache.TryPeekFinal(out _));

        var changed = WoundInput(requestId: "request_wound_cache_changed");
        var secondPrepared = AssertPrepared(cache.GetOrBuildPrepared(changed));

        Assert.NotEqual(
            firstPrepared.WoundPreparationFingerprint,
            secondPrepared.WoundPreparationFingerprint);
        Assert.Equal(2, preparer.Calls);
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Theory]
    [InlineData("session")]
    [InlineData("request")]
    [InlineData("snapshot")]
    [InlineData("turn")]
    [InlineData("event_ref")]
    [InlineData("event_fingerprint")]
    [InlineData("carrier")]
    public void WoundPreparedCache_EveryBoundAuthorityChangeBuildsFreshPlan(
        string mutation)
    {
        var preparer = new CountingWoundPreparer();
        var cache = new WoundAcceptedTurnPlanCache(
            preparer.Build,
            WoundAcceptedTurnPlanner.Finalize);

        var first = AssertPrepared(cache.GetOrBuildPrepared(WoundInput()));
        var second = AssertPrepared(
            cache.GetOrBuildPrepared(MutateWoundInput(mutation)));

        Assert.NotEqual(first.InputFingerprint, second.InputFingerprint);
        Assert.Equal(2, preparer.Calls);
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Fact]
    public void WoundPreparedCache_NormalizesObjectOrderButPreservesArrayOrder()
    {
        var preparer = new CountingWoundPreparer();
        var cache = new WoundAcceptedTurnPlanCache(
            preparer.Build,
            WoundAcceptedTurnPlanner.Finalize);
        var firstInput = WoundInput() with
        {
            PreTurnIdentityIndex = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            }
        };
        var reorderedObjectInput = WoundInput() with
        {
            PreTurnIdentityIndex = new JsonObject
            {
                ["entries"] = new JsonArray(),
                ["schemaVersion"] = 1
            }
        };

        var first = AssertPrepared(cache.GetOrBuildPrepared(firstInput));
        var reordered = AssertPrepared(
            cache.GetOrBuildPrepared(reorderedObjectInput));

        Assert.Equal(first.InputFingerprint, reordered.InputFingerprint);
        Assert.Equal(1, preparer.Calls);

        var arrayInput = WoundInputWithTwoEvents(reverse: false);
        var reversedArrayInput = WoundInputWithTwoEvents(reverse: true);
        var ordered = AssertPrepared(cache.GetOrBuildPrepared(arrayInput));
        var reversed = AssertPrepared(cache.GetOrBuildPrepared(reversedArrayInput));

        Assert.NotEqual(ordered.InputFingerprint, reversed.InputFingerprint);
        Assert.Equal(3, preparer.Calls);
    }

    [Theory]
    [InlineData("realm")]
    [InlineData("opportunity")]
    [InlineData("proposal")]
    [InlineData("definition")]
    [InlineData("root")]
    [InlineData("slot")]
    [InlineData("identity")]
    [InlineData("history")]
    public void WoundPreparedCache_EveryCompleteInputSectionPreventsOldPlanReuse(
        string mutation)
    {
        var preparer = new CountingWoundPreparer();
        var cache = new WoundAcceptedTurnPlanCache(
            preparer.Build,
            WoundAcceptedTurnPlanner.Finalize);
        var input = WoundEffectBatchPlannerTests.CreateInputForAcceptedCache();
        var changed = MutateCompleteWoundInput(input, mutation);
        var original = AssertPrepared(cache.GetOrBuildPrepared(input));

        var result = cache.GetOrBuildPrepared(changed);

        Assert.NotEqual(
            WoundAcceptedTurnFingerprints.ComputeInput(input),
            WoundAcceptedTurnFingerprints.ComputeInput(changed));
        if (result.Success)
        {
            Assert.NotEqual(
                original.InputFingerprint,
                result.Plan!.InputFingerprint);
            Assert.Equal(2, preparer.Calls);
        }
        else
        {
            Assert.False(cache.TryPeekPrepared(out _));
            Assert.InRange(preparer.Calls, 1, 2);
        }
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Fact]
    public void WoundEffectCache_CanonicalObjectOrderReusesExactPlan()
    {
        var prepared = AssertPrepared(
            WoundAcceptedTurnPlanner.Prepare(
                WoundEffectBatchPlannerTests.CreateInputForAcceptedCache()));
        var input = WoundEffectBatchPlannerTests
            .CreateEffectInputForAcceptedCache(prepared);
        var reordered = input with
        {
            RawCommands = new JsonObject(input.RawCommands
                .Reverse()
                .Select(static pair =>
                    KeyValuePair.Create(
                        pair.Key,
                        pair.Value?.DeepClone())))
        };
        var cache = new EffectAcceptedTurnPlanCache();

        var first = cache.GetOrBuildWoundValidated(
            prepared,
            input,
            out var firstReused);
        var second = cache.GetOrBuildWoundValidated(
            prepared,
            reordered,
            out var secondReused);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.False(firstReused);
        Assert.True(secondReused);
        Assert.Same(first, second);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("preallocation")]
    public void WoundEffectCache_TargetAndPreallocationAuthorityPreventReuse(
        string mutation)
    {
        var prepared = AssertPrepared(
            WoundAcceptedTurnPlanner.Prepare(
                WoundEffectBatchPlannerTests.CreateInputForAcceptedCache()));
        var input = WoundEffectBatchPlannerTests
            .CreateEffectInputForAcceptedCache(prepared);
        var identities = CombatantIdentityState.BuildNew(
            new JsonArray(),
            new CombatantIdentityFactory()).State!;
        var changed = mutation switch
        {
            "target" => input with
            {
                TargetAuthorityInput = new EffectTargetAuthorityInput(
                    Array.Empty<EffectTargetExport>(),
                    Array.Empty<EffectTargetExport>(),
                    new HashSet<string>(StringComparer.Ordinal),
                    CombatantIdentities: null)
            },
            "preallocation" => input with
            {
                PreallocatedCombatantIdentities = identities
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var cache = new EffectAcceptedTurnPlanCache();
        var original = cache.GetOrBuildWoundValidated(
            prepared,
            input,
            out var originalReused);

        var changedResult = cache.GetOrBuildWoundValidated(
            prepared,
            changed,
            out var changedReused);

        Assert.True(original.Success);
        Assert.True(changedResult.Success);
        Assert.False(originalReused);
        Assert.False(changedReused);
        Assert.NotEqual(
            WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input),
            WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, changed));
        Assert.NotSame(original, changedResult);
    }

    [Theory]
    [InlineData("publication")]
    [InlineData("accepted")]
    public void WoundEffectCache_NullAndExplicitEmptyCarrierBaselinesDoNotReuse(
        string baseline)
    {
        var prepared = AssertPrepared(
            WoundAcceptedTurnPlanner.Prepare(
                WoundEffectBatchPlannerTests.CreateInputForAcceptedCache()));
        var input = WoundEffectBatchPlannerTests
            .CreateEffectInputForAcceptedCache(prepared) with
        {
            PreTurnCarriers = new EffectCarrierCatalogInput(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["activeEffects"] = new JsonArray()
                },
                null,
                null,
                null,
                null,
                null)
        };
        var empty = new EffectCarrierCatalogInput(
            null,
            null,
            null,
            null,
            null,
            null);
        var changed = baseline switch
        {
            "publication" => input with
            {
                PublicationCarrierBaselines = empty
            },
            "accepted" => input with
            {
                AcceptedCarrierBaselines = empty
            },
            _ => throw new ArgumentOutOfRangeException(nameof(baseline))
        };
        var cache = new EffectAcceptedTurnPlanCache();

        var first = cache.GetOrBuildWoundValidated(
            prepared,
            input,
            out var firstReused);
        var second = cache.GetOrBuildWoundValidated(
            prepared,
            changed,
            out var secondReused);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.False(firstReused);
        Assert.False(secondReused);
        Assert.NotEqual(
            WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input),
            WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, changed));
        Assert.NotSame(first, second);
    }

    [Fact]
    public void WoundPreparedCache_AcceptsCompleteNonEmptyTransitionAuthority()
    {
        var cache = new WoundAcceptedTurnPlanCache();
        var input = WoundEffectBatchPlannerTests
            .CreateInputForAcceptedCache();

        var prepared = AssertPrepared(cache.GetOrBuildPrepared(input));

        Assert.Single(prepared.PreparedWounds);
        Assert.Single(prepared.EffectOperationBatches);
        Assert.True(cache.TryPeekPrepared(out var peeked));
        Assert.Equal(
            prepared.WoundPreparationFingerprint,
            peeked.Plan!.WoundPreparationFingerprint);
    }

    [Fact]
    public void WoundPreparedCache_RejectsThirtyThirdTransitionBeforePreparerCall()
    {
        var preparer = new CountingWoundPreparer();
        var cache = new WoundAcceptedTurnPlanCache(
            preparer.Build,
            WoundAcceptedTurnPlanner.Finalize);

        var accepted = cache.GetOrBuildPrepared(
            WoundEffectBatchPlannerTests.CreateInputForAcceptedCache(32));
        Assert.True(
            accepted.Success,
            string.Join(Environment.NewLine, accepted.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        Assert.Equal(1, preparer.Calls);

        var result = cache.GetOrBuildPrepared(
            WoundEffectBatchPlannerTests.CreateInputForAcceptedCache(33));

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_plan_transition_limit_exceeded");
        Assert.Equal(1, preparer.Calls);
        Assert.False(cache.TryPeekPrepared(out _));
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Fact]
    public void WoundPreparedCache_FailedPreparationClearsPriorPreparedAndFinal()
    {
        var preparer = new CountingWoundPreparer();
        var cache = new WoundAcceptedTurnPlanCache(
            preparer.Build,
            WoundAcceptedTurnPlanner.Finalize);
        var prepared = AssertPrepared(cache.GetOrBuildPrepared(WoundInput()));
        Assert.True(cache.GetOrBuildFinal(
            prepared,
            BuildEmptyEffectStage(prepared)).Success);
        preparer.FailNext = true;

        var failed = cache.GetOrBuildPrepared(
            WoundInput(requestId: "request_wound_cache_failure"));

        Assert.False(failed.Success);
        Assert.Null(failed.Plan);
        Assert.Contains(failed.Issues, issue =>
            issue.Code == "wound_cache_injected_prepare_failure");
        Assert.False(cache.TryPeekPrepared(out _));
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Theory]
    [InlineData("partial", "wound_plan_partial_result")]
    [InlineData("empty", "wound_plan_partial_result")]
    [InlineData("fingerprint", "wound_plan_prepared_binding_mismatch")]
    [InlineData("baseline", "wound_plan_prepared_seal_mismatch")]
    [InlineData("baseline_resealed", "wound_plan_prepared_binding_mismatch")]
    public void WoundPreparedCache_RejectsMalformedCollaboratorResultAndClearsHandoffs(
        string corruption,
        string expectedCode)
    {
        var preparer = new CorruptingWoundPreparer();
        var cache = new WoundAcceptedTurnPlanCache(
            preparer.Build,
            WoundAcceptedTurnPlanner.Finalize);
        var prepared = AssertPrepared(cache.GetOrBuildPrepared(WoundInput()));
        Assert.True(cache.GetOrBuildFinal(
            prepared,
            BuildEmptyEffectStage(prepared)).Success);
        preparer.CorruptNext = corruption;

        var result = cache.GetOrBuildPrepared(
            WoundInput(requestId: "request_wound_cache_corrupt_" + corruption));

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Equal(expectedCode, Assert.Single(result.Issues).Code);
        Assert.False(cache.TryPeekPrepared(out _));
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Theory]
    [InlineData("prepared_payload_resealed")]
    [InlineData("source_payload_resealed")]
    public void WoundPreparedCache_RejectsResealedPayloadDerivedFromDifferentInput(
        string corruption)
    {
        var preparer = new CorruptingWoundPreparer
        {
            CorruptNext = corruption
        };
        var cache = new WoundAcceptedTurnPlanCache(
            preparer.Build,
            WoundAcceptedTurnPlanner.Finalize);

        var result = cache.GetOrBuildPrepared(
            WoundEffectBatchPlannerTests.CreateInputForAcceptedCache());

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Equal(
            "wound_plan_prepared_binding_mismatch",
            Assert.Single(result.Issues).Code);
        Assert.False(cache.TryPeekPrepared(out _));
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WoundPreparedCache_BrokenCollaboratorClearsHandoffsBeforeThrowing(
        bool returnNull)
    {
        var preparer = new CorruptingWoundPreparer();
        var cache = new WoundAcceptedTurnPlanCache(
            preparer.Build,
            WoundAcceptedTurnPlanner.Finalize);
        AssertPrepared(cache.GetOrBuildPrepared(WoundInput()));
        preparer.CorruptNext = returnNull ? "null" : "throw";

        Assert.Throws<InvalidOperationException>(() =>
            cache.GetOrBuildPrepared(
                WoundInput(requestId: "request_wound_cache_broken")));
        Assert.False(cache.TryPeekPrepared(out _));
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Fact]
    public void WoundFinalCache_IdenticalExactHandoffFinalizesOnce()
    {
        var finalizer = new CountingWoundFinalizer();
        var cache = new WoundAcceptedTurnPlanCache(
            WoundAcceptedTurnPlanner.Prepare,
            finalizer.Build);
        var prepared = AssertPrepared(cache.GetOrBuildPrepared(WoundInput()));
        var effect = BuildEmptyEffectStage(prepared);

        var first = cache.GetOrBuildFinal(prepared, effect);
        var second = cache.GetOrBuildFinal(prepared, effect);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(
            first.Plan!.WoundFinalPlanFingerprint,
            second.Plan!.WoundFinalPlanFingerprint);
        Assert.Equal(1, finalizer.Calls);
        Assert.True(cache.TryPeekFinal(out var peeked));
        Assert.Equal(
            first.Plan.WoundFinalPlanFingerprint,
            peeked.Plan!.WoundFinalPlanFingerprint);
    }

    [Theory]
    [InlineData("partial", "wound_plan_partial_result")]
    [InlineData("empty", "wound_plan_partial_result")]
    [InlineData("fingerprint", "wound_plan_final_fingerprint_mismatch")]
    [InlineData("payload_resealed", "wound_plan_final_fingerprint_mismatch")]
    public void WoundFinalCache_RejectsMalformedCollaboratorResult(
        string corruption,
        string expectedCode)
    {
        var finalizer = new CorruptingWoundFinalizer();
        var cache = new WoundAcceptedTurnPlanCache(
            WoundAcceptedTurnPlanner.Prepare,
            finalizer.Build);
        var prepared = AssertPrepared(cache.GetOrBuildPrepared(WoundInput()));
        var effect = BuildEmptyEffectStage(prepared);
        Assert.True(cache.GetOrBuildFinal(prepared, effect).Success);
        cache.InvalidateFinal();
        finalizer.CorruptNext = corruption;

        var result = cache.GetOrBuildFinal(prepared, effect);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Equal(expectedCode, Assert.Single(result.Issues).Code);
        Assert.True(cache.TryPeekPrepared(out _));
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WoundFinalCache_BrokenCollaboratorClearsFinalBeforeThrowing(
        bool returnNull)
    {
        var finalizer = new CorruptingWoundFinalizer();
        var cache = new WoundAcceptedTurnPlanCache(
            WoundAcceptedTurnPlanner.Prepare,
            finalizer.Build);
        var prepared = AssertPrepared(cache.GetOrBuildPrepared(WoundInput()));
        var effect = BuildEmptyEffectStage(prepared);
        Assert.True(cache.GetOrBuildFinal(prepared, effect).Success);
        cache.InvalidateFinal();
        finalizer.CorruptNext = returnNull ? "null" : "throw";

        Assert.Throws<InvalidOperationException>(() =>
            cache.GetOrBuildFinal(prepared, effect));
        Assert.True(cache.TryPeekPrepared(out _));
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("empty")]
    public void WoundFinalCache_RejectsMalformedEffectEnvelopeWithoutFinalizing(
        string corruption)
    {
        var finalizer = new CountingWoundFinalizer();
        var cache = new WoundAcceptedTurnPlanCache(
            WoundAcceptedTurnPlanner.Prepare,
            finalizer.Build);
        var prepared = AssertPrepared(cache.GetOrBuildPrepared(WoundInput()));
        var validEffect = BuildEmptyEffectStage(prepared);
        Assert.True(cache.GetOrBuildFinal(prepared, validEffect).Success);
        var callsBeforeCorruption = finalizer.Calls;
        var malformed = corruption == "partial"
            ? new WoundEffectBatchPlanningResult(
                validEffect.Plan,
                new[] { WoundIssue("wound_cache_injected_effect_partial") })
            : new WoundEffectBatchPlanningResult(
                null,
                Array.Empty<ValidationIssue>());

        var result = cache.GetOrBuildFinal(prepared, malformed);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Equal(
            "wound_plan_effect_binding_mismatch",
            Assert.Single(result.Issues).Code);
        Assert.Equal(callsBeforeCorruption, finalizer.Calls);
        Assert.True(cache.TryPeekPrepared(out _));
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Fact]
    public void WoundFinalCache_RejectsPreparedPlanThatIsNotCurrentWithoutFinalizing()
    {
        var finalizer = new CountingWoundFinalizer();
        var cache = new WoundAcceptedTurnPlanCache(
            WoundAcceptedTurnPlanner.Prepare,
            finalizer.Build);
        var stalePrepared = AssertPrepared(cache.GetOrBuildPrepared(WoundInput()));
        var staleEffect = BuildEmptyEffectStage(stalePrepared);
        AssertPrepared(cache.GetOrBuildPrepared(
            WoundInput(requestId: "request_wound_cache_fresh")));

        var result = cache.GetOrBuildFinal(stalePrepared, staleEffect);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("wound_plan_prepared_binding_mismatch", issue.Code);
        Assert.Equal(0, finalizer.Calls);
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Fact]
    public void WoundFinalCache_RejectsStructurallyIdenticalPreparedPlanFromAnotherAuthorityState()
    {
        var foreignCache = new WoundAcceptedTurnPlanCache();
        var currentFinalizer = new CountingWoundFinalizer();
        var currentCache = new WoundAcceptedTurnPlanCache(
            WoundAcceptedTurnPlanner.Prepare,
            currentFinalizer.Build);
        var foreignPrepared = AssertPrepared(
            foreignCache.GetOrBuildPrepared(WoundInput()));
        var foreignEffect = BuildEmptyEffectStage(foreignPrepared);
        var currentPrepared = AssertPrepared(
            currentCache.GetOrBuildPrepared(WoundInput()));

        Assert.Equal(
            foreignPrepared.WoundPreparationFingerprint,
            currentPrepared.WoundPreparationFingerprint);

        var result = currentCache.GetOrBuildFinal(
            foreignPrepared,
            foreignEffect);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("wound_plan_stale_generation", issue.Code);
        Assert.Equal(0, currentFinalizer.Calls);
        Assert.False(currentCache.TryPeekFinal(out _));
    }

    [Fact]
    public void WoundFinalCache_RejectsOldPreparedStageAfterInputCyclesBackToEqualBytes()
    {
        var finalizer = new CountingWoundFinalizer();
        var cache = new WoundAcceptedTurnPlanCache(
            WoundAcceptedTurnPlanner.Prepare,
            finalizer.Build);
        var oldPrepared = AssertPrepared(cache.GetOrBuildPrepared(WoundInput()));
        var oldEffect = BuildEmptyEffectStage(oldPrepared);
        AssertPrepared(cache.GetOrBuildPrepared(
            WoundInput(requestId: "request_wound_cache_intermediate")));
        var currentPrepared = AssertPrepared(
            cache.GetOrBuildPrepared(WoundInput()));

        Assert.Equal(
            oldPrepared.WoundPreparationFingerprint,
            currentPrepared.WoundPreparationFingerprint);

        var result = cache.GetOrBuildFinal(oldPrepared, oldEffect);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("wound_plan_prepared_binding_mismatch", issue.Code);
        Assert.Equal(0, finalizer.Calls);
        Assert.False(cache.TryPeekFinal(out _));
    }

    [Fact]
    public void WoundCache_HasNoIndependentTakeSurface()
    {
        var methods = typeof(WoundAcceptedTurnPlanCache).GetMethods(
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic);

        Assert.DoesNotContain(methods, method =>
            method.Name.Contains("Take", StringComparison.Ordinal));
    }

    [Fact]
    public void WoundCache_StoresAndPeeksDetachedImmutableStageGraphs()
    {
        var identity = WoundContractTestData.CreateIdentityIndex();
        var history = WoundContractTestData.CreateHistory();
        var baseline = WoundInput();
        var input = baseline with
        {
            PreTurnIdentityIndex = identity,
            PreTurnHistory = history
        };
        var cache = new WoundAcceptedTurnPlanCache();

        var prepared = AssertPrepared(cache.GetOrBuildPrepared(input));
        var final = cache.GetOrBuildFinal(
            prepared,
            BuildEmptyEffectStage(prepared));
        Assert.True(final.Success);

        identity["forged"] = true;
        history["forged"] = true;
        var returnedBaseline = prepared.BaselineAuthority.PreTurnIdentityIndex;
        returnedBaseline["forged"] = true;
        var returnedFinalIdentity = final.Plan!.IdentityIndexAfterImage;
        returnedFinalIdentity["forged"] = true;

        Assert.True(cache.TryPeekPrepared(out var preparedPeek));
        Assert.True(cache.TryPeekFinal(out var finalPeek));
        Assert.Null(preparedPeek.Plan!.BaselineAuthority
            .PreTurnIdentityIndex["forged"]);
        Assert.Null(preparedPeek.Plan.BaselineAuthority.PreTurnHistory["forged"]);
        Assert.Null(finalPeek.Plan!.IdentityIndexAfterImage["forged"]);
        Assert.Equal(
            prepared.WoundPreparationFingerprint,
            preparedPeek.Plan.WoundPreparationFingerprint);
        Assert.Equal(
            final.Plan.WoundFinalPlanFingerprint,
            finalPeek.Plan.WoundFinalPlanFingerprint);
    }

    [Fact]
    public async Task WoundRegistry_RotatedGenerationRejectsOldPreparedHandoff()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            WoundPreparedAcceptedTurnPlan oldPrepared;
            WoundEffectBatchPlanningResult oldEffect;
            await using (var lease =
                         await fileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                oldPrepared = AssertPrepared(
                    WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                        fileSystem,
                        lease,
                        WoundInput()));
                oldEffect = BuildEmptyEffectStage(oldPrepared);
            }

            await using var lifecycle =
                await fileSystem.AcquireSessionLifecycleLeaseAsync();
            await using var replacement =
                await fileSystem.AcquireSessionReplacementWriteLeaseAsync(
                    lifecycle);
            fileSystem.RotateSessionGeneration(replacement);
            AssertPrepared(
                WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                    fileSystem,
                    replacement,
                    WoundInput()));

            var result =
                WoundAcceptedTurnPlanAuthority.GetOrBuildFinalValidated(
                    fileSystem,
                    replacement,
                    oldPrepared,
                    oldEffect);

            Assert.False(result.Success);
            Assert.Equal(
                "wound_plan_stale_generation",
                Assert.Single(result.Issues).Code);
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekFinal(
                fileSystem,
                replacement,
                out _));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task WoundRegistry_SameGenerationNewRevisionRejectsOldPreparedHandoff()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            var oldPrepared = AssertPrepared(
                WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                    fileSystem,
                    lease,
                    WoundInput()));
            var oldEffect = BuildEmptyEffectStage(oldPrepared);

            fileSystem.CanonicalRootAuthorityIdentity
                .AdvanceSessionGenerationRevision();
            AssertPrepared(
                WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                    fileSystem,
                    lease,
                    WoundInput()));

            var result =
                WoundAcceptedTurnPlanAuthority.GetOrBuildFinalValidated(
                    fileSystem,
                    lease,
                    oldPrepared,
                    oldEffect);

            Assert.False(result.Success);
            Assert.Equal(
                "wound_plan_stale_generation",
                Assert.Single(result.Issues).Code);
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task WoundRegistry_SameRootAndGenerationSharePreparedAuthority()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var firstFileSystem = CreateFileSystem(root);
            var secondFileSystem = CreateFileSystem(root);
            WoundPreparedAcceptedTurnPlan prepared;
            await using (var firstLease =
                         await firstFileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                prepared = AssertPrepared(
                    WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                        firstFileSystem,
                        firstLease,
                        WoundInput()));
            }

            await using var secondLease =
                await secondFileSystem.AcquireCanonicalWriteLeaseAsync();

            Assert.True(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                secondFileSystem,
                secondLease,
                out var peeked));
            Assert.Equal(
                prepared.WoundPreparationFingerprint,
                peeked.Plan!.WoundPreparationFingerprint);
            var effectInput = BuildEmptyEffectStage(prepared).Plan!.EffectInput;
            var effect =
                WoundAcceptedTurnPlanAuthority.GetOrBuildEffectValidated(
                    secondFileSystem,
                    secondLease,
                    prepared,
                    effectInput);
            Assert.True(
                WoundAcceptedTurnPlanAuthority.GetOrBuildFinalValidated(
                    secondFileSystem,
                    secondLease,
                    prepared,
                    effect).Success);
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task WoundRegistry_DifferentCanonicalRootsNeverShareAuthority()
    {
        var firstRoot = CreateAuthorityRoot();
        var secondRoot = CreateAuthorityRoot();
        try
        {
            var firstFileSystem = CreateFileSystem(firstRoot);
            var secondFileSystem = CreateFileSystem(secondRoot);
            await using (var firstLease =
                         await firstFileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                AssertPrepared(
                    WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                        firstFileSystem,
                        firstLease,
                        WoundInput()));
            }

            await using var secondLease =
                await secondFileSystem.AcquireCanonicalWriteLeaseAsync();

            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                secondFileSystem,
                secondLease,
                out _));
        }
        finally
        {
            DeleteAuthorityRoot(firstRoot);
            DeleteAuthorityRoot(secondRoot);
        }
    }

    [Fact]
    public async Task WoundRegistry_RequiresOwningActiveCanonicalWriteLease()
    {
        var firstRoot = CreateAuthorityRoot();
        var secondRoot = CreateAuthorityRoot();
        try
        {
            var firstFileSystem = CreateFileSystem(firstRoot);
            var secondFileSystem = CreateFileSystem(secondRoot);
            var disposedLease =
                await firstFileSystem.AcquireCanonicalWriteLeaseAsync();
            await disposedLease.DisposeAsync();

            Assert.Throws<InvalidOperationException>(() =>
                WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                    firstFileSystem,
                    disposedLease,
                    WoundInput()));

            await using var activeForeignLease =
                await firstFileSystem.AcquireCanonicalWriteLeaseAsync();
            Assert.Throws<InvalidOperationException>(() =>
                WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                    secondFileSystem,
                    activeForeignLease,
                    WoundInput()));
        }
        finally
        {
            DeleteAuthorityRoot(firstRoot);
            DeleteAuthorityRoot(secondRoot);
        }
    }

    [Fact]
    public async Task AcceptedTurnRegistry_AggregateInvalidationClearsEveryAuthorityStage()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            SeedAcceptedTurnAuthority(fileSystem, lease);

            AcceptedTurnPlanAuthority.InvalidateValidated(fileSystem, lease);
            AcceptedTurnPlanAuthority.InvalidateValidated(fileSystem, lease);

            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                fileSystem,
                lease,
                out _));
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekFinal(
                fileSystem,
                lease,
                out _));
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _,
                out _));
            Assert.False(AcceptedTurnAuthorityRegistry.HasMortalItemsValidated(
                fileSystem,
                lease));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task AcceptedTurnRegistry_RepairWaveRegistrationClearsEveryAuthorityStage()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            SeedAcceptedTurnAuthority(fileSystem, lease);
            var authority = WoundAcceptedTurnPlanCacheTests.CreateAuthority();
            var packet = WoundAcceptedTurnPlanCacheTests.CreatePacket();

            Assert.True(AcceptedMechanicsPlanAuthority.TryRegisterWoundRepairWave(
                fileSystem,
                lease,
                authority,
                new[] { packet }));

            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                fileSystem,
                lease,
                out _));
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekFinal(
                fileSystem,
                lease,
                out _));
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _,
                out _));
            Assert.False(AcceptedTurnAuthorityRegistry.HasMortalItemsValidated(
                fileSystem,
                lease));
            Assert.True(AcceptedMechanicsPlanAuthority.HasWoundRepairWave(
                fileSystem,
                lease));
            Assert.True(AcceptedMechanicsPlanAuthority.TryTakeWoundRepairPacket(
                fileSystem,
                lease,
                authority,
                packet.CreateReceipt(),
                out var taken));
            Assert.Equal(packet.CandidateRef, taken.CandidateRef);
            Assert.False(AcceptedMechanicsPlanAuthority.HasWoundRepairWave(
                fileSystem,
                lease));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task AcceptedTurnRegistry_RepairSnapshotMismatchClearsEveryAuthorityStage()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            SeedAcceptedTurnAuthority(fileSystem, lease);
            var authority = WoundAcceptedTurnPlanCacheTests.CreateAuthority();
            var mismatchedPacket = WoundAcceptedTurnPlanCacheTests.CreatePacket(
                snapshotToken: "snapshot_repair_changed");

            Assert.False(AcceptedMechanicsPlanAuthority.TryRegisterWoundRepairWave(
                fileSystem,
                lease,
                authority,
                new[] { mismatchedPacket }));

            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                fileSystem,
                lease,
                out _));
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekFinal(
                fileSystem,
                lease,
                out _));
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _,
                out _));
            Assert.False(AcceptedTurnAuthorityRegistry.HasMortalItemsValidated(
                fileSystem,
                lease));
            Assert.False(AcceptedMechanicsPlanAuthority.HasWoundRepairWave(
                fileSystem,
                lease));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task AcceptedTurnRegistry_ConsumedRepairReceiptPreservesValidSibling()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            var authority = WoundAcceptedTurnPlanCacheTests.CreateAuthority();
            var first = WoundAcceptedTurnPlanCacheTests.CreatePacket(
                "candidate_repair_001",
                WoundFingerprint("repair-first"));
            var second = WoundAcceptedTurnPlanCacheTests.CreatePacket(
                "candidate_repair_002",
                WoundFingerprint("repair-second"));
            Assert.True(AcceptedMechanicsPlanAuthority.TryRegisterWoundRepairWave(
                fileSystem,
                lease,
                authority,
                new[] { first, second }));

            Assert.True(AcceptedMechanicsPlanAuthority.TryTakeWoundRepairPacket(
                fileSystem,
                lease,
                authority,
                first.CreateReceipt(),
                out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryTakeWoundRepairPacket(
                fileSystem,
                lease,
                authority,
                first.CreateReceipt(),
                out _));
            Assert.True(AcceptedMechanicsPlanAuthority.HasWoundRepairWave(
                fileSystem,
                lease));
            Assert.True(AcceptedMechanicsPlanAuthority.TryTakeWoundRepairPacket(
                fileSystem,
                lease,
                authority,
                second.CreateReceipt(),
                out var taken));
            Assert.Equal(second.CandidateRef, taken.CandidateRef);
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task AcceptedTurnRegistry_NewWoundPreparationRevokesPendingRepairWave()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            var authority = WoundAcceptedTurnPlanCacheTests.CreateAuthority();
            var packet = WoundAcceptedTurnPlanCacheTests.CreatePacket();
            Assert.True(AcceptedMechanicsPlanAuthority.TryRegisterWoundRepairWave(
                fileSystem,
                lease,
                authority,
                new[] { packet }));

            AssertPrepared(WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                fileSystem,
                lease,
                WoundInput()));

            Assert.False(AcceptedMechanicsPlanAuthority.HasWoundRepairWave(
                fileSystem,
                lease));
            Assert.False(AcceptedMechanicsPlanAuthority.TryTakeWoundRepairPacket(
                fileSystem,
                lease,
                authority,
                packet.CreateReceipt(),
                out _));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task AcceptedTurnRegistry_RotatedSessionGenerationRevokesRepairWave()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            var authority = WoundAcceptedTurnPlanCacheTests.CreateAuthority();
            var packet = WoundAcceptedTurnPlanCacheTests.CreatePacket();
            await using (var lease =
                         await fileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.True(
                    AcceptedMechanicsPlanAuthority.TryRegisterWoundRepairWave(
                        fileSystem,
                        lease,
                        authority,
                        new[] { packet }));
            }

            await using var lifecycle =
                await fileSystem.AcquireSessionLifecycleLeaseAsync();
            await using var replacement =
                await fileSystem.AcquireSessionReplacementWriteLeaseAsync(
                    lifecycle);
            fileSystem.RotateSessionGeneration(replacement);

            Assert.False(AcceptedMechanicsPlanAuthority.HasWoundRepairWave(
                fileSystem,
                replacement));
            Assert.False(AcceptedMechanicsPlanAuthority.TryTakeWoundRepairPacket(
                fileSystem,
                replacement,
                authority,
                packet.CreateReceipt(),
                out _));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task AcceptedTurnRegistry_CommonTakeClearsSubordinateHandoffsAtomically()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            var commonInput = SeedAcceptedTurnAuthority(fileSystem, lease);

            Assert.True(AcceptedMechanicsPlanAuthority.TryTakeValidated(
                fileSystem,
                lease,
                commonInput.CreateBinding(),
                out _));

            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                fileSystem,
                lease,
                out _));
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekFinal(
                fileSystem,
                lease,
                out _));
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryTakeValidated(
                fileSystem,
                lease,
                commonInput.CreateBinding(),
                out _));
            Assert.False(AcceptedTurnAuthorityRegistry.HasMortalItemsValidated(
                fileSystem,
                lease));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task AcceptedTurnRegistry_ExplicitRevalidationCanReuseExactTakenCommonPlan()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            var commonInput = SeedAcceptedTurnAuthority(fileSystem, lease);
            Assert.True(AcceptedMechanicsPlanAuthority.TryTakeValidated(
                fileSystem,
                lease,
                commonInput.CreateBinding(),
                out var taken));

            var explicitlyRevalidated =
                AcceptedMechanicsPlanAuthority.GetOrBuildValidated(
                    fileSystem,
                    lease,
                    commonInput);

            Assert.Same(taken, explicitlyRevalidated);
            Assert.True(explicitlyRevalidated.Success);
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                fileSystem,
                lease,
                out _));
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task AcceptedTurnRegistry_MismatchedCommonTakeClearsEveryHandoff()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            SeedAcceptedTurnAuthority(fileSystem, lease);

            Assert.False(AcceptedMechanicsPlanAuthority.TryTakeValidated(
                fileSystem,
                lease,
                Input("request").CreateBinding(),
                out _));

            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                fileSystem,
                lease,
                out _));
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekFinal(
                fileSystem,
                lease,
                out _));
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _,
                out _));
            Assert.False(AcceptedTurnAuthorityRegistry.HasMortalItemsValidated(
                fileSystem,
                lease));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task AcceptedTurnRegistry_ChangedWoundPreparationClearsDownstreamHandoffs()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            SeedAcceptedTurnAuthority(fileSystem, lease);

            var changed = AssertPrepared(
                WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                    fileSystem,
                    lease,
                    WoundInput(requestId: "request_wound_registry_changed")));

            Assert.Equal(
                "request_wound_registry_changed",
                changed.Binding.RequestId);
            Assert.True(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                fileSystem,
                lease,
                out _));
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekFinal(
                fileSystem,
                lease,
                out _));
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _,
                out _));
            Assert.True(AcceptedTurnAuthorityRegistry.HasMortalItemsValidated(
                fileSystem,
                lease));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedTurnRegistry_ChangedOrFailedEffectClearsFinalAndCommon(
        bool failEffect)
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            SeedAcceptedTurnAuthority(fileSystem, lease);
            Assert.True(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                fileSystem,
                lease,
                out var preparedResult));
            var prepared = preparedResult.Plan!;
            var currentInput = BuildEmptyEffectStage(prepared).Plan!.EffectInput;
            var changedInput = failEffect
                ? currentInput with
                {
                    RawCommands = new JsonObject { ["unknown"] = true }
                }
                : currentInput with
                {
                    SnapshotToken = "snapshot_wound_effect_changed"
                };

            var result = EffectAcceptedTurnPlanAuthority.GetOrBuildValidated(
                fileSystem,
                lease,
                changedInput);

            Assert.Equal(!failEffect, result.Success);
            Assert.True(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                fileSystem,
                lease,
                out _));
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekFinal(
                fileSystem,
                lease,
                out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _,
                out _));
            Assert.True(AcceptedTurnAuthorityRegistry.HasMortalItemsValidated(
                fileSystem,
                lease));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task WoundRegistry_FinalizationRequiresCurrentRegistryOwnedEffectStage()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            var prepared = AssertPrepared(
                WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                    fileSystem,
                    lease,
                    WoundEffectBatchPlannerTests.CreateInputForAcceptedCache()));
            var effectInput = WoundEffectBatchPlannerTests
                .CreateEffectInputForAcceptedCache(prepared);
            var authoritative =
                WoundAcceptedTurnPlanAuthority.GetOrBuildEffectValidated(
                    fileSystem,
                    lease,
                    prepared,
                    effectInput);
            Assert.True(authoritative.Success);
            var foreign = WoundEffectBatchPlanner.Build(
                prepared,
                effectInput,
                new EffectIdentityFactory());
            Assert.True(foreign.Success);
            Assert.NotEqual(
                authoritative.Plan!.EffectAcceptedTurnPlanFingerprint,
                foreign.Plan!.EffectAcceptedTurnPlanFingerprint);

            var rejected =
                WoundAcceptedTurnPlanAuthority.GetOrBuildFinalValidated(
                    fileSystem,
                    lease,
                    prepared,
                    foreign);

            Assert.False(rejected.Success);
            Assert.Equal(
                "wound_plan_effect_binding_mismatch",
                Assert.Single(rejected.Issues).Code);
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekFinal(
                fileSystem,
                lease,
                out _));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task WoundRegistry_AuthoritativeNonEmptyEffectStageFinalizesAndRemainsPeekable()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            var prepared = AssertPrepared(
                WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                    fileSystem,
                    lease,
                    WoundEffectBatchPlannerTests.CreateInputForAcceptedCache()));
            var effectInput = WoundEffectBatchPlannerTests
                .CreateEffectInputForAcceptedCache(prepared);
            var effect = WoundAcceptedTurnPlanAuthority.GetOrBuildEffectValidated(
                fileSystem,
                lease,
                prepared,
                effectInput);

            var finalized = WoundAcceptedTurnPlanAuthority.GetOrBuildFinalValidated(
                fileSystem,
                lease,
                prepared,
                effect);

            Assert.True(effect.Success);
            Assert.NotEmpty(effect.Plan!.EffectPlan.ActiveEffects);
            Assert.True(finalized.Success);
            Assert.NotEmpty(finalized.Plan!.CarrierContributions);
            Assert.True(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _));
            Assert.True(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                fileSystem,
                lease,
                out _));
            Assert.True(WoundAcceptedTurnPlanAuthority.TryPeekFinal(
                fileSystem,
                lease,
                out var peeked));
            Assert.Equal(
                finalized.Plan.WoundFinalPlanFingerprint,
                peeked.Plan!.WoundFinalPlanFingerprint);
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task AcceptedTurnRegistry_ExplicitEffectInvalidationClearsDependentStages()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            SeedAcceptedTurnAuthority(fileSystem, lease);

            EffectAcceptedTurnPlanAuthority.InvalidateValidated(
                fileSystem,
                lease);

            Assert.True(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                fileSystem,
                lease,
                out _));
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekFinal(
                fileSystem,
                lease,
                out _));
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _,
                out _));
            Assert.True(AcceptedTurnAuthorityRegistry.HasMortalItemsValidated(
                fileSystem,
                lease));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public async Task WoundRegistry_ExplicitEffectRevalidationRotatesOpaqueHandoff()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            var prepared = AssertPrepared(
                WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                    fileSystem,
                    lease,
                    WoundInput()));
            var effectInput = BuildEmptyEffectStage(prepared).Plan!.EffectInput;
            var first =
                WoundAcceptedTurnPlanAuthority.GetOrBuildEffectValidated(
                    fileSystem,
                    lease,
                    prepared,
                    effectInput);
            Assert.True(first.Success);

            EffectAcceptedTurnPlanAuthority.InvalidateValidated(
                fileSystem,
                lease);
            var second =
                WoundAcceptedTurnPlanAuthority.GetOrBuildEffectValidated(
                    fileSystem,
                    lease,
                    prepared,
                    effectInput);
            Assert.True(second.Success);
            Assert.Equal(
                first.Plan!.EffectAcceptedTurnPlanFingerprint,
                second.Plan!.EffectAcceptedTurnPlanFingerprint);

            var result =
                WoundAcceptedTurnPlanAuthority.GetOrBuildFinalValidated(
                    fileSystem,
                    lease,
                    prepared,
                    first);

            Assert.False(result.Success);
            Assert.Equal(
                "wound_plan_effect_binding_mismatch",
                Assert.Single(result.Issues).Code);
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    [Fact]
    public void AcceptedTurnState_PreparerExceptionClearsEveryDependentAuthority()
    {
        var preparer = new CorruptingWoundPreparer();
        var state = AcceptedTurnStateHarness.Create(
            woundPlan: new WoundAcceptedTurnPlanCache(
                preparer.Build,
                WoundAcceptedTurnPlanner.Finalize));
        var detachedPrepared = AssertPrepared(
            WoundAcceptedTurnPlanner.Prepare(WoundInput()));
        var effectInput = BuildEmptyEffectStage(detachedPrepared).Plan!.EffectInput;
        Assert.True(state.GetOrBuildEffectValidated(effectInput).Success);
        Assert.True(state.GetOrBuildCommonValidated(ValidCommonInput()).Success);
        preparer.CorruptNext = "throw";

        Assert.Throws<InvalidOperationException>(() =>
            state.GetOrBuildWoundPrepared(WoundInput()));

        Assert.False(state.TryPeekWoundPrepared(out _));
        Assert.False(state.TryPeekWoundFinal(out _));
        Assert.False(state.TryPeekEffectValidated(out _));
        Assert.False(state.TryPeekCommonValidated(out _, out _));
    }

    [Fact]
    public void AcceptedTurnState_FinalizerExceptionClearsEveryDependentAuthority()
    {
        var finalizer = new CorruptingWoundFinalizer();
        var state = AcceptedTurnStateHarness.Create(
            woundPlan: new WoundAcceptedTurnPlanCache(
                WoundAcceptedTurnPlanner.Prepare,
                finalizer.Build));
        var prepared = AssertPrepared(
            state.GetOrBuildWoundPrepared(WoundInput()));
        var effectInput = BuildEmptyEffectStage(prepared).Plan!.EffectInput;
        var effect = state.GetOrBuildWoundEffectValidated(
            prepared,
            effectInput);
        Assert.True(effect.Success);
        Assert.True(state.GetOrBuildCommonValidated(ValidCommonInput()).Success);
        finalizer.CorruptNext = "throw";

        Assert.Throws<InvalidOperationException>(() =>
            state.GetOrBuildWoundFinal(prepared, effect));

        Assert.False(state.TryPeekWoundPrepared(out _));
        Assert.False(state.TryPeekWoundFinal(out _));
        Assert.False(state.TryPeekEffectValidated(out _));
        Assert.False(state.TryPeekCommonValidated(out _, out _));
    }

    [Fact]
    public void AcceptedTurnState_EffectPlannerExceptionClearsDependentAuthority()
    {
        var planner = new FaultingEffectPlanner();
        var effectCache = new EffectAcceptedTurnPlanCache(
            new EffectIdentityFactory(),
            planner.Build,
            planner.BuildWound);
        var state = AcceptedTurnStateHarness.Create(
            effectPlan: effectCache);
        var prepared = AssertPrepared(
            state.GetOrBuildWoundPrepared(WoundInput()));
        var effectInput = BuildEmptyEffectStage(prepared).Plan!.EffectInput;
        var effect = state.GetOrBuildWoundEffectValidated(
            prepared,
            effectInput);
        Assert.True(effect.Success);
        Assert.True(state.GetOrBuildWoundFinal(prepared, effect).Success);
        Assert.True(state.GetOrBuildCommonValidated(ValidCommonInput()).Success);
        state.RegisterEmptyMortalItems(
            prepared.Binding.SessionId,
            prepared.Binding.SnapshotToken);
        Assert.True(state.HasMortalItemsValidated());
        planner.ThrowNextWound = true;

        Assert.Throws<InvalidOperationException>(() =>
            state.GetOrBuildWoundEffectValidated(
                prepared,
                effectInput with
                {
                    RawCommands = new JsonObject { ["changed"] = true }
                }));

        Assert.False(state.TryPeekWoundPrepared(out _));
        Assert.False(state.TryPeekWoundFinal(out _));
        Assert.False(state.TryPeekEffectValidated(out _));
        Assert.False(state.TryPeekCommonValidated(out _, out _));
        Assert.False(state.HasMortalItemsValidated());
    }

    [Fact]
    public void AcceptedTurnState_CommonPlannerExceptionClearsEveryAuthority()
    {
        var planner = new FaultingCommonPlanner();
        var state = AcceptedTurnStateHarness.Create(
            commonPlan: new AcceptedMechanicsPlanCache(planner.Build));
        var prepared = AssertPrepared(
            state.GetOrBuildWoundPrepared(WoundInput()));
        var effectInput = BuildEmptyEffectStage(prepared).Plan!.EffectInput;
        var effect = state.GetOrBuildWoundEffectValidated(
            prepared,
            effectInput);
        Assert.True(effect.Success);
        Assert.True(state.GetOrBuildWoundFinal(prepared, effect).Success);
        var commonInput = ValidCommonInput();
        Assert.True(state.GetOrBuildCommonValidated(commonInput).Success);
        state.RegisterEmptyMortalItems(
            prepared.Binding.SessionId,
            prepared.Binding.SnapshotToken);
        Assert.True(state.HasMortalItemsValidated());
        planner.ThrowNext = true;

        Assert.Throws<InvalidOperationException>(() =>
            state.GetOrBuildCommonValidated(ValidCommonInput("request")));

        Assert.False(state.TryPeekWoundPrepared(out _));
        Assert.False(state.TryPeekWoundFinal(out _));
        Assert.False(state.TryPeekEffectValidated(out _));
        Assert.False(state.TryPeekCommonValidated(out _, out _));
        Assert.False(state.HasMortalItemsValidated());
    }

    [Fact]
    public void AcceptedTurnState_FinalizationRechecksCurrentEffectCache()
    {
        var effectCache = new EffectAcceptedTurnPlanCache();
        var state = AcceptedTurnStateHarness.Create(
            effectPlan: effectCache);
        var prepared = AssertPrepared(
            state.GetOrBuildWoundPrepared(WoundInput()));
        var effectInput = BuildEmptyEffectStage(prepared).Plan!.EffectInput;
        var effect = state.GetOrBuildWoundEffectValidated(
            prepared,
            effectInput);
        Assert.True(effect.Success);

        effectCache.InvalidateValidated();
        var result = state.GetOrBuildWoundFinal(prepared, effect);

        Assert.False(result.Success);
        Assert.Equal(
            "wound_plan_effect_binding_mismatch",
            Assert.Single(result.Issues).Code);
        Assert.False(state.TryPeekWoundPrepared(out _));
        Assert.False(state.TryPeekWoundFinal(out _));
        Assert.False(state.TryPeekEffectValidated(out _));
    }

    [Fact]
    public async Task AcceptedTurnRegistry_ForeignFinalizationHandoffClearsAllDependentStages()
    {
        var root = CreateAuthorityRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await using var lease =
                await fileSystem.AcquireCanonicalWriteLeaseAsync();
            SeedAcceptedTurnAuthority(fileSystem, lease);
            var foreignCache = new WoundAcceptedTurnPlanCache();
            var foreignPrepared = AssertPrepared(
                foreignCache.GetOrBuildPrepared(WoundInput()));

            var result =
                WoundAcceptedTurnPlanAuthority.GetOrBuildFinalValidated(
                    fileSystem,
                    lease,
                    foreignPrepared,
                    BuildEmptyEffectStage(foreignPrepared));

            Assert.False(result.Success);
            Assert.Equal(
                "wound_plan_stale_generation",
                Assert.Single(result.Issues).Code);
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                fileSystem,
                lease,
                out _));
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekFinal(
                fileSystem,
                lease,
                out _));
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
                fileSystem,
                lease,
                out _,
                out _));
        }
        finally
        {
            DeleteAuthorityRoot(root);
        }
    }

    private static WoundAcceptedTurnInput WoundInput(
        string requestId = "request_wound_cache")
    {
        const int turn = 42;
        var accepted = new WoundAcceptedEventAuthority(
            "turn_42:accepted:wound-cache",
            "accepted_turn",
            "authority_wound_cache",
            WoundFingerprint("accepted-event:wound-cache"));
        var events = new[] { accepted };
        return new WoundAcceptedTurnInput(
            new WoundAcceptedTurnBinding(
                "session_wound_cache",
                requestId,
                "snapshot_wound_cache",
                "mortal_world",
                turn,
                events,
                WoundAcceptedEventSetFingerprint.Compute(events)),
            Array.Empty<WoundOpportunityAuthority>(),
            Array.Empty<WoundAcceptedTransitionDraft>(),
            new WoundCarrierCatalogInput(
                WoundContractTestData.CreatePlayerCarrier(),
                null,
                null,
                null,
                null),
            WoundContractTestData.CreateIdentityIndex(),
            WoundContractTestData.CreateHistory());
    }

    private static WoundAcceptedTurnInput MutateWoundInput(string mutation)
    {
        var input = WoundInput();
        if (mutation == "carrier")
        {
            return input with
            {
                PreTurnCarriers = input.PreTurnCarriers with
                {
                    PlayerWounds = null
                }
            };
        }

        var binding = input.Binding;
        if (mutation is "event_ref" or "event_fingerprint")
        {
            var accepted = binding.AcceptedEvents.Single();
            accepted = mutation == "event_ref"
                ? accepted with { EventRef = "turn_42:accepted:wound-cache-changed" }
                : accepted with
                {
                    SemanticFingerprint = WoundFingerprint(
                        "accepted-event:wound-cache-changed")
                };
            var events = new[] { accepted };
            return input with
            {
                Binding = binding with
                {
                    AcceptedEvents = events,
                    AcceptedEventsFingerprint =
                        WoundAcceptedEventSetFingerprint.Compute(events)
                }
            };
        }

        return input with
        {
            Binding = mutation switch
            {
                "session" => binding with { SessionId = "session_wound_changed" },
                "request" => binding with { RequestId = "request_wound_changed" },
                "snapshot" => binding with
                {
                    SnapshotToken = "snapshot_wound_changed"
                },
                "turn" => binding with { Turn = binding.Turn + 1 },
                _ => throw new InvalidOperationException(
                    "Unknown wound input mutation.")
            }
        };
    }

    private static WoundAcceptedTurnInput WoundInputWithTwoEvents(bool reverse)
    {
        var input = WoundInput();
        var first = input.Binding.AcceptedEvents.Single();
        var second = new WoundAcceptedEventAuthority(
            "turn_42:accepted:wound-cache-second",
            "accepted_turn",
            "authority_wound_cache_second",
            WoundFingerprint("accepted-event:wound-cache-second"));
        var events = reverse
            ? new[] { second, first }
            : new[] { first, second };
        return input with
        {
            Binding = input.Binding with
            {
                AcceptedEvents = events,
                AcceptedEventsFingerprint =
                    WoundAcceptedEventSetFingerprint.Compute(events)
            }
        };
    }

    private static WoundAcceptedTurnInput MutateCompleteWoundInput(
        WoundAcceptedTurnInput input,
        string mutation)
    {
        if (mutation == "realm")
        {
            return input with
            {
                Binding = input.Binding with { Realm = "afterlife" }
            };
        }
        if (mutation == "opportunity")
        {
            var opportunities = input.Opportunities.ToArray();
            opportunities[0] = opportunities[0] with
            {
                SourceId = opportunities[0].SourceId + "_changed",
                AuthorityFingerprint = WoundFingerprint(
                    "changed-opportunity-authority")
            };
            return input with { Opportunities = opportunities };
        }
        if (mutation == "identity")
        {
            return input with
            {
                PreTurnIdentityIndex = WoundContractTestData.CreateIdentityIndex(
                    WoundContractTestData.CreateIdentityEntry(
                        woundId: "wound_changed_cache_identity"))
            };
        }
        if (mutation == "history")
        {
            var history = input.PreTurnHistory;
            history["nextOrdinal"] = 2;
            return input with { PreTurnHistory = history };
        }

        var transitions = input.Transitions.ToArray();
        var transition = transitions[0];
        transitions[0] = mutation switch
        {
            "proposal" => transition with
            {
                ProposedAfter = transition.ProposedAfter with
                {
                    Display = transition.ProposedAfter.Display with
                    {
                        Name = transition.ProposedAfter.Display.Name +
                            " changed"
                    }
                }
            },
            "definition" => MutateDefinition(transition),
            "root" => MutateRoot(transition),
            "slot" => transition with
            {
                SlotBindings = transition.SlotBindings.Select(
                    static value => value with
                    {
                        ReadableSummary = value.ReadableSummary + " changed"
                    }).ToArray()
            },
            _ => throw new InvalidOperationException(
                "Unknown complete wound-input mutation.")
        };
        return input with { Transitions = transitions };
    }

    private static WoundAcceptedTransitionDraft MutateDefinition(
        WoundAcceptedTransitionDraft transition)
    {
        var definitions = transition.EffectDefinitions.ToArray();
        var definition = definitions[0].Definition;
        definition["displayName"] = "Changed cached wound definition";
        definitions[0] = definitions[0] with { Definition = definition };
        return transition with { EffectDefinitions = definitions };
    }

    private static WoundAcceptedTransitionDraft MutateRoot(
        WoundAcceptedTransitionDraft transition)
    {
        var roots = transition.RootApplications.ToArray();
        var oldRef = roots[0].LocalApplicationRef;
        var changedRef = oldRef + "_changed";
        roots[0] = roots[0] with { LocalApplicationRef = changedRef };
        var slots = transition.SlotBindings.Select(value =>
            string.Equals(
                value.LocalApplicationRef,
                oldRef,
                StringComparison.Ordinal)
                ? value with { LocalApplicationRef = changedRef }
                : value).ToArray();
        return transition with
        {
            RootApplications = roots,
            SlotBindings = slots
        };
    }

    private static WoundEffectBatchPlanningResult BuildEmptyEffectStage(
        WoundPreparedAcceptedTurnPlan prepared)
    {
        var sourceAuthority = EffectSourceAuthority.Build(
            new EffectSourceAuthorityInput(
                Array.Empty<EffectSourceExport>(),
                Array.Empty<EffectSourceExport>(),
                new HashSet<string>(StringComparer.Ordinal)));
        var targetAuthority = EffectTargetAuthority.Build(
            new EffectTargetAuthorityInput(
                Array.Empty<EffectTargetExport>(),
                Array.Empty<EffectTargetExport>(),
                new HashSet<string>(StringComparer.Ordinal),
                CombatantIdentities: null));
        var effectInput = new EffectAcceptedTurnInput(
            prepared.Binding.SessionId,
            prepared.Binding.SnapshotToken,
            EffectMaterializationTestFixture.CreateCommandRoot(),
            sourceAuthority,
            targetAuthority,
            new JsonObject
            {
                ["turn"] = prepared.Binding.Turn,
                ["events"] = new JsonArray(prepared.Binding.AcceptedEvents.Select(
                    static value => (JsonNode)new JsonObject
                    {
                        ["eventRef"] = value.EventRef,
                        ["kind"] = value.Kind,
                        ["authorityId"] = value.AuthorityId
                    }).ToArray())
            },
            prepared.Binding.Realm,
            PreTurnIdentityIndex: new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            });
        return WoundEffectBatchPlanner.Build(
            prepared,
            effectInput,
            new EffectIdentityFactory());
    }

    private static WoundPreparedAcceptedTurnPlan AssertPrepared(
        WoundAcceptedTurnPreparationResult result)
    {
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        return Assert.IsType<WoundPreparedAcceptedTurnPlan>(result.Plan);
    }

    private static string WoundFingerprint(string value) =>
        "sha256:" + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private sealed class CountingWoundPreparer
    {
        internal int Calls { get; private set; }
        internal bool FailNext { get; set; }

        internal WoundAcceptedTurnPreparationResult Build(
            WoundAcceptedTurnInput input)
        {
            Calls++;
            if (FailNext)
            {
                FailNext = false;
                return new WoundAcceptedTurnPreparationResult(
                    null,
                    new[] { WoundIssue("wound_cache_injected_prepare_failure") });
            }
            return WoundAcceptedTurnPlanner.Prepare(input);
        }
    }

    private sealed class CountingWoundFinalizer
    {
        internal int Calls { get; private set; }

        internal WoundAcceptedTurnPlanningResult Build(
            WoundPreparedAcceptedTurnPlan prepared,
            WoundEffectBatchPlanningResult effectResult)
        {
            Calls++;
            return WoundAcceptedTurnPlanner.Finalize(prepared, effectResult);
        }
    }

    private sealed class CorruptingWoundPreparer
    {
        internal string? CorruptNext { get; set; }

        internal WoundAcceptedTurnPreparationResult Build(
            WoundAcceptedTurnInput input)
        {
            var corruption = CorruptNext;
            CorruptNext = null;
            if (corruption == "throw")
                throw new InvalidOperationException("Injected wound preparer failure.");
            if (corruption == "null")
                return null!;

            var result = WoundAcceptedTurnPlanner.Prepare(input);
            if (corruption == null)
                return result;
            var plan = result.Plan!;
            return corruption switch
            {
                "partial" => new WoundAcceptedTurnPreparationResult(
                    plan,
                    new[] { WoundIssue("wound_cache_injected_partial") }),
                "empty" => new WoundAcceptedTurnPreparationResult(
                    null,
                    Array.Empty<ValidationIssue>()),
                "fingerprint" => new WoundAcceptedTurnPreparationResult(
                    ClonePrepared(plan, inputFingerprint: "sha256:forged"),
                    Array.Empty<ValidationIssue>()),
                "baseline" => new WoundAcceptedTurnPreparationResult(
                    ClonePrepared(
                        plan,
                        baselineAuthority: CorruptBaseline(plan.BaselineAuthority)),
                    Array.Empty<ValidationIssue>()),
                "baseline_resealed" => new WoundAcceptedTurnPreparationResult(
                    ClonePrepared(
                        plan,
                        baselineAuthority: CorruptBaseline(
                            plan.BaselineAuthority,
                            reseal: true)),
                    Array.Empty<ValidationIssue>()),
                "prepared_payload_resealed" =>
                    new WoundAcceptedTurnPreparationResult(
                        CorruptPreparedPayloadAndReseal(plan),
                        Array.Empty<ValidationIssue>()),
                "source_payload_resealed" =>
                    new WoundAcceptedTurnPreparationResult(
                        CorruptPreparedSourceAndReseal(plan),
                        Array.Empty<ValidationIssue>()),
                _ => throw new InvalidOperationException(
                    "Unknown injected wound preparation corruption.")
            };
        }
    }

    private sealed class CorruptingWoundFinalizer
    {
        internal string? CorruptNext { get; set; }

        internal WoundAcceptedTurnPlanningResult Build(
            WoundPreparedAcceptedTurnPlan prepared,
            WoundEffectBatchPlanningResult effectResult)
        {
            var corruption = CorruptNext;
            CorruptNext = null;
            if (corruption == "throw")
                throw new InvalidOperationException("Injected wound finalizer failure.");
            if (corruption == "null")
                return null!;

            var result = WoundAcceptedTurnPlanner.Finalize(prepared, effectResult);
            if (corruption == null)
                return result;
            var plan = result.Plan!;
            return corruption switch
            {
                "partial" => new WoundAcceptedTurnPlanningResult(
                    plan,
                    new[] { WoundIssue("wound_cache_injected_partial") }),
                "empty" => new WoundAcceptedTurnPlanningResult(
                    null,
                    Array.Empty<ValidationIssue>()),
                "fingerprint" => new WoundAcceptedTurnPlanningResult(
                    CloneFinal(plan, finalFingerprint: "sha256:forged"),
                    Array.Empty<ValidationIssue>()),
                "payload_resealed" => new WoundAcceptedTurnPlanningResult(
                    CorruptFinalPayloadAndReseal(
                        prepared,
                        effectResult.Plan!,
                        plan),
                    Array.Empty<ValidationIssue>()),
                _ => throw new InvalidOperationException(
                    "Unknown injected wound finalization corruption.")
            };
        }
    }

    private sealed class FaultingEffectPlanner
    {
        internal bool ThrowNextWound { get; set; }

        internal EffectAcceptedTurnPlanningResult Build(
            EffectAcceptedTurnInput input,
            string fingerprint,
            EffectIdentityFactory identityFactory) =>
            EffectAcceptedTurnPlanner.Build(input, fingerprint, identityFactory);

        internal EffectAcceptedTurnPlanningResult BuildWound(
            EffectAcceptedTurnInput input,
            WoundPreparedAcceptedTurnPlan prepared,
            EffectIdentityFactory identityFactory)
        {
            if (ThrowNextWound)
            {
                ThrowNextWound = false;
                throw new InvalidOperationException(
                    "Injected wound effect planner failure.");
            }
            return EffectAcceptedTurnPlanner.BuildWoundBatch(
                input,
                prepared,
                identityFactory);
        }
    }

    private sealed class FaultingCommonPlanner
    {
        internal bool ThrowNext { get; set; }

        internal AcceptedMechanicsPlanningResult Build(
            AcceptedMechanicsInput input,
            string inputFingerprint)
        {
            if (ThrowNext)
            {
                ThrowNext = false;
                throw new InvalidOperationException(
                    "Injected common accepted-turn planner failure.");
            }
            return AcceptedMechanicsPlanner.BuildAcceptedPlan(
                input,
                inputFingerprint);
        }
    }

    private sealed class AcceptedTurnStateHarness
    {
        private readonly object _state;
        private readonly Type _stateType;

        private AcceptedTurnStateHarness(object state, Type stateType)
        {
            _state = state;
            _stateType = stateType;
        }

        internal static AcceptedTurnStateHarness Create(
            AcceptedMechanicsPlanCache? commonPlan = null,
            EffectAcceptedTurnPlanCache? effectPlan = null,
            WoundAcceptedTurnPlanCache? woundPlan = null)
        {
            var stateType = typeof(AcceptedTurnAuthorityRegistry).GetNestedType(
                "AcceptedTurnAuthorityState",
                BindingFlags.NonPublic) ??
                throw new InvalidOperationException(
                    "Accepted-turn authority state type was not found.");
            var constructor = stateType.GetConstructors(
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(value => value.GetParameters().Length == 4);
            var state = constructor.Invoke(new object?[]
            {
                commonPlan,
                effectPlan,
                woundPlan,
                null
            });
            return new AcceptedTurnStateHarness(state, stateType);
        }

        internal AcceptedMechanicsPlanningResult GetOrBuildCommonValidated(
            AcceptedMechanicsInput input) =>
            Invoke<AcceptedMechanicsPlanningResult>(
                "GetOrBuildCommonValidated",
                input);

        internal EffectAcceptedTurnPlanningResult GetOrBuildEffectValidated(
            EffectAcceptedTurnInput input) =>
            Invoke<EffectAcceptedTurnPlanningResult>(
                "GetOrBuildEffectValidated",
                input);

        internal WoundAcceptedTurnPreparationResult GetOrBuildWoundPrepared(
            WoundAcceptedTurnInput input) =>
            Invoke<WoundAcceptedTurnPreparationResult>(
                "GetOrBuildWoundPrepared",
                input);

        // Reflection needs explicit defaults; ordinary stages carry neither authority.
        internal WoundEffectBatchPlanningResult GetOrBuildWoundEffectValidated(
            WoundPreparedAcceptedTurnPlan prepared,
            EffectAcceptedTurnInput input) =>
            Invoke<WoundEffectBatchPlanningResult>(
                "GetOrBuildWoundEffectValidated",
                prepared,
                input,
                null,
                null);

        internal WoundAcceptedTurnPlanningResult GetOrBuildWoundFinal(
            WoundPreparedAcceptedTurnPlan prepared,
            WoundEffectBatchPlanningResult effect) =>
            Invoke<WoundAcceptedTurnPlanningResult>(
                "GetOrBuildWoundFinal",
                prepared,
                effect,
                null,
                null);

        internal void RegisterEmptyMortalItems(
            string sessionId,
            string snapshotToken)
        {
            var emptyProjectionRoots =
                MortalItemCanonicalProjectionPlanner.ProjectionRootPaths.ToDictionary(
                    static path => path,
                    static _ => (JsonNode?)null,
                    StringComparer.Ordinal);
            Invoke<object?>(
                "RegisterMortalItemsValidated",
                sessionId,
                snapshotToken,
                "sha256:wound-cache-test-mortal-items",
                Array.Empty<MortalItemAcceptedTurnAuthority.NewCandidate>(),
                Array.Empty<MortalItemAcceptedTurnAuthority.StableCandidate>(),
                Array.Empty<string>(),
                new Dictionary<string, MortalItemRouteAuthority>(StringComparer.Ordinal),
                Array.Empty<MortalItemAcceptedTransfer>(),
                emptyProjectionRoots,
                emptyProjectionRoots);
        }

        internal bool HasMortalItemsValidated() =>
            Invoke<bool>("HasMortalItemsValidated");

        internal bool TryPeekWoundPrepared(
            out WoundAcceptedTurnPreparationResult result)
        {
            object?[] arguments = { null };
            var found = Invoke<bool>("TryPeekWoundPrepared", arguments);
            result = (WoundAcceptedTurnPreparationResult?)arguments[0] ?? null!;
            return found;
        }

        internal bool TryPeekWoundFinal(
            out WoundAcceptedTurnPlanningResult result)
        {
            object?[] arguments = { null };
            var found = Invoke<bool>("TryPeekWoundFinal", arguments);
            result = (WoundAcceptedTurnPlanningResult?)arguments[0] ?? null!;
            return found;
        }

        internal bool TryPeekEffectValidated(
            out EffectAcceptedTurnPlanningResult result)
        {
            object?[] arguments = { null };
            var found = Invoke<bool>("TryPeekEffectValidated", arguments);
            result = (EffectAcceptedTurnPlanningResult?)arguments[0] ?? null!;
            return found;
        }

        internal bool TryPeekCommonValidated(
            out AcceptedMechanicsPlanBinding binding,
            out AcceptedMechanicsPlanningResult result)
        {
            object?[] arguments = { null, null };
            var found = Invoke<bool>("TryPeekCommonValidated", arguments);
            binding = (AcceptedMechanicsPlanBinding?)arguments[0] ?? null!;
            result = (AcceptedMechanicsPlanningResult?)arguments[1] ?? null!;
            return found;
        }

        private T Invoke<T>(string name, params object?[] arguments)
        {
            var method = _stateType.GetMethods(
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(value =>
                    string.Equals(value.Name, name, StringComparison.Ordinal) &&
                    value.GetParameters().Length == arguments.Length);
            try
            {
                return (T)method.Invoke(_state, arguments)!;
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(exception.InnerException)
                    .Throw();
                throw;
            }
        }
    }

    private static WoundPreparedAcceptedTurnPlan CorruptPreparedPayloadAndReseal(
        WoundPreparedAcceptedTurnPlan plan)
    {
        var wounds = plan.PreparedWounds.ToArray();
        var wound = Assert.Single(wounds);
        wounds[0] = wound with
        {
            Display = wound.Display with
            {
                Name = wound.Display.Name + " (forged)"
            }
        };
        return ResealPrepared(plan, wounds, plan.EffectOperationBatches);
    }

    private static WoundPreparedAcceptedTurnPlan CorruptPreparedSourceAndReseal(
        WoundPreparedAcceptedTurnPlan plan)
    {
        var batches = plan.EffectOperationBatches.ToArray();
        var batch = Assert.Single(batches);
        var export = batch.SourceExport;
        var definitions = export.Definitions.ToArray();
        var definition = definitions[0].Definition;
        definition["displayName"] = "Forged cached source definition";
        definitions[0] = new WoundEffectSourceDefinition(
            definitions[0].DefinitionKey,
            definition);
        var changedExport = new WoundEffectSourceExport(
            export.SchemaVersion,
            export.Kind,
            export.SourceId,
            export.SourceRef,
            export.State,
            export.Materializable,
            export.Realm,
            export.Owner,
            export.CausalEventRef,
            export.EventSemanticFingerprint,
            export.OpportunityId,
            export.OpportunityAuthorityFingerprint,
            definitions);
        var provisionalBatch = new WoundEffectOperationBatch(
            batch.LocalWoundRef,
            batch.PreparedWoundId,
            changedExport,
            batch.RootApplications,
            batch.TerminalOperations,
            batch.RootLineageAuthority,
            string.Empty,
            batch.TransitionAuthority);
        batches[0] = new WoundEffectOperationBatch(
            provisionalBatch.LocalWoundRef,
            provisionalBatch.PreparedWoundId,
            provisionalBatch.SourceExport,
            provisionalBatch.RootApplications,
            provisionalBatch.TerminalOperations,
            provisionalBatch.RootLineageAuthority,
            WoundAcceptedTurnFingerprints.ComputeSourceExport(provisionalBatch),
            provisionalBatch.TransitionAuthority);
        return ResealPrepared(plan, plan.PreparedWounds, batches);
    }

    private static WoundPreparedAcceptedTurnPlan ResealPrepared(
        WoundPreparedAcceptedTurnPlan plan,
        IReadOnlyList<WoundMaterializationEnvelope> wounds,
        IReadOnlyList<WoundEffectOperationBatch> batches)
    {
        var provisional = new WoundPreparedAcceptedTurnPlan(
            plan.Binding,
            plan.BindingFingerprint,
            plan.InputFingerprint,
            string.Empty,
            plan.AllocatedWoundIds,
            plan.AllocatedTransitionIds,
            wounds,
            batches,
            plan.BaselineAuthority);
        return new WoundPreparedAcceptedTurnPlan(
            provisional.Binding,
            provisional.BindingFingerprint,
            provisional.InputFingerprint,
            WoundAcceptedTurnFingerprints.ComputePreparation(provisional),
            provisional.AllocatedWoundIds,
            provisional.AllocatedTransitionIds,
            provisional.PreparedWounds,
            provisional.EffectOperationBatches,
            provisional.BaselineAuthority);
    }

    private static WoundAcceptedTurnPlan CorruptFinalPayloadAndReseal(
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchAcceptedPlan effectPlan,
        WoundAcceptedTurnPlan plan)
    {
        var history = plan.HistoryAfterImage;
        history["forged"] = true;
        var finalFingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(
            prepared,
            effectPlan,
            plan.CarrierContributions,
            plan.IdentityIndexAfterImage,
            history,
            plan.TransitionIntents);
        return new WoundAcceptedTurnPlan(
            plan.Binding,
            plan.BindingFingerprint,
            plan.InputFingerprint,
            plan.WoundPreparationFingerprint,
            plan.EffectInputFingerprint,
            plan.EffectAcceptedTurnPlanFingerprint,
            finalFingerprint,
            plan.AllocatedWoundIds,
            plan.AllocatedTransitionIds,
            plan.CarrierContributions,
            plan.IdentityIndexAfterImage,
            history,
            plan.TransitionIntents);
    }

    private static WoundPreparedAcceptedTurnPlan ClonePrepared(
        WoundPreparedAcceptedTurnPlan plan,
        string? inputFingerprint = null,
        WoundPreparedBaselineAuthority? baselineAuthority = null) =>
        new(
            plan.Binding,
            plan.BindingFingerprint,
            inputFingerprint ?? plan.InputFingerprint,
            plan.WoundPreparationFingerprint,
            plan.AllocatedWoundIds,
            plan.AllocatedTransitionIds,
            plan.PreparedWounds,
            plan.EffectOperationBatches,
            baselineAuthority ?? plan.BaselineAuthority);

    private static WoundPreparedBaselineAuthority CorruptBaseline(
        WoundPreparedBaselineAuthority baseline,
        bool reseal = false)
    {
        var identity = baseline.PreTurnIdentityIndex;
        identity["forged"] = true;
        var seal = reseal
            ? WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(
                baseline.PreparedInputFingerprint,
                baseline.PreTurnCarriers,
                identity,
                baseline.PreTurnHistory)
            : baseline.AuthoritySeal;
        return new WoundPreparedBaselineAuthority(
            baseline.PreparedInputFingerprint,
            baseline.PreTurnCarriers,
            identity,
            baseline.PreTurnHistory,
            seal);
    }

    private static WoundAcceptedTurnPlan CloneFinal(
        WoundAcceptedTurnPlan plan,
        string? finalFingerprint = null) =>
        new(
            plan.Binding,
            plan.BindingFingerprint,
            plan.InputFingerprint,
            plan.WoundPreparationFingerprint,
            plan.EffectInputFingerprint,
            plan.EffectAcceptedTurnPlanFingerprint,
            finalFingerprint ?? plan.WoundFinalPlanFingerprint,
            plan.AllocatedWoundIds,
            plan.AllocatedTransitionIds,
            plan.CarrierContributions,
            plan.IdentityIndexAfterImage,
            plan.HistoryAfterImage,
            plan.TransitionIntents);

    private static ValidationIssue WoundIssue(string code) => new(
        "game_state/wounds",
        IssueSeverity.Error,
        "Injected wound cache failure.",
        code,
        section: "wounds");

    private static string CreateAuthorityRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "boe-wound-authority-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static FileSystemManager CreateFileSystem(string root)
    {
        var fileSystem = new FileSystemManager(
            root,
            NullLogger<FileSystemManager>.Instance);
        fileSystem.EnsureDirectoryStructure();
        return fileSystem;
    }

    private static AcceptedMechanicsInput SeedAcceptedTurnAuthority(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease)
    {
        var prepared = AssertPrepared(
            WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                fileSystem,
                lease,
                WoundInput()));
        var effectInput = BuildEmptyEffectStage(prepared).Plan!.EffectInput;
        var woundEffect =
            WoundAcceptedTurnPlanAuthority.GetOrBuildEffectValidated(
                fileSystem,
                lease,
                prepared,
                effectInput);
        Assert.True(woundEffect.Success);
        Assert.True(WoundAcceptedTurnPlanAuthority.GetOrBuildFinalValidated(
            fileSystem,
            lease,
            prepared,
            woundEffect).Success);
        var commonInput = ValidCommonInput();
        var common = AcceptedMechanicsPlanAuthority.GetOrBuildValidated(
            fileSystem,
            lease,
            commonInput);
        Assert.True(
            common.Success,
            string.Join(Environment.NewLine, common.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        AcceptedTurnAuthorityRegistry.RegisterMortalItemsValidated(
            fileSystem,
            lease,
            prepared.Binding.SessionId,
            prepared.Binding.SnapshotToken,
            "sha256:wound-cache-seeded-mortal-items",
            Array.Empty<MortalItemAcceptedTurnAuthority.NewCandidate>(),
            Array.Empty<MortalItemAcceptedTurnAuthority.StableCandidate>(),
            Array.Empty<string>(),
            new Dictionary<string, MortalItemRouteAuthority>(StringComparer.Ordinal),
            Array.Empty<MortalItemAcceptedTransfer>(),
            MortalItemCanonicalProjectionPlanner.ProjectionRootPaths.ToDictionary(
                static path => path,
                static _ => (JsonNode?)null,
                StringComparer.Ordinal),
            MortalItemCanonicalProjectionPlanner.ProjectionRootPaths.ToDictionary(
                static path => path,
                static _ => (JsonNode?)null,
                StringComparer.Ordinal));
        Assert.True(AcceptedTurnAuthorityRegistry.HasMortalItemsValidated(
            fileSystem,
            lease));
        return commonInput;
    }

    private static AcceptedMechanicsInput ValidCommonInput(
        string? mutation = null)
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var history = ResourceHistoryState.CreateValidated(
            Array.Empty<ResourceTransition>(),
            definitions).History!;
        var owners = ResourceOwnerAuthority.Build(
            new ResourceOwnerAuthorityInput(
                Array.Empty<ResourceOwnerExport>(),
                Array.Empty<ResourceOwnerExport>(),
                Array.Empty<ResourceOwnerKey>()));
        var sources = ResourceMutationSourceCatalog.Create(
            Array.Empty<ResourceMutationSourceExport>()).Catalog!;
        var commands = ResourceAcceptedTurnInputComposer.Parse(null);
        var context = new AcceptedMechanicsPlanningContext(
            definitions.ToCanonicalRoot(),
            definitions,
            new ResourceStateLedger(Array.Empty<ResourceStateEntry>()),
            history,
            owners,
            sources,
            commands,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            },
            effectPlan: null);
        var beforeImages = BeforeImages(new byte[] { 4, 5, 6 });
        beforeImages[CanonicalResourceOwnerAuthorityComposer.AuthorityPath] =
            new CanonicalBeforeImage(true, new byte[] { 7 });
        return Input(
            mutation,
            resourceCommands: commands.Root,
            beforeImages: beforeImages,
            planningContext: context);
    }

    private static void DeleteAuthorityRoot(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        Assert.StartsWith(
            tempRoot,
            fullRoot,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "boe-wound-authority-",
            Path.GetFileName(fullRoot),
            StringComparison.Ordinal);
        if (Directory.Exists(fullRoot))
            Directory.Delete(fullRoot, recursive: true);
    }
}
