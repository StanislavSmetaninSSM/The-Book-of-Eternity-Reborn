using System.Diagnostics;
using System.Text;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerCliRunnerTests
{
    [Fact]
    public async Task RunnerDryRun_WritesPromptWithProposalProtocol()
    {
        var root = CreateTempRoot();
        try
        {
            var sessionPath = Path.Combine(root, "game_session");
            var taskPath = Path.Combine(root, "task.json");
            var proposalPath = Path.Combine(root, "worker_proposals", "inbox", "task-1", "proposal.json");
            var promptPath = Path.Combine(root, "prompt.txt");
            Directory.CreateDirectory(sessionPath);
            await File.WriteAllTextAsync(taskPath, """
                {
                  "schemaVersion": 1,
                  "taskId": "worker_task_runner_dry_run",
                  "workerId": "validation_repair_codex",
                  "taskType": "validation-repair",
                  "responseContract": "worker-proposal-v1",
                  "instructions": "Repair only allowed files."
                }
                """, Encoding.UTF8);

            var result = await RunRunnerAsync(
                ["-DryRun", "-PromptOutPath", promptPath],
                new Dictionary<string, string>
                {
                    ["BOE_WORKER_TASK_PATH"] = taskPath,
                    ["BOE_WORKER_PROPOSAL_PATH"] = proposalPath,
                    ["BOE_WORKER_SESSION_PATH"] = sessionPath
                });

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(promptPath), result.StandardError);
            var prompt = await File.ReadAllTextAsync(promptPath, Encoding.UTF8);
            Assert.Contains("worker-proposal-v1", prompt, StringComparison.Ordinal);
            Assert.Contains("worker_task_runner_dry_run", prompt, StringComparison.Ordinal);
            Assert.Contains(taskPath, prompt, StringComparison.Ordinal);
            Assert.Contains(proposalPath, prompt, StringComparison.Ordinal);
            Assert.Contains(sessionPath, prompt, StringComparison.Ordinal);
            Assert.Contains("Do not edit canonical game_session files directly.", prompt, StringComparison.Ordinal);
            Assert.Contains("Write exactly one worker-proposal-v1 JSON object", prompt, StringComparison.Ordinal);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunnerDryRun_WritesSelfContainedProposalSchema()
    {
        var root = CreateTempRoot();
        try
        {
            var sessionPath = Path.Combine(root, "game_session");
            var taskPath = Path.Combine(root, "task.json");
            var proposalPath = Path.Combine(root, "worker_proposals", "inbox", "task-schema", "proposal.json");
            var promptPath = Path.Combine(root, "prompt-schema.txt");
            Directory.CreateDirectory(sessionPath);
            await File.WriteAllTextAsync(taskPath, """
                {
                  "schemaVersion": 1,
                  "taskId": "worker_task_runner_schema",
                  "workerId": "narrative_draft_codex",
                  "taskType": "narrative-draft",
                  "responseContract": "worker-proposal-v1",
                  "instructions": "Return a scene draft."
                }
                """, Encoding.UTF8);

            var result = await RunRunnerAsync(
                ["-DryRun", "-PromptOutPath", promptPath],
                new Dictionary<string, string>
                {
                    ["BOE_WORKER_TASK_PATH"] = taskPath,
                    ["BOE_WORKER_PROPOSAL_PATH"] = proposalPath,
                    ["BOE_WORKER_SESSION_PATH"] = sessionPath
                });

            Assert.Equal(0, result.ExitCode);
            var prompt = await File.ReadAllTextAsync(promptPath, Encoding.UTF8);
            Assert.Contains("Required worker-proposal-v1 JSON shape", prompt, StringComparison.Ordinal);
            Assert.Contains("\"schemaVersion\": 1", prompt, StringComparison.Ordinal);
            Assert.Contains("\"proposalId\"", prompt, StringComparison.Ordinal);
            Assert.Contains("\"taskId\"", prompt, StringComparison.Ordinal);
            Assert.Contains("\"workerId\"", prompt, StringComparison.Ordinal);
            Assert.Contains("\"status\": \"completed\"", prompt, StringComparison.Ordinal);
            Assert.Contains("\"summary\"", prompt, StringComparison.Ordinal);
            Assert.Contains("\"changedFiles\": []", prompt, StringComparison.Ordinal);
            Assert.Contains("\"findings\": []", prompt, StringComparison.Ordinal);
            Assert.Contains("\"draftText\"", prompt, StringComparison.Ordinal);
            Assert.Contains("\"selfCheck\"", prompt, StringComparison.Ordinal);
            Assert.Contains("\"scopeReviewed\": true", prompt, StringComparison.Ordinal);
            Assert.Contains("\"validationExpectedToPass\": true", prompt, StringComparison.Ordinal);
            Assert.Contains("\"createdAtUtc\"", prompt, StringComparison.Ordinal);
            Assert.Contains("Do not omit summary, status, changedFiles, findings, selfCheck, or createdAtUtc.", prompt, StringComparison.Ordinal);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunnerDryRun_WhenEnvironmentMissing_ReportsMissingVariable()
    {
        var result = await RunRunnerAsync(
            ["-DryRun"],
            new Dictionary<string, string>());

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("BOE_WORKER_TASK_PATH", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunnerRealMode_FeedsPromptToAgentAndRequiresProposal()
    {
        var root = CreateTempRoot();
        try
        {
            var sessionPath = Path.Combine(root, "game_session");
            var taskPath = Path.Combine(root, "task.json");
            var proposalPath = Path.Combine(root, "worker_proposals", "inbox", "task-2", "proposal.json");
            var capturedPromptPath = Path.Combine(root, "captured-prompt.txt");
            var fakeAgentPath = Path.Combine(root, "fake-agent.ps1");
            Directory.CreateDirectory(sessionPath);
            await File.WriteAllTextAsync(taskPath, """
                {
                  "schemaVersion": 1,
                  "taskId": "worker_task_runner_real_mode",
                  "workerId": "analysis_codex",
                  "taskType": "analysis",
                  "responseContract": "worker-proposal-v1",
                  "instructions": "Return findings only."
                }
                """, Encoding.UTF8);
            await File.WriteAllTextAsync(fakeAgentPath, $$"""
                $prompt = [Console]::In.ReadToEnd()
                [System.IO.File]::WriteAllText('{{capturedPromptPath}}', $prompt, [System.Text.UTF8Encoding]::new($false))
                if ($prompt -notlike '*worker-proposal-v1*') { exit 21 }
                if ($prompt -notlike '*worker_task_runner_real_mode*') { exit 22 }
                $proposal = '{"schemaVersion":1,"proposalId":"worker_proposal_runner_real_mode","taskId":"worker_task_runner_real_mode","workerId":"analysis_codex","status":"completed","summary":"Fake agent wrote proposal.","changedFiles":[],"findings":[],"selfCheck":{"scopeReviewed":true,"validationExpectedToPass":true,"notes":[]},"createdAtUtc":"2026-06-20T00:00:00Z"}'
                [System.IO.File]::WriteAllText($env:BOE_WORKER_PROPOSAL_PATH, $proposal, [System.Text.UTF8Encoding]::new($false))
                """, Encoding.UTF8);

            var result = await RunRunnerAsync(
                [
                    "-AgentCommand",
                    $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{fakeAgentPath}\"",
                    "-TimeoutSeconds",
                    "10"
                ],
                new Dictionary<string, string>
                {
                    ["BOE_WORKER_TASK_PATH"] = taskPath,
                    ["BOE_WORKER_PROPOSAL_PATH"] = proposalPath,
                    ["BOE_WORKER_SESSION_PATH"] = sessionPath
                });

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(proposalPath), result.StandardError);
            Assert.Contains("worker_proposal_runner_real_mode", await File.ReadAllTextAsync(proposalPath, Encoding.UTF8), StringComparison.Ordinal);
            Assert.Contains("worker_task_runner_real_mode", await File.ReadAllTextAsync(capturedPromptPath, Encoding.UTF8), StringComparison.Ordinal);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunnerRealMode_WhenAgentSkipsProposal_ReturnsMissingProposalError()
    {
        var root = CreateTempRoot();
        try
        {
            var sessionPath = Path.Combine(root, "game_session");
            var taskPath = Path.Combine(root, "task.json");
            var proposalPath = Path.Combine(root, "worker_proposals", "inbox", "task-3", "proposal.json");
            var fakeAgentPath = Path.Combine(root, "fake-agent-no-proposal.ps1");
            Directory.CreateDirectory(sessionPath);
            await File.WriteAllTextAsync(taskPath, """
                {
                  "schemaVersion": 1,
                  "taskId": "worker_task_runner_no_proposal",
                  "workerId": "analysis_codex",
                  "taskType": "analysis",
                  "responseContract": "worker-proposal-v1"
                }
                """, Encoding.UTF8);
            await File.WriteAllTextAsync(fakeAgentPath, "[Console]::In.ReadToEnd() | Out-Null; exit 0", Encoding.UTF8);

            var result = await RunRunnerAsync(
                [
                    "-AgentCommand",
                    $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{fakeAgentPath}\"",
                    "-TimeoutSeconds",
                    "10"
                ],
                new Dictionary<string, string>
                {
                    ["BOE_WORKER_TASK_PATH"] = taskPath,
                    ["BOE_WORKER_PROPOSAL_PATH"] = proposalPath,
                    ["BOE_WORKER_SESSION_PATH"] = sessionPath
                });

            Assert.Equal(5, result.ExitCode);
            Assert.Contains("completed without writing BOE_WORKER_PROPOSAL_PATH", result.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunnerRealMode_BareAgentCommand_ResolvesCmdFromPath()
    {
        var root = CreateTempRoot();
        try
        {
            var sessionPath = Path.Combine(root, "game_session");
            var taskPath = Path.Combine(root, "task.json");
            var proposalPath = Path.Combine(root, "worker_proposals", "inbox", "task-4", "proposal.json");
            var fakeAgentCmdPath = Path.Combine(root, "fake-agent.cmd");
            var fakeAgentScriptPath = Path.Combine(root, "fake-agent-cmd.ps1");
            Directory.CreateDirectory(sessionPath);
            await File.WriteAllTextAsync(taskPath, """
                {
                  "schemaVersion": 1,
                  "taskId": "worker_task_runner_path_cmd",
                  "workerId": "analysis_codex",
                  "taskType": "analysis",
                  "responseContract": "worker-proposal-v1"
                }
                """, Encoding.UTF8);
            await File.WriteAllTextAsync(fakeAgentCmdPath, $$"""
                @echo off
                powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "{{fakeAgentScriptPath}}"
                """, Encoding.ASCII);
            await File.WriteAllTextAsync(fakeAgentScriptPath, """
                [Console]::In.ReadToEnd() | Out-Null
                $proposal = '{"schemaVersion":1,"proposalId":"worker_proposal_runner_path_cmd","taskId":"worker_task_runner_path_cmd","workerId":"analysis_codex","status":"completed","summary":"Fake PATH agent wrote proposal.","changedFiles":[],"findings":[],"selfCheck":{"scopeReviewed":true,"validationExpectedToPass":true,"notes":[]},"createdAtUtc":"2026-06-20T00:00:00Z"}'
                [System.IO.File]::WriteAllText($env:BOE_WORKER_PROPOSAL_PATH, $proposal, [System.Text.UTF8Encoding]::new($false))
                """, Encoding.UTF8);

            var result = await RunRunnerAsync(
                [
                    "-AgentCommand",
                    "fake-agent",
                    "-TimeoutSeconds",
                    "10"
                ],
                new Dictionary<string, string>
                {
                    ["BOE_WORKER_TASK_PATH"] = taskPath,
                    ["BOE_WORKER_PROPOSAL_PATH"] = proposalPath,
                    ["BOE_WORKER_SESSION_PATH"] = sessionPath,
                    ["PATH"] = root + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH")
                });

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(proposalPath), result.StandardError);
            Assert.Contains("worker_proposal_runner_path_cmd", await File.ReadAllTextAsync(proposalPath, Encoding.UTF8), StringComparison.Ordinal);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunnerRealMode_FeedsPromptToAgentAsUtf8()
    {
        var root = CreateTempRoot();
        try
        {
            var sessionPath = Path.Combine(root, "game_session");
            var taskPath = Path.Combine(root, "task.json");
            var proposalPath = Path.Combine(root, "worker_proposals", "inbox", "task-5", "proposal.json");
            var fakeAgentPath = Path.Combine(root, "fake-agent-utf8.ps1");
            Directory.CreateDirectory(sessionPath);
            await File.WriteAllTextAsync(taskPath, """
                {
                  "schemaVersion": 1,
                  "taskId": "worker_task_runner_utf8",
                  "workerId": "validation_repair_codex",
                  "taskType": "validation-repair",
                  "responseContract": "worker-proposal-v1",
                  "instructions": "Проверить и починить погоду."
                }
                """, Encoding.UTF8);
            await File.WriteAllTextAsync(fakeAgentPath, """
                $stdin = [Console]::OpenStandardInput()
                $memory = [System.IO.MemoryStream]::new()
                $stdin.CopyTo($memory)
                $bytes = $memory.ToArray()
                $decoder = [System.Text.UTF8Encoding]::new($false, $true)
                try {
                    $prompt = $decoder.GetString($bytes)
                }
                catch {
                    [Console]::Error.WriteLine($_.Exception.Message)
                    exit 31
                }

                if ($prompt -notlike '*Проверить и починить погоду.*') {
                    [Console]::Error.WriteLine('UTF-8 prompt text was decoded, but the Cyrillic task instruction was not preserved.')
                    exit 32
                }

                $proposal = '{"schemaVersion":1,"proposalId":"worker_proposal_runner_utf8","taskId":"worker_task_runner_utf8","workerId":"validation_repair_codex","status":"completed","summary":"Fake UTF-8 agent wrote proposal.","changedFiles":[],"findings":[],"selfCheck":{"scopeReviewed":true,"validationExpectedToPass":true,"notes":[]},"createdAtUtc":"2026-06-20T00:00:00Z"}'
                [System.IO.File]::WriteAllText($env:BOE_WORKER_PROPOSAL_PATH, $proposal, [System.Text.UTF8Encoding]::new($false))
                """, Encoding.UTF8);

            var result = await RunRunnerAsync(
                [
                    "-AgentCommand",
                    $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{fakeAgentPath}\"",
                    "-TimeoutSeconds",
                    "10"
                ],
                new Dictionary<string, string>
                {
                    ["BOE_WORKER_TASK_PATH"] = taskPath,
                    ["BOE_WORKER_PROPOSAL_PATH"] = proposalPath,
                    ["BOE_WORKER_SESSION_PATH"] = sessionPath
                });

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(proposalPath), result.StandardError);
            Assert.Contains("worker_proposal_runner_utf8", await File.ReadAllTextAsync(proposalPath, Encoding.UTF8), StringComparison.Ordinal);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static async Task<RunnerResult> RunRunnerAsync(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment)
    {
        var scriptPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Launcher",
            "gm_worker_cli_runner.ps1");
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        foreach (var pair in environment)
            startInfo.Environment[pair.Key] = pair.Value;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("PowerShell runner did not start.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new RunnerResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-worker-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }

    private sealed record RunnerResult(int ExitCode, string StandardOutput, string StandardError);
}
