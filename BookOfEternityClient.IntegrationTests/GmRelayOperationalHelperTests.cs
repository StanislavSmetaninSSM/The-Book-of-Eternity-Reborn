using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class GmRelayOperationalHelperTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("write", "gm_prompt_delivery.json", true)]
    [InlineData("write", "gm_relay_apply_receipt.json", true)]
    [InlineData("write", "gm_prompt_delivery.json", false)]
    [InlineData("write", "gm_relay_apply_receipt.json", false)]
    [InlineData("files-modified", "gm_prompt_delivery.json", true)]
    [InlineData("files-modified", "gm_relay_apply_receipt.json", true)]
    [InlineData("files-modified", "gm_prompt_delivery.json", false)]
    [InlineData("files-modified", "gm_relay_apply_receipt.json", false)]
    public async Task OperationalRecords_RealCurrentHelperDeniesOnlyExactTargets(string operation, string name, bool reserved)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-relay-operational-helper-" + Guid.NewGuid().ToString("N"));
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var session = Path.Combine(root, "game_session");
        var relative = "game_state/control/" + name + (reserved ? "" : ".neighbor");
        var physical = Path.Combine(session, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
        byte[] before = "{\"marker\":\"owned-before\"}"u8.ToArray();
        await File.WriteAllBytesAsync(physical, before);
        var helper = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
        string Q(string value) => "'" + value.Replace("'", "''") + "'";
        // The shared scenario bootstraps genuine current generation/config, inserts only
        // ownership glue, and joins the actual dedicated helper beneath the original guardian.
        var command = string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Stop'",
            ". " + Q(helper),
            "Initialize-BoeGmTurnHelper -GameSessionPath " + Q(session),
            "$current = Read-BoeJson -RelativePath " + Q(relative),
            "if ($current.marker -cne 'owned-before') { throw 'Owned before-image prerequisite failed.' }",
            "Write-Output 'GM1559_REAL_HELPER_READ_PREREQUISITE_COMPLETE'",
            operation == "write"
                ? "Write-BoeJson -RelativePath " + Q(relative) + " -Data ([ordered]@{ marker = 'gm-after' })"
                // This is the actual policy consumer called first by Complete-BoeTurn;
                // no missing pending snapshot can masquerade as the intended refusal.
                : "Assert-BoeGmFilesModifiedEntries -FilesModified @(" + Q(relative) + ")",
            "Write-Output 'GM1559_REAL_HELPER_OPERATION_RETURNED'"
        });
        var diagnosticRoot = Path.Combine(TestRepoPaths.RepoRoot, "TestResults/native-terminal");
        HashSet<string> oldDirectories = Directory.Exists(diagnosticRoot)
            ? Directory.GetDirectories(diagnosticRoot).ToHashSet(StringComparer.Ordinal) : [];
        var result = await GmHelperContractScenario.RunOwnedAsync(command);
        var diagnostic = Assert.Single(Directory.GetDirectories(diagnosticRoot).Where(path => !oldDirectories.Contains(path) &&
            File.ReadAllText(Path.Combine(path, "original.ps1")) == command));
        var after = await File.ReadAllBytesAsync(physical);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Operation = operation, Path = relative, ExpectedDenied = reserved, result.ExitCode, result.StdOut, result.StdErr,
            ArtifactDirectory = diagnostic, RepoHelperPath = helper, RepoHelperSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(helper))),
            BeforeSha256 = Convert.ToHexString(SHA256.HashData(before)), AfterSha256 = Convert.ToHexString(SHA256.HashData(after)),
            BeforeBytes = before.Length, AfterBytes = after.Length,
            Scope = "ACTUAL_CURRENT_HELPER_WRITE_OR_FILESMODIFIED_POLICY_CONSUMER; NOT_COMPLETE_TURN_OR_CLIENT_ACCEPTANCE"
        }));
        Assert.Contains("GM1559_REAL_HELPER_READ_PREREQUISITE_COMPLETE", result.StdOut, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(session, "ready/turn_complete.json")));
        Assert.False(File.Exists(Path.Combine(session, "ready/turn_error.json")));
        if (reserved)
        {
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("client-owned runtime state", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(relative, result.StdErr + result.StdOut, StringComparison.Ordinal);
            Assert.DoesNotContain("GM1559_REAL_HELPER_OPERATION_RETURNED", result.StdOut, StringComparison.Ordinal);
            Assert.Equal(before, after);
        }
        else
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("GM1559_REAL_HELPER_OPERATION_RETURNED", result.StdOut, StringComparison.Ordinal);
            if (operation == "write")
            {
                using var written = JsonDocument.Parse(Encoding.UTF8.GetString(after));
                Assert.Equal("gm-after", written.RootElement.GetProperty("marker").GetString());
                Assert.NotEqual(Convert.ToHexString(SHA256.HashData(before)), Convert.ToHexString(SHA256.HashData(after)));
            }
            else Assert.Equal(before, after);
        }
    }
}
