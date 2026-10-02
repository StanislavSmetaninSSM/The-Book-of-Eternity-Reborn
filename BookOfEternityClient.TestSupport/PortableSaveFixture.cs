using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static class PortableSaveFixture
{
    internal static StateManager Seed(FileSystemManager files)
    {
        files.EnsureDirectoryStructure();
        Put(files, "game_state/meta/soul_state.json", """{"soulName":"Тестовая Душа","currentRealm":"Mortal World","currentIncarnation":1}""");
        Put(files, "game_state/world/test_fixture_state.json", """{"state":"test-fixture"}""");
        var resources = ResourceBootstrapStateBuilder.BuildPristine();
        Put(files, ResourceMaterializationContract.DefinitionsPath, resources.Definitions!.ToCanonicalJson());
        Put(files, ResourceMaterializationContract.StatePath, resources.State!.ToCanonicalJson());
        Put(files, ResourceMaterializationContract.HistoryPath, resources.History!.ToCanonicalJson());
        Put(files, CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            """{"schemaVersion":1,"historicalOwners":[],"capacityDrafts":[]}""");
        Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
        File.WriteAllBytes(files.SessionGenerationPath, JsonSerializer.SerializeToUtf8Bytes(
            new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") }));
        return new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
    }

    private static void Put(FileSystemManager files, string relative, string text)
    {
        var path = files.ResolvePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(text));
    }

    internal sealed class CaptureLogger : ILogger<SaveLoadService>
    {
        internal List<Exception> Errors { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception != null) Errors.Add(exception);
        }
    }
}
