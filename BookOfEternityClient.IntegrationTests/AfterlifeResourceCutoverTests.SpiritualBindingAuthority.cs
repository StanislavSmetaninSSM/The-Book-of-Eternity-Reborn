using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private const string BindingAuthoritySoulPath = "game_state/meta/soul_state.json";
    private const string BindingAuthorityArtPath = BindingAuthoritySoulPath + ".afterlifeCombatProfile.artTiers.";

    [Theory]
    [InlineData("binding")]
    [InlineData("force_binding")]
    public async Task BindingAuthority_RawProfileAcceptsExactTierThree(string operation)
    {
        var errors = await ValidateBindingAuthorityRawProfileAsync(operation, "3");
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("unknown_binding")]
    [InlineData("recover_spiritual_power")]
    public async Task BindingAuthority_RawProfileRejectsUnsupportedArt(string operation)
    {
        var error = Assert.Single(await ValidateBindingAuthorityRawProfileAsync(operation, "3"));
        Assert.Equal("afterlife_combat_profile_unknown_art", error.Code);
        Assert.Equal(BindingAuthorityArtPath + operation, error.FilePath);
        Assert.Equal(operation, error.Actual);
    }

    [Theory]
    [InlineData("binding", "-1")]
    [InlineData("binding", "6")]
    [InlineData("binding", "1.5")]
    [InlineData("binding", "\"3\"")]
    [InlineData("binding", "null")]
    [InlineData("force_binding", "-1")]
    [InlineData("force_binding", "6")]
    [InlineData("force_binding", "1.5")]
    [InlineData("force_binding", "\"3\"")]
    [InlineData("force_binding", "null")]
    public async Task BindingAuthority_RawProfileRejectsInvalidTier(string operation, string tierJson)
    {
        var error = Assert.Single(await ValidateBindingAuthorityRawProfileAsync(operation, tierJson));
        // Before the fix, force_binding stops at unknown_art: its tier predicate is UNREACHED.
        // The known binding cases independently exercise the existing bounds on that RED source.
        Assert.Equal("afterlife_combat_profile_invalid_art_tier", error.Code);
        Assert.Equal(BindingAuthorityArtPath + operation, error.FilePath);
    }

    [Theory]
    [InlineData("spiritual_resilience")]
    [InlineData("spiritual_healing")]
    public async Task BindingAuthority_RawProfileRequiresExplicitWoundArts(string missingArt)
    {
        var error = Assert.Single(await ValidateBindingAuthorityRawProfileAsync("binding", "3", missingArt));
        Assert.Equal("afterlife_combat_profile_missing_art_tier", error.Code);
        Assert.Equal(BindingAuthorityArtPath + missingArt, error.FilePath);
    }

    private async Task<ValidationIssue[]> ValidateBindingAuthorityRawProfileAsync(
        string operation, string tierJson, string? missingArt = null)
    {
        var context = await ResourceMaterializationTestContext.CreateAsync();
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), _storageOutput.WriteLine);
        // Exact raw soul admission, independent of complete-frame fixtures whose catalog
        // intentionally contains other entity-only keys. No normalizer or profile projection.
        var soul = SoulState(spiritFocusTier: 0);
        var arts = new JsonObject
        {
            ["spiritual_resilience"] = 0,
            ["spiritual_healing"] = 0,
            [operation] = JsonNode.Parse(tierJson)
        };
        if (missingArt is not null) arts.Remove(missingArt);
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"] = arts;
        await context.WriteExactJsonAsync(BindingAuthoritySoulPath, soul.ToJsonString());
        var before = await context.FileSystem.ReadFileBytesAsync(BindingAuthoritySoulPath);
        var selection = new GameStateValidationSelection(GameStateValidationPhase.MetaMiscStateFiles,
            new[] { BindingAuthoritySoulPath });
        var issues = await context.Validator.ValidateGameStateAsync(selection);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { operation, tierJson, missingArt, issues }));
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(BindingAuthoritySoulPath));
        return issues.Where(issue => issue.Severity == IssueSeverity.Error).ToArray();
    }

    [Theory]
    [InlineData("exact")]
    [InlineData("missing")]
    [InlineData("binding_alias")]
    [InlineData("audit_tier")]
    [InlineData("audit_operation")]
    public async Task BindingAuthority_SignedOriginalUsesExactCostAuthority(string variant)
    {
        var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: async fixture =>
            {
                await SeedOriginalLastBindingInputsAsync(fixture, strong: true, mixed: false);
                if (variant is not ("missing" or "binding_alias")) return;
                var soul = Assert.IsType<JsonObject>(await fixture.ReadJsonAsync(BindingAuthoritySoulPath));
                var profiles = Assert.IsType<JsonObject>(await fixture.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
                var player = profiles["profiles"]!.AsArray().Single(row => row!["actorId"]!.GetValue<string>() == "player_soul")!;
                var soulArts = soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!.AsObject();
                var playerArts = player["standardArts"]!.AsObject();
                foreach (var arts in new[] { soulArts, playerArts })
                {
                    arts.Remove("force_binding");
                    if (variant == "binding_alias") arts["binding"] = 3;
                }
                await fixture.WriteExactJsonAsync(BindingAuthoritySoulPath, soul.ToJsonString());
                await fixture.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
            }, signedDice: [5, 15, 13, 11], bypassPreparedFixture: true);
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), _storageOutput.WriteLine);
        var original = await WriteOriginalLastBindingDraftAsync(context, strong: true, "lost");
        var exchanges = original["activeConflict"]!["exchangeLog"]!.AsArray();
        var last = exchanges[1]!;
        Assert.Equal("force_binding", last["operationType"]!.GetValue<string>());
        Assert.True(last["setup"]!.GetValue<bool>());
        Assert.Equal("player_dominant", original["activeConflict"]!["conflictPosition"]!.GetValue<string>());
        var lastAudit = last["actionCostAudit"]!["player"]!;
        if (variant == "audit_tier") lastAudit["artTier"] = 2;
        if (variant == "audit_operation") lastAudit["operationType"] = "binding";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, original.ToJsonString());
        var stateBefore = await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.StatePath);
        var historyBefore = await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.HistoryPath);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { variant, prepared.Issues }));
        Assert.Equal(stateBefore, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath));
        Assert.Equal(historyBefore, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.HistoryPath));
        if (variant != "exact")
        {
            Assert.Null(prepared.Session);
            var operationMismatch = variant == "audit_operation";
            var error = Assert.Single(prepared.Issues.Where(issue => issue.Code == (operationMismatch
                ? "afterlife_conflict_action_cost_mismatch" : "afterlife_conflict_action_cost_art_tier_authority_mismatch")));
            Assert.EndsWith(".actionCostAudit.player." + (operationMismatch ? "operationType" : "artTier"), error.FilePath);
            Assert.Equal(operationMismatch ? "force_binding" : variant == "audit_tier" ? "3" : "0", error.Expected);
            Assert.Equal(operationMismatch ? "binding" : variant == "audit_tier" ? "2" : "3", error.Actual);
            return;
        }

        AssertNoConflictFrameErrors(prepared.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
        Assert.Equal(2, source.CheckedExchanges.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, source.ClaimedDice);
        Assert.Empty(source.PendingRequirements);
        var signedSoul = JsonNode.Parse(source.ReadOriginal(BindingAuthoritySoulPath)!)!;
        var signedProfiles = JsonNode.Parse(source.ReadOriginal(AfterlifeEntityProfileState.StatePath)!)!["profiles"]!.AsArray();
        Assert.Equal(3, signedSoul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!["force_binding"]!.GetValue<int>());
        Assert.Equal(3, Assert.Single(signedProfiles, row => row!["actorId"]!.GetValue<string>() == "player_soul")!["standardArts"]!["force_binding"]!.GetValue<int>());
        Assert.Equal(1, Assert.Single(signedProfiles, row => row!["actorId"]!.GetValue<string>() == "guardian_frame")!["standardArts"]!["pressure"]!.GetValue<int>());
        var signedConflict = JsonNode.Parse(source.ReadOriginal(AfterlifeSpiritualConflictState.StatePath)!)!["activeConflict"]!;
        Assert.Empty(signedConflict["exchangeLog"]!.AsArray());
        Assert.Equal("guardian_frame", signedConflict["oppositionSide"]!["leadContestant"]!["actorId"]!.GetValue<string>());
        Assert.Equal(1, signedConflict["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]!["pressure"]!.GetValue<int>());
        var baseline = ReadSourceTransactionResourceBaseline(source);
        var batches = source.BuildResourceBatches(baseline.Owners, baseline.State);
        Assert.True(batches.IsValid, string.Join(Environment.NewLine, batches.Issues));
        Assert.Empty(batches.PendingRequirements);
        Assert.Equal(2, batches.Exchanges.Count);
        for (var index = 0; index < 2; index++)
        {
            var batch = batches.Exchanges[index];
            Assert.Empty(batch.PendingSides);
            Assert.Equal(exchanges[index]!["exchangeId"]!.GetValue<string>(), batch.ExchangeId);
            Assert.Equal(2, batch.Mutations.Count);
            foreach (var side in new[] { "player", "opposition" })
            {
                var audit = exchanges[index]!["actionCostAudit"]![side]!;
                var player = side == "player";
                var before = index == 0 ? 6 : player ? 3 : 4;
                var after = index == 0 ? player ? 3 : 4 : player ? 1 : 2;
                Assert.Equal(before, audit["before"]!.GetValue<int>());
                Assert.Equal(after, audit["after"]!.GetValue<int>());
                Assert.Equal(before - after, audit["effectiveCost"]!.GetValue<int>());
                var mutation = Assert.Single(batch.Mutations, item => item.Coordinate.OwnerKind ==
                    (player ? ResourceOwnerKind.AfterlifeActor : ResourceOwnerKind.AfterlifeConflictSide));
                Assert.Equal(player ? "player_soul" : "afterlife_conflict_side_cost", mutation.Coordinate.ResourceOwnerId);
                Assert.Equal("spiritual_action_points", mutation.Coordinate.ResourceKey);
                Assert.Equal("chaos_sea", mutation.Coordinate.Realm);
                Assert.Equal(ResourceOperation.Spend, mutation.Source.Operation);
                Assert.Equal((decimal)(before - after), mutation.Amount);
                Assert.Equal($"turn_42:afterlife_conflict:{batch.ExchangeId}:{side}", mutation.EventRef);
            }
        }
        // These are validated authoritative costs and emitted intents, not applied ledger evidence.
        Assert.Equal(stateBefore, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath));
        Assert.Equal(historyBefore, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.HistoryPath));
    }
}
