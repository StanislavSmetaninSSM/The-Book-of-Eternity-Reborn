using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
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
    /// Requires unchanged files, directories, response and complete runtime tuple around a repeated actual late-owner call.
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
        Assert.Equal(before.Directories, after.Directories);
        Assert.Equal(before.SessionId, after.SessionId);
        Assert.Equal(before.ResponseJson, after.ResponseJson);
        Assert.Equal(before.TurnNumber, after.TurnNumber);
    }

    /// <summary>
    /// Requires the complete normalized option objects in order and the expected nullable image prompt.
    /// </summary>
    /// <param name="response">
    /// The independent snapshot of the actual installed response.
    /// </param>
    /// <param name="expectedInterface">
    /// The retained accepted interface DATA; absent or null optional fields use the production omission semantics.
    /// </param>
    internal static void VerifyInterfaceResponse(JsonElement response, JsonElement expectedInterface)
    {
        var expectedImage = expectedInterface.TryGetProperty("image_prompt", out var image) ? image.GetString() : null;
        var actualImage = response.TryGetProperty("image_prompt", out var installedImage) ? installedImage.GetString() : null;
        Assert.Equal(expectedImage, actualImage);
        DialogueOption[]? expectedOptions = null;
        if (expectedInterface.TryGetProperty("dialogueOptions", out var options) && options.ValueKind == JsonValueKind.Array)
        {
            expectedOptions = JsonSerializer.Deserialize<DialogueOption[]>(options.GetRawText());
            if (expectedOptions is not null)
                foreach (var option in expectedOptions)
                {
                    var text = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(option.Text);
                    var input = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(option.InputValue);
                    option.InputValue = DialogueOptionControlTagNormalizer.ResolveInputValue(text, input);
                    option.Text = DialogueOptionControlTagNormalizer.NormalizeVisibleText(text);
                }
        }
        var expected = JsonSerializer.SerializeToNode(expectedOptions, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        var actual = response.TryGetProperty("dialogueOptions", out var installed)
            ? JsonNode.Parse(installed.GetRawText()) : null;
        Assert.True(JsonNode.DeepEquals(expected, actual), "Installed dialogue option contents or order differ from the normalized accepted interface DATA.");
    }
}
