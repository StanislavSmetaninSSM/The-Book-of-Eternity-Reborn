using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Services;
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
    public void Helper_CompleteBoeTurnRejectsRequestThatAlreadyHasTerminalError()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-terminal-error-" + Guid.NewGuid().ToString("N"));
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
                  "requestId": "request-terminal-error",
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
                  "requestId": "request-terminal-error",
                  "turnNumber": 46
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.authority.json"),
                "{}",
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "ready", "turn_error.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-terminal-error",
                  "turnNumber": 46,
                  "timestamp": "2026-07-04T00:02:35Z",
                  "status": "error",
                  "error": "terminal error already emitted"
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
            Assert.Contains("terminal", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(session, "ready", "turn_complete.json")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_WriteBoeJsonRejectsRuntimeWriteAfterTerminalError()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-terminal-write-" + Guid.NewGuid().ToString("N"));
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
                  "requestId": "request-terminal-write",
                  "turnNumber": 47,
                  "playerAction": "test"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-terminal-write",
                  "turnNumber": 47
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.authority.json"),
                "{}",
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "ready", "turn_error.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-terminal-write",
                  "turnNumber": 47,
                  "timestamp": "2026-07-04T00:02:35Z",
                  "status": "error",
                  "error": "terminal error already emitted"
                }
                """,
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "$payload = @{ response = 'late write' }",
                "Write-BoeJson -RelativePath 'output/narrative_response.json' -Data $payload"
            });

            var result = RunPowerShell(command);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("terminal", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(session, "output", "narrative_response.json")));
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
    public void Helper_CompleteBoeTurnRejectsFirstChaosSeaSystemGuardianBootstrapMirrorMutation()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-system-guardian-mirror-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "game_state", "meta"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control", "pending_turn_snapshot", "game_state", "meta"));
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
                  "requestId": "request-system-guardian-mirror",
                  "turnNumber": 1,
                  "playerAction": "Душа по имени «Искра Странствий» пробуждается в Море Хаоса. Хранитель: Азалия. Опиши обитель Хранителя и первую встречу с ним."
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-system-guardian-mirror",
                  "turnNumber": 1
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot.authority.json"),
                "{}",
                Encoding.UTF8);

            const string snapshotGuardianJson = """
                {
                  "guardians": [
                    {
                      "guardianId": "guard_system_azalia_001",
                      "canonicalName": "Азалия",
                      "originType": "system_preset",
                      "sourcePreset": {
                        "presetId": "azalia",
                        "displayName": "Азалия",
                        "version": "1.0",
                        "library": "built_in"
                      }
                    }
                  ],
                  "activeGuardian": {
                    "guardianId": "guard_system_azalia_001",
                    "canonicalName": "Азалия",
                    "originType": "system_preset",
                    "sourcePreset": {
                      "presetId": "azalia",
                      "displayName": "Азалия",
                      "version": "1.0",
                      "library": "built_in"
                    }
                  },
                  "chaosSeaNavigation": {
                    "currentAbodeId": "abode_system_azalia_001",
                    "currentGuardianId": "guard_system_azalia_001"
                  }
                }
                """;
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_turn_snapshot", "game_state", "meta", "guardians.json"),
                snapshotGuardianJson,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "meta", "guardians.json"),
                snapshotGuardianJson.Replace(
                    "\"library\": \"built_in\"",
                    "\"library\": \"built_in\", \"gmAddedNarrationState\": \"should not be here\""),
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
            Assert.Contains("system Guardian", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("guardians.json", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
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
    public void Helper_CompleteBoeValidationRepairAllowsRepairReadyAfterAcceptedTurnCompletion()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-validation-repair-terminal-" + Guid.NewGuid().ToString("N"));
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
                  "requestId": "request-repair-ready",
                  "turnNumber": 48,
                  "playerAction": "test"
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "ready", "turn_complete.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-repair-ready",
                  "turnNumber": 48,
                  "timestamp": "2026-07-04T00:02:35Z",
                  "status": "success",
                  "filesModified": [ "output/narrative_response.json" ]
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "validation_repair_request.json"),
                """
                {
                  "sessionId": "test-session",
                  "requestId": "request-repair-ready",
                  "turnNumber": 48,
                  "metadataDiagnosticOnly": false
                }
                """,
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "Complete-BoeValidationRepair"
            });

            var result = RunPowerShell(command);

            Assert.Equal(0, result.ExitCode);
            var readyPath = Path.Combine(session, "game_state", "control", "validation_repair_ready.json");
            Assert.True(File.Exists(readyPath), result.StdErr + result.StdOut);

            using var document = JsonDocument.Parse(File.ReadAllText(readyPath, Encoding.UTF8));
            var rootElement = document.RootElement;
            Assert.Equal("test-session", rootElement.GetProperty("sessionId").GetString());
            Assert.Equal("request-repair-ready", rootElement.GetProperty("requestId").GetString());
            Assert.Equal(48, rootElement.GetProperty("turnNumber").GetInt32());
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
    public void Helper_ReadBoeJsonReturnsObjectsThatAllowMissingPropertyDotAssignment()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-mutable-json-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var npcPath = Path.Combine(session, "game_state", "npcs", "npc_core.json");
        Directory.CreateDirectory(Path.GetDirectoryName(npcPath)!);

        try
        {
            File.WriteAllText(
                npcPath,
                """
                {
                  "npcs": [
                    {
                      "npcId": "npc_mara",
                      "name": "Мара Дымная"
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                "$ErrorActionPreference = 'Stop'",
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "$root = Read-BoeJson -RelativePath 'game_state/npcs/npc_core.json'",
                "$npc = @($root.npcs)[0]",
                "$npc.trainingShowcase = [ordered]@{ teacherId = 'npc_mara'; items = @([ordered]@{ skillId = 'skill_magic_flow'; price = 12 }) }",
                "Write-BoeJson -RelativePath 'game_state/npcs/npc_core.json' -Data $root -Depth 20"
            });

            var result = RunPowerShell(command);

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StdErr), result.StdErr);
            using var document = JsonDocument.Parse(File.ReadAllText(npcPath, Encoding.UTF8));
            var trainingShowcase = document.RootElement
                .GetProperty("npcs")[0]
                .GetProperty("trainingShowcase");
            Assert.Equal("npc_mara", trainingShowcase.GetProperty("teacherId").GetString());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void Helper_SetBoeJsonPropertyReplacesExistingNonPropertyMember()
    {
        var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
        var command = string.Join("; ", new[]
        {
            "$ErrorActionPreference = 'Stop'",
            ". " + QuotePowerShell(helperPath),
            "$actor = [pscustomobject]@{ name = 'Gate thief' }",
            "$actor | Add-Member -MemberType ScriptMethod -Name role -Value { 'old role' }",
            "Set-BoeJsonProperty -Object $actor -Name 'role' -Value 'combatant'",
            "if ($actor.role -ne 'combatant') { throw \"Unexpected role: $($actor.role)\" }",
            "$member = $actor.PSObject.Members | Where-Object { $_.Name -eq 'role' } | Select-Object -First 1",
            "if ($member.MemberType -ne [System.Management.Automation.PSMemberTypes]::NoteProperty) { throw \"Unexpected role member type: $($member.MemberType)\" }"
        });

        var result = RunPowerShell(command);

        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StdErr), result.StdErr);
    }

    [Fact]
    public void Helper_CompleteBoeNpcTradeInventoryRequestFindsSameTurnInitialIdAndWritesReceipt()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-turn-helper-npc-trade-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(session, "game_state", "control"));
        Directory.CreateDirectory(Path.Combine(session, "game_state", "npcs"));

        try
        {
            File.WriteAllText(
                Path.Combine(session, "game_state", "control", "pending_npc_trade_inventory_requests.json"),
                """
                {
                  "requests": [
                    {
                      "requestId": "npc_trade_req_egor_001",
                      "npcId": "npc_egor_frontier_trader",
                      "npcName": "Егор",
                      "merchantProfile": "GeneralGoods",
                      "tradeCycleId": "world_trade_0",
                      "derivedTradeSlotCount": 2,
                      "createdAtTurn": 8,
                      "createdAtUtc": "2026-07-06T00:00:00Z",
                      "createdAtWorldDate": 100,
                      "refreshAfterWorldDate": 43200
                    }
                  ]
                }
                """,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(session, "game_state", "npcs", "npc_core.json"),
                """
                {
                  "NPCsInScene": [
                    {
                      "npcId": null,
                      "initialId": "npc_egor_frontier_trader",
                      "npcName": "Егор",
                      "name": "Егор",
                      "tradeState": {
                        "canTrade": true,
                        "merchantProfile": "GeneralGoods"
                      }
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            var helperPath = Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "Launcher", "GM_Turn_Helper.ps1");
            var command = string.Join("; ", new[]
            {
                "$ErrorActionPreference = 'Stop'",
                ". " + QuotePowerShell(helperPath),
                "Initialize-BoeGmTurnHelper -GameSessionPath " + QuotePowerShell(session),
                "$items = @(" +
                    "[ordered]@{ itemId = 'npc_item_egor_bread'; price = 12; itemData = [ordered]@{ itemId = 'npc_item_egor_bread'; name = 'Дорожный хлеб'; description = 'Плотная буханка для дороги.'; type = 'Food'; tradeItemClass = 'Functional'; quality = 'Common'; price = 10; baseSellPrice = 4; weight = '0.2'; group = 'Еда' } }, " +
                    "[ordered]@{ slotId = 'npc_trade_slot_custom'; itemId = 'npc_item_egor_lantern'; price = 25; merchantProfile = 'GeneralGoods'; soldOut = $false; itemData = [ordered]@{ itemId = 'npc_item_egor_lantern'; name = 'Масляный фонарь'; description = 'Простой фонарь для темных дорог.'; type = 'Tool'; tradeItemClass = 'FlavorOrUtility'; quality = 'Common'; price = 22; baseSellPrice = 8; weight = '0.7'; group = 'Инструменты' } })",
                "Complete-BoeNpcTradeInventoryRequest -RequestId 'npc_trade_req_egor_001' -Items $items -GenerationTradeTier 'Good' -PricingTradeTier 'Neutral'"
            });

            var result = RunPowerShell(command);

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StdErr), result.StdErr);

            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(session, "game_state", "npcs", "npc_core.json"), Encoding.UTF8));
            var npcRoot = document.RootElement;
            var npc = npcRoot.GetProperty("NPCsInScene")[0];
            var tradeInventory = npc.GetProperty("tradeInventory");
            Assert.Equal("world_trade_0", tradeInventory.GetProperty("tradeCycleId").GetString());
            Assert.Equal(100, tradeInventory.GetProperty("generatedAtWorldDate").GetInt32());
            Assert.Equal(43200, tradeInventory.GetProperty("refreshAfterWorldDate").GetInt32());
            Assert.Equal("Good", tradeInventory.GetProperty("generationTradeTier").GetString());
            Assert.Equal("Neutral", tradeInventory.GetProperty("pricingTradeTier").GetString());
            Assert.Equal(2, tradeInventory.GetProperty("items").GetArrayLength());
            Assert.Equal("GeneralGoods", tradeInventory.GetProperty("items")[0].GetProperty("merchantProfile").GetString());
            Assert.False(tradeInventory.GetProperty("items")[0].GetProperty("soldOut").GetBoolean());

            var receipt = npcRoot.GetProperty("UpdateNpcTradeInventoryReceipts")[0];
            Assert.Equal("npc_trade_req_egor_001", receipt.GetProperty("requestId").GetString());
            Assert.Equal("npc_egor_frontier_trader", receipt.GetProperty("npcId").GetString());
            Assert.Equal("Егор", receipt.GetProperty("npcName").GetString());
            Assert.Equal("world_trade_0", receipt.GetProperty("tradeCycleId").GetString());
            Assert.Equal("GeneralGoods", receipt.GetProperty("merchantProfile").GetString());
            Assert.Equal("ready", receipt.GetProperty("status").GetString());
            Assert.Equal(2, receipt.GetProperty("itemCount").GetInt32());
            Assert.Equal(8, receipt.GetProperty("resolvedAtTurn").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(receipt.GetProperty("resolvedAtUtc").GetString()));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
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
        Assert.Contains("Complete-BoeNpcTradeInventoryRequest", daemon, StringComparison.Ordinal);
        Assert.Contains("@('NPCId','npcId','id','initialId')", daemon, StringComparison.Ordinal);
        Assert.Contains("UpdateNpcTradeInventoryReceipts", daemon, StringComparison.Ordinal);
        Assert.Contains("mutable JSON-like objects that preserve arrays", daemon, StringComparison.Ordinal);
        Assert.Contains("$object.newField = <value>", daemon, StringComparison.Ordinal);
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
    public void DaemonTurnPrompt_InlinesCompactExperienceLessons()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var turnBlock = ExtractFunctionBlock(daemon, "function Process-Turn");

        Assert.Contains("function Get-GmExperiencePromptDigest", daemon, StringComparison.Ordinal);
        Assert.Contains("$experiencePrompt = Get-GmExperiencePromptDigest", turnBlock, StringComparison.Ordinal);

        var refreshIndex = turnBlock.IndexOf("$null = Write-GmExperienceLessons", StringComparison.Ordinal);
        var digestIndex = turnBlock.IndexOf("$experiencePrompt = Get-GmExperiencePromptDigest", StringComparison.Ordinal);
        var messageIndex = turnBlock.IndexOf("$message = \"Process turn", StringComparison.Ordinal);

        Assert.True(refreshIndex >= 0, "Expected ordinary turn to refresh experience lessons.");
        Assert.True(digestIndex > refreshIndex, "Expected digest to be built after refreshed lessons.");
        Assert.True(messageIndex > digestIndex, "Expected dispatch prompt to include the refreshed digest.");

        Assert.Contains("$($experiencePrompt)", turnBlock, StringComparison.Ordinal);
        Assert.Contains("RLM PRE-TURN LESSONS", daemon, StringComparison.Ordinal);
        Assert.Contains("acceptedFix", daemon, StringComparison.Ordinal);
        Assert.Contains("preferredHarnessSurface", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonTurnPrompt_AddsFirstMortalBootstrapOutputChecklist()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var turnBlock = ExtractFunctionBlock(daemon, "function Process-Turn");

        Assert.Contains("function Get-FirstMortalBootstrapPrompt", daemon, StringComparison.Ordinal);
        Assert.Contains("$firstMortalBootstrapPrompt = Get-FirstMortalBootstrapPrompt -TurnRequest $turnRequest", turnBlock, StringComparison.Ordinal);
        Assert.Contains("$($firstMortalBootstrapPrompt)", turnBlock, StringComparison.Ordinal);
        Assert.Contains("FIRST MORTAL BOOTSTRAP OUTPUT CHECKLIST", daemon, StringComparison.Ordinal);
        Assert.Contains("game_state/control/mortal_bootstrap_scaffold.json", daemon, StringComparison.Ordinal);
        Assert.Contains("output/narrative_response.json", daemon, StringComparison.Ordinal);
        Assert.Contains("output/debug_logs.json", daemon, StringComparison.Ordinal);
        Assert.Contains("Complete-BoeTurn", daemon, StringComparison.Ordinal);
        Assert.Contains("do not open large examples", daemon, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DaemonTurnPrompt_UsesCompactFirstMortalBootstrapDispatchPacket()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var turnBlock = ExtractFunctionBlock(daemon, "function Process-Turn");
        var compactBuilder = ExtractFunctionBlock(daemon, "function Build-FirstMortalBootstrapDispatchMessage");

        Assert.Contains("Build-FirstMortalBootstrapDispatchMessage", daemon, StringComparison.Ordinal);
        Assert.Contains("if (-not [string]::IsNullOrWhiteSpace($firstMortalBootstrapPrompt))", turnBlock, StringComparison.Ordinal);
        Assert.Contains("Build-FirstMortalBootstrapDispatchMessage", turnBlock, StringComparison.Ordinal);
        Assert.Contains("FIRST MORTAL BOOTSTRAP OUTPUT CHECKLIST", compactBuilder, StringComparison.Ordinal);
        Assert.Contains("game_state/control/mortal_bootstrap_scaffold.json", compactBuilder, StringComparison.Ordinal);
        Assert.Contains("$script:CompactTurnOutputTemplatePath", compactBuilder, StringComparison.Ordinal);
        Assert.Contains("$script:CompactActorReasoningTemplatePath", compactBuilder, StringComparison.Ordinal);
        Assert.Contains("$script:GmTurnHelperDirective", compactBuilder, StringComparison.Ordinal);
        Assert.Contains("Complete-BoeTurn -FilesModified", compactBuilder, StringComparison.Ordinal);
        Assert.DoesNotContain("You MUST read '$($script:CompactMortalFactionTemplatePath)'", compactBuilder, StringComparison.Ordinal);
        Assert.DoesNotContain("You MUST read '$($script:CompactMortalCombatTemplatePath)'", compactBuilder, StringComparison.Ordinal);
        Assert.DoesNotContain("$script:AfterlifeRealmGateDirective", compactBuilder, StringComparison.Ordinal);
        Assert.DoesNotContain("$script:WeatherContractDirective", compactBuilder, StringComparison.Ordinal);
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
        Assert.Contains("Templates\\MORTAL_SKILL_PROGRESSION_TEMPLATE.md", daemon, StringComparison.Ordinal);
        Assert.Contains("Templates\\MORTAL_COMBAT_STATE_TEMPLATE.md", daemon, StringComparison.Ordinal);
        Assert.Contains("Templates\\AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json", daemon, StringComparison.Ordinal);

        Assert.Contains("compact_turn_output_template", daemon, StringComparison.Ordinal);
        Assert.Contains("compact_validation_repair_template", daemon, StringComparison.Ordinal);
        Assert.Contains("compact_progression_report_template", daemon, StringComparison.Ordinal);
        Assert.Contains("compact_actor_reasoning_template", daemon, StringComparison.Ordinal);
        Assert.Contains("compact_mortal_skill_progression_template", daemon, StringComparison.Ordinal);
        Assert.Contains("compact_mortal_combat_state_template", daemon, StringComparison.Ordinal);
        Assert.Contains("compact_tempo_advantage_template", daemon, StringComparison.Ordinal);

        Assert.Contains("$script:CompactTurnOutputTemplatePath", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:CompactValidationRepairTemplatePath", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:CompactProgressionReportTemplatePath", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:CompactActorReasoningTemplatePath", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:CompactMortalCombatTemplatePath", daemon, StringComparison.Ordinal);
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
        Assert.Contains("\"inputValue\":", daemon, StringComparison.Ordinal);
        Assert.Contains("\"category\":", daemon, StringComparison.Ordinal);
        Assert.Contains("keep `text` clean for the player", daemon, StringComparison.Ordinal);
        Assert.Contains("Do not show control tags", daemon, StringComparison.Ordinal);
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
        Assert.Contains("Direct-speaking or directly addressed Mortal actors must not be excluded only because their personal name is unknown", daemon, StringComparison.Ordinal);
        Assert.Contains("## Reasoning", daemon, StringComparison.Ordinal);
        Assert.Contains("New-StringFromCodePoints", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("## Scope\n", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("## Actor reasoning", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("AfterlifeProfile", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonCompactTemplates_PreventFirstIncarnationBootstrapRepairPatterns()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var turnTemplate = ExtractHereStringBodyByNeedle(daemon, "# Compact Turn Output Template");
        var progressionTemplate = ExtractTemplateBlock(daemon, "Templates\\PROGRESSION_REPORT_TEMPLATE.json");
        var afterlifeChronicleTemplate = ExtractTemplateBlock(daemon, "Templates\\AFTERLIFE_CHRONICLE_TEMPLATE.md");
        var locationTemplate = ExtractTemplateBlock(daemon, "Templates\\MORTAL_LOCATION_TRANSITION_TEMPLATE.md");

        Assert.Contains(
            "Always include timestamp in both output/narrative_response.json and output/debug_logs.json",
            turnTemplate,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not skip NPC Scope during incarnation bootstrap",
            turnTemplate,
            StringComparison.Ordinal);
        Assert.Contains(
            "Copy exact sessionId/requestId/turnNumber and include every processed-count field even when value is 0",
            turnTemplate,
            StringComparison.Ordinal);
        Assert.Contains(
            "Fresh New Game system Guardian seed is client-owned",
            turnTemplate,
            StringComparison.Ordinal);
        Assert.Contains(
            "For fresh Mortal bootstrap, copy canonical coordinates from game_state/control/mortal_bootstrap_scaffold.json",
            turnTemplate,
            StringComparison.Ordinal);
        Assert.Contains(
            "current_location_coordinates_mismatch",
            turnTemplate,
            StringComparison.Ordinal);
        Assert.Contains("\"chaosSeaCyclesProcessed\": 0", progressionTemplate, StringComparison.Ordinal);
        Assert.Contains("\"guardianProjectCyclesProcessed\": 0", progressionTemplate, StringComparison.Ordinal);
        Assert.Contains("\"residentAgencyCyclesProcessed\": 0", progressionTemplate, StringComparison.Ordinal);
        Assert.Contains(
            "Use Russian in-world terms: посмертие, Море Хаоса, Сияющая Обитель, смертный мир",
            afterlifeChronicleTemplate,
            StringComparison.Ordinal);
        Assert.Contains("activeThreats", locationTemplate, StringComparison.Ordinal);
        Assert.Contains("canonical `biome`", locationTemplate, StringComparison.Ordinal);
        Assert.Contains("TemperateForest", locationTemplate, StringComparison.Ordinal);
        Assert.Contains("adjacencyMap", locationTemplate, StringComparison.Ordinal);
        Assert.Contains("locationStorages", locationTemplate, StringComparison.Ordinal);
        Assert.Contains("internalDifficultyProfile", locationTemplate, StringComparison.Ordinal);
        Assert.Contains("externalDifficultyProfile", locationTemplate, StringComparison.Ordinal);
        Assert.Contains("estimatedInternalDifficultyProfile", locationTemplate, StringComparison.Ordinal);
        Assert.Contains("estimatedExternalDifficultyProfile", locationTemplate, StringComparison.Ordinal);
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
            Assert.Contains("\"inputValue\":", turnTemplate, StringComparison.Ordinal);
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
    public void DaemonContextPack_RuntimeGeneratesEveryAdvertisedBootstrapArtifact()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-context-pack-advertised-" + Guid.NewGuid().ToString("N"));
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

            var contextPack = Path.Combine(session, "game_state", "control", "gm_context_pack");
            var advertised = new[]
            {
                Path.Combine(contextPack, "context_pack_manifest.json"),
                Path.Combine(contextPack, "README.md"),
                Path.Combine(contextPack, "Templates", "TURN_OUTPUT_TEMPLATE.md"),
                Path.Combine(contextPack, "Templates", "VALIDATION_REPAIR_TEMPLATE.md"),
                Path.Combine(contextPack, "Templates", "PROGRESSION_REPORT_TEMPLATE.json"),
                Path.Combine(contextPack, "Templates", "ACTOR_REASONING_TEMPLATE.md"),
                Path.Combine(contextPack, "Templates", "MORTAL_NPC_UPDATE_TEMPLATE.md"),
                Path.Combine(contextPack, "Templates", "MORTAL_FACTION_UPDATE_TEMPLATE.md"),
                Path.Combine(contextPack, "Templates", "MORTAL_LOCATION_TRANSITION_TEMPLATE.md"),
                Path.Combine(contextPack, "Templates", "MORTAL_SKILL_PROGRESSION_TEMPLATE.md"),
                Path.Combine(contextPack, "Templates", "MORTAL_EXPERIENCE_LEVEL_TEMPLATE.md"),
                Path.Combine(contextPack, "Templates", "MORTAL_COMBAT_STATE_TEMPLATE.md"),
                Path.Combine(contextPack, "Templates", "AFTERLIFE_CHRONICLE_TEMPLATE.md"),
                Path.Combine(contextPack, "Templates", "AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json"),
                Path.Combine(contextPack, "Lessons", "GM_EXPERIENCE_LESSONS.md"),
                Path.Combine(contextPack, "Lessons", "GM_EXPERIENCE_LESSONS.json"),
                Path.Combine(contextPack, "Probes", "GM_SAFE_PROBES.md"),
                Path.Combine(contextPack, "Rubrics", "GM_LIVE_TEST_RUBRIC.md"),
                Path.Combine(contextPack, "Rubrics", "GM_LIVE_TEST_RUBRIC.json")
            };

            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline && advertised.Any(path => !File.Exists(path)))
            {
                if (process.HasExited)
                    break;

                Thread.Sleep(100);
            }

            var stdOut = process.HasExited ? process.StandardOutput.ReadToEnd() : string.Empty;
            var stdErr = process.HasExited ? process.StandardError.ReadToEnd() : string.Empty;
            var missing = advertised.Where(path => !File.Exists(path)).ToArray();
            Assert.True(missing.Length == 0, "Missing advertised context-pack files:" + Environment.NewLine + string.Join(Environment.NewLine, missing) + Environment.NewLine + stdErr + stdOut);

            var manifestText = File.ReadAllText(Path.Combine(contextPack, "context_pack_manifest.json"), Encoding.UTF8);
            Assert.Contains("PROGRESSION_REPORT_TEMPLATE.json", manifestText, StringComparison.Ordinal);
            Assert.Contains("MORTAL_SKILL_PROGRESSION_TEMPLATE.md", manifestText, StringComparison.Ordinal);
            Assert.Contains("MORTAL_EXPERIENCE_LEVEL_TEMPLATE.md", manifestText, StringComparison.Ordinal);
            Assert.Contains("MORTAL_COMBAT_STATE_TEMPLATE.md", manifestText, StringComparison.Ordinal);
            Assert.Contains("AFTERLIFE_CHRONICLE_TEMPLATE.md", manifestText, StringComparison.Ordinal);
            Assert.Contains("AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json", manifestText, StringComparison.Ordinal);
            Assert.Contains("GM_LIVE_TEST_RUBRIC.json", manifestText, StringComparison.Ordinal);

            var turnTemplate = File.ReadAllText(Path.Combine(contextPack, "Templates", "TURN_OUTPUT_TEMPLATE.md"), Encoding.UTF8);
            Assert.Contains("- Текущая локация / Current location:", turnTemplate, StringComparison.Ordinal);
            Assert.Contains("For EVERY relevant NPC block, the current-location line is mandatory", turnTemplate, StringComparison.Ordinal);
            Assert.Contains("Only `response` and `timestamp` belong in `output/narrative_response.json`", turnTemplate, StringComparison.Ordinal);
            Assert.Contains("Never put `afterlifeChronicleUpdates` into `output/narrative_response.json`", turnTemplate, StringComparison.Ordinal);

            var actorTemplate = File.ReadAllText(Path.Combine(contextPack, "Templates", "ACTOR_REASONING_TEMPLATE.md"), Encoding.UTF8);
            Assert.Contains("- Текущая локация / Current location:", actorTemplate, StringComparison.Ordinal);
            Assert.Contains("For EVERY relevant NPC block, the current-location line is mandatory", actorTemplate, StringComparison.Ordinal);

            var mortalNpcTemplate = File.ReadAllText(Path.Combine(contextPack, "Templates", "MORTAL_NPC_UPDATE_TEMPLATE.md"), Encoding.UTF8);
            Assert.Contains("NPCsInScene is only for actors physically present in currentLocationData", mortalNpcTemplate, StringComparison.Ordinal);
            Assert.Contains("voices behind a door", mortalNpcTemplate, StringComparison.Ordinal);
            Assert.Contains("nearbyExitLocationId", mortalNpcTemplate, StringComparison.Ordinal);

            var repairTemplate = File.ReadAllText(Path.Combine(contextPack, "Templates", "VALIDATION_REPAIR_TEMPLATE.md"), Encoding.UTF8);
            Assert.Contains("accepted_turn_output_artifact_repair", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("output/narrative_response.json", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("output/interface_updates.json", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("output/debug_logs.json", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("afterlife_chronicle_string_array_repair", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("persistentConsequences[]", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("openThreads[]", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("do not add `eventDescriptions[]`", repairTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("afterlife_spiritual_conflict_reward_repair", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("afterlife_entity_profile_scaffold_repair", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("specialArtLearningReceipts", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("guardian_trade_inventory_resolution_repair", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("pending_guardian_trade_request.json", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("guardian.tradeInventory", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("guardian_pending_creation_materialization_repair", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("pendingGuardianCreation", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("UpdateGuardians.create", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("command=create", repairTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("data=<full canonical Guardian>", repairTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("UpdateGuardians[]", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("JSON array", repairTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("canonicalCreateSkeleton", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("allowedEnums", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("pending-only fallback", repairTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("narrative_response_unknown_field", repairTemplate, StringComparison.Ordinal);
            Assert.Contains("remove the unsupported field from `output/narrative_response.json`", repairTemplate, StringComparison.OrdinalIgnoreCase);

            var afterlifeChronicleTemplate = File.ReadAllText(Path.Combine(contextPack, "Templates", "AFTERLIFE_CHRONICLE_TEMPLATE.md"), Encoding.UTF8);
            Assert.Contains("afterlifeChronicleUpdates[]", afterlifeChronicleTemplate, StringComparison.Ordinal);
            Assert.Contains("Never write `afterlifeChronicleUpdates` into `output/narrative_response.json`", afterlifeChronicleTemplate, StringComparison.Ordinal);
            Assert.Contains("first meeting", afterlifeChronicleTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("significant Guardian dialogue", afterlifeChronicleTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("persistentConsequences[]", afterlifeChronicleTemplate, StringComparison.Ordinal);
            Assert.Contains("openThreads[]", afterlifeChronicleTemplate, StringComparison.Ordinal);
            Assert.Contains("never include `eventDescriptions[]`", afterlifeChronicleTemplate, StringComparison.OrdinalIgnoreCase);
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
            WriteDaemonPendingTurnSnapshot(session, "trajectory-session", "request-success", 77);
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
    public void DaemonTrajectoryLedger_BackfillsLiveTestNoteRecordIdsForSameRequest()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-note-link-" + Guid.NewGuid().ToString("N"));
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
                  "requestId": "request-note-link",
                  "turnNumber": 81,
                  "currentRealm": "Chaos Sea",
                  "playerAction": "I test live-test note correlation.",
                  "progressionControl": {
                    "currentRealm": "Chaos Sea"
                  }
                }
                """,
                Encoding.UTF8);
            WriteDaemonPendingTurnSnapshot(session, "trajectory-session", "request-note-link", 81);
            File.WriteAllText(
                Path.Combine(session, "ready", "turn_complete.json"),
                """
                {
                  "sessionId": "trajectory-session",
                  "requestId": "request-note-link",
                  "turnNumber": 81,
                  "status": "success",
                  "filesModified": [
                    "output/narrative_response.json",
                    "game_state/control/gm_live_test_notes.jsonl"
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
            File.WriteAllText(
                Path.Combine(control, "gm_live_test_notes.jsonl"),
                """
                {"noteId":"note_1","recordId":"unknown","requestId":"request-note-link","turnNumber":81,"realm":"ChaosSea","dimension":"harness_containment","severity":"info","observation":"The GM wrote this before ledger emission.","harnessFollowUp":"Link this note after the trajectory record exists.","issueRef":"#1290","createdAtUtc":"2026-06-29T17:37:20Z"}

                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var ledgerPath = Path.Combine(control, "gm_trajectory_ledger.jsonl");
            Assert.True(WaitForFileContaining(ledgerPath, "request-note-link", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            using var ledgerDocument = JsonDocument.Parse(File.ReadLines(ledgerPath, Encoding.UTF8).Last());
            var recordId = ledgerDocument.RootElement.GetProperty("recordId").GetString();
            Assert.StartsWith("gmtraj_", recordId);

            var notesPath = Path.Combine(control, "gm_live_test_notes.jsonl");
            Assert.True(WaitForFileContaining(notesPath, recordId!, process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));
            var noteLine = File.ReadLines(notesPath, Encoding.UTF8).Single(line => line.Contains("note_1", StringComparison.Ordinal));
            using var noteDocument = JsonDocument.Parse(noteLine);
            Assert.Equal(recordId, noteDocument.RootElement.GetProperty("recordId").GetString());
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
            WriteDaemonPendingTurnSnapshot(session, "trajectory-session", "request-soul-realm", 79);
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
            WriteDaemonPendingTurnSnapshot(session, "trajectory-session", "request-needs-repair", 81);
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
                      "path": "game_state/inventory/items.json",
                      "jsonPath": "$.items[0].itemId",
                      "message": "Referenced item is missing.",
                      "expected": "Known item id",
                      "actual": "item_missing",
                      "repairHint": "Materialize the item or remove the reference."
                    }
                  ],
                  "harnessRepairPackets": [
                    {
                      "packetId": "repair-packet-1",
                      "targetFiles": [
                        "game_state/inventory/items.json"
                      ]
                    },
                    {
                      "kind": "mortal_bootstrap_materialization_repair",
                      "targetFiles": [
                        "game_state/world/current_location.json",
                        "game_state/world/world_map.json"
                      ]
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
            Assert.Equal("mortal_bootstrap_materialization_repair", record.GetProperty("validation").GetProperty("repairPacketRefs")[1].GetString());
            var diagnostics = record.GetProperty("validation").GetProperty("diagnostics");
            Assert.Equal("inventory_item_missing", diagnostics[0].GetProperty("code").GetString());
            Assert.Equal("StateConsistency", diagnostics[0].GetProperty("category").GetString());
            Assert.Equal("Inventory", diagnostics[0].GetProperty("section").GetString());
            Assert.Equal("game_state/inventory/items.json", diagnostics[0].GetProperty("path").GetString());
            Assert.Equal("$.items[0].itemId", diagnostics[0].GetProperty("jsonPath").GetString());
            Assert.Equal("Referenced item is missing.", diagnostics[0].GetProperty("message").GetString());
            Assert.Equal("Known item id", diagnostics[0].GetProperty("expected").GetString());
            Assert.Equal("item_missing", diagnostics[0].GetProperty("actual").GetString());
            Assert.Equal("Materialize the item or remove the reference.", diagnostics[0].GetProperty("repairHint").GetString());
            var packetDiagnostics = record.GetProperty("validation").GetProperty("repairPacketDiagnostics");
            Assert.Equal("repair-packet-1", packetDiagnostics[0].GetProperty("packetId").GetString());
            Assert.Equal("game_state/inventory/items.json", packetDiagnostics[0].GetProperty("targetFiles")[0].GetString());
            Assert.Equal("mortal_bootstrap_materialization_repair", packetDiagnostics[1].GetProperty("kind").GetString());
            Assert.Equal("game_state/world/current_location.json", packetDiagnostics[1].GetProperty("targetFiles")[0].GetString());
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
            WriteDaemonPendingTurnSnapshot(session, "trajectory-session", "request-worker-proposal", 79);
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
                {"schemaVersion":1,"eventId":"worker_audit_inventory_dispatch","eventType":"task-dispatched","workerId":"inventory_content_codex","taskId":"worker_task_inventory_content_0001","timestampUtc":"2099-01-01T00:00:02Z","summary":"Dispatched inventory-content worker task.","details":{"taskType":["inventory-content"],"responseContract":["worker-proposal-v1"],"allowedProposalPaths":[]}}
                {"schemaVersion":1,"eventId":"worker_audit_inventory_proposal","eventType":"proposal-received","workerId":"inventory_content_codex","taskId":"worker_task_inventory_content_0001","proposalId":"worker_proposal_inventory_content_0001","timestampUtc":"2099-01-01T00:00:03Z","summary":"Prepared item proposals for main-GM review.","details":{"changedFiles":[]}}
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
            Assert.Contains(workerEvents, workerEvent =>
                workerEvent.GetProperty("eventType").GetString() == "task-dispatched" &&
                workerEvent.GetProperty("workerId").GetString() == "inventory_content_codex" &&
                workerEvent.GetProperty("taskType").GetString() == "inventory-content" &&
                workerEvent.GetProperty("proposalOnly").GetBoolean());
            Assert.Contains(workerEvents, workerEvent =>
                workerEvent.GetProperty("eventType").GetString() == "proposal-received" &&
                workerEvent.GetProperty("proposalId").GetString() == "worker_proposal_inventory_content_0001" &&
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
            var diagnostics = record.GetProperty("validation").GetProperty("diagnostics");
            Assert.Equal("normalized_weather_missing_description", diagnostics[0].GetProperty("code").GetString());
            Assert.Equal("game_state/world/weather.json", diagnostics[0].GetProperty("path").GetString());
            Assert.Equal("Weather description is missing.", diagnostics[0].GetProperty("message").GetString());
            Assert.DoesNotContain("validation_repair_request.json", record.GetRawText(), StringComparison.OrdinalIgnoreCase);
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
    public void DaemonContextPack_RendersIdleNoOutputHarnessLesson()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-idle-no-output-lessons-" + Guid.NewGuid().ToString("N"));
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
                {"recordId":"gmtraj_first_mortal_idle_no_output","kind":"turn","sessionId":"prior","turnId":"idle-no-output","requestId":"idle-no-output","turnNumber":24,"realm":"MortalWorld","mode":"ordinary","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","mortalLocation":"v1","mortalNpc":"v1"},"outputFiles":[],"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"sent"},"validation":{"status":"rejected","issueKinds":[],"diagnostics":[]},"repair":{"attempts":0,"status":"none"},"workerEvents":[],"rollbackEvents":[],"terminal":{"kind":"error","signalPath":"ready/turn_error.json"},"rubric":{"validTurn":false,"playerFacingOutputPresent":false,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":"gm_bridge_idle_without_terminal_signal"},"createdAt":"2026-07-03T17:37:55Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "gm_bridge_idle_without_terminal_signal", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var markdown = File.ReadAllText(lessonsMarkdownPath, Encoding.UTF8);
            Assert.Contains("actionable prior harness failures", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FIRST MORTAL BOOTSTRAP OUTPUT CHECKLIST", markdown, StringComparison.Ordinal);
            Assert.Contains("mortal_bootstrap_scaffold.json", markdown, StringComparison.Ordinal);
            Assert.Contains("Complete-BoeTurn", markdown, StringComparison.Ordinal);

            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var lesson = document.RootElement.GetProperty("lessons")[0];
            Assert.Equal("TURN_OUTPUT_TEMPLATE.md", lesson.GetProperty("preferredHarnessSurface").GetString());
            Assert.Contains("gm_bridge_idle_without_terminal_signal", lesson.GetProperty("match").GetProperty("issueKinds")[0].GetString(), StringComparison.Ordinal);
            Assert.Contains("FIRST MORTAL BOOTSTRAP OUTPUT CHECKLIST", lesson.GetProperty("acceptedFix").GetString(), StringComparison.Ordinal);
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
    public void DaemonContextPack_RoutesMortalFactionIdentityLessonsToFactionTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-mortal-faction-lessons-" + Guid.NewGuid().ToString("N"));
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
                {"recordId":"gmtraj_mortal_faction_fix","kind":"repair","sessionId":"prior","turnId":"repair-faction","requestId":"repair-faction","turnNumber":5,"realm":"MortalWorld","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"accepted","issueKinds":["faction_full_object_unknown_faction_id","canonical_faction_sidecar_unknown_faction_id"],"repairPacketRefs":["faction_identity_repair"]},"repair":{"attempts":1,"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-06-28T10:01:00Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-repair",
                  "turnNumber": 6,
                  "currentRealm": "Mortal World",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "faction_full_object_unknown_faction_id",
                      "category": "StateConsistency",
                      "section": "Factions",
                      "message": "Full faction object references an unknown permanent factionId."
                    },
                    {
                      "code": "canonical_faction_sidecar_unknown_faction_id",
                      "category": "StateConsistency",
                      "section": "Factions",
                      "message": "Canonical sidecar references an unknown faction."
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "faction_full_object_unknown_faction_id", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var markdown = File.ReadAllText(lessonsMarkdownPath, Encoding.UTF8);
            Assert.Contains("MORTAL_FACTION_UPDATE_TEMPLATE.md", markdown, StringComparison.Ordinal);
            Assert.Contains("missing faction", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sidecar", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MORTAL_NPC_UPDATE_TEMPLATE.md", markdown, StringComparison.Ordinal);

            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var lesson = document.RootElement.GetProperty("lessons")[0];
            Assert.Equal("MORTAL_FACTION_UPDATE_TEMPLATE.md", lesson.GetProperty("preferredHarnessSurface").GetString());
            Assert.Contains("existing canonical factionId", lesson.GetProperty("acceptedFix").GetString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_RoutesMortalLocationTransitionLessonsToLocationTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-mortal-location-lessons-" + Guid.NewGuid().ToString("N"));
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
                {"recordId":"gmtraj_mortal_location_fix","kind":"repair","sessionId":"prior","turnId":"repair-location","requestId":"repair-location","turnNumber":6,"realm":"MortalWorld","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"accepted","issueKinds":["current_location_unknown_location_id","npc_unknown_current_location_id","world_map_new_location_coordinates_duplicate_same_turn"],"repairPacketRefs":["mortal_location_transition_repair"]},"repair":{"attempts":1,"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-06-28T10:02:00Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-repair",
                  "turnNumber": 7,
                  "currentRealm": "Mortal World",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "current_location_unknown_location_id",
                      "category": "StateConsistency",
                      "section": "WorldMap",
                      "message": "Current location references an unknown location id."
                    },
                    {
                      "code": "world_map_new_location_coordinates_duplicate_same_turn",
                      "category": "StateConsistency",
                      "section": "WorldMap",
                      "message": "Two same-turn locations use duplicate coordinates."
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "current_location_unknown_location_id", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var markdown = File.ReadAllText(lessonsMarkdownPath, Encoding.UTF8);
            Assert.Contains("MORTAL_LOCATION_TRANSITION_TEMPLATE.md", markdown, StringComparison.Ordinal);
            Assert.Contains("world_map", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("duplicate coordinates", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MORTAL_NPC_UPDATE_TEMPLATE.md", markdown, StringComparison.Ordinal);

            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var lesson = document.RootElement.GetProperty("lessons")[0];
            Assert.Equal("MORTAL_LOCATION_TRANSITION_TEMPLATE.md", lesson.GetProperty("preferredHarnessSurface").GetString());
            Assert.Contains("register", lesson.GetProperty("acceptedFix").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("current_location", lesson.GetProperty("acceptedFix").GetString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_RoutesMortalWorldMapAdjacencyLessonsToLocationTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-mortal-map-adjacency-lessons-" + Guid.NewGuid().ToString("N"));
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
                {"recordId":"gmtraj_mortal_map_adjacency_fix","kind":"repair","sessionId":"prior","turnId":"repair-map-adjacency","requestId":"repair-map-adjacency","turnNumber":6,"realm":"MortalWorld","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"accepted","issueKinds":["world_map_adjacency_unknown_target"],"repairPacketRefs":["mortal_world_map_adjacency_repair"]},"repair":{"attempts":1,"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-07-02T08:12:00Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-repair",
                  "turnNumber": 7,
                  "currentRealm": "Mortal World",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "world_map_adjacency_unknown_target",
                      "category": "StateConsistency",
                      "section": "WorldMap",
                      "message": "World map adjacency points to an unknown target location."
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "world_map_adjacency_unknown_target", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var markdown = File.ReadAllText(lessonsMarkdownPath, Encoding.UTF8);
            Assert.Contains("MORTAL_LOCATION_TRANSITION_TEMPLATE.md", markdown, StringComparison.Ordinal);
            Assert.Contains("unknown target", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fully materialized", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("VALIDATION_REPAIR_TEMPLATE.md", markdown, StringComparison.Ordinal);

            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var lesson = document.RootElement.GetProperty("lessons")[0];
            Assert.Equal("MORTAL_LOCATION_TRANSITION_TEMPLATE.md", lesson.GetProperty("preferredHarnessSurface").GetString());
            Assert.Contains("unknown target", lesson.GetProperty("acceptedFix").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fully materialized", lesson.GetProperty("acceptedFix").GetString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_MortalItemDurabilityLessonsNamePercentageString()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-mortal-item-durability-lessons-" + Guid.NewGuid().ToString("N"));
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
                {"recordId":"gmtraj_mortal_item_durability_fix","kind":"repair","sessionId":"prior","turnId":"repair-durability","requestId":"repair-durability","turnNumber":3,"realm":"MortalWorld","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"accepted","issueKinds":["validation_error"],"diagnostics":[{"code":"validation_error","category":"StateConsistency","path":"game_state/inventory/items.json.items[0].durability","message":"durability должен быть percentage string"}],"repairPacketRefs":["mortal_bootstrap_materialization_repair"]},"repair":{"attempts":1,"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-07-01T04:58:21Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-repair",
                  "turnNumber": 4,
                  "currentRealm": "Mortal World",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "validation_error",
                      "category": "StateConsistency",
                      "path": "game_state/inventory/items.json.items[0].durability",
                      "message": "durability должен быть percentage string"
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "validation_error", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var markdown = File.ReadAllText(lessonsMarkdownPath, Encoding.UTF8);
            Assert.Contains("durability", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("percentage string", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("100%", markdown, StringComparison.Ordinal);
            Assert.Contains("VALIDATION_REPAIR_TEMPLATE.md", markdown, StringComparison.Ordinal);

            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var lesson = document.RootElement.GetProperty("lessons")[0];
            Assert.Equal("VALIDATION_REPAIR_TEMPLATE.md", lesson.GetProperty("preferredHarnessSurface").GetString());
            Assert.Contains("durability", lesson.GetProperty("acceptedFix").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("100%", lesson.GetProperty("acceptedFix").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_MortalItemJournalEntriesLessonsNameStringArray()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-mortal-item-journal-entries-lessons-" + Guid.NewGuid().ToString("N"));
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
                {"recordId":"gmtraj_mortal_item_journal_fix","kind":"repair","sessionId":"prior","turnId":"repair-journal","requestId":"repair-journal","turnNumber":3,"realm":"MortalWorld","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"accepted","issueKinds":["invalid_string_array_item"],"diagnostics":[{"code":"invalid_string_array_item","category":"StateConsistency","path":"game_state/inventory/items.json.items[0].journalEntries[0]","message":"Элемент должен быть непустой строкой","expected":"non-empty string","actual":"Object","repairHint":"Исправь элемент массива до непустой строки."}],"repairPacketRefs":["mortal_bootstrap_materialization_repair"]},"repair":{"attempts":1,"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-07-01T06:43:56Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-repair",
                  "turnNumber": 4,
                  "currentRealm": "Mortal World",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "invalid_string_array_item",
                      "category": "StateConsistency",
                      "path": "game_state/inventory/items.json.items[0].journalEntries[0]",
                      "message": "Элемент должен быть непустой строкой",
                      "expected": "non-empty string",
                      "actual": "Object",
                      "repairHint": "Исправь элемент массива до непустой строки."
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "invalid_string_array_item", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var markdown = File.ReadAllText(lessonsMarkdownPath, Encoding.UTF8);
            Assert.Contains("journalEntries", markdown, StringComparison.Ordinal);
            Assert.Contains("array of non-empty strings", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not objects", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("VALIDATION_REPAIR_TEMPLATE.md", markdown, StringComparison.Ordinal);

            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var lesson = document.RootElement.GetProperty("lessons")[0];
            Assert.Equal("VALIDATION_REPAIR_TEMPLATE.md", lesson.GetProperty("preferredHarnessSurface").GetString());
            Assert.Contains("journalEntries", lesson.GetProperty("acceptedFix").GetString(), StringComparison.Ordinal);
            Assert.Contains("non-empty strings", lesson.GetProperty("acceptedFix").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not objects", lesson.GetProperty("acceptedFix").GetString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_NarrativeResponseUnknownFieldLessonsNameAfterlifeChronicleSurface()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-narrative-response-unknown-field-lessons-" + Guid.NewGuid().ToString("N"));
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
                {"recordId":"gmtraj_afterlife_chronicle_wrong_output_file","kind":"repair","sessionId":"prior","turnId":"repair-narrative-response","requestId":"repair-narrative-response","turnNumber":6,"realm":"ChaosSea","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"accepted","issueKinds":["narrative_response_unknown_field"],"diagnostics":[{"code":"narrative_response_unknown_field","category":"OutputArtifact","path":"output/narrative_response.json.afterlifeChronicleUpdates","message":"output/narrative_response.json contains unsupported field afterlifeChronicleUpdates","expected":"response | timestamp","actual":"afterlifeChronicleUpdates"}],"repairPacketRefs":["accepted_turn_output_artifact_repair"]},"repair":{"attempts":1,"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-07-01T08:21:00Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-repair",
                  "turnNumber": 7,
                  "currentRealm": "Chaos Sea",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "narrative_response_unknown_field",
                      "category": "OutputArtifact",
                      "path": "output/narrative_response.json.afterlifeChronicleUpdates",
                      "message": "output/narrative_response.json contains unsupported field afterlifeChronicleUpdates",
                      "expected": "response | timestamp",
                      "actual": "afterlifeChronicleUpdates"
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "narrative_response_unknown_field", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var markdown = File.ReadAllText(lessonsMarkdownPath, Encoding.UTF8);
            Assert.Contains("output/narrative_response.json", markdown, StringComparison.Ordinal);
            Assert.Contains("response", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("timestamp", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("afterlifeChronicleUpdates", markdown, StringComparison.Ordinal);
            Assert.Contains("AFTERLIFE_CHRONICLE_TEMPLATE.md", markdown, StringComparison.Ordinal);

            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var lesson = document.RootElement.GetProperty("lessons")[0];
            Assert.Equal("AFTERLIFE_CHRONICLE_TEMPLATE.md", lesson.GetProperty("preferredHarnessSurface").GetString());
            Assert.Contains("output/narrative_response.json", lesson.GetProperty("acceptedFix").GetString(), StringComparison.Ordinal);
            Assert.Contains("afterlifeChronicleUpdates", lesson.GetProperty("acceptedFix").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_GenericOutputArtifactLessonsNameTurnOutputTemplateForOrdinaryTurns()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-generic-output-artifact-lessons-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");
        var control = Path.Combine(session, "game_state", "control");
        var input = Path.Combine(session, "input");
        Directory.CreateDirectory(control);
        Directory.CreateDirectory(input);

        Process? process = null;
        try
        {
            WriteDaemonConfig(session);
            File.WriteAllText(
                Path.Combine(control, "gm_trajectory_ledger.jsonl"),
                """
                {"recordId":"gmtraj_mortal_generic_output_envelope","kind":"repair","sessionId":"prior","turnId":"repair-output-envelope","requestId":"repair-output-envelope","turnNumber":25,"realm":"MortalWorld","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"sent"},"validation":{"status":"accepted","issueKinds":["narrative_response_unknown_field","narrative_response_missing_timestamp","missing_gm_thoughts","debug_logs_unknown_field","debug_logs_missing_timestamp","interface_updates_missing_payload","interface_updates_unknown_field","interface_updates_missing_timestamp"],"diagnostics":[{"code":"narrative_response_unknown_field","category":"OutputArtifact","path":"output/narrative_response.json.checks","message":"output/narrative_response.json contains unsupported field checks","expected":"response | timestamp","actual":"checks"},{"code":"missing_gm_thoughts","category":"OutputArtifact","path":"output/debug_logs.json.gm_thoughts_markdown","message":"output/debug_logs.json missing gm_thoughts_markdown","expected":"gm_thoughts_markdown | timestamp","actual":"missing"},{"code":"interface_updates_missing_payload","category":"OutputArtifact","path":"output/interface_updates.json.payload","message":"output/interface_updates.json missing payload","expected":"payload | timestamp","actual":"missing"}],"repairPacketRefs":["accepted_turn_output_artifact_repair"]},"repair":{"attempts":1,"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-07-03T18:10:00Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(input, "turn_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-turn",
                  "turnNumber": 26,
                  "currentRealm": "Mortal World",
                  "playerAction": "Continue the mortal scene.",
                  "progressionControl": {
                    "currentRealm": "Mortal World"
                  }
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "narrative_response_unknown_field", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var markdown = File.ReadAllText(lessonsMarkdownPath, Encoding.UTF8);
            Assert.Contains("TURN_OUTPUT_TEMPLATE.md", markdown, StringComparison.Ordinal);
            Assert.Contains("output/narrative_response.json", markdown, StringComparison.Ordinal);
            Assert.Contains("response", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("timestamp", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("gm_thoughts_markdown", markdown, StringComparison.Ordinal);
            Assert.Contains("interface_updates.json", markdown, StringComparison.Ordinal);

            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var lesson = document.RootElement.GetProperty("lessons")[0];
            Assert.Equal("TURN_OUTPUT_TEMPLATE.md", lesson.GetProperty("preferredHarnessSurface").GetString());
            var acceptedFix = lesson.GetProperty("acceptedFix").GetString() ?? string.Empty;
            Assert.Contains("output/narrative_response.json", acceptedFix, StringComparison.Ordinal);
            Assert.Contains("gm_thoughts_markdown", acceptedFix, StringComparison.Ordinal);
            Assert.Contains("interface_updates.json", acceptedFix, StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_StaleOutputArtifactLessonsNameOutputArtifactRepairTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-stale-output-artifact-lessons-" + Guid.NewGuid().ToString("N"));
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
                {"recordId":"gmtraj_stale_player_facing_output","kind":"repair","sessionId":"prior","turnId":"repair-stale-output","requestId":"repair-stale-output","turnNumber":12,"realm":"ChaosSea","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"sent"},"validation":{"status":"accepted","issueKinds":["accepted_turn_stale_player_facing_output_after_canonical_repair"],"diagnostics":[{"code":"accepted_turn_stale_player_facing_output_after_canonical_repair","category":"ProtocolViolation","path":"output/narrative_response.json","message":"output/narrative_response.json was written before canonical validation repair","expected":"fresh output after canonical repair","actual":"stale output"}],"repairPacketRefs":["accepted_turn_output_artifact_repair"]},"repair":{"attempts":5,"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":true,"missingHarnessTool":"output_only_repair_template"},"createdAt":"2026-07-04T16:20:00Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-repair",
                  "turnNumber": 13,
                  "currentRealm": "Chaos Sea",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "accepted_turn_stale_player_facing_output_after_canonical_repair",
                      "category": "ProtocolViolation",
                      "section": "PlayerFacingOutput",
                      "path": "output/narrative_response.json",
                      "message": "output/narrative_response.json was written before canonical validation repair",
                      "expected": "fresh output after canonical repair",
                      "actual": "stale output"
                    }
                  ],
                  "harnessRepairPackets": [
                    {
                      "kind": "accepted_turn_output_artifact_repair",
                      "targetFiles": [
                        "output/narrative_response.json",
                        "output/interface_updates.json"
                      ]
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "accepted_turn_stale_player_facing_output_after_canonical_repair", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var markdown = File.ReadAllText(lessonsMarkdownPath, Encoding.UTF8);
            Assert.Contains("OUTPUT_ARTIFACT_REPAIR_TEMPLATE.md", markdown, StringComparison.Ordinal);
            Assert.Contains("output/narrative_response.json", markdown, StringComparison.Ordinal);
            Assert.Contains("output/interface_updates.json", markdown, StringComparison.Ordinal);

            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var lesson = document.RootElement.GetProperty("lessons")[0];
            Assert.Equal("OUTPUT_ARTIFACT_REPAIR_TEMPLATE.md", lesson.GetProperty("preferredHarnessSurface").GetString());
            var acceptedFix = lesson.GetProperty("acceptedFix").GetString() ?? string.Empty;
            Assert.Contains("output-only", acceptedFix, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("output/interface_updates.json", acceptedFix, StringComparison.Ordinal);
            Assert.Contains("do not touch canonical", acceptedFix, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_OutputArtifactRepairTemplateGivesNarrowRepairFlow()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-output-artifact-template-" + Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "game_session");
        var logPath = Path.Combine(root, "daemon.log");
        var control = Path.Combine(session, "game_state", "control");
        Directory.CreateDirectory(control);

        Process? process = null;
        try
        {
            WriteDaemonConfig(session);
            process = StartDaemon(session, logPath);

            var templatePath = Path.Combine(control, "gm_context_pack", "Templates", "OUTPUT_ARTIFACT_REPAIR_TEMPLATE.md");
            Assert.True(WaitForFileContaining(templatePath, "Output-only accepted turn repair", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var template = File.ReadAllText(templatePath, Encoding.UTF8);
            Assert.Contains("validation_repair_request.json", template, StringComparison.Ordinal);
            Assert.Contains("output/narrative_response.json", template, StringComparison.Ordinal);
            Assert.Contains("output/interface_updates.json", template, StringComparison.Ordinal);
            Assert.Contains("output/debug_logs.json", template, StringComparison.Ordinal);
            Assert.Contains("Do not touch canonical game_state files", template, StringComparison.Ordinal);
            Assert.Contains("Complete-BoeValidationRepair", template, StringComparison.Ordinal);
            Assert.Contains("narrative_response_technical_repair_leak", template, StringComparison.Ordinal);
            Assert.Contains("Never mention", template, StringComparison.Ordinal);
            Assert.Contains("JSON, validation, repair, canonical state, arrays", template, StringComparison.Ordinal);

            var daemon = File.ReadAllText(Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "game_master_daemon.ps1"), Encoding.UTF8);
            Assert.Contains("$script:CompactOutputArtifactRepairTemplatePath", daemon, StringComparison.Ordinal);
            Assert.Contains("accepted_turn_output_artifact_repair", daemon, StringComparison.Ordinal);
            Assert.Contains("You MUST read '$($script:CompactOutputArtifactRepairTemplatePath)'", daemon, StringComparison.Ordinal);
            Assert.Contains("narrative_response_technical_repair_leak", daemon, StringComparison.Ordinal);
            Assert.Contains("Test-GmValidationRepairArtifactWritingStall", daemon, StringComparison.Ordinal);
            Assert.Contains("gm_validation_repair_artifact_stall", daemon, StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonSource_ValidationRepairWatchClearsWhenRequestIsGoneOrChanged()
    {
        var daemon = File.ReadAllText(Path.Combine(LocateRepoRoot(), "BookOfEternityClient", "game_master_daemon.ps1"), Encoding.UTF8);

        Assert.Contains("Test-GmValidationRepairWatchStillCurrent", daemon, StringComparison.Ordinal);
        Assert.Contains("validation repair request disappeared or changed; clearing artifact watch", daemon, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Test-Path $RepairRequestFile", daemon, StringComparison.Ordinal);
        Assert.Contains("requestId", daemon, StringComparison.Ordinal);
        Assert.Contains("revalidationAttempt", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonContextPack_AfterlifeConflictRewardLessonsNameRewardRepair()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-afterlife-reward-lessons-" + Guid.NewGuid().ToString("N"));
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
                {"recordId":"gmtraj_afterlife_reward_not_allowed","kind":"repair","sessionId":"prior","turnId":"repair-afterlife-reward","requestId":"repair-afterlife-reward","turnNumber":9,"realm":"ChaosSea","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"accepted","issueKinds":["afterlife_conflict_reward_not_allowed"],"diagnostics":[{"code":"afterlife_conflict_reward_not_allowed","category":"StateConsistency","path":"game_state/meta/afterlife_spiritual_conflict_state.json.recentConflicts[0].rewardAudit","message":"Этот terminal afterlife conflict outcome не может выдавать currency reward.","expected":"resolved contested player victory with diceAudit.outcomeBand=player_success|decisive_player_success","actual":"terminalOutcome=negotiated_training"}],"repairPacketRefs":["afterlife_spiritual_conflict_reward_repair"]},"repair":{"attempts":1,"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-07-02T11:12:00Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-repair",
                  "turnNumber": 10,
                  "currentRealm": "Chaos Sea",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "afterlife_conflict_reward_not_allowed",
                      "category": "StateConsistency",
                      "path": "game_state/meta/afterlife_spiritual_conflict_state.json.recentConflicts[0].rewardAudit",
                      "message": "Этот terminal afterlife conflict outcome не может выдавать currency reward.",
                      "expected": "resolved contested player victory with diceAudit.outcomeBand=player_success|decisive_player_success",
                      "actual": "terminalOutcome=negotiated_training"
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "afterlife_conflict_reward_not_allowed", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var markdown = File.ReadAllText(lessonsMarkdownPath, Encoding.UTF8);
            Assert.Contains("afterlife_spiritual_conflict_reward_repair", markdown, StringComparison.Ordinal);
            Assert.Contains("rewardAudit", markdown, StringComparison.Ordinal);
            Assert.Contains("player_success", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("negotiated", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("remove", markdown, StringComparison.OrdinalIgnoreCase);

            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var lesson = document.RootElement.GetProperty("lessons")[0];
            Assert.Equal("VALIDATION_REPAIR_TEMPLATE.md", lesson.GetProperty("preferredHarnessSurface").GetString());
            Assert.Contains("afterlife_spiritual_conflict_reward_repair", lesson.GetProperty("acceptedFix").GetString(), StringComparison.Ordinal);
            Assert.Contains("remove rewardAudit", lesson.GetProperty("acceptedFix").GetString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_AfterlifeEntityProfileScaffoldLessonsNameProfileRepair()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-afterlife-profile-scaffold-lessons-" + Guid.NewGuid().ToString("N"));
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
                {"recordId":"gmtraj_afterlife_profile_scaffold","kind":"repair","sessionId":"prior","turnId":"repair-afterlife-profile","requestId":"repair-afterlife-profile","turnNumber":9,"realm":"ChaosSea","mode":"validation_repair","contextPackPath":"game_state/control/gm_context_pack","templateVersions":{"turnOutput":"v1","validationRepair":"v1"},"dispatch":{"attempts":1,"busyRetries":0,"timeout":false,"status":"clipboard"},"validation":{"status":"accepted","issueKinds":["afterlife_entity_profile_agency_goals_not_object","afterlife_entity_profile_missing_progression_strategy","afterlife_entity_profile_missing_ledger","incomplete_special_art_learning_receipt"],"diagnostics":[{"code":"afterlife_entity_profile_agency_goals_not_object","category":"StateConsistency","path":"game_state/meta/afterlife_entity_profiles.json.profiles[0].goals","message":"goals профиля духовной сущности должен быть object.","expected":"object","actual":"Array"}],"repairPacketRefs":["afterlife_entity_profile_scaffold_repair"]},"repair":{"attempts":1,"status":"accepted"},"workerEvents":[],"rollbackEvents":[],"rubric":{"validTurn":true,"playerFacingOutputPresent":true,"implementationSourceRead":false,"rawWrongRealmWrite":false,"manualReasoningNeeded":false,"missingHarnessTool":null},"createdAt":"2026-07-02T11:22:00Z"}
                """.Trim() + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(control, "validation_repair_request.json"),
                """
                {
                  "sessionId": "current-session",
                  "requestId": "current-repair",
                  "turnNumber": 10,
                  "currentRealm": "Chaos Sea",
                  "revalidationAttempt": 1,
                  "errors": [
                    {
                      "code": "afterlife_entity_profile_missing_progression_strategy",
                      "category": "StateConsistency",
                      "path": "game_state/meta/afterlife_entity_profiles.json.profiles[0].progressionStrategy",
                      "message": "Профиль должен явно хранить progressionStrategy.",
                      "expected": "object",
                      "actual": "missing"
                    }
                  ]
                }
                """,
                Encoding.UTF8);

            process = StartDaemon(session, logPath);
            var lessonsMarkdownPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.md");
            Assert.True(WaitForFileContaining(lessonsMarkdownPath, "afterlife_entity_profile_scaffold_repair", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var markdown = File.ReadAllText(lessonsMarkdownPath, Encoding.UTF8);
            Assert.Contains("goals", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("progressionStrategy", markdown, StringComparison.Ordinal);
            Assert.Contains("ledger", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("specialArtLearningReceipts", markdown, StringComparison.Ordinal);
            Assert.Contains("initialTier", markdown, StringComparison.Ordinal);

            var lessonsJsonPath = Path.Combine(control, "gm_context_pack", "Lessons", "GM_EXPERIENCE_LESSONS.json");
            using var document = JsonDocument.Parse(File.ReadAllText(lessonsJsonPath, Encoding.UTF8));
            var lesson = document.RootElement.GetProperty("lessons")[0];
            Assert.Equal("VALIDATION_REPAIR_TEMPLATE.md", lesson.GetProperty("preferredHarnessSurface").GetString());
            Assert.Contains("minimum profile scaffold", lesson.GetProperty("acceptedFix").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("specialArtLearningReceipts", lesson.GetProperty("acceptedFix").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_RendersMortalLocationTransitionTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-mortal-location-template-" + Guid.NewGuid().ToString("N"));
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
                "MORTAL_LOCATION_TRANSITION_TEMPLATE.md");
            Assert.True(WaitForFileContaining(templatePath, "Mortal location transition repair", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var template = File.ReadAllText(templatePath, Encoding.UTF8);
            Assert.Contains("game_state/world/world_map.json", template, StringComparison.Ordinal);
            Assert.Contains("game_state/world/current_location.json", template, StringComparison.Ordinal);
            Assert.Contains("known ids", template, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("duplicate coordinates", template, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("narrative color", template, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("canonical `biome`", template, StringComparison.Ordinal);
            Assert.Contains("TemperateForest", template, StringComparison.Ordinal);
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
            Assert.Contains("known current location", template, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("same-turn new location", template, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"currentLocationId\": \"<currentLocationData.locationId for known current location>\"", template, StringComparison.Ordinal);
            Assert.Contains("currentLocationId` to `null", template, StringComparison.Ordinal);
            Assert.Contains("\"attitude\": \"Neutral\"", template, StringComparison.Ordinal);
            Assert.Contains("Conformist", template, StringComparison.Ordinal);
            Assert.Contains("Pragmatist", template, StringComparison.Ordinal);
            Assert.Contains("Dissident", template, StringComparison.Ordinal);
            Assert.Contains("relationshipLock", template, StringComparison.Ordinal);
            Assert.Contains("Actors outside scope", template, StringComparison.Ordinal);
            Assert.Contains("Role-identifiable visible, speaking, acting, clue-giving, or directly addressed scene actors are NPC candidates even when their personal name is unknown", template, StringComparison.Ordinal);
            Assert.Contains("Use a stable role-based visible name", template, StringComparison.Ordinal);

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
    public void DaemonContextPack_RendersMortalFactionUpdateTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-mortal-faction-template-" + Guid.NewGuid().ToString("N"));
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
                "MORTAL_FACTION_UPDATE_TEMPLATE.md");
            Assert.True(WaitForFileContaining(templatePath, "Mortal faction identity repair", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var template = File.ReadAllText(templatePath, Encoding.UTF8);
            Assert.Contains("game_state/factions/faction_core.json", template, StringComparison.Ordinal);
            Assert.Contains("factions[]", template, StringComparison.Ordinal);
            Assert.Contains("factionId", template, StringComparison.Ordinal);
            Assert.Contains("sidecar", template, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("create the missing faction", template, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_RendersMortalExperienceLevelTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-mortal-experience-template-" + Guid.NewGuid().ToString("N"));
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
                "MORTAL_EXPERIENCE_LEVEL_TEMPLATE.md");
            Assert.True(WaitForFileContaining(templatePath, "Mortal experience and level template", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var template = File.ReadAllText(templatePath, Encoding.UTF8);
            Assert.Contains("game_state/player/experience.json", template, StringComparison.Ordinal);
            Assert.Contains("experienceGained", template, StringComparison.Ordinal);
            Assert.Contains("currentExperience", template, StringComparison.Ordinal);
            Assert.Contains("experienceForNextLevel", template, StringComparison.Ordinal);
            Assert.Contains("playerLevel", template, StringComparison.Ordinal);
            Assert.Contains("stat points", template, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("level-up", template, StringComparison.OrdinalIgnoreCase);

            var readmePath = Path.Combine(session, "game_state", "control", "gm_context_pack", "README.md");
            Assert.True(WaitForFileContaining(readmePath, "Mortal World experience and level progression", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));
            var readme = File.ReadAllText(readmePath, Encoding.UTF8);
            Assert.Contains("Mortal World experience and level progression", readme, StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_RendersMortalCombatStateTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-mortal-combat-template-" + Guid.NewGuid().ToString("N"));
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
                "MORTAL_COMBAT_STATE_TEMPLATE.md");
            Assert.True(WaitForFileContaining(templatePath, "Mortal combat state template", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var template = File.ReadAllText(templatePath, Encoding.UTF8);
            Assert.Contains("game_state/combat/enemies.json", template, StringComparison.Ordinal);
            Assert.Contains("game_state/combat/allies.json", template, StringComparison.Ordinal);
            Assert.Contains("game_state/combat/combat_log.json", template, StringComparison.Ordinal);
            Assert.Contains("/бой", template, StringComparison.Ordinal);
            Assert.Contains("skillMasteryChanges", template, StringComparison.Ordinal);
            Assert.Contains("experience.json", template, StringComparison.Ordinal);

            var readmePath = Path.Combine(session, "game_state", "control", "gm_context_pack", "README.md");
            Assert.True(WaitForFileContaining(readmePath, "Mortal World combat state", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));
            var readme = File.ReadAllText(readmePath, Encoding.UTF8);
            Assert.Contains("Mortal World combat state", readme, StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(process);
            try { Directory.Delete(root, recursive: true); } catch { /* ignored */ }
        }
    }

    [Fact]
    public void DaemonContextPack_RendersMortalSkillProgressionTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-mortal-skill-template-" + Guid.NewGuid().ToString("N"));
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
                "MORTAL_SKILL_PROGRESSION_TEMPLATE.md");
            Assert.True(WaitForFileContaining(templatePath, "Mortal skill progression", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));

            var template = File.ReadAllText(templatePath, Encoding.UTF8);
            Assert.Contains("passiveSkillChanges", template, StringComparison.Ordinal);
            Assert.Contains("activeSkillChanges", template, StringComparison.Ordinal);
            Assert.Contains("skillMasteryChanges", template, StringComparison.Ordinal);
            Assert.Contains("attribute-only check", template, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("prose-only learning", template, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("starter passive skills", template, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fresh Mortal bootstrap", template, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Чтение свидетельских меток", template, StringComparison.Ordinal);
            Assert.DoesNotContain("Р§С‚РµРЅРёРµ", template, StringComparison.Ordinal);

            var readmePath = Path.Combine(session, "game_state", "control", "gm_context_pack", "README.md");
            Assert.True(WaitForFileContaining(readmePath, "Mortal World skill progression", process, TimeSpan.FromSeconds(20)), ReadProcessOutput(process));
            var readme = File.ReadAllText(readmePath, Encoding.UTF8);
            Assert.Contains("Mortal World skill progression", readme, StringComparison.Ordinal);
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
        Assert.Contains("'$($script:CompactMortalCombatTemplatePath)'", turnBlock, StringComparison.Ordinal);
        Assert.Contains("'$($script:CompactTempoAdvantageTemplatePath)'", turnBlock, StringComparison.Ordinal);
        Assert.Contains("before opening large copied examples", turnBlock, StringComparison.Ordinal);

        Assert.Contains("'$($script:CompactValidationRepairTemplatePath)'", repairBlock, StringComparison.Ordinal);
        Assert.Contains("'$($script:CompactActorReasoningTemplatePath)'", repairBlock, StringComparison.Ordinal);
        Assert.Contains("'$($script:CompactMortalNpcTemplatePath)'", repairBlock, StringComparison.Ordinal);
        Assert.Contains("'$($script:CompactMortalCombatTemplatePath)'", repairBlock, StringComparison.Ordinal);
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
    public void DaemonRepairPrompt_ExplainsMortalSkillProgressionShapePacket()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("mortal_skill_progression_shape_repair", daemon, StringComparison.Ordinal);
        Assert.Contains("activeSkillChanges", daemon, StringComparison.Ordinal);
        Assert.Contains("passiveSkillChanges", daemon, StringComparison.Ordinal);
        Assert.Contains("skillMasteryChanges", daemon, StringComparison.Ordinal);
        Assert.Contains("pending_training_showcase_requests.json", daemon, StringComparison.Ordinal);
        Assert.Contains("do not charge", daemon, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DaemonContextPack_CopiesTrainingShowcaseExample()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var contextPackBlock = ExtractFunctionBlock(daemon, "function Write-GmContextPack {");

        Assert.Contains(@"Examples\E_CLI_Training_Showcases.txt", contextPackBlock, StringComparison.Ordinal);
        Assert.Contains("training_showcase_example", contextPackBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonTerminalObservation_DoesNotDeleteClientOwnedTurnRequest()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("$script:ObservedTerminalRequestKeys", daemon, StringComparison.Ordinal);
        Assert.Contains("$ObservedTerminalRequestKeysFile", daemon, StringComparison.Ordinal);
        Assert.Contains("gm_observed_terminal_requests.json", daemon, StringComparison.Ordinal);
        Assert.Contains("function Load-ObservedTerminalRequestKeys", daemon, StringComparison.Ordinal);
        Assert.Contains("function Save-ObservedTerminalRequestKeys", daemon, StringComparison.Ordinal);
        Assert.Contains("Add-ObservedTerminalRequestKey", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item $RequestPath", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item -Path $RequestPath", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain("_fs.DeleteFile(\"input/turn_request.json\")", daemon, StringComparison.Ordinal);

        var addFunction = ExtractFunctionBlock(daemon, "function Add-ObservedTerminalRequestKey");
        Assert.Contains("Save-ObservedTerminalRequestKeys", addFunction, StringComparison.Ordinal);

        var loadIndex = daemon.IndexOf("Load-ObservedTerminalRequestKeys", StringComparison.Ordinal);
        var bannerIndex = daemon.IndexOf("# Banner", StringComparison.Ordinal);
        Assert.True(loadIndex >= 0 && bannerIndex > loadIndex, "Daemon must load persisted observed request keys before watcher startup.");
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
    public void DaemonTurnDispatch_ValidatesDetachedPendingSnapshotAuthorityBeforeDispatch()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("function Test-PendingTurnSnapshotAuthorityEnvelope", daemon, StringComparison.Ordinal);
        Assert.Contains("payloadJsonBase64", daemon, StringComparison.Ordinal);
        Assert.Contains("payloadSha256", daemon, StringComparison.Ordinal);
        Assert.Contains("SHA256", daemon, StringComparison.Ordinal);

        var contextGuard = ExtractFunctionBlock(daemon, "function Test-TurnRequestHasPendingSnapshotContext");
        Assert.Contains("Test-PendingTurnSnapshotAuthorityEnvelope", contextGuard, StringComparison.Ordinal);
        Assert.Contains("$manifest", contextGuard, StringComparison.Ordinal);
        Assert.Contains("$TurnRequest", contextGuard, StringComparison.Ordinal);
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
    public void DaemonTurnWait_ClassifiesPendingSnapshotAuthorityTerminalErrorsAsHarnessFriction()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("function Get-MissingHarnessToolFromTerminalError", daemon, StringComparison.Ordinal);
        Assert.Contains("pending_turn_snapshot_authority_recovery_gap", daemon, StringComparison.Ordinal);
        Assert.Contains("Pending turn snapshot authority", daemon, StringComparison.Ordinal);

        var errorBranchIndex = daemon.IndexOf("elseif ($null -ne $terminalSignal -and $terminalSignal.Kind -eq \"error\")", StringComparison.Ordinal);
        var classifyIndex = daemon.IndexOf("Get-MissingHarnessToolFromTerminalError -TerminalSignal $terminalSignal", errorBranchIndex, StringComparison.Ordinal);
        var recordIndex = daemon.IndexOf("Write-GmTrajectoryRecord", errorBranchIndex, StringComparison.Ordinal);

        Assert.True(errorBranchIndex >= 0, "Expected terminal error branch.");
        Assert.True(classifyIndex > errorBranchIndex, "Expected terminal error branch to classify harness friction.");
        Assert.True(recordIndex > classifyIndex, "Expected harness friction classification before ledger write.");
    }

    [Fact]
    public void DaemonTurnWait_EmitsTerminalErrorWhenBridgeReturnsIdleWithoutTerminalSignal()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("function Test-GmBridgeReturnedIdleWithoutTerminalSignal", daemon, StringComparison.Ordinal);
        Assert.Contains("gm_bridge_idle_without_terminal_signal", daemon, StringComparison.Ordinal);
        Assert.Contains("GM bridge returned to idle without a correlated terminal signal", daemon, StringComparison.Ordinal);
        Assert.Contains("Write tests for @filename", daemon, StringComparison.Ordinal);
        Assert.Contains(".IndexOf(\"› Implement {feature}\", [System.StringComparison]::Ordinal) -ge 0", daemon, StringComparison.Ordinal);
        Assert.DoesNotContain(".Contains(\"› Implement {feature}\", [System.StringComparison]::Ordinal)", daemon, StringComparison.Ordinal);

        var waitLoopIndex = daemon.IndexOf("while ($null -eq $terminalSignal", StringComparison.Ordinal);
        var idleProbeIndex = daemon.IndexOf("Test-GmBridgeReturnedIdleWithoutTerminalSignal", waitLoopIndex, StringComparison.Ordinal);
        var timeoutIndex = daemon.IndexOf("Timeout after ${elapsed}s", waitLoopIndex, StringComparison.Ordinal);
        Assert.True(waitLoopIndex >= 0, "Expected a terminal wait loop.");
        Assert.True(idleProbeIndex > waitLoopIndex, "Expected idle-without-terminal detection inside the wait loop.");
        Assert.True(timeoutIndex > idleProbeIndex, "Expected idle-without-terminal detection before full timeout fallback.");
    }

    [Fact]
    public void DaemonTurnWait_DoesNotTreatCodexWorkingScreenAsIdle()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("function Test-GmBridgeDiagnosticsIndicatesActiveCodexWork", daemon, StringComparison.Ordinal);

        var functionBlock = ExtractFunctionBlock(daemon, "function Test-GmBridgeReturnedIdleWithoutTerminalSignal");
        var activeWorkGuardIndex = functionBlock.IndexOf("Test-GmBridgeDiagnosticsIndicatesActiveCodexWork -Text $visibleScreenText", StringComparison.Ordinal);
        var idlePromptIndex = functionBlock.IndexOf("$hasCodexIdlePrompt", StringComparison.Ordinal);
        var returnIndex = functionBlock.IndexOf("return ($ready -and $hasCodexIdlePrompt)", StringComparison.Ordinal);

        Assert.True(activeWorkGuardIndex >= 0, "Expected idle-terminal detection to inspect the visible Codex working state.");
        Assert.True(idlePromptIndex > activeWorkGuardIndex, "Expected active-work guard before idle-prompt classification.");
        Assert.True(returnIndex > activeWorkGuardIndex, "Expected active-work guard before the idle terminal shortcut can return true.");
    }

    [Fact]
    public void DaemonTurnWait_EmitsEarlyTerminalErrorWhenBridgeStallsWhilePreparingArtifacts()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("function Test-GmBridgeArtifactWritingStall", daemon, StringComparison.Ordinal);
        Assert.Contains("gm_bridge_artifact_write_stall", daemon, StringComparison.Ordinal);
        Assert.Contains("gm_artifact_write_stall_report.json", daemon, StringComparison.Ordinal);
        Assert.Contains("GM bridge appears stalled while preparing turn artifacts", daemon, StringComparison.Ordinal);
        Assert.Contains("Complete-BoeTurn", daemon, StringComparison.Ordinal);
        Assert.Contains("outputVersion", daemon, StringComparison.Ordinal);
        Assert.Contains("recentOutputTail", daemon, StringComparison.Ordinal);
        Assert.Contains("visibleScreenText", daemon, StringComparison.Ordinal);

        var waitLoopIndex = daemon.IndexOf("while ($null -eq $terminalSignal", StringComparison.Ordinal);
        var idleProbeIndex = daemon.IndexOf("Test-GmBridgeReturnedIdleWithoutTerminalSignal", waitLoopIndex, StringComparison.Ordinal);
        var artifactProbeIndex = daemon.IndexOf("Test-GmBridgeArtifactWritingStall", waitLoopIndex, StringComparison.Ordinal);
        var timeoutIndex = daemon.IndexOf("Timeout after ${elapsed}s", waitLoopIndex, StringComparison.Ordinal);
        Assert.True(waitLoopIndex >= 0, "Expected a terminal wait loop.");
        Assert.True(artifactProbeIndex > waitLoopIndex, "Expected artifact-writing stall detection inside the wait loop.");
        Assert.True(timeoutIndex > artifactProbeIndex, "Expected artifact-writing stall detection before full timeout fallback.");
        Assert.True(artifactProbeIndex > idleProbeIndex, "Expected artifact-writing stall detection to run after the cheaper idle probe.");

        var artifactBranch = daemon[artifactProbeIndex..timeoutIndex];
        Assert.Contains("Stop-GmBridgeAfterTurnTimeout -TurnRequest $turnRequest -ElapsedSeconds $elapsed -Reason \"gm_bridge_artifact_write_stall\"", artifactBranch, StringComparison.Ordinal);
        Assert.Contains("artifactWriteStall = $artifactStall", artifactBranch, StringComparison.Ordinal);
        Assert.Contains("Set-Content -Path $errorPath", artifactBranch, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonTurnWait_EmitsEarlyTerminalErrorWhenGmWritesPayloadWithoutTerminalSignal()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("function Test-GmOutputWithoutTerminalSignal", daemon, StringComparison.Ordinal);
        Assert.Contains("gm_output_without_terminal_signal", daemon, StringComparison.Ordinal);
        Assert.Contains("GM wrote turn payload files without a correlated terminal signal", daemon, StringComparison.Ordinal);
        Assert.Contains("changedFiles = $payloadStall.changedFiles", daemon, StringComparison.Ordinal);
        Assert.Contains("gm_output_without_terminal_report.json", daemon, StringComparison.Ordinal);

        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides/CLI_Step_Main.txt");
        var mainExample = ReadRepoFile("Examples/E_CLI_Step_Main.txt");
        foreach (var gmFacingText in new[] { daemonSpec, taskGuide, mainExample })
        {
            Assert.Contains("gm_output_without_terminal_signal", gmFacingText, StringComparison.Ordinal);
            Assert.Contains("Complete-BoeTurn", gmFacingText, StringComparison.Ordinal);
        }

        var waitLoopIndex = daemon.IndexOf("while ($null -eq $terminalSignal", StringComparison.Ordinal);
        var idleProbeIndex = daemon.IndexOf("Test-GmBridgeReturnedIdleWithoutTerminalSignal", waitLoopIndex, StringComparison.Ordinal);
        var payloadProbeIndex = daemon.IndexOf("Test-GmOutputWithoutTerminalSignal", waitLoopIndex, StringComparison.Ordinal);
        var artifactProbeIndex = daemon.IndexOf("Test-GmBridgeArtifactWritingStall", waitLoopIndex, StringComparison.Ordinal);
        var timeoutIndex = daemon.IndexOf("Timeout after ${elapsed}s", waitLoopIndex, StringComparison.Ordinal);

        Assert.True(waitLoopIndex >= 0, "Expected a terminal wait loop.");
        Assert.True(payloadProbeIndex > waitLoopIndex, "Expected output-without-terminal detection inside the wait loop.");
        Assert.True(payloadProbeIndex > idleProbeIndex, "Expected output-without-terminal detection after the cheaper idle probe.");
        Assert.True(artifactProbeIndex > payloadProbeIndex, "Expected artifact-writing stall detection after concrete payload detection.");
        Assert.True(timeoutIndex > payloadProbeIndex, "Expected output-without-terminal detection before full timeout fallback.");
    }

    [Fact]
    public void DaemonTurnTimeout_ShutsDownBridgeBeforeWritingTimeoutTerminalError()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("function Stop-GmBridgeAfterTurnTimeout", daemon, StringComparison.Ordinal);
        Assert.Contains("shutdown-bridge", daemon, StringComparison.Ordinal);
        Assert.Contains("gm_timeout_bridge_cleanup.json", daemon, StringComparison.Ordinal);
        Assert.Contains("timeoutBridgeCleanup = $timeoutBridgeCleanup", daemon, StringComparison.Ordinal);

        var waitLoopIndex = daemon.IndexOf("while ($null -eq $terminalSignal", StringComparison.Ordinal);
        var timeoutBranchIndex = daemon.IndexOf("if ($TurnTimeout -gt 0 -and $elapsed -ge $TurnTimeout -and $null -eq $terminalSignal)", waitLoopIndex, StringComparison.Ordinal);
        var cleanupIndex = daemon.IndexOf("$timeoutBridgeCleanup = Stop-GmBridgeAfterTurnTimeout", timeoutBranchIndex, StringComparison.Ordinal);
        var setErrorIndex = daemon.IndexOf("Set-Content -Path $errorPath", timeoutBranchIndex, StringComparison.Ordinal);

        Assert.True(waitLoopIndex >= 0, "Expected a terminal wait loop.");
        Assert.True(timeoutBranchIndex > waitLoopIndex, "Expected full turn timeout branch after the wait loop.");
        Assert.True(cleanupIndex > timeoutBranchIndex, "Expected timeout branch to isolate the GM bridge.");
        Assert.True(setErrorIndex > cleanupIndex, "Daemon must stop/quarantine the stale bridge before publishing turn_error.json.");
    }

    [Fact]
    public void DaemonStatus_PublishesConfiguredTurnTimeoutForClientTerminalWait()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        var statusFunction = ExtractFunctionBlock(daemon, "function Write-DaemonStatus");

        Assert.Contains("turnTimeoutSeconds = $TurnTimeout", statusFunction, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonTurnDispatch_EmitsTerminalErrorWhenBridgePipeNeverAcceptsDispatch()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("$script:BridgeDispatchMaxWaitSeconds", daemon, StringComparison.Ordinal);
        Assert.Contains("[int]$MaxWaitSeconds = 0", daemon, StringComparison.Ordinal);
        Assert.Contains("bridge-dispatch-timeout", daemon, StringComparison.Ordinal);
        Assert.Contains("gm_bridge_dispatch_unavailable", daemon, StringComparison.Ordinal);
        Assert.Contains("GM bridge did not accept dispatch before the dispatch timeout.", daemon, StringComparison.Ordinal);

        var dispatchFunction = ExtractFunctionBlock(daemon, "function Dispatch-WithRetry");
        Assert.Contains("New-GmDispatchDiagnostics -Status \"bridge-dispatch-timeout\"", dispatchFunction, StringComparison.Ordinal);
        Assert.Contains("-Timeout $true", dispatchFunction, StringComparison.Ordinal);

        var dispatchIndex = daemon.IndexOf("$dispatchDiagnostics = Dispatch-WithRetry -Message $message", StringComparison.Ordinal);
        var timeoutBranchIndex = daemon.IndexOf("$dispatchDiagnostics.Status -eq \"bridge-dispatch-timeout\"", dispatchIndex, StringComparison.Ordinal);
        var waitLoopIndex = daemon.IndexOf("while ($null -eq $terminalSignal", dispatchIndex, StringComparison.Ordinal);
        Assert.True(dispatchIndex >= 0, "Expected turn dispatch call.");
        Assert.True(timeoutBranchIndex > dispatchIndex, "Expected bridge-dispatch timeout handling after dispatch returns.");
        Assert.True(waitLoopIndex > timeoutBranchIndex, "Expected bridge-dispatch timeout handling before terminal wait loop.");

        var timeoutBranch = daemon[timeoutBranchIndex..waitLoopIndex];
        Assert.Contains("Add-ObservedTerminalRequestKey -Key $turnRequestKey", timeoutBranch, StringComparison.Ordinal);
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
    public void DaemonStatus_RecoversAfterSessionResetAndTurnCompletion()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var heartbeatBlock = ExtractFunctionBlock(daemon, "function Update-DaemonHeartbeat");
        var turnBlock = ExtractFunctionBlock(daemon, "function Process-Turn");

        Assert.Contains("Test-Path $DaemonStatusFile", heartbeatBlock, StringComparison.Ordinal);
        Assert.Contains("Write-DaemonStatus -Status \"running\" -Reason \"status_file_missing\"", heartbeatBlock, StringComparison.Ordinal);

        var finallyIndex = turnBlock.IndexOf("finally {", StringComparison.Ordinal);
        var statusIndex = turnBlock.IndexOf("Write-DaemonStatus -Status \"running\" -Reason \"turn_processing_finished\"", StringComparison.Ordinal);
        var releaseIndex = turnBlock.IndexOf("$script:IsProcessing = $false", finallyIndex, StringComparison.Ordinal);

        Assert.True(finallyIndex >= 0, "Expected Process-Turn finally block.");
        Assert.True(statusIndex > finallyIndex, "Expected Process-Turn to republish daemon status in finally.");
        Assert.True(releaseIndex > statusIndex, "Expected status publication before Process-Turn releases processing state.");
    }

    [Fact]
    public void DaemonStatus_RefreshesWhileWaitingForGmTurnResponse()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var processingHeartbeatBlock = ExtractFunctionBlock(daemon, "function Update-DaemonProcessingHeartbeat");
        var turnBlock = ExtractFunctionBlock(daemon, "function Process-Turn");

        Assert.Contains("Write-DaemonStatus -Status \"processing\" -Reason \"turn_processing_waiting\"", processingHeartbeatBlock, StringComparison.Ordinal);
        Assert.Contains("-CurrentTurnNumber $TurnNumber", processingHeartbeatBlock, StringComparison.Ordinal);
        Assert.Contains("-TurnElapsedSeconds $ElapsedSeconds", processingHeartbeatBlock, StringComparison.Ordinal);

        var waitLogIndex = turnBlock.IndexOf("Write-Log \"  Waiting... (${elapsed}s)\"", StringComparison.Ordinal);
        var heartbeatIndex = turnBlock.IndexOf("Update-DaemonProcessingHeartbeat -TurnNumber $turnNumber -ElapsedSeconds $elapsed", StringComparison.Ordinal);

        Assert.True(waitLogIndex >= 0, "Expected Process-Turn to log long GM waits.");
        Assert.True(heartbeatIndex > waitLogIndex, "Expected Process-Turn to refresh daemon status whenever it reports a long GM wait.");
    }

    [Fact]
    public void DaemonTurnCounter_IncrementsOnlyForAcceptedTurnProcessing()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var turnBlock = ExtractFunctionBlock(daemon, "function Process-Turn");

        var counterIndex = turnBlock.IndexOf("$script:TurnCount++", StringComparison.Ordinal);
        var duplicateGuardIndex = turnBlock.IndexOf("if (Test-ObservedTerminalRequestKey -Key $turnRequestKey)", StringComparison.Ordinal);
        var staleSnapshotGuardIndex = turnBlock.IndexOf("if (-not (Test-TurnRequestHasPendingSnapshotContext -TurnRequest $turnRequest))", StringComparison.Ordinal);
        var turnLogIndex = turnBlock.IndexOf("Write-Log \"Turn #${turnNumber}:", StringComparison.Ordinal);

        Assert.True(counterIndex >= 0, "Expected Process-Turn to increment the daemon turn counter.");
        Assert.True(duplicateGuardIndex >= 0, "Expected Process-Turn to ignore already observed terminal request keys.");
        Assert.True(staleSnapshotGuardIndex > duplicateGuardIndex, "Expected stale snapshot guard after duplicate request guard.");
        Assert.True(
            counterIndex > staleSnapshotGuardIndex,
            "Daemon turn counter must not count duplicate or stale watcher hits as processed GM turns.");
        Assert.True(
            counterIndex < turnLogIndex,
            "Daemon turn counter should increment immediately before the accepted turn is logged and processed.");
    }

    [Fact]
    public void DaemonLifecycle_PreservesWatcherAndReportsFatalDiagnostics()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("gm_daemon_fatal_error.json", daemon, StringComparison.Ordinal);
        Assert.Contains("function New-DaemonErrorPayload", daemon, StringComparison.Ordinal);
        Assert.Contains("function Write-DaemonJsonFileBestEffort", daemon, StringComparison.Ordinal);
        Assert.Contains("function Write-DaemonFatalReport", daemon, StringComparison.Ordinal);
        Assert.Contains("fatalError", daemon, StringComparison.Ordinal);
        Assert.Contains("lastLoopError", daemon, StringComparison.Ordinal);
        Assert.Contains("Main loop error recovered", daemon, StringComparison.Ordinal);
        Assert.Contains("main_loop_error_recovered", daemon, StringComparison.Ordinal);
        Assert.Contains("Fatal daemon error", daemon, StringComparison.Ordinal);
        Assert.Contains("Write-DaemonStatus -Status \"failed\" -FatalError $script:DaemonFatalError", daemon, StringComparison.Ordinal);

        var mainLoopIndex = daemon.IndexOf("while ($true)", StringComparison.Ordinal);
        var recoveredErrorIndex = daemon.IndexOf("Main loop error recovered", StringComparison.Ordinal);
        var fatalErrorIndex = daemon.IndexOf("Fatal daemon error", StringComparison.Ordinal);
        Assert.True(mainLoopIndex >= 0, "Expected daemon main loop.");
        Assert.True(recoveredErrorIndex > mainLoopIndex, "Expected recoverable loop error handling inside or after the daemon loop.");
        Assert.True(fatalErrorIndex > recoveredErrorIndex, "Expected fatal diagnostics outside the recoverable polling loop.");
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

    [Fact]
    public void LauncherConfigDefaults_UseForceWhenAddingMissingProperties()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var launcher = ReadRepoFile("BookOfEternityClient/Launcher/bookofeternity.ps1");

        Assert.Contains("Add-Member -NotePropertyName $key -NotePropertyValue $defaults[$key] -Force", daemon, StringComparison.Ordinal);
        Assert.Contains("Add-Member -NotePropertyName $key -NotePropertyValue $defaults[$key] -Force", launcher, StringComparison.Ordinal);
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
            if (TryReadFileContaining(path, expectedText))
            {
                return true;
            }

            if (process is { HasExited: true })
            {
                return TryReadFileContaining(path, expectedText);
            }

            Thread.Sleep(100);
        }

        return false;
    }

    private static bool TryReadFileContaining(string path, string expectedText)
    {
        try
        {
            return File.Exists(path) &&
                File.ReadAllText(path, Encoding.UTF8).Contains(expectedText, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
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

    private static void WriteDaemonPendingTurnSnapshot(
        string session,
        string sessionId,
        string requestId,
        int turnNumber)
    {
        var control = Path.Combine(session, "game_state", "control");
        Directory.CreateDirectory(control);

        var manifest = new DaemonPendingTurnSnapshotManifest
        {
            SessionId = sessionId,
            RequestId = requestId,
            TurnNumber = turnNumber
        };

        manifest.ManifestPayloadHash = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
            manifest,
            PendingTurnSnapshotManifestJsonOptions,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash);

        File.WriteAllText(
            Path.Combine(control, "pending_turn_snapshot.json"),
            JsonSerializer.Serialize(manifest, PendingTurnSnapshotManifestJsonOptions),
            Encoding.UTF8);

        var authorityJson = PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(
            manifest,
            PendingTurnSnapshotManifestJsonOptions,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash,
            static snapshotManifest => snapshotManifest.SessionId,
            static snapshotManifest => snapshotManifest.RequestId,
            static snapshotManifest => snapshotManifest.TurnNumber,
            static snapshotManifest => snapshotManifest.Files,
            static snapshotManifest => snapshotManifest.SnapshotFileHashes,
            static snapshotManifest => snapshotManifest.ClientOwnedValidationHashes,
            static snapshotManifest => snapshotManifest.RollbackBaselineFiles,
            static snapshotManifest => snapshotManifest.SourceLabel,
            static snapshotManifest => snapshotManifest.RollbackBackups,
            relativePath => ReadSessionRelativeFile(session, relativePath));

        File.WriteAllText(
            Path.Combine(control, "pending_turn_snapshot.authority.json"),
            authorityJson,
            Encoding.UTF8);
    }

    private static string? ReadSessionRelativeFile(string session, string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = Path.Combine(session, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(fullPath) ? File.ReadAllText(fullPath, Encoding.UTF8) : null;
    }

    private static readonly JsonSerializerOptions PendingTurnSnapshotManifestJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private sealed class DaemonPendingTurnSnapshotManifest
    {
        public string SessionId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public int TurnNumber { get; set; }
        public string ManifestPayloadHash { get; set; } = string.Empty;
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string SourceLabel { get; set; } = "daemon-test";
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

    private static string ExtractHereStringBodyByNeedle(string text, string bodyNeedle)
    {
        var start = text.IndexOf(bodyNeedle, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected here-string body containing {bodyNeedle}.");

        var end = text.IndexOf("\n'@", start, StringComparison.Ordinal);
        Assert.True(end > start, $"Expected here-string terminator after {bodyNeedle}.");
        return text[start..end];
    }

    private static string LocateRepoRoot()
    {
        return TestRepoPaths.RepoRoot;
    }
}
