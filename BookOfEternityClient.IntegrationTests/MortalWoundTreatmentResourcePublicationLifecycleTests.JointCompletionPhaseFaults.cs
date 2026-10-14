using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public Task GuaranteedTreatment_PhaseKnownReadbackFailureCompensatesOnlyThreeMembersAndRetainsMechanics() =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseUnappliedForCold: true, refusalScenario: "phase_known_compensation");

    [Fact]
    public Task GuaranteedTreatment_PhaseForeignMemberUncertaintyRetainsDebtAndCompleteMechanicalUnion() =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseUnappliedForCold: true, refusalScenario: "phase_uncertain_debt");

    private sealed class JointPhaseFaultProbe : IDisposable
    {
        private readonly bool _uncertain;
        private readonly CleanupPublicationCut? _cut;
        private FileSystemManager? _files;
        private bool _armed, _readArmed;
        private int _readFailures, _phaseCommits, _compensationCommits;
        private readonly List<byte[]> _committedJournals = [];

        internal JointPhaseFaultProbe(bool uncertain)
        {
            _uncertain = uncertain;
            if (uncertain)
            {
                _cut = new CleanupPublicationCut();
                _cut.Select = (target, _) => _armed && Path.GetRelativePath(_files!.GameSessionPath, target).Replace('\\', '/') ==
                    ProgressionScheduleService.SchedulePath && IsActualThreeMemberPhase();
                Hooks = _cut.Hooks;
            }
            else
                Hooks = new FileSystemManagerHooks
                {
                    LocalPublicationObserver = ObserveCommitted,
                    AfterCanonicalReadInitialValidationAsync = ReadAfterCommitAsync
                };
        }

        internal FileSystemManagerHooks Hooks { get; }
        private string JournalPath => Path.Combine(_files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        internal void Attach(FileSystemManager files) { _files = files; _cut?.Attach(files); }
        internal void Arm() { _armed = true; if (_cut is not null) _cut.Armed = true; }

        private bool IsActualThreeMemberPhase()
        {
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(JournalPath));
            var members = journal.RootElement.GetProperty("Members").EnumerateArray().ToArray();
            var paths = members.Select(member => Path.GetRelativePath(_files!.GameSessionPath,
                member.GetProperty("Path").GetString()!).Replace('\\', '/')).ToArray();
            return members.Length == 3 && paths.ToHashSet(StringComparer.Ordinal).SetEquals(new[]
            { ProgressionScheduleService.SchedulePath, ProgressionScheduleService.ReportPath, MortalQteDecisionPublication.StatePath });
        }

        private void ObserveCommitted(TrustedLocalPublicationPhase phase, int index)
        {
            if (!_armed || phase != TrustedLocalPublicationPhase.Committed || !IsActualThreeMemberPhase()) return;
            var journal = File.ReadAllBytes(JournalPath);
            using var metadata = CleanupPublicationCut.Metadata(journal);
            Assert.True(metadata.RootElement.GetProperty("Committed").GetBoolean());
            _committedJournals.Add(journal); _phaseCommits++;
            var marker = ParseJsonObjectBytes(File.ReadAllBytes(_files!.ResolvePath(MortalQteDecisionPublication.StatePath)));
            var phaseName = marker["progression"]!["phase"]!.GetValue<string>();
            if (phaseName == "applied" && _phaseCommits == 1) _readArmed = true;
            else { Assert.Equal("unapplied", phaseName); _compensationCommits++; }
        }

        private Task ReadAfterCommitAsync(string path)
        {
            if (_readArmed && _readFailures == 0 && path == ProgressionScheduleService.SchedulePath)
            {
                _readFailures++;
                throw new InvalidDataException("joint phase post-commit readback test failure");
            }
            return Task.CompletedTask;
        }

        internal void AssertOutcome(IReadOnlyDictionary<string, byte[]?> before, IReadOnlyDictionary<string, byte[]?> after)
        {
            if (!_uncertain)
            {
                Assert.Equal(1, _readFailures); Assert.Equal(2, _phaseCommits); Assert.Equal(1, _compensationCommits);
                AssertJointGenerationRefusalRawImagesEqual(before, after); // Entire cohort/union restored; no mechanics rearm.
                Assert.False(File.Exists(JournalPath));
                var marker = ParseJsonObjectBytes(after[MortalQteDecisionPublication.StatePath]!);
                Assert.Equal("unapplied", marker["progression"]!["phase"]!.GetValue<string>());
                Assert.Equal(before[ProgressionScheduleService.ReportPath], after[ProgressionScheduleService.ReportPath]);
                return;
            }
            Assert.Equal(1, _cut!.Cuts); Assert.Equal(0, _cut.Index);
            Assert.Equal(_files!.ResolvePath(ProgressionScheduleService.SchedulePath), _cut.Target);
            Assert.Equal(_cut.JournalAtCut, File.ReadAllBytes(JournalPath));
            using var retained = CleanupPublicationCut.Metadata(File.ReadAllBytes(JournalPath));
            Assert.False(retained.RootElement.GetProperty("Committed").GetBoolean());
            Assert.True(IsActualThreeMemberPhase());
            Assert.Equal(CleanupPublicationCut.Foreign, after[ProgressionScheduleService.SchedulePath]);
            Assert.Equal(before.Keys.Where(path => !path.StartsWith('$')).OrderBy(path => path, StringComparer.Ordinal),
                after.Keys.Where(path => !path.StartsWith('$')).OrderBy(path => path, StringComparer.Ordinal));
            foreach (var pair in before.Where(pair => !pair.Key.StartsWith('$') && pair.Key != ProgressionScheduleService.SchedulePath))
                Assert.Equal(pair.Value, after[pair.Key]);
            Assert.Equal(before["$sessionGeneration"], after["$sessionGeneration"]);
            foreach (var pair in before.Where(pair => pair.Key.StartsWith("$runtime/", StringComparison.Ordinal)))
                Assert.Equal(pair.Value, after[pair.Key]);
            foreach (var path in after.Keys.Except(before.Keys, StringComparer.Ordinal))
                Assert.StartsWith("$runtime/trusted-local-publication-v1/", path);
            Assert.Equal(before["$gameSessionDirectories"], after["$gameSessionDirectories"]);
        }

        internal object Evidence() => new
        {
            UncertainCase = _uncertain, ReadFailures = _readFailures, PhaseCommits = _phaseCommits,
            CompensationCommits = _compensationCommits, CommittedJournals = _committedJournals,
            Cut = _cut?.Evidence()
        };
        public void Dispose() => _cut?.Dispose();
    }
}
