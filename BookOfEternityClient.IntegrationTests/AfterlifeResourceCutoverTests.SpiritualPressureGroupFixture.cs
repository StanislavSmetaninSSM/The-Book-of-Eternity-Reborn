using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>One Fact's independently authored retained DATA and literal ordinary expectations.</summary>
    private sealed record PressureGroupCase(string Name, int Parents = 2, string After = "fractured",
        int PlayerDie = 4, int PlayerBefore = 3, int PlayerCost = 3, int Resilience = 0,
        bool SelectedCost = false, bool OtherCost = false, bool PressureRoll = false, SpiritualPressureParentRanks? Ranks = null,
        bool AdvanceExpected = false, bool CompletePressureRollAudit = false, bool RankFourPressureArt = false,
        PairLowPressureContour? LowContour = null);

    /// <summary>Resolves the pre-sign canonical-ID position, retaining the old III third-parent and I cost-only controls.</summary>
    private static int PressureGroupParentRank(PressureGroupCase spec, int oneBasedIndex) =>
        oneBasedIndex > spec.Parents ? 1 : oneBasedIndex == 1 ? (spec.Ranks?.First ?? 3) :
            oneBasedIndex == 2 ? (spec.Ranks?.Second ?? 3) : 3;

    /// <summary>Per-Fact live owners and detached witnesses, never shared or cached across cases.</summary>
    private sealed record PressureGroupWindow(PressureGroupCase Spec, PressureOrdinaryOwnerCopy Copy,
        FileSystemManager.CanonicalWriteLease Lease, ValidationService.SpiritualOriginalTurnCapture Capture,
        IReadOnlyDictionary<string, byte[]> Files, byte[] Generation,
        ValidationService.SpiritualOrdinaryPressureProbeState Frontier)
    {
        /// <summary>Independent complete selected parent envelopes authored once before the signature.</summary>
        internal IReadOnlyList<string> ExpectedParentJson { get; init; } = Array.Empty<string>();
    }

    /// <summary>Creates complete independently signed DATA, real intake/resource owners and the closed original prefix.</summary>
    private async Task WithPressureGroupCaseAsync(PressureGroupCase spec, Func<PressureGroupWindow, Task> work)
    {
        ResourceMaterializationTestContext? context = null;
        var data = SpiritualPressureGroupOwnerCaseData.Create(spec.Parents, spec.SelectedCost, spec.OtherCost,
            spec.PressureRoll, spec.Ranks, rankFourPressureArt: spec.RankFourPressureArt);
        if (spec.CompletePressureRollAudit && (!spec.PressureRoll || spec.PlayerDie != 4))
            throw new ArgumentException("The complete pressure-roll audit requires the authored disadvantage and selected4.", nameof(spec));
        var expectedParents = Array.AsReadOnly(data.Parents.Take(spec.Parents)
            .OrderBy(parent => parent["woundId"]!.GetValue<string>(), StringComparer.Ordinal)
            .Select(parent => parent.ToJsonString()).ToArray());
        FileSystemManager.CanonicalWriteLease? lease = null;
        ValidationService.SpiritualOriginalTurnCapture? capture = null;
        PressureGroupWindow? window = null;
        IReadOnlyDictionary<string, byte[]>? disposalFiles = null;
        await ExecutePressureGroupFixtureStagesAsync(new(
            Work: async () =>
            {
                var started = Stopwatch.GetTimestamp();
                context = await CreatePressureGroupSignedContextAsync(spec, data, allocated => context = allocated);
                WritePressureOwnerTiming("group_signed_preparation", spec.Name, started);
                lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                var signed = await AssertPressureGroupSignedInputsAsync(context, lease, spec, data);
                var copy = await AuthorPressureGroupOrdinaryAsync(context, lease, spec, signed);
                capture = await BeginPressureOrdinaryOwnerAsync(copy, lease, spec.Name);
                var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, spec.Name + "_before");
                AssertPressureGroupFrontier(spec, copy, capture, lease, frontier);
                window = new(spec, copy, lease, capture, files, generation, frontier)
                    { ExpectedParentJson = expectedParents };
                await work(window);
            },
            HasWindow: () => window is not null,
            HasCapture: () => capture is not null,
            HasLease: () => lease is not null,
            HasContext: () => context is not null,
            AssertWindowInert: async () =>
            {
                // Preserve original current-owner eligibility without masking the test body's failure.
                if (capture!.IsCurrentOwner && !spec.AdvanceExpected)
                    await AssertPressureGroupWindowInertAsync(window!);
            },
            CaptureDisposalFiles: () => disposalFiles = CaptureExactSpiritualFiles(context!.FileSystem.GameSessionPath),
            DisposeCaptureExplicitly: () => capture!.Dispose(),
            AssertDisposedCaptureInert: () =>
            {
                if (disposalFiles is not null)
                    AssertExistingPressureInputInert(context!, lease!, disposalFiles, window!.Generation);
            },
            DisposeCaptureImplicitly: () => capture!.Dispose(),
            AssertNoPrivatePair: () => AssertPressureNoPrivatePair(context!, lease!),
            DisposeLease: async () => await lease!.DisposeAsync(),
            DisposeContext: async () =>
            {
                var cleanup = Stopwatch.GetTimestamp();
                await context!.DisposeAsync();
                WritePressureOwnerTiming("group_owned_cleanup", spec.Name, cleanup);
            },
            AssertRootRemoved: () => Assert.False(Directory.Exists(context!.RootPath))));
    }

    /// <summary>
    /// Fixed callbacks for this fixture's original ownership stages; no callback grants a game capability.
    /// </summary>
    /// <param name="Work">Original allocation, preparation and test body.</param>
    /// <param name="HasWindow">Whether complete preflight reached the original inner try.</param>
    /// <param name="HasCapture">Whether the capture using declaration acquired its owner.</param>
    /// <param name="HasLease">Whether the lease using declaration acquired its owner.</param>
    /// <param name="HasContext">Whether allocation retained context cleanup ownership.</param>
    /// <param name="AssertWindowInert">Original eligible pre-disposal assertion.</param>
    /// <param name="CaptureDisposalFiles">Original physical disposal before-image capture.</param>
    /// <param name="DisposeCaptureExplicitly">Original explicit capture disposal.</param>
    /// <param name="AssertDisposedCaptureInert">Original post-disposal physical assertion.</param>
    /// <param name="DisposeCaptureImplicitly">Original using-finally capture disposal, including its second success-path call.</param>
    /// <param name="AssertNoPrivatePair">Original assertion after successful capture scope exit.</param>
    /// <param name="DisposeLease">Original await-using lease disposal.</param>
    /// <param name="DisposeContext">Original context disposal and timing output.</param>
    /// <param name="AssertRootRemoved">Original root-absence assertion after successful context disposal.</param>
    private sealed record PressureGroupFixtureStages(
        Func<Task> Work, Func<bool> HasWindow, Func<bool> HasCapture,
        Func<bool> HasLease, Func<bool> HasContext,
        Func<Task> AssertWindowInert, Action CaptureDisposalFiles,
        Action DisposeCaptureExplicitly, Action AssertDisposedCaptureInert,
        Action DisposeCaptureImplicitly, Action AssertNoPrivatePair,
        Func<ValueTask> DisposeLease, Func<Task> DisposeContext, Action AssertRootRemoved);

    /// <summary>
    /// Attempts every retained owner's eligible cleanup stage in original order while preserving the first failure.
    /// </summary>
    /// <param name="stages">This fixture's fixed work and eligible ownership callbacks.</param>
    /// <returns>Completion, or the first failure with its original stack and ordered secondary failures.</returns>
    private static async Task ExecutePressureGroupFixtureStagesAsync(PressureGroupFixtureStages stages)
    {
        Exception? primary = null;
        try { await stages.Work(); }
        catch (Exception error) { primary = error; }
        if (stages.HasWindow())
        {
            await AttemptAsync("window", stages.AssertWindowInert);
            Attempt("disposal_files", stages.CaptureDisposalFiles);
            Attempt("capture_explicit", stages.DisposeCaptureExplicitly);
            // The callback remains ineligible when its disposal before-image was not captured.
            Attempt("disposed_inert", stages.AssertDisposedCaptureInert);
        }
        if (stages.HasCapture()) Attempt("capture_implicit", stages.DisposeCaptureImplicitly);
        if (stages.HasLease())
        {
            Attempt("no_private_pair", stages.AssertNoPrivatePair);
            await AttemptAsync("lease", async () => await stages.DisposeLease());
        }
        if (stages.HasContext())
        {
            await AttemptAsync("context", stages.DisposeContext);
            Attempt("root", stages.AssertRootRemoved);
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();

        // These callbacks are only the fixed stages of this private fixture, not a general cleanup framework.
        void Attempt(string stage, Action action)
        {
            try { action(); }
            catch (Exception error) { primary = RetainPressureFixtureFailure(primary, stage, error); }
        }
        async Task AttemptAsync(string stage, Func<Task> action)
        {
            try { await action(); }
            catch (Exception error) { primary = RetainPressureFixtureFailure(primary, stage, error); }
        }
    }

    /// <summary>Keeps the first exact exception and appends a secondary stage error without overwriting existing metadata.</summary>
    /// <param name="primary">The first failure, if any.</param>
    /// <param name="stage">The fixed fixture cleanup stage that failed.</param>
    /// <param name="error">The exact new exception object.</param>
    /// <returns>The unchanged first failure, retaining ordered secondary errors in its Data collection.</returns>
    private static Exception RetainPressureFixtureFailure(Exception? primary, string stage, Exception error)
    {
        if (primary is null) return error;
        var index = 0;
        string key;
        do { key = "PressureGroup-" + stage + "-" + index++; }
        while (primary.Data.Contains(key));
        primary.Data.Add(key, error);
        return primary;
    }

    /// <summary>Allocates one independently owned signed context; the callback retains cleanup ownership before seeding can fail.</summary>
    private async Task<ResourceMaterializationTestContext> CreatePressureGroupSignedContextAsync(
        PressureGroupCase spec, RetainedPressureGroupData data,
        Action<ResourceMaterializationTestContext> retainAllocated, FileSystemManagerHooks? hooks = null)
    {
        _ = ExpectedPressureGroupOrdinary(spec);
        if (spec.LowContour is not null) _ = ExpectedPressurePairActual(spec);
        return await CreateCompleteConflictFrameContextAsync(hooks: hooks, playerCurrent: spec.PlayerBefore,
            oppositionCurrent: 3m, spiritFocusTier: 3, oppositionMaximum: 9m,
            initializeContext: allocated => { retainAllocated(allocated); return Task.CompletedTask; },
            seedOriginalInputs: async fixture =>
            {
                await SeedOriginalIntakeBaselinesAsync(fixture);
                await SeedRetainedPressureGroupDataAsync(fixture, data);
                if (spec.Resilience != 0)
                {
                    var soul = (await fixture.ReadJsonAsync(ValidationService.SpiritualWoundSourceSession.SoulPath))!.AsObject();
                    soul["afterlifeCombatProfile"]!["artTiers"]!["spiritual_resilience"] = spec.Resilience;
                    await fixture.WriteExactJsonAsync(ValidationService.SpiritualWoundSourceSession.SoulPath, soul.ToJsonString());
                    var profiles = (await fixture.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
                    var player = Assert.Single(profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row =>
                        row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
                    player["standardArts"]!["spiritual_resilience"] = spec.Resilience;
                    await fixture.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
                }
            },
            captureOriginalSnapshot: fixture => fixture.CaptureValidatedPendingSnapshotAsync(turn: 46,
                currentRealm: "Chaos Sea", preGeneratedDices1d20: spec.CompletePressureRollAudit ? [4, 12, 8] : [spec.PlayerDie, 12],
                sessionId: "session_" + spec.Name, requestId: "request_" + spec.Name));
    }

    /// <summary>Authors the ordinary full root only after signed/current preflight, without retaining a lease or starting execution.</summary>
    private static async Task<PressureOrdinaryOwnerCopy> AuthorPressureGroupOrdinaryAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease,
        PressureGroupCase spec, PendingTurnSnapshotReadAuthority signed)
    {
        var root = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)!.AsObject();
        var active = root["activeConflict"]!.AsObject();
        Assert.Empty(active["exchangeLog"]!.AsArray());
        Assert.Equal("clear", active["playerSideStrain"]!.GetValue<string>());
        var ordinary = SpiritualPressureGroupOwnerCaseData.Ordinary(spec.After, spec.PlayerDie, spec.PlayerBefore, spec.PlayerCost);
        if (spec.CompletePressureRollAudit)
        {
            var dice = ordinary["diceAudit"]!.AsObject();
            dice["diceUsed"]![0]!["selection"] = "selected";
            dice["diceUsed"]!.AsArray().Add(new JsonObject
            {
                ["side"] = "player", ["sourceIndex"] = 2, ["sides"] = 20,
                ["value"] = 8, ["selection"] = "discarded"
            });
            dice["rollMode"] = new JsonObject
            {
                ["player"] = new JsonObject
                {
                    ["effectiveMode"] = "disadvantage", ["advantageSources"] = new JsonArray(),
                    ["disadvantageSources"] = new JsonArray("Надлом второй раны мешает давлению души.")
                }
            };
        }
        AssertPressureGroupOrdinaryInput(spec, ordinary, active, signed);
        active["exchangeLog"]!.AsArray().Add(ordinary.DeepClone());
        active["playerSideStrain"] = spec.After;
        var raw = spec.LowContour is null ? Encoding.UTF8.GetBytes(root.ToJsonString()) :
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, raw);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath, Encoding.UTF8.GetBytes(new JsonObject
        {
            ["response"] = "Душа встречает давление Хранителя. Противостояние продолжается.",
            ["timestamp"] = "2026-11-05T00:00:00Z"
        }.ToJsonString()));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
        var definitions = (await context.FileSystem.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath))!;
        return new PressureOrdinaryOwnerCopy(context,
            Assert.IsType<string>(context.FileSystem.ReadExistingSessionGeneration(lease)), ordinary, root, definitions);
    }

    /// <summary>Authenticates all signed bytes and complete creation joins, without imposing the old fixed III-only fixture oracle.</summary>
    private static async Task<PendingTurnSnapshotReadAuthority> AssertPressureGroupSignedInputsAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease,
        PressureGroupCase spec, RetainedPressureGroupData data)
    {
        var optional = new[] { WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath,
            WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath,
            EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath };
        var required = PressureOrdinarySignedPaths.Append(ResourceMaterializationContract.DefinitionsPath)
            .Append(CanonicalResourceOwnerAuthorityComposer.AuthorityPath).ToArray();
        var read = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(required, optional));
        Assert.True(read.Success, string.Join("\n", read.Issues));
        var signed = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
        Assert.Equal((46, "chaos_sea", "request_" + spec.Name), (signed.TurnNumber, signed.Realm, signed.RequestId));
        Assert.Equal(spec.CompletePressureRollAudit ? new[] { 4, 12, 8 } : [spec.PlayerDie, 12], signed.AcceptedD20EventValues);
        foreach (var path in required.Concat(optional))
        {
            var bytes = await context.FileSystem.ReadFileBytesAsync(lease, path);
            if (bytes is null) Assert.Contains(path, signed.AbsentLogicalPaths);
            else Assert.Equal(bytes, signed.ReadRequiredBytes(path));
        }
        Assert.Equal(spec.Resilience, JsonNode.Parse(signed.ReadRequiredBytes(ValidationService.SpiritualWoundSourceSession.SoulPath))!
            ["afterlifeCombatProfile"]!["artTiers"]!["spiritual_resilience"]!.GetValue<int>());
        var signedPlayerProfile = Assert.Single(JsonNode.Parse(signed.ReadRequiredBytes(AfterlifeEntityProfileState.StatePath))!
            [AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row => row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
        Assert.Equal(spec.Resilience, signedPlayerProfile["standardArts"]!["spiritual_resilience"]!.GetValue<int>());
        var manifest = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!)!;
        if (spec.CompletePressureRollAudit)
        {
            Assert.Equal(new[] { 4, 12, 8 }, manifest["preGeneratedDices1d20"]!.AsArray().Select(row => row!.GetValue<int>()));
            var request = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease,
                LiveTurnPreparationService.TurnRequestPath))!)!;
            Assert.Equal((46, "request_" + spec.Name),
                (request["turnNumber"]!.GetValue<int>(), request["requestId"]!.GetValue<string>()));
            Assert.Equal(new[] { 4, 12, 8 }, request["preGeneratedDices1d20"]!.AsArray().Select(row => row!.GetValue<int>()));
        }
        foreach (var pair in manifest["files"]!.AsObject())
            Assert.Equal(await context.FileSystem.ReadFileBytesAsync(lease, pair.Key),
                await context.FileSystem.ReadFileBytesAsync(lease, pair.Value!.GetValue<string>()));
        var parsed = ReadRetainedPressureParentData(signed, data.Parents.Count);
        Assert.Equal(spec.Parents + (spec.OtherCost ? 1 : 0), parsed.Catalog.Occurrences.Count);
        Assert.Equal(parsed.Catalog.Occurrences.Count,
            parsed.Catalog.Occurrences.Select(row => row.WoundId).Distinct(StringComparer.Ordinal).Count());
        Assert.False(parsed.Receipts.HasRetainedPressureComparisons);
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            Assert.Equal(data.Parents.Count, parsed.ReceiptRoot[field]!.AsArray().Count);
        foreach (var occurrence in parsed.Catalog.Occurrences)
        {
            var wound = occurrence.Wound;
            var expectedIndex = int.Parse(wound.WoundId["wound_retained_pressure_owner_".Length..],
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(expectedIndex, 1, spec.Parents + (spec.OtherCost ? 1 : 0));
            var expectedRank = PressureGroupParentRank(spec, expectedIndex);
            Assert.Equal(("active", "spiritual", expectedRank), (wound.Lifecycle, wound.Classification.Domain, wound.Severity.Rank));
            Assert.True(wound.Origin.CreatedAtTurn < 46);
            var expected = Assert.Single(data.Parents, row => row["woundId"]!.GetValue<string>() == wound.WoundId);
            Assert.Equal(("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath),
                (occurrence.Coordinate.Realm, occurrence.Coordinate.OwnerKind, occurrence.Coordinate.OwnerId, occurrence.Coordinate.CarrierPath));
            Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))));
            var creation = Assert.Single(parsed.History.ResolveWoundTransitions(wound.WoundId));
            Assert.Equal((1, "create", wound.Origin.EventRef, wound.Origin.CreatedAtTurn),
                (creation.WoundTransitionOrdinal, creation.Kind, creation.EventRef, creation.Turn));
            Assert.Equal(WoundHistoryState.ComputeNonexistentBeforeFingerprint(wound.WoundId), creation.BeforeFingerprint);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(wound), creation.AfterFingerprint);
            Assert.True(parsed.Receipts.CoversOriginalCreation(wound.WoundId, wound.Origin.OpportunityId, creation));
            var decision = Assert.Single(parsed.ReceiptRoot["decisions"]!.AsArray(), row => row!["woundId"]!.GetValue<string>() == wound.WoundId)!;
            var source = decision["sourceWitness"]!.AsObject();
            var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var strain);
            Assert.Null(strain);
            Assert.Equal(wound.Severity.Rank, calculation.MaximumSeverityRank);
            Assert.Equal((0, expectedRank, expectedRank == 4 ? 25L : expectedRank == 3 ? 20L : 10L),
                (calculation.PreviousStrainRank, calculation.NewStrainRank, calculation.TraumaPressure));
            Assert.Equal(wound.Severity.Rank, decision["selectedSeverityRank"]!.GetValue<int>());
            Assert.Equal(source["sourceFingerprint"]!.GetValue<string>(), creation.SourceFingerprint);
            var originalSource = Assert.Single(parsed.ReceiptRoot["sources"]!.AsArray(), row =>
                row!["witness"]!["sourceId"]!.GetValue<string>() == source["sourceId"]!.GetValue<string>())!;
            Assert.True(JsonNode.DeepEquals(source, originalSource["witness"]));
            var recorded = parsed.Receipts.FindRecordedConflictSideWound(decision["instanceId"]!.GetValue<string>(), "player");
            Assert.Empty(recorded.Issues);
            Assert.Equal((wound.WoundId, creation.TransitionId), (recorded.Wound!.WoundId, recorded.Wound.CreateTransitionId));
        }
        await AssertPressureGroupGraphsAsync(context, lease, spec, parsed);
        return signed;
    }

    /// <summary>Requires every authored source/root/component and the literal full applicable/nonmatching contribution inventory.</summary>
    private static async Task AssertPressureGroupGraphsAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, PressureGroupCase spec, RetainedPressureParentData parents)
    {
        var loaded = await EffectMechanicsSnapshot.LoadWithDataAsync(context.FileSystem, lease);
        Assert.True(loaded.Snapshot.IsAccepted, string.Join("\n", loaded.Snapshot.Issues));
        var identities = Assert.IsType<EffectIdentityState>(loaded.Identities);
        var conflict = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)!.AsObject();
        var sources = EffectAcceptedTurnInputComposer.BuildCanonicalWoundSourceAuthority(parents.Catalog);
        Assert.Empty(sources.Issues);
        var targets = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(
            new(null, null, parents.Carriers.EnemyCombatants, parents.Carriers.AllyCombatants, parents.Profiles, conflict),
            new Dictionary<string, JsonNode?>());
        Assert.Empty(targets.Issues);
        var projected = SpiritualWoundConflictContributionProjector.Project(loaded.Snapshot, sources, targets, conflict);
        Assert.True(projected.IsAccepted, string.Join("\n", projected.Issues));
        var rankFourCount = Enumerable.Range(1, spec.Parents).Count(index => PressureGroupParentRank(spec, index) == 4);
        Assert.Equal(3 * spec.Parents + rankFourCount + (spec.OtherCost ? 1 : 0), projected.Contributions.Count);
        Assert.Equal(spec.Parents, projected.Contributions.Count(row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure"));
        Assert.Equal(spec.PressureRoll ? 1 : 0, projected.Contributions.Count(row => row.Profile == "spiritual_roll_hindrance" && row.Operation == "pressure"));
        Assert.Equal(spec.Parents - (spec.PressureRoll ? 1 : 0), projected.Contributions.Count(row => row.Profile == "spiritual_roll_hindrance" && row.Operation == "counter"));
        Assert.Equal(spec.Parents - (spec.SelectedCost ? 1 : 0), projected.Contributions.Count(row => row.Profile == "spiritual_action_cost_burden" && row.Operation == "guard"));
        Assert.Equal((spec.SelectedCost ? 1 : 0) + (spec.OtherCost ? 1 : 0), projected.Contributions.Count(row =>
            row.Profile == "spiritual_action_cost_burden" && row.Operation == "pressure"));
        Assert.Equal(rankFourCount - (spec.RankFourPressureArt ? 1 : 0), projected.Contributions.Count(row =>
            row.Profile == "spiritual_action_cost_burden" && row.Operation == "counter"));
        Assert.Equal(spec.RankFourPressureArt ? 1 : 0, projected.Contributions.Count(row =>
            row.Profile == "spiritual_art_restriction" && row.Operation == "pressure" && row.Magnitude.GetString() == "forbid"));
        var selectedCost = PressureGroupParentRank(spec, 2) == 4 ? 3 : 2;
        Assert.Equal((spec.SelectedCost ? selectedCost : 0) + (spec.OtherCost ? 1 : 0), projected.Contributions.Where(row =>
            row.Profile == "spiritual_action_cost_burden" && row.Operation == "pressure").Sum(row => row.Magnitude.GetInt32()));
        if (spec.SelectedCost)
        {
            var burden = Assert.Single(projected.Contributions, row => row.WoundSource.SourceId == "wound_retained_pressure_owner_2" &&
                row.Profile == "spiritual_action_cost_burden" && row.Operation == "pressure");
            Assert.Equal(selectedCost, burden.Magnitude.GetInt32());
            var strain = Assert.Single(projected.Contributions, row => row.WoundSource.SourceId == "wound_retained_pressure_owner_2" &&
                row.Profile == "spiritual_strain_burden");
            Assert.Equal(strain.EffectId, burden.EffectId);
            Assert.NotEqual(strain.ComponentId, burden.ComponentId);
        }
        if (spec.OtherCost)
        {
            var burden = Assert.Single(projected.Contributions, row => row.WoundSource.SourceId ==
                "wound_retained_pressure_owner_" + (spec.Parents + 1) && row.Profile == "spiritual_action_cost_burden" && row.Operation == "pressure");
            Assert.Equal(1, burden.Magnitude.GetInt32());
        }
        if (spec.PressureRoll)
        {
            var roll = Assert.Single(projected.Contributions, row => row.WoundSource.SourceId == "wound_retained_pressure_owner_2" &&
                row.Profile == "spiritual_roll_hindrance" && row.Operation == "pressure");
            var strain = Assert.Single(projected.Contributions, row => row.WoundSource.SourceId == "wound_retained_pressure_owner_2" &&
                row.Profile == "spiritual_strain_burden");
            Assert.Equal("disadvantage", roll.Magnitude.GetString());
            Assert.Equal(strain.EffectId, roll.EffectId);
            Assert.NotEqual(strain.ComponentId, roll.ComponentId);
        }
        foreach (var index in Enumerable.Range(1, spec.Parents).Where(index => PressureGroupParentRank(spec, index) == 4))
        {
            var rows = projected.Contributions.Where(row => row.WoundSource.SourceId == "wound_retained_pressure_owner_" + index).ToArray();
            Assert.Equal(4, rows.Length);
            Assert.Equal(4, rows.Select(row => row.ComponentId).Distinct(StringComparer.Ordinal).Count());
            var strain = Assert.Single(rows, row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure");
            var roll = Assert.Single(rows, row => row.Profile == "spiritual_roll_hindrance" &&
                row.Operation == (spec.PressureRoll && index == 2 ? "pressure" : "counter"));
            var guard = Assert.Single(rows, row => row.Profile == "spiritual_action_cost_burden" &&
                row.Operation == (spec.SelectedCost && index == 2 ? "pressure" : "guard"));
            var fourth = Assert.Single(rows, row => row.ComponentId == "component_004");
            Assert.Equal(("component_001", 1), (strain.ComponentId, strain.Magnitude.GetInt32()));
            Assert.Equal(("component_002", "disadvantage"), (roll.ComponentId, roll.Magnitude.GetString()));
            Assert.Equal(("component_003", 3), (guard.ComponentId, guard.Magnitude.GetInt32()));
            if (spec.RankFourPressureArt && index == 2)
                Assert.Equal(("spiritual_art_restriction", "pressure", "forbid"),
                    (fourth.Profile, fourth.Operation, fourth.Magnitude.GetString()));
            else
                Assert.Equal(("spiritual_action_cost_burden", "counter", 3),
                    (fourth.Profile, fourth.Operation, fourth.Magnitude.GetInt32()));
            Assert.Equal(strain.EffectId, guard.EffectId);
            Assert.Equal(strain.EffectId, fourth.EffectId);
        }
        foreach (var occurrence in parents.Catalog.Occurrences)
        {
            var wound = occurrence.Wound;
            var lineage = WoundEffectLineagePlanner.Plan(wound, identities);
            Assert.True(lineage.Success, string.Join("\n", lineage.Issues));
            var root = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
            var definition = JsonNode.Parse(Assert.Single(wound.Consequences.OwnedEffectSources.Definitions).GetRawText())!.AsObject();
            Assert.True(loaded.Catalog.TryResolveOne(root.EffectId, out var effect));
            Assert.Equal(("active", 1, 1), (effect.Effect["state"]!.GetValue<string>(),
                effect.Effect["stacking"]!["currentStacks"]!.GetValue<int>(), effect.Effect["stacking"]!["maxStacks"]!.GetValue<int>()));
            Assert.True(WoundEffectCarrierAdapter.TryCreateTargetKey(wound.Owner, out var target));
            Assert.True(WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(wound.Owner, target, definition, out var carrier));
            Assert.True(WoundAcceptedTurnPlannerCore.PriorRootHasExactGenerationAuthority(root.EffectId,
                new(wound.Owner.Realm, "wound", wound.WoundId, root.DefinitionKey), target, carrier, definition, loaded.Catalog, identities));
            var rows = projected.Contributions.Where(row => row.WoundSource.SourceId == wound.WoundId).ToArray();
            Assert.Equal(wound.Severity.Rank == 1 ? 1 : wound.Severity.Rank == 4 ? 4 : 3, rows.Length);
            Assert.All(rows, row => Assert.Equal(("chaos_sea", "player", "player_soul", "player", 1, root.EffectId),
                (row.Actor.Realm, row.Actor.Kind, row.Actor.TargetId, row.ResolvedSide, row.CurrentStacks, row.EffectId)));
        }
    }

    /// <summary>Validates independent complete shape/dice/rules/source DATA before real owner evaluation.</summary>
    private static void AssertPressureGroupOrdinaryInput(PressureGroupCase spec, JsonObject ordinary,
        JsonObject active, PendingTurnSnapshotReadAuthority signed)
    {
        var source = SpiritualRetainedPressureGroupTestData.CreateSource(ordinary, signed.SessionId, signed.RequestId,
            signed.SnapshotToken, active["conflictId"]!.GetValue<string>(),
            new CanonicalBeforeImage(true, signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath)).Fingerprint);
        var expected = ExpectedPressureGroupOrdinary(spec);
        if (spec.CompletePressureRollAudit)
        {
            var evidence = source["turnEvidence"]!.AsObject();
            evidence["acceptedD20Values"] = new JsonArray(4, 12, 8);
            evidence["bounds"]!["diceCount"] = 3;
            source["selectedDiceIndices"] = new JsonArray(0, 1, 2);
            var claim = new JsonObject
            {
                ["sourceIndex"] = 2, ["value"] = 8, ["exchangeOrdinal"] = 0, ["claimFingerprint"] = ""
            };
            claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim);
            evidence["diceClaims"]!.AsArray().Add(claim);
        }
        source["targetResilienceTier"] = spec.Resilience;
        source["maximumSeverityRank"] = expected.MaximumSeverityRank;
        SpiritualExistingPressureTestData.Seal(source);
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "fixture.ordinary", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "fixture.ordinary", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, source["turnEvidence"]!.AsObject(), issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
        AssertNoConflictFrameErrors(issues);
        Assert.Equal(signed.AcceptedD20EventValues, source["turnEvidence"]!["acceptedD20Values"]!.AsArray().Select(row => row!.GetValue<int>()));
        Assert.Equal(expected, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var strain));
        Assert.Null(strain);
        Assert.Equal((3, 1, 0), (ordinary["actionCostAudit"]!["player"]!["baseCost"]!.GetValue<int>(),
            ordinary["actionCostAudit"]!["player"]!["minCost"]!.GetValue<int>(), ordinary["actionCostAudit"]!["player"]!["artTier"]!.GetValue<int>()));
    }

    /// <summary>Checks every closed-prefix inventory field and the independently authored resource balance.</summary>
    private static void AssertPressureGroupFrontier(PressureGroupCase spec, PressureOrdinaryOwnerCopy copy,
        ValidationService.SpiritualOriginalTurnCapture capture, FileSystemManager.CanonicalWriteLease lease,
        ValidationService.SpiritualOrdinaryPressureProbeState state)
    {
        var source = JsonNode.Parse(state.SourceStateJson)!;
        foreach (var field in new[] { "sources", "checkedExchanges", "claimedDice", "pendingRequirements" }) Assert.Empty(source[field]!.AsArray());
        Assert.IsType<JsonObject>(source["frontiers"]); Assert.IsType<JsonObject>(source["checkedByCoordinate"]);
        Assert.Equal(0, state.NextOrdinal);
        Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
        Assert.Equal(capture.ReadAllocationCursor(lease), state.AllocationCursor);
        Assert.Equal(state.AllocationCursor, JsonNode.Parse(state.AllocationJournalJson)!.AsArray().Count);
        Assert.True(JsonNode.DeepEquals(capture.ReadAllocationJournal(lease), JsonNode.Parse(state.AllocationJournalJson)));
        var resources = AfterlifeConflictActionPointProjectionService.Resolve(copy.ResourceDefinitionsJson,
            state.ResourceStateJson, copy.Conflict["activeConflict"]!.AsObject());
        AssertNoConflictFrameErrors(resources.Issues);
        Assert.Equal(((decimal)spec.PlayerBefore, 10m, 3m, 9m), (resources.Projection!.Player.Current,
            resources.Projection.Player.Maximum, resources.Projection.Opposition.Current, resources.Projection.Opposition.Maximum));
    }

    /// <summary>Requires complete detached ordinary DATA and unpaid exact resource transitions, with no actual semantics.</summary>
    private static void AssertPressureGroupComparison(PressureGroupWindow window,
        ValidationService.SpiritualOrdinaryPressureComparisonResult result)
    {
        AssertNoConflictFrameErrors(result.Issues); Assert.False(result.PrefixBlocked);
        var comparison = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(result.Comparison);
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.ExchangePointer);
        Assert.True(JsonNode.DeepEquals(window.Copy.Ordinary, comparison.ReadOrdinaryExchange()));
        Assert.True(JsonNode.DeepEquals(window.Copy.Ordinary, JsonNode.Parse(comparison.OrdinaryExchangeJson)));
        var bytes = window.Files.Single(row => row.Key.Replace('\\', '/') == AfterlifeSpiritualConflictState.StatePath).Value;
        Assert.Equal(new CanonicalBeforeImage(true, bytes).Fingerprint, comparison.RawInputFingerprint);
        var spec = window.Spec;
        var expected = ExpectedPressureGroupOrdinary(spec);
        Assert.Equal(expected, comparison.OrdinaryCalculation);
        Assert.Equal(2, comparison.ExpectedTransitions.Count);
        foreach (var side in new[] { ResourceOwnerKind.AfterlifeActor, ResourceOwnerKind.AfterlifeConflictSide })
        {
            var transition = Assert.Single(comparison.ExpectedTransitions, row => row.Coordinate.OwnerKind == side);
            var player = side == ResourceOwnerKind.AfterlifeActor;
            var cost = player ? spec.PlayerCost : 3;
            var before = player ? spec.PlayerBefore : 3;
            var ownerId = player ? "player_soul" : window.Copy.Conflict["activeConflict"]!
                [AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!["opposition"]!["resourceOwnerId"]!.GetValue<string>();
            Assert.Equal(new ResourceCoordinate("chaos_sea", side, ownerId, "spiritual_action_points"), transition.Coordinate);
            Assert.Equal(ResourceTransitionOperation.Spend, transition.Operation);
            Assert.Equal(((decimal)before, (decimal)(before - cost), (decimal)cost, 0m),
                (transition.Before, transition.After, transition.Amount, transition.RecoveryPayment));
            Assert.Equal("exchange_pressure_group_46", transition.SourceId);
        }
    }

    /// <summary>Requires full unchanged live frontier, physical bytes/absence and generation after a closed probe.</summary>
    private async Task AssertPressureGroupWindowInertAsync(PressureGroupWindow window)
    {
        var after = await ReadPressureOwnerSnapshotAsync(window.Capture, window.Lease, window.Spec.Name + "_after");
        AssertPressureOrdinaryFrontiersEqual(window.Frontier, after);
        AssertPressureGroupFrontier(window.Spec, window.Copy, window.Capture, window.Lease, after);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
    }

    /// <summary>Checks complete probe inertness even when a desired-success result assertion will be RED.</summary>
    private async Task<ValidationService.SpiritualOrdinaryPressureComparisonResult> ReadPressureGroupComparisonAsync(
        PressureGroupWindow window)
    {
        return await ExecutePressureGroupComparisonStagesAsync(
            () => window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease),
            () => window.Capture.IsCurrentOwner,
            () => AssertPressureGroupWindowInertAsync(window));
    }

    /// <summary>Retains the comparison failure when its eligible closing assertion also fails.</summary>
    /// <param name="query">The actual typed comparison operation.</param>
    /// <param name="isCurrentOwner">Original current-owner condition for the closing assertion.</param>
    /// <param name="assertWindowInert">Original closing assertion.</param>
    /// <returns>The comparison result if both query and eligible closing assertion succeed.</returns>
    private static async Task<ValidationService.SpiritualOrdinaryPressureComparisonResult> ExecutePressureGroupComparisonStagesAsync(
        Func<Task<ValidationService.SpiritualOrdinaryPressureComparisonResult>> query,
        Func<bool> isCurrentOwner, Func<Task> assertWindowInert)
    {
        Exception? primary = null;
        ValidationService.SpiritualOrdinaryPressureComparisonResult? result = null;
        try { result = await query(); }
        catch (Exception error) { primary = error; }
        if (isCurrentOwner())
        {
            try { await assertWindowInert(); }
            catch (Exception error) { primary = RetainPressureFixtureFailure(primary, "comparison_window", error); }
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        return result!;
    }

}
