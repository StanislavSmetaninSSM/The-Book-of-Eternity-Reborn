using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AgentConsoleApiHostTests
{
    [Fact]
    public void AccessTokenResolve_AutoGeneratesPerRunToken()
    {
        var first = AgentConsoleAccessToken.Resolve("auto");
        var second = AgentConsoleAccessToken.Resolve("auto");

        Assert.True(first.WasGenerated);
        Assert.True(second.WasGenerated);
        Assert.NotEqual("auto", first.Value);
        Assert.NotEqual(first.Value, second.Value);
        Assert.True(first.Value.Length >= 32);
        Assert.DoesNotContain(" ", first.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessTokenResolve_ExplicitTokenPreservesValue()
    {
        var token = AgentConsoleAccessToken.Resolve("operator-token");

        Assert.False(token.WasGenerated);
        Assert.Equal("operator-token", token.Value);
    }

    [Fact]
    public void Build_RejectsNonLoopbackUrls()
    {
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AgentConsoleApiHost.Build(
                Array.Empty<string>(),
                new AgentConsoleApiHostOptions("http://0.0.0.0:8790", "secret-token", store, input)));

        Assert.Contains("loopback", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SnapshotEndpoint_ReturnsLatestSnapshotWithoutToken()
    {
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        var snapshot = BuildMenuSnapshot("main-menu", selectedIndex: 1);
        store.UpdateSnapshot(snapshot);
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = AgentConsoleApiHost.Build(
            Array.Empty<string>(),
            new AgentConsoleApiHostOptions(url, "secret-token", store, input));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var root = JsonNode.Parse(await client.GetStringAsync("/api/agent-console/snapshot"))!.AsObject();

        Assert.Equal("main-menu", root["screenId"]!.GetValue<string>());
        Assert.Equal("menu", root["mode"]!.GetValue<string>());
        Assert.True(root["awaitingInput"]!.GetValue<bool>());
        Assert.Equal("exit", root["actions"]![1]!["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task EventsEndpoint_ReturnsBoundedHistoryWithoutToken()
    {
        var store = new AgentConsoleStateStore(eventCapacity: 2);
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.AppendEvent(AgentConsoleEventKind.StateChanged, message: "first");
        store.AppendEvent(AgentConsoleEventKind.StateChanged, message: "second");
        store.AppendEvent(AgentConsoleEventKind.StateChanged, message: "third");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = AgentConsoleApiHost.Build(
            Array.Empty<string>(),
            new AgentConsoleApiHostOptions(url, "secret-token", store, input));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var events = JsonNode.Parse(await client.GetStringAsync("/api/agent-console/events"))!.AsArray();

        Assert.Equal(2, events.Count);
        Assert.Equal(2L, events[0]!["sequenceId"]!.GetValue<long>());
        Assert.Equal("second", events[0]!["message"]!.GetValue<string>());
        Assert.Equal("third", events[1]!["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task ControlEndpoints_RejectMissingAndInvalidTokens()
    {
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = AgentConsoleApiHost.Build(
            Array.Empty<string>(),
            new AgentConsoleApiHostOptions(url, "secret-token", store, input));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var missing = await client.PostAsJsonAsync("/api/agent-console/text", new { text = "look" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");
        using var invalid = await client.PostAsJsonAsync("/api/agent-console/key", new { key = "enter" });

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Empty(store.GetEvents());
    }

    [Fact]
    public async Task ControlEndpoints_RejectUnauthorizedMalformedBodiesBeforeJsonBinding()
    {
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = AgentConsoleApiHost.Build(
            Array.Empty<string>(),
            new AgentConsoleApiHostOptions(url, "secret-token", store, input));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var missing = await client.PostAsync(
            "/api/agent-console/text",
            new StringContent("{", Encoding.UTF8, "application/json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");
        using var invalid = await client.PostAsync(
            "/api/agent-console/key",
            new StringContent("{", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Empty(store.GetEvents());
    }

    [Fact]
    public async Task ControlEndpoints_WithValidToken_FeedLiveInputSource()
    {
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = AgentConsoleApiHost.Build(
            Array.Empty<string>(),
            new AgentConsoleApiHostOptions(url, "secret-token", store, input));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "secret-token");

        store.UpdateSnapshot(BuildTextSnapshot("game-loop"));
        using var textResponse = await client.PostAsJsonAsync("/api/agent-console/text", new { text = "look north" });
        Assert.Equal(HttpStatusCode.OK, textResponse.StatusCode);
        Assert.Equal("look north", input.ReadLine());

        store.UpdateSnapshot(BuildKeySnapshot("command-output"));
        using var keyResponse = await client.PostAsJsonAsync("/api/agent-console/key", new { key = "enter" });
        Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);

        store.UpdateSnapshot(BuildMenuSnapshot("main-menu", selectedIndex: 0));
        using var actionResponse = await client.PostAsJsonAsync("/api/agent-console/action", new
        {
            actionId = "continue",
            screenId = "main-menu",
            inputKind = "menuSelection"
        });

        Assert.Equal(HttpStatusCode.OK, actionResponse.StatusCode);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
        Assert.Contains(store.GetEvents(), agentEvent => agentEvent.Kind == AgentConsoleEventKind.InputAccepted);
    }

    private static AgentConsoleSnapshot BuildMenuSnapshot(string screenId, int selectedIndex)
    {
        var renderedAt = new DateTimeOffset(2026, 5, 31, 10, 0, 0, TimeSpan.Zero);
        return new AgentConsoleSnapshot
        {
            ScreenId = screenId,
            Mode = AgentConsoleMode.Menu,
            Title = "Main Menu",
            PlainText = "Choose your path.",
            AwaitingInput = true,
            InputKind = AgentConsoleInputKind.MenuSelection,
            SelectedIndex = selectedIndex,
            Actions =
            [
                new AgentConsoleAction { Id = "continue", Label = "Continue", Shortcut = "Enter", IsDefault = selectedIndex == 0 },
                new AgentConsoleAction { Id = "exit", Label = "Exit", IsDefault = selectedIndex == 1 }
            ],
            RenderedAtUtc = renderedAt,
            UpdatedAtUtc = renderedAt
        };
    }

    private static AgentConsoleSnapshot BuildTextSnapshot(string screenId)
    {
        var renderedAt = new DateTimeOffset(2026, 5, 31, 10, 0, 0, TimeSpan.Zero);
        return new AgentConsoleSnapshot
        {
            ScreenId = screenId,
            Mode = AgentConsoleMode.TextPrompt,
            Title = "Your Turn",
            PlainText = "What next?",
            AwaitingInput = true,
            InputKind = AgentConsoleInputKind.Text,
            RenderedAtUtc = renderedAt,
            UpdatedAtUtc = renderedAt
        };
    }

    private static AgentConsoleSnapshot BuildKeySnapshot(string screenId)
    {
        var renderedAt = new DateTimeOffset(2026, 5, 31, 10, 0, 0, TimeSpan.Zero);
        return new AgentConsoleSnapshot
        {
            ScreenId = screenId,
            Mode = AgentConsoleMode.TextPrompt,
            Title = "Command Output",
            PlainText = "Press any key.",
            AwaitingInput = true,
            InputKind = AgentConsoleInputKind.Key,
            RenderedAtUtc = renderedAt,
            UpdatedAtUtc = renderedAt
        };
    }

    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
