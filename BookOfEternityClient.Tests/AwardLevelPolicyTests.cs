using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AwardLevelPolicyTests
{
    [Theory]
    [InlineData("{}", null)]
    [InlineData("{\"level\":2,\"playerLevel\":9}", 2)]
    [InlineData("{\"level\":\"2\",\"playerLevel\":9}", 9)]
    [InlineData("{\"level\":null,\"playerLevel\":9}", 9)]
    [InlineData("{\"level\":true,\"playerLevel\":9}", 9)]
    [InlineData("{\"LEVEL\":2,\"PlayerLevel\":9}", null)]
    [InlineData("{\"level\":0,\"playerLevel\":9}", 0)]
    [InlineData("{\"level\":-3,\"playerLevel\":9}", -3)]
    [InlineData("{\"level\":-2147483648}", int.MinValue)]
    [InlineData("{\"level\":2147483647}", int.MaxValue)]
    [InlineData("{\"level\":2,\"level\":5}", 5)]
    [InlineData("{\"level\":2,\"level\":\"2\",\"playerLevel\":4}", 4)]
    [InlineData("{\"playerLevel\":7}", 7)]
    public void AwardExperiencePolicy_ReturnsExactNumericPriority(string json, int? expected) =>
        Assert.Equal(expected, ReadPolicy("ReadAwardExperienceLevel", json));

    [Theory]
    [InlineData("{}", null)]
    [InlineData("{\"level\":2,\"playerLevel\":9}", 2)]
    [InlineData("{\"playerLevel\":7}", null)]
    [InlineData("{\"level\":\"7\"}", null)]
    [InlineData("{\"level\":0}", 0)]
    [InlineData("{\"level\":-2}", -2)]
    [InlineData("{\"level\":-2147483648}", int.MinValue)]
    [InlineData("{\"level\":2147483647}", int.MaxValue)]
    [InlineData("{\"Level\":7}", null)]
    [InlineData("{\"level\":2,\"level\":false}", null)]
    public void AwardStatusPolicy_ReturnsOnlyExactNumericLevel(string json, int? expected) =>
        Assert.Equal(expected, ReadPolicy("ReadAwardStatusLevel", json));

    [Theory]
    [InlineData("ReadAwardExperienceLevel", "{\"level\":2.5,\"playerLevel\":7}")]
    [InlineData("ReadAwardExperienceLevel", "{\"level\":2147483648,\"playerLevel\":7}")]
    [InlineData("ReadAwardStatusLevel", "{\"level\":2.5}")]
    [InlineData("ReadAwardStatusLevel", "{\"level\":2147483648}")]
    public void AwardParsedPolicy_PreservesConversionFailure(string method, string json) =>
        Assert.Throws<FormatException>(() => ReadPolicy(method, json));

    [Theory]
    [InlineData("ReadAwardExperienceLevel", "[]")]
    [InlineData("ReadAwardExperienceLevel", "null")]
    [InlineData("ReadAwardExperienceLevel", "2")]
    [InlineData("ReadAwardStatusLevel", "[]")]
    [InlineData("ReadAwardStatusLevel", "null")]
    [InlineData("ReadAwardStatusLevel", "2")]
    public void AwardParsedPolicy_PreservesNonObjectFailure(string method, string json) =>
        Assert.Throws<InvalidOperationException>(() => ReadPolicy(method, json));

    private static int? ReadPolicy(string methodName, string json)
    {
        using var document = JsonDocument.Parse(json);
        var method = typeof(CharacteristicsService).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic,
            binder: null, types: [typeof(JsonElement)], modifiers: null);
        Assert.NotNull(method);
        Assert.Equal(typeof(int?), method.ReturnType);
        try { return (int?)method.Invoke(null, [document.RootElement]); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

}
