using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmTurnHelperContractTests
{
    [Fact]
    public void Helper_CompleteBoeTurnWritesCorrelatedTerminalSignal()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control"));

        try
        {
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-123",
                  "turnNumber": 42,
                  "playerAction": "test"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-123",
                  "turnNumber": 42
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.authority.json"),
                "{}",
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "Complete-BoeTurn -FilesModified @('output/narrative_response.json')"
            });

            var result = RunPowerShell(command);

            Assert.Equal(0, result.ExitCode);
            var readyPath = Path.Combine(session, "ready", "turn_complete.json");
            Assert.True(File.Exists(readyPath), result.StdErr + result.StdOut);

            using var document = JsonDocument.Parse(File.ReadAllText(readyPath, Encoding.UTF8));
            var rootElement = document.RootElement;
            Assert.Equal("test-session", rootElement.GetProperty("sessionId").GetString());
            Assert.Equal("request-123", rootElement.GetProperty("requestId").GetString());
            Assert.Equal(42, rootElement.GetProperty("turnNumber").GetInt32());
            Assert.Equal("success", rootElement.GetProperty("status").GetString());
            Assert.Equal("output/narrative_response.json", rootElement.GetProperty("filesModified")[0].GetString());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_CompleteBoeTurnRejectsMissingPendingSnapshotContext()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-missing-snapshot-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control"));

        try
        {
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-missing-snapshot",
                  "turnNumber": 44,
                  "playerAction": "test"
                }
                """,
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "Complete-BoeTurn -FilesModified @('output/narrative_response.json')"
            });

            var result = RunPowerShell(command);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("pending_turn_snapshot", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(session, "ready", "turn_complete.json")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_CompleteBoeTurnRejectsClientOwnedFilesModifiedEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-client-owned-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));

        try
        {
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-client-owned",
                  "turnNumber": 43,
                  "playerAction": "test"
                }
                """,
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "Complete-BoeTurn -FilesModified @('output/narrative_response.json', 'game_state/history/chat_log.json')"
            });

            var result = RunPowerShell(command);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("client-owned", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(session, "ready", "turn_complete.json")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_FailBoeTurnWritesCorrelatedTerminalSignalAndFailsShellCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-error-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control"));

        try
        {
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-err",
                  "turnNumber": 7,
                  "playerAction": "test"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-err",
                  "turnNumber": 7
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.authority.json"),
                "{}",
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "Fail-BoeTurn -ErrorMessage 'synthetic failure'"
            });

            var result = RunPowerShell(command);

            Assert.NotEqual(0, result.ExitCode);
            var readyPath = Path.Combine(session, "ready", "turn_error.json");
            Assert.True(File.Exists(readyPath), result.StdErr + result.StdOut);

            using var document = JsonDocument.Parse(File.ReadAllText(readyPath, Encoding.UTF8));
            var rootElement = document.RootElement;
            Assert.Equal("test-session", rootElement.GetProperty("sessionId").GetString());
            Assert.Equal("request-err", rootElement.GetProperty("requestId").GetString());
            Assert.Equal(7, rootElement.GetProperty("turnNumber").GetInt32());
            Assert.Equal("error", rootElement.GetProperty("status").GetString());
            Assert.Equal("synthetic failure", rootElement.GetProperty("error").GetString());
            Assert.Contains("synthetic failure", result.StdErr + result.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_DotSourceDoesNotEnableStrictModeForGmTurnScripts()
    {
        var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
        var helper = File.ReadAllText(helperPath, Encoding.UTF8);
        Assert.Contains("Set-StrictMode -Off", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-StrictMode -Version", helper, StringComparison.Ordinal);

        var command = string.Join("; ", new[]
        {
            ". " + QuotePowerShell(helperPath),
            "$journal = [pscustomobject]@{ npcId = 'npc_marius' }",
            "$missingLegacyId = $journal.NPCId",
            "$null = $missingLegacyId"
        });

        var result = RunPowerShell(command);

        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StdErr), result.StdErr);
    }

    [Fact]
    public void Helper_GetBoeJsonValueReadsFirstExistingJsonProperty()
    {
        var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
        var command = string.Join("; ", new[]
        {
            ". " + QuotePowerShell(helperPath),
            "$journal = [pscustomobject]@{ npcId = 'npc_marius'; name = 'Мариус' }",
            "$id = Get-BoeJsonValue -Object $journal -Names @('NPCId','npcId','id')",
            "if ($id -ne 'npc_marius') { throw \"Unexpected id: $id\" }",
            "$missing = Get-BoeJsonValue -Object $journal -Names @('missing') -Default 'fallback'",
            "if ($missing -ne 'fallback') { throw \"Unexpected fallback: $missing\" }"
        });

        var result = RunPowerShell(command);

        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StdErr), result.StdErr);
    }

    [Fact]
    public void Helper_WriteBoeJsonClampsDepthAbovePowerShellLimit()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-depth-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(session);

        try
        {
            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "$data = [ordered]@{ root = [ordered]@{ child = 'value' } }",
                "Write-BoeJson -RelativePath 'output/test.json' -Data $data -Depth 120",
                "$roundTrip = Read-BoeJson -RelativePath 'output/test.json'",
                "if ($roundTrip.root.child -ne 'value') { throw 'Roundtrip failed.' }"
            });

            var result = RunPowerShell(command);

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StdErr), result.StdErr);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_WriteBoeJsonRejectsClientOwnedRuntimeFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-client-owned-write-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "game_state", "history"));

        try
        {
            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "$data = [ordered]@{ entries = @() }",
                "Write-BoeJson -RelativePath 'game_state/history/chat_log.json' -Data $data"
            });

            var result = RunPowerShell(command);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("client-owned", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(session, "game_state", "history", "chat_log.json")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_WriteBoeJsonRejectsPendingTurnSnapshotAuthority()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-snapshot-authority-write-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control"));

        try
        {
            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "$data = [ordered]@{ sessionId = 'forbidden' }",
                "Write-BoeJson -RelativePath 'game_state/control/pending_turn_snapshot.authority.json' -Data $data"
            });

            var result = RunPowerShell(command);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("client-owned", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(session, "game_state", "control", "pending_turn_snapshot.authority.json")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_WriteBoeJsonRejectsMortalWorldProfileFilesInAfterlifeRealm()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-wrong-realm-write-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "game_state", "meta"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "factions"));

        try
        {
            File.WriteAllText(
                Path.Combine(session, "game_state", "meta", "soul_state.json"),
                """
                {
                  "currentRealm": "Chaos Sea"
                }
                """,
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "$data = [ordered]@{ factions = @() }",
                "Write-BoeJson -RelativePath 'game_state/factions/faction_core.json' -Data $data"
            });

            var result = RunPowerShell(command);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("wrong realm", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(session, "game_state", "factions", "faction_core.json")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_CompleteBoeTurnRejectsMortalWorldProfileFilesModifiedInAfterlifeRealm()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-wrong-realm-filesmodified-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "game_state", "meta"));
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));

        try
        {
            File.WriteAllText(
                Path.Combine(session, "game_state", "meta", "soul_state.json"),
                """
                {
                  "currentRealm": "Shining Abode"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-wrong-realm",
                  "turnNumber": 44,
                  "playerAction": "test"
                }
                """,
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "Complete-BoeTurn -FilesModified @('output/narrative_response.json', 'game_state/npcs/npc_core.json')"
            });

            var result = RunPowerShell(command);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("wrong realm", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(session, "ready", "turn_complete.json")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_CompleteBoeTurnRejectsRawMortalWorldProfileMutationInAfterlifeRealm()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-wrong-realm-raw-mutation-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "game_state", "meta"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "factions"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control", "pending_turn_snapshot", "game_state", "factions"));
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));

        try
        {
            File.WriteAllText(
                Path.Combine(session, "game_state", "meta", "soul_state.json"),
                """
                {
                  "currentRealm": "Chaos Sea"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-wrong-realm-raw",
                  "turnNumber": 45,
                  "playerAction": "test"
                }
                """,
                Encoding.UTF8);

            const string snapshotFactionJson = "{ \"factions\": [{ \"id\": \"old\" }] }";
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot", "game_state", "factions", "faction_core.json"),
                snapshotFactionJson,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "factions", "faction_core.json"),
                "{ \"factions\": [{ \"id\": \"raw_wrong_realm_change\" }] }",
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "Complete-BoeTurn -FilesModified @('output/narrative_response.json')"
            });

            var result = RunPowerShell(command);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("wrong-realm", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("game_state/factions/faction_core.json", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(session, "ready", "turn_complete.json")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_CompleteBoeTurnAllowsSemanticallySameMortalWorldSnapshotJsonInAfterlifeRealm()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-wrong-realm-semantic-json-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "game_state", "meta"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "factions"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control", "pending_turn_snapshot", "game_state", "factions"));
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));

        try
        {
            File.WriteAllText(
                Path.Combine(session, "game_state", "meta", "soul_state.json"),
                """
                {
                  "currentRealm": "Chaos Sea"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-wrong-realm-semantic-json",
                  "turnNumber": 45,
                  "playerAction": "test"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-wrong-realm-semantic-json",
                  "turnNumber": 45
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.authority.json"),
                "{}",
                Encoding.UTF8);

            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot", "game_state", "factions", "faction_core.json"),
                """
                {
                  "factions": [
                    {
                      "id": "merchant_guild",
                      "stage": 2
                    }
                  ]
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "factions", "faction_core.json"),
                """{"factions":[{"id":"merchant_guild","stage":2}]}""",
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "Complete-BoeTurn -FilesModified @('output/narrative_response.json')"
            });

            var result = RunPowerShell(command);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(session, "ready", "turn_complete.json")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_CompleteBoeTurnIgnoresMortalWorldRollbackArtifactsInAfterlifeRealm()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-wrong-realm-rollback-artifact-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "game_state", "meta"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "world"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control", "pending_turn_snapshot", "game_state", "world"));
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));

        try
        {
            File.WriteAllText(
                Path.Combine(session, "game_state", "meta", "soul_state.json"),
                """
                {
                  "currentRealm": "Chaos Sea"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-wrong-realm-rollback-artifact",
                  "turnNumber": 46,
                  "playerAction": "test"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-wrong-realm-rollback-artifact",
                  "turnNumber": 46
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.authority.json"),
                "{}",
                Encoding.UTF8);

            const string currentLocationJson = """{"locationId":"loc_old","name":"Порог"}""";
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot", "game_state", "world", "current_location.json"),
                currentLocationJson,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "world", "current_location.json"),
                currentLocationJson,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "world", "current_location.json.rollback.123456"),
                currentLocationJson,
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "Complete-BoeTurn -FilesModified @('output/narrative_response.json')"
            });

            var result = RunPowerShell(command);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(session, "ready", "turn_complete.json")), result.StdErr + result.StdOut);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_CompleteBoeValidationRepairRejectsRawNewMortalWorldProfileFileInAfterlifeRealm()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-wrong-realm-raw-repair-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "game_state", "meta"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "npcs"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control", "pending_turn_snapshot"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control"));

        try
        {
            File.WriteAllText(
                Path.Combine(session, "game_state", "meta", "soul_state.json"),
                """
                {
                  "currentRealm": "Shining Abode"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "validation_repair_request.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-wrong-realm-raw-repair",
                  "turnNumber": 46,
                  "metadataDiagnosticOnly": false
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "npcs", "npc_core.json"),
                "{ \"NPCsInScene\": [{ \"npcId\": \"raw_wrong_realm_new\" }] }",
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "Complete-BoeValidationRepair"
            });

            var result = RunPowerShell(command);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("wrong-realm", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("game_state/npcs/npc_core.json", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(session, "game_state", "control", "validation_repair_ready.json")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_CompleteBoeTurnRejectsRawDeletedMortalWorldProfileFileInAfterlifeRealm()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-wrong-realm-raw-delete-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "game_state", "meta"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "player"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control", "pending_turn_snapshot", "game_state", "player"));
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));

        try
        {
            File.WriteAllText(
                Path.Combine(session, "game_state", "meta", "soul_state.json"),
                """
                {
                  "currentRealm": "Chaos Sea"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-wrong-realm-delete",
                  "turnNumber": 47,
                  "playerAction": "test"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot", "game_state", "player", "player_status.json"),
                "{ \"health\": { \"current\": 85 } }",
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "Complete-BoeTurn -FilesModified @('output/narrative_response.json')"
            });

            var result = RunPowerShell(command);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("wrong-realm", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("game_state/player/player_status.json", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(session, "ready", "turn_complete.json")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_WriteBoeJsonRejectsClientOwnedAbsoluteRuntimeFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-client-owned-absolute-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var clientOwnedPath = Path.Combine(session, "game_state", "history", "chat_log.json");
        Directory.CreateDirectory(Path.GetDirectoryName(clientOwnedPath)!);

        try
        {
            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "$data = [ordered]@{ entries = @() }",
                "Write-BoeJson -RelativePath " + QuotePowerShell(clientOwnedPath) + " -Data $data"
            });

            var result = RunPowerShell(command);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("client-owned", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(clientOwnedPath));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_AddBoeJsonArrayItemPreservesArraysWhenJsonPropertyIsSingleObject()
    {
        var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
        var command = string.Join("; ", new[]
        {
            ". " + QuotePowerShell(helperPath),
            "$letter = [pscustomobject]@{ customProperties = [pscustomobject]@{ key = 'resonanceState'; name = 'Resonance' } }",
            "Add-BoeJsonArrayItem -Object $letter -PropertyName 'customProperties' -Item ([ordered]@{ key = 'sealState'; name = 'Seal preserved' }) -UniqueBy 'key' | Out-Null",
            "Add-BoeJsonArrayItem -Object $letter -PropertyName 'customProperties' -Item ([ordered]@{ key = 'sealState'; name = 'Seal updated' }) -UniqueBy 'key' | Out-Null",
            "$items = @($letter.customProperties)",
            "if ($items.Count -ne 2) { throw \"Unexpected count: $($items.Count)\" }",
            "$seal = $items | Where-Object { $_.key -eq 'sealState' } | Select-Object -First 1",
            "if ($seal.name -ne 'Seal updated') { throw \"Unexpected seal value: $($seal.name)\" }",
            "$json = ([ordered]@{ customProperties = $letter.customProperties } | ConvertTo-Json -Depth 10)",
            "if ($json -notmatch '\"customProperties\"\\s*:\\s*\\[') { throw \"customProperties was not serialized as an array: $json\" }"
        });

        var result = RunPowerShell(command);

        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StdErr), result.StdErr);
    }

    [Fact]
    public void DaemonPrompt_ExposesSessionLocalGmTurnHelper()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("Write-GmTurnHelperBootstrap", daemon, StringComparison.Ordinal);
        Assert.Contains("gm_turn_helper.bootstrap.ps1", daemon, StringComparison.Ordinal);
        Assert.Contains("GM_Turn_Helper.ps1", daemon, StringComparison.Ordinal);
        Assert.Contains("Read-BoeJson", daemon, StringComparison.Ordinal);
        Assert.Contains("Complete-BoeTurn", daemon, StringComparison.Ordinal);
        Assert.Contains("Get-BoeJsonValue", daemon, StringComparison.Ordinal);
        Assert.Contains("Set-BoeJsonProperty", daemon, StringComparison.Ordinal);
        Assert.Contains("Add-BoeJsonArrayItem", daemon, StringComparison.Ordinal);
        Assert.Contains("optional or differently cased JSON fields", daemon, StringComparison.Ordinal);
        Assert.Contains("add or update optional object properties", daemon, StringComparison.Ordinal);
        Assert.Contains("PowerShell collapses single JSON array items into scalars", daemon, StringComparison.Ordinal);
        Assert.Contains("fails the shell command deliberately", daemon, StringComparison.Ordinal);
        Assert.Contains("wrong-realm Mortal World profile paths", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:GmTurnHelperDirective", daemon, StringComparison.Ordinal);
        Assert.Contains("$($script:GmTurnHelperDirective)", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonPrompt_ExposesSessionLocalGmContextPack()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("Write-GmContextPack", daemon, StringComparison.Ordinal);
        Assert.Contains("gm_context_pack", daemon, StringComparison.Ordinal);
        Assert.Contains("context_pack_manifest.json", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:GmContextPackDirective", daemon, StringComparison.Ordinal);
        Assert.Contains("$($script:GmContextPackDirective)", daemon, StringComparison.Ordinal);
        Assert.Contains("implementation code", daemon, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BookOfEternityClient/**/*.cs", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("===== BEGIN CLI_LAUNCH_SCRIPT =====", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("$launchScript", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonBootstrap_DoesNotAskGmToReadLargeContextPackDocs()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("Bootstrap scope:", daemon, StringComparison.Ordinal);
        Assert.Contains("read only context_pack_manifest.json and README.md", daemon, StringComparison.Ordinal);
        Assert.Contains("Do not open copied guides/examples during bootstrap", daemon, StringComparison.Ordinal);
        Assert.Contains("Open large copied docs only when a per-turn, repair, or terminal-failure prompt explicitly names them.", daemon, StringComparison.Ordinal);

        var bootstrapStart = daemon.IndexOf("BOOTSTRAP GM SESSION", StringComparison.Ordinal);
        var bootstrapEnd = daemon.IndexOf("$dispatch = Send-ToCliWindow -Message $message", bootstrapStart, StringComparison.Ordinal);
        Assert.True(bootstrapStart >= 0 && bootstrapEnd > bootstrapStart, "Expected bootstrap message block.");
        var bootstrap = daemon[bootstrapStart..bootstrapEnd];
        Assert.DoesNotContain("copied GM docs", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("copied guides and examples", bootstrap, StringComparison.Ordinal);
        Assert.Contains("BOE_GM_BOOTSTRAP_READY", bootstrap, StringComparison.Ordinal);
        Assert.Contains("finish your response", bootstrap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wait for the daemon", bootstrap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wait for a real correlated message", bootstrap, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DaemonAutomaticDispatch_DoesNotSendBootstrapAsSeparateGmRequest()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var turnBlock = ExtractFunctionBlock(daemon, "function Process-Turn");
        var repairBlock = ExtractFunctionBlock(daemon, "function Process-RepairRequest");
        var terminalFailureBlock = ExtractFunctionBlock(daemon, "function Process-TerminalProtocolFailureRequest");

        Assert.Contains("function Ensure-CliBootstrapSent", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("Ensure-CliBootstrapSent", turnBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Ensure-CliBootstrapSent", repairBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Ensure-CliBootstrapSent", terminalFailureBlock, StringComparison.Ordinal);

        var startupWaitIndex = daemon.IndexOf("Waiting for turns... (Ctrl+C to stop)", StringComparison.Ordinal);
        var processExistingIndex = daemon.IndexOf("# Process existing request if any", startupWaitIndex, StringComparison.Ordinal);
        Assert.True(startupWaitIndex >= 0 && processExistingIndex > startupWaitIndex, "Expected daemon startup wait section.");
        var startupWaitBlock = daemon[startupWaitIndex..processExistingIndex];
        Assert.DoesNotContain("Ensure-CliBootstrapSent", startupWaitBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonContextPack_GeneratesCompactTurnAndRepairTemplates()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("Templates\\TURN_OUTPUT_TEMPLATE.md", daemon, StringComparison.Ordinal);
        Assert.Contains("Templates\\VALIDATION_REPAIR_TEMPLATE.md", daemon, StringComparison.Ordinal);
        Assert.Contains("Templates\\PROGRESSION_REPORT_TEMPLATE.json", daemon, StringComparison.Ordinal);
        Assert.Contains("Templates\\ACTOR_REASONING_TEMPLATE.md", daemon, StringComparison.Ordinal);
        Assert.Contains("Templates\\AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json", daemon, StringComparison.Ordinal);

        Assert.Contains("compact_turn_output_template", daemon, StringComparison.Ordinal);
        Assert.Contains("compact_validation_repair_template", daemon, StringComparison.Ordinal);
        Assert.Contains("compact_progression_report_template", daemon, StringComparison.Ordinal);
        Assert.Contains("compact_actor_reasoning_template", daemon, StringComparison.Ordinal);
        Assert.Contains("compact_tempo_advantage_template", daemon, StringComparison.Ordinal);

        Assert.Contains("$script:CompactTurnOutputTemplatePath", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:CompactValidationRepairTemplatePath", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:CompactProgressionReportTemplatePath", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:CompactActorReasoningTemplatePath", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:CompactTempoAdvantageTemplatePath", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:GmCompactTemplateDirective", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonContextPack_ExposesLiveTestRubric()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("Rubrics\\GM_LIVE_TEST_RUBRIC.md", daemon, StringComparison.Ordinal);
        Assert.Contains("Rubrics\\GM_LIVE_TEST_RUBRIC.json", daemon, StringComparison.Ordinal);
        Assert.Contains("gm_live_test_notes.jsonl", daemon, StringComparison.Ordinal);
        Assert.Contains("turn_success", daemon, StringComparison.Ordinal);
        Assert.Contains("harness_containment", daemon, StringComparison.Ordinal);
        Assert.Contains("friction", daemon, StringComparison.Ordinal);
        Assert.Contains("delegation", daemon, StringComparison.Ordinal);
        Assert.Contains("experience_memory", daemon, StringComparison.Ordinal);
        Assert.Contains("follow_up_generation", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:GmLiveTestRubricDirective", daemon, StringComparison.Ordinal);
        Assert.Contains("$($script:GmLiveTestRubricDirective)", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonCompactTemplates_DoNotTeachInvalidValidationShapes()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("$turnOutputTemplate", daemon, StringComparison.Ordinal);
        Assert.Contains("output/debug_logs.json", daemon, StringComparison.Ordinal);
        Assert.Contains("output/interface_updates.json", daemon, StringComparison.Ordinal);
        Assert.Contains("\"timestamp\"", daemon, StringComparison.Ordinal);
        Assert.Contains("\"dialogueOptions\": [", daemon, StringComparison.Ordinal);
        Assert.Contains("\"text\":", daemon, StringComparison.Ordinal);
        Assert.Contains("\"category\":", daemon, StringComparison.Ordinal);
        Assert.Contains("__ACTOR_SITUATION_LABEL__", daemon, StringComparison.Ordinal);
        Assert.Contains("__ACTOR_THOUGHTS_LABEL__", daemon, StringComparison.Ordinal);
        Assert.Contains("__ACTOR_ACTIONS_LABEL__", daemon, StringComparison.Ordinal);

        var progressionTemplate = ExtractTemplateBlock(daemon, "Templates\\PROGRESSION_REPORT_TEMPLATE.json");
        Assert.Contains("\"progressionProcessingReport\"", progressionTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain(": null", progressionTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("\"summary\"", progressionTemplate, StringComparison.Ordinal);
        Assert.Contains("\"newLastWorldSimulationTimeInMinutes\": 0", progressionTemplate, StringComparison.Ordinal);
        Assert.Contains("\"newLastShiningTradeCycleOrdinal\": 0", progressionTemplate, StringComparison.Ordinal);

        Assert.Contains("$actorReasoningTemplate", daemon, StringComparison.Ordinal);
        Assert.Contains("## NPC Scope", daemon, StringComparison.Ordinal);
        Assert.Contains("Scene-local | World-progression | Guardian-centric | Mixed", daemon, StringComparison.Ordinal);
        Assert.Contains("Why relevant", daemon, StringComparison.Ordinal);
        Assert.Contains("Why outside scope", daemon, StringComparison.Ordinal);
        Assert.Contains("## Reasoning", daemon, StringComparison.Ordinal);
        Assert.Contains("New-StringFromCodePoints", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("## Scope\n", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("## Actor reasoning", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("AfterlifeProfile", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonContextPack_RuntimeGeneratedTemplatesPreserveRussianLabels()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-context-pack-runtime-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");
        Directory.CreateDirectory(session);

        Process? process = null;
        try
        {
            var daemonPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "game_master_daemon.ps1");
            process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = string.Join(
                    " ",
                    "-NoLogo",
                    "-NoProfile",
                    "-ExecutionPolicy Bypass",
                    "-File " + QuoteProcessArgument(daemonPath),
                    "-GameSessionPath " + QuoteProcessArgument(session),
                    "-PollingInterval 5000",
                    "-LogFile " + QuoteProcessArgument(logPath)),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            });
            Assert.NotNull(process);

            var actorTemplatePath = Path.Combine(
                session,
                "game_state",
                "control",
                "gm_context_pack",
                "Templates",
                "ACTOR_REASONING_TEMPLATE.md");
            var turnTemplatePath = Path.Combine(
                session,
                "game_state",
                "control",
                "gm_context_pack",
                "Templates",
                "TURN_OUTPUT_TEMPLATE.md");

            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline && (!File.Exists(actorTemplatePath) || !File.Exists(turnTemplatePath)))
            {
                if (process.HasExited)
                {
                    break;
                }

                Thread.Sleep(100);
            }

            var stdOut = process.HasExited ? process.StandardOutput.ReadToEnd() : string.Empty;
            var stdErr = process.HasExited ? process.StandardError.ReadToEnd() : string.Empty;
            Assert.True(File.Exists(actorTemplatePath), stdErr + stdOut);
            Assert.True(File.Exists(turnTemplatePath), stdErr + stdOut);

            var actorTemplate = File.ReadAllText(actorTemplatePath, Encoding.UTF8);
            var turnTemplate = File.ReadAllText(turnTemplatePath, Encoding.UTF8);
            Assert.Contains("- Ситуация:", actorTemplate, StringComparison.Ordinal);
            Assert.Contains("- Мысли:", actorTemplate, StringComparison.Ordinal);
            Assert.Contains("- Действия:", actorTemplate, StringComparison.Ordinal);
            Assert.Contains("- Ситуация:", turnTemplate, StringComparison.Ordinal);
            Assert.Contains("\"dialogueOptions\": [", turnTemplate, StringComparison.Ordinal);
            Assert.Contains("\"text\":", turnTemplate, StringComparison.Ordinal);
            Assert.Contains("\"category\":", turnTemplate, StringComparison.Ordinal);
            Assert.Contains("\"timestamp\"", turnTemplate, StringComparison.Ordinal);
            Assert.DoesNotContain("РЎРё", actorTemplate, StringComparison.Ordinal);
            Assert.DoesNotContain("РњС‹", actorTemplate, StringComparison.Ordinal);
            Assert.DoesNotContain("Р”Рµ", actorTemplate, StringComparison.Ordinal);
        }
        finally
        {
            if (process is { HasExited: false })
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignored */ }
            }

            process?.Dispose();
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonTrajectoryLedger_RecordsSuccessfulTurnOutcome()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-trajectory-success-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));
        Directory.CreateDirectory(Path.Combine(session, "output"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control"));

        Process? process = null;
        try
        {
            WriteDaemonConfig(session);
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-success",
                  "turnNumber": 77,
                  "currentRealm": "Chaos Sea",
                  "playerAction": "I test the compact GM trajectory ledger.",
                  "progressionControl": {
                    "currentRealm": "Chaos Sea"
                  }
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-success",
                  "turnNumber": 77
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(Path.Combine(session, "game_state", "control", "pending_turn_snapshot.authority.json"), "{}", Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "ready", "turn_complete.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-success",
                  "turnNumber": 77,
                  "status": "success",
                  "filesModified": [
                    "output/narrative_response.json"
                  ]
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "output", "narrative_response.json"),
                """
                {
                  "response": "The turn produced player-facing text."
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var ledgerPath = Path.Combine(session, "game_state", "control", "gm_trajectory_ledger.jsonl");
            Assert.True(WaitForFileContaining(ledgerPath, "request-success", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            using var document = JsonDocument.Parse(File.ReadLines(ledgerPath, Encoding.UTF8).Last());
            var record = document.RootElement;
            Assert.Equal("turn", record.GetProperty("kind").GetString());
            Assert.Equal("ordinary", record.GetProperty("mode").GetString());
            Assert.Equal("trajectory-session", record.GetProperty("sessionId").GetString());
            Assert.Equal("request-success", record.GetProperty("turnId").GetString());
            Assert.Equal(77, record.GetProperty("turnNumber").GetInt32());
            Assert.Equal("ChaosSea", record.GetProperty("realm").GetString());
            Assert.Equal("game_state/control/gm_context_pack", record.GetProperty("contextPackPath").GetString());
            Assert.Equal("v1", record.GetProperty("templateVersions").GetProperty("turnOutput").GetString());
            Assert.Equal("preexisting-terminal", record.GetProperty("dispatch").GetProperty("status").GetString());
            Assert.False(record.GetProperty("dispatch").GetProperty("timeout").GetBoolean());
            Assert.Equal("output/narrative_response.json", record.GetProperty("outputFiles")[0].GetString());
            Assert.Equal("accepted", record.GetProperty("validation").GetProperty("status").GetString());
            Assert.Equal(0, record.GetProperty("repair").GetProperty("attempts").GetInt32());
            Assert.True(record.GetProperty("rubric").GetProperty("validTurn").GetBoolean());
            Assert.True(record.GetProperty("rubric").GetProperty("playerFacingOutputPresent").GetBoolean());
            Assert.False(record.GetProperty("rubric").GetProperty("rawWrongRealmWrite").GetBoolean());
            Assert.DoesNotContain("Process turn #77", record.GetRawText(), StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonTrajectoryLedger_ResolvesRealmFromSoulStateWhenTurnRequestOmitsRealm()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-trajectory-soul-realm-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));
        Directory.CreateDirectory(Path.Combine(session, "output"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "meta"));

        Process? process = null;
        try
        {
            WriteDaemonConfig(session);
            File.WriteAllText(
                Path.Combine(session, "game_state", "meta", "soul_state.json"),
                """
                {
                  "currentRealm": "Mortal World"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-soul-realm",
                  "turnNumber": 79,
                  "playerAction": "I test realm fallback from canonical soul state."
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-soul-realm",
                  "turnNumber": 79
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(Path.Combine(session, "game_state", "control", "pending_turn_snapshot.authority.json"), "{}", Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "ready", "turn_complete.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-soul-realm",
                  "turnNumber": 79,
                  "status": "success",
                  "filesModified": [
                    "output/narrative_response.json"
                  ]
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "output", "narrative_response.json"),
                """
                {
                  "response": "The turn produced player-facing text."
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var ledgerPath = Path.Combine(session, "game_state", "control", "gm_trajectory_ledger.jsonl");
            Assert.True(WaitForFileContaining(ledgerPath, "request-soul-realm", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            using var document = JsonDocument.Parse(File.ReadLines(ledgerPath, Encoding.UTF8).Last());
            var record = document.RootElement;
            Assert.Equal("MortalWorld", record.GetProperty("realm").GetString());
            Assert.Equal("soul_state.currentRealm", record.GetProperty("realmResolution").GetProperty("source").GetString());
            Assert.Equal("Mortal World", record.GetProperty("realmResolution").GetProperty("rawValue").GetString());
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonTrajectoryLedger_MarksTurnRejectedWhenCorrelatedRepairRequestExists()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-trajectory-turn-repair-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");
        var control = Path.Combine(session, "game_state", "control");
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));
        Directory.CreateDirectory(Path.Combine(session, "output"));
        Directory.CreateDirectory(control);

        Process? process = null;
        try
        {
            WriteDaemonConfig(session);
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-needs-repair",
                  "turnNumber": 81,
                  "currentRealm": "Mortal World",
                  "playerAction": "I test terminal success followed by validation repair.",
                  "progressionControl": {
                    "currentRealm": "Mortal World"
                  }
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "pending_turn_snapshot.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-needs-repair",
                  "turnNumber": 81
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(Path.Combine(control, "pending_turn_snapshot.authority.json"), "{}", Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "ready", "turn_complete.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-needs-repair",
                  "turnNumber": 81,
                  "status": "success",
                  "filesModified": [
                    "output/narrative_response.json"
                  ]
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "output", "narrative_response.json"),
                """
                {
                  "response": "The terminal signal exists, but validation still needs repair."
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-needs-repair",
                  "turnNumber": 81,
                  "currentRealm": "Mortal World",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "location_last_events_timestamp_invalid",
                      "category": "StateConsistency",
                      "section": "Location",
                      "message": "lastEventsDescription uses an invalid timestamp."
                    }
                  ],
                  "harnessRepairPackets": [
                    {
                      "packetId": "repair-location-timestamp"
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var ledgerPath = Path.Combine(control, "gm_trajectory_ledger.jsonl");
            Assert.True(WaitForFileContaining(ledgerPath, "location_last_events_timestamp_invalid", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var turnRecordJson = File.ReadLines(ledgerPath, Encoding.UTF8)
                .Last(line => line.Contains("\"kind\":\"turn\"", StringComparison.Ordinal) &&
                              line.Contains("request-needs-repair", StringComparison.Ordinal));
            using var document = JsonDocument.Parse(turnRecordJson);
            var record = document.RootElement;
            Assert.Equal("rejected", record.GetProperty("validation").GetProperty("status").GetString());
            Assert.Equal("location_last_events_timestamp_invalid", record.GetProperty("validation").GetProperty("issueKinds")[0].GetString());
            Assert.Equal("repair-location-timestamp", record.GetProperty("validation").GetProperty("repairPacketRefs")[0].GetString());
            Assert.Equal(1, record.GetProperty("repair").GetProperty("attempts").GetInt32());
            Assert.Equal("requested", record.GetProperty("repair").GetProperty("status").GetString());
            Assert.False(record.GetProperty("rubric").GetProperty("validTurn").GetBoolean());
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonTrajectoryLedger_UnknownRealmIncludesResolutionReason()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("realmResolution", daemon, StringComparison.Ordinal);
        Assert.Contains("missing_current_realm", daemon, StringComparison.Ordinal);
        Assert.Contains("soul_state.currentRealm", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonTrajectoryLedger_RecordsValidationRepairRequest()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-trajectory-repair-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control"));

        Process? process = null;
        try
        {
            WriteDaemonConfig(session);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "validation_repair_request.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-repair",
                  "turnNumber": 78,
                  "currentRealm": "Mortal World",
                  "revalidationAttempt": 2,
                  "summaryGroups": [
                    "Inventory state is inconsistent."
                  ],
                  "errors": [
                    {
                      "code": "inventory_item_missing",
                      "category": "StateConsistency",
                      "section": "Inventory",
                      "message": "Referenced item is missing."
                    }
                  ],
                  "harnessRepairPackets": [
                    {
                      "packetId": "repair-packet-1"
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var ledgerPath = Path.Combine(session, "game_state", "control", "gm_trajectory_ledger.jsonl");
            Assert.True(WaitForFileContaining(ledgerPath, "request-repair", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            using var document = JsonDocument.Parse(File.ReadLines(ledgerPath, Encoding.UTF8).Last());
            var record = document.RootElement;
            Assert.Equal("repair", record.GetProperty("kind").GetString());
            Assert.Equal("validation_repair", record.GetProperty("mode").GetString());
            Assert.Equal("trajectory-session", record.GetProperty("sessionId").GetString());
            Assert.Equal("request-repair", record.GetProperty("turnId").GetString());
            Assert.Equal(78, record.GetProperty("turnNumber").GetInt32());
            Assert.Equal("MortalWorld", record.GetProperty("realm").GetString());
            Assert.Equal("rejected", record.GetProperty("validation").GetProperty("status").GetString());
            Assert.Equal("inventory_item_missing", record.GetProperty("validation").GetProperty("issueKinds")[0].GetString());
            Assert.Equal("repair-packet-1", record.GetProperty("validation").GetProperty("repairPacketRefs")[0].GetString());
            Assert.Equal(2, record.GetProperty("repair").GetProperty("attempts").GetInt32());
            Assert.Equal("requested", record.GetProperty("repair").GetProperty("status").GetString());
            Assert.False(record.GetProperty("rubric").GetProperty("validTurn").GetBoolean());
            Assert.False(record.GetProperty("rubric").GetProperty("playerFacingOutputPresent").GetBoolean());
            Assert.DoesNotContain("validation_repair_request.json", record.GetRawText(), StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonTrajectoryLedger_IncludesProposalOnlyWorkerEvents()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-trajectory-worker-proposal-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");
        var control = Path.Combine(session, "game_state", "control");
        Directory.CreateDirectory(Path.Combine(session, "input"));
        Directory.CreateDirectory(Path.Combine(session, "ready"));
        Directory.CreateDirectory(Path.Combine(session, "output"));
        Directory.CreateDirectory(control);

        Process? process = null;
        try
        {
            WriteDaemonConfig(session);
            File.WriteAllText(
                Path.Combine(session, "input", "turn_request.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-worker-proposal",
                  "turnNumber": 79,
                  "currentRealm": "Mortal World",
                  "playerAction": "I ask the GM to draft a tense scene.",
                  "progressionControl": {
                    "currentRealm": "Mortal World"
                  }
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "pending_turn_snapshot.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-worker-proposal",
                  "turnNumber": 79
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(Path.Combine(control, "pending_turn_snapshot.authority.json"), "{}", Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "ready", "turn_complete.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-worker-proposal",
                  "turnNumber": 79,
                  "status": "success",
                  "filesModified": [
                    "output/narrative_response.json"
                  ]
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "output", "narrative_response.json"),
                """
                {
                  "response": "The main GM used a worker draft without exposing it directly."
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "gm_worker_audit.jsonl"),
                """
                {"schemaVersion":1,"eventId":"worker_audit_narrative_dispatch","eventType":"task-dispatched","workerId":"narrative_draft_codex","taskId":"worker_task_narrative_0001","timestampUtc":"2099-01-01T00:00:00Z","summary":"Dispatched NarrativeDraft worker task.","details":{"taskType":["narrative-draft"],"responseContract":["worker-proposal-v1"],"allowedProposalPaths":[]}}
                {"schemaVersion":1,"eventId":"worker_audit_narrative_proposal","eventType":"proposal-received","workerId":"narrative_draft_codex","taskId":"worker_task_narrative_0001","proposalId":"worker_proposal_narrative_0001","timestampUtc":"2099-01-01T00:00:01Z","summary":"Drafted scene for main-GM review.","details":{"changedFiles":[]}}
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var ledgerPath = Path.Combine(control, "gm_trajectory_ledger.jsonl");
            Assert.True(WaitForFileContaining(ledgerPath, "request-worker-proposal", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            using var document = JsonDocument.Parse(File.ReadLines(ledgerPath, Encoding.UTF8).Last());
            var record = document.RootElement;
            var workerEvents = record.GetProperty("workerEvents").EnumerateArray().ToArray();
            Assert.Contains(workerEvents, workerEvent =>
                workerEvent.GetProperty("eventType").GetString() == "task-dispatched" &&
                workerEvent.GetProperty("workerId").GetString() == "narrative_draft_codex" &&
                workerEvent.GetProperty("taskType").GetString() == "narrative-draft" &&
                workerEvent.GetProperty("proposalOnly").GetBoolean());
            Assert.Contains(workerEvents, workerEvent =>
                workerEvent.GetProperty("eventType").GetString() == "proposal-received" &&
                workerEvent.GetProperty("proposalId").GetString() == "worker_proposal_narrative_0001" &&
                workerEvent.GetProperty("changedFileCount").GetInt32() == 0);
            Assert.DoesNotContain("Raw WorkerTaskPacket JSON", record.GetRawText(), StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonTrajectoryLedger_IncludesValidationRepairWorkerEvents()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-trajectory-worker-repair-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");
        var control = Path.Combine(session, "game_state", "control");
        Directory.CreateDirectory(control);

        Process? process = null;
        try
        {
            WriteDaemonConfig(session);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-worker-repair",
                  "turnNumber": 80,
                  "currentRealm": "Mortal World",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "normalized_weather_missing_description",
                      "category": "StateConsistency",
                      "section": "Weather",
                      "message": "Weather description is missing.",
                      "filePath": "game_state/world/weather.json"
                    }
                  ]
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "gm_worker_audit.jsonl"),
                """
                {"schemaVersion":1,"eventId":"worker_audit_repair_dispatch","eventType":"task-dispatched","workerId":"validation_repair_codex","taskId":"worker_task_validation_repair_0001","timestampUtc":"2099-01-01T00:00:00Z","summary":"Dispatched ValidationRepair worker task.","details":{"taskType":["validation-repair"],"responseContract":["worker-proposal-v1"],"allowedProposalPaths":["game_state/world/weather.json"]}}
                {"schemaVersion":1,"eventId":"worker_audit_repair_applied","eventType":"proposal-applied","workerId":"validation_repair_codex","taskId":"worker_task_validation_repair_0001","proposalId":"worker_proposal_validation_repair_0001","timestampUtc":"2099-01-01T00:00:02Z","summary":"Apply gate decision: Accepted.","details":{"appliedFiles":["game_state/world/weather.json"],"rejectionReasons":[]}}
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var ledgerPath = Path.Combine(control, "gm_trajectory_ledger.jsonl");
            Assert.True(WaitForFileContaining(ledgerPath, "request-worker-repair", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            using var document = JsonDocument.Parse(File.ReadLines(ledgerPath, Encoding.UTF8).Last());
            var record = document.RootElement;
            var workerEvents = record.GetProperty("workerEvents").EnumerateArray().ToArray();
            Assert.Contains(workerEvents, workerEvent =>
                workerEvent.GetProperty("eventType").GetString() == "task-dispatched" &&
                workerEvent.GetProperty("taskType").GetString() == "validation-repair" &&
                workerEvent.GetProperty("allowedProposalPathCount").GetInt32() == 1 &&
                !workerEvent.GetProperty("proposalOnly").GetBoolean());
            Assert.Contains(workerEvents, workerEvent =>
                workerEvent.GetProperty("eventType").GetString() == "proposal-applied" &&
                workerEvent.GetProperty("proposalId").GetString() == "worker_proposal_validation_repair_0001" &&
                workerEvent.GetProperty("appliedFileCount").GetInt32() == 1);
            Assert.DoesNotContain("weather description is missing", record.GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_RendersBoundedRelevantExperienceLessons()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-experience-lessons-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");
        var control = Path.Combine(session, "game_state", "control");
        Directory.CreateDirectory(control);

        Process? process = null;
        try
        {
            WriteDaemonConfig(session);
            var ledgerPath = Path.Combine(control, "gm_trajectory_ledger.jsonl");
            var ledgerLines = Enumerable.Range(1, 7)
                .Select(i => $$"""
                    {"recordId":"gmtraj_relevant_{{i}}","kind":"repair","sessionId":"prior","turnId":"repair-{{i}}","requestId":"repair-{{i}}","turnNumber":{{i}},"realm":"ChaosSea","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1","actorReasoning":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"accepted","issueKinds":["actor_reasoning_missing"],"repairPacketRefs":["packet-{{i}}"]},"repair":{"attempts":{{i}},"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-06-26T10:0{{i}}:00Z"}
                    """.Trim())
                .Append("""
                    {"recordId":"gmtraj_rejected_same_issue","kind":"repair","sessionId":"prior","turnId":"repair-rejected","requestId":"repair-rejected","turnNumber":89,"realm":"ChaosSea","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1","actorReasoning":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"rejected","issueKinds":["actor_reasoning_missing"],"repairPacketRefs":["packet-rejected"]},"repair":{"attempts":1,"status":"requested"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":false,"playerFacingOutputPresent":false,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-06-26T10:30:00Z"}
                    """.Trim())
                .Append("""
                    {"recordId":"gmtraj_irrelevant_mortal","kind":"repair","sessionId":"prior","turnId":"repair-mortal","requestId":"repair-mortal","turnNumber":90,"realm":"MortalWorld","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"rejected","issueKinds":["inventory_item_missing"],"repairPacketRefs":["packet-mortal"]},"repair":{"attempts":1,"status":"requested"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":false,"playerFacingOutputPresent":false,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-06-26T09:00:00Z"}
                    """.Trim());
            File.WriteAllText(ledgerPath, string.Join(Environment.NewLine, ledgerLines) + Environment.NewLine, Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-repair",
                  "turnNumber": 100,
                  "currentRealm": "Chaos Sea",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "actor_reasoning_missing",
                      "category": "StateConsistency",
                      "section": "DebugLogs",
                      "message": "Actor reasoning is missing."
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            Assert.True(WaitForFileContaining(lessonsJsonPath, "actor_reasoning_missing", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var rootElement = document.RootElement;
            Assert.Equal(5, rootElement.GetProperty("lessons").GetArrayLength());
            Assert.Equal("ChaosSea", rootElement.GetProperty("query").GetProperty("realm").GetString());
            Assert.Equal("validation_repair", rootElement.GetProperty("query").GetProperty("mode").GetString());
            Assert.Equal("actor_reasoning_missing", rootElement.GetProperty("query").GetProperty("issueKinds")[0].GetString());
            Assert.DoesNotContain("inventory_item_missing", rootElement.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain("gmtraj_rejected_same_issue", rootElement.GetRawText(), StringComparison.Ordinal);
            Assert.Contains("accepted prior repair outcomes", rootElement.GetProperty("guidance").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("validators and current templates remain authoritative", rootElement.GetProperty("guidance").GetString(), StringComparison.OrdinalIgnoreCase);

            var firstLesson = rootElement.GetProperty("lessons")[0];
            Assert.Equal("actor_reasoning_missing", firstLesson.GetProperty("match").GetProperty("issueKinds")[0].GetString());
            Assert.Equal("ACTOR_REASONING_TEMPLATE.md", firstLesson.GetProperty("preferredHarnessSurface").GetString());
            Assert.Equal("v1", firstLesson.GetProperty("versions").GetProperty("template").GetString());
            Assert.DoesNotContain("Process turn #", firstLesson.GetRawText(), StringComparison.Ordinal);

            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "GM Experience Lessons", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));
            var readmePath = Path.Combine(control, "gm_context_pack", "README.md");
            Assert.True(WaitForFileContaining(readmePath, "GM_EXPERIENCE_LESSONS", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));
            var readme = File.ReadAllText(readmePath, Encoding.UTF8);
            Assert.Contains("GM_EXPERIENCE_LESSONS", readme, StringComparison.Ordinal);
            Assert.Contains("hints", readme, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_RendersActionableMortalNpcPersistenceLesson()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-mortal-npc-lessons-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");
        var control = Path.Combine(session, "game_state", "control");
        Directory.CreateDirectory(control);

        Process? process = null;
        try
        {
            WriteDaemonConfig(session);
            var ledgerPath = Path.Combine(control, "gm_trajectory_ledger.jsonl");
            File.WriteAllText(
                ledgerPath,
                """
                {"recordId":"gmtraj_mortal_npc_fix","kind":"repair","sessionId":"prior","turnId":"repair-npc","requestId":"repair-npc","turnNumber":2,"realm":"MortalWorld","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1","actorReasoning":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"accepted","issueKinds":["mortal_relevant_actor_missing_persistence"],"repairPacketRefs":[]},"repair":{"attempts":1,"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-06-26T10:01:00Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-repair",
                  "turnNumber": 3,
                  "currentRealm": "Mortal World",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "mortal_relevant_actor_missing_persistence",
                      "category": "StateConsistency",
                      "section": "npc_scope",
                      "message": "Mortal World relevant actor has no persistent NPC surface."
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "mortal_relevant_actor_missing_persistence", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var markdown = File.ReadAllText(lessonsMarkdownPath, Encoding.UTF8);
            Assert.Contains("UpdateNPCs", markdown, StringComparison.Ordinal);
            Assert.Contains("NPCsInScene", markdown, StringComparison.Ordinal);
            Assert.Contains("Actors outside scope", markdown, StringComparison.Ordinal);
            Assert.Contains("background-only", markdown, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_RendersMortalNpcUpdateTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-mortal-npc-template-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");

        Process? process = null;
        try
        {
            Directory.CreateDirectory(session);
            WriteDaemonConfig(session);
            process = StartDaemon(session, logPath);

            var templatePath = Path.Combine(
                session,
                "game_state",
                "control",
                "gm_context_pack",
                "Templates",
                "MORTAL_NPC_UPDATE_TEMPLATE.md");
            Assert.True(WaitForFileContaining(templatePath, "Minimal safe NPC scene object", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var template = File.ReadAllText(templatePath, Encoding.UTF8);
            Assert.Contains("NPCsInScene", template, StringComparison.Ordinal);
            Assert.Contains("\"currentLocationId\": null", template, StringComparison.Ordinal);
            Assert.Contains("\"attitude\": \"Neutral\"", template, StringComparison.Ordinal);
            Assert.Contains("Conformist", template, StringComparison.Ordinal);
            Assert.Contains("Pragmatist", template, StringComparison.Ordinal);
            Assert.Contains("Dissident", template, StringComparison.Ordinal);
            Assert.Contains("relationshipLock", template, StringComparison.Ordinal);
            Assert.Contains("Actors outside scope", template, StringComparison.Ordinal);

            var readmePath = Path.Combine(session, "game_state", "control", "gm_context_pack", "README.md");
            Assert.True(WaitForFileContaining(readmePath, "Mortal World NPC updates", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));
            var readme = File.ReadAllText(readmePath, Encoding.UTF8);
            Assert.Contains("Mortal World NPC updates", readme, StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_RoutesNpcLocationExperienceLessonToMortalNpcTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-npc-location-lessons-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");
        var control = Path.Combine(session, "game_state", "control");
        Directory.CreateDirectory(control);

        Process? process = null;
        try
        {
            WriteDaemonConfig(session);
            File.WriteAllText(
                Path.Combine(control, "gm_trajectory_ledger.jsonl"),
                """
                {"recordId":"gmtraj_npc_location_fix","kind":"repair","sessionId":"prior","turnId":"repair-npc-location","requestId":"repair-npc-location","turnNumber":4,"realm":"MortalWorld","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1","actorReasoning":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"accepted","issueKinds":["npc_same_turn_initial_location_requires_null_current_location","missing_actor_current_location"],"repairPacketRefs":[]},"repair":{"attempts":1,"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-06-26T10:01:00Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-repair",
                  "turnNumber": 5,
                  "currentRealm": "Mortal World",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "npc_same_turn_initial_location_requires_null_current_location",
                      "category": "StateConsistency",
                      "section": "NPC",
                      "message": "Same-turn NPC location shape is invalid."
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "MORTAL_NPC_UPDATE_TEMPLATE.md", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var lesson = document.RootElement.GetProperty("lessons")[0];
            Assert.Equal("MORTAL_NPC_UPDATE_TEMPLATE.md", lesson.GetProperty("preferredHarnessSurface").GetString());
            Assert.Contains("currentLocationId to JSON null", lesson.GetProperty("acceptedFix").GetString(), StringComparison.Ordinal);
            Assert.Contains("initialLocationId", lesson.GetProperty("acceptedFix").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonTurnAndRepairPrompts_PreferCompactTemplatesOverLargeExamples()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var turnBlock = ExtractFunctionBlock(daemon, "function Process-Turn");
        var repairBlock = ExtractFunctionBlock(daemon, "function Process-RepairRequest");
        var terminalFailureBlock = ExtractFunctionBlock(daemon, "function Process-TerminalProtocolFailureRequest");

        Assert.Contains("$($script:GmCompactTemplateDirective)", turnBlock, StringComparison.Ordinal);
        Assert.Contains("$($script:GmCompactTemplateDirective)", repairBlock, StringComparison.Ordinal);
        Assert.Contains("$($script:GmCompactTemplateDirective)", terminalFailureBlock, StringComparison.Ordinal);

        Assert.Contains("'$($script:CompactTurnOutputTemplatePath)'", turnBlock, StringComparison.Ordinal);
        Assert.Contains("'$($script:CompactProgressionReportTemplatePath)'", turnBlock, StringComparison.Ordinal);
        Assert.Contains("'$($script:CompactActorReasoningTemplatePath)'", turnBlock, StringComparison.Ordinal);
        Assert.Contains("'$($script:CompactMortalNpcTemplatePath)'", turnBlock, StringComparison.Ordinal);
        Assert.Contains("'$($script:CompactTempoAdvantageTemplatePath)'", turnBlock, StringComparison.Ordinal);
        Assert.Contains("before opening large copied examples", turnBlock, StringComparison.Ordinal);

        Assert.Contains("'$($script:CompactValidationRepairTemplatePath)'", repairBlock, StringComparison.Ordinal);
        Assert.Contains("'$($script:CompactActorReasoningTemplatePath)'", repairBlock, StringComparison.Ordinal);
        Assert.Contains("'$($script:CompactMortalNpcTemplatePath)'", repairBlock, StringComparison.Ordinal);
        Assert.Contains("harnessRepairPackets", repairBlock, StringComparison.Ordinal);
        Assert.Contains("before opening large copied examples", repairBlock, StringComparison.Ordinal);

        Assert.Contains("'$($script:CompactValidationRepairTemplatePath)'", terminalFailureBlock, StringComparison.Ordinal);
        Assert.Contains("terminal_protocol_failure_request.json", terminalFailureBlock, StringComparison.Ordinal);
        Assert.Contains("before opening large copied examples", terminalFailureBlock, StringComparison.Ordinal);

        Assert.DoesNotContain("You MUST read '$($script:TaskGuideMainPath)' and '$($script:ExampleMainPath)' before writing files.", turnBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("You MUST reread $GameSessionPath\\game_state\\control\\validation_repair_request.json plus '$($script:TaskGuideMainPath)' and '$($script:ExampleMainPath)'.", repairBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("You MUST reread $GameSessionPath\\game_state\\control\\terminal_protocol_failure_request.json plus '$($script:TaskGuideMainPath)' and '$($script:ExampleMainPath)'.", terminalFailureBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonTerminalObservation_DoesNotDeleteClientOwnedTurnRequest()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("$script:ObservedTerminalRequestKeys", daemon, StringComparison.Ordinal);
        Assert.Contains("Add-ObservedTerminalRequestKey", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item $RequestPath", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item -Path $RequestPath", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("_fs.DeleteFile(\"input/turn_request.json\")", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonTurnDispatch_RequiresMatchingPendingSnapshotContext()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("$PendingTurnSnapshotManifestFile", daemon, StringComparison.Ordinal);
        Assert.Contains("$PendingTurnSnapshotAuthorityFile", daemon, StringComparison.Ordinal);
        Assert.Contains("function Test-TurnRequestHasPendingSnapshotContext", daemon, StringComparison.Ordinal);
        Assert.Contains("Skipping stale turn request without matching pending snapshot context", daemon, StringComparison.Ordinal);

        var guardIndex = daemon.IndexOf("Test-TurnRequestHasPendingSnapshotContext -TurnRequest $turnRequest", StringComparison.Ordinal);
        var dispatchIndex = daemon.IndexOf("Dispatch-WithRetry -Message $message", StringComparison.Ordinal);
        Assert.True(
            guardIndex >= 0 && dispatchIndex >= 0 && guardIndex < dispatchIndex,
            "Daemon must reject stale turn_request.json before dispatching a prompt to GM.");

        var guardBlockStart = daemon.IndexOf("if (-not (Test-TurnRequestHasPendingSnapshotContext -TurnRequest $turnRequest))", StringComparison.Ordinal);
        Assert.True(guardBlockStart >= 0, "Expected a stale request guard in Process-Turn.");
        var guardBlockEnd = daemon.IndexOf("Write-Host \"\"", guardBlockStart, StringComparison.Ordinal);
        Assert.True(guardBlockEnd > guardBlockStart, "Expected the stale request guard before normal turn logging.");
        var guardBlock = daemon[guardBlockStart..guardBlockEnd];
        Assert.Contains("Add-ObservedTerminalRequestKey -Key $turnRequestKey", guardBlock, StringComparison.Ordinal);
        Assert.Contains("return", guardBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonRepairDispatch_RetriesWhenGmBridgeIsBusy()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        var functionBlock = ExtractFunctionBlock(daemon, "function Process-RepairRequest");

        Assert.Contains("Dispatch-WithRetry -Message $message", functionBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Send-ToCliWindow -Message $message | Out-Null", functionBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonRepairDispatch_SkipsDiagnosticOnlyRepairRequests()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        var functionBlock = ExtractFunctionBlock(daemon, "function Process-RepairRequest");

        Assert.Contains("metadataDiagnosticOnly", functionBlock, StringComparison.Ordinal);
        var guardIndex = functionBlock.IndexOf("$hasDiagnosticOnlyMetadata", StringComparison.Ordinal);
        var dispatchIndex = functionBlock.IndexOf("Dispatch-WithRetry -Message $message", StringComparison.Ordinal);
        Assert.True(guardIndex >= 0, "Expected Process-RepairRequest to inspect metadataDiagnosticOnly.");
        Assert.True(dispatchIndex > guardIndex, "Expected diagnostic-only guard before repair dispatch.");

        var guardBlock = functionBlock[guardIndex..dispatchIndex];
        Assert.Contains("Skipping diagnostic-only validation repair request", guardBlock, StringComparison.Ordinal);
        Assert.Contains("Write-GmTrajectoryRecord", guardBlock, StringComparison.Ordinal);
        Assert.Contains("-RepairStatus \"diagnostic-only-skipped\"", guardBlock, StringComparison.Ordinal);
        Assert.Contains("return", guardBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonTerminalProtocolDispatch_RetriesWhenGmBridgeIsBusy()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        var functionBlock = ExtractFunctionBlock(daemon, "function Process-TerminalProtocolFailureRequest");

        Assert.Contains("Dispatch-WithRetry -Message $message", functionBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Send-ToCliWindow -Message $message | Out-Null", functionBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonTurnWait_StopsWhenClientClosedPendingSnapshotContext()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains(
            "Turn wait closed because the client no longer has matching pending snapshot context",
            daemon,
            StringComparison.Ordinal);

        var waitLoopIndex = daemon.IndexOf("while ($null -eq $terminalSignal", StringComparison.Ordinal);
        var waitGuardIndex = daemon.IndexOf("Test-TurnRequestHasPendingSnapshotContext -TurnRequest $turnRequest", waitLoopIndex, StringComparison.Ordinal);
        var terminalPollIndex = daemon.IndexOf("Get-CorrelatedTerminalSignal -TurnRequest $turnRequest", waitLoopIndex, StringComparison.Ordinal);
        Assert.True(waitLoopIndex >= 0, "Expected a terminal wait loop.");
        Assert.True(waitGuardIndex > waitLoopIndex, "Expected pending snapshot guard inside the terminal wait loop.");
        Assert.True(terminalPollIndex > waitGuardIndex, "Expected stale pending snapshot guard before terminal polling in the wait loop.");

        var guardBlockEnd = daemon.IndexOf("$terminalSignal = Get-CorrelatedTerminalSignal", waitGuardIndex, StringComparison.Ordinal);
        Assert.True(guardBlockEnd > waitGuardIndex, "Expected the wait guard before terminal polling.");
        var guardBlock = daemon[waitGuardIndex..guardBlockEnd];
        Assert.Contains("Add-ObservedTerminalRequestKey -Key $turnRequestKey", guardBlock, StringComparison.Ordinal);
        Assert.Contains("break", guardBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonStartup_UsesSessionLocalStatusAndRefusesActivePeer()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("$DaemonStatusFile", daemon, StringComparison.Ordinal);
        Assert.Contains("gm_daemon_status.json", daemon, StringComparison.Ordinal);
        Assert.Contains("function Test-DaemonProcessAlive", daemon, StringComparison.Ordinal);
        Assert.Contains("function Assert-SingleDaemonInstance", daemon, StringComparison.Ordinal);
        Assert.Contains("GM daemon already running for this game_session", daemon, StringComparison.Ordinal);
        Assert.Contains("Write-DaemonStatus", daemon, StringComparison.Ordinal);
        Assert.Contains("heartbeatAtUtc", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonTerminalSignalConflict_PrefersRealSuccessOverDaemonTimeoutArtifact()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("harnessSource = \"gm_daemon_timeout\"", daemon, StringComparison.Ordinal);
        Assert.Contains("Resolve-DaemonTimeoutTerminalConflict", daemon, StringComparison.Ordinal);
        Assert.Contains("Removed stale daemon timeout terminal signal artifact", daemon, StringComparison.Ordinal);
        Assert.Contains("$matchedSignals.Count -gt 1", daemon, StringComparison.Ordinal);
        Assert.Contains("return $resolvedTimeoutConflict", daemon, StringComparison.Ordinal);
    }

    private static (int ExitCode, string StdOut, string StdErr) RunPowerShell(string command)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-test-" + Guid.NewGuid().ToString("N") + ".ps1");
        File.WriteAllText(scriptPath, command, Encoding.UTF8);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File " + QuoteProcessArgument(scriptPath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }) ?? throw new InvalidOperationException("Failed to start powershell.exe.");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(10_000);
            return (process.ExitCode, stdout, stderr);
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { /* ignored */ }
        }
    }

    private static Process StartDaemon(string session, string logPath)
    {
        var daemonPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "game_master_daemon.ps1");
        return Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = string.Join(
                " ",
                "-NoLogo",
                "-NoProfile",
                "-ExecutionPolicy Bypass",
                "-File " + QuoteProcessArgument(daemonPath),
                "-GameSessionPath " + QuoteProcessArgument(session),
                "-PollingInterval 5000",
                "-TurnTimeout 1",
                "-LogFile " + QuoteProcessArgument(logPath)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        }) ?? throw new InvalidOperationException("Failed to start game_master_daemon.ps1.");
    }

    private static void StopProcess(Process? process)
    {
        if (process is { HasExited: false })
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignored */ }
        }

        process?.Dispose();
    }

    private static bool WaitForFileContaining(string path, string expectedText, Process? process, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8).Contains(expectedText, StringComparison.Ordinal))
            {
                return true;
            }

            if (process is { HasExited: true })
            {
                return File.Exists(path) &&
                    File.ReadAllText(path, Encoding.UTF8).Contains(expectedText, StringComparison.Ordinal);
            }

            Thread.Sleep(100);
        }

        return false;
    }

    private static string ReadProcessOutput(Process? process)
    {
        if (process == null)
        {
            return "Process was not started.";
        }

        if (!process.HasExited)
        {
            return "Process is still running.";
        }

        return process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
    }

    private static void WriteDaemonConfig(string session)
    {
        File.WriteAllText(
            Path.Combine(session, "config.json"),
            """
            {
              "GmBridgeEnabled": false,
              "GmBridgeBackend": "Disabled"
            }
            """,
            Encoding.UTF8);
    }

    private static string QuotePowerShell(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string QuoteProcessArgument(string value) =>
        "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string ReadRepoFile(string relativePath)
    {
        var root = LocateRepoRoot();
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string ExtractFunctionBlock(string text, string functionHeader)
    {
        var start = text.IndexOf(functionHeader, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected {functionHeader}.");

        var nextFunction = text.IndexOf("\nfunction ", start + functionHeader.Length, StringComparison.Ordinal);
        Assert.True(nextFunction > start, $"Expected another function after {functionHeader}.");
        return text[start..nextFunction];
    }

    private static string ExtractTemplateBlock(string text, string relativePath)
    {
        var marker = "-RelativePath \"" + relativePath + "\"";
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected template {relativePath}.");

        var contentStart = text.IndexOf("-Content @'", start, StringComparison.Ordinal);
        Assert.True(contentStart > start, $"Expected content here-string for template {relativePath}.");
        var bodyStart = text.IndexOf('\n', contentStart);
        Assert.True(bodyStart > contentStart, $"Expected template body for {relativePath}.");

        var end = text.IndexOf("\n'@", bodyStart, StringComparison.Ordinal);
        Assert.True(end > bodyStart, $"Expected template terminator for {relativePath}.");
        return text[bodyStart..end];
    }

    private static string LocateRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "TheBookOfEternityReborn.sln")) ||
                File.Exists(Path.Combine(current, ".git")) ||
                Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            if (parent == null)
                break;
            current = parent.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
