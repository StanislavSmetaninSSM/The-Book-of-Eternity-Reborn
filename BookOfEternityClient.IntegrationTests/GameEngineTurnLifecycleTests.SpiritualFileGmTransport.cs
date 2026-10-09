using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    // File-GM output is external input to the waiting engine. It must not acquire a
    // second client main-operation owner. Only these existing authored surfaces are allowed.
    private static async Task WriteSpiritualFileGmJsonAsync(FileSystemManager files, string relative,
        string json, CancellationToken cancellationToken = default)
    {
        Assert.Contains(relative, new[] { "output/narrative_response.json",
            "game_state/control/validation_repair_ready.json", AfterlifeSpiritualConflictState.StatePath });
        var path = files.ResolvePath(relative);
        var temporary = path + ".gm-fixture-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(temporary, json, new UTF8Encoding(false), cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
