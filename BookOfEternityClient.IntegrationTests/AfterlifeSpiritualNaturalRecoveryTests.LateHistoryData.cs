using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    private const string LateHistoryCaptureSha256 = "601271973f3e76f7298daed0eea23bf769c26b3c59db95ac06a3311b01dea1a0";
    private const string EarlyHistoryCaptureSha256 = "5da0a36f6b0908a3ad478ce4dc2db3ba675c2f3550d5e3fae0e8afdeac2b24a0";

    [Fact]
    public async Task LateHistoryDataOnlyMeasuresCanonicalKernels()
    {
        // Independent inputs, no fixture, live owner, proof, publication or test ordering.
        var late = await ReadLateHistoryDiagnosticCaptureAsync();
        var early = await ReadPinnedHistoryCaptureAsync("BOE_SPIRITUAL_EARLY_HISTORY_INPUT",
            EarlyHistoryCaptureSha256, 1);
        var evidence = Path.Combine(RequireLateHistoryDiagnosticRoot(), "late-history-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        var rows = new List<object>();
        var passed = false;
        Exception? failure = null;
        try
        {
            foreach (var (label, root) in new[] { ("early", early), ("late", late) })
            {
                // Agreement is established outside all measured loops, not replaced by timing.
                var profiles = root["profiles"]!.AsObject();
                var identities = root["identities"]!.AsObject();
                var historyRoot = root["history"]!.AsObject();
                var catalog = RequireCurrentAgreement(profiles, identities, historyRoot);
                var identity = WoundIdentityState.Parse(identities.ToJsonString(), "diagnostic.identity").State!;
                var historyText = historyRoot.ToJsonString();
                var history = WoundHistoryState.Parse(historyText, "diagnostic.history").State!;
                var expectedCanonical = WoundHistoryState.SerializeCanonical(history);
                var expectedOrdered = WoundAcceptedTurnFingerprintWriter.CanonicalJson(historyRoot);
                var expectedFingerprint = WoundAcceptedTurnFingerprintWriter.Compute([expectedOrdered]);
                // Empty events and drafts are intentionally non-authorizing DATA, never admitted.
                var binding = root["result"]!["binding"]!;
                WoundAcceptedEventAuthority[] events = [];
                var input = new WoundAcceptedTurnInput(new(
                    binding["sessionId"]!.GetValue<string>(), binding["requestId"]!.GetValue<string>(),
                    binding["snapshotToken"]!.GetValue<string>(), binding["realm"]!.GetValue<string>(),
                    binding["turn"]!.GetValue<int>(), events, WoundAcceptedEventSetFingerprint.Compute(events)),
                    [], [], new(null, null, null, null, profiles), identities, historyRoot,
                    new(null, null, null, null, profiles, AfterlifeSpiritualConflictState.CreateDefaultRoot()),
                    root["effects"]!.AsObject());
                var expectedInput = WoundAcceptedTurnFingerprints.ComputeInput(input);
                var timings = new OperationTimingAccumulator();
                using var window = timings.StartWindow();
                var complete = false;
                try
                {
                    for (var repeat = 0; repeat < 4; repeat++)
                    {
                        var sample = repeat == 0 ? "FirstMeasured" : "Repeat";
                        WoundHistoryParseResult parsed;
                        string serialized, ordered, fingerprint, inputFingerprint;
                        JsonNode clone;
                        WoundAcceptedTurnInput? detached;
                        IReadOnlyList<ValidationIssue> agreement;
                        // Thread-static timing is strictly synchronous and never spans an await.
                        using (FileSystemManager.EnterOperationTimingContext(null, timings))
                        {
                            using (timings.Measure($"DataOnly.{sample}.HistoryParse"))
                                parsed = WoundHistoryState.Parse(historyText, "diagnostic.measured.history");
                            using (timings.Measure($"DataOnly.{sample}.HistorySerialize"))
                                serialized = WoundHistoryState.SerializeCanonical(history);
                            using (timings.Measure($"DataOnly.{sample}.HistoryAgreement"))
                                agreement = history.ValidateAgreement(identity, catalog);
                            using (timings.Measure($"DataOnly.{sample}.HistoryClone"))
                                clone = historyRoot.DeepClone();
                            using (timings.Measure($"DataOnly.{sample}.HistoryCanonicalJson"))
                                ordered = WoundAcceptedTurnFingerprintWriter.CanonicalJson(historyRoot)!;
                            using (timings.Measure($"DataOnly.{sample}.HistoryFingerprint"))
                                fingerprint = WoundAcceptedTurnFingerprintWriter.Compute([ordered]);
                            using (timings.Measure($"DataOnly.{sample}.InputClone"))
                                detached = WoundAcceptedTurnData.CloneInput(input);
                            using (timings.Measure($"DataOnly.{sample}.InputFingerprint"))
                                inputFingerprint = WoundAcceptedTurnFingerprints.ComputeInput(input);
                        }
                        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues.Select(i => i.Actual)));
                        Assert.Equal(expectedCanonical, WoundHistoryState.SerializeCanonical(parsed.State!));
                        Assert.Empty(agreement);
                        Assert.Equal(expectedCanonical, serialized);
                        Assert.Equal(expectedOrdered, ordered);
                        Assert.Equal(expectedFingerprint, fingerprint);
                        Assert.Equal(expectedInput, inputFingerprint);
                        Assert.NotNull(detached);
                        Assert.Equal(expectedInput, WoundAcceptedTurnFingerprints.ComputeInput(detached!));
                        Assert.True(JsonNode.DeepEquals(historyRoot, clone));
                        clone["nextOrdinal"] = -1;
                        Assert.NotEqual(-1, historyRoot["nextOrdinal"]!.GetValue<int>());
                        var detachedHistory = detached!.PreTurnHistory;
                        detachedHistory["nextOrdinal"] = -2;
                        Assert.NotEqual(-2, input.PreTurnHistory["nextOrdinal"]!.GetValue<int>());
                    }
                }
                finally
                {
                    window.Dispose();
                    var capture = window.Capture();
                    complete = capture.Complete;
                    rows.Add(new { label, historyRows = history.Transitions.Count,
                        historyUtf8Bytes = Encoding.UTF8.GetByteCount(historyText),
                        canonicalUtf8Bytes = Encoding.UTF8.GetByteCount(expectedOrdered!),
                        nonAuthorizingInputFingerprint = expectedInput, timings = capture });
                }
                Assert.True(complete);
            }
            passed = true;
        }
        catch (Exception exception) { failure = exception; throw; }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(evidence, "outcome.json"), JsonSerializer.Serialize(new
            {
                scenario = "detached-data-only-no-owner-no-publication", passed, failure = failure?.ToString(),
                lateInputSha256 = LateHistoryCaptureSha256, earlyInputSha256 = EarlyHistoryCaptureSha256,
                samplesPerShape = 4, warmup = "Canonical validation and correctness oracles precede samples; no cold-start claim.", rows,
                comparison = "Compare equal DATA kernels and per-operation costs, not total duration against a real turn."
            }));
        }
    }

    /// <summary>Loads independently pinned late DATA without restoring an old runtime owner or resource lineage.</summary>
    /// <returns>The strictly checked captured DATA, freshly parsed for this caller.</returns>
    private static Task<JsonObject> ReadLateHistoryDiagnosticCaptureAsync() =>
        ReadPinnedHistoryCaptureAsync("BOE_SPIRITUAL_LATE_HISTORY_INPUT", LateHistoryCaptureSha256, 17);

    /// <summary>Fails closed before fixture allocation unless the external capture is exact and canonically consistent.</summary>
    /// <param name="variable">The explicit absolute input path variable.</param>
    /// <param name="sha256">The independently pinned raw input digest.</param>
    /// <param name="cycle">The expected saved cycle.</param>
    /// <returns>A fresh, complete parsed capture.</returns>
    private static async Task<JsonObject> ReadPinnedHistoryCaptureAsync(string variable, string sha256, int cycle)
    {
        _ = RequireLateHistoryDiagnosticRoot();
        var path = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidDataException($"{variable} must name the pinned external capture with an absolute path.");
        RequireOutsideRepository(path);
        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        var root = JsonNode.Parse(bytes)!.AsObject();
        Assert.Equal(cycle, root["cycle"]!.GetValue<int>());
        var result = root["result"]!.AsObject();
        var binding = result["binding"]!.AsObject();
        Assert.Equal(cycle + 42, binding["turn"]!.GetValue<int>());
        Assert.Equal(root["requestId"]!.GetValue<string>(), binding["requestId"]!.GetValue<string>());
        Assert.Equal(root["snapshotToken"]!.GetValue<string>(), binding["snapshotToken"]!.GetValue<string>());
        var history = root["history"]!.AsObject();
        Assert.Equal(cycle + 1, history["transitions"]!.AsArray().Count);
        Assert.True(JsonNode.DeepEquals(result, history["transitions"]!.AsArray()[^1]!["transitionResult"]));
        RequireCurrentAgreement(root["profiles"]!.AsObject(), root["identities"]!.AsObject(), history);
        var mechanics = EffectMechanicsSnapshot.Build(new(
            new(null, null, null, null, root["profiles"]!.AsObject(), AfterlifeSpiritualConflictState.CreateDefaultRoot()),
            root["effects"]!.AsObject()));
        Assert.True(mechanics.IsAccepted, string.Join(";", mechanics.Issues.Select(i => i.Actual)));
        var scheduleText = result["scheduleAfterJson"]!.GetValue<string>();
        Assert.True(JsonNode.DeepEquals(root["schedule"], JsonNode.Parse(scheduleText)));
        var scheduleBytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(scheduleText)).ToArray();
        Assert.Equal(result["scheduleAfterFingerprint"]!.GetValue<string>(),
            new CanonicalBeforeImage(true, scheduleBytes).Fingerprint);
        Assert.Equal(cycle, root["schedule"]!["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
        return root;
    }

    /// <summary>Requires explicit diagnostic storage outside the source checkout before loading inputs or creating output.</summary>
    /// <returns>The configured absolute external directory.</returns>
    private static string RequireLateHistoryDiagnosticRoot()
    {
        var requested = Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT");
        if (string.IsNullOrWhiteSpace(requested))
            throw new InvalidDataException("This manual diagnostic requires BOE_TEST_DIAGNOSTIC_ROOT outside Git.");
        var root = TestDiagnosticPaths.ResolveRoot(requested);
        RequireOutsideRepository(root);
        return root;
    }

    /// <summary>Rejects repository-contained paths and symbolic links before reading inputs or creating diagnostic output.</summary>
    /// <param name="path">An absolute input or output path.</param>
    private static void RequireOutsideRepository(string path)
    {
        var relative = Path.GetRelativePath(TestRepoPaths.RepoRoot, Path.GetFullPath(path));
        if (!Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("Late-History diagnostic inputs and output must remain outside the checkout.");
        if (File.Exists(path) && new FileInfo(path).LinkTarget is not null)
            throw new InvalidDataException("Late-History diagnostic inputs cannot use a symbolic link.");
        for (var directory = new DirectoryInfo(File.Exists(path) ? Path.GetDirectoryName(path)! : path);
             directory is not null; directory = directory.Parent)
            if (directory.Exists && directory.LinkTarget is not null)
                throw new InvalidDataException("Late-History diagnostics cannot use a symbolic-link ancestor.");
    }
}
