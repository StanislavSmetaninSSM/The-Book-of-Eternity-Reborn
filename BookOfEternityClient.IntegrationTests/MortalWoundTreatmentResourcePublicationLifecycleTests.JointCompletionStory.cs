using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("story_prefix_changed")]
    [InlineData("story_row_changed")]
    [InlineData("story_row_generation")]
    [InlineData("story_append_failure")]
    [InlineData("story_readback_failure")]
    public Task GuaranteedTreatment_StoryOwnerFailureRetainsAppliedUnionAndOriginal(string scenario) =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true, refusalScenario: scenario);

    private sealed class JointStoryControlProbe
    {
        private const string AppendFailure = "joint story actual append boundary test failure";
        private const string ReadbackFailure = "joint story actual post-commit readback test failure";
        private readonly string _scenario;
        private FileSystemManager? _files;
        private bool _armed, _readbackArmed;
        private string? _sessionId, _requestId, _generation, _preparedAtUtc, _scene;
        private byte[]? _originalPrefix;
        internal byte[]? ExpectedStoryAfter { get; private set; }
        private byte[]? _committedStory, _committedJournal;
        private string? _actualRow, _observerFailure;
        private int _appendBoundaryCalls, _storyCommits, _readbackFaults;

        internal JointStoryControlProbe(string scenario)
        {
            _scenario = scenario;
            Hooks = new FileSystemManagerHooks
            {
                BeforeCanonicalMutationAsync = BeforeAppendAsync,
                LocalPublicationObserver = ObserveCommittedStory,
                AfterCanonicalReadInitialValidationAsync = AfterReadAsync
            };
        }
        internal FileSystemManagerHooks Hooks { get; }
        internal void Attach(FileSystemManager files) => _files = files;
        internal void Arm(IReadOnlyDictionary<string, byte[]?> before, PendingTurnSnapshotReadAuthority original, string generation)
        {
            Assert.False(_armed); Assert.NotNull(_files);
            _sessionId = original.SessionId; _requestId = original.RequestId; _generation = generation;
            _originalPrefix = Assert.IsType<byte[]>(before[QteStoryPath]);
            _scene = ParseJsonObjectBytes(Assert.IsType<byte[]>(before["output/narrative_response.json"]))["response"]!.GetValue<string>();
            var marker = ParseJsonObjectBytes(Assert.IsType<byte[]>(before[MortalQteDecisionPublication.StatePath]));
            Assert.Equal("applied", marker["progression"]!["phase"]!.GetValue<string>());
            _preparedAtUtc = marker["progression"]!["preparedAtUtc"]!.GetValue<string>();
            ExpectedStoryAfter = _originalPrefix;
            _armed = true;
        }
        internal async Task AlterOnlyOriginalPrefixAsync()
        {
            Assert.Equal("story_prefix_changed", _scenario);
            // Alter genuine prior42 bytes; no decision43 story row is authored.
            ExpectedStoryAfter = _originalPrefix!.Concat(Encoding.UTF8.GetBytes(" ")).ToArray();
            await File.WriteAllBytesAsync(_files!.ResolvePath(QteStoryPath), ExpectedStoryAfter);
        }
        private Task BeforeAppendAsync(string path)
        {
            if (!_armed || path != QteStoryPath) return Task.CompletedTask;
            _appendBoundaryCalls++;
            if (_scenario == "story_append_failure")
                throw new InvalidDataException(AppendFailure);
            return Task.CompletedTask;
        }
        private void ObserveCommittedStory(TrustedLocalPublicationPhase phase, int index)
        {
            if (!_armed || phase != TrustedLocalPublicationPhase.Committed || _observerFailure is not null) return;
            try
            {
                var bytes = File.ReadAllBytes(Path.Combine(_files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json"));
                using var journal = CleanupPublicationCut.Metadata(bytes);
                var members = journal.RootElement.GetProperty("Members").EnumerateArray().ToArray();
                if (members.Length != 1 || members[0].GetProperty("Path").GetString() != _files.ResolvePath(QteStoryPath)) return;
                Assert.Equal(1, journal.RootElement.GetProperty("Format").GetInt32());
                Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
                Assert.Equal(_originalPrefix, members[0].GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64());
                var actual = members[0].GetProperty("After").GetProperty("Bytes").GetBytesFromBase64();
                Assert.Equal(actual, File.ReadAllBytes(_files.ResolvePath(QteStoryPath)));
                Assert.Equal(members[0].GetProperty("After").GetProperty("Sha256").GetString(),
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(actual)));
                Assert.True(actual.AsSpan().StartsWith(_originalPrefix));
                var suffix = Encoding.UTF8.GetString(actual.AsSpan(_originalPrefix!.Length));
                _actualRow = Assert.Single(suffix.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                var row = StrictJsonAuthority.Deserialize<StoryEntry>(_actualRow,
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual committed joint story row");
                Assert.NotNull(row); Assert.Equal(43, row!.Turn); Assert.Equal(_requestId, row.RequestId);
                Assert.Equal(_generation, row.SessionGeneration); Assert.Equal(_preparedAtUtc, row.Timestamp);
                Assert.Equal("Осмотреть ушиб.", row.Player);
                Assert.Equal(_scene, row.Narrative);
                Assert.Equal(0, _storyCommits++);
                _committedJournal = bytes; _committedStory = actual;
                ExpectedStoryAfter = actual;
                if (_scenario is "story_row_changed" or "story_row_generation")
                {
                    // Derive negative data from the actual trusted writer After;
                    // this never fabricates an accepted row or proof/capability.
                    var changed = ParseJsonObjectBytes(Encoding.UTF8.GetBytes(_actualRow));
                    if (_scenario == "story_row_changed")
                        changed["narrative"] = changed["narrative"]!.GetValue<string>() + " Изменённая строка.";
                    else changed["sessionGeneration"] = _generation + "_changed_row_binding";
                    ExpectedStoryAfter = _originalPrefix.Concat(Encoding.UTF8.GetBytes(
                        changed.ToJsonString(new JsonSerializerOptions(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
                            { WriteIndented = false }) + "\n")).ToArray();
                    Assert.False(actual.AsSpan().SequenceEqual(ExpectedStoryAfter));
                    File.WriteAllBytes(_files.ResolvePath(QteStoryPath), ExpectedStoryAfter);
                }
                if (_scenario == "story_readback_failure") _readbackArmed = true;
            }
            catch (Exception error) { _observerFailure = error.ToString(); }
        }
        private Task AfterReadAsync(string path)
        {
            if (!_readbackArmed || path != QteStoryPath) return Task.CompletedTask;
            _readbackArmed = false; _readbackFaults++;
            throw new InvalidDataException(ReadbackFailure);
        }
        internal object Evidence() => new
        {
            Scenario = _scenario, SessionId = _sessionId, RequestId = _requestId, Generation = _generation,
            PreparedAtUtc = _preparedAtUtc, OriginalPrefix = _originalPrefix, ActualWriterRow = _actualRow,
            ActualCommittedStory = _committedStory, ExpectedStoryAfter, ActualCommittedJournal = _committedJournal,
            AppendBoundaryCalls = _appendBoundaryCalls, StoryCommits = _storyCommits, ReadbackFaults = _readbackFaults,
            ObserverFailure = _observerFailure
        };
        internal void AssertOutcome(IReadOnlyDictionary<string, byte[]?> before,
            IReadOnlyDictionary<string, byte[]?> after, string actualEngineDiagnostics)
        {
            Assert.Null(_observerFailure);
            var expected = before.ToDictionary(row => row.Key, row => row.Value?.ToArray(), StringComparer.Ordinal);
            expected[QteStoryPath] = ExpectedStoryAfter;
            AssertJointGenerationRefusalRawImagesEqual(expected, after);
            Assert.Contains(_scenario switch
            {
                "story_append_failure" => AppendFailure,
                "story_readback_failure" => ReadbackFailure,
                _ => "joint_story_suffix_not_exact"
            }, actualEngineDiagnostics, StringComparison.Ordinal);
            if (_scenario == "story_prefix_changed")
            {
                Assert.Equal(0, _appendBoundaryCalls); Assert.Equal(0, _storyCommits); Assert.Equal(0, _readbackFaults);
            }
            else if (_scenario == "story_append_failure")
            {
                Assert.Equal(1, _appendBoundaryCalls); Assert.Equal(0, _storyCommits); Assert.Equal(0, _readbackFaults);
                Assert.Equal(_originalPrefix, after[QteStoryPath]);
            }
            else
            {
                Assert.Equal(1, _appendBoundaryCalls); Assert.Equal(1, _storyCommits); Assert.NotNull(_committedStory);
                Assert.Equal(_scenario == "story_readback_failure" ? 1 : 0, _readbackFaults);
            }
        }
    }
}
