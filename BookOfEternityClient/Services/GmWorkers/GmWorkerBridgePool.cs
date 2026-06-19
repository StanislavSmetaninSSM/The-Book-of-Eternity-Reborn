using System.Diagnostics;
using System.Text;

namespace BookOfEternityClient.Services.GmWorkers;

public sealed class GmWorkerBridgePool
{
    public static WorkerRoutingResult SelectWorkerForTask(
        IReadOnlyList<WorkerBridgeProfile> profiles,
        WorkerTaskType taskType)
    {
        foreach (var profile in profiles)
        {
            if (!profile.Enabled)
                continue;
            var validation = GmWorkerContractValidator.ValidateProfile(profile);
            if (!validation.IsValid)
                continue;
            if (profile.Permissions.TaskTypes.Contains(taskType))
                return new WorkerRoutingResult(true, profile, "");
        }

        return new WorkerRoutingResult(false, null, $"No enabled worker profile can handle task type {taskType}.");
    }

    public static IReadOnlyList<WorkerBridgeStatus> BuildInitialStatuses(IReadOnlyList<WorkerBridgeProfile> profiles)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        return profiles
            .Select(profile => new WorkerBridgeStatus
            {
                WorkerId = profile.WorkerId,
                State = profile.Enabled ? WorkerBridgeState.Stopped : WorkerBridgeState.Disabled,
                Ready = false,
                UpdatedAtUtc = now
            })
            .ToArray();
    }

    public static ProcessStartInfo CreateWorkerStartInfo(WorkerBridgeProfile profile, string workingDirectory)
    {
        var validation = GmWorkerContractValidator.ValidateProfile(profile);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join(Environment.NewLine, validation.Errors), nameof(profile));

        var command = SplitCommandLine(profile.LaunchCommand);
        if (command.Count == 0)
            throw new ArgumentException("Worker launchCommand must contain an executable.", nameof(profile));

        var startInfo = new ProcessStartInfo
        {
            FileName = command[0],
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        for (var i = 1; i < command.Count; i++)
            startInfo.ArgumentList.Add(command[i]);

        return startInfo;
    }

    internal static IReadOnlyList<string> SplitCommandLine(string commandLine)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(commandLine))
            return result;

        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < commandLine.Length; i++)
        {
            var ch = commandLine[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                AddCurrent();
                continue;
            }

            current.Append(ch);
        }

        AddCurrent();
        return result;

        void AddCurrent()
        {
            if (current.Length == 0)
                return;
            result.Add(current.ToString());
            current.Clear();
        }
    }
}
