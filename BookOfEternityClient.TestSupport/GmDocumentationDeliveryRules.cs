using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BookOfEternityClient.Tests;

// Finite documentation/source verifiers; no PowerShell evaluation or game authority.
internal static class GmDocumentationDeliveryRules
{
    private static string S(JsonNode row, string field) => row[field]?.GetValue<string>() ?? string.Empty;
    private static string[] Refs(JsonNode row) => row["refs"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
    internal static IReadOnlyList<string> VerifyFullSource(JsonObject ast)
    {
        var issues = new List<string>();
        var writes = ast["writes"]!.AsArray().Select(row => row!.AsObject()).ToArray();
        var calls = ast["calls"]!.AsArray().Select(row => row!.AsObject()).ToArray();
        var returns = ast["returns"]!.AsArray().Select(row => row!.AsObject()).ToArray();
        var functions = ast["functions"]!.AsArray().Select(row => row!.AsObject()).ToArray();
        var startup = ast["startupOffset"]!.GetValue<int>();
        static int Offset(JsonObject row) => row["offset"]!.GetValue<int>();
        static string Command(string text) => Regex.Replace(text.Replace("`\r\n", " ", StringComparison.Ordinal)
            .Replace("`\n", " ", StringComparison.Ordinal), @"\s+", " ").Trim();
        static bool Guard(JsonObject row, string pattern) => row["guards"]!.AsArray()
            .Any(value => Regex.IsMatch(value!.GetValue<string>(), pattern));
        var sourceWrites = writes.Where(row => S(row, "left") == "$script:GmSourceFallbackDirective").ToArray();
        var authoritative = sourceWrites.Where(row => S(row, "scope") == "Write-GmContextPack").ToArray();
        if (authoritative.Length != 1 || !S(authoritative[0], "right").Contains(
                "Do not read implementation code such as BookOfEternityClient/**/*.cs during normal play or validation repair", StringComparison.Ordinal))
            issues.Add("source.authoritative_ban_missing");
        var earlyEmpty = sourceWrites.Where(row => S(row, "scope") == "<root>" && S(row, "operator") == "Equals" &&
            S(row, "right") == "\"\"" && row["rootStatement"]!.GetValue<bool>() &&
            row["guards"]!.AsArray().Count == 0 && Offset(row) < startup &&
            authoritative.Length == 1 && Offset(row) < Offset(authoritative[0])).ToArray();
        if (earlyEmpty.Length != 1) issues.Add("source.early_initializer_shape");
        foreach (var row in sourceWrites)
            if (!(authoritative.Length == 1 && ReferenceEquals(row, authoritative[0]) && S(row, "operator") == "Equals") &&
                !(earlyEmpty.Length == 1 && ReferenceEquals(row, earlyEmpty[0])))
                issues.Add("source.late_source_override");
        foreach (var row in writes.Where(row => S(row, "left") is "$script:GmContextPackDirective" or "$script:GmDocPathDirective" or
                     "$script:AfterlifeExamplesDirective" or "$script:GmSourceFallbackDirective"))
            if (Regex.IsMatch(S(row, "right"), @"(?:Read|Open) all (?:afterlife )?(?:modules|topic bodies) (?:every|for|on)", RegexOptions.IgnoreCase))
                issues.Add("source.universal_read");

        bool HasFile(JsonObject row, string file, HashSet<string> visited)
        {
            if (S(row, "right").Contains(file, StringComparison.Ordinal)) return true;
            foreach (var name in Refs(row))
                if (visited.Add(name) && writes.Where(candidate => S(candidate, "left") == "$" + name)
                        .Any(candidate => HasFile(candidate, file, visited))) return true;
            return false;
        }
        var docWriters = writes.Where(row => S(row, "left") == "$script:GmDocPathDirective" && S(row, "scope") == "Write-GmContextPack").ToArray();
        if (!docWriters.Any(row => HasFile(row, "CLI_Rules_Index.md", new(StringComparer.OrdinalIgnoreCase)))) issues.Add("source.router_dependency_missing");

        foreach (var (scope, kind, label) in new[]
                 { ("Process-TurnCore", "turn", "ordinary"), ("Process-RepairRequestCore", "repair", "repair"),
                     ("Process-TerminalProtocolFailureRequestCore", "terminal-repair", "terminal") })
        {
            var dispatches = calls.Where(row => S(row, "scope") == scope &&
                Regex.IsMatch(S(row, "text"), @"-OperationKind\s+" + Regex.Escape(kind) + @"(?:\s|$)") &&
                Regex.IsMatch(S(row, "text"), @"-Message\s+\$message(?:\s|$)")).ToArray();
            if (dispatches.Length != 1) { issues.Add("source.dispatch_shape:" + label); continue; }
            var offset = dispatches[0]["offset"]!.GetValue<int>();
            var messages = writes.Where(row => S(row, "scope") == scope && S(row, "left") == "$message" &&
                row["offset"]!.GetValue<int>() < offset).ToArray();
            var initializers = messages.Where(row => S(row, "operator") == "Equals" && S(row, "right") != "$null").ToArray();
            var direct = initializers.Where(row => !(scope == "Process-TurnCore" &&
                S(row, "right").StartsWith("Build-FirstMortalBootstrapDispatchMessage", StringComparison.Ordinal))).ToArray();
            if (direct.Length != 1 || !Refs(direct[0]).Contains("script:GmSourceFallbackDirective", StringComparer.OrdinalIgnoreCase))
                issues.Add("source." + label + "_ban_missing");
            if (direct.Length != 1 || new[] { "script:GmDocPathDirective", "script:GmContextPackDirective", "script:GmSafeProbeDirective" }
                    .Any(name => !Refs(direct[0]).Contains(name, StringComparer.OrdinalIgnoreCase))) issues.Add("source." + label + "_reading_flow_missing");
            if (scope == "Process-TurnCore")
            {
                // Closed current caller shape: null -> guarded bootstrap builder -> null-only ordinary fallback.
                var ordered = messages.OrderBy(Offset).ToArray();
                const string bootstrapCall = "Build-FirstMortalBootstrapDispatchMessage -TurnRequest $turnRequest -TurnNumber $turnNumber -RequestId $requestId -ExperiencePrompt $experiencePrompt -FirstMortalBootstrapPrompt $firstMortalBootstrapPrompt";
                const string bootstrapGuard = @"^\s*\(?-not\s+\[string\]::IsNullOrWhiteSpace\(\$firstMortalBootstrapPrompt\)\)?\s*$";
                const string fallbackGuard = @"^\s*\(?\$null\s+-eq\s+\$message\)?\s*$";
                if (ordered.Length != 4 || S(ordered[0], "operator") != "Equals" || S(ordered[0], "right") != "$null" ||
                    S(ordered[1], "operator") != "Equals" || Command(S(ordered[1], "right")) != bootstrapCall ||
                    !Guard(ordered[1], bootstrapGuard) || !Guard(ordered[2], fallbackGuard) || !Guard(ordered[3], fallbackGuard) ||
                    direct.Length != 1 || !ReferenceEquals(ordered[2], direct[0]) || S(ordered[3], "operator") != "PlusEquals" ||
                    !S(ordered[3], "right").StartsWith("\" If a Mortal World turn resolves open combat,", StringComparison.Ordinal) ||
                    Refs(ordered[3]).Any(name => name != "script:CompactMortalCombatTemplatePath"))
                    issues.Add("source.bootstrap_dispatch_flow_missing");
            }
        }
        var bootstrap = returns.Where(row => S(row, "scope") == "Build-FirstMortalBootstrapDispatchMessage").ToArray();
        var bootstrapFunctions = functions.Where(row => S(row, "name") == "Build-FirstMortalBootstrapDispatchMessage").ToArray();
        if (bootstrap.Length != 1 || !Refs(bootstrap[0]).Contains("script:GmSourceFallbackDirective", StringComparer.OrdinalIgnoreCase)) issues.Add("source.bootstrap_ban_missing");
        if (bootstrapFunctions.Length != 1 || !BuilderShape(bootstrapFunctions[0],
                ["TurnRequest", "TurnNumber", "RequestId", "ExperiencePrompt", "FirstMortalBootstrapPrompt"], ["ReturnStatementAst"]))
            issues.Add("source.bootstrap_builder_shape");
        if (bootstrap.Any(row => S(row, "text").Contains("repository source unless validation later names an exact surface", StringComparison.Ordinal)))
            issues.Add("source.bootstrap_source_exception");
        var receipt = returns.Where(row => S(row, "scope") == "Build-QteEffectResolutionDispatchMessage").ToArray();
        if (receipt.Length != 1) issues.Add("source.receipt_builder_missing");
        else
        {
            var value = S(receipt[0], "text");
            if (Refs(receipt[0]).Any(name => name is "script:GmContextPackDirective" or "script:GmDocPathDirective" or "script:AfterlifeExamplesDirective") ||
                Regex.IsMatch(value, @"(?m)^Read[^\r\n]*(?:GM_Common_Core\.md|CLI_Rules_Index\.md|input\\turn_request\.json)", RegexOptions.IgnoreCase))
                issues.Add("source.receipt_ordinary_injected");
            if (!value.Contains("Use only safePacket.requests", StringComparison.Ordinal) ||
                !value.Contains("Complete-BoeQteEffectResolution", StringComparison.Ordinal)) issues.Add("source.receipt_contract_missing");
        }
        var receiptBuilders = functions.Where(row => S(row, "name") == "Build-QteEffectResolutionDispatchMessage").ToArray();
        if (receiptBuilders.Length != 1 || !BuilderShape(receiptBuilders[0], ["Request"],
                ["AssignmentStatementAst", "AssignmentStatementAst", "AssignmentStatementAst", "ReturnStatementAst"]) ||
            !receiptBuilders[0]["statements"]!.AsArray().Take(3).Select(row => Command(S(row!, "text"))).SequenceEqual(
                ["$requestId = [string]$Request.requestId", "$waveId = [string]$Request.waveId", "$waveOrdinal = [int64]$Request.waveOrdinal"]))
            issues.Add("source.receipt_builder_shape");
        var receiptFunctions = functions.Where(row => S(row, "name") == "Process-QteEffectResolutionRequestCore").ToArray();
        var receiptDispatches = calls.Where(row => S(row, "scope") == "Process-QteEffectResolutionRequestCore").ToArray();
        if (receiptFunctions.Length != 1 || receiptDispatches.Length != 1 ||
            !Regex.IsMatch(Command(S(receiptDispatches[0], "text")), @"-Message\s+\$message(?:\s|$)") ||
            !Regex.IsMatch(Command(S(receiptDispatches[0], "text")), @"-OperationKind\s+qte-effect(?:\s|$)") ||
            !Regex.IsMatch(Command(S(receiptDispatches[0], "text")), @"-PendingPath\s+\$RequestPath(?:\s|$)") ||
            !Regex.IsMatch(Command(S(receiptDispatches[0], "text")), @"-ExpectedSourceHash\s+\$pendingSnapshot\.Hash(?:\s|$)") ||
            !Regex.IsMatch(S(receiptFunctions[0], "text"), @"\$eligibility=Read-QteDaemonEligibility\s+\$RequestPath\s+if\(\$null -eq \$eligibility\)\{return\}\s+\$pendingSnapshot = \$eligibility\.Pending\s+\$request = \$pendingSnapshot\.Request\s+if \(-not \[string\]::Equals\(\s+\[string\]\$request\.requestKind,\s+""qte_deferred_effect_resolution"",\s+\[System\.StringComparison\]::Ordinal\) -or\s+\$null -eq \$request\.safePacket\) \{\s+throw ""Invalid dedicated QTE effect-resolution request\.""\s+\}"))
            issues.Add("source.receipt_dispatch_flow_missing");
        if (receiptDispatches.Length == 1)
        {
            var messages = writes.Where(row => S(row, "scope") == "Process-QteEffectResolutionRequestCore" &&
                S(row, "left") == "$message" && Offset(row) < Offset(receiptDispatches[0])).ToArray();
            if (messages.Length != 1 || S(messages[0], "operator") != "Equals" ||
                Command(S(messages[0], "right")) != "Build-QteEffectResolutionDispatchMessage -Request $request")
                issues.Add("source.receipt_ordinary_injected");
        }
        return issues;
    }
    private static bool BuilderShape(JsonObject function, string[] parameters, string[] statementKinds) =>
        function["parameters"]!.AsArray().Select(value => value!.GetValue<string>()).SequenceEqual(parameters) &&
        function["statements"]!.AsArray().Select(row => S(row!, "kind")).SequenceEqual(statementKinds);

    internal static IReadOnlyList<string> VerifyClosure(IReadOnlyDictionary<string, byte[]> docs, string? physicalRoot = null,
        IReadOnlyList<GmDocumentationDeliveryScenario.Doc>? requiredDocs = null)
    {
        var issues = new List<string>();
        var expected = requiredDocs ?? GmDocumentationDeliveryScenario.Expected;
        foreach (var doc in expected)
            if (!docs.ContainsKey(doc.Path)) issues.Add("delivery.copy_missing:" + doc.Path);
        foreach (var (source, bytes) in docs.Where(pair => pair.Key == "CLI_Rules_Index.md" ||
                     pair.Key == "OtherGuides/GM_Common_Core.md" || pair.Key.Contains("Afterlife_", StringComparison.Ordinal) ||
                     pair.Key.StartsWith("Rules/Block_5", StringComparison.Ordinal) ||
                     pair.Key.StartsWith("Examples/E_Block_5", StringComparison.Ordinal)))
        {
            var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            foreach (var line in text.Split('\n'))
            {
                // Repository-only provenance may remain in the global index; it is not a required session reading edge.
                var repositoryOnly = source == "CLI_Rules_Index.md" && line.Contains("repository-only", StringComparison.OrdinalIgnoreCase);
                foreach (Match match in Regex.Matches(line, @"\[[^\]]*\]\(([^)]+)\)"))
                    if (!repositoryOnly) CheckLink(source, match.Groups[1].Value.Trim('<', '>'), docs, physicalRoot, issues);
            }
            if (source == "CLI_Rules_Index.md")
                foreach (var topic in expected.Where(doc => doc.Role.StartsWith("afterlife_rules_", StringComparison.Ordinal) ||
                             doc.Role.StartsWith("afterlife_examples_", StringComparison.Ordinal) ||
                             doc.Role.StartsWith("block5_", StringComparison.Ordinal)))
                {
                    if (!text.Split('\n').Any(line => line.Contains(topic.Path, StringComparison.Ordinal) && !line.Contains("repository-only", StringComparison.OrdinalIgnoreCase)))
                        issues.Add("delivery.router_edge_missing:" + topic.Path);
                    else CheckLink(source, topic.Path, docs, physicalRoot, issues);
                }
        }
        return issues;
    }
    private static void CheckLink(string source, string target, IReadOnlyDictionary<string, byte[]> docs,
        string? physicalRoot, List<string> issues)
    {
        if (target.StartsWith("https://", StringComparison.Ordinal) || target.StartsWith("http://", StringComparison.Ordinal)) return;
        var split = target.Split('#', 2); var file = Uri.UnescapeDataString(split[0]);
        // Markdown hrefs resolve from the document directory, including copied session docs.
        var relative = file.Length == 0 ? source : Path.GetRelativePath("/gm1557-closure", Path.GetFullPath(Path.Combine(
            "/gm1557-closure", Path.GetDirectoryName(source)!, file)));
        if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal)) { issues.Add("delivery.link_escape:" + target); return; }
        if (!docs.TryGetValue(relative, out var bytes)) { issues.Add("delivery.link_missing:" + target); return; }
        if (physicalRoot is not null)
        {
            var path = Path.GetFullPath(Path.Combine(physicalRoot, relative));
            for (var current = path; current != physicalRoot; current = Path.GetDirectoryName(current)!)
                if (new FileInfo(current).LinkTarget is not null) { issues.Add("delivery.link_symlink:" + target); return; }
        }
        if (split.Length == 2 && !Anchors(Encoding.UTF8.GetString(bytes)).Contains(Uri.UnescapeDataString(split[1])))
            issues.Add("delivery.anchor_missing:" + target);
    }
    private static HashSet<string> Anchors(string text)
    {
        var result = new HashSet<string>(StringComparer.Ordinal); var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var visible = new StringBuilder(); char fence = '\0'; var width = 0;
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var marker = Regex.Match(line, @"^ {0,3}(`{3,}|~{3,})(.*)$");
            if (fence != '\0')
            {
                if (marker.Success && marker.Groups[1].Value[0] == fence && marker.Groups[1].Length >= width &&
                    string.IsNullOrWhiteSpace(marker.Groups[2].Value)) fence = '\0';
                continue;
            }
            if (marker.Success && (marker.Groups[1].Value[0] != '`' || !marker.Groups[2].Value.Contains('`')))
            {
                fence = marker.Groups[1].Value[0]; width = marker.Groups[1].Length; continue;
            }
            visible.AppendLine(line);
        }
        var rendered = visible.ToString();
        foreach (Match match in Regex.Matches(rendered, "<a\\s+id=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase)) result.Add(match.Groups[1].Value);
        foreach (Match match in Regex.Matches(rendered, @"(?m)^#{1,6}\s+(.+?)\s*$"))
        {
            var heading = Regex.Replace(match.Groups[1].Value.ToLowerInvariant(), @"[^\p{L}\p{N}_\-\s]", string.Empty).Replace(' ', '-');
            var suffix = counts.GetValueOrDefault(heading); counts[heading] = suffix + 1;
            result.Add(heading + (suffix == 0 ? string.Empty : "-" + suffix));
        }
        return result;
    }
}
