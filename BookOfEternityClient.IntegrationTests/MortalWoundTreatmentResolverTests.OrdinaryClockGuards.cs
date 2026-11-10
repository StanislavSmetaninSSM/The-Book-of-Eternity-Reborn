using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public async Task OrdinaryClockRejectsMalformedAndCompetingCommandsWithoutMutation()
    {
        await RunOrdinaryClockGuardAsync("Malformed", async (fixture, capture) =>
        {
            var fs = fixture.Files;
            var badCommands = new[]
            {
                "{\"timeChange\":-1}", "{\"timeChange\":\"480\"}",
                "{\"timeChange\":2147483647}", "{\"timeChange\":1.5}",
                "{\"timeChange\":480,\"currentTimeInMinutes\":1740}",
                "{\"timeChange\":480,\"setWorldTime\":{\"year\":1,\"monthName\":\"Fixture month\",\"dayOfMonth\":1,\"timeOfDay\":\"16:00\",\"currentTimeInMinutes\":1740}}",
                "{\"timeChange\":480,\"unexpectedClockField\":true}",
                "{\"timeChange\":480,\"TimeChange\":1}"
            };
            foreach (var command in badCommands)
            {
                await fs.WriteFileAtomicAsync(fixture.Lease, EffectAcceptedTurnInputComposer.WorldTimePath, command);
                var before = fixture.CaptureJson();
                var issues = await new ValidationService(fs, NullLogger<ValidationService>.Instance)
                    .ValidateAcceptedTurnRawResourceMaterializationAsync(fixture.Lease);
                Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
                fixture.AssertJson(before);
                capture.WriteLine(JsonSerializer.Serialize(new { command, issues }));
            }
            // Existing null means no incremental command, including beside an
            // absolute command owned by another path. Do not invent its result.
            foreach (var command in new[] { "{\"timeChange\":null}",
                "{\"timeChange\":null,\"setWorldTime\":{\"year\":1,\"monthName\":\"Fixture month\",\"dayOfMonth\":1,\"timeOfDay\":\"16:00\",\"currentTimeInMinutes\":1740}}" })
            {
                await fs.WriteFileAtomicAsync(fixture.Lease, EffectAcceptedTurnInputComposer.WorldTimePath, command);
                var before = fixture.CaptureJson();
                var issues = await new ValidationService(fs, NullLogger<ValidationService>.Instance)
                    .ValidateAcceptedTurnRawResourceMaterializationAsync(fixture.Lease);
                Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
                fixture.AssertJson(before);
            }
        });
    }

    [Fact]
    public async Task OrdinaryClockRejectsChangedOriginalOrCurrentAuthorityWithoutMutation()
    {
        await RunOrdinaryClockGuardAsync("Sources", async (fixture, capture) =>
        {
            var fs = fixture.Files;
            var manifest = JsonNode.Parse(Assert.IsType<string>(await fs.ReadFileAsync(fixture.Lease,
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath)))!.AsObject();
            var originalPath = manifest["files"]![EffectAcceptedTurnInputComposer.WorldTimePath]!.GetValue<string>();
            var original = Assert.IsType<byte[]>(await fs.ReadFileBytesAsync(fixture.Lease, originalPath));
            var requestPath = LiveTurnPreparationService.TurnRequestPath;
            var requestBytes = Assert.IsType<byte[]>(await fs.ReadFileBytesAsync(fixture.Lease, requestPath));
            var authorityBytes = Assert.IsType<byte[]>(await fs.ReadFileBytesAsync(fixture.Lease, PendingTurnSnapshotAuthority.AuthorityPath));
            var request = JsonNode.Parse(FileSystemManager.DecodeFileBytes(requestBytes))!.AsObject();
            var wrongControl = request.DeepClone().AsObject();
            wrongControl["progressionControl"]!["currentWorldTimeInMinutes"] = 999;
            var wrongRequest = request.DeepClone().AsObject(); wrongRequest["requestId"] = "stale-clock-request";
            var mutations = new (string Label, string Path, byte[]? Bytes)[]
            {
                ("original-drift", originalPath, FileSystemManager.EncodeUtf8WithPreamble("{\"currentTimeInMinutes\":1261}")),
                ("original-absent", originalPath, null),
                ("control-drift", requestPath, FileSystemManager.EncodeUtf8WithPreamble(wrongControl.ToJsonString())),
                ("request-drift", requestPath, FileSystemManager.EncodeUtf8WithPreamble(wrongRequest.ToJsonString())),
                ("authority-absent", PendingTurnSnapshotAuthority.AuthorityPath, null)
            };
            foreach (var mutation in mutations)
            {
                await fs.WriteFileAtomicAsync(fixture.Lease, EffectAcceptedTurnInputComposer.WorldTimePath, "{\"timeChange\":480}");
                // Deliberate external corruption, never an authored accepted state.
                if (mutation.Bytes is null) File.Delete(fs.ResolvePath(mutation.Path));
                else await File.WriteAllBytesAsync(fs.ResolvePath(mutation.Path), mutation.Bytes);
                var before = fixture.CaptureJson();
                try
                {
                    var issues = await new ValidationService(fs, NullLogger<ValidationService>.Instance)
                        .ValidateAcceptedTurnRawResourceMaterializationAsync(fixture.Lease);
                    Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
                    fixture.AssertJson(before);
                    capture.WriteLine(JsonSerializer.Serialize(new { mutation.Label, issues }));
                }
                finally
                {
                    await File.WriteAllBytesAsync(fs.ResolvePath(originalPath), original);
                    await File.WriteAllBytesAsync(fs.ResolvePath(requestPath), requestBytes);
                    await File.WriteAllBytesAsync(fs.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath), authorityBytes);
                }
            }
        });
    }

    [Theory]
    [InlineData("before-publish")]
    [InlineData("after-commit")]
    [InlineData("foreign-clock")]
    public async Task OrdinaryClockPublicationFailurePreservesExactOwnership(string fault)
    {
        var armed = false; var committed = false; var fired = false;
        FileSystemManager? actual = null;
        var injected = new IOException("Injected ordinary clock " + fault);
        var foreign = FileSystemManager.EncodeUtf8WithPreamble("{\"foreignClock\":true}");
        await RunOrdinaryClockGuardAsync("Publication-" + fault, async (fixture, capture) =>
        {
            actual = fixture.Files;
            await actual.WriteFileAtomicAsync(fixture.Lease, EffectAcceptedTurnInputComposer.WorldTimePath, "{\"timeChange\":480}");
            var before = fixture.CaptureJson();
            armed = true;
            var validator = new ValidationService(actual, NullLogger<ValidationService>.Instance);
            var error = await Record.ExceptionAsync(() => validator.ValidateAcceptedTurnRawResourceMaterializationAsync(fixture.Lease));
            armed = false;
            Assert.True(fired);
            if (fault == "foreign-clock")
            {
                Assert.IsType<CoordinatedStatePublicationUncertainException>(error);
                var after = fixture.CaptureJson();
                Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
                foreach (var pair in before)
                    Assert.Equal(pair.Key == actual.ResolvePath(EffectAcceptedTurnInputComposer.WorldTimePath) ? foreign : pair.Value, after[pair.Key]);
                capture.WriteLine("Foreign after-image preserved; no speculative compensation or retry.");
            }
            else
            {
                Assert.Same(injected, error);
                fixture.AssertJson(before);
                if (fault == "after-commit") Assert.True(committed);
                // A known restored failure permits the same original command once.
                Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(fixture.Lease),
                    issue => issue.Severity == IssueSeverity.Error);
                var clock = JsonNode.Parse(Assert.IsType<string>(await actual.ReadFileAsync(fixture.Lease,
                    EffectAcceptedTurnInputComposer.WorldTimePath)))!.AsObject();
                Assert.Equal(1740, clock["currentTimeInMinutes"]!.GetValue<int>());
            }
        }, timings => new FileSystemManagerHooks
        {
            PassiveOperationTimings = timings,
            LocalPublicationObserver = (phase, _) => { if (armed && phase == TrustedLocalPublicationPhase.Committed) committed = true; },
            BeforeCanonicalMutationBoundaryAsync = path =>
            {
                if (armed && !fired && fault == "before-publish" && path == EffectAcceptedTurnInputComposer.WorldTimePath)
                { fired = true; throw injected; }
                return Task.CompletedTask;
            },
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && committed && !fired)
                {
                    fired = true;
                    if (fault == "foreign-clock") File.WriteAllBytes(actual!.ResolvePath(EffectAcceptedTurnInputComposer.WorldTimePath), foreign);
                    else throw injected;
                }
                return Task.CompletedTask;
            }
        });
    }

    private async Task RunOrdinaryClockGuardAsync(string name,
        Func<DiagnosisOwnershipFixture, LifecyclePassiveTimingDiagnostics, Task> body,
        Func<OperationTimingAccumulator, FileSystemManagerHooks>? hooks = null)
    {
        var timings = new OperationTimingAccumulator();
        var window = timings.StartWindow(captureEvents: true);
        var capture = new LifecyclePassiveTimingDiagnostics("OrdinaryMortalClock-" + name, output, gzipRecords: true);
        DiagnosisOwnershipFixture? fixture = null; Exception? primary = null;
        try
        {
            fixture = await DiagnosisOwnershipFixture.CreateAsync(signedInput: false, exportDiagnosis: false,
                hooks: hooks?.Invoke(timings) ?? new FileSystemManagerHooks { PassiveOperationTimings = timings },
                timingCapture: capture, timingWindow: window, originalProducer: ProduceOrdinaryClockOriginalAsync);
            using (timings.Measure("OrdinaryClock.GuardBody")) await body(fixture, capture);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            if (fixture is not null)
                using (timings.Measure("OrdinaryClock.FixtureClosing"))
                    try { await fixture.DisposeAsync(); } catch (Exception error) { primary ??= error; }
            await capture.CompleteAsync(primary);
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }
}
