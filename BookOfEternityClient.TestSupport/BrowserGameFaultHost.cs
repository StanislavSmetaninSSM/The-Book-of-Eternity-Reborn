using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.DependencyInjection;

namespace BookOfEternityClient.Tests;

/// <summary>Actual web host/handlers with isolated test-only fault/diagnostic boundaries.</summary>
internal static class BrowserGameFaultHost
{
    [ThreadStatic] private static bool _writing;

    internal static async Task<int> RunAsync(string root, string evidence, string url, string assets)
    {
        var cuts = 0;
        var exceptions = 0;
        var phases = new List<string>();
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (_writing || !File.Exists(Path.Combine(evidence, "arm-save-diagnostic")) ||
                Interlocked.Increment(ref exceptions) > 32) return;
            try
            {
                _writing = true;
                File.AppendAllText(Path.Combine(evidence, "save-diagnostic.jsonl"),
                    JsonSerializer.Serialize(new { Type = args.Exception.GetType().FullName, Failure = args.Exception.ToString() }) + Environment.NewLine);
            }
            finally { _writing = false; }
        };
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) =>
            {
                if (!File.Exists(Path.Combine(evidence, "arm-load-fault"))) return;
                phases.Add(phase + ":" + index);
                if (phase != TrustedLocalPublicationPhase.CommitStaged || cuts != 0) return;
                var journal = Path.Combine(root, ".boe_runtime/trusted-local-publication-v1/active.json");
                using var actual = File.OpenRead(journal);
                Span<byte> prefix = stackalloc byte[8]; actual.ReadExactly(prefix);
                if (!prefix.SequenceEqual("BOELP3\r\n"u8)) return;
                cuts++;
                File.WriteAllText(Path.Combine(evidence, "load-fault.json"), JsonSerializer.Serialize(new
                {
                    Scope = "actual replacement journal BOELP3 and real CommitStaged late write cut",
                    Cuts = cuts, Phases = phases.ToArray(), ModelCalls = 0
                }));
                throw new IOException("controlled actual browser Load CommitStaged failure");
            }
        };
        AppDomain.CurrentDomain.FirstChanceException += observe;
        try
        {
            await using var app = LocalWebUiHost.Build([], new(root, url, assets), hooks);
            // Resolve the real singleton now; no service or authority replacement.
            _ = app.Services.GetRequiredService<FileSystemManager>();
            await app.RunAsync();
            return 0;
        }
        finally { AppDomain.CurrentDomain.FirstChanceException -= observe; }
    }
}
