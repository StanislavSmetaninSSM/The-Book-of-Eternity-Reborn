using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmDocumentationBodySectionSourceTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("reordered")]
    public void ActualOwnedSectionViewsRejectMissingOrReorderedSegments(string mutation)
    {
        foreach (var id in new[] { "primary_chaos", "primary_shining", "primary_guardian_trade",
                     "actor_materialization_matrix", "lifecycle_matrix_return" })
        {
            var section = GmDocumentationBodySectionSource.Retrieve(TestRepoPaths.RepoRoot, id);
            Assert.Equal(2, section.Segments.Count);
            var original = GmDocumentationNamedSectionSource.Assemble(section);
            Assert.Equal(section.Sha256, GmDocumentationPreservationFixtures.Hash(
                GmDocumentationPreservationFixtures.Bytes(original)));
            var changed = section with { Segments = mutation == "missing"
                ? section.Segments.Skip(1).ToArray() : section.Segments.Reverse().ToArray() };
            var error = Assert.Throws<InvalidDataException>(() => GmDocumentationNamedSectionSource.Assemble(changed));
            Assert.Equal("named.section_order_or_bytes:" + id, error.Message);
        }
    }
}
