using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private static int _originalIntakeConflictFramePreparationCount;
    private static double _originalIntakeConflictFramePreparationMilliseconds;
    private static int _uncachedConflictFramePreparationCount;
    private const string TrustedConflictFrameSigningTuple =
        "42\nChaos Sea\nsession_resource_materialization\nrequest_resource_materialization\n" +
        "Validate unified resource materialization.\n2026-08-15T00:00:00Z\n" +
        "Unified resource materialization integration test";
    private static readonly ConcurrentDictionary<TrustedConflictFrameKey, int> TrustedConflictFramePreparations = new();
    private static readonly ConcurrentDictionary<TrustedConflictFrameKey, Lazy<Task<PreparedFixtureTree>>>
        TrustedConflictFrameTemplates = new();

    /// <summary>
    /// Identifies only a known original seed version and its complete signed fixture inputs.
    /// </summary>
    /// <param name="SeedVersion">
    /// Sealed version identifier selected by exact known seeder identity; arbitrary callbacks have no key.
    /// </param>
    /// <param name="OrderedDice">
    /// Exact ordered integer pool, with <see langword="null"/> and the explicit default pool represented identically.
    /// </param>
    /// <param name="PlayerCurrent">
    /// Original player balance without conversion to an integer or rounded string.
    /// </param>
    /// <param name="OppositionCurrent">
    /// Original opposition balance without conversion to an integer or rounded string.
    /// </param>
    /// <param name="SigningTuple">
    /// Fixed turn, realm, session, request, action, timestamp and source label used by the actual fixture signer.
    /// </param>
    private sealed record TrustedConflictFrameKey(string SeedVersion, string OrderedDice,
        decimal PlayerCurrent, decimal OppositionCurrent, string SigningTuple);

    /// <summary>
    /// Identifies known hookless preparation inputs for observation without granting cache eligibility to custom callbacks.
    /// </summary>
    /// <param name="hooks">
    /// <see langword="null"/> for an ordinary frame; any filesystem hooks exclude the frame.
    /// </param>
    /// <param name="seedOriginalInputs">
    /// <see langword="null"/> for the default profile or one exact named trusted seeder; every other delegate is excluded.
    /// </param>
    /// <param name="signedDice">
    /// Exact ordered pool; <see langword="null"/> has the same identity as 15, 5, 12, 8.
    /// </param>
    /// <param name="playerCurrent">
    /// Exact initial player balance.
    /// </param>
    /// <param name="oppositionCurrent">
    /// Exact initial opposition balance.
    /// </param>
    /// <param name="initializeContext">
    /// <see langword="null"/> for the trusted scaffold; any initializer excludes the frame.
    /// </param>
    /// <param name="captureOriginalSnapshot">
    /// <see langword="null"/> for the fixed fixture signer; any signing callback excludes the frame.
    /// </param>
    /// <returns>
    /// The complete trusted input key, or <see langword="null"/> when any preparation input is custom.
    /// </returns>
    private static TrustedConflictFrameKey? ReadTrustedConflictFrameKey(FileSystemManagerHooks? hooks,
        Func<ResourceMaterializationTestContext, Task>? seedOriginalInputs, int[]? signedDice,
        decimal playerCurrent, decimal oppositionCurrent,
        Func<ResourceMaterializationTestContext, Task>? initializeContext,
        Func<ResourceMaterializationTestContext, Task>? captureOriginalSnapshot)
    {
        if (hooks is not null || initializeContext is not null || captureOriginalSnapshot is not null)
            return null;
        var seedVersion = seedOriginalInputs is null ? "default_v1" :
            seedOriginalInputs == (Func<ResourceMaterializationTestContext, Task>)SeedOriginalIntakeBaselinesAsync
                ? "original_intake_v1" :
            seedOriginalInputs == (Func<ResourceMaterializationTestContext, Task>)SeedOriginalIntakeTierTwoBaselinesAsync
                ? "original_intake_tier_two_v1" : null;
        return seedVersion is null ? null : new(seedVersion,
            string.Join(",", (signedDice ?? [15, 5, 12, 8]).Select(die =>
                die.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            playerCurrent, oppositionCurrent, TrustedConflictFrameSigningTuple);
    }

    /// <summary>
    /// Prepares one known signed frame from its exact immutable key and detaches only session-file bytes.
    /// </summary>
    /// <param name="key">
    /// Trusted seed version, ordered dice, balances and fixed signing tuple selected before lookup.
    /// </param>
    /// <returns>
    /// Portable signed session bytes without a retained root, generation, filesystem or runtime owner.
    /// </returns>
    private static async Task<PreparedFixtureTree> PrepareTrustedConflictFrameTemplateAsync(TrustedConflictFrameKey key)
    {
        Func<ResourceMaterializationTestContext, Task>? seedOriginalInputs = key.SeedVersion switch
        {
            "default_v1" => null,
            "original_intake_v1" => SeedOriginalIntakeBaselinesAsync,
            "original_intake_tier_two_v1" => SeedOriginalIntakeTierTwoBaselinesAsync,
            _ => throw new InvalidOperationException("Unrecognized trusted conflict-frame seed version.")
        };
        var signedDice = key.OrderedDice.Split(',').Select(die =>
            int.Parse(die, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: seedOriginalInputs, signedDice: signedDice,
            playerCurrent: key.PlayerCurrent, oppositionCurrent: key.OppositionCurrent,
            bypassPreparedFixture: true);
        return PreparedFixtureTree.Capture(context.FileSystem.GameSessionPath);
    }

    /// <summary>
    /// Requires distinct trusted seed, ordered-pool and balance inputs to prepare once while retaining real local authority.
    /// </summary>
    /// <returns>
    /// Completion after exact signed inputs, preparation reuse, fresh owners and local snapshot tampering have been checked.
    /// </returns>
    [Fact]
    public async Task PreparedConflictFrame_TrustedVariantsReuseExactInputsWithFreshOwners()
    {
        await using var first = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [15, 5, 13, 8]);
        await using var second = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [15, 5, 13, 8]);
        await using var reordered = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [15, 5, 8, 13]);
        await using var playerBalance = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [15, 5, 13, 8], playerCurrent: 3m);
        await using var oppositionBalance = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [15, 5, 13, 8], oppositionCurrent: 3m);
        await using var defaultSeed = await CreateCompleteConflictFrameContextAsync(signedDice: [15, 5, 13, 8]);
        await using var tierTwo = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeTierTwoBaselinesAsync, signedDice: [5, 15, 20, 18, 9, 8]);
        foreach (var context in new[] { first, second, reordered, playerBalance, oppositionBalance, defaultSeed, tierTwo })
            await AssertPreparedConflictFrameAuthorityAsync(context, expectedValid: true);
        await AssertTrustedConflictFrameInputsAsync(first, [15, 5, 13, 8], 6m, 6m, "original_intake_v1");
        await AssertTrustedConflictFrameInputsAsync(second, [15, 5, 13, 8], 6m, 6m, "original_intake_v1");
        await AssertTrustedConflictFrameInputsAsync(reordered, [15, 5, 8, 13], 6m, 6m, "original_intake_v1");
        await AssertTrustedConflictFrameInputsAsync(playerBalance, [15, 5, 13, 8], 3m, 6m, "original_intake_v1");
        await AssertTrustedConflictFrameInputsAsync(oppositionBalance, [15, 5, 13, 8], 6m, 3m, "original_intake_v1");
        await AssertTrustedConflictFrameInputsAsync(defaultSeed, [15, 5, 13, 8], 6m, 6m, "default_v1");
        await AssertTrustedConflictFrameInputsAsync(tierTwo, [5, 15, 20, 18, 9, 8], 6m, 6m, "original_intake_tier_two_v1");
        foreach (var key in new[]
        {
            ReadTrustedConflictFrameKey(null, SeedOriginalIntakeBaselinesAsync, [15, 5, 13, 8], 6m, 6m, null, null),
            ReadTrustedConflictFrameKey(null, SeedOriginalIntakeBaselinesAsync, [15, 5, 8, 13], 6m, 6m, null, null),
            ReadTrustedConflictFrameKey(null, SeedOriginalIntakeBaselinesAsync, [15, 5, 13, 8], 3m, 6m, null, null),
            ReadTrustedConflictFrameKey(null, SeedOriginalIntakeBaselinesAsync, [15, 5, 13, 8], 6m, 3m, null, null),
            ReadTrustedConflictFrameKey(null, null, [15, 5, 13, 8], 6m, 6m, null, null),
            ReadTrustedConflictFrameKey(null, SeedOriginalIntakeTierTwoBaselinesAsync, [5, 15, 20, 18, 9, 8], 6m, 6m, null, null)
        })
            Assert.True(TrustedConflictFramePreparations.TryGetValue(key!, out var count) && count == 1,
                $"Expected one genuine preparation for {key}; actual={count}.");

        Assert.NotEqual(first.RootPath, second.RootPath);
        Assert.NotSame(first.FileSystem, second.FileSystem);
        Assert.NotSame(first.Validator, second.Validator);
        foreach (var context in new[] { first, second })
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            await WriteOriginalIntakeDraftAsync(context);
        }
        await using var firstLease = await first.FileSystem.AcquireCanonicalWriteLeaseAsync();
        await using var secondLease = await second.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.NotEqual(first.FileSystem.ReadExistingSessionGeneration(firstLease),
            second.FileSystem.ReadExistingSessionGeneration(secondLease));
        var firstAdmission = await first.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(firstLease);
        var secondAdmission = await second.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(secondLease);
        AssertNoConflictFrameErrors(firstAdmission.Issues);
        AssertNoConflictFrameErrors(secondAdmission.Issues);
        using var firstCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(firstAdmission.Capture);
        using var secondCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(secondAdmission.Capture);
        Assert.NotSame(firstCapture, secondCapture);
        Assert.NotSame(OriginalCaptureField(firstCapture, "_source"), OriginalCaptureField(secondCapture, "_source"));
        Assert.True(firstCapture.IsCurrentOwner);
        Assert.True(secondCapture.IsCurrentOwner);
        Assert.Throws<InvalidOperationException>(() => firstCapture.ReadC1ImageInventory(secondLease));
        Assert.Throws<InvalidOperationException>(() => secondCapture.ReadC1ImageInventory(firstLease));
        var signed = secondCapture.ReadVerifiedSignedC1Origin(secondLease);
        var snapshotPath = "game_state/control/pending_turn_snapshot/" + AfterlifeSpiritualConflictState.StatePath;
        var originalBytes = await second.FileSystem.ReadFileBytesAsync(secondLease, snapshotPath);
        await first.FileSystem.WriteFileAtomicAsync(firstLease, snapshotPath, "{}");
        Assert.False(PendingTurnSnapshotReader.ReadCurrent(first.FileSystem, firstLease,
            [AfterlifeSpiritualConflictState.StatePath]).Success);
        Assert.Throws<InvalidOperationException>(() => firstCapture.ReadVerifiedSignedC1Origin(firstLease));
        Assert.Equal(originalBytes, await second.FileSystem.ReadFileBytesAsync(secondLease, snapshotPath));
        Assert.Equal(signed.SnapshotFingerprint, secondCapture.ReadVerifiedSignedC1Origin(secondLease).SnapshotFingerprint);
        Assert.True(secondCapture.IsCurrentOwner);
    }

    /// <summary>
    /// Treats <see langword="null"/> and explicit default pools as the same existing default and intake preparation profiles.
    /// </summary>
    /// <returns>
    /// Completion after both profiles retain their original once-only counters and exact authenticated dice.
    /// </returns>
    [Fact]
    public async Task PreparedConflictFrame_TrustedVariantsCanonicalizeExplicitDefaultPool()
    {
        await using var defaultImplicit = await CreateCompleteConflictFrameContextAsync();
        await using var defaultExplicit = await CreateCompleteConflictFrameContextAsync(signedDice: [15, 5, 12, 8]);
        await using var intakeImplicit = await CreateCompleteConflictFrameContextAsync(seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await using var intakeExplicit = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [15, 5, 12, 8]);
        foreach (var context in new[] { defaultImplicit, defaultExplicit, intakeImplicit, intakeExplicit })
            await AssertPreparedConflictFrameAuthorityAsync(context, expectedValid: true);
        await AssertTrustedConflictFrameInputsAsync(defaultExplicit, [15, 5, 12, 8], 6m, 6m, "default_v1");
        await AssertTrustedConflictFrameInputsAsync(intakeExplicit, [15, 5, 12, 8], 6m, 6m, "original_intake_v1");
        foreach (var seeder in new Func<ResourceMaterializationTestContext, Task>?[] { null, SeedOriginalIntakeBaselinesAsync })
        {
            var key = ReadTrustedConflictFrameKey(null, seeder, null, 6m, 6m, null, null)!;
            Assert.True(TrustedConflictFramePreparations.TryGetValue(key, out var count) && count == 1,
                $"Expected one preparation for implicit and explicit default pool {key}; actual={count}.");
        }
        Assert.Equal(1, Volatile.Read(ref _defaultConflictFramePreparationCount));
        Assert.Equal(1, Volatile.Read(ref _originalIntakeConflictFramePreparationCount));
    }

    /// <summary>
    /// Executes every custom preparation twice without allowing equivalent callback output to claim a trusted cache profile.
    /// </summary>
    /// <returns>
    /// Completion after hooks, initializers, genuine custom signers and arbitrary seed callbacks all run separately.
    /// </returns>
    [Fact]
    public async Task PreparedConflictFrame_TrustedVariantsBypassCustomPreparation()
    {
        await using var warm = await CreateCompleteConflictFrameContextAsync(seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        var before = Volatile.Read(ref _uncachedConflictFramePreparationCount);
        var hookWrites = 0;
        var initialized = 0;
        var signed = 0;
        var seeded = 0;
        for (var iteration = 1; iteration <= 2; iteration++)
        {
            await using var hooked = await CreateCompleteConflictFrameContextAsync(
                hooks: new FileSystemManagerHooks
                {
                    BeforeCanonicalMutationAsync = path =>
                    {
                        if (path == ResourceMaterializationContract.DefinitionsPath) hookWrites++;
                        return Task.CompletedTask;
                    }
                }, seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
            await using var initializedContext = await CreateCompleteConflictFrameContextAsync(
                seedOriginalInputs: SeedOriginalIntakeBaselinesAsync,
                initializeContext: async context =>
                {
                    initialized++;
                    await context.WriteExactJsonAsync(ProjectionNarrativePath,
                        new JsonObject { ["response"] = "initializer " + initialized }.ToJsonString());
                });
            await using var signedContext = await CreateCompleteConflictFrameContextAsync(
                seedOriginalInputs: SeedOriginalIntakeBaselinesAsync,
                captureOriginalSnapshot: async context =>
                {
                    signed++;
                    await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
                        preGeneratedDices1d20: [15, 5, 12, 8]);
                });
            await using var arbitrary = await CreateCompleteConflictFrameContextAsync(seedOriginalInputs: async context =>
            {
                seeded++;
                await SeedOriginalIntakeBaselinesAsync(context);
                await context.WriteExactJsonAsync(ProjectionNarrativePath,
                    new JsonObject { ["response"] = "arbitrary " + seeded }.ToJsonString());
            });
            Assert.Equal("initializer " + iteration,
                (await initializedContext.ReadJsonAsync(ProjectionNarrativePath))!["response"]!.GetValue<string>());
            Assert.Equal("arbitrary " + iteration,
                (await arbitrary.ReadJsonAsync(ProjectionNarrativePath))!["response"]!.GetValue<string>());
            foreach (var context in new[] { hooked, initializedContext, signedContext, arbitrary })
                await AssertPreparedConflictFrameAuthorityAsync(context, expectedValid: true);
        }
        Assert.Equal(2, hookWrites);
        Assert.Equal(2, initialized);
        Assert.Equal(2, signed);
        Assert.Equal(2, seeded);
        Assert.Equal(8, Volatile.Read(ref _uncachedConflictFramePreparationCount) - before);
    }

    /// <summary>
    /// Checks the actual signed request, both ledger balances and profile-specific original authority bytes.
    /// </summary>
    /// <param name="context">
    /// Fresh materialized or genuinely prepared frame being checked.
    /// </param>
    /// <param name="dice">
    /// Exact expected ordered signed pool.
    /// </param>
    /// <param name="playerCurrent">
    /// Expected original player balance.
    /// </param>
    /// <param name="oppositionCurrent">
    /// Expected original opposition balance.
    /// </param>
    /// <param name="seedVersion">
    /// Expected known seed/version whose physical and signed baselines must remain distinct.
    /// </param>
    /// <returns>
    /// Completion after the full fixed signing tuple and expected profile inputs have been checked.
    /// </returns>
    private static async Task AssertTrustedConflictFrameInputsAsync(ResourceMaterializationTestContext context,
        int[] dice, decimal playerCurrent, decimal oppositionCurrent, string seedVersion)
    {
        var request = (await context.ReadJsonAsync("input/turn_request.json"))!;
        var manifest = (await context.ReadJsonAsync("game_state/control/pending_turn_snapshot.json"))!;
        foreach (var root in new[] { request, manifest })
        {
            Assert.Equal(42, root["turnNumber"]!.GetValue<int>());
            Assert.Equal("session_resource_materialization", root["sessionId"]!.GetValue<string>());
            Assert.Equal("request_resource_materialization", root["requestId"]!.GetValue<string>());
            Assert.Equal("Validate unified resource materialization.", root["playerAction"]!.GetValue<string>());
            Assert.Equal(dice, root["preGeneratedDices1d20"]!.AsArray().Select(die => die!.GetValue<int>()));
        }
        Assert.Equal("Chaos Sea", request["currentRealm"]!.GetValue<string>());
        Assert.Equal("Chaos Sea", manifest["progressionControl"]!["currentRealm"]!.GetValue<string>());
        Assert.Equal("2026-08-15T00:00:00Z", manifest["requestTimestamp"]!.GetValue<string>());
        Assert.Equal("Unified resource materialization integration test", manifest["sourceLabel"]!.GetValue<string>());
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
        var state = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions.Catalog!, allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        Assert.Equal(playerCurrent, Assert.Single(state.Ledger!.Entries,
            entry => entry.Coordinate.ResourceOwnerId == "player_soul").Current);
        Assert.Equal(oppositionCurrent, Assert.Single(state.Ledger.Entries,
            entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide).Current);
        var intake = seedVersion != "default_v1";
        var itemIdentityPath = "game_state/control/pending_turn_snapshot/" + MortalItemIdentityState.StatePath;
        Assert.Equal(intake, context.FileSystem.FileExists(itemIdentityPath));
        if (intake)
        {
            Assert.Equal(MortalItemIdentityState.CreateEmptyRoot().ToJsonString(),
                await context.FileSystem.ReadFileAsync(itemIdentityPath));
            var locationPath = "game_state/control/pending_turn_snapshot/" + MortalLocationIdentityState.StatePath;
            Assert.Equal(await context.FileSystem.ReadFileAsync(MortalLocationIdentityState.StatePath),
                await context.FileSystem.ReadFileAsync(locationPath));
        }
        var expectedTier = seedVersion == "original_intake_tier_two_v1" ? 2 : 0;
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!;
        foreach (var profile in profiles["profiles"]!.AsArray())
            Assert.Equal(expectedTier, profile!["standardArts"]!["pressure"]!.GetValue<int>());
        var soul = (await context.ReadJsonAsync("game_state/meta/soul_state.json"))!;
        Assert.Equal(expectedTier, soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!["pressure"]!.GetValue<int>());
        var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!;
        Assert.Equal(expectedTier, conflict["activeConflict"]!["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]!["pressure"]!.GetValue<int>());
        foreach (var path in new[] { ResourceMaterializationContract.StatePath, AfterlifeEntityProfileState.StatePath,
            "game_state/meta/soul_state.json", AfterlifeSpiritualConflictState.StatePath })
            Assert.Equal(await context.FileSystem.ReadFileBytesAsync(path),
                await context.FileSystem.ReadFileBytesAsync("game_state/control/pending_turn_snapshot/" + path));
    }

    private static readonly Lazy<Task<PreparedFixtureTree>> DefaultConflictFrameTemplate =
        new(PrepareDefaultConflictFrameTemplateAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<Task<PreparedFixtureTree>> OriginalIntakeConflictFrameTemplate =
        new(PrepareOriginalIntakeConflictFrameTemplateAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Prepares the ordinary service fixture through its real signer once and detaches its complete relative byte corpus.
    /// </summary>
    /// <returns>
    /// Prepared signed files without a retained filesystem, validator, lease or C2 owner.
    /// </returns>
    private static async Task<PreparedFixtureTree> PrepareDefaultConflictFrameTemplateAsync()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(bypassPreparedFixture: true);
        return PreparedFixtureTree.Capture(context.FileSystem.GameSessionPath);
    }

    /// <summary>
    /// Prepares the exact named intake baseline through its real seeder and signer once.
    /// </summary>
    /// <returns>
    /// Detached signed session files without runtime generation files or retained live owners.
    /// </returns>
    private static async Task<PreparedFixtureTree> PrepareOriginalIntakeConflictFrameTemplateAsync()
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, bypassPreparedFixture: true);
        var prepared = PreparedFixtureTree.Capture(context.FileSystem.GameSessionPath);
        Volatile.Write(ref _originalIntakeConflictFramePreparationMilliseconds,
            System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return prepared;
    }

    /// <summary>
    /// Reuses the named intake bytes while authenticating fresh generations and lease-bound C2 captures.
    /// </summary>
    /// <returns>
    /// A task completing after local tampering, foreign leases and source disposal leave later copies independent.
    /// </returns>
    [Fact]
    public async Task PreparedOriginalIntakeConflictFrame_ReusesBytesWithFreshGenerationsAndC2Owners()
    {
        await using var first = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        var warmCopyStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        await using var second = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        var warmCopyMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(warmCopyStarted).TotalMilliseconds;
        Assert.Equal(1, Volatile.Read(ref _originalIntakeConflictFramePreparationCount));
        Assert.NotEqual(first.RootPath, second.RootPath);
        Assert.NotSame(first.FileSystem, second.FileSystem);
        Assert.NotSame(first.Validator, second.Validator);
        Assert.NotSame(first.Normalizer, second.Normalizer);
        var snapshotPath = "game_state/control/pending_turn_snapshot/" + AfterlifeSpiritualConflictState.StatePath;
        var originalBytes = await second.FileSystem.ReadFileBytesAsync(snapshotPath);
        Assert.Equal(originalBytes, await first.FileSystem.ReadFileBytesAsync(snapshotPath));
        foreach (var context in new[] { first, second })
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            await WriteOriginalIntakeDraftAsync(context);
        }

        string firstGeneration;
        string secondGeneration;
        string snapshotFingerprint;
        await using (var firstLease = await first.FileSystem.AcquireCanonicalWriteLeaseAsync())
        await using (var secondLease = await second.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            firstGeneration = Assert.IsType<string>(first.FileSystem.ReadExistingSessionGeneration(firstLease));
            secondGeneration = Assert.IsType<string>(second.FileSystem.ReadExistingSessionGeneration(secondLease));
            Assert.NotEqual(firstGeneration, secondGeneration);
            Assert.True(PendingTurnSnapshotReader.ReadCurrent(first.FileSystem, firstLease,
                [AfterlifeSpiritualConflictState.StatePath]).Success);
            Assert.True(PendingTurnSnapshotReader.ReadCurrent(second.FileSystem, secondLease,
                [AfterlifeSpiritualConflictState.StatePath]).Success);
            var firstAdmission = await first.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(firstLease);
            var secondAdmission = await second.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(secondLease);
            AssertNoConflictFrameErrors(firstAdmission.Issues);
            AssertNoConflictFrameErrors(secondAdmission.Issues);
            using var firstCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(firstAdmission.Capture);
            using var secondCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(secondAdmission.Capture);
            Assert.True(firstCapture.IsCurrentOwner);
            Assert.True(secondCapture.IsCurrentOwner);
            Assert.NotSame(firstCapture, secondCapture);
            Assert.NotSame(OriginalCaptureField(firstCapture, "_source"), OriginalCaptureField(secondCapture, "_source"));
            Assert.Equal(firstCapture.ReadC1ImageInventory(firstLease).RegisteredPaths,
                secondCapture.ReadC1ImageInventory(secondLease).RegisteredPaths);
            var signedOrigin = secondCapture.ReadVerifiedSignedC1Origin(secondLease);
            snapshotFingerprint = signedOrigin.SnapshotFingerprint;
            Assert.Equal(snapshotFingerprint, firstCapture.ReadVerifiedSignedC1Origin(firstLease).SnapshotFingerprint);
            Assert.Throws<InvalidOperationException>(() => firstCapture.ReadC1ImageInventory(secondLease));
            Assert.Throws<InvalidOperationException>(() => secondCapture.ReadC1ImageInventory(firstLease));
            Assert.True(firstCapture.IsCurrentOwner);
            Assert.True(secondCapture.IsCurrentOwner);

            await first.FileSystem.WriteFileAtomicAsync(firstLease, snapshotPath, "{}");
            Assert.False(PendingTurnSnapshotReader.ReadCurrent(first.FileSystem, firstLease,
                [AfterlifeSpiritualConflictState.StatePath]).Success);
            Assert.Throws<InvalidOperationException>(() => firstCapture.ReadVerifiedSignedC1Origin(firstLease));
            Assert.Equal(originalBytes, await second.FileSystem.ReadFileBytesAsync(secondLease, snapshotPath));
            Assert.Equal(snapshotFingerprint, secondCapture.ReadVerifiedSignedC1Origin(secondLease).SnapshotFingerprint);
            Assert.True(secondCapture.IsCurrentOwner);
        }

        await first.DisposeAsync();
        Assert.False(Directory.Exists(first.RootPath));
        await using var third = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        Assert.Equal(1, Volatile.Read(ref _originalIntakeConflictFramePreparationCount));
        Assert.Equal(originalBytes, await third.FileSystem.ReadFileBytesAsync(snapshotPath));
        await WriteCompleteConflictFrameExchangeAsync(third);
        await WriteOriginalIntakeDraftAsync(third);
        await using var thirdLease = await third.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var thirdGeneration = Assert.IsType<string>(third.FileSystem.ReadExistingSessionGeneration(thirdLease));
        Assert.NotEqual(firstGeneration, thirdGeneration);
        Assert.NotEqual(secondGeneration, thirdGeneration);
        Assert.True(PendingTurnSnapshotReader.ReadCurrent(third.FileSystem, thirdLease,
            [AfterlifeSpiritualConflictState.StatePath]).Success);
        var thirdAdmission = await third.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(thirdLease);
        AssertNoConflictFrameErrors(thirdAdmission.Issues);
        using var thirdCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(thirdAdmission.Capture);
        Assert.True(thirdCapture.IsCurrentOwner);
        Assert.Equal(snapshotFingerprint, thirdCapture.ReadVerifiedSignedC1Origin(thirdLease).SnapshotFingerprint);
        var diagnosticDirectory = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "fixture-performance");
        Directory.CreateDirectory(diagnosticDirectory);
        File.WriteAllText(Path.Combine(diagnosticDirectory, $"original-intake-{Environment.ProcessId}.json"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                PreparedProfile = "original_intake_default",
                PreparationMilliseconds = Volatile.Read(ref _originalIntakeConflictFramePreparationMilliseconds),
                FreshRootGenerationAndCopyMilliseconds = warmCopyMilliseconds,
                Preparations = Volatile.Read(ref _originalIntakeConflictFramePreparationCount)
            }));
    }

    /// <summary>
    /// Reuses a prepared signed baseline while each mutation and owner remains local to its own root.
    /// </summary>
    /// <returns>
    /// A task completing after independent roots authenticate, reject local tampering and leave the cache intact.
    /// </returns>
    [Fact]
    public async Task PreparedConflictFrame_ReusesPreparationWithFreshRootsAndAuthenticatedOwners()
    {
        await using var first = await CreateCompleteConflictFrameContextAsync();
        await using var second = await CreateCompleteConflictFrameContextAsync();
        Assert.Equal(1, Volatile.Read(ref _defaultConflictFramePreparationCount));
        Assert.NotEqual(first.RootPath, second.RootPath);
        Assert.NotSame(first.FileSystem, second.FileSystem);
        Assert.NotSame(first.Validator, second.Validator);
        Assert.NotSame(first.Normalizer, second.Normalizer);
        var statePath = ResourceMaterializationContract.StatePath;
        var snapshotPath = "game_state/control/pending_turn_snapshot/" + statePath;
        var original = await second.FileSystem.ReadFileBytesAsync(snapshotPath);
        await AssertPreparedConflictFrameAuthorityAsync(first, expectedValid: true);
        await AssertPreparedConflictFrameAuthorityAsync(second, expectedValid: true);
        await first.WriteExactJsonAsync(snapshotPath, "{}");
        await AssertPreparedConflictFrameAuthorityAsync(first, expectedValid: false);
        Assert.Equal(original, await second.FileSystem.ReadFileBytesAsync(snapshotPath));
        await first.DisposeAsync();
        Assert.False(Directory.Exists(first.RootPath));
        await using var third = await CreateCompleteConflictFrameContextAsync();
        Assert.Equal(1, Volatile.Read(ref _defaultConflictFramePreparationCount));
        Assert.Equal(original, await third.FileSystem.ReadFileBytesAsync(snapshotPath));
        await AssertPreparedConflictFrameAuthorityAsync(third, expectedValid: true);
        var customDefinitionWrites = 0;
        await using var customized = await CreateCompleteConflictFrameContextAsync(
            hooks: new FileSystemManagerHooks
            {
                BeforeCanonicalMutationAsync = path =>
                {
                    if (path == ResourceMaterializationContract.DefinitionsPath) customDefinitionWrites++;
                    return Task.CompletedTask;
                }
            }, signedDice: [1, 2, 3, 4], playerCurrent: 3m);
        Assert.Equal(1, customDefinitionWrites);
        var request = (await customized.ReadJsonAsync("input/turn_request.json"))!;
        Assert.Equal(new[] { 1, 2, 3, 4 }, request["preGeneratedDices1d20"]!.AsArray()
            .Select(die => die!.GetValue<int>()));
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await customized.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false);
        var state = ResourceStateContract.ParseCanonical(
            await customized.FileSystem.ReadFileAsync(statePath), definitions.Catalog!, allowMissingPristine: false);
        Assert.Equal(3m, Assert.Single(state.Ledger!.Entries,
            entry => entry.Coordinate.ResourceOwnerId == "player_soul").Current);
        await AssertPreparedConflictFrameAuthorityAsync(customized, expectedValid: true);
        Assert.Equal(1, Volatile.Read(ref _defaultConflictFramePreparationCount));
    }

    /// <summary>
    /// Reconstructs a real signed snapshot reader from the supplied root rather than retaining prepared owner objects.
    /// </summary>
    /// <param name="context">
    /// Isolated fixture whose snapshot files and authority are read again under its own live lease.
    /// </param>
    /// <param name="expectedValid">
    /// Whether the baseline bytes must authenticate or the deliberately corrupted snapshot must be rejected.
    /// </param>
    /// <returns>
    /// A task completing after the actual reader's result matches the expected tamper outcome.
    /// </returns>
    private static async Task AssertPreparedConflictFrameAuthorityAsync(
        ResourceMaterializationTestContext context, bool expectedValid)
    {
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            [ResourceMaterializationContract.StatePath]);
        Assert.Equal(expectedValid, result.Success);
    }
}
