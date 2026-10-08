using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using Xunit;

namespace BookOfEternityClient.Tests;

// Exact public child-PTY evidence from og9; replay does not run the CLI or
// establish model reception, live readiness, or acceptance of the old operation.
public sealed class GmMiniDefaultForegroundTests
{
    [Fact]
    public async Task ActualPastePrefix_DefaultForegroundRetainsExactDraftRegion()
    {
        await using var h=new GmBridgePromptOperationTests.PromptHostFixture();
        using var screen=new GmSynchronizedTerminalPresentationTests.ScreenProbe(h.BindingId);
        h.HostType.GetField("_terminalScreen",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(h.Host,screen.Screen);
        var prefix=Read("actual-pasted-prefix.vt");var raw=Read("bridge.raw");
        Assert.Equal("f8327edf23ab1b9a5d0824dade840a6ed010a319396d9ad59bdb9cddd0c8bab2",Convert.ToHexString(SHA256.HashData(prefix)).ToLowerInvariant());
        Assert.Equal(raw[439..4772],prefix);
        screen.Feed(prefix);
        var view=screen.Capture();
        Assert.True(GmSynchronizedTerminalPresentationTests.Value<bool>(view,"Reliable"));
        Assert.Equal(10,GmSynchronizedTerminalPresentationTests.Value<int>(view,"CursorRow"));
        Assert.Equal(68,GmSynchronizedTerminalPresentationTests.Value<int>(view,"CursorColumn"));
        var cells=GmSynchronizedTerminalPresentationTests.Value<string[]>(view,"Cells");
        var colors=GmSynchronizedTerminalPresentationTests.Value<int[][]>(view,"Foreground");
        Assert.StartsWith(" BUILD",cells[12]);
        for(var row=5;row<=10;row++)
            for(var col=0;col<cells[row].Length;col++)
                if(cells[row][col]!=' ')Assert.Equal(-1,colors[row][col]);
        using var config=JsonDocument.Parse(Read("play/game_session/config.json"));
        var profile=config.RootElement.GetProperty("GmCliInputProfile").Deserialize<GmCliInputProfile>()!;
        var snapshotType=h.HostType.GetNestedType("PromptSnapshot",BindingFlags.NonPublic)!;
        var operationType=h.HostType.GetNestedType("PromptOperation",BindingFlags.NonPublic)!;
        var snapshot=Activator.CreateInstance(snapshotType,["causal-prefix","turn","revision",h.BindingId,"owned public prompt",true,profile])!;
        var op=Activator.CreateInstance(operationType,[snapshot,h.Binding])!;
        object?[] arguments=[op,null,0,0];
        Assert.True((bool)h.Method("MiniDraftRegion")!.Invoke(h.Host,arguments)!,
            "Completed exact six-row draft uses known SGR39 default foreground; style alone must not refuse its region.");
        Assert.Equal(5,(int)arguments[2]!);Assert.Equal(10,(int)arguments[3]!);
        Assert.Empty(h.Input.Bytes);
    }

    private static byte[] Read(string relative)
    {
        var path=Path.Combine(TestRepoPaths.RepoRoot,"specs/1553-portable-local-storage/recovery/evidence/opencode-live/current-turn-paste-refusal",relative+".gz");
        using var file=File.OpenRead(path);using var gzip=new GZipStream(file,CompressionMode.Decompress);
        using var bytes=new MemoryStream();gzip.CopyTo(bytes);return bytes.ToArray();
    }
}
