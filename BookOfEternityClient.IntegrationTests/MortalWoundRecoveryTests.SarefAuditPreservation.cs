using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundRecoveryTests
{
    /// <summary>Preserves historical story audit bytes through real Mortal healing, without a Saref effect claim.</summary>
    [Fact]
    public void Publication_TerminalHealingPreservesExistingSarefMemorySuppressionAudit()
    {
        var scenario = Scenario.AtDueBoundary();
        var authored = scenario.Wound.DeepClone().AsObject();
        authored["recovery"]!["currentStepThreshold"] = 1;
        authored["display"]!["name"] = "M01 historical audit " + Guid.NewGuid().ToString("N");
        scenario = scenario with { Wound = authored };
        var fixture = Fixture.CreateForHistoricalSarefAudit(scenario, output.WriteLine, out var originalAuditBytes);
        using var owned = new OriginalFixtureCompletion(fixture.FileSystem.BasePath, fixture.Dispose, output.WriteLine);
        AssertHistoricalSarefAudit(fixture.FileSystem, originalAuditBytes, "signed_evaluation", output.WriteLine);

        // Keep the genuine full-refresh seam's existing order: raw item admission precedes Compose.
        fixture.PrepareCanonicalRefreshItemAuthority();
        fixture.ReadmitCurrentRecoveryBinding();
        AssertHistoricalSarefAudit(fixture.FileSystem, originalAuditBytes, "before_heal", output.WriteLine);
        var beforeTree = fixture.CaptureCanonicalTreeBytes();
        var beforeWound = WoundMaterializationContract.Parse(
            ReadActiveOrTerminalRecoveryWound(fixture.FileSystem, fixture.WoundId).ToJsonString(), "m01BeforeHeal");
        Assert.True(beforeWound.IsValid, Issues(beforeWound.Issues));
        Assert.Equal("active", beforeWound.Wound!.Lifecycle);
        var roots = beforeWound.Wound.Consequences.OwnedEffectSources.RootBindings
            .Select(binding => binding.EffectId).ToArray();
        Assert.NotEmpty(roots);
        using var beforeIdentityDocument = JsonDocument.Parse(
            File.ReadAllText(fixture.FileSystem.ResolvePath(EffectIdentityState.StatePath)));
        var beforeIdentities = EffectIdentityState.Parse(beforeIdentityDocument.RootElement, EffectIdentityState.StatePath);
        Assert.Empty(beforeIdentities.Issues);
        foreach (var id in roots)
        {
            Assert.True(beforeIdentities.State!.TryGetEntry(id, out var identity));
            Assert.Contains(identity.State, new[] { "active", "suspended" });
            Assert.Equal("wound", identity.Source["kind"]!.GetValue<string>());
            Assert.Equal(fixture.WoundId, identity.Source["sourceId"]!.GetValue<string>());
        }
        var resolution = Assert.IsType<MortalWoundRecoveryResolution>(Required(InvokePlan(fixture), "Resolution"));
        var composed = MortalWoundRecoveryAcceptedPlanComposer.Compose(
            fixture.FileSystem, fixture.Lease, fixture.Binding, resolution);
        Assert.Equal(MortalWoundRecoveryAcceptedPlanCompositionDisposition.Composed, composed.Disposition);
        Assert.Empty(composed.Issues);
        Assert.NotNull(composed.AcceptedPlan);
        Assert.NotNull(composed.Receipt);
        fixture.AssertCanonicalTreeBytesUnchanged(beforeTree);

        var published = fixture.PublishWithCanonicalRefresh();
        Assert.DoesNotContain(published.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Same(composed.AcceptedPlan, published.MechanicsPlan);
        Assert.Equal("healed", ReadActiveOrTerminalRecoveryWound(
            fixture.FileSystem, fixture.WoundId)["lifecycle"]!.GetValue<string>());
        fixture.AssertCarrierIdentityHistoryAgreement();
        var carrier = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            JsonNode.Parse(File.ReadAllText(fixture.FileSystem.ResolvePath(WoundCarrierCatalog.PlayerPath)))!.AsObject(),
            null, null, null, null));
        Assert.Empty(carrier.Issues);
        Assert.Equal(0, carrier.CountExactOccurrences(fixture.WoundId));
        using var afterIdentityDocument = JsonDocument.Parse(
            File.ReadAllText(fixture.FileSystem.ResolvePath(EffectIdentityState.StatePath)));
        var afterIdentities = EffectIdentityState.Parse(afterIdentityDocument.RootElement, EffectIdentityState.StatePath);
        Assert.Empty(afterIdentities.Issues);
        var playerEffects = JsonNode.Parse(File.ReadAllText(
            fixture.FileSystem.ResolvePath(EffectCarrierCatalog.PlayerPath)))!.AsObject();
        var effectCatalog = EffectCarrierCatalog.Build(new EffectCarrierCatalogInput(playerEffects, null, null, null, null, null));
        Assert.Empty(effectCatalog.Issues);
        foreach (var id in roots)
        {
            Assert.DoesNotContain(effectCatalog.Occurrences, occurrence => occurrence.EffectId == id);
            Assert.True(afterIdentities.State!.TryGetEntry(id, out var identity));
            Assert.Equal("expired", identity.State);
            var terminal = Assert.Single(identity.Transitions, transition => transition.Kind == "expire");
            Assert.Equal(fixture.Binding.Turn, terminal.Turn);
            Assert.Equal(new[] { id }, terminal.SourceEffectIds);
            Assert.Empty(terminal.ResultEffectIds);
            Assert.Same(terminal, identity.Transitions[^1]);
        }
        AssertHistoricalSarefAudit(fixture.FileSystem, originalAuditBytes, "after_heal", output.WriteLine);
        ValidateHistoricalSarefAudit(fixture.FileSystem, "after_heal", output.WriteLine);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            M01 = "historical_audit_state_survival_only", fixture.WoundId,
            HealingTurn = fixture.Binding.Turn, ActualHealed = true, PreHealOwnedRoots = roots,
            ExpectedAuditSha256 = Convert.ToHexString(SHA256.HashData(originalAuditBytes)),
            OriginalAuditBase64 = Convert.ToBase64String(originalAuditBytes),
            CanonicalRefreshIssues = published.Issues.Select(issue => new { issue.Code, issue.FilePath, issue.Severity }),
            RuntimeEffectOrSpiritualHealingClaim = false
        }));
    }

    private static byte[] ProduceHistoricalSarefAudit(FileSystemManager fs, Action<string> log)
    {
        var root = SarefMainStoryState.CreateDefaultRoot();
        for (var turn = 1; turn <= 2; turn++)
        {
            var suffix = turn == 1 ? "primary" : "sentinel";
            root = SarefMainStoryState.ApplyUpdate(root, new JsonObject
            {
                ["mode"] = SarefMainStoryState.DefeatUpdateModeRecord,
                ["resolvedAtTurn"] = turn,
                ["defeatOutcome"] = new JsonObject
                {
                    ["outcomeId"] = "m01_historical_memory_" + suffix,
                    ["outcomeType"] = SarefMainStoryState.DefeatOutcomeMemorySuppression,
                    ["sceneType"] = SarefMainStoryState.SceneMemoryAttack,
                    ["summary"] = "Историческая запись " + suffix,
                    ["gmMotivation"] = "Сохранить исход прошлого, не применять новое поражение в мире смертных."
                },
                ["memorySuppressionAudit"] = new JsonObject
                {
                    ["suppressedMemoryScope"] = "historical_scope_" + suffix,
                    ["severity"] = "partial",
                    ["summary"] = "Подавленная память " + suffix
                }
            });
        }
        AssertHistoricalSarefSemantics(root);
        var fullPath = fs.ResolvePath(SarefMainStoryState.StatePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        ValidateHistoricalSarefAudit(fs, "producer_before_first_signature", log);
        var original = File.ReadAllBytes(fullPath);
        log(JsonSerializer.Serialize(new { M01Stage = "producer_before_first_signature", fs.BasePath,
            AuditSha256 = Convert.ToHexString(SHA256.HashData(original)), AuditBytes = original.Length }));
        return original;
    }

    private static void AssertHistoricalSarefAudit(FileSystemManager fs, byte[] expected, string stage, Action<string> log)
    {
        var path = fs.ResolvePath(SarefMainStoryState.StatePath);
        Assert.True(File.Exists(path));
        var actual = File.ReadAllBytes(path);
        log(JsonSerializer.Serialize(new { M01Stage = stage, AuditSha256 = Convert.ToHexString(SHA256.HashData(actual)),
            AuditBytes = actual.Length, ActualAuditBase64 = Convert.ToBase64String(actual) }));
        Assert.True(expected.AsSpan().SequenceEqual(actual), "M01 historical audit physical bytes changed at " + stage);
        AssertHistoricalSarefSemantics(JsonNode.Parse(actual)!.AsObject());
    }

    private static void AssertHistoricalSarefSemantics(JsonObject root)
    {
        Assert.False(root.ContainsKey(SarefMainStoryState.ResponseField));
        Assert.False(root.ContainsKey(SarefMainStoryState.StateResponseField));
        var rows = root["defeatOutcomes"]!.AsArray();
        Assert.Equal(2, rows.Count);
        for (var turn = 1; turn <= 2; turn++)
        {
            var suffix = turn == 1 ? "primary" : "sentinel";
            var row = Assert.IsType<JsonObject>(rows[turn - 1]);
            Assert.Equal("m01_historical_memory_" + suffix, row["outcomeId"]!.GetValue<string>());
            Assert.Equal(SarefMainStoryState.DefeatOutcomeMemorySuppression, row["outcomeType"]!.GetValue<string>());
            Assert.Equal(SarefMainStoryState.SceneMemoryAttack, row["sceneType"]!.GetValue<string>());
            Assert.Equal(turn, row["resolvedAtTurn"]!.GetValue<int>());
            Assert.True(turn < 42);
            Assert.Equal("Историческая запись " + suffix, row["summary"]!.GetValue<string>());
            var audit = Assert.IsType<JsonObject>(row["memorySuppressionAudit"]);
            Assert.Equal("historical_scope_" + suffix, audit["suppressedMemoryScope"]!.GetValue<string>());
            Assert.Equal("partial", audit["severity"]!.GetValue<string>());
            Assert.Equal("Подавленная память " + suffix, audit["summary"]!.GetValue<string>());
        }
    }

    private static void ValidateHistoricalSarefAudit(FileSystemManager fs, string stage, Action<string> log)
    {
        var issues = new ValidationService(fs, NullLogger<ValidationService>.Instance)
            .ValidateGameStateAsync(IntegrationValidationProfiles.SarefStory).GetAwaiter().GetResult();
        log(JsonSerializer.Serialize(new { M01SarefValidation = stage,
            Issues = issues.Select(issue => new { issue.Code, issue.FilePath, issue.Severity, issue.Message }) }));
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error &&
            (issue.FilePath.StartsWith(SarefMainStoryState.StatePath, StringComparison.Ordinal) ||
             issue.Code?.StartsWith("saref_", StringComparison.Ordinal) == true));
    }

    private sealed partial class Fixture
    {
        internal static Fixture CreateForHistoricalSarefAudit(Scenario scenario, Action<string> log, out byte[] originalBytes)
        {
            Assert.False(PreparedRecoveryTemplates.ContainsKey(GetPreparationKey(scenario)));
            Assert.False(PreparedRecoverySources.ContainsKey(GetSourcePreparationKey(scenario)));
            byte[]? expected = null;
            string? sourceRoot = null;
            var key = "m01_private_source_" + Guid.NewGuid().ToString("N");
            var source = PrepareRecoverySourceTemplate(scenario, key, fs =>
            {
                sourceRoot = fs.BasePath;
                Assert.False(File.Exists(fs.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)));
                expected = ProduceHistoricalSarefAudit(fs, log);
            });
            Assert.Equal(1, SourcePreparationCounts[key]);
            Assert.NotNull(expected);
            Assert.False(Directory.Exists(Assert.IsType<string>(sourceRoot)));
            var completedSourceBytes = source.Tree.ReadFileBytes(SarefMainStoryState.StatePath);
            Assert.True(expected.AsSpan().SequenceEqual(completedSourceBytes));
            log(JsonSerializer.Serialize(new { M01Stage = "completed_source", SourceRoot = sourceRoot,
                SourceRootRemoved = true, SourceAuditSha256 = Convert.ToHexString(SHA256.HashData(completedSourceBytes)),
                PrivateSourcePreparationCount = SourcePreparationCounts[key] }));
            var fixture = CreateUncached(scenario, source);
            try
            {
                Assert.NotEqual(sourceRoot, fixture.FileSystem.BasePath);
                AssertHistoricalSarefAudit(fixture.FileSystem, expected, "fresh_evaluation_factory", log);
                Assert.False(PreparedRecoveryTemplates.ContainsKey(GetPreparationKey(scenario)));
                Assert.False(PreparedRecoverySources.ContainsKey(GetSourcePreparationKey(scenario)));
                originalBytes = expected.ToArray();
                return fixture;
            }
            catch
            {
                fixture.Dispose();
                throw;
            }
        }
    }
}
