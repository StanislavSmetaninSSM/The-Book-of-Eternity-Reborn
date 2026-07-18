using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ActorMaterializationValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly ValidationService _validator;

    public ActorMaterializationValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-actor-materialization-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        var fileSystem = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        fileSystem.EnsureDirectoryStructure();
        _validator = new ValidationService(fileSystem, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public void ValidateResponse_NewMortalNpcWithoutEnvelope_ReportsMissingMaterialization()
    {
        using var document = JsonDocument.Parse("""
        {
          "NPCsInScene": [
            {
              "NPCId": null,
              "initialId": "npc_station_medic",
              "name": "Дежурный медик орбитальной станции"
            }
          ]
        }
        """);

        var issues = _validator.ValidateResponse(document.RootElement);

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_missing");
    }

    [Fact]
    public void ValidateResponse_NewAfterlifeProfileWithoutEnvelope_ReportsMissingMaterialization()
    {
        using var document = JsonDocument.Parse("""
        {
          "afterlifeEntityProfiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_voice_of_north_gallery",
              "displayName": "Голос северной галереи",
              "realm": "Shining Abode"
            }
          ]
        }
        """);

        var issues = _validator.ValidateResponse(document.RootElement);

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_missing");
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
            // Best-effort test cleanup.
        }
    }
}
