using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.AgentConsole;

public sealed record AgentConsoleApiHostOptions(
    string Url,
    string Token,
    AgentConsoleStateStore StateStore,
    AgentConsoleLiveInputSource InputSource);

public static class AgentConsoleApiHost
{
    private const int MaxKeyNameLength = 64;
    private const int MaxTextLength = 4096;

    public static WebApplication Build(string[] args, AgentConsoleApiHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Token);
        ArgumentNullException.ThrowIfNull(options.StateStore);
        ArgumentNullException.ThrowIfNull(options.InputSource);

        if (!IsLoopbackHttpUrl(options.Url))
            throw new InvalidOperationException("Agent Console API can only bind to localhost/loopback URLs.");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.WebHost.UseUrls(options.Url);

        builder.Services.Configure<JsonOptions>(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = AgentConsoleJson.Options.PropertyNamingPolicy;
            json.SerializerOptions.DefaultIgnoreCondition = AgentConsoleJson.Options.DefaultIgnoreCondition;
            json.SerializerOptions.WriteIndented = AgentConsoleJson.Options.WriteIndented;
            foreach (var converter in AgentConsoleJson.Options.Converters)
                json.SerializerOptions.Converters.Add(converter);
        });

        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            if (RequiresControlToken(context.Request) && !IsAuthorized(context.Request, options.Token))
            {
                await WriteUnauthorizedAsync(context);
                return;
            }

            await next(context);
        });

        app.MapGet("/api/agent-console/snapshot", () =>
            Results.Json(options.StateStore.GetSnapshot(), AgentConsoleJson.Options));

        app.MapGet("/api/agent-console/events", () =>
            Results.Json(options.StateStore.GetEvents(), AgentConsoleJson.Options));

        app.MapPost("/api/agent-console/key", (HttpContext context, AgentConsoleKeyInputRequest request) =>
        {
            if (!IsAuthorized(context.Request, options.Token))
                return Unauthorized(context);

            if (!TryParseKeyRequest(request, out var key, out var error))
                return Results.BadRequest(new { error });

            var result = options.InputSource.EnqueueKey(key);
            return ToControlResult(result);
        });

        app.MapPost("/api/agent-console/text", (HttpContext context, AgentConsoleTextInputRequest request) =>
        {
            if (!IsAuthorized(context.Request, options.Token))
                return Unauthorized(context);

            if (request.Text is null)
                return Results.BadRequest(new { error = "Text is required." });
            if (request.Text.Length > MaxTextLength)
                return Results.BadRequest(new { error = $"Text must be {MaxTextLength} characters or fewer." });

            var result = options.InputSource.EnqueueLine(request.Text);
            return ToControlResult(result);
        });

        app.MapPost("/api/agent-console/action", (HttpContext context, AgentConsoleActionRequest request) =>
        {
            if (!IsAuthorized(context.Request, options.Token))
                return Unauthorized(context);

            var result = options.InputSource.TryQueueAction(request);
            return ToControlResult(result);
        });

        return app;
    }

    private static IResult ToControlResult(AgentConsoleInputResult result) =>
        result.Accepted
            ? Results.Json(result, AgentConsoleJson.Options)
            : Results.Json(result, AgentConsoleJson.Options, statusCode: StatusCodes.Status409Conflict);

    private static IResult Unauthorized(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Results.Json(new { error = "Missing or invalid Agent Console token." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid Agent Console token." }, AgentConsoleJson.Options);
    }

    private static bool RequiresControlToken(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
            return false;

        var path = request.Path.Value;
        return string.Equals(path, "/api/agent-console/key", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(path, "/api/agent-console/text", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(path, "/api/agent-console/action", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAuthorized(HttpRequest request, string expectedToken)
    {
        var value = request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (!value.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var candidate = value[bearerPrefix.Length..].Trim();
        if (candidate.Length == 0)
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        var candidateBytes = Encoding.UTF8.GetBytes(candidate);
        return expectedBytes.Length == candidateBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes);
    }

    private static bool TryParseKeyRequest(
        AgentConsoleKeyInputRequest request,
        out ConsoleKeyInfo key,
        out string error)
    {
        key = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(request.Key))
        {
            error = "Key is required.";
            return false;
        }

        if (request.Key.Length > MaxKeyNameLength)
        {
            error = $"Key must be {MaxKeyNameLength} characters or fewer.";
            return false;
        }

        if (!TryParseConsoleKey(request.Key, request.Text, out key))
        {
            error = $"Unsupported console key '{request.Key}'.";
            return false;
        }

        return true;
    }

    private static bool TryParseConsoleKey(string keyName, string? text, out ConsoleKeyInfo keyInfo)
    {
        keyInfo = default;
        var normalized = keyName.Trim();
        if (normalized.Length == 0)
            return false;

        if (normalized.Length == 1)
        {
            var ch = normalized[0];
            if (char.IsLetter(ch) &&
                Enum.TryParse<ConsoleKey>(char.ToUpperInvariant(ch).ToString(), ignoreCase: false, out var letterKey))
            {
                keyInfo = new ConsoleKeyInfo(ResolveKeyChar(ch, text), letterKey, shift: false, alt: false, control: false);
                return true;
            }

            if (char.IsDigit(ch) &&
                Enum.TryParse<ConsoleKey>("D" + ch, ignoreCase: false, out var digitKey))
            {
                keyInfo = new ConsoleKeyInfo(ResolveKeyChar(ch, text), digitKey, shift: false, alt: false, control: false);
                return true;
            }
        }

        var keyChar = ResolveNamedKeyChar(text);
        switch (normalized.ToLowerInvariant())
        {
            case "space":
            case "spacebar":
                keyInfo = new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, shift: false, alt: false, control: false);
                return true;
            case "up":
            case "uparrow":
                keyInfo = new ConsoleKeyInfo(keyChar, ConsoleKey.UpArrow, shift: false, alt: false, control: false);
                return true;
            case "down":
            case "downarrow":
                keyInfo = new ConsoleKeyInfo(keyChar, ConsoleKey.DownArrow, shift: false, alt: false, control: false);
                return true;
            case "left":
            case "leftarrow":
                keyInfo = new ConsoleKeyInfo(keyChar, ConsoleKey.LeftArrow, shift: false, alt: false, control: false);
                return true;
            case "right":
            case "rightarrow":
                keyInfo = new ConsoleKeyInfo(keyChar, ConsoleKey.RightArrow, shift: false, alt: false, control: false);
                return true;
            case "enter":
            case "return":
                keyInfo = new ConsoleKeyInfo(keyChar, ConsoleKey.Enter, shift: false, alt: false, control: false);
                return true;
            case "escape":
            case "esc":
                keyInfo = new ConsoleKeyInfo(keyChar, ConsoleKey.Escape, shift: false, alt: false, control: false);
                return true;
            case "tab":
                keyInfo = new ConsoleKeyInfo('\t', ConsoleKey.Tab, shift: false, alt: false, control: false);
                return true;
            case "backspace":
                keyInfo = new ConsoleKeyInfo('\b', ConsoleKey.Backspace, shift: false, alt: false, control: false);
                return true;
            default:
                if (Enum.TryParse<ConsoleKey>(normalized, ignoreCase: true, out var parsed))
                {
                    keyInfo = new ConsoleKeyInfo(keyChar, parsed, shift: false, alt: false, control: false);
                    return true;
                }

                return false;
        }
    }

    private static char ResolveKeyChar(char fallback, string? text) =>
        !string.IsNullOrEmpty(text) && text.Length == 1
            ? text[0]
            : char.ToLowerInvariant(fallback);

    private static char ResolveNamedKeyChar(string? text) =>
        !string.IsNullOrEmpty(text) && text.Length == 1 ? text[0] : '\0';

    private static bool IsLoopbackHttpUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
    }

    private sealed record AgentConsoleKeyInputRequest
    {
        [JsonPropertyName("key")]
        public string? Key { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    private sealed record AgentConsoleTextInputRequest
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }
}
