using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Public dispatch controls for the existing canonical vehicle receipt boundary.</summary>
public sealed class VehicleReceiptValidationScopeTests : IDisposable
{
    private const string ReceiptCode = "resource_owner_vehicle_receipt_invalid";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-vehicle-receipt-scope-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public VehicleReceiptValidationScopeTests()
    {
        Directory.CreateDirectory(_root);
        _fs = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Theory]
    [InlineData("game_state/misc/multipliers.json", "{\"multipliers\":[]}")]
    [InlineData("lore/codex_entries.json", "{\"entries\":[]}")]
    [InlineData("game_state/misc/player_interactions.json", "{\"otherPlayersInteractions\":[]}")]
    public async Task UnrelatedCanonicalObjects_DoNotRequireVehicleReceipts(string path, string json)
    {
        await _fs.WriteFileAtomicAsync(path, json);
        var issues = await _validator.ValidateGameStateAsync();
        Assert.DoesNotContain(issues, issue => issue.Code == ReceiptCode);
        Assert.False(File.Exists(_fs.ResolvePath("game_state/misc/vehicles.json")));
    }

    [Theory]
    [InlineData("{\"vehicles\":[]}", true)]
    [InlineData("{\"vehicles\":[],\"birthReceipts\":null}", true)]
    [InlineData("{\"vehicles\":[],\"birthReceipts\":1}", true)]
    [InlineData("{\"vehicles\":[],\"birthReceipts\":[]}", false)]
    public async Task ExactCanonicalVehicleObject_StillRequiresReceiptCollection(string json, bool rejected)
    {
        await _fs.WriteFileAtomicAsync("game_state/misc/vehicles.json", json);
        var issues = await _validator.ValidateGameStateAsync();
        Assert.Equal(rejected, issues.Any(issue => issue.Code == ReceiptCode));
        Assert.Equal(json, await File.ReadAllTextAsync(_fs.ResolvePath("game_state/misc/vehicles.json")));
    }

    [Theory]
    [InlineData("{\"response\":\"A quiet shore.\"}")]
    [InlineData("{\"response\":\"A quiet shore.\",\"removeVehicles\":[]}")]
    [InlineData("{\"response\":\"A quiet shore.\",\"UpdateVehicles\":[]}")]
    public void ResponseCommands_DoNotRequireCanonicalReceiptCollection(string json)
    {
        using var document = JsonDocument.Parse(json);
        var issues = _validator.ValidateResponse(document.RootElement);
        Assert.DoesNotContain(issues, issue => issue.Code == ReceiptCode);
    }

    [Fact]
    public void InvalidResponseCommand_RetainsItsOwnTypeValidationWithoutReceiptRequirement()
    {
        using var document = JsonDocument.Parse("""{"response":"A quiet shore.","activeVehicleChange":42}""");
        var issues = _validator.ValidateResponse(document.RootElement);
        Assert.Contains(issues, issue => issue.Code == "active_vehicle_change_invalid_type");
        Assert.DoesNotContain(issues, issue => issue.Code == ReceiptCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
