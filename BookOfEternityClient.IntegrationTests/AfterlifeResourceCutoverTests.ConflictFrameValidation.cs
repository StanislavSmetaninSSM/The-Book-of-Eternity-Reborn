using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private const string FrameSoulPath = "game_state/meta/soul_state.json";
    private const string FrameRequestPath = "input/turn_request.json";
    private const string FrameManifestPath = "game_state/control/pending_turn_snapshot.json";

    // Stage RED: this method and the non-frame helpers compile before the API exists.
    [Fact]
    public async Task ConflictFrame_DetachedApiExistsAndEvaluatesRealPublishedState()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        var expected = await ValidateCompleteConflictFrameAsync(context);
        AssertNoConflictFrameErrors(expected);

        var capture = typeof(ValidationService).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .SingleOrDefault(method => method.Name == "CaptureSpiritualConflictValidationFrameAsync" &&
                method.GetParameters() is { Length: 1 } parameters && parameters[0].IsOptional &&
                parameters[0].DefaultValue is null);
        Assert.NotNull(capture); // First executable semantic RED, not a missing-type build.
        var pending = Assert.IsAssignableFrom<Task>(capture!.Invoke(context.Validator, new object?[] { null }));
        await pending;
        var resultProperty = pending.GetType().GetProperty("Result");
        Assert.NotNull(resultProperty);
        var frame = resultProperty!.GetValue(pending);
        Assert.NotNull(frame);
        var evaluate = typeof(ValidationService).GetMethod(
            "EvaluateSpiritualConflictValidationFrame",
            BindingFlags.Instance | BindingFlags.NonPublic, null, [frame!.GetType()], null);
        Assert.NotNull(evaluate);
        var actual = Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            evaluate!.Invoke(context.Validator, [frame]));
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(actual));
        AssertNoConflictFrameErrors(actual);
    }

    [Theory]
    [InlineData("published")]
    [InlineData("unauthorized_die")]
    [InlineData("terminal_game_over")]
    public async Task ConflictFrame_WrapperAndDetachedHaveIdenticalCompleteIssues(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        AssertNoConflictFrameErrors(await ValidateCompleteConflictFrameAsync(context));
        if (mutation == "unauthorized_die")
        {
            var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
                AfterlifeSpiritualConflictState.StatePath));
            root["activeConflict"]!["exchangeLog"]![0]!["diceAudit"]!["diceUsed"]![0]!["value"] = 14;
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        }
        else if (mutation == "terminal_game_over")
        {
            var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(FrameSoulPath));
            soul[AfterlifeSpiritualConflictState.TerminalGameOverProperty] = new JsonObject
            {
                ["state"] = AfterlifeSpiritualConflictState.TerminalSoulDissipationState,
                ["message"] = AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage,
                ["conflictId"] = "conflict_resource_cost"
            };
            var original = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
            var candidate = original.WithCandidateImages(original.Candidate with { Soul = FrameImage(soul) });
            AssertNoConflictFrameErrors(context.Validator.EvaluateSpiritualConflictValidationFrame(original));
            Assert.Contains(context.Validator.EvaluateSpiritualConflictValidationFrame(candidate), issue =>
                issue.Code == "afterlife_conflict_player_soul_dissipation_unlinked_game_over");
            await context.WriteExactJsonAsync(FrameSoulPath, soul.ToJsonString());
        }

        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        var actual = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        var expected = await ValidateCompleteConflictFrameAsync(context);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(actual));
        if (mutation == "published")
            AssertNoConflictFrameErrors(actual);
        else
            Assert.Contains(actual, issue => issue.Severity == IssueSeverity.Error &&
                issue.Code == (mutation == "unauthorized_die"
                    ? "afterlife_conflict_dice_value_not_authorized"
                    : "afterlife_conflict_player_soul_dissipation_unlinked_game_over"));
    }

    [Fact]
    public async Task ConflictFrame_CapturedImagesSurviveAllFilesystemInputsChangingWithoutReads()
    {
        var probe = new ConflictFrameReadProbe();
        await using var context = await CreateCompleteConflictFrameContextAsync(probe.Hooks);
        // Populate both optional captured inputs before a genuine fresh signed capture.
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["lightSparks"] = 0;
        await context.WriteExactJsonAsync(ShiningAbodeState.StatePath, shining.ToJsonString());
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath,
            """{"difficulty":"normal"}""");
        await context.CaptureValidatedPendingSnapshotAsync(42, "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8]);
        await PublishCompleteConflictFrameAsync(context);
        // This fixture normally has no settings; readable normal settings require
        // their actual complete dice audit even though the modifier remains zero.
        var configuredRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeSpiritualConflictState.StatePath));
        configuredRoot["activeConflict"]!["exchangeLog"]![0]!["diceAudit"]!["difficultyAudit"] = new JsonObject
        {
            ["difficulty"] = "normal",
            ["source"] = "game_state/core/game_settings.json.difficulty",
            ["oppositionModifier"] = 0,
            ["rewardMultiplierPercent"] = 100
        };
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, configuredRoot.ToJsonString());
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        Assert.Null(frame.Candidate.Profiles);
        Assert.Null(frame.TurnRequest);
        Assert.NotNull(frame.Baseline.Conflict);
        Assert.NotNull(frame.Baseline.Soul);
        Assert.NotNull(frame.Baseline.Shining);
        Assert.NotNull(frame.Baseline.Profiles);
        Assert.NotNull(frame.Baseline.ResourceDefinitions);
        Assert.NotNull(frame.Baseline.ResourceState);
        Assert.False(frame.Baseline.ResourceAcquisitionFailed);
        var expected = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        AssertNoConflictFrameErrors(expected);
        var candidateBefore = frame.Candidate;
        var baselineBefore = frame.Baseline;

        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(FrameManifestPath));
        var signedPaths = Assert.IsType<JsonObject>(manifest["files"]).Select(
            pair => pair.Value!.GetValue<string>()).ToArray();
        var currentPaths = new[]
        {
            AfterlifeSpiritualConflictState.StatePath, FrameSoulPath, ShiningAbodeState.StatePath,
            AfterlifeEntityProfileState.StatePath, AfterlifeSpiritualConflictState.DifficultySettingsPath,
            FrameRequestPath, ResourceMaterializationTestContext.DefinitionsPath,
            ResourceMaterializationTestContext.StatePath, PendingTurnSnapshotAuthority.AuthorityPath,
            FrameManifestPath
        };
        foreach (var path in signedPaths.Concat(currentPaths).Distinct(StringComparer.Ordinal))
            await context.WriteExactJsonAsync(path, "{ deliberately changed after capture");

        probe.Start();
        var actual = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        var again = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        probe.Stop();
        Assert.Empty(probe.Events);
        Assert.Same(candidateBefore, frame.Candidate);
        Assert.Same(baselineBefore, frame.Baseline);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(actual));
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(again));
        var changed = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.False(changed.HasValidatedSnapshot);
        Assert.Contains(context.Validator.EvaluateSpiritualConflictValidationFrame(changed),
            issue => issue.Code == "afterlife_conflict_state_invalid_json");
    }

    [Fact]
    public async Task ConflictFrame_SnapshotDiceGetterCannotMutateCapturedOrCandidateFrame()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        var clone = frame.WithCandidateImages(frame.Candidate);
        var before = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        AssertNoConflictFrameErrors(before);
        var exposed = Assert.IsType<int[]>(frame.SnapshotDice);
        Assert.Equal(new[] { 15, 5, 12, 8 }, exposed);
        exposed[0] = 14;
        Assert.NotSame(exposed, frame.SnapshotDice);
        Assert.Equal(new[] { 15, 5, 12, 8 }, frame.SnapshotDice);
        Assert.Equal(new[] { 15, 5, 12, 8 }, clone.SnapshotDice);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(before),
            ConflictFrameIssueFingerprint.Create(context.Validator.EvaluateSpiritualConflictValidationFrame(frame)));
        Assert.Equal(ConflictFrameIssueFingerprint.Create(before),
            ConflictFrameIssueFingerprint.Create(context.Validator.EvaluateSpiritualConflictValidationFrame(clone)));
    }

    [Fact]
    public async Task ConflictFrame_CandidateCannotReplaceSignedRealmOrPlayerAndEntityTierAuthority()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        var originalImages = frame.Candidate;
        var expected = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        AssertNoConflictFrameErrors(expected);
        Assert.Null(originalImages.Profiles);
        var soul = JsonNode.Parse(frame.Candidate.Soul.Text!)!.AsObject();
        soul["currentRealm"] = "Mortal World";
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!["pressure"] = 5;
        var profiles = JsonNode.Parse(frame.Baseline.Profiles!)!.AsObject();
        foreach (var profile in profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().OfType<JsonObject>())
            profile["standardArts"]!["pressure"] = 5;
        var candidate = frame.WithCandidateImages(frame.Candidate with
        {
            Soul = FrameImage(soul),
            Profiles = FrameImage(profiles)
        });
        // Merely changing current alleged authority cannot alter this signed evaluation.
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(context.Validator.EvaluateSpiritualConflictValidationFrame(candidate)));
        Assert.True(candidate.HasValidatedSnapshot);
        Assert.Same(frame.Baseline, candidate.Baseline);
        Assert.Equal(frame.SnapshotTurnNumber, candidate.SnapshotTurnNumber);

        var root = JsonNode.Parse(candidate.Candidate.Conflict.Text!)!.AsObject();
        var active = root["activeConflict"]!.AsObject();
        active["realm"] = "Shining Abode";
        var cost = active["exchangeLog"]![0]!["actionCostAudit"]!;
        cost["player"]!["artTier"] = 5;
        cost["opposition"]!["artTier"] = 5;
        var changed = candidate.WithCandidateImages(candidate.Candidate with { Conflict = FrameImage(root) });
        var issues = context.Validator.EvaluateSpiritualConflictValidationFrame(changed);
        Assert.Contains(issues, issue => issue.Code == "afterlife_conflict_active_realm_mismatch");
        Assert.Contains(issues, issue => issue.Code == "afterlife_conflict_action_cost_art_tier_authority_mismatch");
        Assert.Contains(issues, issue => issue.Code == "afterlife_conflict_opposition_action_cost_art_tier_authority_mismatch");
        Assert.Same(originalImages, frame.Candidate);
        Assert.Null(frame.Candidate.Profiles);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(context.Validator.EvaluateSpiritualConflictValidationFrame(frame)));
    }

    [Fact]
    public async Task ConflictFrame_OfflineCandidateUsesCurrentRealmAndCapturesProfilesWithoutAcquiringSignedStatus()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        await context.DeleteAsync(FrameManifestPath);
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.False(frame.HasValidatedSnapshot);
        Assert.NotNull(frame.Candidate.Profiles);
        var original = ConflictFrameIssueFingerprint.Create(
            context.Validator.EvaluateSpiritualConflictValidationFrame(frame));
        var soul = JsonNode.Parse(frame.Candidate.Soul.Text!)!.AsObject();
        soul["currentRealm"] = "Mortal World";
        var changed = frame.WithCandidateImages(frame.Candidate with { Soul = FrameImage(soul) });
        Assert.False(changed.HasValidatedSnapshot);
        Assert.Contains(context.Validator.EvaluateSpiritualConflictValidationFrame(changed),
            issue => issue.Code == "afterlife_conflict_active_wrong_realm");
        Assert.Equal(original, ConflictFrameIssueFingerprint.Create(
            context.Validator.EvaluateSpiritualConflictValidationFrame(frame)));
        // Offline is deliberately not a newly strict signed-only source loader.
        Assert.Equal(ConflictFrameIssueFingerprint.Create(await ValidateCompleteConflictFrameAsync(context)), original);
    }

    [Fact]
    public async Task ConflictFrame_SignedDiceSkipOnlyTheAdditionalLiveRequestAndCurrentProfilesRead()
    {
        var populatedProbe = new ConflictFrameReadProbe();
        var emptyProbe = new ConflictFrameReadProbe();
        await using var populated = await CreateCompleteConflictFrameContextAsync(populatedProbe.Hooks);
        await using var empty = await CreateCompleteConflictFrameContextAsync(emptyProbe.Hooks);
        await empty.CaptureValidatedPendingSnapshotAsync(42, "Chaos Sea", preGeneratedDices1d20: []);

        populatedProbe.Start();
        var populatedFrame = await populated.Validator.CaptureSpiritualConflictValidationFrameAsync();
        populatedProbe.Stop();
        emptyProbe.Start();
        var emptyFrame = await empty.Validator.CaptureSpiritualConflictValidationFrameAsync();
        emptyProbe.Stop();
        Assert.True(populatedFrame.HasValidatedSnapshot);
        Assert.True(emptyFrame.HasValidatedSnapshot);
        Assert.Null(populatedFrame.TurnRequest);
        Assert.NotNull(emptyFrame.TurnRequest);
        Assert.Null(populatedFrame.Candidate.Profiles);
        Assert.Null(emptyFrame.Candidate.Profiles);
        Assert.Equal(0, populatedProbe.Attempts(AfterlifeEntityProfileState.StatePath));
        Assert.Equal(0, emptyProbe.Attempts(AfterlifeEntityProfileState.StatePath));
        var securityReads = populatedProbe.Attempts(FrameRequestPath);
        Assert.True(securityReads > 0); // Existing verification reads remain real.
        Assert.Equal(securityReads + 1, emptyProbe.Attempts(FrameRequestPath));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "afterlife_conflict_state_empty")]
    [InlineData(" \r\n\t", "afterlife_conflict_state_empty")]
    [InlineData("{", "afterlife_conflict_state_invalid_json")]
    [InlineData("[]", "afterlife_conflict_state_invalid_json")]
    public async Task ConflictFrame_ProductionEarlyReturnDoesNotAcquireUnusedContext(
        string? conflictText, string? expectedCode)
    {
        var probe = new ConflictFrameReadProbe();
        await using var context = await ResourceMaterializationTestContext.CreateAsync(probe.Hooks);
        Assert.False(context.FileSystem.FileExists(FrameManifestPath));
        if (conflictText is not null)
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflictText);
        probe.ForbiddenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FrameSoulPath, ShiningAbodeState.StatePath, AfterlifeEntityProfileState.StatePath,
            AfterlifeSpiritualConflictState.DifficultySettingsPath, FrameRequestPath,
            ResourceMaterializationTestContext.DefinitionsPath, ResourceMaterializationTestContext.StatePath
        };
        probe.Start();
        var expected = await ValidateCompleteConflictFrameAsync(context);
        probe.Stop();
        // FileExists also invokes this hook after the null read; both attempts are
        // the unchanged production path, not an extra acquisition by the frame.
        Assert.Equal(conflictText is null ? 2 : 1,
            probe.Attempts(AfterlifeSpiritualConflictState.StatePath));
        if (expectedCode is null)
            Assert.Empty(expected);
        else
            Assert.Equal(expectedCode, Assert.Single(expected).Code);

        // Explicit capture is a complete operation even when production returns early.
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.Equal(conflictText is not null, frame.Candidate.Conflict.Exists);
        Assert.Equal(conflictText, frame.Candidate.Conflict.Text);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(context.Validator.EvaluateSpiritualConflictValidationFrame(frame)));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("{", false)]
    [InlineData("{\"turnNumber\":42,\"preGeneratedDices1d20\":[15,\"ignored\",5,null,12,8]}", true)]
    public async Task ConflictFrame_OfflineLiveDiceFallbackPreservesExistingShapeSemantics(
        string? requestText, bool hasDice)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        await context.DeleteAsync(FrameManifestPath);
        if (requestText is null)
            await context.DeleteAsync(FrameRequestPath);
        else
            await context.WriteExactJsonAsync(FrameRequestPath, requestText);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var dice = root["activeConflict"]!["exchangeLog"]![0]!["diceAudit"]!.DeepClone();
        dice["diceUsed"]![0]!["value"] = 14;
        // Offline active exchanges deliberately drop current-turn dice authority.
        // Recent proofs do consume the captured fallback: exercise that real route.
        root["activeConflict"] = null;
        root["recentConflicts"] = new JsonArray(new JsonObject
        {
            ["mode"] = "resolve", ["dangerMode"] = "hostile",
            ["conflictId"] = "offline_frame_resolution", ["realm"] = "Chaos Sea",
            ["sideModel"] = "direct_duel", ["resolutionState"] = "resolved",
            ["operationType"] = "pressure", ["playerOutcome"] = "won",
            ["resolvedAtTurn"] = 42, ["diceAudit"] = dice
        });
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.False(frame.HasValidatedSnapshot);
        Assert.NotNull(frame.Candidate.Profiles);
        Assert.NotNull(frame.TurnRequest);
        Assert.Equal(requestText is not null, frame.TurnRequest!.Exists);
        Assert.Equal(requestText, frame.TurnRequest.Text);
        var actual = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(await ValidateCompleteConflictFrameAsync(context)),
            ConflictFrameIssueFingerprint.Create(actual));
        Assert.Equal(hasDice, actual.Any(issue => issue.Code == "afterlife_conflict_dice_value_not_authorized"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictFrame_SignedAbsentOrEmptyDiceUseCapturedLiveFallback(bool emptyDice)
    {
        var probe = new ConflictFrameReadProbe();
        await using var context = await CreateCompleteConflictFrameContextAsync(probe.Hooks);
        await context.CaptureValidatedPendingSnapshotAsync(42, "Chaos Sea",
            preGeneratedDices1d20: emptyDice ? Array.Empty<int>() : null);
        // This changes only live fallback data; identity/turn and all signed bytes stay intact.
        var request = Assert.IsType<JsonObject>(await context.ReadJsonAsync(FrameRequestPath));
        request["preGeneratedDices1d20"] = new JsonArray(15, "ignored", 5, null, 12, 8);
        await context.WriteExactJsonAsync(FrameRequestPath, request.ToJsonString());
        await PublishCompleteConflictFrameAsync(context);
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        Assert.Equal(42, frame.SnapshotTurnNumber);
        Assert.True(frame.SnapshotDice is null || frame.SnapshotDice.Length == 0);
        Assert.NotNull(frame.TurnRequest);
        var expected = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        AssertNoConflictFrameErrors(expected);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(await ValidateCompleteConflictFrameAsync(context)),
            ConflictFrameIssueFingerprint.Create(expected));
        var capturedRequest = frame.TurnRequest;
        request["preGeneratedDices1d20"] = new JsonArray(14, 5, 12, 8);
        await context.WriteExactJsonAsync(FrameRequestPath, request.ToJsonString());
        probe.Start();
        var replay = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        probe.Stop();
        Assert.Empty(probe.Events);
        Assert.Same(capturedRequest, frame.TurnRequest);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected), ConflictFrameIssueFingerprint.Create(replay));
        var recaptured = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(recaptured.HasValidatedSnapshot);
        Assert.Contains(context.Validator.EvaluateSpiritualConflictValidationFrame(recaptured),
            issue => issue.Code == "afterlife_conflict_dice_value_not_authorized");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictFrame_SnapshotTamperingPreservesManifestAndFileAuthorityBoundaries(
        bool tamperDetachedAuthority)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        var accepted = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(accepted.HasValidatedSnapshot);
        AssertNoConflictFrameErrors(context.Validator.EvaluateSpiritualConflictValidationFrame(accepted));
        if (tamperDetachedAuthority)
        {
            await context.WriteExactJsonAsync(PendingTurnSnapshotAuthority.AuthorityPath,
                "{ deliberately invalid detached authority");
        }
        else
        {
            var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(FrameManifestPath));
            var path = manifest["files"]![FrameSoulPath]!.GetValue<string>();
            await context.WriteExactJsonAsync(path, """{"currentRealm":"Mortal World"}""");
        }

        // Manifest usability and each signed file's readability are separate existing
        // boundaries. Never re-sign the corruption or substitute current profile data
        // when an otherwise usable manifest's signed soul bytes fail their own hash.
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.Null(frame.Baseline.Soul);
        var actual = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        if (tamperDetachedAuthority)
        {
            Assert.False(frame.HasValidatedSnapshot);
            Assert.NotNull(frame.Candidate.Profiles);
        }
        else
        {
            Assert.True(frame.HasValidatedSnapshot);
            Assert.Null(frame.Candidate.Profiles);
            Assert.Equal(accepted.SnapshotTurnNumber, frame.SnapshotTurnNumber);
            Assert.Equal(accepted.SnapshotDice, frame.SnapshotDice);
            Assert.Contains(actual, issue =>
                issue.Severity == IssueSeverity.Error &&
                issue.Code == "afterlife_conflict_active_wrong_realm");
        }
        Assert.Equal(ConflictFrameIssueFingerprint.Create(await ValidateCompleteConflictFrameAsync(context)),
            ConflictFrameIssueFingerprint.Create(actual));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConflictFrame_ResourceSnapshotReadFailurePreservesNarrowCaughtContext(
        bool failDefinitions)
    {
        var probe = new ConflictFrameReadProbe();
        await using var context = await CreateCompleteConflictFrameContextAsync(probe.Hooks);
        await PublishCompleteConflictFrameAsync(context);
        var control = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(control.HasValidatedSnapshot);
        Assert.False(control.Baseline.ResourceAcquisitionFailed);
        Assert.NotNull(control.Baseline.ResourceDefinitions);
        Assert.NotNull(control.Baseline.ResourceState);
        AssertNoConflictFrameErrors(context.Validator.EvaluateSpiritualConflictValidationFrame(control));

        // Real existing authenticated lookup, before any fault or prevalidated scope.
        // The exact returned object is retained; never deserialize/construct a manifest.
        var validatedManifest = await ReadGenuinelyValidatedConflictFrameManifestAsync(context.Validator);
        var filesProperty = validatedManifest.GetType().GetProperty("Files");
        Assert.NotNull(filesProperty);
        var files = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(
            filesProperty!.GetValue(validatedManifest));
        var definitionsSnapshotPath = files[ResourceMaterializationTestContext.DefinitionsPath];
        var stateSnapshotPath = files[ResourceMaterializationTestContext.StatePath];
        var faultPath = failDefinitions ? definitionsSnapshotPath : stateSnapshotPath;
        Assert.NotEqual(definitionsSnapshotPath, stateSnapshotPath);
        Assert.True(context.FileSystem.FileExists(faultPath));

        using (context.Validator.UsePrevalidatedPendingTurnSnapshotScope(validatedManifest))
        {
            // Only the selected physical signed resource path throws. Existing readers
            // still load detached authority and current request for every readable root.
            probe.ForbiddenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { faultPath };
            probe.Start();
            ValidationService.SpiritualConflictValidationFrame frame;
            try
            {
                frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
            }
            finally
            {
                probe.Stop();
            }

            Assert.True(frame.HasValidatedSnapshot);
            Assert.True(frame.Baseline.ResourceAcquisitionFailed);
            Assert.Null(frame.Baseline.ResourceDefinitions);
            Assert.Null(frame.Baseline.ResourceState);
            Assert.Equal(control.Candidate, frame.Candidate);
            Assert.Equal(control.Baseline.Conflict, frame.Baseline.Conflict);
            Assert.Equal(control.Baseline.Soul, frame.Baseline.Soul);
            Assert.Equal(control.Baseline.Shining, frame.Baseline.Shining);
            Assert.Equal(control.Baseline.Profiles, frame.Baseline.Profiles);
            Assert.Equal(control.SnapshotTurnNumber, frame.SnapshotTurnNumber);
            Assert.Equal(control.SnapshotDice, frame.SnapshotDice);
            Assert.Equal(control.DifficultySettings, frame.DifficultySettings);
            Assert.Equal(control.TurnRequest, frame.TurnRequest);
            Assert.Contains(probe.Events, entry => entry.Kind == "open" &&
                entry.Path == definitionsSnapshotPath);
            Assert.Equal(!failDefinitions, probe.Events.Any(entry =>
                entry.Kind == "open" && entry.Path == stateSnapshotPath));
            Assert.True(probe.Attempts(PendingTurnSnapshotAuthority.AuthorityPath) > 0);
            Assert.True(probe.Attempts(FrameRequestPath) > 0);

            probe.Start();
            var actual = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
            var replay = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
            probe.Stop();
            Assert.Empty(probe.Events);
            Assert.Contains(actual, issue => issue.Severity == IssueSeverity.Error &&
                issue.Code == "afterlife_conflict_resource_projection_missing");
            Assert.Equal(ConflictFrameIssueFingerprint.Create(actual),
                ConflictFrameIssueFingerprint.Create(replay));

            probe.Start();
            List<ValidationIssue> wrapped;
            try
            {
                wrapped = await ValidateCompleteConflictFrameAsync(context);
            }
            finally
            {
                probe.Stop();
            }
            Assert.Equal(ConflictFrameIssueFingerprint.Create(actual),
                ConflictFrameIssueFingerprint.Create(wrapped));
            Assert.Contains(probe.Events, entry => entry.Kind == "open" &&
                entry.Path == definitionsSnapshotPath);
            Assert.Equal(!failDefinitions, probe.Events.Any(entry =>
                entry.Kind == "open" && entry.Path == stateSnapshotPath));
            Assert.True(probe.Attempts(PendingTurnSnapshotAuthority.AuthorityPath) > 0);
            Assert.True(probe.Attempts(FrameRequestPath) > 0);
        }

        // Restore only the fault hook and dispose the existing scope. No disk bytes,
        // hashes, identity, authority, or rollback membership were changed for the fault.
        probe.ForbiddenPaths = null;
        var recovered = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(recovered.HasValidatedSnapshot);
        Assert.False(recovered.Baseline.ResourceAcquisitionFailed);
        Assert.Equal(control.Baseline, recovered.Baseline);
        AssertNoConflictFrameErrors(context.Validator.EvaluateSpiritualConflictValidationFrame(recovered));
    }

    private static async Task<object> ReadGenuinelyValidatedConflictFrameManifestAsync(
        ValidationService validator)
    {
        var overrideField = typeof(ValidationService).GetField(
            "_prevalidatedPendingTurnSnapshotOverride", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(overrideField);
        Assert.Null(overrideField!.GetValue(validator));

        var lookupMethod = typeof(ValidationService).GetMethod(
            "LoadValidatedPendingTurnSnapshotLookupAsync",
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(FileSystemManager.CanonicalWriteLease)], null);
        Assert.NotNull(lookupMethod);
        var pending = Assert.IsAssignableFrom<Task>(
            lookupMethod!.Invoke(validator, new object?[] { null }));
        await pending;
        var resultProperty = pending.GetType().GetProperty("Result");
        Assert.NotNull(resultProperty);
        var lookup = resultProperty!.GetValue(pending);
        Assert.NotNull(lookup);
        var statusProperty = lookup!.GetType().GetProperty("Status");
        var manifestProperty = lookup.GetType().GetProperty("Manifest");
        Assert.NotNull(statusProperty);
        Assert.NotNull(manifestProperty);
        Assert.Equal("Usable", statusProperty!.GetValue(lookup)?.ToString());
        var manifest = manifestProperty!.GetValue(lookup);
        Assert.NotNull(manifest);
        return manifest!;
    }

    private static ValidationService.SpiritualConflictFileImage FrameImage(JsonObject root) =>
        new(true, root.ToJsonString());

    private static Task<List<ValidationIssue>> ValidateCompleteConflictFrameAsync(
        ResourceMaterializationTestContext context) =>
        context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState));

    private static async Task PublishCompleteConflictFrameAsync(ResourceMaterializationTestContext context)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var plan = await PeekPlanAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.Same(plan, await context.Normalizer.BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
    }

    private sealed class ConflictFrameReadProbe
    {
        private readonly ConcurrentQueue<(string Kind, string Path)> _events = new();
        private bool _enabled;
        internal IReadOnlyList<(string Kind, string Path)> Events => _events.ToArray();
        internal HashSet<string>? ForbiddenPaths { get; set; }
        internal FileSystemManagerHooks Hooks => new()
        {
            BeforeCanonicalReadOpenAsync = path => Record("open", path),
            AfterCanonicalReadAttemptAsync = path => Record("attempt", path),
            BeforeRuntimeFileReadOpenAsync = path => Record("runtime", path),
            BeforeCanonicalExistenceFollowUpProbeAsync = path => Record("existence", path)
        };
        internal void Start() { _events.Clear(); _enabled = true; }
        internal void Stop() => _enabled = false;
        internal int Attempts(string path) => _events.Count(entry =>
            entry.Kind == "attempt" && string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
        private Task Record(string kind, string path)
        {
            if (!_enabled)
                return Task.CompletedTask;
            var normalized = path.Replace('\\', '/');
            _events.Enqueue((kind, normalized));
            if (ForbiddenPaths?.Contains(normalized) == true)
                throw new InvalidOperationException("Unexpected conflict-context read: " + normalized);
            return Task.CompletedTask;
        }
    }
}

internal static class ConflictFrameIssueFingerprint
{
    internal static string[] Create(IEnumerable<ValidationIssue> issues) =>
        issues.Select(issue =>
        {
            // This owning checker does not attach structured cross-subsystem repair context.
            // Do not silently omit it if that contract changes.
            Assert.Null(issue.FactionRepairClassification);
            Assert.Null(issue.MortalItemRepairContext);
            Assert.Null(issue.MortalLocationRepairContext);
            Assert.Null(issue.EffectRepairContext);
            Assert.Null(issue.WoundRepairContext);
            return JsonSerializer.Serialize(new
            {
                issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code,
                issue.Actor, issue.Section, issue.Expected, issue.Actual, issue.RepairHint,
                RepairTargetFiles = issue.RepairTargetFiles.ToArray()
            });
        }).ToArray(); // Preserve order, duplicates, nulls and all diagnostics, not just Error codes.
}
