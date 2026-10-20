using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks every detached story proof-image field without creating game authority or game fixtures.
/// </summary>
public sealed class JointStoryProofImageVerifierTests
{
    /// <summary>
    /// Requires exact current proof contents and preserves both input images on success or refusal.
    /// </summary>
    /// <param name="change">
    /// The one field to change, or an exact/case-comparer control.
    /// </param>
    [Theory]
    [InlineData("exact")]
    [InlineData("disposition")]
    [InlineData("path")]
    [InlineData("row")]
    [InlineData("bytes")]
    [InlineData("absent-bytes")]
    [InlineData("missing-history")]
    [InlineData("extra-history")]
    [InlineData("history-value")]
    [InlineData("history-key")]
    [InlineData("case-insensitive")]
    [InlineData("case-sensitive")]
    public void ExactImage_RefusesEveryChangedFieldWithoutMutation(string change)
    {
        var comparer = change == "case-sensitive" ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var expectedHistories = new Dictionary<string, string>(comparer)
        {
            ["stories/one.jsonl"] = "ABC", ["game_state/history/chat_log.json"] = "DEF"
        };
        var currentHistories = expectedHistories.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        byte[] expected = [1, 2, 3];
        byte[]? current = expected.ToArray();
        var path = "stories/one.jsonl";
        var row = "exact-row";
        var appended = true;
        switch (change)
        {
            case "disposition": appended = false; break;
            case "path": path = "stories/other.jsonl"; break;
            case "row": row = "changed-row"; break;
            case "bytes": current![1] = 9; break;
            case "absent-bytes": current = null; break;
            case "missing-history": currentHistories.Remove("stories/one.jsonl"); break;
            case "extra-history": currentHistories.Add("stories/extra.jsonl", "GHI"); break;
            case "history-value": currentHistories["stories/one.jsonl"] = "CHANGED"; break;
            case "history-key": currentHistories.Remove("stories/one.jsonl"); currentHistories.Add("stories/other.jsonl", "ABC"); break;
            case "case-insensitive":
            case "case-sensitive":
                currentHistories.Remove("stories/one.jsonl"); currentHistories.Add("STORIES/ONE.JSONL", "ABC"); break;
        }
        var beforeExpected = expected.ToArray();
        var beforeCurrent = current?.ToArray();
        var beforeExpectedHistories = expectedHistories.ToArray();
        var beforeCurrentHistories = currentHistories.ToArray();
        void Verify() => JointStoryProofImageVerifier.RequireExact("stories/one.jsonl", "exact-row", expected,
            expectedHistories, appended, path, row, current, currentHistories);
        if (change is "exact" or "case-insensitive") Verify();
        else Assert.Equal("joint_story_proof_current_changed", Assert.Throws<InvalidDataException>(Verify).Message);
        Assert.Equal(beforeExpected, expected);
        Assert.Equal(beforeCurrent, current);
        Assert.Equal(beforeExpectedHistories, expectedHistories.ToArray());
        Assert.Equal(beforeCurrentHistories, currentHistories.ToArray());
    }
}
