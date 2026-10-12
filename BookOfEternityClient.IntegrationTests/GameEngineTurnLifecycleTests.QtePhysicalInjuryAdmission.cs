using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const int PhysicalInjurySourceTurn = 42;
    private const string PhysicalInjuryPath = "output/qte_offer.json.terminalOutcomes[0].physicalInjury";

    /// <summary>Admits raw declarations through a genuine engine snapshot and retains both branches before selection.</summary>
    /// <param name="declared">Whether this offer opts into the physical injury contract.</param>
    /// <param name="maximum">The first branch's source maximum; the other branch uses the opposite bound.</param>
    /// <returns>A task completing after admission, frozen offer and unchanged resource/context assertions.</returns>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 4)]
    public async Task QtePhysicalInjury_OrdinaryAdmissionFreezesExactDeclaredBranches(bool declared, int maximum)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var fixture = await PreparePhysicalInjuryAdmissionAsync();
        var raw = BuildPhysicalInjuryOffer(declared, maximum);
        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, raw.ToJsonString());
        var protectedBefore = await CapturePhysicalInjuryProtectedBytesAsync(includeOfferAndFreeze: false);
        var issues = await fixture.Validator.ValidateAcceptedTurnQteOfferAsync();
        WritePhysicalInjuryDiagnostic("lawful_admission", new { declared, maximum, issues });
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        var offer = Assert.IsType<QteSceneService.QteOffer>(await fixture.Service.TryReadOfferAsync());
        offer = await fixture.Service.BindAcceptedTurnAuthorityAsync(offer, PhysicalInjurySourceTurn);
        await fixture.Service.BeginAcceptedSceneAsync(offer, PhysicalInjurySourceTurn);

        var runtime = JsonNode.Parse((await _fs.ReadFileAsync(QteSceneService.QteRuntimePath))!)!.AsObject();
        var frozen = Assert.IsType<JsonObject>(runtime["activeScene"]?["offer"]);
        var frozenContinuation = JsonNode.Parse((await _fs.ReadFileAsync(QteDeferredEffectContinuation.StatePath))!)!.AsObject();
        WritePhysicalInjuryDiagnostic("frozen_offer", new
        {
            declared, maximum, FrozenBranches = frozen["terminalOutcomes"],
            ContinuationState = frozenContinuation["state"], OfferFingerprint = frozenContinuation["offerFingerprint"],
            AuthorityFingerprint = frozenContinuation["authorityFingerprint"], AcceptedSourceTurn = frozenContinuation["acceptedSourceTurn"]
        });
        AssertPhysicalInjuryBranches(raw, frozen, declared);
        Assert.False(_fs.FileExists(QteSceneService.QteOfferPath));
        var active = Assert.IsType<QteSceneService.ActiveQteSceneState>((await fixture.Service.ReadRuntimeStateAsync()).ActiveScene);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            var continuation = await QteDeferredEffectContinuation.ValidateForTerminalSelectionAsync(
                _fs, lease, active, active.Offer, PhysicalInjurySourceTurn);
            Assert.Equal("armed", continuation["state"]!.GetValue<string>());
            Assert.Equal(fixture.Request.SessionId, continuation["sessionId"]!.GetValue<string>());
            Assert.Equal(PhysicalInjurySourceTurn, continuation["acceptedSourceTurn"]!.GetValue<int>());
        }
        await AssertPhysicalInjuryProtectedBytesAsync(protectedBefore);
        Assert.False(_fs.FileExists(ResourceMaterializationContract.CommandPath));
        Assert.False(_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
    }

    /// <summary>Rejects malformed raw injury declarations before any QTE freeze or canonical mutation.</summary>
    /// <param name="mutation">One independent violation of the closed declaration or selected resource binding.</param>
    /// <returns>A task completing after a causal raw refusal and exact owned-state preservation.</returns>
    [Theory]
    [InlineData("null")]
    [InlineData("missing_schema")]
    [InlineData("schema_two")]
    [InlineData("missing_ordinal")]
    [InlineData("ordinal_zero")]
    [InlineData("ordinal_outside")]
    [InlineData("ordinal_fraction")]
    [InlineData("missing_maximum")]
    [InlineData("maximum_zero")]
    [InlineData("maximum_five")]
    [InlineData("maximum_string")]
    [InlineData("missing_cause")]
    [InlineData("cause_empty")]
    [InlineData("cause_padded")]
    [InlineData("cause_long")]
    [InlineData("cause_control")]
    [InlineData("missing_locations")]
    [InlineData("locations_empty")]
    [InlineData("locations_duplicate")]
    [InlineData("locations_unknown")]
    [InlineData("locations_null")]
    [InlineData("unknown_field")]
    [InlineData("duplicate_field")]
    [InlineData("confusable_field")]
    [InlineData("wrong_case_field")]
    [InlineData("non_health")]
    [InlineData("unselected_invalid")]
    public async Task QtePhysicalInjury_OrdinaryAdmissionRejectsMalformedRawDeclaration(string mutation)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var fixture = await PreparePhysicalInjuryAdmissionAsync();
        var raw = MutatePhysicalInjuryOffer(mutation);
        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, raw);
        var before = await CapturePhysicalInjuryProtectedBytesAsync(includeOfferAndFreeze: true);
        var issues = await fixture.Validator.ValidateAcceptedTurnQteOfferAsync();
        Exception? parseFailure = null;
        if (!issues.Any(issue => issue.Severity == IssueSeverity.Error))
            parseFailure = await Record.ExceptionAsync(async () => { await fixture.Service.TryReadOfferAsync(); });
        WritePhysicalInjuryDiagnostic("malformed_admission", new { mutation, issues, ParseFailure = parseFailure?.ToString() });
        await AssertPhysicalInjuryProtectedBytesAsync(before);
        Assert.False(_fs.FileExists(QteSceneService.QteRuntimePath));
        Assert.False(_fs.FileExists(QteDeferredEffectContinuation.StatePath));

        var expectedPath = mutation == "unselected_invalid"
            ? PhysicalInjuryPath.Replace("[0]", "[1]", StringComparison.Ordinal)
            : PhysicalInjuryPath;
        var errors = issues.Where(issue => issue.Severity == IssueSeverity.Error).ToArray();
        // Preserve an existing causal duplicate-property refusal rather than inventing a new diagnostic requirement.
        var duplicateRefusal = (mutation is "duplicate_field" or "confusable_field") &&
            parseFailure is InvalidDataException &&
            parseFailure.ToString().Contains("duplicate JSON property", StringComparison.Ordinal) &&
            parseFailure.ToString().Contains("physicalInjury", StringComparison.Ordinal);
        Assert.True(duplicateRefusal || errors.Any(issue =>
                issue.FilePath.StartsWith(expectedPath, StringComparison.Ordinal)),
            $"Missing physical-injury admission refusal for {mutation}; actual issues=" +
            JsonSerializer.Serialize(issues) + "; parser=" + parseFailure);
        Assert.All(errors, issue => Assert.True(
            issue.FilePath.StartsWith(expectedPath, StringComparison.Ordinal),
            "Unrelated ordinary failure is not injury-contract RED: " + JsonSerializer.Serialize(issue)));
        if (!duplicateRefusal) Assert.Null(parseFailure);
    }

    /// <summary>Requires a frozen declaration to participate in the existing terminal offer fingerprint.</summary>
    /// <returns>A task completing after a real unchanged-offer validation and changed-maximum rejection without writes.</returns>
    [Fact]
    public async Task QtePhysicalInjury_FrozenMaximumCannotChangeBeforeTerminalSelection()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var fixture = await PreparePhysicalInjuryAdmissionAsync();
        var raw = BuildPhysicalInjuryOffer(declared: true, maximum: 2);
        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, raw.ToJsonString());
        var issues = await fixture.Validator.ValidateAcceptedTurnQteOfferAsync();
        WritePhysicalInjuryDiagnostic("tamper_admission", new { issues });
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var offer = Assert.IsType<QteSceneService.QteOffer>(await fixture.Service.TryReadOfferAsync());
        offer = await fixture.Service.BindAcceptedTurnAuthorityAsync(offer, PhysicalInjurySourceTurn);
        await fixture.Service.BeginAcceptedSceneAsync(offer, PhysicalInjurySourceTurn);
        var runtime = await fixture.Service.ReadRuntimeStateAsync();
        var active = Assert.IsType<QteSceneService.ActiveQteSceneState>(runtime.ActiveScene);
        var frozen = JsonSerializer.SerializeToNode(active.Offer, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!.AsObject();
        WritePhysicalInjuryDiagnostic("tamper_frozen_offer", new { FrozenBranches = frozen["terminalOutcomes"] });
        AssertPhysicalInjuryBranches(raw, frozen, declared: true);
        var before = await CapturePhysicalInjuryProtectedBytesAsync(includeOfferAndFreeze: true);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            await QteDeferredEffectContinuation.ValidateForTerminalSelectionAsync(
                _fs, lease, active, active.Offer, PhysicalInjurySourceTurn);
            frozen["terminalOutcomes"]![0]!["physicalInjury"]!["maximumSeverityRank"] = 3;
            var changed = JsonSerializer.Deserialize<QteSceneService.QteOffer>(
                frozen.ToJsonString(), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
                QteDeferredEffectContinuation.ValidateForTerminalSelectionAsync(
                    _fs, lease, active, changed, PhysicalInjurySourceTurn));
            WritePhysicalInjuryDiagnostic("frozen_tamper_rejected", new { Failure = failure.ToString() });
            Assert.Contains("qte_deferred_continuation_offer_mismatch", failure.Message, StringComparison.Ordinal);
        }
        await AssertPhysicalInjuryProtectedBytesAsync(before);
    }

    /// <summary>Uses the real engine snapshot owner and actual physical request; never injects a prevalidated manifest.</summary>
    /// <returns>The engine's validator and QTE service over this test's independently bootstrapped health owner.</returns>
    private async Task<(ValidationService Validator, QteSceneService Service, TurnRequest Request)>
        PreparePhysicalInjuryAdmissionAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}");
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", "{\"qteEventsEnabled\":true}");
        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", "{\"currentTimeInMinutes\":840}");
        var request = new TurnRequest
        {
            SessionId = "session_physical_injury_admission", RequestId = "request_physical_injury_admission",
            TurnNumber = PhysicalInjurySourceTurn, PlayerAction = "Укрыться от падающих камней.",
            Timestamp = "2026-10-12T00:00:00Z", PreGeneratedDices1d20 = [13, 7],
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };
        await _fs.WriteFileAtomicAsync("game_state/history/chat_log.json", new JsonObject
        {
            ["sessionId"] = request.SessionId, ["entries"] = new JsonArray()
        }.ToJsonString());
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1, turn: 41, permanentStrength: 20, permanentConstitution: 20,
            permanentIntelligence: 20, permanentWisdom: 20, permanentFaith: 20);
        Assert.True(resources.IsValid, JsonSerializer.Serialize(resources.Issues));
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.DefinitionsPath, resources.Definitions!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.StatePath, resources.State!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.HistoryPath, resources.History!.ToCanonicalJson());
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            resources.Definitions, path => _fs.ReadFileAsync(path), resources.State, resources.History,
            CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(authority.IsValid, JsonSerializer.Serialize(authority.Issues));
        await _fs.WriteFileAtomicAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, authority.CanonicalAuthorityJson!);
        var health = Assert.Single(resources.State.Entries, entry =>
            entry.Coordinate.OwnerKind == ResourceOwnerKind.Player &&
            entry.Coordinate.ResourceOwnerId == "player_current" && entry.Coordinate.ResourceKey == "health");
        Assert.True(health.Current > 10m);

        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        await _fs.WriteFileAtomicAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
        var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "physical-injury-admission");
        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollback,
            QteSceneService.OrdinaryPlayerTurnSourceLabel);
        var resolution = await InvokePrivateTaskResultAsync(engine, "ResolveActivePendingTurnSnapshotContextAsync");
        Assert.Equal("Usable", resolution.GetType().GetProperty("Status")!.GetValue(resolution)!.ToString());
        var manifest = JsonNode.Parse((await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
        Assert.Equal(QteSceneService.OrdinaryPlayerTurnSourceLabel, manifest["sourceLabel"]!.GetValue<string>());
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
                [ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                 ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                 "game_state/meta/soul_state.json", "game_state/core/game_settings.json"]);
            Assert.True(read.Success, JsonSerializer.Serialize(read.Issues));
            Assert.Equal(request.SessionId, read.Snapshot!.SessionId);
            Assert.Equal(request.RequestId, read.Snapshot.RequestId);
            Assert.Equal(PhysicalInjurySourceTurn, read.Snapshot.TurnNumber);
            Assert.False(string.IsNullOrWhiteSpace(read.Snapshot.SnapshotToken));
            WritePhysicalInjuryDiagnostic("genuine_snapshot", new
            {
                read.Snapshot.SessionId, read.Snapshot.RequestId, read.Snapshot.SnapshotToken,
                read.Snapshot.TurnNumber, SourceLabel = manifest["sourceLabel"], Health = health.Coordinate, health.Current
            });
        }
        return (GetPrivateField<ValidationService>(engine, "_validator"),
            GetPrivateField<QteSceneService>(engine, "_qteSceneService"), request);
    }

    /// <summary>Authors ordinary raw offer JSON, keeping the new declaration out of production DTO construction.</summary>
    /// <param name="declared">Adds the proposed raw declaration only when true.</param>
    /// <param name="maximum">The first outcome maximum; the second uses five minus this value.</param>
    /// <returns>A raw two-branch offer with equal health damage and independently authored maxima.</returns>
    private static JsonObject BuildPhysicalInjuryOffer(bool declared, int maximum)
    {
        JsonObject Outcome(int ordinal) => new()
        {
            ["outcomeId"] = "outcome_" + ordinal, ["title"] = "Каменный удар",
            ["finalNarrative"] = "Камень задевает героя.", ["gmSummary"] = "Выбранный исход обвала.",
            ["responseFragment"] = new JsonObject
            {
                ["response"] = "Камень задевает героя.", ["experienceGained"] = ordinal == 1 ? 1 : 0,
                ["resourceChanges"] = new JsonArray(new JsonObject
                {
                    ["operation"] = "damage", ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
                    ["resourceKey"] = "health", ["amount"] = 5,
                    ["source"] = new JsonObject { ["kind"] = "narrative_outcome" },
                    ["eventRef"] = $"turn_{PhysicalInjurySourceTurn}:qte_terminal:{ordinal}:resource:1",
                    ["reason"] = "Прямой удар камня."
                })
            }
        };
        var outcomes = new JsonArray(Outcome(1), Outcome(2));
        if (declared)
            for (var index = 0; index < outcomes.Count; index++)
                outcomes[index]!["physicalInjury"] = new JsonObject
                {
                    ["schemaVersion"] = 1, ["resourceCommandOrdinal"] = 1,
                    ["maximumSeverityRank"] = index == 0 ? maximum : 5 - maximum,
                    ["cause"] = "Прямой удар камня.",
                    ["allowedLocationKinds"] = new JsonArray("anatomical", "other")
                };
        JsonObject Route(int ordinal) => new() { ["terminalOutcomeId"] = "outcome_" + ordinal };
        return new JsonObject
        {
            ["qteId"] = "qte_physical_injury_admission", ["title"] = "Обвал", ["offerText"] = "Укрыться от камней.",
            ["introNarrative"] = "Камни падают со свода.", ["startChapterId"] = "impact",
            ["chapters"] = new JsonArray(new JsonObject
            {
                ["chapterId"] = "impact", ["title"] = "Удар", ["narrative"] = "Осколки летят вниз.",
                ["actions"] = new JsonArray(new JsonObject
                {
                    ["actionId"] = "brace", ["label"] = "Укрыться", ["successText"] = "Удар ослаблен.",
                    ["partialText"] = "Укрытие не помогло.", ["failText"] = "Камень попал в героя.",
                    ["check"] = new JsonObject { ["type"] = "BranchChoice", ["primaryCharacteristic"] = "dexterity", ["baseDifficulty"] = 1,
                        ["config"] = new JsonObject { ["choiceGrade"] = "success" } },
                    ["routing"] = new JsonObject { ["success"] = Route(1), ["partial"] = Route(2), ["fail"] = Route(2) }
                })
            }),
            ["terminalOutcomes"] = outcomes
        };
    }

    /// <summary>Produces raw duplicate fields without normalizing them through a JSON object parser.</summary>
    /// <param name="mutation">One closed test mutation identifier.</param>
    /// <returns>The unnormalized raw offer, including duplicate fields when requested.</returns>
    private static string MutatePhysicalInjuryOffer(string mutation)
    {
        var offer = BuildPhysicalInjuryOffer(declared: true, maximum: 2);
        var outcome = offer["terminalOutcomes"]![0]!.AsObject();
        var injury = outcome["physicalInjury"]!.AsObject();
        switch (mutation)
        {
            case "null": outcome["physicalInjury"] = null; break;
            case "missing_schema": injury.Remove("schemaVersion"); break;
            case "schema_two": injury["schemaVersion"] = 2; break;
            case "missing_ordinal": injury.Remove("resourceCommandOrdinal"); break;
            case "ordinal_zero": injury["resourceCommandOrdinal"] = 0; break;
            case "ordinal_outside": injury["resourceCommandOrdinal"] = 2; break;
            case "ordinal_fraction": injury["resourceCommandOrdinal"] = 1.5m; break;
            case "missing_maximum": injury.Remove("maximumSeverityRank"); break;
            case "maximum_zero": injury["maximumSeverityRank"] = 0; break;
            case "maximum_five": injury["maximumSeverityRank"] = 5; break;
            case "maximum_string": injury["maximumSeverityRank"] = "II"; break;
            case "missing_cause": injury.Remove("cause"); break;
            case "cause_empty": injury["cause"] = ""; break;
            case "cause_padded": injury["cause"] = " удар "; break;
            case "cause_long": injury["cause"] = new string('а', 2001); break;
            case "cause_control": injury["cause"] = "удар\nкамня"; break;
            case "missing_locations": injury.Remove("allowedLocationKinds"); break;
            case "locations_empty": injury["allowedLocationKinds"] = new JsonArray(); break;
            case "locations_duplicate": injury["allowedLocationKinds"] = new JsonArray("anatomical", "anatomical"); break;
            case "locations_unknown": injury["allowedLocationKinds"] = new JsonArray("head"); break;
            case "locations_null": injury["allowedLocationKinds"] = null; break;
            case "unknown_field": injury["hardMaximumSeverityRank"] = 4; break;
            case "duplicate_field": return offer.ToJsonString().Replace("\"maximumSeverityRank\":2", "\"maximumSeverityRank\":2,\"maximumSeverityRank\":3", StringComparison.Ordinal);
            case "confusable_field": injury["MaximumSeverityRank"] = 2; break;
            case "wrong_case_field": injury.Remove("maximumSeverityRank"); injury["MaximumSeverityRank"] = 2; break;
            case "non_health": outcome["responseFragment"]!["resourceChanges"]![0]!["resourceKey"] = "poise"; break;
            case "unselected_invalid": offer["terminalOutcomes"]![1]!["physicalInjury"]!["maximumSeverityRank"] = 5; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown injury test mutation.");
        }
        return offer.ToJsonString();
    }

    /// <summary>Checks semantic retention of both predeclared branches without manufacturing a source result.</summary>
    /// <param name="raw">The submitted raw offer before typed reading.</param>
    /// <param name="frozen">The actual persisted active offer.</param>
    /// <param name="declared">Whether declaration retention or absence is required.</param>
    private static void AssertPhysicalInjuryBranches(JsonObject raw, JsonObject frozen, bool declared)
    {
        var expected = raw["terminalOutcomes"]!.AsArray();
        var actual = frozen["terminalOutcomes"]!.AsArray();
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index]!["outcomeId"]!.GetValue<string>(), actual[index]!["outcomeId"]!.GetValue<string>());
            if (declared)
                Assert.True(JsonNode.DeepEquals(expected[index]!["physicalInjury"], actual[index]!["physicalInjury"]),
                    $"Lawful physicalInjury was lost or changed before selection at terminalOutcomes[{index}].");
            else
                Assert.False(actual[index]!.AsObject().ContainsKey("physicalInjury"));
        }
    }

    /// <summary>Captures real snapshot payloads, resource roots and optional QTE/wound controls as exact bytes.</summary>
    /// <param name="includeOfferAndFreeze">Includes QTE surfaces when the operation must not change them.</param>
    /// <returns>Exact present or absent bytes for all protected paths.</returns>
    private async Task<IReadOnlyDictionary<string, byte[]?>> CapturePhysicalInjuryProtectedBytesAsync(bool includeOfferAndFreeze)
    {
        var paths = ResourceMaterializationTestContext.AllResourcePaths
            .Concat(WoundAcceptedTurnSnapshotContract.RequiredPaths)
            .Concat(["input/turn_request.json", "game_state/control/pending_turn_snapshot.json", PendingTurnSnapshotAuthority.AuthorityPath, QteSceneService.QteHistoryPath])
            .Concat(includeOfferAndFreeze ? [QteSceneService.QteOfferPath, QteSceneService.QteRuntimePath, QteDeferredEffectContinuation.StatePath] : Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
        var manifest = JsonNode.Parse((await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
        foreach (var pair in manifest["files"]!.AsObject())
        {
            paths.Add(pair.Key);
            paths.Add(pair.Value!.GetValue<string>());
        }
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in paths) result.Add(path, await _fs.ReadFileBytesAsync(path));
        return result;
    }

    /// <summary>Requires byte-exact preservation, including originally absent command and canonical paths.</summary>
    /// <param name="before">The exact owned before-images captured before the operation.</param>
    /// <returns>A task completing after every byte comparison.</returns>
    private async Task AssertPhysicalInjuryProtectedBytesAsync(IReadOnlyDictionary<string, byte[]?> before)
    {
        foreach (var pair in before)
            Assert.Equal(pair.Value, await _fs.ReadFileBytesAsync(pair.Key));
        WritePhysicalInjuryDiagnostic("protected_bytes_unchanged", new { Count = before.Count });
    }

    /// <summary>Retains first raw diagnostics and milestones in the ordinary xUnit/TRX output.</summary>
    /// <param name="boundary">The observed admission or freeze boundary.</param>
    /// <param name="observation">Structured actual result and diagnostic values.</param>
    private void WritePhysicalInjuryDiagnostic(string boundary, object observation) =>
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { Boundary = boundary, Observation = observation }));
}
