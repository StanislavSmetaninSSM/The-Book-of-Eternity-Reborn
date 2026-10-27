using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualSelfTreatmentSchedulerSourceTests
{
    [Theory]
    [InlineData("{\"currentTimeInMinutes\":123}", true, 123, false)]
    [InlineData("{\"currentTimeInMinutes\":123}", false, 123, false)]
    [InlineData("{\"setWorldTime\":{\"currentTimeInMinutes\":321}}", true, 321, false)]
    [InlineData("{\"timeChange\":5}", true, 105, false)]
    [InlineData("{\"timeChange\":5}", false, 100, true)]
    [InlineData("{\"timeChange\":-1000}", true, 0, false)]
    [InlineData("{\"setWorldTime\":{\"year\":1,\"dayOfMonth\":1,\"monthName\":\"месяц\"},\"timeChange\":5}", true, 105, false)]
    [InlineData("{\"setWorldTime\":{\"year\":1,\"dayOfMonth\":1,\"monthName\":\"месяц\",\"timeOfDay\":\"утро\"},\"timeChange\":5}", true, 100, true)]
    [InlineData("{\"year\":1,\"dayOfMonth\":1,\"monthName\":\"месяц\",\"timeOfDay\":\"утро\",\"timeChange\":5}", true, 105, false)]
    [InlineData("{}", true, 100, true)]
    [InlineData(null, false, 100, true)]
    public void RealWorldTimeParserPreservesAbsoluteIncrementalAndAfterlifeSemantics(
        string? json, bool allowIncrement, int expectedMinutes, bool expectedUnresolved)
    {
        var result = ProgressionScheduleService.ResolveFrozenWorldTime(
            json is null ? null : System.Text.Encoding.UTF8.GetBytes(json), 100, allowIncrement);
        Assert.Equal(expectedMinutes, result.Minutes);
        Assert.Equal(expectedUnresolved, result.HasUnresolvedAbsoluteOverride);
    }
}
