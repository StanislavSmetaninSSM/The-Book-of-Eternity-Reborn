using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AcceptedTurnInterfacePayloadNormalizationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AcceptedTurnInterfacePayloadNormalizationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "boe-interface-normalization-" + Guid.NewGuid().ToString("N"));
        _fs = new FileSystemManager(_tempRoot, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public async Task ValidateAcceptedTurnInterfacePayloadAsync_NormalizesStringDialogueOptionsIntoObjects()
    {
        await _fs.WriteFileAtomicAsync("output/interface_updates.json", """
        {
          "timestamp": "2026-06-30T22:57:09Z",
          "dialogueOptions": [
            "Проверить журнал реставрационных масел.",
            "Попросить сторожа провести к старшему ключнику."
          ]
        }
        """);

        var issues = await _validator.ValidateAcceptedTurnInterfacePayloadAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "expected_object", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "expected_object_in_array", StringComparison.OrdinalIgnoreCase));

        var normalized = JsonNode.Parse(await _fs.ReadFileAsync("output/interface_updates.json") ?? "{}")!.AsObject();
        var options = normalized["dialogueOptions"]!.AsArray();
        var first = Assert.IsType<JsonObject>(options[0]);
        var second = Assert.IsType<JsonObject>(options[1]);

        Assert.Equal("Проверить журнал реставрационных масел.", first["text"]!.GetValue<string>());
        Assert.Equal("Попросить сторожа провести к старшему ключнику.", second["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task ValidateAcceptedTurnInterfacePayloadAsync_AddsMissingTimestampForUsefulPayload()
    {
        await _fs.WriteFileAtomicAsync("output/interface_updates.json", """
        {
          "dialogueOptions": [
            {
              "text": "Осмотреть печать письма."
            }
          ]
        }
        """);

        var issues = await _validator.ValidateAcceptedTurnInterfacePayloadAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "interface_updates_missing_timestamp", StringComparison.OrdinalIgnoreCase));

        var normalized = JsonNode.Parse(await _fs.ReadFileAsync("output/interface_updates.json") ?? "{}")!.AsObject();
        var timestamp = normalized["timestamp"]!.GetValue<string>();
        Assert.True(DateTimeOffset.TryParse(timestamp, out _), $"Expected ISO timestamp, got '{timestamp}'.");
    }
}
