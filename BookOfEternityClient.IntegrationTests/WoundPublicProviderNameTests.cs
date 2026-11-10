using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Real read-only public treatment choices must identify the displayed provider safely.</summary>
public sealed class WoundPublicProviderNameTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("ambiguous")]
    [InlineData("public_tiers")]
    [InlineData("distinct_names")]
    [InlineData("stale_name")]
    [InlineData("stale_ambiguity")]
    public async Task PublicProvidersUseKnownContextOrRefuseAmbiguityAtTheActualSelfMenu(string scenario)
    {
        // Broken behavior: valid paid providers are offered by equal raw names,
        // or Confirm accepts an unchanged quote after its displayed name changes.
        var services = ShiningFactionHealingServiceTestData.Create(2);
        var rows = services.Residents["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        var actors = services.Profiles["profiles"]!.AsArray().OfType<JsonObject>().ToArray();
        var repeated = scenario is "ambiguous" or "public_tiers";
        for (var i = 0; i < actors.Length; i++)
        {
            var name = repeated ? "Общий целитель" : i == 0 ? "Первый целитель" : "Второй целитель";
            rows[i]["displayName"] = name;
            actors[i]["displayName"] = name;
            actors[i].Remove("name");
        }
        if (scenario == "public_tiers") actors[1]["standardArts"]!["spiritual_healing"] = 4;
        var wound = WoundContractTestData.CreateSpiritualActiveWound("wound_provider_names", "shining_abode",
            "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
        var input = WoundPlayerProjectionTestData.InputInRealm("shining_abode", wound);
        foreach (var actor in actors) input.Carriers.AfterlifeProfiles!["profiles"]!.AsArray().Add(actor.DeepClone());
        var catalog = WoundCarrierCatalog.Build(input.Carriers);
        Assert.Empty(catalog.Issues);
        Assert.Empty(input.History.ValidateAgreement(input.Identities, catalog));
        Assert.True(WoundPlayerProjection.Build(input, catalog).IsAvailable);
        var admitted = ShiningFactionHealingSupport.ProjectCurrentCurrencyServices(services.Shining, services.Residents,
            input.Carriers.AfterlifeProfiles!);
        Assert.Equal(2, admitted.Count);
        Assert.Equal(2, admitted.Select(service => service.Profile.Provider).Distinct().Count());
        foreach (var service in admitted)
        {
            var actor = actors.Single(a => a["actorId"]!.GetValue<string>() == service.Profile.Provider.OwnerId);
            Assert.Empty(ActorMaterializationContract.ValidateAfterlifeProfile(JsonSerializer.SerializeToElement(actor),
                "public-provider.fixture", requireEnvelope: true, canTradeEvidence: false));
            Assert.True(LocalInteractionScopeService.IsAfterlifeActorLocal(service.Scope, actor));
        }

        var reads = new CanonicalReadActivityAccumulator();
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: "shining_abode",
            suppliedInput: input, reads: reads);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            await fixture.Files.WriteFileAtomicAsync(lease, ShiningAbodeState.StatePath, services.Shining.ToJsonString());
            await fixture.Files.WriteFileAtomicAsync(lease, GuardianAbodeResidentState.StatePath, services.Residents.ToJsonString());
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/meta/soul_state.json", new JsonObject
            {
                ["currentRealm"] = "Shining Abode", ["soulName"] = "Путник",
                ["inkFeathers"] = new JsonObject { ["current"] = 500, ["total"] = 900 }
            }.ToJsonString());
            _ = fixture.Files.GetOrCreateSessionGeneration(lease);
        }
        var before = fixture.Snapshot(); var mutations = fixture.MutationCalls;
        var paths = new[] { ShiningAbodeState.StatePath, GuardianAbodeResidentState.StatePath };
        var counts = paths.Select(reads.ReadCount).ToArray();
        var opened = await WoundApplicationService.OpenAsync(fixture.Files, "/лечить", treatment: true);
        for (var i = 0; i < paths.Length; i++) Assert.Equal(1L, reads.ReadCount(paths[i]) - counts[i]);
        var navigation = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
        var self = Assert.Single(navigation.Targets, target => target.Owner.OwnerKind == "player_soul");
        Assert.Equal("Себя", navigation.Targets[0].Name);
        Assert.True(self.Available);
        ExplorerCommandResult detail;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            detail = await WoundApplicationService.SelectTargetAsync(fixture.Files, lease, navigation, self.Value, "/лечить");
        var state = Assert.IsType<WoundCommandState>(detail.WoundCommandState);
        Assert.Equal(self.Owner, state.SelectedTarget!.Owner);
        var paid = state.Choices.Where(choice => choice.Selection.Service is not null).ToArray();
        Assert.Equal(2, paid.Length);
        Assert.All(paid, choice =>
        {
            var quote = choice.Selection.Service!.Quote;
            Assert.Equal(self.Owner, quote.Patient);
            Assert.Equal("wound_provider_names", quote.WoundId);
            Assert.Equal(51, quote.Amount);
            Assert.Equal(quote, AfterlifeHealingServiceContract.ParseQuote(quote.ToJson()));
            Assert.False(choice.Selection.Service.PlayerConfirmed);
            Assert.Contains(admitted, service => service.Profile.Provider == quote.Provider && service.Profile.Fingerprint == quote.ServiceFingerprint);
        });
        Assert.Equal(2, paid.Select(choice => choice.Selection.Service!.Quote.Provider).Distinct().Count());
        var ui = JsonSerializer.Serialize(detail);
        foreach (var privateToken in new[] { "resident_public_healer_", "wound_provider_names", "sha256:", "carrierPath", "quoteFingerprint" })
            Assert.DoesNotContain(privateToken, ui);
        Assert.Equal(before, fixture.Snapshot()); Assert.Equal(mutations, fixture.MutationCalls);

        if (scenario.StartsWith("stale_", StringComparison.Ordinal))
        {
            Assert.All(paid, choice => Assert.True(choice.Available));
            var selected = paid.Single(choice => choice.Selection.Service!.Quote.Provider.OwnerId == actors[0]["actorId"]!.GetValue<string>());
            var changedIndex = scenario == "stale_name" ? 0 : 1;
            rows[changedIndex]["displayName"] = scenario == "stale_name" ? "Новое публичное имя" : rows[0]["displayName"]!.GetValue<string>();
            await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
                await fixture.Files.WriteFileAtomicAsync(lease, GuardianAbodeResidentState.StatePath, services.Residents.ToJsonString());
            var changed = fixture.Snapshot(); var changedMutations = fixture.MutationCalls;
            var fresh = await WoundApplicationService.OpenAsync(fixture.Files, "/лечить", treatment: true);
            var freshChoice = Assert.Single(fresh.WoundCommandState!.Choices, choice => choice.Selection.Service?.Quote.Provider == selected.Selection.Service!.Quote.Provider);
            Assert.Equal(selected.Selection, freshChoice.Selection); // Display drift is not quote drift.
            if (scenario == "stale_name")
            {
                Assert.True(freshChoice.Available);
                Assert.NotEqual(selected.Name, freshChoice.Name);
            }
            Exception? error;
            await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
                error = await Record.ExceptionAsync(() => WoundApplicationService.ConfirmAsync(fixture.Files, lease, state, selected.Value));
            Assert.Equal(changed, fixture.Snapshot()); Assert.Equal(changedMutations, fixture.MutationCalls);
            Assert.IsType<InvalidDataException>(error); // Causal stale-display oracle, after all premises.
            return;
        }
        if (scenario == "ambiguous")
        {
            Assert.All(paid, choice => Assert.False(choice.Available)); // Causal ambiguity oracle.
            Assert.All(paid, choice => Assert.Contains("различить", choice.Reason));
            Assert.All(paid, choice => Assert.Throws<InvalidDataException>(() => state.Resolve(choice.Value)));
        }
        else
        {
            Assert.All(paid, choice => Assert.True(choice.Available));
            Assert.Equal(2, paid.Select(choice => choice.Name).Distinct(StringComparer.Ordinal).Count()); // Causal context oracle.
            if (scenario == "public_tiers")
            {
                Assert.Contains(paid, choice => choice.Name.Contains("Исцеление III", StringComparison.Ordinal));
                Assert.Contains(paid, choice => choice.Name.Contains("Исцеление IV", StringComparison.Ordinal));
            }
            SpiritualSelfTreatmentSelection confirmed;
            await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
                confirmed = await WoundApplicationService.ConfirmAsync(fixture.Files, lease, state, paid[0].Value);
            Assert.True(confirmed.Service!.PlayerConfirmed);
            Assert.Equal(paid[0].Selection.Service!.Quote, confirmed.Service.Quote);
            Assert.Equal(before, fixture.Snapshot()); Assert.Equal(mutations, fixture.MutationCalls);
        }
    }
}
