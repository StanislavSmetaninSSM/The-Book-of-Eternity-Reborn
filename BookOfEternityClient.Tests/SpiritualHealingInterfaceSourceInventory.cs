using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Composes the exact new spiritual adapter inverse before every older frozen owner hash.</summary>
internal static class SpiritualHealingInterfaceSourceInventory
{
    internal static string ReverseIfPresent(string source)
    {
        const string adapter = "            await using var boundSpiritualInterface = await BindSpiritualHealingInterfaceAsync(doc.RootElement, json, issues);\n            var policyRoot = boundSpiritualInterface?.Root ?? doc.RootElement;\n";
        if (!source.Contains("BindSpiritualHealingInterfaceAsync(", StringComparison.Ordinal)) return source;
        (string Current, string Previous)[] replacements =
        [
            (adapter, string.Empty),
            ("EvaluateAcceptedInterfacePrefix(policyRoot, interfacePath, issues)",
                "EvaluateAcceptedInterfacePrefix(doc.RootElement, interfacePath, issues)"),
            ("ValidateRequiredIsoTimestampField(\n                policyRoot,\n                interfacePath,",
                "ValidateRequiredIsoTimestampField(\n                doc.RootElement,\n                interfacePath,"),
            ("EvaluateAcceptedInterfaceTail(policyRoot, interfacePath, issues)",
                "EvaluateAcceptedInterfaceTail(doc.RootElement, interfacePath, issues)")
        ];
        foreach (var (current, previous) in replacements)
        {
            Assert.Equal(1, source.Split(current, StringSplitOptions.None).Length - 1);
            source = source.Replace(current, previous, StringComparison.Ordinal);
        }
        return source;
    }
}
