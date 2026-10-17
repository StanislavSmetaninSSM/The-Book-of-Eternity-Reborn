using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Exact reservation policy, without introducing any relay record reader or writer.</summary>
public sealed class GmRelayOperationalProtectionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("repair", "game_state/control/gm_prompt_delivery.json", true)]
    [InlineData("repair", "game_state/control/gm_relay_apply_receipt.json", true)]
    [InlineData("repair", "game_state/control/gm_prompt_delivery.json.neighbor", false)]
    [InlineData("repair", "game_state/control/gm_relay_apply_receipt.json.neighbor", false)]
    [InlineData("recoverable", "game_state/control/gm_prompt_delivery.json", false)]
    [InlineData("recoverable", "game_state/control/gm_relay_apply_receipt.json", false)]
    [InlineData("recoverable", "game_state/control/gm_prompt_delivery.json.neighbor", true)]
    [InlineData("recoverable", "game_state/control/gm_relay_apply_receipt.json.neighbor", true)]
    [InlineData("validation", "game_state/control/gm_prompt_delivery.json", true)]
    [InlineData("validation", "game_state/control/gm_relay_apply_receipt.json", true)]
    [InlineData("validation", "game_state/control/gm_prompt_delivery.json.neighbor", false)]
    [InlineData("validation", "game_state/control/gm_relay_apply_receipt.json.neighbor", false)]
    [InlineData("surface", "game_state/control/gm_prompt_delivery.json", true)]
    [InlineData("surface", "game_state/control/gm_relay_apply_receipt.json", true)]
    [InlineData("surface", "game_state/control/gm_prompt_delivery.json.neighbor", false)]
    [InlineData("surface", "game_state/control/gm_relay_apply_receipt.json.neighbor", false)]
    public void OperationalRecords_ActualPrivatePolicyIsExact(string owner, string path, bool expected)
    {
        var (type, name) = owner switch
        {
            "repair" => (typeof(GameEngine), "IsClientOwnedRepairResubmissionPath"),
            "recoverable" => (typeof(GameEngine), "IsRecoverableFilesModifiedPath"),
            "validation" => (typeof(ValidationService), "IsClientOwnedSurfaceValidationPath"),
            "surface" => (typeof(ValidationService), "IsClientOwnedSurfacePath"),
            _ => throw new ArgumentOutOfRangeException(nameof(owner))
        };
        var method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal(typeof(bool), method.ReturnType);
        Assert.Equal(typeof(string), Assert.Single(method.GetParameters()).ParameterType);
        var actual = Assert.IsType<bool>(method.Invoke(null, [path]));
        output.WriteLine(JsonSerializer.Serialize(new { Owner = type.FullName, Method = name, Path = path, Expected = expected, Actual = actual }));
        Assert.Equal(expected, actual);
    }
}
