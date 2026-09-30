using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks the shared arithmetic boundary without substituting pure values for signed owner authority.
/// </summary>
public sealed class SpiritualDependentCostProjectionTests
{
    /// <summary>
    /// Keeps payment before recovery and preserves an opposed interval until the cap makes it unique.
    /// </summary>
    /// <param name="before">
    /// Available points before payment.
    /// </param>
    /// <param name="cost">
    /// Required wound payment.
    /// </param>
    /// <param name="maximum">
    /// Canonical capacity.
    /// </param>
    /// <param name="outcome">
    /// Existing recovery outcome.
    /// </param>
    /// <param name="opposed">
    /// Whether an opposing pressure action restricts the gain to zero or one.
    /// </param>
    /// <param name="minimumAfter">
    /// First legal action-only result.
    /// </param>
    /// <param name="maximumAfter">
    /// Last legal action-only result; a different value preserves GM choice.
    /// </param>
    [Theory]
    [InlineData(3, 1, 6, "success", false, 5, 5)]
    [InlineData(3, 1, 6, "partial_success", false, 4, 4)]
    [InlineData(3, 1, 6, "no_effect", false, 2, 2)]
    [InlineData(6, 1, 6, "success", false, 6, 6)]
    [InlineData(3, 1, 6, "success", true, 2, 3)]
    [InlineData(6, 0, 6, "success", true, 6, 6)]
    [InlineData(int.MaxValue, 0, int.MaxValue, "success", false, int.MaxValue, int.MaxValue)]
    public void RecoveryAfter_PreservesExactLegalRange(int before, int cost, int maximum,
        string outcome, bool opposed, int minimumAfter, int maximumAfter)
    {
        var result = ValidationService.ProjectSpiritualRecoveryAfter(before, cost, maximum,
            outcome, opposed ? "pressure" : "recover_spiritual_power");
        Assert.NotNull(result);
        Assert.Equal(((long)minimumAfter, (long)maximumAfter, opposed), result.Value);
    }

    /// <summary>
    /// Does not let future recovery fund payment or synthesize a result without valid capacity.
    /// </summary>
    /// <param name="before">
    /// Available points before payment.
    /// </param>
    /// <param name="cost">
    /// Required payment, including the invalid negative case.
    /// </param>
    /// <param name="maximum">
    /// Canonical capacity or an invalid zero value.
    /// </param>
    [Theory]
    [InlineData(0, 1, 6)]
    [InlineData(3, -1, 6)]
    [InlineData(3, 1, 0)]
    public void RecoveryAfter_RejectsInvalidPrerequisites(int before, int cost, int maximum) =>
        Assert.Null(ValidationService.ProjectSpiritualRecoveryAfter(before, cost, maximum, "success", null));

    /// <summary>
    /// Applies the wound increment after the rounded special-art multiplier, preserving the existing cost formula.
    /// </summary>
    [Fact]
    public void ActionCost_AddsWoundAfterSpecialArtMultiplier()
    {
        var expected = ValidationService.ProjectSpiritualActionCost(new(3, 1), 1,
            new JsonObject { ["costMultiplierPercent"] = 125 }, 2);
        Assert.Equal((2, 5L), expected);
    }
}
