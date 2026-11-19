using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Exercises the existing physical read admission; no generated response or game turn.</summary>
public sealed class OrdinaryPreSendValidationTests
{
    [Theory]
    [InlineData("input/turn_request.json")]
    [InlineData("game_state/control/pending_turn_snapshot.json")]
    [InlineData("game_state/control/pending_turn_snapshot.authority.json")]
    [InlineData("game_state/control/validation_repair_request.json")]
    [InlineData("game_state/control/validation_repair_ready.json")]
    [InlineData("ready/turn_complete.json")]
    [InlineData("ready/turn_error.json")]
    public async Task PreHealth_ActiveOrMalformedMarkerRefusesWithoutRemoval(string path)
    {
        using var f = new Fixture();
        await f.InitializeAsync();
        var full = f.Files.ResolvePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, "{}");
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => f.Validator.RequireOrdinaryPreSendIdleAsync());
        Assert.Equal("{}", await File.ReadAllTextAsync(full));
    }

    [Fact]
    public async Task PreHealth_OrphanSnapshotDirectoryRefuses()
    {
        using var f = new Fixture();
        await f.InitializeAsync();
        var path = f.Files.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotDirectory);
        Directory.CreateDirectory(path);
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => f.Validator.RequireOrdinaryPreSendIdleAsync());
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task PreHealth_InitializedFirstTurnNeedsNoRetiredOriginal()
    {
        using var f = new Fixture();
        await f.InitializeAsync();
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await f.Validator.RequireOrdinaryPreSendIdleAsync();
    }

    [Fact]
    public async Task PreHealth_MissingGenerationRefusesWithoutCreatingIt()
    {
        using var f = new Fixture();
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => f.Validator.RequireOrdinaryPreSendIdleAsync());
        await using var lease = await f.Files.AcquireCanonicalWriteLeaseAsync();
        Assert.Null(f.Files.ReadExistingSessionGeneration(lease));
    }

    [Fact]
    public async Task PreHealth_NoAmbientOwnerRefuses()
    {
        using var f = new Fixture();
        await f.InitializeAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => f.Validator.RequireOrdinaryPreSendIdleAsync());
    }

    [Fact]
    public async Task FullAll_InitializedMortalStateIsValidAndLeavesStrictReaderUnchanged()
    {
        using var f = new Fixture();
        await f.SeedAsync();
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await f.Validator.RequireOrdinaryPreSendIdleAsync();
        var issues = await f.Validator.ValidateOrdinaryPreSendGameStateAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var strict = await f.Validator.ValidateGameStateAsync();
        Assert.DoesNotContain(strict, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task FullAll_MarkerAppearingAfterPreHealthRefuses()
    {
        using var f = new Fixture();
        await f.InitializeAsync();
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await f.Validator.RequireOrdinaryPreSendIdleAsync();
        f.Raw("game_state/control/validation_repair_ready.json", "{}");
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.Validator.ValidateOrdinaryPreSendGameStateAsync());
        Assert.Contains("absent marker", error.Message);
        Assert.True(File.Exists(f.Files.ResolvePath("game_state/control/validation_repair_ready.json")));
    }

    [Theory]
    [InlineData("game_state/world/world_map.json")]
    [InlineData("game_state/world/location_identity_index.json")]
    [InlineData("game_state/npcs/npc_core.json")]
    public async Task FullAll_ClosingDependencyDriftCannotReturnIssues(string changed)
    {
        using var f = new Fixture();
        await f.SeedAsync();
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var mutated = false;
        f.OnRead = path =>
        {
            if (!mutated && path == "game_state/meta/soul_state.json")
            {
                mutated = true;
                // The three opening dependencies precede soul; preserve JSON meaning but change exact bytes.
                File.AppendAllText(f.Files.ResolvePath(changed), " ");
            }
            return Task.CompletedTask;
        };
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.Validator.ValidateOrdinaryPreSendGameStateAsync());
        Assert.True(mutated);
        Assert.Contains("dependencies changed", error.Message);
    }

    [Fact]
    public async Task FullAll_GenerationLossCannotReturnIssues()
    {
        using var f = new Fixture();
        await f.SeedAsync();
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var changed = false;
        f.OnRead = path =>
        {
            if (!changed && path == "game_state/meta/soul_state.json")
            {
                changed = true;
                File.WriteAllText(f.Files.SessionGenerationPath,
                    System.Text.Json.JsonSerializer.Serialize(new { SchemaVersion = 1, GenerationId = Guid.NewGuid().ToString("N") }));
            }
            return Task.CompletedTask;
        };
        await Assert.ThrowsAnyAsync<Exception>(() => f.Validator.ValidateOrdinaryPreSendGameStateAsync());
        Assert.True(changed);
    }

    [Fact]
    public async Task FullAll_ReentrantConsumerRefusesBeforeNestedRead()
    {
        using var f = new Fixture();
        await f.SeedAsync();
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var entered = false;
        f.OnRead = async path =>
        {
            if (!entered && path == "game_state/meta/soul_state.json")
            {
                entered = true;
                await f.Validator.RequireOrdinaryPreSendIdleAsync();
            }
        };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Validator.ValidateOrdinaryPreSendGameStateAsync());
        Assert.True(entered);
        Assert.Contains("conflicts", error.Message);
    }

    [Fact]
    public async Task FullAll_NonMortalCannotAcquireCanonicalInterpretation()
    {
        using var f = new Fixture();
        await f.InitializeAsync();
        f.Raw("game_state/meta/soul_state.json", "{\"currentRealm\":\"Chaos Sea\"}");
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.Validator.ValidateOrdinaryPreSendGameStateAsync());
        Assert.Contains("Mortal-only", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FullAll_ClosingMarkerOrDirectoryAppearingRefuses(bool directory)
    {
        using var f = new Fixture();
        await f.SeedAsync();
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var inserted = false;
        f.OnRead = path =>
        {
            if (!inserted && path == "game_state/meta/soul_state.json")
            {
                inserted = true;
                if (directory) Directory.CreateDirectory(f.Files.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotDirectory));
                else f.Raw("ready/turn_error.json", "{}");
            }
            return Task.CompletedTask;
        };
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.Validator.ValidateOrdinaryPreSendGameStateAsync());
        Assert.True(inserted);
        Assert.Contains(directory ? "orphan snapshot directory" : "absent marker", error.Message);
    }

    [Fact]
    public async Task FullAll_RawResponseCannotConsumeOrRetainCanonicalPurpose()
    {
        using var f = new Fixture();
        await f.SeedAsync();
        var core = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(f.Files.ResolvePath("game_state/npcs/npc_core.json")))!;
        var actor = core["NPCsInScene"]![0]!.DeepClone();
        actor["inventory"] = new System.Text.Json.Nodes.JsonArray();
        using var response = System.Text.Json.JsonDocument.Parse(
            new System.Text.Json.Nodes.JsonObject { ["UpdateNPCs"] = new System.Text.Json.Nodes.JsonArray(actor) }.ToJsonString());
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var attempted = false;
        f.OnRead = path =>
        {
            if (!attempted && path == "game_state/meta/soul_state.json")
            {
                attempted = true;
                Assert.Throws<InvalidOperationException>(() => f.Validator.ValidateResponse(response.RootElement));
            }
            return Task.CompletedTask;
        };
        var issues = await f.Validator.ValidateOrdinaryPreSendGameStateAsync();
        Assert.True(attempted);
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        f.OnRead = null;
        var rawIssues = f.Validator.ValidateResponse(response.RootElement);
        Assert.Contains(rawIssues, issue => issue.Code == "npc_existing_inventory_resend_forbidden");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-pre-send-" + Guid.NewGuid().ToString("N"));
        internal readonly FileSystemManager Files;
        internal readonly ValidationService Validator;
        internal Func<string, Task>? OnRead;
        internal Fixture()
        {
            Directory.CreateDirectory(_root);
            Files = new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks { AfterCanonicalReadInitialValidationAsync = path => OnRead?.Invoke(path) ?? Task.CompletedTask });
            Files.EnsureDirectoryStructure();
            Validator = new(Files, NullLogger<ValidationService>.Instance);
        }
        internal async Task InitializeAsync()
        {
            await using var lease = await Files.AcquireCanonicalWriteLeaseAsync();
            _ = Files.GetOrCreateSessionGeneration(lease);
        }
        internal void Raw(string path, string value)
        {
            var target = Files.ResolvePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, value);
        }
        internal async Task SeedAsync()
        {
            await MortalLiveSeedBuilder.PrepareAsync(Files, TestRepoPaths.BaseSessionRoot,
                MortalLiveSeedBuilder.SeedActorsAsync, stateOnly: true);
            await InitializeAsync();
        }
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }
}
