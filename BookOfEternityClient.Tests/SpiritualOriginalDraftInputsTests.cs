using System.Reflection;
using System.Text;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies detached original draft images and their closed path and identity rules.
/// </summary>
public sealed class SpiritualOriginalDraftInputsTests
{
    /// <summary>
    /// Preserves exact bytes and distinct empty and absent images without retaining caller aliases.
    /// </summary>
    [Fact]
    public void OriginalInputs_DetachExactPresentEmptyAndAbsentImages()
    {
        var source = new byte[] { 0, 255, 10, 13 };
        var images = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
        {
            ["lore/probe.bin"] = new(true, source),
            ["world_profiles/empty.json"] = new(true, Array.Empty<byte>()),
            ["game_state/resources/uncreated.json"] = new(false, null)
        };
        var inventory = images.Keys.ToArray();
        var view = Create(inventory, images);
        source[0] = 99;
        inventory[0] = "lore/replaced.bin";
        images["lore/probe.bin"] = new CanonicalBeforeImage(true, new byte[] { 9 });
        Assert.Contains("lore/probe.bin", (IReadOnlyList<string>)view.GetType()
            .GetProperty("PathInventory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(view)!);
        Assert.Equal(new byte[] { 0, 255, 10, 13 }, Read(view, "lore/probe.bin").Bytes);
        var first = Read(view, "lore/probe.bin").Bytes!;
        first[0] = 88;
        Assert.Equal(new byte[] { 0, 255, 10, 13 }, Read(view, "lore/probe.bin").Bytes);
        Assert.True(Read(view, "world_profiles/empty.json").Existed);
        Assert.Empty(Read(view, "world_profiles/empty.json").Bytes!);
        Assert.False(Read(view, "game_state/resources/uncreated.json").Existed);
        Assert.Null(Read(view, "game_state/resources/uncreated.json").Bytes);
        Assert.Throws<KeyNotFoundException>(() => Read(view, "lore/unknown.json"));
    }

    /// <summary>
    /// Keeps immutable A and unchanged paths intact when a detached continuation layer replaces one image.
    /// </summary>
    [Fact]
    public void OriginalInputs_ImageChangesCreateDetachedLayerWithoutChangingOriginal()
    {
        var originals = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
        {
            ["lore/probe.json"] = new(true, new byte[] { 1 }),
            ["output/absent.json"] = new(false, null)
        };
        var initial = Create(originals.Keys.ToArray(), originals);
        var changed = new byte[] { 2, 3 };
        var layer = WithChanges(initial, new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
        {
            ["lore/probe.json"] = new(true, changed)
        });
        changed[0] = 9;
        Assert.Equal(new byte[] { 1 }, Read(initial, "lore/probe.json").Bytes);
        Assert.Equal(new byte[] { 2, 3 }, Read(layer, "lore/probe.json").Bytes);
        Assert.False(Read(layer, "output/absent.json").Existed);
        Assert.Throws<ArgumentException>(() => WithChanges(initial,
            new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
            {
                ["lore/foreign.json"] = new(true, new byte[] { 1 })
            }));
    }

    /// <summary>
    /// Rejects unsafe, ambiguous and physical control paths while retaining ordinary pending inputs.
    /// </summary>
    [Fact]
    public void OriginalInputs_RejectUnsafeAliasesAndExcludedControlPayload()
    {
        foreach (var path in new[]
                 {
                     "../escape.json", "lore\\escaped.json", "lore//double.json",
                     "game_state/control/pending_turn_snapshot.json",
                     "game_state/control/pending_turn_snapshot/copy.json",
                     "game_state/control/spiritual_wound_capture_checkpoint.json",
                     "game_state/control/validation_auto_rollback_report.json",
                     "game_state/control/explorer_local_turn_rollback/browser_write/123/browser_write_manifest.json",
                     "game_state/control/explorer_local_turn_rollback/browser_write/123/browser_write_committed.marker",
                     "Lore/independent.bin", "Output/unread.bin"
                 })
        {
            var images = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
            {
                [path] = new(true, new byte[] { 1 })
            };
            Assert.Throws<ArgumentException>(() => Create([path], images));
        }

        var aliases = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
        {
            ["lore/Archive.json"] = new(true, new byte[] { 1 }),
            ["lore/archive.json"] = new(true, new byte[] { 2 })
        };
        Assert.Throws<ArgumentException>(() => Create(aliases.Keys.ToArray(), aliases));
    }

    /// <summary>
    /// Requires all four coordinates of the original capture identity.
    /// </summary>
    [Fact]
    public void OriginalInputs_IdentityRequiresExactOriginalTuple()
    {
        var images = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
        {
            ["output/prompt.txt"] = new(true, new byte[] { 7 }),
            ["game_state/control/pending_archive_consultation_request.json"] = new(true, new byte[] { 8 })
        };
        var view = Create(images.Keys.ToArray(), images);
        Assert.Equal(new byte[] { 8 },
            Read(view, "game_state/control/pending_archive_consultation_request.json").Bytes);
        Assert.True(Matches(view, "session-1", "request-1", "snapshot-1", 42));
        Assert.False(Matches(view, "session-2", "request-1", "snapshot-1", 42));
        Assert.False(Matches(view, "session-1", "request-2", "snapshot-1", 42));
        Assert.False(Matches(view, "session-1", "request-1", "snapshot-2", 42));
        Assert.False(Matches(view, "session-1", "request-1", "snapshot-1", 43));
    }

    /// <summary>
    /// Decodes retained current text with byte-order-mark detection while keeping absent and empty images distinct.
    /// </summary>
    /// <param name="utf16">
    /// Selects a UTF-16 byte-order mark when <see langword="true"/>; otherwise selects UTF-8.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalInputs_ReadTextMatchesOrdinaryDecoderAndPreservesAbsence(bool utf16)
    {
        var encoding = utf16 ? Encoding.Unicode : new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var original = encoding.GetPreamble().Concat(encoding.GetBytes("Лес\nснова")).ToArray();
        var images = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
        {
            ["lore/present.txt"] = new(true, original),
            ["lore/empty.txt"] = new(true, Array.Empty<byte>()),
            ["lore/absent.txt"] = new(false, null)
        };
        var view = Create(images.Keys.ToArray(), images);
        original[0] = 0;

        Assert.Equal("Лес\nснова", ReadText(view, "lore/present.txt"));
        Assert.Equal(string.Empty, ReadText(view, "lore/empty.txt"));
        Assert.Null(ReadText(view, "lore/absent.txt"));
        Assert.Throws<KeyNotFoundException>(() => ReadText(view, "lore/unknown.txt"));
    }

    private static object Create(IReadOnlyList<string> inventory,
        IReadOnlyDictionary<string, CanonicalBeforeImage> images)
    {
        var type = typeof(ValidationService).Assembly.GetType(
            "BookOfEternityClient.Services.SpiritualOriginalDraftInputs");
        Assert.NotNull(type);
        var method = type.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try
        {
            return method.Invoke(null,
                ["session-1", "request-1", "snapshot-1", 42, inventory, images])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static CanonicalBeforeImage Read(object view, string path)
    {
        var method = view.GetType().GetMethod("ReadImage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try
        {
            return Assert.IsType<CanonicalBeforeImage>(method.Invoke(view, [path]));
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    /// <summary>
    /// Invokes the detached image-layer builder and preserves its original exception for assertions.
    /// </summary>
    /// <param name="view">
    /// Original immutable draft view.
    /// </param>
    /// <param name="changes">
    /// Exact path/image replacements to apply to a new view.
    /// </param>
    /// <returns>
    /// Detached replacement view.
    /// </returns>
    private static object WithChanges(object view,
        IReadOnlyDictionary<string, CanonicalBeforeImage> changes)
    {
        var method = view.GetType().GetMethod("WithImageChanges", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try
        {
            return method.Invoke(view, [changes])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    /// <summary>
    /// Invokes the retained text reader while preserving its original exception for assertions.
    /// </summary>
    /// <param name="view">
    /// Detached original draft under test.
    /// </param>
    /// <param name="path">
    /// Exact registered or deliberately unknown path.
    /// </param>
    /// <returns>
    /// Decoded text, or <see langword="null"/> for registered absence.
    /// </returns>
    private static string? ReadText(object view, string path)
    {
        var method = view.GetType().GetMethod("ReadText", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try
        {
            return (string?)method.Invoke(view, [path]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static bool Matches(object view, string session, string request, string snapshot, int turn)
    {
        var method = view.GetType().GetMethod("MatchesIdentity", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(view, [session, request, snapshot, turn]));
    }
}
