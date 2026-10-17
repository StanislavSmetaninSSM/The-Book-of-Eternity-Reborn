using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class DarenRetirementClientTests(ITestOutputHelper output)
{
    private static readonly string[] PracticeTypes = ["BranchChoice", "TimingBar", "PromptChain", "BalanceMeter", "ChargeRelease",
        "MashInput", "PatternMemory", "RhythmPulse", "PrecisionChoice", "StealthNoise", "LockPinSet"];
    public static IEnumerable<object[]> Types() => PracticeTypes.Select(type => new object[] { type });

    [Fact]
    public Task RealBrowserMenu_KeepsPracticeWithoutCampaignLaunch() => WithHostAsync(async (client, files) =>
    {
        var profile = File.ReadAllBytes(Path.Combine(files.BasePath, DarenRetirementEvidenceFixtures.Profile));
        var menu = JsonNode.Parse(await client.GetStringAsync("/api/main-menu"))!.AsObject();
        var ids = menu["actions"]!.AsArray().Select(node => node!["id"]!.GetValue<string>()).ToArray();
        Assert.Contains("qte-practice", ids);
        Assert.Equal(profile, File.ReadAllBytes(Path.Combine(files.BasePath, DarenRetirementEvidenceFixtures.Profile)));
        output.WriteLine(JsonSerializer.Serialize(new { RealHostMenuActionIds = ids }));
        Assert.DoesNotContain("daren-showcase", ids);
    });

    [Theory]
    [InlineData("GET", "/api/qte/daren")]
    [InlineData("POST", "/api/qte/daren/start")]
    [InlineData("POST", "/api/qte/daren/action")]
    [InlineData("POST", "/api/qte/daren/retry")]
    [InlineData("POST", "/api/qte/daren/exit")]
    public Task RetiredDirectHostRoute_CannotDispatchOrMutateProfile(string method, string path) => WithHostAsync(async (client, files) =>
    {
        var catalog = await client.GetFromJsonAsync<QtePracticeWebStateDto>("/api/qte/practice");
        Assert.NotNull(catalog); Assert.Equal(PracticeTypes, catalog.Catalog.Select(x => x.TypeId));
        Assert.False(string.IsNullOrWhiteSpace(catalog.InteractionToken));
        var before = File.ReadAllBytes(Path.Combine(files.BasePath, DarenRetirementEvidenceFixtures.Profile));
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new { interactionToken = catalog.InteractionToken, actionId = "stale", grade = "success" });
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var after = File.ReadAllBytes(Path.Combine(files.BasePath, DarenRetirementEvidenceFixtures.Profile));
        output.WriteLine(JsonSerializer.Serialize(new { Method = method, Path = path, Status = (int)response.StatusCode, Body = body,
            ProfileBefore = DarenRetirementEvidenceFixtures.Hash(before), ProfileAfter = DarenRetirementEvidenceFixtures.Hash(after) }));
        Assert.Equal(before, after);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    });

    [Theory]
    [MemberData(nameof(Types))]
    public Task RealPracticeHost_StartCompleteRetryExitPreservesRewardData(string type) => WithHostAsync(async (client, files) =>
    {
        var before = File.ReadAllBytes(Path.Combine(files.BasePath, DarenRetirementEvidenceFixtures.Profile));
        var soulBefore = File.ReadAllBytes(files.ResolvePath("game_state/meta/soul_state.json"));
        var catalog = await client.GetFromJsonAsync<QtePracticeWebStateDto>("/api/qte/practice");
        Assert.NotNull(catalog); Assert.Equal(PracticeTypes, catalog.Catalog.Select(x => x.TypeId));
        using var startResponse = await client.PostAsJsonAsync("/api/qte/practice/start", new QtePracticeStartRequest(type, "normal", catalog.InteractionToken));
        startResponse.EnsureSuccessStatusCode();
        var active = (await startResponse.Content.ReadFromJsonAsync<QtePracticeWebStateDto>())!;
        Assert.Equal("Active", active.State); Assert.True(string.IsNullOrWhiteSpace(active.Error), active.Error);
        var action = Assert.Single(active.ActiveScene!.CurrentChapter!.Actions);
        using var actionResponse = await client.PostAsJsonAsync("/api/qte/practice/action", new QtePracticeActionRequest(action.ActionId, "success", active.InteractionToken));
        actionResponse.EnsureSuccessStatusCode();
        var completed = (await actionResponse.Content.ReadFromJsonAsync<QtePracticeWebStateDto>())!;
        Assert.Equal("Completed", completed.State); Assert.NotNull(completed.Completion);
        using var retryResponse = await client.PostAsJsonAsync("/api/qte/practice/retry", new QteInteractionRequest(completed.InteractionToken));
        retryResponse.EnsureSuccessStatusCode();
        var retry = (await retryResponse.Content.ReadFromJsonAsync<QtePracticeWebStateDto>())!;
        Assert.Equal("Active", retry.State);
        using var exitResponse = await client.PostAsJsonAsync("/api/qte/practice/exit", new QteInteractionRequest(retry.InteractionToken));
        exitResponse.EnsureSuccessStatusCode();
        var exited = (await exitResponse.Content.ReadFromJsonAsync<QtePracticeWebStateDto>())!;
        Assert.Equal("Catalog", exited.State);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(files.BasePath, DarenRetirementEvidenceFixtures.Profile)));
        Assert.Equal(soulBefore, File.ReadAllBytes(files.ResolvePath("game_state/meta/soul_state.json")));
        output.WriteLine(JsonSerializer.Serialize(new { Type = type, RealHost = true, Operations = new[] { "start", "action", "retry", "exit" },
            ProofLimit = "HTTP and real C# owner execution; not an interactive browser minigame playthrough", completed.State,
            Profile = DarenRetirementEvidenceFixtures.Hash(before), Soul = DarenRetirementEvidenceFixtures.Hash(soulBefore) }));
    });

    private async Task WithHostAsync(Func<HttpClient, FileSystemManager, Task> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-daren-retirement-host-" + Guid.NewGuid().ToString("N"));
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        PortableSaveFixture.Seed(files);
        DarenRetirementEvidenceFixtures.Put(root, DarenRetirementEvidenceFixtures.Profile, DarenRetirementEvidenceFixtures.Before);
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        var url = "http://127.0.0.1:" + port;
        await using var app = LocalWebUiHost.Build([], new(root, url,
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "public")));
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(15) };
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
            await body(client, files);
        }
        finally { await app.StopAsync(); }
    }
}
