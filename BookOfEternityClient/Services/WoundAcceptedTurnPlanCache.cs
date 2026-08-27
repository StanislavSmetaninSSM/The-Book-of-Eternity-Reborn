namespace BookOfEternityClient.Services;

internal delegate WoundAcceptedTurnPreparationResult WoundAcceptedTurnPreparationFactory(
    WoundAcceptedTurnInput input);

internal delegate WoundAcceptedTurnPlanningResult WoundAcceptedTurnFinalizationFactory(
    WoundPreparedAcceptedTurnPlan prepared,
    WoundEffectBatchPlanningResult effectResult);

internal sealed class WoundAcceptedTurnPlanCache
{
    private const string CachePath = "game_state/wounds/accepted_turn_plan";

    private readonly object _gate = new();
    private readonly object _authorityStateToken = new();
    private readonly WoundAcceptedTurnPreparationFactory _preparer;
    private readonly WoundAcceptedTurnFinalizationFactory _finalizer;
    private string? _preparedInputFingerprint;
    private string? _preparedFingerprint;
    private object? _preparedStageToken;
    private WoundAcceptedTurnPreparationResult? _preparedResult;
    private string? _finalPreparedFingerprint;
    private string? _finalEffectFingerprint;
    private WoundAcceptedTurnPlanningResult? _finalResult;

    internal WoundAcceptedTurnPlanCache()
        : this(
            WoundAcceptedTurnPlanner.Prepare,
            WoundAcceptedTurnPlanner.Finalize)
    {
    }

    internal WoundAcceptedTurnPlanCache(
        WoundAcceptedTurnPreparationFactory preparer,
        WoundAcceptedTurnFinalizationFactory finalizer)
    {
        _preparer = preparer ?? throw new ArgumentNullException(nameof(preparer));
        _finalizer = finalizer ?? throw new ArgumentNullException(nameof(finalizer));
    }

    internal bool HasPrepared
    {
        get
        {
            lock (_gate)
                return _preparedResult is not null;
        }
    }

    internal bool HasFinal
    {
        get
        {
            lock (_gate)
                return _finalResult is not null;
        }
    }

    internal WoundAcceptedTurnPreparationResult GetOrBuildPrepared(
        WoundAcceptedTurnInput input) =>
        GetOrBuildPrepared(input, out _);

    internal WoundAcceptedTurnPreparationResult GetOrBuildPrepared(
        WoundAcceptedTurnInput input,
        out bool reused)
    {
        ArgumentNullException.ThrowIfNull(input);
        reused = false;
        lock (_gate)
        {
            WoundAcceptedTurnPlannerCore.ValidatedInput validation;
            string inputFingerprint;
            try
            {
                validation = WoundAcceptedTurnPlannerCore.ValidateInput(input);
                if (validation.Issues.Count != 0)
                {
                    InvalidateAllCore();
                    return new WoundAcceptedTurnPreparationResult(
                        null,
                        validation.Issues);
                }
                inputFingerprint = WoundAcceptedTurnFingerprints.ComputeInput(input);
            }
            catch (Exception exception) when (IsMalformedBoundary(exception))
            {
                InvalidateAllCore();
                return FailedPreparation(
                    "wound_plan_input_invalid",
                    "complete well-formed accepted wound input",
                    exception.GetType().Name);
            }

            if (_preparedResult is not null &&
                string.Equals(
                    _preparedInputFingerprint,
                    inputFingerprint,
                    StringComparison.Ordinal))
            {
                reused = true;
                return Detach(_preparedResult);
            }

            InvalidateAllCore();
            WoundAcceptedTurnPreparationResult result;
            try
            {
                result = _preparer(WoundAcceptedTurnData.CloneInput(input)!) ??
                    throw new InvalidOperationException(
                        "Wound accepted-turn preparer returned null.");
            }
            catch
            {
                InvalidateAllCore();
                throw;
            }

            var validated = ValidatePreparedResult(input, inputFingerprint, result);
            if (!validated.Success)
            {
                InvalidateAllCore();
                return validated;
            }

            var stageToken = new object();
            var plan = validated.Plan!.BindToCacheAuthority(
                _authorityStateToken,
                stageToken);
            validated = new WoundAcceptedTurnPreparationResult(
                plan,
                Array.Empty<ValidationIssue>());
            _preparedInputFingerprint = inputFingerprint;
            _preparedFingerprint = plan.WoundPreparationFingerprint;
            _preparedStageToken = stageToken;
            _preparedResult = Detach(validated);
            return Detach(_preparedResult);
        }
    }

    internal WoundAcceptedTurnPlanningResult GetOrBuildFinal(
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchPlanningResult effectResult) =>
        GetOrBuildFinal(prepared, effectResult, out _);

    internal WoundAcceptedTurnPlanningResult GetOrBuildFinal(
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchPlanningResult effectResult,
        out bool reused)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(effectResult);
        reused = false;
        lock (_gate)
        {
            if (!prepared.BelongsToCacheAuthorityState(_authorityStateToken))
            {
                InvalidateFinalCore();
                var code = prepared.HasCacheAuthorityState
                    ? "wound_plan_stale_generation"
                    : "wound_plan_prepared_binding_mismatch";
                return FailedFinal(
                    code,
                    "the current generation-scoped wound preparation",
                    "a preparation from another authority state");
            }
            if (!CurrentPreparedAgrees(prepared, out var preparationFingerprint))
            {
                InvalidateFinalCore();
                return FailedFinal(
                    "wound_plan_prepared_binding_mismatch",
                    _preparedFingerprint ?? "one current cached prepared plan",
                    SafePreparationFingerprint(prepared));
            }

            var acceptedEffect = ValidateEffectResult(prepared, effectResult);
            if (!acceptedEffect.Success)
            {
                InvalidateFinalCore();
                return new WoundAcceptedTurnPlanningResult(
                    null,
                    acceptedEffect.Issues);
            }

            var effectPlan = acceptedEffect.Plan!;
            var effectFingerprint = effectPlan.EffectAcceptedTurnPlanFingerprint;
            if (_finalResult is not null &&
                string.Equals(
                    _finalPreparedFingerprint,
                    preparationFingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    _finalEffectFingerprint,
                    effectFingerprint,
                    StringComparison.Ordinal))
            {
                reused = true;
                return Detach(_finalResult);
            }

            InvalidateFinalCore();
            WoundAcceptedTurnPlanningResult result;
            try
            {
                result = _finalizer(
                    WoundAcceptedTurnData.ClonePreparedPlan(prepared)!,
                    new WoundEffectBatchPlanningResult(
                        effectPlan,
                        acceptedEffect.Issues)) ??
                    throw new InvalidOperationException(
                        "Wound accepted-turn finalizer returned null.");
            }
            catch
            {
                InvalidateFinalCore();
                throw;
            }

            var validated = ValidateFinalResult(prepared, effectPlan, result);
            if (!validated.Success)
            {
                InvalidateFinalCore();
                return validated;
            }

            _finalPreparedFingerprint = preparationFingerprint;
            _finalEffectFingerprint = effectFingerprint;
            _finalResult = Detach(validated);
            return Detach(_finalResult);
        }
    }

    internal bool TryPeekPrepared(
        out WoundAcceptedTurnPreparationResult result)
    {
        lock (_gate)
        {
            if (_preparedResult is not null)
            {
                result = Detach(_preparedResult);
                return true;
            }
        }
        result = null!;
        return false;
    }

    internal bool IsCurrentPrepared(WoundPreparedAcceptedTurnPlan prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        lock (_gate)
            return GetPreparedMismatchCodeCore(prepared) is null;
    }

    internal string? GetPreparedMismatchCode(
        WoundPreparedAcceptedTurnPlan prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        lock (_gate)
            return GetPreparedMismatchCodeCore(prepared);
    }

    internal bool TryPeekFinal(out WoundAcceptedTurnPlanningResult result)
    {
        lock (_gate)
        {
            if (_finalResult is not null)
            {
                result = Detach(_finalResult);
                return true;
            }
        }
        result = null!;
        return false;
    }

    internal void InvalidateFinal()
    {
        lock (_gate)
            InvalidateFinalCore();
    }

    internal void InvalidateAll()
    {
        lock (_gate)
            InvalidateAllCore();
    }

    private bool CurrentPreparedAgrees(
        WoundPreparedAcceptedTurnPlan prepared,
        out string preparationFingerprint)
    {
        preparationFingerprint = string.Empty;
        if (_preparedResult is null ||
            _preparedFingerprint is null ||
            _preparedStageToken is null ||
            !prepared.BelongsToPreparedStage(
                _authorityStateToken,
                _preparedStageToken))
            return false;

        try
        {
            if (WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(prepared).Count != 0)
                return false;
            preparationFingerprint =
                WoundAcceptedTurnFingerprints.ComputePreparation(prepared);
            return string.Equals(
                       prepared.WoundPreparationFingerprint,
                       preparationFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       _preparedFingerprint,
                       preparationFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       _preparedInputFingerprint,
                       prepared.InputFingerprint,
                       StringComparison.Ordinal);
        }
        catch (Exception exception) when (IsMalformedBoundary(exception))
        {
            return false;
        }
    }

    private string? GetPreparedMismatchCodeCore(
        WoundPreparedAcceptedTurnPlan prepared)
    {
        if (!prepared.BelongsToCacheAuthorityState(_authorityStateToken))
        {
            return prepared.HasCacheAuthorityState
                ? "wound_plan_stale_generation"
                : "wound_plan_prepared_binding_mismatch";
        }
        return CurrentPreparedAgrees(prepared, out _)
            ? null
            : "wound_plan_prepared_binding_mismatch";
    }

    private static WoundEffectBatchPlanningResult ValidateEffectResult(
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchPlanningResult effectResult)
    {
        try
        {
            var plan = effectResult.Plan;
            var issues = effectResult.Issues;
            if (issues is null || issues.Any(static issue => issue is null))
            {
                return FailedEffect(
                    "wound_plan_effect_binding_mismatch",
                    "one well-formed effect result envelope",
                    "null issue authority");
            }
            if (plan is null)
            {
                return issues.Count == 0
                    ? FailedEffect(
                        "wound_plan_effect_binding_mismatch",
                        "one successful effect plan or bounded issues",
                        "empty effect result")
                    : new WoundEffectBatchPlanningResult(null, issues);
            }
            if (issues.Count != 0)
            {
                return FailedEffect(
                    "wound_plan_effect_binding_mismatch",
                    "successful effect plan without issues",
                    "partial effect result");
            }

            var accepted = WoundEffectBatchAcceptedPlan.AcceptCandidate(
                prepared,
                plan.EffectInput,
                plan.EffectPlan,
                plan.ApplicationResults,
                plan.TerminationResults,
                plan.WoundPreparationFingerprint,
                plan.EffectInputFingerprint,
                plan.EffectAcceptedTurnPlanFingerprint);
            if (!accepted.Success || accepted.Plan is null)
            {
                return FailedEffect(
                    "wound_plan_effect_binding_mismatch",
                    prepared.WoundPreparationFingerprint,
                    plan.WoundPreparationFingerprint);
            }
            return accepted;
        }
        catch (Exception exception) when (IsMalformedBoundary(exception))
        {
            return FailedEffect(
                "wound_plan_effect_binding_mismatch",
                "well-formed exact effect handoff",
                exception.GetType().Name);
        }
    }

    internal static WoundAcceptedTurnPreparationResult ValidatePreparedResult(
        WoundAcceptedTurnInput input,
        string inputFingerprint,
        WoundAcceptedTurnPreparationResult result)
    {
        try
        {
            var plan = result.Plan;
            var issues = result.Issues;
            if (issues is null || issues.Any(static issue => issue is null))
            {
                return FailedPreparation(
                    "wound_plan_partial_result",
                    "one well-formed preparation result envelope",
                    "null issue authority");
            }
            if (plan is null)
            {
                return issues.Count == 0
                    ? FailedPreparation(
                        "wound_plan_partial_result",
                        "one prepared plan or bounded issues",
                        "empty preparation result")
                    : new WoundAcceptedTurnPreparationResult(null, issues);
            }
            if (issues.Count != 0)
            {
                return FailedPreparation(
                    "wound_plan_partial_result",
                    "successful prepared plan without issues",
                    "partial preparation result");
            }

            var expectedBinding = WoundAcceptedTurnFingerprints.ComputeBinding(
                input.Binding);
            var actualBinding = WoundAcceptedTurnFingerprints.ComputeBinding(
                plan.Binding);
            if (!string.Equals(
                    plan.BindingFingerprint,
                    expectedBinding,
                    StringComparison.Ordinal) ||
                !string.Equals(actualBinding, expectedBinding, StringComparison.Ordinal) ||
                !string.Equals(
                    plan.InputFingerprint,
                    inputFingerprint,
                    StringComparison.Ordinal))
            {
                return FailedPreparation(
                    "wound_plan_prepared_binding_mismatch",
                    inputFingerprint,
                    plan.InputFingerprint);
            }

            var authorityIssues =
                WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(plan);
            if (authorityIssues.Count != 0)
            {
                return new WoundAcceptedTurnPreparationResult(
                    null,
                    authorityIssues);
            }
            if (!PreparedInputAuthorityAgrees(input, inputFingerprint, plan))
            {
                return FailedPreparation(
                    "wound_plan_prepared_binding_mismatch",
                    inputFingerprint,
                    "prepared payload derived from different input authority");
            }
            if (!PreparedReplayAgrees(input, plan))
            {
                return FailedPreparation(
                    "wound_plan_prepared_binding_mismatch",
                    inputFingerprint,
                    "prepared payload differs from the canonical input replay");
            }
            var preparationFingerprint =
                WoundAcceptedTurnFingerprints.ComputePreparation(plan);
            if (!string.Equals(
                    plan.WoundPreparationFingerprint,
                    preparationFingerprint,
                    StringComparison.Ordinal))
            {
                return FailedPreparation(
                    "wound_plan_prepared_binding_mismatch",
                    preparationFingerprint,
                    plan.WoundPreparationFingerprint);
            }
            return new WoundAcceptedTurnPreparationResult(
                plan,
                Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (IsMalformedBoundary(exception))
        {
            return FailedPreparation(
                "wound_plan_partial_result",
                "well-formed exact prepared result",
                exception.GetType().Name);
        }
    }

    private static bool PreparedInputAuthorityAgrees(
        WoundAcceptedTurnInput input,
        string inputFingerprint,
        WoundPreparedAcceptedTurnPlan plan)
    {
        var baseline = plan.BaselineAuthority;
        var expectedBaselineSeal =
            WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(
                inputFingerprint,
                input.PreTurnCarriers,
                input.PreTurnIdentityIndex,
                input.PreTurnHistory);
        if (!string.Equals(
                baseline.PreparedInputFingerprint,
                inputFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                baseline.AuthoritySeal,
                expectedBaselineSeal,
                StringComparison.Ordinal))
        {
            return false;
        }

        var transitions = input.Transitions;
        var batches = plan.EffectOperationBatches;
        if (transitions.Count != batches.Count)
            return false;
        var opportunities = input.Opportunities.ToDictionary(
            static value => value.OpportunityId,
            StringComparer.Ordinal);
        for (var index = 0; index < transitions.Count; index++)
        {
            var draft = transitions[index];
            var batch = batches[index];
            var authority = batch.TransitionAuthority;
            if (!opportunities.TryGetValue(
                    draft.OpportunityId,
                    out var opportunity) ||
                !string.Equals(
                    batch.LocalWoundRef,
                    draft.LocalWoundRef,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    authority.OpportunityId,
                    draft.OpportunityId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    authority.OpportunityAuthorityFingerprint,
                    opportunity.AuthorityFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    authority.OperationKey,
                    draft.OperationKey,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    authority.ReadableSummary,
                    draft.ReadableSummary,
                    StringComparison.Ordinal) ||
                authority.MaximumSeverityRank != opportunity.MaximumSeverityRank)
            {
                return false;
            }
        }
        return true;
    }

    private static bool PreparedReplayAgrees(
        WoundAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan plan)
    {
        var replay = WoundAcceptedTurnPlanner.Prepare(
            WoundAcceptedTurnData.CloneInput(input)!);
        if (!replay.Success || replay.Plan is not { } expected)
            return false;
        if (!string.Equals(
                expected.BindingFingerprint,
                plan.BindingFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.InputFingerprint,
                plan.InputFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.WoundPreparationFingerprint,
                plan.WoundPreparationFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.BaselineAuthority.PreparedInputFingerprint,
                plan.BaselineAuthority.PreparedInputFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.BaselineAuthority.AuthoritySeal,
                plan.BaselineAuthority.AuthoritySeal,
                StringComparison.Ordinal))
        {
            return false;
        }

        var expectedBatches = expected.EffectOperationBatches;
        var actualBatches = plan.EffectOperationBatches;
        if (expectedBatches.Count != actualBatches.Count)
            return false;
        for (var index = 0; index < expectedBatches.Count; index++)
        {
            if (!string.Equals(
                    expectedBatches[index].SourceExportFingerprint,
                    actualBatches[index].SourceExportFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    expectedBatches[index].TransitionAuthority.AuthoritySeal,
                    actualBatches[index].TransitionAuthority.AuthoritySeal,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    internal static WoundAcceptedTurnPlanningResult ValidateFinalResult(
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchAcceptedPlan effectPlan,
        WoundAcceptedTurnPlanningResult result)
    {
        try
        {
            var plan = result.Plan;
            var issues = result.Issues;
            if (issues is null || issues.Any(static issue => issue is null))
            {
                return FailedFinal(
                    "wound_plan_partial_result",
                    "one well-formed final result envelope",
                    "null issue authority");
            }
            if (plan is null)
            {
                return issues.Count == 0
                    ? FailedFinal(
                        "wound_plan_partial_result",
                        "one final plan or bounded issues",
                        "empty final result")
                    : new WoundAcceptedTurnPlanningResult(null, issues);
            }
            if (issues.Count != 0)
            {
                return FailedFinal(
                    "wound_plan_partial_result",
                    "successful final plan without issues",
                    "partial final result");
            }

            var bindingFingerprint = WoundAcceptedTurnFingerprints.ComputeBinding(
                plan.Binding);
            var expectedFinal = WoundAcceptedTurnFingerprints.ComputeFinal(
                prepared,
                effectPlan,
                plan.CarrierContributions,
                plan.IdentityIndexAfterImage,
                plan.HistoryAfterImage,
                plan.TransitionIntents);
            var replay = WoundAcceptedTurnPlanner.Finalize(
                WoundAcceptedTurnData.ClonePreparedPlan(prepared)!,
                new WoundEffectBatchPlanningResult(
                    effectPlan,
                    Array.Empty<ValidationIssue>()));
            var canonicalFinal = replay.Success
                ? replay.Plan!.WoundFinalPlanFingerprint
                : null;
            if (!string.Equals(
                    plan.BindingFingerprint,
                    prepared.BindingFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    bindingFingerprint,
                    prepared.BindingFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    plan.InputFingerprint,
                    prepared.InputFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    plan.WoundPreparationFingerprint,
                    effectPlan.WoundPreparationFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    plan.EffectInputFingerprint,
                    effectPlan.EffectInputFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    plan.EffectAcceptedTurnPlanFingerprint,
                    effectPlan.EffectAcceptedTurnPlanFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    plan.WoundFinalPlanFingerprint,
                    expectedFinal,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    plan.WoundFinalPlanFingerprint,
                    canonicalFinal,
                    StringComparison.Ordinal) ||
                !plan.AllocatedWoundIds.SequenceEqual(
                    prepared.AllocatedWoundIds,
                    StringComparer.Ordinal) ||
                !plan.AllocatedTransitionIds.SequenceEqual(
                    prepared.AllocatedTransitionIds,
                    StringComparer.Ordinal))
            {
                return FailedFinal(
                    "wound_plan_final_fingerprint_mismatch",
                    expectedFinal,
                    plan.WoundFinalPlanFingerprint);
            }
            return new WoundAcceptedTurnPlanningResult(
                plan,
                Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (IsMalformedBoundary(exception))
        {
            return FailedFinal(
                "wound_plan_partial_result",
                "well-formed exact final result",
                exception.GetType().Name);
        }
    }

    private void InvalidateAllCore()
    {
        _preparedInputFingerprint = null;
        _preparedFingerprint = null;
        _preparedStageToken = null;
        _preparedResult = null;
        InvalidateFinalCore();
    }

    private void InvalidateFinalCore()
    {
        _finalPreparedFingerprint = null;
        _finalEffectFingerprint = null;
        _finalResult = null;
    }

    private static WoundAcceptedTurnPreparationResult Detach(
        WoundAcceptedTurnPreparationResult result) =>
        new(result.Plan, result.Issues);

    private static WoundAcceptedTurnPlanningResult Detach(
        WoundAcceptedTurnPlanningResult result) =>
        new(result.Plan, result.Issues);

    private static string SafePreparationFingerprint(
        WoundPreparedAcceptedTurnPlan prepared)
    {
        try
        {
            return WoundAcceptedTurnFingerprints.ComputePreparation(prepared);
        }
        catch (Exception exception) when (IsMalformedBoundary(exception))
        {
            return exception.GetType().Name;
        }
    }

    private static bool IsMalformedBoundary(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or
            System.Text.Json.JsonException or NullReferenceException;

    private static WoundAcceptedTurnPreparationResult FailedPreparation(
        string code,
        string expected,
        string actual) =>
        new(null, new[] { Issue(code, expected, actual) });

    private static WoundEffectBatchPlanningResult FailedEffect(
        string code,
        string expected,
        string actual) =>
        new(null, new[] { Issue(code, expected, actual) });

    private static WoundAcceptedTurnPlanningResult FailedFinal(
        string code,
        string expected,
        string actual) =>
        new(null, new[] { Issue(code, expected, actual) });

    private static ValidationIssue Issue(
        string code,
        string expected,
        string actual) =>
        new(
            CachePath,
            IssueSeverity.Error,
            "The cached accepted wound stage did not match its sealed authority.",
            code,
            actor: "wound_planner",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
                "Discard every dependent accepted-turn handoff and rebuild from the complete live binding.",
            repairTargetFiles: new[] { CachePath });
}

internal static class WoundAcceptedTurnPlanAuthority
{
    internal static WoundAcceptedTurnPreparationResult GetOrBuildPreparedValidated(
        BookOfEternityClient.Core.FileSystemManager fileSystem,
        BookOfEternityClient.Core.FileSystemManager.CanonicalWriteLease writeLease,
        WoundAcceptedTurnInput input)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(input);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.GetOrBuildWoundPreparedValidated(
            fileSystem,
            writeLease,
            input);
    }

    internal static WoundAcceptedTurnPlanningResult GetOrBuildFinalValidated(
        BookOfEternityClient.Core.FileSystemManager fileSystem,
        BookOfEternityClient.Core.FileSystemManager.CanonicalWriteLease writeLease,
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchPlanningResult effectResult)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(effectResult);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.GetOrBuildWoundFinalValidated(
            fileSystem,
            writeLease,
            prepared,
            effectResult);
    }

    internal static WoundEffectBatchPlanningResult GetOrBuildEffectValidated(
        BookOfEternityClient.Core.FileSystemManager fileSystem,
        BookOfEternityClient.Core.FileSystemManager.CanonicalWriteLease writeLease,
        WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnInput input)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(input);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.GetOrBuildWoundEffectValidated(
            fileSystem,
            writeLease,
            prepared,
            input);
    }

    internal static bool TryPeekPrepared(
        BookOfEternityClient.Core.FileSystemManager fileSystem,
        BookOfEternityClient.Core.FileSystemManager.CanonicalWriteLease writeLease,
        out WoundAcceptedTurnPreparationResult result)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.TryPeekWoundPrepared(
            fileSystem,
            writeLease,
            out result);
    }

    internal static bool TryPeekFinal(
        BookOfEternityClient.Core.FileSystemManager fileSystem,
        BookOfEternityClient.Core.FileSystemManager.CanonicalWriteLease writeLease,
        out WoundAcceptedTurnPlanningResult result)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.TryPeekWoundFinal(
            fileSystem,
            writeLease,
            out result);
    }
}
