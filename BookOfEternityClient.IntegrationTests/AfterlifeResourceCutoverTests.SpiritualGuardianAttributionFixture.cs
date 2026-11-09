using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Only the two existing singleton pressure lanes; fixed before ordinary authoring or execution.</summary>
    private enum PressureSingletonJourneyRole { DeclaredGuardian, Player }

    /// <summary>One journey's signed origin and independently authored ordinary DATA, with no retained live capability.</summary>
    private sealed record GuardianAttributionJourney(string Name, PressureOrdinaryOwnerCopy Copy,
        PressureGroupSignedOrigin Origin, RetainedPressureGroupData Retained,
        IReadOnlyDictionary<string, byte[]?> Authored, int ExpectedRank = 3,
        PressureSingletonJourneyRole Role = PressureSingletonJourneyRole.DeclaredGuardian,
        PlayerSingletonPressureContour PlayerContour = PlayerSingletonPressureContour.BelowBroken,
        GuardianSingletonPressureContour GuardianContour = GuardianSingletonPressureContour.BelowBroken)
    {
        internal bool IsGuardian => Role switch
        {
            PressureSingletonJourneyRole.DeclaredGuardian when PlayerContour == PlayerSingletonPressureContour.BelowBroken &&
                GuardianContour is GuardianSingletonPressureContour.BelowBroken or GuardianSingletonPressureContour.ReachingBroken => true,
            PressureSingletonJourneyRole.Player when GuardianContour == GuardianSingletonPressureContour.BelowBroken &&
                PlayerContour is PlayerSingletonPressureContour.BelowBroken or PlayerSingletonPressureContour.ReachingBroken or PlayerSingletonPressureContour.CappedBroken => false,
            _ => throw new ArgumentOutOfRangeException(nameof(Role))
        };
        internal string AffectedSide => IsGuardian ? "opposition" : "player";
        internal string StrainField => AffectedSide + "SideStrain";
        internal int Version => IsGuardian ? 5 : 3;
        internal string ExchangeId => IsGuardian ? "exchange_guardian_attribution_46" : "exchange_pressure_group_46";
        internal int[] Dice => IsGuardian ? [12, 4] : [4, 12];
        internal string AffectedKind => IsGuardian ? "guardian" : "player_soul";
        internal string AffectedId => IsGuardian ? SpiritualGuardianAttributionTestData.GuardianId : "player_soul";
        internal string ActingKind => IsGuardian ? "player_soul" : "guardian";
        internal string ActingId => IsGuardian ? "player_soul" : SpiritualGuardianAttributionTestData.GuardianId;
        internal string OrdinaryStrain => IsGuardian ? SpiritualGuardianAttributionTestData.OrdinaryStrain(GuardianContour) : SpiritualSingletonIvPlayerDataTests.OrdinaryStrain(PlayerContour);
        internal string ActualStrain => IsGuardian ? SpiritualGuardianAttributionTestData.ActualStrain(GuardianContour) : SpiritualSingletonIvPlayerDataTests.ActualStrain(PlayerContour);
        internal SpiritualWoundCalculation OrdinaryCalculation => IsGuardian ? GuardianAttributionOrdinaryCalculation(GuardianContour) :
            SpiritualSingletonIvPlayerDataTests.OrdinaryCalculation(PlayerContour);
        internal SpiritualWoundCalculation ActualCalculation => IsGuardian ? GuardianAttributionActualCalculation(GuardianContour) :
            SpiritualSingletonIvPlayerDataTests.ActualCalculation(PlayerContour);
        internal JsonObject Ordinary => Copy.Ordinary;
        internal JsonObject Actual
        {
            get
            {
                var result = Ordinary.DeepClone().AsObject();
                result["after"]![StrainField] = ActualStrain;
                return result;
            }
        }
        internal JsonObject ActualRoot
        {
            get
            {
                var result = Copy.Conflict.DeepClone().AsObject();
                result["activeConflict"]!["exchangeLog"]![0] = Actual;
                result["activeConflict"]![StrainField] = ActualStrain;
                return result;
            }
        }
    }

    /// <summary>Independent per-Fact retained DATA is authored before signing; synthetic creation History is not a native birth.</summary>
    /// <param name="name">Independent journey identity for this invocation.</param>
    /// <param name="work">Scoped work after complete signed/current preflight and ordinary authoring.</param>
    /// <param name="hooks">Existing explicit test hooks, unchanged by rank parameterization.</param>
    /// <param name="retainedRank">III by default, or full IV fixed before graph, History and signature creation.</param>
    /// <param name="contour">Closed guardian destination pair fixed before ordinary authoring; beforeI is unchanged.</param>
    private async Task WithGuardianAttributionJourneyAsync(string name, Func<GuardianAttributionJourney, Task> work,
        FileSystemManagerHooks? hooks = null, int retainedRank = 3,
        GuardianSingletonPressureContour contour = GuardianSingletonPressureContour.BelowBroken)
    {
        var ordinaryStrain = SpiritualGuardianAttributionTestData.OrdinaryStrain(contour);
        ResourceMaterializationTestContext? context = null;
        var data = SpiritualGuardianAttributionTestData.CreateRetained(rank: retainedRank);
        await ExecutePressureGroupFixtureStagesAsync(new(
            Work: async () =>
            {
                var started = Stopwatch.GetTimestamp();
                context = await CreateCompleteConflictFrameContextAsync(hooks: hooks,
                    playerCurrent: 3m, oppositionCurrent: 3m, spiritFocusTier: 3, oppositionMaximum: 9m,
                    initializeContext: allocated => { context = allocated; return Task.CompletedTask; },
                    seedOriginalInputs: async fixture =>
                    {
                        await SeedOriginalIntakeBaselinesAsync(fixture);
                        var profiles = (await fixture.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
                        var guardian = ReadGuardianAttributionProfile(profiles);
                        guardian["activeWounds"] = new JsonArray(data.Parents.Select(row => row.DeepClone()).ToArray());
                        guardian["activeEffects"] = new JsonArray(data.Effects.Select(row => row.DeepClone()).ToArray());
                        var conflict = (await fixture.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
                        conflict["activeConflict"]!["oppositionSideStrain"] = "strained";
                        SpiritualGuardianAttributionDataCheck.Require(data, profiles, conflict, retainedRank);
                        foreach (var (path, image) in new[]
                        {
                            (AfterlifeEntityProfileState.StatePath, profiles), (AfterlifeSpiritualConflictState.StatePath, conflict),
                            (WoundIdentityState.StatePath, data.WoundIdentities), (WoundHistoryState.HistoryPath, data.History),
                            (EffectIdentityState.StatePath, data.EffectIdentities), (SpiritualWoundOpportunityReceiptState.StatePath, data.Receipts),
                            (EffectCarrierCatalog.PlayerPath, new JsonObject { ["schemaVersion"] = 1, ["activeEffects"] = new JsonArray() }),
                            (EffectCarrierCatalog.NpcPath, new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray() })
                        }) await fixture.WriteExactJsonAsync(path, image.ToJsonString());
                    },
                    captureOriginalSnapshot: fixture => fixture.CaptureValidatedPendingSnapshotAsync(turn: 46,
                        currentRealm: "Chaos Sea", preGeneratedDices1d20: [12, 4], sessionId: "session_" + name,
                        requestId: "request_" + name));
                WritePressureOwnerTiming("guardian_journey_signed_preparation", name, started);
                GuardianAttributionJourney? setup = null;
                FileSystemManager.CanonicalWriteLease? lease = null;
                await ExecutePressureGroupFixtureStagesAsync(new(
                    Work: async () =>
                    {
                        lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                        var signed = await ReadGuardianAttributionSignedInputsAsync(context, lease, name, data, retainedRank);
                        // This is the exact physical signed origin, before any ordinary/output authoring.
                        var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                        var tracked = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
                        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(new[]
                                 { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                                     AcceptedMechanicsPlan.WoundCommandPath, SpiritualWoundOpportunityReceiptState.StatePath,
                                     ProjectionNarrativePath, ProjectionInterfacePath }).Distinct(StringComparer.Ordinal))
                            tracked[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
                        var origin = new PressureGroupSignedOrigin(files, tracked,
                            File.ReadAllBytes(context.FileSystem.SessionGenerationPath),
                            ReadGuardianAttributionJson(tracked[SpiritualWoundOpportunityReceiptState.StatePath]!).ToJsonString(),
                            ReadGuardianAttributionJson(tracked[EffectIdentityState.StatePath]!).ToJsonString());
                        AssertPressureNoPrivatePair(context, lease);
                        var root = ReadGuardianAttributionJson(signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath));
                        var active = root["activeConflict"]!.AsObject();
                        var ordinary = SpiritualGuardianAttributionTestData.CreateExchange("exchange_guardian_attribution_46", 46,
                            "strained", ordinaryStrain, declaration: true);
                        var issues = new List<ValidationIssue>();
                        ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "guardian.journey.ordinary", issues);
                        ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "guardian.journey.ordinary", issues);
                        ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
                        var source = SpiritualGuardianAttributionTestData.CreateSource(ordinary, signed.SessionId, signed.RequestId,
                            signed.SnapshotToken, active["conflictId"]!.GetValue<string>(),
                            new CanonicalBeforeImage(true, signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath)).Fingerprint);
                        ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, source["turnEvidence"]!.AsObject(), issues);
                        RequireGuardianJourneyIssues(name, "ordinary_authored_shape_rules_dice", issues);
                        Assert.Equal(GuardianAttributionOrdinaryCalculation(contour), SpiritualWoundSourceWitness.Validate(source));
                        Assert.Empty(active["exchangeLog"]!.AsArray());
                        Assert.Equal(("clear", "strained"), (active["playerSideStrain"]!.GetValue<string>(), active["oppositionSideStrain"]!.GetValue<string>()));
                        active["exchangeLog"]!.AsArray().Add(ordinary.DeepClone());
                        active["oppositionSideStrain"] = ordinaryStrain;
                        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
                        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, raw);
                        await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath, Encoding.UTF8.GetBytes(new JsonObject
                        {
                            ["response"] = "Душа оказывает давление; Хранитель не справляется со своим встречным давлением. Противостояние продолжается.",
                            ["timestamp"] = "2026-11-07T00:00:00Z"
                        }.ToJsonString()));
                        RequireGuardianJourneyIssues(name, "ordinary_location_preflight", await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
                        RequireGuardianJourneyIssues(name, "ordinary_item_preflight", await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
                        var authored = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
                        foreach (var path in PressureAuthoredDraftPaths)
                            authored[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
                        Assert.Equal(raw, authored[AfterlifeSpiritualConflictState.StatePath]);
                        Assert.NotEqual(new CanonicalBeforeImage(true, raw).Fingerprint,
                            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(root.ToJsonString())).Fingerprint);
                        var copy = new PressureOrdinaryOwnerCopy(context,
                            Assert.IsType<string>(context.FileSystem.ReadExistingSessionGeneration(lease)), ordinary, root,
                            (await context.FileSystem.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath))!);
                        setup = new(name, copy, origin, data, authored, ExpectedRank: retainedRank, GuardianContour: contour);
                    },
                    HasWindow: () => false, HasCapture: () => false, HasLease: () => lease is not null, HasContext: () => false,
                    AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
                    AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => { },
                    AssertNoPrivatePair: () => AssertPressureNoPrivatePair(context, lease!),
                    DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
                await work(Assert.IsType<GuardianAttributionJourney>(setup));
            },
            HasWindow: () => false, HasCapture: () => false, HasLease: () => false, HasContext: () => context is not null,
            AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
            AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => { }, AssertNoPrivatePair: () => { },
            DisposeLease: () => ValueTask.CompletedTask,
            DisposeContext: async () =>
            {
                var started = Stopwatch.GetTimestamp();
                await context!.DisposeAsync();
                WritePressureOwnerTiming("guardian_journey_owned_cleanup", name, started);
            },
            AssertRootRemoved: () => Assert.False(Directory.Exists(context!.RootPath))));
    }

    /// <summary>Authenticates exact signed/current inputs, retained guardian graph and participant ownership before authoring.</summary>
    /// <param name="context">This invocation's genuine owned context.</param>
    /// <param name="lease">Its active canonical lease.</param>
    /// <param name="name">Expected request identity.</param>
    /// <param name="data">Independent pre-sign authored graph.</param>
    /// <param name="expectedRank">Independent expected III/IV, not inferred from current or signed DATA.</param>
    private async Task<PendingTurnSnapshotReadAuthority> ReadGuardianAttributionSignedInputsAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease, string name,
        RetainedPressureGroupData data, int expectedRank = 3)
    {
        var optional = new[] { WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath, WoundCarrierCatalog.EnemiesPath,
            WoundCarrierCatalog.AlliesPath, EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath };
        var required = PressureOrdinarySignedPaths.Append(ResourceMaterializationContract.DefinitionsPath)
            .Append(CanonicalResourceOwnerAuthorityComposer.AuthorityPath).ToArray();
        var read = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(required, optional));
        RequireGuardianJourneyIssues(name, "signed_input_read", read.Issues);
        Assert.True(read.Success, string.Join("\n", read.Issues));
        var signed = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
        Assert.Equal((46, "chaos_sea", "request_" + name), (signed.TurnNumber, signed.Realm, signed.RequestId));
        Assert.Equal(new[] { 12, 4 }, signed.AcceptedD20EventValues);
        foreach (var path in required.Concat(optional))
        {
            var bytes = await context.FileSystem.ReadFileBytesAsync(lease, path);
            if (bytes is null) Assert.Contains(path, signed.AbsentLogicalPaths);
            else Assert.Equal(bytes, signed.ReadRequiredBytes(path));
        }
        JsonObject Read(string path) => ReadGuardianAttributionJson(signed.ReadRequiredBytes(path));
        var signedData = new RetainedPressureGroupData(data.Parents, data.Effects, Read(WoundIdentityState.StatePath),
            Read(WoundHistoryState.HistoryPath), Read(EffectIdentityState.StatePath), Read(SpiritualWoundOpportunityReceiptState.StatePath));
        SpiritualGuardianAttributionDataCheck.Require(signedData, Read(AfterlifeEntityProfileState.StatePath), Read(AfterlifeSpiritualConflictState.StatePath), expectedRank);
        var conflict = Read(AfterlifeSpiritualConflictState.StatePath)["activeConflict"]!.AsObject();
        Assert.Equal(SpiritualGuardianAttributionTestData.GuardianId,
            conflict["oppositionSide"]!["leadContestant"]!["actorId"]!.GetValue<string>());
        var balance = AfterlifeConflictActionPointProjectionService.Resolve(
            SpiritualWoundStateJson.DecodeUtf8JsonText(signed.ReadRequiredBytes(ResourceMaterializationContract.DefinitionsPath)),
            SpiritualWoundStateJson.DecodeUtf8JsonText(signed.ReadRequiredBytes(ResourceMaterializationContract.StatePath)), conflict);
        RequireGuardianJourneyIssues(name, "signed_ap_projection", balance.Issues);
        Assert.Equal((3m, 10m, 3m, 9m), (balance.Projection!.Player.Current, balance.Projection.Player.Maximum,
            balance.Projection.Opposition.Current, balance.Projection.Opposition.Maximum));
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" }) Assert.Single(signedData.Receipts[field]!.AsArray());
        return signed;
    }

    /// <summary>Owns one exact journey lease through the accepted failure-preserving fixed cleanup stages.</summary>
    private static Task ExecuteGuardianLeaseStagesAsync(ResourceMaterializationTestContext context,
        Func<FileSystemManager.CanonicalWriteLease, Task> work)
    {
        FileSystemManager.CanonicalWriteLease? lease = null;
        return ExecutePressureGroupFixtureStagesAsync(new(
            Work: async () => { lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync(); await work(lease); },
            HasWindow: () => false, HasCapture: () => false, HasLease: () => lease is not null, HasContext: () => false,
            AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
            AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => { }, AssertNoPrivatePair: () => { },
            DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
    }

    /// <summary>Decodes original signed or authored JSON bytes without discarding their separately checked raw identity.</summary>
    private static JsonObject ReadGuardianAttributionJson(byte[] bytes) =>
        SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(bytes));

    /// <summary>Finds only the exact signed fixture guardian, never a player or an arbitrary guardian profile.</summary>
    private static JsonObject ReadGuardianAttributionProfile(JsonObject profiles) => Assert.Single(
        profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().OfType<JsonObject>(), row =>
            row["actorType"]!.GetValue<string>() == "guardian" &&
            row["actorId"]!.GetValue<string>() == SpiritualGuardianAttributionTestData.GuardianId);

    /// <summary>Independent ordinary pressure10/maxI or reaching-broken pressure15/maxII arithmetic.</summary>
    private static SpiritualWoundCalculation GuardianAttributionOrdinaryCalculation(
        GuardianSingletonPressureContour contour = GuardianSingletonPressureContour.BelowBroken) =>
        SpiritualGuardianAttributionTestData.OrdinaryCalculation(contour);

    /// <summary>Independent actual pressure15/maxII or reaching-broken pressure20/maxIII arithmetic after the sole guardian strain component.</summary>
    private static SpiritualWoundCalculation GuardianAttributionActualCalculation(
        GuardianSingletonPressureContour contour = GuardianSingletonPressureContour.BelowBroken) =>
        SpiritualGuardianAttributionTestData.ActualCalculation(contour);
}
