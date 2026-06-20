using System.Text.RegularExpressions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeRussianTerminologyScannerTests
{
    private static readonly (Regex Pattern, string Replacement)[] ForbiddenGameplayTerms =
    {
        (new Regex(@"(?<![A-Za-z0-9_.])tier(?![A-Za-z0-9_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "тир")
    };

    [Fact]
    public void ExplorerAfterlifePlayerFacingStringsMustPreferRussianGameplayTerms()
    {
        var findings = new List<string>();
        var directory = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "UI",
            "ExplorerMode");

        foreach (var file in Directory.EnumerateFiles(directory, "ExplorerMode.Afterlife*.cs"))
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (line.Contains("=>", StringComparison.Ordinal))
                    continue;

                foreach (var literal in ExtractStringLiterals(line).Select(StripInterpolations))
                {
                    if (IsAllowedTechnicalLiteral(literal))
                        continue;

                    foreach (var (pattern, replacement) in ForbiddenGameplayTerms)
                    {
                        if (!pattern.IsMatch(literal))
                            continue;

                        findings.Add(
                            $"{Path.GetFileName(file)}:{lineNumber}: replace English gameplay term with `{replacement}` in `{literal}`");
                    }
                }
            }
        }

        Assert.True(
            findings.Count == 0,
            "English afterlife player-facing terms found:\n" + string.Join("\n", findings.Take(50)));
    }

    [Fact]
    public void AfterlifeStatusAndPendingAuditPanelsMustUseRussianGameplayWording()
    {
        var sources = new[]
        {
            ReadExplorerSource("ExplorerMode.Afterlife.GuardiansProjectsTrade.cs"),
            ReadExplorerSource("ExplorerMode.Afterlife.StatusAudit.cs"),
            ReadExplorerSource("ExplorerMode.MetaStoryAndStatus.cs")
        };

        foreach (var source in sources)
        foreach (var forbidden in new[]
        {
            "operational overview",
            "read-only Guardian overview",
            "audit-панели",
            "afterlife ресурсов",
            "blockers, contracts",
            "state deltas",
            "pending/control-контракты",
            "repair-only в неверной области",
            "receipts/state",
            "audit/repair",
            "no compact fields; inspect JSON/state file"
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }

        var statusAudit = sources[1];
        Assert.Contains("Активные ожидающие/контрольные контракты", statusAudit, StringComparison.Ordinal);
        Assert.Contains("только ремонт в неверной области", statusAudit, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifeSpiritualCombatScreensMustUseRussianGameplayWording()
    {
        var source = ReadExplorerSource("ExplorerMode.Afterlife.SpiritualConflict.cs");

        foreach (var forbidden in new[]
        {
            "persisted state",
            "contested conflict",
            "exchange history",
            "итоги (totals)",
            "выигрыш (payoff)",
            "требует выигрыш (payoff)",
            "актор (actor)=",
            "канонический файл состояния",
            "side strain"
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("Когда в сцене появится проверяемое духовное противостояние", source, StringComparison.Ordinal);
        Assert.Contains("здесь показаны обмены действиями", source, StringComparison.Ordinal);
        Assert.Contains("проверяемом спорном конфликте", source, StringComparison.Ordinal);
        Assert.Contains("итоги бросков", source, StringComparison.Ordinal);
        Assert.Contains("актор=", source, StringComparison.Ordinal);
    }

    private static bool IsAllowedTechnicalLiteral(string literal)
    {
        var text = literal.Trim();
        if (text.Length == 0)
            return true;

        // Allow canonical commands, paths, JSON keys, enum-like ids, and formula variables.
        return Regex.IsMatch(text, "^[A-Za-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant) ||
               Regex.IsMatch(text, "^[A-Za-z0-9_./\\[\\]+=-]+$", RegexOptions.CultureInvariant);
    }

    private static IEnumerable<string> ExtractStringLiterals(string line)
    {
        var inString = false;
        var escaped = false;
        var literal = new List<char>();

        foreach (var ch in line)
        {
            if (!inString)
            {
                if (ch == '"')
                {
                    inString = true;
                    escaped = false;
                    literal.Clear();
                }

                continue;
            }

            if (escaped)
            {
                literal.Add(ch);
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                literal.Add(ch);
                continue;
            }

            if (ch == '"')
            {
                inString = false;
                yield return new string(literal.ToArray());
                literal.Clear();
                continue;
            }

            literal.Add(ch);
        }
    }

    private static string StripInterpolations(string literal)
    {
        var depth = 0;
        var output = new List<char>(literal.Length);

        foreach (var ch in literal)
        {
            if (ch == '{')
            {
                depth++;
                continue;
            }

            if (ch == '}' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth == 0)
                output.Add(ch);
        }

        return new string(output.ToArray());
    }

    private static string ReadExplorerSource(string fileName) =>
        File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            fileName));
}
