using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    private sealed partial class Fixture
    {
        /// <summary>
        /// Verifies the retained admission tree and exact seal/resource inventory without per-copy private byte hashing.
        /// Checks initial constructor hashing separately after all existing semantic, refusal, restoration and abort controls.
        /// </summary>
        /// <param name="range">
        /// Half-open offsets returned by the existing direct-copy helper; no additional copy is performed.
        /// </param>
        internal void AssertJointAdmissionTimingTree((int Start, int End) range)
        {
            var rows = ReadAdmissionTimingRange(range);
            string[] required =
            [
                "JointContinuation.ValidateSeal",
                "JointContinuation.ComputeFingerprint",
                "JointPreparation.PreparedAgrees",
                "JointPreparation.Replay",
                "JointEffectInput.Compose",
                "JointEffectInput.Build",
                "WoundPreparation.PrepareCore",
                "WoundPreparation.ValidateInput",
                "JointFinalAdmission.Baseline",
                "TreatmentResources.Finalize"
            ];
            var missing = required.Where(name => !rows.Any(row => row.Name == name && row.Boundary == "begin")).ToArray();
            Assert.True(missing.Length == 0, "Missing required joint admission timing names: " + string.Join(", ", missing));
            foreach (var name in required)
            {
                var began = rows.Count(row => row.Name == name && row.Boundary == "begin");
                var ended = rows.Count(row => row.Name == name && row.Boundary == "end");
                Assert.True(began > 0, name);
                Assert.Equal(began, ended);
            }

            var nodes = ReadStrictAdmissionTree(rows);
            string[][] chains =
            [
                ["JointPreparation.PreparedAgrees", "JointPreparation.Replay", "WoundPreparation.PrepareCore", "WoundPreparation.ValidateInput"],
                ["JointEffectInput.Compose", "JointEffectInput.Build"],
                ["WoundPlanner.JointFinalAdmission", "JointFinalAdmission.Baseline"]
            ];
            foreach (var chain in chains)
                Assert.True(nodes.Any(node => HasAdmissionTimingChain(node, chain, 0)),
                    "Missing nested timing chain: " + string.Join(" > ", chain));
            Assert.True(nodes.Any(node =>
                node.Name == "JointContinuation.ValidateSeal" &&
                node.Children.Any(child => child.Name == "TreatmentResources.Finalize") &&
                HasAdmissionTimingChain(node,
                    ["JointContinuation.ValidateSeal", "JointContinuation.ComputeFingerprint"], 0)),
                "One continuation seal must contain resource finalization and the freshly assembled final fingerprint.");
            var sealBegins = rows.Count(row => row.Name == "JointContinuation.ValidateSeal" && row.Boundary == "begin");
            var sealEnds = rows.Count(row => row.Name == "JointContinuation.ValidateSeal" && row.Boundary == "end");
            var resourceBegins = rows.Count(row => row.Name == "TreatmentResources.Finalize" && row.Boundary == "begin");
            var resourceEnds = rows.Count(row => row.Name == "TreatmentResources.Finalize" && row.Boundary == "end");
            Assert.Equal(63, sealBegins);
            Assert.Equal(63, sealEnds);
            Assert.Equal(63, resourceBegins);
            Assert.Equal(63, resourceEnds);

            var beforeCopy = ReadAdmissionTimingRange((0, range.Start));
            Assert.Single(beforeCopy, row => row.Name == "JointContinuation.InitialFingerprint" && row.Boundary == "begin");
            Assert.Single(beforeCopy, row => row.Name == "JointContinuation.InitialFingerprint" && row.Boundary == "end");
            var initialStart = Array.FindIndex(beforeCopy,
                row => row.Name == "JointContinuation.InitialFingerprint" && row.Boundary == "begin");
            var initialEnd = Array.FindIndex(beforeCopy, initialStart + 1,
                row => row.Name == "JointContinuation.InitialFingerprint" && row.Boundary == "end");
            var initialRows = beforeCopy.Skip(initialStart).Take(initialEnd - initialStart + 1).ToArray();
            _ = ReadStrictAdmissionTree(initialRows);
            Assert.Single(initialRows, row => row.Name == "JointContinuation.ComputeFingerprint" && row.Boundary == "begin");
            Assert.Single(initialRows, row => row.Name == "JointContinuation.ComputeFingerprint" && row.Boundary == "end");
            Assert.Single(initialRows, row => row.Name == "JointContinuation.HashBeforeImages" && row.Boundary == "begin");
            Assert.Single(initialRows, row => row.Name == "JointContinuation.HashBeforeImages" && row.Boundary == "end");
            var copyHashBegins = rows.Count(row => row.Name == "JointContinuation.HashBeforeImages" && row.Boundary == "begin");
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Kind = "JointImmutableFingerprintWork", range.Start, range.End,
                SealBegins = sealBegins, SealEnds = sealEnds, ResourceBegins = resourceBegins, ResourceEnds = resourceEnds,
                InitialStart = initialStart, InitialEnd = initialEnd + 1, InitialFingerprintCount = 1,
                InitialBeforeImageHashCount = 1, CopyBeforeImageHashCount = copyHashBegins,
                ExpectedCopyBeforeImageHashCount = 0
            }));
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Kind = "JointAdmissionTimingTree", range.Start, range.End, Events = rows.Length,
                RequiredNames = required, StrictLifo = true, DeclaredChains = chains,
                SealContainsResourceAndFingerprint = true
            }));
            Assert.Equal(0, copyHashBegins);
            Assert.DoesNotContain(rows, row => row.Name == "JointContinuation.HashBeforeImages");
        }

        /// <summary>
        /// Requires nonempty actual supplied-stage validator evidence and refusal before final validation.
        /// </summary>
        /// <param name="phase">
        /// The prepared or effect refusal label associated with this recorded constructor call.
        /// </param>
        /// <param name="range">
        /// The half-open offsets surrounding only that existing failed constructor call.
        /// </param>
        private void AssertNoFinalValidationInRecordedRange(string phase, (int Start, int End) range)
        {
            var rows = ReadAdmissionTimingRange(range);
            Assert.NotEmpty(rows);
            var requiredValidator = phase switch
            {
                "Prepared" => "WoundPlanCache.ValidatePreparedResult",
                "Effect" => "WoundEffect.AcceptEffectResult",
                _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown supplied-stage refusal.")
            };
            var validatorBegins = rows.Count(row => row.Name == requiredValidator && row.Boundary == "begin");
            var validatorEnds = rows.Count(row => row.Name == requiredValidator && row.Boundary == "end");
            Assert.True(validatorBegins > 0, "The refusal range must contain its actual validator entry.");
            Assert.True(validatorEnds > 0, "The refusal range must contain its actual validator completion boundary.");
            Assert.DoesNotContain(rows, row => row.Name == "WoundPlanCache.ValidateFinalResult");
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Kind = "JointAdmissionRefusalTiming", Phase = phase, range.Start, range.End,
                Events = rows.Length, RequiredValidator = requiredValidator, ValidatorBegins = validatorBegins,
                ValidatorEnds = validatorEnds, FinalValidationAbsent = true
            }));
        }

        /// <summary>
        /// Selects an exact existing observation interval without changing or limiting the uncapped capture.
        /// </summary>
        /// <param name="range">
        /// Valid half-open offsets into the retained observation queue.
        /// </param>
        /// <returns>
        /// All observations in that interval, in their original order.
        /// </returns>
        private (string Name, string Boundary, double Milliseconds, long SourceScopeId)[] ReadAdmissionTimingRange((int Start, int End) range)
        {
            var observations = _observations.ToArray();
            Assert.InRange(range.Start, 0, observations.Length);
            Assert.InRange(range.End, range.Start, observations.Length);
            Assert.True(range.End > range.Start, "The recorded timing interval must be nonempty.");
            return observations.Skip(range.Start).Take(range.End - range.Start).ToArray();
        }

        /// <summary>
        /// Builds the recorded tree while requiring matching begin/end events in strict LIFO order.
        /// </summary>
        /// <param name="rows">
        /// Every event from one complete synchronous observation interval, in original order.
        /// </param>
        /// <returns>
        /// Every node, retaining its direct children for the declared chain checks.
        /// </returns>
        private static IReadOnlyList<AdmissionTimingNode> ReadStrictAdmissionTree(
            (string Name, string Boundary, double Milliseconds, long SourceScopeId)[] rows)
        {
            var nodes = new List<AdmissionTimingNode>();
            var stack = new Stack<AdmissionTimingNode>();
            foreach (var row in rows)
            {
                Assert.True(double.IsFinite(row.Milliseconds) && row.Milliseconds >= 0);
                if (row.Boundary == "begin")
                {
                    var node = new AdmissionTimingNode(row.Name, []);
                    nodes.Add(node);
                    if (stack.TryPeek(out var parent)) parent.Children.Add(node);
                    stack.Push(node);
                }
                else
                {
                    Assert.Equal("end", row.Boundary);
                    Assert.NotEmpty(stack);
                    Assert.Equal(stack.Pop().Name, row.Name);
                }
            }
            Assert.Empty(stack);
            return nodes;
        }

        /// <summary>
        /// Matches a declared chain through direct recorded child relationships.
        /// </summary>
        /// <param name="node">
        /// The current observed node to compare.
        /// </param>
        /// <param name="names">
        /// The nonempty ordered chain of names expected in one tree.
        /// </param>
        /// <param name="position">
        /// The current chain position; callers start at zero.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when this node and its direct descendants match the remaining complete chain; otherwise <see langword="false"/>.
        /// </returns>
        private static bool HasAdmissionTimingChain(AdmissionTimingNode node, IReadOnlyList<string> names, int position) =>
            node.Name == names[position] &&
            (position == names.Count - 1 ||
                node.Children.Any(child => HasAdmissionTimingChain(child, names, position + 1)));

        /// <summary>
        /// Retains only an observed timing name and its direct children.
        /// </summary>
        /// <param name="Name">
        /// The exact name received by the existing observer.
        /// </param>
        /// <param name="Children">
        /// All direct child scopes, in original begin order.
        /// </param>
        private sealed record AdmissionTimingNode(string Name, List<AdmissionTimingNode> Children);
    }
}
