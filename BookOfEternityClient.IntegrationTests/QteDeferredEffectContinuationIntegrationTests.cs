using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class QteDeferredEffectContinuationIntegrationTests
{
    private const string ContinuationPath =
        "game_state/control/qte_deferred_effect_continuation.json";
    private static readonly string[] ReceiptResumeProtectedPaths =
        QteSceneService.BrowserTransactionRollbackPaths
            .Concat(QteDeferredEffectContinuation.SealedRootPaths)
            .Concat(
            [
                ResourcePendingResolutionState.PendingPath,
                ContinuationPath,
                QteDeferredEffectContinuation.RequestPath,
                QteDeferredEffectContinuation.ReceiptPath,
                QteDeferredEffectContinuation.ReadyPath
            ])
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public async Task AcceptOffer_SealsGenerationOfferTurnAndExactEffectRootWithoutLiveRebuild()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string sessionId = "session_qte_deferred_effect";
        const int sourceTurn = 42;
        await SeedDeferredAuthorityAsync(context, sessionId);

        var playerCarrierBefore = await context.FileSystem.ReadFileBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath);
        Assert.NotNull(playerCarrierBefore);

        var offer = BuildOffer(sourceTurn);
        await context.WriteJsonAsync(
            QteSceneService.QteOfferPath,
            JsonSerializer.SerializeToNode(offer)!.AsObject());
        var service = CreateQteService(context.FileSystem);

        string generation;
        await using (var writeLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            generation = context.FileSystem.GetOrCreateSessionGeneration(writeLease);
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.BeginAcceptedSceneAsync(
                    writeLease,
                    offer,
                    sourceTurn));
        }

        var continuationBytes = await context.FileSystem.ReadFileBytesAsync(
            ContinuationPath);
        Assert.NotNull(continuationBytes);
        using var continuation = JsonDocument.Parse(
            (await context.FileSystem.ReadFileAsync(ContinuationPath))!);
        var root = continuation.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(sessionId, root.GetProperty("sessionId").GetString());
        Assert.Equal(generation, root.GetProperty("sessionGeneration").GetString());
        Assert.Equal(sourceTurn, root.GetProperty("acceptedSourceTurn").GetInt32());
        Assert.Equal(offer.QteId, root.GetProperty("qteId").GetString());
        Assert.StartsWith("sha256:", root.GetProperty("offerFingerprint").GetString());
        Assert.Equal("armed", root.GetProperty("state").GetString());
        Assert.StartsWith("sha256:", root.GetProperty("authorityFingerprint").GetString());
        var identityLedger = root.GetProperty("identityLedger")
            .EnumerateArray()
            .Select(static entry => entry.GetProperty("semanticKey").GetString()!)
            .ToArray();
        Assert.NotEmpty(identityLedger);
        var preallocatedSelectionNamespaces = identityLedger
            .Where(static key => key.Contains(
                ":resource_operation:",
                StringComparison.Ordinal))
            .Select(static key => key[..key.LastIndexOf(
                ":resource_operation:",
                StringComparison.Ordinal)])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(3, preallocatedSelectionNamespaces.Length);
        Assert.All(preallocatedSelectionNamespaces, static key =>
            Assert.StartsWith("selected:sha256:", key, StringComparison.Ordinal));

        var playerBinding = Assert.Single(
            root.GetProperty("sealedRootBindings").EnumerateArray(),
            binding => string.Equals(
                binding.GetProperty("path").GetString(),
                EffectMaterializationTestContext.PlayerEffectsPath,
                StringComparison.Ordinal));
        Assert.True(playerBinding.GetProperty("existed").GetBoolean());
        Assert.Equal(
            Convert.ToBase64String(playerCarrierBefore!),
            playerBinding.GetProperty("payloadBase64").GetString());
        Assert.StartsWith("sha256:", playerBinding.GetProperty("sha256").GetString());

        using var runtime = JsonDocument.Parse(
            (await context.FileSystem.ReadFileAsync(QteSceneService.QteRuntimePath))!);
        var active = runtime.RootElement.GetProperty("activeScene");
        Assert.Equal(
            root.GetProperty("continuationId").GetString(),
            active.GetProperty("deferredEffectContinuationId").GetString());
        Assert.Equal(
            root.GetProperty("authorityFingerprint").GetString(),
            active.GetProperty("deferredEffectContinuationFingerprint").GetString());
        Assert.Equal("armed", active.GetProperty("effectResolutionState").GetString());
        Assert.False(context.FileSystem.FileExists(QteSceneService.QteOfferPath));

        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray()
            });

        Assert.Equal(
            continuationBytes,
            await context.FileSystem.ReadFileBytesAsync(ContinuationPath));
    }

    [Fact]
    public async Task TerminalSelection_ByteDifferentSealedRootFailsBeforeAnyWrite()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string sessionId = "session_qte_deferred_tamper";
        const int sourceTurn = 42;
        await SeedDeferredAuthorityAsync(context, sessionId);
        var offer = BuildOffer(sourceTurn);
        await context.WriteJsonAsync(
            QteSceneService.QteOfferPath,
            JsonSerializer.SerializeToNode(offer)!.AsObject());
        var service = CreateQteService(context.FileSystem);

        string generation;
        await using (var writeLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            generation = context.FileSystem.GetOrCreateSessionGeneration(writeLease);
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.BeginAcceptedSceneAsync(
                    writeLease,
                    offer,
                    sourceTurn));
        }
        var semanticCarrier = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!;
        var byteDifferentCarrier = System.Text.Encoding.UTF8.GetBytes(
            semanticCarrier.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        await context.FileSystem.WriteFileAtomicBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            byteDifferentCarrier);
        var trackedPaths = QteDeferredEffectContinuation.SealedRootPaths
            .Concat(
            [
                ContinuationPath,
                QteSceneService.QteRuntimePath,
                QteSceneService.QteHistoryPath,
                QteDeferredEffectContinuation.RequestPath,
                QteDeferredEffectContinuation.ReceiptPath,
                QteDeferredEffectContinuation.ReadyPath
            ])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var before = await context.CaptureBytesAsync(trackedPaths);

        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var writeLease =
                await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.ResolveActiveActionAsync(
                    writeLease,
                    "brace",
                    "fail",
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        });

        Assert.Contains(
            "qte_deferred_continuation_sealed_root_mismatch",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(before, await context.CaptureBytesAsync(trackedPaths));
        var continuation = (await context.ReadJsonAsync(ContinuationPath))!.AsObject();
        Assert.Equal("armed", continuation["state"]!.GetValue<string>());
        var runtime = (await context.ReadJsonAsync(
            QteSceneService.QteRuntimePath))!.AsObject();
        Assert.NotNull(runtime["activeScene"]);
        Assert.False(context.FileSystem.FileExists(QteSceneService.QteHistoryPath));
    }

    [Theory]
    [InlineData("nested_authority")]
    [InlineData("armed_receipt_state")]
    public async Task TerminalSelection_InvalidContinuationShapeFailsBeforeAnyWrite(
        string invalidShape)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const int sourceTurn = 42;
        var sessionId = "session_qte_deferred_" + invalidShape;
        await SeedDeferredAuthorityAsync(context, sessionId);
        var offer = BuildOffer(sourceTurn);
        await context.WriteJsonAsync(
            QteSceneService.QteOfferPath,
            JsonSerializer.SerializeToNode(offer)!.AsObject());
        var service = CreateQteService(context.FileSystem);

        string generation;
        await using (var writeLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            generation = context.FileSystem.GetOrCreateSessionGeneration(writeLease);
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.BeginAcceptedSceneAsync(
                    writeLease,
                    offer,
                    sourceTurn));
        }

        var continuation = (await context.ReadJsonAsync(
            ContinuationPath))!.AsObject();
        if (invalidShape == "nested_authority")
        {
            continuation["sourceAuthorityBinding"]!["extra"] = "forbidden";
        }
        else
        {
            continuation["currentWave"] = new JsonObject
            {
                ["requestId"] = "qte_effect_request_forbidden",
                ["waveId"] = "qte_effect_wave_forbidden",
                ["ordinal"] = 0,
                ["safePacketFingerprint"] = "sha256:" + new string('4', 64)
            };
        }
        var refreshed = QteDeferredEffectContinuation
            .RefreshAuthorityFingerprint(continuation);
        await context.WriteJsonAsync(ContinuationPath, continuation);
        var runtime = (await context.ReadJsonAsync(
            QteSceneService.QteRuntimePath))!.AsObject();
        runtime["activeScene"]!["deferredEffectContinuationFingerprint"] =
            refreshed;
        await context.WriteJsonAsync(QteSceneService.QteRuntimePath, runtime);

        var trackedPaths = ReceiptResumeProtectedPaths
            .Append(QteSceneService.QteHistoryPath)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var before = await context.CaptureBytesAsync(trackedPaths);
        service = CreateQteService(context.FileSystem);
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var writeLease =
                await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.ResolveActiveActionAsync(
                    writeLease,
                    "brace",
                    "fail",
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        });

        Assert.Contains(
            "qte_deferred_continuation_authority_binding_invalid",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(before, await context.CaptureBytesAsync(trackedPaths));
    }

    [Fact]
    public async Task AcceptedTerminalDamage_AfterRestartUsesSealedEffectTriggerAndClosesContinuation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string sessionId = "session_qte_deferred_restart";
        const int sourceTurn = 42;
        await SeedDeferredAuthorityAsync(context, sessionId);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.DefinitionsPath))!.ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        Assert.Empty(definitions.Issues);
        var beforeState = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.StatePath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(beforeState.Ledger);
        Assert.Empty(beforeState.Issues);
        var poiseBefore = Assert.Single(
            beforeState.Ledger!.Entries,
            entry => entry.Coordinate.ResourceKey == "poise").Current;
        var healthBefore = Assert.Single(
            beforeState.Ledger.Entries,
            entry => entry.Coordinate.ResourceKey == "health").Current;

        var offer = BuildOffer(sourceTurn);
        await context.WriteJsonAsync(
            QteSceneService.QteOfferPath,
            JsonSerializer.SerializeToNode(offer)!.AsObject());
        var service = CreateQteService(context.FileSystem);
        string generation;
        await using (var writeLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            generation = context.FileSystem.GetOrCreateSessionGeneration(writeLease);
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.BeginAcceptedSceneAsync(
                    writeLease,
                    offer,
                    sourceTurn));
        }
        var acceptanceIdentityLedger = (await context.ReadJsonAsync(
            ContinuationPath))!["identityLedger"]!.DeepClone();
        Assert.NotEmpty(acceptanceIdentityLedger.AsArray());

        if (context.FileSystem.FileExists("input/turn_request.json"))
            context.FileSystem.DeleteFile("input/turn_request.json");
        if (context.FileSystem.FileExists(
                "game_state/control/pending_turn_snapshot.json"))
        {
            context.FileSystem.DeleteFile(
                "game_state/control/pending_turn_snapshot.json");
        }
        if (context.FileSystem.FileExists(
                PendingTurnSnapshotAuthority.AuthorityPath))
        {
            context.FileSystem.DeleteFile(
                PendingTurnSnapshotAuthority.AuthorityPath);
        }

        service = CreateQteService(context.FileSystem);
        QteSceneService.QteActionResolution resolution = null!;
        await using (var writeLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            resolution = await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.ResolveActiveActionAsync(
                    writeLease,
                    "brace",
                    "fail",
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        }

        Assert.Equal("Completed", resolution.State);
        var afterState = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.StatePath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(afterState.Ledger);
        Assert.Empty(afterState.Issues);
        Assert.Equal(
            poiseBefore - 5m,
            Assert.Single(
                afterState.Ledger!.Entries,
                entry => entry.Coordinate.ResourceKey == "poise").Current);
        Assert.Equal(
            healthBefore - 3m,
            Assert.Single(
                afterState.Ledger.Entries,
                entry => entry.Coordinate.ResourceKey == "health").Current);

        var history = ResourceHistoryState.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.HistoryPath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(history.History);
        Assert.Empty(history.Issues);
        var qteTransitions = history.History!.Transitions
            .Where(transition => transition.Turn == sourceTurn)
            .ToArray();
        Assert.Single(qteTransitions, transition =>
            transition.OriginKind == "narrative_outcome" &&
            transition.EventRef == "turn_42:qte_terminal:1:resource:1");
        Assert.Single(qteTransitions, transition =>
            transition.Phase == ResourceMutationPhase.EffectTrigger &&
            transition.OriginKind == "effect_component");

        var playerEffects = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Empty(playerEffects["activeEffects"]!.AsArray());
        var identity = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var identityEntry = Assert.IsType<JsonObject>(
            Assert.Single(identity["entries"]!.AsArray()));
        Assert.Equal("expired", identityEntry["state"]!.GetValue<string>());
        var continuation = (await context.ReadJsonAsync(ContinuationPath))!.AsObject();
        Assert.Equal("terminal", continuation["state"]!.GetValue<string>());
        Assert.NotNull(continuation["selectedTerminalBinding"]);
        Assert.NotNull(continuation["terminalFingerprint"]);
        Assert.True(JsonNode.DeepEquals(
            acceptanceIdentityLedger,
            continuation["identityLedger"]));
        var runtime = (await context.ReadJsonAsync(
            QteSceneService.QteRuntimePath))!.AsObject();
        Assert.Null(runtime["activeScene"]);
        Assert.Single((await context.ReadJsonAsync(
            QteSceneService.QteHistoryPath))!.AsArray());
    }

    [Fact]
    public async Task BoundedTerminal_AfterRestartPublishesOnlyAwaitingReceiptAuthorityWithoutOrdinaryTurn()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string sessionId = "session_qte_deferred_bounded";
        const int sourceTurn = 42;
        await SeedDeferredAuthorityAsync(
            context,
            sessionId,
            resolutionMode: "bounded_receipt");
        var offer = BuildOffer(sourceTurn);
        await context.WriteJsonAsync(
            QteSceneService.QteOfferPath,
            JsonSerializer.SerializeToNode(offer)!.AsObject());
        var service = CreateQteService(context.FileSystem);

        string generation;
        await using (var writeLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            generation = context.FileSystem.GetOrCreateSessionGeneration(writeLease);
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.BeginAcceptedSceneAsync(
                    writeLease,
                    offer,
                    sourceTurn));
        }

        var unpublishedMechanicalPaths = new[]
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath
        };
        var unpublishedBaseline = await context.CaptureBytesAsync(
            unpublishedMechanicalPaths);
        service = CreateQteService(context.FileSystem);

        QteSceneService.QteActionResolution resolution = null!;
        await using (var writeLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            resolution = await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.ResolveActiveActionAsync(
                    writeLease,
                    "brace",
                    "fail",
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        }

        Assert.Equal("AwaitingEffectResolution", resolution.State);
        Assert.Null(resolution.Completion);
        Assert.Equal(
            unpublishedBaseline,
            await context.CaptureBytesAsync(unpublishedMechanicalPaths));
        Assert.False(context.FileSystem.FileExists(QteSceneService.QteHistoryPath));

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.DefinitionsPath))!.ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        Assert.Empty(definitions.Issues);
        var pendingResult = ResourcePendingResolutionState.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourcePendingResolutionState.PendingPath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(
            pendingResult.IsValid,
            string.Join(
                Environment.NewLine,
                pendingResult.Issues.Select(static issue =>
                    $"{issue.Code}@{issue.FilePath}")));
        var pending = Assert.IsType<ResourcePendingResolutionState>(
            pendingResult.State);
        Assert.Single(pending.Requests);
        Assert.Empty(pending.TerminalReceipts);

        var continuation = (await context.ReadJsonAsync(
            ContinuationPath))!.AsObject();
        Assert.Equal("awaiting_receipt", continuation["state"]!.GetValue<string>());
        var selected = Assert.IsType<JsonObject>(
            continuation["selectedTerminalBinding"]);
        var currentWave = Assert.IsType<JsonObject>(continuation["currentWave"]);
        Assert.Empty(continuation["resolvedWaveBindings"]!.AsArray());

        var request = (await context.ReadJsonAsync(
            QteDeferredEffectContinuation.RequestPath))!.AsObject();
        Assert.Equal(1, request["schemaVersion"]!.GetValue<int>());
        Assert.Equal(
            "qte_deferred_effect_resolution",
            request["requestKind"]!.GetValue<string>());
        Assert.Equal(sessionId, request["sessionId"]!.GetValue<string>());
        Assert.Equal(generation, request["sessionGeneration"]!.GetValue<string>());
        Assert.Equal(
            continuation["continuationId"]!.GetValue<string>(),
            request["continuationId"]!.GetValue<string>());
        Assert.Equal(sourceTurn, request["acceptedSourceTurn"]!.GetValue<int>());
        Assert.Equal(offer.QteId, request["qteId"]!.GetValue<string>());
        Assert.Equal(
            selected["fingerprint"]!.GetValue<string>(),
            request["selectedTerminalFingerprint"]!.GetValue<string>());
        Assert.Equal(
            pending.Fingerprint,
            request["pendingStateFingerprint"]!.GetValue<string>());
        Assert.Equal(
            Assert.Single(pending.Requests).FullTurnFingerprint,
            request["fullTurnFingerprint"]!.GetValue<string>());
        Assert.Equal(
            Assert.Single(pending.Requests).SemanticTurnFingerprint,
            request["semanticTurnFingerprint"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(
            pending.BuildSafeGmPacket(),
            request["safePacket"]));
        Assert.Equal(
            request["requestId"]!.GetValue<string>(),
            currentWave["requestId"]!.GetValue<string>());
        Assert.Equal(
            request["waveId"]!.GetValue<string>(),
            currentWave["waveId"]!.GetValue<string>());
        Assert.Equal(
            request["waveOrdinal"]!.GetValue<int>(),
            currentWave["ordinal"]!.GetValue<int>());
        Assert.StartsWith(
            "sha256:",
            currentWave["safePacketFingerprint"]!.GetValue<string>());

        var runtime = (await context.ReadJsonAsync(
            QteSceneService.QteRuntimePath))!.AsObject();
        Assert.NotNull(runtime["activeScene"]);
        Assert.Equal(
            "awaiting_receipt",
            runtime["activeScene"]!["effectResolutionState"]!.GetValue<string>());
        Assert.False(context.FileSystem.FileExists(
            QteDeferredEffectContinuation.ReceiptPath));
        Assert.False(context.FileSystem.FileExists(
            QteDeferredEffectContinuation.ReadyPath));
        Assert.False(context.FileSystem.FileExists("input/turn_request.json"));
        Assert.False(context.FileSystem.FileExists(
            "game_state/control/pending_turn_snapshot.json"));
        Assert.False(context.FileSystem.FileExists(
            PendingTurnSnapshotAuthority.AuthorityPath));
    }

    [Fact]
    public async Task BoundedReceipt_AfterSecondRestartFinalizesWithoutOrdinaryTurnReplay()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string sessionId = "session_qte_deferred_receipt";
        const int sourceTurn = 42;
        await SeedDeferredAuthorityAsync(
            context,
            sessionId,
            resolutionMode: "bounded_receipt");
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.DefinitionsPath))!.ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        Assert.Empty(definitions.Issues);
        var beforeState = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.StatePath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(beforeState.Ledger);
        Assert.Empty(beforeState.Issues);
        var poiseBefore = Assert.Single(
            beforeState.Ledger!.Entries,
            entry => entry.Coordinate.ResourceKey == "poise").Current;
        var healthBefore = Assert.Single(
            beforeState.Ledger.Entries,
            entry => entry.Coordinate.ResourceKey == "health").Current;

        var offer = BuildOffer(sourceTurn);
        await context.WriteJsonAsync(
            QteSceneService.QteOfferPath,
            JsonSerializer.SerializeToNode(offer)!.AsObject());
        var service = CreateQteService(context.FileSystem);
        string generation;
        await using (var writeLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            generation = context.FileSystem.GetOrCreateSessionGeneration(writeLease);
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.BeginAcceptedSceneAsync(
                    writeLease,
                    offer,
                    sourceTurn));
            var awaiting = await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.ResolveActiveActionAsync(
                    writeLease,
                    "brace",
                    "fail",
                    sourceTurn,
                    allowPreexistingStateIssues: true));
            Assert.Equal("AwaitingEffectResolution", awaiting.State);
        }

        var request = (await context.ReadJsonAsync(
            QteDeferredEffectContinuation.RequestPath))!.AsObject();
        var pending = ResourcePendingResolutionState.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourcePendingResolutionState.PendingPath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false).State!;
        var pendingRequest = Assert.Single(pending.Requests);
        var receiptRoot = BuildReceiptRoot(
            request,
            new JsonArray(new JsonObject
            {
                ["requestId"] = pendingRequest.RequestId,
                ["resultKind"] = "resource_delta",
                ["amount"] = 2,
                ["reason"] = "Каменная крошка усиливает рану."
            }));
        await context.WriteJsonAsync(
            QteDeferredEffectContinuation.ReceiptPath,
            receiptRoot);
        var receiptBytes = await context.FileSystem.ReadFileBytesAsync(
            QteDeferredEffectContinuation.ReceiptPath);
        Assert.NotNull(receiptBytes);
        await context.WriteJsonAsync(
            QteDeferredEffectContinuation.ReadyPath,
            BuildReadyRoot(request, receiptBytes!));

        service = CreateQteService(context.FileSystem);
        QteSceneService.QteSceneCompletion? completion;
        await using (var writeLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            completion = await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.ResumeDeferredEffectResolutionAsync(
                    writeLease,
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        }

        Assert.NotNull(completion);
        Assert.False(completion!.AwaitingEffectResolution);
        Assert.Equal(offer.QteId, completion.QteId);
        Assert.Equal("crushing", completion.OutcomeId);
        var afterState = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.StatePath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(afterState.Ledger);
        Assert.Empty(afterState.Issues);
        Assert.Equal(
            poiseBefore - 5m,
            Assert.Single(
                afterState.Ledger!.Entries,
                entry => entry.Coordinate.ResourceKey == "poise").Current);
        Assert.Equal(
            healthBefore - 2m,
            Assert.Single(
                afterState.Ledger.Entries,
                entry => entry.Coordinate.ResourceKey == "health").Current);
        var history = ResourceHistoryState.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.HistoryPath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(history.History);
        Assert.Empty(history.Issues);
        Assert.Single(history.History!.Transitions, transition =>
            transition.OriginKind == "bounded_receipt" &&
            transition.ReceiptId == pendingRequest.RequestId);
        Assert.Empty((await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!["activeEffects"]!
            .AsArray());
        Assert.Empty(ResourcePendingResolutionState.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourcePendingResolutionState.PendingPath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false).State!.Requests);
        Assert.Equal(
            "terminal",
            (await context.ReadJsonAsync(ContinuationPath))!["state"]!
            .GetValue<string>());
        Assert.Null((await context.ReadJsonAsync(
            QteSceneService.QteRuntimePath))!["activeScene"]);
        Assert.Single((await context.ReadJsonAsync(
            QteSceneService.QteHistoryPath))!.AsArray());
        Assert.False(context.FileSystem.FileExists(
            QteDeferredEffectContinuation.RequestPath));
        Assert.False(context.FileSystem.FileExists(
            QteDeferredEffectContinuation.ReceiptPath));
        Assert.False(context.FileSystem.FileExists(
            QteDeferredEffectContinuation.ReadyPath));
        Assert.False(context.FileSystem.FileExists("input/turn_request.json"));
        Assert.False(context.FileSystem.FileExists(
            "game_state/control/pending_turn_snapshot.json"));
        Assert.False(context.FileSystem.FileExists(
            PendingTurnSnapshotAuthority.AuthorityPath));
    }

    [Fact]
    public async Task BoundedReceipt_TwoWavesSurviveRestartsAndPublishMechanicsOnlyAtFinalWave()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string sessionId = "session_qte_deferred_two_waves";
        const int sourceTurn = 42;
        await SeedDeferredAuthorityAsync(
            context,
            sessionId,
            resolutionMode: "bounded_receipt",
            remainingUses: 2);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.DefinitionsPath))!.ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        var beforeState = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.StatePath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false).Ledger!;
        var poiseBefore = Assert.Single(
            beforeState.Entries,
            entry => entry.Coordinate.ResourceKey == "poise").Current;
        var healthBefore = Assert.Single(
            beforeState.Entries,
            entry => entry.Coordinate.ResourceKey == "health").Current;

        var offer = BuildOffer(sourceTurn);
        await context.WriteJsonAsync(
            QteSceneService.QteOfferPath,
            JsonSerializer.SerializeToNode(offer)!.AsObject());
        var service = CreateQteService(context.FileSystem);
        string generation;
        await using (var lease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            generation = context.FileSystem.GetOrCreateSessionGeneration(lease);
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                lease,
                () => service.BeginAcceptedSceneAsync(
                    lease,
                    offer,
                    sourceTurn));
            var resolution = await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                lease,
                () => service.ResolveActiveActionAsync(
                    lease,
                    "brace",
                    "fail",
                    sourceTurn,
                    allowPreexistingStateIssues: true));
            Assert.Equal("AwaitingEffectResolution", resolution.State);
        }

        var mechanicalPaths = new[]
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath
        };
        var unpublished = await context.CaptureBytesAsync(mechanicalPaths);
        var firstRequest = (await context.ReadJsonAsync(
            QteDeferredEffectContinuation.RequestPath))!.AsObject();
        var firstPending = ResourcePendingResolutionState.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourcePendingResolutionState.PendingPath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false).State!;
        await WriteReceiptTransportAsync(
            context,
            firstRequest,
            Assert.Single(firstPending.Requests).RequestId,
            amount: 2m);

        service = CreateQteService(context.FileSystem);
        QteSceneService.QteSceneCompletion? firstWave;
        await using (var lease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            firstWave = await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                lease,
                () => service.ResumeDeferredEffectResolutionAsync(
                    lease,
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        }
        Assert.NotNull(firstWave);
        Assert.True(firstWave!.AwaitingEffectResolution);
        Assert.Equal(
            unpublished,
            await context.CaptureBytesAsync(mechanicalPaths));
        Assert.False(context.FileSystem.FileExists(QteSceneService.QteHistoryPath));

        var secondPending = ResourcePendingResolutionState.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourcePendingResolutionState.PendingPath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false).State!;
        var secondPendingRequest = Assert.Single(secondPending.Requests);
        Assert.Single(secondPending.TerminalReceipts);
        var secondRequest = (await context.ReadJsonAsync(
            QteDeferredEffectContinuation.RequestPath))!.AsObject();
        Assert.NotEqual(
            firstRequest["requestId"]!.GetValue<string>(),
            secondRequest["requestId"]!.GetValue<string>());
        Assert.Equal(
            firstRequest["waveOrdinal"]!.GetValue<int>() + 1,
            secondRequest["waveOrdinal"]!.GetValue<int>());
        var awaitingContinuation = (await context.ReadJsonAsync(
            ContinuationPath))!.AsObject();
        Assert.Equal(
            secondRequest["requestId"]!.GetValue<string>(),
            awaitingContinuation["currentWave"]!["requestId"]!.GetValue<string>());
        Assert.Single(awaitingContinuation["resolvedWaveBindings"]!.AsArray());
        Assert.False(context.FileSystem.FileExists(
            QteDeferredEffectContinuation.ReceiptPath));
        Assert.False(context.FileSystem.FileExists(
            QteDeferredEffectContinuation.ReadyPath));

        await WriteReceiptTransportAsync(
            context,
            secondRequest,
            secondPendingRequest.RequestId,
            amount: 1m);
        service = CreateQteService(context.FileSystem);
        QteSceneService.QteSceneCompletion? final;
        await using (var lease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            final = await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                lease,
                () => service.ResumeDeferredEffectResolutionAsync(
                    lease,
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        }
        Assert.NotNull(final);
        Assert.False(final!.AwaitingEffectResolution);
        var finalState = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.StatePath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false).Ledger!;
        Assert.Equal(
            poiseBefore - 5m,
            Assert.Single(
                finalState.Entries,
                entry => entry.Coordinate.ResourceKey == "poise").Current);
        Assert.Equal(
            healthBefore - 3m,
            Assert.Single(
                finalState.Entries,
                entry => entry.Coordinate.ResourceKey == "health").Current);
        var terminal = (await context.ReadJsonAsync(ContinuationPath))!.AsObject();
        Assert.Equal("terminal", terminal["state"]!.GetValue<string>());
        Assert.Equal(2, terminal["resolvedWaveBindings"]!.AsArray().Count);
        Assert.Single((await context.ReadJsonAsync(
            QteSceneService.QteHistoryPath))!.AsArray());
        Assert.False(context.FileSystem.FileExists(
            QteDeferredEffectContinuation.RequestPath));
        Assert.False(context.FileSystem.FileExists(
            QteDeferredEffectContinuation.ReceiptPath));
        Assert.False(context.FileSystem.FileExists(
            QteDeferredEffectContinuation.ReadyPath));
    }

    [Fact]
    public async Task BoundedReceipt_TamperedReadyHashFailsBeforeAnyWrite()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const int sourceTurn = 42;
        var scenario = await PrepareAwaitingReceiptAsync(
            context,
            "session_qte_deferred_ready_hash");
        await WriteReceiptTransportAsync(
            context,
            scenario.Request,
            scenario.PendingRequestId,
            amount: 2m);
        var ready = (await context.ReadJsonAsync(
            QteDeferredEffectContinuation.ReadyPath))!.AsObject();
        ready["receiptsFingerprint"] = "sha256:" + new string('0', 64);
        await context.WriteJsonAsync(
            QteDeferredEffectContinuation.ReadyPath,
            ready);
        var before = await context.CaptureBytesAsync(
            ReceiptResumeProtectedPaths);

        var service = CreateQteService(context.FileSystem);
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var lease =
                await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                scenario.Generation,
                lease,
                () => service.ResumeDeferredEffectResolutionAsync(
                    lease,
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        });

        Assert.Contains(
            "qte_deferred_ready_invalid",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(ReceiptResumeProtectedPaths));
    }

    [Fact]
    public async Task BoundedReceipt_TamperedRequestCorrelationFailsBeforeAnyWrite()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const int sourceTurn = 42;
        var scenario = await PrepareAwaitingReceiptAsync(
            context,
            "session_qte_deferred_request_correlation");
        await WriteReceiptTransportAsync(
            context,
            scenario.Request,
            scenario.PendingRequestId,
            amount: 2m);
        var request = (await context.ReadJsonAsync(
            QteDeferredEffectContinuation.RequestPath))!.AsObject();
        request["waveId"] = "qte_effect_wave_tampered";
        await context.WriteJsonAsync(
            QteDeferredEffectContinuation.RequestPath,
            request);
        var before = await context.CaptureBytesAsync(
            ReceiptResumeProtectedPaths);

        var service = CreateQteService(context.FileSystem);
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var lease =
                await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                scenario.Generation,
                lease,
                () => service.ResumeDeferredEffectResolutionAsync(
                    lease,
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        });

        Assert.Contains(
            "qte_deferred_transport_correlation_mismatch",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(ReceiptResumeProtectedPaths));
    }

    [Fact]
    public async Task BoundedReceipt_NonClosedPriorWaveBindingFailsBeforeAnyWrite()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const int sourceTurn = 42;
        var scenario = await PrepareAwaitingReceiptAsync(
            context,
            "session_qte_deferred_prior_wave_schema");
        await WriteReceiptTransportAsync(
            context,
            scenario.Request,
            scenario.PendingRequestId,
            amount: 2m);

        var continuation = (await context.ReadJsonAsync(
            ContinuationPath))!.AsObject();
        continuation["resolvedWaveBindings"]!.AsArray().Add(new JsonObject
        {
            ["requestId"] = "qte_effect_request_injected",
            ["waveId"] = "qte_effect_wave_injected",
            ["ordinal"] = 0,
            ["pendingStateFingerprint"] = "sha256:" + new string('1', 64),
            ["receiptsFingerprint"] = "sha256:" + new string('2', 64),
            ["fingerprint"] = "sha256:" + new string('3', 64),
            ["extra"] = "forbidden"
        });
        var refreshed = QteDeferredEffectContinuation
            .RefreshAuthorityFingerprint(continuation);
        await context.WriteJsonAsync(ContinuationPath, continuation);
        var runtime = (await context.ReadJsonAsync(
            QteSceneService.QteRuntimePath))!.AsObject();
        runtime["activeScene"]!["deferredEffectContinuationFingerprint"] =
            refreshed;
        await context.WriteJsonAsync(QteSceneService.QteRuntimePath, runtime);
        var before = await context.CaptureBytesAsync(
            ReceiptResumeProtectedPaths);

        var service = CreateQteService(context.FileSystem);
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var lease =
                await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                scenario.Generation,
                lease,
                () => service.ResumeDeferredEffectResolutionAsync(
                    lease,
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        });

        Assert.Contains(
            "qte_deferred_receipt_authority_invalid",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(ReceiptResumeProtectedPaths));
    }

    [Fact]
    public async Task BoundedReceipt_ReadyWithoutReceiptFailsBeforeAnyWrite()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const int sourceTurn = 42;
        var scenario = await PrepareAwaitingReceiptAsync(
            context,
            "session_qte_deferred_missing_receipt");
        await WriteReceiptTransportAsync(
            context,
            scenario.Request,
            scenario.PendingRequestId,
            amount: 2m);
        context.FileSystem.DeleteFile(
            QteDeferredEffectContinuation.ReceiptPath);
        var before = await context.CaptureBytesAsync(
            ReceiptResumeProtectedPaths);

        var service = CreateQteService(context.FileSystem);
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var lease =
                await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                scenario.Generation,
                lease,
                () => service.ResumeDeferredEffectResolutionAsync(
                    lease,
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        });

        Assert.Contains(
            "qte_deferred_receipt_missing",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(ReceiptResumeProtectedPaths));
    }

    [Fact]
    public async Task BoundedReceipt_MissingReadyRemainsAwaitingWithoutAnyWrite()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const int sourceTurn = 42;
        var scenario = await PrepareAwaitingReceiptAsync(
            context,
            "session_qte_deferred_missing_ready");
        var before = await context.CaptureBytesAsync(
            ReceiptResumeProtectedPaths);

        var service = CreateQteService(context.FileSystem);
        QteSceneService.QteSceneCompletion? completion;
        await using (var lease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            completion = await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                scenario.Generation,
                lease,
                () => service.ResumeDeferredEffectResolutionAsync(
                    lease,
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        }

        Assert.NotNull(completion);
        Assert.True(completion!.AwaitingEffectResolution);
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(ReceiptResumeProtectedPaths));
    }

    [Fact]
    public async Task BoundedReceipt_ReplacedSessionGenerationFailsBeforeAnyWrite()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const int sourceTurn = 42;
        var scenario = await PrepareAwaitingReceiptAsync(
            context,
            "session_qte_deferred_replaced_generation");
        await WriteReceiptTransportAsync(
            context,
            scenario.Request,
            scenario.PendingRequestId,
            amount: 2m);

        string replacementGeneration;
        await using (var lifecycleLease =
                     await context.FileSystem.AcquireSessionLifecycleLeaseAsync())
        await using (var replacementLease =
                     await context.FileSystem.AcquireSessionReplacementWriteLeaseAsync(
                         lifecycleLease))
        {
            replacementGeneration = context.FileSystem.RotateSessionGeneration(
                replacementLease);
        }
        var before = await context.CaptureBytesAsync(
            ReceiptResumeProtectedPaths);

        var service = CreateQteService(context.FileSystem);
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var lease =
                await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                replacementGeneration,
                lease,
                () => service.ResumeDeferredEffectResolutionAsync(
                    lease,
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        });

        Assert.Contains(
            "qte_deferred_continuation_session_mismatch",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(ReceiptResumeProtectedPaths));
    }

    [Theory]
    [InlineData("final:mechanics")]
    [InlineData("final:history")]
    [InlineData("final:runtime")]
    [InlineData("final:input/qte_effect_resolution_request.json")]
    [InlineData("final:output/qte_effect_resolution_receipts.json")]
    [InlineData("final:ready/qte_effect_resolution_complete.json")]
    [InlineData("final:continuation")]
    public async Task BoundedReceipt_FinalFailureAtEveryMutationRestoresExactAwaitingBoundary(
        string failurePoint)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const int sourceTurn = 42;
        var scenario = await PrepareAwaitingReceiptAsync(
            context,
            "session_qte_deferred_final_failure");
        await WriteReceiptTransportAsync(
            context,
            scenario.Request,
            scenario.PendingRequestId,
            amount: 2m);
        var before = await context.CaptureBytesAsync(
            ReceiptResumeProtectedPaths);
        var injected = new IOException(
            $"Injected deferred QTE failure at {failurePoint}.");
        var service = CreateQteService(
            context.FileSystem,
            new QteSceneServiceHooks
            {
                AfterDeferredEffectMutationAsync = actual =>
                    string.Equals(actual, failurePoint, StringComparison.Ordinal)
                        ? Task.FromException(injected)
                        : Task.CompletedTask
            });

        var error = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await using var lease =
                await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                scenario.Generation,
                lease,
                () => service.ResumeDeferredEffectResolutionAsync(
                    lease,
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        });

        Assert.Same(injected, error);
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(ReceiptResumeProtectedPaths));
        Assert.Equal(
            "awaiting_receipt",
            (await context.ReadJsonAsync(ContinuationPath))!["state"]!
                .GetValue<string>());
    }

    [Theory]
    [InlineData("next_wave:game_state/control/pending_effect_resolutions.json")]
    [InlineData("next_wave:output/qte_effect_resolution_receipts.json")]
    [InlineData("next_wave:ready/qte_effect_resolution_complete.json")]
    [InlineData("next_wave:game_state/control/qte_deferred_effect_continuation.json")]
    [InlineData("next_wave:game_state/control/qte_runtime.json")]
    [InlineData("next_wave:input/qte_effect_resolution_request.json")]
    public async Task BoundedReceipt_NextWaveFailureAtEveryMutationRestoresExactCurrentWave(
        string failurePoint)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const int sourceTurn = 42;
        var scenario = await PrepareAwaitingReceiptAsync(
            context,
            "session_qte_deferred_next_wave_failure",
            remainingUses: 2);
        await WriteReceiptTransportAsync(
            context,
            scenario.Request,
            scenario.PendingRequestId,
            amount: 2m);
        var before = await context.CaptureBytesAsync(
            ReceiptResumeProtectedPaths);
        var injected = new IOException(
            $"Injected deferred QTE failure at {failurePoint}.");
        var service = CreateQteService(
            context.FileSystem,
            new QteSceneServiceHooks
            {
                AfterDeferredEffectMutationAsync = actual =>
                    string.Equals(actual, failurePoint, StringComparison.Ordinal)
                        ? Task.FromException(injected)
                        : Task.CompletedTask
            });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var lease =
                await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                scenario.Generation,
                lease,
                () => service.ResumeDeferredEffectResolutionAsync(
                    lease,
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        });

        Assert.Contains(
            "qte_deferred_next_wave_publication_conflict",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(ReceiptResumeProtectedPaths));
        Assert.True(JsonNode.DeepEquals(
            scenario.Request,
            await context.ReadJsonAsync(
                QteDeferredEffectContinuation.RequestPath)));
    }

    [Fact]
    public async Task LateFailure_RestoresSelectedBoundaryAndRetryReusesPersistentIdentities()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string sessionId = "session_qte_deferred_retry";
        const int sourceTurn = 42;
        await SeedDeferredAuthorityAsync(context, sessionId);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.DefinitionsPath))!.ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        Assert.Empty(definitions.Issues);
        var beforeState = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.StatePath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(beforeState.Ledger);
        Assert.Empty(beforeState.Issues);
        var poiseBefore = Assert.Single(
            beforeState.Ledger!.Entries,
            entry => entry.Coordinate.ResourceKey == "poise").Current;
        var healthBefore = Assert.Single(
            beforeState.Ledger.Entries,
            entry => entry.Coordinate.ResourceKey == "health").Current;
        var offer = BuildOffer(sourceTurn);
        await context.WriteJsonAsync(
            QteSceneService.QteOfferPath,
            JsonSerializer.SerializeToNode(offer)!.AsObject());
        var injected = new IOException("Injected failure after QTE history publication.");
        var service = CreateQteService(
            context.FileSystem,
            new QteSceneServiceHooks
            {
                AfterHistoryWrittenAsync = () => Task.FromException(injected)
            });

        string generation;
        await using (var writeLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            generation = context.FileSystem.GetOrCreateSessionGeneration(writeLease);
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.BeginAcceptedSceneAsync(
                    writeLease,
                    offer,
                    sourceTurn));
        }
        var acceptanceRoots = await context.CaptureBytesAsync(
            QteDeferredEffectContinuation.SealedRootPaths.ToArray());

        var error = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await using var writeLease =
                await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.ResolveActiveActionAsync(
                    writeLease,
                    "brace",
                    "fail",
                    sourceTurn,
                    allowPreexistingStateIssues: true));
        });
        Assert.Same(injected, error);
        Assert.Equal(
            acceptanceRoots,
            await context.CaptureBytesAsync(
                QteDeferredEffectContinuation.SealedRootPaths.ToArray()));
        var selected = (await context.ReadJsonAsync(ContinuationPath))!.AsObject();
        Assert.Equal("terminal_selected", selected["state"]!.GetValue<string>());
        var selectedBinding = selected["selectedTerminalBinding"]!.DeepClone();
        var identityLedger = selected["identityLedger"]!.DeepClone();
        Assert.NotEmpty(identityLedger.AsArray());
        var selectedRuntime = (await context.ReadJsonAsync(
            QteSceneService.QteRuntimePath))!.AsObject();
        Assert.Equal(
            "terminal_selected",
            selectedRuntime["activeScene"]!["effectResolutionState"]!
                .GetValue<string>());
        Assert.False(context.FileSystem.FileExists(QteSceneService.QteHistoryPath));

        service = CreateQteService(context.FileSystem);
        await using (var writeLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var resolution = await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                writeLease,
                () => service.ResolveActiveActionAsync(
                    writeLease,
                    "brace",
                    "fail",
                    sourceTurn,
                    allowPreexistingStateIssues: true));
            Assert.Equal("Completed", resolution.State);
        }

        var terminal = (await context.ReadJsonAsync(ContinuationPath))!.AsObject();
        Assert.Equal("terminal", terminal["state"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(
            selectedBinding,
            terminal["selectedTerminalBinding"]));
        Assert.True(JsonNode.DeepEquals(
            identityLedger,
            terminal["identityLedger"]));
        Assert.Single((await context.ReadJsonAsync(
            QteSceneService.QteHistoryPath))!.AsArray());

        var afterState = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.StatePath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(afterState.Ledger);
        Assert.Empty(afterState.Issues);
        Assert.Equal(
            poiseBefore - 5m,
            Assert.Single(
                afterState.Ledger!.Entries,
                entry => entry.Coordinate.ResourceKey == "poise").Current);
        Assert.Equal(
            healthBefore - 3m,
            Assert.Single(
                afterState.Ledger.Entries,
                entry => entry.Coordinate.ResourceKey == "health").Current);

        var history = ResourceHistoryState.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.HistoryPath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(history.History);
        Assert.Empty(history.Issues);
        var retryTransitions = history.History!.Transitions
            .Where(transition => transition.Turn == sourceTurn)
            .ToArray();
        Assert.Single(retryTransitions, transition =>
            transition.OriginKind == "narrative_outcome" &&
            transition.EventRef == "turn_42:qte_terminal:1:resource:1");
        Assert.Single(retryTransitions, transition =>
            transition.Phase == ResourceMutationPhase.EffectTrigger &&
            transition.OriginKind == "effect_component");
    }

    private sealed record AwaitingReceiptScenario(
        string Generation,
        JsonObject Request,
        string PendingRequestId);

    private static async Task<AwaitingReceiptScenario> PrepareAwaitingReceiptAsync(
        EffectMaterializationTestContext context,
        string sessionId,
        int remainingUses = 1)
    {
        const int sourceTurn = 42;
        await SeedDeferredAuthorityAsync(
            context,
            sessionId,
            resolutionMode: "bounded_receipt",
            remainingUses);
        var offer = BuildOffer(sourceTurn);
        await context.WriteJsonAsync(
            QteSceneService.QteOfferPath,
            JsonSerializer.SerializeToNode(offer)!.AsObject());
        var service = CreateQteService(context.FileSystem);
        string generation;
        await using (var lease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            generation = context.FileSystem.GetOrCreateSessionGeneration(lease);
            await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                lease,
                () => service.BeginAcceptedSceneAsync(
                    lease,
                    offer,
                    sourceTurn));
            var resolution = await SessionOperationContext.RunBoundAsync(
                context.FileSystem,
                generation,
                lease,
                () => service.ResolveActiveActionAsync(
                    lease,
                    "brace",
                    "fail",
                    sourceTurn,
                    allowPreexistingStateIssues: true));
            Assert.Equal("AwaitingEffectResolution", resolution.State);
        }

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourceMaterializationContract.DefinitionsPath))!.ToJsonString(),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid);
        Assert.NotNull(definitions.Catalog);
        var pending = ResourcePendingResolutionState.ParseCanonical(
            (await context.ReadJsonAsync(
                ResourcePendingResolutionState.PendingPath))!.ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(pending.IsValid);
        var pendingRequest = Assert.Single(pending.State!.Requests);
        var request = (await context.ReadJsonAsync(
            QteDeferredEffectContinuation.RequestPath))!.AsObject();
        return new AwaitingReceiptScenario(
            generation,
            request,
            pendingRequest.RequestId);
    }

    private static JsonObject BuildReceiptRoot(
        JsonObject request,
        JsonArray receipts) => new()
    {
        ["schemaVersion"] = 1,
        ["requestKind"] = request["requestKind"]!.DeepClone(),
        ["sessionId"] = request["sessionId"]!.DeepClone(),
        ["sessionGeneration"] = request["sessionGeneration"]!.DeepClone(),
        ["continuationId"] = request["continuationId"]!.DeepClone(),
        ["requestId"] = request["requestId"]!.DeepClone(),
        ["waveId"] = request["waveId"]!.DeepClone(),
        ["waveOrdinal"] = request["waveOrdinal"]!.DeepClone(),
        ["acceptedSourceTurn"] = request["acceptedSourceTurn"]!.DeepClone(),
        ["qteId"] = request["qteId"]!.DeepClone(),
        ["selectedTerminalFingerprint"] =
            request["selectedTerminalFingerprint"]!.DeepClone(),
        ["pendingStateFingerprint"] =
            request["pendingStateFingerprint"]!.DeepClone(),
        ["fullTurnFingerprint"] =
            request["fullTurnFingerprint"]!.DeepClone(),
        ["semanticTurnFingerprint"] =
            request["semanticTurnFingerprint"]!.DeepClone(),
        ["effectResolutionReceipts"] = receipts.DeepClone()
    };

    private static async Task WriteReceiptTransportAsync(
        EffectMaterializationTestContext context,
        JsonObject request,
        string pendingRequestId,
        decimal amount)
    {
        await context.WriteJsonAsync(
            QteDeferredEffectContinuation.ReceiptPath,
            BuildReceiptRoot(
                request,
                new JsonArray(new JsonObject
                {
                    ["requestId"] = pendingRequestId,
                    ["resultKind"] = "resource_delta",
                    ["amount"] = amount,
                    ["reason"] = "Квитанция текущей волны QTE."
                })));
        var receiptBytes = await context.FileSystem.ReadFileBytesAsync(
            QteDeferredEffectContinuation.ReceiptPath);
        Assert.NotNull(receiptBytes);
        await context.WriteJsonAsync(
            QteDeferredEffectContinuation.ReadyPath,
            BuildReadyRoot(request, receiptBytes!));
    }

    private static JsonObject BuildReadyRoot(
        JsonObject request,
        byte[] receiptBytes) => new()
    {
        ["schemaVersion"] = 1,
        ["requestKind"] = request["requestKind"]!.DeepClone(),
        ["sessionId"] = request["sessionId"]!.DeepClone(),
        ["sessionGeneration"] = request["sessionGeneration"]!.DeepClone(),
        ["continuationId"] = request["continuationId"]!.DeepClone(),
        ["requestId"] = request["requestId"]!.DeepClone(),
        ["waveId"] = request["waveId"]!.DeepClone(),
        ["waveOrdinal"] = request["waveOrdinal"]!.DeepClone(),
        ["acceptedSourceTurn"] = request["acceptedSourceTurn"]!.DeepClone(),
        ["qteId"] = request["qteId"]!.DeepClone(),
        ["pendingStateFingerprint"] =
            request["pendingStateFingerprint"]!.DeepClone(),
        ["receiptsFingerprint"] = "sha256:" + Convert.ToHexString(
                SHA256.HashData(receiptBytes))
            .ToLowerInvariant(),
        ["timestamp"] = "2026-08-24T23:40:00.0000000+10:00",
        ["status"] = "success"
    };

    private static QteSceneService CreateQteService(
        FileSystemManager fileSystem,
        QteSceneServiceHooks? hooks = null)
    {
        var settings = new GameSettings();
        var stateManager = new StateManager(
            fileSystem,
            settings,
            NullLogger<StateManager>.Instance);
        return new QteSceneService(
            fileSystem,
            settings,
            null!,
            new ImageService(
                fileSystem,
                settings,
                new LocalizationManager { CurrentLanguage = "ru" },
                NullLogger<ImageService>.Instance),
            new AudioService(
                fileSystem,
                settings,
                NullLogger<AudioService>.Instance),
            new StateDistributor(
                fileSystem,
                NullLogger<StateDistributor>.Instance),
            new ValidationService(
                fileSystem,
                NullLogger<ValidationService>.Instance),
            new CanonicalStateNormalizer(
                fileSystem,
                NullLogger<CanonicalStateNormalizer>.Instance),
            stateManager,
            NullLogger<QteSceneService>.Instance,
            inputSource: null,
            hooks: hooks);
    }

    private static async Task SeedDeferredAuthorityAsync(
        EffectMaterializationTestContext context,
        string sessionId,
        string resolutionMode = "deterministic",
        int remainingUses = 1)
    {
        await context.WriteJsonAsync(
            "game_state/history/chat_log.json",
            new JsonObject
            {
                ["sessionId"] = sessionId,
                ["entries"] = new JsonArray()
            });
        var definition = CreateResourceDamagedDefinition(
            resolutionMode,
            remainingUses);
        var effect = CreateResourceDamagedEffect(
            resolutionMode,
            remainingUses);
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
    }

    private static QteSceneService.QteOffer BuildOffer(int sourceTurn) => new()
    {
        QteId = "qte_deferred_effect_damage",
        Title = "Обвал",
        OfferText = "Удержаться под каменной волной.",
        IntroNarrative = "Свод рушится.",
        StartChapterId = "impact",
        SourceTurnNumber = sourceTurn,
        Chapters =
        [
            new QteSceneService.QteChapter
            {
                ChapterId = "impact",
                Title = "Удар",
                Narrative = "Камни достигают героя.",
                Actions =
                [
                    new QteSceneService.QteAction
                    {
                        ActionId = "brace",
                        Label = "Упереться",
                        Check = new QteSceneService.QteCheck
                        {
                            Type = "BranchChoice",
                            BaseDifficulty = 1,
                            Config = new JsonObject { ["choiceGrade"] = "fail" }
                        },
                        Routing = new QteSceneService.QteRouting
                        {
                            Success = new QteSceneService.QteBranchTarget
                            {
                                TerminalOutcomeId = "crushing"
                            },
                            Partial = new QteSceneService.QteBranchTarget
                            {
                                TerminalOutcomeId = "crushing"
                            },
                            Fail = new QteSceneService.QteBranchTarget
                            {
                                TerminalOutcomeId = "crushing"
                            }
                        }
                    }
                ]
            }
        ],
        TerminalOutcomes =
        [
            new QteSceneService.QteTerminalOutcome
            {
                OutcomeId = "crushing",
                Title = "Сокрушительный удар",
                FinalNarrative = "Героя сбивает с ног.",
                GmSummary = "QTE наносит урон стойкости.",
                ResponseFragment = new JsonObject
                {
                    ["response"] = "Каменная волна сбивает равновесие.",
                    ["resourceChanges"] = new JsonArray(new JsonObject
                    {
                        ["operation"] = "damage",
                        ["target"] = new JsonObject
                        {
                            ["kind"] = "player",
                            ["targetId"] = "player_current"
                        },
                        ["resourceKey"] = "poise",
                        ["amount"] = 5,
                        ["source"] = new JsonObject
                        {
                            ["kind"] = "narrative_outcome"
                        },
                        ["eventRef"] =
                            $"turn_{sourceTurn}:qte_terminal:1:resource:1",
                        ["reason"] = "Цена выбранного исхода QTE."
                    })
                }
            }
        ]
    };

    private static JsonObject CreateResourceDamagedDefinition(
        string resolutionMode = "deterministic",
        int remainingUses = 1)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "periodic_damage");
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = remainingUses,
            ["consumingEventTypes"] = new JsonArray("resource_damaged")
        };
        definition["triggers"] = new JsonArray(
            CreateResourceDamagedTrigger(resolutionMode));
        return definition;
    }

    private static JsonObject CreateResourceDamagedEffect(
        string resolutionMode = "deterministic",
        int remainingUses = 1)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "periodic_damage");
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = remainingUses,
            ["consumingTriggerIds"] = new JsonArray("on_resource_damaged")
        };
        effect["triggers"] = new JsonArray(
            CreateResourceDamagedTrigger(resolutionMode));
        return effect;
    }

    private static JsonObject CreateResourceDamagedTrigger(
        string resolutionMode = "deterministic") => new()
    {
        ["triggerId"] = "on_resource_damaged",
        ["eventType"] = "resource_damaged",
        ["priority"] = 100,
        ["componentIds"] = new JsonArray("component_001"),
        ["consumeUses"] = true,
        ["resolutionMode"] = resolutionMode
    };
}
