using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

// Two explicit historical named sections cross physical topic owners. This test-only
// source view retrieves their actual owned payloads; it never reads router labels,
// new headers or an old monolith copy, and never changes GM reading/authority.
internal static class GmDocumentationNamedSectionSource
{
    private const string PlanHash = "b490f58cff9de9e4e67fe31e58068bc17a9e3bbf9e3c08337958402c43901e4d";
    internal sealed record VerifiedSection(string ContractId, string Sha256, IReadOnlyList<byte[]> Segments);

    internal static string Read(string repo, string file, string contractId)
    {
        if (contractId is not ("afterlife_effect_conditions_five_kinds_v1" or "afterlife_effect_conditions_projection_v1"))
            return File.ReadAllText(Path.Combine(repo, "Examples", file));
        var section = Retrieve(repo, file, contractId);
        return Assemble(section);
    }

    internal static VerifiedSection Retrieve(string repo, string file, string contractId)
    {
        var plan = File.ReadAllBytes(Path.Combine(repo, "specs/1557-gm-doc-modularization/named-section-source-views.json"));
        if (GmDocumentationPreservationFixtures.Hash(plan) != PlanHash)
            throw new InvalidDataException("named.source_plan_pin");
        var row = JsonNode.Parse(plan)!["rows"]!.AsArray().Single(value => value!["contractId"]!.GetValue<string>() == contractId)!;
        var segments = row["segments"]!.AsArray();
        if (file != Path.GetFileName(segments[0]!["owner"]!.GetValue<string>()))
            throw new InvalidDataException("named.source_view_owner:" + contractId);
        var pieces = new List<byte[]>();
        var cursor = row["sourceStart"]!.GetValue<int>();
        foreach (var segment in segments)
        {
            var id = segment!["block"]!.GetValue<string>();
            var owner = segment["owner"]!.GetValue<string>();
            if (segment["sourceStart"]!.GetValue<int>() != cursor)
                throw new InvalidDataException("named.segment_order:" + id);
            byte[] bytes;
            try { bytes = File.ReadAllBytes(Path.Combine(repo, owner)); }
            catch (IOException error) { throw new InvalidDataException("named.segment_missing:" + id, error); }
            var startMarker = GmDocumentationPreservationFixtures.Bytes("<!-- GM1557:" + id + ":START -->\n");
            var endMarker = GmDocumentationPreservationFixtures.Bytes("<!-- GM1557:" + id + ":END -->\n");
            var start = bytes.AsSpan().IndexOf(startMarker);
            if (start < 0 || bytes.AsSpan(start + startMarker.Length).IndexOf(startMarker) >= 0)
                throw new InvalidDataException("named.segment_marker:" + id);
            start += startMarker.Length;
            var end = bytes.AsSpan(start).IndexOf(endMarker);
            if (end < 0 || bytes.AsSpan(start + end + endMarker.Length).IndexOf(endMarker) >= 0)
                throw new InvalidDataException("named.segment_end:" + id);
            var payload = bytes.AsSpan(start, end);
            var first = segment["withinBlockStart"]!.GetValue<int>();
            var last = segment["withinBlockEnd"]!.GetValue<int>();
            if (first < 0 || last < first || last > payload.Length ||
                last - first != segment["sourceEnd"]!.GetValue<int>() - cursor)
                throw new InvalidDataException("named.segment_extent:" + id);
            var piece = payload[first..last].ToArray();
            if (GmDocumentationPreservationFixtures.Hash(piece) != segment["sha256"]!.GetValue<string>())
                throw new InvalidDataException("named.segment_bytes:" + id);
            pieces.Add(piece); cursor = segment["sourceEnd"]!.GetValue<int>();
        }
        if (cursor != row["sourceEnd"]!.GetValue<int>())
            throw new InvalidDataException("named.section_extent:" + contractId);
        return new(contractId, row["sha256"]!.GetValue<string>(), pieces);
    }

    internal static string Assemble(VerifiedSection section)
    {
        var bytes = section.Segments.SelectMany(piece => piece).ToArray();
        if (GmDocumentationPreservationFixtures.Hash(bytes) != section.Sha256)
            throw new InvalidDataException("named.section_order_or_bytes:" + section.ContractId);
        return new UTF8Encoding(false, true).GetString(bytes);
    }
}
