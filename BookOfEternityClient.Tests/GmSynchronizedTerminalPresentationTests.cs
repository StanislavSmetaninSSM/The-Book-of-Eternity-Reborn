using System.IO.Compression;
using System.Reflection;
using System.Text;
using Xunit;

namespace BookOfEternityClient.Tests;

// Actual consumed bridge parser/pump and immutable observed CLI bytes. No native
// process, provider, player clipboard/audio or fabricated production Ready.
public sealed class GmSynchronizedTerminalPresentationTests
{
    private const string Escape = "\u001b";

    [Fact]
    public async Task ActualOutputPump_PinnedStartup_CommitsFocusedUnicodeCapableFrame()
    {
        await using var host = new GmBridgePromptOperationTests.PromptHostFixture();
        using var parser = new ScreenProbe(host.BindingId);
        host.HostType.GetField("_terminalScreen", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host.Host, parser.Screen);
        using var output = new HeldBytes(ActualTranscript("startup"));
        using var forwarded = new MemoryStream();
        var pump = (Task)host.Invoke("PumpOutputAsync", output, forwarded, CancellationToken.None)!;
        try
        {
            await output.Consumed.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var view = parser.Capture();
            Assert.True(Value<bool>(view, "Reliable"), "Pinned startup must become a reliable completed actual synchronized frame.");
            Assert.Equal(host.BindingId, Value<string>(view, "BindingId"));
            Assert.Contains("Ask anything...", Value<string>(view, "Text"));
            Assert.Contains("BUILD", Value<string>(view, "Text"));
            Assert.Equal(5, Value<int>(view, "CursorRow"));
            Assert.Equal(0, Value<int>(view, "CursorColumn"));
            Assert.True(Value<bool>(view, "CursorVisible"));
            Assert.Equal(ActualTranscript("startup"), forwarded.ToArray());
        }
        finally { output.Release.TrySetResult(); await pump.WaitAsync(TimeSpan.FromSeconds(3)); }
        Assert.False(Value<bool>(parser.Capture(), "Reliable"), "Actual EOF withdraws observation.");
    }

    [Fact]
    public void ActualEditorTranscript_DynamicFooterAndUnicode_NoPasteCursorAssumption()
    {
        using var p = new ScreenProbe("original");
        p.Feed(ActualTranscript("draft-observed"));
        var view = p.Capture();
        Assert.True(Value<bool>(view, "Reliable"));
        Assert.Contains("Кириллица café; UpdateGuardians", Value<string>(view, "Text"));
        Assert.Equal(10, Value<int>(view, "CursorRow"));
        Assert.Equal(12, Value<int>(view, "CursorColumn")); // Actual restored cursor, different from paste.
        Assert.Contains(" BUILD", Value<string[]>(view, "Cells")[12]);
    }

    [Theory]
    [InlineData("\u001b[?1049h")]
    [InlineData("\u001b]8;;https://unknown.invalid\u0007")]
    [InlineData("\u001b[999;1H")]
    [InlineData("🙂")]
    public void UnknownSequenceGeometryOrWidth_CannotBeClearedByAnotherKnownFrame(string unknown)
    {
        using var p = new ScreenProbe("original");
        p.Feed(ActualTranscript("startup"));
        p.Feed(Encoding.UTF8.GetBytes(unknown));
        p.Feed(Encoding.UTF8.GetBytes(Escape + "[?2026h" + Escape + "[1;1H" + Escape + "[?2026l"));
        Assert.False(Value<bool>(p.Capture(), "Reliable"));
    }

    [Fact]
    public void SplitUtf8AndSynchronizedFrame_DoNotExposePartialObservations()
    {
        using var p = new ScreenProbe("original");
        p.Feed(ActualTranscript("startup"));
        var old = Value<long>(p.Capture(), "Revision");
        p.Feed(Encoding.UTF8.GetBytes(Escape + "[?2026h" + Escape + "[6;1Hcaf"));
        p.Feed([0xc3]);
        Assert.False(Value<bool>(p.Capture(), "Reliable"));
        Assert.Equal(old, Value<long>(p.Capture(), "Revision"));
        p.Feed([0xa9]);
        Assert.False(Value<bool>(p.Capture(), "Reliable"));
        p.Feed(Encoding.UTF8.GetBytes(Escape + "[?2026l"));
        Assert.True(Value<bool>(p.Capture(), "Reliable"));
        Assert.True(Value<long>(p.Capture(), "Revision") > old);
        Assert.StartsWith("café", Value<string[]>(p.Capture(), "Cells")[5]);
    }

    [Fact]
    public void ResizeAndFault_WithdrawOriginalObservationPermanently()
    {
        using var p = new ScreenProbe("original");
        p.Feed(ActualTranscript("startup"));
        p.Type.GetMethod("Resize", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(p.Screen, [98, 25]);
        Assert.False(Value<bool>(p.Capture(), "Reliable"));
        p.Type.GetMethod("Fault", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(p.Screen, null);
        p.Feed(ActualTranscript("startup"));
        Assert.False(Value<bool>(p.Capture(), "Reliable"));
    }

    [Fact]
    public void LegacyNeutral_ViewRemainsSeparateFromOptionalPresentation()
    {
        using var p = new ScreenProbe("legacy", mini: false);
        p.Feed(Encoding.UTF8.GetBytes(Escape + "[2J" + Escape + "[HNEUTRAL READY\r\n> "));
        Assert.True(Value<bool>(p.Capture(), "Reliable"));
        Assert.Equal("NEUTRAL READY\n> ", Value<string>(p.Capture(), "Text"));
    }

    [Theory]
    [InlineData("\u001b[1;2 H")]
    [InlineData("\u001b[1;101H")]
    [InlineData("\u001b]66;w=1; \u001b\\")]
    [InlineData("\u001b]66;s=2; \u001b\\")]
    [InlineData("\u001b[38;2;1;2;3 m")]
    [InlineData("\u001bP+q4d73\u0007")]
    public void UnsupportedGrammarOrFirstFrameProbe_RefusesBeforeFirstCommit(string unsupported)
    {
        using var p = new ScreenProbe("original");
        // Known actual preamble leaves both probes at blank/home with a save;
        // the negative must test frame phase, not absence of another prerequisite.
        p.Feed(Encoding.UTF8.GetBytes(Escape + "[s"));
        if(unsupported.StartsWith(Escape + "]66;s=2;",StringComparison.Ordinal))
            p.Feed(Encoding.UTF8.GetBytes(Escape + "]66;w=1; " + Escape + "\\" + Escape + "[H"));
        p.Feed(Encoding.UTF8.GetBytes(Escape + "[?2026h" + unsupported + Escape + "[6;1Hcomposer" + Escape + "[?25h" + Escape + "[?2026l"));
        Assert.False(Value<bool>(p.Capture(), "Reliable"), "An unsupported sequence cannot certify even the first synchronized frame.");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\b")]
    [InlineData("\u001b[s")]
    [InlineData("\u001b[K")]
    [InlineData("Z")]
    public void UnmodelledPendingMarginTransition_CannotCertifyCellsOrCursor(string transition)
    {
        using var p = new ScreenProbe("original");
        p.Feed(Encoding.UTF8.GetBytes(Escape + "[?2026h" + new string('A', 100) + transition + Escape + "[6;1H" + Escape + "[?2026l"));
        Assert.False(Value<bool>(p.Capture(), "Reliable"));
    }

    [Theory]
    [InlineData("X\u001b[H")]
    [InlineData("\u001b[2;1H")]
    public void InitialSpaceProbe_RequiresActualBlankHomeStartupState(string preceding)
    {
        using var p = new ScreenProbe("original");
        p.Feed(Encoding.UTF8.GetBytes(Escape + "[s" + preceding + Escape + "]66;w=1; " + Escape + "\\" + Escape + "[?2026h" + Escape + "[6;1Hpartial" + Escape + "[?2026l"));
        Assert.False(Value<bool>(p.Capture(), "Reliable"));
    }

    [Fact]
    public void CompleteRowPendingMargin_CapturesPhysicalCursorAndPendingState()
    {
        using var p = new ScreenProbe("original");
        p.Feed(Encoding.UTF8.GetBytes(Escape + "[?2026h" + new string('A',100) + Escape + "[?25h" + Escape + "[?2026l"));
        Assert.Equal(99,Value<int>(p.Capture(),"CursorColumn"));
        Assert.True(Value<bool>(p.Capture(),"PendingWrap"));
    }

    internal static T Value<T>(object obj, string name) => (T)obj.GetType().GetProperty(name)!.GetValue(obj)!;
    internal static byte[] ActualTranscript(string receipt)
    {
        var path = Path.Combine(GmBridgePromptOperationTests.PromptHostFixture.Repo,
            "specs/1553-portable-local-storage/recovery/evidence/opencode-q1", receipt, "startup.raw.gz");
        using var file = File.OpenRead(path); using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var bytes = new MemoryStream(); gzip.CopyTo(bytes);
        var raw = bytes.ToArray(); var start = raw.AsSpan().IndexOf(Encoding.ASCII.GetBytes(Escape + "[?2031h"));
        var final = raw.AsSpan().LastIndexOf(Encoding.ASCII.GetBytes(Escape + "[?2026l")) + 8;
        Assert.True(start >= 0 && final > start, "Immutable actual inner PTY transcript bounds.");
        return raw[start..final]; // Outer bridge banner and later shutdown are separate output.
    }

    internal sealed class ScreenProbe : IDisposable
    {
        internal readonly Type Type;
        internal readonly object Screen;
        private delegate void FeedMethod(ReadOnlySpan<byte> bytes, ReadOnlySpan<char> text);
        private readonly FeedMethod _feed;
        private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
        internal ScreenProbe(string binding, bool mini = true)
        {
            var repo = GmBridgePromptOperationTests.PromptHostFixture.Repo;
            var build = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            Type = Assembly.LoadFrom(Path.Combine(repo, "BookOfEternityGMBridge/bin", build, "net8.0/BookOfEternityGMBridge.dll"))
                .GetType("BookOfEternityGMBridge.TerminalScreen", true)!;
            var extended = Type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                [typeof(string), typeof(string), typeof(int), typeof(int)], null);
            Screen = extended == null ? Activator.CreateInstance(Type, [binding])! : extended.Invoke([binding, mini ? "synchronized-mini-v1" : "", 100, 25]);
            _feed = Type.GetMethod("Feed", BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<FeedMethod>(Screen);
        }
        internal object Capture() => Type.GetMethod("Capture", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Screen, null)!;
        internal void Feed(byte[] bytes)
        {
            var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
            var count = _decoder.GetChars(bytes, 0, bytes.Length, chars, 0, false);
            _feed(bytes, chars.AsSpan(0, count));
        }
        public void Dispose() { }
    }

    private sealed class HeldBytes(byte[] bytes) : Stream
    {
        private int _position;
        internal readonly TaskCompletionSource Consumed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (_position == bytes.Length) { Consumed.TrySetResult(); await Release.Task.WaitAsync(token); return 0; }
            var count = Math.Min(13, Math.Min(buffer.Length, bytes.Length - _position)); // Split controls/UTF8 through real decoder.
            bytes.AsMemory(_position, count).CopyTo(buffer); _position += count; return count;
        }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException(); public override int Read(byte[] b, int o, int c) => throw new NotSupportedException();
        public override long Seek(long p, SeekOrigin o) => throw new NotSupportedException(); public override void SetLength(long l) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }
}
