using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ConsoleE2ERunbookTests
{
    [Fact]
    public void AgentConsoleLiveRunbookDocumentsRequiredWorkflowAndSafetyRules()
    {
        var runbook = ReadRepoFile("docs", "e2e", "agent-console-runbook.md");

        foreach (var requiredText in new[]
        {
            "Issue: #753",
            "Parent task: #749",
            "FileSystemExample/game_session",
            "disposable",
            "--agent-console",
            "--agent-url http://127.0.0.1:",
            "--agent-token auto",
            "--agent-token <token>",
            "Authorization: Bearer $TOKEN",
            "Authorization: Bearer $Token",
            "GET /api/agent-console/snapshot",
            "GET /api/agent-console/events",
            "POST /api/agent-console/key",
            "POST /api/agent-console/text",
            "POST /api/agent-console/action",
            "POST /api/agent-console/return-to-game-loop-step",
            "curl",
            "does not store secrets",
            "shutdown",
            "scripted E2E"
        })
        {
            Assert.Contains(requiredText, runbook, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AgentConsoleLiveRunbookCoversTroubleshootingAndBoundedArtifacts()
    {
        var runbook = ReadRepoFile("docs", "e2e", "agent-console-runbook.md");

        foreach (var requiredText in new[]
        {
            "port conflict",
            "missing token",
            "invalid token",
            "non-loopback",
            "no snapshot yet",
            "blocked/waiting input",
            "bounded artifacts",
            "stdout.txt",
            "stderr.txt",
            "events.jsonl",
            "do not commit generated run output"
        })
        {
            Assert.Contains(requiredText, runbook, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("Join-Path $env:TEMP", runbook, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentConsoleLiveRunbookDocumentsSafeReadOnlyCommandSweepHelper()
    {
        var runbook = ReadRepoFile("docs", "e2e", "agent-console-runbook.md");

        foreach (var requiredText in new[]
        {
            "scripts/agent-console-readonly-sweep.ps1",
            "read-only command sweep",
            "return-to-game-loop-step",
            "do not use `/default-action`",
            "turn-preparing",
            "forbidden markers"
        })
        {
            Assert.Contains(requiredText, runbook, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AgentConsoleReadOnlySweepScriptUsesOnlySafeReadOnlyControlEndpoints()
    {
        var script = ReadRepoFile("scripts", "agent-console-readonly-sweep.ps1");

        foreach (var requiredText in new[]
        {
            "/api/agent-console/snapshot",
            "/api/agent-console/text",
            "/api/agent-console/return-to-game-loop-step",
            "screenId",
            "inputKind",
            "game-loop",
            "command-processing",
            "turn-preparing",
            "forbiddenMarkers"
        })
        {
            Assert.Contains(requiredText, script, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("/api/agent-console/default-action", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentConsoleReadOnlySweepChecksForbiddenMarkersOnlyInPlayerVisibleText()
    {
        var script = ReadRepoFile("scripts", "agent-console-readonly-sweep.ps1");

        foreach (var requiredText in new[]
        {
            "function Get-PlayerVisibleSnapshotText",
            "plainText",
            "prompt",
            "actions",
            "diagnostics",
            "Find-ForbiddenMarkers -VisibleText",
            "Return-ToGameLoop -Trace $trace | Out-Null",
            "forbidden-marker-found"
        })
        {
            Assert.Contains(requiredText, script, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("Find-ForbiddenMarkers $resultSnapshot", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentConsoleRunbookDocumentsGoldenRouteDriver()
    {
        var runbook = ReadRepoFile("docs", "e2e", "agent-console-runbook.md");

        foreach (var requiredText in new[]
        {
            "scripts/agent-console-golden-route-driver.ps1",
            "state-aware golden route driver",
            "step kinds",
            "text",
            "action",
            "defaultAction",
            "keys",
            "returnToGameLoop",
            "autoContinueKeyScreens",
            "stat-allocation"
        })
        {
            Assert.Contains(requiredText, runbook, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AgentConsoleRunbookDocumentsPreflightedLiveRunLauncher()
    {
        var runbook = ReadRepoFile("docs", "e2e", "agent-console-runbook.md");

        foreach (var requiredText in new[]
        {
            "scripts\\start-agent-console-live-run.ps1",
            "preflighted launcher",
            "starts the daemon",
            "starts the GM bridge",
            "ready=true",
            "live-meta.json",
            "before selecting New Game",
            "boe-live-runs",
            "same drive as the repository"
        })
        {
            Assert.Contains(requiredText, runbook, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AgentConsoleGoldenRouteDriverScriptUsesStateAwareControlEndpoints()
    {
        var script = ReadRepoFile("scripts", "agent-console-golden-route-driver.ps1");

        foreach (var requiredText in new[]
        {
            "/api/agent-console/snapshot",
            "/api/agent-console/text",
            "/api/agent-console/action",
            "/api/agent-console/default-action",
            "/api/agent-console/key",
            "/api/agent-console/return-to-game-loop-step",
            "step kinds",
            "autoContinueKeyScreens",
            "stat-allocation",
            "notAwaitingInput",
            "returnToGameLoop"
        })
        {
            Assert.Contains(requiredText, script, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AgentConsoleGoldenRouteDriverAutoContinuesKnownLifecycleKeyScreens()
    {
        var script = ReadRepoFile("scripts", "agent-console-golden-route-driver.ps1");

        foreach (var requiredScreen in new[]
        {
            "stat-allocation-finished",
            "life-transition-death",
            "realm-transition-chaos-sea",
            "life-evaluation-rewards"
        })
        {
            Assert.Contains(requiredScreen, script, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AgentConsoleGoldenRouteDriverFailsFastOnUnexpectedAwaitingScreens()
    {
        var script = ReadRepoFile("scripts", "agent-console-golden-route-driver.ps1");

        Assert.Contains("FailOnUnexpectedAwaitingScreen", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unexpected awaiting screen", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentConsoleLiveRunLauncherPreflightsBridgeBeforeGmBoundTurns()
    {
        var script = ReadRepoFile("scripts", "start-agent-console-live-run.ps1");

        foreach (var requiredText in new[]
        {
            "start-daemon",
            "start-bridge",
            "Wait-GmBridgeReady",
            "gm_bridge_status.json",
            "ready",
            "Wait-AgentSnapshot",
            "live-meta.json",
            "BookOfEternityClient.exe"
        })
        {
            Assert.Contains(requiredText, script, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("Remove-Item", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Split-Path $repoRoot -Parent", script, StringComparison.Ordinal);
        Assert.Contains("boe-live-runs", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Join-Path $env:TEMP", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentConsoleGmRuntimePreflightScriptChecksDaemonAndBridgeLiveness()
    {
        var script = ReadRepoFile("scripts", "agent-console-gm-runtime-preflight.ps1");

        foreach (var requiredText in new[]
        {
            "gm_daemon_status.json",
            "gm_bridge_status.json",
            "Get-Process",
            "helperPid",
            "shellPid",
            "pid",
            "RequireBridge",
            "RequireReadyBridge",
            "WaitSeconds",
            "exit 1"
        })
        {
            Assert.Contains(requiredText, script, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LauncherScriptExposesSafeDaemonStartAction()
    {
        var launcher = ReadRepoFile("BookOfEternityClient", "Launcher", "bookofeternity.ps1");

        foreach (var requiredText in new[]
        {
            "start-daemon",
            "Start-Daemon",
            "game_master_daemon.ps1",
            "-EncodedCommand",
            "daemonPid",
            "daemon.log"
        })
        {
            Assert.Contains(requiredText, launcher, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GmWorkersLiveRunbookUsesLauncherDaemonStart()
    {
        var runbook = ReadRepoFile("docs", "e2e", "gm-workers-live-regression-runbook.md");

        foreach (var requiredText in new[]
        {
            "bookofeternity.ps1 start-daemon",
            "--timeout 900",
            "daemon.start.json",
            "daemonPid",
            "gm_daemon_status.json",
            "status=processing",
            "currentTurnNumber",
            "turnElapsedSeconds",
            "lastLoopError",
            "gm_daemon_fatal_error.json",
            "harness bug"
        })
        {
            Assert.Contains(requiredText, runbook, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GmWorkersLiveRunbookDocumentsRuntimeDeadlockHarnessSignals()
    {
        var runbook = ReadRepoFile("docs", "e2e", "gm-workers-live-regression-runbook.md");

        foreach (var requiredText in new[]
        {
            "scripts/agent-console-gm-runtime-preflight.ps1",
            "GmTimeoutSeconds",
            "dead pid",
            "gm_runtime_unavailable",
            "gm_terminal_wait_timeout",
            "ready/turn_error.json",
            "harnessSource",
            "WaitSeconds",
            "stale status"
        })
        {
            Assert.Contains(requiredText, runbook, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ScriptedAgentRunbookLinksToLiveAgentConsoleRunbook()
    {
        var runbook = ReadRepoFile("docs", "e2e", "console-agent-runbook.md");

        Assert.Contains("agent-console-runbook.md", runbook, StringComparison.Ordinal);
        Assert.Contains("live Agent Console", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scripted E2E", runbook, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentRunbookDocumentsRequiredWorkflowAndSafetyRules()
    {
        var runbook = ReadRepoFile("docs", "e2e", "console-agent-runbook.md");

        foreach (var requiredText in new[]
        {
            "Issue: #679",
            "FileSystemExample/game_session",
            "ConsoleE2ESandbox.CreateFromFixture",
            "dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter ConsoleE2E",
            "--e2e-script",
            "--e2e-artifacts",
            "--plain-output",
            "kind",
            "key",
            "text",
            "preserveArtifacts: true",
            "tracked GitHub issue",
            "Mortal World mechanics",
            "Afterlife contract",
            "docs/console-e2e-sandbox.md"
        })
        {
            Assert.Contains(requiredText, runbook, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AgentRunbookCoversCommonConsoleE2ETroubleshootingCases()
    {
        var runbook = ReadRepoFile("docs", "e2e", "console-agent-runbook.md");

        foreach (var requiredText in new[]
        {
            "invalid `game_session`",
            "prompt/input hang",
            "timeout",
            "ANSI",
            "NO_COLOR",
            "cleanup",
            "screen/state snapshots",
            "failure artifacts",
            "Console E2E scripted input failed at step 0",
            "$RUN_ROOT/artifacts/failure.txt",
            "screens/*error*.json"
        })
        {
            Assert.Contains(requiredText, runbook, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadRepoFile(params string[] relativePathParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativePathParts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate repository file: " + Path.Combine(relativePathParts));
    }
}
