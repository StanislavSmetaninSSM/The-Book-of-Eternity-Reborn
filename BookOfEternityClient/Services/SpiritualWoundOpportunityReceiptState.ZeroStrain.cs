using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualWoundOpportunityReceiptState
{
    /// <summary>Returns comparison-only original requests for append/replay checks, never publication permission.</summary>
    private static IEnumerable<(string Session, string Request)> ComparisonRequests(JsonObject root) =>
        root["strainComparisons"] is JsonArray rows ? rows.Select(row => OriginalRequest(Object(row!["witness"]))) : [];

    /// <summary>Checks closed v3 rows through the existing witness/parent readers and shared source indexes exactly once.</summary>
    private static JsonObject[] ValidateStrainComparisons(JsonObject root, int version,
        IReadOnlyDictionary<string, JsonObject> instances, IReadOnlyDictionary<string, long> closures,
        IReadOnlyDictionary<string, JsonObject> injurySources,
        HashSet<(string Instance, string Exchange, string Side)> coordinates,
        Dictionary<(string Instance, string Exchange), (string Session, string Request, long Ordinal)> exchanges,
        List<(JsonObject Row, SpiritualStrainPreimageData Data)> parents)
    {
        if (version != 3) return [];
        var rows = Rows(root, "strainComparisons", "nextStrainComparisonOrdinal");
        Require(rows.Length > 0, "V3 requires comparison-only data.");
        var ids = injurySources.Keys.ToHashSet(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            Closed(row, "ordinal instanceId instanceComparisonOrdinal witness");
            var instanceId = Text(row["instanceId"]);
            Require(instances.TryGetValue(instanceId, out var instance), "Unknown comparison instance.");
            var witness = Object(row["witness"]);
            var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var parent);
            Require(calculation.MaximumSeverityRank == 0 && calculation.HasIncreasingStrain &&
                witness["guaranteedSeverityRank"] is null && parent is { Version: 2 or 3 or 4 or 5 or 6 },
                "Only increasing non-guaranteed zero-ceiling v2/v3/v4/v5/v6 comparisons belong in this table.");
            var evidence = Object(witness["turnEvidence"]);
            Require(Text(witness["conflictId"]) == Text(instance!["displayConflictId"]) &&
                Text(evidence["realm"]) == Text(instance["realm"]), "Comparison belongs to another conflict.");
            var turn = Integer(evidence["turn"], Integer(instance["startTurn"]));
            Require(!closures.TryGetValue(instanceId, out var terminal) || turn <= terminal,
                "Comparison follows instance closure.");
            var next = counts.GetValueOrDefault(instanceId) + 1;
            Integer(row["instanceComparisonOrdinal"], next, next); counts[instanceId] = next;
            Require(ids.Add(Text(witness["sourceId"])), "Duplicate injury/comparison source identity.");
            Require(coordinates.Add((instanceId, Text(witness["exchangeId"]), Text(witness["affectedSide"]))),
                "An injury/comparison exchange side cannot occur twice.");
            var key = (instanceId, Text(witness["exchangeId"]));
            var request = OriginalRequest(witness);
            var identity = (request.Session, request.Request, Integer(witness["exchangeOrdinal"]));
            Require(!exchanges.TryGetValue(key, out var prior) || prior == identity,
                "An injury/comparison exchange cannot change its original request or position.");
            exchanges[key] = identity;
            parents.Add((row, parent!));
        }
        return rows;
    }

    /// <summary>
    /// Merges only already order-checked tables. Transient original-request precedence preserves both histories;
    /// within a shared request, actual sourceOrdinal supplies the existing causal/dice validation order.
    /// No immutable proof DATA is reparsed and no permission or index survives this parse.
    /// </summary>
    private static IEnumerable<JsonObject> MergeComparisonTurnRows(JsonObject[] injuries, JsonObject[] comparisons)
    {
        if (comparisons.Length == 0) return injuries;
        ValidateRequestTableOrder(injuries); ValidateRequestTableOrder(comparisons);
        ValidateOriginalSourceOrder(injuries); ValidateOriginalSourceOrder(comparisons);
        var groups = injuries.Concat(comparisons).GroupBy(row => OriginalRequest(Object(row["witness"])))
            .ToDictionary(group => group.Key, group => group.OrderBy(row => Integer(row["witness"]!["sourceOrdinal"])).ToArray());
        var preceding = groups.Keys.ToDictionary(key => key, _ => new HashSet<(string Session, string Request)>());
        foreach (var table in new[] { injuries, comparisons })
        {
            (string Session, string Request)? previous = null;
            foreach (var row in table)
            {
                var key = OriginalRequest(Object(row["witness"]));
                if (previous is { } prior && prior != key) preceding[key].Add(prior);
                previous = key;
            }
        }
        var followers = groups.Keys.ToDictionary(key => key, _ => new List<(string Session, string Request)>());
        foreach (var (key, prior) in preceding)
            foreach (var parent in prior) followers[parent].Add(key);
        var indegree = preceding.ToDictionary(pair => pair.Key, pair => pair.Value.Count);
        var comparer = Comparer<(long Turn, string Session, string Request)>.Create((left, right) =>
        {
            var order = left.Turn.CompareTo(right.Turn);
            if (order == 0) order = StringComparer.Ordinal.Compare(left.Session, right.Session);
            return order != 0 ? order : StringComparer.Ordinal.Compare(left.Request, right.Request);
        });
        var ready = new PriorityQueue<(string Session, string Request), (long Turn, string Session, string Request)>(comparer);
        void Enqueue((string Session, string Request) key) => ready.Enqueue(key,
            (Integer(groups[key][0]["witness"]!["turnEvidence"]!["turn"]), key.Session, key.Request));
        foreach (var (key, count) in indegree) if (count == 0) Enqueue(key);
        var merged = new List<JsonObject>(injuries.Length + comparisons.Length);
        var emitted = 0;
        while (ready.TryDequeue(out var key, out _))
        {
            emitted++; merged.AddRange(groups[key]);
            foreach (var follower in followers[key]) if (--indegree[follower] == 0) Enqueue(follower);
        }
        Require(emitted == groups.Count, "Injury/comparison request histories contradict each other.");
        return merged;
    }

    /// <summary>Checks each input table's original scalar causal order before a shared request can be merged.</summary>
    private static void ValidateOriginalSourceOrder(IEnumerable<JsonObject> rows)
    {
        foreach (var request in rows.GroupBy(row => OriginalRequest(Object(row["witness"]))))
        {
            var previousSource = -1L; var previousSlot = -1L; var previousExchange = -1L;
            foreach (var row in request)
            {
                var witness = Object(row["witness"]);
                var source = Integer(witness["sourceOrdinal"]); var exchange = Integer(witness["exchangeOrdinal"]);
                var slot = checked(2 * exchange + (Text(witness["affectedSide"]) == "opposition" ? 1 : 0));
                Require(source > previousSource && slot > previousSlot && exchange >= previousExchange &&
                    source - previousSource <= slot - previousSlot, "An original table cannot reorder its causal source slots.");
                previousSource = source; previousSlot = slot; previousExchange = exchange;
            }
        }
    }

    /// <summary>Preserves the comparison prefix and prohibits appending to any already accepted cross-family request.</summary>
    private static bool ComparisonAppendAgrees(JsonObject before, JsonObject next)
    {
        var old = before["strainComparisons"] as JsonArray;
        var rows = next["strainComparisons"] as JsonArray;
        if (old is not null && rows is null) return false;
        if (rows is null) return true;
        if (rows.Count < (old?.Count ?? 0) || old is not null &&
            old.Where((row, index) => !JsonNode.DeepEquals(row, rows[index])).Any()) return false;
        var accepted = before["sources"]!.AsArray().Select(row => OriginalRequest(Object(row!["witness"])))
            .Concat(HealingRequests(before)).Concat(ComparisonRequests(before)).ToHashSet();
        var closed = before["closures"]!.AsArray().Select(row => Text(row!["instanceId"])).ToHashSet(StringComparer.Ordinal);
        return !rows.Skip(old?.Count ?? 0).Any(row => closed.Contains(Text(row!["instanceId"])) ||
            accepted.Contains(OriginalRequest(Object(row["witness"]))));
    }
}
