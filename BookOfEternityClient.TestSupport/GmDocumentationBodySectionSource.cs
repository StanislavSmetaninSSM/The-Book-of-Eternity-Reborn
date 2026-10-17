using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

// Five explicit test-only historical section views cross physical topic owners.
// Existing Extract/Split assertions keep their exact original context boundaries.
internal static class GmDocumentationBodySectionSource
{
    private const string PlanHash = "4869cd1c1af38dd8dd5cca3b5f4b67fd26b99f4126737d76b1800f9a50e5196c";

    internal static string Read(string repo, string id) =>
        GmDocumentationNamedSectionSource.Assemble(Retrieve(repo, id));

    internal static GmDocumentationNamedSectionSource.VerifiedSection Retrieve(string repo, string id)
    {
        var plan = File.ReadAllBytes(Path.Combine(repo, "specs/1557-gm-doc-modularization/body-section-source-views.json"));
        if (GmDocumentationPreservationFixtures.Hash(plan) != PlanHash)
            throw new InvalidDataException("body.source_plan_pin");
        var row = JsonNode.Parse(plan)!["views"]!.AsArray().Single(value => value!["id"]!.GetValue<string>() == id)!;
        var pieces = new List<byte[]>();
        var cursor = row["sourceStart"]!.GetValue<int>();
        foreach (var segment in row["segments"]!.AsArray())
        {
            var block = segment!["block"]!.GetValue<string>();
            if (segment["sourceStart"]!.GetValue<int>() != cursor)
                throw new InvalidDataException("body.segment_order:" + block);
            byte[] bytes;
            try { bytes = File.ReadAllBytes(Path.Combine(repo, segment["destination"]!.GetValue<string>())); }
            catch (IOException error) { throw new InvalidDataException("body.segment_missing:" + block, error); }
            var startMarker = GmDocumentationPreservationFixtures.Bytes(segment["startMarker"]!.GetValue<string>());
            var endMarker = GmDocumentationPreservationFixtures.Bytes(segment["endMarker"]!.GetValue<string>());
            var start = bytes.AsSpan().IndexOf(startMarker);
            if (start < 0 || bytes.AsSpan(start + startMarker.Length).IndexOf(startMarker) >= 0)
                throw new InvalidDataException("body.segment_marker:" + block);
            start += startMarker.Length;
            var end = bytes.AsSpan(start).IndexOf(endMarker);
            if (end < 0 || bytes.AsSpan(start + end + endMarker.Length).IndexOf(endMarker) >= 0)
                throw new InvalidDataException("body.segment_end:" + block);
            var payload = bytes.AsSpan(start, end);
            var first = segment["payloadOffset"]!.GetValue<int>();
            var length = segment["length"]!.GetValue<int>();
            if (first < 0 || length < 0 || first > payload.Length - length ||
                length != segment["sourceEnd"]!.GetValue<int>() - cursor)
                throw new InvalidDataException("body.segment_extent:" + block);
            var piece = payload.Slice(first, length).ToArray();
            if (GmDocumentationPreservationFixtures.Hash(piece) != segment["sha256"]!.GetValue<string>())
                throw new InvalidDataException("body.segment_bytes:" + block);
            pieces.Add(piece); cursor = segment["sourceEnd"]!.GetValue<int>();
        }
        if (cursor != row["sourceEnd"]!.GetValue<int>())
            throw new InvalidDataException("body.section_extent:" + id);
        return new(id, row["sha256"]!.GetValue<string>(), pieces);
    }
}
