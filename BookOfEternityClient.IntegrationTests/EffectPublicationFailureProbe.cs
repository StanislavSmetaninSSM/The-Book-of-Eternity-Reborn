using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private void WriteEffectCutEvidence(string value) =>
        Assert.IsAssignableFrom<Xunit.Abstractions.ITestOutputHelper>(_directGachaOutput).WriteLine(value);

    private static void AssertEffectRollbackRaw(FileSystemManager files,
        IReadOnlyDictionary<string, string> baseline, byte[]? generationBefore,
        byte[] diagnosticBefore, Action<string> output)
    {
        var paths = baseline.Keys.Concat(new[]
        {
            EffectAcceptedTurnPlan.CommandPath, AcceptedMechanicsPlan.WoundCommandPath,
            ResourcePendingResolutionState.PendingPath,
            "output/narrative_response.json", "output/interface_updates.json"
        }).Distinct(StringComparer.Ordinal).ToArray();
        var expected = paths.ToDictionary(path => path,
            path => baseline.TryGetValue(path, out var value) ? Convert.FromBase64String(value) : null,
            StringComparer.Ordinal);
        var actual = paths.ToDictionary(path => path,
            path => CleanupPublicationCut.ReadOptional(files.ResolvePath(path)), StringComparer.Ordinal);
        var generationAfter = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        var journalAfter = CleanupPublicationCut.ReadOptional(Path.Combine(
            files.RuntimeRootPath, "trusted-local-publication-v1", "active.json"));
        var diagnosticAfter = CleanupPublicationCut.ReadOptional(files.ResolvePath(
            "game_state/control/validation_diagnostic_failure_report.json"));
        output(JsonSerializer.Serialize(new
        {
            kind = "effect-original-engine-rollback", expected, actual, generationBefore,
            generationAfter, journalAfter, diagnosticBefore, diagnosticAfter
        }));
        Assert.Null(journalAfter);
        Assert.Equal(generationBefore, generationAfter);
        Assert.Equal(diagnosticBefore, diagnosticAfter);
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private sealed class EffectPublicationFailureProbe
    {
        private FileSystemManager? _files;
        private KnownRollbackPublicationCut? _cut;
        private string? _relativePath;
        private IReadOnlyDictionary<string, byte[]?>? _before;
        private string? _committedPath;
        private string? _corruptPath;
        private byte[]? _committedJournal;
        private byte[]? _publishedBytes;
        private byte[]? _commandJournal;
        private byte[]? _commandBefore;
        private int _committedWitnesses;
        private int _commandConsumptionWitnesses;
        private static readonly byte[] CorruptBytes = Encoding.UTF8.GetBytes(
            "{\"injected\":\"post-check mismatch\"}");
        internal byte[]? GenerationBefore { get; private set; }
        private string JournalPath => Path.Combine(_files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json");

        internal void ArmAfterPublication(FileSystemManager files, string path)
        {
            _files = files;
            _relativePath = path;
            GenerationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
            _before = new Dictionary<string, byte[]?>(StringComparer.Ordinal)
            {
                [path] = CleanupPublicationCut.ReadOptional(files.ResolvePath(path))
            };
            _cut = new KnownRollbackPublicationCut(files.ResolvePath(path));
            _cut.Attach(files);
        }

        internal void ArmPostCheckCorruption(FileSystemManager files, string committedPath, string corruptPath)
        {
            _files = files;
            _committedPath = files.ResolvePath(committedPath);
            _corruptPath = files.ResolvePath(corruptPath);
            GenerationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
            _commandBefore = File.ReadAllBytes(files.ResolvePath(EffectAcceptedTurnPlan.CommandPath));
        }

        internal void Observe(TrustedLocalPublicationPhase phase, int index)
        {
            if (_cut != null)
            {
                _cut.Hooks.LocalPublicationObserver!(phase, index);
                return;
            }
            if (_committedPath == null || phase != TrustedLocalPublicationPhase.Committed) return;
            var bytes = File.ReadAllBytes(JournalPath);
            using var parsed = CleanupPublicationCut.Metadata(bytes);
            var root = parsed.RootElement;
            Assert.True(root.GetProperty("Committed").GetBoolean());
            foreach (var member in root.GetProperty("Members").EnumerateArray())
            {
                var path = member.GetProperty("Path").GetString();
                var after = member.GetProperty("After");
                if (_committedWitnesses == 0 && string.Equals(path, _committedPath,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                {
                    _publishedBytes = File.ReadAllBytes(_committedPath);
                    Assert.True(after.GetProperty("Exists").GetBoolean());
                    Assert.Equal(after.GetProperty("Sha256").GetString(),
                        Convert.ToHexString(SHA256.HashData(_publishedBytes)));
                    _committedJournal = bytes;
                    _committedWitnesses++;
                    File.WriteAllBytes(_corruptPath!, CorruptBytes);
                }
                if (string.Equals(path, _files!.ResolvePath(EffectAcceptedTurnPlan.CommandPath),
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                    !after.GetProperty("Exists").GetBoolean())
                {
                    Assert.Null(CleanupPublicationCut.ReadOptional(path!));
                    _commandJournal = bytes;
                    _commandConsumptionWitnesses++;
                }
            }
        }

        internal void AssertNormalizationFailure(Exception? failure, Action<string> output)
        {
            if (_cut != null)
            {
                // Earlier ordinary publications are committed; the explicit engine rollback below owns them.
                _cut.AssertRestored(_before!, GenerationBefore, failure, output);
                var writeFailure = Assert.IsType<CanonicalStateWriteException>(failure);
                Assert.Equal(_relativePath, writeFailure.RelativePath);
                Assert.Same(_cut.Failure, writeFailure.InnerException);
                return;
            }
            var commandAfter = CleanupPublicationCut.ReadOptional(_files!.ResolvePath(EffectAcceptedTurnPlan.CommandPath));
            var journalAfter = CleanupPublicationCut.ReadOptional(JournalPath);
            var generationAfter = CleanupPublicationCut.ReadOptional(_files.SessionGenerationPath);
            var corruptAfter = CleanupPublicationCut.ReadOptional(_corruptPath!);
            output(JsonSerializer.Serialize(new
            {
                kind = "effect-committed-post-check", _committedWitnesses, _committedJournal,
                _publishedBytes, _commandConsumptionWitnesses, _commandJournal, _commandBefore,
                commandAfter, journalAfter, GenerationBefore, generationAfter, corruptAfter,
                failure = failure?.ToString()
            }));
            Assert.Equal(1, _committedWitnesses);
            Assert.Equal(1, _commandConsumptionWitnesses);
            Assert.NotNull(_committedJournal);
            Assert.NotNull(_commandJournal);
            Assert.NotNull(_commandBefore);
            Assert.Null(commandAfter);
            Assert.Null(journalAfter);
            Assert.Equal(GenerationBefore, generationAfter);
            Assert.Equal(CorruptBytes, corruptAfter);
            var mismatch = Assert.IsType<InvalidDataException>(failure);
            Assert.Contains("validated after-image", mismatch.Message, StringComparison.Ordinal);
        }
    }
}
