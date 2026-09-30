using System.Reflection;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GameEngineSpiritualReadyTests
{
    /// <summary>
    /// Keeps continuation-bearing responses out of the ordinary repair acknowledgement route.
    /// </summary>
    /// <param name="envelopeName">
    /// Exact or case-aliased continuation property that ordinary repair must reject even when its value is <see langword="null"/>.
    /// </param>
    [Theory]
    [InlineData("spiritualWoundContinuation")]
    [InlineData("SpiritualWoundContinuation")]
    [InlineData("SPIRITUALWOUNDCONTINUATION")]
    public void OrdinaryRepair_RejectsUnsolicitedContinuation(string envelopeName)
    {
        var json = "{\"sessionId\":\"session\",\"requestId\":\"request\",\"turnNumber\":42," +
            "\"updatedAtUtc\":\"2026-09-27T00:00:00Z\",\"" + envelopeName + "\":null}";

        Assert.Null(ReadOrdinaryReady(json));
    }

    /// <summary>
    /// Preserves ordinary repair acknowledgements without a continuation envelope.
    /// </summary>
    [Fact]
    public void OrdinaryRepair_AcceptsOriginalMetadataShape()
    {
        var ready = ReadOrdinaryReady("""
            {"sessionId":"session","requestId":"request","turnNumber":42,
             "updatedAtUtc":"2026-09-27T00:00:00Z","note":"Исправлено."}
            """);

        Assert.NotNull(ready);
        Assert.Equal("session", ready.GetType().GetProperty("SessionId")!.GetValue(ready));
        Assert.Equal("request", ready.GetType().GetProperty("RequestId")!.GetValue(ready));
        Assert.Equal(42, ready.GetType().GetProperty("TurnNumber")!.GetValue(ready));
    }

    /// <summary>
    /// Invokes the actual ordinary GameEngine parser without creating a session or replacing its behavior.
    /// </summary>
    /// <param name="json">
    /// Complete physical Ready text to parse.
    /// </param>
    /// <returns>
    /// The private ordinary metadata object, or <see langword="null"/> when that parser rejects the response.
    /// </returns>
    private static object? ReadOrdinaryReady(string json) => typeof(GameEngine)
        .GetMethod("ReadValidationRepairReady", BindingFlags.Static | BindingFlags.NonPublic)!
        .Invoke(null, [json]);
}
