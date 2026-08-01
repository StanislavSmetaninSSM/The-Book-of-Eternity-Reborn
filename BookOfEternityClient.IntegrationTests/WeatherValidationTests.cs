using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class WeatherValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public WeatherValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-weather-validation-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(_rootPath, "game_session"));

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentLocationDescription_WithNestedNormalizedWeather_DoesNotRequireRootWeatherTendency()
    {
        var locationJson = await _fs.ReadFileAsync("game_state/world/current_location.json");
        var root = JsonNode.Parse(locationJson!)!.AsObject();
        root.Remove("tendency");
        root["description"] = "Комната сохраняет обычное описание локации, не описание weather direct root.";
        root["normalizedWeatherState"] = new JsonObject
        {
            ["description"] = "За окнами держится холодный утренний туман.",
            ["tendency"] = "NO_CHANGE"
        };

        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", root.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.Weather);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "weather_direct_state_missing_required_fields", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/world/current_location.json.normalizedWeatherState", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
