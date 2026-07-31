using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class NpcTradeAuthorityValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly ValidationService _validator;

    public NpcTradeAuthorityValidationTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-npc-trade-authority-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        var fileSystem = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        _validator = new ValidationService(
            fileSystem,
            NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public void ValidateNpcTradeState_CanTradeRequiresExplicitMerchantProfileDespiteMerchantProse()
    {
        using var document = JsonDocument.Parse("""
        {
          "NPCId": "npc_quartermaster_001",
          "name": "Купец дальних дорог",
          "role": "Торговец и снабженец",
          "occupation": "Starship trader",
          "class": "Black-market vendor",
          "tradeState": {
            "canTrade": true
          }
        }
        """);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateNpcTradeState",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(_validator, [document.RootElement, "npc", issues]);

        Assert.Contains(issues, issue =>
            issue.Code == "npc_trade_requires_valid_profile" &&
            issue.FilePath == "npc.tradeState.merchantProfile" &&
            issue.Actual == "missing");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Best-effort cleanup of a test-only directory.
        }
    }
}
