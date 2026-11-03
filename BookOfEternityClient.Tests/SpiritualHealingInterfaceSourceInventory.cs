using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Reverses reviewed spiritual and Elyara adapters before every older frozen owner hash.</summary>
internal static class SpiritualHealingInterfaceSourceInventory
{
    internal static string ReverseIfPresent(string source)
    {
        // df88a6bd28402ca674e1eb16a7a9f91aad782a81 added this one call.
        // Its complete source before/after is independently recoverable from Git;
        // reverse only that reviewed addition before the older spiritual adapter.
        const string elyara = "            await ValidateElyaraVisitOutcomeAsync(playerAction, issues);\n";
        if (source.Contains(elyara, StringComparison.Ordinal))
        {
            Assert.Equal("8ae18b4e23a70ebd0989ea2f18d173dcb7b4503e3130ad9502f89bbd828a9dca",
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant());
            Assert.Equal(1, source.Split(elyara, StringSplitOptions.None).Length - 1);
            source = source.Replace(elyara, string.Empty, StringComparison.Ordinal);
            Assert.Equal("408441e70c5d192b17fb16722ea2aff65aad9884e437576478ca1cbdde37edbf",
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant());
        }
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
