using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    private sealed partial class Fixture
    {
        /// <summary>
        /// Checks three serialized Beginning scopes against the real busy-owner guard before candidate measurement.
        /// The middle scope deliberately throws after its refusals; every scope must remain unissued and end its owner.
        /// Issued, Staged, live joint-attempt and custody behavior are outside this helper's coverage.
        /// </summary>
        /// <param name="original">
        /// The genuine current original used to authenticate each private Beginning scope.
        /// </param>
        /// <param name="bundle">
        /// The genuinely completed joint wound stages from the fixture's normal preparation.
        /// </param>
        /// <param name="continuation">
        /// The actual joint continuation retained by the fixture's normal preparation.
        /// </param>
        /// <returns>
        /// A task completing after four real refusals per scope, monotonic ordinals and unchanged files and generation.
        /// Failed or uncertain verification retains this fixture's owned container.
        /// </returns>
        private async Task AssertBeginningOwnerRefusalsAsync(GameEngine.JointComponentOriginal original,
            AcceptedMechanicsWoundStageBundle bundle, object continuation)
        {
            using var total = Files.MeasureOperation("JointBundleFixture.BeginningOwnerRefusals");
            try
            {
                var before = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var path in Files.EnumerateFiles(Lease, "*").OrderBy(path => path, StringComparer.Ordinal))
                    before.Add(path, await Files.ReadFileBytesAsync(Lease, path) ??
                        throw new InvalidDataException("A Beginning probe baseline disappeared: " + path));
                var generation = Files.ReadLocalGenerationSnapshot(Lease);
                Assert.Equal(original.Generation, generation.Binding.Id);
                Assert.NotNull(generation.Bytes);

                var first = await Runtime.Engine.ProbeBeginningOwnerRefusalsForComponentAsync(
                    Lease, original, bundle, continuation);
                _output.WriteLine(JsonSerializer.Serialize(new
                {
                    Name = "JointBundleFixture.BeginningOwnerReport", Phase = "FirstSuccess", Report = first
                }));
                await AssertBeginningProbeBytesUnchangedAsync(before, generation, "FirstSuccess");

                var deliberate = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    try
                    {
                        await Runtime.Engine.ProbeBeginningOwnerRefusalsForComponentAsync(
                            Lease, original, bundle, continuation, throwAfterProbes: true);
                    }
                    catch (CoordinatedStatePublicationUncertainException)
                    {
                        _retained = true;
                        throw;
                    }
                });
                var middleData = deliberate.Data[nameof(GameEngine.BeginningOwnerComponentRefusalReport)];
                _output.WriteLine(JsonSerializer.Serialize(new
                {
                    Name = "JointBundleFixture.BeginningOwnerDeliberateFailure", Failure = deliberate.ToString(),
                    Report = middleData, FinallyFailure = deliberate.Data["BeginningOwnerProbeFinallyFailure"]?.ToString()
                }));
                Assert.Equal("Deliberate Beginning-owner component probe failure.", deliberate.Message);
                Assert.False(deliberate.Data.Contains("BeginningOwnerProbeFinallyFailure"));
                var middle = Assert.IsType<GameEngine.BeginningOwnerComponentRefusalReport>(middleData);
                await AssertBeginningProbeBytesUnchangedAsync(before, generation, "DeliberateFailure");

                var last = await Runtime.Engine.ProbeBeginningOwnerRefusalsForComponentAsync(
                    Lease, original, bundle, continuation);
                _output.WriteLine(JsonSerializer.Serialize(new
                {
                    Name = "JointBundleFixture.BeginningOwnerReport", Phase = "LastSuccess", Report = last
                }));
                await AssertBeginningProbeBytesUnchangedAsync(before, generation, "LastSuccess");

                var expectedRefusals = new[]
                {
                    nameof(GameEngine.PrepareOrdinaryOriginalForComponentAsync),
                    nameof(GameEngine.CreateJointMechanicalAdmissionForComponentAsync),
                    nameof(GameEngine.CaptureJointProgressionForComponentAsync),
                    nameof(GameEngine.ProbeBeginningOwnerRefusalsForComponentAsync)
                };
                foreach (var report in new[] { first, middle, last })
                {
                    Assert.Equal(expectedRefusals, report.Refusals);
                    Assert.True(report.NoAuthorityIssued);
                    Assert.True(report.Ended);
                    Assert.True(report.ReadyUnchanged);
                }
                Assert.True(first.Ordinal > 0);
                Assert.Equal(first.Ordinal + 1, middle.Ordinal);
                Assert.Equal(first.Ordinal + 2, last.Ordinal);
                _output.WriteLine(JsonSerializer.Serialize(new
                {
                    Name = "JointBundleFixture.BeginningOwnerRefusalsComplete", First = first, Middle = middle, Last = last,
                    CoveredPhase = "Actual unissued Beginning owner only",
                    Outstanding = new[] { "Issued", "Staged", "LiveJointAttempt", "Custody" }
                }));
            }
            catch
            {
                _retained = true;
                throw;
            }
        }

        /// <summary>
        /// Compares every canonical file and the physical generation with the captured pre-probe bytes.
        /// </summary>
        /// <param name="before">
        /// The exact canonical file inventory and bytes captured before the first Beginning scope.
        /// </param>
        /// <param name="generation">
        /// The actual physical generation binding and bytes captured before the first scope.
        /// </param>
        /// <param name="phase">
        /// The completed success or deliberate-failure phase used in the complete comparison diagnostics.
        /// </param>
        /// <returns>
        /// A task completing after all inventory, file-byte and generation checks for this phase.
        /// </returns>
        private async Task AssertBeginningProbeBytesUnchangedAsync(IReadOnlyDictionary<string, byte[]> before,
            LocalSessionGenerationSnapshot generation, string phase)
        {
            var actualPaths = Files.EnumerateFiles(Lease, "*").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Name = "JointBundleFixture.BeginningOwnerInventory", Phase = phase,
                Before = before.Keys.ToArray(), After = actualPaths
            }));
            Assert.Equal(before.Keys.ToArray(), actualPaths);
            foreach (var row in before)
            {
                var actual = await Files.ReadFileBytesAsync(Lease, row.Key);
                _output.WriteLine(JsonSerializer.Serialize(new
                {
                    Name = "JointBundleFixture.BeginningOwnerFileBytes", Phase = phase, Path = row.Key,
                    BeforeBytes = row.Value.Length, AfterBytes = actual?.Length,
                    BeforeHash = Convert.ToHexString(SHA256.HashData(row.Value)),
                    AfterHash = actual is null ? null : Convert.ToHexString(SHA256.HashData(actual))
                }));
                Assert.Equal(row.Value, actual);
            }
            var current = Files.ReadLocalGenerationSnapshot(Lease);
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Name = "JointBundleFixture.BeginningOwnerGeneration", Phase = phase,
                Before = generation.Binding, After = current.Binding,
                BeforeHash = generation.Bytes is null ? null : Convert.ToHexString(SHA256.HashData(generation.Bytes)),
                AfterHash = current.Bytes is null ? null : Convert.ToHexString(SHA256.HashData(current.Bytes))
            }));
            Assert.Equal(generation.Binding, current.Binding);
            Assert.Equal(generation.Bytes, current.Bytes);
        }
    }
}
