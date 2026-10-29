using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    [Fact]
    public Task ActiveAidRequiresTheCommittedGrantAndRetainsOneAttemptThroughPublicationAndReplay() => RunActiveAidAsync(receiveAid: false);

    [Fact]
    public Task ConfirmedGuardianOfferUsesGuardianArtAndPlayerNaturalTierThroughPublicationAndReplay() => RunActiveAidAsync(receiveAid: true);

    [Fact]
    public Task ResidentAidUsesTypedCommittedConsentOriginalNormalizationAndHistoricalReplay() => RunActiveAidAsync(receiveAid: false, resident: true);

    private async Task RunActiveAidAsync(bool receiveAid, bool resident = false)
    {
        var participantId = resident ? AidResidentId : AidGuardianId;
        var family = resident ? 5 : receiveAid ? 4 : 3;
        await using var fixture = await HealingFixture.CreateAsync(rank: 3, tier: receiveAid ? 0 : 3, die: 14, stageRequest: false,
            beforeResourceBootstrap: resident ? ExplorerWoundSelfTreatmentTests.SeedEngineAidResidentAsync : receiveAid ? ExplorerWoundSelfTreatmentTests.SeedEngineReceivingGuardianAsync
                : ExplorerWoundSelfTreatmentTests.SeedEngineAidGuardianAsync);
        var selection = await CommitAidGrantAsync(fixture, receiveAid, resident);
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(1, original.Recovery.CurrentStepProgress); // Actual grant-turn natural tick changed the selected FP.
        var files = fixture.Files;
        var scheduler = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
        // A retained record on another visible profile is not the current scope.
        // The closed DATA shape is valid; this is a menu filter control, not a
        // claimed second accepted grant producer.
        var menuProfiles = (await files.ReadFileBytesAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!;
        var menuRoot = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(menuProfiles))!.AsObject();
        var foreign = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(resident ? "resident" : "guardian", "guardian_previous_aid", "Chaos Sea", 42);
        foreign[SpiritualTreatmentParticipation.RecordProperty] = menuRoot["profiles"]!.AsArray().OfType<JsonObject>()
            .Single(p => SpiritualTreatmentParticipation.ProfileId(p) == participantId)[SpiritualTreatmentParticipation.RecordProperty]!.DeepClone();
        if (resident) foreign[SpiritualTreatmentParticipation.RecordProperty]!["target"]!["ownerId"] = "guardian_previous_aid";
        else foreign[SpiritualTreatmentParticipation.RecordProperty]![receiveAid ? "healerId" : "targetId"] = "guardian_previous_aid";
        SpiritualTreatmentParticipation.ValidateRecord(foreign[SpiritualTreatmentParticipation.RecordProperty]!.AsObject());
        menuRoot["profiles"]!.AsArray().Insert(0, foreign);
        await files.WriteFileAtomicAsync(WoundCarrierCatalog.AfterlifeProfilesPath, menuRoot.ToJsonString());
        var menu = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
        Assert.Equal(selection, Assert.Single(menu.WoundCommandState!.Choices, c => c.Available).Selection);
        await files.WriteFileAtomicBytesAsync(WoundCarrierCatalog.AfterlifeProfilesPath, menuProfiles);
        var draws = 0;
        int[] Draw(int count) { draws++; Assert.Equal(1, count); return [14]; }
        if (receiveAid)
        {
            Assert.False(selection.Aid!.PlayerConfirmed); // Menu/record alone never confirms for the player.
            var choices = (await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)).WoundCommandState!;
            var choice = Assert.Single(choices.Choices, c => c.Available);
            Assert.True(choice.Selection.Aid!.ReceiveAid);
            await using var confirmationLease = await files.AcquireCanonicalWriteLeaseAsync();
            var before = fixture.CaptureMutationRoots([PendingPlayerActionService.PendingPath]);
            var unconfirmed = await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, confirmationLease, scheduler,
                "session_spiritual_healing", "request_aid", 44, selection, Draw);
            Assert.False(unconfirmed.Stage.IsStaged);
            Assert.Null(unconfirmed.Control);
            Assert.Equal(0, draws);
            Assert.Throws<InvalidDataException>(() => PendingPlayerActionService.PrepareQueued(files, confirmationLease,
                WoundApplicationService.ReceiveAidActionText, "browser-wound-action", DateTime.UtcNow.ToString("o"), selection));
            fixture.AssertMutationRootsUnchanged(before);
            selection = await WoundApplicationService.ConfirmAsync(files, confirmationLease, choices, choice.Value);
            Assert.True(selection.Aid!.PlayerConfirmed);
            fixture.AssertMutationRootsUnchanged(before); // Actual confirmation itself does not draw or publish treatment.
        }
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var before = fixture.CaptureMutationRoots([LiveTurnPreparationService.TurnRequestPath]);
            foreach (var (session, turn) in new[] { ("foreign_session", 44), ("session_spiritual_healing", 43), ("session_spiritual_healing", 45) })
            {
                var denied = await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                    session, "request_aid", turn, selection, Draw);
                Assert.False(denied.Stage.IsStaged);
                Assert.Null(denied.Control);
                Assert.Equal(0, draws);
                fixture.AssertMutationRootsUnchanged(before);
            }
            if (resident)
            {
                var path = ProgressionScheduleService.SchedulePath;
                var scheduleBytes = (await files.ReadFileBytesAsync(lease, path))!;
                var foreignCycle = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(scheduleBytes))!.AsObject();
                foreignCycle["currentChaosSeaTurnOrdinal"] = foreignCycle["currentChaosSeaTurnOrdinal"]!.GetValue<int>() + 1;
                await files.WriteFileAtomicAsync(lease, path, foreignCycle.ToJsonString());
                var changedCycle = fixture.CaptureMutationRoots();
                Assert.False((await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                    "session_spiritual_healing", "request_aid", 44, selection, Draw)).Stage.IsStaged);
                Assert.Equal(0, draws); fixture.AssertMutationRootsUnchanged(changedCycle);
                await files.WriteFileAtomicBytesAsync(lease, path, scheduleBytes);
            }
            // A present competing request is not treated as the normal cleaned absence.
            await files.WriteFileAtomicAsync(lease, LiveTurnPreparationService.TurnRequestPath,
                new JsonObject { ["sessionId"] = "foreign_session", ["turnNumber"] = 43 }.ToJsonString());
            var competing = fixture.CaptureMutationRoots([LiveTurnPreparationService.TurnRequestPath]);
            Assert.False((await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                "session_spiritual_healing", "request_aid", 44, selection, Draw)).Stage.IsStaged);
            Assert.Equal(0, draws);
            fixture.AssertMutationRootsUnchanged(competing);
            files.DeleteFile(lease, LiveTurnPreparationService.TurnRequestPath);
            var profileBytes = (await files.ReadFileBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!;
            foreach (var refusal in new[] { "missing", "revoked", "incapable", "copied_content" })
            {
                var root = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(profileBytes))!.AsObject();
                var target = root["profiles"]!.AsArray().OfType<JsonObject>().Single(p => SpiritualTreatmentParticipation.ProfileId(p) == participantId);
                switch (refusal)
                {
                    case "missing": target.Remove(SpiritualTreatmentParticipation.RecordProperty); break;
                    case "revoked": target[SpiritualTreatmentParticipation.RecordProperty]!["status"] = "revoked"; break;
                    case "incapable": target[SpiritualTreatmentParticipation.RecordProperty]![receiveAid ? "providerCapable" : "capable"] = false; break;
                    default: target[SpiritualTreatmentParticipation.RecordProperty]!["readableEvidence"] = "Иное содержание при том же старом tuple."; break;
                }
                await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, root.ToJsonString());
                var unchanged = fixture.CaptureMutationRoots();
                Assert.False((await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                    "session_spiritual_healing", "request_aid", 44, selection, Draw)).Stage.IsStaged);
                Assert.Equal(0, draws);
                fixture.AssertMutationRootsUnchanged(unchanged);
                await files.WriteFileAtomicBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profileBytes);
            }
        }
        BookOfEternityClient.Models.ProgressionControl control;
        JsonObject request;
        JsonObject projection;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var staged = await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                "session_spiritual_healing", "request_aid", 44, selection, Draw);
            Assert.True(staged.Stage.IsStaged, staged.Stage.IssueCode);
            control = Assert.IsType<BookOfEternityClient.Models.ProgressionControl>(staged.Control);
            Assert.Equal(1, draws);
            projection = Assert.IsType<JsonObject>(staged.Stage.GmContext);
            request = JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
            Assert.Equal("aid", request["mode"]!.GetValue<string>());
            Assert.Equal(family, request["schemaVersion"]!.GetValue<int>());
            if (resident) Assert.Equal("player_to_resident", request["direction"]!.GetValue<string>());
            if (receiveAid)
            {
                Assert.Equal("guardian_to_player", request["direction"]!.GetValue<string>());
                Assert.Equal("player_target_confirmed", request["playerConfirmation"]!.GetValue<string>());
            }
            Assert.Equal(3, request["activeSource"]!["healingTier"]!.GetValue<int>());
            var nullRecord = request.DeepClone().AsObject();
            nullRecord["participation"]!["record"]![receiveAid ? "targetCapable" : "capable"] = null;
            Assert.False(SpiritualSelfTreatmentRequestCodec.IsBoundAid(nullRecord,
                new("session_spiritual_healing", "request_aid", 44, control), selection, "chaos_sea", control.NextChaosSeaTurnOrdinal));
            if (resident)
            {
                var context = new SpiritualSelfTreatmentStageContext("session_spiritual_healing", "request_aid", 44, control);
                Assert.True(SpiritualSelfTreatmentRequestCodec.IsBoundAid(request, context, selection, "chaos_sea", control.NextChaosSeaTurnOrdinal));
                Assert.False(SpiritualSelfTreatmentRequestCodec.IsBoundAid(request, context, selection, "chaos_sea", control.NextChaosSeaTurnOrdinal + 1));
                foreach (var mutation in new[] { "schema", "direction", "healer", "target", "locality", "extra", "unknown_schema", "self_mode" })
                {
                    var forgedRequest = request.DeepClone().AsObject();
                    switch (mutation)
                    {
                        case "schema": forgedRequest["schemaVersion"] = 4; break;
                        case "unknown_schema": forgedRequest["schemaVersion"] = 99; break;
                        case "self_mode": forgedRequest["mode"] = "self"; break;
                        case "direction": forgedRequest["direction"] = "guardian_to_player"; break;
                        case "healer": forgedRequest["healer"]!["ownerKind"] = "guardian"; break;
                        case "target": forgedRequest["target"]!["ownerKind"] = "guardian"; break;
                        case "locality": forgedRequest["participation"]!["record"]!["locality"]!["abodeId"] = "foreign_abode"; break;
                        default: forgedRequest["playerConfirmation"] = "player_target_confirmed"; break;
                    }
                    // Rehash the mutated public DATA so stale integrity alone
                    // cannot explain the family/header refusal.
                    var unsigned = forgedRequest.DeepClone().AsObject(); unsigned.Remove("requestFingerprint");
                    forgedRequest["requestFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
                    {
                        "spiritual_treatment_request", forgedRequest["schemaVersion"]!.GetValue<int>().ToString(System.Globalization.CultureInfo.InvariantCulture),
                        WoundAcceptedTurnFingerprintWriter.CanonicalJson(unsigned)
                    });
                    Assert.False(SpiritualSelfTreatmentRequestCodec.IsBoundAid(forgedRequest, context, selection, "chaos_sea", control.NextChaosSeaTurnOrdinal));
                }
                Assert.Equal(1, draws);
            }
            var retained = fixture.CaptureMutationRoots();
            var repeated = await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                "session_spiritual_healing", "request_aid", 44, selection, Draw);
            Assert.False(repeated.Stage.IsStaged);
            Assert.Null(repeated.Control);
            Assert.Equal(1, draws);
            fixture.AssertMutationRootsUnchanged(retained); // No pending deletion or second control/draw.
        }
        await new LiveTurnPreparationService(files).PrepareAsync(new()
        {
            SessionId = "session_spiritual_healing", RequestId = "request_aid", TurnNumber = 44,
            CurrentRealm = "Chaos Sea", PlayerAction = resident ? WoundApplicationService.ResidentAidActionText : receiveAid ? WoundApplicationService.ReceiveAidActionText : WoundApplicationService.AidActionText, ProgressionControl = control
        });
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            await WriteAidReportAsync(files, lease, control);
            var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease), i => i.Severity == IssueSeverity.Error);
            var missingGraphs = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            Assert.Contains(missingGraphs, i => i.Actual?.Contains("GM-authored spiritual destination graphs are required", StringComparison.Ordinal) == true);
            Assert.True(JsonNode.DeepEquals(request, JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)));
            var manifest = await fixture.ReadManifestAsync(lease);
            await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", new JsonObject
            {
                ["dialogueOptions"] = new JsonArray(), ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["spiritualHealingGraphs"] = AuthorNaturalGraphs(original, projection["destinationGraphs"]!.AsArray())
            }.ToJsonString());
            if (resident) await WriteResidentAidMemoryAsync(files, lease, "request_aid", 44);
            var localPath = AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0];
            var locality = (await files.ReadFileBytesAsync(lease, localPath))!;
            var moved = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(locality))!.AsObject();
            moved["chaosSeaNavigation"]!["currentAbodeId"] = "different_abode";
            await files.WriteFileAtomicAsync(lease, localPath, moved.ToJsonString());
            var before = fixture.CaptureMutationRoots([localPath]);
            Assert.Contains(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease), i => i.Severity == IssueSeverity.Error);
            fixture.AssertMutationRootsUnchanged(before);
            await files.WriteFileAtomicBytesAsync(lease, localPath, locality);
            var consentBefore = (await files.ReadFileBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!;
            var revoked = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(consentBefore))!.AsObject();
            var withdrawingGuardian = revoked["profiles"]!.AsArray().OfType<JsonObject>()
                .Single(p => SpiritualTreatmentParticipation.ProfileId(p) == participantId);
            if (receiveAid)
            {
                var withdrawal = GuardianOfferProposal(); withdrawal["operation"] = "revoke";
                withdrawingGuardian[SpiritualTreatmentParticipation.ChangeProperty] = withdrawal;
            }
            else withdrawingGuardian[SpiritualTreatmentParticipation.RecordProperty]!["status"] = "revoked";
            await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, revoked.ToJsonString());
            before = fixture.CaptureMutationRoots();
            Assert.Contains(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease), i => i.Severity == IssueSeverity.Error);
            fixture.AssertMutationRootsUnchanged(before);
            Assert.True(JsonNode.DeepEquals(request, JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)));
            Assert.Equal(1, draws); // Post-draw revocation refuses without reroll or stale prepared fallback.
            await files.WriteFileAtomicBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, consentBefore);
            var issues = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            Assert.True(!issues.Any(i => i.Severity == IssueSeverity.Error), string.Join("\n", issues.Select(i => i.Code + ":" + i.Actual)));
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var ready));
            var aidOwner = Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(ready.Plan!.SpiritualHealingContinuation);
            var originalSourceFingerprint = aidOwner.SourceFingerprint;
            var signedLocality = (await files.ReadFileBytesAsync(lease, manifest.Files[localPath]))!;
            if (resident)
            {
                var responseBytes = (await files.ReadFileBytesAsync(lease, "output/interface_updates.json"))!;
                var changedResponse = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(responseBytes))!.AsObject();
                changedResponse[SpiritualTreatmentParticipation.ResidentThoughtResponseProperty]![0]!["summary"] = "Я изменил память после допуска.";
                await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", changedResponse.ToJsonString());
                var retainedSources = fixture.CaptureMutationRoots();
                Assert.Throws<InvalidDataException>(() => aidOwner.RequireCurrent(files, lease));
                fixture.AssertMutationRootsUnchanged(retainedSources);
                Assert.True(JsonNode.DeepEquals(request, JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)));
                await files.WriteFileAtomicBytesAsync(lease, "output/interface_updates.json", responseBytes);
                // Generic case-insensitive locality sets are unchanged, but the
                // exact typed join must stop the actual owner before its writer.
                var residentPath = GuardianAbodeResidentState.StatePath;
                var residentRoot = JsonNode.Parse((await files.ReadFileAsync(lease, residentPath))!)!.AsObject();
                var rosterBytes = (await files.ReadFileBytesAsync(lease, residentPath))!;
                var rewrittenMemory = residentRoot.DeepClone().AsObject();
                rewrittenMemory[GuardianAbodeResidentState.ThoughtJournalProperty]![0]!["summary"] = "Я переписал уже принятую память.";
                await files.WriteFileAtomicAsync(lease, residentPath, rewrittenMemory.ToJsonString());
                var refusedSources = fixture.CaptureMutationRoots(new[] { residentPath });
                Assert.Throws<InvalidDataException>(() => aidOwner.RequireCurrent(files, lease));
                fixture.AssertMutationRootsUnchanged(refusedSources);
                await files.WriteFileAtomicBytesAsync(lease, residentPath, rosterBytes);
                var changed = residentRoot.DeepClone().AsObject();
                changed["entries"]![0]!["guardianId"] = AidGuardianId.ToUpperInvariant();
                var genericBefore = LocalInteractionScopeService.ResolveChaosSeaScope(
                    JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(locality))!.AsObject(), residentRoot, Array.Empty<LocalInteractionAuthoritySnapshot>());
                var genericAfter = LocalInteractionScopeService.ResolveChaosSeaScope(
                    JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(locality))!.AsObject(), changed, Array.Empty<LocalInteractionAuthoritySnapshot>());
                Assert.Equal(genericBefore.LocalActorIds.OrderBy(x => x), genericAfter.LocalActorIds.OrderBy(x => x));
                var published = false;
                await Assert.ThrowsAsync<InvalidDataException>(() => aidOwner.PublishOwnedLocalityNormalizationAsync(files, lease,
                    residentPath, changed.ToJsonString(), _ => { published = true; return Task.CompletedTask; }));
                Assert.False(published); Assert.Equal(1, draws);
            }
            Assert.True(aidOwner.HasCompleteProfileProjection);
            // Real preflight must precede owned normalization. A manual write
            // followed by a second preflight correctly fails the sealed plan's
            // original BeforeImages and is not the production writer route.
            Assert.NotNull(await new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAccumulatedStateWithPlanAsync(manifest.Files));
            Assert.True(aidOwner.IsClosed); // Actual publication re-proved the aid main18 closing map.
            Assert.Equal(originalSourceFingerprint, aidOwner.SourceFingerprint);
            Assert.Equal(signedLocality, await files.ReadFileBytesAsync(lease, manifest.Files[localPath]));
            var normalizedLocality = (await files.ReadFileBytesAsync(lease, localPath))!;
            Assert.False(locality.AsSpan().SequenceEqual(normalizedLocality));
            Assert.Empty(JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(normalizedLocality))!
                ["guardians"]![0]![GuardianTradeRequestState.ReceiptsProperty]!.AsArray());
            Assert.NotNull(await ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(control,
                manifest.SessionId, manifest.RequestId, 44, p => files.ReadFileBytesAsync(lease, p), manifest.ManifestPayloadHash, request));
        }
        var final = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(2, final.Severity.Rank);
        Assert.Equal(2, final.Recovery.CurrentStepProgress); // III1 → activeH3 II1 → naturalH0 II2.
        var history = await fixture.ReadHistoryAsync();
        var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions, t => t.AttemptId is not null).TransitionResult);
        Assert.Equal("aid", result.Mode);
        Assert.Equal(family, result.SchemaVersion);
        Assert.True(JsonNode.DeepEquals(request, result.ToCanonicalJson()["request"]));
        if (resident)
        {
            var unknownResult = result.ToCanonicalJson(); unknownResult["schemaVersion"] = 99;
            unknownResult.Remove("direction"); // Exact generic root shape; header must reject before inner DATA.
            unknownResult["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(unknownResult);
            var unknownIssues = new List<ValidationIssue>();
            Assert.Null(SpiritualHealingPersistedResult.Parse(JsonSerializer.SerializeToElement(unknownResult), "resident.unknown.result", unknownIssues));
            Assert.Contains(unknownIssues, i => i.Actual == "Spiritual result kind/schema/fingerprint differs.");
        }
        var missingGrant = JsonNode.Parse(WoundHistoryState.SerializeCanonical(history))!.AsObject();
        var grantRow = missingGrant["transitions"]!.AsArray().OfType<JsonObject>()
            .Single(r => r["transitionResult"]?["mode"]?.GetValue<string>() == "natural");
        grantRow.Remove("transitionResult"); // Retain every row, chronology/FP and the complete accepted aid result.
        var standaloneIssues = new List<ValidationIssue>();
        Assert.NotNull(SpiritualHealingPersistedResult.Parse(JsonSerializer.SerializeToElement(result.ToCanonicalJson()),
            "standalone.aid", standaloneIssues));
        Assert.Empty(standaloneIssues);
        var missing = WoundHistoryState.Parse(missingGrant.ToJsonString(), "missing.grant.history");
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Issues, i => i.Code == "spiritual_aid_grant_history_invalid");
        var forged = result.ToCanonicalJson();
        forged["source"]!["healingTier"] = 4;
        forged["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(forged);
        var parserIssues = new List<ValidationIssue>();
        Assert.Null(SpiritualHealingPersistedResult.Parse(JsonSerializer.SerializeToElement(forged), "changed.activeH", parserIssues));
        Assert.Contains(parserIssues, i => i.Actual?.Contains("exact retained request", StringComparison.Ordinal) == true);
        if (resident)
        {
            await using var localityLease = await files.AcquireCanonicalWriteLeaseAsync();
            var rosterBytes = (await files.ReadFileBytesAsync(localityLease, GuardianAbodeResidentState.StatePath))!;
            var settledMemory = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(rosterBytes))![GuardianAbodeResidentState.ThoughtJournalProperty]!.AsArray();
            Assert.Equal(new[] { 43, 44 }, settledMemory.Select(e => e!["turn"]!.GetValue<int>()));
            var departed = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(rosterBytes))!.AsObject();
            departed["entries"]![0]!["isPresent"] = false;
            await files.WriteFileAtomicAsync(localityLease, GuardianAbodeResidentState.StatePath, departed.ToJsonString());
            // History is historical DATA and contains no current roster. A new
            // live attempt still requires presence; no after-image guard is waived.
            var historical = WoundHistoryState.Parse((await files.ReadFileAsync(localityLease, WoundHistoryState.HistoryPath))!, "resident.departed.history");
            Assert.True(historical.IsValid, string.Join(";", historical.Issues.Select(i => i.Actual)));
            var profiles = JsonNode.Parse((await files.ReadFileAsync(localityLease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
            var guardians = JsonNode.Parse((await files.ReadFileAsync(localityLease, AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0]))!)!.AsObject();
            Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.RequireResidentScope(
                guardians, departed, profiles, result.Stages[0].Before.Owner, result.Stages[0].Before));
            Assert.NotNull(await ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(control,
                "session_spiritual_healing", "request_aid", 44, p => files.ReadFileBytesAsync(localityLease, p)));
            await files.WriteFileAtomicBytesAsync(localityLease, GuardianAbodeResidentState.StatePath, rosterBytes);
        }
        if (resident)
        {
            var outputBytes = (await files.ReadFileBytesAsync("output/interface_updates.json"))!;
            var rosterBytes = (await files.ReadFileBytesAsync(GuardianAbodeResidentState.StatePath))!;
            foreach (var drift in new[] { "response", "old_memory", "new_memory" })
            {
                await using (var mutation = await files.AcquireCanonicalWriteLeaseAsync())
                {
                    if (drift == "response")
                    {
                        var changed = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(outputBytes))!.AsObject();
                        changed[SpiritualTreatmentParticipation.ResidentThoughtResponseProperty]![0]!["summary"] = "Я заменил ответ после публикации.";
                        await files.WriteFileAtomicAsync(mutation, "output/interface_updates.json", changed.ToJsonString());
                    }
                    else
                    {
                        var changed = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(rosterBytes))!.AsObject();
                        changed[GuardianAbodeResidentState.ThoughtJournalProperty]![drift == "old_memory" ? 0 : 1]!["summary"] = "Я переписал принятую память.";
                        await files.WriteFileAtomicAsync(mutation, GuardianAbodeResidentState.StatePath, changed.ToJsonString());
                    }
                }
                var refused = await new ValidationService(files, NullLogger<ValidationService>.Instance).ValidateAcceptedTurnInterfacePayloadAsync();
                Assert.Contains(refused, issue => issue.Code == "spiritual_healing_interface_unbound" &&
                    issue.Actual!.Contains(drift == "response" ? "exact published family5 response" : "complete exact accepted append", StringComparison.Ordinal));
                await using var restore = await files.AcquireCanonicalWriteLeaseAsync();
                await files.WriteFileAtomicBytesAsync(restore, "output/interface_updates.json", outputBytes);
                await files.WriteFileAtomicBytesAsync(restore, GuardianAbodeResidentState.StatePath, rosterBytes);
            }
        }
        var settled = fixture.CaptureMutationRoots();
        Assert.DoesNotContain(await new ValidationService(files, NullLogger<ValidationService>.Instance)
            .ValidateAcceptedTurnInterfacePayloadAsync(), i => i.Severity == IssueSeverity.Error);
        fixture.AssertMutationRootsUnchanged(settled);
        fixture.AssertResourcesUnchanged();
        Assert.Equal(1, draws);
    }

    private static async Task<SpiritualSelfTreatmentSelection> CommitAidGrantAsync(HealingFixture fixture, bool receiveAid = false, bool resident = false)
    {
        var participantId = resident ? AidResidentId : AidGuardianId;
        await StageAsync(fixture);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
            profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => SpiritualTreatmentParticipation.ProfileId(p) == participantId)
                [SpiritualTreatmentParticipation.ChangeProperty] = resident ? ResidentGrantProposal() : receiveAid ? GuardianOfferProposal() : AidGrantProposal();
            await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        }
        if (resident)
        {
            await using var publication = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            await fixture.WriteReportAsync(publication);
            await fixture.Files.WriteFileAtomicAsync(publication, "output/interface_updates.json", new JsonObject
            {
                ["timestamp"] = DateTime.UtcNow.ToString("o"), ["dialogueOptions"] = new JsonArray()
            }.ToJsonString());
            await WriteResidentAidMemoryAsync(fixture.Files, publication, "request_spiritual_healing", 43, memoryOnly: true);
            var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(publication), i => i.Severity == IssueSeverity.Error);
            var issues = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(publication);
            Assert.True(!issues.Any(i => i.Severity == IssueSeverity.Error), string.Join(";", issues.Select(i => i.Code + ":" + i.Actual)));
            var manifest = await fixture.ReadManifestAsync(publication);
            Assert.NotNull(await new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(publication).NormalizeAccumulatedStateWithPlanAsync(manifest.Files));
        }
        else await PublishAsync(fixture);
        if (resident)
            Assert.DoesNotContain(await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
                .ValidateAcceptedTurnInterfacePayloadAsync(), i => i.Severity == IssueSeverity.Error);
        var after = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        await using var cleanup = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        // Direct publisher fixture models the cleaned protocol absence; actual
        // Engine cleanup is separately required by the real guided caller tests.
        fixture.Files.DeleteFile(cleanup, LiveTurnPreparationService.TurnRequestPath);
        var root = JsonNode.Parse((await fixture.Files.ReadFileAsync(cleanup, WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
        var record = root["profiles"]!.AsArray().OfType<JsonObject>().Single(p => SpiritualTreatmentParticipation.ProfileId(p) == participantId)
            [SpiritualTreatmentParticipation.RecordProperty]!.AsObject();
        return new(after.WoundId, WoundIdentityState.ComputeSemanticFingerprint(after))
            { Aid = SpiritualTreatmentParticipation.Selection(record) };
    }

    internal static async Task WriteResidentAidMemoryAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, string requestId, int turn, bool memoryOnly = false)
    {
        var response = JsonNode.Parse((await files.ReadFileAsync(lease, "output/interface_updates.json")) ?? "{}")!.AsObject();
        if (memoryOnly)
        {
            Assert.True(response["spiritualHealingGraphs"] is null or JsonArray { Count: 0 });
            response.Remove("spiritualHealingGraphs");
        }
        response[SpiritualTreatmentParticipation.ResidentThoughtResponseProperty] = new JsonArray(new JsonObject
        {
            ["entryId"] = "resident_aid_thought_" + requestId, ["residentId"] = AidResidentId, ["turn"] = turn,
            ["timestamp"] = DateTime.UtcNow.ToString("o"), ["title"] = "Осознанная помощь",
            ["summary"] = "Я понимаю предложение души, добровольно принимаю помощь и сохраняю право отказаться.",
            ["eventType"] = "conversation", ["consequence"] = "Помощь без нового долга.",
            ["attitude"] = "trusting", ["intent"] = "Восстановить целостность добровольно.", ["tags"] = new JsonArray()
        });
        await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", response.ToJsonString());
    }

    internal static JsonObject ResidentGrantProposal()
    {
        var proposal = AidGrantProposal(); proposal["schemaVersion"] = 3; proposal["kind"] = "resident_target_consent";
        proposal["readableEvidence"] = "Житель целостности осознанно разрешает душе лечить эту рану в следующий безопасный цикл.";
        return proposal;
    }

    internal static JsonObject GuardianOfferProposal() => new()
    {
        ["schemaVersion"] = 2, ["kind"] = "provider_offer", ["operation"] = "offer",
        ["targetId"] = "player_soul", ["woundId"] = "wound_spiritual_session",
        ["providerConscious"] = true, ["providerCapable"] = true,
        ["targetConscious"] = true, ["targetCapable"] = true,
        ["readableEvidence"] = "Хранитель готов бесплатно помочь сознательной душе; решение принять помощь остаётся за игроком."
    };

    internal static JsonObject AidGrantProposal() => new()
    {
        ["schemaVersion"] = 1, ["operation"] = "grant", ["healerId"] = "player_soul", ["woundId"] = "wound_spiritual_session",
        ["conscious"] = true, ["capable"] = true, ["readableEvidence"] = "Хранитель понимает предложение и прямо разрешает душе лечить эту рану."
    };

    private static Task WriteAidReportAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        BookOfEternityClient.Models.ProgressionControl c, string requestId = "request_aid", int turn = 44) => files.WriteFileAtomicAsync(lease, ProgressionScheduleService.ReportPath,
        new JsonObject { ["progressionProcessingReport"] = new JsonObject
        {
            ["sessionId"] = "session_spiritual_healing", ["requestId"] = requestId, ["turnNumber"] = turn,
            ["worldCyclesProcessed"] = 0, ["factionCyclesProcessed"] = 0,
            ["chaosSeaCyclesProcessed"] = 1, ["guardianProjectCyclesProcessed"] = 1, ["residentAgencyCyclesProcessed"] = 1,
            ["newLastChaosSeaSimulationOrdinal"] = c.NextChaosSeaTurnOrdinal,
            ["newLastGuardianProjectCycleOrdinal"] = c.NextGuardianProjectCycleOrdinal,
            ["newLastResidentAgencyCycleOrdinal"] = c.NextResidentAgencyCycleOrdinal
        }}.ToJsonString());
}
