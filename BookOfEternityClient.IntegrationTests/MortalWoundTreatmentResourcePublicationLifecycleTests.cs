using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedResourceQuantity_ResourceFreeCommonPlanKeepsOpenTransactionCarrierEmpty()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.WriteExactJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceMaterializationValidationTests.DefinitionAndInitializationCommand()
                .ToJsonString());

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var resourceIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(itemIssues, static issue =>
            issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(resourceIssues, static issue =>
            issue.Severity == IssueSeverity.Error);

        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem,
            context.Normalizer,
            context.Validator,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var carrier = RequireOpenTreatmentTransactionCarrier();

        Assert.NotNull(result.MechanicsPlan);
        Assert.Null(carrier.GetValue(result));
    }

    [Fact]
    public void GuaranteedResourceQuantity_CanonicalRefreshReturnsOneOpaqueOpenTransactionCarrier()
    {
        var property = RequireOpenTreatmentTransactionCarrier();
        var carrierType = property.PropertyType;
        var gameEngineProperty = RequireGameEngineRefreshTransactionCarrier(carrierType);
        RequireCarrierForwardingDataFlow(property, gameEngineProperty);

        Assert.False(
            carrierType.IsPublic || carrierType.IsNestedPublic,
            "The held-treatment publication transaction must remain an internal production authority.");
        Assert.True(
            carrierType.IsSealed,
            "The held-treatment publication transaction must be a closed, non-extensible authority.");
        Assert.Empty(carrierType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Contains(typeof(IAsyncDisposable), carrierType.GetInterfaces());

        var completion = carrierType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.DeclaringType == carrierType)
            .Where(static method => IsCompletionMethodName(method.Name))
            .ToArray();
        var compensation = carrierType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.DeclaringType == carrierType)
            .Where(static method => IsCompensationMethodName(method.Name))
            .ToArray();

        var complete = Assert.Single(completion);
        var compensate = Assert.Single(compensation);
        Assert.False(complete.IsPublic);
        Assert.False(compensate.IsPublic);
        Assert.True(IsAwaitableReturn(complete.ReturnType));
        Assert.True(IsAwaitableReturn(compensate.ReturnType));
        Assert.Equal(carrierType, gameEngineProperty.PropertyType);
    }

    [Fact]
    public void GuaranteedResourceQuantity_FullPipelineOwnsCompensationScopeAndCompletesAfterFinalRefresh()
    {
        var analysis = ReadMethodAnalysis(
            "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync");
        _ = RequireControlFlowGraph(analysis);
        _ = RequireOpenTreatmentTransactionCarrier();
        var method = analysis.Method;
        var invocations = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToArray();

        var canonicalRefresh = RequireInvocation(
            invocations,
            "RefreshAcceptedTurnCanonicalStateForValidationAsync");
        var criticalValidation = RequireInvocation(
            invocations,
            "ValidateCriticalCanonicalStateAsync");
        var fullStateValidation = RequireInvocation(
            invocations,
            "ValidateCurrentGameStateOrShowErrorsAsync");
        var cleanupInvocations = RequireInvocations(
            invocations,
            "CleanupAcceptedTurnCommandSurfacesAsync");
        var runtimeRefreshes = RequireInvocations(invocations, "RefreshRuntimeStateAsync");
        var finalRuntimeRefresh = runtimeRefreshes.MaxBy(static invocation => invocation.SpanStart)!;

        var compensationScope = RequireCanonicalRefreshTransactionScope(analysis);
        var completion = RequireDirectAwaitedTypedCompletion(
            analysis,
            compensationScope);
        var notificationAssignments = method.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(static assignment => assignment.Left.ToString().Contains(
                "_acceptedTurnWoundNotifications",
                StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(notificationAssignments);
        var notificationAssignment = notificationAssignments.MaxBy(
            static assignment => assignment.SpanStart)!;
        var acceptedReturn = Assert.Single(method.DescendantNodes()
            .OfType<ReturnStatementSyntax>(),
            static statement => string.Equals(
                statement.Expression?.ToString(),
                "true",
                StringComparison.Ordinal));

        Assert.True(canonicalRefresh.SpanStart < compensationScope.Statement.SpanStart);
        var refreshTry = method.DescendantNodes()
            .OfType<TryStatementSyntax>()
            .Where(statement => statement.Block.Span.Contains(canonicalRefresh.Span))
            .MinBy(static statement => statement.Block.Span.Length);
        Assert.NotNull(refreshTry);
        var firstPostRefreshExit = method.DescendantNodes()
            .Where(static node => node is ContinueStatementSyntax or ReturnStatementSyntax)
            .Where(node => node.SpanStart > refreshTry!.Span.End)
            .MinBy(static node => node.SpanStart);
        Assert.NotNull(firstPostRefreshExit);
        Assert.True(
            compensationScope.Statement.SpanStart < firstPostRefreshExit!.SpanStart,
            "Every post-helper continue/false/exception path must already be owned by the compensation scope.");
        var compensationBlock = Assert.IsType<BlockSyntax>(
            compensationScope.Statement.Parent);
        var postCarrierExits = method.DescendantNodes()
            .Where(static node =>
                node is ContinueStatementSyntax or ThrowStatementSyntax ||
                node is ReturnStatementSyntax returnStatement &&
                !string.Equals(
                    returnStatement.Expression?.ToString(),
                    "true",
                    StringComparison.Ordinal))
            .Where(node => node.SpanStart > canonicalRefresh.Span.End &&
                           node.SpanStart < completion.SpanStart)
            .ToArray();
        Assert.NotEmpty(postCarrierExits);
        Assert.All(postCarrierExits, exit =>
        {
            Assert.True(
                exit.SpanStart > compensationScope.Statement.SpanStart,
                $"Post-carrier exit '{exit}' occurs before the exact transaction scope.");
            Assert.True(
                compensationBlock.Span.Contains(exit.Span),
                $"Post-carrier exit '{exit}' is outside the exact transaction scope's lifetime.");
        });

        Assert.True(compensationScope.Statement.SpanStart < criticalValidation.SpanStart);
        Assert.True(criticalValidation.SpanStart < fullStateValidation.SpanStart);
        Assert.True(fullStateValidation.SpanStart < cleanupInvocations.Min(static value => value.SpanStart));
        Assert.True(cleanupInvocations.Max(static value => value.SpanStart) < finalRuntimeRefresh.SpanStart);
        Assert.True(finalRuntimeRefresh.SpanStart < completion.SpanStart);
        Assert.True(completion.SpanStart < notificationAssignment.SpanStart);
        Assert.True(notificationAssignment.SpanStart < acceptedReturn.SpanStart);
        RequireTypedCompletionSuccessDominatesAcceptedExit(
            analysis,
            completion,
            notificationAssignment,
            acceptedReturn);

        Assert.DoesNotContain(
            method.DescendantNodes().OfType<AwaitExpressionSyntax>(),
            value => value.SpanStart > completion.SpanStart);
    }

    [Theory]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs",
        "RefreshCanonicalStateAsync",
        "RefreshRuntimeStateAsync",
        false)]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
        "RefreshAcceptedTurnCanonicalStateForValidationAsync",
        "ValidateAcceptedWoundPostPublicationAuthorityAsync",
        false)]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
        "RefreshAcceptedTurnCanonicalStateForValidationAsync",
        "BindPublishedAcceptedWoundOutputAsync",
        false)]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
        "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
        "ValidateCriticalCanonicalStateAsync",
        false)]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
        "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
        "ValidateCurrentGameStateOrShowErrorsAsync",
        false)]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
        "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
        "CleanupAcceptedTurnCommandSurfacesAsync",
        true)]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
        "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
        "RefreshRuntimeStateAsync",
        true)]
    public void GuaranteedResourceQuantity_PostHelperFailureBoundaryRemainsInsideCompensationFence(
        string relativePath,
        string methodName,
        string boundaryInvocationName,
        bool useLastOccurrence)
    {
        var analysis = ReadMethodAnalysis(relativePath, methodName);
        _ = RequireControlFlowGraph(analysis);
        var carrierProperty = RequireOpenTreatmentTransactionCarrier();
        var method = analysis.Method;
        var invocations = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToArray();
        var boundaries = RequireInvocations(invocations, boundaryInvocationName);
        var boundary = useLastOccurrence
            ? boundaries.MaxBy(static value => value.SpanStart)!
            : boundaries.MinBy(static value => value.SpanStart)!;

        if (!string.Equals(
                methodName,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                StringComparison.Ordinal))
        {
            var upstreamInvocationName = string.Equals(
                methodName,
                "RefreshCanonicalStateAsync",
                StringComparison.Ordinal)
                ? "NormalizeAndValidateWithPlanAsync"
                : "RefreshCanonicalStateAsync";
            RequireExactCarrierCompensationForBoundary(
                analysis,
                boundary,
                upstreamInvocationName,
                carrierProperty.Name);
            return;
        }

        var scope = RequireCanonicalRefreshTransactionScope(analysis);
        var completion = RequireDirectAwaitedTypedCompletion(analysis, scope);
        Assert.True(scope.Statement.SpanStart < boundary.SpanStart);
        Assert.True(boundary.SpanStart < completion.SpanStart);
    }

    [Theory]
    [InlineData("runtime_refresh", 1)]
    [InlineData("wound_post_seal", 1)]
    [InlineData("wound_output", 1)]
    [InlineData("critical_validation", 1)]
    [InlineData("full_state_validation", 1)]
    [InlineData("cleanup", 1)]
    [InlineData("runtime_refresh", 2)]
    [InlineData("transaction_commit_conflict", 2)]
    public async Task GuaranteedResourceQuantity_RealGameEngineBoundaryFailureCompensatesExactHeldPlan(
        string boundary,
        int occurrence)
    {
        var fault = new AcceptedTreatmentPipelineFault(boundary, occurrence);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(fault);
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        var commandBefore = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(
            AcceptedMechanicsPlan.WoundCommandPath));

        await context.ReleaseLeaseAsync();
        fault.Arm(context);
        bool? accepted = null;
        var exception = await Record.ExceptionAsync(async () =>
        {
            accepted = await InvokePrivateAsync<bool>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "held treatment integration oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);
        });

        Assert.True(
            fault.Fired,
            $"The existing filesystem hook did not reach '{boundary}' occurrence {occurrence}. " +
            $"Observed phases: {string.Join(", ", fault.ObservedPhases)}");
        Assert.True(
            exception is not null || accepted == false,
            "A pre-finalization failure must not return an accepted turn.");
        if (string.Equals(
                boundary,
                "transaction_commit_conflict",
                StringComparison.Ordinal))
        {
            Assert.Null(exception);
            Assert.False(accepted);
        }
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        Assert.Equal(
            commandBefore,
            await context.FileSystem.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));

        await context.AcquireLeaseAsync();
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out var rearmedBinding,
            out var rearmed));
        Assert.True(rearmed.Success, DescribeValidationIssues(rearmed.Issues));
        Assert.Same(context.Plan, rearmed.Plan);
        Assert.Equal(
            AcceptedMechanicsPlanFingerprints.ComputeInput(context.OriginalBinding),
            AcceptedMechanicsPlanFingerprints.ComputeInput(rearmedBinding));
        AssertConfirmedHeldBlocksCompetingTreatment(context);
    }

    [Fact]
    public async Task GuaranteedResourceQuantity_ColdConfirmedCommandRealPipelineCommitThenRestartReplaySpendsOnce()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(fault: null);
        await context.RestartAsync(hooks: null);
        await RestoreHeldTreatmentAndComposeSameSemanticPlanAsync(context);
        var historyBefore = await ReadTreatmentResourceHistoryAsync(context.FileSystem);
        var spendCountBefore = CountHeldTreatmentHealthSpends(historyBefore);
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(Array.Empty<ConsoleKeyInfo>()),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);

        await context.ReleaseLeaseAsync();
        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "cold held treatment integration oracle",
            snapshotContext,
            null,
            HeldTreatmentPipelineContext.Turn,
            null);

        Assert.True(accepted);
        var stateAfterCommit = await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationContract.StatePath);
        var historyAfterCommit = await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationContract.HistoryPath);
        Assert.NotNull(stateAfterCommit);
        Assert.NotNull(historyAfterCommit);
        Assert.Equal(
            spendCountBefore + 1,
            CountHeldTreatmentHealthSpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));

        await context.RestartAsync(hooks: null);
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = acceptedState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        var finalized = Assert.Single(recovered.FinalizedRequests);
        var wound = ReadCurrentTreatmentWound(context.FileSystem);
        var replay = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(
            finalized,
            history,
            wound,
            acceptedState);

        Assert.Equal("ExactReplay", replay.Disposition);
        Assert.NotNull(replay.ReplayReceipt);
        Assert.Equal(
            stateAfterCommit,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.StatePath));
        Assert.Equal(
            historyAfterCommit,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.HistoryPath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            context.Lease));
    }

    private static void RequireExactCarrierCompensationForBoundary(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax boundary,
        string upstreamInvocationName,
        string carrierPropertyName)
    {
        var method = analysis.Method;
        var upstream = Assert.Single(
            method.DescendantNodes().OfType<InvocationExpressionSyntax>(),
            invocation => string.Equals(
                ReadInvocationName(invocation),
                upstreamInvocationName,
                StringComparison.Ordinal));
        var resultSymbol = RequireAssignedLocalSymbol(analysis, upstream);
        var resultStatement = upstream.Ancestors()
            .OfType<StatementSyntax>()
            .First();
        var carrierAliases = BuildExactCarrierAliases(
            analysis,
            method,
            resultSymbol,
            carrierPropertyName);
        var exactCompensationMethodName = RequireExactCarrierCompensationMethod().Name;
        var controlFlow = RequireControlFlowGraph(analysis);
        var dominators = ComputeDominators(controlFlow);

        var owningTry = method.DescendantNodes()
            .OfType<TryStatementSyntax>()
            .Where(statement => statement.Block.Span.Contains(boundary.Span))
            .MinBy(static statement => statement.Block.Span.Length);
        Assert.NotNull(owningTry);

        Assert.True(
            owningTry!.Catches.Count > 0 || owningTry.Finally is not null,
            $"Failure boundary '{boundary}' after the helper returned its carrier " +
            "must be enclosed by an explicit compensation catch/finally.");
        foreach (var clause in owningTry.Catches)
        {
            Assert.False(
                clause.Filter?.DescendantNodesAndSelf().Any(static node =>
                    node is InvocationExpressionSyntax or AwaitExpressionSyntax or
                        ObjectCreationExpressionSyntax) == true,
                "A post-carrier catch filter cannot run fallible work before exact " +
                "carrier compensation owns the failure path.");
            RequireUnconditionalExactCarrierCompensation(
                analysis,
                clause.Block,
                resultSymbol,
                carrierPropertyName,
                carrierAliases,
                exactCompensationMethodName,
                $"catch {clause.Declaration?.Type}");
        }
        if (owningTry.Finally is { } finallyClause)
        {
            RequireUnconditionalExactCarrierCompensation(
                analysis,
                finallyClause.Block,
                resultSymbol,
                carrierPropertyName,
                carrierAliases,
                exactCompensationMethodName,
                "finally");
        }

        var postCarrierExits = method.DescendantNodes()
            .Where(static node => node is ReturnStatementSyntax or
                                  ThrowStatementSyntax or
                                  ContinueStatementSyntax)
            .Where(node => node.SpanStart > resultStatement.Span.End)
            .Where(node => ExitCanObserveReturnedCarrier(node, resultStatement))
            .ToArray();
        Assert.True(
            postCarrierExits.Length > 0,
            "The helper oracle must inspect each ownership-transfer or compensation exit.");
        foreach (var exit in postCarrierExits)
        {
            if (exit is ReturnStatementSyntax returnStatement &&
                ReturnTransfersExactCarrier(
                    analysis,
                    returnStatement,
                    resultSymbol,
                    carrierPropertyName,
                    carrierAliases))
            {
                continue;
            }
            Assert.True(
                IsExitDominatedByExactCarrierCompensation(
                    analysis,
                    controlFlow,
                    dominators,
                    exit,
                    resultSymbol,
                    carrierPropertyName,
                    carrierAliases,
                    exactCompensationMethodName),
                $"Post-carrier exit '{exit}' in {method.Identifier.ValueText} must " +
                "either transfer the exact carrier or be dominated by unconditional " +
                "compensation of that exact carrier.");
        }
    }

    private static IReadOnlySet<ILocalSymbol> BuildExactCarrierAliases(
        SourceMethodAnalysis analysis,
        SyntaxNode scope,
        ILocalSymbol resultSymbol,
        string carrierPropertyName)
    {
        var aliases = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
        bool changed;
        do
        {
            changed = false;
            foreach (var variable in scope.DescendantNodes()
                         .OfType<VariableDeclaratorSyntax>()
                         .Where(static variable => variable.Initializer is not null))
            {
                var symbol = analysis.Model.GetDeclaredSymbol(variable) as ILocalSymbol;
                if (symbol is null || aliases.Contains(symbol) ||
                    !ExpressionIsExactCarrierIdentity(
                        analysis,
                        variable.Initializer!.Value,
                        resultSymbol,
                        carrierPropertyName,
                        aliases) ||
                    !IsImmutableLocal(analysis, scope, symbol, variable))
                {
                    continue;
                }
                aliases.Add(symbol);
                changed = true;
            }
        } while (changed);
        return aliases;
    }

    private static void RequireUnconditionalExactCarrierCompensation(
        SourceMethodAnalysis analysis,
        BlockSyntax block,
        ILocalSymbol resultSymbol,
        string carrierPropertyName,
        IReadOnlySet<ILocalSymbol> aliases,
        string exactCompensationMethodName,
        string label)
    {
        var lifecycle = block.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => IsExactCarrierCompensationLifecycleName(
                ReadInvocationName(invocation),
                exactCompensationMethodName))
            .ToArray();
        Assert.True(
            lifecycle.Length > 0,
            $"Every {label} handling a post-carrier failure needs exact-carrier compensation.");
        Assert.All(lifecycle, invocation => Assert.True(
            IsResolvedInstanceInvocation(analysis, invocation),
            $"Lifecycle call '{invocation}' in {label} must resolve to an instance " +
            "method on the exact carrier flow, not a similarly named helper."));
        Assert.All(lifecycle, invocation => Assert.True(
            InvocationUsesExactCarrierDataFlow(
                analysis,
                invocation,
                resultSymbol,
                carrierPropertyName,
                aliases),
            $"Lifecycle call '{invocation}' in {label} targets an unrelated authority."));
        Assert.All(lifecycle, invocation => Assert.True(
            IsDirectlyAwaited(invocation),
            $"Exact-carrier lifecycle call '{invocation}' in {label} must be directly " +
            "awaited so compensation cannot be lost or reordered."));
        var unconditional = lifecycle
            .Where(invocation => invocation.Ancestors()
                .OfType<StatementSyntax>()
                .FirstOrDefault(statement => ReferenceEquals(statement.Parent, block)) is
                    ExpressionStatementSyntax or LocalDeclarationStatementSyntax)
            .ToArray();
        Assert.True(
            unconditional.Length > 0,
            $"Exact-carrier compensation in {label} must be unconditional, not nested " +
            "behind a branch that can bypass it.");
        var firstCompensationIndex = unconditional.Min(invocation =>
            TopLevelStatementIndex(block, invocation));
        Assert.All(
            block.Statements.Take(firstCompensationIndex),
            statement => Assert.False(
                IsPotentiallyFallibleOrExitingStatement(statement),
                $"Exact-carrier compensation in {label} must precede every fallible " +
                $"top-level statement or exit, but found '{statement}'."));
        foreach (var exit in block.DescendantNodes().Where(static node =>
                     node is ReturnStatementSyntax or ThrowStatementSyntax or
                         ContinueStatementSyntax))
        {
            Assert.True(
                unconditional.Any(invocation =>
                    TopLevelStatementIndex(block, invocation) <
                    TopLevelStatementIndex(block, exit)),
                $"Exact-carrier compensation must dominate exit '{exit}' in {label}.");
        }
    }

    private static int TopLevelStatementIndex(BlockSyntax block, SyntaxNode node)
    {
        var statement = node.AncestorsAndSelf()
            .OfType<StatementSyntax>()
            .First(candidate => ReferenceEquals(candidate.Parent, block));
        return block.Statements.IndexOf(statement);
    }

    private static bool IsDirectlyAwaited(
        InvocationExpressionSyntax invocation) =>
        invocation.Parent is AwaitExpressionSyntax awaited &&
        ReferenceEquals(awaited.Expression, invocation);

    private static bool IsPotentiallyFallibleOrExitingStatement(
        StatementSyntax statement) =>
        statement.DescendantNodesAndSelf().Any(static node =>
            node is InvocationExpressionSyntax or AwaitExpressionSyntax or
                ObjectCreationExpressionSyntax or
                ImplicitObjectCreationExpressionSyntax or ThrowExpressionSyntax or
                ThrowStatementSyntax or ReturnStatementSyntax or
                ContinueStatementSyntax or BreakStatementSyntax or
                GotoStatementSyntax or YieldStatementSyntax or
                UsingStatementSyntax or LockStatementSyntax ||
            node is LocalDeclarationStatementSyntax declaration &&
                !declaration.UsingKeyword.IsKind(SyntaxKind.None));

    private static bool ExitCanObserveReturnedCarrier(
        SyntaxNode exit,
        StatementSyntax creationStatement)
    {
        foreach (var clause in exit.Ancestors().OfType<CatchClauseSyntax>())
        {
            var owningTry = Assert.IsType<TryStatementSyntax>(clause.Parent);
            if (!owningTry.Block.Span.Contains(creationStatement.Span))
                continue;
            var creationTopLevel = creationStatement.AncestorsAndSelf()
                .OfType<StatementSyntax>()
                .First(statement => ReferenceEquals(
                    statement.Parent,
                    owningTry.Block));
            var index = owningTry.Block.Statements.IndexOf(creationTopLevel);
            var hasFallibleWorkAfterCreation = owningTry.Block.Statements
                .Skip(index + 1)
                .Any(statement => statement.DescendantNodesAndSelf().Any(node =>
                    node is InvocationExpressionSyntax or AwaitExpressionSyntax));
            if (!hasFallibleWorkAfterCreation)
                return false;
        }
        return true;
    }

    private static bool ReturnTransfersExactCarrier(
        SourceMethodAnalysis analysis,
        ReturnStatementSyntax statement,
        ILocalSymbol resultSymbol,
        string carrierPropertyName,
        IReadOnlySet<ILocalSymbol> aliases,
        string? targetCarrierPropertyName = null)
    {
        if (statement.Expression is null)
            return false;
        if (UnwrapIdentityPreservingOperation(
                analysis.Model.GetOperation(statement.Expression)) is
                    ILocalReferenceOperation local &&
            SymbolEqualityComparer.Default.Equals(local.Local, resultSymbol) &&
            SymbolEqualityComparer.Default.Equals(
                local.Type,
                GetEffectiveMethodReturnType(analysis)))
        {
            var declaration = analysis.Method.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .SingleOrDefault(variable => SymbolEqualityComparer.Default.Equals(
                    analysis.Model.GetDeclaredSymbol(variable),
                    resultSymbol));
            return declaration is not null &&
                   IsImmutableLocal(
                       analysis,
                       analysis.Method,
                       resultSymbol,
                       declaration);
        }
        return ReturnExpressionPlacesExactCarrierInResultSlot(
            analysis,
            statement.Expression,
            resultSymbol,
            carrierPropertyName,
            aliases,
            targetCarrierPropertyName ?? carrierPropertyName);
    }

    private static ITypeSymbol? GetEffectiveMethodReturnType(
        SourceMethodAnalysis analysis)
    {
        var method = analysis.Model.GetDeclaredSymbol(analysis.Method) as IMethodSymbol;
        var returnType = method?.ReturnType;
        if (returnType is INamedTypeSymbol
            {
                IsGenericType: true,
                TypeArguments.Length: 1
            } awaitable &&
            awaitable.Name is nameof(Task) or nameof(ValueTask) &&
            string.Equals(
                awaitable.ContainingNamespace?.ToDisplayString(),
                "System.Threading.Tasks",
                StringComparison.Ordinal))
        {
            return awaitable.TypeArguments[0];
        }
        return returnType;
    }

    private static bool ReturnExpressionPlacesExactCarrierInResultSlot(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        ILocalSymbol sourceResultSymbol,
        string sourceCarrierPropertyName,
        IReadOnlySet<ILocalSymbol> sourceCarrierAliases,
        string targetCarrierPropertyName)
    {
        if (UnwrapIdentityPreservingOperation(
                analysis.Model.GetOperation(expression)) is not
                    IObjectCreationOperation creation ||
            creation.Type is not INamedTypeSymbol createdType ||
            !SymbolEqualityComparer.Default.Equals(
                createdType,
                GetEffectiveMethodReturnType(analysis)))
        {
            return false;
        }
        var targetProperties = createdType.GetMembers(targetCarrierPropertyName)
            .OfType<IPropertySymbol>()
            .Where(static property => !property.IsStatic)
            .ToArray();
        if (targetProperties.Length != 1)
            return false;
        var targetProperty = targetProperties[0];
        var sameTypedProperties = createdType.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static property => !property.IsStatic)
            .Where(property => SymbolEqualityComparer.Default.Equals(
                property.Type,
                targetProperty.Type))
            .ToArray();
        if (sameTypedProperties.Length != 1)
            return false;

        var constructorSlots = creation.Constructor?.Parameters
            .Where(parameter =>
                SymbolEqualityComparer.Default.Equals(
                    parameter.Type,
                    targetProperty.Type))
            .ToArray() ?? Array.Empty<IParameterSymbol>();
        IParameterSymbol? targetParameter = null;
        var namedSlots = constructorSlots
            .Where(parameter => string.Equals(
                parameter.Name,
                targetProperty.Name,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (namedSlots.Length == 1)
            targetParameter = namedSlots[0];
        else if (namedSlots.Length > 1)
            return false;

        var targetSlotWrites = 0;
        var exactSlotWrites = 0;
        if (targetParameter is not null)
        {
            var targetArguments = creation.Arguments
                .Where(argument => SymbolEqualityComparer.Default.Equals(
                    argument.Parameter,
                    targetParameter))
                .ToArray();
            targetSlotWrites += targetArguments.Length;
            exactSlotWrites += targetArguments.Count(argument =>
                OperationIsExactCarrierIdentity(
                    argument.Value,
                    sourceResultSymbol,
                    sourceCarrierPropertyName,
                    sourceCarrierAliases));
        }
        if (creation.Initializer is not null)
        {
            var targetAssignments = creation.Initializer.Initializers
                .OfType<ISimpleAssignmentOperation>()
                .Where(assignment =>
                    UnwrapIdentityPreservingOperation(assignment.Target) is
                        IPropertyReferenceOperation property &&
                    SymbolEqualityComparer.Default.Equals(
                        property.Property,
                        targetProperty))
                .ToArray();
            targetSlotWrites += targetAssignments.Length;
            exactSlotWrites += targetAssignments.Count(assignment =>
                OperationIsExactCarrierIdentity(
                    assignment.Value,
                    sourceResultSymbol,
                    sourceCarrierPropertyName,
                    sourceCarrierAliases));
        }
        return targetSlotWrites == 1 && exactSlotWrites == 1;
    }

    private static bool IsExitDominatedByExactCarrierCompensation(
        SourceMethodAnalysis analysis,
        ControlFlowGraph controlFlow,
        IReadOnlyDictionary<int, HashSet<int>> dominators,
        SyntaxNode exit,
        ILocalSymbol resultSymbol,
        string carrierPropertyName,
        IReadOnlySet<ILocalSymbol> aliases,
        string exactCompensationMethodName)
    {
        var exitBlock = TryFindBlockContaining(controlFlow, exit);
        if (exitBlock is not null)
        {
            foreach (var invocation in analysis.Method.DescendantNodes()
                         .OfType<InvocationExpressionSyntax>()
                         .Where(invocation =>
                             IsExactCarrierCompensationLifecycleName(
                                 ReadInvocationName(invocation),
                                 exactCompensationMethodName) &&
                             IsDirectlyAwaited(invocation) &&
                             IsResolvedInstanceInvocation(analysis, invocation) &&
                             InvocationUsesExactCarrierDataFlow(
                                 analysis,
                                 invocation,
                                 resultSymbol,
                                 carrierPropertyName,
                                 aliases)))
            {
                var compensationBlock = TryFindBlockContaining(
                    controlFlow,
                    invocation);
                if (compensationBlock is null ||
                    !Dominates(dominators, compensationBlock, exitBlock))
                {
                    continue;
                }
                if (compensationBlock.Ordinal != exitBlock.Ordinal ||
                    invocation.SpanStart < exit.SpanStart)
                {
                    return true;
                }
            }
        }

        foreach (var block in exit.Ancestors().OfType<BlockSyntax>())
        {
            var exitIndex = TopLevelStatementIndex(block, exit);
            if (block.Statements.Take(exitIndex).Any(statement =>
                    statement is ExpressionStatementSyntax or LocalDeclarationStatementSyntax &&
                    statement.DescendantNodesAndSelf()
                        .OfType<InvocationExpressionSyntax>()
                        .Any(invocation =>
                            IsExactCarrierCompensationLifecycleName(
                                ReadInvocationName(invocation),
                                exactCompensationMethodName) &&
                            IsDirectlyAwaited(invocation) &&
                            IsResolvedInstanceInvocation(analysis, invocation) &&
                            InvocationUsesExactCarrierDataFlow(
                                analysis,
                                invocation,
                                resultSymbol,
                                carrierPropertyName,
                                aliases))))
            {
                return true;
            }
        }
        foreach (var tryStatement in analysis.Method.DescendantNodes()
                     .OfType<TryStatementSyntax>()
                     .Where(statement =>
                         statement.Block.Span.Contains(exit.Span) &&
                         statement.Finally is not null))
        {
            if (tryStatement.Finally!.Block.Statements.Any(statement =>
                    statement is ExpressionStatementSyntax or
                        LocalDeclarationStatementSyntax &&
                    statement.DescendantNodesAndSelf()
                        .OfType<InvocationExpressionSyntax>()
                        .Any(invocation =>
                            IsExactCarrierCompensationLifecycleName(
                                ReadInvocationName(invocation),
                                exactCompensationMethodName) &&
                            IsDirectlyAwaited(invocation) &&
                            IsResolvedInstanceInvocation(analysis, invocation) &&
                            InvocationUsesExactCarrierDataFlow(
                                analysis,
                                invocation,
                                resultSymbol,
                                carrierPropertyName,
                                aliases))))
            {
                return true;
            }
        }
        return false;
    }

    private static bool InvocationUsesExactCarrierDataFlow(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax invocation,
        ILocalSymbol resultSymbol,
        string carrierPropertyName,
        IReadOnlySet<ILocalSymbol> aliases)
    {
        var receivers = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => new[] { member.Expression },
            MemberBindingExpressionSyntax => invocation.Ancestors()
                .OfType<ConditionalAccessExpressionSyntax>()
                .Where(candidate => candidate.WhenNotNull.Span.Contains(invocation.Span))
                .Select(static candidate => candidate.Expression),
            _ => Array.Empty<ExpressionSyntax>()
        };
        return receivers.Any(expression => ExpressionIsExactCarrierIdentity(
                analysis,
                expression,
                resultSymbol,
                carrierPropertyName,
                aliases));
    }

    private static void RequireCarrierForwardingDataFlow(
        PropertyInfo helperCarrier,
        PropertyInfo gameEngineCarrier)
    {
        var canonicalAnalysis = ReadMethodAnalysis(
            "BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs",
            "RefreshCanonicalStateAsync");
        var canonicalMethod = canonicalAnalysis.Method;
        var canonicalInvocation = Assert.Single(canonicalMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>(),
            static invocation => string.Equals(
                ReadInvocationName(invocation),
                "NormalizeAndValidateWithPlanAsync",
                StringComparison.Ordinal));
        var canonicalResultSymbol = RequireAssignedLocalSymbol(
            canonicalAnalysis,
            canonicalInvocation);
        var canonicalAliases = BuildExactCarrierAliases(
            canonicalAnalysis,
            canonicalMethod,
            canonicalResultSymbol,
            helperCarrier.Name);
        Assert.Contains(
            canonicalMethod.DescendantNodes().OfType<ReturnStatementSyntax>(),
            statement => ReturnTransfersExactCarrier(
                canonicalAnalysis,
                statement,
                canonicalResultSymbol,
                helperCarrier.Name,
                canonicalAliases));

        var gameEngineAnalysis = ReadMethodAnalysis(
            "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
            "RefreshAcceptedTurnCanonicalStateForValidationAsync");
        var gameEngineMethod = gameEngineAnalysis.Method;
        var helperInvocation = Assert.Single(gameEngineMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>(),
            static invocation => string.Equals(
                ReadInvocationName(invocation),
                "RefreshCanonicalStateAsync",
                StringComparison.Ordinal));
        var helperResultSymbol = RequireAssignedLocalSymbol(
            gameEngineAnalysis,
            helperInvocation);
        var helperAliases = BuildExactCarrierAliases(
            gameEngineAnalysis,
            gameEngineMethod,
            helperResultSymbol,
            helperCarrier.Name);
        var resultType = gameEngineCarrier.DeclaringType;
        Assert.NotNull(resultType);
        var matchingTransfers = gameEngineMethod.DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .Where(statement => statement.Expression is not null)
            .Where(statement => ReturnTransfersExactCarrier(
                gameEngineAnalysis,
                statement,
                helperResultSymbol,
                helperCarrier.Name,
                helperAliases,
                gameEngineCarrier.Name))
            .ToArray();
        Assert.True(
            matchingTransfers.Length == 1,
            "AcceptedTurnCanonicalRefreshResult must receive the exact carrier property " +
            "flowing from AcceptedTurnCanonicalStateRefresh.Result exactly once. " +
            $"Matching ownership transfers: {matchingTransfers.Length}.");
    }

    private async Task<HeldTreatmentPipelineContext>
        CreateHeldTreatmentPipelineContextAsync(
        AcceptedTreatmentPipelineFault? fault)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "boe-held-treatment-pipeline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var fileSystem = new FileSystemManager(
            root,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            fault?.Hooks);
        fileSystem.EnsureDirectoryStructure();
        HeldTreatmentPipelineContext? context = null;
        try
        {
            CopyDirectory(TestRepoPaths.BaseSessionRoot, fileSystem.GameSessionPath);
            var wound = CreateGuaranteedResourceTreatmentWound();
            await SeedHeldTreatmentAuthorityAsync(fileSystem, wound);
            var prepared = await new LiveTurnPreparationService(fileSystem)
                .PrepareAsync(new LiveTurnPreparationOptions
                {
                    SessionId = "session_t070b3_game_engine",
                    RequestId = "request_t070b3_game_engine",
                    TurnNumber = 42,
                    PlayerAction =
                        "Stabilize the accepted wound with the exact held health resource.",
                    CurrentRealm = "Mortal World",
                    PreGeneratedDices1d20 = new[] { 17, 4, 1, 20 }
                });
            Assert.Equal("input/turn_request.json", prepared.TurnRequestPath);
            var parsedContext = MortalWoundTreatmentAuthority.ParseContext(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["realm"] = "mortal_world",
                    ["targetKind"] = "player",
                    ["targetId"] = "player_current",
                    ["providerKind"] = "npc",
                    ["providerId"] = "field_medic_01",
                    ["currentLocationId"] = "loc_field_clinic_001"
                }.ToJsonString(),
                "treatmentContext");
            Assert.True(
                parsedContext.IsValid,
                DescribeValidationIssues(parsedContext.Issues));
            context = new HeldTreatmentPipelineContext(
                root,
                fileSystem,
                Assert.IsType<MortalWoundTreatmentAuthority.Context>(
                    parsedContext.Context),
                fault?.Hooks);
            await context.AcquireLeaseAsync();

            var acceptedState = ExportCurrentTreatmentAcceptedState(context);
            var history = ReadCurrentTreatmentHistory(fileSystem);
            var currentWound = ReadCurrentTreatmentWound(fileSystem);
            var preparedRequest = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
                acceptedState,
                history,
                currentWound,
                HeldTreatmentPipelineContext.OperationKey,
                HeldTreatmentPipelineContext.RouteId,
                Assert.Single(acceptedState.Binding.AcceptedEvents).EventRef);
            Assert.True(
                preparedRequest.IsValid,
                DescribeValidationIssues(preparedRequest.Issues));
            var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
                preparedRequest.Request);
            var resolved = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(
                request,
                history,
                currentWound,
                acceptedState);
            Assert.Equal("Resolved", resolved.Disposition);
            Assert.Empty(resolved.Issues);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(
                resolved.Resolution);
            var commandRoot = WoundResponseInputComposer
                .ComposeMortalWoundTreatmentCommandRoot(
                    acceptedState.Binding,
                    resolution,
                    HeldTreatmentPipelineContext.FinalSceneText);
            var parsedCommand = WoundResponseInputComposer.ParseCommandRoot(
                JsonSerializer.SerializeToElement(commandRoot));
            Assert.True(
                parsedCommand.Success,
                DescribeValidationIssues(parsedCommand.Issues));
            var recomposed = WoundResponseInputComposer.RecomposeCommandRoot(
                acceptedState.Binding,
                parsedCommand,
                Array.Empty<WoundOpportunityDecisionReceipt>());
            Assert.True(
                recomposed.Success,
                DescribeValidationIssues(recomposed.Issues));

            await context.ReleaseLeaseAsync();
            await new StateDistributor(
                    fileSystem,
                    NullLogger<StateDistributor>.Instance)
                .DistributeAsync(
                    new GameResponse
                    {
                        Response = HeldTreatmentPipelineContext.FinalSceneText
                    },
                    recomposed);
            await AddGuardianCleanupCommandSurfaceAsync(fileSystem);
            await context.AcquireLeaseAsync();
            await RestoreHeldTreatmentAndComposeSameSemanticPlanAsync(context);
            return context;
        }
        catch
        {
            if (context is not null)
                await context.DisposeAsync();
            else
                DeleteHeldTreatmentRootBestEffort(root);
            throw;
        }
    }

    private static async Task RestoreHeldTreatmentAndComposeSameSemanticPlanAsync(
        HeldTreatmentPipelineContext context)
    {
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = acceptedState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        var request = Assert.Single(recovered.HeldRequests);
        Assert.Equal("held", request.ResourceAuthority.ReservationDisposition);
        var wound = ReadCurrentTreatmentWound(context.FileSystem);
        var resolved = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(
            request,
            history,
            wound,
            acceptedState);
        Assert.Equal("Resolved", resolved.Disposition);
        Assert.Empty(resolved.Issues);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(
            resolved.Resolution);
        var publication = WoundAcceptedTurnPlanner
            .ComposeMortalWoundTreatmentPublication(
                context.FileSystem,
                context.Lease,
                new GameResponse(),
                acceptedState,
                request,
                resolution);
        Assert.True(
            publication.IsValid,
            DescribeValidationIssues(publication.Issues));
        Assert.Empty(publication.Issues);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(publication.Plan);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out var binding,
            out var cached));
        Assert.True(cached.Success, DescribeValidationIssues(cached.Issues));
        Assert.Same(plan, cached.Plan);
        context.SetCurrentAuthorities(
            acceptedState,
            request,
            resolution,
            plan,
            binding);
    }

    private static MortalWoundTreatmentAcceptedStateAuthority
        ExportCurrentTreatmentAcceptedState(HeldTreatmentPipelineContext context)
    {
        var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(
            context.FileSystem,
            context.Lease,
            context.TreatmentContext,
            HeldTreatmentPipelineContext.WoundId);
        Assert.True(exported.IsValid, DescribeValidationIssues(exported.Issues));
        return Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            exported.Authority);
    }

    private static WoundMaterializationEnvelope ReadCurrentTreatmentWound(
        FileSystemManager fileSystem)
    {
        var root = JsonNode.Parse(File.ReadAllText(fileSystem.ResolvePath(
            WoundCarrierCatalog.PlayerPath)))!.AsObject();
        var woundRoot = Assert.IsType<JsonObject>(Assert.Single(
            root["activeWounds"]!.AsArray()));
        var parsed = WoundMaterializationContract.Parse(
            woundRoot.ToJsonString(),
            WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues));
        return Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
    }

    private static WoundHistoryParseResult ReadCurrentTreatmentHistory(
        FileSystemManager fileSystem)
    {
        var parsed = WoundHistoryState.Parse(
            File.ReadAllText(fileSystem.ResolvePath(WoundHistoryState.HistoryPath)),
            WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues));
        return parsed;
    }

    private static async Task<ResourceHistoryState> ReadTreatmentResourceHistoryAsync(
        FileSystemManager fileSystem)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await fileSystem.ReadFileAsync(
                ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
        var history = ResourceHistoryState.ParseCanonical(
            await fileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            Assert.IsType<ResourceDefinitionCatalog>(definitions.Catalog),
            allowMissingPristine: false);
        Assert.True(history.IsValid, DescribeValidationIssues(history.Issues));
        return Assert.IsType<ResourceHistoryState>(history.History);
    }

    private static int CountHeldTreatmentHealthSpends(ResourceHistoryState history) =>
        history.Transitions.Count(static transition =>
            transition.Operation == ResourceTransitionOperation.Spend &&
            transition.Phase == ResourceMutationPhase.RegisteredSystemOutcome &&
            transition.Coordinate.OwnerKind == ResourceOwnerKind.Player &&
            string.Equals(
                transition.Coordinate.ResourceOwnerId,
                "player_current",
                StringComparison.Ordinal) &&
            string.Equals(
                transition.Coordinate.ResourceKey,
                "health",
                StringComparison.Ordinal));

    private static async Task AssertConfirmedHeldLiveRegistryProbeAsync(
        HeldTreatmentPipelineContext context)
    {
        var finalized = MortalWoundTreatmentResourceComposer.Finalize(
            context.Resolution);
        Assert.True(finalized.IsValid, DescribeValidationIssues(finalized.Issues));
        var finalization = Assert.IsType<MortalWoundTreatmentResourceFinalization>(
            finalized.Finalization);
        var candidates = typeof(AcceptedTurnAuthorityRegistry).Assembly
            .GetTypes()
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic))
            .Where(static method => method.Name.Contains(
                "probe",
                StringComparison.OrdinalIgnoreCase))
            .Where(static method =>
                method.Name.Contains("treatment", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("publication", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("resource", StringComparison.OrdinalIgnoreCase))
            .Select(method => new
            {
                Method = method,
                Arguments = TryBindLiveRegistryProbeArguments(
                    method,
                    context,
                    finalization)
            })
            .Where(static candidate => candidate.Arguments is not null)
            .ToArray();
        Assert.True(
            candidates.Length == 1,
            "Production must expose exactly one non-mutating typed live-registry probe " +
            "for the held-treatment publication authority. Candidates: " +
            string.Join(", ", candidates.Select(static value =>
                value.Method.DeclaringType?.FullName + "." + value.Method.Name)));
        var candidate = candidates[0];
        var raw = candidate.Method.Invoke(null, candidate.Arguments);
        var result = await AwaitReflectedResultAsync(raw);
        var typed = Assert.IsAssignableFrom<object>(result);

        Assert.True(Assert.IsType<bool>(ReadRequiredProbeProperty(
            typed,
            "IsValid")));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            ReadRequiredProbeProperty(typed, "Issues")));
        Assert.Equal(0, Assert.IsType<int>(ReadRequiredProbeProperty(
            typed,
            "ChangedCount")));
        Assert.Equal("ConfirmedHeld", Convert.ToString(
            ReadRequiredProbeProperty(typed, "State")));
        Assert.Equal(context.Request.Coordinates.OperationKey, Convert.ToString(
            ReadRequiredProbeProperty(typed, "OperationKey")));
        Assert.Equal(context.Request.RequestFingerprint, Convert.ToString(
            ReadRequiredProbeProperty(typed, "RequestFingerprint")));
        Assert.Equal(finalization.ResourceAuthorityFingerprint, Convert.ToString(
            ReadRequiredProbeProperty(typed, "ResourceAuthorityFingerprint")));
        Assert.Equal(finalization.FinalizationFingerprint, Convert.ToString(
            ReadRequiredProbeProperty(typed, "FinalizationFingerprint")));
        Assert.Equal(context.OriginalSessionGeneration, Convert.ToString(
            ReadRequiredProbeProperty(typed, "SessionGeneration")));
        Assert.Equal(context.OriginalSessionGenerationRevision, Convert.ToInt64(
            ReadRequiredProbeProperty(typed, "SessionGenerationRevision")));
    }

    private static object?[]? TryBindLiveRegistryProbeArguments(
        MethodInfo method,
        HeldTreatmentPipelineContext context,
        MortalWoundTreatmentResourceFinalization finalization)
    {
        if (!method.IsStatic || method.ContainsGenericParameters ||
            method.GetParameters().Any(static parameter =>
                parameter.ParameterType.IsByRef || parameter.IsOut))
        {
            return null;
        }

        object[] authorities =
        {
            context.FileSystem,
            context.Lease,
            context.AcceptedState,
            context.Request,
            context.Resolution,
            finalization,
            context.Plan,
            context.OriginalBinding
        };
        var arguments = new object?[method.GetParameters().Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            var parameter = method.GetParameters()[index];
            var matches = authorities
                .Where(parameter.ParameterType.IsInstanceOfType)
                .ToArray();
            if (matches.Length == 1)
            {
                arguments[index] = matches[0];
                continue;
            }
            if (parameter.HasDefaultValue)
            {
                arguments[index] = parameter.DefaultValue;
                continue;
            }
            return null;
        }
        return arguments;
    }

    private static async Task<object?> AwaitReflectedResultAsync(object? value)
    {
        if (value is null)
            return null;
        if (value is Task task)
        {
            await task;
            return task.GetType().GetProperty(
                "Result",
                BindingFlags.Instance | BindingFlags.Public)?.GetValue(task);
        }
        var type = value.GetType();
        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var asTask = Assert.IsAssignableFrom<Task>(type.GetMethod(
                "AsTask",
                BindingFlags.Instance | BindingFlags.Public)!.Invoke(value, null));
            await asTask;
            return asTask.GetType().GetProperty(
                "Result",
                BindingFlags.Instance | BindingFlags.Public)?.GetValue(asTask);
        }
        return value;
    }

    private static object ReadRequiredProbeProperty(object value, string name)
    {
        var property = value.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<object>(property.GetValue(value));
    }

    private static void AssertConfirmedHeldBlocksCompetingTreatment(
        HeldTreatmentPipelineContext context)
    {
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = acceptedState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        var durableHeld = Assert.Single(recovered.HeldRequests);
        Assert.Equal(
            context.Request.Coordinates.OperationKey,
            durableHeld.Coordinates.OperationKey);
        Assert.Equal(
            context.Request.Coordinates.AttemptId,
            durableHeld.Coordinates.AttemptId);
        Assert.Equal(context.Request.RequestFingerprint, durableHeld.RequestFingerprint);
        Assert.Equal("held", durableHeld.ResourceAuthority.ReservationDisposition);
        var wound = ReadCurrentTreatmentWound(context.FileSystem);
        var competing = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
            acceptedState,
            history,
            wound,
            HeldTreatmentPipelineContext.OperationKey + "_competing",
            HeldTreatmentPipelineContext.RouteId,
            Assert.Single(acceptedState.Binding.AcceptedEvents).EventRef);
        Assert.False(competing.IsValid);
        Assert.Null(competing.Request);
        Assert.Equal(
            "mortal_wound_treatment_resource_reservation_overbooked",
            Assert.Single(competing.Issues).Code);
    }

    private static async Task<IReadOnlyDictionary<string, ExactFileImage>>
        CaptureExactTreatmentTransactionBytesAsync(
        HeldTreatmentPipelineContext context)
    {
        var paths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(context.Plan.TouchedPaths)
            .Append(AcceptedMechanicsPlan.WoundCommandPath)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var images = new Dictionary<string, ExactFileImage>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var bytes = await context.FileSystem.ReadFileBytesAsync(path);
            images.Add(path, new ExactFileImage(bytes is not null, bytes?.ToArray()));
        }
        return images;
    }

    private static async Task AssertExactTreatmentTransactionBytesAsync(
        HeldTreatmentPipelineContext context,
        IReadOnlyDictionary<string, ExactFileImage> expected)
    {
        foreach (var pair in expected)
        {
            var actual = await context.FileSystem.ReadFileBytesAsync(pair.Key);
            Assert.Equal(pair.Value.Exists, actual is not null);
            if (pair.Value.Exists)
                Assert.Equal(pair.Value.Bytes, actual);
        }
    }

    private static async Task SeedHeldTreatmentAuthorityAsync(
        FileSystemManager fileSystem,
        JsonObject wound)
    {
        await fileSystem.WriteFileAtomicAsync(
            WoundCarrierCatalog.PlayerPath,
            WoundContractTestData.CreatePlayerCarrier(wound).ToJsonString());
        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues));
        var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(
            Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound));
        await fileSystem.WriteFileAtomicAsync(
            WoundIdentityState.StatePath,
            WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry(
                    semanticFingerprint: fingerprint)).ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            WoundHistoryState.HistoryPath,
            WoundContractTestData.CreateHistory().ToJsonString());

        var provider = MortalActorTestFixtures.CreateActor(
            "field_medic_01",
            "loc_field_clinic_001",
            "Field clinic");
        provider["displayName"] = "Field medic";
        provider["activeSkills"] = new JsonArray(
            CreateGuaranteedTreatmentSkill());
        provider["passiveSkills"] = new JsonArray();
        provider["inventory"] = new JsonArray();
        await fileSystem.WriteFileAtomicAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(provider)
            }.ToJsonString());

        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_field_clinic_001",
            "T070-B.3 field clinic");
        await fileSystem.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(location).ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(location).ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationTestFixture.CreateIdentityIndex(location).ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            "game_state/world/world_time.json",
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["currentTimeInMinutes"] = 1_260L
            }.ToJsonString());

        var soulPath = "game_state/meta/soul_state.json";
        var soul = JsonNode.Parse(await fileSystem.ReadFileAsync(soulPath) ?? "{}")
            ?.AsObject() ?? new JsonObject();
        soul["currentRealm"] = "Mortal World";
        await fileSystem.WriteFileAtomicAsync(soulPath, soul.ToJsonString());

        await fileSystem.WriteFileAtomicAsync(
            EffectCarrierCatalog.PlayerPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray()
            }.ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            EffectIdentityState.StatePath,
            EffectMaterializationTestFixture.CreateIdentityIndex().ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            "game_state/player/skills_active.json",
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray()
            }.ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            "game_state/player/skills_passive.json",
            new JsonObject
            {
                ["passiveSkillChanges"] = new JsonArray()
            }.ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            "game_state/player/skill_mastery.json",
            new JsonObject
            {
                ["skillMasteryChanges"] = new JsonArray()
            }.ToJsonString());
        await WriteCanonicalPlayerHealthAuthorityAsync(fileSystem, current: 2);
    }

    private static JsonObject CreateGuaranteedResourceTreatmentWound()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["consequences"]!["ownedEffectSources"]!["definitions"]!
            .AsArray().RemoveAt(1);
        wound["consequences"]!["ownedEffectSources"]!["rootBindings"]!
            .AsArray().RemoveAt(1);
        wound["consequences"]!["entries"]!.AsArray().RemoveAt(1);
        wound["consequences"]!["slotsUsed"] = 1;
        var route = new JsonObject
        {
            ["routeId"] = "guaranteed_t070b3_resource",
            ["displayName"] = "Guaranteed resource stabilization",
            ["visibility"] = "known_to_player",
            ["mode"] = "guaranteed",
            ["requirements"] = new JsonArray(
                new JsonObject
                {
                    ["kind"] = "source_capability",
                    ["capabilityRef"] = "exact_materialized_healing_source",
                    ["actorRole"] = "provider"
                },
                new JsonObject
                {
                    ["kind"] = "resource_quantity",
                    ["resourceRef"] = "health",
                    ["quantity"] = 2,
                    ["ownerRole"] = "target"
                }),
            ["resourcePolicy"] = new JsonObject
            {
                ["reserveBeforeResolution"] = true,
                ["consumeOn"] = new JsonArray("success"),
                ["refundOn"] = new JsonArray(
                    "cancelled",
                    "validation_failed",
                    "rolled_back"),
                ["mutations"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "consume_requirement",
                    ["scope"] = "common",
                    ["milestoneOrdinal"] = null,
                    ["requirementIndex"] = 1
                })
            },
            ["resolution"] = new JsonObject
            {
                ["capabilityRef"] = "exact_materialized_healing_source",
                ["actorRole"] = "provider"
            },
            ["outcomes"] = new JsonArray(new JsonObject
            {
                ["category"] = "success",
                ["result"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "stabilize"
                })
            }),
            ["interruption"] = null
        };
        wound["treatment"]!["routes"] = new JsonArray(route);
        wound["treatment"]!["knownRouteIds"] = new JsonArray(
            "guaranteed_t070b3_resource");
        return wound;
    }

    private static JsonObject CreateGuaranteedTreatmentSkill() => new()
    {
        ["skillId"] = "skill_guaranteed_care_t070b3",
        ["displayName"] = "Guaranteed Care",
        ["currentMasteryLevel"] = 3,
        ["skillName"] = "Guaranteed Care",
        ["skillDescription"] = "Provides exact materialized healing authority.",
        ["rarity"] = "Common",
        ["actionCost"] = "Main",
        ["combatEffect"] = new JsonObject
        {
            ["isActivatedEffect"] = true,
            ["actionName"] = "Field treatment",
            ["effects"] = new JsonArray(new JsonObject
            {
                ["effectType"] = "Damage",
                ["value"] = "10%",
                ["targetType"] = "Enemy",
                ["effectDescription"] = "A controlled intervention.",
                ["poiseDamage"] = "5%"
            })
        },
        ["mortalWoundTreatmentCapabilities"] = new JsonArray(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["capabilityRef"] = "exact_materialized_healing_source",
            ["woundDomain"] = "physical",
            ["minimumSeverityRank"] = 1,
            ["maximumSeverityRank"] = 4,
            ["operationLimits"] = new JsonObject
            {
                ["mayStabilize"] = true,
                ["maximumRecoveryPoints"] = 2,
                ["maximumSeverityReductionSteps"] = 1,
                ["removableComplicationKinds"] = new JsonArray("infection"),
                ["mayHealAtSeverityI"] = true,
                ["maximumCosmeticHealLegacies"] = 1,
                ["maximumMechanicalEffectHealLegacies"] = 4
            }
        })
    };

    private static async Task WriteCanonicalPlayerHealthAuthorityAsync(
        FileSystemManager fileSystem,
        int current)
    {
        const string initializeFingerprint =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string damageFingerprint =
            "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("health", out var definition));
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current",
            "health");
        var binding = new ResourceCapacityBinding(
            definition!.CapacityPolicy.Kind,
            definition.CapacityPolicy.FormulaKey!,
            initializeFingerprint);
        var initialized = new ResourceStateSnapshot(
            10,
            10,
            binding,
            ResourceLifecycleState.Active);
        var initialize = new ResourceTransition(
            "transition_health_initialize_t070b3",
            "operation_health_initialize_t070b3",
            "turn_1:resource:t070b3",
            "bootstrap_materialization",
            "mortal_incarnation_1",
            ResourceMutationPhase.RegisteredSystemOutcome,
            40,
            0,
            coordinate,
            ResourceTransitionOperation.Initialize,
            0,
            0,
            ResourceTransitionOutcome.Applied,
            ResourceCapacityDisposition.InitializeFromDefinition,
            null,
            initialized,
            new ResourceSourceEvidence(
                "bootstrap_materialization",
                "mortal_incarnation_1",
                initializeFingerprint),
            initializeFingerprint,
            null,
            1);
        var damaged = new ResourceTransition(
            "transition_health_damage_t070b3",
            "operation_health_damage_t070b3",
            "turn_2:resource:t070b3",
            "combat_outcome",
            "wound_fixture_damage_t070b3",
            ResourceMutationPhase.DirectOutcome,
            100,
            0,
            coordinate,
            ResourceTransitionOperation.Damage,
            10 - current,
            10 - current,
            ResourceTransitionOutcome.Applied,
            null,
            initialized,
            initialized with { Current = current },
            new ResourceSourceEvidence(
                "combat_outcome",
                "wound_fixture_damage_t070b3",
                damageFingerprint),
            damageFingerprint,
            null,
            2);
        var historyResult = ResourceHistoryState.CreateValidated(
            new[] { initialize, damaged },
            definitions);
        Assert.True(
            historyResult.IsValid,
            DescribeValidationIssues(historyResult.Issues));
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                coordinate,
                current,
                10,
                binding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    1,
                    initialize.EventRef,
                    damaged.TransitionId,
                    damaged.EventRef,
                    damaged.Turn))
        });
        Assert.Empty(historyResult.History!.ValidateStateAgreement(state));
        await fileSystem.WriteFileAtomicAsync(
            ResourceMaterializationContract.DefinitionsPath,
            definitions.ToCanonicalJson());
        await fileSystem.WriteFileAtomicAsync(
            ResourceMaterializationContract.StatePath,
            state.ToCanonicalJson());
        await fileSystem.WriteFileAtomicAsync(
            ResourceMaterializationContract.HistoryPath,
            historyResult.History.ToCanonicalJson());
        var composed = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            fileSystem.ReadFileAsync,
            state,
            historyResult.History,
            CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        Assert.True(composed.IsValid, DescribeValidationIssues(composed.Issues));
        await fileSystem.WriteFileAtomicAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            Assert.IsType<string>(composed.CanonicalAuthorityJson));
    }

    private static async Task AddGuardianCleanupCommandSurfaceAsync(
        FileSystemManager fileSystem)
    {
        const string path = "game_state/meta/guardians.json";
        var root = JsonNode.Parse(await fileSystem.ReadFileAsync(path) ?? "{}")
            ?.AsObject() ?? new JsonObject();
        root[GuardianProjectState.QuestProgressUpdatesProperty] = new JsonArray();
        await fileSystem.WriteFileAtomicAsync(path, root.ToJsonString());
    }

    private static string DescribeValidationIssues(
        IEnumerable<ValidationIssue> issues) => string.Join(
        " | ",
        issues.Select(static issue =>
            $"{issue.Code}@{issue.FilePath}: {issue.Actual}"));

    private static void DeleteHeldTreatmentRootBestEffort(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        var tempPrefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullRoot.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullRoot).StartsWith(
                "boe-held-treatment-pipeline-",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to remove unexpected held-treatment test root '{fullRoot}'.");
        }
        try
        {
            if (Directory.Exists(fullRoot))
                Directory.Delete(fullRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record ExactFileImage(bool Exists, byte[]? Bytes);

    private sealed record SourceMethodAnalysis(
        MethodDeclarationSyntax Method,
        SemanticModel Model);

    private sealed record CarrierOwnershipScope(
        StatementSyntax Statement,
        SyntaxNode Lifetime,
        ILocalSymbol RefreshResultSymbol,
        ILocalSymbol CarrierSymbol,
        string CarrierPropertyName);

    private static readonly Lazy<IReadOnlyList<MetadataReference>>
        SourceGuardMetadataReferences = new(CreateSourceGuardMetadataReferences);

    private static IReadOnlyList<MetadataReference>
        CreateSourceGuardMetadataReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trustedPlatformAssemblies = AppContext.GetData(
            "TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            foreach (var path in trustedPlatformAssemblies.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                paths.Add(path);
            }
        }
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic ||
                assembly == typeof(GameEngineTurnLifecycleTests).Assembly ||
                string.IsNullOrWhiteSpace(assembly.Location))
                continue;
            paths.Add(assembly.Location);
        }
        paths.Add(typeof(GameEngine).Assembly.Location);
        return paths
            .Where(File.Exists)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    private sealed class HeldTreatmentPipelineContext : IAsyncDisposable
    {
        private FileSystemManager.CanonicalWriteLease? _lease;
        private readonly FileSystemManagerHooks? _hooks;

        internal HeldTreatmentPipelineContext(
            string root,
            FileSystemManager fileSystem,
            MortalWoundTreatmentAuthority.Context treatmentContext,
            FileSystemManagerHooks? hooks)
        {
            Root = root;
            FileSystem = fileSystem;
            TreatmentContext = treatmentContext;
            _hooks = hooks;
        }

        internal const int Turn = 42;
        internal const string WoundId = "wound_test_torn_side";
        internal const string RouteId = "guaranteed_t070b3_resource";
        internal const string OperationKey = "operation_t070b3_game_engine";
        internal const string FinalSceneText =
            "The exact treatment stabilizes the wound without losing its held resource.";

        internal string Root { get; }
        internal FileSystemManager FileSystem { get; private set; }
        internal MortalWoundTreatmentAuthority.Context TreatmentContext { get; }
        internal FileSystemManager.CanonicalWriteLease Lease =>
            _lease ?? throw new InvalidOperationException(
                "The held-treatment fixture lease is not active.");
        internal MortalWoundTreatmentAcceptedStateAuthority AcceptedState { get; private set; } = null!;
        internal MortalWoundTreatmentAttemptRequest Request { get; private set; } = null!;
        internal MortalWoundTreatmentResolution Resolution { get; private set; } = null!;
        internal AcceptedMechanicsPlan Plan { get; private set; } = null!;
        internal AcceptedMechanicsPlanBinding OriginalBinding { get; private set; } = null!;
        internal string OriginalSessionGeneration { get; private set; } = string.Empty;
        internal long OriginalSessionGenerationRevision { get; private set; } = -1;

        internal void SetCurrentAuthorities(
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentResolution resolution,
            AcceptedMechanicsPlan plan,
            AcceptedMechanicsPlanBinding binding)
        {
            AcceptedState = acceptedState;
            Request = request;
            Resolution = resolution;
            Plan = plan;
            if (OriginalBinding is null)
            {
                OriginalBinding = binding;
                OriginalSessionGeneration = FileSystem.GetOrCreateSessionGeneration(Lease);
                OriginalSessionGenerationRevision = FileSystem
                    .CanonicalRootAuthorityIdentity
                    .SessionGenerationRevision;
            }
        }

        internal async Task AcquireLeaseAsync()
        {
            Assert.Null(_lease);
            _lease = await FileSystem.AcquireCanonicalWriteLeaseAsync();
        }

        internal async Task ReleaseLeaseAsync()
        {
            if (_lease is null)
                return;
            await _lease.DisposeAsync();
            _lease = null;
        }

        internal async Task RestartAsync(FileSystemManagerHooks? hooks)
        {
            await ReleaseLeaseAsync();
            FileSystem = new FileSystemManager(
                Root,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks ?? _hooks);
            FileSystem.EnsureDirectoryStructure();
            await AcquireLeaseAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await ReleaseLeaseAsync();
            DeleteHeldTreatmentRootBestEffort(Root);
        }
    }

    private sealed class AcceptedTreatmentPipelineFault
    {
        private readonly string _boundary;
        private readonly int _targetOccurrence;
        private readonly HashSet<string> _observedPhases = new(StringComparer.Ordinal);
        private int _matchingOccurrences;
        private bool _armed;
        private HeldTreatmentPipelineContext? _context;

        internal AcceptedTreatmentPipelineFault(
            string boundary,
            int targetOccurrence)
        {
            _boundary = boundary;
            _targetOccurrence = targetOccurrence;
            Hooks = new FileSystemManagerHooks
            {
                AfterCanonicalReadInitialValidationAsync = OnCanonicalReadAsync,
                BeforeCanonicalMutationAsync = OnCanonicalMutationAsync
            };
        }

        internal FileSystemManagerHooks Hooks { get; }
        internal bool Fired { get; private set; }
        internal IReadOnlyCollection<string> ObservedPhases => _observedPhases;

        internal void Arm(HeldTreatmentPipelineContext? context = null)
        {
            _context = context;
            _armed = true;
        }

        private Task OnCanonicalReadAsync(string path)
        {
            if (!_armed || Fired)
                return Task.CompletedTask;
            var phase = ClassifyCurrentPhase(path, isMutation: false);
            if (phase is null)
                return Task.CompletedTask;
            _observedPhases.Add(phase);
            var targetPhase = string.Equals(
                _boundary,
                "transaction_commit_conflict",
                StringComparison.Ordinal)
                ? "runtime_refresh"
                : _boundary;
            if (!string.Equals(phase, targetPhase, StringComparison.Ordinal))
                return Task.CompletedTask;
            _matchingOccurrences++;
            if (_matchingOccurrences != _targetOccurrence)
                return Task.CompletedTask;
            Fired = true;
            if (string.Equals(
                    _boundary,
                    "transaction_commit_conflict",
                    StringComparison.Ordinal))
            {
                return InvalidateAcceptedPlanFenceAsync();
            }
            return Task.FromException(new IOException(
                $"Injected held-treatment pipeline failure at '{phase}' ({path})."));
        }

        private Task OnCanonicalMutationAsync(string path)
        {
            if (!_armed || Fired)
                return Task.CompletedTask;
            var phase = ClassifyCurrentPhase(path, isMutation: true);
            if (phase is null)
                return Task.CompletedTask;
            _observedPhases.Add(phase);
            if (!string.Equals(phase, _boundary, StringComparison.Ordinal))
                return Task.CompletedTask;
            _matchingOccurrences++;
            if (_matchingOccurrences != _targetOccurrence)
                return Task.CompletedTask;
            Fired = true;
            return Task.FromException(new IOException(
                $"Injected held-treatment pipeline failure at '{phase}' ({path})."));
        }

        private async Task InvalidateAcceptedPlanFenceAsync()
        {
            var context = Assert.IsType<HeldTreatmentPipelineContext>(_context);
            await using var lease = await context.FileSystem
                .AcquireCanonicalWriteLeaseAsync();
            AcceptedMechanicsPlanAuthority.InvalidateValidated(
                context.FileSystem,
                lease);
        }

        private static string? ClassifyCurrentPhase(string path, bool isMutation)
        {
            if (isMutation)
            {
                return string.Equals(
                           path,
                           "game_state/meta/guardians.json",
                           StringComparison.Ordinal) &&
                       StackContains(
                           "RemoveGuardianQuestProgressUpdatesCommandSurfaceAsync")
                    ? "cleanup"
                    : null;
            }

            if (StackContains("BindPublishedAcceptedWoundOutputAsync"))
                return "wound_output";
            if (StackContains("ValidateAcceptedWoundPostPublicationAuthorityAsync") ||
                StackContains("ValidatePublishedOutputAuthorityAsync"))
            {
                return "wound_post_seal";
            }
            if (StackContains("ValidateCriticalCanonicalStateAsync"))
                return "critical_validation";
            if (StackContains("ValidateGameStateAsync"))
                return "full_state_validation";
            if (string.Equals(
                    path,
                    ResourceMaterializationContract.DefinitionsPath,
                    StringComparison.Ordinal) &&
                StackContains("RefreshGameStateCoreAsync") &&
                (StackContains("RefreshCanonicalStateAsync") ||
                 StackContains("ValidateAcceptedTurnOutcomeWithRepairLoopAsync") &&
                 !StackContains("EnsureClientOwnedSystemFilesHealthyAsync")))
            {
                return "runtime_refresh";
            }
            return null;
        }

        private static bool StackContains(string marker) =>
            new StackTrace().GetFrames().Any(frame =>
            {
                var method = frame.GetMethod();
                return method?.Name.Contains(marker, StringComparison.Ordinal) == true ||
                       method?.DeclaringType?.FullName?.Contains(
                           marker,
                           StringComparison.Ordinal) == true;
            });
    }

    private static PropertyInfo RequireOpenTreatmentTransactionCarrier()
    {
        var resultType = typeof(AcceptedTurnCanonicalStateRefresh.Result);
        var candidates = resultType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(static property =>
                property.PropertyType != typeof(AcceptedMechanicsPlan) &&
                property.PropertyType != typeof(IReadOnlyList<ValidationIssue>))
            .Where(static property =>
                property.Name.Contains("transaction", StringComparison.OrdinalIgnoreCase) ||
                HasTransactionLifecycle(property.PropertyType))
            .ToArray();

        Assert.True(
            candidates.Length == 1,
            "AcceptedTurnCanonicalStateRefresh.Result must carry exactly one internal opaque " +
            "open held-treatment transaction. Found: " +
            string.Join(", ", candidates.Select(static value =>
                value.Name + ":" + value.PropertyType.FullName)));
        return candidates[0];
    }

    private static bool HasTransactionLifecycle(Type type)
    {
        var methods = type.GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return methods.Any(static method => IsCompletionMethodName(method.Name)) &&
               methods.Any(static method => IsCompensationMethodName(method.Name));
    }

    private static PropertyInfo RequireGameEngineRefreshTransactionCarrier(Type carrierType)
    {
        var resultType = typeof(GameEngine).GetNestedType(
            "AcceptedTurnCanonicalRefreshResult",
            BindingFlags.NonPublic);
        Assert.NotNull(resultType);
        var candidates = resultType!
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.PropertyType == carrierType)
            .ToArray();
        Assert.True(
            candidates.Length == 1,
            "AcceptedTurnCanonicalRefreshResult must carry the exact open transaction " +
            "returned by AcceptedTurnCanonicalStateRefresh.Result. Found: " +
            string.Join(", ", candidates.Select(static value =>
                value.Name + ":" + value.PropertyType.FullName)));
        return candidates[0];
    }

    private static bool IsCompletionMethodName(string name) =>
        name.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("commit", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("finalize", StringComparison.OrdinalIgnoreCase);

    private static bool IsCompensationMethodName(string name) =>
        name.Contains("compensate", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("rollback", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("abort", StringComparison.OrdinalIgnoreCase);

    private static MethodInfo RequireExactCarrierCompensationMethod()
    {
        var carrierType = RequireOpenTreatmentTransactionCarrier().PropertyType;
        return Assert.Single(
            carrierType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.DeclaringType == carrierType &&
                      IsCompensationMethodName(method.Name));
    }

    private static bool IsExactCarrierCompensationLifecycleName(
        string name,
        string exactCompensationMethodName) =>
        string.Equals(
            name,
            exactCompensationMethodName,
            StringComparison.Ordinal) ||
        string.Equals(
            name,
            nameof(IAsyncDisposable.DisposeAsync),
            StringComparison.Ordinal);

    private static bool IsResolvedInstanceInvocation(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax invocation) =>
        analysis.Model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
        !method.IsStatic &&
        string.Equals(
            method.Name,
            ReadInvocationName(invocation),
            StringComparison.Ordinal);

    private static bool IsAwaitableReturn(Type type) =>
        type == typeof(Task) ||
        type == typeof(ValueTask) ||
        (type.IsGenericType &&
         (type.GetGenericTypeDefinition() == typeof(Task<>) ||
          type.GetGenericTypeDefinition() == typeof(ValueTask<>)));

    private static MethodDeclarationSyntax ReadMethod(
        string relativePath,
        string methodName) => ReadMethodAnalysis(relativePath, methodName).Method;

    private static SourceMethodAnalysis ReadMethodAnalysis(
        string relativePath,
        string methodName)
    {
        var absolutePath = Path.Combine(
            TestRepoPaths.RepoRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        var source = File.ReadAllText(absolutePath);
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview),
            absolutePath);
        var compilation = CSharpCompilation.Create(
            "BookOfEternityClient.IntegrationTests",
            new[] { tree },
            SourceGuardMetadataReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        var root = tree.GetRoot();
        var method = Assert.Single(root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>(),
            method => string.Equals(
                method.Identifier.ValueText,
                methodName,
                StringComparison.Ordinal));
        return new SourceMethodAnalysis(
            method,
            compilation.GetSemanticModel(tree, ignoreAccessibility: false));
    }

    private static CarrierOwnershipScope RequireCanonicalRefreshTransactionScope(
        SourceMethodAnalysis analysis)
    {
        var method = analysis.Method;
        var carrierType = RequireOpenTreatmentTransactionCarrier().PropertyType;
        var gameEngineCarrier = RequireGameEngineRefreshTransactionCarrier(carrierType);
        var refreshInvocation = Assert.Single(method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>(),
            static invocation => string.Equals(
                ReadInvocationName(invocation),
                "RefreshAcceptedTurnCanonicalStateForValidationAsync",
                StringComparison.Ordinal));
        var refreshResultSymbol = RequireAssignedLocalSymbol(
            analysis,
            refreshInvocation);
        Assert.Equal(
            "AcceptedTurnCanonicalRefreshResult",
            refreshResultSymbol.Type.Name);

        var assignmentStatement = refreshInvocation.Ancestors()
            .OfType<StatementSyntax>()
            .First();
        StatementSyntax ownershipAnchor = assignmentStatement;
        var refreshTry = refreshInvocation.Ancestors()
            .OfType<TryStatementSyntax>()
            .Where(statement => statement.Block.Span.Contains(refreshInvocation.Span))
            .MinBy(static statement => statement.Block.Span.Length);
        if (refreshTry is not null)
        {
            Assert.Same(refreshTry.Block, assignmentStatement.Parent);
            var assignmentIndex = refreshTry.Block.Statements.IndexOf(
                assignmentStatement);
            Assert.True(assignmentIndex >= 0);
            Assert.DoesNotContain(
                refreshTry.Block.Statements.Skip(assignmentIndex + 1),
                IsExecutableSourceStatement);
            Assert.False(
                refreshTry.Finally?.Block.Statements.Any(
                    IsExecutableSourceStatement) == true,
                "A finally body would execute after canonical refresh returned its " +
                "carrier but before the exact await-using ownership capture.");
            ownershipAnchor = refreshTry;
        }
        var owningBlock = Assert.IsType<BlockSyntax>(ownershipAnchor.Parent);
        var anchorIndex = owningBlock.Statements.IndexOf(ownershipAnchor);
        Assert.True(anchorIndex >= 0);
        Assert.True(
            anchorIndex + 1 < owningBlock.Statements.Count,
            "The exact transaction returned by canonical refresh must be captured " +
            "before the accepted-turn loop executes any other statement.");
        var scopeStatement = owningBlock.Statements[anchorIndex + 1];

        VariableDeclaratorSyntax carrierVariable;
        SyntaxNode lifetime;
        if (scopeStatement is LocalDeclarationStatementSyntax localDeclaration)
        {
            Assert.False(localDeclaration.AwaitKeyword.IsKind(SyntaxKind.None));
            Assert.False(localDeclaration.UsingKeyword.IsKind(SyntaxKind.None));
            carrierVariable = Assert.Single(localDeclaration.Declaration.Variables);
            lifetime = owningBlock;
        }
        else
        {
            var usingStatement = Assert.IsType<UsingStatementSyntax>(scopeStatement);
            Assert.False(usingStatement.AwaitKeyword.IsKind(SyntaxKind.None));
            Assert.NotNull(usingStatement.Declaration);
            carrierVariable = Assert.Single(usingStatement.Declaration!.Variables);
            lifetime = usingStatement.Statement;
        }
        var initializer = Assert.IsAssignableFrom<ExpressionSyntax>(
            carrierVariable.Initializer?.Value);
        Assert.True(
            ExpressionIsExactCarrierIdentity(
                analysis,
                initializer,
                refreshResultSymbol,
                gameEngineCarrier.Name,
                new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default)),
            "The first post-refresh executable statement must await-use the exact " +
            "transaction carrier flowing from AcceptedTurnCanonicalRefreshResult " +
            "without a conditional, coalesce, or wrapper invocation.");
        var carrierSymbol = Assert.IsAssignableFrom<ILocalSymbol>(
            analysis.Model.GetDeclaredSymbol(carrierVariable));
        return new CarrierOwnershipScope(
            scopeStatement,
            lifetime,
            refreshResultSymbol,
            carrierSymbol,
            gameEngineCarrier.Name);
    }

    private static bool IsExecutableSourceStatement(StatementSyntax statement) =>
        statement is not EmptyStatementSyntax and not LocalFunctionStatementSyntax;

    private static ILocalSymbol RequireAssignedLocalSymbol(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax invocation)
    {
        var declarator = invocation.AncestorsAndSelf()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(candidate => candidate.Initializer?.Value.Span.Contains(
                invocation.Span) == true);
        if (declarator is not null)
        {
            return Assert.IsAssignableFrom<ILocalSymbol>(
                analysis.Model.GetDeclaredSymbol(declarator));
        }
        var assignment = invocation.AncestorsAndSelf()
            .OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(candidate => candidate.Right.Span.Contains(invocation.Span));
        Assert.NotNull(assignment);
        return Assert.IsAssignableFrom<ILocalSymbol>(
            analysis.Model.GetSymbolInfo(assignment!.Left).Symbol);
    }

    private static InvocationExpressionSyntax RequireDirectAwaitedTypedCompletion(
        SourceMethodAnalysis analysis,
        CarrierOwnershipScope scope)
    {
        var carrierType = RequireOpenTreatmentTransactionCarrier().PropertyType;
        var complete = Assert.Single(
            carrierType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.DeclaringType == carrierType &&
                      IsCompletionMethodName(method.Name));
        var aliases = BuildImmutableLocalAliases(
            analysis,
            scope.Lifetime,
            new[] { scope.CarrierSymbol });
        var completions = scope.Lifetime.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation =>
                string.Equals(
                    ReadInvocationName(invocation),
                    complete.Name,
                    StringComparison.Ordinal) &&
                IsResolvedInstanceInvocation(analysis, invocation) &&
                InvocationUsesCarrierSymbol(analysis, invocation, aliases))
            .ToArray();
        Assert.True(
            completions.Length == 1,
            "The accepted-turn coordinator must invoke completion exactly once on " +
            "the exact await-using transaction authority.");
        var completion = completions[0];
        var awaited = completion.Parent as AwaitExpressionSyntax;
        Assert.NotNull(awaited);
        Assert.True(
            ReferenceEquals(completion, awaited!.Expression),
            "Transaction completion must be directly awaited, not hidden behind " +
            "a fire-and-forget or unrelated wrapper invocation.");
        _ = RequireAssignedLocalSymbol(analysis, completion);

        var resultType = RequireAwaitedResultType(complete.ReturnType);
        Assert.Equal(typeof(bool), RequireResultProperty(resultType, "IsValid").PropertyType);
        Assert.True(typeof(IEnumerable<ValidationIssue>).IsAssignableFrom(
            RequireResultProperty(resultType, "Issues").PropertyType));
        Assert.Equal(typeof(int), RequireResultProperty(
            resultType,
            "ChangedCount").PropertyType);
        var outcomeType = RequireResultProperty(resultType, "Outcome").PropertyType;
        Assert.True(
            outcomeType == typeof(string) || outcomeType.IsEnum,
            "Typed completion Outcome may be a string or closed enum, but it must " +
            "represent Finalized explicitly.");
        return completion;
    }

    private static Type RequireAwaitedResultType(Type awaitableType)
    {
        Assert.True(
            awaitableType.IsGenericType &&
            (awaitableType.GetGenericTypeDefinition() == typeof(Task<>) ||
             awaitableType.GetGenericTypeDefinition() == typeof(ValueTask<>)),
            "Transaction completion must return an awaited typed result; Task/ValueTask " +
            "without IsValid/Issues/ChangedCount/Outcome cannot gate accepted success.");
        return awaitableType.GetGenericArguments()[0];
    }

    private static PropertyInfo RequireResultProperty(Type resultType, string name)
    {
        var property = resultType.GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property!;
    }

    private static void RequireTypedCompletionSuccessDominatesAcceptedExit(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax completion,
        AssignmentExpressionSyntax notificationAssignment,
        ReturnStatementSyntax acceptedReturn)
    {
        var resultSymbol = RequireAssignedLocalSymbol(analysis, completion);
        var resultAliases = BuildImmutableLocalAliases(
            analysis,
            analysis.Method,
            new[] { resultSymbol });
        var graph = RequireControlFlowGraph(analysis);
        var completionBlock = RequireBlockContaining(graph, completion);
        var notificationBlock = RequireBlockContaining(graph, notificationAssignment);
        var acceptedReturnBlock = RequireBlockContaining(graph, acceptedReturn);
        var dominators = ComputeDominators(graph);
        var requiredProperties = new[]
        {
            "IsValid",
            "Issues",
            "ChangedCount",
            "Outcome"
        };

        foreach (var propertyName in requiredProperties)
        {
            var guards = graph.Blocks
                .Where(block => block.BranchValue is not null)
                .Where(block => OperationTree(block.BranchValue!).Any(operation =>
                    operation is IPropertyReferenceOperation property &&
                    string.Equals(
                        property.Property.Name,
                        propertyName,
                        StringComparison.Ordinal) &&
                    OperationIsIdentityLocalReference(
                        property.Instance,
                        resultAliases)))
                .Select(block => new
                {
                    Block = block,
                    ExpectedConditionValue = CompletionConditionExpectedValue(
                        analysis,
                        block.BranchValue!.Syntax,
                        propertyName,
                        resultAliases)
                })
                .Where(candidate => candidate.ExpectedConditionValue.HasValue)
                .Where(candidate => Dominates(
                                        dominators,
                                        candidate.Block,
                                        notificationBlock) &&
                                    Dominates(
                                        dominators,
                                        candidate.Block,
                                        acceptedReturnBlock))
                .Where(candidate => BranchRejectsUnexpectedCompletionValue(
                    candidate.Block,
                    candidate.ExpectedConditionValue.GetValueOrDefault(),
                    completionBlock,
                    notificationBlock,
                    acceptedReturnBlock))
                .Select(static candidate => candidate.Block)
                .ToArray();
            Assert.True(
                guards.Length > 0,
                $"Typed completion property '{propertyName}' must be checked for its " +
                "successful value on every path before notifications and return true.");
            Assert.All(guards, guard => Assert.True(
                Dominates(dominators, completionBlock, guard),
                $"Completion result guard '{propertyName}' cannot execute before " +
                "the direct awaited completion."));
        }
    }

    private static bool? CompletionConditionExpectedValue(
        SourceMethodAnalysis analysis,
        SyntaxNode condition,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases)
    {
        return propertyName switch
        {
            "IsValid" => ExactBooleanCompletionPredicateExpectedValue(
                analysis,
                condition,
                propertyName,
                resultAliases),
            "Issues" => ExactEmptyIssuesPredicateExpectedValue(
                analysis,
                condition,
                resultAliases),
            "ChangedCount" => ExactCompletionComparisonExpectedValue(
                analysis,
                condition,
                propertyName,
                resultAliases,
                expression => IsInt32Constant(analysis, expression, 1)),
            "Outcome" => ExactCompletionComparisonExpectedValue(
                analysis,
                condition,
                propertyName,
                resultAliases,
                expression => IsFinalizedCompletionValue(analysis, expression)),
            _ => null
        };
    }

    private static bool? ExactBooleanCompletionPredicateExpectedValue(
        SourceMethodAnalysis analysis,
        SyntaxNode condition,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases)
    {
        if (condition is not ExpressionSyntax expression)
            return null;
        var negated = UnwrapLogicalNegation(ref expression);
        if (IsDirectCompletionPropertyReference(
                analysis,
                expression,
                propertyName,
                resultAliases))
        {
            return !negated;
        }
        expression = StripParentheses(expression);
        if (expression is not BinaryExpressionSyntax binary ||
            !binary.IsKind(SyntaxKind.EqualsExpression) &&
            !binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return null;
        }
        ExpressionSyntax constant;
        if (IsDirectCompletionPropertyReference(
                analysis,
                binary.Left,
                propertyName,
                resultAliases))
        {
            constant = binary.Right;
        }
        else if (IsDirectCompletionPropertyReference(
                     analysis,
                     binary.Right,
                     propertyName,
                     resultAliases))
        {
            constant = binary.Left;
        }
        else
        {
            return null;
        }
        if (analysis.Model.GetConstantValue(constant) is not
            { HasValue: true, Value: bool comparedValue })
        {
            return null;
        }
        var valueWhenExpected = binary.IsKind(SyntaxKind.EqualsExpression)
            ? comparedValue
            : !comparedValue;
        return negated ? !valueWhenExpected : valueWhenExpected;
    }

    private static bool? ExactEmptyIssuesPredicateExpectedValue(
        SourceMethodAnalysis analysis,
        SyntaxNode condition,
        IReadOnlySet<ILocalSymbol> resultAliases)
    {
        if (condition is not ExpressionSyntax expression)
            return null;
        var negated = UnwrapLogicalNegation(ref expression);
        expression = StripParentheses(expression);
        if (expression is InvocationExpressionSyntax anyInvocation &&
            string.Equals(
                ReadInvocationName(anyInvocation),
                "Any",
                StringComparison.Ordinal) &&
            InvocationDirectlyReadsCompletionProperty(
                analysis,
                anyInvocation,
                "Issues",
                resultAliases))
        {
            return negated;
        }
        if (expression is not BinaryExpressionSyntax binary ||
            !binary.IsKind(SyntaxKind.EqualsExpression) &&
            !binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return null;
        }
        var exactCountComparison =
            IsIssuesCountSide(analysis, binary.Left, resultAliases) &&
            IsInt32Constant(analysis, binary.Right, 0) ||
            IsIssuesCountSide(analysis, binary.Right, resultAliases) &&
            IsInt32Constant(analysis, binary.Left, 0);
        if (!exactCountComparison)
            return null;
        var valueWhenExpected = binary.IsKind(SyntaxKind.EqualsExpression);
        return negated ? !valueWhenExpected : valueWhenExpected;
    }

    private static bool IsIssuesCountSide(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        IReadOnlySet<ILocalSymbol> resultAliases) =>
        StripParentheses(expression) is MemberAccessExpressionSyntax member &&
        member.Name.Identifier.ValueText is "Count" or "Length" &&
        IsDirectCompletionPropertyReference(
            analysis,
            member.Expression,
            "Issues",
            resultAliases);

    private static bool? ExactCompletionComparisonExpectedValue(
        SourceMethodAnalysis analysis,
        SyntaxNode condition,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases,
        Func<ExpressionSyntax, bool> isExpected)
    {
        if (condition is not ExpressionSyntax expression)
            return null;
        var negated = UnwrapLogicalNegation(ref expression);
        expression = StripParentheses(expression);
        bool? valueWhenExpected = expression switch
        {
            BinaryExpressionSyntax binary
                when binary.IsKind(SyntaxKind.EqualsExpression) ||
                     binary.IsKind(SyntaxKind.NotEqualsExpression) =>
                ExactBinaryCompletionComparisonExpectedValue(
                    analysis,
                    binary,
                    propertyName,
                    resultAliases,
                    isExpected),
            InvocationExpressionSyntax invocation
                when string.Equals(
                    ReadInvocationName(invocation),
                    nameof(object.Equals),
                    StringComparison.Ordinal) &&
                     InvocationExactlyComparesCompletionProperty(
                         analysis,
                         invocation,
                         propertyName,
                         resultAliases,
                         isExpected) => true,
            IsPatternExpressionSyntax pattern
                when IsDirectCompletionPropertyReference(
                         analysis,
                         pattern.Expression,
                         propertyName,
                         resultAliases) &&
                     pattern.Pattern is ConstantPatternSyntax constant &&
                     isExpected(constant.Expression) => true,
            _ => null
        };
        return valueWhenExpected.HasValue && negated
            ? !valueWhenExpected.Value
            : valueWhenExpected;
    }

    private static bool? ExactBinaryCompletionComparisonExpectedValue(
        SourceMethodAnalysis analysis,
        BinaryExpressionSyntax binary,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases,
        Func<ExpressionSyntax, bool> isExpected)
    {
        var exactOperands =
            IsDirectCompletionPropertyReference(
                analysis,
                binary.Left,
                propertyName,
                resultAliases) && isExpected(binary.Right) ||
            IsDirectCompletionPropertyReference(
                analysis,
                binary.Right,
                propertyName,
                resultAliases) && isExpected(binary.Left);
        if (!exactOperands)
            return null;
        return binary.IsKind(SyntaxKind.EqualsExpression);
    }

    private static bool InvocationExactlyComparesCompletionProperty(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax invocation,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases,
        Func<ExpressionSyntax, bool> isExpected)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax member &&
            IsDirectCompletionPropertyReference(
                analysis,
                member.Expression,
                propertyName,
                resultAliases))
        {
            return invocation.ArgumentList.Arguments is [{ Expression: var argument }] &&
                   isExpected(argument);
        }
        if (invocation.ArgumentList.Arguments is not
            [{ Expression: var left }, { Expression: var right }])
        {
            return false;
        }
        return IsDirectCompletionPropertyReference(
                   analysis,
                   left,
                   propertyName,
                   resultAliases) && isExpected(right) ||
               IsDirectCompletionPropertyReference(
                   analysis,
                   right,
                   propertyName,
                   resultAliases) && isExpected(left);
    }

    private static bool InvocationDirectlyReadsCompletionProperty(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax invocation,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax member &&
            IsDirectCompletionPropertyReference(
                analysis,
                member.Expression,
                propertyName,
                resultAliases))
        {
            return true;
        }
        return invocation.ArgumentList.Arguments.Any(argument =>
            IsDirectCompletionPropertyReference(
                analysis,
                argument.Expression,
                propertyName,
                resultAliases));
    }

    private static bool IsDirectCompletionPropertyReference(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases) =>
        OperationIsDirectCompletionPropertyReference(
            analysis.Model.GetOperation(StripParentheses(expression)),
            propertyName,
            resultAliases);

    private static bool OperationIsDirectCompletionPropertyReference(
        IOperation? operation,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases) =>
        UnwrapIdentityPreservingOperation(operation) is
            IPropertyReferenceOperation property &&
        string.Equals(
            property.Property.Name,
            propertyName,
            StringComparison.Ordinal) &&
        OperationIsIdentityLocalReference(property.Instance, resultAliases);

    private static bool UnwrapLogicalNegation(ref ExpressionSyntax expression)
    {
        var negated = false;
        expression = StripParentheses(expression);
        while (expression is PrefixUnaryExpressionSyntax prefix &&
               prefix.IsKind(SyntaxKind.LogicalNotExpression))
        {
            negated = !negated;
            expression = StripParentheses(prefix.Operand);
        }
        return negated;
    }

    private static bool IsInt32Constant(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        int expected) => analysis.Model.GetConstantValue(expression) is
            { HasValue: true, Value: int value } && value == expected;

    private static bool IsFinalizedCompletionValue(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression)
    {
        if (analysis.Model.GetConstantValue(expression) is
                { HasValue: true, Value: string value } &&
            string.Equals(value, "Finalized", StringComparison.Ordinal))
        {
            return true;
        }
        return analysis.Model.GetSymbolInfo(expression).Symbol is IFieldSymbol field &&
               field.ContainingType?.TypeKind == TypeKind.Enum &&
               string.Equals(field.Name, "Finalized", StringComparison.Ordinal);
    }

    private static ExpressionSyntax StripParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parentheses)
            expression = parentheses.Expression;
        return expression;
    }

    private static bool BranchRejectsUnexpectedCompletionValue(
        BasicBlock guard,
        bool expectedConditionValue,
        BasicBlock completion,
        BasicBlock notification,
        BasicBlock acceptedReturn)
    {
        var fallThrough = guard.FallThroughSuccessor?.Destination;
        var conditional = guard.ConditionalSuccessor?.Destination;
        if (fallThrough is null || conditional is null ||
            fallThrough.Ordinal == conditional.Ordinal)
        {
            return false;
        }
        BasicBlock expected;
        BasicBlock unexpected;
        switch (guard.ConditionKind)
        {
            case ControlFlowConditionKind.WhenTrue:
                expected = expectedConditionValue ? conditional : fallThrough;
                unexpected = expectedConditionValue ? fallThrough : conditional;
                break;
            case ControlFlowConditionKind.WhenFalse:
                expected = expectedConditionValue ? fallThrough : conditional;
                unexpected = expectedConditionValue ? conditional : fallThrough;
                break;
            default:
                return false;
        }
        return CanReachBefore(expected, notification, completion) &&
               CanReachBefore(expected, acceptedReturn, completion) &&
               !CanReachBefore(unexpected, notification, completion) &&
               !CanReachBefore(unexpected, acceptedReturn, completion);
    }

    private static IEnumerable<BasicBlock> EnumerateSuccessors(BasicBlock block)
    {
        if (block.FallThroughSuccessor?.Destination is { } fallThrough)
            yield return fallThrough;
        if (block.ConditionalSuccessor?.Destination is { } conditional)
            yield return conditional;
    }

    private static bool CanReachBefore(
        BasicBlock source,
        BasicBlock target,
        BasicBlock barrier)
    {
        var visited = new HashSet<int>();
        var pending = new Queue<BasicBlock>();
        pending.Enqueue(source);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!visited.Add(current.Ordinal))
                continue;
            if (current.Ordinal == target.Ordinal)
                return true;
            if (current.Ordinal == barrier.Ordinal)
                continue;
            foreach (var successor in EnumerateSuccessors(current))
                pending.Enqueue(successor);
        }
        return false;
    }

    private static bool ExpressionIsExactCarrierIdentity(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        ILocalSymbol resultSymbol,
        string carrierPropertyName,
        IReadOnlySet<ILocalSymbol> aliases) =>
        OperationIsExactCarrierIdentity(
            analysis.Model.GetOperation(expression),
            resultSymbol,
            carrierPropertyName,
            aliases);

    private static bool OperationIsExactCarrierIdentity(
        IOperation? input,
        ILocalSymbol resultSymbol,
        string carrierPropertyName,
        IReadOnlySet<ILocalSymbol> aliases)
    {
        var operation = UnwrapIdentityPreservingOperation(input);
        if (operation is ILocalReferenceOperation local)
        {
            return aliases.Contains(local.Local);
        }
        return operation is IPropertyReferenceOperation property &&
               string.Equals(
                   property.Property.Name,
                   carrierPropertyName,
                   StringComparison.Ordinal) &&
               UnwrapIdentityPreservingOperation(property.Instance) is
                   ILocalReferenceOperation owner &&
               SymbolEqualityComparer.Default.Equals(owner.Local, resultSymbol);
    }

    private static IReadOnlySet<ILocalSymbol> BuildImmutableLocalAliases(
        SourceMethodAnalysis analysis,
        SyntaxNode scope,
        IEnumerable<ILocalSymbol> roots)
    {
        var aliases = new HashSet<ILocalSymbol>(
            roots,
            SymbolEqualityComparer.Default);
        bool changed;
        do
        {
            changed = false;
            foreach (var variable in scope.DescendantNodes()
                         .OfType<VariableDeclaratorSyntax>()
                         .Where(static variable => variable.Initializer is not null))
            {
                var symbol = analysis.Model.GetDeclaredSymbol(variable) as ILocalSymbol;
                if (symbol is null || aliases.Contains(symbol) ||
                    !ExpressionIsIdentityLocalAlias(
                        analysis,
                        variable.Initializer!.Value,
                        aliases) ||
                    !IsImmutableLocal(analysis, scope, symbol, variable))
                {
                    continue;
                }
                aliases.Add(symbol);
                changed = true;
            }
        } while (changed);
        return aliases;
    }

    private static bool IsImmutableLocal(
        SourceMethodAnalysis analysis,
        SyntaxNode scope,
        ILocalSymbol symbol,
        VariableDeclaratorSyntax declaration) =>
        !scope.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => identifier.SpanStart > declaration.Span.End)
            .Where(identifier => SymbolEqualityComparer.Default.Equals(
                analysis.Model.GetSymbolInfo(identifier).Symbol,
                symbol))
            .Any(static identifier =>
                identifier.Parent is AssignmentExpressionSyntax assignment &&
                assignment.Left.Span.Contains(identifier.Span) ||
                identifier.Parent is PrefixUnaryExpressionSyntax prefix &&
                (prefix.IsKind(SyntaxKind.PreIncrementExpression) ||
                 prefix.IsKind(SyntaxKind.PreDecrementExpression)) ||
                identifier.Parent is PostfixUnaryExpressionSyntax postfix &&
                (postfix.IsKind(SyntaxKind.PostIncrementExpression) ||
                 postfix.IsKind(SyntaxKind.PostDecrementExpression)) ||
                identifier.Parent is ArgumentSyntax argument &&
                !argument.RefKindKeyword.IsKind(SyntaxKind.None));

    private static bool ExpressionReferencesAnyLocal(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        IReadOnlySet<ILocalSymbol> locals) =>
        expression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => analysis.Model.GetSymbolInfo(identifier).Symbol is
                ILocalSymbol local && locals.Contains(local));

    private static bool ExpressionIsIdentityLocalAlias(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        IReadOnlySet<ILocalSymbol> locals) =>
        UnwrapIdentityPreservingOperation(
            analysis.Model.GetOperation(expression)) is
                ILocalReferenceOperation local &&
        locals.Contains(local.Local);

    private static IOperation? UnwrapIdentityPreservingOperation(
        IOperation? operation)
    {
        while (operation is IConversionOperation conversion &&
               !conversion.Conversion.IsUserDefined &&
               (conversion.Conversion.IsIdentity ||
                conversion.Conversion.IsReference))
        {
            operation = conversion.Operand;
        }
        return operation;
    }

    private static bool InvocationUsesCarrierSymbol(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax invocation,
        IReadOnlySet<ILocalSymbol> aliases)
    {
        var receivers = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => new[] { member.Expression },
            MemberBindingExpressionSyntax => invocation.Ancestors()
                .OfType<ConditionalAccessExpressionSyntax>()
                .Where(candidate => candidate.WhenNotNull.Span.Contains(invocation.Span))
                .Select(static candidate => candidate.Expression),
            _ => Array.Empty<ExpressionSyntax>()
        };
        return receivers.Any(input => ExpressionIsIdentityLocalAlias(
            analysis,
            input,
            aliases));
    }

    private static ControlFlowGraph RequireControlFlowGraph(
        SourceMethodAnalysis analysis)
    {
        var body = Assert.IsAssignableFrom<IMethodBodyOperation>(
            analysis.Model.GetOperation(analysis.Method));
        return ControlFlowGraph.Create(body);
    }

    private static BasicBlock RequireBlockContaining(
        ControlFlowGraph graph,
        SyntaxNode syntax)
    {
        var block = TryFindBlockContaining(graph, syntax);
        Assert.NotNull(block);
        return block!;
    }

    private static BasicBlock? TryFindBlockContaining(
        ControlFlowGraph graph,
        SyntaxNode syntax)
    {
        var candidates = graph.Blocks
            .Select(block => new
            {
                Block = block,
                Operations = block.Operations.SelectMany(OperationTree)
                    .Concat(block.BranchValue is null
                        ? Array.Empty<IOperation>()
                        : OperationTree(block.BranchValue))
                    .ToArray()
            })
            .Where(candidate => candidate.Operations.Any(operation =>
                operation.Syntax.Span == syntax.Span))
            .Select(static candidate => candidate.Block)
            .DistinctBy(static block => block.Ordinal)
            .ToArray();
        if (candidates.Length == 1)
            return candidates[0];
        candidates = graph.Blocks
            .Where(block => block.Operations.SelectMany(OperationTree)
                .Concat(block.BranchValue is null
                    ? Array.Empty<IOperation>()
                    : OperationTree(block.BranchValue))
                .Any(operation =>
                    operation.Syntax.Span.Contains(syntax.Span) ||
                    syntax.Span.Contains(operation.Syntax.Span)))
            .DistinctBy(static block => block.Ordinal)
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static IEnumerable<IOperation> OperationTree(IOperation root)
    {
        yield return root;
        foreach (var child in root.ChildOperations)
        foreach (var nested in OperationTree(child))
            yield return nested;
    }

    private static bool OperationReferencesAnyLocal(
        IOperation? operation,
        IReadOnlySet<ILocalSymbol> locals) =>
        operation is not null && OperationTree(operation).Any(candidate =>
            candidate is ILocalReferenceOperation local &&
            locals.Contains(local.Local));

    private static bool OperationIsIdentityLocalReference(
        IOperation? operation,
        IReadOnlySet<ILocalSymbol> locals) =>
        UnwrapIdentityPreservingOperation(operation) is
            ILocalReferenceOperation local &&
        locals.Contains(local.Local);

    private static IReadOnlyDictionary<int, HashSet<int>> ComputeDominators(
        ControlFlowGraph graph)
    {
        var ordinals = graph.Blocks.Select(static block => block.Ordinal).ToHashSet();
        var entry = graph.Blocks.Single(static block =>
            block.Kind == BasicBlockKind.Entry);
        var dominators = graph.Blocks.ToDictionary(
            static block => block.Ordinal,
            block => block.Ordinal == entry.Ordinal
                ? new HashSet<int> { entry.Ordinal }
                : new HashSet<int>(ordinals));
        bool changed;
        do
        {
            changed = false;
            foreach (var block in graph.Blocks.Where(block =>
                         block.Ordinal != entry.Ordinal))
            {
                var predecessors = block.Predecessors
                    .Select(static branch => branch.Source.Ordinal)
                    .Distinct()
                    .ToArray();
                var next = predecessors.Length == 0
                    ? new HashSet<int>()
                    : new HashSet<int>(dominators[predecessors[0]]);
                foreach (var predecessor in predecessors.Skip(1))
                    next.IntersectWith(dominators[predecessor]);
                next.Add(block.Ordinal);
                if (dominators[block.Ordinal].SetEquals(next))
                    continue;
                dominators[block.Ordinal] = next;
                changed = true;
            }
        } while (changed);
        return dominators;
    }

    private static bool Dominates(
        IReadOnlyDictionary<int, HashSet<int>> dominators,
        BasicBlock candidate,
        BasicBlock target) => dominators[target.Ordinal].Contains(candidate.Ordinal);

    private static InvocationExpressionSyntax RequireInvocation(
        IEnumerable<InvocationExpressionSyntax> invocations,
        string methodName) => Assert.Single(invocations, invocation =>
            string.Equals(
                ReadInvocationName(invocation),
                methodName,
                StringComparison.Ordinal));

    private static InvocationExpressionSyntax[] RequireInvocations(
        IEnumerable<InvocationExpressionSyntax> invocations,
        string methodName)
    {
        var matches = invocations.Where(invocation => string.Equals(
                ReadInvocationName(invocation),
                methodName,
                StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(matches);
        return matches;
    }

    private static string ReadInvocationName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            _ => string.Empty
        };
}
