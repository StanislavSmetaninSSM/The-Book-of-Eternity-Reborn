using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public async Task OrdinaryClockCommandBecomesNumericBeforeTheNextTreatmentMenu()
    {
        var diagnostics = Path.GetFullPath(TestDiagnosticPaths.Root);
        var repo = Path.GetFullPath(TestRepoPaths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (diagnostics.Equals(repo, comparison) || diagnostics.StartsWith(repo + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Ordinary clock capture requires the existing external diagnostic root.");
        var timings = new OperationTimingAccumulator();
        var window = timings.StartWindow(captureEvents: true);
        var capture = new LifecyclePassiveTimingDiagnostics("OrdinaryMortalClock-PreIntake", output, gzipRecords: true);
        DiagnosisOwnershipFixture? fixture = null;
        Exception? primary = null;
        try
        {
            fixture = await DiagnosisOwnershipFixture.CreateAsync(signedInput: false, exportDiagnosis: false,
                hooks: new FileSystemManagerHooks { PassiveOperationTimings = timings },
                timingCapture: capture, timingWindow: window,
                originalProducer: async (files, prepared) =>
                {
                    // Complete initial fixture DATA before the sole genuine original.
                    // Calendar labels are declared here, never inferred from minutes.
                    var seeded = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(
                        EffectAcceptedTurnInputComposer.WorldTimePath)))!.AsObject();
                    var clock = new JsonObject { ["year"] = 1, ["monthName"] = "Fixture month", ["dayOfMonth"] = 1,
                        ["timeOfDay"] = "08:00", ["currentTimeInMinutes"] = seeded["currentTimeInMinutes"]!.DeepClone() };
                    await files.WriteFileAtomicAsync(EffectAcceptedTurnInputComposer.WorldTimePath, clock.ToJsonString());
                    var control = await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance)
                        .BuildControlForNextTurnAsync("Mortal World");
                    await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
                    {
                        SessionId = prepared.SessionId, RequestId = prepared.RequestId, TurnNumber = prepared.TurnNumber,
                        CurrentRealm = "Mortal World", PlayerAction = "Спокойно подождать в клинике.",
                        ProgressionControl = control, PreGeneratedDices1d20 = prepared.PreGeneratedDices1d20
                    });
                });
            var fs = fixture.Files;
            Assert.True(fixture.Authority.HasCurrentAdmissionAuthority());
            Assert.False(fixture.Authority.HasOriginalMortalPreparedInput);
            var request = JsonSerializer.Deserialize<TurnRequest>(Assert.IsType<string>(await fs.ReadFileAsync(
                fixture.Lease, LiveTurnPreparationService.TurnRequestPath)), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
            Assert.Null(request.MortalDiagnosis); Assert.Null(request.MortalTreatment); Assert.Null(request.SpiritualHealing);
            var original = PendingTurnSnapshotReader.ReadCurrent(fs, fixture.Lease, [EffectAcceptedTurnInputComposer.WorldTimePath]);
            Assert.True(original.Success, JsonSerializer.Serialize(original.Issues));
            var source = original.Snapshot!;
            var originalClockBytes = source.ReadRequiredBytes(EffectAcceptedTurnInputComposer.WorldTimePath);
            var originalClock = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(originalClockBytes))!.AsObject();
            var minute = originalClock["currentTimeInMinutes"]!.GetValue<long>();
            Assert.Equal(fixture.Authority.CurrentGameMinute, minute);
            Assert.Equal(request.ProgressionControl!.CurrentWorldTimeInMinutes, minute);
            Assert.True(JsonNode.DeepEquals(source.ReadOriginalControl(), JsonSerializer.SerializeToNode(request.ProgressionControl)));
            foreach (var field in new[] { "year", "monthName", "dayOfMonth", "timeOfDay" }) Assert.NotNull(originalClock[field]);
            const int elapsed = 480;
            var expectedMinute = checked(minute + elapsed);
            var before = fixture.CaptureJson();
            await fs.WriteFileAtomicAsync(fixture.Lease, EffectAcceptedTurnInputComposer.WorldTimePath,
                new JsonObject { ["timeChange"] = elapsed }.ToJsonString());
            var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
            IReadOnlyList<ValidationIssue> issues;
            using (timings.Measure("OrdinaryClock.RawAdmission"))
                issues = await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(fixture.Lease);
            var after = fixture.CaptureJson();
            Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
            var clockPath = fs.ResolvePath(EffectAcceptedTurnInputComposer.WorldTimePath);
            foreach (var pair in before.Where(pair => pair.Key != clockPath)) Assert.Equal(pair.Value, after[pair.Key]);
            var freshOriginal = PendingTurnSnapshotReader.ReadCurrent(fs, fixture.Lease, [EffectAcceptedTurnInputComposer.WorldTimePath]);
            Assert.True(freshOriginal.Success, JsonSerializer.Serialize(freshOriginal.Issues));
            Assert.Equal(originalClockBytes, freshOriginal.Snapshot!.ReadRequiredBytes(EffectAcceptedTurnInputComposer.WorldTimePath));
            Assert.True(JsonNode.DeepEquals(source.ReadOriginalControl(), freshOriginal.Snapshot.ReadOriginalControl()));
            var current = JsonNode.Parse(Assert.IsType<string>(await fs.ReadFileAsync(fixture.Lease,
                EffectAcceptedTurnInputComposer.WorldTimePath)))!.AsObject();
            capture.WriteLine(JsonSerializer.Serialize(new { originalMinute = minute, elapsed, expectedMinute,
                originalClock = originalClock.ToJsonString(), currentClock = current.ToJsonString(), issues }));
            // The raw-admission baseline must reach this exact missing-numeric
            // oracle. Any unrelated refusal/setup failure is not causal RED.
            Assert.True(current["currentTimeInMinutes"] is JsonValue,
                "Ordinary timeChange must become canonical numeric time before the next treatment reads it.");
            Assert.Equal(expectedMinute, current["currentTimeInMinutes"]!.GetValue<long>());
            Assert.False(current.ContainsKey("timeChange")); Assert.False(current.ContainsKey("setWorldTime"));
            foreach (var field in new[] { "year", "monthName", "dayOfMonth", "timeOfDay" })
                Assert.True(JsonNode.DeepEquals(originalClock[field], current[field]));
            Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
            var once = await fs.ReadFileBytesAsync(fixture.Lease, EffectAcceptedTurnInputComposer.WorldTimePath);
            using (timings.Measure("OrdinaryClock.RepeatedAdmission"))
                Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(fixture.Lease),
                    issue => issue.Severity == IssueSeverity.Error);
            Assert.Equal(once, await fs.ReadFileBytesAsync(fixture.Lease, EffectAcceptedTurnInputComposer.WorldTimePath));
            await fixture.ReleaseLeaseAsync();
            try
            {
                var scheduler = new ProgressionScheduleService(fs, NullLogger<ProgressionScheduleService>.Instance);
                using (timings.Measure("OrdinaryClock.ProgressionPublication"))
                    await scheduler.ApplyAcceptedTurnOutcomeAsync(request.ProgressionControl);
                for (var pass = 0; pass < 2; pass++)
                    Assert.Equal(expectedMinute, (await scheduler.BuildControlForNextTurnAsync("Mortal World")).CurrentWorldTimeInMinutes);
                using (timings.Measure("OrdinaryClock.NextMenu"))
                    Assert.IsType<WoundCommandState>((await WoundApplicationService.OpenAsync(fs, "/лечить", treatment: true)).WoundCommandState);
                Assert.Equal(once, await fs.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath));
            }
            finally { await fixture.AcquireLeaseAsync(); }
        }
        catch (Exception error) { primary = error; }
        finally
        {
            if (fixture is not null)
                using (timings.Measure("OrdinaryClock.FixtureClosing"))
                {
                    try { await fixture.DisposeAsync(); }
                    catch (Exception error) { primary ??= error; }
                }
            await capture.CompleteAsync(primary);
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }
}
