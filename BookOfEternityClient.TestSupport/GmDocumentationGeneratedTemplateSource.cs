using System.Text;

namespace BookOfEternityClient.Tests;

// One explicit generated reference, not a blanket Templates/* exemption.
internal static class GmDocumentationGeneratedTemplateSource
{
    internal const string RelativePath = "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md";
    internal const string Role = "compact_mortal_npc_update_template";
    internal const string Prefix = "    $templates += Write-GmContextPackTemplate -RelativePath \"Templates\\MORTAL_NPC_UPDATE_TEMPLATE.md\" -Role \"compact_mortal_npc_update_template\" -Content @'\n";
    internal sealed record Definition(string Content, string Declaration, string SourceSha256)
    {
        // Template appends one newline; Write-GmCanonicalText appends another.
        internal byte[] FileBytes => new UTF8Encoding(false, true).GetBytes(
            Content + Environment.NewLine + Environment.NewLine);
    }

    internal static Definition Parse(byte[] source, string relativePath)
    {
        if (relativePath != RelativePath) throw new InvalidDataException("generated.unknown_reference:" + relativePath);
        var text = new UTF8Encoding(false, true).GetString(source).TrimStart('\uFEFF');
        var start = text.IndexOf(Prefix, StringComparison.Ordinal);
        if (start < 0 || text.IndexOf(Prefix, start + Prefix.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidDataException("generated.declaration_missing_or_duplicate");
        var first = start + Prefix.Length;
        var last = text.IndexOf("\n'@", first, StringComparison.Ordinal);
        if (last < 0) throw new InvalidDataException("generated.declaration_end");
        var content = text[first..last];
        if (!content.StartsWith("# Compact Mortal World NPC Update Template\n", StringComparison.Ordinal) ||
            !content.Contains("game_state/npcs/npc_core.json", StringComparison.Ordinal) ||
            !content.Contains("NPCJournals", StringComparison.Ordinal))
            throw new InvalidDataException("generated.body_missing_or_wrong");
        return new(content, text[start..(last + 3)], GmDocumentationPreservationFixtures.Hash(source));
    }
}
