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
        Assert.Contains("'$($script:CompactTempoAdvantageTemplatePath)'", turnBlock, StringComparison.Ordinal);
        Assert.Contains("before opening large copied examples", turnBlock, StringComparison.Ordinal);

        Assert.Contains("'$($script:CompactValidationRepairTemplatePath)'", repairBlock, StringComparison.Ordinal);
        Assert.Contains("'$($script:CompactActorReasoningTemplatePath)'", repairBlock, StringComparison.Ordinal);
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
