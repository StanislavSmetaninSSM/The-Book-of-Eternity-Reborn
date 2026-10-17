using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace BookOfEternityClient.Tests;

internal sealed partial class GmDocumentationBlock5Fixtures
{
    internal string[] NavigatorIds(string source, string topic) => Groups
        .Where(row => S(row, "sourcePath") == source && S(row, "topic") == topic)
        .OrderBy(row => I(row, "start")).SelectMany(row => row["allRuleIds"]!.AsArray())
        .Select(row => row!.GetValue<string>()).ToArray();
    private string NavigatorRow(string source, JsonNode topic)
    {
        var rules = source.StartsWith("Rules/", StringComparison.Ordinal);
        return "| " + S(topic, "topic") + " | " + string.Join(", ", NavigatorIds(source, S(topic, "topic")).Select(id => "`" + id + "`")) +
            " | [rules](" + (rules ? Path.GetFileName(S(topic, "rulePath")) : "../" + S(topic, "rulePath")) +
            ") | [examples](" + (rules ? "../" + S(topic, "examplePath") : Path.GetFileName(S(topic, "examplePath"))) + ") |";
    }
    internal IReadOnlyList<string> VerifyNavigation(IReadOnlyDictionary<string, byte[]> docs)
    {
        var issues = new List<string>();
        foreach (var source in Sources)
        {
            var path = S(source, "path");
            if (!docs.TryGetValue(path, out var bytes)) { issues.Add("navigation.missing:" + path); continue; }
            var lines = Text(bytes).Split('\n');
            foreach (var topic in Topics)
            {
                var id = S(topic, "topic"); var rows = lines.Where(line => line.StartsWith("| " + id + " |", StringComparison.Ordinal)).ToArray();
                if (rows.Length != 1) { issues.Add("navigation.row:" + path + ":" + id); continue; }
                var expected = NavigatorRow(path, topic); var cells = rows[0].Split('|'); var expectedCells = expected.Split('|');
                if (cells.Length != expectedCells.Length || cells[2] != expectedCells[2]) issues.Add("navigation.ids:" + path + ":" + id);
                if (rows[0] != expected) issues.Add("navigation.alias:" + path + ":" + id);
                var target = S(topic, path.StartsWith("Rules/", StringComparison.Ordinal) ? "rulePath" : "examplePath");
                if (!docs.TryGetValue(target, out var body)) { issues.Add("navigation.destination:" + target); continue; }
                try
                {
                    var xml = XDocument.Parse(Text(body)); var actualIds = xml.Descendants("rule").Select(node => (string?)node.Attribute("id")).ToHashSet(StringComparer.Ordinal);
                    foreach (var ruleId in NavigatorIds(path, id))
                        if (!actualIds.Contains(ruleId)) issues.Add("navigation.body_lookup:" + target + ":" + ruleId);
                }
                catch (System.Xml.XmlException) { issues.Add("navigation.invalid_destination:" + target); }
            }
        }
        return issues;
    }
}
