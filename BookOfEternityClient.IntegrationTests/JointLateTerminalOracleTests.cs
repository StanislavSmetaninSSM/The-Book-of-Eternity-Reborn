using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using Xunit;
using Xunit.Sdk;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Rejects profile false positives using tiny ordinary DATA observations instead of gameplay preparation.
/// </summary>
public sealed class JointLateTerminalOracleTests
{
    /// <summary>
    /// Accepts omitted nullable response fields when retained interface DATA supplies explicit null values.
    /// </summary>
    [Fact]
    public void InterfaceOracleAcceptsOmittedNullableFields()
    {
        using var expected = JsonDocument.Parse("{\"image_prompt\":null,\"dialogueOptions\":null}");
        using var actual = JsonDocument.Parse("{}");
        JointLateTerminalAssertions.VerifyInterfaceResponse(actual.RootElement, expected.RootElement);
    }

    /// <summary>
    /// Requires the repeat oracle to reject directory or session drift even when files, response and turn match.
    /// </summary>
    /// <param name="fault">
    /// None preserves the observation; directory-add, directory-remove or session changes only that field.
    /// </param>
    [Theory]
    [InlineData("none")]
    [InlineData("directory-add")]
    [InlineData("directory-remove")]
    [InlineData("session")]
    public void RepeatOracleRejectsDirectoryAndSessionDrift(string fault)
    {
        var before = new JointLateTerminalObservation(new Dictionary<string, string> { ["game_session/file"] = "1:hash" },
            ["game_session", "game_session/empty"], "session", 43, "{\"response\":\"retained\"}");
        var after = fault switch
        {
            "directory-add" => before with { Directories = [..before.Directories, "game_session/new-empty"] },
            "directory-remove" => before with { Directories = ["game_session"] },
            "session" => before with { SessionId = "foreign" },
            _ => before with { }
        };
        if (fault == "none") JointLateTerminalAssertions.VerifyInertRepeat(before, after);
        else Assert.ThrowsAny<XunitException>(() => JointLateTerminalAssertions.VerifyInertRepeat(before, after));
    }

    /// <summary>
    /// Requires complete normalized interface options and order, not merely matching array length.
    /// </summary>
    /// <param name="fault">
    /// None supplies normalized accepted DATA; other values change one option field, order or the image prompt.
    /// </param>
    [Theory]
    [InlineData("none")]
    [InlineData("text")]
    [InlineData("input")]
    [InlineData("category")]
    [InlineData("option-id")]
    [InlineData("order")]
    [InlineData("image")]
    public void InterfaceOracleRejectsSameCountWrongContentAndOrder(string fault)
    {
        var expected = JsonSerializer.SerializeToElement(new
        {
            image_prompt = "accepted-image",
            dialogueOptions = new[]
            {
                new DialogueOption { OptionId = "first", Text = "Первая\\nстрока", InputValue = "ход\\nодин", Category = "neutral" },
                new DialogueOption { OptionId = "second", Text = "Вторая строка", InputValue = "ход два", Category = "interact" }
            }
        }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        var actual = new[]
        {
            new DialogueOption { OptionId = "first", Text = "Первая\nстрока", InputValue = "ход\nодин", Category = "neutral" },
            new DialogueOption { OptionId = "second", Text = "Вторая строка", InputValue = "ход два", Category = "interact" }
        };
        if (fault == "text") actual[0].Text = "wrong text";
        if (fault == "input") actual[0].InputValue = "wrong input";
        if (fault == "category") actual[0].Category = "wrong category";
        if (fault == "option-id") actual[0].OptionId = "wrong id";
        if (fault == "order") Array.Reverse(actual);
        var response = JsonSerializer.SerializeToElement(new
        {
            image_prompt = fault == "image" ? "wrong image" : "accepted-image",
            dialogueOptions = actual
        }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        if (fault == "none") JointLateTerminalAssertions.VerifyInterfaceResponse(response, expected);
        else Assert.ThrowsAny<XunitException>(() => JointLateTerminalAssertions.VerifyInterfaceResponse(response, expected));
    }
}
