using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Holds independently observed fixture and runtime DATA for an inert-repeat comparison.
/// </summary>
/// <param name="Images">
/// Exact relative file names, byte lengths and hashes captured for this observation.
/// </param>
/// <param name="Directories">
/// The captured ordinal inventory, including empty directories.
/// </param>
/// <param name="SessionId">
/// The observed runtime session identifier, without any authority assertion.
/// </param>
/// <param name="TurnNumber">
/// The observed runtime turn number.
/// </param>
/// <param name="ResponseJson">
/// The serialized installed response DATA captured independently of the mutable runtime.
/// </param>
internal sealed record JointLateTerminalObservation(
    IReadOnlyDictionary<string, string> Images, string[] Directories,
    string SessionId, int TurnNumber, string ResponseJson);

/// <summary>
/// Shares actual profile assertions with small adversarial DATA controls, without constructing a game.
/// </summary>
internal static class JointLateTerminalAssertions
{
    /// <summary>
    /// Compares the observations surrounding a repeated actual late-owner call.
    /// </summary>
    /// <param name="before">
    /// The independently captured observation immediately before the repeat.
    /// </param>
    /// <param name="after">
    /// The independently captured observation after the repeat returns.
    /// </param>
    internal static void VerifyInertRepeat(JointLateTerminalObservation before, JointLateTerminalObservation after)
    {
        Assert.Equal(before.Images.OrderBy(pair => pair.Key, StringComparer.Ordinal), after.Images.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal(before.ResponseJson, after.ResponseJson);
        Assert.Equal(before.TurnNumber, after.TurnNumber);
    }

    /// <summary>
    /// Checks installed interface response DATA against retained accepted interface output.
    /// </summary>
    /// <param name="response">
    /// The independent snapshot of the actual installed response.
    /// </param>
    /// <param name="expectedInterface">
    /// The retained accepted interface DATA, with optional image prompt and dialogue options.
    /// </param>
    internal static void VerifyInterfaceResponse(JsonElement response, JsonElement expectedInterface)
    {
        if (expectedInterface.TryGetProperty("image_prompt", out var image))
            Assert.Equal(image.GetString(), response.GetProperty("image_prompt").GetString());
        if (expectedInterface.TryGetProperty("dialogueOptions", out var options))
            Assert.Equal(options.GetArrayLength(), response.GetProperty("dialogueOptions").GetArrayLength());
    }
}
